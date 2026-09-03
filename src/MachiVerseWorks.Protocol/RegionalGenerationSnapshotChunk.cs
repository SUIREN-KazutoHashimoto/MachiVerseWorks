using System.Buffers.Binary;
using System.Text.Json;

namespace MachiVerseWorks.Protocol;

public sealed record RegionalGenerationSnapshotChunkMessage(
    ulong SnapshotId,
    int ChunkIndex,
    int ChunkCount,
    int TotalPayloadBytes,
    byte[] Data) : IProtocolMessage
{
    public MessageType Type => MessageType.RegionalGenerationSnapshotChunk;
}

public static class RegionalGenerationSnapshotChunker
{
    private const int MetadataLength = 20;
    private const int MaximumAggregatePayloadBytes = 64 * 1024 * 1024;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = 16,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static IReadOnlyList<RegionalGenerationSnapshotChunkMessage> Split(
        RegionalGenerationSnapshotMessage message,
        ulong snapshotId)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (snapshotId == 0) throw new ArgumentOutOfRangeException(nameof(snapshotId));
        RegionalGenerationProtocolCodec.Validate(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if (payload.Length > MaximumAggregatePayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation aggregate exceeds the chunked transport limit.");

        var maximumChunkBytes = checked((int)ProtocolFrameHeader.MaxPayloadLength - MetadataLength);
        var chunkCount = Math.Max(1, checked((payload.Length + maximumChunkBytes - 1) / maximumChunkBytes));
        var chunks = new RegionalGenerationSnapshotChunkMessage[chunkCount];
        for (var index = 0; index < chunks.Length; index++)
        {
            var offset = checked(index * maximumChunkBytes);
            var length = Math.Min(maximumChunkBytes, payload.Length - offset);
            var data = payload.AsSpan(offset, length).ToArray();
            chunks[index] = new RegionalGenerationSnapshotChunkMessage(snapshotId, index, chunkCount, payload.Length, data);
        }
        return chunks;
    }

    public static RegionalGenerationSnapshotMessage Assemble(IReadOnlyList<RegionalGenerationSnapshotChunkMessage> chunks)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        if (chunks.Count == 0) throw new ArgumentException("At least one chunk is required.", nameof(chunks));
        var first = chunks[0] ?? throw new ArgumentException("Chunks cannot contain null entries.", nameof(chunks));
        ValidateMetadata(first);
        if (chunks.Count != first.ChunkCount)
            throw new ArgumentException("Regional generation chunk set is incomplete.", nameof(chunks));

        var ordered = new RegionalGenerationSnapshotChunkMessage[first.ChunkCount];
        var totalBytes = 0;
        foreach (var chunk in chunks)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            ValidateMetadata(chunk);
            if (chunk.SnapshotId != first.SnapshotId || chunk.ChunkCount != first.ChunkCount || chunk.TotalPayloadBytes != first.TotalPayloadBytes)
                throw new ArgumentException("Regional generation chunks belong to different snapshots.", nameof(chunks));
            if (ordered[chunk.ChunkIndex] is not null)
                throw new ArgumentException("Regional generation chunk index is duplicated.", nameof(chunks));
            ordered[chunk.ChunkIndex] = chunk;
            totalBytes = checked(totalBytes + chunk.Data.Length);
        }
        if (totalBytes != first.TotalPayloadBytes)
            throw new ArgumentException("Regional generation chunk byte count is inconsistent.", nameof(chunks));

        var payload = new byte[first.TotalPayloadBytes];
        var offset = 0;
        foreach (var chunk in ordered)
        {
            if (chunk is null) throw new ArgumentException("Regional generation chunk set is incomplete.", nameof(chunks));
            chunk.Data.CopyTo(payload, offset);
            offset += chunk.Data.Length;
        }

        var message = JsonSerializer.Deserialize<RegionalGenerationSnapshotMessage>(payload, SerializerOptions)
            ?? throw new InvalidDataException("Regional generation aggregate payload is empty.");
        RegionalGenerationProtocolCodec.Validate(message);
        return message;
    }

    internal static void ValidateMetadata(RegionalGenerationSnapshotChunkMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(message.Data);
        if (message.SnapshotId == 0 || message.ChunkCount <= 0 || message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount)
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation chunk metadata is invalid.");
        if (message.TotalPayloadBytes <= 0 || message.TotalPayloadBytes > MaximumAggregatePayloadBytes || message.Data.Length > message.TotalPayloadBytes)
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation aggregate payload length is invalid.");
        if (message.Data.Length > checked((int)ProtocolFrameHeader.MaxPayloadLength - MetadataLength))
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation chunk exceeds the protocol payload limit.");
    }
}

public static class RegionalGenerationSnapshotChunkProtocolCodec
{
    private const int MetadataLength = 20;

    public static byte[] Serialize(RegionalGenerationSnapshotChunkMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRegionalGenerationChunking)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Regional generation chunk messages require Protocol 2.22 or newer.");
        RegionalGenerationSnapshotChunker.ValidateMetadata(message);
        var payloadLength = checked(MetadataLength + message.Data.Length);
        var frame = new byte[ProtocolFrameHeader.GetFrameLength(payloadLength)];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RegionalGenerationSnapshotChunk, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        BinaryPrimitives.WriteUInt64LittleEndian(payload, message.SnapshotId);
        BinaryPrimitives.WriteInt32LittleEndian(payload[8..], message.ChunkIndex);
        BinaryPrimitives.WriteInt32LittleEndian(payload[12..], message.ChunkCount);
        BinaryPrimitives.WriteInt32LittleEndian(payload[16..], message.TotalPayloadBytes);
        message.Data.CopyTo(payload[MetadataLength..]);
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out RegionalGenerationSnapshotChunkMessage message, out ProtocolDecodeError error)
    {
        message = null!;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.RegionalGenerationSnapshotChunk)
        {
            error = ProtocolDecodeError.UnknownMessageType;
            return false;
        }
        if (!header.Version.SupportsRegionalGenerationChunking || header.PayloadLength < MetadataLength)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        try
        {
            var payload = frame[ProtocolFrameHeader.Size..];
            message = new RegionalGenerationSnapshotChunkMessage(
                BinaryPrimitives.ReadUInt64LittleEndian(payload),
                BinaryPrimitives.ReadInt32LittleEndian(payload[8..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[12..]),
                BinaryPrimitives.ReadInt32LittleEndian(payload[16..]),
                payload[MetadataLength..].ToArray());
            RegionalGenerationSnapshotChunker.ValidateMetadata(message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or OverflowException)
        {
            message = null!;
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }
}
