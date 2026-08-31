using MachiVerseWorks.Simulation.Internal;

namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private void PlanPopulationAndEconomyTrips(SimulationTime nextTime)
    {
        SynchronizeHouseholdEconomyStates();
        if (_population.PersonCount == 0) return;

        var minuteOfDay = (int)((nextTime.Elapsed.Ticks / TimeSpan.TicksPerMinute) % 1440L);
        var deltaHours = Config.TickDurationSeconds / 3600d;
        for (var index = 0; index < _population.PersonCount; index++)
        {
            var person = _population.GetPersonAt(index);
            UpdateNeeds(person, deltaHours);
            if (person.TravelState != PersonTravelState.AtActivity) continue;

            var desired = SelectEconomyAwareDesiredActivity(person, minuteOfDay);
            if (desired.Destination == person.CurrentLocation)
            {
                person.CurrentActivity = desired.Activity;
                SatisfyNeed(person, desired.Activity, deltaHours);
                continue;
            }

            TryStartPopulationTrip(person, desired.Activity, desired.Destination);
        }
    }

    private (ActivityKind Activity, TripEndpoint Destination) SelectEconomyAwareDesiredActivity(
        PersonState person,
        int minuteOfDay)
    {
        if (TryGetEmploymentWorkplace(person.Id, out var workplace))
        {
            if (minuteOfDay >= EconomyDefaults.WorkStartMinuteOfDay
                && minuteOfDay < EconomyDefaults.WorkEndMinuteOfDay)
            {
                return (ActivityKind.Work, workplace);
            }

            if (person.CurrentActivity == ActivityKind.Work)
            {
                return (ActivityKind.Home, person.Residence);
            }
        }

        return SelectDesiredActivity(person, minuteOfDay);
    }

    private void SynchronizeHouseholdEconomyStates()
    {
        var households = _population.CreateHouseholdCheckpoint();
        for (var index = 0; index < households.Count; index++)
            EnsureHouseholdEconomyState(households[index].Id);
    }
}
