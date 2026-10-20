using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;

namespace KeyboardLauncher;

// An explicit recording action, rather than an editable or focus-activated field.
internal sealed class ShortcutCapture : ContentControl
{
    private static readonly Style RecordingStyle = (Style)XamlReader.Load("""
        <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
            <Setter Property="BorderBrush" Value="{ThemeResource AccentFillColorDefaultBrush}" />
        </Style>
        """);
    private readonly App app;
    private readonly Window window;
    private readonly nint hwnd;
    private readonly bool singleKey;
    private readonly bool requireModifier, cancelOnEscape;
    private readonly string label;
    private readonly Func<string?, string?> save;
    private readonly Button trigger = new PointerButton
    {
        MinHeight = 44, Padding = new Thickness(12, 7, 12, 7), CornerRadius = new CornerRadius(10),
        HorizontalContentAlignment = HorizontalAlignment.Left, HorizontalAlignment = HorizontalAlignment.Stretch
    };
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer timeout;
    private bool completing;
    internal bool IsRecording { get; private set; }
    internal string? Value { get; private set; }
    internal event Action? ValueChanged;

    internal ShortcutCapture(App app, Window window, bool singleKey, string label, string? value, Func<string?, string?> save, bool requireModifier = true, bool cancelOnEscape = true)
    {
        this.app = app; this.window = window; this.singleKey = singleKey;
        this.label = label; this.save = save; Value = value;
        this.requireModifier = requireModifier; this.cancelOnEscape = cancelOnEscape;
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        Width = 228;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Content = trigger;
        timeout = DispatcherQueue.CreateTimer();
        timeout.Interval = TimeSpan.FromSeconds(15);
        timeout.Tick += (_, _) => StopRecording();
        trigger.Click += (_, _) => { if (IsRecording) StopRecording(); else StartRecording(); };
        trigger.LostFocus += (_, _) => StopRecording();
        Loaded += (_, _) => { window.Activated += WindowActivated; window.Closed += WindowClosed; };
        Unloaded += (_, _) =>
        {
            StopRecording(); window.Activated -= WindowActivated; window.Closed -= WindowClosed;
        };
        Render();
    }

    private void WindowActivated(object sender, WindowActivatedEventArgs e)
    {
        if (e.WindowActivationState == WindowActivationState.Deactivated) StopRecording();
    }
    private void WindowClosed(object sender, WindowEventArgs e) => StopRecording();

    internal void StartRecording()
    {
        if (!IsLoaded || IsRecording) return;
        trigger.Focus(FocusState.Programmatic);
        IsRecording = true; completing = false;
        app.BeginShortcutRecording(hwnd, Recorded, singleKey, cancelOnEscape ? StopRecording : null);
        timeout.Start(); Render();
    }

    internal void StopRecording()
    {
        if (!IsRecording) return;
        IsRecording = false;
        timeout.Stop(); app.EndShortcutRecording(hwnd); Render();
    }

    private async void Recorded(Hotkey key)
    {
        if (!IsRecording || completing) return;
        string value;
        try
        {
            value = singleKey ? key.Key switch
            {
                0xA0 => "leftShift", 0xA1 => "rightShift", 0xA2 => "leftControl", 0xA3 => "rightControl",
                0xA4 => "leftAlt", 0xA5 => "rightAlt", 0x20 => "space", _ => throw new FormatException()
            } : key.ToShortcutString();
            if (!singleKey) Hotkey.Parse(value, requireModifier);
        }
        catch (FormatException) { return; }
        completing = true;
        // Keep intercepting until the entire gesture is released; holding a key
        // must not leak repeats into the settings window after capture completes.
        while (IsRecording && app.HasPressedKeys) await Task.Delay(15);
        if (!IsRecording) return;
        StopRecording();
        Value = save(value); Render(); ValueChanged?.Invoke();
    }

    internal void Clear()
    {
        StopRecording(); Value = save(null); Render(); ValueChanged?.Invoke();
    }

    private void Render()
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        row.Children.Add(new FontIcon { Glyph = "\uE765", FontSize = 15, Opacity = IsRecording ? 1 : .55, Margin = new Thickness(0, 0, 3, 0) });
        if (IsRecording)
        {
            trigger.Style = RecordingStyle;
            row.Children.Add(new TextBlock { Text = L.T(singleKey ? "请按下单个键…" : "请按下组合键…"), FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            if (cancelOnEscape) row.Children.Add(Keycap("Esc"));
        }
        else
        {
            trigger.Style = null;
            if (string.IsNullOrWhiteSpace(Value))
                row.Children.Add(new TextBlock { Text = L.T("点击录入"), FontSize = 13, Opacity = .6 });
            else
            {
                var keys = new[] { DoubleTapLabel(Value) };
                if (!singleKey)
                {
                    try { keys = Hotkey.Parse(Value, requireModifier).ToDisplayString().Split('+', StringSplitOptions.TrimEntries); }
                    catch (FormatException) { keys = [Value]; }
                }
                for (int i = 0; i < keys.Length; i++)
                {
                    if (i > 0) row.Children.Add(new TextBlock { Text = "+", FontSize = 12, Opacity = .45, VerticalAlignment = VerticalAlignment.Center });
                    row.Children.Add(Keycap(keys[i]));
                }
            }
        }
        trigger.Content = new Viewbox { Child = row, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, MaxWidth = 202, Height = 28, HorizontalAlignment = HorizontalAlignment.Left };
        AutomationProperties.SetName(trigger, label + ": " + (IsRecording ? L.T("正在录入") : string.IsNullOrWhiteSpace(Value) ? L.T("未设置") : singleKey ? DoubleTapLabel(Value) : Value));
        ToolTipService.SetToolTip(trigger, L.T(IsRecording ? cancelOnEscape ? "按 Esc 或点击别处取消录入。" : "再次点击或点击别处取消录入。" : "点击后录入快捷键"));
    }

    private static Border Keycap(string text)
    {
        var cap = (Border)XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    Background="{ThemeResource ControlFillColorDefaultBrush}"
                    BorderBrush="{ThemeResource ControlStrokeColorDefaultBrush}" BorderThickness="1"
                    CornerRadius="4" Padding="6,2" VerticalAlignment="Center" />
            """);
        cap.Child = new TextBlock { Text = text, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.Medium };
        return cap;
    }

    private static string DoubleTapLabel(string? value) => L.T(value switch
    {
        "leftControl" => "左 Ctrl", "rightControl" => "右 Ctrl", "leftAlt" => "左 Alt", "rightAlt" => "右 Alt",
        "leftShift" => "左 Shift", "rightShift" => "右 Shift", "space" => "Space",
        "control" => "Ctrl（任意一侧）", "alt" => "Alt（任意一侧）", "shift" => "Shift（任意一侧）", _ => value ?? ""
    });
}
