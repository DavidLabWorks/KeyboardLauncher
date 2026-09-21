using KeyboardLauncher.Core;

var passed = 0;
void Check(string name, Action check) { check(); Console.WriteLine($"PASS {name}"); passed++; }
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Throws(Action action) { try { action(); } catch (FormatException) { return; } throw new Exception("Expected FormatException"); }

Check("ANSI positions and pages match macOS", () =>
{
    Assert(KeyboardLayout.Keys.Length == 38);
    Assert(KeyboardLayout.Slot(16, 0) == 12);
    Assert(KeyboardLayout.Slot(16, 2) == 88);
    Assert(KeyboardLayout.Slot(1, 0) == -1);
    Assert(KeyboardLayout.Slot(16, -1) == -1);
    Assert(KeyboardLayout.ScanCodes.Distinct().Count() == 38);
});
Check("Deleting legacy bindings preserves other positions", () =>
{
    var config = new LauncherConfig { Launchers = [new() { Name = "A", Exec = "a.exe" }, new() { Name = "B", Exec = "b.exe" }, new() { Name = "C", Exec = "c.exe" }] };
    var changed = config.Bind(0, null);
    Assert(changed.At(0) == null && changed.At(1)?.Name == "B" && changed.At(2)?.Name == "C");
    Assert(config.Launchers[1].KeyIndex == null);
    Assert(changed.Bind(76, new() { Name = "D", Exec = "d.exe" }).PageCount == 3);
});
Check("Shortcut parser rejects ambiguous and macOS shortcuts", () =>
{
    Assert(Hotkey.Parse("ctrl+shift+space") == new Hotkey(6, 32));
    Assert(Hotkey.Parse("win+e") == new Hotkey(8, 69));
    Assert(Hotkey.Parse("f24", false).Key == 0x87);
    foreach (var invalid in new[] { "", "ctrl", "ctrl++a", "ctrl+a+b", "cmd+space", "ctrl+ctrl+a", "ctrl+nope", "a", "ctrl+f25" }) Throws(() => Hotkey.Parse(invalid));
});
Check("Recorded shortcuts round-trip through storage syntax", () =>
{
    foreach (var text in new[] { "ctrl+a", "ctrl+shift+s", "alt+f4", "win+e", "shift+tab", "ctrl+left", "ctrl+/", "ctrl+backspace", "f24", "esc" })
    {
        var parsed = Hotkey.Parse(text, false);
        Assert(Hotkey.Parse(parsed.ToShortcutString(), false) == parsed);
    }
});
Check("All double-tap modifiers, disabled state and configuration persistence", () =>
{
    foreach (var (mode, left, right) in new[] { ("control", 0xA2u, 0xA3u), ("alt", 0xA4u, 0xA5u), ("shift", 0xA0u, 0xA1u) })
    {
        (LauncherConfig.Default() with { DoubleTapKey = mode, Shortcut = "" }).Validate();
        foreach (var key in new[] { left, right })
        {
            var detector = new DoubleModifierTap { Modifier = mode };
            Assert(!detector.Handle(key, true, 0, false, false));
            Assert(!detector.Handle(key, false, 50, false, false));
            Assert(!detector.Handle(key, true, 150, false, false));
            Assert(detector.Handle(key, false, 200, false, false));
            detector.Modifier = null;
            detector.Handle(key, true, 300, false, false); detector.Handle(key, false, 350, false, false);
            detector.Handle(key, true, 450, false, false); Assert(!detector.Handle(key, false, 500, false, false));
        }
        var mixed = new DoubleModifierTap { Modifier = mode };
        mixed.Handle(left, true, 0, false, false); mixed.Handle(left, false, 50, false, false);
        mixed.Handle(right, true, 150, false, false); Assert(!mixed.Handle(right, false, 200, false, false));
    }
});
Check("Side-specific double taps reject the opposite side and mixed taps", () =>
{
    foreach (var (mode, selected, opposite) in new[] {
        ("leftControl", 0xA2u, 0xA3u), ("rightControl", 0xA3u, 0xA2u),
        ("leftAlt", 0xA4u, 0xA5u), ("rightAlt", 0xA5u, 0xA4u),
        ("leftShift", 0xA0u, 0xA1u), ("rightShift", 0xA1u, 0xA0u) })
    {
        var config = LauncherConfig.Default() with { DoubleTapKey = mode };
        config.Validate();
        var json = System.Text.Json.JsonSerializer.Serialize(config, ConfigStore.JsonOptions);
        Assert(System.Text.Json.JsonSerializer.Deserialize<LauncherConfig>(json, ConfigStore.JsonOptions)!.DoubleTapKey == mode);
        var detector = new DoubleModifierTap { Modifier = mode };
        bool Tap(uint key, long at) {
            Assert(!detector.Handle(key, true, at, false, false));
            return detector.Handle(key, false, at + 50, false, false);
        }
        Assert(!Tap(opposite, 0)); Assert(!Tap(opposite, 150));
        detector.Reset(); Assert(!Tap(selected, 300)); Assert(!Tap(opposite, 450)); Assert(!Tap(selected, 600));
        Assert(Tap(selected, 750));
        detector.Reset(); Assert(!Tap(selected, 1000)); Assert(Tap(selected, 1150));
    }
});
Check("Double Control requires isolated short presses", () =>
{
    var tap = new DoubleModifierTap();
    Assert(!tap.Handle(0xA2, true, 0, false, false)); Assert(!tap.Handle(0xA2, false, 80, false, false));
    Assert(!tap.Handle(0xA2, true, 200, false, false)); Assert(tap.Handle(0xA2, false, 270, false, false));
    tap.Handle(0xA2, true, 1000, false, false); tap.Handle(0x41, true, 1020, false, false);
    Assert(!tap.Handle(0xA2, false, 1080, false, false));
    tap.Handle(0xA2, true, 1200, false, false); Assert(!tap.Handle(0xA2, false, 1280, false, false));
    tap.Reset(); tap.Handle(0xA2, true, 2000, false, false); Assert(!tap.Handle(0xA2, false, 2400, false, false));
    tap.Handle(0xA2, true, 2500, false, false); Assert(!tap.Handle(0xA2, false, 2550, false, false));
});
Check("Repeat, other modifiers, opposite Control and mouse reset cancel double tap", () =>
{
    foreach (var mode in new[] { "repeat", "modifier", "opposite", "mouse", "timeout" })
    {
        var tap = new DoubleModifierTap(); tap.Handle(0xA2, true, 0, false, false); tap.Handle(0xA2, false, 50, false, false);
        if (mode == "mouse") tap.Reset();
        var key = mode == "opposite" ? 0xA3u : 0xA2u;
        var time = mode == "timeout" ? 600 : 100;
        tap.Handle(key, true, time, mode == "repeat", mode == "modifier");
        Assert(!tap.Handle(key, false, time + 50, false, false));
    }
});
Check("Validation rejects collisions and incompatible commands", () =>
{
    var config = LauncherConfig.Default(); config.Validate();
    Throws(() => (config with { Launchers = [new() { KeyIndex = 0, Name = "A", Exec = "a.exe" }, new() { KeyIndex = 0, Name = "B", Exec = "b.exe" }] }).Validate());
    Throws(() => config.Bind(0, new() { Name = "Mac", Exec = "open -a Finder" }).Validate());
    Throws(() => config.Bind(0, new() { Name = "URL", ActionType = "url", Exec = "file:///tmp" }).Validate());
});
Check("Atomic configuration save, backup and corruption preservation", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "KeyboardLauncherChecks-" + Guid.NewGuid());
    var path = Path.Combine(directory, "config.json");
    try
    {
        var store = new ConfigStore(path); var config = store.Load();
        store.Save(config with { Theme = "dark" });
        Assert(store.Load().Theme == "dark"); Assert(File.Exists(path + ".bak"));
        store.Save(config with { Theme = "dark", DoubleTapKey = null });
        Assert(store.Load().DoubleTapKey == null);
        var text = File.ReadAllText(path); Assert(text.Contains("\"keyIndex\"")); Assert(!text.Contains("pageCount"));
        File.WriteAllText(path, "broken");
        try { store.Load(); throw new Exception("Expected JSON error"); } catch (System.Text.Json.JsonException) { }
        Assert(File.ReadAllText(path) == "broken");
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
});
Check("Renamed application migrates configuration without touching the original", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "KeyboardLauncherMigration-" + Guid.NewGuid());
    var legacy = Path.Combine(directory, "old", "config.json");
    var current = Path.Combine(directory, "new", "config.json");
    try
    {
        new ConfigStore(legacy).Save(LauncherConfig.Default() with { Theme = "dark" });
        var original = File.ReadAllText(legacy);
        var store = new ConfigStore(current, legacy);
        Assert(store.Load().Theme == "dark"); Assert(File.ReadAllText(legacy) == original);
        store.Save(store.Load() with { Theme = "light" });
        Assert(store.Load().Theme == "light"); Assert(File.ReadAllText(legacy) == original);
    }
    finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
});
Console.WriteLine($"{passed} checks passed.");
