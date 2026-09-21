using KeyboardLauncher.Core;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace KeyboardLauncher;

internal sealed class PanelWindow : Window
{
    private readonly App app;
    private readonly StackPanel rows = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock pageLabel = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly Grid root = new() { Padding = new Thickness(30, 0, 30, 0) };
    private readonly Button previous, next;
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer focusCheck;
    private readonly PanelShadow shadow;
    private readonly GlassBackdrop backdrop = new();
    private nint previousWindow;
    private bool visible, editing, launching, dragging;
    private UIElement? dragSurface;
    private Native.Point dragStart;
    private PointInt32 dragWindowStart;
    private int page;
    private Action refreshLanguage = () => { };
    public nint Handle { get; }

    public PanelWindow(App app)
    {
        this.app = app; Title = "Keyboard Launcher";
        Handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        shadow = new PanelShadow(Handle);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        SystemBackdrop = backdrop;
        // Use the mac radius explicitly; DWM's fixed rounded preset is only ~8px.
        var corner = 1; Native.DwmSetWindowAttribute(Handle, 33, ref corner, sizeof(int));
        var borderColor = unchecked((int)0xFFFFFFFE); // DWMWA_COLOR_NONE
        Native.DwmSetWindowAttribute(Handle, 34, ref borderColor, sizeof(int));
        focusCheck = DispatcherQueue.CreateTimer();
        focusCheck.Interval = TimeSpan.FromMilliseconds(100);
        focusCheck.Tick += (_, _) => CheckForeground();
        Closed += (_, _) => { focusCheck.Stop(); shadow.Dispose(); };
        AppWindow.Changed += (_, e) =>
        {
            if (e.DidSizeChange) UpdateWindowShape();
            if (visible && (e.DidPositionChange || e.DidSizeChange)) UpdateShadow();
        };
        AppWindow.IsShownInSwitchers = false;
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false); presenter.IsAlwaysOnTop = true;
        presenter.IsResizable = false; presenter.IsMaximizable = false; presenter.IsMinimizable = false;
        // Disable non-client frame rendering as well as the presenter border.
        Native.SetWindowLongPtr(Handle, -16, (nint)(Native.GetWindowLongPtr(Handle, -16).ToInt64() & ~0x00C40000L));
        Native.SetWindowLongPtr(Handle, -20, (nint)(Native.GetWindowLongPtr(Handle, -20).ToInt64() & ~0x00020300L));
        Native.SetWindowPos(Handle, 0, 0, 0, 0, 0, 0x37); // FRAMECHANGED, no move/size/z-order/activation
        var nonClientPolicy = 1; // DWMNCRP_DISABLED
        Native.DwmSetWindowAttribute(Handle, 2, ref nonClientPolicy, sizeof(int));
        root.RowDefinitions.Add(new() { Height = new GridLength(76) });
        root.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new() { Height = new GridLength(46) });
        var header = new Grid(); header.ColumnDefinitions.Add(new()); header.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var title = new DragPanel { Spacing = 12, Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Stretch, Background = new SolidColorBrush(Colors.Transparent) };
        title.Children.Add(Ui.AppIcon(34));
        title.Children.Add(new TextBlock { Text = "Keyboard Launcher", FontSize = 15, VerticalAlignment = VerticalAlignment.Center, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
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
        header.Children.Add(title);
        var settings = new PointerButton { Content = new FontIcon { Glyph = "\uE713", FontSize = 19 }, Width = 42, Height = 38, Padding = new Thickness(0), CornerRadius = new CornerRadius(10), VerticalAlignment = VerticalAlignment.Center };
        Ui.GlassButton(settings, false);
        ToolTipService.SetToolTip(settings, L.T("设置"));
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(settings, L.T("设置"));
        settings.Click += (_, _) => app.OpenSettings(); Grid.SetColumn(settings, 1); header.Children.Add(settings);
        root.Children.Add(header); Grid.SetRow(rows, 1); root.Children.Add(rows);
        var footer = new Grid(); footer.ColumnDefinitions.Add(new()); footer.ColumnDefinitions.Add(new() { Width = GridLength.Auto });
        var footerHint = new TextBlock { Text = L.T("⌨  点击空键绑定 · 右键编辑 · 按键或点击启动"), Opacity = .65, FontSize = 11, VerticalAlignment = VerticalAlignment.Center }; footer.Children.Add(footerHint);
        var paging = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        previous = new PointerButton { Content = "←" }; next = new PointerButton { Content = "→" };
        Ui.GlassButton(previous, false); Ui.GlassButton(next, false);
        previous.Click += (_, _) => ChangePage(-1); next.Click += (_, _) => ChangePage(1);
        ToolTipService.SetToolTip(previous, L.T("上一页（←）")); ToolTipService.SetToolTip(next, L.T("下一页（→）"));
        paging.Children.Add(previous); paging.Children.Add(pageLabel); paging.Children.Add(next);
        var closeHint = new TextBlock { Text = L.T("Esc  关闭"), FontSize = 11, Opacity = .65, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center }; paging.Children.Add(closeHint);
        Grid.SetColumn(paging, 1); footer.Children.Add(paging); Grid.SetRow(footer, 2); root.Children.Add(footer);
        root.Width = 1040; root.Height = 560;
        Content = new Viewbox { Child = new Border { Child = root, CornerRadius = new CornerRadius(26) }, Stretch = Stretch.Uniform };
        // Do not rebuild the visual tree from ActualThemeChanged: WinUI is traversing
        // that tree during theme propagation. Each existing control updates its brushes.
        root.ActualThemeChanged += (_, _) => backdrop.ApplyTheme(root.ActualTheme);
        root.PreviewKeyDown += HandleKey;
        Activated += (_, e) => { if (e.WindowActivationState == WindowActivationState.Deactivated) DispatcherQueue.TryEnqueue(CheckForeground); };
        refreshLanguage = () =>
        {
            root.Language = app.Config.Language;
            footerHint.Text = L.T("⌨  点击空键绑定 · 右键编辑 · 按键或点击启动");
            closeHint.Text = L.T("Esc  关闭");
            ToolTipService.SetToolTip(settings, L.T("设置"));
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(settings, L.T("设置"));
            ToolTipService.SetToolTip(previous, L.T("上一页（←）"));
            ToolTipService.SetToolTip(next, L.T("下一页（→）"));
        };
        Refresh();
    }

    internal void Refresh()
    {
        refreshLanguage();
        ((FrameworkElement)Content).RequestedTheme = Ui.Theme(app.Config.Theme);
        backdrop.ApplyTheme(root.ActualTheme);
        page = Math.Min(page, app.Config.PageCount); // One extra page is available for new bindings.
        rows.Children.Clear();
        var index = page * KeyboardLayout.Count;
        for (var rowIndex = 0; rowIndex < KeyboardLayout.Rows.Length; rowIndex++)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(rowIndex == 2 ? 12 : rowIndex == 3 ? 22 : 0, 0, 0, 0) };
            foreach (var key in KeyboardLayout.Rows[rowIndex])
            {
                var slot = index++; var item = app.Config.At(slot);
                var cap = new Grid();
                if (item != null)
                {
                    cap.Children.Add(Ui.KeyIcon(item, 70));
                    cap.Children.Add(new TextBlock { Text = key.ToString(), FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(5), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom });
                }
                else cap.Children.Add(new TextBlock { Text = key.ToString(), FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center });
                var button = new PointerButton { Content = cap, Width = 70, Height = 70, Padding = new Thickness(0), CornerRadius = new CornerRadius(16), HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
                Ui.GlassButton(button, item != null);
                button.Shadow = new ThemeShadow(); button.Translation = new System.Numerics.Vector3(0, 0, 8);
                Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(button, $"{key} · {item?.Name ?? L.T("未绑定")}");
                button.Click += async (_, _) => { if (item == null) await Edit(slot); else await Launch(item); };
                var menu = new MenuFlyout(); var edit = new MenuFlyoutItem { Text = item == null ? L.T("绑定动作…") : L.T("编辑绑定…") };
                edit.Click += async (_, _) => await Edit(slot); menu.Items.Add(edit); button.ContextFlyout = menu;
                var keyCell = new StackPanel { Spacing = 5, Width = 70 };
                var hitArea = new Grid(); hitArea.Children.Add(button);
                if (item != null)
                {
                    var remove = Ui.UnbindButton();
                    ToolTipService.SetToolTip(remove, L.T("移除绑定"));
                    Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(remove, L.F("解除 {0} 的绑定", key));
                    remove.Click += async (_, _) => { try { app.Save(app.Config.Bind(slot, null)); } catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); } };
                    hitArea.Children.Add(remove);
                    hitArea.PointerEntered += (_, _) => remove.Visibility = Visibility.Visible;
                    hitArea.PointerExited += (_, _) => remove.Visibility = Visibility.Collapsed;
                }
                keyCell.Children.Add(hitArea);
                keyCell.Children.Add(new TextBlock { Text = item?.Name ?? "", FontSize = 11, Height = 14, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center, Opacity = .75 });
                row.Children.Add(keyCell);
            }
            rows.Children.Add(row);
        }
        pageLabel.Text = page < app.Config.PageCount ? $"{page + 1} / {app.Config.PageCount}" : L.F("{0} · 新页", page + 1);
        previous.IsEnabled = page > 0; next.IsEnabled = page < Math.Min(99, app.Config.PageCount);
        previous.Visibility = next.Visibility = pageLabel.Visibility = app.Config.PageCount > 1 || page > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public void Toggle() { if (editing || launching) return; if (visible) Hide(true); else Show(); }
    public void Show()
    {
        if (visible) return;
        var foreground = Native.GetForegroundWindow();
        Native.GetWindowThreadProcessId(foreground, out var pid);
        if (pid != Environment.ProcessId) previousWindow = foreground;
        var info = new Native.MonitorInfo { Size = (uint)System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
        Native.GetMonitorInfoW(Native.MonitorFromWindow(foreground, 2), ref info);
        var dpi = Math.Max(96u, Native.GetDpiForWindow(foreground)) / 96d;
        var scale = Math.Min(dpi, Math.Min((info.Work.Right - info.Work.Left - 32) / 1040d, (info.Work.Bottom - info.Work.Top - 32) / 560d));
        var width = (int)(1040 * scale); var height = (int)(560 * scale);
        AppWindow.MoveAndResize(new RectInt32(info.Work.Left + (info.Work.Right - info.Work.Left - width) / 2, info.Work.Top + (info.Work.Bottom - info.Work.Top - height) / 2, width, height));
        UpdateWindowShape();
        visible = true;
        AppWindow.Show(); Activate(); Native.ActivatePanel(Handle); root.Focus(FocusState.Programmatic);
        UpdateShadow();
        focusCheck.Start();
    }

    private void UpdateWindowShape()
    {
        var size = AppWindow.Size;
        if (size.Width <= 0 || size.Height <= 0) return;
        var scale = Math.Min(size.Width / 1040d, size.Height / 560d);
        backdrop.Resize(size.Width, size.Height, (float)(26 * scale));
        // Leave a two-pixel allowance outside the alpha edge; HRGN is for hit testing.
        var diameter = Math.Max(2, (int)Math.Round(52 * scale) - 4);
        var region = Native.CreateRoundRectRgn(0, 0, size.Width + 1, size.Height + 1, diameter, diameter);
        if (region == 0) return;
        // Windows owns the region after a successful SetWindowRgn call.
        if (Native.SetWindowRgn(Handle, region, true) == 0) Native.DeleteObject(region);
    }

    private void UpdateShadow() => shadow.Show(AppWindow.Position.X, AppWindow.Position.Y, AppWindow.Size.Width, AppWindow.Size.Height);

    private void CheckForeground()
    {
        if (!visible || editing) return;
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
        StopDragging();
        focusCheck.Stop();
        shadow.Hide();
        visible = false; AppWindow.Hide();
        if (restore && Native.IsWindow(previousWindow)) Native.SetForegroundWindow(previousWindow);
    }

    private void ChangePage(int direction) { page = Math.Clamp(page + direction, 0, Math.Min(99, app.Config.PageCount)); Refresh(); }
    internal async void HandleNativeKey(uint key, uint scanCode)
    {
        if (!visible || editing || launching) return;
        if (key == 0x1B) { Hide(true); return; }
        if (key is 0x25 or 0x27) { ChangePage(key == 0x25 ? -1 : 1); return; }
        var slot = KeyboardLayout.Slot((int)scanCode, page);
        if (slot >= 0 && app.Config.At(slot) is Launcher launcher) await Launch(launcher);
    }
    private async void HandleKey(object sender, KeyRoutedEventArgs e)
    {
        if (editing || launching || !visible) return;
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
        if (editing) return;
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

    private async Task Launch(Launcher launcher)
    {
        if (launching) return; launching = true; Hide(true);
        try { await ActionRunner.Run(launcher, previousWindow, () => app.HasPressedKeys); }
        catch (Exception ex) { Show(); await Ui.Error(root.XamlRoot, ex.Message); }
        finally { launching = false; }
    }
}
