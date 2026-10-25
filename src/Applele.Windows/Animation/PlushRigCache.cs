using System.Drawing.Imaging;
using Applele.Core;
using Applele.Windows.Assets;

namespace Applele.Windows.Animation;

/// <summary>Cached procedural mesh animation of the reference texture. Not artist-authored frames.</summary>
internal sealed class PlushRigCache : IDisposable
{
    readonly Bitmap[] frames;
    public PlushRigCache(SpriteAsset source, double canvasDip, double spriteDip, double scale)
    {
        frames=new Bitmap[Gait.FrameCount];
        try {for(int i=0;i<frames.Length;i++)frames[i]=Build(source,canvasDip,spriteDip,scale,i/(double)frames.Length);}
        catch {Dispose();throw;}
    }
    public CachedPose Sample(double phase)
    {
        var sample=AnimationSampling.Sample(phase,frames.Length,true);
        return new(frames[sample.First],frames[sample.Second],sample.Blend);
    }
    public void Export(string directory)
    {
        Directory.CreateDirectory(directory);
        for(int i=0;i<frames.Length;i++)frames[i].Save(Path.Combine(directory,$"walk-{i:00}.png"),ImageFormat.Png);
    }
    static Bitmap Build(SpriteAsset source,double canvasDip,double spriteDip,double scale,double phase)
        =>MeshFrameFactory.Create(source,canvasDip,spriteDip,scale,(u,v,w,h)=>Gait.Deform(u,v,w,h,phase));
    public void Dispose(){foreach(var frame in frames)frame?.Dispose();}
}
