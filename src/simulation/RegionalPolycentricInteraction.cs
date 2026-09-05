namespace MachiVerseWorks.Simulation;

public enum RegionalInteractionMode : byte
{
    Competition = 0,
    Complementarity = 1,
    Specialization = 2,
}

public readonly record struct RegionalInteractionProfile(
    SettlementId FirstSettlementId,
    SettlementId SecondSettlementId,
    double Competition,
    double Complementarity,
    double Specialization,
    RegionalInteractionMode DominantMode);

public static class RegionalPolycentricInteractionRules
{
    public static RegionalInteractionProfile Evaluate(
        SettlementEvolutionState first,
        SettlementEvolutionState second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        if (first.SettlementId == second.SettlementId)
            throw new ArgumentException("Regional interaction requires two different settlements.", nameof(second));

        var dx = first.Center.X - second.Center.X;
        var dy = first.Center.Y - second.Center.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        var combinedInfluence = Math.Max(1d, first.InfluenceRadiusMeters + second.InfluenceRadiusMeters);
        var proximity = Math.Clamp(1d - distance / (combinedInfluence * 2d), 0d, 1d);

        var firstJobIntensity = Math.Clamp(first.Jobs / Math.Max(1d, first.Population + first.Jobs), 0d, 1d);
        var secondJobIntensity = Math.Clamp(second.Jobs / Math.Max(1d, second.Population + second.Jobs), 0d, 1d);
        var jobContrast = Math.Abs(firstJobIntensity - secondJobIntensity);
        var serviceContrast = Math.Abs(first.ServiceIndex - second.ServiceIndex);
        var densityContrast = Math.Abs(first.Density - second.Density);
        var functionalContrast = Math.Clamp(jobContrast * 0.55d + serviceContrast * 0.25d + densityContrast * 0.20d, 0d, 1d);

        var scaleSimilarity = 1d - Math.Abs((int)first.Scale - (int)second.Scale) / 4d;
        var serviceSimilarity = 1d - serviceContrast;
        var competition = Math.Clamp(proximity * (0.45d * scaleSimilarity + 0.35d * serviceSimilarity + 0.20d * (1d - jobContrast)), 0d, 1d);

        var firstJobSurplus = Math.Clamp((first.Jobs - first.Population * 0.45d) / Math.Max(1d, first.Population), -1d, 1d);
        var secondJobSurplus = Math.Clamp((second.Jobs - second.Population * 0.45d) / Math.Max(1d, second.Population), -1d, 1d);
        var reciprocalLaborFit = firstJobSurplus * secondJobSurplus < 0d
            ? Math.Clamp(Math.Abs(firstJobSurplus - secondJobSurplus), 0d, 1d)
            : 0d;
        var complementarity = Math.Clamp(proximity * 0.35d + reciprocalLaborFit * 0.40d + functionalContrast * 0.25d, 0d, 1d);

        var specialization = Math.Clamp(functionalContrast * 0.70d + Math.Max(firstJobIntensity, secondJobIntensity) * 0.20d + (1d - scaleSimilarity) * 0.10d, 0d, 1d);
        var dominant = SelectDominant(competition, complementarity, specialization);
        var firstId = first.SettlementId.Value <= second.SettlementId.Value ? first.SettlementId : second.SettlementId;
        var secondId = first.SettlementId.Value <= second.SettlementId.Value ? second.SettlementId : first.SettlementId;
        return new RegionalInteractionProfile(firstId, secondId, competition, complementarity, specialization, dominant);
    }

    private static RegionalInteractionMode SelectDominant(
        double competition,
        double complementarity,
        double specialization)
    {
        if (specialization >= complementarity && specialization >= competition)
            return RegionalInteractionMode.Specialization;
        if (complementarity >= competition)
            return RegionalInteractionMode.Complementarity;
        return RegionalInteractionMode.Competition;
    }
}

public sealed partial class SimulationWorld
{
    public RegionalInteractionProfile[] CreateRegionalInteractionProfileSnapshot()
    {
        EnsurePersistentRegionalEvolution();
        var settlements = _persistentRegionalEvolution!.Settlements
            .Where(static item => item.IsActive)
            .OrderBy(static item => item.SettlementId.Value)
            .ToArray();
        var profiles = new List<RegionalInteractionProfile>(settlements.Length * Math.Max(0, settlements.Length - 1) / 2);
        for (var first = 0; first < settlements.Length; first++)
        {
            for (var second = first + 1; second < settlements.Length; second++)
                profiles.Add(RegionalPolycentricInteractionRules.Evaluate(settlements[first], settlements[second]));
        }
        return profiles.ToArray();
    }
}
