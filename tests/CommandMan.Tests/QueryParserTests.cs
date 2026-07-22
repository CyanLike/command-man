using CommandMan.Core;

namespace CommandMan.Tests;

[TestClass]
public sealed class QueryParserTests
{
    [TestMethod]
    [DataRow("full git rebase", "rebase", CommandPlatform.Git, UsageMode.Full)]
    [DataRow("windows 常用 ping", "ping", CommandPlatform.Windows, UsageMode.Simple)]
    [DataRow("linux 详细 journalctl", "journalctl", CommandPlatform.Linux, UsageMode.Full)]
    public void ParseRecognizesModeAndPlatform(
        string input,
        string searchText,
        CommandPlatform platform,
        UsageMode mode)
    {
        var result = CommandQueryParser.Parse(input);

        Assert.AreEqual(searchText, result.SearchText);
        Assert.AreEqual(platform, result.Platform);
        Assert.AreEqual(mode, result.ModeOverride);
    }

    [TestMethod]
    public void ParseLeavesUnknownTokensForSearch()
    {
        var result = CommandQueryParser.Parse("git commit --amend");

        Assert.AreEqual("commit --amend", result.SearchText);
        Assert.AreEqual(CommandPlatform.Git, result.Platform);
        Assert.IsNull(result.ModeOverride);
    }

    [TestMethod]
    public void ReservedWordAfterSearchStartsIsTreatedAsSearchText()
    {
        var result = CommandQueryParser.Parse("windows Get-Command git");

        Assert.AreEqual("Get-Command git", result.SearchText);
        Assert.AreEqual(CommandPlatform.Windows, result.Platform);
    }
}
