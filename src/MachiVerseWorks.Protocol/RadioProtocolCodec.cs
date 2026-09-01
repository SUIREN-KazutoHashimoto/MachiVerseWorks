using System.Buffers.Binary;
using System.Text;

namespace MachiVerseWorks.Protocol;

public static class RadioProtocolCodec
{
    private const int RadioStatisticsLength = 52;
    private const int RadioFixedLength = 66;
    private const int SiteLength = 50;
    private const int AntennaLength = 90;
    private const int TransmitterLength = 34;
    private const int ReceiverLength = 50;
    private const int EmissionLength = 58;
    private const int LinkLength = 82;
    private const int ServiceAreaLength = 32;
    private const int SpectrumFixedLength = 14;
    private const int BandFixedLength = 26;
    private const int FrequencyBlockLength = 32;
    private const int ConflictFixedLength = 34;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Serialize(RadioSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Radio messages require Protocol 2.16 or newer.");
        ValidateRadio(message);
        var payloadLength = checked(
            RadioFixedLength
            + message.Sites.Count * SiteLength
            + message.Antennas.Count * AntennaLength
            + message.Transmitters.Count * TransmitterLength
            + message.Receivers.Count * ReceiverLength
            + message.Emissions.Count * EmissionLength
            + message.Links.Count * LinkLength
            + message.ServiceAreas.Count * ServiceAreaLength);
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Radio snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RadioSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteRadioStatistics(payload, message.Statistics);
        WriteUInt16(payload[52..], checked((ushort)message.Sites.Count));
        WriteUInt16(payload[54..], checked((ushort)message.Antennas.Count));
        WriteUInt16(payload[56..], checked((ushort)message.Transmitters.Count));
        WriteUInt16(payload[58..], checked((ushort)message.Receivers.Count));
        WriteUInt16(payload[60..], checked((ushort)message.Emissions.Count));
        WriteUInt16(payload[62..], checked((ushort)message.Links.Count));
        WriteUInt16(payload[64..], checked((ushort)message.ServiceAreas.Count));
        var offset = RadioFixedLength;
        foreach (var item in message.Sites) { WriteSite(payload.Slice(offset, SiteLength), item); offset += SiteLength; }
        foreach (var item in message.Antennas) { WriteAntenna(payload.Slice(offset, AntennaLength), item); offset += AntennaLength; }
        foreach (var item in message.Transmitters) { WriteTransmitter(payload.Slice(offset, TransmitterLength), item); offset += TransmitterLength; }
        foreach (var item in message.Receivers) { WriteReceiver(payload.Slice(offset, ReceiverLength), item); offset += ReceiverLength; }
        foreach (var item in message.Emissions) { WriteEmission(payload.Slice(offset, EmissionLength), item); offset += EmissionLength; }
        foreach (var item in message.Links) { WriteLink(payload.Slice(offset, LinkLength), item); offset += LinkLength; }
        foreach (var item in message.ServiceAreas) { WriteServiceArea(payload.Slice(offset, ServiceAreaLength), item); offset += ServiceAreaLength; }
        return frame;
    }

