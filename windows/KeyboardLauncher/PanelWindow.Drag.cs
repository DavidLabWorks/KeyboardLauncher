using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;

namespace KeyboardLauncher;

internal sealed partial class PanelWindow
{
    private readonly Canvas bindingOverlay = new() { IsHitTestVisible = false, Translation = new System.Numerics.Vector3(0, 0, 16) };
    private readonly Dictionary<int, Grid> keyCaps = [];
    private readonly Dictionary<int, Border> dropMarks = [];
    private readonly Dictionary<int, UIElement> movedIcons = [];
    private Grid? bindingSurface;
    private int bindingSource = -1;
    private Point bindingStart;
    private UIElement? movingIcon;
    private Button? movingKeycap;
    private Grid? movingContent;
    private bool bindingDragging;

    private void InitializeBindingDrag()
    {
        Grid.SetRowSpan(bindingOverlay, 3);
        root.Children.Add(bindingOverlay);
    }

    private void AttachBindingDrag(Grid cap, int slot, Button button)
    {
        // Handle at the button's content before Button sees the pointer: a drag
        // must never also invoke Click. Keyboard/automation clicks remain native.
        cap.PointerPressed += (_, e) =>
        {
            if (editing || launching || app.BindingSavePending || app.Config.At(slot) == null ||
                !e.GetCurrentPoint(cap).Properties.IsLeftButtonPressed) return;
            CancelBindingDrag();
            if (!cap.CapturePointer(e.Pointer)) return;
            bindingSurface = cap; bindingSource = slot;
            bindingStart = e.GetCurrentPoint(bindingOverlay).Position;
            e.Handled = true;
        };
        cap.PointerMoved += (_, e) =>
        {
            if (bindingSurface != cap) return;
            var point = e.GetCurrentPoint(bindingOverlay);
            if (!point.Properties.IsLeftButtonPressed) { CancelBindingDrag(); return; }
            if (!bindingDragging && Math.Pow(point.Position.X - bindingStart.X, 2) + Math.Pow(point.Position.Y - bindingStart.Y, 2) >= 36)
            {
                bindingDragging = true;
                movingIcon = cap.Children[0]; cap.Children.RemoveAt(0);
                var content = new Grid();
                content.Children.Add(movingIcon);
                if (cap.Children.OfType<TextBlock>().LastOrDefault() is TextBlock marker)
                    content.Children.Add(new TextBlock
                    {
                        Text = marker.Text, FontSize = marker.FontSize, FontWeight = marker.FontWeight,
                        FontFamily = marker.FontFamily, Margin = marker.Margin,
                        HorizontalAlignment = marker.HorizontalAlignment, VerticalAlignment = marker.VerticalAlignment
                    });
                movingContent = content;
                movingKeycap = new Button
                {
                    Width = button.ActualWidth, Height = button.ActualHeight,
                    Padding = button.Padding, CornerRadius = button.CornerRadius,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Stretch,
                    RequestedTheme = button.ActualTheme, Content = content,
                    IsHitTestVisible = false, IsTabStop = false,
                    Shadow = new Microsoft.UI.Xaml.Media.ThemeShadow()
                };
                Ui.GlassButton(movingKeycap, false);
                bindingOverlay.Children.Add(movingKeycap);
                cap.Opacity = .35;
            }
            if (bindingDragging)
            {
                var icon = movingKeycap!;
                Canvas.SetLeft(icon, point.Position.X - icon.Width / 2);
                Canvas.SetTop(icon, point.Position.Y - icon.Height / 2);
                var target = BindingTarget(point.Position);
                foreach (var (key, mark) in dropMarks)
                    mark.Visibility = key == target && key != bindingSource ? Visibility.Visible : Visibility.Collapsed;
            }
            e.Handled = true;
        };
        cap.PointerReleased += async (_, e) =>
        {
            if (bindingSurface != cap) return;
            var target = BindingTarget(e.GetCurrentPoint(bindingOverlay).Position);
            var source = bindingSource; var wasDragging = bindingDragging;
            e.Handled = true;
            CancelBindingDrag();
            try
            {
                if (wasDragging) { if (target >= 0) await app.MoveBinding(source, target); }
                else if (target == source && app.Config.At(source) is Launcher item) await Launch(item);
            }
            catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); }
        };
        cap.PointerCanceled += (_, _) => { if (bindingSurface == cap) CancelBindingDrag(); };
        cap.PointerCaptureLost += (_, _) => { if (bindingSurface == cap) CancelBindingDrag(); };
    }

    private int BindingTarget(Point point)
    {
        foreach (var (slot, cap) in keyCaps)
        {
            var bounds = cap.TransformToVisual(bindingOverlay).TransformBounds(new Rect(0, 0, cap.ActualWidth, cap.ActualHeight));
            if (bounds.Contains(point)) return slot;
        }
        return -1;
    }

    private void CancelBindingDrag()
    {
        var surface = bindingSurface;
        bindingSurface = null; bindingSource = -1; bindingDragging = false;
        if (movingIcon != null)
        {
            movingContent?.Children.Remove(movingIcon);
            surface?.Children.Insert(0, movingIcon);
            movingIcon = null;
        }
        if (movingKeycap != null) bindingOverlay.Children.Remove(movingKeycap);
        movingKeycap = null; movingContent = null;
        if (surface != null) { surface.Opacity = 1; surface.ReleasePointerCaptures(); }
        foreach (var mark in dropMarks.Values) mark.Visibility = Visibility.Collapsed;
    }

    internal void RefreshMovedBindings(LauncherConfig before, int source, int destination)
    {
        CancelBindingDrag();
        // Reparent existing artwork instead of extracting/decoding icons again.
        foreach (var (from, to) in new[] { (source, destination), (destination, source) })
            if (before.At(from) != null && keyCaps.TryGetValue(from, out var cap))
            {
                movedIcons[to] = cap.Children[0]; cap.Children.RemoveAt(0);
            }
        Refresh();
        movedIcons.Clear();
    }
}
