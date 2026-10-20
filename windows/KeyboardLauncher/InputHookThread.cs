using System.ComponentModel;
using System.Runtime.InteropServices;
using KeyboardLauncher.Core;

namespace KeyboardLauncher;

// Low-level hooks must keep pumping messages even while WinUI is rendering or
// loading an icon. Windows silently removes hooks whose owning thread stalls.
internal sealed class InputHookThread : IDisposable
{
    private readonly Native.HookProc keyboardProc, mouseProc;
    private readonly Thread thread;
    private uint threadId;
    private bool disposed;

    internal InputHookThread(Native.HookProc keyboard, Native.HookProc mouse)
    {
        keyboardProc = keyboard; mouseProc = mouse;
        using var ready = new ManualResetEventSlim();
        Exception? startupError = null;
        thread = new Thread(() =>
        {
            nint keyboardHook = 0, mouseHook = 0;
            try
            {
                threadId = Native.GetCurrentThreadId();
                // Create the queue before allowing Dispose to post WM_QUIT.
                PeekMessageW(out _, 0, 0, 0, 0);
                keyboardHook = Native.SetWindowsHookExW(13, keyboardProc, Native.GetModuleHandleW(null), 0);
                if (keyboardHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("无法安装键盘钩子。"));
                mouseHook = Native.SetWindowsHookExW(14, mouseProc, Native.GetModuleHandleW(null), 0);
                if (mouseHook == 0) throw new Win32Exception(Marshal.GetLastWin32Error(), L.T("无法安装鼠标钩子。"));
            }
            catch (Exception ex) { startupError = ex; }
            finally { ready.Set(); }
            try
            {
                if (startupError != null) return;
                while (GetMessageW(out var message, 0, 0, 0) > 0)
                {
                    TranslateMessage(ref message);
                    DispatchMessageW(ref message);
                }
            }
            finally
            {
                if (keyboardHook != 0) Native.UnhookWindowsHookEx(keyboardHook);
                if (mouseHook != 0) Native.UnhookWindowsHookEx(mouseHook);
            }
        }) { IsBackground = true, Name = "Keyboard Launcher input hooks" };
        thread.Start();
        ready.Wait();
        if (startupError != null) { thread.Join(); throw startupError; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        PostThreadMessageW(threadId, 0x12, 0, 0);
        thread.Join();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Window;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public Native.Point Point;
        public uint Private;
    }

    [DllImport("user32.dll")] private static extern bool PeekMessageW(out Message message, nint window, uint min, uint max, uint remove);
    [DllImport("user32.dll")] private static extern int GetMessageW(out Message message, nint window, uint min, uint max);
    [DllImport("user32.dll")] private static extern bool TranslateMessage(ref Message message);
    [DllImport("user32.dll")] private static extern nint DispatchMessageW(ref Message message);
    [DllImport("user32.dll")] private static extern bool PostThreadMessageW(uint threadId, uint message, nuint wParam, nint lParam);
}
