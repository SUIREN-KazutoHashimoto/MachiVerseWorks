namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private static ValidatingWaterSupplySolver CreateWaterSupplySolver(IWaterSupplySolver? solver) =>
        new(solver ?? new CapacityWaterSupplySolver());

    private static ValidatingSewerSolver CreateSewerSolver(ISewerSolver? solver) =>
        new(solver ?? new CapacitySewerSolver());
}
