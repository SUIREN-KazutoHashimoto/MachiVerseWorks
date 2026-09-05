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
        var freeSpacePathLossDb = SaturatingAdd(32.44d, SaturatingAdd(20d * Math.Log10(distanceKilometers), 20d * Math.Log10(frequencyMegahertz)));
        var frequencyDependentLossDb = CalculateFrequencyDependentAttenuationDb(frequencyMegahertz, distanceKilometers);
        var correctionDb = _pathCorrection.CalculateAdditionalLossDb(request, distanceMeters);
        if (!double.IsFinite(correctionDb) || correctionDb < 0d)
            throw new InvalidOperationException("Radio path correction must return a finite non-negative loss.");

        var pathLossDb = SaturatingAdd(freeSpacePathLossDb, frequencyDependentLossDb);
        pathLossDb = SaturatingAdd(pathLossDb, request.ObstructionLossDb);
        pathLossDb = SaturatingAdd(pathLossDb, correctionDb);
        var transmitter = request.LinkBudget.Transmitter;
        var receivedPowerDbm = transmitter.EffectiveRadiatedPower.Dbm;
        receivedPowerDbm = SaturatingSubtract(receivedPowerDbm, transmitter.FeederLossDb);
        receivedPowerDbm = SaturatingSubtract(receivedPowerDbm, transmitter.MiscellaneousLossDb);
        receivedPowerDbm = SaturatingSubtract(receivedPowerDbm, pathLossDb);
        receivedPowerDbm = SaturatingAdd(receivedPowerDbm, request.Receiver.AntennaGainDb);
        receivedPowerDbm = SaturatingAdd(receivedPowerDbm, request.LinkBudget.ReceiveAntennaGainDb);
        var effectiveNoiseDbm = CombinePowersDbm(request.NoiseFloorDbm, request.InterferenceDbm);
        var sinrDb = SaturatingSubtract(receivedPowerDbm, effectiveNoiseDbm);
        var minimumReceiveDbm = SaturatingAdd(request.LinkBudget.ReceiverSensitivityDbm, request.LinkBudget.FadeMarginDb);
        var reachable = receivedPowerDbm >= minimumReceiveDbm;
        return new RadioPropagationResult(distanceMeters, pathLossDb, receivedPowerDbm, request.InterferenceDbm, sinrDb, reachable);
    }

    private static double CalculateDistanceMeters(WorldPoint left, WorldPoint right)
    {
        var dx = AbsoluteDifference(left.X, right.X);
        var dy = AbsoluteDifference(left.Y, right.Y);
        var dz = AbsoluteDifference(left.Z, right.Z);
        var scale = Math.Max(dx, Math.Max(dy, dz));
        if (scale == 0d) return 0d;
        var norm = Math.Sqrt((dx / scale * (dx / scale)) + (dy / scale * (dy / scale)) + (dz / scale * (dz / scale)));
        return scale > double.MaxValue / norm ? double.MaxValue : scale * norm;
    }

    private static double AbsoluteDifference(double left, double right)
    {
        if ((left >= 0d && right < 0d) || (left < 0d && right >= 0d))
        {
            var a = Math.Abs(left);
            var b = Math.Abs(right);
            return a > double.MaxValue - b ? double.MaxValue : a + b;
        }
        return Math.Abs(left - right);
    }

    private static double CalculateFrequencyDependentAttenuationDb(double frequencyMegahertz, double distanceKilometers)
    {
        if (frequencyMegahertz <= 6_000d) return 0d;
        var normalized = Math.Min(1d, (frequencyMegahertz - 6_000d) / 54_000d);
        var scaled = normalized * 0.8d;
        return distanceKilometers > double.MaxValue / scaled ? double.MaxValue : distanceKilometers * scaled;
    }

    internal static double CombinePowersDbm(double firstDbm, double secondDbm)
    {
        var maximum = Math.Max(firstDbm, secondDbm);
        var minimum = Math.Min(firstDbm, secondDbm);
        var delta = SaturatingSubtract(minimum, maximum) / 10d;
        var scaled = delta < -324d ? 0d : Math.Pow(10d, delta);
        return SaturatingAdd(maximum, 10d * Math.Log10(1d + scaled));
    }

    private static double SaturatingAdd(double left, double right)
    {
        if (right > 0d && left > double.MaxValue - right) return double.MaxValue;
        if (right < 0d && left < -double.MaxValue - right) return -double.MaxValue;
        return left + right;
    }

    private static double SaturatingSubtract(double left, double right) =>
        right >= 0d
            ? (left < -double.MaxValue + right ? -double.MaxValue : left - right)
            : (left > double.MaxValue + right ? double.MaxValue : left - right);
}
