using System.Windows.Controls;
using System.Windows.Input;

using CommandMan.Core;
using ManagedCommon;
using Microsoft.PowerToys.Settings.UI.Library;
using Wox.Plugin;

namespace Community.PowerToys.Run.Plugin.CommandMan;

public sealed class Main : IPlugin, IPluginI18n, IContextMenu, ISettingProvider, IDisposable
{
    private const string DefaultFullModeKey = "DefaultFullMode";
    private const string LightIcon = "Images/commandman.light.png";
    private const string DarkIcon = "Images/commandman.dark.png";
    private readonly CommandSearchEngine _searchEngine;
    private PluginInitContext? _context;
    private UsageMode _defaultMode = UsageMode.Simple;
    private string _iconPath = DarkIcon;
    private bool _disposed;

    public Main()
        : this(new CommandSearchEngine())
    {
    }

    internal Main(CommandSearchEngine searchEngine)
    {
        _searchEngine = searchEngine;
    }

    public static string PluginID => "F40E2D8DA2804DF3ADBB3E70B61C2F11";

    public string Name => "Command Man";

    public string Description => "查询并复制 Windows、Linux 与 Git 常用命令";

    public IEnumerable<PluginAdditionalOption> AdditionalOptions => new List<PluginAdditionalOption>
    {
        new()
        {
            Key = DefaultFullModeKey,
            DisplayLabel = "默认显示完整用法",
            DisplayDescription = "关闭时默认显示最常用的简单示例；查询中的 simple/full 会临时覆盖此设置。",
            Value = _defaultMode == UsageMode.Full,
        },
    };

    public void Init(PluginInitContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _context.API.ThemeChanged += OnThemeChanged;
        UpdateIcon(_context.API.GetCurrentTheme());
    }

    public List<Result> Query(Query query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var actionKeyword = string.IsNullOrWhiteSpace(query.ActionKeyword)
            ? "cmd"
            : query.ActionKeyword.Trim();

        return _searchEngine.Search(query.Search, _defaultMode)
            .Select(hit => CreateResult(hit, actionKeyword))
            .ToList();
    }

    public List<ContextMenuResult> LoadContextMenus(Result selectedResult)
    {
        if (selectedResult?.ContextData is not ResultContext data)
        {
            return [];
        }

        var targetMode = data.Hit.Mode == UsageMode.Simple ? UsageMode.Full : UsageMode.Simple;
        var targetLabel = targetMode == UsageMode.Full ? "完整" : "常用";

        return
        [
            new ContextMenuResult
            {
                PluginName = Name,
                Title = "复制命令",
                Glyph = "\uE8C8",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.C,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => Copy(data.Hit.DisplayCommand),
            },
            new ContextMenuResult
            {
                PluginName = Name,
                Title = $"切换到{targetLabel}用法",
                Glyph = "\uE8AB",
                FontFamily = "Segoe Fluent Icons,Segoe MDL2 Assets",
                AcceleratorKey = Key.Enter,
                AcceleratorModifiers = ModifierKeys.Control,
                Action = _ => SwitchMode(data, targetMode),
            },
        ];
    }

    public Control CreateSettingPanel() => throw new NotImplementedException();

    public void UpdateSettings(PowerLauncherPluginSettings settings)
    {
        _defaultMode = settings?.AdditionalOptions?
            .FirstOrDefault(option => option.Key == DefaultFullModeKey)?.Value == true
            ? UsageMode.Full
            : UsageMode.Simple;
    }

    public string GetTranslatedPluginTitle() => Name;

    public string GetTranslatedPluginDescription() => Description;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_context?.API is not null)
        {
            _context.API.ThemeChanged -= OnThemeChanged;
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private Result CreateResult(CommandSearchHit hit, string actionKeyword)
    {
        var modeLabel = hit.Mode == UsageMode.Simple ? "常用" : "完整";
        var platformLabel = hit.Entry.Platform switch
        {
            CommandPlatform.Windows => "Windows",
            CommandPlatform.Linux => "Linux",
            CommandPlatform.Git => "Git",
            CommandPlatform.Common => "通用",
            _ => hit.Entry.Platform.ToString(),
        };

        var data = new ResultContext(hit, actionKeyword);
        var result = new Result
        {
            Title = hit.DisplayCommand,
            SubTitle = $"[{platformLabel} · {modeLabel} · {hit.Entry.Source}] {hit.Example.Description}  ·  Enter 复制  ·  Ctrl+Enter 切换",
            Score = hit.Score,
            ContextData = data,
            ToolTipData = new ToolTipData(hit.Entry.Name, $"{hit.Entry.Summary}\n来源：{hit.Entry.Source}"),
            Action = _ => Copy(hit.DisplayCommand),
        };

        // PowerToys provides the file-system abstraction used by Result.IcoPath.
        // Keeping this out of uninitialized tests avoids copying host DLLs into the plugin package.
        if (_context is not null)
        {
            result.IcoPath = _iconPath;
        }

        return result;
    }

    private bool Copy(string command)
    {
        if (ClipboardService.TryCopy(command))
        {
            return true;
        }

        _context?.API.ShowNotification("Command Man：复制失败", "剪贴板正被其他程序占用，请重试。");
        return false;
    }

    private bool SwitchMode(ResultContext data, UsageMode targetMode)
    {
        var modeToken = targetMode == UsageMode.Full ? "full" : "simple";
        var platformToken = data.Hit.Entry.Platform.ToString().ToLowerInvariant();
        var displayedExecutable = data.Hit.DisplayCommand
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        var searchName = !data.Hit.Entry.Name.Contains(' ') &&
                         displayedExecutable is not null &&
                         data.Hit.Entry.Aliases.Contains(displayedExecutable, StringComparer.OrdinalIgnoreCase)
            ? displayedExecutable
            : data.Hit.Entry.Platform == CommandPlatform.Git &&
              data.Hit.Entry.Name.StartsWith("git ", StringComparison.OrdinalIgnoreCase)
                ? data.Hit.Entry.Name[4..]
                : data.Hit.Entry.Name;
        var nextQuery = $"{data.ActionKeyword} {modeToken} {platformToken} {searchName}";
        _context?.API.ChangeQuery(nextQuery, true);
        return false;
    }

    private void OnThemeChanged(Theme currentTheme, Theme newTheme) => UpdateIcon(newTheme);

    private void UpdateIcon(Theme theme)
    {
        _iconPath = theme is Theme.Light or Theme.HighContrastWhite ? LightIcon : DarkIcon;
    }

    private sealed record ResultContext(CommandSearchHit Hit, string ActionKeyword);
}
