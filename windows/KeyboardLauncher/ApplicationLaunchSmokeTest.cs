using KeyboardLauncher.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardLauncher;

public partial class App
{
    private async void RunLaunchProbe()
    {
        var window = new Window { Title = "Keyboard Launcher activation check", Content = new TextBlock { Text = "Application activation check" } };
        window.Activate();
        await Task.Delay(400);
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "launch-probe.txt"),
            Native.GetForegroundWindow() == handle ? "PASS" : "FAIL: child application did not reach foreground");
        await Task.Delay(700);
        window.Close(); Exit();
    }

    private async void RunApplicationLaunchSmokeTest()
    {
        var original = Config;
        var report = Path.Combine(AppContext.BaseDirectory, "application-launch-smoke-test.txt");
        var probeReport = Path.Combine(AppContext.BaseDirectory, "launch-probe.txt");
        try
        {
            File.Delete(probeReport);
            bool explorer = Environment.GetCommandLineArgs().Contains("--explorer-launch-smoke-test");
            bool cursor = Environment.GetCommandLineArgs().Contains("--cursor-launch-smoke-test");
            bool IsTarget(nint window) { if (!cursor) return ActionRunner.IsExplorerWindow(window); Native.GetWindowThreadProcessId(window, out var pid); using var process = System.Diagnostics.Process.GetProcessById((int)pid); return process.ProcessName.Equals("Cursor", StringComparison.OrdinalIgnoreCase); }
            Config = Config.Bind(1, new Launcher { Name = "Activation check", ActionType = "application",
                Exec = cursor ? original.Launchers.First(x => x.Name == "Cursor").Exec : explorer ? "explorer.exe" : Environment.ProcessPath!, Arguments = explorer || cursor ? "" : "--launch-probe" });
            panel!.Refresh(); panel.Show();
            await Task.Delay(250);
            // Exercise the real global hook with the physical 2 key's scan code.
            var inputs = new uint[] { 0, 2 }.Select(flags => new Native.Input { Type = 1,
                Data = new() { Keyboard = new() { Key = 0x32, ScanCode = 3, Flags = flags, ExtraInfo = 0x54455354 } } }).ToArray();
            Native.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
            if (explorer || cursor)
            {
                var limit = Environment.TickCount64 + 5000;
                while (!IsTarget(Native.GetForegroundWindow()) && Environment.TickCount64 < limit) await Task.Delay(100);
                if (!IsTarget(Native.GetForegroundWindow())) throw new Exception("Pressing 2 did not foreground Explorer");
                var target = Native.GetForegroundWindow();
                panel.Show(); await Task.Delay(250);
                Native.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
                await Task.Delay(700);
                if (Native.GetForegroundWindow() != target) throw new Exception("Repeated launch did not activate existing window");
                Native.ShowWindow(target, 6); // Verify a minimized existing window too.
                panel.Show(); await Task.Delay(250);
                Native.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Native.Input>());
                await Task.Delay(1200);
                if (Native.GetForegroundWindow() != target || Native.IsIconic(target)) throw new Exception($"Existing window not restored: expected={target} foreground={Native.GetForegroundWindow()} minimized={Native.IsIconic(target)}");
                File.WriteAllText(report, "PASS: keyboard launch, repeated launch of existing window, and minimized-window restoration for " + (cursor ? "Cursor" : "Explorer") + ". User bindings unchanged.");
                return;
            }
            var deadline = Environment.TickCount64 + 8000;
            while (!File.Exists(probeReport) && Environment.TickCount64 < deadline) await Task.Delay(100);
            if (!File.Exists(probeReport)) throw new Exception("Keyboard launch did not create the probe window");
            var outcome = File.ReadAllText(probeReport);
            if (outcome != "PASS") throw new Exception(outcome);
            if (Native.IsWindowVisible(panel.Handle)) throw new Exception("Launcher remained visible");
            File.WriteAllText(report, "PASS: pressing 2 through the keyboard hook launches a separate app in the foreground and hides Launcher; user bindings unchanged.");
            await Task.Delay(800);
        }
        catch (Exception ex) { File.WriteAllText(report, "FAIL: " + ex); }
        finally { Config = original; Quit(); }
    }
}