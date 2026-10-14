using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardLauncher;

internal static class ShortcutRecorder
{
    internal static void Attach(App app, TextBox field, Window window, bool requireModifier)
    {
        if (!string.IsNullOrWhiteSpace(field.Text))
        {
            try { field.Text = Core.Hotkey.Parse(field.Text, requireModifier).ToDisplayString(); }
            catch (FormatException) { /* Preserve invalid saved values for correction. */ }
        }
        AttachRecording(app, field, window, key =>
        {
            try
            {
                var value = key.ToDisplayString();
                Core.Hotkey.Parse(value, requireModifier);
                field.Text = value;
                field.PlaceholderText = L.T("点击后按下快捷键");
            }
            catch (FormatException ex) { field.PlaceholderText = ex.Message; }
        }, false);
    }

    internal static void AttachDoubleTap(App app, TextBox field, Window window, Action<string> recorded)
    {
        AttachRecording(app, field, window, key => recorded(key.Key switch
        {
            0xA0 => "leftShift", 0xA1 => "rightShift", 0xA2 => "leftControl",
            0xA3 => "rightControl", 0xA4 => "leftAlt", 0xA5 => "rightAlt", 0x20 => "space",
            _ => throw new InvalidOperationException("Unsupported double-tap key")
        }), true);
    }

    private static void AttachRecording(App app, TextBox field, Window window, Action<Hotkey> recorded, bool modifierOnly)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        field.IsReadOnly = true;
        field.IsSpellCheckEnabled = false;
        field.PlaceholderText = L.T(modifierOnly ? "点击后按下 Ctrl、Alt、Shift 或 Space" : "点击后按下快捷键");
        void Begin() => app.BeginShortcutRecording(hwnd, recorded, modifierOnly);
        field.GotFocus += (_, _) => Begin();
        field.LostFocus += (_, _) => app.EndShortcutRecording(hwnd);
        field.Unloaded += (_, _) => app.EndShortcutRecording(hwnd);
        window.Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) app.EndShortcutRecording(hwnd);
            else if (field.IsLoaded && field.FocusState != FocusState.Unfocused) Begin();
        };
        window.Closed += (_, _) => app.EndShortcutRecording(hwnd);
    }
}
