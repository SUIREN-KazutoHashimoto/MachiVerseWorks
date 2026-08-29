namespace MachiVerseWorks.Simulation;

public readonly record struct BuildingId(ulong Value)
{
    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public readonly record struct PoiId(ulong Value)
{
    public override string ToString()
    {
        return Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public enum BuildingKind : byte
{
    Generic = 0,
    Residential = 1,
    Commercial = 2,
    Industrial = 3,
    Civic = 4,
    MixedUse = 5,
}

public enum PoiKind : byte
{
    Generic = 0,
    Residence = 1,
    Workplace = 2,
    Retail = 3,
    Education = 4,
    Healthcare = 5,
    Recreation = 6,
    Transit = 7,
    Service = 8,
}

public readonly record struct BuildingSnapshot(
    BuildingId Id,
    BuildingKind Kind,
    WorldVolume Bounds);

public readonly record struct PoiSnapshot(
    PoiId Id,
    PoiKind Kind,
    WorldPoint Position,
    BuildingId? BuildingId);
