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
            var modifiers = new List<Hotkey>();
            app.BeginShortcutRecording(hwnd, key => modifiers.Add(key), modifierOnly: true);
            foreach (ushort modifier in new ushort[] { 0xA0, 0xA1, 0xA2, 0xA3, 0xA4, 0xA5 })
            {
                Send(modifier);
                await Task.Delay(100);
                Require(modifiers.LastOrDefault() == new Hotkey(0, modifier), "double-tap side recording");
            }
            Require(modifiers.Count == 6, "double-tap recording count");
            Send(0xA2, 0x41); Send(0xA4, 0xA0); Send(0x41);
            await Task.Delay(150);
            Require(modifiers.Count == 6, "chord or ordinary key accepted as double-tap key");
            Require(delivered == 0, "double-tap recording leaked keys");
            app.EndShortcutRecording(hwnd);
            var doubleTapField = new TextBox();
            var ordinaryField = new TextBox();
            var fields = new StackPanel(); fields.Children.Add(ordinaryField); fields.Children.Add(doubleTapField);
            probe.Content = fields;
            string? selected = null;
            ShortcutRecorder.Attach(app, ordinaryField, probe, true);
            ShortcutRecorder.AttachDoubleTap(app, doubleTapField, probe, value => { selected = value; doubleTapField.Text = value; });
            await Task.Delay(100);
            foreach (var (key, value) in new[] { ((ushort)0xA4, "leftAlt"), ((ushort)0xA5, "rightAlt"), ((ushort)0x12, "leftAlt"), ((ushort)0x20, "space") })
            {
                Native.ActivatePanel(hwnd);
                Require(ordinaryField.Focus(FocusState.Programmatic), "ordinary field could not receive focus");
                await Task.Delay(100);
                Require(doubleTapField.Focus(FocusState.Programmatic), "double-tap field could not receive focus");
                await Task.Delay(100);
                selected = null;
                Send(key, key); // One held key may report repeated down events; it is still valid to record.
                await Task.Delay(150);
                Require(selected == value && doubleTapField.Text == value, $"double-tap field failed to record {value} with repeated key-down");
                var saved = new LauncherConfig { DoubleTapKey = selected };
                new ConfigStore(path).Save(saved);
                Require(new ConfigStore(path).Load().DoubleTapKey == value, "recorded double-tap key not persisted");
            }
            selected = null; Send(0xA4, 0x20); await Task.Delay(150);
            Require(selected == null, "Alt+Space accepted as a single double-tap key");
            probe.Content = input;
            input.Focus(FocusState.Programmatic);
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
            return "PASS: Ctrl+A and Alt+F4 captured without executing; all six double-tap modifier sides recorded; actual fields record held Alt and Space, persist selections, and reject chords; isolated save/reload; real launcher dispatch restores target focus and executes Ctrl+A; modifiers released.";
        }
        finally
        {
            app.EndShortcutRecording(hwnd); app.SuspendHotkeys(false);
            probe.Close();
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
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