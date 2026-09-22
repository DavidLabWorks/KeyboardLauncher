using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyboardLauncher.Core;

namespace KeyboardLauncher;

internal static class ApplicationActivation
{
    private static string? Executable(string command, out bool shortcutHasArguments)
    {
        shortcutHasArguments = false;
        var path = Environment.ExpandEnvironmentVariables(command);
        if (Path.GetExtension(path).Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            object? shell = null, shortcut = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!);
                shortcut = ((dynamic)shell!).CreateShortcut(path);
                path = ((dynamic)shortcut).TargetPath;
                shortcutHasArguments = !string.IsNullOrWhiteSpace((string)((dynamic)shortcut).Arguments);
            }
            catch { return null; }
            finally
            {
                if (shortcut != null && Marshal.IsComObject(shortcut)) Marshal.ReleaseComObject(shortcut);
                if (shell != null && Marshal.IsComObject(shell)) Marshal.ReleaseComObject(shell);
            }
        }
        if (!Path.IsPathRooted(path))
            path = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(';')
                .Select(folder => Path.Combine(folder, path)).FirstOrDefault(File.Exists) ?? path;
        return Path.IsPathRooted(path) && Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase) ? Path.GetFullPath(path) : null;
    }

    internal static async Task Open(Launcher launcher, Action? releasePanel)
    {
        var executable = Executable(launcher.Exec, out var shortcutHasArguments);
        bool explorer = executable != null && string.Equals(executable,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), StringComparison.OrdinalIgnoreCase);
        HashSet<uint> Processes()
        {
            var ids = new HashSet<uint>();
            if (executable == null) return ids;
            foreach (var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable)))
                using (process)
                    try { if (string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) ids.Add((uint)process.Id); }
                    catch (System.ComponentModel.Win32Exception) { }
                    catch (InvalidOperationException) { }
            return ids;
        }
        nint Find(HashSet<uint> ids)
        {
            nint found = 0;
            Native.EnumWindows((window, _) =>
            {
                if (!Native.IsWindowVisible(window)) return true;
                Native.GetWindowThreadProcessId(window, out var pid);
                if (!ids.Contains(pid) || (explorer && !ActionRunner.IsExplorerWindow(window))) return true;
                // Exclude tool/owned windows such as Electron utility windows.
                if ((Native.GetWindowLongPtr(window, -20).ToInt64() & 0x80) != 0 || Native.GetAncestor(window, 3) != window) return true;
                found = window; return false;
            }, 0);
            return found;
        }
        var processes = Processes();
        foreach (var pid in processes) Native.AllowSetForegroundWindow(pid);
        var existing = Find(processes);
        // Plain app activation reuses its current window; arguments still execute normally.
        if (existing == 0 || shortcutHasArguments || !string.IsNullOrWhiteSpace(launcher.Arguments))
            Process.Start(new ProcessStartInfo(Environment.ExpandEnvironmentVariables(launcher.Exec))
                { Arguments = launcher.Arguments, UseShellExecute = true })?.Dispose();
        releasePanel?.Invoke();
        if (executable == null) return;
        // Hiding a foreground HWND can change activation asynchronously. Finish that
        // transition before activating the destination, then verify the actual HWND.
        await Task.Delay(80);
        var deadline = Environment.TickCount64 + 4000;
        while (Environment.TickCount64 < deadline)
        {
            var window = Find(Processes());
            if (window != 0)
            {
                if (Native.IsIconic(window)) Native.ShowWindow(window, 9);
                Native.ActivatePanel(window);
                await Task.Delay(80);
                if (Native.GetForegroundWindow() == window) return;
            }
            await Task.Delay(80);
        }
        // Some executable actions have no GUI window (for example console helpers).
        // Successful process creation remains valid for those actions.
    }
}