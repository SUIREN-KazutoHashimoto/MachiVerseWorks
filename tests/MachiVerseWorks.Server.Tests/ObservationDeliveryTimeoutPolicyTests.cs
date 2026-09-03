using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class ObservationDeliveryTimeoutPolicyTests
{
    [TestMethod]
    public void MultiMessageDeliveryCreatesTimeoutBudgetInsideEachSendIteration()
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "src", "MachiVerseWorks.Server", "ObservationDeliveryCoordinator.cs");
        var source = File.ReadAllText(sourcePath);

        var plainLoop = source.IndexOf("foreach (var message in messages)", StringComparison.Ordinal);
        Assert.IsTrue(plainLoop >= 0);
        var plainSend = source.IndexOf("connection.SendAsync(message", plainLoop, StringComparison.Ordinal);
        Assert.IsTrue(plainSend > plainLoop);
        var plainWindow = source[plainLoop..plainSend];
        StringAssert.Contains(plainWindow, "CreateLinkedTokenSource(cancellationToken)");
        StringAssert.Contains(plainWindow, "CancelAfter(options.ObservationDeliveryTimeout)");

        var cachedLoop = source.IndexOf("for (var index = 0; index < messages.Count; index++)", plainSend, StringComparison.Ordinal);
        Assert.IsTrue(cachedLoop >= 0);
        var cachedSend = source.IndexOf("connection.SendCachedAsync", cachedLoop, StringComparison.Ordinal);
        Assert.IsTrue(cachedSend > cachedLoop);
        var cachedWindow = source[cachedLoop..cachedSend];
        StringAssert.Contains(cachedWindow, "CreateLinkedTokenSource(cancellationToken)");
        StringAssert.Contains(cachedWindow, "CancelAfter(options.ObservationDeliveryTimeout)");
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
