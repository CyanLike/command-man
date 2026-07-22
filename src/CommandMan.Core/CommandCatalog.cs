using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CommandMan.Core;

public sealed class CommandCatalog
{
    private static readonly string[] ResourceNames =
    [
        "CommandMan.Core.Data.windows.json",
        "CommandMan.Core.Data.linux.json",
        "CommandMan.Core.Data.git.json",
        "CommandMan.Core.Data.tldr.json",
    ];
    private static readonly Lazy<CommandCatalog> DefaultCatalog = new(LoadEmbedded);

    public CommandCatalog(IEnumerable<CommandEntry> commands)
    {
        Commands = commands?.ToArray() ?? throw new ArgumentNullException(nameof(commands));
        Validate(Commands);
    }

    public IReadOnlyList<CommandEntry> Commands { get; }

    public static CommandCatalog Default => DefaultCatalog.Value;

    private static CommandCatalog LoadEmbedded()
    {
        var assembly = typeof(CommandCatalog).Assembly;
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() },
        };

        var commands = new List<CommandEntry>();
        foreach (var resourceName in ResourceNames)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"Embedded command catalog '{resourceName}' was not found.");
            commands.AddRange(JsonSerializer.Deserialize<List<CommandEntry>>(stream, options)
                ?? throw new InvalidOperationException($"The embedded command catalog '{resourceName}' is invalid."));
        }

        return new CommandCatalog(commands);
    }

    private static void Validate(IReadOnlyCollection<CommandEntry> commands)
    {
        if (commands.Count == 0)
        {
            throw new InvalidOperationException("The command catalog must contain at least one command.");
        }

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var command in commands)
        {
            if (string.IsNullOrWhiteSpace(command.Id) ||
                string.IsNullOrWhiteSpace(command.Name) ||
                string.IsNullOrWhiteSpace(command.Summary))
            {
                throw new InvalidOperationException("Every command requires an id, name, and summary.");
            }

            if (!ids.Add(command.Id))
            {
                throw new InvalidOperationException($"Duplicate command id: {command.Id}");
            }

            ValidateExamples(command, command.Simple, nameof(command.Simple));
            ValidateExamples(command, command.Full, nameof(command.Full));
        }
    }

    private static void ValidateExamples(
        CommandEntry command,
        IReadOnlyCollection<CommandExample> examples,
        string groupName)
    {
        if (examples.Count == 0 || examples.Any(x =>
                string.IsNullOrWhiteSpace(x.Command) || string.IsNullOrWhiteSpace(x.Description)))
        {
            throw new InvalidOperationException($"Command '{command.Id}' has invalid {groupName} examples.");
        }
    }
}
