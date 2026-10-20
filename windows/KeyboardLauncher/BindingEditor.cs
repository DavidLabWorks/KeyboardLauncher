using KeyboardLauncher.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;
using Windows.Storage.Pickers;

namespace KeyboardLauncher;

internal static class BindingEditor
{
    internal static async Task Show(App app, XamlRoot root, nint hwnd, int slot)
    {
        var editor = new EditorWindow(app, hwnd, slot);
        Native.EnableWindow(hwnd, false);
        try { editor.Activate(); await editor.Completion; }
        finally { Native.EnableWindow(hwnd, true); Native.ActivatePanel(hwnd); }
    }

    private sealed class EditorWindow : Window
    {
        private readonly App app;
        private readonly int slot;
        private readonly nint handle;
        private readonly TaskCompletionSource completion = new();
        internal Task Completion => completion.Task;
        private readonly Grid root = (Grid)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                  Background="{ThemeResource SolidBackgroundFillColorBaseBrush}" />
            """);
        private readonly TextBox name = new() { FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Padding = new Thickness(12, 7, 12, 7) };
        private readonly TextBox application = new(), url = new(), command = new(), arguments = new();
        private string shortcutValue = "";
        private ShortcutCapture? shortcut;
        private readonly TextBox filePath = new(), folderPath = new();
        private readonly TextBox icon = new() { PlaceholderText = L.T("输入符号、emoji 或图片路径"), FontSize = 13 };
        private readonly Grid preview = new() { Width = 60, Height = 60 };
        private readonly Grid appPreview = new() { Width = 36, Height = 36 };
        private readonly TextBlock appName = new() { Text = L.T("选择应用…"), FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        private readonly StackPanel fields = new() { Spacing = 20 };
        private readonly TextBlock hint = Ui.SecondaryText("");
        private readonly TextBlock error = Ui.ValidationText();
        private readonly TextBlock nameError = Ui.ValidationText();
        private readonly Button save = Ui.ActionButton(L.T("保存"));
        private readonly List<Button> segments = [];
        private Button chooseApp = null!;
        private bool scriptFile = true;
        private int selected;
        private List<AppEntry> apps = [];

        internal EditorWindow(App app, nint owner, int slot)
        {
            this.app = app; this.slot = slot; root.Language = app.Config.Language;
            var existing = app.Config.At(slot);
            selected = existing?.KeyboardShortcut != null ? 3 : existing?.ActionType switch { "url" => 1, "command" or "script" => 2, "file" => 4, "folder" => 5, _ => 0 };
            filePath.Text = selected == 4 ? existing?.Exec ?? "" : "";
            folderPath.Text = selected == 5 ? existing?.Exec ?? "" : "";
            name.Text = existing?.Name ?? L.T("新建项目");
            application.Text = selected == 0 ? existing?.Exec ?? "" : "";
            url.Text = selected == 1 ? existing?.Exec ?? "" : "";
            command.Text = selected == 2 ? existing?.Exec ?? "" : "";
            scriptFile = existing?.ActionType != "command";
            arguments.Text = existing?.Arguments ?? "";
            shortcutValue = existing?.KeyboardShortcut ?? "";
            icon.Text = existing?.Icon ?? "";
            Title = L.F("绑定按键 {0}", KeyboardLayout.Keys[slot % KeyboardLayout.Count]);
            handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            Native.SetWindowLongPtr(handle, -8, owner);
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
            var dpi = Math.Max(96u, Native.GetDpiForWindow(owner)) / 96d;
            var monitor = new Native.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
            Native.GetMonitorInfoW(Native.MonitorFromWindow(owner, 2), ref monitor);
            var width = (int)(580 * dpi); var height = Math.Min((int)(632 * dpi), monitor.Work.Bottom - monitor.Work.Top - 32);
            AppWindow.MoveAndResize(new RectInt32(monitor.Work.Left + (monitor.Work.Right - monitor.Work.Left - width) / 2,
                monitor.Work.Top + (monitor.Work.Bottom - monitor.Work.Top - height) / 2, width, height));
            var presenter = (OverlappedPresenter)AppWindow.Presenter;
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
            root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = new GridLength(80) });
            var form = new StackPanel { Spacing = 24, Padding = new Thickness(24) };

            var identity = new Grid { ColumnSpacing = 18 };
            identity.ColumnDefinitions.Add(new() { Width = new GridLength(76) }); identity.ColumnDefinitions.Add(new());
            var iconButton = new PointerButton { Content = preview, Width = 76, Height = 76, Padding = new Thickness(8), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(16) };
            var iconHost = new Grid { Width = 76, Height = 76 };
            iconHost.Children.Add(iconButton);
            var pencil = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                        Width="24" Height="24" CornerRadius="12" Background="{ThemeResource AccentFillColorDefaultBrush}"
                        BorderBrush="{ThemeResource CardBackgroundFillColorDefaultBrush}" BorderThickness="2"
                        HorizontalAlignment="Right" VerticalAlignment="Bottom" Margin="0,0,-3,-3" IsHitTestVisible="False">
                    <FontIcon Glyph="&#xE70F;" FontSize="12" Foreground="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
                </Border>
                """);
            iconHost.Children.Add(pencil); identity.Children.Add(iconHost);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(iconButton, L.T("更改图标"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(name, L.T("名称"));
            BuildIconPicker(iconButton);
            var identityText = new StackPanel { Spacing = 8 };
            identityText.Children.Add(Label(L.T("名称")));
            name.Resources["TextControlCornerRadius"] = new CornerRadius(8);

            identityText.Children.Add(name);
            nameError.Visibility = Visibility.Collapsed;
            identityText.Children.Add(nameError);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(nameError, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetLiveSetting(error, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
            identityText.Children.Add(Ui.SecondaryText(L.T("自定义键盘面板上显示的名称和图标。")));
            Grid.SetColumn(identityText, 1); identity.Children.Add(identityText);
            form.Children.Add(Ui.Card(identity));

            var actionSection = new StackPanel { Spacing = 10 };
            var actionLabel = Label(L.T("动作")); actionSection.Children.Add(actionLabel);
            var action = new StackPanel { Spacing = 20 };
            var segmentRow = new Grid { ColumnSpacing = 8, RowSpacing = 8 };
            for (int column = 0; column < 3; column++) segmentRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            segmentRow.RowDefinitions.Add(new() { Height = GridLength.Auto });
            segmentRow.RowDefinitions.Add(new() { Height = GridLength.Auto });
            var segmentBackground = new Border { Child = segmentRow, HorizontalAlignment = HorizontalAlignment.Stretch };
            var titles = new[] { L.T("打开应用"), L.T("打开网址"), L.T("运行脚本"), L.T("执行快捷键"), L.T("打开文件"), L.T("打开文件夹") };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var button = new PointerButton { Content = titles[i], FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Height = 36, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(8, 0, 8, 0), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(8) };
                button.Click += (_, _) => { selected = index; UpdateFields(); };
                var position = new[] { 0, 3, 4, 5, 1, 2 }[i];
                segments.Add(button); Grid.SetColumn(button, position % 3); Grid.SetRow(button, position / 3); segmentRow.Children.Add(button);
            }
            action.Children.Add(segmentBackground);
            var separator = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Height="1"
                        Background="{ThemeResource DividerStrokeColorDefaultBrush}" />
                """);
            action.Children.Add(separator); action.Children.Add(fields); action.Children.Add(error);
            actionSection.Children.Add(Ui.Card(action)); form.Children.Add(actionSection);
            var info = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 0, 6, 0) };
            info.Children.Add(new FontIcon { Glyph = "\uE946", FontSize = 12, Opacity = .65 }); info.Children.Add(hint);
            form.Children.Add(info);
            root.Children.Add(new ScrollViewer { Content = form, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            var footer = (Grid)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                      Padding="24,20" BorderThickness="0,1,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}" />
                """);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var cancel = Ui.ActionButton(L.T("取消")); cancel.MinWidth = save.MinWidth = 96;
            cancel.Click += (_, _) => Close();
            save.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            save.Click += (_, _) => Save();
            buttons.Children.Add(cancel); buttons.Children.Add(save); footer.Children.Add(buttons); Grid.SetRow(footer, 1); root.Children.Add(footer);
            BuildAppPicker();
            foreach (var field in new[] { name, application, url, command, arguments, icon, filePath, folderPath })
                field.TextChanged += (_, _) => { UpdatePreview(); Validate(); };
            root.RequestedTheme = Ui.Theme(app.Config.Theme);
            Content = root; root.ActualThemeChanged += (_, _) => Paint(); root.Loaded += (_, _) => Paint();
            root.PreviewKeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape && shortcut?.IsRecording != true) { e.Handled = true; Close(); } };
            Closed += (_, _) => { shortcut?.StopRecording(); completion.TrySetResult(); };
            UpdateFields(); Paint();
            _ = LoadApps();
        }

        private bool Dark => root.ActualTheme == ElementTheme.Dark;
        private static TextBlock Label(string text) => new() { Text = text, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium };
        private void Paint()
        {
            PaintSegments();
            var dark = Dark ? 1 : 0;
            Native.DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var caption = Dark ? 0x1D1D1D : 0xF5F5F5;
            var foreground = Dark ? 0xFFFFFF : 0x232323;
            Native.DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
            Native.DwmSetWindowAttribute(handle, 36, ref foreground, sizeof(int));
        }
        private void PaintSegments()
        {
            for (int i = 0; i < segments.Count; i++)
            {
                segments[i].Style = i == selected ? (Style)Application.Current.Resources["AccentButtonStyle"] : null;
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(segments[i], $"{segments[i].Content}{(i == selected ? L.T("，已选择") : "")}");
            }
        }
        private static StackPanel Field(string title, TextBox box, string placeholder)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(Label(title)); box.PlaceholderText = placeholder;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(box, title);
            box.FontSize = 13; box.Resources["TextControlCornerRadius"] = new CornerRadius(8);
            panel.Children.Add(box); return panel;
        }
        private void UpdateFields()
        {
            shortcut?.StopRecording();
            app.EndShortcutRecording(handle);
            // Detach retained text boxes before rebuilding action-specific wrappers.
            foreach (var container in fields.Children.OfType<StackPanel>()) container.Children.Clear();
            fields.Children.Clear();
            switch (selected)
            {
                case 0: fields.Children.Add(chooseApp); fields.Children.Add(Field(L.T("参数   可选"), arguments, L.T("例如 C:\\Projects\\my-app"))); break;
                case 1: fields.Children.Add(Field(L.T("网址"), url, "https://example.com")); break;
                case 2: fields.Children.Add(BuildScriptPicker()); break;
                case 4: fields.Children.Add(BuildPathPicker(false)); break;
                case 5: fields.Children.Add(BuildPathPicker(true)); break;
                case 3:
                    var recorderField = new StackPanel { Spacing = 8 };
                    recorderField.Children.Add(Label(L.T("快捷键")));
                    shortcut = new ShortcutCapture(app, this, false, L.T("快捷键"), shortcutValue, value =>
                    {
                        shortcutValue = value ?? ""; UpdatePreview(); Validate(); return shortcutValue;
                    }, requireModifier: false, cancelOnEscape: false);
                    var captureRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                    var clear = Ui.ActionButton(L.T("清空")); clear.MinHeight = 44; clear.MinWidth = 64;
                    clear.IsEnabled = !string.IsNullOrWhiteSpace(shortcutValue);
                    shortcut.ValueChanged += () => clear.IsEnabled = !string.IsNullOrWhiteSpace(shortcutValue);
                    clear.Click += (_, _) => shortcut.Clear();
                    captureRow.Children.Add(shortcut); captureRow.Children.Add(clear); recorderField.Children.Add(captureRow);
                    fields.Children.Add(recorderField);
                    fields.Children.Add(Ui.SecondaryText(L.T("返回之前的应用，然后执行快捷键。支持全局快捷键。")));
                    break;
            }
            PaintSegments(); UpdatePreview(); Validate();
        }
        private Launcher Draft() => new() { Name = name.Text.Trim(), Exec = selected switch { 0 => application.Text.Trim(), 1 => url.Text.Trim(), 2 => command.Text.Trim(), 4 => filePath.Text, 5 => folderPath.Text, _ => "" },
            ActionType = selected switch { 1 => "url", 2 => scriptFile ? "script" : "command", 4 => "file", 5 => "folder", _ => "application" }, Arguments = selected == 0 ? arguments.Text : "",
            KeyboardShortcut = selected == 3 ? shortcutValue.Trim() : null, Icon = string.IsNullOrWhiteSpace(icon.Text) ? null : icon.Text.Trim() };
        private void Validate()
        {
            nameError.Text = string.IsNullOrWhiteSpace(name.Text) ? L.T("请输入绑定名称。") : "";
            nameError.Visibility = nameError.Text.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
            error.Text = ""; error.Visibility = Visibility.Collapsed;
            try
            {
                if (selected == 2 && scriptFile && !File.Exists(command.Text))
                    throw new FormatException(L.T("脚本文件不存在，请重新选择。"));
                if (selected == 3 && string.IsNullOrWhiteSpace(shortcutValue))
                    throw new FormatException(L.T("请先录入要执行的快捷键。"));
                if (selected == 3) Hotkey.Parse(shortcutValue, false);
                // Validate the action independently so both fields can explain their own state.
                var draft = Draft() with { Name = "Validation" };
                app.Config.Bind(slot, draft).Validate();
                save.IsEnabled = nameError.Text.Length == 0;
            }
            catch (Exception ex)
            {
                error.Text = ex.Message; error.Visibility = Visibility.Visible;
                save.IsEnabled = false;
            }
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(name, nameError.Text);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(save, nameError.Text.Length > 0 ? nameError.Text : error.Text);
        }
        private void Save()
        {
            try { app.Save(app.Config.Bind(slot, Draft())); Close(); }
            catch (Exception ex) { error.Text = ex.Message; error.Visibility = Visibility.Visible; }
        }
        private void UpdatePreview()
        {
            preview.Children.Clear();
            var draft = Draft();
            if (string.IsNullOrWhiteSpace(draft.Exec) && string.IsNullOrWhiteSpace(draft.Icon) && string.IsNullOrWhiteSpace(draft.KeyboardShortcut))
                preview.Children.Add(new FontIcon { Glyph = "\uE710", FontSize = 24, Opacity = .6 });
            else preview.Children.Add(Ui.KeyIcon(draft, 76));
            ToolTipService.SetToolTip(chooseApp, string.IsNullOrWhiteSpace(application.Text) ? L.T("选择应用…") : application.Text);
            appName.Text = string.IsNullOrWhiteSpace(application.Text) ? L.T("选择应用…") : Path.GetFileNameWithoutExtension(application.Text);
            appPreview.Children.Clear();
            appPreview.Children.Add(string.IsNullOrWhiteSpace(application.Text)
                ? new FontIcon { Glyph = "\uE739", FontSize = 27, Opacity = .6 }
                : Ui.KeyIcon(new Launcher { Exec = application.Text }, 48));
            hint.Text = string.IsNullOrWhiteSpace(icon.Text) ? L.T("根据命令自动识别图标") : L.T("使用自定义图标");
        }
        private StackPanel BuildPathPicker(bool folder)
        {
            var target = folder ? folderPath : filePath;
            var panel = new StackPanel { Spacing = 10 };
            var title = L.T(folder ? "选择文件夹…" : "选择文件…");
            var text = new StackPanel { Spacing = 5 };
            var displayName = Path.GetFileName(Path.TrimEndingDirectorySeparator(target.Text));
            text.Children.Add(new TextBlock { Text = string.IsNullOrEmpty(target.Text) ? title : string.IsNullOrEmpty(displayName) ? target.Text : displayName, FontSize = 14, TextWrapping = TextWrapping.Wrap });
            var pathLabel = Ui.SecondaryText(target.Text); pathLabel.MaxLines = 2; pathLabel.TextTrimming = TextTrimming.CharacterEllipsis;
            text.Children.Add(pathLabel);
            ToolTipService.SetToolTip(text, target.Text);
            var button = new PointerButton { Content = text, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12), CornerRadius = new CornerRadius(10) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, title);
            button.Click += async (_, _) =>
            {
                try
                {
                    string? path;
                    if (folder)
                    {
                        var picker = new FolderPicker();
                        WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
                        picker.FileTypeFilter.Add("*");
                        path = (await picker.PickSingleFolderAsync())?.Path;
                    }
                    else path = (await Pick("*"))?.Path;
                    if (path == null) return;
                    target.Text = path;
                    if (string.IsNullOrWhiteSpace(name.Text) || name.Text == L.T("新建项目"))
                    {
                        var chosenName = Path.GetFileName(Path.TrimEndingDirectorySeparator(path));
                        name.Text = string.IsNullOrEmpty(chosenName) ? path : chosenName;
                    }
                    UpdateFields();
                }
                catch (Exception ex) { error.Text = ex.Message; error.Visibility = Visibility.Visible; }
            };
            panel.Children.Add(button);
            return panel;
        }

        private StackPanel BuildScriptPicker()
        {
            var panel = new StackPanel { Spacing = 10 };
            panel.Children.Add(Label(L.T("脚本文件")));
            var text = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            var fileName = new TextBlock { Text = string.IsNullOrWhiteSpace(command.Text) ? L.T("选择脚本文件…")
                : scriptFile ? Path.GetFileName(command.Text) : L.T("已有命令"), FontSize = 14,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
            var path = Ui.SecondaryText(command.Text); path.MaxLines = 2; path.TextTrimming = TextTrimming.CharacterEllipsis;
            path.Visibility = string.IsNullOrWhiteSpace(command.Text) ? Visibility.Collapsed : Visibility.Visible;
            ToolTipService.SetToolTip(text, command.Text);
            text.Children.Add(fileName); text.Children.Add(path);
            var row = new Grid { ColumnSpacing = 12, VerticalAlignment = VerticalAlignment.Center };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(32) }); row.ColumnDefinitions.Add(new());
            row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(new FontIcon { Glyph = "\uE943", FontSize = 26, Opacity = .7, VerticalAlignment = VerticalAlignment.Center });
            Grid.SetColumn(text, 1); row.Children.Add(text);
            var browseIcon = new FontIcon { Glyph = "\uE8B7", FontSize = 18, VerticalAlignment = VerticalAlignment.Center }; Grid.SetColumn(browseIcon, 2); row.Children.Add(browseIcon);
            var button = new PointerButton { Content = row, MinHeight = 64, Padding = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Center,
                BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(10) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, L.T("选择脚本文件…"));
            button.Click += async (_, _) =>
            {
                button.IsEnabled = false;
                try
                {
                    var file = await Pick(".cmd", ".bat", ".ps1");
                    if (file == null) return;
                    scriptFile = true; command.Text = file.Path;
                    fileName.Text = file.Name; path.Text = file.Path; path.Visibility = Visibility.Visible;
                    ToolTipService.SetToolTip(text, file.Path);
                    if (string.IsNullOrWhiteSpace(name.Text) || name.Text == L.T("新建项目")) name.Text = Path.GetFileNameWithoutExtension(file.Name);
                    error.Visibility = Visibility.Collapsed;
                    UpdatePreview(); Validate();
                }
                catch (Exception ex) { error.Text = ex.Message; error.Visibility = Visibility.Visible; }
                finally { button.IsEnabled = true; }
            };
            panel.Children.Add(button);
            panel.Children.Add(Ui.SecondaryText(L.T("支持 .cmd、.bat 和 .ps1，点击选择或更换脚本。")));
            if (!scriptFile) panel.Children.Add(Ui.SecondaryText(L.T("已有命令会保留；选择文件后将替换为脚本。")));
            return panel;
        }
        private void BuildAppPicker()
        {
            var label = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(Ui.SecondaryText(L.T("应用"))); label.Children.Add(appName);
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(36) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(appPreview); Grid.SetColumn(label, 1); row.Children.Add(label);
            var arrow = new FontIcon { Glyph = "\uE70D", FontSize = 11, Opacity = .6 }; Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
            chooseApp = new PointerButton { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(10) };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(chooseApp, L.T("选择应用…"));
            var search = new TextBox { PlaceholderText = L.T("搜索应用"), Width = 420, FontSize = 13, MinHeight = 36 };
            var results = new PointerGridView { Height = 192, Width = 420, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
            results.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <ItemsWrapGrid Orientation="Horizontal" MaximumRowsOrColumns="5"/>
                </ItemsPanelTemplate>
                """);
            results.ItemContainerStyle = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="GridViewItem">
                    <Setter Property="Width" Value="80"/><Setter Property="Height" Value="96"/><Setter Property="ToolTipService.ToolTip" Value="{Binding Name}"/>
                    <Setter Property="Margin" Value="1"/><Setter Property="Padding" Value="4"/>
                    <Setter Property="BorderThickness" Value="0"/><Setter Property="CornerRadius" Value="8"/>
                    <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
                    <Setter Property="VerticalContentAlignment" Value="Center"/>
                </Style>
                """);
            results.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <StackPanel Spacing="8" HorizontalAlignment="Center">
                        <Image Source="{Binding Icon}" Width="40" Height="40" Stretch="Uniform"/>
                        <TextBlock Text="{Binding Name}" FontSize="12" TextAlignment="Center" TextTrimming="CharacterEllipsis" TextWrapping="Wrap" MaxLines="2" Width="70"/>
                    </StackPanel>
                </DataTemplate>
                """);
            var browse = Ui.ActionButton(L.T("浏览应用…")); browse.HorizontalAlignment = HorizontalAlignment.Left;
            var pickerPanel = new StackPanel { Spacing = 12 };
            pickerPanel.Children.Add(search); pickerPanel.Children.Add(results);
            var manualPath = Field(L.T("应用路径"), application, L.T("例如 notepad.exe"));
            manualPath.Children.Remove(application);
            var pathActions = new Grid { ColumnSpacing = 8 };
            pathActions.ColumnDefinitions.Add(new()); pathActions.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            application.HorizontalAlignment = HorizontalAlignment.Stretch;
            pathActions.Children.Add(application); Grid.SetColumn(browse, 1); pathActions.Children.Add(browse);
            manualPath.Children.Add(pathActions); pickerPanel.Children.Add(manualPath);
            var flyout = new Flyout { Content = pickerPanel };
            flyout.FlyoutPresenterStyle = Ui.PickerStyle();
            chooseApp.Flyout = flyout;
            var empty = Ui.SecondaryText(""); empty.Visibility = Visibility.Collapsed;
            pickerPanel.Children.Insert(2, empty);
            void Filter()
            {
                var matching = apps.Where(x => x.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
                results.ItemsSource = matching;
                results.Visibility = matching.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
                empty.Text = L.T("没有匹配的应用，可浏览选择。");
                empty.Visibility = matching.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            search.TextChanged += (_, _) => Filter();
            flyout.Opened += async (_, _) =>
            {
                empty.Text = L.T("正在加载应用…"); empty.Visibility = Visibility.Visible;
                await LoadApps(); Filter(); search.Focus(FocusState.Programmatic);
            };
            results.ItemClick += (_, e) => { if (e.ClickedItem is AppEntry entry) { Choose(entry.Path, entry.Name); flyout.Hide(); } };
            browse.Click += async (_, _) =>
            {
                try
                {
                    var file = await Pick(".exe", ".lnk", ".appref-ms");
                    if (file != null) { Choose(file.Path, Path.GetFileNameWithoutExtension(file.Name)); flyout.Hide(); }
                }
                catch (Exception ex) { error.Text = ex.Message; error.Visibility = Visibility.Visible; }
            };
        }
        private void Choose(string path, string title)
        {
            application.Text = path;
            if (string.IsNullOrWhiteSpace(name.Text) || name.Text == L.T("新建项目")) name.Text = title;
        }
        private void BuildIconPicker(Button button)
        {
            var panel = new StackPanel { Spacing = 12, Width = 320 };
            panel.Children.Add(Label(L.T("更改图标"))); panel.Children.Add(icon);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(icon, L.T("更改图标"));
            var browse = Ui.ActionButton(L.T("选择图片…")); var reset = Ui.ActionButton(L.T("自动识别图标"));
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            actions.Children.Add(browse); actions.Children.Add(reset); panel.Children.Add(actions);
            var flyout = new Flyout { Content = panel, FlyoutPresenterStyle = Ui.PickerStyle() }; button.Flyout = flyout;
            reset.Click += (_, _) => { icon.Text = ""; flyout.Hide(); };
            browse.Click += async (_, _) =>
            {
                try { var file = await Pick(".png", ".jpg", ".jpeg", ".ico", ".bmp"); if (file != null) { icon.Text = file.Path; flyout.Hide(); } }
                catch (Exception ex) { error.Text = ex.Message; error.Visibility = Visibility.Visible; }
            };
        }
        private async Task<Windows.Storage.StorageFile?> Pick(params string[] extensions)
        {
            var picker = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(picker, handle);
            foreach (var extension in extensions) picker.FileTypeFilter.Add(extension);
            return await picker.PickSingleFileAsync();
        }
        private async Task LoadApps() { apps = await Task.Run(ScanApps); }
    }

    private sealed class AppEntry(string name, string path) : System.ComponentModel.INotifyPropertyChanged
    {
        public string Name { get; } = name;
        public string Path { get; } = path;
        private bool loading;
        private Microsoft.UI.Xaml.Media.ImageSource? icon;
        public Microsoft.UI.Xaml.Media.ImageSource? Icon
        {
            get { if (!loading) { loading = true; _ = LoadIcon(); } return icon; }
        }
        private async Task LoadIcon()
        {
            icon = await ApplicationIcons.Load(Path);
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Icon)));
        }
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
        public override string ToString() => Name;
    }
    private static List<AppEntry> ScanApps()
    {
        var result = new List<AppEntry>();
        foreach (var folder in new[] { Environment.SpecialFolder.StartMenu, Environment.SpecialFolder.CommonStartMenu })
        {
            var directory = Environment.GetFolderPath(folder);
            if (!Directory.Exists(directory)) continue;
            try
            {
                result.AddRange(Directory.EnumerateFiles(directory, "*.lnk", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
                    .Select(path => new AppEntry(Path.GetFileNameWithoutExtension(path), path)));
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return result.DistinctBy(x => x.Name).OrderBy(x => x.Name).ToList();
    }
}
