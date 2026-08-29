namespace MachiVerseWorks.Protocol;

public readonly record struct ProtocolVersion(ushort Major, ushort Minor)
{
    public static ProtocolVersion Current => new(2, 0);

    public bool CanAccept(ProtocolVersion requestedVersion)
    {
        return requestedVersion.Major == Major && requestedVersion.Minor <= Minor;
    }

    public bool TryNegotiate(ProtocolVersion requestedVersion, out ProtocolVersion negotiatedVersion)
    {
        if (CanAccept(requestedVersion))
        {
            negotiatedVersion = requestedVersion;
            return true;
        }

        negotiatedVersion = default;
        return false;
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}";
    }
}
