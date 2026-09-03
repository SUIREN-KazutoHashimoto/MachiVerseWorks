namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly record struct BuildingCenterAccumulator(double WeightedX, double WeightedY, double WeightedZ, double Weight)
    {
        public BuildingCenterAccumulator Add(WorldPoint center, double weight) => new(
            WeightedX + center.X * weight,
            WeightedY + center.Y * weight,
            WeightedZ + center.Z * weight,
            Weight + weight);
    }

    private sealed record RegionalConnectivityIndex(
        IReadOnlyList<(WorldPoint Position, bool IsArterial)> RoadNodes,
        IReadOnlyList<WorldPoint> StationCenters);

    private PersistentRegionalEvolutionSnapshot RecalculatePersistentRegionalSpatialState(PersistentRegionalEvolutionSnapshot source)
    {
        if (source.Settlements.Count == 0) return source;
        var buildings = CreateBuildingSnapshot();
        var roads = CreateRoadNetworkSnapshot();
        var railway = CreateRailwayInfrastructureSnapshot();
        var settlementSpatialIndex = new SettlementSpatialIndex(source.Settlements);
        var buildingCenters = new Dictionary<SettlementId, BuildingCenterAccumulator>();
        foreach (var building in buildings)
        {
            var buildingCenter = Center(building.Bounds);
            var nearest = settlementSpatialIndex.FindNearest(buildingCenter);
            if (nearest is null || Distance2D(buildingCenter, nearest.Center) > nearest.InfluenceRadiusMeters * 1.5d) continue;
            var localWeight = building.Kind switch
            {
                BuildingKind.MixedUse => 3d,
                BuildingKind.Commercial => 2.5d,
                BuildingKind.Residential => 2d,
                BuildingKind.Industrial => 1.5d,
                _ => 1d,
            };
            buildingCenters.TryGetValue(nearest.SettlementId, out var accumulator);
            buildingCenters[nearest.SettlementId] = accumulator.Add(buildingCenter, localWeight);
        }
        var connectivityIndex = CreateRegionalConnectivityIndex(roads, railway);
        var result = new SettlementEvolutionState[source.Settlements.Count];

        for (var settlementIndex = 0; settlementIndex < source.Settlements.Count; settlementIndex++)
        {
            var settlement = source.Settlements[settlementIndex];
            var weightedX = settlement.Center.X;
            var weightedY = settlement.Center.Y;
            var weightedZ = settlement.Center.Z;
            var weight = 1d;
            if (buildingCenters.TryGetValue(settlement.SettlementId, out var accumulator))
            {
                weightedX += accumulator.WeightedX;
                weightedY += accumulator.WeightedY;
                weightedZ += accumulator.WeightedZ;
                weight += accumulator.Weight;
            }

            var observedCenter = new WorldPoint(weightedX / weight, weightedY / weight, weightedZ / weight);
            var evolvedCenter = new WorldPoint(
                settlement.Center.X * 0.75d + observedCenter.X * 0.25d,
                settlement.Center.Y * 0.75d + observedCenter.Y * 0.25d,
                settlement.Center.Z * 0.75d + observedCenter.Z * 0.25d);
            var connectivity = MeasureRegionalConnectivity(evolvedCenter, settlement.InfluenceRadiusMeters, connectivityIndex);
            var accessibility = Math.Clamp(settlement.Accessibility * 0.55d + connectivity * 0.45d, 0d, 1d);
            var scale = PersistentRegionalEvolutionEngine.Classify(
                settlement.Population,
                settlement.Jobs,
                settlement.ServiceIndex,
                settlement.Density,
                accessibility);
            result[settlementIndex] = settlement with
            {
                Center = evolvedCenter,
                Accessibility = accessibility,
                Scale = scale,
            };
        }

        return source with { Settlements = result };
    }

    private double MeasureRegionalConnectivity(SettlementEvolutionState settlement)
    {
        var roads = CreateRoadNetworkSnapshot();
        var railway = CreateRailwayInfrastructureSnapshot();
        return MeasureRegionalConnectivity(
            settlement.Center,
            settlement.InfluenceRadiusMeters,
            CreateRegionalConnectivityIndex(roads, railway));
    }

    private static RegionalConnectivityIndex CreateRegionalConnectivityIndex(
        RoadNetworkSnapshot roads,
        RailwayInfrastructureSnapshot railway)
    {
        var roadKinds = roads.Segments.ToDictionary(static item => item.Id, static item => item.Kind);
        var segmentNodes = roads.Segments
            .SelectMany(static segment => new[] { (segment.StartNodeId, segment.Id), (segment.EndNodeId, segment.Id) })
            .GroupBy(static item => item.Item1)
            .ToDictionary(static group => group.Key, static group => group.Select(static item => item.Item2).ToArray());
        var roadNodes = roads.Nodes.Select(node =>
        {
            var arterial = segmentNodes.TryGetValue(node.Id, out var segmentIds)
                && segmentIds.Any(id => roadKinds.TryGetValue(id, out var kind) && kind is RoadKind.Arterial or RoadKind.Highway);
            return (node.Position, arterial);
        }).ToArray();
        var stationCenters = railway.Stations.Select(static station => Center(station.Bounds)).ToArray();
        return new RegionalConnectivityIndex(roadNodes, stationCenters);
    }

    private static double MeasureRegionalConnectivity(
        WorldPoint center,
        double influenceRadiusMeters,
        RegionalConnectivityIndex index)
    {
        var radius = Math.Max(1_000d, influenceRadiusMeters);
        var nearbyRoadNodes = 0;
        var nearbyArterialNodes = 0;
        foreach (var node in index.RoadNodes)
        {
            if (Distance2D(node.Position, center) > radius) continue;
            nearbyRoadNodes++;
            if (node.IsArterial) nearbyArterialNodes++;
        }

        var nearbyStations = 0;
        foreach (var stationCenter in index.StationCenters)
        {
            if (Distance2D(stationCenter, center) <= radius * 1.5d) nearbyStations++;
        }

        var roadScore = Math.Clamp(nearbyRoadNodes / 12d, 0d, 1d);
        var arterialScore = Math.Clamp(nearbyArterialNodes / 4d, 0d, 1d);
        var railScore = Math.Clamp(nearbyStations / 2d, 0d, 1d);
        return Math.Clamp(roadScore * 0.45d + arterialScore * 0.30d + railScore * 0.25d, 0d, 1d);
    }
}
