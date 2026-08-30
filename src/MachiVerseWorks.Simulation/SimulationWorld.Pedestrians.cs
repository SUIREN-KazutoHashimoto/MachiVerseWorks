using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly PedestrianNetworkStore _pedestrianNetwork = new();
    private readonly PedestrianStore _pedestrians = new();
    private bool _pedestrianNetworkDirty = true;

    public int PedestrianCount => _pedestrians.Count;
    public int ActivePedestrianCount => _pedestrians.ActiveCount;

    public PedestrianNetworkSnapshot CreatePedestrianNetworkSnapshot()
    {
        EnsurePedestrianNetwork();
        return _pedestrianNetwork.CreateSnapshot();
    }

    public PedestrianRoute FindWalkingRoute(TripEndpoint origin, TripEndpoint destination)
    {
        ValidateTripEndpoint(origin, nameof(origin));
        ValidateTripEndpoint(destination, nameof(destination));
        EnsurePedestrianNetwork();
        return _pedestrianNetwork.FindRoute(origin, destination);
    }

    public PedestrianId CreatePedestrian(TripRequest request, double walkingSpeedMetersPerSecond = 1.4d)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateTripEndpoint(request.Origin, nameof(request));
        ValidateTripEndpoint(request.Destination, nameof(request));
        ValidateEnum(request.Mode, nameof(request));
        EnsurePedestrianNetwork();
        var route = _pedestrianNetwork.FindRoute(request.Origin, request.Destination);
        return _pedestrians.Add(request, route, walkingSpeedMetersPerSecond, _pedestrianNetwork);
    }

    public bool RemovePedestrian(PedestrianId id) => _pedestrians.Remove(id);

    public bool TryGetPedestrianSnapshot(PedestrianId id, out PedestrianSnapshot snapshot) =>
        _pedestrians.TryGetSnapshot(id, Time.TickCount, out snapshot);

    public PedestrianSnapshot[] CreatePedestrianSnapshot(WorldVolume volume)
    {
        _spatialIndex.ValidatePosition(new WorldPoint(volume.MinX, volume.MinY, volume.MinZ));
        _spatialIndex.ValidatePosition(new WorldPoint(volume.MaxX, volume.MaxY, volume.MaxZ));
        return _pedestrians.CreateSnapshot(volume, Time.TickCount);
    }

    public bool SetPedestrianCrossingOpen(PedestrianCrossingId id, bool isOpen)
    {
        EnsurePedestrianNetwork();
        return _pedestrianNetwork.TrySetCrossingOpen(id, isOpen);
    }

    private void StepPedestrians(double deltaSeconds)
    {
        if (_pedestrians.Count == 0) return;
        EnsurePedestrianNetwork();
        _pedestrians.Step(deltaSeconds, _pedestrianNetwork);
    }

    private void InvalidatePedestrianNetwork()
    {
        if (_pedestrians.Count > 0)
            throw new InvalidOperationException("Road topology cannot be changed while stored Pedestrians reference derived routes. Remove them before mutating the walk network.");
        _pedestrianNetworkDirty = true;
    }

    private void EnsurePedestrianNetwork()
    {
        if (!_pedestrianNetworkDirty) return;
        _pedestrianNetwork.Rebuild(_roads.CreateSnapshot());
        _pedestrianNetworkDirty = false;
    }

    private void ValidateTripEndpoint(TripEndpoint endpoint, string parameterName)
    {
        if ((endpoint.BuildingId is null) == (endpoint.PoiId is null))
            throw new ArgumentException("Trip endpoint must reference exactly one Building or POI.", parameterName);
        if (endpoint.BuildingId is { } buildingId && !_buildings.Contains(buildingId))
            throw new ArgumentException($"Building {buildingId.Value} does not exist.", parameterName);
        if (endpoint.PoiId is { } poiId && !_pois.TryGetSnapshot(poiId, out _))
            throw new ArgumentException($"POI {poiId.Value} does not exist.", parameterName);
    }

    private static void ValidatePedestrianCheckpoint(SimulationCheckpoint checkpoint)
    {
        if (checkpoint.NextPedestrianId == 0)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextPedestrianId, "Next Pedestrian ID must be greater than zero.");
        var pedestrians = checkpoint.Pedestrians ?? Array.Empty<SimulationPedestrianCheckpoint>();
        var ids = new HashSet<ulong>(pedestrians.Count);
        var maximum = 0UL;
        foreach (var pedestrian in pedestrians)
        {
            if (pedestrian.Id.Value == 0 || !ids.Add(pedestrian.Id.Value))
                throw new ArgumentException($"Pedestrian ID {pedestrian.Id.Value} is zero or duplicated.", nameof(checkpoint));
            if (pedestrian.TripRequestId.Value == 0)
                throw new ArgumentException("Pedestrian Trip Request ID must be greater than zero.", nameof(checkpoint));
            ValidateEnum(pedestrian.Mode, nameof(checkpoint));
            ValidateEnum(pedestrian.State, nameof(checkpoint));
            if (!double.IsFinite(pedestrian.WalkingSpeedMetersPerSecond) || pedestrian.WalkingSpeedMetersPerSecond <= 0d)
                throw new ArgumentException("Pedestrian walking speed must be finite and greater than zero.", nameof(checkpoint));
            if (pedestrian.LegIndex < 0 || !double.IsFinite(pedestrian.ProgressMeters) || pedestrian.ProgressMeters < 0d)
                throw new ArgumentException("Pedestrian route progress is invalid.", nameof(checkpoint));
            maximum = Math.Max(maximum, pedestrian.Id.Value);
        }
        if (checkpoint.NextPedestrianId <= maximum)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextPedestrianId, "Next Pedestrian ID must be greater than every stored Pedestrian ID.");
    }
}