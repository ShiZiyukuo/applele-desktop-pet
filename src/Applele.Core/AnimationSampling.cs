namespace Applele.Core;

/// <summary>Samples adjacent cached poses continuously, including the loop seam.</summary>
public readonly record struct FrameSample(int First,int Second,double Blend);

public static class AnimationSampling
{
    public static FrameSample Sample(double phase,int count,bool loop,bool interpolate=true)
    {
        if(!double.IsFinite(phase))throw new ArgumentOutOfRangeException(nameof(phase));
        if(count<2)throw new ArgumentOutOfRangeException(nameof(count));
        if(!interpolate)
        {
            var held=(int)Math.Floor((loop?phase-Math.Floor(phase):Math.Clamp(phase,0,1))*count);
            held=Math.Min(held,count-1);return new(held,held,0);
        }
        var position=loop?(phase-Math.Floor(phase))*count:Math.Clamp(phase,0,1)*(count-1);
        var first=Math.Min((int)Math.Floor(position),count-1);
        var second=loop?(first+1)%count:Math.Min(first+1,count-1);
        return new(first,second,Math.Clamp(position-first,0,1));
    }
}
