using System.Diagnostics;
using System.Runtime.InteropServices;
using KeyboardLauncher.Core;

namespace KeyboardLauncher;

internal static class ActionRunner
{
    public static async Task Run(Launcher launcher, nint previousWindow, Func<bool>? hasPressedKeys = null, Action? releasePanel = null)
    {
        if (!string.IsNullOrWhiteSpace(launcher.KeyboardShortcut))
        {
            var hotkey = Hotkey.Parse(launcher.KeyboardShortcut, false);
            if (previousWindow == 0 || !Native.IsWindow(previousWindow)) throw new InvalidOperationException(L.T("原应用窗口已关闭，请先切换到目标应用再打开启动器。"));
            // Wait for the physical launcher key, mouse button and modifiers to be released.
            var deadline = Environment.TickCount64 + 2000;
            while (hasPressedKeys?.Invoke() == true || Enumerable.Range(1, 254).Any(Native.Down))
            {
                if (Environment.TickCount64 >= deadline) throw new InvalidOperationException(L.T("按键仍未释放，已取消发送快捷键。"));
                await Task.Delay(20);
            }
            if (!Native.ActivatePanel(previousWindow)) throw new InvalidOperationException(L.T("无法激活原应用，已取消发送快捷键。"));
            await Task.Delay(100);
            if (Native.GetForegroundWindow() != previousWindow) throw new InvalidOperationException(L.T("焦点已改变，已取消发送快捷键。"));
            var keys = new List<ushort>();
            if ((hotkey.Modifiers & 2) != 0) keys.Add(0xA2);
            if ((hotkey.Modifiers & 1) != 0) keys.Add(0xA4);
            if ((hotkey.Modifiers & 4) != 0) keys.Add(0xA0);
            if ((hotkey.Modifiers & 8) != 0) keys.Add(0x5B);
            keys.Add((ushort)hotkey.Key);
            var inputs = keys.Select(k => MakeInput(k, false)).Concat(keys.AsEnumerable().Reverse().Select(k => MakeInput(k, true))).ToArray();
            var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.Input>());
            if (sent != inputs.Length)
            {
                // Release only keys whose down events were actually injected.
                var releases = keys.Take((int)Math.Min(sent, (uint)keys.Count)).Reverse().Select(k => MakeInput(k, true)).ToArray();
                if (releases.Length > 0) Native.SendInput((uint)releases.Length, releases, Marshal.SizeOf<Native.Input>());
                throw new InvalidOperationException(L.T("Windows 阻止了快捷键发送。普通权限客户端无法向管理员权限应用注入按键。"));
            }
            return;
        }
        if (launcher.ActionType == "script")
        {
            Process.Start(ScriptAction.CreateStartInfo(launcher.Exec))?.Dispose();
        }
        else if (launcher.ActionType == "command")
        {
            var start = new ProcessStartInfo(Environment.GetEnvironmentVariable("COMSPEC") ?? "cmd.exe") { UseShellExecute = false };
            start.Arguments = "/d /s /c \"" + launcher.Exec + "\"";
            Process.Start(start)?.Dispose();
        }
        else await ApplicationActivation.Open(launcher, releasePanel);
    }

    internal static bool IsExplorerWindow(nint window)
    {
        var name = new System.Text.StringBuilder(256);
        Native.GetClassNameW(window, name, name.Capacity);
        return name.ToString() is "CabinetWClass" or "ExploreWClass";
    }

    private static Native.Input MakeInput(ushort key, bool up) => new()
    {
        Type = 1, Data = new() { Keyboard = new() { Key = key, Flags = (up ? 2u : 0) | (key is >= 0x21 and <= 0x2E or 0x5B ? 1u : 0), ExtraInfo = 0x4C504943 } }
    };
}
