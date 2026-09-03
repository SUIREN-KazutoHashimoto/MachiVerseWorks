using Microsoft.Extensions.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class RemoteMcpRequestGateTests
{
    [TestMethod]
    public void ConcurrencyRejectionsDoNotConsumeMinuteQuota()
    {
        using var gate = CreateGate(maxConcurrent: 1, requestsPerMinute: 2);
        Assert.IsTrue(gate.TryAcquire("credential", out var first, out var firstStatus));
        Assert.AreEqual(200, firstStatus);
        Assert.IsNotNull(first);

        for (var attempt = 0; attempt < 8; attempt++)
        {
            Assert.IsFalse(gate.TryAcquire("credential", out var rejected, out var status));
            Assert.IsNull(rejected);
            Assert.AreEqual(503, status);
        }

        first.Dispose();
        Assert.IsTrue(gate.TryAcquire("credential", out var second, out var secondStatus));
        Assert.AreEqual(200, secondStatus);
        Assert.IsNotNull(second);
        second.Dispose();

        Assert.IsFalse(gate.TryAcquire("credential", out var limited, out var limitedStatus));
        Assert.IsNull(limited);
        Assert.AreEqual(429, limitedStatus);
    }

    [TestMethod]
    public async Task ConcurrentBurstAdmitsOnlyAvailableSlotsWithoutPoisoningQuota()
    {
        using var gate = CreateGate(maxConcurrent: 1, requestsPerMinute: 64);
        Assert.IsTrue(gate.TryAcquire("credential", out var held, out _));
        Assert.IsNotNull(held);

        var attempts = await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            var accepted = gate.TryAcquire("credential", out var lease, out var status);
            lease?.Dispose();
            return (accepted, status);
        })));
        Assert.IsTrue(attempts.All(result => !result.accepted && result.status == 503));

        held.Dispose();
        Assert.IsTrue(gate.TryAcquire("credential", out var retry, out var retryStatus));
        Assert.AreEqual(200, retryStatus);
        retry?.Dispose();
    }

    private static RemoteMcpRequestGate CreateGate(int maxConcurrent, int requestsPerMinute)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Server:Mcp:Enabled"] = "true",
            ["Server:Mcp:ReadToken"] = "read-token-0123456789-0123456789-abcdef",
            ["Server:Mcp:MaxConcurrentRequests"] = maxConcurrent.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Server:Mcp:RequestsPerMinute"] = requestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture),
        }).Build();
        return new RemoteMcpRequestGate(RemoteMcpOptions.Load(configuration));
    }
}
