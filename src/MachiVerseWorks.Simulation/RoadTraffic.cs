namespace MachiVerseWorks.Simulation;

public readonly record struct VehicleId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum VehicleMovementState : byte
{
    Driving = 0,
    WaitingForTraffic = 1,
    ChangingLane = 2,
    Arrived = 3,
}

public readonly record struct VehicleDimensions(double LengthMeters, double WidthMeters, double HeightMeters)
{
    public static VehicleDimensions PassengerCar => new(4.5d, 1.8d, 1.5d);
}

public readonly record struct VehiclePerformance(
    double MaximumSpeedMetersPerSecond,
    double MaximumAccelerationMetersPerSecondSquared,
    double ComfortableDecelerationMetersPerSecondSquared,
    double MinimumGapMeters,
    double TimeHeadwaySeconds)
{
    public static VehiclePerformance PassengerCar => new(33.3333333333d, 2.5d, 4.5d, 2d, 1.5d);
}

public readonly record struct VehicleSnapshot(
    VehicleId Id,
    LaneId LaneId,
    int RouteStepIndex,
    double SegmentOffset,
    double RouteProgressMeters,
    WorldPoint Position,
    WorldVector Velocity,
    WorldVector Forward,
    double SpeedMetersPerSecond,
    VehicleDimensions Dimensions,
    VehicleMovementState State,
    ulong TickCount);

public readonly record struct TrafficMetrics(
    int VehicleCount,
    int ActiveVehicleCount,
    double TotalLaneKilometers,
    double DensityVehiclesPerKilometer,
    double AverageSpeedMetersPerSecond,
    int QueueLength);