    public static byte[] Serialize(SpectrumSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRadio) throw new ArgumentOutOfRangeException(nameof(version), version, "Spectrum messages require Protocol 2.16 or newer.");
        ValidateSpectrum(message);
        var payloadLength = SpectrumFixedLength;
        foreach (var band in message.Bands) payloadLength = checked(payloadLength + BandFixedLength + Utf8.GetByteCount(band.Name));
        payloadLength = checked(payloadLength + message.FrequencyBlocks.Count * FrequencyBlockLength);
        foreach (var conflict in message.Conflicts) payloadLength = checked(payloadLength + ConflictFixedLength + Utf8.GetByteCount(conflict.Reason));
        if ((uint)payloadLength > ProtocolFrameHeader.MaxPayloadLength) throw new ArgumentOutOfRangeException(nameof(message), "Spectrum snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payloadLength];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.SpectrumSnapshot, checked((uint)payloadLength)));
        var payload = frame.AsSpan(ProtocolFrameHeader.Size);
        WriteUInt64(payload, message.TickCount);
        WriteUInt16(payload[8..], checked((ushort)message.Bands.Count));
        WriteUInt16(payload[10..], checked((ushort)message.FrequencyBlocks.Count));
        WriteUInt16(payload[12..], checked((ushort)message.Conflicts.Count));
        var offset = SpectrumFixedLength;
        foreach (var band in message.Bands) offset += WriteBand(payload[offset..], band);
        foreach (var block in message.FrequencyBlocks) { WriteFrequencyBlock(payload.Slice(offset, FrequencyBlockLength), block); offset += FrequencyBlockLength; }
        foreach (var conflict in message.Conflicts) offset += WriteConflict(payload[offset..], conflict);
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (!header.Version.SupportsRadio) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        return header.MessageType switch
        {
            MessageType.RadioSnapshot => TryDeserializeRadio(header, frame[ProtocolFrameHeader.Size..], out envelope, out error),
            MessageType.SpectrumSnapshot => TryDeserializeSpectrum(header, frame[ProtocolFrameHeader.Size..], out envelope, out error),
            _ => Fail(out envelope, out error, ProtocolDecodeError.UnknownMessageType),
        };
    }

    private static bool TryDeserializeRadio(ProtocolFrameHeader header, ReadOnlySpan<byte> payload, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (payload.Length < RadioFixedLength) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        var siteCount = ReadUInt16(payload[52..]);
        var antennaCount = ReadUInt16(payload[54..]);
        var transmitterCount = ReadUInt16(payload[56..]);
        var receiverCount = ReadUInt16(payload[58..]);
        var emissionCount = ReadUInt16(payload[60..]);
        var linkCount = ReadUInt16(payload[62..]);
        var areaCount = ReadUInt16(payload[64..]);
        int expected;
        try
        {
            expected = checked(
                RadioFixedLength
                + siteCount * SiteLength
                + antennaCount * AntennaLength
                + transmitterCount * TransmitterLength
                + receiverCount * ReceiverLength
                + emissionCount * EmissionLength
                + linkCount * LinkLength
                + areaCount * ServiceAreaLength);
        }
        catch (OverflowException)
        {
            return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        }
        if (payload.Length != expected) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);

