using System.Drawing.Imaging;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Applele.Windows.Assets;

internal sealed class SpriteAsset : IDisposable
{
    public Bitmap Bitmap { get; }
    public Rectangle ContentBounds { get; }
    public double TransparentFraction { get; }
    public SpriteAsset(string path)
    {
        using var original = new Bitmap(path);
        if (original.Width > 4096 || original.Height > 4096 || !Image.IsAlphaPixelFormat(original.PixelFormat))
            throw new InvalidDataException("素材必须是带 Alpha 的透明 PNG，最大 4096 像素：" + path);
        Bitmap = original.Clone(new Rectangle(0, 0, original.Width, original.Height), PixelFormat.Format32bppPArgb);
        try
        {
            var data = Bitmap.LockBits(new Rectangle(0, 0, Bitmap.Width, Bitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppPArgb);
            var bytes = new byte[data.Stride * Bitmap.Height];
            try { Marshal.Copy(data.Scan0, bytes, 0, bytes.Length); } finally { Bitmap.UnlockBits(data); }
            int minX = Bitmap.Width, minY = Bitmap.Height, maxX = -1, maxY = -1, transparent = 0;
            for (int y = 0; y < Bitmap.Height; y++) for (int x = 0; x < Bitmap.Width; x++)
            {
                var alpha = bytes[y * data.Stride + x * 4 + 3];
                if (alpha < 8) transparent++;
                if (alpha >= 16) { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
                if ((x == 0 || y == 0 || x == Bitmap.Width - 1 || y == Bitmap.Height - 1) && alpha > 8)
                    throw new InvalidDataException("素材边缘不透明或被截断，需要透明留白：" + path);
            }
            TransparentFraction = transparent / (double)(Bitmap.Width * Bitmap.Height);
            if (maxX < minX || TransparentFraction < .15) throw new InvalidDataException("素材为空或仍有不透明底色：" + path);
            ContentBounds = Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            // Normalize once in memory. Rendering a full-resolution generated photo every idle frame
            // wastes CPU; a 512px subject cache still covers the 232-DIP pet at 200% scaling.
            var factor = Math.Min(1, 512.0 / Math.Max(ContentBounds.Width, ContentBounds.Height));
            int cacheWidth = (int)Math.Ceiling(ContentBounds.Width * factor), cacheHeight = (int)Math.Ceiling(ContentBounds.Height * factor);
            var normalized = new Bitmap(cacheWidth + 8, cacheHeight + 8, PixelFormat.Format32bppPArgb);
            using (var graphics = Graphics.FromImage(normalized))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy; graphics.Clear(Color.Transparent);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic; graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.DrawImage(Bitmap, new Rectangle(4, 4, cacheWidth, cacheHeight), ContentBounds, GraphicsUnit.Pixel);
            }
            Bitmap.Dispose(); Bitmap = normalized; ContentBounds = new Rectangle(4, 4, cacheWidth, cacheHeight);
        }
        catch { Bitmap.Dispose(); throw; }
    }
    public void Dispose() => Bitmap.Dispose();
}
