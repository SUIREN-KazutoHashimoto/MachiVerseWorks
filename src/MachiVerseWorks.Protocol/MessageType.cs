namespace MachiVerseWorks.Protocol;

public enum MessageType : ushort
{
    Hello = 1,
    HelloAck = 2,
    SubscribeArea = 3,
    AgentSpawn = 100,
    AgentUpdate = 101,
    AgentRemove = 102,
    Error = 900,
}
