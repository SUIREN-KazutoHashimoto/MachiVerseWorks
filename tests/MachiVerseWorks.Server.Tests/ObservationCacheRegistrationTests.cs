using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationCacheRegistrationTests
{
    [TestMethod]
    public void ObservationCacheIsRegisteredAsGatewaySingleton()
    {
        var services = new ServiceCollection();

        services.AddObservationGateway();

        var descriptor = services.Single(service => service.ServiceType == typeof(ObservationCache));
        Assert.AreEqual(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.AreEqual(typeof(ObservationCache), descriptor.ImplementationType);
    }
}
