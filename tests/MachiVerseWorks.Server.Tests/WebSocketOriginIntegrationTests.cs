using System.Net.WebSockets;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WebSocketOriginIntegrationTests
{
    [TestMethod]
    public async Task AllowedBrowserOriginCanHandshake()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0);
        using var socket = await host.ConnectWebSocketAsync("http://localhost:5173");

        await ServerTestHost.HandshakeAsync(socket);

        Assert.AreEqual(WebSocketState.Open, socket.State);
    }

    [TestMethod]
    public async Task DisallowedBrowserOriginIsRejectedBeforeUpgrade()
    {
        await using var host = await ServerTestHost.StartAsync(initialAgentCount: 0);

        await Assert.ThrowsExactlyAsync<WebSocketException>(() =>
            host.ConnectWebSocketAsync("https://evil.example"));
    }
}
