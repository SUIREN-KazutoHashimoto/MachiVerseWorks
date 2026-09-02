using System.Reflection;
using MachiVerseWorks.Simulation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationRequestBoundaryTests
{
    [TestMethod]
    public void ObservationAndAdministrationRequestsUseDistinctTypeBoundaries()
    {
        Assert.IsFalse(typeof(ObservationRequest).IsAssignableFrom(typeof(AdminCommandRequest)));
        Assert.IsFalse(typeof(AdminCommandRequest).IsAssignableFrom(typeof(ObservationRequest)));
    }

    [TestMethod]
    public void ObservationRequestProcessorDoesNotDependOnSimulationRuntime()
    {
        var constructor = typeof(ObservationRequestProcessor)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single();

        Assert.IsFalse(constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(SimulationRuntime)));
    }
}
