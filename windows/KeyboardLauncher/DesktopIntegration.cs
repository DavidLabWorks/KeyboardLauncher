using System.ComponentModel;
using System.Runtime.InteropServices;
using KeyboardLauncher.Core;
using Microsoft.UI.Dispatching;

namespace KeyboardLauncher;

internal sealed class DesktopIntegration : IDisposable
{
    private readonly nint hwnd;
    private readonly DispatcherQueue dispatcher;
    private readonly Native.SubclassProc windowProc;
    private readonly Native.HookProc keyboardProc, mouseProc;
    private nint keyboardHook, mouseHook;
    private Native.NotifyIconData tray;
    private readonly uint taskbarCreated = Native.RegisterWindowMessageW("TaskbarCreated");
    private readonly DoubleModifierTap doubleTap = new();
    private readonly HashSet<uint> down = [], swallowed = [];
    private Hotkey? shortcut;
    private bool suppress, disposed;
    private int hotkeyId = 1;
    private nint recordingWindow;
    private Action<Hotkey>? recordingCallback;
    public void BeginRecording(nint window, Action<Hotkey> callback)
    {
        recordingWindow = window; recordingCallback = callback; doubleTap.Reset();
    }
    public void EndRecording(nint window)
    {
        if (recordingWindow != window) return;
        recordingCallback = null; recordingWindow = 0; doubleTap.Reset();
    }
    public bool IsAnyKeyDown => down.Count != 0;
    public bool Suspended { get; set; }
    public event Action? Toggle, Settings, Quit;
    public event Action<uint, uint>? PanelKeyPressed;
    internal Action<string>? DoubleTapDiagnostic;

