using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;

namespace KeyboardLauncher;

public partial class App : Application
{
    private Mutex? instance;
    private EventWaitHandle? activationEvent;
    private RegisteredWaitHandle? activationWait;
    private PanelWindow? panel;
    private SettingsWindow? settings;
    private DesktopIntegration? desktop;
    internal ConfigStore Store { get; } = new();
    internal LauncherConfig Config { get; private set; } = LauncherConfig.Default();
    internal string? StartupError { get; private set; }
    internal bool ConfigLoadFailed { get; private set; }

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            try
            {
                var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KeyboardLauncher");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "error.log"), DateTimeOffset.Now + " " + e.Exception + Environment.NewLine);
            }
            catch { }
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        instance = new Mutex(true, "Local\\KeyboardLauncher.Windows." + Environment.UserName, out bool created);
        if (!created)
        {
            try { using var signal = EventWaitHandle.OpenExisting("Local\\KeyboardLauncher.Activate." + Environment.UserName); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { Native.MessageBoxW(0, L.T("Keyboard Launcher 正在启动，请稍后重试。"), "Keyboard Launcher", 0x40); }
            instance.Dispose(); instance = null; Exit(); return;
        }
        try { Config = Store.Load(); }
        catch (Exception ex) { ConfigLoadFailed = true; StartupError = L.T("配置读取失败，原文件已保留。本次使用默认配置；请先修复配置并重启。\n") + ex.Message; }
        try
        {
            L.Language = Config.Language;
            panel = new PanelWindow(this);
            activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\KeyboardLauncher.Activate." + Environment.UserName);
            activationWait = ThreadPool.RegisterWaitForSingleObject(activationEvent, (_, _) => panel.DispatcherQueue.TryEnqueue(() => panel.Show()), null, Timeout.Infinite, false);
            desktop = new DesktopIntegration(panel.Handle, panel.DispatcherQueue);
            desktop.Toggle += () => panel.Toggle(); desktop.Settings += OpenSettings; desktop.Quit += Quit;
            desktop.PanelKeyPressed += panel.HandleNativeKey;
            try { desktop.Configure(Config); }
            catch (Exception ex) { StartupError = ex.Message; }
            if (!Environment.GetCommandLineArgs().Contains("--background")) panel.Show();
            if (StartupError != null || Environment.GetCommandLineArgs().Contains("--settings")) OpenSettings();
            if (Environment.GetCommandLineArgs().Contains("--icon-smoke-test")) RunIconSmokeTest();
            if (Environment.GetCommandLineArgs().Contains("--shortcut-smoke-test")) RunShortcutSmokeTest();
            if (Environment.GetCommandLineArgs().Contains("--editor-preview")) panel.PreviewEditor();
            if (Environment.GetCommandLineArgs().Contains("--theme-smoke-test")) RunThemeSmokeTest();
            if (Environment.GetCommandLineArgs().Contains("--control-smoke-test")) RunControlSmokeTest();
            if (Environment.GetCommandLineArgs().Contains("--panel-smoke-test")) RunPanelSmokeTest();
        }
        catch (Exception ex) { Native.MessageBoxW(0, ex.ToString(), L.T("Keyboard Launcher 启动失败"), 0x10); Quit(); }
    }

    internal void Save(LauncherConfig config)
    {
        if (ConfigLoadFailed) throw new InvalidOperationException(L.T("原配置未能加载。请打开配置目录，修复或备份并移走 config.json 后重启，避免覆盖原数据。"));
        config.Validate();
        desktop!.Configure(config);
        try { Store.Save(config); }
        catch { desktop.Configure(Config); throw; }
        var languageChanged = Config.Language != config.Language;
        Config = config; StartupError = null; L.Language = config.Language;
        panel!.Refresh(); settings?.ApplyTheme();
        if (languageChanged) { desktop.RefreshLanguage(); settings?.ApplyLanguage(); }
    }

    internal bool HasPressedKeys => desktop?.IsAnyKeyDown == true;
    internal void BeginShortcutRecording(nint window, Action<Hotkey> callback) => desktop?.BeginRecording(window, callback);
    internal void EndShortcutRecording(nint window) => desktop?.EndRecording(window);
    internal void SuspendHotkeys(bool suspend) { if (desktop != null) desktop.Suspended = suspend; }

