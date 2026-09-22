using System.Text.Json;
using System.Text.Json.Serialization;

namespace KeyboardLauncher.Core;

public sealed record Launcher
{
    public int? KeyIndex { get; set; }
    public string Name { get; set; } = "";
    public string Exec { get; set; } = "";
    public string? Icon { get; set; }
    public string? KeyboardShortcut { get; set; }
    // Windows extensions: exec is a shell target unless actionType is command.
    public string ActionType { get; set; } = "application";
    public string Arguments { get; set; } = "";
}

public sealed record LauncherConfig
{
    public string Shortcut { get; set; } = "ctrl+shift+space";
    [JsonIgnore(Condition = JsonIgnoreCondition.Never)]
    public string? DoubleTapKey { get; set; } = "control";
    public bool SuppressSystemShortcut { get; set; }
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "system";
    public List<Launcher> Launchers { get; set; } = [];
    public int PageCount => Math.Max(1, (Launchers.Select((x, i) => x.KeyIndex ?? i).DefaultIfEmpty(0).Max() / KeyboardLayout.Count) + 1);

    public Launcher? At(int slot) => Launchers.Where((x, i) => (x.KeyIndex ?? i) == slot).FirstOrDefault();

    public LauncherConfig Bind(int slot, Launcher? launcher)
    {
        if (slot is < 0 or >= 3800) throw new ArgumentOutOfRangeException(nameof(slot));
        var frozen = Launchers.Select((x, i) => x with { KeyIndex = x.KeyIndex ?? i }).Where(x => x.KeyIndex != slot).ToList();
        if (launcher is not null) frozen.Add(launcher with { KeyIndex = slot });
        return this with { Launchers = frozen };
    }

    public LauncherConfig MoveBinding(int source, int destination)
    {
        if (source is < 0 or >= 3800 || destination is < 0 or >= 3800)
            throw new ArgumentOutOfRangeException(nameof(destination));
        if (source == destination || At(source) is null) return this;
        return this with { Launchers = Launchers.Select((item, index) =>
        {
            var slot = item.KeyIndex ?? index;
            return item with { KeyIndex = slot == source ? destination : slot == destination ? source : slot };
        }).ToList() };
    }

    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(Shortcut)) _ = Hotkey.Parse(Shortcut);
        if (DoubleTapKey is not (null or "" or "control" or "alt" or "shift" or "leftControl" or "rightControl" or "leftAlt" or "rightAlt" or "leftShift" or "rightShift")) throw new FormatException(L.T("请选择有效的左侧或右侧双击按键。"));
        if (Language is not ("en" or "zh-CN")) throw new FormatException(L.T("未知语言。"));
        if (Theme is not ("system" or "light" or "dark")) throw new FormatException(L.T("未知主题。"));
        if (Launchers is null || Launchers.Count > 3800) throw new FormatException(L.T("绑定数量超出限制。"));
        var slots = new HashSet<int>();
        for (var i = 0; i < Launchers.Count; i++)
        {
            var item = Launchers[i] ?? throw new FormatException(L.T("绑定不能为 null。"));
            var slot = item.KeyIndex ?? i;
            if (slot is < 0 or >= 3800 || !slots.Add(slot)) throw new FormatException(L.T("键位重复或超出范围。"));
            if (string.IsNullOrWhiteSpace(item.Name)) throw new FormatException(L.T("请输入绑定名称。"));
            if (!string.IsNullOrWhiteSpace(item.KeyboardShortcut)) _ = Hotkey.Parse(item.KeyboardShortcut, requireModifier: false);
            else
            {
                if (string.IsNullOrWhiteSpace(item.Exec)) throw new FormatException(L.T("请输入启动目标或命令。"));
                if (item.Exec.StartsWith("open ", StringComparison.Ordinal) || item.Exec.Contains(".app/", StringComparison.Ordinal) || item.Exec.EndsWith(".app", StringComparison.Ordinal))
                    throw new FormatException(L.T("macOS 启动命令不能在 Windows 运行，请重新选择 Windows 应用。"));
                if (item.ActionType is not ("application" or "url" or "command" or "script")) throw new FormatException(L.T("未知动作类型。"));
                if (item.ActionType == "script" && !ScriptAction.IsSupported(item.Exec)) throw new FormatException(L.T("请选择 .cmd、.bat 或 .ps1 脚本文件。"));
                if (item.ActionType == "url" && (!Uri.TryCreate(item.Exec, UriKind.Absolute, out var uri) || uri.Scheme is not ("https" or "http")))
                    throw new FormatException(L.T("网址必须以 http:// 或 https:// 开头。"));
            }
        }
    }

    public static LauncherConfig Default() => new()
    {
        Launchers =
        [
            new() { KeyIndex = 12, Name = L.T("资源管理器"), Exec = "explorer.exe", Icon = "📁" },
            new() { KeyIndex = 13, Name = L.T("浏览器"), Exec = "https://www.bing.com", ActionType = "url", Icon = "🌐" },
            new() { KeyIndex = 14, Name = L.T("终端"), Exec = "powershell.exe", Icon = "⌨" },
            new() { KeyIndex = 15, Name = L.T("记事本"), Exec = "notepad.exe", Icon = "📝" },
            new() { KeyIndex = 22, Name = L.T("计算器"), Exec = "calc.exe", Icon = "▦" },
            new() { KeyIndex = 23, Name = L.T("设置"), Exec = "ms-settings:", Icon = "⚙" },
            new() { KeyIndex = 24, Name = L.T("复制"), KeyboardShortcut = "ctrl+c", Icon = "⧉" }
        ]
    };
}

public sealed class ConfigStore(string? path = null, string? legacyPath = null)
{
    public string FilePath { get; } = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardLauncher", "config.json");
    private readonly string? legacyFilePath = legacyPath ?? (path == null ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Launchpick", "config.json") : null);
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        IgnoreReadOnlyProperties = true
    };

    public LauncherConfig Load()
    {
        if (!File.Exists(FilePath) && legacyFilePath != null && File.Exists(legacyFilePath))
        {
            // Copy, never move or rewrite, so invalid legacy files remain recoverable too.
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
            File.Copy(legacyFilePath, FilePath, overwrite: false);
        }
        if (!File.Exists(FilePath)) { var initial = LauncherConfig.Default(); Save(initial); return initial; }
        var config = JsonSerializer.Deserialize<LauncherConfig>(File.ReadAllText(FilePath), JsonOptions) ?? throw new FormatException(L.T("配置为空。"));
        config.Validate();
        return config;
    }

    public void Save(LauncherConfig config)
    {
        config.Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(FilePath))!);
        var temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(config, JsonOptions));
            if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
            else File.Move(temp, FilePath);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