    public DesktopIntegration(nint window, DispatcherQueue queue)
    {
        hwnd = window; dispatcher = queue;
        windowProc = WindowMessage; keyboardProc = KeyboardMessage; mouseProc = MouseMessage;
        try
        {
            if (!Native.SetWindowSubclass(hwnd, windowProc, 1, 0)) throw new Win32Exception("无法安装窗口消息处理程序。");
            keyboardHook = Native.SetWindowsHookExW(13, keyboardProc, Native.GetModuleHandleW(null), 0);
            if (keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装键盘钩子。");
            mouseHook = Native.SetWindowsHookExW(14, mouseProc, Native.GetModuleHandleW(null), 0);
            if (mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法安装鼠标钩子。");
            tray = new() { Size = (uint)Marshal.SizeOf<Native.NotifyIconData>(), Window = hwnd, Id = 1, Flags = 7, CallbackMessage = 0x8001,
                Icon = Native.LoadImageW(0, Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"), 1, 32, 32, 0x10), Tip = "Keyboard Launcher · 键盘启动器", Info = "", InfoTitle = "" };
            if (!Native.Shell_NotifyIconW(0, ref tray)) throw new Win32Exception("无法创建托盘图标。");
        }
        catch { Dispose(); throw; }
    }

    // Register a new ID before removing the old shortcut, so conflicts preserve the working one.
    public void Configure(LauncherConfig config)
    {
        var next = string.IsNullOrWhiteSpace(config.Shortcut) ? (Hotkey?)null : Hotkey.Parse(config.Shortcut);
        if (next != shortcut || suppress != config.SuppressSystemShortcut)
        {
            var newId = hotkeyId == 1 ? 2 : 1;
            if (next is Hotkey hotkey && !config.SuppressSystemShortcut && !Native.RegisterHotKey(hwnd, newId, hotkey.Modifiers | 0x4000, hotkey.Key))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "快捷键已被其他程序占用，请选择其他组合，或启用拦截模式。");
            Native.UnregisterHotKey(hwnd, hotkeyId);
            hotkeyId = newId; shortcut = next; suppress = config.SuppressSystemShortcut;
        }
        doubleTap.Modifier = config.DoubleTapKey;
        doubleTap.Reset();
    }

    private nint WindowMessage(nint window, uint message, nint wParam, nint lParam, nuint id, nuint data)
    {
        if (message == taskbarCreated) Native.Shell_NotifyIconW(0, ref tray);
        if (message == 0x312 && (int)wParam == hotkeyId && !Suspended) dispatcher.TryEnqueue(() => Toggle?.Invoke());
        if (message == 0x8001)
        {
            if ((uint)lParam == 0x202) dispatcher.TryEnqueue(() => Toggle?.Invoke());
            if ((uint)lParam == 0x205) ShowTrayMenu();
        }
        return Native.DefSubclassProc(window, message, wParam, lParam);
    }

    private void ShowTrayMenu()
    {
        var menu = Native.CreatePopupMenu();
        try
        {
            Native.AppendMenuW(menu, 0, 1, "打开启动器"); Native.AppendMenuW(menu, 0, 2, "设置…");
            Native.AppendMenuW(menu, 0x800, 0, null); Native.AppendMenuW(menu, 0, 3, "退出");
            Native.GetCursorPos(out var point); Native.SetForegroundWindow(hwnd);
            var result = Native.TrackPopupMenu(menu, 0x100 | 2, point.X, point.Y, 0, hwnd, 0);
            Native.PostMessageW(hwnd, 0, 0, 0);
            dispatcher.TryEnqueue(() => { if (result == 1) Toggle?.Invoke(); if (result == 2) Settings?.Invoke(); if (result == 3) Quit?.Invoke(); });
        }
        finally { Native.DestroyMenu(menu); }
    }

    private nint KeyboardMessage(int code, nint wParam, nint lParam)
    {
        if (code < 0) return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        var key = Marshal.PtrToStructure<Native.KeyboardData>(lParam);
        // Ignore our own SendInput events, not all injected input: VM / remote
        // keyboard drivers may also mark a user's physical keystrokes injected.
        if (key.ExtraInfo == 0x4C504943) return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        // Alt menu suppression tools can insert VK_NONAME. It is not a
        // physical chord key and must not cancel an otherwise isolated Alt tap.
        if (key.Key == 0xFC) return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        if (key.Key == 0x07 && (key.Flags & 0x10) != 0) return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        // Some virtual/remote keyboards report generic modifier VKs.
        key.Key = key.Key switch
        {
            0x12 => (key.Flags & 1) != 0 ? 0xA5u : 0xA4u,
            0x11 => (key.Flags & 1) != 0 ? 0xA3u : 0xA2u,
            0x10 => key.ScanCode == 0x36 ? 0xA1u : 0xA0u,
            _ => key.Key
        };
        bool isDown = (uint)wParam is 0x100 or 0x104;
        bool repeat = isDown && !down.Add(key.Key);
        if (!isDown) down.Remove(key.Key);
        // Capture before normal shortcuts, including while the launcher is suspended.
        // Use hook state: suppressed modifier presses never reach GetAsyncKeyState.
        if (recordingCallback is { } callback && Native.GetForegroundWindow() == recordingWindow)
        {
            doubleTap.Reset();
            if (isDown)
            {
                swallowed.Add(key.Key);
                if (!repeat && key.Key is not (0x10 or 0x11 or 0x12 or >= 0xA0 and <= 0xA5 or 0x5B or 0x5C))
                {
                    uint mods = 0;
                    if (down.Overlaps(new uint[] { 0x11, 0xA2, 0xA3 })) mods |= 2;
                    if (down.Overlaps(new uint[] { 0x12, 0xA4, 0xA5 })) mods |= 1;
                    if (down.Overlaps(new uint[] { 0x10, 0xA0, 0xA1 })) mods |= 4;
                    if (down.Overlaps(new uint[] { 0x5B, 0x5C })) mods |= 8;
                    var captured = new Hotkey(mods, key.Key);
                    dispatcher.TryEnqueue(() => { if (recordingCallback == callback) callback(captured); });
                }
                return 1;
            }
            // Let releases through for keys pressed before recording began.
            if (swallowed.Remove(key.Key)) return 1;
            return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
        }
        // Pair swallowed key-downs with their releases even if settings changed meanwhile.
        if (!isDown && swallowed.Remove(key.Key)) return 1;
        if (Suspended) doubleTap.Reset();
        else
        {
            if (isDown && Native.GetForegroundWindow() == hwnd &&
                (key.Key == 0x1B || (Native.Modifiers == 0 && (key.Key is 0x25 or 0x27 || KeyboardLayout.Slot((int)key.ScanCode, 0) >= 0))))
            {
                if (!repeat) dispatcher.TryEnqueue(() => PanelKeyPressed?.Invoke(key.Key, key.ScanCode));
                doubleTap.Reset(); swallowed.Add(key.Key); return 1;
            }
            uint selectedModifier = doubleTap.Modifier switch { "control" or "leftControl" or "rightControl" => 2u, "alt" or "leftAlt" or "rightAlt" => 1u, "shift" or "leftShift" or "rightShift" => 4u, _ => 0u };
            bool other = (Native.Modifiers & ~selectedModifier) != 0 || down.Any(pressed => pressed != key.Key);
            var fired = doubleTap.Handle(key.Key, isDown, key.Time, repeat, other);
            DoubleTapDiagnostic?.Invoke($"key={key.Key:X} down={isDown} mods={Native.Modifiers} other={other} repeat={repeat} fired={fired} held={string.Join(",", down)}");
            if (fired) dispatcher.TryEnqueue(() => Toggle?.Invoke());
            if (suppress && shortcut is Hotkey hotkey && isDown && key.Key == hotkey.Key && Native.Modifiers == hotkey.Modifiers)
            {
                if (!repeat) dispatcher.TryEnqueue(() => Toggle?.Invoke());
                swallowed.Add(key.Key); return 1;
            }
        }
        return Native.CallNextHookEx(keyboardHook, code, wParam, lParam);
    }

    private nint MouseMessage(int code, nint wParam, nint lParam)
    {
        if (code >= 0 && (uint)wParam is 0x201 or 0x204 or 0x207 or 0x20B) doubleTap.Reset();
        return Native.CallNextHookEx(mouseHook, code, wParam, lParam);
    }

    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Native.UnregisterHotKey(hwnd, hotkeyId);
        if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
        if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
        if (tray.Window != 0) Native.Shell_NotifyIconW(2, ref tray);
        if (tray.Icon != 0) Native.DestroyIcon(tray.Icon);
        Native.RemoveWindowSubclass(hwnd, windowProc, 1);
    }
}
