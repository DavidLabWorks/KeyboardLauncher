using System.Diagnostics;
using KeyboardLauncher.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Win32;
using Windows.Graphics;

namespace KeyboardLauncher;

internal sealed class SettingsWindow : Window
{
    private readonly App app;
    private readonly Grid root = new();
    private readonly StackPanel detail = new() { Spacing = 28, Padding = new Thickness(28), MaxWidth = 740, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListView navigation = new() { Margin = new Thickness(10, 18, 10, 0), SelectionMode = ListViewSelectionMode.Single };
    private readonly TextBlock heading = new() { FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Margin = new Thickness(28, 20, 28, 0) };
    private readonly Grid body = new();

    internal SettingsWindow(App app)
    {
        this.app = app; Title = "Keyboard Launcher 设置"; SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var dpi = Math.Max(96u, Native.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96d;
        AppWindow.Resize(new SizeInt32((int)(880 * dpi), (int)(650 * dpi)));
        root.ColumnDefinitions.Add(new() { Width = new GridLength(200) }); root.ColumnDefinitions.Add(new());
        var sidebar = new Grid(); sidebar.RowDefinitions.Add(new()); sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var labels = new[] { "常规", "快捷键", "系统" };
        var glyphs = new[] { "\uE713", "\uE765", "\uE770" };
        var colors = new[] { Colors.Gray, Colors.DodgerBlue, Colors.MediumPurple };
        for (var i = 0; i < labels.Length; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(0, 5, 0, 5) };
            row.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(colors[i]), Child = new FontIcon { Glyph = glyphs[i], FontSize = 13, Foreground = new SolidColorBrush(Colors.White) } });
            row.Children.Add(new TextBlock { Text = labels[i], FontSize = 13, VerticalAlignment = VerticalAlignment.Center });
            navigation.Items.Add(new PointerListViewItem { Content = row, Tag = labels[i] });
        }
        navigation.SelectionChanged += (_, _) => ShowPage(); sidebar.Children.Add(navigation);
        var brand = new TextBlock { Text = "Keyboard Launcher", FontSize = 12, Opacity = .6, Margin = new Thickness(18) };
        Grid.SetRow(brand, 1); sidebar.Children.Add(brand); root.Children.Add(sidebar);
        body.RowDefinitions.Add(new() { Height = GridLength.Auto }); body.RowDefinitions.Add(new()); body.Children.Add(heading);
        var scroll = new ScrollViewer { Content = detail, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        Grid.SetRow(scroll, 1); body.Children.Add(scroll); Grid.SetColumn(body, 1); root.Children.Add(body);
        Content = root; root.ActualThemeChanged += (_, _) => ApplySurface(); ApplyTheme(); navigation.SelectedIndex = 0;
    }

