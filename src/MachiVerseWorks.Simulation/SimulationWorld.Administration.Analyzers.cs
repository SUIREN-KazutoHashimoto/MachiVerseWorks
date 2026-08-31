namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private static T[] ReplaceAdminEntry<T>(IReadOnlyList<T> source, Func<T, bool> predicate, T replacement)
    {
        var items = source.ToArray();
        var index = Array.FindIndex(items, item => predicate(item));
        if (index < 0) return items;
        items[index] = replacement;
        return items;
    }
}
