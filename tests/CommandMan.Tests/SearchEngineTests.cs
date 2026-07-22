using CommandMan.Core;

namespace CommandMan.Tests;

[TestClass]
public sealed class SearchEngineTests
{
    private readonly CommandSearchEngine _engine = new();

    [TestMethod]
    public void SimpleModeReturnsCopyableCommonExamples()
    {
        var results = _engine.Search("git commit");

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("git-commit", results[0].Entry.Id);
        Assert.IsTrue(results.All(hit => hit.Mode == UsageMode.Simple));
        StringAssert.Contains(results[0].Example.Command, "git commit");
    }

    [TestMethod]
    public void FullKeywordOverridesDefaultMode()
    {
        var results = _engine.Search("full git rebase", UsageMode.Simple);

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(hit => hit.Mode == UsageMode.Full));
        Assert.IsTrue(results.Any(hit => hit.Example.Command.Contains("-i", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SimpleKeywordOverridesFullDefault()
    {
        var results = _engine.Search("simple linux curl", UsageMode.Full);

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(hit => hit.Mode == UsageMode.Simple));
    }

    [TestMethod]
    public void PlatformOnlyQueryReturnsOneExamplePerCommand()
    {
        var results = _engine.Search("windows");

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(hit => hit.Entry.Platform is CommandPlatform.Windows or CommandPlatform.Common));
        Assert.AreEqual(results.Count, results.Select(hit => hit.Entry.Id).Distinct().Count());
    }

    [TestMethod]
    public void ExactCommandRanksAheadOfKeywordMatches()
    {
        var results = _engine.Search("linux find");

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("linux-find", results[0].Entry.Id);
    }

    [TestMethod]
    public void UnmatchedQueryReturnsNoResults()
    {
        Assert.AreEqual(0, _engine.Search("this-command-does-not-exist-xyz").Count);
    }

    [TestMethod]
    public void AliasWithoutArgumentsUsesSimpleExamples()
    {
        var results = _engine.Search("ll");

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.All(result => result.Mode == UsageMode.Simple));
        Assert.IsTrue(results[0].DisplayCommand.StartsWith("ll ", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ArgumentAutomaticallyUsesFullModeAndRanksExactExampleFirst()
    {
        var results = _engine.Search("ll -a");

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("ll -a", results[0].DisplayCommand);
        Assert.AreEqual(UsageMode.Full, results[0].Mode);
        StringAssert.Contains(results[0].Example.Description, "隐藏");
        Assert.IsTrue(results.All(result => result.DisplayCommand.Contains('a')));
    }

    [TestMethod]
    public void LongArgumentFiltersToMatchingUsage()
    {
        var results = _engine.Search("git commit --amend");

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual(UsageMode.Full, results[0].Mode);
        Assert.IsTrue(results.All(result =>
            result.Example.SearchTerms.Contains("--amend", StringComparer.OrdinalIgnoreCase) ||
            result.DisplayCommand.Contains("--amend", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void WindowsSlashArgumentIsMatchedPrecisely()
    {
        var results = _engine.Search("windows ipconfig /all");

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("ipconfig /all", results[0].DisplayCommand);
        Assert.AreEqual(UsageMode.Full, results[0].Mode);
    }

    [TestMethod]
    public void PrefixIndexFindsCommandsWithoutFullCorpusScan()
    {
        var results = _engine.Search("kubect");

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results[0].Entry.Name.StartsWith("kubectl", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void ExactParentCommandAlsoReturnsHierarchicalSubcommands()
    {
        var results = _engine.Search("adb");

        Assert.IsTrue(results.Count > 2);
        Assert.AreEqual("adb", results[0].Entry.Name);
        Assert.IsTrue(results.Any(result => result.Entry.Name == "adb shell"));
        Assert.IsTrue(results.Any(result => result.Entry.Name == "adb devices"));
    }

    [TestMethod]
    public void FullParentCommandReturnsEveryExampleFromTheParentPage()
    {
        var results = _engine.Search("full adb");
        var parentExamples = results.Where(result => result.Entry.Name == "adb").ToArray();

        Assert.AreEqual(8, parentExamples.Length);
        Assert.IsTrue(parentExamples.Any(result => result.DisplayCommand == "adb devices"));
        Assert.IsTrue(parentExamples.All(result => result.Mode == UsageMode.Full));
    }

    [TestMethod]
    public void ExactCommandDoesNotReturnUnrelatedTextualPrefixes()
    {
        var results = _engine.Search("ls");

        Assert.IsTrue(results.Count > 0);
        Assert.IsFalse(results.Any(result => result.Entry.Name == "lsof"));
        Assert.IsFalse(results.Any(result => result.Entry.Name == "lsusb"));
    }

    [TestMethod]
    public void SemanticSearchStillFallsBackToDescriptions()
    {
        var results = _engine.Search("监听端口");

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results.Any(result =>
            result.Entry.Summary.Contains("端口", StringComparison.Ordinal) ||
            result.Example.Description.Contains("端口", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void RepeatedQueryUsesBoundedResultCache()
    {
        var first = _engine.Search("ll -a");
        var second = _engine.Search("ll -a");

        Assert.AreSame(first, second);
    }
}
