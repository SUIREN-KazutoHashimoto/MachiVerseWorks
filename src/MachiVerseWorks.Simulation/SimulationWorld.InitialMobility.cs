namespace MachiVerseWorks.Simulation;

public readonly record struct InitialMobilitySummary(
    int ParticipantCount,
    int PedestrianCount,
    int VehicleCount);

public sealed partial class SimulationWorld
{
    /// <summary>
    /// Adds a small set of ordinary residents whose all-day errand activity primes the
    /// population mobility pipeline for a newly materialized regional world.
    /// </summary>
    public InitialMobilitySummary SeedInitialMobility(int participantCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(participantCount);
        if (participantCount == 0) return default;

        var requiredModes = RoadAccessMode.Foot | RoadAccessMode.Motor;
        var buildingIds = CreateRoadNetworkSnapshot().AccessPoints
            .Where(access => access.BuildingId is not null && (access.Mode & requiredModes) == requiredModes)
            .Select(static access => access.BuildingId!.Value)
            .Distinct()
            .OrderBy(static id => id.Value)
            .ToArray();
        if (buildingIds.Length < 2) return default;

        var beforePedestrians = ActivePedestrianCount;
        var beforeVehicles = ActiveVehicleCount;
        var created = 0;

        for (var index = 0; index < participantCount; index++)
        {
            var origin = buildingIds[index % buildingIds.Length];
            var destination = buildingIds[(index + Math.Max(1, buildingIds.Length / 2)) % buildingIds.Length];
            if (origin == destination) destination = buildingIds[(index + 1) % buildingIds.Length];

            var household = CreateHousehold(TripEndpoint.ForBuilding(origin));
            _ = CreatePerson(
                household,
                new PersonDemographics(
                    AgeYears: 20 + (index % 45),
                    IsEmployed: false,
                    IsStudent: false,
                    HasPrivateVehicle: index % 2 == 1),
                [
                    new DailyActivityWindow(
                        ActivityKind.Errand,
                        StartMinuteOfDay: 0,
                        EndMinuteOfDay: 1440,
                        Destination: TripEndpoint.ForBuilding(destination),
                        Priority: ActivityPriority.High),
                ]);
            created++;
        }

        Step();
        return new InitialMobilitySummary(
            created,
            ActivePedestrianCount - beforePedestrians,
            ActiveVehicleCount - beforeVehicles);
    }
}
