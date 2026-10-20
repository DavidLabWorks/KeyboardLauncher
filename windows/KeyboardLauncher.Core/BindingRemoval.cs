namespace KeyboardLauncher.Core;

// Snapshot only the removed binding; unrelated edits must survive an undo.
public sealed class BindingRemoval(int slot, Launcher launcher)
{
    private readonly Launcher snapshot = launcher with { };
    public string Name => snapshot.Name;
    public bool CanRestore(LauncherConfig config) => config.At(slot) is null;
    public LauncherConfig? Restore(LauncherConfig config) => CanRestore(config) ? config.Bind(slot, snapshot) : null;
}
