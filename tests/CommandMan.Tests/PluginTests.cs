using Community.PowerToys.Run.Plugin.CommandMan;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace CommandMan.Tests;

[TestClass]
public sealed class PluginTests
{
    [TestMethod]
    public void QueryBuildsPowerToysResultsWithoutInitialization()
    {
        using var plugin = new Main();

        var results = plugin.Query(new Query("cmd git status", "cmd"));

        Assert.IsTrue(results.Count > 0);
        Assert.IsTrue(results[0].Title.StartsWith("git status", StringComparison.Ordinal));
        StringAssert.Contains(results[0].SubTitle, "Enter 复制");
        Assert.IsNotNull(results[0].Action);
    }

    [TestMethod]
    public void ResultProvidesCopyAndModeSwitchContextMenus()
    {
        using var plugin = new Main();
        var result = plugin.Query(new Query("cmd linux grep", "cmd"))[0];

        var menus = plugin.LoadContextMenus(result);

        Assert.AreEqual(2, menus.Count);
        Assert.IsTrue(menus.Any(menu => menu.Title == "复制命令"));
        Assert.IsTrue(menus.Any(menu => menu.Title.Contains("完整", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void SettingCanMakeFullModeTheDefault()
    {
        using var plugin = new Main();
        plugin.UpdateSettings(new PowerLauncherPluginSettings
        {
            AdditionalOptions =
            [
                new PluginAdditionalOption { Key = "DefaultFullMode", Value = true },
            ],
        });

        var results = plugin.Query(new Query("cmd git rebase", "cmd"));

        Assert.IsTrue(results.Any(result => result.Title.Contains("-i", StringComparison.Ordinal)));
        Assert.IsTrue(results.All(result => result.SubTitle.Contains("完整", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ParameterQueryShowsPreciseTldrUsage()
    {
        using var plugin = new Main();

        var results = plugin.Query(new Query("cmd ll -a", "cmd"));

        Assert.IsTrue(results.Count > 0);
        Assert.AreEqual("ll -a", results[0].Title);
        StringAssert.Contains(results[0].SubTitle, "完整");
        StringAssert.Contains(results[0].SubTitle, "TLDR Pages");
    }
}
