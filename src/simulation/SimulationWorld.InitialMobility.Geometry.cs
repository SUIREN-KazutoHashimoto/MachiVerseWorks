namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private static WorldPoint Interpolate(WorldPoint first, WorldPoint second, double alpha)
    {
        var clamped = Math.Clamp(alpha, 0d, 1d);
        return new WorldPoint(
            first.X + ((second.X - first.X) * clamped),
            first.Y + ((second.Y - first.Y) * clamped),
            first.Z + ((second.Z - first.Z) * clamped));
    }
}
