namespace MachiVerseWorks.Protocol;

public enum MessageType : ushort
{
    Hello = 1,
    HelloAck = 2,
    SubscribeVolume = 3,
    AgentSpawn = 100,
    AgentUpdate = 101,
    AgentRemove = 102,
    Error = 900,
}
