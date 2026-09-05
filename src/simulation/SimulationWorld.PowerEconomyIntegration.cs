namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<CompanyId, double> _powerProductionBaselines = [];

    private bool HasEconomyUtilityConstraints =>
        _powerLoads.Count != 0
        || _waterSewerServicePoints.Count != 0
        || _gasServicePoints.Count != 0;

    private void CapturePowerProductionBaselines()
    {
        if (!HasEconomyUtilityConstraints) return;
        _powerProductionBaselines.Clear();
        foreach (var company in _economyCompanies)
            _powerProductionBaselines[company.Id] = company.ProducedUnits;
    }

    private void ApplyPowerOperationalConstraints()
    {
        if (!HasEconomyUtilityConstraints) return;

        foreach (var company in _economyCompanies)
        {
            var baseline = _powerProductionBaselines.GetValueOrDefault(company.Id, company.ProducedUnits);
            var producedDelta = company.ProducedUnits - baseline;
            if (producedDelta <= PowerDefaults.SupplyEpsilonMegawatts) continue;

            var establishments = _economyEstablishments.Where(item => item.CompanyId == company.Id).ToArray();
            if (establishments.Length == 0) continue;

            var weightedAvailability = 0d;
            var totalWeight = 0d;
            foreach (var establishment in establishments)
            {
                var weight = Math.Max(1, _economyJobs.Where(item => item.EstablishmentId == establishment.Id).Sum(static item => item.RequiredWorkerCount));
                var powerAvailability = GetEstablishmentPowerAvailabilityFactor(establishment.Id);
                var waterSewerAvailability = GetEstablishmentWaterSewerAvailabilityFactor(establishment.Id);
                var gasAvailability = GetEstablishmentGasAvailabilityFactor(establishment.Id);
                weightedAvailability += Math.Min(Math.Min(powerAvailability, waterSewerAvailability), gasAvailability) * weight;
                totalWeight += weight;
            }

            var availability = totalWeight <= 0d ? 1d : Math.Clamp(weightedAvailability / totalWeight, 0d, 1d);
            company.ProducedUnits = baseline + (producedDelta * availability);
        }
    }
}
