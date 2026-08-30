namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolVersion(ushort Major, ushort Minor)
{
    public static ProtocolVersion Current => new(2, 8);
    public bool SupportsRoadNetwork => Major == 2 && Minor >= 1;
    public bool SupportsPedestrians => Major == 2 && Minor >= 2;
    public bool SupportsVehicles => Major == 2 && Minor >= 3;
    public bool SupportsIntersectionControl => Major == 2 && Minor >= 4;
    public bool SupportsPopulation => Major == 2 && Minor >= 5;
    public bool SupportsRailwayInfrastructure => Major == 2 && Minor >= 6;
    public bool SupportsRailwayOperations => Major == 2 && Minor >= 7;
    public bool SupportsMultimodalTransit => Major == 2 && Minor >= 8;
    public bool CanAccept(ProtocolVersion requestedVersion) => requestedVersion.Major == Major && requestedVersion.Minor <= Minor;
    public bool TryNegotiate(ProtocolVersion requestedVersion, out ProtocolVersion negotiatedVersion)
    {
        if (CanAccept(requestedVersion)) { negotiatedVersion = requestedVersion; return true; }
        negotiatedVersion = default; return false;
    }
    public override string ToString() => $"{Major}.{Minor}";
}
