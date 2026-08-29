namespace MachiVerseWorks.Simulation;

public readonly record struct AgentId(ulong Value)
{
    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
