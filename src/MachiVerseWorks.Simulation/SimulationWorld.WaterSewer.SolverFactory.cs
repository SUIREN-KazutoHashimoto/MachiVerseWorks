namespace MachiVerseWorks.Simulation;

public sealed partial class SimulationWorld
{
    private static IWaterSupplySolver CreateWaterSupplySolver(IWaterSupplySolver? solver) =>
        new ValidatingWaterSupplySolver(solver ?? new CapacityWaterSupplySolver());

    private static ISewerSolver CreateSewerSolver(ISewerSolver? solver) =>
        new ValidatingSewerSolver(solver ?? new CapacitySewerSolver());
}
