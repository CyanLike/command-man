using System.Collections.Concurrent;

namespace CommandMan.Core;

public sealed class CommandSearchEngine
{
    private const int CacheCapacity = 128;
    private static readonly Lazy<SearchIndex> DefaultIndex = new(
        () => BuildIndex(CommandCatalog.Default),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IndexedCommand[] _commands;
    private readonly int[] _allCommandIndexes;
    private readonly Dictionary<string, int[]> _exactLookup;
    private readonly SearchKey[] _searchKeys;
    private readonly ConcurrentDictionary<SearchCacheKey, IReadOnlyList<CommandSearchHit>> _cache = new();
    private readonly ConcurrentQueue<SearchCacheKey> _cacheOrder = new();

    public CommandSearchEngine(CommandCatalog? catalog = null)
    {
        var index = catalog is null ? DefaultIndex.Value : BuildIndex(catalog);
        _commands = index.Commands;
        _allCommandIndexes = index.AllCommandIndexes;
        _searchKeys = index.SearchKeys;
        _exactLookup = index.ExactLookup;
    }

    private static SearchIndex BuildIndex(CommandCatalog catalog)
    {
        var commands = catalog.Commands.Select(Index).ToArray();
        var allCommandIndexes = Enumerable.Range(0, commands.Length).ToArray();

        var keys = new List<SearchKey>(commands.Length * 2);
        for (var commandIndex = 0; commandIndex < commands.Length; commandIndex++)
        {
            var command = commands[commandIndex];
            var commandKeys = command.Aliases
                .Append(command.Name)
                .Append(command.ShortName)
                .Where(key => key.Length > 0)
                .Distinct(StringComparer.Ordinal);
            keys.AddRange(commandKeys.Select(key => new SearchKey(key, commandIndex)));
        }

        var searchKeys = keys
            .OrderBy(key => key.Key, StringComparer.Ordinal)
            .ThenBy(key => key.CommandIndex)
            .ToArray();
        var exactLookup = keys
            .GroupBy(key => key.Key, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(key => key.CommandIndex).Distinct().ToArray(),
                StringComparer.Ordinal);

        return new SearchIndex(commands, allCommandIndexes, exactLookup, searchKeys);
    }

    public IReadOnlyList<CommandSearchHit> Search(
        string? input,
        UsageMode defaultMode = UsageMode.Simple,
        int maxResults = 30)
    {
        if (maxResults <= 0)
        {
            return [];
        }

        var cacheKey = new SearchCacheKey((input ?? string.Empty).Trim(), defaultMode, maxResults);
        if (_cache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        var results = SearchCore(input, defaultMode, maxResults);
        if (_cache.TryAdd(cacheKey, results))
        {
            _cacheOrder.Enqueue(cacheKey);
            TrimCache();
        }

        return results;
    }

    private IReadOnlyList<CommandSearchHit> SearchCore(
        string? input,
        UsageMode defaultMode,
        int maxResults)
    {
        var query = CommandQueryParser.Parse(input);
        var queryParts = query.SearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var argumentTokens = queryParts.Where(IsArgumentToken).Select(NormalizeOption).ToArray();
        var commandSearchText = Normalize(string.Join(' ', queryParts.Where(token => !IsArgumentToken(token))));
        var commandTerms = commandSearchText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var hasCommandSearch = commandSearchText.Length > 0;
        var hasArguments = argumentTokens.Length > 0;
        var mode = query.ModeOverride ?? (hasArguments ? UsageMode.Full : defaultMode);

        var rankedCommands = new List<RankedCommand>();
        foreach (var commandIndex in ResolveCandidateIndexes(commandSearchText))
        {
            var command = _commands[commandIndex];
            if (!MatchesPlatform(command.Entry.Platform, query.Platform))
            {
                continue;
            }

            var score = Score(command, commandSearchText, commandTerms);
            if (hasCommandSearch && score <= 0)
            {
                continue;
            }

            rankedCommands.Add(new RankedCommand(commandIndex, score));
        }

        rankedCommands.Sort((left, right) =>
        {
            var scoreComparison = right.Score.CompareTo(left.Score);
            if (scoreComparison != 0)
            {
                return scoreComparison;
            }

            var leftCommand = _commands[left.CommandIndex].Entry;
            var rightCommand = _commands[right.CommandIndex].Entry;
            var platformComparison = leftCommand.Platform.CompareTo(rightCommand.Platform);
            return platformComparison != 0
                ? platformComparison
                : StringComparer.OrdinalIgnoreCase.Compare(leftCommand.Name, rightCommand.Name);
        });

        var results = new List<CommandSearchHit>(maxResults);
        foreach (var ranked in rankedCommands)
        {
            var command = _commands[ranked.CommandIndex];
            var examples = mode == UsageMode.Simple ? command.SimpleExamples : command.FullExamples;
            var rankedExamples = new List<RankedExample>(examples.Length);
            foreach (var example in examples)
            {
                var argumentScore = MatchArguments(example, argumentTokens);
                if (!hasArguments || argumentScore >= 0)
                {
                    rankedExamples.Add(new RankedExample(example, argumentScore));
                }
            }

            if (hasArguments && rankedExamples.Count == 0)
            {
                continue;
            }

            rankedExamples.Sort((left, right) =>
            {
                var scoreComparison = right.ArgumentScore.CompareTo(left.ArgumentScore);
                return scoreComparison != 0
                    ? scoreComparison
                    : left.Example.Position.CompareTo(right.Example.Position);
            });

            var exampleCount = hasCommandSearch || hasArguments
                ? rankedExamples.Count
                : Math.Min(1, rankedExamples.Count);
            for (var index = 0; index < exampleCount && results.Count < maxResults; index++)
            {
                var rankedExample = rankedExamples[index];
                results.Add(new CommandSearchHit(
                    command.Entry,
                    rankedExample.Example.Example,
                    mode,
                    ranked.Score + rankedExample.ArgumentScore - index,
                    BuildDisplayCommand(command.Entry, rankedExample.Example.Example, commandSearchText)));
            }

            if (results.Count >= maxResults)
            {
                break;
            }
        }

        return results
            .OrderByDescending(result => result.Score)
            .Take(maxResults)
            .ToArray();
    }

    private IReadOnlyList<int> ResolveCandidateIndexes(string searchText)
    {
        if (searchText.Length == 0)
        {
            return _allCommandIndexes;
        }

        if (_exactLookup.TryGetValue(searchText, out var exactMatches))
        {
            // Keep exact commands first in scoring, but also expose true subcommands.
            // The space boundary prevents queries such as "ls" from pulling in "lsof".
            var childPrefix = searchText + " ";
            var firstChild = LowerBound(childPrefix);
            if (firstChild >= _searchKeys.Length ||
                !_searchKeys[firstChild].Key.StartsWith(childPrefix, StringComparison.Ordinal))
            {
                return exactMatches;
            }

            var exactAndChildren = new HashSet<int>(exactMatches);
            AddPrefixMatches(childPrefix, firstChild, exactAndChildren);
            return exactAndChildren.ToArray();
        }

        var firstMatch = LowerBound(searchText);
        if (firstMatch >= _searchKeys.Length ||
            !_searchKeys[firstMatch].Key.StartsWith(searchText, StringComparison.Ordinal))
        {
            // Semantic searches such as Chinese descriptions still use the complete corpus.
            return _allCommandIndexes;
        }

        var matches = new HashSet<int>();
        AddPrefixMatches(searchText, firstMatch, matches);

        return matches.ToArray();
    }

    private void AddPrefixMatches(string prefix, int firstMatch, HashSet<int> matches)
    {
        for (var index = firstMatch;
             index < _searchKeys.Length && _searchKeys[index].Key.StartsWith(prefix, StringComparison.Ordinal);
             index++)
        {
            matches.Add(_searchKeys[index].CommandIndex);
        }
    }

    private int LowerBound(string value)
    {
        var low = 0;
        var high = _searchKeys.Length;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (StringComparer.Ordinal.Compare(_searchKeys[middle].Key, value) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }

    private static IndexedCommand Index(CommandEntry command)
    {
        var name = Normalize(command.Name);
        var shortName = command.Platform == CommandPlatform.Git && name.StartsWith("git ", StringComparison.Ordinal)
            ? name[4..]
            : name;
        var aliases = command.Aliases.Select(Normalize).ToArray();
        var fullExampleCommands = command.Full
            .Select(example => example.Command)
            .ToHashSet(StringComparer.Ordinal);
        var simpleOnlyExamples = command.Simple
            .Where(example => !fullExampleCommands.Contains(example.Command));
        var corpus = Normalize(string.Join(' ',
            command.Name,
            command.Summary,
            string.Join(' ', command.Aliases),
            string.Join(' ', command.Keywords),
            string.Join(' ', simpleOnlyExamples.Select(x => $"{x.Command} {x.Description}")),
            string.Join(' ', command.Full.Select(x => $"{x.Command} {x.Description}"))));

        return new IndexedCommand(
            command,
            name,
            shortName,
            aliases,
            corpus,
            IndexExamples(command.Simple),
            IndexExamples(command.Full));
    }

    private static IndexedExample[] IndexExamples(IReadOnlyList<CommandExample> examples)
    {
        var indexed = new IndexedExample[examples.Count];
        for (var index = 0; index < examples.Count; index++)
        {
            var example = examples[index];
            var options = example.SearchTerms
                .Concat(ExtractOptions(example.Command))
                .Select(NormalizeOption)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            indexed[index] = new IndexedExample(example, options, index);
        }

        return indexed;
    }

    private static int Score(
        IndexedCommand command,
        string searchText,
        IReadOnlyList<string> terms)
    {
        if (searchText.Length == 0)
        {
            return 100;
        }

        for (var index = 0; index < terms.Count; index++)
        {
            if (!command.Corpus.Contains(terms[index], StringComparison.Ordinal))
            {
                return 0;
            }
        }

        var score = terms.Count * 125;
        if (command.Name == searchText || command.ShortName == searchText)
        {
            score += 1_200;
        }
        else if (command.Aliases.Contains(searchText, StringComparer.Ordinal))
        {
            score += 1_050;
        }
        else if (command.Name.StartsWith(searchText, StringComparison.Ordinal) ||
                 command.ShortName.StartsWith(searchText, StringComparison.Ordinal))
        {
            score += 850;
        }
        else if (command.Name.Contains(searchText, StringComparison.Ordinal) ||
                 command.ShortName.Contains(searchText, StringComparison.Ordinal))
        {
            score += 650;
        }

        return score;
    }

    private static int MatchArguments(IndexedExample example, IReadOnlyList<string> arguments)
    {
        if (arguments.Count == 0)
        {
            return 0;
        }

        var score = 0;
        for (var argumentIndex = 0; argumentIndex < arguments.Count; argumentIndex++)
        {
            var argument = arguments[argumentIndex];
            if (example.Options.Contains(argument, StringComparer.OrdinalIgnoreCase))
            {
                score += 240;
                continue;
            }

            if (example.Options.Any(candidate => IsShortFlagBundleMatch(argument, candidate)))
            {
                score += 90;
                continue;
            }

            return -1;
        }

        return score;
    }

    private static IEnumerable<string> ExtractOptions(string command)
    {
        return command
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(token => token.Trim(',', ';', '(', ')', '[', ']', '<', '>', '\'', '"'))
            .Where(IsArgumentToken);
    }

    private static bool IsShortFlagBundleMatch(string query, string candidate)
    {
        if (!query.StartsWith("-", StringComparison.Ordinal) ||
            query.StartsWith("--", StringComparison.Ordinal) ||
            !candidate.StartsWith("-", StringComparison.Ordinal) ||
            candidate.StartsWith("--", StringComparison.Ordinal))
        {
            return false;
        }

        var requiredFlags = query[1..];
        var candidateFlags = candidate[1..];
        return requiredFlags.Length > 0 && requiredFlags.All(candidateFlags.Contains);
    }

    private static string BuildDisplayCommand(
        CommandEntry entry,
        CommandExample example,
        string commandSearchText)
    {
        if (entry.Name.Contains(' ') || commandSearchText.Contains(' '))
        {
            return example.Command;
        }

        if (!entry.Aliases.Contains(commandSearchText, StringComparer.OrdinalIgnoreCase))
        {
            return example.Command;
        }

        if (example.Command.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
        {
            return commandSearchText;
        }

        var prefix = entry.Name + " ";
        return example.Command.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? commandSearchText + example.Command[entry.Name.Length..]
            : example.Command;
    }

    private static bool MatchesPlatform(CommandPlatform platform, CommandPlatform? requested)
    {
        if (!requested.HasValue)
        {
            return true;
        }

        return platform == requested.Value ||
               (platform == CommandPlatform.Common && requested.Value is CommandPlatform.Windows or CommandPlatform.Linux);
    }

    private static bool IsArgumentToken(string token)
    {
        return token.Length > 1 &&
               (token.StartsWith("-", StringComparison.Ordinal) || token.StartsWith("/", StringComparison.Ordinal));
    }

    private static string NormalizeOption(string option)
    {
        var value = option.Trim().Trim(',', ';', '(', ')', '[', ']', '<', '>', '\'', '"');
        var equalsIndex = value.IndexOf('=');
        return (equalsIndex > 0 ? value[..equalsIndex] : value).ToLowerInvariant();
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant();

    private void TrimCache()
    {
        while (_cache.Count > CacheCapacity && _cacheOrder.TryDequeue(out var oldest))
        {
            _cache.TryRemove(oldest, out _);
        }
    }

    private sealed record IndexedCommand(
        CommandEntry Entry,
        string Name,
        string ShortName,
        string[] Aliases,
        string Corpus,
        IndexedExample[] SimpleExamples,
        IndexedExample[] FullExamples);

    private sealed record IndexedExample(
        CommandExample Example,
        string[] Options,
        int Position);

    private sealed record SearchIndex(
        IndexedCommand[] Commands,
        int[] AllCommandIndexes,
        Dictionary<string, int[]> ExactLookup,
        SearchKey[] SearchKeys);

    private readonly record struct SearchKey(string Key, int CommandIndex);

    private readonly record struct SearchCacheKey(string Query, UsageMode DefaultMode, int MaxResults);

    private readonly record struct RankedCommand(int CommandIndex, int Score);

    private readonly record struct RankedExample(IndexedExample Example, int ArgumentScore);
}
