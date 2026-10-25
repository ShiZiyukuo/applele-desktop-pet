namespace Applele.Core;

public enum PortraitClip { Idle, Walk, ReactionA, ReactionB }
public sealed record LocalMotionAnchor
{
    public string Label { get; init; } = "";
    public double X { get; init; }=.5;
    public double Y { get; init; }=.5;
    public double RadiusX { get; init; }=.25;
    public double RadiusY { get; init; }=.25;
    public double PhaseOffset { get; init; }
    public double IdleX { get; init; }
    public double IdleY { get; init; }
    public double WalkX { get; init; }
    public double WalkY { get; init; }
    public double ReactionAX { get; init; }
    public double ReactionAY { get; init; }
    public double ReactionBX { get; init; }
    public double ReactionBY { get; init; }
    public void Validate()
    {
        if(new[]{X,Y,RadiusX,RadiusY,PhaseOffset,IdleX,IdleY,WalkX,WalkY,ReactionAX,ReactionAY,ReactionBX,ReactionBY}.Any(x=>!double.IsFinite(x)) || X<0 || X>1 || Y<0 || Y>1 || RadiusX<=0 || RadiusY<=0 || RadiusX>1 || RadiusY>1 || new[]{IdleX,IdleY,WalkX,WalkY,ReactionAX,ReactionAY,ReactionBX,ReactionBY}.Any(x=>Math.Abs(x)>16))
            throw new InvalidDataException("局部动画锚点配置无效："+Label);
    }
}
public sealed record FormAnimationSettings
{
    public string Rig { get; init; }="plush";
    public string StyleId { get; init; }="plush-softmesh";
    public string ReactionAName { get; init; }="惊讶弹跳";
    public string ReactionBName { get; init; }="高雅举杯";
    public double SpriteSizeDip { get; init; }=232;
    public double IdleCycleSeconds { get; init; }=3.2;
    public double WalkCycleSeconds { get; init; }=1;
    public double IdleLift { get; init; }=.8;
    public double IdleRoll { get; init; }=.6;
    public double WalkLift { get; init; }=2;
    public double WalkRoll { get; init; }=1;
    public double ReactionALift { get; init; }=4;
    public double ReactionARoll { get; init; }=2;
    public double ReactionBLift { get; init; }=2;
    public double ReactionBRoll { get; init; }=-2;
    public LocalMotionAnchor[] Anchors { get; init; }=[];
    public void Validate()
    {
        if(Rig is not ("plush" or "portrait") || string.IsNullOrWhiteSpace(StyleId) ||
            new[]{SpriteSizeDip,IdleCycleSeconds,WalkCycleSeconds,IdleLift,IdleRoll,WalkLift,WalkRoll,ReactionALift,ReactionARoll,ReactionBLift,ReactionBRoll}.Any(x=>!double.IsFinite(x)) ||
            SpriteSizeDip<80 || SpriteSizeDip>260 || IdleCycleSeconds<1 || IdleCycleSeconds>15 || WalkCycleSeconds<.5 || WalkCycleSeconds>3 ||
            new[]{IdleLift,IdleRoll,WalkLift,WalkRoll,ReactionALift,ReactionARoll,ReactionBLift,ReactionBRoll}.Any(x=>Math.Abs(x)>12) || Anchors.Length>8)
            throw new InvalidDataException("形态动画配置无效："+StyleId);
        foreach(var anchor in Anchors)anchor.Validate();
    }
}
public static class PortraitMotion
{
    public static RigPoint Deform(FormAnimationSettings config,ReactionSettings timing,PortraitClip clip,double phase,double u,double v,double width,double height)
    {
        double strength,roll,lift;
        if(clip is PortraitClip.ReactionA or PortraitClip.ReactionB)
        {
            var age=Math.Clamp(phase,0,1)*timing.Duration;
            if(age<timing.AnticipationSeconds)strength=-.12*Math.Pow(Math.Sin(Math.PI*age/timing.AnticipationSeconds),2);
            else if(age<timing.AnticipationSeconds+timing.ExpressionSeconds)strength=Math.Pow(Math.Sin(Math.PI*(age-timing.AnticipationSeconds)/timing.ExpressionSeconds),2);
            else strength=0;
            roll=(clip==PortraitClip.ReactionA?config.ReactionARoll:config.ReactionBRoll)*strength;
            lift=(clip==PortraitClip.ReactionA?config.ReactionALift:config.ReactionBLift)*strength;
        }
        else
        {
            strength=Math.Sin(phase*Math.PI*2);
            roll=(clip==PortraitClip.Idle?config.IdleRoll:config.WalkRoll)*strength;
            lift=(clip==PortraitClip.Idle?config.IdleLift:config.WalkLift)*(clip==PortraitClip.Idle?.5*(1-Math.Cos(phase*Math.PI*2)):Math.Abs(strength));
        }
        var x=(u-.5)*width;var y=(v-1)*height;var pivot=-height*.42;var angle=roll*Math.PI/180;
        var resultX=x*Math.Cos(angle)-(y-pivot)*Math.Sin(angle);
        var resultY=x*Math.Sin(angle)+(y-pivot)*Math.Cos(angle)+pivot-lift;
        foreach(var anchor in config.Anchors)
        {
            var du=(u-anchor.X)/anchor.RadiusX;var dv=(v-anchor.Y)/anchor.RadiusY;var weight=Math.Pow(Math.Max(0,1-du*du-dv*dv),2);
            var wave=clip is PortraitClip.Idle or PortraitClip.Walk?Math.Sin(phase*Math.PI*2+anchor.PhaseOffset):strength;
            var dx=clip switch{PortraitClip.Idle=>anchor.IdleX,PortraitClip.Walk=>anchor.WalkX,PortraitClip.ReactionA=>anchor.ReactionAX,_=>anchor.ReactionBX};
            var dy=clip switch{PortraitClip.Idle=>anchor.IdleY,PortraitClip.Walk=>anchor.WalkY,PortraitClip.ReactionA=>anchor.ReactionAY,_=>anchor.ReactionBY};
            resultX+=weight*dx*wave;resultY+=weight*dy*wave;
        }
        return new(resultX,resultY);
    }
}
