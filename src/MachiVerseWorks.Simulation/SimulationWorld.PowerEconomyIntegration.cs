namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private readonly Dictionary<CompanyId, double> _powerProductionBaselines = [];

    private void CapturePowerProductionBaselines()
    {
        _powerProductionBaselines.Clear();
        foreach (var company in _economyCompanies)
            _powerProductionBaselines[company.Id] = company.ProducedUnits;
    }

    private void ApplyPowerOperationalConstraints()
    {
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
                weightedAvailability += GetEstablishmentPowerAvailabilityFactor(establishment.Id) * weight;
                totalWeight += weight;
            }

            var availability = totalWeight <= 0d ? 1d : Math.Clamp(weightedAvailability / totalWeight, 0d, 1d);
            company.ProducedUnits = baseline + (producedDelta * availability);
        }
    }
}
