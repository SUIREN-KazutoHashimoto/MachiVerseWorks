using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly RoadTrafficTopology _roadTrafficTopology = new();
    private readonly IntersectionControlStore _intersectionControl = new();
    private readonly VehicleStore _vehicles = new();

    public int VehicleCount => _vehicles.Count;
    public int ActiveVehicleCount => _vehicles.ActiveCount;

    public VehicleId CreateVehicle(
        RouteResult route,
        VehicleDimensions? dimensions = null,
        VehiclePerformance? performance = null,
        double initialSpeedMetersPerSecond = 0d)
    {
        ArgumentNullException.ThrowIfNull(route);
        return CreateVehicle(route.Steps, dimensions, performance, initialSpeedMetersPerSecond);
    }

    public VehicleId CreateVehicle(
        IReadOnlyList<RouteLaneStep> routeSteps,
        VehicleDimensions? dimensions = null,
        VehiclePerformance? performance = null,
        double initialSpeedMetersPerSecond = 0d)
    {
        ArgumentNullException.ThrowIfNull(routeSteps);
        EnsureRoadTrafficTopology();
        return _vehicles.Add(
            routeSteps,
            dimensions ?? VehicleDimensions.PassengerCar,
            performance ?? VehiclePerformance.PassengerCar,
            initialSpeedMetersPerSecond,
            _roadTrafficTopology);
    }

    public bool RemoveVehicle(VehicleId id)
    {
        if (_population.ContainsVehicleReference(id))
            throw new InvalidOperationException($"Vehicle {id.Value} cannot be removed while an active Population trip references it.");
        if (ContainsLogisticsVehicleReference(id))
            throw new InvalidOperationException($"Vehicle {id.Value} cannot be removed while a Logistics shipment references it.");
        if (_multimodalTransit.ContainsRoadVehicleReference(id))
            throw new InvalidOperationException($"Vehicle {id.Value} cannot be removed while a Multimodal Transit vehicle references it.");
        return RemoveVehicleCore(id);
    }

    private bool RemoveVehicleCore(VehicleId id) => _vehicles.Remove(id);

    public bool TryGetVehicleSnapshot(VehicleId id, out VehicleSnapshot snapshot) =>
        _vehicles.TryGetSnapshot(id, Time.TickCount, out snapshot);

    public VehicleSnapshot[] CreateVehicleSnapshot(WorldVolume volume)
    {
        ValidatePoint(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ));
        ValidatePoint(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ));
        return _vehicles.CreateSnapshot(volume, Time.TickCount);
    }

    public IntersectionControlSnapshot CreateIntersectionControlSnapshot()
    {
        EnsureRoadTrafficTopology();
        return _intersectionControl.CreateSnapshot(Time.TickCount);
    }

    public TrafficMetrics CreateTrafficMetrics()
    {
        EnsureRoadTrafficTopology();
        return _vehicles.CreateMetrics(_roadTrafficTopology);
    }

    private void StepVehicles(double deltaSeconds, ulong tickCount)
    {
        EnsureRoadTrafficTopology();
        _vehicles.Step(deltaSeconds, tickCount, _roadTrafficTopology, _intersectionControl);
    }

    private void EnsureRoadTrafficTopology()
    {
        if (!_roadTrafficTopology.NeedsTopology) return;
        var snapshot = _roads.CreateSnapshot();
        _roadTrafficTopology.Rebuild(snapshot);
        _intersectionControl.Rebuild(snapshot, _roadTrafficTopology, Config.TickRate);
    }

    private static void ValidateVehicleCheckpoint(SimulationCheckpoint checkpoint)
    {
        if (checkpoint.NextVehicleId == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextVehicleId, "Next Vehicle ID must be greater than zero.");
        var vehicles = checkpoint.Vehicles ?? Array.Empty<SimulationVehicleCheckpoint>();
        var ids = new HashSet<ulong>(vehicles.Count);
        var maximum = 0UL;
        foreach (var vehicle in vehicles)
        {
            if (vehicle.Id.Value == 0 || !ids.Add(vehicle.Id.Value)) throw new ArgumentException($"Vehicle ID {vehicle.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (vehicle.RouteSteps is null || vehicle.RouteSteps.Count == 0) throw new ArgumentException($"Vehicle {vehicle.Id.Value} has no Route steps.", nameof(checkpoint));
            if (vehicle.RouteStepIndex < 0 || vehicle.RouteStepIndex >= vehicle.RouteSteps.Count) throw new ArgumentException($"Vehicle {vehicle.Id.Value} Route step index is invalid.", nameof(checkpoint));
            if (!double.IsFinite(vehicle.RouteProgressMeters) || vehicle.RouteProgressMeters < 0d || !double.IsFinite(vehicle.SpeedMetersPerSecond) || vehicle.SpeedMetersPerSecond < 0d)
                throw new ArgumentException($"Vehicle {vehicle.Id.Value} progress or speed is invalid.", nameof(checkpoint));
            if (!Enum.IsDefined(vehicle.State)) throw new ArgumentException($"Vehicle {vehicle.Id.Value} movement state is invalid.", nameof(checkpoint));
            maximum = Math.Max(maximum, vehicle.Id.Value);
        }
        if (checkpoint.NextVehicleId <= maximum) throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextVehicleId, "Next Vehicle ID must be greater than every stored Vehicle ID.");
    }
}
