using ICompositionSupportsSystemBackdrop = Microsoft.UI.Composition.ICompositionSupportsSystemBackdrop;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace KeyboardLauncher;

// A true desktop backdrop: DWM blurs the windows behind the launcher.
internal sealed class GlassBackdrop : SystemBackdrop
{
    private DesktopAcrylicController? controller;
    private FrameworkElement? root;
    private ElementTheme theme = ElementTheme.Light;
    internal void ApplyTheme(ElementTheme value) { theme = value; UpdateColors(); }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        if (!DesktopAcrylicController.IsSupported()) return;
        controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(target, xamlRoot));
        controller.AddSystemBackdropTarget(target);
        root = xamlRoot.Content as FrameworkElement;
        if (root != null) root.ActualThemeChanged += ThemeChanged;
        UpdateColors();
    }

    private void ThemeChanged(FrameworkElement sender, object args) => UpdateColors();
    protected override void OnDefaultSystemBackdropConfigurationChanged(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        // The custom controller owns this target. The base callback is for the
        // built-in controller and rejects it when the hidden panel changes theme.
        if (controller == null) return;
        // The configuration attached at connection is updated by WinUI in place.
        // A hidden target cannot be queried again during theme propagation.
        UpdateColors();
    }
    private void UpdateColors()
    {
        if (controller == null) return;
        var dark = theme == ElementTheme.Dark;
        controller.TintColor = dark ? Color.FromArgb(255, 64, 64, 64) : Color.FromArgb(255, 248, 248, 248);
        controller.TintOpacity = dark ? .90f : .62f;
        controller.LuminosityOpacity = dark ? .85f : .75f;
        controller.FallbackColor = dark ? Color.FromArgb(255, 64, 64, 64) : Color.FromArgb(255, 248, 248, 248);
    }

    protected override void OnTargetDisconnected(ICompositionSupportsSystemBackdrop target)
    {
        if (root != null) root.ActualThemeChanged -= ThemeChanged;
        controller?.RemoveSystemBackdropTarget(target);
        controller?.Dispose(); controller = null;
        root = null;
        base.OnTargetDisconnected(target);
    }

}
