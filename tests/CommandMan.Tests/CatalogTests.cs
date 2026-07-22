using CommandMan.Core;

namespace CommandMan.Tests;

[TestClass]
public sealed class CatalogTests
{
    [TestMethod]
    public void EmbeddedCatalogContainsAllPlatformsAndEnoughCommands()
    {
        var commands = CommandCatalog.Default.Commands;

        Assert.IsTrue(commands.Count >= 60, $"Expected at least 60 commands, found {commands.Count}.");
        CollectionAssert.AreEquivalent(
            Enum.GetValues<CommandPlatform>(),
            commands.Select(command => command.Platform).Distinct().ToArray());
    }

    [TestMethod]
    public void EveryCommandHasBothUsageModes()
    {
        foreach (var command in CommandCatalog.Default.Commands)
        {
            Assert.IsTrue(command.Simple.Count > 0, $"{command.Id} has no simple examples.");
            Assert.IsTrue(command.Full.Count > 0, $"{command.Id} has no full examples.");
        }
    }
}