    internal void ApplyTheme() { root.RequestedTheme = Ui.Theme(app.Config.Theme); ApplySurface(); }
    private void ApplySurface()
    {
        bool dark = root.ActualTheme == ElementTheme.Dark;
        body.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 29, 29, 29) : Windows.UI.Color.FromArgb(255, 245, 245, 245));
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var immersiveDark = dark ? 1 : 0;
        Native.DwmSetWindowAttribute(handle, 20, ref immersiveDark, sizeof(int));
        var titleBackground = dark ? Windows.UI.Color.FromArgb(255, 40, 40, 40) : Windows.UI.Color.FromArgb(255, 245, 245, 245);
        var titleForeground = dark ? Colors.White : Windows.UI.Color.FromArgb(255, 35, 35, 35);
        var bar = AppWindow.TitleBar;
        bar.BackgroundColor = bar.InactiveBackgroundColor = titleBackground;
        bar.ForegroundColor = titleForeground;
        bar.InactiveForegroundColor = dark ? Colors.LightGray : Colors.Gray;
        bar.ButtonBackgroundColor = bar.ButtonInactiveBackgroundColor = titleBackground;
        bar.ButtonForegroundColor = titleForeground;
        bar.ButtonInactiveForegroundColor = bar.InactiveForegroundColor;
        bar.ButtonHoverBackgroundColor = dark ? Windows.UI.Color.FromArgb(255, 65, 65, 65) : Windows.UI.Color.FromArgb(255, 225, 225, 225);
        bar.ButtonHoverForegroundColor = bar.ButtonPressedForegroundColor = titleForeground;
        bar.ButtonPressedBackgroundColor = dark ? Windows.UI.Color.FromArgb(255, 80, 80, 80) : Windows.UI.Color.FromArgb(255, 210, 210, 210);
    }
    private static TextBlock Note(string text) => new() { Text = text, FontSize = 12, Opacity = .6, TextWrapping = TextWrapping.Wrap };
    private static Border Card(UIElement content, double radius = 14)
    {
        var card = new Border { Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(radius) };
        void Paint() => card.Background = new SolidColorBrush(card.ActualTheme == ElementTheme.Dark ? Windows.UI.Color.FromArgb(255, 40, 40, 40) : Windows.UI.Color.FromArgb(255, 253, 253, 253));
        card.ActualThemeChanged += (_, _) => Paint(); Paint(); return card;
    }
    private void Section(string title, params UIElement[] children)
    {
        var section = new StackPanel { Spacing = 10 };
        section.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = .6, Margin = new Thickness(6, 0, 0, 0) });
        var group = new StackPanel { Spacing = 18 };
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0) group.Children.Add(new Border { Height = 1, Background = new SolidColorBrush(Windows.UI.Color.FromArgb(20, 128, 128, 128)) });
            group.Children.Add(children[i]);
        }
        section.Children.Add(Card(group)); detail.Children.Add(section);
    }
    private static Grid Row(string title, string description, FrameworkElement control)
    {
        var row = new Grid { ColumnSpacing = 20 };
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium }); label.Children.Add(Note(description));
        row.Children.Add(label);
        FrameworkElement interactive = control is ToggleSwitch ? new PointerHost { Content = control, HorizontalContentAlignment = HorizontalAlignment.Stretch } : control;
        Grid.SetColumn(interactive, 1); row.Children.Add(interactive); return row;
    }
    private async void Save(LauncherConfig config)
    {
        try { app.Save(config); }
        catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); ShowPage(); }
    }
    private void ShowPage()
    {
        detail.Children.Clear(); heading.Text = (navigation.SelectedItem as ListViewItem)?.Tag?.ToString() ?? "常规";
        if (app.StartupError != null) detail.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning, Title = "启动提示", Message = app.StartupError });
        switch (navigation.SelectedIndex) { case 0: General(); break; case 1: Shortcuts(); break; case 2: SystemSettings(); break; }
        detail.Children.Add(Note("更改会自动保存。"));
    }
    private void General()
    {
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        brand.Children.Add(Ui.AppIcon(76));
        var name = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = "Keyboard Launcher", FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        name.Children.Add(Note("你的应用，一键即达。")); name.Children.Add(Note("版本 1.0")); brand.Children.Add(name); detail.Children.Add(Card(brand, 18));
        var theme = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, VerticalAlignment = VerticalAlignment.Center };
        var modes = new[] { "system", "light", "dark" }; var labels = new[] { "系统", "浅色", "深色" };
        for (var i = 0; i < modes.Length; i++)
        {
            var mode = modes[i]; var choice = new PointerRadioButton { Content = labels[i], GroupName = "Theme", IsChecked = app.Config.Theme == mode, MinWidth = 62, FontSize = 12 };
            choice.Click += (_, _) => DispatcherQueue.TryEnqueue(() => { if (app.Config.Theme != mode) Save(app.Config with { Theme = mode }); }); theme.Children.Add(choice);
        }
        var startup = new ToggleSwitch { IsOn = StartupEnabled(), OnContent = "", OffContent = "", MinWidth = 44, VerticalAlignment = VerticalAlignment.Center };
        startup.Toggled += async (_, _) =>
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startup.IsOn) key.SetValue("KeyboardLauncher", "\"" + Environment.ProcessPath + "\" --background"); else key.DeleteValue("KeyboardLauncher", false);
            }
            catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); }
        };
        Section("常规", Row("外观", "选择主题或跟随系统。", theme), Row("登录时启动", "登录后自动启动应用。", startup));
        Section("权限", Row("快捷键访问", "双击快捷键已启用，无需额外辅助功能授权。", new FontIcon { Glyph = "\uE73E", FontSize = 18, Foreground = new SolidColorBrush(Colors.MediumSeaGreen) }));
    }
    private static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("KeyboardLauncher") is string value && value.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase);
    }
    private void Shortcuts()
    {
        var shortcut = new TextBox { Text = app.Config.Shortcut, Width = 200, PlaceholderText = "未设置", FontSize = 13 };
        ShortcutRecorder.Attach(app, shortcut, this, true);
        shortcut.LostFocus += (_, _) => { if (shortcut.Text.Trim() != app.Config.Shortcut) Save(app.Config with { Shortcut = shortcut.Text.Trim() }); };
        var clear = new PointerButton { Content = "清空", MinWidth = 60, Height = 32, Padding = new Thickness(12, 0, 12, 0), IsEnabled = !string.IsNullOrWhiteSpace(shortcut.Text) };
        shortcut.TextChanged += (_, _) => clear.IsEnabled = !string.IsNullOrWhiteSpace(shortcut.Text);
        clear.Click += (_, _) =>
        {
            app.EndShortcutRecording(WinRT.Interop.WindowNative.GetWindowHandle(this));
            shortcut.Text = "";
            Save(app.Config with { Shortcut = "" });
        };
        var shortcutRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        shortcutRow.Children.Add(shortcut); shortcutRow.Children.Add(clear);
        var doubleTap = new PointerComboBox { Width = 268, HorizontalAlignment = HorizontalAlignment.Stretch };
        foreach (var (title, value) in new[] { ("关闭", ""), ("左 Ctrl", "leftControl"), ("右 Ctrl", "rightControl"), ("左 Alt", "leftAlt"), ("右 Alt", "rightAlt"), ("左 Shift", "leftShift"), ("右 Shift", "rightShift"), ("Ctrl（任意一侧）", "control"), ("Alt（任意一侧）", "alt"), ("Shift（任意一侧）", "shift") })
            doubleTap.Items.Add(new ComboBoxItem { Content = title, Tag = value });
        doubleTap.SelectedItem = doubleTap.Items.Cast<ComboBoxItem>().FirstOrDefault(item => (string)item.Tag == (app.Config.DoubleTapKey ?? "")) ?? doubleTap.Items[0];
        doubleTap.SelectionChanged += (_, _) =>
        {
            var value = (doubleTap.SelectedItem as ComboBoxItem)?.Tag as string;
            var next = string.IsNullOrEmpty(value) ? null : value;
            if (next != app.Config.DoubleTapKey) Save(app.Config with { DoubleTapKey = next });
        };
        Section("快捷键", Row("键盘快捷键", "点击输入框后直接按下组合键。", shortcutRow), Row("双击按键", "连续按两次所选按键以显示或隐藏。", doubleTap));
        detail.Children.Add(Note("仅响应所选侧的按键；选择任意一侧时，两次须为同一侧。长按、组合键和鼠标点击会取消检测。"));
    }
    private void SystemSettings()
    {
        var suppress = new ToggleSwitch { IsOn = app.Config.SuppressSystemShortcut, OnContent = "", OffContent = "", MinWidth = 44 };
        suppress.Toggled += (_, _) => Save(app.Config with { SuppressSystemShortcut = suppress.IsOn });
        Section("系统", Row("覆盖冲突", "优先响应启动器，而非系统快捷键。", suppress));
        var open = new PointerButton { Content = "打开文件夹", FontSize = 12 };
        open.Click += async (_, _) => { try { Directory.CreateDirectory(Path.GetDirectoryName(app.Store.FilePath)!); Process.Start(new ProcessStartInfo(Path.GetDirectoryName(app.Store.FilePath)!) { UseShellExecute = true })?.Dispose(); } catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); } };
        Section("配置", Row("配置文件", "保存键位绑定，每次更改保留上一版本。", open));
    }
}
