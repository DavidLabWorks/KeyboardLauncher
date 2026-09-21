using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardLauncher;

internal static class PointerCursors
{
    internal static readonly InputSystemCursor Hand = InputSystemCursor.Create(InputSystemCursorShape.Hand);
    internal static readonly InputSystemCursor Arrow = InputSystemCursor.Create(InputSystemCursorShape.Arrow);
    internal static readonly InputSystemCursor Move = InputSystemCursor.Create(InputSystemCursorShape.SizeAll);
}

internal sealed class PointerButton : Button
{
    public PointerButton() { Loaded += (_, _) => Update(); IsEnabledChanged += (_, _) => Update(); }
    private void Update() => ProtectedCursor = IsEnabled ? PointerCursors.Hand : PointerCursors.Arrow;
}
internal sealed class DragPanel : StackPanel
{
    public DragPanel() { Loaded += (_, _) => ProtectedCursor = PointerCursors.Move; }
}
internal sealed class PointerComboBox : ComboBox
{
    public PointerComboBox() { Loaded += (_, _) => Update(); IsEnabledChanged += (_, _) => Update(); }
    private void Update() => ProtectedCursor = IsEnabled ? PointerCursors.Hand : PointerCursors.Arrow;
}
internal sealed class PointerRadioButton : RadioButton
{
    public PointerRadioButton() { Loaded += (_, _) => Update(); IsEnabledChanged += (_, _) => Update(); }
    private void Update() => ProtectedCursor = IsEnabled ? PointerCursors.Hand : PointerCursors.Arrow;
}
internal sealed class PointerHost : ContentControl
{
    public PointerHost() { Loaded += (_, _) => ProtectedCursor = PointerCursors.Hand; }
}
internal sealed class PointerListViewItem : ListViewItem
{
    public PointerListViewItem() { Loaded += (_, _) => ProtectedCursor = PointerCursors.Hand; }
}
internal sealed class PointerGridViewItem : GridViewItem
{
    public PointerGridViewItem() { Loaded += (_, _) => ProtectedCursor = PointerCursors.Hand; }
}
internal sealed class PointerGridView : GridView
{
    protected override DependencyObject GetContainerForItemOverride() => new PointerGridViewItem();
}