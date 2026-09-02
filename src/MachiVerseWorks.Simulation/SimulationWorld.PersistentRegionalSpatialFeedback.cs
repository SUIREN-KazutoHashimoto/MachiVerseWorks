namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private PersistentRegionalEvolutionSnapshot RecalculatePersistentRegionalSpatialState(PersistentRegionalEvolutionSnapshot source)
    {
        if (source.Settlements.Count == 0) return source;
        var buildings = CreateBuildingSnapshot();
        var roads = CreateRoadNetworkSnapshot();
        var railway = CreateRailwayInfrastructureSnapshot();
        var result = new SettlementEvolutionState[source.Settlements.Count];

        for (var settlementIndex = 0; settlementIndex < source.Settlements.Count; settlementIndex++)
        {
            var settlement = source.Settlements[settlementIndex];
            var weightedX = settlement.Center.X;
            var weightedY = settlement.Center.Y;
            var weightedZ = settlement.Center.Z;
            var weight = 1d;

            foreach (var building in buildings)
            {
                var center = Center(building.Bounds);
                var nearest = FindNearestSettlement(source.Settlements, center);
                if (nearest?.SettlementId != settlement.SettlementId) continue;
                var distance = Distance2D(center, settlement.Center);
                if (distance > settlement.InfluenceRadiusMeters * 1.5d) continue;
                var localWeight = building.Kind switch
                {
                    BuildingKind.MixedUse => 3d,
                    BuildingKind.Commercial => 2.5d,
                    BuildingKind.Residential => 2d,
                    BuildingKind.Industrial => 1.5d,
                    _ => 1d,
                };
                weightedX += center.X * localWeight;
                weightedY += center.Y * localWeight;
                weightedZ += center.Z * localWeight;
                weight += localWeight;
            }

            var observedCenter = new WorldPoint(weightedX / weight, weightedY / weight, weightedZ / weight);
            var center = new WorldPoint(
                settlement.Center.X * 0.75d + observedCenter.X * 0.25d,
                settlement.Center.Y * 0.75d + observedCenter.Y * 0.25d,
                settlement.Center.Z * 0.75d + observedCenter.Z * 0.25d);
            var connectivity = MeasureRegionalConnectivity(center, settlement.InfluenceRadiusMeters, roads, railway);
            var accessibility = Math.Clamp(settlement.Accessibility * 0.55d + connectivity * 0.45d, 0d, 1d);
            var scale = PersistentRegionalEvolutionEngine.Classify(
                settlement.Population,
                settlement.Jobs,
                settlement.ServiceIndex,
                settlement.Density,
                accessibility);
            result[settlementIndex] = settlement with
            {
                Center = center,
                Accessibility = accessibility,
                Scale = scale,
            };
        }

        return source with { Settlements = result };
    }

    private double MeasureRegionalConnectivity(SettlementEvolutionState settlement) =>
        MeasureRegionalConnectivity(
            settlement.Center,
            settlement.InfluenceRadiusMeters,
            CreateRoadNetworkSnapshot(),
            CreateRailwayInfrastructureSnapshot());

    private static double MeasureRegionalConnectivity(
        WorldPoint center,
        double influenceRadiusMeters,
        RoadNetworkSnapshot roads,
        RailwayInfrastructureSnapshot railway)
    {
        var radius = Math.Max(1_000d, influenceRadiusMeters);
        var nearbyRoadNodes = 0;
        var nearbyArterialNodes = 0;
        var roadKinds = roads.Segments.ToDictionary(static item => item.Id, static item => item.Kind);
        var segmentNodes = roads.Segments
            .SelectMany(static segment => new[] { (segment.StartNodeId, segment.Id), (segment.EndNodeId, segment.Id) })
            .GroupBy(static item => item.Item1)
            .ToDictionary(static group => group.Key, static group => group.Select(static item => item.Item2).ToArray());
        foreach (var node in roads.Nodes)
        {
            if (Distance2D(node.Position, center) > radius) continue;
            nearbyRoadNodes++;
            if (segmentNodes.TryGetValue(node.Id, out var segmentIds)
                && segmentIds.Any(id => roadKinds.TryGetValue(id, out var kind) && kind is RoadKind.Arterial or RoadKind.Highway))
            {
                nearbyArterialNodes++;
            }
        }

        var nearbyStations = 0;
        foreach (var station in railway.Stations)
        {
            if (Distance2D(Center(station.Bounds), center) <= radius * 1.5d) nearbyStations++;
        }

        var roadScore = Math.Clamp(nearbyRoadNodes / 12d, 0d, 1d);
        var arterialScore = Math.Clamp(nearbyArterialNodes / 4d, 0d, 1d);
        var railScore = Math.Clamp(nearbyStations / 2d, 0d, 1d);
        return Math.Clamp(roadScore * 0.45d + arterialScore * 0.30d + railScore * 0.25d, 0d, 1d);
    }
}
