using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WebSocketOriginPolicyTests
{
    [TestMethod]
    public void MissingOriginIsAllowedForNonBrowserClients()
    {
        var policy = new WebSocketOriginPolicy(["http://localhost:5173"]);

        Assert.IsTrue(policy.IsAllowed(null));
        Assert.IsTrue(policy.IsAllowed(string.Empty));
    }

    [TestMethod]
    public void OnlyConfiguredBrowserOriginIsAllowed()
    {
        var policy = new WebSocketOriginPolicy(["http://localhost:5173/"]);

        Assert.IsTrue(policy.IsAllowed("http://localhost:5173"));
        Assert.IsFalse(policy.IsAllowed("https://evil.example"));
        Assert.IsFalse(policy.IsAllowed("null"));
    }

    [TestMethod]
    public void ConfiguredOriginWithPathIsRejected()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new WebSocketOriginPolicy(["http://localhost:5173/app"]));
    }
}
