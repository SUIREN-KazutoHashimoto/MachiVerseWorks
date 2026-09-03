using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RadioObservationPurityTests
{
    [TestMethod]
    public void MutationsRefreshAuthoritativeStateWhileSnapshotsRemainPure()
    {
        var solver = new CountingRadioPropagationSolver();
        var world = new SimulationWorld(
            new SimulationConfig(tickRate: 1, seed: 2810),
            radioPropagationSolver: solver);
        var band = world.CreateSpectrumBand("observation-purity", 5_000d, 5_500d);
        var block = world.CreateFrequencyBlock(band, 5_200d, 20d);
        var source = world.CreateRadioSite(new WorldPoint(0d, 0d, 0d), RadioSiteKind.PointToPoint);
        var destination = world.CreateRadioSite(new WorldPoint(1_000d, 0d, 0d), RadioSiteKind.PointToPoint);
        var budget = new RadioLinkBudget(
            new TransmitterPathBudget(new EffectiveRadiatedPower(43d), 0d, 0d),
            ReceiveAntennaGainDb: 10d,
            ReceiverSensitivityDbm: -110d,
            FadeMarginDb: 6d);
        var link = world.CreateRadioLink(source, destination, block, budget, utilization: 0.5d);

        Assert.IsTrue(solver.SolveCount > 0, "Authoritative Radio mutations must refresh derived link state immediately.");
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var before));
        Assert.AreNotEqual(RadioLinkState.Unreachable, before.State);
        var solveCountBeforeObservation = solver.SolveCount;

        _ = world.CreateRadioSnapshot();
        _ = world.CreateRadioSnapshot();

        Assert.AreEqual(solveCountBeforeObservation, solver.SolveCount, "Pure Radio observations must not invoke propagation solving.");
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var after));
        Assert.AreEqual(before, after);

        world.SetRadioLinkUtilization(link, 0.75d);

        Assert.IsTrue(solver.SolveCount > solveCountBeforeObservation, "Radio mutation must synchronously refresh the derived plan.");
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var updated));
        Assert.AreEqual(0.75d, updated.Utilization, 0d);
    }

    private sealed class CountingRadioPropagationSolver : IRadioPropagationSolver
    {
        private readonly DeterministicRadioPropagationSolver _inner = new();

        public int SolveCount { get; private set; }

        public RadioPropagationResult Solve(RadioPropagationRequest request)
        {
            SolveCount++;
            return _inner.Solve(request);
        }
    }
}
