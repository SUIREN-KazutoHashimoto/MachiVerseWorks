using System.Globalization;
using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal readonly record struct EntityInspectionTarget(ProtocolEntityType EntityType, ulong EntityId);
internal readonly record struct EntityInspectionSelection(EntityInspectionTarget? Target, long Revision);

internal sealed class EntityInspectionRegistry
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, EntityInspectionSelection> _selections = [];

    public EntityInspectionSelection Capture(Guid connectionId)
    {
        lock (_gate) return _selections.TryGetValue(connectionId, out var value) ? value : default;
    }

    public void Set(Guid connectionId, EntityInspectionTarget target)
    {
        if (target.EntityId == 0) throw new ArgumentOutOfRangeException(nameof(target));
        lock (_gate)
        {
            var revision = _selections.TryGetValue(connectionId, out var current)
                ? checked(current.Revision + 1)
                : 1;
            _selections[connectionId] = new EntityInspectionSelection(target, revision);
        }
    }

    public void Clear(Guid connectionId)
    {
        lock (_gate)
        {
            var revision = _selections.TryGetValue(connectionId, out var current)
                ? checked(current.Revision + 1)
                : 1;
            _selections[connectionId] = new EntityInspectionSelection(null, revision);
        }
    }

    public bool IsCurrent(Guid connectionId, EntityInspectionSelection selection)
    {
        lock (_gate) return CaptureUnsafe(connectionId) == selection;
    }

    public bool TryStartCurrentSend(
        Guid connectionId,
        EntityInspectionSelection selection,
        Func<Task> startSend,
        out Task? sendTask)
    {
        ArgumentNullException.ThrowIfNull(startSend);
        lock (_gate)
        {
            if (CaptureUnsafe(connectionId) != selection)
            {
                sendTask = null;
                return false;
            }
            sendTask = startSend();
            return true;
        }
    }

    public void Prune(IReadOnlySet<Guid> activeConnectionIds)
    {
        ArgumentNullException.ThrowIfNull(activeConnectionIds);
        lock (_gate)
        {
            foreach (var connectionId in _selections.Keys.Where(id => !activeConnectionIds.Contains(id)).ToArray())
                _selections.Remove(connectionId);
        }
    }

    private EntityInspectionSelection CaptureUnsafe(Guid connectionId) =>
        _selections.TryGetValue(connectionId, out var value) ? value : default;
}

internal static class EntityInspectionMessageMapper
{
    public static EntityInspectionSnapshotMessage Create(
        EntityInspectionTarget target,
        PopulationPublishSnapshot population,
        IReadOnlyDictionary<ulong, TrainSnapshot> trains,
        PersistentRegionalEvolutionSnapshotMessage? regional) =>
        Create(target, population, new Dictionary<ulong, VehicleSnapshot>(), trains, regional);

    public static EntityInspectionSnapshotMessage Create(
        EntityInspectionTarget target,
        PopulationPublishSnapshot population,
        IReadOnlyDictionary<ulong, VehicleSnapshot> vehicles,
        IReadOnlyDictionary<ulong, TrainSnapshot> trains,
        PersistentRegionalEvolutionSnapshotMessage? regional)
    {
        ArgumentNullException.ThrowIfNull(population);
        ArgumentNullException.ThrowIfNull(vehicles);
        ArgumentNullException.ThrowIfNull(trains);

        return target.EntityType switch
        {
            ProtocolEntityType.Person => CreatePerson(target.EntityId, population, regional?.CurrentYear),
            ProtocolEntityType.Vehicle => CreateVehicle(target.EntityId, population.TickCount, vehicles, regional?.CurrentYear),
            ProtocolEntityType.Train => CreateTrain(target.EntityId, population.TickCount, trains, regional?.CurrentYear),
            ProtocolEntityType.Settlement => CreateSettlement(target.EntityId, population.TickCount, regional),
            ProtocolEntityType.Parcel => CreateParcel(target.EntityId, population.TickCount, regional),
            ProtocolEntityType.Building => CreateBuilding(target.EntityId, population.TickCount, regional),
            _ => NotFound(target, population.TickCount, regional?.CurrentYear),
        };
    }

