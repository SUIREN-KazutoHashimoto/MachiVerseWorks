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
    double MinZ,
    double MaxX,
    double MaxY,
    double MaxZ) : IProtocolMessage
{
    public SubscribeAreaMessage(double minX, double minY, double maxX, double maxY)
        : this(minX, minY, 0d, maxX, maxY, 0d)
    {
    }

    public MessageType Type => MessageType.SubscribeArea;
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
    public AgentSpawnMessage(
        ulong agentId,
        double x,
        double y,
        double velocityX,
        double velocityY,
        ulong tickCount)
        : this(agentId, x, y, 0d, velocityX, velocityY, 0d, tickCount)
    {
    }

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
    public AgentUpdateMessage(
        ulong agentId,
        double x,
        double y,
        double velocityX,
        double velocityY,
        ulong tickCount)
        : this(agentId, x, y, 0d, velocityX, velocityY, 0d, tickCount)
    {
    }

    public MessageType Type => MessageType.AgentUpdate;
}

public sealed record AgentRemoveMessage(
    ulong AgentId,
    ulong TickCount) : IProtocolMessage
{
    public MessageType Type => MessageType.AgentRemove;
}
