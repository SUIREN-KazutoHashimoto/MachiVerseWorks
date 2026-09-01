namespace MachiVerseWorks.Simulation;

public sealed class DeterministicRadioPropagationSolver(IRadioPathCorrection? pathCorrection = null) : IRadioPropagationSolver
{
    private readonly IRadioPathCorrection _pathCorrection = pathCorrection ?? new NoRadioPathCorrection();

    public RadioPropagationResult Solve(RadioPropagationRequest request)
    {
        RadioValidation.ValidatePropagationRequest(request);
        var distanceMeters = CalculateDistanceMeters(request.Transmitter.Position, request.Receiver.Position);
        var distanceKilometers = Math.Max(distanceMeters / 1_000d, 0.001d);
        var frequencyMegahertz = request.FrequencyBlock.CenterFrequencyMegahertz;
        var freeSpacePathLossDb = 32.44d + (20d * Math.Log10(distanceKilometers)) + (20d * Math.Log10(frequencyMegahertz));
        var frequencyDependentLossDb = CalculateFrequencyDependentAttenuationDb(frequencyMegahertz, distanceKilometers);
        var correctionDb = _pathCorrection.CalculateAdditionalLossDb(request, distanceMeters);
        if (!double.IsFinite(correctionDb) || correctionDb < 0d)
            throw new InvalidOperationException("Radio path correction must return a finite non-negative loss.");

        var pathLossDb = freeSpacePathLossDb + frequencyDependentLossDb + correctionDb;
        var transmitter = request.LinkBudget.Transmitter;
        var receivedPowerDbm = transmitter.EffectiveRadiatedPower.Dbm
            - transmitter.FeederLossDb
            - transmitter.MiscellaneousLossDb
            - pathLossDb
            + request.Receiver.AntennaGainDb
            + request.LinkBudget.ReceiveAntennaGainDb;
        var effectiveNoiseDbm = CombinePowersDbm(request.NoiseFloorDbm, request.InterferenceDbm);
        var sinrDb = receivedPowerDbm - effectiveNoiseDbm;
        var minimumReceiveDbm = request.LinkBudget.ReceiverSensitivityDbm + request.LinkBudget.FadeMarginDb;
        var reachable = receivedPowerDbm >= minimumReceiveDbm && sinrDb >= RadioDefaults.MinimumSinrDb;
        return new RadioPropagationResult(distanceMeters, pathLossDb, receivedPowerDbm, request.InterferenceDbm, sinrDb, reachable);
    }

    private static double CalculateDistanceMeters(WorldPoint left, WorldPoint right)
    {
        var dx = left.X - right.X;
        var dy = left.Y - right.Y;
        var dz = left.Z - right.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    private static double CalculateFrequencyDependentAttenuationDb(double frequencyMegahertz, double distanceKilometers)
    {
        // Deterministic MVP correction. Higher microwave/mmWave frequencies receive a small
        // additional distance-proportional atmospheric penalty while sub-6 GHz remains neutral.
        if (frequencyMegahertz <= 6_000d) return 0d;
        var normalized = Math.Min(1d, (frequencyMegahertz - 6_000d) / 54_000d);
        return normalized * distanceKilometers * 0.8d;
    }

    private static double CombinePowersDbm(double firstDbm, double secondDbm)
    {
        var firstMilliwatts = Math.Pow(10d, firstDbm / 10d);
        var secondMilliwatts = Math.Pow(10d, secondDbm / 10d);
        return 10d * Math.Log10(firstMilliwatts + secondMilliwatts);
    }
}
