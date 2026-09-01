namespace MachiVerseWorks.Simulation;

public enum TerrainConstraintKind : byte
{
    Generic = 0,
    Road = 1,
    Railway = 2,
    Building = 3,
}

public readonly record struct TerrainPartitionId(long X, long Y);

public sealed class TerrainSurface
{
    private readonly WorldEnvironmentGenerator _environment;
    private readonly WorldEnvironmentConfig _config;

    public TerrainSurface(WorldEnvironmentGenerator environment, WorldVolume region)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _config = environment.Config;
        Region = region;
    }

    public WorldVolume Region { get; }

    public TerrainSurfaceSample Sample(double x, double y)
    {
        ValidateXY(x, y);
        var height = SampleHeight(x, y);
        var spacing = Math.Max(1d, _config.TerrainDetailScaleMeters / 32d);
        var left = SampleHeight(x - spacing, y);
        var right = SampleHeight(x + spacing, y);
        var down = SampleHeight(x, y - spacing);
        var up = SampleHeight(x, y + spacing);
        var dzdx = (right - left) / (2d * spacing);
        var dzdy = (up - down) / (2d * spacing);
        var length = Math.Sqrt((dzdx * dzdx) + (dzdy * dzdy) + 1d);
        var normal = new WorldVector(-dzdx / length, -dzdy / length, 1d / length);
        var slope = Math.Atan(Math.Sqrt((dzdx * dzdx) + (dzdy * dzdy))) * (180d / Math.PI);
        var environment = _environment.Sample(new WorldPoint(x, y, height));
        var roughness = Math.Clamp((Math.Abs(right - left) + Math.Abs(up - down)) / Math.Max(1d, spacing * 3d), 0d, 1d);
        var water = height <= _config.SeaLevelMeters
            ? SurfaceWaterKind.Ocean
            : environment.Hydrology.SurfaceWater;
        var material = SelectMaterial(height, slope, environment.Climate.MeanAnnualTemperatureCelsius, water);
        return new TerrainSurfaceSample(new WorldPoint(x, y, height), normal, slope, roughness, material, water);
    }

    public double SampleHeight(double x, double y)
    {
        ValidateXY(x, y);
        var broad = _environment.SampleElevation(x, y);
        var detailScale = _config.TerrainDetailScaleMeters;
        var hills = (FractalNoise(x, y, detailScale * 18d, 4, 0x120012UL) - 0.5d) * 180d;
        var ridges = RidgedNoise(x, y, detailScale * 7d, 4, 0x230023UL);
        var micro = (FractalNoise(x, y, detailScale * 1.8d, 3, 0x340034UL) - 0.5d) * 22d;
        var broadEnvironment = _environment.Sample(new WorldPoint(x, y, broad));
        var mountainWeight = Math.Clamp((broadEnvironment.TerrainRuggedness - 0.28d) * 1.65d, 0d, 1d);
        var cliffSignal = Math.Pow(Math.Clamp((ridges - 0.62d) / 0.38d, 0d, 1d), 3d) * mountainWeight;
        var cliff = cliffSignal * 260d;
        var valleyCarve = broadEnvironment.Hydrology.RiverStrength * Math.Pow(1d - ridges, 2d) * 90d;
        return broad + (hills * (0.28d + mountainWeight * 0.72d)) + cliff + micro - valleyCarve;
    }

    private void ValidateXY(double x, double y)
    {
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new ArgumentOutOfRangeException(nameof(x), "Terrain coordinates must be finite.");
        if (Region.Width > 0d && (x < Region.MinX || x > Region.MaxX)) throw new ArgumentOutOfRangeException(nameof(x), x, "X is outside the terrain region.");
        if (Region.Depth > 0d && (y < Region.MinY || y > Region.MaxY)) throw new ArgumentOutOfRangeException(nameof(y), y, "Y is outside the terrain region.");
    }

    private static TerrainMaterialKind SelectMaterial(double height, double slope, double temperature, SurfaceWaterKind water)
    {
        if (temperature < -4d && height > 500d) return TerrainMaterialKind.Snow;
        if (slope > 42d) return TerrainMaterialKind.Rock;
        if (water == SurfaceWaterKind.Ocean) return TerrainMaterialKind.Sand;
        if (water is SurfaceWaterKind.River or SurfaceWaterKind.Tributary) return TerrainMaterialKind.Gravel;
        return TerrainMaterialKind.Soil;
    }

    private double FractalNoise(double x, double y, double scale, int octaves, ulong salt)
    {
        var total = 0d;
        var weight = 1d;
        var totalWeight = 0d;
        for (var octave = 0; octave < octaves; octave++)
        {
            total += ValueNoise(x, y, scale, salt + (ulong)octave * 0x9E3779B97F4A7C15UL) * weight;
            totalWeight += weight;
            scale *= 0.5d;
            weight *= 0.5d;
        }
        return total / totalWeight;
    }

    private double RidgedNoise(double x, double y, double scale, int octaves, ulong salt)
    {
        var value = FractalNoise(x, y, scale, octaves, salt);
        return 1d - Math.Abs((value * 2d) - 1d);
    }

    private double ValueNoise(double x, double y, double scale, ulong salt)
    {
        var gx = x / scale;
        var gy = y / scale;
        var x0 = checked((long)Math.Floor(gx));
        var y0 = checked((long)Math.Floor(gy));
        var tx = Smooth(gx - x0);
        var ty = Smooth(gy - y0);
        var a = ToUnit(Hash(x0, y0, salt));
        var b = ToUnit(Hash(x0 + 1, y0, salt));
        var c = ToUnit(Hash(x0, y0 + 1, salt));
        var d = ToUnit(Hash(x0 + 1, y0 + 1, salt));
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private ulong Hash(long x, long y, ulong salt)
    {
        var value = unchecked((ulong)x) ^ RotateLeft(unchecked((ulong)y), 31) ^ _config.WorldSeed ^ salt;
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    private static double ToUnit(ulong value) => (value >> 11) * (1d / 9_007_199_254_740_992d);
    private static double Smooth(double value) => value * value * (3d - (2d * value));
    private static double Lerp(double left, double right, double amount) => left + ((right - left) * amount);
}

public sealed class TerrainVolume
{
    private readonly TerrainSurface _surface;
    private readonly WorldEnvironmentConfig _config;

    public TerrainVolume(TerrainSurface surface)
    {
        _surface = surface ?? throw new ArgumentNullException(nameof(surface));
        _config = surface.Sample(surface.Region.MinX, surface.Region.MinY) is { } ? GetConfig(surface) : throw new InvalidOperationException();
    }

    public TerrainVolumeSample Sample(WorldPoint position)
    {
        var ground = _surface.SampleHeight(position.X, position.Y);
        var signedDistance = position.Z - ground;
        TerrainMatterKind matter;
        if (position.Z > ground)
        {
            matter = ground < _config.SeaLevelMeters && position.Z <= _config.SeaLevelMeters ? TerrainMatterKind.Water : TerrainMatterKind.Air;
        }
        else if (IsInsideCavity(position.X, position.Y, position.Z, ground))
        {
            matter = TerrainMatterKind.Void;
        }
        else if (ground - position.Z <= 3d)
        {
            matter = TerrainMatterKind.Soil;
        }
        else
        {
            matter = TerrainMatterKind.Rock;
        }
        return new TerrainVolumeSample(position, matter, signedDistance);
    }

    public IReadOnlyList<TerrainSurfaceIntersection> GetSurfaces(double x, double y, double minimumZ, double maximumZ)
    {
        if (!double.IsFinite(minimumZ) || !double.IsFinite(maximumZ) || maximumZ < minimumZ) throw new ArgumentOutOfRangeException(nameof(maximumZ));
        var ground = _surface.Sample(x, y);
        var intersections = new List<TerrainSurfaceIntersection>(4);
        if (ground.Position.Z >= minimumZ && ground.Position.Z <= maximumZ)
            intersections.Add(new TerrainSurfaceIntersection(ground.Position.Z, ground.Normal, ground.Material, true, false, false));
        if (ground.Position.Z < _config.SeaLevelMeters && _config.SeaLevelMeters >= minimumZ && _config.SeaLevelMeters <= maximumZ)
            intersections.Add(new TerrainSurfaceIntersection(_config.SeaLevelMeters, new WorldVector(0d, 0d, 1d), TerrainMaterialKind.Water, false, true, false));

        if (TryGetCavity(x, y, ground.Position.Z, out var cavityCenter, out var cavityRadius))
        {
            var floor = cavityCenter - cavityRadius;
            var ceiling = cavityCenter + cavityRadius;
            if (floor >= minimumZ && floor <= maximumZ)
                intersections.Add(new TerrainSurfaceIntersection(floor, new WorldVector(0d, 0d, 1d), TerrainMaterialKind.Rock, false, false, true));
            if (ceiling >= minimumZ && ceiling <= maximumZ)
                intersections.Add(new TerrainSurfaceIntersection(ceiling, new WorldVector(0d, 0d, -1d), TerrainMaterialKind.Rock, false, false, true));
        }

        intersections.Sort(static (left, right) => left.Z.CompareTo(right.Z));
        return intersections;
    }

    private bool IsInsideCavity(double x, double y, double z, double ground)
    {
        return TryGetCavity(x, y, ground, out var center, out var radius) && z > center - radius && z < center + radius;
    }

    private bool TryGetCavity(double x, double y, double ground, out double center, out double radius)
    {
        var cell = Math.Max(48d, _config.TerrainDetailScaleMeters * 0.3d);
        var cellX = checked((long)Math.Floor(x / cell));
        var cellY = checked((long)Math.Floor(y / cell));
        var selector = ToUnit(Hash(unchecked((ulong)cellX) ^ RotateLeft(unchecked((ulong)cellY), 29) ^ _config.WorldSeed ^ 0xCA7EUL));
        if (selector < 0.84d || ground <= _config.SeaLevelMeters - 100d)
        {
            center = 0d;
            radius = 0d;
            return false;
        }
        var depth = 18d + (ToUnit(Hash(unchecked((ulong)cellX) ^ _config.WorldSeed ^ 0xD337UL)) * 140d);
        radius = 4d + (ToUnit(Hash(unchecked((ulong)cellY) ^ _config.WorldSeed ^ 0xA11FUL)) * 22d);
        center = ground - depth;
        return true;
    }

    private static WorldEnvironmentConfig GetConfig(TerrainSurface surface)
    {
        var field = typeof(TerrainSurface).GetField("_config", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return field?.GetValue(surface) as WorldEnvironmentConfig ?? throw new InvalidOperationException("Terrain surface configuration is unavailable.");
    }

    private static ulong Hash(ulong value)
    {
        value += 0x9E3779B97F4A7C15UL;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        return value ^ (value >> 31);
    }

    private static ulong RotateLeft(ulong value, int count) => (value << count) | (value >> (64 - count));
    private static double ToUnit(ulong value) => (value >> 11) * (1d / 9_007_199_254_740_992d);
}

public sealed class TerrainPartition
{
    public TerrainPartition(TerrainPartitionId id, WorldVolume bounds, TerrainSurface surface)
    {
        Id = id;
        Bounds = bounds;
        Surface = surface ?? throw new ArgumentNullException(nameof(surface));
        Volume = new TerrainVolume(surface);
    }

    public TerrainPartitionId Id { get; }
    public WorldVolume Bounds { get; }
    public TerrainSurface Surface { get; }
    public TerrainVolume Volume { get; }
}

public static class TerrainConstraintEvaluator
{
    public static TerrainConstraintResult Evaluate(TerrainPartition terrain, WorldVolume footprint, TerrainConstraintKind kind)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var maximumAllowedSlope = kind switch
        {
            TerrainConstraintKind.Railway => 4d,
            TerrainConstraintKind.Building => 8d,
            TerrainConstraintKind.Road => 12d,
            _ => 18d,
        };
        var points = new[]
        {
            (footprint.MinX, footprint.MinY),
            (footprint.MaxX, footprint.MinY),
            (footprint.MinX, footprint.MaxY),
            (footprint.MaxX, footprint.MaxY),
            ((footprint.MinX + footprint.MaxX) * 0.5d, (footprint.MinY + footprint.MaxY) * 0.5d),
        };
        var minimumElevation = double.PositiveInfinity;
        var maximumElevation = double.NegativeInfinity;
        var maximumSlope = 0d;
        var intersectsWater = false;
        var intersectsVoid = false;
        foreach (var (x, y) in points)
        {
            var surface = terrain.Surface.Sample(x, y);
            minimumElevation = Math.Min(minimumElevation, surface.Position.Z);
            maximumElevation = Math.Max(maximumElevation, surface.Position.Z);
            maximumSlope = Math.Max(maximumSlope, surface.SlopeDegrees);
            intersectsWater |= surface.SurfaceWater != SurfaceWaterKind.None;
            var foundationPoint = new WorldPoint(x, y, surface.Position.Z - 2d);
            intersectsVoid |= terrain.Volume.Sample(foundationPoint).Matter == TerrainMatterKind.Void;
        }
        var elevationRange = maximumElevation - minimumElevation;
        var allowed = maximumSlope <= maximumAllowedSlope && !intersectsVoid && (kind == TerrainConstraintKind.Generic || !intersectsWater);
        var reason = allowed
            ? "terrain-compatible"
            : intersectsVoid
                ? "foundation-intersects-void"
                : intersectsWater && kind != TerrainConstraintKind.Generic
                    ? "surface-water"
                    : "slope-limit";
        return new TerrainConstraintResult(allowed, maximumSlope, elevationRange, intersectsWater, intersectsVoid, reason);
    }

    public static WorldPoint SnapToGround(TerrainPartition terrain, WorldPoint position)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        var surface = terrain.Surface.Sample(position.X, position.Y);
        return new WorldPoint(position.X, position.Y, surface.Position.Z);
    }
}
