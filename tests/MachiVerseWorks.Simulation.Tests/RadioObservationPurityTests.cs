using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RadioObservationPurityTests
{
    [TestMethod]
    public void CreateRadioSnapshotDoesNotRecalculateAuthoritativeState()
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

        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var before));
        var solveCountBeforeObservation = solver.SolveCount;
        Assert.IsTrue(solveCountBeforeObservation > 0);

        _ = world.CreateRadioSnapshot();

        Assert.AreEqual(solveCountBeforeObservation, solver.SolveCount);
        Assert.IsTrue(world.TryGetRadioLinkSnapshot(link, out var after));
        Assert.AreEqual(before, after);

        world.Step();

        Assert.IsTrue(solver.SolveCount > solveCountBeforeObservation);
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
