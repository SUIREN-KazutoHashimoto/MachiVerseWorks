namespace MachiVerseWorks.Simulation.Internal;

internal sealed class PopulationStore
{
    private readonly List<HouseholdState> households = [];
    private readonly Dictionary<HouseholdId, int> householdIndex = [];
    private readonly List<PersonState> persons = [];
    private readonly Dictionary<PersonId, int> personIndex = [];
    private ulong nextHouseholdId = 1;
    private ulong nextPersonId = 1;
    private ulong nextTripRequestId = 1;

    public int HouseholdCount => households.Count;
    public int PersonCount => persons.Count;
    public ulong NextHouseholdId => nextHouseholdId;
    public ulong NextPersonId => nextPersonId;
    public ulong NextTripRequestId => nextTripRequestId;

    public HouseholdId AddHousehold(TripEndpoint residence)
    {
        EnsureCapacity(nextHouseholdId, "Household");
        var id = new HouseholdId(nextHouseholdId++);
        householdIndex.Add(id, households.Count);
        households.Add(new HouseholdState(id, residence));
        return id;
    }

    public PersonId AddPerson(
        HouseholdId householdId,
        PersonDemographics demographics,
        DailyActivityWindow[] schedule,
        PersonNeed[] needs)
    {
        if (!householdIndex.TryGetValue(householdId, out var householdPosition))
            throw new ArgumentException($"Household {householdId.Value} does not exist.", nameof(householdId));
        EnsureCapacity(nextPersonId, "Person");
        var household = households[householdPosition];
        var id = new PersonId(nextPersonId++);
        personIndex.Add(id, persons.Count);
        persons.Add(new PersonState(
            id,
            householdId,
            demographics,
            household.Residence,
            household.Residence,
            ActivityKind.Home,
            schedule,
            needs));
        household.PersonCount++;
        return id;
    }

