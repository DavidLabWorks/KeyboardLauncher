using System.Runtime.InteropServices;
using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardLauncher;

internal static class ShortcutSmokeTest
{
    internal static async Task<string> Run(App app, PanelWindow panel)
    {
        var input = new TextBox { Text = "Keyboard Launcher shortcut execution", Margin = new Thickness(20) };
        var probe = new Window { Title = "Keyboard Launcher shortcut check", Content = input };
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(probe);
        var recorded = new List<Hotkey>();
        int delivered = 0;
        input.KeyDown += (_, _) => delivered++;
        var path = Path.Combine(Path.GetTempPath(), "KeyboardLauncher-shortcut-" + Guid.NewGuid() + ".json");
        try
        {
            panel.Hide(false); probe.Activate(); Native.ActivatePanel(hwnd);
            input.Focus(FocusState.Programmatic); input.Select(0, 0);
            await Task.Delay(200);
            app.SuspendHotkeys(true);
            app.BeginShortcutRecording(hwnd, key => recorded.Add(key));
            Send(0xA2, 0x41); // Ctrl+A must be captured, not select text.
            await Task.Delay(150);
            Require(recorded.Count == 1 && recorded[0] == new Hotkey(2, 0x41), "Ctrl+A recording");
            Require(delivered == 0 && input.SelectionLength == 0, "recorded keys leaked into application");
            Send(0xA4, 0x73); // Alt+F4 must not close the recorder.
            await Task.Delay(150);
            Require(Native.IsWindow(hwnd) && recorded.Count == 2 && recorded[1] == new Hotkey(1, 0x73), "Alt+F4 escaped recording");
            app.EndShortcutRecording(hwnd); app.SuspendHotkeys(false);
            Require(!app.HasPressedKeys && Native.Modifiers == 0, "stuck recording modifiers");
            // Persist and reload in an isolated file, then exercise the real panel launch path.
            var store = new ConfigStore(path);
            store.Save(new LauncherConfig { Launchers = [new Launcher { Name = "Select all", KeyboardShortcut = recorded[0].ToShortcutString() }] });
            var binding = store.Load().At(0)!;
            panel.Show();
            await Task.Delay(150);
            await panel.ExecuteForSmokeTest(binding, hwnd);
            await Task.Delay(200);
            Require(Native.GetForegroundWindow() == hwnd, "previous app focus not restored");
            Require(input.SelectedText == input.Text && delivered > 0, "saved shortcut did not execute");
            Require(!app.HasPressedKeys && Native.Modifiers == 0, "stuck execution modifiers");
            return "PASS: Ctrl+A and Alt+F4 captured without executing; isolated save/reload; real launcher dispatch restores target focus and executes Ctrl+A; modifiers released.";
        }
        finally
        {
            app.EndShortcutRecording(hwnd); app.SuspendHotkeys(false);
            probe.Close();
            if (File.Exists(path)) File.Delete(path);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static void Send(params ushort[] keys)
    {
        Native.Input Make(ushort key, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { Key = key, Flags = up ? 2u : 0, ExtraInfo = 0x54455354 } } };
        var inputs = keys.Select(k => Make(k, false)).Concat(keys.Reverse().Select(k => Make(k, true))).ToArray();
        if (Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>()) != inputs.Length) throw new InvalidOperationException("Test input rejected");
    }
}