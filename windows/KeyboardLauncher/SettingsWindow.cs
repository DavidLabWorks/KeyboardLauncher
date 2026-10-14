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
    private readonly TextBlock brand = new() { FontSize = 10, Opacity = .45, Margin = new Thickness(0), TextWrapping = TextWrapping.Wrap };
    private readonly Button quitButton = new PointerButton { Template = RoundedTemplate("Button"), Background = new SolidColorBrush(Colors.Transparent), Foreground = new SolidColorBrush(Colors.Gray), Padding = new Thickness(0, 4, 0, 4), MinWidth = 0, MinHeight = 28, UseSystemFocusVisuals = true };
    private readonly Grid root = new();
    private readonly StackPanel detail = new() { Spacing = 28, Padding = new Thickness(28), MaxWidth = 740, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly ListView navigation = new() { Margin = new Thickness(10, 10, 10, 0), SelectionMode = ListViewSelectionMode.Single };
    private readonly Grid body = new();
    private readonly Border sidebarSurface = new() { Margin = new Thickness(8, 6, 0, 8), CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(.5), Shadow = new ThemeShadow(), Translation = new System.Numerics.Vector3(0, 0, 8) };
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
        var colors = new[] { Colors.Gray, Colors.DodgerBlue, Colors.MediumPurple, Colors.DodgerBlue };
        for (var i = 0; i < labels.Length; i++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, Padding = new Thickness(0, 5, 0, 5) };
            row.Children.Add(new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(colors[i]), Child = new FontIcon { Glyph = glyphs[i], FontSize = 13, Foreground = new SolidColorBrush(Colors.White) } });
            row.Children.Add(new TextBlock { Text = labels[i], FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium, VerticalAlignment = VerticalAlignment.Center });
            navigation.Items.Add(new PointerListViewItem { Template = RoundedTemplate("ListViewItem"), Content = row, Tag = labels[i], CornerRadius = new CornerRadius(9), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 2, 0, 2), HorizontalContentAlignment = HorizontalAlignment.Stretch });
        }
        navigation.SelectionChanged += (_, _) => ShowPage(); sidebar.Children.Add(navigation);
        brand.Text = App.DisplayName;
        var footer = new StackPanel { Spacing = 8, Margin = new Thickness(18, 8, 18, 14) };
        footer.Children.Add(new Border { Height = 1, Opacity = .2, Background = new SolidColorBrush(Colors.Gray), Margin = new Thickness(0, 0, 0, 4) });
        SetQuitLabel(); quitButton.HorizontalAlignment = HorizontalAlignment.Left;
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
        var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7 };
        label.Children.Add((UIElement)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Viewbox xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="16" Height="16" VerticalAlignment="Center">
                <Path Width="20" Height="20" Data="M 11,3 L 4,3 Q 2,3 2,5 L 2,15 Q 2,17 4,17 L 11,17 M 8,10 L 18,10 M 14,6 L 18,10 L 14,14" Stroke="Gray" StrokeThickness="1.5" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" />
            </Viewbox>
            """));
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
        body.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 29, 29, 29) : Windows.UI.Color.FromArgb(255, 245, 245, 245));
        root.Background = new SolidColorBrush(Colors.Transparent);
        sidebarSurface.Background = new AcrylicBrush
        {
            TintColor = dark ? Windows.UI.Color.FromArgb(255, 31, 43, 52) : Windows.UI.Color.FromArgb(255, 218, 240, 250),
            TintOpacity = .72,
            FallbackColor = dark ? Windows.UI.Color.FromArgb(255, 34, 45, 54) : Windows.UI.Color.FromArgb(255, 224, 242, 251)
        };
        sidebarSurface.BorderBrush = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(30, 255, 255, 255) : Windows.UI.Color.FromArgb(210, 255, 255, 255));
        var selected = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 65, 65, 65) : Windows.UI.Color.FromArgb(255, 220, 220, 220));
        foreach (ListViewItem item in navigation.Items)
            item.Background = item.IsSelected ? selected : new SolidColorBrush(Colors.Transparent);
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
                        <VisualState x:Name="Selected" />
                        <VisualState x:Name="PointerOver"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.8" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="PointerOverSelected"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.8" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="Pressed"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.6" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="PressedSelected"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.6" /></VisualState.Setters></VisualState>
                        <VisualState x:Name="Disabled"><VisualState.Setters><Setter Target="Surface.Opacity" Value="0.4" /></VisualState.Setters></VisualState>
                    </VisualStateGroup>
                </VisualStateManager.VisualStateGroups>
                <ContentPresenter Content="{TemplateBinding Content}" ContentTemplate="{TemplateBinding ContentTemplate}" Foreground="{TemplateBinding Foreground}" HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="Center" />
            </Border>
        </ControlTemplate>
        """);
    private static Button SettingsButton(string title)
    {
        var button = new PointerButton { Content = title, Template = RoundedTemplate("Button"), CornerRadius = new CornerRadius(8), Padding = new Thickness(12, 6, 12, 6), FontSize = 13, MinHeight = 30, UseSystemFocusVisuals = true, HorizontalContentAlignment = HorizontalAlignment.Center };
        void Paint()
        {
            bool dark = button.ActualTheme == ElementTheme.Dark;
            button.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 65, 65, 65) : Windows.UI.Color.FromArgb(255, 236, 236, 236));
            button.Foreground = new SolidColorBrush(dark ? Colors.White : Windows.UI.Color.FromArgb(255, 35, 35, 35));
        }
        button.Loaded += (_, _) => Paint(); button.ActualThemeChanged += (_, _) => Paint(); Paint();
        return button;
    }
    private static TextBlock Note(string text) => new() { Text = text, FontSize = 12, Opacity = .6, TextWrapping = TextWrapping.Wrap };
    private static Border Card(UIElement content, double radius = 8)
    {
        var card = new Border { Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(radius) };
        void Paint() => card.Background = new SolidColorBrush(card.ActualTheme == ElementTheme.Dark ? Windows.UI.Color.FromArgb(255, 40, 40, 40) : Windows.UI.Color.FromArgb(255, 253, 253, 253));
        card.Loaded += (_, _) => Paint(); card.ActualThemeChanged += (_, _) => Paint(); Paint(); return card;
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
    private static Border SegmentedPicker(string name, (string Label, string Value)[] options, string selected, Action<string> changed)
    {
        var grid = new Grid();
        var host = new Border { Child = grid, Width = options.Length == 2 ? 154 : 210, HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(3), CornerRadius = new CornerRadius(8), VerticalAlignment = VerticalAlignment.Center };
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
            bool dark = host.ActualTheme == ElementTheme.Dark;
            host.Background = new SolidColorBrush(dark ? Windows.UI.Color.FromArgb(255, 55, 55, 55) : Windows.UI.Color.FromArgb(255, 232, 232, 232));
            foreach (var button in buttons)
            {
                bool active = button.IsChecked == true;
                button.Background = new SolidColorBrush(active
                    ? Windows.UI.Color.FromArgb(255, 0, 110, 245)
                    : button == hovered ? Windows.UI.Color.FromArgb(25, 128, 128, 128) : Colors.Transparent);
                button.Foreground = new SolidColorBrush(active || dark ? Colors.White : Windows.UI.Color.FromArgb(255, 35, 35, 35));
                button.BorderBrush = new SolidColorBrush(active ? Windows.UI.Color.FromArgb(15, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0, dark ? (byte)255 : (byte)0) : Colors.Transparent);
                button.FontWeight = active ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal;
            }
        }
        for (int i = 0; i < options.Length; i++)
        {
            var option = options[i]; grid.ColumnDefinitions.Add(new());
            var button = new PointerRadioButton { Content = option.Label, GroupName = group, IsChecked = option.Value == selected,
                Template = template, Height = 28, MinHeight = 0, MinWidth = 0, FontSize = 12, Padding = new Thickness(0),
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
        var row = new Grid { ColumnSpacing = 20 };
        row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var label = new StackPanel { Spacing = 5, VerticalAlignment = VerticalAlignment.Center };
        label.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap, FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium }); label.Children.Add(Note(description));
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
                if (startup.IsOn) key.SetValue("KeyboardLauncher", "\"" + Environment.ProcessPath + "\" --background");
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
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 20 };
        brand.Children.Add(Ui.AppIcon(76));
        var name = new StackPanel { Spacing = 7, VerticalAlignment = VerticalAlignment.Center };
        name.Children.Add(new TextBlock { Text = App.DisplayName, FontSize = 24, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        name.Children.Add(Note(L.T("你的应用，一键即达。"))); name.Children.Add(Note(L.F("版本 {0}", typeof(App).Assembly.GetName().Version!.ToString(3)))); brand.Children.Add(name); detail.Children.Add(Card(brand));
        var github = new HyperlinkButton { Content = "DavidLabWorks/KeyboardLauncher", NavigateUri = new Uri("https://github.com/DavidLabWorks/KeyboardLauncher") };
        var check = SettingsButton(L.T("检测更新"));
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
        Section(L.T("关于"), Row("GitHub", L.T("源代码与发布版本。"), github),
            Row(L.T("版本"), L.F("版本 {0}", typeof(App).Assembly.GetName().Version!.ToString(3)), check));
        detail.Children.Add(status); detail.Children.Add(release);
        LicenseSection();
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
        Section(L.T("授权"), Note(L.T("免费试用 7 天，之后一次性购买完整版。")), status, actions);
        detail.Children.Add(message);
    }
    private static bool StartupEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        return key?.GetValue("KeyboardLauncher") is string value && value.Contains(Environment.ProcessPath ?? "\0", StringComparison.OrdinalIgnoreCase);
    }
    private void Shortcuts()
    {
        var shortcut = new TextBox { Text = app.Config.Shortcut, Width = 200, PlaceholderText = L.T("未设置"), FontSize = 13 };
        ShortcutRecorder.Attach(app, shortcut, this, true);
        shortcut.LostFocus += (_, _) => { if (shortcut.Text.Trim() != app.Config.Shortcut) Save(app.Config with { Shortcut = shortcut.Text.Trim() }); };
        var clear = SettingsButton(L.T("清空")); clear.IsEnabled = !string.IsNullOrWhiteSpace(shortcut.Text);
        shortcut.TextChanged += (_, _) => clear.IsEnabled = !string.IsNullOrWhiteSpace(shortcut.Text);
        clear.Click += (_, _) =>
        {
            app.EndShortcutRecording(WinRT.Interop.WindowNative.GetWindowHandle(this));
            shortcut.Text = "";
            Save(app.Config with { Shortcut = "" });
        };
        var shortcutRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        shortcutRow.Children.Add(shortcut); shortcutRow.Children.Add(clear);
        string DoubleTapLabel(string? value) => L.T(value switch
        {
            "leftControl" => "左 Ctrl", "rightControl" => "右 Ctrl",
            "leftAlt" => "左 Alt", "rightAlt" => "右 Alt",
            "leftShift" => "左 Shift", "rightShift" => "右 Shift",
            "space" => "Space",
            "control" => "Ctrl（任意一侧）", "alt" => "Alt（任意一侧）", "shift" => "Shift（任意一侧）",
            _ => ""
        });
        var doubleTap = new TextBox { Text = DoubleTapLabel(app.Config.DoubleTapKey), Width = 200, FontSize = 13 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(doubleTap, L.T("双击按键"));
        var clearDoubleTap = SettingsButton(L.T("清空")); clearDoubleTap.IsEnabled = app.Config.DoubleTapKey != null;
        ShortcutRecorder.AttachDoubleTap(app, doubleTap, this, value =>
        {
            if (value != app.Config.DoubleTapKey) Save(app.Config with { DoubleTapKey = value });
            doubleTap.Text = DoubleTapLabel(app.Config.DoubleTapKey);
            clearDoubleTap.IsEnabled = app.Config.DoubleTapKey != null;
        });
        clearDoubleTap.Click += (_, _) =>
        {
            app.EndShortcutRecording(WinRT.Interop.WindowNative.GetWindowHandle(this));
            Save(app.Config with { DoubleTapKey = null });
            doubleTap.Text = DoubleTapLabel(app.Config.DoubleTapKey);
            clearDoubleTap.IsEnabled = app.Config.DoubleTapKey != null;
        };
        var doubleTapRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        doubleTapRow.Children.Add(doubleTap); doubleTapRow.Children.Add(clearDoubleTap);
        Section(L.T("快捷键"), Row(L.T("键盘快捷键"), L.T("点击输入框后直接按下组合键。"), shortcutRow), Row(L.T("双击按键"), L.T("点击输入框后按下 Ctrl、Alt、Shift 或 Space；修饰键区分左右。"), doubleTapRow));
        detail.Children.Add(Note(L.T("连续按两次录入的按键以显示或隐藏；清空后关闭。")));
        detail.Children.Add(Note(L.T("仅响应所选侧的按键；选择任意一侧时，两次须为同一侧。长按、组合键和鼠标点击会取消检测。")));
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
