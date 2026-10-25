using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Applele.Windows.Windowing;

/// <summary>Reusable top-down premultiplied-alpha DIB. Owns every native resource it allocates.</summary>
internal sealed class LayeredSurface : IDisposable
{
    readonly IntPtr screenDc, memoryDc, dib, oldBitmap, dibBits;
    readonly byte[] pixels;
    bool disposed;
    public Bitmap Canvas { get; }
    public int Width => Canvas.Width;
    public LayeredSurface(int size)
    {
        Canvas = new Bitmap(size, size, PixelFormat.Format32bppPArgb); pixels = new byte[size * size * 4];
        screenDc = NativeMethods.GetDC(IntPtr.Zero); memoryDc = NativeMethods.CreateCompatibleDC(screenDc);
        var info = new NativeMethods.BitmapInfo { HeaderSize = (uint)Marshal.SizeOf<NativeMethods.BitmapInfo>(), Width = size, Height = -size, Planes = 1, BitCount = 32 };
        dib = NativeMethods.CreateDIBSection(screenDc, ref info, 0, out dibBits, IntPtr.Zero, 0);
        if (dib == IntPtr.Zero || memoryDc == IntPtr.Zero) { Dispose(); throw new InvalidOperationException("无法创建桌宠透明画布。"); }
        oldBitmap = NativeMethods.SelectObject(memoryDc, dib);
    }
    public void Present(IntPtr hwnd, Point position)
    {
        var data = Canvas.LockBits(new Rectangle(0, 0, Width, Width), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
        try { Marshal.Copy(data.Scan0, pixels, 0, pixels.Length); } finally { Canvas.UnlockBits(data); }
        Marshal.Copy(pixels, 0, dibBits, pixels.Length);
        var destination = new NativeMethods.Point(position.X, position.Y); var source = new NativeMethods.Point(0, 0);
        var size = new NativeMethods.Size { Width = Width, Height = Width }; var blend = new NativeMethods.Blend { Operation = 0, Alpha = 255, Format = 1 };
        NativeMethods.Require(NativeMethods.UpdateLayeredWindow(hwnd, screenDc, ref destination, ref size, memoryDc, ref source, 0, ref blend, 2));
    }
    public bool Hits(int x, int y, int threshold) => x >= 0 && y >= 0 && x < Width && y < Width && pixels[(y * Width + x) * 4 + 3] >= threshold;
    public byte AlphaAt(int x, int y) => pixels[(y * Width + x) * 4 + 3];
    public void Save(string path) => Canvas.Save(path, ImageFormat.Png);
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (oldBitmap != IntPtr.Zero && memoryDc != IntPtr.Zero) NativeMethods.SelectObject(memoryDc, oldBitmap);
        if (dib != IntPtr.Zero) NativeMethods.DeleteObject(dib);
        if (memoryDc != IntPtr.Zero) NativeMethods.DeleteDC(memoryDc);
        if (screenDc != IntPtr.Zero) NativeMethods.ReleaseDC(IntPtr.Zero, screenDc);
        Canvas.Dispose();
    }
}
