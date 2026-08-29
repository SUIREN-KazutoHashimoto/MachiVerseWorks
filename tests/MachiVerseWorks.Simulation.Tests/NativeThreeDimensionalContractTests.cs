using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class NativeThreeDimensionalContractTests
{
    [TestMethod]
    public void PublicGeometryContractHasNoTwoDimensionalCompatibilityTypesOrConstructors()
    {
        var assembly = typeof(WorldPoint).Assembly;

        Assert.IsNull(assembly.GetType("MachiVerseWorks.Simulation.WorldRect"));
        Assert.IsTrue(typeof(WorldPoint).GetConstructors().All(static constructor => constructor.GetParameters().Length == 3));
        Assert.IsTrue(typeof(WorldVector).GetConstructors().All(static constructor => constructor.GetParameters().Length == 3));
        Assert.IsTrue(typeof(SpatialCell).GetConstructors().All(static constructor => constructor.GetParameters().Length == 3));
        Assert.IsTrue(typeof(SimulationWorld).GetMethods().Where(static method => method.Name is "CreateAgents" or "CreateSnapshot")
            .SelectMany(static method => method.GetParameters())
            .All(static parameter => parameter.ParameterType != assembly.GetType("MachiVerseWorks.Simulation.WorldRect")));
    }
}