    private static EntityInspectionSnapshotMessage CreatePerson(ulong id, PopulationPublishSnapshot population, int? currentYear)
    {
        if (!population.InspectedPersons.TryGetValue(id, out var person))
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Person, id), population.TickCount, currentYear);

        var debug = PopulationMessageMapper.Create(person);
        var fields = new List<ProtocolInspectionField>
        {
            Field("householdId", debug.HouseholdId),
            Field("residenceBuildingId", debug.ResidenceBuildingId),
            Field("residencePoiId", debug.ResidencePoiId),
            Field("currentBuildingId", debug.CurrentBuildingId),
            Field("currentPoiId", debug.CurrentPoiId),
            Field("currentActivity", debug.CurrentActivity.ToString()),
            Field("travelState", debug.TravelState.ToString()),
            Field("destinationBuildingId", debug.DestinationBuildingId),
            Field("destinationPoiId", debug.DestinationPoiId),
            Field("destinationActivity", debug.DestinationActivity?.ToString() ?? "none"),
            Field("activeTripRequestId", debug.ActiveTripRequestId),
            Field("activeTravelMode", debug.ActiveTravelMode?.ToString() ?? "none"),
            Field("pedestrianId", debug.PedestrianId),
            Field("vehicleId", debug.VehicleId),
        };
        var relations = new List<ProtocolInspectionRelation>();
        AddBuildingRelation(relations, "residence", debug.ResidenceBuildingId);
        AddBuildingRelation(relations, "currentLocation", debug.CurrentBuildingId);
        AddBuildingRelation(relations, "destination", debug.DestinationBuildingId);
        return Snapshot(ProtocolEntityType.Person, id, debug.TickCount, currentYear, fields, relations, []);
    }

    private static EntityInspectionSnapshotMessage CreateVehicle(
        ulong id,
        ulong tickCount,
        IReadOnlyDictionary<ulong, VehicleSnapshot> vehicles,
        int? currentYear)
    {
        if (!vehicles.TryGetValue(id, out var vehicle))
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Vehicle, id), tickCount, currentYear);

        var fields = new List<ProtocolInspectionField>
        {
            Field("laneId", vehicle.LaneId.Value),
            Field("routeStepIndex", vehicle.RouteStepIndex),
            Field("segmentOffset", vehicle.SegmentOffset),
            Field("routeProgressMeters", vehicle.RouteProgressMeters),
            Field("x", vehicle.Position.X),
            Field("y", vehicle.Position.Y),
            Field("z", vehicle.Position.Z),
            Field("velocityX", vehicle.Velocity.X),
            Field("velocityY", vehicle.Velocity.Y),
            Field("velocityZ", vehicle.Velocity.Z),
            Field("forwardX", vehicle.Forward.X),
            Field("forwardY", vehicle.Forward.Y),
            Field("forwardZ", vehicle.Forward.Z),
            Field("speedMetersPerSecond", vehicle.SpeedMetersPerSecond),
            Field("lengthMeters", vehicle.Dimensions.LengthMeters),
            Field("widthMeters", vehicle.Dimensions.WidthMeters),
            Field("heightMeters", vehicle.Dimensions.HeightMeters),
            Field("state", vehicle.State.ToString()),
        };
        return Snapshot(ProtocolEntityType.Vehicle, id, vehicle.TickCount, currentYear, fields, [], []);
    }

    private static EntityInspectionSnapshotMessage CreateTrain(
        ulong id,
        ulong tickCount,
        IReadOnlyDictionary<ulong, TrainSnapshot> trains,
        int? currentYear)
    {
        if (!trains.TryGetValue(id, out var train))
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Train, id), tickCount, currentYear);

        var fields = new List<ProtocolInspectionField>
        {
            Field("formationId", train.FormationId.Value),
            Field("serviceId", train.ServiceId.Value),
            Field("routeId", train.RouteId.Value),
            Field("routeDistanceMeters", train.RouteDistanceMeters),
            Field("x", train.Position.X),
            Field("y", train.Position.Y),
            Field("z", train.Position.Z),
            Field("forwardX", train.Forward.X),
            Field("forwardY", train.Forward.Y),
            Field("forwardZ", train.Forward.Z),
            Field("speedMetersPerSecond", train.SpeedMetersPerSecond),
            Field("state", train.State.ToString()),
            Field("currentBlockId", train.CurrentBlockId?.Value ?? 0),
            Field("currentPlatformId", train.CurrentPlatformId?.Value ?? 0),
            Field("assignedPlatformId", train.AssignedPlatformId?.Value ?? 0),
            Field("currentDepotId", train.CurrentDepotId?.Value ?? 0),
            Field("dwellDepartureTick", train.DwellDepartureTick),
        };
        return Snapshot(ProtocolEntityType.Train, id, train.TickCount, currentYear, fields, [], []);
    }

    private static EntityInspectionSnapshotMessage CreateSettlement(
        ulong id,
        ulong tickCount,
        PersistentRegionalEvolutionSnapshotMessage? regional)
    {
        var settlement = regional?.Settlements.FirstOrDefault(item => item.SettlementId == id);
        if (settlement is null)
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Settlement, id), tickCount, regional?.CurrentYear);

        var fields = new List<ProtocolInspectionField>
        {
            Field("x", settlement.X), Field("y", settlement.Y), Field("z", settlement.Z),
            Field("population", settlement.Population), Field("jobs", settlement.Jobs),
            Field("serviceIndex", settlement.ServiceIndex), Field("density", settlement.Density),
            Field("accessibility", settlement.Accessibility), Field("influenceRadiusMeters", settlement.InfluenceRadiusMeters),
            Field("scale", ((SettlementScale)settlement.Scale).ToString()),
            Field("trend", ((SettlementTrend)settlement.Trend).ToString()),
            Field("isActive", settlement.IsActive), Field("establishedYear", settlement.EstablishedYear),
            Field("dormantSinceYear", settlement.DormantSinceYear?.ToString(CultureInfo.InvariantCulture) ?? "none"),
        };
        foreach (var catchment in regional!.ServiceCatchments.Where(item => item.SettlementId == id).OrderBy(item => item.Kind))
            fields.Add(Field($"serviceCatchment.{(RegionalServiceKind)catchment.Kind}", $"radius={Format(catchment.RadiusMeters)};coverage={Format(catchment.Coverage)}"));
        foreach (var demand in regional.InfrastructureDemands.Where(item => item.SettlementId == id).OrderBy(item => item.Kind))
            fields.Add(Field($"infrastructureDemand.{(InfrastructureDemandKind)demand.Kind}", $"demand={Format(demand.Demand)};reason={demand.Reason}"));

        var relations = regional.Relations
            .Where(item => item.FromSettlementId == id || item.ToSettlementId == id)
            .OrderByDescending(item => item.Strength).ThenBy(item => item.RelationId)
            .Take(EntityInspectionProtocolCodec.MaximumRelations)
            .Select(item => new ProtocolInspectionRelation(
                ((RegionalRelationKind)item.Kind).ToString(),
                ProtocolEntityType.Settlement,
                item.FromSettlementId == id ? item.ToSettlementId : item.FromSettlementId,
                item.Strength))
            .ToArray();
        var recent = RecentEvents(regional.Events.Where(item => item.SettlementId == id));
        return Snapshot(ProtocolEntityType.Settlement, id, regional.TickCount, regional.CurrentYear, fields, relations, recent);
    }

    private static EntityInspectionSnapshotMessage CreateParcel(
        ulong id,
        ulong tickCount,
        PersistentRegionalEvolutionSnapshotMessage? regional)
    {
        var parcel = regional?.Parcels.FirstOrDefault(item => item.ParcelId == id);
        if (parcel is null)
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Parcel, id), tickCount, regional?.CurrentYear);

        var fields = new List<ProtocolInspectionField>
        {
            Field("settlementId", parcel.SettlementId), Field("developmentDemand", parcel.DevelopmentDemand),
            Field("landValue", parcel.LandValue), Field("developmentState", ((ParcelDevelopmentState)parcel.DevelopmentState).ToString()),
            Field("buildingId", parcel.BuildingId),
        };
        var relations = new List<ProtocolInspectionRelation>
        {
            new("settlement", ProtocolEntityType.Settlement, parcel.SettlementId, 1d),
        };
        if (parcel.BuildingId != 0) relations.Add(new("building", ProtocolEntityType.Building, parcel.BuildingId, 1d));
        var recent = parcel.BuildingId == 0 ? [] : RecentEvents(regional!.Events.Where(item => item.BuildingId == parcel.BuildingId));
        return Snapshot(ProtocolEntityType.Parcel, id, regional!.TickCount, regional.CurrentYear, fields, relations, recent);
    }

    private static EntityInspectionSnapshotMessage CreateBuilding(
        ulong id,
        ulong tickCount,
        PersistentRegionalEvolutionSnapshotMessage? regional)
    {
        var building = regional?.Buildings.FirstOrDefault(item => item.BuildingId == id);
        if (building is null)
            return NotFound(new EntityInspectionTarget(ProtocolEntityType.Building, id), tickCount, regional?.CurrentYear);

        var fields = new List<ProtocolInspectionField>
        {
            Field("parcelId", building.ParcelId), Field("use", ((GeneratedBuildingUse)building.Use).ToString()),
            Field("builtYear", building.BuiltYear), Field("lastChangedYear", building.LastChangedYear),
            Field("condition", building.Condition), Field("occupancy", building.Occupancy),
            Field("capacity", building.Capacity), Field("status", ((BuildingLifecycleStatus)building.Status).ToString()),
        };
        var relations = new List<ProtocolInspectionRelation>
        {
            new("parcel", ProtocolEntityType.Parcel, building.ParcelId, 1d),
        };
        var parcel = regional!.Parcels.FirstOrDefault(item => item.ParcelId == building.ParcelId);
        if (parcel is not null) relations.Add(new("settlement", ProtocolEntityType.Settlement, parcel.SettlementId, 1d));
        var recent = RecentEvents(regional.Events.Where(item => item.BuildingId == id));
        return Snapshot(ProtocolEntityType.Building, id, regional.TickCount, regional.CurrentYear, fields, relations, recent);
    }

    private static ProtocolInspectionEvent[] RecentEvents(IEnumerable<ProtocolRegionalEvolutionEvent> events) =>
        events.OrderByDescending(item => item.Year).ThenByDescending(item => item.EventId)
            .Take(EntityInspectionProtocolCodec.MaximumRecentEvents)
            .Select(item => new ProtocolInspectionEvent(item.EventId, item.Year, ((RegionalEvolutionEventKind)item.Kind).ToString(), item.Reason))
            .ToArray();

    private static EntityInspectionSnapshotMessage Snapshot(
        ProtocolEntityType type,
        ulong id,
        ulong tickCount,
        int? currentYear,
        IReadOnlyList<ProtocolInspectionField> fields,
        IReadOnlyList<ProtocolInspectionRelation> relations,
        IReadOnlyList<ProtocolInspectionEvent> recent) =>
        new(type, id, tickCount, currentYear, true, fields, relations, recent, PlannedFutureAvailable: false, PlannedFuture: []);

    private static EntityInspectionSnapshotMessage NotFound(EntityInspectionTarget target, ulong tickCount, int? currentYear) =>
        new(target.EntityType, target.EntityId, tickCount, currentYear, false, [], [], [], PlannedFutureAvailable: false, PlannedFuture: []);

    private static void AddBuildingRelation(List<ProtocolInspectionRelation> relations, string kind, ulong buildingId)
    {
        if (buildingId != 0) relations.Add(new ProtocolInspectionRelation(kind, ProtocolEntityType.Building, buildingId, 1d));
    }

    private static ProtocolInspectionField Field(string name, string value) => new(name, value);
    private static ProtocolInspectionField Field(string name, ulong value) => new(name, value.ToString(CultureInfo.InvariantCulture));
    private static ProtocolInspectionField Field(string name, int value) => new(name, value.ToString(CultureInfo.InvariantCulture));
    private static ProtocolInspectionField Field(string name, double value) => new(name, Format(value));
    private static ProtocolInspectionField Field(string name, bool value) => new(name, value ? "true" : "false");
    private static string Format(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
