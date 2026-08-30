using MachiVerseWorks.Protocol;
using MachiVerseWorks.Simulation;

namespace MachiVerseWorks.Server;

internal static class PopulationMessageMapper
{
    public static PopulationStatisticsMessage Create(PopulationStatistics statistics) => new(
        checked((uint)statistics.HouseholdCount),
        checked((uint)statistics.PersonCount),
        checked((uint)statistics.AtActivityCount),
        checked((uint)statistics.WalkingCount),
        checked((uint)statistics.DrivingCount),
        checked((uint)statistics.HomeCount),
        checked((uint)statistics.WorkCount),
        checked((uint)statistics.EducationCount),
        checked((uint)statistics.ShoppingCount),
        checked((uint)statistics.HealthcareCount),
        checked((uint)statistics.RecreationCount),
        checked((uint)statistics.ErrandCount),
        statistics.TickCount);

    public static PersonDebugMessage Create(PersonSnapshot person) => new(
        person.Id.Value,
        person.HouseholdId.Value,
        person.Residence.BuildingId?.Value ?? 0,
        person.Residence.PoiId?.Value ?? 0,
        person.CurrentLocation.BuildingId?.Value ?? 0,
        person.CurrentLocation.PoiId?.Value ?? 0,
        (ProtocolActivityKind)person.CurrentActivity,
        (ProtocolPersonTravelState)person.TravelState,
        person.Destination?.BuildingId?.Value ?? 0,
        person.Destination?.PoiId?.Value ?? 0,
        person.DestinationActivity is { } destinationActivity ? (ProtocolActivityKind)destinationActivity : null,
        person.ActiveTripRequestId?.Value ?? 0,
        person.ActiveTravelMode is { } travelMode ? (ProtocolTravelMode)travelMode : null,
        person.PedestrianId?.Value ?? 0,
        person.VehicleId?.Value ?? 0,
        person.TickCount);
}
