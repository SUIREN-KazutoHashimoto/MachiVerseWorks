namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolVersion(ushort Major, ushort Minor)
{
    public static ProtocolVersion Current => new(2, 0);

    public bool CanAccept(ProtocolVersion requestedVersion)
    {
        return requestedVersion.Major == Major && requestedVersion.Minor <= Minor;
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}";
    }
}
