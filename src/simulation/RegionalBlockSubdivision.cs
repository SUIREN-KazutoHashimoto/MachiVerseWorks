namespace MachiVerseWorks.Simulation;

public sealed record RegionalBlockSubdivisionResult(
    DistrictId DistrictId,
    double RoadBearingRadians,
    IReadOnlyList<WorldVolume> Blocks);

public static class RegionalBlockSubdivision
{
    public static RegionalBlockSubdivisionResult Subdivide(
        District district,
        IReadOnlyList<RegionalCorridor> corridors,
        double roadReserveMeters = 18d)
    {
        ArgumentNullException.ThrowIfNull(district);
        ArgumentNullException.ThrowIfNull(corridors);
        if (!double.IsFinite(roadReserveMeters) || roadReserveMeters < 0d)
            throw new ArgumentOutOfRangeException(nameof(roadReserveMeters));

        var center = Center(district.Bounds);
        var nearestRoad = corridors
            .Where(static corridor => corridor.Kind != RegionalCorridorKind.Railway && corridor.Geometry.Count >= 2)
            .OrderBy(corridor => DistanceToPolyline(center, corridor.Geometry))
            .ThenBy(static corridor => corridor.Id.Value)
            .FirstOrDefault();
        var bearing = nearestRoad is null ? 0d : Bearing(nearestRoad.Geometry[0], nearestRoad.Geometry[^1]);

        var horizontal = Math.Abs(Math.Cos(bearing)) >= Math.Abs(Math.Sin(bearing));
        var reserve = Math.Min(
            roadReserveMeters,
            Math.Min(district.Bounds.Width, district.Bounds.Depth) * 0.18d);
        var halfReserve = reserve * 0.5d;
        var midX = center.X;
        var midY = center.Y;
        var blocks = horizontal
            ? SplitAlongHorizontalRoad(district.Bounds, midX, midY, halfReserve)
            : SplitAlongVerticalRoad(district.Bounds, midX, midY, halfReserve);

        return new RegionalBlockSubdivisionResult(district.Id, bearing, blocks);
    }

    private static IReadOnlyList<WorldVolume> SplitAlongHorizontalRoad(
        WorldVolume bounds,
        double midX,
        double midY,
        double halfReserve)
    {
        var southMaxY = Math.Max(bounds.MinY, midY - halfReserve);
        var northMinY = Math.Min(bounds.MaxY, midY + halfReserve);
        return
        [
            CreateBlock(bounds, bounds.MinX, bounds.MinY, midX, southMaxY),
            CreateBlock(bounds, midX, bounds.MinY, bounds.MaxX, southMaxY),
            CreateBlock(bounds, bounds.MinX, northMinY, midX, bounds.MaxY),
            CreateBlock(bounds, midX, northMinY, bounds.MaxX, bounds.MaxY),
        ];
    }

    private static IReadOnlyList<WorldVolume> SplitAlongVerticalRoad(
        WorldVolume bounds,
        double midX,
        double midY,
        double halfReserve)
    {
        var westMaxX = Math.Max(bounds.MinX, midX - halfReserve);
        var eastMinX = Math.Min(bounds.MaxX, midX + halfReserve);
        return
        [
            CreateBlock(bounds, bounds.MinX, bounds.MinY, westMaxX, midY),
            CreateBlock(bounds, bounds.MinX, midY, westMaxX, bounds.MaxY),
            CreateBlock(bounds, eastMinX, bounds.MinY, bounds.MaxX, midY),
            CreateBlock(bounds, eastMinX, midY, bounds.MaxX, bounds.MaxY),
        ];
    }

    private static WorldVolume CreateBlock(WorldVolume source, double minX, double minY, double maxX, double maxY)
    {
        if (maxX <= minX || maxY <= minY)
            return new WorldVolume(source.MinX, source.MinY, source.MinZ, source.MaxX, source.MaxY, source.MaxZ);
        return new WorldVolume(minX, minY, source.MinZ, maxX, maxY, source.MaxZ);
    }

    private static double DistanceToPolyline(WorldPoint point, IReadOnlyList<WorldPoint> geometry)
    {
        var best = double.PositiveInfinity;
        for (var index = 1; index < geometry.Count; index++)
            best = Math.Min(best, DistanceToSegment(point, geometry[index - 1], geometry[index]));
        return best;
    }

    private static double DistanceToSegment(WorldPoint point, WorldPoint start, WorldPoint end)
    {
        var dx = end.X - start.X;
        var dy = end.Y - start.Y;
        var lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 1e-12) return Distance2D(point, start);
        var t = Math.Clamp(((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared, 0d, 1d);
        return Distance2D(point, new WorldPoint(start.X + dx * t, start.Y + dy * t, point.Z));
    }

    private static double Bearing(WorldPoint start, WorldPoint end) =>
        Math.Atan2(end.Y - start.Y, end.X - start.X);

    private static WorldPoint Center(WorldVolume volume) => new(
        (volume.MinX + volume.MaxX) * 0.5d,
        (volume.MinY + volume.MaxY) * 0.5d,
        (volume.MinZ + volume.MaxZ) * 0.5d);

    private static double Distance2D(WorldPoint first, WorldPoint second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt(x * x + y * y);
    }
}
