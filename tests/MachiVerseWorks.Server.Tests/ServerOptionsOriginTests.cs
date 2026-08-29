using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ServerOptionsOriginTests
{
    private static readonly string[] ExpectedConfiguredOrigins = ["https://client.example:8443"];

    [TestMethod]
    public void HigherPriorityProviderReplacesEntireWebSocketOriginAllowlist()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins"] =
                    "http://localhost:5173;http://127.0.0.1:5173",
            })
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins"] = "https://client.example:8443/",
            })
            .Build();

        var options = ServerOptions.Load(configuration);

        CollectionAssert.AreEqual(
            ExpectedConfiguredOrigins,
            options.AllowedWebSocketOrigins.ToArray());
    }

    [TestMethod]
    public void EmptyWebSocketOriginAllowlistDisablesBrowserOrigins()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins"] = string.Empty,
            })
            .Build();

        var options = ServerOptions.Load(configuration);

        Assert.AreEqual(0, options.AllowedWebSocketOrigins.Count);
    }

    [TestMethod]
    public void InvalidConfiguredWebSocketOriginIsRejected()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Server:AllowedWebSocketOrigins"] = "https://client.example/path",
            })
            .Build();

        Assert.ThrowsExactly<InvalidOperationException>(() => ServerOptions.Load(configuration));
    }
}
