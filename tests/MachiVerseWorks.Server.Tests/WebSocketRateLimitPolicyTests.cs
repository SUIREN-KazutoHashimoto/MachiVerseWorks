using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class WebSocketRateLimitPolicyTests
{
    [TestMethod]
    public void RateLimitedHandlerPathDoesNotRegisterInvalidRequestStrike()
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "src", "MachiVerseWorks.Server", "WebSocketSessionHandler.cs");
        var source = File.ReadAllText(sourcePath);
        var rateStart = source.IndexOf("if (!connection.TryConsumeRequest", StringComparison.Ordinal);
        Assert.IsTrue(rateStart >= 0);
        var observationCheck = source.IndexOf("if (envelope.Message is not IObservationRequestMessage)", rateStart, StringComparison.Ordinal);
        Assert.IsTrue(observationCheck > rateStart);
        var rateBlock = source[rateStart..observationCheck];
        StringAssert.Contains(rateBlock, "ProtocolErrorParameterKeys.DetailCode, \"rateLimited\"");
        StringAssert.Contains(rateBlock, "return true;");
        Assert.IsFalse(rateBlock.Contains("RejectRecoverableAsync", StringComparison.Ordinal));
        Assert.IsFalse(rateBlock.Contains("RegisterInvalidRequest", StringComparison.Ordinal));
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