    private async void RunIconSmokeTest()
    {
        var report = Path.Combine(AppContext.BaseDirectory, "icon-smoke-test.txt");
        try
        {
            var paths = new List<string> { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe") };
            foreach (var location in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
            {
                var directory = Environment.GetFolderPath(location);
                if (Directory.Exists(directory)) paths.AddRange(Directory.EnumerateFiles(directory, "*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true }).Take(3));
            }
            foreach (var path in paths)
            {
                var icon = await ApplicationIcons.Load(path);
                if (icon == null || icon.PixelWidth <= 0 || icon.PixelHeight <= 0) throw new InvalidOperationException("Icon extraction failed: " + path);
            }
            File.WriteAllText(report, $"PASS: Shell icon extraction for executable and {paths.Count - 1} Start menu shortcuts.");
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { Quit(); }
    }
    private async void RunShortcutSmokeTest()
    {
        var report = Path.Combine(AppContext.BaseDirectory, "shortcut-smoke-test.txt");
        try { File.WriteAllText(report, await ShortcutSmokeTest.Run(this, panel!)); }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { Quit(); }
    }
    private async void RunThemeSmokeTest()
    {
        var original = Config;
        var report = Path.Combine(AppContext.BaseDirectory, "theme-smoke-test.txt");
        try
        {
            OpenSettings();
            foreach (var theme in new[] { "light", "dark", "system", "dark", "light", "system" })
            {
                Config = original with { Theme = theme };
                panel!.Refresh(); settings!.ApplyTheme();
                await Task.Delay(400);
                panel.Show();
                await Task.Delay(200);
                panel.Hide(false);
            }
            File.WriteAllText(report, "PASS: six theme transitions, hidden/visible Acrylic panel, settings window. Configuration unchanged.");
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { Config = original; Quit(); }
    }

    private async void RunControlSmokeTest()
    {
        var count = 0;
        var observations = new List<string>();
        void Count() => count++;
        desktop!.Toggle += Count;
        desktop.DoubleTapDiagnostic = message => observations.Add(message);
        var report = Path.Combine(AppContext.BaseDirectory, "control-smoke-test.txt");
        try
        {
            panel!.Hide(false);
            foreach (var key in new ushort[] { 0xA2, 0xA3, 0xA4, 0xA5, 0xA0, 0xA1 })
            {
                var before = count;
                desktop.Configure(Config with { DoubleTapKey = key switch { 0xA2 => "leftControl", 0xA3 => "rightControl", 0xA4 => "leftAlt", 0xA5 => "rightAlt", 0xA0 => "leftShift", _ => "rightShift" } });
                for (var tap = 0; tap < 2; tap++)
                {
                    foreach (var flags in new uint[] { 0, 2 })
                    {
                        var input = new Native.Input { Type = 1, Data = new() { Keyboard = new()
                        {
                            Key = key, Flags = flags | (key is 0xA3 or 0xA5 ? 1u : 0), ExtraInfo = 0x54455354
                        } } };
                        if (Native.SendInput(1, [input], System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>()) != 1)
                            throw new InvalidOperationException("Test input rejected");
                        await Task.Delay(60);
                    }
                }
                await Task.Delay(500);
                observations.Add($"0x{key:X}: {count - before}");
            }
            File.WriteAllText(report, count == 6 ? "PASS: left/right Control, Alt and Shift each toggle once through the global hook." : $"FAIL: expected six toggles, got {count}. {string.Join(", ", observations)}");
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { desktop.Toggle -= Count; Quit(); }
    }

    private async void RunPanelSmokeTest()
    {
        var report = Path.Combine(AppContext.BaseDirectory, "panel-smoke-test.txt");
        Window? probe = null;
        try
        {
            panel!.Show(); await Task.Delay(300);
            var region = Native.CreateRoundRectRgn(0, 0, 1, 1, 1, 1);
            try
            {
                if (Native.GetWindowRgn(panel.Handle, region) == 0 || Native.PtInRegion(region, 1, 1) || !Native.PtInRegion(region, 80, 40))
                    throw new InvalidOperationException("Rounded window region failed");
            }
            finally { Native.DeleteObject(region); }
            if (Native.GetForegroundWindow() != panel.Handle) throw new InvalidOperationException("Panel did not acquire focus");
            var inputs = new uint[] { 0, 2 }.Select(flags => new Native.Input { Type = 1, Data = new() { Keyboard = new() { Key = 0x1B, Flags = flags, ExtraInfo = 0x54455354 } } }).ToArray();
            Native.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
            await Task.Delay(300);
            if (Native.IsWindowVisible(panel.Handle)) throw new InvalidOperationException("Escape did not hide panel");
            panel.Show(); await Task.Delay(200);
            probe = new Window { Title = "Keyboard Launcher focus test", Content = new Microsoft.UI.Xaml.Controls.TextBlock { Text = "Focus test" } };
            probe.Activate(); Native.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(probe));
            await Task.Delay(500);
            if (Native.IsWindowVisible(panel.Handle)) throw new InvalidOperationException("Focus loss did not hide panel");
            File.WriteAllText(report, "PASS: rounded window region, Escape through keyboard hook, automatic hide on focus loss.");
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { probe?.Close(); Quit(); }
    }

    internal void OpenSettings()
    {
        panel?.Hide(false);
        if (settings == null)
        {
            settings = new SettingsWindow(this);
            settings.Closed += (_, _) => { settings = null; SuspendHotkeys(false); };
        }
        settings.Activate();
        Native.SetForegroundWindow(WinRT.Interop.WindowNative.GetWindowHandle(settings));
    }

    internal void Quit()
    {
        desktop?.Dispose(); desktop = null;
        activationWait?.Unregister(null); activationEvent?.Dispose();
        settings?.Close(); panel?.Close();
        instance?.Dispose(); instance = null;
        Exit();
    }
}
