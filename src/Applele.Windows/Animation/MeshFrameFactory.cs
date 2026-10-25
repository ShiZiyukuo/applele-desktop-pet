using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Applele.Core;
using Applele.Windows.Assets;

namespace Applele.Windows.Animation;

internal static class MeshFrameFactory
{
    public static Bitmap Create(SpriteAsset source,double canvasDip,double spriteDip,double scale,Func<double,double,double,double,RigPoint> deform,int columns=8,int rows=12)
    {
        var size=(int)Math.Round(canvasDip*scale);var bitmap=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        try
        {
            using var graphics=Graphics.FromImage(bitmap);graphics.Clear(Color.Transparent);graphics.CompositingMode=CompositingMode.SourceOver;
            graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;graphics.SmoothingMode=SmoothingMode.None;
            var bounds=source.ContentBounds;var factor=spriteDip/Math.Max(bounds.Width,bounds.Height);var width=bounds.Width*factor;var height=bounds.Height*factor;
            PointF Destination(double u,double v){var p=deform(u,v,width,height);return new((float)((canvasDip/2+p.X)*scale),(float)((canvasDip-38+p.Y)*scale));}
            for(int row=0;row<rows;row++)for(int column=0;column<columns;column++)
            {
                float sx0=bounds.Left+bounds.Width*column/(float)columns,sx1=bounds.Left+bounds.Width*(column+1)/(float)columns;
                float sy0=bounds.Top+bounds.Height*row/(float)rows,sy1=bounds.Top+bounds.Height*(row+1)/(float)rows;var rect=new RectangleF(sx0,sy0,sx1-sx0,sy1-sy0);
                var a=Destination(column/(double)columns,row/(double)rows);var b=Destination((column+1)/(double)columns,row/(double)rows);
                var c=Destination(column/(double)columns,(row+1)/(double)rows);var d=Destination((column+1)/(double)columns,(row+1)/(double)rows);
                Triangle(graphics,source.Bitmap,rect,a,b,c,false);Triangle(graphics,source.Bitmap,rect,b,d,c,true);
            }
            return bitmap;
        }
        catch{bitmap.Dispose();throw;}
    }
    static void Triangle(Graphics g,Bitmap texture,RectangleF source,PointF a,PointF b,PointF c,bool lower)
    {
        var cx=(a.X+b.X+c.X)/3;var cy=(a.Y+b.Y+c.Y)/3;
        PointF Expand(PointF p){var dx=p.X-cx;var dy=p.Y-cy;var distance=Math.Max(.01,Math.Sqrt(dx*dx+dy*dy));return new(p.X+(float)(dx/distance*.65),p.Y+(float)(dy/distance*.65));}
        using var clip=new GraphicsPath();clip.AddPolygon([Expand(a),Expand(b),Expand(c)]);var state=g.Save();g.SetClip(clip,CombineMode.Replace);
        PointF[] target=lower?[new(a.X+c.X-b.X,a.Y+c.Y-b.Y),a,c]:[a,b,c];var origin=target[0];
        var ux=(target[1].X-origin.X)/source.Width;var uy=(target[1].Y-origin.Y)/source.Width;var vx=(target[2].X-origin.X)/source.Height;var vy=(target[2].Y-origin.Y)/source.Height;
        const float padding=1.5f;
        target=[new(origin.X-(ux+vx)*padding,origin.Y-(uy+vy)*padding),new(target[1].X+(ux-vx)*padding,target[1].Y+(uy-vy)*padding),new(target[2].X+(-ux+vx)*padding,target[2].Y+(-uy+vy)*padding)];
        source.Inflate(padding,padding);using var attributes=new ImageAttributes();attributes.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(texture,target,source,GraphicsUnit.Pixel,attributes);g.Restore(state);
    }
}
