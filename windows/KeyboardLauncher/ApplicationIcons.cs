using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;

namespace KeyboardLauncher;

// Ask the Windows Shell for the application's icon, not a document thumbnail.
// The Shell resolves shortcut icon locations and registered file associations.
internal static class ApplicationIcons
{
    private sealed record Pixels(int Width, int Height, byte[] Data);
    private static readonly ConcurrentDictionary<string, Task<Pixels?>> cache = new(StringComparer.OrdinalIgnoreCase);
    internal static async Task<WriteableBitmap?> Load(string path)
    {
        var pixels = await cache.GetOrAdd(path, p => Task.Run(() => Extract(p)));
        if (pixels == null) return null;
        var bitmap = new WriteableBitmap(pixels.Width, pixels.Height);
        using var stream = bitmap.PixelBuffer.AsStream();
        await stream.WriteAsync(pixels.Data);
        bitmap.Invalidate();
        return bitmap;
    }
    private static Pixels? Extract(string path)
    {
        int initialized = CoInitializeEx(0, 0);
        IShellItemImageFactory? factory = null;
        nint bitmap = 0, dc = 0;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            if (SHCreateItemFromParsingName(path, 0, ref iid, out factory) < 0 || factory == null) return null;
            if (factory.GetImage(new Size { Width = 96, Height = 96 }, 4, out bitmap) < 0 || bitmap == 0) return null;
            if (GetObjectW(bitmap, Marshal.SizeOf<Bitmap>(), out var image) == 0) return null;
            int width = image.Width, height = Math.Abs(image.Height);
            if (width <= 0 || height <= 0 || width > 2048 || height > 2048) return null;
            var bytes = new byte[checked(width * height * 4)];
            var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
            dc = CreateCompatibleDC(0);
            if (GetDIBits(dc, bitmap, 0, (uint)height, bytes, ref info, 0) == 0) return null;
            bool alpha = false;
            for (int i = 3; i < bytes.Length; i += 4) alpha |= bytes[i] != 0;
            if (!alpha) for (int i = 3; i < bytes.Length; i += 4) bytes[i] = 255;
            // XAML expects premultiplied BGRA. Clear hidden RGB from transparent
            // shell pixels; otherwise some legacy icons show a pale square outline.
            bool straightAlpha = false;
            for (int i = 0; i < bytes.Length; i += 4)
                if (bytes[i + 3] is > 0 and < 255 && (bytes[i] > bytes[i + 3] || bytes[i + 1] > bytes[i + 3] || bytes[i + 2] > bytes[i + 3])) straightAlpha = true;
            for (int i = 0; i < bytes.Length; i += 4)
            {
                if (bytes[i + 3] == 0) bytes[i] = bytes[i + 1] = bytes[i + 2] = 0;
                else if (straightAlpha)
                    for (int channel = 0; channel < 3; channel++) bytes[i + channel] = (byte)((bytes[i + channel] * bytes[i + 3] + 127) / 255);
            }
            return new(width, height, bytes);
        }
        catch { return null; }
        finally
        {
            if (bitmap != 0) Native.DeleteObject(bitmap);
            if (dc != 0) DeleteDC(dc);
            if (factory != null) Marshal.ReleaseComObject(factory);
            if (initialized >= 0) CoUninitialize();
        }
    }
    [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IShellItemImageFactory { [PreserveSig] int GetImage(Size size, uint flags, out nint bitmap); }
    [StructLayout(LayoutKind.Sequential)] private struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential)] private struct Bitmap { public int Type, Width, Height, WidthBytes; public ushort Planes, BitsPixel; public nint Bits; }
    [StructLayout(LayoutKind.Sequential)] private struct BitmapInfo
    {
        public uint Size; public int Width, Height; public ushort Planes, BitCount;
        public uint Compression, SizeImage; public int XPels, YPels; public uint Used, Important;
    }
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(nint reserved, uint flags);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] private static extern int SHCreateItemFromParsingName(string path, nint bind, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory factory);
    [DllImport("gdi32.dll")] private static extern int GetObjectW(nint bitmap, int size, out Bitmap data);
    [DllImport("gdi32.dll")] private static extern nint CreateCompatibleDC(nint dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(nint dc);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(nint dc, nint bitmap, uint start, uint lines, [Out] byte[] data, ref BitmapInfo info, uint usage);
}