using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Applele.Windows.Windowing;

internal static class NativeMethods
{
    internal const int WsExLayered = 0x80000, WsExNoActivate = 0x08000000, WsExToolWindow = 0x80;
    internal const int WmNcHitTest = 0x84, WmMouseActivate = 0x21, HtTransparent = -1, HtClient = 1, MaNoActivate = 3;
    [StructLayout(LayoutKind.Sequential)] internal struct Point { public int X, Y; public Point(int x, int y) { X = x; Y = y; } }
    [StructLayout(LayoutKind.Sequential)] internal struct Size { public int Width, Height; }
    [StructLayout(LayoutKind.Sequential, Pack = 1)] internal struct Blend { public byte Operation, Flags, Alpha, Format; }
    [StructLayout(LayoutKind.Sequential)] internal struct BitmapInfo
    {
        public uint HeaderSize; public int Width, Height; public ushort Planes, BitCount; public uint Compression, SizeImage;
        public int XPelsPerMeter, YPelsPerMeter; public uint ClrUsed, ClrImportant;
    }
    [DllImport("user32.dll")] internal static extern IntPtr GetDC(IntPtr hwnd);
    [DllImport("user32.dll")] internal static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] internal static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] internal static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll", SetLastError = true)] internal static extern IntPtr CreateDIBSection(IntPtr dc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool UpdateLayeredWindow(IntPtr hwnd, IntPtr destinationDc, ref Point destination, ref Size size, IntPtr sourceDc, ref Point source, uint colorKey, ref Blend blend, uint flags);
    [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] internal static extern IntPtr SendMessage(IntPtr hwnd, int message, IntPtr wp, IntPtr lp);
    [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(Point point);
    [DllImport("user32.dll")] internal static extern int GetGuiResources(IntPtr process, int flags);
    internal static IntPtr PackedPoint(int x, int y) => new(unchecked((int)(((uint)(ushort)y << 16) | (ushort)x)));
    internal static void Require(bool ok) { if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error()); }
}