    public bool RelocateHousehold(HouseholdId id, TripEndpoint residence)
    {
        if (!householdIndex.TryGetValue(id, out var householdPosition)) return false;
        var household = households[householdPosition];
        var previous = household.Residence;
        if (previous == residence) return true;
        household.Residence = residence;

        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.HouseholdId != id) continue;
            person.Residence = residence;
            if (person.TravelState == PersonTravelState.AtActivity
                && person.CurrentActivity == ActivityKind.Home
                && person.CurrentLocation == previous)
            {
                person.CurrentLocation = residence;
            }
        }
        return true;
    }

    public void ReplaceBuildingReferences(BuildingId buildingId, TripEndpoint replacement)
    {
        for (var index = 0; index < households.Count; index++)
        {
            var household = households[index];
            if (household.Residence.BuildingId == buildingId) household.Residence = replacement;
        }

        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.Residence.BuildingId == buildingId) person.Residence = replacement;
            if (person.CurrentLocation.BuildingId == buildingId) person.CurrentLocation = replacement;
            if (person.Destination is { } destination && destination.BuildingId == buildingId) person.Destination = replacement;
            for (var scheduleIndex = 0; scheduleIndex < person.Schedule.Length; scheduleIndex++)
            {
                var window = person.Schedule[scheduleIndex];
                if (window.Destination is { } scheduled && scheduled.BuildingId == buildingId)
                    person.Schedule[scheduleIndex] = window with { Destination = replacement };
            }
        }
    }

    public void ReplacePoiReferences(PoiId poiId, TripEndpoint replacement)
    {
        for (var index = 0; index < households.Count; index++)
        {
            var household = households[index];
            if (household.Residence.PoiId == poiId) household.Residence = replacement;
        }

        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.Residence.PoiId == poiId) person.Residence = replacement;
            if (person.CurrentLocation.PoiId == poiId) person.CurrentLocation = replacement;
            if (person.Destination is { } destination && destination.PoiId == poiId) person.Destination = replacement;
            for (var scheduleIndex = 0; scheduleIndex < person.Schedule.Length; scheduleIndex++)
            {
                var window = person.Schedule[scheduleIndex];
                if (window.Destination is { } scheduled && scheduled.PoiId == poiId)
                    person.Schedule[scheduleIndex] = window with { Destination = replacement };
            }
        }
    }

    public TripRequestId PeekTripRequestId()
    {
        EnsureCapacity(nextTripRequestId, "Trip request");
        return new TripRequestId(nextTripRequestId);
    }

    public void CommitTripRequestId(TripRequestId id)
    {
        if (id.Value != nextTripRequestId)
            throw new InvalidOperationException($"Trip Request ID {id.Value} is not the currently reserved ID {nextTripRequestId}.");
        nextTripRequestId = checked(nextTripRequestId + 1);
    }

    public HouseholdState GetHouseholdAt(int index) => households[index];
    public PersonState GetPersonAt(int index) => persons[index];

    public bool TryGetHousehold(HouseholdId id, out HouseholdState state)
    {
        if (householdIndex.TryGetValue(id, out var index))
        {
            state = households[index];
            return true;
        }
        state = null!;
        return false;
    }

    public bool TryGetPerson(PersonId id, out PersonState state)
    {
        if (personIndex.TryGetValue(id, out var index))
        {
            state = persons[index];
            return true;
        }
        state = null!;
        return false;
    }

    public bool ContainsPedestrianReference(PedestrianId id)
    {
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.TravelState == PersonTravelState.Walking && person.PedestrianId == id) return true;
        }
        return false;
    }

    public bool ContainsVehicleReference(VehicleId id)
    {
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.TravelState == PersonTravelState.Driving && person.VehicleId == id) return true;
        }
        return false;
    }

    public bool ContainsBuildingReference(BuildingId id)
    {
        for (var index = 0; index < households.Count; index++)
            if (households[index].Residence.BuildingId == id) return true;
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.CurrentLocation.BuildingId == id || person.Destination?.BuildingId == id) return true;
            for (var scheduleIndex = 0; scheduleIndex < person.Schedule.Length; scheduleIndex++)
                if (person.Schedule[scheduleIndex].Destination?.BuildingId == id) return true;
        }
        return false;
    }

    public bool ContainsPoiReference(PoiId id)
    {
        for (var index = 0; index < households.Count; index++)
            if (households[index].Residence.PoiId == id) return true;
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.CurrentLocation.PoiId == id || person.Destination?.PoiId == id) return true;
            for (var scheduleIndex = 0; scheduleIndex < person.Schedule.Length; scheduleIndex++)
                if (person.Schedule[scheduleIndex].Destination?.PoiId == id) return true;
        }
        return false;
    }

    public IReadOnlyList<SimulationHouseholdCheckpoint> CreateHouseholdCheckpoint()
    {
        var result = new SimulationHouseholdCheckpoint[households.Count];
        for (var index = 0; index < households.Count; index++)
        {
            var household = households[index];
            result[index] = new SimulationHouseholdCheckpoint(household.Id, household.Residence);
        }
        return result;
    }

    public IReadOnlyList<SimulationPersonCheckpoint> CreatePersonCheckpoint()
    {
        var result = new SimulationPersonCheckpoint[persons.Count];
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            result[index] = new SimulationPersonCheckpoint(
                person.Id,
                person.HouseholdId,
                person.Demographics,
                person.Residence,
                person.CurrentLocation,
                person.CurrentActivity,
                person.TravelState,
                person.Destination,
                person.DestinationActivity,
                person.ActiveTripRequestId,
                person.ActiveTravelMode,
                person.PedestrianId,
                person.VehicleId,
                Array.AsReadOnly(person.Schedule.ToArray()),
                Array.AsReadOnly(person.Needs.ToArray()));
        }
        return result;
    }

    public void Restore(
        IReadOnlyList<SimulationHouseholdCheckpoint> householdCheckpoint,
        ulong nextHousehold,
        IReadOnlyList<SimulationPersonCheckpoint> personCheckpoint,
        ulong nextPerson,
        ulong nextTripRequest)
    {
        households.Clear();
        householdIndex.Clear();
        persons.Clear();
        personIndex.Clear();

        for (var index = 0; index < householdCheckpoint.Count; index++)
        {
            var item = householdCheckpoint[index];
            householdIndex.Add(item.Id, households.Count);
            households.Add(new HouseholdState(item.Id, item.Residence));
        }

        for (var index = 0; index < personCheckpoint.Count; index++)
        {
            var item = personCheckpoint[index];
            var state = new PersonState(
                item.Id,
                item.HouseholdId,
                item.Demographics,
                item.Residence,
                item.CurrentLocation,
                item.CurrentActivity,
                item.Schedule.ToArray(),
                item.Needs.ToArray())
            {
                TravelState = item.TravelState,
                Destination = item.Destination,
                DestinationActivity = item.DestinationActivity,
                ActiveTripRequestId = item.ActiveTripRequestId,
                ActiveTravelMode = item.ActiveTravelMode,
                PedestrianId = item.PedestrianId,
                VehicleId = item.VehicleId,
            };
            personIndex.Add(item.Id, persons.Count);
            persons.Add(state);
            households[householdIndex[item.HouseholdId]].PersonCount++;
        }

        nextHouseholdId = nextHousehold;
        nextPersonId = nextPerson;
        nextTripRequestId = nextTripRequest;
    }

    private static void EnsureCapacity(ulong nextId, string name)
    {
        if (nextId == ulong.MaxValue) throw new OverflowException($"{name} ID capacity has been exhausted.");
    }
}

internal sealed class HouseholdState(HouseholdId id, TripEndpoint residence)
{
    public HouseholdId Id { get; } = id;
    public TripEndpoint Residence { get; set; } = residence;
    public int PersonCount { get; set; }
}

internal sealed class PersonState(
    PersonId id,
    HouseholdId householdId,
    PersonDemographics demographics,
    TripEndpoint residence,
    TripEndpoint currentLocation,
    ActivityKind currentActivity,
    DailyActivityWindow[] schedule,
    PersonNeed[] needs)
{
    public PersonId Id { get; } = id;
    public HouseholdId HouseholdId { get; } = householdId;
    public PersonDemographics Demographics { get; } = demographics;
    public TripEndpoint Residence { get; set; } = residence;
    public TripEndpoint CurrentLocation { get; set; } = currentLocation;
    public ActivityKind CurrentActivity { get; set; } = currentActivity;
    public PersonTravelState TravelState { get; set; } = PersonTravelState.AtActivity;
    public TripEndpoint? Destination { get; set; }
    public ActivityKind? DestinationActivity { get; set; }
    public TripRequestId? ActiveTripRequestId { get; set; }
    public TravelMode? ActiveTravelMode { get; set; }
    public PedestrianId? PedestrianId { get; set; }
    public VehicleId? VehicleId { get; set; }
    public DailyActivityWindow[] Schedule { get; } = schedule;
    public PersonNeed[] Needs { get; } = needs;
}