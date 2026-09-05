namespace MachiVerseWorks.Simulation.Internal;

internal struct DeterministicRandom
{
    private ulong _state;

    public DeterministicRandom(ulong seed)
    {
        _state = seed;
    }

    public readonly ulong State => _state;

    public double NextUnitDouble()
    {
        const double scale = 1d / (1UL << 53);
        return (NextUInt64() >> 11) * scale;
    }

    public double NextDouble(double minInclusive, double maxExclusive)
    {
        return minInclusive + ((maxExclusive - minInclusive) * NextUnitDouble());
    }

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var value = _state;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }
}
