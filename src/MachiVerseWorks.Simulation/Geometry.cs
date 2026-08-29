namespace MachiVerseWorks.Simulation;

public readonly record struct WorldPoint
{
    public WorldPoint(double x, double y)
        : this(x, y, 0d)
    {
    }

    public WorldPoint(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "World coordinates must be finite.");
        }
    }
}

public readonly record struct WorldVector
{
    public WorldVector(double x, double y)
        : this(x, y, 0d)
    {
    }

    public WorldVector(double x, double y, double z)
    {
        ValidateFinite(x, nameof(x));
        ValidateFinite(y, nameof(y));
        ValidateFinite(z, nameof(z));
        X = x;
        Y = y;
        Z = z;
    }

    public double X { get; }

    public double Y { get; }

    public double Z { get; }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "World vector components must be finite.");
        }
    }
}

public readonly record struct SpatialCell(int X, int Y, int Z)
{
    public SpatialCell(int x, int y)
        : this(x, y, 0)
    {
    }
}

public readonly record struct WorldRect
{
    public WorldRect(double minX, double minY, double maxX, double maxY)
    {
        ValidateFinite(minX, nameof(minX));
        ValidateFinite(minY, nameof(minY));
        ValidateFinite(maxX, nameof(maxX));
        ValidateFinite(maxY, nameof(maxY));

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

    public WorldVolume ToVolume(double minZ = 0d, double maxZ = 0d)
    {
        return new WorldVolume(MinX, MinY, minZ, MaxX, MaxY, maxZ);
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "World rectangle coordinates must be finite.");
        }
    }
}

public readonly record struct WorldVolume
{
    public WorldVolume(
        double minX,
        double minY,
        double minZ,
        double maxX,
        double maxY,
        double maxZ)
    {
        ValidateFinite(minX, nameof(minX));
        ValidateFinite(minY, nameof(minY));
        ValidateFinite(minZ, nameof(minZ));
        ValidateFinite(maxX, nameof(maxX));
        ValidateFinite(maxY, nameof(maxY));
        ValidateFinite(maxZ, nameof(maxZ));

        if (maxX < minX)
        {
            throw new ArgumentOutOfRangeException(nameof(maxX), maxX, "maxX must be greater than or equal to minX.");
        }

        if (maxY < minY)
        {
            throw new ArgumentOutOfRangeException(nameof(maxY), maxY, "maxY must be greater than or equal to minY.");
        }

        if (maxZ < minZ)
        {
            throw new ArgumentOutOfRangeException(nameof(maxZ), maxZ, "maxZ must be greater than or equal to minZ.");
        }

        MinX = minX;
        MinY = minY;
        MinZ = minZ;
        MaxX = maxX;
        MaxY = maxY;
        MaxZ = maxZ;
    }

    public double MinX { get; }

    public double MinY { get; }

    public double MinZ { get; }

    public double MaxX { get; }

    public double MaxY { get; }

    public double MaxZ { get; }

    public double Width => MaxX - MinX;

    public double Depth => MaxY - MinY;

    public double Height => MaxZ - MinZ;

    public bool Contains(WorldPoint point)
    {
        return point.X >= MinX &&
            point.X <= MaxX &&
            point.Y >= MinY &&
            point.Y <= MaxY &&
            point.Z >= MinZ &&
            point.Z <= MaxZ;
    }

    private static void ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "World volume coordinates must be finite.");
        }
    }
}

public static class SpatialGrid
{
    public static SpatialCell ToCell(WorldPoint point, double cellSize)
    {
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) || !double.IsFinite(point.Z))
        {
            throw new ArgumentOutOfRangeException(nameof(point), "World coordinates must be finite.");
        }

        if (!double.IsFinite(cellSize) || cellSize <= 0d)
        {
            throw new ArgumentOutOfRangeException(nameof(cellSize), cellSize, "Cell size must be finite and greater than zero.");
        }

        var cellX = Math.Floor(point.X / cellSize);
        var cellY = Math.Floor(point.Y / cellSize);
        var cellZ = Math.Floor(point.Z / cellSize);

        if (cellX < int.MinValue || cellX > int.MaxValue ||
            cellY < int.MinValue || cellY > int.MaxValue ||
            cellZ < int.MinValue || cellZ > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(point), "World coordinates exceed the supported spatial grid range.");
        }

        return new SpatialCell((int)cellX, (int)cellY, (int)cellZ);
    }
}
