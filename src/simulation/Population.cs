namespace MachiVerseWorks.Simulation;

public readonly record struct HouseholdId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public readonly record struct PersonId(ulong Value)
{
    public override string ToString() => Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

public enum ActivityKind : byte
{
    Home = 0,
    Work = 1,
    Education = 2,
    Shopping = 3,
    Healthcare = 4,
    Recreation = 5,
    Errand = 6,
}

public enum NeedKind : byte
{
    Rest = 0,
    Work = 1,
    Education = 2,
    Shopping = 3,
    Healthcare = 4,
    Recreation = 5,
    Errand = 6,
}

public enum ActivityPriority : byte
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3,
}

public enum PersonTravelState : byte
{
    AtActivity = 0,
    Walking = 1,
    Driving = 2,
    Transit = 3,
}

public readonly record struct PersonDemographics(
    int AgeYears,
    bool IsEmployed = false,
    bool IsStudent = false,
    bool HasPrivateVehicle = false);

public readonly record struct DailyActivityWindow(
    ActivityKind Activity,
    ushort StartMinuteOfDay,
    ushort EndMinuteOfDay,
    TripEndpoint? Destination = null,
    ActivityPriority Priority = ActivityPriority.Normal);

public readonly record struct PersonNeed(
    NeedKind Kind,
    double Satisfaction,
    double DecayPerHour = 0.02d);

public readonly record struct HouseholdSnapshot(
    HouseholdId Id,
    TripEndpoint Residence,
    int PersonCount);

public readonly record struct PersonSnapshot(
    PersonId Id,
    HouseholdId HouseholdId,
    PersonDemographics Demographics,
    TripEndpoint Residence,
    TripEndpoint CurrentLocation,
    ActivityKind CurrentActivity,
    PersonTravelState TravelState,
    TripEndpoint? Destination,
    ActivityKind? DestinationActivity,
    TripRequestId? ActiveTripRequestId,
    TravelMode? ActiveTravelMode,
    PedestrianId? PedestrianId,
    VehicleId? VehicleId,
    ulong TickCount);

public sealed record PersonDebugSnapshot(
    PersonSnapshot Person,
    IReadOnlyList<DailyActivityWindow> Schedule,
    IReadOnlyList<PersonNeed> Needs);

public readonly record struct PopulationStatistics(
    int HouseholdCount,
    int PersonCount,
    int AtActivityCount,
    int WalkingCount,
    int DrivingCount,
    int HomeCount,
    int WorkCount,
    int EducationCount,
    int ShoppingCount,
    int HealthcareCount,
    int RecreationCount,
    int ErrandCount,
    ulong TickCount,
    int TransitCount = 0);
