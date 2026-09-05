using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WebSocketRateLimitPolicyTests
{
    [TestMethod]
    public void RateLimitedHandlerPathUsesRecoverableStrikePolicy()
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "src", "server", "WebSocketSessionHandler.cs");
        var source = File.ReadAllText(sourcePath);
        var rateStart = source.IndexOf("if (!connection.TryConsumeRequest", StringComparison.Ordinal);
        Assert.IsTrue(rateStart >= 0);
        var observationCheck = source.IndexOf("if (envelope.Message is not IObservationRequestMessage)", rateStart, StringComparison.Ordinal);
        Assert.IsTrue(observationCheck > rateStart);
        var rateBlock = source[rateStart..observationCheck];
        StringAssert.Contains(rateBlock, "ProtocolErrorParameterKeys.DetailCode, \"rateLimited\"");
        StringAssert.Contains(rateBlock, "RejectRecoverableAsync");
        Assert.IsFalse(rateBlock.Contains("return true;", StringComparison.Ordinal));

        var rejectStart = source.IndexOf("private async Task<bool> RejectRecoverableAsync", StringComparison.Ordinal);
        Assert.IsTrue(rejectStart >= 0);
        var rejectBlock = source[rejectStart..];
        StringAssert.Contains(rejectBlock, "RegisterInvalidRequest(options.InvalidRequestStrikeLimit, options.InvalidRequestStrikeWindow)");
        StringAssert.Contains(rejectBlock, "WebSocketCloseStatus.PolicyViolation");
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MachiVerseWorks.slnx"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
