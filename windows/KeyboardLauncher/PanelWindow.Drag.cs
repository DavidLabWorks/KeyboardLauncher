using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.Foundation;
using Microsoft.UI.Input;

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
    // Own the entire mouse gesture before ListViewItem can turn release into ItemClick.
    // Keyboard, automation, and touch scrolling still use the stock list container.
    private sealed class SearchList : ListView
    {
        internal Action<ListViewItem, PointerRoutedEventArgs>? MousePressed;
        protected override DependencyObject GetContainerForItemOverride() => new SearchItem(this);
        private sealed class SearchItem(SearchList owner) : ListViewItem
        {
            protected override void OnPointerPressed(PointerRoutedEventArgs e)
            {
                if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                {
                    e.Handled = true;
                    owner.MousePressed?.Invoke(this, e);
                }
                else base.OnPointerPressed(e);
            }
        }
    }

    private sealed class SearchDragLayer : Canvas
    {
        private readonly InputCursor blocked = InputSystemCursor.Create(InputSystemCursorShape.UniversalNo);
        internal void SetAllowed(bool allowed) => ProtectedCursor = allowed ? PointerCursors.Hand : blocked;
    }

    private readonly SearchDragLayer searchDragLayer = new()
    {
        Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent),
        Visibility = Visibility.Collapsed, Translation = new System.Numerics.Vector3(0, 0, 48)
    };
    private readonly Border searchDragPreview = (Border)Microsoft.UI.Xaml.Markup.XamlReader.Load("""
        <Border xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                IsHitTestVisible="False" Visibility="Collapsed" CornerRadius="10" Padding="10,6"
                Background="{ThemeResource SolidBackgroundFillColorBaseBrush}" BorderThickness="1"
                BorderBrush="{ThemeResource SurfaceStrokeColorFlyoutBrush}">
            <StackPanel Orientation="Horizontal" Spacing="8">
                <Image Width="32" Height="32" Source="{Binding Icon}"/>
                <TextBlock Text="{Binding Name}" MaxWidth="164" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
            </StackPanel>
        </Border>
        """);
    private AppEntry? pressedSearchApp;
    private uint searchPointer;
    private Point searchPressPoint;
    private Rect searchPressBounds;
    private bool searchDragging;

    private void StartSearchPress(ListViewItem source, PointerRoutedEventArgs e)
    {
        if (!Searching || launching || app.BindingSavePending || pressedSearchApp != null || ResultApp(source.Content) is not AppEntry entry) return;
        searchDragLayer.Visibility = Visibility.Visible;
        if (!searchDragLayer.CapturePointer(e.Pointer)) { searchDragLayer.Visibility = Visibility.Collapsed; return; }
        searchDragLayer.SetAllowed(true);
        pressedSearchApp = entry;
        searchPointer = e.Pointer.PointerId;
        searchPressPoint = e.GetCurrentPoint(searchDragLayer).Position;
        searchPressBounds = source.TransformToVisual(searchDragLayer).TransformBounds(new Rect(0, 0, source.ActualWidth, source.ActualHeight));
    }

    private void UpdateSearchDrag(Point point)
    {
        if (!searchDragging)
        {
            if (Math.Abs(point.X - searchPressPoint.X) < 6 && Math.Abs(point.Y - searchPressPoint.Y) < 6) return;
            searchDragging = true;
            searchDropdown.Visibility = dismissSearch.Visibility = Visibility.Collapsed;
            searchDragPreview.DataContext = pressedSearchApp;
            searchDragPreview.Visibility = Visibility.Visible;
            searchDragPreview.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        }
        var preview = searchDragPreview.DesiredSize;
        Canvas.SetLeft(searchDragPreview, point.X + 14 + preview.Width <= searchDragLayer.ActualWidth
            ? point.X + 14 : Math.Max(0, point.X - preview.Width - 14));
        Canvas.SetTop(searchDragPreview, Math.Clamp(point.Y + 12, 0, Math.Max(0, searchDragLayer.ActualHeight - preview.Height)));
        int target = EmptySearchTarget(point);
        searchDragLayer.SetAllowed(target >= 0);
        foreach (var (slot, mark) in dropMarks) mark.Visibility = slot == target ? Visibility.Visible : Visibility.Collapsed;
    }

    private int EmptySearchTarget(Point point)
    {
        int slot = BindingTarget(point);
        return slot >= 0 && !app.BindingSavePending && app.Config.At(slot) == null ? slot : -1;
    }

    private void FinishSearchDrag()
    {
        if (pressedSearchApp == null) return;
        pressedSearchApp = null;
        searchDragging = false;
        searchDragPreview.Visibility = Visibility.Collapsed;
        searchDragPreview.DataContext = null;
        searchDragLayer.ReleasePointerCaptures();
        searchDragLayer.Visibility = Visibility.Collapsed;
        if (Searching) searchDropdown.Visibility = dismissSearch.Visibility = Visibility.Visible;
        foreach (var mark in dropMarks.Values) mark.Visibility = Visibility.Collapsed;
    }

    private async void ReleaseSearchPress(object sender, PointerRoutedEventArgs e)
    {
        if (pressedSearchApp is not AppEntry entry || e.Pointer.PointerId != searchPointer) return;
        e.Handled = true;
        var point = e.GetCurrentPoint(searchDragLayer).Position;
        UpdateSearchDrag(point);
        bool click = !searchDragging && searchPressBounds.Contains(point);
        int target = searchDragging ? EmptySearchTarget(point) : -1;
        FinishSearchDrag();
        if (click) { await LaunchResult(entry); return; }
        if (target < 0) return;
        try
        {
            app.Save(app.Config.Bind(target, new Launcher { Name = entry.Name, Exec = entry.Path }));
            CloseSearch(animate: false);
            focusHost.Focus(FocusState.Programmatic);
        }
        catch (Exception ex) { await Ui.Error(root.XamlRoot, ex.Message); }
    }

    private void InitializeBindingDrag()
    {
        Grid.SetRowSpan(bindingOverlay, 3);
        root.Children.Add(bindingOverlay);
        Grid.SetRowSpan(searchDragLayer, 3);
        searchDragLayer.Children.Add(searchDragPreview);
        root.Children.Add(searchDragLayer);
        recommendations.MousePressed = searchResults.MousePressed = StartSearchPress;
        searchDragLayer.PointerMoved += (_, e) =>
        {
            if (pressedSearchApp == null || e.Pointer.PointerId != searchPointer) return;
            var point = e.GetCurrentPoint(searchDragLayer);
            if (point.Properties.IsLeftButtonPressed) UpdateSearchDrag(point.Position);
            else FinishSearchDrag();
            e.Handled = true;
        };
        searchDragLayer.PointerReleased += ReleaseSearchPress;
        searchDragLayer.PointerCanceled += (_, e) => { if (e.Pointer.PointerId == searchPointer) FinishSearchDrag(); };
        searchDragLayer.PointerCaptureLost += (_, e) => { if (e.Pointer.PointerId == searchPointer) FinishSearchDrag(); };
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
