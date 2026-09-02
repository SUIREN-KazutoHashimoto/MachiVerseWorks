using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MachiVerseWorks.Server;

public static class ServerApplication
{
    public static WebApplication Build(string[]? args = null, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []);
        configureBuilder?.Invoke(builder);
        var options = ServerOptions.Load(builder.Configuration);
        var mcpOptions = RemoteMcpOptions.Load(builder.Configuration);
        var mcpLogs = new RemoteMcpLogBuffer(mcpOptions.MaxLogEntries);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(options.ListenAddress, options.Port));
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(mcpOptions);
        builder.Services.AddSingleton(mcpLogs);
        builder.Services.AddSingleton<RemoteMcpRequestGate>();
        builder.Services.AddSingleton<RemoteMcpAdminGateway>();
        builder.Services.AddSingleton(new WebSocketOriginPolicy(options.AllowedWebSocketOrigins));
        builder.Services.AddSingleton<SimulationRuntime>();
        builder.Services.AddSingleton<AdminCommandQueue>();
        builder.Services.AddSingleton<E2eMetrics>();
        builder.Services.AddHostedService<LogisticsFixtureHostedService>();
        builder.Services.AddHostedService<PowerFixtureHostedService>();
        builder.Services.AddHostedService<WaterSewerFixtureHostedService>();
        builder.Services.AddHostedService<GasFixtureHostedService>();
        builder.Services.AddHostedService<OpticalFixtureHostedService>();
        builder.Services.AddHostedService<RadioFixtureHostedService>();
        builder.Services.AddHostedService<SimulationTickService>();
        builder.Services.AddHostedService<AdminCommandExecutorV2>();
        builder.Services.AddHostedService<ServerConsoleService>();
        builder.Services.AddObservationGateway();

        if (mcpOptions.Enabled)
        {
            builder.Logging.AddProvider(mcpLogs);
            builder.Services.AddAuthorization(authorization =>
            {
                authorization.AddPolicy(RemoteMcpPolicies.Read, policy => policy.RequireAuthenticatedUser().RequireClaim(RemoteMcpPolicies.ScopeClaim, "read"));
                authorization.AddPolicy(RemoteMcpPolicies.Write, policy => policy.RequireAuthenticatedUser().RequireClaim(RemoteMcpPolicies.ScopeClaim, "write"));
                authorization.AddPolicy(RemoteMcpPolicies.Destructive, policy => policy.RequireAuthenticatedUser().RequireClaim(RemoteMcpPolicies.ScopeClaim, "destructive"));
            });
            builder.Services.AddMcpServer()
                .WithHttpTransport(transport => transport.Stateless = true)
                .AddAuthorizationFilters()
                .WithTools<RemoteMcpTools>();
        }

        var app = builder.Build();
        if (mcpOptions.Enabled)
        {
            app.UseMiddleware<RemoteMcpSecurityMiddleware>();
            app.MapMcp("/mcp");
        }
        app.UseWebSockets(new WebSocketOptions
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30),
            KeepAliveTimeout = TimeSpan.FromSeconds(15),
        });
        app.MapGet("/health", (SimulationRuntime simulation, ClientConnectionRegistry connections) =>
        {
            if (!options.DetailedDiagnosticsAvailable) return Results.Ok(new { status = "ok" });
            return Results.Ok(new
            {
                status = "ok",
                tick = simulation.TickCount,
                paused = simulation.IsPaused,
                agents = simulation.ActiveAgentCount,
                pedestrians = simulation.ActivePedestrianCount,
                vehicles = simulation.ActiveVehicleCount,
                households = simulation.HouseholdCount,
                persons = simulation.PersonCount,
                roadSegments = simulation.RoadSegmentCount,
                trackSegments = simulation.TrackSegmentCount,
                connections = connections.Count,
            });
        });
        app.MapGet("/metrics/e2e", (E2eMetrics metrics) =>
            options.DetailedDiagnosticsAvailable ? Results.Ok(metrics.Capture()) : Results.NotFound());
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var originPolicy = context.RequestServices.GetRequiredService<WebSocketOriginPolicy>();
            var origin = context.Request.Headers["Origin"].ToString();
            if (!originPolicy.IsAllowed(origin))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }
            var connections = context.RequestServices.GetRequiredService<ClientConnectionRegistry>();
            if (connections.Count >= options.MaximumWebSocketConnections)
            {
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
            using var socket = await context.WebSockets.AcceptWebSocketAsync();
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, app.Lifetime.ApplicationStopping);
            var handler = context.RequestServices.GetRequiredService<WebSocketSessionHandler>();
            await handler.HandleAsync(socket, linkedCancellation.Token);
        });
        return app;
    }
}
