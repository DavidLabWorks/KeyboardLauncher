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
        private readonly Grid root = new();
        private readonly TextBox name = new() { FontSize = 19, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Padding = new Thickness(12, 7, 12, 7) };
        private readonly TextBox application = new(), url = new(), command = new(), arguments = new(), shortcut = new();
        private readonly TextBox icon = new() { PlaceholderText = L.T("输入符号、emoji 或图片路径"), Width = 280 };
        private readonly Grid preview = new() { Width = 60, Height = 60 };
        private readonly Grid appPreview = new() { Width = 36, Height = 36 };
        private readonly TextBlock appName = new() { Text = L.T("选择应用…"), FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        private readonly StackPanel fields = new() { Spacing = 18 };
        private readonly TextBlock hint = new() { FontSize = 11, Opacity = .65, TextWrapping = TextWrapping.Wrap };
        private readonly TextBlock error = new() { Foreground = new SolidColorBrush(Colors.IndianRed), FontSize = 12, TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
        private readonly Button save = new PointerButton() { Content = L.T("保存"), MinWidth = 96, Height = 36, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Padding = new Thickness(18, 0, 18, 0), CornerRadius = new CornerRadius(7) };
        private readonly List<Button> segments = [];
        private readonly List<Action> paints = [];
        private Button chooseApp = null!;
        private int selected;
        private List<AppEntry> apps = [];

        internal EditorWindow(App app, nint owner, int slot)
        {
            this.app = app; this.slot = slot; root.Language = app.Config.Language;
            var existing = app.Config.At(slot);
            selected = existing?.KeyboardShortcut != null ? 3 : existing?.ActionType switch { "url" => 1, "command" => 2, _ => 0 };
            name.Text = existing?.Name ?? L.T("新建项目");
            application.Text = selected == 0 ? existing?.Exec ?? "" : "";
            url.Text = selected == 1 ? existing?.Exec ?? "" : "";
            command.Text = selected == 2 ? existing?.Exec ?? "" : "";
            arguments.Text = existing?.Arguments ?? "";
            shortcut.Text = existing?.KeyboardShortcut ?? "";
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
            paints.Add(() => iconButton.Background = Brush(Dark ? 49 : 247));
            var iconHost = new Grid { Width = 76, Height = 76 };
            iconHost.Children.Add(iconButton);
            var pencil = new Border { Width = 23, Height = 23, CornerRadius = new CornerRadius(12), Background = Brush(48, 112, 244),
                BorderThickness = new Thickness(2), BorderBrush = Brush(255), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(0, 0, -3, -3), IsHitTestVisible = false,
                Child = new FontIcon { Glyph = "\uE70F", FontSize = 12, Foreground = Brush(255) } };
            iconHost.Children.Add(pencil); identity.Children.Add(iconHost);
            BuildIconPicker(iconButton);
            var identityText = new StackPanel { Spacing = 8 };
            identityText.Children.Add(Label(L.T("名称")));
            name.Resources["TextControlCornerRadius"] = new CornerRadius(8);
            paints.Add(() => { name.Background = Brush(Dark ? 56 : 245); name.BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(51, (byte)(Dark ? 255 : 0), (byte)(Dark ? 255 : 0), (byte)(Dark ? 255 : 0))); });
            identityText.Children.Add(name);
            identityText.Children.Add(new TextBlock { Text = L.T("自定义键盘面板上显示的名称和图标。"), FontSize = 11, Opacity = .65, TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(identityText, 1); identity.Children.Add(identityText);
            form.Children.Add(Card(identity, 18));

            var actionSection = new StackPanel { Spacing = 10 };
            var actionLabel = Label(L.T("动作")); actionLabel.Margin = new Thickness(6, 0, 0, 0); actionSection.Children.Add(actionLabel);
            var action = new StackPanel { Spacing = 18 };
            var segmentRow = new Grid();
            for (int column = 0; column < 4; column++) segmentRow.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
            var segmentBackground = new Border { Child = segmentRow, Padding = new Thickness(3), CornerRadius = new CornerRadius(9), HorizontalAlignment = HorizontalAlignment.Stretch };
            paints.Add(() => segmentBackground.Background = Brush(Dark ? 56 : 235));
            var titles = new[] { L.T("打开应用"), L.T("打开网址"), L.T("Shell 命令"), L.T("执行快捷键") };
            for (int i = 0; i < titles.Length; i++)
            {
                int index = i;
                var button = new PointerButton { Content = titles[i], FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Height = 36, MinWidth = 0, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, Padding = new Thickness(8, 0, 8, 0), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(7) };
                button.Click += (_, _) => { selected = index; UpdateFields(); };
                segments.Add(button); Grid.SetColumn(button, i); segmentRow.Children.Add(button);
            }
            action.Children.Add(segmentBackground);
            var separator = new Border { Height = 1 };
            paints.Add(() => separator.Background = Brush(Dark ? 60 : 229));
            action.Children.Add(separator); action.Children.Add(fields);
            actionSection.Children.Add(Card(action, 14)); form.Children.Add(actionSection);
            var info = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new Thickness(6, 0, 6, 0) };
            info.Children.Add(new FontIcon { Glyph = "\uE946", FontSize = 12, Opacity = .65 }); info.Children.Add(hint);
            form.Children.Add(info); form.Children.Add(error);
            root.Children.Add(new ScrollViewer { Content = form, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });

            var footer = new Grid { Padding = new Thickness(24, 20, 24, 20), BorderThickness = new Thickness(0, 1, 0, 0) };
            paints.Add(() => footer.BorderBrush = Brush(Dark ? 60 : 222));
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var cancel = new PointerButton { Content = L.T("取消"), MinWidth = 96, Height = 36, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium, Padding = new Thickness(18, 0, 18, 0), CornerRadius = new CornerRadius(7) };
            cancel.Click += (_, _) => Close();
            save.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            save.Click += (_, _) => Save();
            buttons.Children.Add(cancel); buttons.Children.Add(save); footer.Children.Add(buttons); Grid.SetRow(footer, 1); root.Children.Add(footer);
            ShortcutRecorder.Attach(app, shortcut, this, false);
            BuildAppPicker();
            foreach (var field in new[] { name, application, url, command, arguments, shortcut, icon })
                field.TextChanged += (_, _) => { UpdatePreview(); Validate(); };
            root.RequestedTheme = Ui.Theme(app.Config.Theme);
            Content = root; root.ActualThemeChanged += (_, _) => Paint(); root.Loaded += (_, _) => Paint();
            root.PreviewKeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; Close(); } };
            Closed += (_, _) => completion.TrySetResult();
            UpdateFields(); Paint();
            _ = LoadApps();
        }

        private bool Dark => root.ActualTheme == ElementTheme.Dark;
        private static SolidColorBrush Brush(int gray) => Brush((byte)gray, (byte)gray, (byte)gray);
        private static SolidColorBrush Brush(byte r, byte g, byte b) => new(Windows.UI.Color.FromArgb(255, r, g, b));
        private static TextBlock Label(string text) => new() { Text = text, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Opacity = .6 };
        private Border Card(UIElement content, double radius)
        {
            var card = new Border { Child = content, Padding = new Thickness(20), CornerRadius = new CornerRadius(radius) };
            paints.Add(() => card.Background = Brush(Dark ? 40 : 255)); return card;
        }
        private void Paint()
        {
            root.Background = Brush(Dark ? 29 : 245);
            foreach (var paint in paints) paint();
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
                segments[i].Background = i == selected ? Brush(48, 112, 244) : new SolidColorBrush(Colors.Transparent);
                segments[i].Foreground = i == selected || Dark ? Brush(255) : Brush(35);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(segments[i], $"{segments[i].Content}{(i == selected ? L.T("，已选择") : "")}");
            }
        }
        private static StackPanel Field(string title, TextBox box, string placeholder)
        {
            var panel = new StackPanel { Spacing = 8 };
            panel.Children.Add(Label(title)); box.PlaceholderText = placeholder;
            box.FontSize = 13; box.Resources["TextControlCornerRadius"] = new CornerRadius(6);
            panel.Children.Add(box); return panel;
        }
        private void UpdateFields()
        {
            app.EndShortcutRecording(handle);
            // Detach retained text boxes before rebuilding action-specific wrappers.
            foreach (var container in fields.Children.OfType<StackPanel>()) container.Children.Clear();
            fields.Children.Clear();
            switch (selected)
            {
                case 0: fields.Children.Add(chooseApp); fields.Children.Add(Field(L.T("参数   可选"), arguments, L.T("例如 C:\\Projects\\my-app"))); break;
                case 1: fields.Children.Add(Field(L.T("网址"), url, "https://example.com")); break;
                case 2: fields.Children.Add(Field(L.T("Shell 命令"), command, L.T("输入 Windows 命令或脚本路径"))); break;
                case 3:
                    fields.Children.Add(Field(L.T("快捷键"), shortcut, L.T("点击后按下快捷键")));
                    fields.Children.Add(new TextBlock { Text = L.T("返回之前的应用，然后执行快捷键。支持全局快捷键。"), FontSize = 11, Opacity = .65, TextWrapping = TextWrapping.Wrap });
                    break;
            }
            PaintSegments(); UpdatePreview(); Validate();
        }
        private Launcher Draft() => new() { Name = name.Text.Trim(), Exec = selected switch { 0 => application.Text.Trim(), 1 => url.Text.Trim(), 2 => command.Text.Trim(), _ => "" },
            ActionType = selected switch { 1 => "url", 2 => "command", _ => "application" }, Arguments = selected == 0 ? arguments.Text : "",
            KeyboardShortcut = selected == 3 ? shortcut.Text.Trim() : null, Icon = string.IsNullOrWhiteSpace(icon.Text) ? null : icon.Text.Trim() };
        private void Validate()
        {
            try
            {
                if (selected == 3) Hotkey.Parse(shortcut.Text, false);
                app.Config.Bind(slot, Draft()).Validate(); save.IsEnabled = true;
            }
            catch { save.IsEnabled = false; }
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
                preview.Children.Add(new Border { Width = 48, Height = 48, CornerRadius = new CornerRadius(14), Background = Brush(38) });
            else preview.Children.Add(Ui.KeyIcon(draft, 76));
            appName.Text = string.IsNullOrWhiteSpace(application.Text) ? L.T("选择应用…") : Path.GetFileNameWithoutExtension(application.Text);
            appPreview.Children.Clear();
            appPreview.Children.Add(string.IsNullOrWhiteSpace(application.Text)
                ? new FontIcon { Glyph = "\uE739", FontSize = 27, Opacity = .6 }
                : Ui.KeyIcon(new Launcher { Exec = application.Text }, 48));
            hint.Text = string.IsNullOrWhiteSpace(icon.Text) ? L.T("根据命令自动识别图标") : L.T("使用自定义图标");
        }
        private void BuildAppPicker()
        {
            var label = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new TextBlock { Text = L.T("应用"), FontSize = 11, Opacity = .6 }); label.Children.Add(appName);
            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new() { Width = new GridLength(36) }); row.ColumnDefinitions.Add(new()); row.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
            row.Children.Add(appPreview); Grid.SetColumn(label, 1); row.Children.Add(label);
            var arrow = new FontIcon { Glyph = "\uE70D", FontSize = 11, Opacity = .6 }; Grid.SetColumn(arrow, 2); row.Children.Add(arrow);
            chooseApp = new PointerButton { Content = row, HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(10), BorderThickness = new Thickness(0), CornerRadius = new CornerRadius(10) };
            paints.Add(() => chooseApp.Background = Brush(Dark ? 47 : 249));
            var search = new TextBox { PlaceholderText = L.T("搜索应用"), Width = 420 };
            var results = new PointerGridView { Height = 300, Width = 420, IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.None, Padding = new Thickness(0), BorderThickness = new Thickness(0) };
            results.ItemsPanel = (ItemsPanelTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                    <ItemsWrapGrid Orientation="Horizontal" MaximumRowsOrColumns="5"/>
                </ItemsPanelTemplate>
                """);
            results.ItemContainerStyle = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="GridViewItem">
                    <Setter Property="Width" Value="80"/><Setter Property="Height" Value="84"/>
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
                        <TextBlock Text="{Binding Name}" FontSize="11" TextAlignment="Center" TextTrimming="CharacterEllipsis" Width="70"/>
                    </StackPanel>
                </DataTemplate>
                """);
            var browse = new PointerButton { Content = L.T("浏览应用或文件…") };
            var pickerPanel = new StackPanel { Spacing = 12 };
            pickerPanel.Children.Add(search); pickerPanel.Children.Add(results); pickerPanel.Children.Add(Field(L.T("应用、文件或文件夹路径"), application, L.T("例如 notepad.exe"))); pickerPanel.Children.Add(browse);
            var flyout = new Flyout { Content = pickerPanel };
            flyout.FlyoutPresenterStyle = (Style)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
                <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="FlyoutPresenter">
                    <Setter Property="BorderThickness" Value="0"/>
                    <Setter Property="CornerRadius" Value="12"/>
                    <Setter Property="Padding" Value="16"/>
                    <Setter Property="MaxWidth" Value="480"/>
                </Style>
                """);
            chooseApp.Flyout = flyout;
            void Filter() => results.ItemsSource = apps.Where(x => x.Name.Contains(search.Text, StringComparison.CurrentCultureIgnoreCase)).ToList();
            search.TextChanged += (_, _) => Filter();
            flyout.Opened += async (_, _) => { await LoadApps(); Filter(); };
            results.ItemClick += (_, e) => { if (e.ClickedItem is AppEntry entry) { Choose(entry.Path, entry.Name); flyout.Hide(); } };
            browse.Click += async (_, _) =>
            {
                try
                {
                    var file = await Pick("*");
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
            var panel = new StackPanel { Spacing = 12 };
            panel.Children.Add(Label(L.T("更改图标"))); panel.Children.Add(icon);
            var browse = new PointerButton { Content = L.T("选择图片…") }; var reset = new PointerButton { Content = L.T("自动识别图标") };
            panel.Children.Add(browse); panel.Children.Add(reset);
            var flyout = new Flyout { Content = panel }; button.Flyout = flyout;
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