        var offset = RadioFixedLength;
        var sites = new ProtocolRadioSite[siteCount];
        for (var i = 0; i < sites.Length; i++) { sites[i] = ReadSite(payload.Slice(offset, SiteLength)); offset += SiteLength; }
        var antennas = new ProtocolRadioAntenna[antennaCount];
        for (var i = 0; i < antennas.Length; i++) { antennas[i] = ReadAntenna(payload.Slice(offset, AntennaLength)); offset += AntennaLength; }
        var transmitters = new ProtocolRadioTransmitter[transmitterCount];
        for (var i = 0; i < transmitters.Length; i++) { transmitters[i] = ReadTransmitter(payload.Slice(offset, TransmitterLength)); offset += TransmitterLength; }
        var receivers = new ProtocolRadioReceiver[receiverCount];
        for (var i = 0; i < receivers.Length; i++) { receivers[i] = ReadReceiver(payload.Slice(offset, ReceiverLength)); offset += ReceiverLength; }
        var emissions = new ProtocolRadioEmission[emissionCount];
        for (var i = 0; i < emissions.Length; i++) { emissions[i] = ReadEmission(payload.Slice(offset, EmissionLength)); offset += EmissionLength; }
        var links = new ProtocolRadioLink[linkCount];
        for (var i = 0; i < links.Length; i++) { links[i] = ReadLink(payload.Slice(offset, LinkLength)); offset += LinkLength; }
        var areas = new ProtocolRadioServiceArea[areaCount];
        for (var i = 0; i < areas.Length; i++) { areas[i] = ReadServiceArea(payload.Slice(offset, ServiceAreaLength)); offset += ServiceAreaLength; }
        var message = new RadioSnapshotMessage(
            ReadRadioStatistics(payload),
            Array.AsReadOnly(sites),
            Array.AsReadOnly(antennas),
            Array.AsReadOnly(transmitters),
            Array.AsReadOnly(receivers),
            Array.AsReadOnly(emissions),
            Array.AsReadOnly(links),
            Array.AsReadOnly(areas));
        if (!IsValidRadio(message)) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        envelope = new ProtocolEnvelope(header.Version, message);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static bool TryDeserializeSpectrum(ProtocolFrameHeader header, ReadOnlySpan<byte> payload, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (payload.Length < SpectrumFixedLength) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        var bandCount = ReadUInt16(payload[8..]);
        var blockCount = ReadUInt16(payload[10..]);
        var conflictCount = ReadUInt16(payload[12..]);
        var offset = SpectrumFixedLength;
        var bands = new ProtocolSpectrumBand[bandCount];
        for (var i = 0; i < bands.Length; i++) if (!TryReadBand(payload, ref offset, out bands[i])) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        var blocks = new ProtocolFrequencyBlock[blockCount];
        for (var i = 0; i < blocks.Length; i++)
        {
            if (offset > payload.Length - FrequencyBlockLength) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
            blocks[i] = ReadFrequencyBlock(payload.Slice(offset, FrequencyBlockLength));
            offset += FrequencyBlockLength;
        }
        var conflicts = new ProtocolSpectrumConflict[conflictCount];
        for (var i = 0; i < conflicts.Length; i++) if (!TryReadConflict(payload, ref offset, out conflicts[i])) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        if (offset != payload.Length) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        var message = new SpectrumSnapshotMessage(ReadUInt64(payload), Array.AsReadOnly(bands), Array.AsReadOnly(blocks), Array.AsReadOnly(conflicts));
        if (!IsValidSpectrum(message)) return Fail(out envelope, out error, ProtocolDecodeError.InvalidPayload);
        envelope = new ProtocolEnvelope(header.Version, message);
        error = ProtocolDecodeError.None;
        return true;
    }

