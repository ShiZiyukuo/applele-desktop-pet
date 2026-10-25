namespace Applele.Core;

public readonly record struct SpecialPose(double ShiftX,double Lift,double Roll,double ScaleX,double ScaleY);

/// <summary>Small procedural gestures of the original plush silhouette, without extra artwork.</summary>
public static class SpecialMotion
{
    public static SpecialPose Sample(BehaviorKind kind,double age,double duration)
    {
        if(!double.IsFinite(age)||!double.IsFinite(duration)||age<0||duration<=0)throw new ArgumentOutOfRangeException(nameof(age));
        var p=Math.Clamp(age/duration,0,1);var envelope=Gait.Envelope(age,duration);
        if(kind==BehaviorKind.Shake)
        {
            var wave=Math.Sin(p*Math.PI*10)*envelope;
            return new(wave*4,Math.Abs(wave)*2,wave*8,1+.025*wave,1-.025*wave);
        }
        if(kind==BehaviorKind.Hop)
        {
            var hop=Math.Abs(Math.Sin(p*Math.PI*2));
            return new(0,hop*25*envelope,Math.Sin(p*Math.PI*4)*5*envelope,1+.06*(1-hop)*envelope,1-.10*(1-hop)*envelope);
        }
        return new(0,0,0,1,1);
    }
}
