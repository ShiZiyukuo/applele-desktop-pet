namespace Applele.Windows.Animation;

internal readonly record struct CachedPose(Bitmap First,Bitmap Second,double Blend)
{
    public void Draw(Bitmap target)=>FrameMixer.Blend(target,First,Second,Blend);
}
