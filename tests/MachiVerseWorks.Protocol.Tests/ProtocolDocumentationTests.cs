using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerseWorks.Protocol.Tests;

[TestClass]
public sealed class ProtocolDocumentationTests
{
    [TestMethod]
    public void ReadmeDeclaresAuthoritativeCurrentProtocolVersion()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "MachiVerseWorks.slnx"))) directory = directory.Parent;
        Assert.IsNotNull(directory, "Repository root could not be located from the test output directory.");
        var readme = File.ReadAllText(Path.Combine(directory.FullName, "src", "MachiVerseWorks.Protocol", "README.md"));
        StringAssert.Contains(readme, $"現在のProtocolは **{ProtocolVersion.Current}** です。");
    }
}
