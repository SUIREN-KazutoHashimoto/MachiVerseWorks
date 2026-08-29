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

public sealed record SubscribeVolumeMessage(
    double MinX,
    double MinY,
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ) : IProtocolMessage
{
    public MessageType Type => MessageType.SubscribeVolume;
}

public sealed record AgentSpawnMessage(
    ulong AgentId,
    double X,
    double Y,
    double Z,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentSpawn;
}

public sealed record AgentUpdateMessage(
    ulong AgentId,
    double X,
    double Y,
    double Z,
    double VelocityX,
    double VelocityY,
    double VelocityZ,
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
