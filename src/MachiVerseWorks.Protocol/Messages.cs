namespace MachiVerseWorks.Protocol;

public interface IProtocolMessage
{
    MessageType Type { get; }
}

public sealed record HelloMessage : IProtocolMessage
{
    public MessageType Type => MessageType.Hello;
}

public sealed record HelloAckMessage(
    ProtocolVersion ProtocolVersion,
    ushort TickRate) : IProtocolMessage
{
    public MessageType Type => MessageType.HelloAck;
}

public sealed record SubscribeAreaMessage(
    double MinX,
    double MinY,
    double MaxX,
    double MaxY) : IProtocolMessage
{
    public MessageType Type => MessageType.SubscribeArea;
}

public sealed record AgentSpawnMessage(
    ulong AgentId,
    double X,
    double Y,
    double VelocityX,
    double VelocityY,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentSpawn;
}

public sealed record AgentUpdateMessage(
    ulong AgentId,
    double X,
    double Y,
    double VelocityX,
    double VelocityY,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentUpdate;
}

public sealed record AgentRemoveMessage(
    ulong AgentId,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentRemove;
}
