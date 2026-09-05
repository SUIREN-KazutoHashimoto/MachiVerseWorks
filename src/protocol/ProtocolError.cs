namespace MachiVerseWorks.Protocol;

public enum ProtocolErrorCode : ushort
{
    UnsupportedProtocolVersion = 1,
    InvalidFrame = 2,
    UnknownMessageType = 3,
    InvalidPayload = 4,
    InvalidRequest = 5,
    InternalServerError = 1000,
}

public static class ProtocolErrorParameterKeys
{
    public const string RequestedVersion = "requestedVersion";
    public const string SupportedVersion = "supportedVersion";
    public const string MessageType = "messageType";
    public const string DetailCode = "detailCode";
    public const string Field = "field";
}

public sealed record ProtocolErrorParameter(string Key, string Value);

public sealed record ProtocolErrorMessage(
    ProtocolErrorCode Code,
    IReadOnlyList<ProtocolErrorParameter> Parameters) : IProtocolMessage
{
    public MessageType Type => MessageType.Error;
}
