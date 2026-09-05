namespace MachiVerseWorks.Simulation;

public sealed record SettlementTerritorySnapshot(
    SettlementId SettlementId,
    WorldPoint Center,
    double TerritoryRadiusMeters,
    double InfluenceRadiusMeters,
    IReadOnlyList<SettlementId> NeighborSettlementIds);

public sealed partial class SimulationWorld
{
    public SettlementTerritorySnapshot[] CreateSettlementTerritorySnapshot()
    {
        EnsurePersistentRegionalEvolution();
        var settlements = _persistentRegionalEvolution!.Settlements
            .Where(static item => item.IsActive)
            .OrderBy(static item => item.SettlementId.Value)
            .ToArray();
        var result = new SettlementTerritorySnapshot[settlements.Length];

        for (var index = 0; index < settlements.Length; index++)
        {
            var settlement = settlements[index];
            var nearestDistance = double.PositiveInfinity;
            var neighbors = new List<SettlementId>();
            for (var otherIndex = 0; otherIndex < settlements.Length; otherIndex++)
            {
                if (index == otherIndex) continue;
                var other = settlements[otherIndex];
                var distance = Distance2D(settlement.Center, other.Center);
                nearestDistance = Math.Min(nearestDistance, distance);
                if (distance <= settlement.InfluenceRadiusMeters + other.InfluenceRadiusMeters)
                    neighbors.Add(other.SettlementId);
            }

            var unconstrainedTerritory = settlement.InfluenceRadiusMeters * 0.75d;
            var territoryRadius = double.IsPositiveInfinity(nearestDistance)
                ? unconstrainedTerritory
                : Math.Min(unconstrainedTerritory, nearestDistance * 0.5d);
            territoryRadius = Math.Clamp(territoryRadius, 250d, settlement.InfluenceRadiusMeters);
            result[index] = new SettlementTerritorySnapshot(
                settlement.SettlementId,
                settlement.Center,
                territoryRadius,
                settlement.InfluenceRadiusMeters,
                neighbors.OrderBy(static id => id.Value).ToArray());
        }

        return result;
    }
}
