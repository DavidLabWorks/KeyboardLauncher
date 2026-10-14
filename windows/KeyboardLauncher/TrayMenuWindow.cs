using KeyboardLauncher.Core;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Windows.Graphics;

namespace KeyboardLauncher;

// A flyout needs a live XamlRoot even while the launcher is hidden.
internal sealed class TrayMenuWindow : Window
{
    private readonly Grid anchor = new();
    private readonly Func<ElementTheme> theme;
    private readonly Action openLauncher, openSettings, quit;
    private MenuFlyout? menu;
    private bool pending;

    internal TrayMenuWindow(Func<ElementTheme> theme, Action openLauncher, Action openSettings, Action quit)
    {
        this.theme = theme; this.openLauncher = openLauncher; this.openSettings = openSettings; this.quit = quit;
        Content = anchor;
        AppWindow.IsShownInSwitchers = false;
        var presenter = (OverlappedPresenter)AppWindow.Presenter;
        presenter.SetBorderAndTitleBar(false, false);
        presenter.IsResizable = presenter.IsMaximizable = presenter.IsMinimizable = false;
        presenter.IsAlwaysOnTop = true;
        presenter.PreferredMinimumWidth = presenter.PreferredMinimumHeight = 1;
        anchor.Loaded += (_, _) => { if (pending) OpenFlyout(); };
    }

    internal bool IsOpen => menu?.IsOpen == true;
    internal void Dismiss() => menu?.Hide();

    internal void Show()
    {
        if (menu?.IsOpen == true) { menu.Hide(); return; }
        if (!Native.GetCursorPos(out var point)) return;
        anchor.RequestedTheme = theme();
        anchor.Language = L.Language;
        pending = true;
        AppWindow.MoveAndResize(new RectInt32(point.X, point.Y, 1, 1));
        Activate();
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Native.ActivatePanel(handle);
        // Only the native popup is visible; make its tiny owner fully transparent.
        var extendedStyle = Native.GetWindowLongPtr(handle, -20).ToInt64();
        Native.SetWindowLongPtr(handle, -20, (nint)(extendedStyle | 0x00080080L)); // LAYERED | TOOLWINDOW
        Native.SetLayeredWindowAttributes(handle, 0, 0, 2); // LWA_ALPHA
        if (anchor.IsLoaded) DispatcherQueue.TryEnqueue(OpenFlyout);
    }

    private void OpenFlyout()
    {
        if (!pending) return;
        pending = false;
        menu = new MenuFlyout { ShouldConstrainToRootBounds = false };
        var style = new Style(typeof(MenuFlyoutPresenter));
        style.Setters.Add(new Setter(Control.CornerRadiusProperty, new CornerRadius(8)));
        style.Setters.Add(new Setter(FrameworkElement.MinWidthProperty, 190d));
        style.Setters.Add(new Setter(FrameworkElement.RequestedThemeProperty, theme()));
        menu.MenuFlyoutPresenterStyle = style;
        AddItem("打开启动器", "\uE765", openLauncher);
        AddItem("设置…", "\uE713", openSettings);
        menu.Items.Add(new MenuFlyoutSeparator());
        AddItem("退出", "\uE7E8", quit);
        menu.Closed += (_, _) => AppWindow.Hide();
        menu.ShowAt(anchor, new FlyoutShowOptions { Placement = FlyoutPlacementMode.TopEdgeAlignedRight });
    }

    private void AddItem(string text, string glyph, Action action)
    {
        var item = new MenuFlyoutItem { Text = L.T(text), Icon = new FontIcon { Glyph = glyph }, FontSize = 13 };
        item.Click += (_, _) => { menu?.Hide(); AppWindow.Hide(); action(); };
        menu!.Items.Add(item);
    }
}
