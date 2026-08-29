namespace MachiVerseWorks.Simulation.Internal;

internal struct AgentState
{
    public AgentState(AgentId id, WorldPoint position, WorldVector velocity)
    {
        Id = id;
        Position = position;
        Velocity = velocity;
        IsActive = true;
    }

    public AgentId Id { get; }

    public WorldPoint Position { get; set; }

    public WorldVector Velocity { get; }

    public bool IsActive { get; set; }
}
