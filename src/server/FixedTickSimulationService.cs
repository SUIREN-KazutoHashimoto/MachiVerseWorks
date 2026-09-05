namespace MachiVerseWorks.Server;

/// <summary>
/// Advances a fresh runtime to one exact simulation tick and leaves it paused. This is opt-in
/// through Simulation:PauseAtTick and is intended for deterministic visual/integration captures.
/// Normal servers continue to use SimulationTickService.
/// </summary>
internal sealed class FixedTickSimulationService(
    SimulationRuntime simulation,
    FixedTickSimulationOptions options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (simulation.TickCount > options.TickCount)
            throw new InvalidOperationException($"Simulation is already past requested pause tick {options.TickCount}.");

        while (simulation.TickCount < options.TickCount)
        {
            cancellationToken.ThrowIfCancellationRequested();
            simulation.Step();
        }

        _ = simulation.Pause();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed record FixedTickSimulationOptions(ulong TickCount);