    private static void ValidateRadio(RadioSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Sites);
        ArgumentNullException.ThrowIfNull(message.Antennas);
        ArgumentNullException.ThrowIfNull(message.Transmitters);
        ArgumentNullException.ThrowIfNull(message.Receivers);
        ArgumentNullException.ThrowIfNull(message.Emissions);
        ArgumentNullException.ThrowIfNull(message.Links);
        ArgumentNullException.ThrowIfNull(message.ServiceAreas);
        if (message.Sites.Count > ushort.MaxValue
            || message.Antennas.Count > ushort.MaxValue
            || message.Transmitters.Count > ushort.MaxValue
            || message.Receivers.Count > ushort.MaxValue
            || message.Emissions.Count > ushort.MaxValue
            || message.Links.Count > ushort.MaxValue
            || message.ServiceAreas.Count > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(message));
        if (!IsValidRadio(message)) throw new ArgumentOutOfRangeException(nameof(message), "Radio snapshot contains invalid values.");
    }

    private static void ValidateSpectrum(SpectrumSnapshotMessage message)
    {
        ArgumentNullException.ThrowIfNull(message.Bands);
        ArgumentNullException.ThrowIfNull(message.FrequencyBlocks);
        ArgumentNullException.ThrowIfNull(message.Conflicts);
        if (message.Bands.Count > ushort.MaxValue || message.FrequencyBlocks.Count > ushort.MaxValue || message.Conflicts.Count > ushort.MaxValue) throw new ArgumentOutOfRangeException(nameof(message));
        if (!IsValidSpectrum(message)) throw new ArgumentOutOfRangeException(nameof(message), "Spectrum snapshot contains invalid values.");
    }

    private static bool IsValidRadio(RadioSnapshotMessage message)
    {
        if (!NonNegative(message.Statistics.PeakSpectrumUtilization) || message.Statistics.PeakSpectrumUtilization > 1d + 1e-9) return false;
        var siteIds = new HashSet<ulong>();
        foreach (var item in message.Sites)
        {
            if (item.SiteId == 0 || !siteIds.Add(item.SiteId) || !Enum.IsDefined(item.Kind) || !Finite(item.X) || !Finite(item.Y) || !Finite(item.Z) || !Finite(item.AntennaGainDb) || !NonNegative(item.AntennaHeightMeters)) return false;
        }
        var antennaIds = new HashSet<ulong>();
        foreach (var item in message.Antennas)
        {
            if (item.AntennaId == 0 || !antennaIds.Add(item.AntennaId) || !siteIds.Contains(item.SiteId) || !Finite(item.OffsetX) || !Finite(item.OffsetY) || !Finite(item.OffsetZ) || !Finite(item.OrientationX) || !Finite(item.OrientationY) || !Finite(item.OrientationZ) || !Finite(item.GainDb) || !Enum.IsDefined(item.PatternKind) || !Positive(item.BeamwidthDegrees) || item.BeamwidthDegrees > 360d || !NonNegative(item.FrontToBackRatioDb)) return false;
        }
        var transmitterIds = new HashSet<ulong>();
        foreach (var item in message.Transmitters)
        {
            if (item.TransmitterId == 0 || !transmitterIds.Add(item.TransmitterId) || !siteIds.Contains(item.SiteId) || !antennaIds.Contains(item.AntennaId) || !Finite(item.MaximumTransmitPowerDbm)) return false;
        }
        foreach (var item in message.Receivers)
        {
            if (item.ReceiverId == 0 || !siteIds.Contains(item.SiteId) || !antennaIds.Contains(item.AntennaId) || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz || !Finite(item.SensitivityDbm) || item.SensitivityDbm >= 0d) return false;
        }
        var emissionIds = new HashSet<ulong>();
        foreach (var item in message.Emissions)
        {
            if (item.EmissionId == 0 || !emissionIds.Add(item.EmissionId) || !transmitterIds.Contains(item.TransmitterId) || item.ChannelId == 0 || !Positive(item.CenterFrequencyMegahertz) || !Positive(item.BandwidthMegahertz) || !Finite(item.TransmitPowerDbm) || !NonNegative(item.Utilization) || item.Utilization > 1d + 1e-9) return false;
        }
        foreach (var item in message.Links)
        {
            if (item.LinkId == 0 || item.FromSiteId == 0 || item.ToSiteId == 0 || item.FromSiteId == item.ToSiteId || !siteIds.Contains(item.FromSiteId) || !siteIds.Contains(item.ToSiteId) || item.FrequencyBlockId == 0 || !NonNegative(item.DistanceMeters) || !Finite(item.PathLossDb) || !Finite(item.ReceivedPowerDbm) || !Finite(item.InterferenceDbm) || !Finite(item.SinrDb) || !NonNegative(item.Utilization) || item.Utilization > 1d + 1e-9 || !Enum.IsDefined(item.State)) return false;
        }
        foreach (var item in message.ServiceAreas) if (!siteIds.Contains(item.SiteId) || item.FrequencyBlockId == 0 || !NonNegative(item.RadiusMeters) || !Finite(item.MinimumSinrDb)) return false;
        return true;
    }

    private static bool IsValidSpectrum(SpectrumSnapshotMessage message)
    {
        foreach (var item in message.Bands) if (item.BandId == 0 || string.IsNullOrWhiteSpace(item.Name) || Utf8.GetByteCount(item.Name) > ushort.MaxValue || !Positive(item.MinimumFrequencyMegahertz) || !Positive(item.MaximumFrequencyMegahertz) || item.MaximumFrequencyMegahertz <= item.MinimumFrequencyMegahertz) return false;
        foreach (var item in message.FrequencyBlocks) if (item.FrequencyBlockId == 0 || item.BandId == 0 || !Positive(item.CenterFrequencyMegahertz) || !Positive(item.BandwidthMegahertz)) return false;
        foreach (var item in message.Conflicts) if (item.FirstBlockId == 0 || item.SecondBlockId == 0 || item.FirstSiteId == 0 || item.SecondSiteId == 0 || string.IsNullOrWhiteSpace(item.Reason) || Utf8.GetByteCount(item.Reason) > ushort.MaxValue) return false;
        return true;
    }

    private static void WriteRadioStatistics(Span<byte> p, ProtocolRadioStatistics v)
    {
        WriteUInt32(p, v.SiteCount); WriteUInt32(p[4..], v.BandCount); WriteUInt32(p[8..], v.FrequencyBlockCount); WriteUInt32(p[12..], v.LinkCount);
        WriteUInt32(p[16..], v.ServiceAreaCount); WriteUInt32(p[20..], v.ConflictCount); WriteUInt32(p[24..], v.HealthyLinkCount); WriteUInt32(p[28..], v.InterferedLinkCount);
        WriteUInt32(p[32..], v.UnreachableLinkCount); WriteDouble(p[36..], v.PeakSpectrumUtilization); WriteUInt64(p[44..], v.TickCount);
    }

    private static ProtocolRadioStatistics ReadRadioStatistics(ReadOnlySpan<byte> p) => new(
        ReadUInt32(p), ReadUInt32(p[4..]), ReadUInt32(p[8..]), ReadUInt32(p[12..]), ReadUInt32(p[16..]), ReadUInt32(p[20..]),
        ReadUInt32(p[24..]), ReadUInt32(p[28..]), ReadUInt32(p[32..]), ReadDouble(p[36..]), ReadUInt64(p[44..]));

    private static void WriteSite(Span<byte> p, ProtocolRadioSite v) { WriteUInt64(p, v.SiteId); p[8] = (byte)v.Kind; WriteDouble(p[9..], v.X); WriteDouble(p[17..], v.Y); WriteDouble(p[25..], v.Z); WriteDouble(p[33..], v.AntennaGainDb); WriteDouble(p[41..], v.AntennaHeightMeters); p[49] = Bool(v.IsInService); }
    private static ProtocolRadioSite ReadSite(ReadOnlySpan<byte> p) => new(ReadUInt64(p), (ProtocolRadioSiteKind)p[8], ReadDouble(p[9..]), ReadDouble(p[17..]), ReadDouble(p[25..]), ReadDouble(p[33..]), ReadDouble(p[41..]), p[49] != 0);
    private static void WriteAntenna(Span<byte> p, ProtocolRadioAntenna v) { WriteUInt64(p, v.AntennaId); WriteUInt64(p[8..], v.SiteId); WriteDouble(p[16..], v.OffsetX); WriteDouble(p[24..], v.OffsetY); WriteDouble(p[32..], v.OffsetZ); WriteDouble(p[40..], v.OrientationX); WriteDouble(p[48..], v.OrientationY); WriteDouble(p[56..], v.OrientationZ); WriteDouble(p[64..], v.GainDb); p[72] = (byte)v.PatternKind; WriteDouble(p[73..], v.BeamwidthDegrees); WriteDouble(p[81..], v.FrontToBackRatioDb); p[89] = Bool(v.IsInService); }
    private static ProtocolRadioAntenna ReadAntenna(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadDouble(p[16..]), ReadDouble(p[24..]), ReadDouble(p[32..]), ReadDouble(p[40..]), ReadDouble(p[48..]), ReadDouble(p[56..]), ReadDouble(p[64..]), (ProtocolRadioAntennaPatternKind)p[72], ReadDouble(p[73..]), ReadDouble(p[81..]), p[89] != 0);
    private static void WriteTransmitter(Span<byte> p, ProtocolRadioTransmitter v) { WriteUInt64(p, v.TransmitterId); WriteUInt64(p[8..], v.SiteId); WriteUInt64(p[16..], v.AntennaId); WriteDouble(p[24..], v.MaximumTransmitPowerDbm); p[32] = Bool(v.IsInService); p[33] = Bool(v.IsOperational); }
    private static ProtocolRadioTransmitter ReadTransmitter(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadDouble(p[24..]), p[32] != 0, p[33] != 0);
    private static void WriteReceiver(Span<byte> p, ProtocolRadioReceiver v) { WriteUInt64(p, v.ReceiverId); WriteUInt64(p[8..], v.SiteId); WriteUInt64(p[16..], v.AntennaId); WriteDouble(p[24..], v.MinimumFrequencyMegahertz); WriteDouble(p[32..], v.MaximumFrequencyMegahertz); WriteDouble(p[40..], v.SensitivityDbm); p[48] = Bool(v.IsInService); p[49] = Bool(v.IsOperational); }
    private static ProtocolRadioReceiver ReadReceiver(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadDouble(p[24..]), ReadDouble(p[32..]), ReadDouble(p[40..]), p[48] != 0, p[49] != 0);
    private static void WriteEmission(Span<byte> p, ProtocolRadioEmission v) { WriteUInt64(p, v.EmissionId); WriteUInt64(p[8..], v.TransmitterId); WriteUInt64(p[16..], v.ChannelId); WriteDouble(p[24..], v.CenterFrequencyMegahertz); WriteDouble(p[32..], v.BandwidthMegahertz); WriteDouble(p[40..], v.TransmitPowerDbm); WriteDouble(p[48..], v.Utilization); p[56] = Bool(v.IsInService); p[57] = Bool(v.IsOperational); }
    private static ProtocolRadioEmission ReadEmission(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadDouble(p[24..]), ReadDouble(p[32..]), ReadDouble(p[40..]), ReadDouble(p[48..]), p[56] != 0, p[57] != 0);
    private static void WriteLink(Span<byte> p, ProtocolRadioLink v) { WriteUInt64(p, v.LinkId); WriteUInt64(p[8..], v.FromSiteId); WriteUInt64(p[16..], v.ToSiteId); WriteUInt64(p[24..], v.FrequencyBlockId); WriteDouble(p[32..], v.DistanceMeters); WriteDouble(p[40..], v.PathLossDb); WriteDouble(p[48..], v.ReceivedPowerDbm); WriteDouble(p[56..], v.InterferenceDbm); WriteDouble(p[64..], v.SinrDb); WriteDouble(p[72..], v.Utilization); p[80] = (byte)v.State; p[81] = Bool(v.IsInService); }
    private static ProtocolRadioLink ReadLink(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadUInt64(p[16..]), ReadUInt64(p[24..]), ReadDouble(p[32..]), ReadDouble(p[40..]), ReadDouble(p[48..]), ReadDouble(p[56..]), ReadDouble(p[64..]), ReadDouble(p[72..]), (ProtocolRadioLinkState)p[80], p[81] != 0);
    private static void WriteServiceArea(Span<byte> p, ProtocolRadioServiceArea v) { WriteUInt64(p, v.SiteId); WriteUInt64(p[8..], v.FrequencyBlockId); WriteDouble(p[16..], v.RadiusMeters); WriteDouble(p[24..], v.MinimumSinrDb); }
    private static ProtocolRadioServiceArea ReadServiceArea(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadDouble(p[16..]), ReadDouble(p[24..]));

    private static int WriteBand(Span<byte> p, ProtocolSpectrumBand v) { var bytes = Utf8.GetBytes(v.Name); WriteUInt64(p, v.BandId); WriteDouble(p[8..], v.MinimumFrequencyMegahertz); WriteDouble(p[16..], v.MaximumFrequencyMegahertz); WriteUInt16(p[24..], checked((ushort)bytes.Length)); bytes.CopyTo(p[26..]); return 26 + bytes.Length; }
    private static bool TryReadBand(ReadOnlySpan<byte> p, ref int offset, out ProtocolSpectrumBand value) { value = default; if (offset > p.Length - BandFixedLength) return false; var id = ReadUInt64(p[offset..]); var min = ReadDouble(p[(offset + 8)..]); var max = ReadDouble(p[(offset + 16)..]); var length = ReadUInt16(p[(offset + 24)..]); if (offset > p.Length - BandFixedLength - length) return false; string name; try { name = Utf8.GetString(p.Slice(offset + 26, length)); } catch (DecoderFallbackException) { return false; } value = new ProtocolSpectrumBand(id, name, min, max); offset += 26 + length; return true; }
    private static void WriteFrequencyBlock(Span<byte> p, ProtocolFrequencyBlock v) { WriteUInt64(p, v.FrequencyBlockId); WriteUInt64(p[8..], v.BandId); WriteDouble(p[16..], v.CenterFrequencyMegahertz); WriteDouble(p[24..], v.BandwidthMegahertz); }
    private static ProtocolFrequencyBlock ReadFrequencyBlock(ReadOnlySpan<byte> p) => new(ReadUInt64(p), ReadUInt64(p[8..]), ReadDouble(p[16..]), ReadDouble(p[24..]));
    private static int WriteConflict(Span<byte> p, ProtocolSpectrumConflict v) { var bytes = Utf8.GetBytes(v.Reason); WriteUInt64(p, v.FirstBlockId); WriteUInt64(p[8..], v.SecondBlockId); WriteUInt64(p[16..], v.FirstSiteId); WriteUInt64(p[24..], v.SecondSiteId); WriteUInt16(p[32..], checked((ushort)bytes.Length)); bytes.CopyTo(p[34..]); return 34 + bytes.Length; }
    private static bool TryReadConflict(ReadOnlySpan<byte> p, ref int offset, out ProtocolSpectrumConflict value) { value = default; if (offset > p.Length - ConflictFixedLength) return false; var firstBlock = ReadUInt64(p[offset..]); var secondBlock = ReadUInt64(p[(offset + 8)..]); var firstSite = ReadUInt64(p[(offset + 16)..]); var secondSite = ReadUInt64(p[(offset + 24)..]); var length = ReadUInt16(p[(offset + 32)..]); if (offset > p.Length - ConflictFixedLength - length) return false; string reason; try { reason = Utf8.GetString(p.Slice(offset + 34, length)); } catch (DecoderFallbackException) { return false; } value = new ProtocolSpectrumConflict(firstBlock, secondBlock, firstSite, secondSite, reason); offset += 34 + length; return true; }

    private static bool Fail(out ProtocolEnvelope? envelope, out ProtocolDecodeError error, ProtocolDecodeError value) { envelope = null; error = value; return false; }
    private static byte Bool(bool value) => value ? (byte)1 : (byte)0;
    private static bool Finite(double value) => double.IsFinite(value);
    private static bool Positive(double value) => double.IsFinite(value) && value > 0d;
    private static bool NonNegative(double value) => double.IsFinite(value) && value >= 0d;
    private static void WriteUInt16(Span<byte> p, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(p, value);
    private static ushort ReadUInt16(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt16LittleEndian(p);
    private static void WriteUInt32(Span<byte> p, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(p, value);
    private static uint ReadUInt32(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt32LittleEndian(p);
    private static void WriteUInt64(Span<byte> p, ulong value) => BinaryPrimitives.WriteUInt64LittleEndian(p, value);
    private static ulong ReadUInt64(ReadOnlySpan<byte> p) => BinaryPrimitives.ReadUInt64LittleEndian(p);
    private static void WriteDouble(Span<byte> p, double value) => WriteUInt64(p, unchecked((ulong)BitConverter.DoubleToInt64Bits(value)));
    private static double ReadDouble(ReadOnlySpan<byte> p) => BitConverter.Int64BitsToDouble(unchecked((long)ReadUInt64(p)));
}
