using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Simulation.Tests;

[TestClass]
public sealed class ProjectSkeletonTests
{
    [TestMethod]
    public void RuntimeExecutesTargetFrameworkTests()
    {
        Assert.AreEqual(10, Environment.Version.Major);
    }
}
