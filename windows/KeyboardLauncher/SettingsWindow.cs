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
    private readonly TextBlock brand = new()
    {
        Text = App.DisplayName, FontSize = 11, Opacity = .6,
        TextWrapping = TextWrapping.NoWrap, TextTrimming = TextTrimming.CharacterEllipsis,
        Margin = new Thickness(8, 0, 0, 0)
    };
    private readonly Button quitButton = new PointerButton { Background = new SolidColorBrush(Colors.Transparent), BorderThickness = new Thickness(0), Padding = new Thickness(8, 6, 8, 6), MinWidth = 0, MinHeight = 36, UseSystemFocusVisuals = true };
    private readonly Grid root = new();
    private readonly StackPanel detail = new() { Spacing = 24, Padding = new Thickness(28), MaxWidth = 740, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListView navigation = new() { Margin = new Thickness(10, 10, 10, 0), SelectionMode = ListViewSelectionMode.Single };
    private readonly Grid body = (Grid)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
              Background="{ThemeResource SolidBackgroundFillColorBaseBrush}" />
        """);
    private readonly Border sidebarSurface = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                Background="{ThemeResource LayerFillColorDefaultBrush}" Margin="8,6,0,8" CornerRadius="12" />
        """);
    private readonly TextBlock titleText = new() { FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };

    internal SettingsWindow(App app)
    {
        this.app = app; Title = L.T("指间 · Keyboard Launcher 设置"); SystemBackdrop = new MicaBackdrop();
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        var dpi = Math.Max(96u, Native.GetDpiForWindow(WinRT.Interop.WindowNative.GetWindowHandle(this))) / 96d;
        var presenter = (Microsoft.UI.Windowing.OverlappedPresenter)AppWindow.Presenter;
        presenter.IsMaximizable = true;
        presenter.PreferredMinimumWidth = (int)(880 * dpi);
        presenter.PreferredMinimumHeight = (int)(560 * dpi);
        AppWindow.Resize(new SizeInt32((int)(880 * dpi), (int)(620 * dpi)));
        root.ColumnDefinitions.Add(new() { Width = new GridLength(220) }); root.ColumnDefinitions.Add(new());
        root.RowDefinitions.Add(new() { Height = new GridLength(44) }); root.RowDefinitions.Add(new());
        var titleBar = new Grid { Background = new SolidColorBrush(Colors.Transparent), Padding = new Thickness(16, 0, 150, 0) };
        var titleContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, VerticalAlignment = VerticalAlignment.Center };
        titleContent.Children.Add(Ui.AppIcon(18)); titleContent.Children.Add(titleText); titleBar.Children.Add(titleContent);
        Grid.SetColumnSpan(titleBar, 2); root.Children.Add(titleBar);
        ExtendsContentIntoTitleBar = true; SetTitleBar(titleBar);
        var sidebar = new Grid(); sidebar.RowDefinitions.Add(new()); sidebar.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var labels = new[] { L.T("常规"), L.T("快捷键"), L.T("系统"), L.T("关于") };
        var glyphs = new[] { "\uE713", "\uE765", "\uE770", "\uE946" };
        for (var i = 0; i < labels.Length; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Padding = new Thickness(2, 5, 0, 5) };
            row.Children.Add(new FontIcon { Glyph = glyphs[i], FontSize = 18, Width = 24, VerticalAlignment = VerticalAlignment.Center });
            row.Children.Add(new TextBlock { Text = labels[i], FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            navigation.Items.Add(new PointerListViewItem { Template = RoundedTemplate("ListViewItem"), Content = row, Tag = labels[i], MinHeight = 44, CornerRadius = new CornerRadius(8), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 2, 0, 2), HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        navigation.SelectionChanged += (_, _) => ShowPage(); sidebar.Children.Add(navigation);
        brand.Text = App.DisplayName;
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(18, 12, 18, 16) };
        SetQuitLabel(); quitButton.HorizontalAlignment = HorizontalAlignment.Stretch;
        quitButton.Style = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="Button">
                <Setter Property="Foreground" Value="{ThemeResource TextFillColorSecondaryBrush}" />
            </Style>
            """);
        quitButton.HorizontalContentAlignment = HorizontalAlignment.Left;
        quitButton.Click += (_, _) => app.Quit();
        footer.Children.Add(quitButton); footer.Children.Add(brand);
        Grid.SetRow(footer, 1); sidebar.Children.Add(footer); sidebarSurface.Child = sidebar; Grid.SetRow(sidebarSurface, 1); root.Children.Add(sidebarSurface);
        var scroll = new ScrollViewer { Content = detail, HorizontalContentAlignment = HorizontalAlignment.Center, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        scroll.SizeChanged += (_, e) => detail.Width = Math.Min(740, e.NewSize.Width);
        body.Children.Add(scroll); Grid.SetRow(body, 1); Grid.SetColumn(body, 1); root.Children.Add(body);
        root.Language = app.Config.Language; Content = root; root.ActualThemeChanged += (_, _) => ApplySurface(); ApplyTheme(); navigation.SelectedIndex = 0;
    }

    internal void ApplyLanguage()
    {
        root.Language = app.Config.Language;
        brand.Text = App.DisplayName;
        SetQuitLabel();
        Title = L.T("指间 · Keyboard Launcher 设置");
        var labels = new[] { L.T("常规"), L.T("快捷键"), L.T("系统"), L.T("关于") };
        for (int i = 0; i < labels.Length; i++)
        {
            var item = (ListViewItem)navigation.Items[i];
            item.Tag = labels[i];
            ((TextBlock)((StackPanel)item.Content).Children[1]).Text = labels[i];
        }
        ShowPage();
    }

    private void SetQuitLabel()
    {
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        label.Children.Add(new FontIcon { Glyph = "\uF3B1", FontFamily = new FontFamily("Segoe Fluent Icons"), FontSize = 14, Width = 24, VerticalAlignment = VerticalAlignment.Center });
        label.Children.Add(new TextBlock { Text = L.T("退出"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
        quitButton.Content = label;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(quitButton, L.T("退出"));
    }

    internal bool IsContentCentered => Math.Abs(
        detail.TransformToVisual(body).TransformPoint(new Windows.Foundation.Point()).X
        + detail.ActualWidth / 2 - body.ActualWidth / 2) < 2;

    internal void ShowAbout() { navigation.SelectedIndex = 3; }

    internal void ApplyTheme() { root.RequestedTheme = Ui.Theme(app.Config.Theme); ApplySurface(); }
    private void ApplySurface()
    {
        bool dark = root.ActualTheme == ElementTheme.Dark;
        root.Background = new SolidColorBrush(Colors.Transparent);
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        var immersiveDark = dark ? 1 : 0;
        Native.DwmSetWindowAttribute(handle, 20, ref immersiveDark, sizeof(int));
        var titleBackground = Colors.Transparent;
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
    private static ControlTemplate RoundedTemplate(string control) => (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load($$"""
        <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="{{control}}">
            <Border x:Name="Surface" Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" Padding="{TemplateBinding Padding}">
                <VisualStateManager.VisualStateGroups>
                    <VisualStateGroup x:Name="CommonStates">
                        <VisualState x:Name="Normal" />
                        <VisualState x:Name="Selected"><VisualState.Setters><Setter Target="Surface.Background" Value="{ThemeResource SubtleFillColorSecondaryBrush}" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="PointerOver"><VisualState.Setters><Setter Target="Surface.Background" Value="{ThemeResource SubtleFillColorSecondaryBrush}" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="PointerOverSelected"><VisualState.Setters><Setter Target="Surface.Background" Value="{ThemeResource SubtleFillColorTertiaryBrush}" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="Pressed"><VisualState.Setters><Setter Target="Surface.Background" Value="{ThemeResource SubtleFillColorTertiaryBrush}" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="PressedSelected"><VisualState.Setters><Setter Target="Surface.Background" Value="{ThemeResource SubtleFillColorTertiaryBrush}" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="Disabled"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.4" /></VisualState.Setters></VisualState>
                    </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
                <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" />
            </Border>
        </ControlTemplate>
        """);
    private static Button SettingsButton(string title) => Ui.ActionButton(title);
    private static TextBlock Note(string text) => Ui.SecondaryText(text);
    private static Border Card(UIElement content, double radius = 12)
    {
        var card = Ui.Card(content); card.CornerRadius = new CornerRadius(radius); return card;
    }
    private void Section(string title, params UIElement[] children)
    {
        var section = new StackPanel { Spacing = 10 };
        if (title != titleText.Text)
            section.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        var group = new StackPanel { Spacing = 20 };
        for (var i = 0; i < children.Length; i++)
        {
            if (i > 0) group.Children.Add((Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Height="1" Background="{ThemeResource DividerStrokeColorDefaultBrush}" />
                """));
            group.Children.Add(children[i]);
        }
        section.Children.Add(Card(group)); detail.Children.Add(section);
    }
    private static Border SegmentedPicker(string name, (string Label, string Value)[] options, string selected, Action<string> changed)
    {
        var grid = new Grid();
        var host = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    Background="{ThemeResource ControlFillColorSecondaryBrush}" Padding="3" CornerRadius="8"
                    HorizontalAlignment="Right" VerticalAlignment="Center" />
            """);
        host.Child = grid; host.Width = options.Length == 2 ? 154 : 210;
        var selectedStyle = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="RadioButton">
                <Setter Property="Background" Value="{ThemeResource AccentFillColorDefaultBrush}" />
                <Setter Property="Foreground" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
            </Style>
            """);
        var group = Guid.NewGuid().ToString();
        var template = (ControlTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="RadioButton">
                <Border Background="{TemplateBinding Background}" CornerRadius="{TemplateBinding CornerRadius}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}">
                    <ContentPresenter Content="{TemplateBinding Content}" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
            </ControlTemplate>
            """);
        var buttons = new List<RadioButton>();
        RadioButton? hovered = null;
        void Paint()
        {
            foreach (var button in buttons)
            {
                bool active = button.IsChecked == true;
                button.Style = active ? selectedStyle : null;
                button.ClearValue(Control.ForegroundProperty);
                if (active) button.ClearValue(Control.BackgroundProperty);
                else button.Background = new SolidColorBrush(button == hovered ? Windows.UI.Color.FromArgb(25, 128, 128, 128) : Colors.Transparent);
                button.BorderBrush = new SolidColorBrush(Colors.Transparent);
                button.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            }
        }
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i]; grid.ColumnDefinitions.Add(new());
            var button = new PointerRadioButton { Content = option.Label, GroupName = group, IsChecked = option.Value == selected,
                Template = template, Height = 32, MinHeight = 32, MinWidth = 0, FontSize = 13, Padding = new Thickness(0),
                CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(.5), HorizontalAlignment = HorizontalAlignment.Stretch,
                UseSystemFocusVisuals = true };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, name + ": " + option.Label);
            button.Checked += (_, _) => { Paint(); changed(option.Value); };
            button.Unchecked += (_, _) => Paint();
            button.PointerEntered += (_, _) => { hovered = button; Paint(); };
            button.PointerExited += (_, _) => { if (hovered == button) hovered = null; Paint(); };
            buttons.Add(button); Grid.SetColumn(button, i); grid.Children.Add(button);
        }
        host.Loaded += (_, _) => Paint(); host.ActualThemeChanged += (_, _) => Paint(); Paint();
        return host;
    }
    private static Grid Row(string title, string description, FrameworkElement control)
    {
        var row = new Grid { ColumnSpacing = 24, MinHeight = 44 };
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium });
        if (description.Length > 0) label.Children.Add(Note(description));
        row.Children.Add(label);
        control.HorizontalAlignment = HorizontalAlignment.Right;
        if (control is ToggleSwitch toggle)
        {
            toggle.MinWidth = 0;
            toggle.Width = 44;
            toggle.OnContent = null;
            toggle.OffContent = null;
        }
        FrameworkElement interactive = control is ToggleSwitch ? new PointerHost { Content = control, HorizontalAlignment = HorizontalAlignment.Right, HorizontalContentAlignment = HorizontalAlignment.Right } : control;
        Grid.SetColumn(interactive, 1); row.Children.Add(interactive); return row;
    }
    private async void Save(LauncherConfig config)
    {
        try { app.Save(config); }
        catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); ShowPage(); }
    }
    private void ShowPage()
    {
        ApplySurface();
        detail.Children.Clear(); titleText.Text = Title = (navigation.SelectedItem as ListViewItem)?.Tag?.ToString() ?? L.T("常规");
        detail.Children.Add(new TextBlock { Text = titleText.Text, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        if (app.StartupError != null) detail.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning, Title = L.T("启动提示"), Message = app.StartupError });
        switch (navigation.SelectedIndex) { case 0: General(); break; case 1: Shortcuts(); break; case 2: SystemSettings(); break; case 3: About(); break; }
        if (navigation.SelectedIndex != 3) detail.Children.Add(Note(L.T("更改会自动保存。")));
    }
    private void General()
    {
        var theme = SegmentedPicker(L.T("外观"),
            [(L.T("系统"), "system"), (L.T("浅色"), "light"), (L.T("深色"), "dark")], app.Config.Theme,
            value => DispatcherQueue.TryEnqueue(() => { if (app.Config.Theme != value) Save(app.Config with { Theme = value }); }));
        var startup = new ToggleSwitch { IsOn = !StoreIntegration.IsPackaged && StartupEnabled(), OnContent = "", OffContent = "", MinWidth = 44, VerticalAlignment = VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(startup, L.T("登录时启动"));
        var loadingStartup = false;
        startup.Loaded += async (_, _) =>
        {
            if (!StoreIntegration.IsPackaged) return;
            loadingStartup = true; startup.IsEnabled = false;
            try { startup.IsOn = await StoreIntegration.StartupEnabledAsync(); }
            catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); }
            finally { loadingStartup = false; startup.IsEnabled = true; }
        };
        startup.Toggled += async (_, _) =>
        {
            if (loadingStartup) return;
            try
            {
                if (StoreIntegration.IsPackaged) { await StoreIntegration.SetStartupAsync(startup.IsOn); return; }
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
                if (startup.IsOn) key.SetValue("KeyboardLauncher", LocalInstallation.StartupCommand);
                else key.DeleteValue("KeyboardLauncher", false);
            }
            catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); ShowPage(); }
        };
        var language = SegmentedPicker(L.T("语言"), [("English", "en"), ("简体中文", "zh-CN")], app.Config.Language,
            value => DispatcherQueue.TryEnqueue(() => { if (app.Config.Language != value) Save(app.Config with { Language = value }); }));
        Section(L.T("常规"), Row(L.T("语言"), L.T("选择界面语言，立即生效。"), language), Row(L.T("外观"), L.T("选择主题或跟随系统。"), theme), Row(L.T("登录时启动"), L.T("登录后自动启动应用。"), startup));
    }
    private void About()
    {
        var brand = new Grid { ColumnSpacing = 16 };
        brand.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        brand.ColumnDefinitions.Add(new());
        brand.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        brand.Children.Add(Ui.AppIcon(56));
        var name = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = App.DisplayName, FontSize = 20, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        name.Children.Add(Note(L.F("版本 {0}", typeof(App).Assembly.GetName().Version!.ToString(3))));
        Grid.SetColumn(name, 1); brand.Children.Add(name);
        var check = SettingsButton(L.T("检测更新"));
        if (StoreIntegration.IsPackaged) { Grid.SetColumn(check, 2); brand.Children.Add(check); }
        else
        {
            var releases = new HyperlinkButton { Content = L.T("查看新版本"), FontSize = 13,
                NavigateUri = new Uri("https://github.com/DavidLabWorks/KeyboardLauncher/releases"), VerticalAlignment = VerticalAlignment.Center };
            ToolTipService.SetToolTip(releases, L.T("前往 GitHub 查看发布版本。"));
            Grid.SetColumn(releases, 2); brand.Children.Add(releases);
        }
        var overview = new StackPanel { Spacing = 14 }; overview.Children.Add(brand);
        overview.Children.Add(Note(L.T("你的应用，一键即达。")));
        if (!StoreIntegration.IsPackaged) overview.Children.Add(Note(L.T("本地安装版本 · 从 GitHub 获取更新")));
        var github = new HyperlinkButton { Content = "GitHub · DavidLabWorks/KeyboardLauncher", NavigateUri = new Uri("https://github.com/DavidLabWorks/KeyboardLauncher"), HorizontalAlignment = HorizontalAlignment.Left, Padding = new Thickness(0), FontSize = 13 };
        ToolTipService.SetToolTip(github, L.T("源代码与发布版本。")); overview.Children.Add(github);
        var status = Note(""); status.Visibility = Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(status, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        var release = new HyperlinkButton { Content = L.T("在 Microsoft Store 更新"), NavigateUri = new Uri("ms-windows-store://downloadsandupdates"), Visibility = Visibility.Collapsed, HorizontalAlignment = HorizontalAlignment.Left };
        check.Click += async (_, _) =>
        {
            status.Visibility = Visibility.Visible; check.IsEnabled = false; check.Content = L.T("正在检测…"); status.Text = ""; release.Visibility = Visibility.Collapsed;
            try
            {
                if (!StoreIntegration.IsPackaged) throw new InvalidOperationException();
                var store = Windows.Services.Store.StoreContext.GetDefault();
                WinRT.Interop.InitializeWithWindow.Initialize(store, WinRT.Interop.WindowNative.GetWindowHandle(this));
                var product = await store.GetStoreProductForCurrentAppAsync();
                if (product.ExtendedError != null) throw product.ExtendedError;
                if (product.Product == null)
                {
                    status.Text = L.T("此应用尚未关联商店，暂时无法检测更新。");
                    return;
                }
                var updates = await store.GetAppAndOptionalStorePackageUpdatesAsync();
                status.Text = L.T(updates.Count > 0 ? "发现可用更新，请前往 Microsoft Store 安装。" : "当前已是最新版本。");
                release.Visibility = updates.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { status.Text = L.T("无法检测更新，请重试。"); }
            finally { check.IsEnabled = true; check.Content = L.T("检测更新"); }
        };
        overview.Children.Add(status); overview.Children.Add(release);
        detail.Children.Add(Card(overview));
        if (StoreIntegration.Required) LicenseSection();
    }
    private void LicenseSection()
    {
        var status = Note(app.Licensing.Status);
        var message = Note(""); message.Visibility = Visibility.Collapsed;
        var buy = SettingsButton(L.T("购买完整版"));
        var restore = SettingsButton(L.T("恢复购买")); restore.IsEnabled = app.Licensing.Available;
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(buy); actions.Children.Add(restore);
        bool busy = false;
        void RefreshStatus()
        {
            status.Text = app.Licensing.Status;
            buy.IsEnabled = !busy && app.Licensing.Available && app.Licensing.License is not { IsActive: true, IsTrial: false };
            restore.IsEnabled = !busy && app.Licensing.Available;
        }
        RefreshStatus();
        status.Loaded += (_, _) => { app.Licensing.Changed += RefreshStatus; RefreshStatus(); };
        status.Unloaded += (_, _) => app.Licensing.Changed -= RefreshStatus;
        buy.Click += async (_, _) =>
        {
            busy = true; RefreshStatus();
            var result = await app.Licensing.PurchaseAsync(WinRT.Interop.WindowNative.GetWindowHandle(this));
            message.Visibility = Visibility.Visible;
            message.Text = result == null ? L.T("购买成功，完整版已解锁。") : L.T(result);
            busy = false; RefreshStatus();
        };
        restore.Click += async (_, _) =>
        {
            busy = true; RefreshStatus(); message.Text = "";
            await app.Licensing.RefreshAsync();
            busy = false; RefreshStatus();
        };
        var license = new StackPanel { Spacing = 12 };
        license.Children.Add(Note(L.T("免费试用 7 天，之后一次性购买完整版。")));
        license.Children.Add(status); license.Children.Add(actions); license.Children.Add(message);
        Section(L.T("授权"), license);
    }
    private static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("KeyboardLauncher") is string value && value.Equals(LocalInstallation.StartupCommand, StringComparison.OrdinalIgnoreCase);
    }
    private void Shortcuts()
    {
        var shortcut = new ShortcutCapture(app, this, false, L.T("键盘快捷键"), app.Config.Shortcut, value =>
        {
            Save(app.Config with { Shortcut = value ?? "" });
            return app.Config.Shortcut;
        });
        var doubleTap = new ShortcutCapture(app, this, true, L.T("双击按键"), app.Config.DoubleTapKey, value =>
        {
            Save(app.Config with { DoubleTapKey = value });
            return app.Config.DoubleTapKey;
        });
        var tripleTap = new ShortcutCapture(app, this, true, L.T("三击按键"), app.Config.TripleTapKey, value =>
        {
            Save(app.Config with { TripleTapKey = value });
            return app.Config.TripleTapKey;
        });
        StackPanel CaptureRow(ShortcutCapture capture)
        {
            var clear = SettingsButton(L.T("清空")); clear.MinHeight = 44; clear.MinWidth = 64;
            void RefreshClear() => clear.IsEnabled = !string.IsNullOrWhiteSpace(capture.Value);
            capture.ValueChanged += RefreshClear;
            clear.Click += (_, _) => capture.Clear();
            RefreshClear();
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(capture); row.Children.Add(clear);
            return row;
        }
        static StackPanel Setting(string title, string description, FrameworkElement control)
        {
            var group = new StackPanel { Spacing = 12 };
            var row = new Grid { ColumnSpacing = 16 };
            row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = title, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(control, 1); row.Children.Add(control);
            group.Children.Add(row); group.Children.Add(Note(description)); return group;
        }
        var normal = Setting(L.T("键盘快捷键"), L.T("点击录入器后按下组合键，松开后保存。"), CaptureRow(shortcut));
        var doubleGroup = Setting(L.T("双击按键"), L.T("点击录入器后按下 Ctrl、Alt、Shift 或 Space；修饰键区分左右。"), CaptureRow(doubleTap));
        static ComboBox ActionPicker(string label, string selected, string[] values, Action<string> changed)
        {
            var picker = new PointerComboBox { MinWidth = 228 };
            foreach (var value in values)
                picker.Items.Add(L.T(value switch { "toggle" => "显示或隐藏面板", "panel" => "打开面板", _ => "打开面板并聚焦搜索" }));
            picker.SelectedIndex = Array.IndexOf(values, selected);
            picker.SelectionChanged += (_, _) => { if (picker.SelectedIndex >= 0) changed(values[picker.SelectedIndex]); };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(picker, label);
            return picker;
        }
        var doubleAction = ActionPicker(L.T("双击动作"), app.Config.DoubleTapAction, ["toggle", "panel", "search"],
            value => Save(app.Config with { DoubleTapAction = value }));
        doubleGroup.Children.Add(Row(L.T("双击动作"), L.T("连续轻按两次执行所选动作；清空按键即可关闭双击。"), doubleAction));
        var tripleGroup = Setting(L.T("三击按键"), L.T("连续轻按同一个键三次；清空按键即可关闭三击。"), CaptureRow(tripleTap));
        var action = ActionPicker(L.T("三击动作"), app.Config.TripleTapAction, ["panel", "search"],
            value => Save(app.Config with { TripleTapAction = value }));
        tripleGroup.Children.Add(Row(L.T("三击动作"), "", action));
        var timingHint = Note(L.F("双击松开后会等待 {0} 毫秒，期间再次轻按同一键即可触发三击。", ModifierTapGestures.GapMs));
        void RefreshTimingHint() => timingHint.Visibility = DoubleModifierTap.KeysOverlap(doubleTap.Value, tripleTap.Value)
            ? Visibility.Visible : Visibility.Collapsed;
        doubleTap.ValueChanged += RefreshTimingHint;
        tripleTap.ValueChanged += RefreshTimingHint;
        RefreshTimingHint();
        tripleGroup.Children.Add(timingHint);
        Section(L.T("快捷键"), normal, doubleGroup, tripleGroup);
    }
    private void SystemSettings()
    {
        var suppress = new ToggleSwitch { IsOn = app.Config.SuppressSystemShortcut, OnContent = "", OffContent = "", MinWidth = 44 };
        suppress.Toggled += (_, _) => Save(app.Config with { SuppressSystemShortcut = suppress.IsOn });
        Section(L.T("系统"), Row(L.T("覆盖冲突"), L.T("优先响应启动器，而非系统快捷键。"), suppress));
        var open = SettingsButton(L.T("打开文件夹"));
        open.Click += async (_, _) => { try { Directory.CreateDirectory(Path.GetDirectoryName(app.Store.FilePath)!); Process.Start(new ProcessStartInfo(Path.GetDirectoryName(app.Store.FilePath)!) { UseShellExecute = true })?.Dispose(); } catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); } };
        Section(L.T("配置"), Row(L.T("配置文件"), L.T("保存键位绑定，每次更改保留上一版本。"), open));
    }
}
