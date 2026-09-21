using System.Runtime.InteropServices;

namespace KeyboardLauncher;

// Click-through, non-activating companion surface. A custom window region disables
// the standard DWM shadow, so draw the soft rounded shadow independently.
internal sealed class PanelShadow : IDisposable
{
    private readonly nint window;
    private int cachedWidth, cachedHeight, cachedMargin;
    private nint bitmap, previousBitmap, dc;
    internal PanelShadow(nint owner)
    {
        window = CreateWindowExW(0x080800A0, "STATIC", "", unchecked((uint)0x80000000), 0, 0, 0, 0, owner, 0, 0, 0);
        if (window == 0) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    internal void Show(int x, int y, int width, int height)
    {
        double scale = width / 1040d;
        int margin = Math.Max(12, (int)Math.Ceiling(24 * scale));
        int fullWidth = width + margin * 2, fullHeight = height + margin * 2;
        if (cachedWidth != fullWidth || cachedHeight != fullHeight || cachedMargin != margin)
        {
            ReleaseBitmap(); dc = CreateCompatibleDC(0);
            var info = new BitmapInfo { Size = 40, Width = fullWidth, Height = -fullHeight, Planes = 1, BitCount = 32 };
            bitmap = CreateDIBSection(dc, ref info, 0, out var pixels, 0, 0);
            if (bitmap == 0) { ReleaseBitmap(); return; }
            previousBitmap = SelectObject(dc, bitmap);
            var bytes = new byte[checked(fullWidth * fullHeight * 4)];
            double radius = 26 * scale, blur = 10 * scale, offset = 4 * scale;
            for (int py = 0; py < fullHeight; py++)
            for (int px = 0; px < fullWidth; px++)
            {
                double dx = Math.Abs(px - margin - width / 2d) - (width / 2d - radius);
                double dy = Math.Abs(py - margin - height / 2d - offset) - (height / 2d - radius);
                double distance = Math.Sqrt(Math.Pow(Math.Max(dx, 0), 2) + Math.Pow(Math.Max(dy, 0), 2)) + Math.Min(Math.Max(dx, dy), 0) - radius;
                // Clip only the panel interior. An exterior transparent gap exposes the
                // bright desktop as a halo between the dark panel and its shadow.
                double ox = Math.Abs(px - margin - width / 2d) - (width / 2d - radius);
                double oy = Math.Abs(py - margin - height / 2d) - (height / 2d - radius);
                double inside = Math.Sqrt(Math.Pow(Math.Max(ox, 0), 2) + Math.Pow(Math.Max(oy, 0), 2)) + Math.Min(Math.Max(ox, oy), 0) - radius;
                double coverage = Math.Clamp(inside + .5, 0, 1);
                if (coverage <= 0) continue;
                bytes[(py * fullWidth + px) * 4 + 3] = (byte)(24 * coverage * Math.Exp(-Math.Pow(Math.Max(0, distance) / Math.Max(1, blur), 2) / 2));
            }
            Marshal.Copy(bytes, 0, pixels, bytes.Length);
            cachedWidth = fullWidth; cachedHeight = fullHeight; cachedMargin = margin;
        }
        if (bitmap == 0) return;
        var position = new Native.Point { X = x - margin, Y = y - margin };
        var size = new Size { Width = fullWidth, Height = fullHeight };
        var source = new Native.Point(); var blend = new Blend { Operation = 0, Alpha = 255, Format = 1 };
        UpdateLayeredWindow(window, 0, ref position, ref size, dc, ref source, 0, ref blend, 2);
        Native.ShowWindow(window, 4); // SW_SHOWNOACTIVATE
    }
    internal void Hide() => Native.ShowWindow(window, 0);
    private void ReleaseBitmap()
    {
        if (dc != 0 && previousBitmap != 0) SelectObject(dc, previousBitmap);
        if (bitmap != 0) Native.DeleteObject(bitmap);
        if (dc != 0) DeleteDC(dc);
        bitmap = previousBitmap = dc = 0;
    }
    public void Dispose() { ReleaseBitmap(); DestroyWindow(window); }

    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] private struct Blend { public byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPels, YPels; public uint Used, Important;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern nint CreateWindowExW(uint exStyle, string className, string title, uint style, int x, int y, int width, int height, nint owner, nint menu, nint instance, nint data);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint hwnd);
    [DllImport("user32.dll")] private static extern bool UpdateLayeredWindow(nint hwnd, nint screenDc, ref Native.Point position, ref Size size, nint sourceDc, ref Native.Point source, uint colorKey, ref Blend blend, uint flags);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern nint SelectObject(nint dc, nint obj);
    [DllImport("gdi32.dll")] private static extern nint CreateDIBSection(nint dc, ref BitmapInfo info, uint usage, out nint pixels, nint section, uint offset);
}
