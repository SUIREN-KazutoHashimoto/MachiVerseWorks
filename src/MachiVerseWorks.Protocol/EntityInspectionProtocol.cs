using System.Buffers.Binary;
using System.Text.Json;

namespace MachiVerseWorks.Protocol;

public enum ProtocolEntityType : byte
{
    Person = 1,
    Train = 2,
    Settlement = 3,
    Parcel = 4,
    Building = 5,
}

public sealed record InspectEntityMessage(ProtocolEntityType EntityType, ulong EntityId) : IObservationRequestMessage
{
    public MessageType Type => MessageType.InspectEntity;
}

public sealed record ClearEntityInspectionMessage : IObservationRequestMessage
{
    public MessageType Type => MessageType.ClearEntityInspection;
}

public sealed record ProtocolInspectionField(string Name, string Value);

public sealed record ProtocolInspectionRelation(
    string Kind,
    ProtocolEntityType TargetType,
    ulong TargetId,
    double Strength);

public sealed record ProtocolInspectionEvent(
    ulong EventId,
    int? Year,
    string Kind,
    string Summary);

public sealed record EntityInspectionSnapshotMessage(
    ProtocolEntityType EntityType,
    ulong EntityId,
    ulong TickCount,
    int? CurrentYear,
    bool Found,
    IReadOnlyList<ProtocolInspectionField> CurrentState,
    IReadOnlyList<ProtocolInspectionRelation> Relations,
    IReadOnlyList<ProtocolInspectionEvent> RecentPast,
    bool PlannedFutureAvailable,
    IReadOnlyList<ProtocolInspectionEvent> PlannedFuture) : IProtocolMessage
{
    public MessageType Type => MessageType.EntityInspectionSnapshot;
}

public static class EntityInspectionProtocolCodec
{
    public const int MaximumCurrentStateFields = 64;
    public const int MaximumRelations = 32;
    public const int MaximumRecentEvents = 32;
    public const int MaximumPlannedEvents = 16;

    private const int InspectPayloadLength = 9;
    private const int MaximumNameLength = 96;
    private const int MaximumValueLength = 256;
    private const int MaximumSummaryLength = 512;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = 16,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static byte[] Serialize(IProtocolMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsPersistentRegionalEvolution)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Entity inspection requires Protocol 2.19 or newer.");

        return message switch
        {
            InspectEntityMessage inspect => SerializeInspect(inspect, version),
            ClearEntityInspectionMessage clear => SerializeClear(clear, version),
            EntityInspectionSnapshotMessage snapshot => SerializeSnapshot(snapshot, version),
            _ => throw new ArgumentException($"Unsupported entity inspection message: {message.GetType().FullName}.", nameof(message)),
        };
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsPersistentRegionalEvolution)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }

        switch (header.MessageType)
        {
            case MessageType.InspectEntity:
                if (header.PayloadLength != InspectPayloadLength)
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                var payload = frame[ProtocolFrameHeader.Size..];
                var entityType = (ProtocolEntityType)payload[0];
                var entityId = BinaryPrimitives.ReadUInt64LittleEndian(payload[1..]);
                if (!Enum.IsDefined(entityType) || entityId == 0)
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                envelope = new ProtocolEnvelope(header.Version, new InspectEntityMessage(entityType, entityId));
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.ClearEntityInspection:
                if (header.PayloadLength != 0)
                {
                    error = ProtocolDecodeError.InvalidPayload;
                    return false;
                }
                envelope = new ProtocolEnvelope(header.Version, new ClearEntityInspectionMessage());
                error = ProtocolDecodeError.None;
                return true;
            case MessageType.EntityInspectionSnapshot:
                return TryDeserializeSnapshot(frame, header, out envelope, out error);
            default:
                error = ProtocolDecodeError.UnknownMessageType;
                return false;
        }
    }

    private static byte[] SerializeInspect(InspectEntityMessage message, ProtocolVersion version)
    {
        if (!Enum.IsDefined(message.EntityType)) throw new ArgumentOutOfRangeException(nameof(message), "Entity type is invalid.");
        if (message.EntityId == 0) throw new ArgumentOutOfRangeException(nameof(message), "Entity ID must be non-zero.");
        var frame = new byte[ProtocolFrameHeader.Size + InspectPayloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.InspectEntity, InspectPayloadLength));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        payload[0] = (byte)message.EntityType;
        BinaryPrimitives.WriteUInt64LittleEndian(payload[1..], message.EntityId);
        return frame;
    }

    private static byte[] SerializeClear(ClearEntityInspectionMessage message, ProtocolVersion version)
    {
        _ = message;
        var frame = new byte[ProtocolFrameHeader.Size];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.ClearEntityInspection, 0));
        return frame;
    }

    private static byte[] SerializeSnapshot(EntityInspectionSnapshotMessage message, ProtocolVersion version)
    {
        if (!IsValid(message)) throw new ArgumentOutOfRangeException(nameof(message), "Entity inspection snapshot contains invalid values.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if ((uint)payload.Length > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "Entity inspection snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payload.Length];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.EntityInspectionSnapshot, checked((uint)payload.Length)));
        payload.CopyTo(frame.AsSpan(ProtocolFrameHeader.Size));
        return frame;
    }

    private static bool TryDeserializeSnapshot(
        ReadOnlySpan<byte> frame,
        ProtocolFrameHeader header,
        out ProtocolEnvelope? envelope,
        out ProtocolDecodeError error)
    {
        try
        {
            var message = JsonSerializer.Deserialize<EntityInspectionSnapshotMessage>(frame[ProtocolFrameHeader.Size..], SerializerOptions);
            if (message is null || !IsValid(message))
            {
                envelope = null;
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            envelope = new ProtocolEnvelope(header.Version, message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            envelope = null;
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static bool IsValid(EntityInspectionSnapshotMessage message)
    {
        if (!Enum.IsDefined(message.EntityType) || message.EntityId == 0 || message.CurrentYear < 0
            || message.CurrentState is null || message.Relations is null || message.RecentPast is null || message.PlannedFuture is null)
            return false;
        if (message.CurrentState.Count > MaximumCurrentStateFields || message.Relations.Count > MaximumRelations
            || message.RecentPast.Count > MaximumRecentEvents || message.PlannedFuture.Count > MaximumPlannedEvents)
            return false;
        if (!message.PlannedFutureAvailable && message.PlannedFuture.Count != 0) return false;
        if (!message.Found && (message.CurrentState.Count != 0 || message.Relations.Count != 0 || message.RecentPast.Count != 0 || message.PlannedFuture.Count != 0))
            return false;

        foreach (var field in message.CurrentState)
            if (field is null || !ValidText(field.Name, MaximumNameLength) || field.Value is null || field.Value.Length > MaximumValueLength) return false;
        foreach (var relation in message.Relations)
            if (relation is null || !ValidText(relation.Kind, MaximumNameLength) || !Enum.IsDefined(relation.TargetType)
                || relation.TargetId == 0 || !double.IsFinite(relation.Strength) || relation.Strength is < 0d or > 1d) return false;
        foreach (var item in message.RecentPast)
            if (!ValidEvent(item, requireId: true)) return false;
        foreach (var item in message.PlannedFuture)
            if (!ValidEvent(item, requireId: false)) return false;
        return true;
    }

    private static bool ValidEvent(ProtocolInspectionEvent? item, bool requireId)
    {
        return item is not null
            && (!requireId || item.EventId != 0)
            && item.Year is not < 0
            && ValidText(item.Kind, MaximumNameLength)
            && ValidText(item.Summary, MaximumSummaryLength);
    }

    private static bool ValidText(string? value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
}
