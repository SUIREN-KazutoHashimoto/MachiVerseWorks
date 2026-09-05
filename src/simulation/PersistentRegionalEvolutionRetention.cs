namespace MachiVerseWorks.Simulation;

internal static class PersistentRegionalEvolutionRetention
{
    public static RegionalEvolutionEvent[] RetainNewest(IEnumerable<RegionalEvolutionEvent> events)
    {
        ArgumentNullException.ThrowIfNull(events);
        var ordered = events.OrderBy(static item => item.Id.Value).ToArray();
        if (ordered.Length <= PersistentRegionalEvolutionLimits.MaximumEventCount) return ordered;
        return ordered[^PersistentRegionalEvolutionLimits.MaximumEventCount..];
    }
}
