namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private PersistentRegionalEvolutionSnapshot ApplyPersistentRegionalWorldChangesWithoutRelationRecording(
        PersistentRegionalEvolutionSnapshot current)
    {
        current = MaterializeRegionalDevelopment(current);
        current = DetectEmergentSettlements(current);
        ApplyRegionalHouseholdMobility(current);
        return current;
    }

    private PersistentRegionalEvolutionSnapshot RecalculatePersistentRegionalRelations(
        PersistentRegionalEvolutionSnapshot previous,
        PersistentRegionalEvolutionSnapshot current)
    {
        EnsurePersistentRegionalRelationIdFloor(previous.Relations);
        if (current.Settlements.Count < 2)
            return current with { Relations = Array.Empty<RegionalRelation>() };

        var interactions = CreateRegionalInteractionSnapshot(current.Settlements);
        var commuting = interactions.CommutingFlows
            .GroupBy(static flow => CanonicalPair(flow.FromSettlementId, flow.ToSettlementId))
            .ToDictionary(static group => group.Key, static group => group.Sum(static flow => flow.WorkerCount));
        var freight = interactions.FreightFlows
            .GroupBy(static flow => CanonicalPair(flow.FromSettlementId, flow.ToSettlementId))
            .ToDictionary(static group => group.Key, static group => group.Sum(static flow => flow.Quantity));
        var catchments = current.ServiceCatchments
            .GroupBy(static item => item.SettlementId)
            .ToDictionary(
                static group => group.Key,
                static group => (
                    Radius: group.Max(static item => item.RadiusMeters),
                    Coverage: group.Max(static item => item.Coverage)));
        var previousRelations = previous.Relations
            .Where(static item => item.IsActive)
            .ToDictionary(
                static item => (CanonicalPair(item.FromSettlementId, item.ToSettlementId), item.Kind),
                static item => item);

        var activeSettlements = current.Settlements
            .Where(static item => item.IsActive)
            .OrderBy(static item => item.SettlementId.Value)
            .ToArray();
        var relations = new List<RegionalRelation>();
        for (var firstIndex = 0; firstIndex < activeSettlements.Length; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < activeSettlements.Length; secondIndex++)
            {
                var first = activeSettlements[firstIndex];
                var second = activeSettlements[secondIndex];
                var pair = CanonicalPair(first.SettlementId, second.SettlementId);
                var distance = Distance2D(first.Center, second.Center);
                var combinedInfluence = Math.Max(1d, first.InfluenceRadiusMeters + second.InfluenceRadiusMeters);
                var proximity = Math.Clamp(1d - distance / (combinedInfluence * 2d), 0d, 1d);

                var workers = commuting.GetValueOrDefault(pair);
                var workerBase = Math.Max(1d, Math.Min(first.Population, second.Population) * 0.05d);
                var commutingScore = Math.Clamp(workers / workerBase, 0d, 1d);

                var freightQuantity = freight.GetValueOrDefault(pair);
                var freightScore = Math.Clamp(Math.Log10(1d + Math.Max(0d, freightQuantity)) / 4d, 0d, 1d);

                var firstCatchment = catchments.GetValueOrDefault(first.SettlementId);
                var secondCatchment = catchments.GetValueOrDefault(second.SettlementId);
                var serviceReach = distance <= Math.Max(firstCatchment.Radius, secondCatchment.Radius);
                var serviceContrast = Math.Abs(first.ServiceIndex - second.ServiceIndex);
                var serviceCoverage = Math.Max(firstCatchment.Coverage, secondCatchment.Coverage);
                var serviceScore = serviceReach
                    ? Math.Clamp(serviceContrast * 0.6d + serviceCoverage * 0.4d, 0d, 1d)
                    : 0d;

                var continuousUrbanArea = distance <= combinedInfluence * 0.70d;
                var densitySupport = Math.Clamp((first.Density + second.Density) * 0.5d, 0d, 1d);
                var metroScore = continuousUrbanArea
                    ? Math.Clamp(
                        proximity * 0.35d
                        + commutingScore * 0.30d
                        + serviceScore * 0.15d
                        + freightScore * 0.10d
                        + densitySupport * 0.10d,
                        0d,
                        1d)
                    : 0d;

                var profile = RegionalPolycentricInteractionRules.Evaluate(first, second);
                var relation = SelectRegionalRelation(
                    metroScore,
                    commutingScore,
                    freightScore,
                    serviceScore,
                    profile);
                if (relation is null) continue;

                var (kind, strength) = relation.Value;
                if (previousRelations.TryGetValue((pair, kind), out var existing))
                {
                    relations.Add(existing with
                    {
                        FromSettlementId = pair.First,
                        ToSettlementId = pair.Second,
                        Strength = strength,
                        IsActive = true,
                    });
                }
                else
                {
                    if (_nextPersistentRegionalRelationId == ulong.MaxValue)
                        throw new OverflowException("Persistent regional Relation ID capacity has been exhausted.");
                    relations.Add(new RegionalRelation(
                        new RegionalRelationId(_nextPersistentRegionalRelationId++),
                        pair.First,
                        pair.Second,
                        kind,
                        strength,
                        true,
                        current.CurrentYear));
                }
            }
        }

        return current with
        {
            Relations = relations
                .OrderBy(static item => item.FromSettlementId.Value)
                .ThenBy(static item => item.ToSettlementId.Value)
                .ThenBy(static item => item.Kind)
                .ToArray(),
        };
    }

    private void EnsurePersistentRegionalRelationIdFloor(IReadOnlyList<RegionalRelation> relations)
    {
        if (relations.Count == 0) return;
        var maximum = relations.Max(static item => item.Id.Value);
        if (maximum == ulong.MaxValue)
            throw new OverflowException("Persistent regional Relation ID capacity has been exhausted.");
        _nextPersistentRegionalRelationId = Math.Max(_nextPersistentRegionalRelationId, maximum + 1UL);
    }

    private static (RegionalRelationKind Kind, double Strength)? SelectRegionalRelation(
        double metroScore,
        double commutingScore,
        double freightScore,
        double serviceScore,
        RegionalInteractionProfile profile)
    {
        if (metroScore >= 0.50d)
            return (RegionalRelationKind.Metro, metroScore);
        if (freightScore >= commutingScore && freightScore >= serviceScore && freightScore >= 0.12d)
            return (RegionalRelationKind.Trade, freightScore);
        if (commutingScore >= serviceScore && commutingScore >= 0.08d)
            return (RegionalRelationKind.Commuting, commutingScore);
        if (serviceScore >= 0.12d)
            return (RegionalRelationKind.Service, serviceScore);

        if (profile.DominantMode == RegionalInteractionMode.Complementarity && profile.Complementarity >= 0.25d)
            return (RegionalRelationKind.Trade, profile.Complementarity * 0.65d);
        if (profile.DominantMode == RegionalInteractionMode.Specialization && profile.Specialization >= 0.30d)
            return (RegionalRelationKind.Service, profile.Specialization * 0.55d);
        return null;
    }

    private static (SettlementId First, SettlementId Second) CanonicalPair(
        SettlementId first,
        SettlementId second) =>
        first.Value <= second.Value ? (first, second) : (second, first);
}
