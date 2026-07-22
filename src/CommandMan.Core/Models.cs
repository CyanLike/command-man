namespace CommandMan.Core;

public enum CommandPlatform
{
    Windows,
    Linux,
    Git,
    Common,
}

public enum UsageMode
{
    Simple,
    Full,
}

public sealed class CommandExample
{
    public string Command { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public IReadOnlyList<string> SearchTerms { get; init; } = [];
}

public sealed class CommandEntry
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public CommandPlatform Platform { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Source { get; init; } = "Command Man";

    public IReadOnlyList<string> Aliases { get; init; } = [];

    public IReadOnlyList<string> Keywords { get; init; } = [];

    public IReadOnlyList<CommandExample> Simple { get; init; } = [];

    public IReadOnlyList<CommandExample> Full { get; init; } = [];
}

public sealed record ParsedQuery(
    string SearchText,
    CommandPlatform? Platform,
    UsageMode? ModeOverride);

public sealed record CommandSearchHit(
    CommandEntry Entry,
    CommandExample Example,
    UsageMode Mode,
    int Score,
    string DisplayCommand);
