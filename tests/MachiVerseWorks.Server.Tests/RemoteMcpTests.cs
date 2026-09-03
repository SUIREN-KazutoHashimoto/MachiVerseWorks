using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using MachiVerseWorks.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RemoteMcpTests
{
    private const string ReadToken = "read-token-0123456789-0123456789-abcdef";
    private const string WriteToken = "write-token-0123456789-0123456789-abcdef";
    private const string DestructiveToken = "destroy-token-0123456789-0123456789-abcdef";
    private static readonly string[] InjectionArguments = ["0", "0", "0\nstop"];
    private static readonly string[] RemoveOneArguments = ["1"];
    private static readonly Action<ILogger, int, string, Exception?> LongLog = LoggerMessage.Define<int, string>(
        LogLevel.Information,
        new EventId(2701, nameof(LongLog)),
        "entry-{Index} {Payload}");

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
    public async Task TrustedBrowserOriginPreflightDoesNotRequireBearerToken()
    {
        await using var host = await StartMcpHostAsync();
        using var client = host.CreateHttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, "/mcp");
        request.Headers.TryAddWithoutValidation("Origin", "https://trusted.example");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Method", "POST");
        request.Headers.TryAddWithoutValidation("Access-Control-Request-Headers", "authorization,content-type");

        using var response = await client.SendAsync(request);
        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);
        Assert.AreEqual("https://trusted.example", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
        Assert.IsTrue(response.Headers.GetValues("Access-Control-Allow-Headers").Single().Contains("Authorization", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task OversizedAndMalformedRequestsAreRejectedWithoutStoppingServer()
    {
        await using var host = await StartMcpHostAsync(new Dictionary<string, string?> { ["Server:Mcp:MaxRequestBytes"] = "4096" });
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        var initialTick = simulation.TickCount;

        using (var oversizedClient = host.CreateHttpClient())
        using (var oversized = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent(new string('x', 5000)) })
        {
            oversized.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadToken);
            try
            {
                using var response = await oversizedClient.SendAsync(oversized);
                Assert.AreEqual(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
            }
            catch (HttpRequestException)
            {
                // Kestrel may terminate an oversized request before a complete 413 response is observable by HttpClient.
            }
        }

        using (var malformedClient = host.CreateHttpClient())
        using (var malformed = new HttpRequestMessage(HttpMethod.Post, "/mcp") { Content = new StringContent("{not-json") })
        {
            malformed.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ReadToken);
            malformed.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            using var response = await malformedClient.SendAsync(malformed);
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
        await AssertMcpProtocolFailureAsync(client.CallToolAsync("simulation_pause", new Dictionary<string, object?>(), cancellationToken: CancellationToken.None));
    }

    [TestMethod]
    public async Task UnknownToolCommandInjectionAndArbitrarySavePathStayInsideAllowlist()
    {
        await using var host = await StartMcpHostAsync();
        var simulation = host.App.Services.GetRequiredService<SimulationRuntime>();
        await using var writeClient = await CreateMcpClientAsync(host, WriteToken);

        await AssertMcpProtocolFailureAsync(writeClient.CallToolAsync("shell_exec", new Dictionary<string, object?> { ["command"] = "stop" }, cancellationToken: CancellationToken.None));

        var injection = await writeClient.CallToolAsync("entity_write", new Dictionary<string, object?>
        {
            ["entity"] = "agent",
            ["operation"] = "add",
            ["arguments"] = InjectionArguments,
        }, cancellationToken: CancellationToken.None);
        AssertStructuredRejection(injection, "invalid_argument");
        Assert.AreEqual(0, simulation.ActiveAgentCount);

        var unsafeSave = await writeClient.CallToolAsync("simulation_save", new Dictionary<string, object?> { ["slot"] = "../../outside" }, cancellationToken: CancellationToken.None);
        AssertStructuredRejection(unsafeSave, "invalid_argument");

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
            ["arguments"] = RemoveOneArguments,
            ["confirm"] = false,
        }, cancellationToken: CancellationToken.None);
        AssertStructuredRejection(unconfirmed, "confirmation_required");
    }

    [TestMethod]
    public async Task MutationAllowlistMatchesSupportedAdministrationOperations()
    {
        await using var host = await StartMcpHostAsync();
        await using var writeClient = await CreateMcpClientAsync(host, WriteToken);

        var unsupportedUpdate = await writeClient.CallToolAsync("entity_write", new Dictionary<string, object?>
        {
            ["entity"] = "formation",
            ["operation"] = "update",
            ["arguments"] = Array.Empty<string>(),
        }, cancellationToken: CancellationToken.None);
        AssertStructuredRejection(unsupportedUpdate, "invalid_argument");
    }

    [TestMethod]
    public async Task EntityQueryRequiresStableIdInsteadOfRemoteFullEnumeration()
    {
        await using var host = await StartMcpHostAsync();
        await using var readClient = await CreateMcpClientAsync(host, ReadToken);

        var missingId = await readClient.CallToolAsync("entity_query", new Dictionary<string, object?> { ["entity"] = "agent" }, cancellationToken: CancellationToken.None);
        Assert.IsTrue(missingId.IsError is true);
    }

    [TestMethod]
    public async Task LogQueryDoesNotExposeGeneralServerLoggerMessages()
    {
        await using var host = await StartMcpHostAsync(new Dictionary<string, string?> { ["Server:Mcp:MaxResultBytes"] = "1024" });
        var loggerFactory = host.App.Services.GetRequiredService<ILoggerFactory>();
        var logger = loggerFactory.CreateLogger("RemoteMcpTests.LongLog");
        var payload = new string('x', 200);
        for (var index = 0; index < 50; index++) LongLog(logger, index, payload, null);

        await using var readClient = await CreateMcpClientAsync(host, ReadToken);
        var result = await readClient.CallToolAsync("logs_query", new Dictionary<string, object?> { ["limit"] = 50 }, cancellationToken: CancellationToken.None);
        Assert.IsFalse(result.IsError is true);
        var structured = GetStructured(result);
        var message = structured.GetProperty("message").GetString();
        Assert.IsNotNull(message);
        using var _ = JsonDocument.Parse(message);
        Assert.IsFalse(message.Contains("entry-", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task SaveOverwriteRequiresDestructiveScopeAndConfirmation()
    {
        var saveDirectory = Path.Combine(Path.GetTempPath(), $"mvw-mcp-save-{Guid.NewGuid():N}");
        try
        {
            await using var host = await StartMcpHostAsync(new Dictionary<string, string?> { ["Server:Mcp:SaveDirectory"] = saveDirectory });
            await using var writeClient = await CreateMcpClientAsync(host, WriteToken);
            var firstSave = await writeClient.CallToolAsync("simulation_save", new Dictionary<string, object?> { ["slot"] = "test" }, cancellationToken: CancellationToken.None);
            Assert.IsFalse(firstSave.IsError is true);
            Assert.IsTrue(GetStructured(firstSave).GetProperty("success").GetBoolean());

            var duplicateSave = await writeClient.CallToolAsync("simulation_save", new Dictionary<string, object?> { ["slot"] = "test" }, cancellationToken: CancellationToken.None);
            AssertStructuredRejection(duplicateSave, "conflict");
            await AssertMcpProtocolFailureAsync(writeClient.CallToolAsync("simulation_save_overwrite", new Dictionary<string, object?> { ["slot"] = "test", ["confirm"] = true }, cancellationToken: CancellationToken.None));

            await using var destructiveClient = await CreateMcpClientAsync(host, DestructiveToken);
            var unconfirmed = await destructiveClient.CallToolAsync("simulation_save_overwrite", new Dictionary<string, object?> { ["slot"] = "test", ["confirm"] = false }, cancellationToken: CancellationToken.None);
            AssertStructuredRejection(unconfirmed, "confirmation_required");
            var confirmed = await destructiveClient.CallToolAsync("simulation_save_overwrite", new Dictionary<string, object?> { ["slot"] = "test", ["confirm"] = true }, cancellationToken: CancellationToken.None);
            Assert.IsFalse(confirmed.IsError is true);
            Assert.IsTrue(GetStructured(confirmed).GetProperty("success").GetBoolean());
        }
        finally
        {
            if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveDirectoryFailureReturnsStableIoError()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            await using var host = await StartMcpHostAsync(new Dictionary<string, string?> { ["Server:Mcp:SaveDirectory"] = filePath });
            await using var writeClient = await CreateMcpClientAsync(host, WriteToken);
            var result = await writeClient.CallToolAsync("simulation_save", new Dictionary<string, object?> { ["slot"] = "test" }, cancellationToken: CancellationToken.None);
            AssertStructuredRejection(result, "io_error");
        }
        finally
        {
            File.Delete(filePath);
        }
    }

    [TestMethod]
    public async Task CanceledAdminRequestIsDiscardedBeforeExecution()
    {
        var queue = new AdminCommandQueue();
        Assert.IsTrue(AdminCommandParser.TryParse("simulation pause", out var canceledCommand, out _));
        Assert.IsTrue(AdminCommandParser.TryParse("status", out var liveCommand, out _));

        using var requestCancellation = new CancellationTokenSource();
        requestCancellation.Cancel();
        var canceledCompletion = new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var liveCompletion = new TaskCompletionSource<AdminCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        Assert.IsTrue(queue.TryWrite(new AdminCommandRequest(canceledCommand!, canceledCompletion, requestCancellation.Token)));
        Assert.IsTrue(queue.TryWrite(new AdminCommandRequest(liveCommand!, liveCompletion)));

        using var readCancellation = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await using var enumerator = queue.ReadAllAsync(readCancellation.Token).GetAsyncEnumerator();
        Assert.IsTrue(await enumerator.MoveNextAsync());
        Assert.AreSame(liveCommand, enumerator.Current.Command);
        Assert.IsTrue(canceledCompletion.Task.IsCanceled);
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
        return McpClient.CreateAsync(transport);
    }

    private static void AssertStructuredRejection(CallToolResult result, string expectedCode)
    {
        Assert.IsFalse(result.IsError is true);
        var structured = GetStructured(result);
        Assert.IsFalse(structured.GetProperty("success").GetBoolean());
        Assert.AreEqual(expectedCode, structured.GetProperty("code").GetString());
    }

    private static JsonElement GetStructured(CallToolResult result)
    {
        Assert.IsTrue(result.StructuredContent.HasValue);
        return result.StructuredContent.Value;
    }

    private static async Task AssertMcpProtocolFailureAsync(ValueTask<CallToolResult> call)
    {
        try
        {
            await call;
            Assert.Fail("Expected an MCP protocol failure.");
        }
        catch (McpProtocolException)
        {
        }
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
