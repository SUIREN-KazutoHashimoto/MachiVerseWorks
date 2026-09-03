using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class RailwayTerminalLifecycleTests
{
    [TestMethod]
    public void CompletedTrainIsObservableForOneStepAndSurvivesCheckpointRoundTrip()
    {
        var world = new SimulationWorld();
        RailwayOperationsFixtures.SeedDeterministic(world);
        TrainSnapshot? completedTrain = null;

        for (var tick = 0; tick < 2400 && completedTrain is null; tick++)
        {
            world.Step();
            completedTrain = world.CreateRailwayOperationsSnapshot().Trains
                .FirstOrDefault(static train => train.State == TrainMovementState.Completed);
        }

        Assert.IsNotNull(completedTrain, "A completed train must be observable before retirement.");
        var terminal = world.CreateRailwayOperationsSnapshot();
        Assert.IsTrue(terminal.Services.Any(service => service.Id == completedTrain.ServiceId && service.State == RailwayServiceState.Completed));

        var restored = SimulationWorld.RestoreCheckpoint(world.CreateCheckpoint());
        Assert.IsTrue(restored.CreateRailwayOperationsSnapshot().Trains.Any(train => train.Id == completedTrain.Id && train.State == TrainMovementState.Completed));

        world.Step();
        restored.Step();

        Assert.IsFalse(world.CreateRailwayOperationsSnapshot().Trains.Any(train => train.Id == completedTrain.Id));
        Assert.IsFalse(restored.CreateRailwayOperationsSnapshot().Trains.Any(train => train.Id == completedTrain.Id));
    }
}
