using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace MachiVerseWorks.Server;

public static class ServerApplication
{
    public static WebApplication Build(string[]? args = null, Action<WebApplicationBuilder>? configureBuilder = null)
    {
        var builder = WebApplication.CreateBuilder(args ?? []); configureBuilder?.Invoke(builder); var options = ServerOptions.Load(builder.Configuration);
        builder.WebHost.ConfigureKestrel(kestrel => kestrel.Listen(options.ListenAddress, options.Port));
        builder.Services.AddSingleton(options); builder.Services.AddSingleton(new WebSocketOriginPolicy(options.AllowedWebSocketOrigins)); builder.Services.AddSingleton<SimulationRuntime>(); builder.Services.AddSingleton<ClientConnectionRegistry>(); builder.Services.AddSingleton<ClientCommandQueue>(); builder.Services.AddSingleton<E2eMetrics>(); builder.Services.AddSingleton<WebSocketSessionHandler>(); builder.Services.AddHostedService<SimulationTickService>(); builder.Services.AddHostedService<ClientCommandProcessor>(); builder.Services.AddHostedService<SnapshotPublishService>(); builder.Services.AddHostedService<PopulationPublishService>();
        var app = builder.Build(); app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(30), KeepAliveTimeout = TimeSpan.FromSeconds(15) });
        app.MapGet("/health", (SimulationRuntime simulation, ClientConnectionRegistry connections) => Results.Ok(new { status = "ok", tick = simulation.TickCount, agents = simulation.ActiveAgentCount, pedestrians = simulation.ActivePedestrianCount, vehicles = simulation.ActiveVehicleCount, households = simulation.HouseholdCount, persons = simulation.PersonCount, roadSegments = simulation.RoadSegmentCount, connections = connections.Count }));
        app.MapGet("/metrics/e2e", (E2eMetrics metrics) => Results.Ok(metrics.Capture()));
        app.Map("/ws", async context =>
        {
            if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = StatusCodes.Status400BadRequest; return; }
            var originPolicy = context.RequestServices.GetRequiredService<WebSocketOriginPolicy>(); var origin = context.Request.Headers["Origin"].ToString(); if (!originPolicy.IsAllowed(origin)) { context.Response.StatusCode = StatusCodes.Status403Forbidden; return; }
            using var socket = await context.WebSockets.AcceptWebSocketAsync(); using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, app.Lifetime.ApplicationStopping); var handler = context.RequestServices.GetRequiredService<WebSocketSessionHandler>(); await handler.HandleAsync(socket, linkedCancellation.Token);
        });
        return app;
    }
}
