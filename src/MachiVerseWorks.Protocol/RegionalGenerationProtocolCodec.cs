using System.Text.Json;

namespace MachiVerseWorks.Protocol;

public static class RegionalGenerationProtocolCodec
{
    private const int MaximumSettlements = 64;
    private const int MaximumGrowthEvents = 1_024;
    private const int MaximumCorridors = 512;
    private const int MaximumCorridorGeometryPoints = 256;
    private const int MaximumDistricts = 512;
    private const int MaximumParcels = 4_096;
    private const int MaximumBuildings = 4_096;
    private const int MaximumPois = 1_024;
    private const int MaximumToponyms = 4_096;
    private const int MaximumRoadSigns = 4_096;
    private const int MaximumTextLength = 256;
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        MaxDepth = 16,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static byte[] Serialize(RegionalGenerationSnapshotMessage message, ProtocolVersion version)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!version.SupportsRegionalGeneration)
            throw new ArgumentOutOfRangeException(nameof(version), version, "Regional generation messages require Protocol 2.18 or newer.");
        Validate(message);
        var payload = JsonSerializer.SerializeToUtf8Bytes(message, SerializerOptions);
        if ((uint)payload.Length > ProtocolFrameHeader.MaxPayloadLength)
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation snapshot exceeds protocol payload limit.");
        var frame = new byte[ProtocolFrameHeader.Size + payload.Length];
        ProtocolFrameHeader.Write(frame, new ProtocolFrameHeader(version, MessageType.RegionalGenerationSnapshot, checked((uint)payload.Length)));
        payload.CopyTo(frame.AsSpan(ProtocolFrameHeader.Size));
        return frame;
    }

    public static bool TryDeserialize(ReadOnlySpan<byte> frame, out ProtocolEnvelope? envelope, out ProtocolDecodeError error)
    {
        envelope = null;
        if (!ProtocolFrameHeader.TryRead(frame, out var header, out error)) return false;
        if (header.MessageType != MessageType.RegionalGenerationSnapshot)
        {
            error = ProtocolDecodeError.UnknownMessageType;
            return false;
        }
        if (!header.Version.SupportsRegionalGeneration)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        try
        {
            var message = JsonSerializer.Deserialize<RegionalGenerationSnapshotMessage>(frame[ProtocolFrameHeader.Size..], SerializerOptions);
            if (message is null || !IsValid(message))
            {
                error = ProtocolDecodeError.InvalidPayload;
                return false;
            }
            envelope = new ProtocolEnvelope(header.Version, message);
            error = ProtocolDecodeError.None;
            return true;
        }
        catch (JsonException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
        catch (NotSupportedException)
        {
            error = ProtocolDecodeError.InvalidPayload;
            return false;
        }
    }

    private static void Validate(RegionalGenerationSnapshotMessage message)
    {
        if (!IsValid(message))
            throw new ArgumentOutOfRangeException(nameof(message), "Regional generation snapshot contains invalid values or references.");
    }

    private static bool IsValid(RegionalGenerationSnapshotMessage message)
    {
        if (message.WorldSeed == 0UL || message.Preset > 2 || message.Iterations is < 0 or > 32) return false;
        if (!ValidVolume(message.MinX, message.MinY, message.MinZ, message.MaxX, message.MaxY, message.MaxZ, requireHorizontalArea: true)) return false;
        if (message.Settlements is null || message.GrowthEvents is null || message.Corridors is null || message.Districts is null
            || message.Parcels is null || message.Buildings is null || message.Pois is null || message.Toponyms is null || message.RoadSigns is null) return false;
        if (message.Settlements.Count > MaximumSettlements || message.GrowthEvents.Count > MaximumGrowthEvents
            || message.Corridors.Count > MaximumCorridors || message.Districts.Count > MaximumDistricts
            || message.Parcels.Count > MaximumParcels || message.Buildings.Count > MaximumBuildings
            || message.Pois.Count > MaximumPois || message.Toponyms.Count > MaximumToponyms || message.RoadSigns.Count > MaximumRoadSigns) return false;
        if (!ValidQuality(message.Quality)) return false;

        var toponymIds = new HashSet<ulong>();
        foreach (var item in message.Toponyms)
        {
            if (item is null || item.ToponymId == 0UL || !toponymIds.Add(item.ToponymId) || item.Kind > 5
                || !ValidText(item.Name, 160) || !ValidText(item.GeneratorKey, 128)) return false;
            if (item.SourceNaturalToponymId == 0UL && !string.IsNullOrEmpty(item.SourceNaturalName)) return false;
            if (item.SourceNaturalToponymId != 0UL && !ValidText(item.SourceNaturalName, 160)) return false;
        }
        foreach (var item in message.Toponyms)
        {
            if (item.ParentHumanToponymId != 0UL && !toponymIds.Contains(item.ParentHumanToponymId)) return false;
        }
        if (!AcyclicParents(message.Toponyms.Select(static item => (item.ToponymId, item.ParentHumanToponymId)))) return false;

        var settlementIds = new HashSet<ulong>();
        foreach (var item in message.Settlements)
        {
            if (item is null || item.SettlementId == 0UL || !settlementIds.Add(item.SettlementId)
                || item.Environment > 7 || item.Origin > 9 || item.Role > 7 || item.InitialEconomy > 7
                || !ValidPoint(item.X, item.Y, item.Z) || !ValidSuitability(item.Suitability)
                || item.Population < 0 || item.Jobs < 0 || !Positive(item.InfluenceRadiusMeters)
                || !toponymIds.Contains(item.NameId)) return false;
        }

        var growthIds = new HashSet<ulong>();
        foreach (var item in message.GrowthEvents)
        {
            if (item is null || item.EventId == 0UL || !growthIds.Add(item.EventId) || !settlementIds.Contains(item.SettlementId)
                || item.Stage > 5 || item.Sequence < 0 || !ValidPoint(item.X, item.Y, item.Z)
                || item.PopulationDelta < 0 || item.JobDelta < 0 || !ValidText(item.Reason, MaximumTextLength)) return false;
        }

        var corridorIds = new HashSet<ulong>();
        foreach (var item in message.Corridors)
        {
            if (item is null || item.CorridorId == 0UL || !corridorIds.Add(item.CorridorId) || item.Kind > 3
                || !settlementIds.Contains(item.FromSettlementId) || !settlementIds.Contains(item.ToSettlementId)
                || item.FromSettlementId == item.ToSettlementId || item.Geometry is null
                || item.Geometry.Count is < 2 or > MaximumCorridorGeometryPoints || !Unit(item.TerrainAdaptation)
                || !NonNegative(item.ConstructionCost) || (item.NameId != 0UL && !toponymIds.Contains(item.NameId))) return false;
            foreach (var point in item.Geometry) if (!ValidPoint(point.X, point.Y, point.Z)) return false;
        }

        var districtIds = new HashSet<ulong>();
        foreach (var item in message.Districts)
        {
            if (item is null || item.DistrictId == 0UL || !districtIds.Add(item.DistrictId) || !settlementIds.Contains(item.SettlementId)
                || item.Kind > 5 || !ValidVolume(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, requireHorizontalArea: true)
                || !toponymIds.Contains(item.NameId) || !Unit(item.Accessibility)) return false;
        }

        var districtById = message.Districts.ToDictionary(static item => item.DistrictId);
        var parcelIds = new HashSet<ulong>();
        foreach (var item in message.Parcels)
        {
            if (item is null || item.ParcelId == 0UL || !parcelIds.Add(item.ParcelId) || !settlementIds.Contains(item.SettlementId)
                || !districtById.TryGetValue(item.DistrictId, out var district) || district.SettlementId != item.SettlementId || item.Zone > 6 || item.DevelopmentState > 3
                || !ValidVolume(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, requireHorizontalArea: true)
                || !Unit(item.DevelopmentSuitability) || !Unit(item.LandValue)) return false;
        }

        var buildingIds = new HashSet<ulong>();
        foreach (var item in message.Buildings)
        {
            if (item is null || item.BuildingId == 0UL || !buildingIds.Add(item.BuildingId) || !parcelIds.Contains(item.ParcelId)
                || item.Use > 6 || !ValidVolume(item.MinX, item.MinY, item.MinZ, item.MaxX, item.MaxY, item.MaxZ, requireHorizontalArea: true)
                || item.Floors is <= 0 or > 256 || item.Capacity < 0 || item.HistoricalStage < 0) return false;
        }
        var parcelById = message.Parcels.ToDictionary(static item => item.ParcelId);
        var buildingById = message.Buildings.ToDictionary(static item => item.BuildingId);
        var occupiedParcels = new HashSet<ulong>();
        foreach (var building in message.Buildings)
        {
            if (!parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.BuildingId != building.BuildingId || !occupiedParcels.Add(building.ParcelId)
                || !ContainsHorizontal(parcel.MinX, parcel.MinY, parcel.MaxX, parcel.MaxY, building.MinX, building.MinY, building.MaxX, building.MaxY)) return false;
        }
        foreach (var parcel in message.Parcels)
        {
            if (parcel.BuildingId != 0UL && (!buildingById.TryGetValue(parcel.BuildingId, out var building) || building.ParcelId != parcel.ParcelId)) return false;
        }

        var poiIds = new HashSet<ulong>();
        foreach (var item in message.Pois)
        {
            if (item is null || item.PoiId == 0UL || !poiIds.Add(item.PoiId) || !settlementIds.Contains(item.SettlementId)
                || item.Kind > 5 || !ValidPoint(item.X, item.Y, item.Z)
                || (item.BuildingId != 0UL && (!buildingById.TryGetValue(item.BuildingId, out var building) || !parcelById.TryGetValue(building.ParcelId, out var parcel) || parcel.SettlementId != item.SettlementId))
                || (item.NameId != 0UL && !toponymIds.Contains(item.NameId))) return false;
        }

        var signIds = new HashSet<ulong>();
        foreach (var item in message.RoadSigns)
        {
            if (item is null || item.RoadSignId == 0UL || !signIds.Add(item.RoadSignId) || item.Kind > 9
                || !ValidPoint(item.X, item.Y, item.Z) || !corridorIds.Contains(item.CorridorId)
                || (item.DestinationSettlementId != 0UL && !settlementIds.Contains(item.DestinationSettlementId))
                || !ValidText(item.Text, MaximumTextLength)) return false;
        }
        return true;
    }

    private static bool ContainsHorizontal(double outerMinX, double outerMinY, double outerMaxX, double outerMaxY, double innerMinX, double innerMinY, double innerMaxX, double innerMaxY) =>
        innerMinX >= outerMinX && innerMaxX <= outerMaxX && innerMinY >= outerMinY && innerMaxY <= outerMaxY;

    private static bool AcyclicParents(IEnumerable<(ulong Id, ulong ParentId)> nodes)
    {
        var parents = nodes.ToDictionary(static item => item.Id, static item => item.ParentId);
        foreach (var start in parents.Keys)
        {
            var seen = new HashSet<ulong>();
            var current = start;
            while (parents.TryGetValue(current, out var parent) && parent != 0UL)
            {
                if (!seen.Add(current)) return false;
                current = parent;
            }
        }
        return true;
    }

    private static bool ValidSuitability(ProtocolSettlementSuitability value) =>
        Unit(value.Flatness) && Unit(value.WaterAccess) && Unit(value.TransportPotential) && Unit(value.Buildability)
        && Unit(value.ResourceAccess) && Unit(value.FloodRisk) && Unit(value.SteepSlopeRisk) && Unit(value.Isolation)
        && Unit(value.ConstructionCost) && Unit(value.TotalScore);

    private static bool ValidQuality(ProtocolRegionalQualityReport value) =>
        Unit(value.TerrainAdaptation) && Unit(value.RoadConnectivity) && Unit(value.AverageSlopeCost) && Unit(value.Accessibility)
        && Unit(value.CongestionRisk) && Unit(value.LandUseConsistency) && Unit(value.FloodExposure)
        && Unit(value.UrbanCompactness) && Unit(value.PolycentricBalance) && Unit(value.OverallScore);

    private static bool ValidPoint(double x, double y, double z) => Finite(x) && Finite(y) && Finite(z);
    private static bool ValidVolume(double minX, double minY, double minZ, double maxX, double maxY, double maxZ, bool requireHorizontalArea) =>
        ValidPoint(minX, minY, minZ) && ValidPoint(maxX, maxY, maxZ) && maxX >= minX && maxY >= minY && maxZ >= minZ
        && (!requireHorizontalArea || (maxX > minX && maxY > minY));
    private static bool ValidText(string value, int maximumLength) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximumLength;
    private static bool Finite(double value) => double.IsFinite(value);
    private static bool Unit(double value) => Finite(value) && value is >= 0d and <= 1d;
    private static bool NonNegative(double value) => Finite(value) && value >= 0d;
    private static bool Positive(double value) => Finite(value) && value > 0d;
}
