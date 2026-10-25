using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Applele.Core;

namespace Applele.Windows.Assets;

internal sealed record AtlasCel(Rectangle Cell,Rectangle Content,double BodyX,double GroundY);

/// <summary>Reads authored cels without changing the PNG. Registers the torso and ground line at import.</summary>
internal sealed class FrameAtlas : IDisposable
{
    readonly Bitmap bitmap;
    public IReadOnlyList<AtlasCel> Cels {get;}
    public FrameAtlas(string path,SpriteAtlasDefinition definition)
    {
        definition.Validate();using var original=new Bitmap(path);
        if(original.Width>4096||original.Height>4096||original.Width<definition.Columns*16||original.Height<definition.Rows*16||!Image.IsAlphaPixelFormat(original.PixelFormat))
            throw new InvalidDataException("人物动作图必须为适当尺寸的透明 PNG："+path);
        bitmap=original.Clone(new Rectangle(Point.Empty,original.Size),PixelFormat.Format32bppPArgb);
        try
        {
            var data=bitmap.LockBits(new Rectangle(Point.Empty,bitmap.Size),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);
            var pixels=new byte[data.Stride*bitmap.Height];
            try{Marshal.Copy(data.Scan0,pixels,0,pixels.Length);}finally{bitmap.UnlockBits(data);}
            byte Alpha(int x,int y)=>pixels[y*data.Stride+x*4+3];
            // Generated atlases sometimes space complete figures unevenly. Detect actual empty gutters;
            // never crop an opaque coat/weapon just to force an equal mathematical grid.
            int FindCut(int goal,int radius,int maximum,Func<int,bool> empty)
            {
                for(int distance=0;distance<=radius;distance++)foreach(var candidate in new[]{goal-distance,goal+distance})
                    if(candidate>0&&candidate<maximum&&empty(candidate)&&empty(candidate-1))return candidate;
                throw new InvalidDataException("人物帧之间没有完整透明间隔，不能无损分格："+path);
            }
            List<Rectangle> RowPartition()
            {
            var rowCuts=new int[definition.Rows+1];rowCuts[^1]=bitmap.Height;
            for(int row=1;row<definition.Rows;row++)
            {
                var goal=(int)Math.Round(row*bitmap.Height/(double)definition.Rows);
                bool EmptyRow(int y){for(int x=0;x<bitmap.Width;x++)if(Alpha(x,y)>=16)return false;return true;}
                rowCuts[row]=definition.UseTransparentGutters?FindCut(goal,bitmap.Height/definition.Rows/5,bitmap.Height,EmptyRow):goal;
            }
            var cells=new List<Rectangle>();
            for(int row=0;row<definition.Rows;row++)
            {
                var cuts=new int[definition.Columns+1];cuts[^1]=bitmap.Width;
                for(int column=1;column<definition.Columns;column++)
                {
                    var goal=(int)Math.Round(column*bitmap.Width/(double)definition.Columns);
                    bool EmptyColumn(int x){for(int y=rowCuts[row];y<rowCuts[row+1];y++)if(Alpha(x,y)>=16)return false;return true;}
                    cuts[column]=definition.UseTransparentGutters?FindCut(goal,bitmap.Width/definition.Columns/3,bitmap.Width,EmptyColumn):goal;
                }
                for(int column=0;column<definition.Columns;column++)cells.Add(Rectangle.FromLTRB(cuts[column],rowCuts[row],cuts[column+1],rowCuts[row+1]));
            }
            return cells;
            }
            List<Rectangle> ColumnPartition()
            {
                var cuts=new int[definition.Columns+1];cuts[^1]=bitmap.Width;
                for(int column=1;column<definition.Columns;column++)
                {
                    bool EmptyColumn(int x){for(int y=0;y<bitmap.Height;y++)if(Alpha(x,y)>=16)return false;return true;}
                    cuts[column]=FindCut((int)Math.Round(column*bitmap.Width/(double)definition.Columns),bitmap.Width/definition.Columns/3,bitmap.Width,EmptyColumn);
                }
                var result=new Rectangle[definition.Count];
                for(int column=0;column<definition.Columns;column++)
                {
                    var rows=new int[definition.Rows+1];rows[^1]=bitmap.Height;
                    for(int row=1;row<definition.Rows;row++)
                    {
                        bool EmptyRow(int y){for(int x=cuts[column];x<cuts[column+1];x++)if(Alpha(x,y)>=16)return false;return true;}
                        rows[row]=FindCut((int)Math.Round(row*bitmap.Height/(double)definition.Rows),bitmap.Height/definition.Rows/3,bitmap.Height,EmptyRow);
                    }
                    for(int row=0;row<definition.Rows;row++)result[row*definition.Columns+column]=Rectangle.FromLTRB(cuts[column],rows[row],cuts[column+1],rows[row+1]);
                }
                return result.ToList();
            }
            List<Rectangle> cells;
            try{cells=RowPartition();}catch(InvalidDataException)when(definition.UseTransparentGutters){cells=ColumnPartition();}
            var cels=new List<AtlasCel>();
            for(int index=0;index<definition.Count;index++)
            {
                var cell=cells[index];
                int minX=cell.Right,minY=cell.Bottom,maxX=-1,maxY=-1,transparent=0;
                for(int y=cell.Top;y<cell.Bottom;y++)for(int x=cell.Left;x<cell.Right;x++)
                {
                    var alpha=Alpha(x,y);if(alpha<8)transparent++;
                    if(alpha<16)continue;
                    if(x==cell.Left||x==cell.Right-1||y==cell.Top||y==cell.Bottom-1)
                        throw new InvalidDataException($"人物动作第 {index} 帧跨格或触边，需要透明留白：{path}");
                    minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);
                }
                if(maxX<minX||transparent/(double)(cell.Width*cell.Height)<.2)throw new InvalidDataException($"人物动作第 {index} 帧为空或有不透明背景：{path}");
                var visible=Rectangle.FromLTRB(minX,minY,maxX+1,maxY+1);
                if(visible.Height<cell.Height*.35||visible.Width<cell.Width*.15)throw new InvalidDataException($"人物动作第 {index} 帧主体过小或缺失：{path}");
                var centers=new List<double>();
                // At the belt/hip band the longest opaque run is the body, rather than a waving hand or gun.
                for(int y=minY+(int)(visible.Height*.49);y<=minY+(int)(visible.Height*.56);y++)
                {
                    int start=-1,bestStart=-1,bestLength=0;
                    for(int x=minX;x<=maxX+1;x++)
                    {
                        var occupied=x<=maxX&&Alpha(x,y)>=128;
                        if(occupied&&start<0)start=x;
                        if(!occupied&&start>=0){if(x-start>bestLength){bestStart=start;bestLength=x-start;}start=-1;}
                    }
                    if(bestLength>=visible.Width*.15)centers.Add(bestStart+bestLength/2.0);
                }
                centers.Sort();var center=centers.Count==0?visible.Left+visible.Width/2.0:centers[centers.Count/2];
                var content=Rectangle.Intersect(cell,Rectangle.FromLTRB(minX-2,minY-2,maxX+3,maxY+3));
                cels.Add(new(cell,content,center,maxY+1));
            }
            Cels=cels;
        }
        catch{bitmap.Dispose();throw;}
    }
    public double ReferenceHeight(int index)=>Cels[index].GroundY-Cels[index].Content.Top;
    public Bitmap Render(int index,double referenceHeight,double canvasDip,double spriteDip,double scale,double lift=0,double roll=0,double stretch=1)
    {
        var cel=Cels[index];var size=(int)Math.Round(canvasDip*scale);var result=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
        try
        {
            using var g=Graphics.FromImage(result);g.Clear(Color.Transparent);g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.ScaleTransform((float)scale,(float)scale);var ground=(float)(canvasDip-38-lift);
            g.TranslateTransform((float)(canvasDip/2),ground);g.RotateTransform((float)roll);g.ScaleTransform(1,(float)stretch);
            var factor=spriteDip/referenceHeight;
            var target=new RectangleF((float)((cel.Content.Left-cel.BodyX)*factor),(float)((cel.Content.Top-cel.GroundY)*factor),(float)(cel.Content.Width*factor),(float)(cel.Content.Height*factor));
            using var attributes=new ImageAttributes();attributes.SetWrapMode(WrapMode.TileFlipXY);
            g.DrawImage(bitmap,Rectangle.Round(target),cel.Content.Left,cel.Content.Top,cel.Content.Width,cel.Content.Height,GraphicsUnit.Pixel,attributes);
            return result;
        }
        catch{result.Dispose();throw;}
    }
    public void Dispose()=>bitmap.Dispose();
}
