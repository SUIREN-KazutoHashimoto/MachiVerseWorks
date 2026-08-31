using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly PopulationStore _population = new();

    public int HouseholdCount => _population.HouseholdCount;
    public int PersonCount => _population.PersonCount;

    public HouseholdId CreateHousehold(TripEndpoint residence)
    {
        ValidateTripEndpoint(residence, nameof(residence));
        return _population.AddHousehold(residence);
    }

    public PersonId CreatePerson(
        HouseholdId householdId,
        PersonDemographics demographics,
        IReadOnlyList<DailyActivityWindow> schedule,
        IReadOnlyList<PersonNeed>? needs = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        ValidatePersonDemographics(demographics);
        if (!_population.TryGetHousehold(householdId, out _))
            throw new ArgumentException($"Household {householdId.Value} does not exist.", nameof(householdId));

        var scheduleCopy = new DailyActivityWindow[schedule.Count];
        for (var index = 0; index < schedule.Count; index++)
        {
            var window = schedule[index];
            ValidateActivityWindow(window, nameof(schedule));
            if (window.Destination is { } destination) ValidateTripEndpoint(destination, nameof(schedule));
            scheduleCopy[index] = window;
        }

        var needCopy = needs is null ? CreateDefaultNeeds(demographics) : CopyAndValidateNeeds(needs, nameof(needs));
        return _population.AddPerson(householdId, demographics, scheduleCopy, needCopy);
    }

    public bool TryGetHouseholdSnapshot(HouseholdId id, out HouseholdSnapshot snapshot)
    {
        if (_population.TryGetHousehold(id, out var state))
        {
            snapshot = new HouseholdSnapshot(state.Id, state.Residence, state.PersonCount);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetPersonSnapshot(PersonId id, out PersonSnapshot snapshot)
    {
        if (_population.TryGetPerson(id, out var state))
        {
            snapshot = CreatePersonSnapshot(state);
            return true;
        }
        snapshot = default;
        return false;
    }

    public bool TryGetPersonDebugSnapshot(PersonId id, out PersonDebugSnapshot? snapshot)
    {
        if (_population.TryGetPerson(id, out var state))
        {
            snapshot = new PersonDebugSnapshot(
                CreatePersonSnapshot(state),
                Array.AsReadOnly(state.Schedule.ToArray()),
                Array.AsReadOnly(state.Needs.ToArray()));
            return true;
        }
        snapshot = null;
        return false;
    }

    public PersonSnapshot[] CreatePersonSnapshot()
    {
        var result = new PersonSnapshot[_population.PersonCount];
        for (var index = 0; index < result.Length; index++) result[index] = CreatePersonSnapshot(_population.GetPersonAt(index));
        return result;
    }

    public PopulationStatistics CreatePopulationStatistics()
    {
        var atActivity = 0;
        var walking = 0;
        var driving = 0;
        var activityCounts = new int[7];
        for (var index = 0; index < _population.PersonCount; index++)
        {
            var person = _population.GetPersonAt(index);
            switch (person.TravelState)
            {
                case PersonTravelState.AtActivity: atActivity++; break;
                case PersonTravelState.Walking: walking++; break;
                case PersonTravelState.Driving: driving++; break;
            }
            activityCounts[(int)person.CurrentActivity]++;
        }
        return new PopulationStatistics(
            _population.HouseholdCount,
            _population.PersonCount,
            atActivity,
            walking,
            driving,
            activityCounts[(int)ActivityKind.Home],
            activityCounts[(int)ActivityKind.Work],
            activityCounts[(int)ActivityKind.Education],
            activityCounts[(int)ActivityKind.Shopping],
            activityCounts[(int)ActivityKind.Healthcare],
            activityCounts[(int)ActivityKind.Recreation],
            activityCounts[(int)ActivityKind.Errand],
            Time.TickCount);
    }

    private void PlanPopulationTrips(SimulationTime nextTime)
    {
        if (_population.PersonCount == 0) return;
        var minuteOfDay = (int)((nextTime.Elapsed.Ticks / TimeSpan.TicksPerMinute) % 1440L);
        var deltaHours = Config.TickDurationSeconds / 3600d;
        for (var index = 0; index < _population.PersonCount; index++)
        {
            var person = _population.GetPersonAt(index);
            UpdateNeeds(person, deltaHours);
            if (person.TravelState != PersonTravelState.AtActivity) continue;

            var desired = SelectDesiredActivity(person, minuteOfDay);
            if (desired.Destination == person.CurrentLocation)
            {
                person.CurrentActivity = desired.Activity;
                SatisfyNeed(person, desired.Activity, deltaHours);
                continue;
            }

            TryStartPopulationTrip(person, desired.Activity, desired.Destination);
        }
    }

    private void CompletePopulationTrips()
    {
        for (var index = 0; index < _population.PersonCount; index++)
        {
            var person = _population.GetPersonAt(index);
            var arrived = false;
            if (person.TravelState == PersonTravelState.Walking && person.PedestrianId is { } pedestrianId)
            {
                arrived = TryGetPedestrianSnapshot(pedestrianId, out var pedestrian) && pedestrian.State == PedestrianMovementState.Arrived;
                if (arrived) RemovePedestrianCore(pedestrianId);
            }
            else if (person.TravelState == PersonTravelState.Driving && person.VehicleId is { } vehicleId)
            {
                arrived = TryGetVehicleSnapshot(vehicleId, out var vehicle) && vehicle.State == VehicleMovementState.Arrived;
                if (arrived) RemoveVehicleCore(vehicleId);
            }
            else if (person.TravelState == PersonTravelState.Transit && person.ActiveTripRequestId is { } transitTripId)
            {
                if (_multimodalTransit.TryGetPassengerForTrip(transitTripId, out var passenger)) arrived = passenger.State == PassengerState.Arrived;
                else if (_multimodalTransit.TryGetTaxiRequestForTrip(transitTripId, out var taxi)) arrived = taxi.State == TaxiRequestState.Completed;
            }

            if (!arrived) continue;
            person.CurrentLocation = person.Destination!.Value;
            person.CurrentActivity = person.DestinationActivity!.Value;
            SatisfyNeed(person, person.CurrentActivity, double.PositiveInfinity);
            person.TravelState = PersonTravelState.AtActivity;
            person.Destination = null;
            person.DestinationActivity = null;
            person.ActiveTripRequestId = null;
            person.ActiveTravelMode = null;
            person.PedestrianId = null;
            person.VehicleId = null;
        }
    }

    private (ActivityKind Activity, TripEndpoint Destination) SelectDesiredActivity(PersonState person, int minuteOfDay)
    {
        var selectedActivity = ActivityKind.Home;
        var selectedDestination = person.Residence;
        var selectedScore = double.NegativeInfinity;
        for (var index = 0; index < person.Schedule.Length; index++)
        {
            var window = person.Schedule[index];
            if (minuteOfDay < window.StartMinuteOfDay || minuteOfDay >= window.EndMinuteOfDay) continue;
            var destination = window.Destination ?? person.Residence;
            var urgency = 1d - GetNeedSatisfaction(person, window.Activity);
            var score = ((int)window.Priority * 2d) + urgency;
            if (score <= selectedScore) continue;
            selectedScore = score;
            selectedActivity = window.Activity;
            selectedDestination = destination;
        }
        return (selectedActivity, selectedDestination);
    }

    private void TryStartPopulationTrip(PersonState person, ActivityKind destinationActivity, TripEndpoint destination)
    {
        var tripId = _population.PeekTripRequestId();
        var request = new TripRequest(tripId, person.CurrentLocation, destination, TravelMode.Any);
        ModeChoiceDecision decision;
        bool started;
        try
        {
            decision = ChooseMode(request, person.Demographics.HasPrivateVehicle);
        }
        catch (InvalidOperationException)
        {
            started = TryStartWalkingTrip(person, destinationActivity, destination, tripId);
            if (started) _population.CommitTripRequestId(tripId);
            return;
        }

        started = decision.Mode switch
        {
            TransitMode.Motor => TryStartMotorTrip(person, destinationActivity, destination, tripId),
            TransitMode.Taxi => TryStartTaxiTrip(person, destinationActivity, destination, tripId),
            TransitMode.Bus or TransitMode.Railway => TryStartTransitTrip(person, destinationActivity, destination, tripId, decision.JourneyId),
            _ => false,
        };
        if (!started) started = TryStartWalkingTrip(person, destinationActivity, destination, tripId);
        if (started) _population.CommitTripRequestId(tripId);
    }

    private bool TryStartMotorTrip(PersonState person, ActivityKind destinationActivity, TripEndpoint destination, TripRequestId tripId)
    {
        var origins = _roads.GetAccessPoints(person.CurrentLocation, RoadAccessMode.Motor);
        var destinations = _roads.GetAccessPoints(destination, RoadAccessMode.Motor);
        RouteResult? selectedRoute = null;
        RoadAccessPointId selectedOrigin = default;
        RoadAccessPointId selectedDestination = default;
        for (var originIndex = 0; originIndex < origins.Length; originIndex++)
        {
            var originAccess = origins[originIndex];
            if (!_roads.TryGetAccessPointPosition(originAccess.Id, out var originPosition)) continue;
            for (var destinationIndex = 0; destinationIndex < destinations.Length; destinationIndex++)
            {
                var destinationAccess = destinations[destinationIndex];
                if (!_roads.TryGetAccessPointPosition(destinationAccess.Id, out var destinationPosition)) continue;
                RouteResult route;
                try
                {
                    route = FindRoadRoute(new RouteRequest(originPosition, destinationPosition, RoutingCostMetric.EstimatedTravelTime));
                }
                catch (InvalidOperationException)
                {
                    continue;
                }
                if (route.Steps.Count == 0) continue;
                if (selectedRoute is null
                    || route.Cost < selectedRoute.Cost
                    || (route.Cost == selectedRoute.Cost && originAccess.Id.Value < selectedOrigin.Value)
                    || (route.Cost == selectedRoute.Cost && originAccess.Id == selectedOrigin && destinationAccess.Id.Value < selectedDestination.Value))
                {
                    selectedRoute = route;
                    selectedOrigin = originAccess.Id;
                    selectedDestination = destinationAccess.Id;
                }
            }
        }
        if (selectedRoute is null) return false;

        VehicleId vehicleId;
        try
        {
            vehicleId = CreateVehicle(selectedRoute);
        }
        catch (InvalidOperationException)
        {
            // A temporarily occupied spawn lane is a normal dispatch failure. The caller may fall back to another mode.
            return false;
        }
        BeginTrip(person, destinationActivity, destination, tripId, TravelMode.Motor);
        person.VehicleId = vehicleId;
        return true;
    }

    private bool TryStartWalkingTrip(PersonState person, ActivityKind destinationActivity, TripEndpoint destination, TripRequestId tripId)
    {
        var request = new TripRequest(tripId, person.CurrentLocation, destination, TravelMode.Foot);
        PedestrianId pedestrianId;
        try
        {
            pedestrianId = CreatePedestrian(request);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        BeginTrip(person, destinationActivity, destination, tripId, TravelMode.Foot);
        person.PedestrianId = pedestrianId;
        return true;
    }

    private bool TryStartTransitTrip(PersonState person, ActivityKind destinationActivity, TripEndpoint destination, TripRequestId tripId, JourneyId? plannedJourneyId)
    {
        JourneyId journeyId;
        try { journeyId = plannedJourneyId ?? PlanMultimodalJourney(new TripRequest(tripId, person.CurrentLocation, destination)); }
        catch (InvalidOperationException) { return false; }
        CreatePassenger(tripId, journeyId);
        BeginTrip(person, destinationActivity, destination, tripId, TravelMode.Transit);
        return true;
    }

    private bool TryStartTaxiTrip(PersonState person, ActivityKind destinationActivity, TripEndpoint destination, TripRequestId tripId)
    {
        if (!TryResolveRoadAccessPosition(person.CurrentLocation, RoadAccessMode.Motor, out var pickup)
            || !TryResolveRoadAccessPosition(destination, RoadAccessMode.Motor, out var dropOff)) return false;
        CreateTaxiRequest(tripId, pickup, dropOff);
        BeginTrip(person, destinationActivity, destination, tripId, TravelMode.Transit);
        return true;
    }

    private static void BeginTrip(
        PersonState person,
        ActivityKind destinationActivity,
        TripEndpoint destination,
        TripRequestId tripRequestId,
        TravelMode mode)
    {
        person.Destination = destination;
        person.DestinationActivity = destinationActivity;
        person.ActiveTripRequestId = tripRequestId;
        person.ActiveTravelMode = mode;
        person.TravelState = mode switch { TravelMode.Motor => PersonTravelState.Driving, TravelMode.Transit => PersonTravelState.Transit, _ => PersonTravelState.Walking };
    }

    private bool TryResolveRoadAccessPosition(TripEndpoint endpoint, RoadAccessMode mode, out WorldPoint position)
    {
        var candidates = _roads.GetAccessPoints(endpoint, mode);
        for (var index = 0; index < candidates.Length; index++)
            if (_roads.TryGetAccessPointPosition(candidates[index].Id, out position)) return true;
        position = default;
        return false;
    }

    private PersonSnapshot CreatePersonSnapshot(PersonState state) => new(
        state.Id,
        state.HouseholdId,
        state.Demographics,
        state.Residence,
        state.CurrentLocation,
        state.CurrentActivity,
        state.TravelState,
        state.Destination,
        state.DestinationActivity,
        state.ActiveTripRequestId,
        state.ActiveTravelMode,
        state.PedestrianId,
        state.VehicleId,
        Time.TickCount);

    private static void UpdateNeeds(PersonState person, double deltaHours)
    {
        for (var index = 0; index < person.Needs.Length; index++)
        {
            var need = person.Needs[index];
            var satisfaction = Math.Max(0d, need.Satisfaction - (need.DecayPerHour * deltaHours));
            person.Needs[index] = need with { Satisfaction = satisfaction };
        }
        if (person.TravelState == PersonTravelState.AtActivity) SatisfyNeed(person, person.CurrentActivity, deltaHours);
    }

    private static void SatisfyNeed(PersonState person, ActivityKind activity, double deltaHours)
    {
        var kind = ToNeedKind(activity);
        for (var index = 0; index < person.Needs.Length; index++)
        {
            var need = person.Needs[index];
            if (need.Kind != kind) continue;
            var satisfaction = double.IsPositiveInfinity(deltaHours)
                ? 1d
                : Math.Min(1d, need.Satisfaction + (0.25d * deltaHours));
            person.Needs[index] = need with { Satisfaction = satisfaction };
            return;
        }
    }

    private static double GetNeedSatisfaction(PersonState person, ActivityKind activity)
    {
        var kind = ToNeedKind(activity);
        for (var index = 0; index < person.Needs.Length; index++)
            if (person.Needs[index].Kind == kind) return person.Needs[index].Satisfaction;
        return 1d;
    }

    private static NeedKind ToNeedKind(ActivityKind activity) => activity switch
    {
        ActivityKind.Home => NeedKind.Rest,
        ActivityKind.Work => NeedKind.Work,
        ActivityKind.Education => NeedKind.Education,
        ActivityKind.Shopping => NeedKind.Shopping,
        ActivityKind.Healthcare => NeedKind.Healthcare,
        ActivityKind.Recreation => NeedKind.Recreation,
        ActivityKind.Errand => NeedKind.Errand,
        _ => throw new ArgumentOutOfRangeException(nameof(activity), activity, null),
    };

    private static PersonNeed[] CreateDefaultNeeds(PersonDemographics demographics)
    {
        var result = new List<PersonNeed>(7)
        {
            new(NeedKind.Rest, 1d, 0.03d),
            new(NeedKind.Shopping, 0.8d, 0.01d),
            new(NeedKind.Healthcare, 1d, 0.001d),
            new(NeedKind.Recreation, 0.8d, 0.02d),
            new(NeedKind.Errand, 0.9d, 0.01d),
        };
        if (demographics.IsEmployed) result.Add(new PersonNeed(NeedKind.Work, 0.7d, 0.02d));
        if (demographics.IsStudent) result.Add(new PersonNeed(NeedKind.Education, 0.7d, 0.02d));
        return result.ToArray();
    }

    private static PersonNeed[] CopyAndValidateNeeds(IReadOnlyList<PersonNeed> needs, string parameterName)
    {
        var result = new PersonNeed[needs.Count];
        var seen = new HashSet<NeedKind>();
        for (var index = 0; index < needs.Count; index++)
        {
            var need = needs[index];
            ValidateEnum(need.Kind, parameterName);
            if (!seen.Add(need.Kind)) throw new ArgumentException($"Need {need.Kind} is duplicated.", parameterName);
            if (!double.IsFinite(need.Satisfaction) || need.Satisfaction < 0d || need.Satisfaction > 1d)
                throw new ArgumentOutOfRangeException(parameterName, "Need satisfaction must be finite and between zero and one.");
            if (!double.IsFinite(need.DecayPerHour) || need.DecayPerHour < 0d)
                throw new ArgumentOutOfRangeException(parameterName, "Need decay must be finite and non-negative.");
            result[index] = need;
        }
        return result;
    }

    private static void ValidatePersonDemographics(PersonDemographics demographics)
    {
        if (demographics.AgeYears is < 0 or > 130)
            throw new ArgumentOutOfRangeException(nameof(demographics), demographics.AgeYears, "Person age must be between zero and 130 years.");
    }

    private static void ValidateActivityWindow(DailyActivityWindow window, string parameterName)
    {
        ValidateEnum(window.Activity, parameterName);
        ValidateEnum(window.Priority, parameterName);
        if (window.StartMinuteOfDay >= 1440 || window.EndMinuteOfDay > 1440 || window.StartMinuteOfDay >= window.EndMinuteOfDay)
            throw new ArgumentOutOfRangeException(parameterName, "Daily activity windows must satisfy 0 <= start < end <= 1440.");
        if (window.Activity != ActivityKind.Home && window.Destination is null)
            throw new ArgumentException("Non-home activity windows require a destination Building or POI.", parameterName);
    }

    private static void ValidatePopulationCheckpoint(SimulationCheckpoint checkpoint)
    {
        var households = checkpoint.Households ?? Array.Empty<SimulationHouseholdCheckpoint>();
        var persons = checkpoint.Persons ?? Array.Empty<SimulationPersonCheckpoint>();
        ValidatePopulationNextId(checkpoint.NextHouseholdId, households.Select(static item => item.Id.Value), "Household");
        ValidatePopulationNextId(checkpoint.NextPersonId, persons.Select(static item => item.Id.Value), "Person");
        if (checkpoint.NextTripRequestId == 0) throw new ArgumentOutOfRangeException(nameof(checkpoint), "Next Trip Request ID must be greater than zero.");

        var buildingIds = checkpoint.Buildings.Select(static item => item.Id).ToHashSet();
        var poiIds = checkpoint.Pois.Select(static item => item.Id).ToHashSet();
        var householdResidences = new Dictionary<HouseholdId, TripEndpoint>();
        for (var index = 0; index < households.Count; index++)
        {
            var household = households[index];
            if (household.Id.Value == 0 || !householdResidences.TryAdd(household.Id, household.Residence)) throw new ArgumentException("Household IDs must be non-zero and unique.", nameof(checkpoint));
            ValidateCheckpointEndpoint(household.Residence, buildingIds, poiIds, nameof(checkpoint));
        }

        var personIds = new HashSet<PersonId>();
        var pedestrians = (checkpoint.Pedestrians ?? []).ToDictionary(static item => item.Id);
        var vehicleIds = (checkpoint.Vehicles ?? []).Select(static item => item.Id).ToHashSet();
        ulong maximumTripId = 0;
        for (var index = 0; index < persons.Count; index++)
        {
            var person = persons[index];
            if (person.Id.Value == 0 || !personIds.Add(person.Id)) throw new ArgumentException("Person IDs must be non-zero and unique.", nameof(checkpoint));
            if (!householdResidences.TryGetValue(person.HouseholdId, out var householdResidence)) throw new ArgumentException($"Person {person.Id.Value} references a missing Household.", nameof(checkpoint));
            if (person.Residence != householdResidence) throw new ArgumentException($"Person {person.Id.Value} residence does not match Household {person.HouseholdId.Value} residence.", nameof(checkpoint));
            ValidatePersonDemographics(person.Demographics);
            ValidateCheckpointEndpoint(person.Residence, buildingIds, poiIds, nameof(checkpoint));
            ValidateCheckpointEndpoint(person.CurrentLocation, buildingIds, poiIds, nameof(checkpoint));
            ValidateEnum(person.CurrentActivity, nameof(checkpoint));
            ValidateEnum(person.TravelState, nameof(checkpoint));
            if (person.Destination is { } destination) ValidateCheckpointEndpoint(destination, buildingIds, poiIds, nameof(checkpoint));
            if (person.DestinationActivity is { } destinationActivity) ValidateEnum(destinationActivity, nameof(checkpoint));
            if (person.ActiveTravelMode is { } activeMode) ValidateEnum(activeMode, nameof(checkpoint));
            if (person.Schedule is null || person.Needs is null) throw new ArgumentException($"Person {person.Id.Value} schedule or needs are missing.", nameof(checkpoint));
            for (var scheduleIndex = 0; scheduleIndex < person.Schedule.Count; scheduleIndex++)
            {
                var window = person.Schedule[scheduleIndex];
                ValidateActivityWindow(window, nameof(checkpoint));
                if (window.Destination is { } scheduleDestination) ValidateCheckpointEndpoint(scheduleDestination, buildingIds, poiIds, nameof(checkpoint));
            }
            _ = CopyAndValidateNeeds(person.Needs, nameof(checkpoint));

            ValidatePopulationTravelCheckpoint(person, pedestrians, vehicleIds, checkpoint.MultimodalTransit);
            if (person.ActiveTripRequestId is { } tripId)
            {
                if (tripId.Value == 0) throw new ArgumentException($"Person {person.Id.Value} has a zero active Trip Request ID.", nameof(checkpoint));
                maximumTripId = Math.Max(maximumTripId, tripId.Value);
            }
        }
        if (checkpoint.NextTripRequestId <= maximumTripId)
            throw new ArgumentOutOfRangeException(nameof(checkpoint), checkpoint.NextTripRequestId, "Next Trip Request ID must be greater than active Trip Request IDs.");
    }

    private static void ValidatePopulationTravelCheckpoint(
        SimulationPersonCheckpoint person,
        IReadOnlyDictionary<PedestrianId, SimulationPedestrianCheckpoint> pedestrians,
        IReadOnlySet<VehicleId> vehicleIds,
        MultimodalTransitCheckpoint? transit)
    {
        var hasDestination = person.Destination is not null && person.DestinationActivity is not null;
        switch (person.TravelState)
        {
            case PersonTravelState.AtActivity:
                if (person.Destination is not null || person.DestinationActivity is not null || person.ActiveTripRequestId is not null
                    || person.ActiveTravelMode is not null || person.PedestrianId is not null || person.VehicleId is not null)
                    throw new ArgumentException($"At-activity Person {person.Id.Value} must not contain active trip references.", nameof(person));
                break;
            case PersonTravelState.Walking:
                if (!hasDestination || person.ActiveTripRequestId is not { } walkingTrip || walkingTrip.Value == 0 || person.ActiveTravelMode != TravelMode.Foot
                    || person.PedestrianId is not { } pedestrianId || person.VehicleId is not null
                    || !pedestrians.TryGetValue(pedestrianId, out var pedestrian) || pedestrian.TripRequestId != walkingTrip)
                    throw new ArgumentException($"Walking Person {person.Id.Value} has inconsistent Pedestrian or trip references.", nameof(person));
                break;
            case PersonTravelState.Driving:
                if (!hasDestination || person.ActiveTripRequestId is not { } drivingTrip || drivingTrip.Value == 0 || person.ActiveTravelMode != TravelMode.Motor
                    || person.VehicleId is not { } vehicleId || person.PedestrianId is not null || !vehicleIds.Contains(vehicleId))
                    throw new ArgumentException($"Driving Person {person.Id.Value} has inconsistent Vehicle or trip references.", nameof(person));
                break;
            case PersonTravelState.Transit:
                if (!hasDestination || person.ActiveTripRequestId is not { } transitTrip || transitTrip.Value == 0 || person.ActiveTravelMode != TravelMode.Transit
                    || person.PedestrianId is not null || person.VehicleId is not null || !ContainsTransitTripReference(transit, transitTrip))
                    throw new ArgumentException($"Transit Person {person.Id.Value} has inconsistent multimodal trip references.", nameof(person));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(person), person.TravelState, "Person travel state is not defined.");
        }
    }

    private static bool ContainsTransitTripReference(MultimodalTransitCheckpoint? transit, TripRequestId tripRequestId)
    {
        if (transit is null) return false;
        for (var index = 0; index < transit.Passengers.Count; index++)
            if (transit.Passengers[index].TripRequestId == tripRequestId) return true;
        for (var index = 0; index < transit.TaxiRequests.Count; index++)
            if (transit.TaxiRequests[index].TripRequestId == tripRequestId) return true;
        return false;
    }

    private static void ValidateCheckpointEndpoint(TripEndpoint endpoint, HashSet<BuildingId> buildings, HashSet<PoiId> pois, string parameterName)
    {
        if ((endpoint.BuildingId is null) == (endpoint.PoiId is null)) throw new ArgumentException("Trip endpoint must reference exactly one Building or POI.", parameterName);
        if (endpoint.BuildingId is { } buildingId && !buildings.Contains(buildingId)) throw new ArgumentException($"Trip endpoint references missing Building {buildingId.Value}.", parameterName);
        if (endpoint.PoiId is { } poiId && !pois.Contains(poiId)) throw new ArgumentException($"Trip endpoint references missing POI {poiId.Value}.", parameterName);
    }

    private static void ValidatePopulationNextId(ulong nextId, IEnumerable<ulong> ids, string entityName)
    {
        if (nextId == 0) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {entityName} ID must be greater than zero.");
        ulong maximum = 0;
        foreach (var id in ids) maximum = Math.Max(maximum, id);
        if (nextId <= maximum) throw new ArgumentOutOfRangeException(nameof(nextId), $"Next {entityName} ID must be greater than every stored ID.");
    }
}