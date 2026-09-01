using MachiVerseWorks.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RemoteMcpTestsHttpsReverseProxy
{
    private const string WriteToken = "write-token-0123456789-0123456789-abcdef";

    [TestMethod]
    public async Task HttpsReverseProxyCarriesReadAndWriteToAuthoritativeSimulationRuntime()
    {
        await using var backend = await ServerTestHost.StartAsync(
            initialAgentCount: 0,
            additionalConfiguration: new Dictionary<string, string?>
            {
                ["Server:Mcp:Enabled"] = "true",
                ["Server:Mcp:WriteToken"] = WriteToken,
                ["Server:Console:Enabled"] = "false",
            });
        await using var reverseProxy = await HttpsReverseProxyTestHost.StartAsync(backend.HttpAddress);
        var simulation = backend.App.Services.GetRequiredService<SimulationRuntime>();

        Assert.AreEqual("https", reverseProxy.HttpsAddress.Scheme);
        Assert.AreNotEqual(backend.HttpAddress, reverseProxy.HttpsAddress);

        using var httpsClient = reverseProxy.CreateTrustedHttpClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions
            {
                Endpoint = new Uri(reverseProxy.HttpsAddress, "/mcp"),
                TransportMode = HttpTransportMode.StreamableHttp,
                AdditionalHeaders = new Dictionary<string, string>
                {
                    ["Authorization"] = $"Bearer {WriteToken}",
                },
            },
            httpsClient,
            loggerFactory: null,
            ownsHttpClient: false);
        await using var client = await McpClient.CreateAsync(transport);

        var status = await client.CallToolAsync(
            "server_status",
            new Dictionary<string, object?>(),
            cancellationToken: CancellationToken.None);
        AssertSuccessfulResult(status);

        var pause = await client.CallToolAsync(
            "simulation_pause",
            new Dictionary<string, object?>(),
            cancellationToken: CancellationToken.None);
        AssertSuccessfulResult(pause);
        Assert.IsTrue(simulation.IsPaused);

        var resume = await client.CallToolAsync(
            "simulation_resume",
            new Dictionary<string, object?>(),
            cancellationToken: CancellationToken.None);
        AssertSuccessfulResult(resume);
        Assert.IsFalse(simulation.IsPaused);
    }

    private static void AssertSuccessfulResult(CallToolResult result)
    {
        Assert.IsFalse(result.IsError is true);
        Assert.IsTrue(result.StructuredContent.HasValue);
        var structured = result.StructuredContent.Value;
        Assert.IsTrue(structured.GetProperty("success").GetBoolean());
        Assert.AreEqual("ok", structured.GetProperty("code").GetString());
    }
}
