using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerOptionsOriginTests
{
    private static readonly string[] ExpectedConfiguredOrigins = ["https://client.example:8443"];

    [TestMethod]
    public void ConfiguredWebSocketOriginsOverrideDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins:0"] = "https://client.example:8443/",
            })
            .Build();

        var options = ServerOptions.Load(configuration);

        CollectionAssert.AreEqual(
            ExpectedConfiguredOrigins,
            options.AllowedWebSocketOrigins.ToArray());
    }

    [TestMethod]
    public void InvalidConfiguredWebSocketOriginIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins:0"] = "https://client.example/path",
            })
            .Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }
}
