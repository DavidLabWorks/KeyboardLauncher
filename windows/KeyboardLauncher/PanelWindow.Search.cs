using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using VirtualKey = Windows.System.VirtualKey;

namespace KeyboardLauncher;

internal sealed partial class PanelWindow
{
    private readonly Grid footer = new();
    private readonly Button searchButton = HeaderButton("\uE721");
    private readonly TextBlock brandName = new() { FontSize = 15, VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
    private readonly Grid searchField = (Grid)XamlReader.Load("""
        <Grid xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Width="768" Height="48"
              HorizontalAlignment="Center" VerticalAlignment="Center" Visibility="Collapsed" CornerRadius="12">
          <Border IsHitTestVisible="False" RenderTransformOrigin="0.5,0.5" CornerRadius="12"
                  Background="{ThemeResource LayerFillColorDefaultBrush}"
                  BorderBrush="{ThemeResource CardStrokeColorDefaultBrush}" BorderThickness="1"/>
          <Grid Margin="1">
            <Grid.ColumnDefinitions><ColumnDefinition Width="54"/><ColumnDefinition/></Grid.ColumnDefinitions>
            <FontIcon Glyph="&#xE721;" FontSize="19" Foreground="{ThemeResource TextFillColorSecondaryBrush}" IsHitTestVisible="False"/>
            <TextBox Grid.Column="1" FontSize="19" Padding="4,4,12,4" VerticalAlignment="Center" VerticalContentAlignment="Center"
                     Background="Transparent" BorderThickness="0" CornerRadius="0" />
            <Border Grid.Column="1" Width="1.5" Height="20" Margin="2,0,0,0" HorizontalAlignment="Left" VerticalAlignment="Center"
                    Background="{ThemeResource TextFillColorPrimaryBrush}" IsHitTestVisible="False" Visibility="Collapsed"/>
          </Grid>
        </Grid>
        """);
    private Grid searchContent => (Grid)searchField.Children[1];
    private TextBox searchBox => (TextBox)searchContent.Children[1];
    private Border emptyCaret => (Border)searchContent.Children[2];
    private readonly Border searchDropdown = (Border)XamlReader.Load("""
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                VerticalAlignment="Top" HorizontalAlignment="Center" Margin="0,68,0,0"
                CornerRadius="16" BorderThickness="1" BorderBrush="{ThemeResource SurfaceStrokeColorFlyoutBrush}"
                Background="{ThemeResource SolidBackgroundFillColorBaseBrush}" Visibility="Collapsed" />
        """);
    private readonly Border dismissSearch = new() { Visibility = Visibility.Collapsed };
    private readonly SearchList searchResults = new() { IsItemClickEnabled = true };
    private readonly SearchList recommendations = new() { IsItemClickEnabled = true };
    private sealed record Recommendation(AppEntry App, string Kind)
    {
        public override string ToString() => $"{App.Name}, {Kind}";
    }
    private readonly TextBlock resultsLabel = Ui.SecondaryText("");
    private readonly TextBlock searchHint = Ui.SecondaryText("");
    private ListView[] searchLists = [];
    private readonly SearchHistory searchHistory;
    private readonly ItemsPanelTemplate catalogPanel = (ItemsPanelTemplate)XamlReader.Load("""
        <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
            <ItemsWrapGrid Orientation="Horizontal" MaximumRowsOrColumns="2" ItemWidth="360" ItemHeight="48"/>
        </ItemsPanelTemplate>
        """);
    private readonly ItemsPanelTemplate matchesPanel = (ItemsPanelTemplate)XamlReader.Load("""
        <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"><ItemsStackPanel/></ItemsPanelTemplate>
        """);
    private readonly TextBlock searchStatus = Ui.SecondaryText("");
    private List<AppEntry>? searchApps;
    private bool searchLoadFailed;
    private bool scanning;
    private long lastScan;
    private List<AppEntry>? renderedCatalog;
    private IReadOnlyList<string>? renderedHistory;
    private string? renderedQuery, renderedSearchLanguage;
    private bool composingSearch;
    private bool Searching { get; set; }
    internal bool HasSearchFocus => Searching && searchBox.FocusState != FocusState.Unfocused;
    private Storyboard? searchMotion;
    private readonly TranslateTransform dropdownSlide = new();
    private const double ClosedScale = .78;
    private readonly ScaleTransform fieldScale = new() { ScaleX = ClosedScale };
    private readonly TranslateTransform contentSlide = new();
    private readonly Windows.UI.ViewManagement.UISettings motionSettings = new();

    private static Button HeaderButton(string glyph)
    {
        var button = new PointerButton
        {
            Content = new FontIcon { Glyph = glyph, FontSize = 19 }, Width = 42, Height = 38,
            Padding = new Thickness(0), CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center
        };
        Ui.GlassButton(button, false);
        return button;
    }

    private void InitializeSearch()
    {
        searchDropdown.RenderTransform = dropdownSlide;
        searchField.Children[0].RenderTransform = fieldScale;
        searchContent.RenderTransform = contentSlide;
        contentSlide.X = searchField.Width * (1 - ClosedScale) / 2;
        searchField.Opacity = searchDropdown.Opacity = 0;
        dropdownSlide.Y = -8;
        var clear = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
        foreach (var state in new[] { "", "PointerOver", "Focused" })
        {
            searchBox.Resources["TextControlBackground" + state] = clear;
            searchBox.Resources["TextControlBorderBrush" + state] = clear;
        }
        var rowTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid Height="42" ColumnSpacing="12">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="32"/><ColumnDefinition/></Grid.ColumnDefinitions>
                    <Image Source="{Binding Icon}" Width="30" Height="30"/>
                    <TextBlock Grid.Column="1" Text="{Binding Name}" FontSize="15" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
                </Grid>
            </DataTemplate>
            """);
        searchLists = [recommendations, searchResults];
        foreach (var list in searchLists)
        {
            list.ItemTemplate = rowTemplate;
            list.ItemContainerTransitions = new Microsoft.UI.Xaml.Media.Animation.TransitionCollection();
            list.ItemClick += async (_, e) => { if (ResultApp(e.ClickedItem) is AppEntry entry) await LaunchResult(entry); };
        }
        recommendations.Margin = new Thickness(16, 12, 16, 0);
        recommendations.ItemsPanel = (ItemsPanelTemplate)XamlReader.Load("""
            <ItemsPanelTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <ItemsStackPanel Orientation="Horizontal"/>
            </ItemsPanelTemplate>
            """);
        ScrollViewer.SetHorizontalScrollMode(recommendations, ScrollMode.Disabled);
        ScrollViewer.SetHorizontalScrollBarVisibility(recommendations, ScrollBarVisibility.Disabled);
        recommendations.ItemContainerStyle = (Style)XamlReader.Load("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" TargetType="ListViewItem">
                <Setter Property="Width" Value="182"/><Setter Property="Padding" Value="8,6"/>
                <Setter Property="HorizontalContentAlignment" Value="Stretch"/>
            </Style>
            """);
        recommendations.ItemTemplate = (DataTemplate)XamlReader.Load("""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation">
                <Grid ColumnSpacing="10" Height="54">
                    <Grid.ColumnDefinitions><ColumnDefinition Width="32"/><ColumnDefinition/></Grid.ColumnDefinitions>
                    <Image Source="{Binding App.Icon}" Width="32" Height="32"/>
                    <StackPanel Grid.Column="1" VerticalAlignment="Center" Spacing="2">
                        <TextBlock Text="{Binding App.Name}" ToolTipService.ToolTip="{Binding App.Name}" FontSize="14" FontWeight="SemiBold" TextTrimming="CharacterEllipsis"/>
                        <TextBlock Text="{Binding Kind}" FontSize="12" Foreground="{ThemeResource TextFillColorSecondaryBrush}"/>
                    </StackPanel>
                </Grid>
            </DataTemplate>
            """);
        resultsLabel.Margin = new Thickness(28, 14, 28, 6);
        resultsLabel.FontSize = 12; resultsLabel.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        searchHint.FontSize = 12;
        searchHint.HorizontalAlignment = HorizontalAlignment.Right;
        var commands = (Border)XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" Padding="16,7"
                    BorderThickness="0,1,0,0" BorderBrush="{ThemeResource DividerStrokeColorDefaultBrush}"/>
            """);
        commands.Child = searchHint;
        var searchBody = new Grid { MinHeight = 58 };
        for (int i = 0; i < 4; i++) searchBody.RowDefinitions.Add(new() { Height = GridLength.Auto });
        searchResults.Margin = new Thickness(16, 0, 16, 10);
        searchStatus.Margin = new Thickness(12, 24, 12, 24);
        Grid.SetRow(resultsLabel, 1); Grid.SetRow(searchResults, 2); Grid.SetRow(searchStatus, 2); Grid.SetRow(commands, 3);
        searchBody.Children.Add(recommendations); searchBody.Children.Add(resultsLabel); searchBody.Children.Add(commands);
        searchBody.Children.Add(searchResults);
        searchBody.Children.Add(searchStatus);
        searchDropdown.Width = searchField.Width;
        searchDropdown.Child = searchBody;
        var shadow = new Microsoft.UI.Xaml.Media.ThemeShadow();
        shadow.Receivers.Add(dismissSearch);
        searchDropdown.Shadow = shadow;
        searchDropdown.Translation = new System.Numerics.Vector3(0, 0, 36);
        Grid.SetRowSpan(searchDropdown, 3);
        dismissSearch.Background = clear;
        Grid.SetRowSpan(dismissSearch, 3);
        root.Children.Add(dismissSearch);
        root.Children.Add(searchDropdown);
        rows.GotFocus += (_, _) => CloseSearch();
        footer.GotFocus += (_, _) => CloseSearch();
        dismissSearch.PointerPressed += (_, e) =>
        {
            e.Handled = true;
            CloseSearch();
            focusHost.Focus(FocusState.Programmatic);
        };
        searchStatus.HorizontalAlignment = HorizontalAlignment.Center;
        searchStatus.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetLiveSetting(searchStatus, Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite);
        searchButton.Click += (_, _) =>
        {
            if (Searching) CloseSearch();
            else OpenSearch();
        };
        // Keep the insertion point visible over the placeholder. Once editing
        // starts, the TextBox owns caret positioning, selection and IME rendering.
        var blink = DispatcherQueue.CreateTimer();
        blink.Tick += (_, _) => emptyCaret.Opacity = emptyCaret.Opacity == 0 ? 1 : 0;
        void UpdateEmptyCaret()
        {
            blink.Stop();
            bool show = Searching && searchBox.FocusState != FocusState.Unfocused && searchBox.Text.Length == 0 && !composingSearch;
            emptyCaret.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            emptyCaret.Opacity = 1;
            if (!show) return;
            var interval = Native.GetCaretBlinkTime();
            if (interval is 0 or uint.MaxValue) return;
            blink.Interval = TimeSpan.FromMilliseconds(interval);
            blink.Start();
        }
        searchBox.GotFocus += (_, _) => UpdateEmptyCaret();
        searchBox.LostFocus += (_, _) => UpdateEmptyCaret();
        searchBox.TextChanged += (_, _) => { UpdateEmptyCaret(); FilterSearch(); };
        searchBox.TextCompositionStarted += (_, _) => { composingSearch = true; UpdateEmptyCaret(); };
        searchBox.TextCompositionEnded += (_, _) => { composingSearch = false; UpdateEmptyCaret(); };
        searchBox.KeyDown += async (_, e) =>
        {
            if (composingSearch) return;
            var selected = searchLists.FirstOrDefault(list => list.SelectedIndex >= 0);
            if (e.Key is VirtualKey.Up or VirtualKey.Down)
            {
                e.Handled = true;
                int index = selected == null ? -1 : searchLists.TakeWhile(list => list != selected).Sum(list => list.Items.Count) + selected.SelectedIndex;
                SelectSearchItem(index + (e.Key == VirtualKey.Up ? -1 : 1));
            }
            else if (e.Key == VirtualKey.Enter && ResultApp(selected?.SelectedItem) is AppEntry entry)
            {
                e.Handled = true;
                await LaunchResult(entry);
            }
        };
        _ = RefreshCatalog();
    }

    private static AppEntry? ResultApp(object? item) => item is Recommendation suggestion ? suggestion.App : item as AppEntry;

    private void SelectSearchItem(int index)
    {
        int count = searchLists.Sum(list => list.Items.Count);
        if (count == 0) return;
        index = Math.Clamp(index, 0, count - 1);
        foreach (var list in searchLists)
        {
            list.SelectedIndex = index >= 0 && index < list.Items.Count ? index : -1;
            if (list == searchResults && list.SelectedItem != null) list.ScrollIntoView(list.SelectedItem);
            index -= list.Items.Count;
        }
    }

    private async Task LaunchResult(AppEntry entry)
    {
        if (await Launch(new Launcher { Name = entry.Name, Exec = entry.Path })) searchHistory.Record(entry.Path);
    }

    private void UpdateSearchLabels()
    {
        var label = L.T(Searching ? "收起搜索" : "搜索应用");
        ((FontIcon)searchButton.Content).Glyph = Searching ? "\uE711" : "\uE721";
        ToolTipService.SetToolTip(searchButton, label);
        AutomationProperties.SetName(searchButton, label);
        searchBox.PlaceholderText = L.T("搜索应用");
        AutomationProperties.SetName(recommendations, L.T("推荐应用"));
        resultsLabel.Text = L.T(searchBox.Text.Trim().Length == 0 ? "所有应用" : "搜索结果");
        searchHint.Text = L.T("↑ ↓  选择     Enter  打开     Esc  返回");
        AutomationProperties.SetName(searchBox, searchBox.PlaceholderText);
        AutomationProperties.SetName(searchResults, L.T("搜索结果"));
    }

    private void OpenSearch()
    {
        Searching = true;
        searchBox.Text = "";
        app.SuspendHotkeys(true);
        searchField.Visibility = searchDropdown.Visibility = dismissSearch.Visibility = Visibility.Visible;
        searchField.IsHitTestVisible = searchDropdown.IsHitTestVisible = true;
        UpdateSearchLabels();
        FilterSearch();
        SelectSearchItem(0);
        searchBox.Focus(FocusState.Keyboard);
        AnimateSearch(true);
        if (searchApps == null || Environment.TickCount64 - lastScan >= 120_000) _ = RefreshCatalog();
    }

    private async Task RefreshCatalog()
    {
        if (scanning) return;
        scanning = true;
        searchLoadFailed = false;
        try
        {
            var updated = await Task.Run(LocalApplications.Scan);
            lastScan = Environment.TickCount64;
            var previous = searchApps;
            if (previous != null && previous.Count == updated.Count && previous.Zip(updated).All(pair =>
                pair.First.Path == pair.Second.Path && pair.First.Name == pair.Second.Name && pair.First.Added == pair.Second.Added))
                return;
            searchApps = updated;
            // Keep the user's selection steady during a background refresh.
            if (previous == null || !Searching) FilterSearch();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(ex);
            searchLoadFailed = searchApps == null;
            if (searchLoadFailed) FilterSearch();
        }
        finally { scanning = false; }
    }

    private void FilterSearch()
    {
        var query = searchBox.Text.Trim();
        var history = searchHistory.Entries;
        if (searchApps != null && searchApps == renderedCatalog && query == renderedQuery && history == renderedHistory
            && L.Language == renderedSearchLanguage) return;
        renderedSearchLanguage = L.Language;
        renderedCatalog = searchApps;
        renderedHistory = history;
        renderedQuery = query;

        List<AppEntry> recent = [], added = [], matches = [];
        if (searchApps != null && query.Length == 0)
        {
            var byPath = searchApps.ToLookup(app => app.Path, StringComparer.OrdinalIgnoreCase);
            added = searchApps.Where(app => app.Added.HasValue).OrderByDescending(app => app.Added).Take(2).ToList();
            recent = history.SelectMany(path => byPath[path]).Except(added).Take(2).ToList();
            matches = searchApps.Except(recent).Except(added).ToList();
        }
        else if (searchApps != null)
        {
            matches = searchApps.Where(app => app.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                .OrderByDescending(app => app.Name.StartsWith(query, StringComparison.CurrentCultureIgnoreCase)).ToList();
        }

        var suggested = added.Select(app => new Recommendation(app, L.T("最近添加")))
            .Concat(recent.Select(app => new Recommendation(app, L.T("最近点击")))).ToList();
        recommendations.ItemsSource = suggested.Count > 0 ? suggested : null;
        bool hasRecommendations = suggested.Count > 0;
        recommendations.Visibility = hasRecommendations ? Visibility.Visible : Visibility.Collapsed;
        var panel = query.Length == 0 ? catalogPanel : matchesPanel;
        if (searchResults.ItemsPanel != panel) searchResults.ItemsPanel = panel;
        searchResults.MaxHeight = query.Length == 0 ? 240 : 288;
        searchResults.ItemsSource = matches;
        resultsLabel.Text = L.T(query.Length == 0 ? "所有应用" : "搜索结果");
        resultsLabel.Visibility = searchResults.Visibility = matches.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        searchStatus.Visibility = hasRecommendations || matches.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
        searchStatus.Text = searchLoadFailed ? L.T("无法加载应用，请重新打开搜索。")
            : searchApps == null ? L.T("正在加载应用…") : L.T("没有匹配的应用");
        SelectSearchItem(0);
    }

    private void CloseSearch(bool animate = true)
    {
        if (!Searching)
        {
            if (!animate && searchMotion != null) AnimateSearch(false);
            return;
        }
        FinishSearchDrag();
        Searching = false;
        searchField.IsHitTestVisible = searchDropdown.IsHitTestVisible = false;
        if (searchBox.FocusState != FocusState.Unfocused) focusHost.Focus(FocusState.Programmatic);
        composingSearch = false;
        app.SuspendHotkeys(false);
        UpdateSearchLabels();
        AnimateSearch(animate);
    }

    private void AnimateSearch(bool animate)
    {
        double fieldOpacity = searchField.Opacity, listOpacity = searchDropdown.Opacity, offset = dropdownSlide.Y;
        double currentScale = fieldScale.ScaleX, currentInset = contentSlide.X;
        double currentNameOpacity = brandName.Opacity;
        searchMotion?.Stop();
        searchMotion = null;
        double opacity = Searching ? 1 : 0, y = Searching ? 0 : -8;
        double nameOpacity = 1 - opacity;
        brandName.Visibility = Visibility.Visible;
        double scale = Searching ? 1 : ClosedScale;
        double inset = searchField.Width * (1 - scale) / 2;
        void Finish()
        {
            searchField.Opacity = searchDropdown.Opacity = opacity;
            fieldScale.ScaleX = scale;
            contentSlide.X = inset;
            dropdownSlide.Y = y;
            brandName.Opacity = nameOpacity;
            brandName.Visibility = Searching ? Visibility.Collapsed : Visibility.Visible;
            if (Searching) return;
            searchField.Visibility = searchDropdown.Visibility = dismissSearch.Visibility = Visibility.Collapsed;
            searchBox.Text = "";
        }
        if (!animate || !motionSettings.AnimationsEnabled) { Finish(); return; }
        var motion = new Storyboard();
        var duration = TimeSpan.FromMilliseconds(Searching ? 165 : 115);
        void Add(DependencyObject target, string property, double from, double to)
        {
            var track = new DoubleAnimation { From = from, To = to, Duration = duration,
                EasingFunction = new CubicEase { EasingMode = Searching ? EasingMode.EaseOut : EasingMode.EaseIn } };
            Storyboard.SetTarget(track, target);
            Storyboard.SetTargetProperty(track, property);
            motion.Children.Add(track);
        }
        Add(searchField, "Opacity", fieldOpacity, opacity);
        Add(brandName, "Opacity", currentNameOpacity, nameOpacity);
        Add(fieldScale, "ScaleX", currentScale, scale);
        Add(contentSlide, "X", currentInset, inset);
        Add(searchDropdown, "Opacity", listOpacity, opacity);
        Add(dropdownSlide, "Y", offset, y);
        motion.Completed += (_, _) =>
        {
            if (searchMotion != motion) return;
            searchMotion = null;
            motion.Stop();
            Finish();
        };
        searchMotion = motion;
        motion.Begin();
    }
}
