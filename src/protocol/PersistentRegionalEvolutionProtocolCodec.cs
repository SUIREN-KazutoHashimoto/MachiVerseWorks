using System.Text.Json;

namespace MachiVerseWorks.Protocol;

public static class PersistentRegionalEvolutionProtocolCodec
{
    private const int MaximumSettlements = 256;
    private const int MaximumParcels = 16_384;
    private const int MaximumBuildings = 16_384;
    private const int MaximumDerivedItems = 65_536;
    private const int MaximumEvents = 262_144;
    private const int MaximumReasonLength = 256;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = 16,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static byte[] Serialize(PersistentRegionalEvolutionSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsPersistentRegionalEvolution)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Persistent regional evolution messages require Protocol 2.19 or newer.");
        if (!IsValid(message))
            throw new ArgumentOutOfRangeException(nameof(message), "Persistent regional evolution snapshot contains invalid values.");
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if ((uint)payload.Length > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "Persistent regional evolution snapshot chunk exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payload.Length];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.PersistentRegionalEvolutionSnapshot, checked((uint)payload.Length)));
        payload.CopyTo(frame.AsSpan(ProtocolFrameHeader.Size));
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.PersistentRegionalEvolutionSnapshot || !header.Version.SupportsPersistentRegionalEvolution)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        try
        {
            var message = JsonSerializer.Deserialize<PersistentRegionalEvolutionSnapshotMessage>(frame[ProtocolFrameHeader.Size..], SerializerOptions);
            if (message is null || !IsValid(message))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            envelope = new ProtocolEnvelope(header.Version, message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static bool IsValid(PersistentRegionalEvolutionSnapshotMessage message)
    {
        if (message.ChunkCount <= 0 || message.ChunkIndex < 0 || message.ChunkIndex >= message.ChunkCount) return false;
        var hasBatchMetadata = message.SnapshotId != 0 || message.ChunkIndex != 0 || message.ChunkCount != 1;
        if (hasBatchMetadata)
        {
            if (message.SnapshotId == 0) return false;
            if ((message.ChunkIndex == 0) != message.IsFullSnapshot) return false;
        }
        if (message.CurrentYear < 0 || message.Settlements is null || message.Parcels is null || message.Buildings is null
            || message.ServiceCatchments is null || message.InfrastructureDemands is null || message.Relations is null
            || message.Events is null || message.CommutingFlows is null || message.FreightFlows is null) return false;
        if (message.Settlements.Count > MaximumSettlements || message.Parcels.Count > MaximumParcels || message.Buildings.Count > MaximumBuildings
            || message.ServiceCatchments.Count > MaximumDerivedItems || message.InfrastructureDemands.Count > MaximumDerivedItems
            || message.Relations.Count > MaximumDerivedItems || message.Events.Count > MaximumEvents
            || message.CommutingFlows.Count > MaximumDerivedItems || message.FreightFlows.Count > MaximumDerivedItems) return false;

        var settlements = new HashSet<ulong>();
        foreach (var item in message.Settlements)
        {
            if (item is null || item.SettlementId == 0 || !settlements.Add(item.SettlementId) || !Finite(item.X) || !Finite(item.Y) || !Finite(item.Z)
                || item.Population < 0 || item.Jobs < 0 || !Unit(item.ServiceIndex) || !Unit(item.Density) || !Unit(item.Accessibility)
                || !Positive(item.InfluenceRadiusMeters) || item.Scale > 4 || item.Trend > 4 || item.EstablishedYear > message.CurrentYear
                || item.DormantSinceYear > message.CurrentYear) return false;
        }

        var parcels = new HashSet<ulong>();
        foreach (var item in message.Parcels)
            if (item is null || item.ParcelId == 0 || item.SettlementId == 0 || !parcels.Add(item.ParcelId)
                || !Unit(item.DevelopmentDemand) || !Unit(item.LandValue) || item.DevelopmentState > 3) return false;

        var buildings = new HashSet<ulong>();
        foreach (var item in message.Buildings)
            if (item is null || item.BuildingId == 0 || item.ParcelId == 0 || !buildings.Add(item.BuildingId)
                || item.Use > 6 || item.BuiltYear > message.CurrentYear || item.LastChangedYear > message.CurrentYear
                || !Unit(item.Condition) || !Unit(item.Occupancy) || item.Capacity < 0 || item.Status > 5) return false;

        foreach (var item in message.ServiceCatchments)
            if (item is null || item.SettlementId == 0 || item.Kind > 2 || !Positive(item.RadiusMeters) || !Unit(item.Coverage)) return false;
        foreach (var item in message.InfrastructureDemands)
            if (item is null || item.SettlementId == 0 || item.Kind > 2 || !Unit(item.Demand) || !ValidReason(item.Reason)) return false;

        var relationIds = new HashSet<ulong>();
        foreach (var item in message.Relations)
            if (item is null || item.RelationId == 0 || !relationIds.Add(item.RelationId) || item.FromSettlementId == 0
                || item.ToSettlementId == 0 || item.FromSettlementId == item.ToSettlementId || item.Kind > 3
                || !Unit(item.Strength) || item.SinceYear > message.CurrentYear) return false;

        ulong previousEventId = 0;
        foreach (var item in message.Events)
        {
            if (item is null || item.EventId == 0 || item.EventId <= previousEventId || item.Year > message.CurrentYear || item.Kind > 14
                || item.SettlementId == 0 || !ValidReason(item.Reason)) return false;
            previousEventId = item.EventId;
        }
        foreach (var item in message.CommutingFlows)
            if (item is null || !ValidFlowSettlements(item.FromSettlementId, item.ToSettlementId) || item.WorkerCount <= 0) return false;
        foreach (var item in message.FreightFlows)
            if (item is null || !ValidFlowSettlements(item.FromSettlementId, item.ToSettlementId) || item.CommodityId == 0
                || !NonNegative(item.Quantity) || item.ShipmentCount <= 0 || !NonNegative(item.DeliveredQuantity) || item.DeliveredQuantity > item.Quantity) return false;
        return true;
    }

    private static bool ValidFlowSettlements(ulong from, ulong to) => from != 0 && to != 0 && from != to;
    private static bool ValidReason(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= MaximumReasonLength;
    private static bool Finite(double value) => double.IsFinite(value);
    private static bool Unit(double value) => Finite(value) && value is >= 0d and <= 1d;
    private static bool Positive(double value) => Finite(value) && value > 0d;
    private static bool NonNegative(double value) => Finite(value) && value >= 0d;
}
