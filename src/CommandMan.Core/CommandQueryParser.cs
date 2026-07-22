namespace CommandMan.Core;

public static class CommandQueryParser
{
    private static readonly Dictionary<string, UsageMode> Modes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["simple"] = UsageMode.Simple,
        ["common"] = UsageMode.Simple,
        ["常用"] = UsageMode.Simple,
        ["简单"] = UsageMode.Simple,
        ["full"] = UsageMode.Full,
        ["advanced"] = UsageMode.Full,
        ["detail"] = UsageMode.Full,
        ["完整"] = UsageMode.Full,
        ["复杂"] = UsageMode.Full,
        ["详细"] = UsageMode.Full,
    };

    private static readonly Dictionary<string, CommandPlatform> Platforms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["windows"] = CommandPlatform.Windows,
        ["win"] = CommandPlatform.Windows,
        ["powershell"] = CommandPlatform.Windows,
        ["linux"] = CommandPlatform.Linux,
        ["unix"] = CommandPlatform.Linux,
        ["git"] = CommandPlatform.Git,
        ["common"] = CommandPlatform.Common,
        ["通用"] = CommandPlatform.Common,
    };

    public static ParsedQuery Parse(string? input)
    {
        var tokens = (input ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        UsageMode? mode = null;
        CommandPlatform? platform = null;
        var searchTokens = new List<string>(tokens.Length);
        var searchStarted = false;

        foreach (var token in tokens)
        {
            if (!searchStarted && Modes.TryGetValue(token, out var parsedMode))
            {
                mode = parsedMode;
            }
            else if (!searchStarted && Platforms.TryGetValue(token, out var parsedPlatform))
            {
                platform = parsedPlatform;
            }
            else
            {
                searchStarted = true;
                searchTokens.Add(token);
            }
        }

        return new ParsedQuery(string.Join(' ', searchTokens), platform, mode);
    }
}
