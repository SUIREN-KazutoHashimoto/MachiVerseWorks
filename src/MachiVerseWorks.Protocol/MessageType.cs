namespace MachiVerseWorks.Protocol;

public enum MessageType : ushort
{
    Hello = 1,
    HelloAck = 2,
    SubscribeVolume = 3,
    AgentSpawn = 100,
    AgentUpdate = 101,
    AgentRemove = 102,
    RoadNetworkSnapshot = 200,
    PedestrianSpawn = 300,
    PedestrianUpdate = 301,
    PedestrianRemove = 302,
    VehicleSpawn = 400,
    VehicleUpdate = 401,
    VehicleRemove = 402,
    IntersectionControlSnapshot = 500,
    Error = 900,
}
