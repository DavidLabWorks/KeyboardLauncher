using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardLauncher;

internal static class ShortcutRecorder
{
    internal static void Attach(App app, TextBox field, Window window, bool requireModifier)
    {
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        if (!string.IsNullOrWhiteSpace(field.Text))
        {
            try { field.Text = Core.Hotkey.Parse(field.Text, requireModifier).ToDisplayString(); }
            catch (FormatException) { /* Preserve invalid saved values for correction. */ }
        }
        field.IsReadOnly = true;
        field.IsSpellCheckEnabled = false;
        field.PlaceholderText = L.T("点击后按下快捷键");
        void Begin()
        {
            app.BeginShortcutRecording(hwnd, key =>
            {
                try
                {
                    var value = key.ToDisplayString();
                    Core.Hotkey.Parse(value, requireModifier);
                    field.Text = value;
                    field.PlaceholderText = L.T("点击后按下快捷键");
                }
                catch (FormatException ex) { field.PlaceholderText = ex.Message; }
            });
        }
        field.GotFocus += (_, _) => Begin();
        field.LostFocus += (_, _) => app.EndShortcutRecording(hwnd);
        window.Activated += (_, e) =>
        {
            if (e.WindowActivationState == WindowActivationState.Deactivated) app.EndShortcutRecording(hwnd);
            else if (field.FocusState != FocusState.Unfocused) Begin();
        };
        window.Closed += (_, _) => app.EndShortcutRecording(hwnd);
    }
}