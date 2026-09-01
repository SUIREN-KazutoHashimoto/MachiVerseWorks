using System.Net;
using System.Net.Http.Headers;
using MachiVerseWorks.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol.Client;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RemoteMcpTests
{
    private const string ReadToken = "read-token-0123456789-0123456789-abcdef";
    private const string WriteToken = "write-token-0123456789-0123456789-abcdef";
    private const string DestructiveToken = "destroy-token-0123456789-0123456789-abcdef";

    [TestMethod]
    public async Task McpEndpointIsAbsentByDefault()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0);
        using var client = host.CreateHttpClient();
        using var response = await client.PostAsync("/mcp", new StringContent("{}"));
        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task EnabledMcpRequiresBearerAuthenticationAndAllowedOrigin()
    {
        await using var host = await StartMcpHostAsync();
        using var client = host.CreateHttpClient();
        using (var unauthenticated = await client.PostAsync("/mcp", new StringContent("{}")))
            Assert.AreEqual(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{}") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadToken);
        request.Headers.TryAddWithoutValidation("Origin", "https://untrusted.example");
        using var rejectedOrigin = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.Forbidden, rejectedOrigin.StatusCode);
    }

    [TestMethod]
    public async Task OversizedAndMalformedRequestsAreRejectedWithoutStoppingServer()
    {
        await using var host = await StartMcpHostAsync(new Dictionary<string, string?> { ["Server:Mcp:MaxRequestBytes"] = "4096" });
        using var client = host.CreateHttpClient();
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var initialTick = simulation.TickCount;

        using (var oversized = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(new string('x', 5000)) })
        {
            oversized.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadToken);
            using var response = await client.SendAsync(oversized);
            Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }

        using (var malformed = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{not-json") })
        {
            malformed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadToken);
            malformed.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await client.SendAsync(malformed);
            Assert.IsTrue((int)response.StatusCode is >= 400 and < 500);
        }

        await WaitUntilAsync(() => simulation.TickCount > initialTick, TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task ToolDiscoveryIsFilteredByScope()
    {
        await using var host = await StartMcpHostAsync();
        await using var readClient = await CreateMcpClientAsync(host, ReadToken);
        var readTools = await readClient.ListToolsAsync();
        Assert.IsTrue(readTools.Any(tool => tool.Name == "server_status"));
        Assert.IsFalse(readTools.Any(tool => tool.Name == "simulation_pause"));
        Assert.IsFalse(readTools.Any(tool => tool.Name == "entity_remove"));
        Assert.IsFalse(readTools.Any(tool => tool.Name.Contains("shell", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(readTools.Any(tool => tool.Name.Contains("process", StringComparison.OrdinalIgnoreCase)));

        await using var writeClient = await CreateMcpClientAsync(host, WriteToken);
        var writeTools = await writeClient.ListToolsAsync();
        Assert.IsTrue(writeTools.Any(tool => tool.Name == "simulation_pause"));
        Assert.IsFalse(writeTools.Any(tool => tool.Name == "entity_remove"));

        await using var destructiveClient = await CreateMcpClientAsync(host, DestructiveToken);
        var destructiveTools = await destructiveClient.ListToolsAsync();
        Assert.IsTrue(destructiveTools.Any(tool => tool.Name == "entity_remove"));
    }

    [TestMethod]
    public async Task RemoteMcpReadAndWriteFlowThroughAdministrationQueue()
    {
        await using var host = await StartMcpHostAsync();
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        await using var client = await CreateMcpClientAsync(host, WriteToken);

        var status = await client.CallToolAsync("server_status", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);
        Assert.IsFalse(status.IsError is true);

        var pause = await client.CallToolAsync("simulation_pause", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);
        Assert.IsFalse(pause.IsError is true);
        Assert.IsTrue(simulation.IsPaused);

        var resume = await client.CallToolAsync("simulation_resume", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);
        Assert.IsFalse(resume.IsError is true);
        Assert.IsFalse(simulation.IsPaused);
    }

    [TestMethod]
    public async Task ReadCredentialCannotInvokeWriteToolByName()
    {
        await using var host = await StartMcpHostAsync();
        await using var client = await CreateMcpClientAsync(host, ReadToken);
        var result = await client.CallToolAsync("simulation_pause", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None);
        Assert.IsTrue(result.IsError is true);
    }

    [TestMethod]
    public async Task UnknownToolCommandInjectionAndArbitrarySavePathStayInsideAllowlist()
    {
        await using var host = await StartMcpHostAsync();
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        await using var writeClient = await CreateMcpClientAsync(host, WriteToken);

        var unknown = await writeClient.CallToolAsync("shell_exec", new Dictionary<string, object?> { ["command"] = "stop" }, cancellationToken: CancellationToken.None);
        Assert.IsTrue(unknown.IsError is true);

        var injection = await writeClient.CallToolAsync("entity_write", new Dictionary<string, object?>
        {
            ["entity"] = "agent",
            ["operation"] = "add",
            ["arguments"] = new[] { "0", "0", "0\nstop" },
        }, cancellationToken: CancellationToken.None);
        Assert.IsFalse(injection.IsError is true);

        var unsafeSave = await writeClient.CallToolAsync("simulation_save", new Dictionary<string, object?> { ["slot"] = "../../outside" }, cancellationToken: CancellationToken.None);
        Assert.IsFalse(unsafeSave.IsError is true);

        var tick = simulation.TickCount;
        await WaitUntilAsync(() => simulation.TickCount > tick, TimeSpan.FromSeconds(2));
    }

    [TestMethod]
    public async Task DestructiveMutationRequiresScopeAndExplicitConfirmation()
    {
        await using var host = await StartMcpHostAsync();
        await using var destructiveClient = await CreateMcpClientAsync(host, DestructiveToken);
        var unconfirmed = await destructiveClient.CallToolAsync("entity_remove", new Dictionary<string, object?>
        {
            ["entity"] = "agent",
            ["arguments"] = new[] { "1" },
            ["confirm"] = false,
        }, cancellationToken: CancellationToken.None);
        Assert.IsFalse(unconfirmed.IsError is true);
    }

    private static Task<ServerTestHost> StartMcpHostAsync(IReadOnlyDictionary<string, string?>? overrides = null)
    {
        var configuration = new Dictionary<string, string?>
        {
            ["Server:Mcp:Enabled"] = "true",
            ["Server:Mcp:ReadToken"] = ReadToken,
            ["Server:Mcp:WriteToken"] = WriteToken,
            ["Server:Mcp:DestructiveToken"] = DestructiveToken,
            ["Server:Mcp:AllowedOrigins"] = "https://trusted.example",
            ["Server:Console:Enabled"] = "false",
        };
        if (overrides is not null)
            foreach (var (key, value) in overrides) configuration[key] = value;
        return ServerTestHost.StartAsync(initialAgentCount: 0, additionalConfiguration: configuration);
    }

    private static Task<McpClient> CreateMcpClientAsync(ServerTestHost host, string token)
    {
        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(host.HttpAddress, "/mcp"),
            TransportMode = HttpTransportMode.StreamableHttp,
            AdditionalHeaders = new Dictionary<string, string> { ["Authorization"] = $"Bearer {token}" },
        });
        return McpClient.CreateAsync(transport).AsTask();
    }

    private static async Task WaitUntilAsync(Func<bool> predicate, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!predicate())
        {
            if (DateTime.UtcNow >= deadline) Assert.Fail("Condition was not satisfied before timeout.");
            await Task.Delay(20);
        }
    }
}
