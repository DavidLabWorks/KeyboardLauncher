using KeyboardLauncher.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace KeyboardLauncher;

internal sealed partial class PanelWindow : Window
{
    private readonly App app;
    private readonly StackPanel rows = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid root = new();
    // Own initial keyboard focus without highlighting an action button.
    private readonly Button focusHost = new()
    {
        IsTabStop = true, UseSystemFocusVisuals = false, Width = 1, Height = 1, Opacity = 0,
        IsHitTestVisible = true, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top,
        HorizontalContentAlignment = HorizontalAlignment.Stretch,
        VerticalContentAlignment = VerticalAlignment.Stretch
    };
    internal bool HasNeutralFocus => ReferenceEquals(FocusManager.GetFocusedElement(root.XamlRoot), focusHost);
    private readonly Button previous, next;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer focusCheck;
    private readonly GlassBackdrop backdrop = new();
    private readonly Border surface = new() { CornerRadius = new CornerRadius(26) };
    // Draw inside the clipped window without changing content size or hit testing.
    private readonly Border outline = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                CornerRadius="26" BorderThickness="1" IsHitTestVisible="False"
                BorderBrush="{ThemeResource SurfaceStrokeColorDefaultBrush}" />
        """);
    private nint previousWindow;
    private bool visible, editing, launching, dragging;
    private UIElement? dragSurface;
    private Native.Point dragStart;
    private PointInt32 dragWindowStart;
    private int page;
    private int renderedPage = -1;
    private string renderedLanguage = "";
    private readonly List<Action<bool>> refreshKeys = [];
    private Action refreshLanguage = () => { };
    public nint Handle { get; }

    public PanelWindow(App app)
    {
        this.app = app; Title = App.DisplayName;
        searchHistory = new SearchHistory(Path.Combine(Path.GetDirectoryName(app.Store.FilePath)!, "search-history.json"));
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        SystemBackdrop = backdrop;
        var corner = 1; Native.DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
        var borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Native.DwmSetWindowAttribute(Handle, 34, ref borderColor, sizeof(int));
        focusCheck = DispatcherQueue.CreateTimer();
        focusCheck.Interval = TimeSpan.FromMilliseconds(100);
        focusCheck.Tick += (_, _) => CheckForeground();
        Closed += (_, _) => focusCheck.Stop();
        AppWindow.Changed += (_, e) => { if (e.DidSizeChange && visible) UpdateWindowShape(); };
        AppWindow.IsShownInSwitchers = true;
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        var nonClientPolicy = 1; // DWMNCRP_DISABLED: no separate system frame/shadow.
        Native.DwmSetWindowAttribute(Handle, 2, ref nonClientPolicy, sizeof(int));
        root.RowDefinitions.Add(new() { Height = new GridLength(76) });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = new GridLength(46) });
        var title = new DragPanel { Spacing = 12, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Background = new SolidColorBrush(Colors.Transparent) };
        title.Margin = new Thickness(30, 0, 30, 0);
        title.Children.Add(Ui.AppIcon(34));
        title.Children.Add(brandName);
        dragSurface = title;
        title.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(title).Properties.IsLeftButtonPressed ||
                !Native.GetCursorPos(out dragStart)) return;
            dragWindowStart = AppWindow.Position;
            dragging = title.CapturePointer(e.Pointer);
            e.Handled = dragging;
        };
        title.PointerMoved += (_, e) =>
        {
            if (!dragging) return;
            if (!e.GetCurrentPoint(title).Properties.IsLeftButtonPressed) { StopDragging(); return; }
            // Screen pixels avoid feedback from moving the window or Viewbox/DPI scaling.
            if (Native.GetCursorPos(out var cursor))
                AppWindow.Move(new PointInt32(dragWindowStart.X + cursor.X - dragStart.X,
                    dragWindowStart.Y + cursor.Y - dragStart.Y));
            e.Handled = true;
        };
        title.PointerReleased += (_, _) => StopDragging();
        title.PointerCaptureLost += (_, _) => dragging = false;
        root.Children.Add(title);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 30, 0) };
        var settings = HeaderButton("\uE713");
        settings.Click += (_, _) => app.OpenSettings();
        actions.Children.Add(searchButton); actions.Children.Add(settings);
        Grid.SetRow(rows, 1); root.Children.Add(rows);
        footer.Margin = new Thickness(30, 0, 30, 0);
        footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var footerHint = new TextBlock { Text = L.T("⌨  点击空键绑定 · 右键编辑 · 按键或点击启动"), Opacity = .75, FontSize = 12, VerticalAlignment = VerticalAlignment.Center }; footer.Children.Add(footerHint);
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        previous = new PointerButton { Content = "←" }; next = new PointerButton { Content = "→" };
        Ui.GlassButton(previous, false); Ui.GlassButton(next, false);
        previous.Click += (_, _) => ChangePage(-1); next.Click += (_, _) => ChangePage(1);
        ToolTipService.SetToolTip(previous, L.T("上一页（←）")); ToolTipService.SetToolTip(next, L.T("下一页（→）"));
        paging.Children.Add(previous); paging.Children.Add(pageLabel); paging.Children.Add(next);
        var closeHint = new TextBlock { Text = L.T("Esc  关闭"), FontSize = 12, Opacity = .75, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; paging.Children.Add(closeHint);
        Grid.SetColumn(paging, 1); footer.Children.Add(paging); Grid.SetRow(footer, 2); root.Children.Add(footer);
        root.Width = 1040; root.Height = 560;
        root.Children.Insert(0, focusHost);
        focusHost.Loaded += (_, _) => { if (visible) focusHost.Focus(FocusState.Programmatic); };
        var layers = new Grid();
        layers.Children.Add(root);
        layers.Children.Add(outline);
        surface.Child = layers;
        Content = new Viewbox { Child = surface, Stretch = Stretch.Uniform };
        // Do not rebuild the visual tree from ActualThemeChanged: WinUI is traversing
        // that tree during theme propagation. Each existing control updates its brushes.
        root.ActualThemeChanged += (_, _) => PaintSurface();
        root.PreviewKeyDown += HandleKey;
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) DispatcherQueue.TryEnqueue(CheckForeground); };
        refreshLanguage = () =>
        {
            root.Language = app.Config.Language;
            UpdateSearchLabels();
            brandName.Text = App.DisplayName;
            footerHint.Text = L.T("⌨  点击空键绑定 · 右键编辑 · 按键或点击启动");
            closeHint.Text = L.T("Esc  关闭");
            ToolTipService.SetToolTip(settings, L.T("设置"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(settings, L.T("设置"));
            ToolTipService.SetToolTip(previous, L.T("上一页（←）"));
            ToolTipService.SetToolTip(next, L.T("下一页（→）"));
        };
        InitializeSearch();
        // Search controls stay interactive above the full-panel dismissal surface.
        root.Children.Add(searchField);
        root.Children.Add(actions);
        InitializeBindingDrag();
        Refresh();
    }

    private void PaintSurface() => backdrop.ApplyTheme(root.ActualTheme);

    internal void Refresh()
    {
        Title = App.DisplayName;
        refreshLanguage();
        var theme = Ui.Theme(app.Config.Theme);
        if (((FrameworkElement)Content).RequestedTheme != theme)
            ((FrameworkElement)Content).RequestedTheme = theme;
        if (renderedPage < 0) PaintSurface();
        page = Math.Min(page, app.Config.PageCount);
        bool languageChanged = renderedLanguage != app.Config.Language;
        if (renderedPage != page)
        {
            CancelBindingDrag();
            keyCaps.Clear(); dropMarks.Clear();
            rows.Children.Clear(); refreshKeys.Clear();
            var index = page * KeyboardLayout.Count;
            for (var rowIndex = 0; rowIndex < KeyboardLayout.Rows.Length; rowIndex++)
            {
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(rowIndex == 2 ? 12 : rowIndex == 3 ? 22 : 0, 0, 0, 0) };
                foreach (var key in KeyboardLayout.Rows[rowIndex]) row.Children.Add(CreateKeyCell(index++, key));
                rows.Children.Add(row);
            }
            renderedPage = page;
        }
        foreach (var refresh in refreshKeys) refresh(languageChanged);
        renderedLanguage = app.Config.Language;
        pageLabel.Text = page < app.Config.PageCount ? $"{page + 1} / {app.Config.PageCount}" : L.F("{0} · 新页", page + 1);
        previous.IsEnabled = page > 0; next.IsEnabled = page < Math.Min(99, app.Config.PageCount);
        previous.Visibility = next.Visibility = pageLabel.Visibility = app.Config.PageCount > 1 || page > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private StackPanel CreateKeyCell(int slot, char key)
    {
        Launcher? current = null;
        bool initialized = false;
        var cap = new Grid { Background = new SolidColorBrush(Colors.Transparent) };
        var button = new PointerButton { Content = cap, Width = 70, Height = 70, Padding = new Thickness(0), CornerRadius = new CornerRadius(16),
            HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Ui.GlassButton(button, false);
        button.Shadow = new ThemeShadow(); button.Translation = new System.Numerics.Vector3(0, 0, 8);
        button.Click += async (_, _) => { if (current == null) await Edit(slot); else await Launch(current); };
        var menu = new MenuFlyout();
        var edit = new MenuFlyoutItem { Icon = new FontIcon { Glyph = "\uE70F" } };
        var removeItem = new MenuFlyoutItem { Icon = new FontIcon { Glyph = "\uE74D" } };
        edit.Click += async (_, _) => await Edit(slot);
        async void Remove(object sender, RoutedEventArgs e)
        {
            if (app.BindingSavePending) return;
            try { RemoveBinding(slot, app.Save); }
            catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); }
        }
        removeItem.Click += Remove;
        menu.Items.Add(edit); menu.Items.Add(removeItem); button.ContextFlyout = menu;
        var hitArea = new Grid(); hitArea.Children.Add(button);
        var dropMark = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
            <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    BorderBrush="{ThemeResource AccentFillColorDefaultBrush}" BorderThickness="2"
                    CornerRadius="16" IsHitTestVisible="False" Visibility="Collapsed" />
            """);
        dropMark.Translation = new System.Numerics.Vector3(0, 0, 10);
        hitArea.Children.Add(dropMark);
        keyCaps[slot] = cap; dropMarks[slot] = dropMark;
        AttachBindingDrag(cap, slot, button);
        var remove = Ui.UnbindButton(); hitArea.Children.Add(remove);
        remove.Click += Remove;
        bool hovering = false;
        void RefreshRemove() => remove.Visibility = current != null && bindingSurface == null &&
            (hovering || button.FocusState == FocusState.Keyboard || remove.FocusState != FocusState.Unfocused)
                ? Visibility.Visible : Visibility.Collapsed;
        hitArea.PointerEntered += (_, _) => { hovering = true; RefreshRemove(); };
        hitArea.PointerExited += (_, _) => { hovering = false; RefreshRemove(); };
        button.GotFocus += (_, _) => { RefreshRemove(); };
        button.LostFocus += (_, _) => DispatcherQueue.TryEnqueue(RefreshRemove);
        remove.GotFocus += (_, _) => RefreshRemove();
        remove.LostFocus += (_, _) => DispatcherQueue.TryEnqueue(RefreshRemove);
        var caption = new TextBlock { FontSize = 12, Height = 16, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Opacity = .75 };
        var cell = new StackPanel { Spacing = 5, Width = 70 };
        cell.Children.Add(hitArea); cell.Children.Add(caption);
        refreshKeys.Add(languageChanged =>
        {
            var item = app.Config.At(slot);
            if (!initialized || current != item)
            {
                current = item is null ? null : item with { };
                RefreshRemove(); cap.Children.Clear();
                if (current != null)
                {
                    cap.Children.Add(movedIcons.Remove(slot, out var icon) ? icon : Ui.KeyIcon(current, 70));
                    cap.Children.Add(new TextBlock { Text = key.ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
                }
                else
                {
                    if (remove.FocusState != FocusState.Unfocused) button.Focus(FocusState.Programmatic);
                    remove.Visibility = Visibility.Collapsed;
                    cap.Children.Add(new TextBlock { Text = key.ToString(), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                }
                caption.Text = current?.Name ?? "";
            }
            else if (!languageChanged) return;
            initialized = true;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{key} · {current?.Name ?? L.T("未绑定")}");
            edit.Text = current == null ? L.T("绑定动作…") : L.T("编辑绑定…");
            removeItem.Text = L.T("移除绑定");
            removeItem.Visibility = current != null ? Visibility.Visible : Visibility.Collapsed;
            ToolTipService.SetToolTip(button, current?.Name ?? L.T("绑定动作…"));
            ToolTipService.SetToolTip(caption, current?.Name);
            ToolTipService.SetToolTip(remove, L.T("移除绑定"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, L.F("解除 {0} 的绑定", key));
        });
        return cell;
    }

    internal void RemoveBinding(int slot, Action<LauncherConfig> persist)
    {
        if (app.Config.At(slot) == null) return;
        persist(app.Config.Bind(slot, null));
        Refresh();
    }

    internal object[] KeyVisualsForSmokeTest() => rows.Children.Cast<StackPanel>()
        .SelectMany(row => row.Children.Cast<StackPanel>()).SelectMany(cell =>
        {
            var hit = (Grid)cell.Children[0]; var button = (Button)hit.Children[0];
            return new object[] { cell, button, cell.Children[1], ((Grid)button.Content).Children[0] };
        }).ToArray();
    public void Toggle() { if (editing || launching) return; if (visible) Hide(true); else Show(); }
    internal void ShowFromTap(bool search)
    {
        if (editing || launching) return;
        Show();
        if (visible && app.Licensing.CanUse && search)
        {
            if (!Searching) OpenSearch();
            else searchBox.Focus(FocusState.Keyboard);
        }
    }

    public void Show()
    {
        if (!app.Licensing.CanUse) { app.OpenLicenseSettings(); return; }
        if (visible)
        {
            if (editing) return;
            if (Native.IsIconic(Handle)) Native.ShowWindow(Handle, 9); // SW_RESTORE
            if (Native.GetForegroundWindow() != Handle)
            {
                Activate();
                Native.ActivatePanel(Handle);
            }
            return;
        }
        var foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out var pid);
        if (pid != Environment.ProcessId) previousWindow = foreground;
        var info = new Native.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        Native.GetMonitorInfoW(Native.MonitorFromWindow(foreground, 2), ref info);
        var dpi = Math.Max(96u, Native.GetDpiForWindow(foreground)) / 96d;
        var scale = Math.Min(dpi, Math.Min((info.Work.Right - info.Work.Left - 32) / 1040d, (info.Work.Bottom - info.Work.Top - 32) / 560d));
        var width = (int)(1040 * scale); var height = (int)(560 * scale);
        AppWindow.MoveAndResize(new RectInt32(info.Work.Left + (info.Work.Right - info.Work.Left - width) / 2, info.Work.Top + (info.Work.Bottom - info.Work.Top - height) / 2, width, height));
        visible = true;
        AppWindow.Show(); Activate(); Native.ActivatePanel(Handle); focusHost.Focus(FocusState.Programmatic);
        UpdateWindowShape();
        // Activation can restore the old XAML focus after Show returns.
        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low, () =>
        {
            if (visible && !editing && !Searching && Native.GetForegroundWindow() == Handle)
                focusHost.Focus(FocusState.Programmatic);
        });
        focusCheck.Start();
    }

    private void UpdateWindowShape()
    {
        // WinUI's presenter restores frame styles on Show. Remove them only after
        // activation, before clipping, or its small frame leaks around our surface.
        var style = Native.GetWindowLongPtr(Handle, -16).ToInt64();
        var borderless = style & ~0x00C40000L; // WS_CAPTION | WS_THICKFRAME
        if (style != borderless)
        {
            Native.SetWindowLongPtr(Handle, -16, (nint)borderless);
            Native.SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x37); // FRAMECHANGED, preserve bounds/focus
        }
        var corner = 1; // DWMWCP_DONOTROUND: our region supplies the large curve.
        Native.DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
        var nonClientPolicy = 1;
        Native.DwmSetWindowAttribute(Handle, 2, ref nonClientPolicy, sizeof(int));
        var size = AppWindow.Size;
        var scale = Math.Min(size.Width / 1040d, size.Height / 560d);
        var diameter = Math.Max(2, (int)Math.Round(52 * scale));
        var region = Native.CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, diameter, diameter);
        if (region != 0 && Native.SetWindowRgn(Handle, region, true) == 0) Native.DeleteObject(region);
    }

    private void CheckForeground()
    {
        if (!visible || editing || Native.IsIconic(Handle)) return;
        var foreground = Native.GetForegroundWindow();
        // Ignore transient null focus and popup windows owned by this panel.
        if (foreground == 0 || foreground == Handle || Native.GetAncestor(foreground, 3) == Handle) return;
        Hide(false);
    }

    private void StopDragging()
    {
        dragging = false;
        dragSurface?.ReleasePointerCaptures();
    }

    public void Hide(bool restore)
    {
        CancelBindingDrag();
        StopDragging();
        CloseSearch(animate: false);
        focusCheck.Stop();
        visible = false; AppWindow.Hide();
        if (restore && Native.IsWindow(previousWindow)) Native.SetForegroundWindow(previousWindow);
    }

    private void ChangePage(int direction) { CancelBindingDrag(); page = Math.Clamp(page + direction, 0, Math.Min(99, app.Config.PageCount)); Refresh(); }
    internal async void HandleNativeKey(uint key, uint scanCode)
    {
        if (!visible || editing || launching || Searching) return;
        if (bindingSurface != null) { if (key == 0x1B) CancelBindingDrag(); return; }
        if (key == 0x1B) { Hide(true); return; }
        if (key is 0x25 or 0x27) { ChangePage(key == 0x25 ? -1 : 1); return; }
        var slot = KeyboardLayout.Slot((int)scanCode, page);
        if (slot >= 0 && app.Config.At(slot) is Launcher launcher) await Launch(launcher);
    }
    private async void HandleKey(object sender, KeyRoutedEventArgs e)
    {
        if (editing || launching || !visible) return;
        if (Searching)
        {
            if (e.Key == Windows.System.VirtualKey.Escape && !composingSearch)
            {
                e.Handled = true;
                CloseSearch();
                searchButton.Focus(FocusState.Keyboard);
            }
            return;
        }
        if (bindingSurface != null) { e.Handled = true; if (e.Key == Windows.System.VirtualKey.Escape) CancelBindingDrag(); return; }
        if (e.Key == Windows.System.VirtualKey.Escape) { e.Handled = true; Hide(true); return; }
        if (Native.Modifiers != 0) return;
        if (e.Key is Windows.System.VirtualKey.Left or Windows.System.VirtualKey.Right)
        { e.Handled = true; if (!e.KeyStatus.WasKeyDown) ChangePage(e.Key == Windows.System.VirtualKey.Left ? -1 : 1); return; }
        var slot = KeyboardLayout.Slot((int)e.KeyStatus.ScanCode, page);
        if (slot < 0) return;
        e.Handled = true;
        if (e.KeyStatus.WasKeyDown) return;
        if (app.Config.At(slot) is Launcher launcher) await Launch(launcher);
    }

    internal async void PreviewEditor() => await Edit(24);

    private async Task Edit(int slot)
    {
        if (editing || app.BindingSavePending) return;
        editing = true; app.SuspendHotkeys(true);
        try { await BindingEditor.Show(app, root.XamlRoot, Handle, slot); }
        finally { editing = false; app.SuspendHotkeys(false); CheckForeground(); }
    }

    internal Task ExecuteForSmokeTest(Launcher launcher, nint target)
    {
        // Production Show excludes our own windows; the smoke target is deliberately in-process.
        previousWindow = target;
        return Launch(launcher);
    }

    private async Task<bool> Launch(Launcher launcher)
    {
        if (!app.Licensing.CanUse) { app.OpenLicenseSettings(); return false; }
        if (launching) return false;
        launching = true;
        bool sendsShortcut = !string.IsNullOrWhiteSpace(launcher.KeyboardShortcut);
        // Start shell activation while we still own the foreground; restoring the
        // previous app first can prevent keyboard-triggered launches coming forward.
        if (sendsShortcut) Hide(true);
        try
        {
            await ActionRunner.Run(launcher, previousWindow, () => app.HasPressedKeys, () => Hide(false));
            if (!sendsShortcut && visible) Hide(false);
            return true;
        }
        catch (Exception ex) { Show(); await Ui.Error(root.XamlRoot, ex.Message); return false; }
        finally { launching = false; }
    }
}
