using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Server.Tests;

[TestClass]
public sealed class AdminCommandCancellationCommitPolicyTests
{
    [TestMethod]
    public void ExecutorDoesNotRecheckRequestCancellationAfterSuccessfulExecution()
    {
        var sourcePath = Path.Combine(FindRepositoryRoot(), "src", "MachiVerseWorks.Server", "AdminCommandExecutorV2.cs");
        var source = File.ReadAllText(sourcePath);
        var execute = source.IndexOf("var result = await ExecuteCoreAsync(request.Command, executionCancellation.Token);", StringComparison.Ordinal);
        var complete = source.IndexOf("request.Completion.TrySetResult(result);", execute, StringComparison.Ordinal);
        Assert.IsTrue(execute >= 0);
        Assert.IsTrue(complete > execute);

        var beforeExecute = source[..execute];
        StringAssert.Contains(beforeExecute, "request.CancellationToken.ThrowIfCancellationRequested();");

        var commitWindow = source[execute..complete];
        Assert.IsFalse(
            commitWindow.Contains("ThrowIfCancellationRequested", StringComparison.Ordinal),
            "Request cancellation must not convert a successfully executed command into a canceled result after its commit point.");
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
