using Windows.UI.Composition;
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
    private RoundedTarget? roundedTarget;
    private System.Numerics.Vector2 size = new(1040, 560);
    private float radius = 26;
    internal void Resize(int width, int height, float cornerRadius)
    {
        size = new(width, height); radius = cornerRadius;
        roundedTarget?.Resize(size, radius);
    }
    private ElementTheme theme = ElementTheme.Light;
    internal void ApplyTheme(ElementTheme value) { theme = value; UpdateColors(); }

    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot xamlRoot)
    {
        base.OnTargetConnected(target, xamlRoot);
        if (!DesktopAcrylicController.IsSupported()) return;
        controller = new DesktopAcrylicController { Kind = DesktopAcrylicKind.Thin };
        controller.SetSystemBackdropConfiguration(GetDefaultSystemBackdropConfiguration(target, xamlRoot));
        roundedTarget = new RoundedTarget(target);
        roundedTarget.Resize(size, radius);
        controller.AddSystemBackdropTarget(roundedTarget);
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
        if (roundedTarget != null) controller?.RemoveSystemBackdropTarget(roundedTarget);
        controller?.Dispose(); controller = null;
        roundedTarget?.Dispose(); roundedTarget = null; root = null;
        base.OnTargetDisconnected(target);
    }

    // Keep the Acrylic controller, but mask its brush instead of clipping visible
    // pixels with an integer HRGN. Composition rasterizes the curved edge with alpha.
    private sealed class RoundedTarget : ICompositionSupportsSystemBackdrop, IDisposable
    {
        private readonly ICompositionSupportsSystemBackdrop target;
        private CompositionRoundedRectangleGeometry geometry = null!;
        private ShapeVisual visual = null!;
        private CompositionVisualSurface surface = null!;
        private CompositionSurfaceBrush surfaceBrush = null!;
        private CompositionMaskBrush mask = null!;
        private CompositionSpriteShape shape = null!;
        private CompositionColorBrush fill = null!;
        private CompositionBrush source = null!;

        internal RoundedTarget(ICompositionSupportsSystemBackdrop target) { this.target = target; }
        private System.Numerics.Vector2 bounds;
        private float cornerRadius;
        private void Initialize(Compositor compositor)
        {
            geometry = compositor.CreateRoundedRectangleGeometry();
            fill = compositor.CreateColorBrush(Microsoft.UI.Colors.White);
            shape = compositor.CreateSpriteShape(geometry);
            shape.FillBrush = fill;
            visual = compositor.CreateShapeVisual();
            visual.Shapes.Add(shape);
            surface = compositor.CreateVisualSurface();
            surface.SourceVisual = visual;
            surfaceBrush = compositor.CreateSurfaceBrush(surface);
            surfaceBrush.Stretch = CompositionStretch.Fill;
            mask = compositor.CreateMaskBrush();
            mask.Mask = surfaceBrush;
        }

        public CompositionBrush SystemBackdrop
        {
            get => source;
            set
            {
                source = value;
                if (value == null) { target.SystemBackdrop = null!; return; }
                if (mask == null) { Initialize(value.Compositor); Resize(bounds, cornerRadius); }
                mask!.Source = value;
                target.SystemBackdrop = value == null ? null! : mask;
            }
        }

        internal void Resize(System.Numerics.Vector2 size, float radius)
        {
            bounds = size; cornerRadius = radius;
            if (geometry == null) return;
            geometry.Size = size;
            geometry.CornerRadius = new(radius, radius);
            visual.Size = size;
            surface.SourceSize = size;
        }

        public void Dispose()
        {
            target.SystemBackdrop = null!;
            if (mask == null) return;
            mask.Dispose(); surfaceBrush.Dispose(); surface.Dispose();
            visual.Dispose(); shape.Dispose(); fill.Dispose(); geometry.Dispose();
        }
    }}
