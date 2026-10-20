namespace KeyboardLauncher.Core;

public static class PressedKeyState
{
    // Low-level callbacks run before the current event updates async key state.
    // On a down event, an async-up key cannot be a genuine repeat. A key-up
    // event, however, must reach the tap detector before its state is discarded.
    public static bool ReconcileBeforeEvent(HashSet<uint> pressed, ISet<uint> suppressed,
        uint eventKey, bool eventIsDown, Func<uint, bool> isDown)
    {
        return pressed.RemoveWhere(key => !suppressed.Contains(key) &&
            (key != eventKey || eventIsDown) && !isDown(key)) != 0;
    }
}
