namespace MachiVerseWorks.Simulation;

public readonly record struct WorldPoint(double X, double Y);

public readonly record struct WorldVector(double X, double Y);

public readonly record struct SpatialCell(int X, int Y);

public readonly record struct WorldRect
{
    public WorldRect(double minX, double minY, double maxX, double maxY)
    {
        if (!double.IsFinite(minX) ||
            !double.IsFinite(minY) ||
            !double.IsFinite(maxX) ||
            !double.IsFinite(maxY))
        {
            throw new ArgumentOutOfRangeException(nameof(minX), "World rectangle coordinates must be finite.");
        }

        if (maxX < minX)
        {
            throw new ArgumentOutOfRangeException(nameof(maxX), maxX, "maxX must be greater than or equal to minX.");
        }

        if (maxY < minY)
        {
            throw new ArgumentOutOfRangeException(nameof(maxY), maxY, "maxY must be greater than or equal to minY.");
        }

        MinX = minX;
        MinY = minY;
        MaxX = maxX;
        MaxY = maxY;
    }

    public double MinX { get; }

    public double MinY { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public double Width => MaxX - MinX;

    public double Height => MaxY - MinY;

    public bool Contains(WorldPoint point)
    {
        return point.X >= MinX &&
            point.X <= MaxX &&
            point.Y >= MinY &&
            point.Y <= MaxY;
    }
}

public static class SpatialGrid
{
    public static SpatialCell ToCell(WorldPoint point, double cellSize)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y))
        {
            throw new ArgumentOutOfRangeException(nameof(point), "World coordinates must be finite.");
        }

        if (!double.IsFinite(cellSize) || cellSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be finite and greater than zero.");
        }

        var cellX = Math.Floor(point.X / cellSize);
        var cellY = Math.Floor(point.Y / cellSize);

        if (cellX < int.MinValue || cellX > int.MaxValue || cellY < int.MinValue || cellY > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "World coordinates exceed the supported spatial grid range.");
        }

        return new SpatialCell((int)cellX, (int)cellY);
    }
}
