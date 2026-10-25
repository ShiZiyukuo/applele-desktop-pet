namespace Applele.Core;

public readonly record struct RigPoint(double X, double Y);
public readonly record struct GaitPose(double LeftFootLift, double RightFootLift, double BodyLift, double Roll, double Squash, double SideShift);
public static class Gait
{
    public const int FrameCount = 24;
    public const double CycleSeconds = .8;
    public static double Smooth(double x) { x=Math.Clamp(x,0,1);return x*x*(3-2*x); }
    public static GaitPose Sample(double phase)
    {
        var p = phase * Math.PI * 2; var swing = Math.Sin(p); var bounce = Math.Pow(Math.Sin(p),2);
        return new(Math.Max(0,swing)*13,Math.Max(0,-swing)*13,bounce*9,swing*6,1-.045*Math.Pow(Math.Cos(p),8),swing*3);
    }
    public static RigPoint Deform(double u, double v, double width, double height, double phase, double amplitude=1)
    {
        var pose = Sample(phase); var x=(u-.5)*width;var y=(v-1)*height;
        var angle=pose.Roll*Math.PI/180*amplitude; var pivotY=-height*.3;
        var bodyX=x*Math.Cos(angle)-(y-pivotY)*Math.Sin(angle)+pose.SideShift*amplitude;
        var bodyY=x*Math.Sin(angle)+(y-pivotY)*Math.Cos(angle)*(1+(pose.Squash-1)*amplitude)+pivotY-pose.BodyLift*amplitude;
        double Influence(double cx,double cy) => Math.Pow(Math.Max(0,1-Math.Pow((u-cx)/.20,2)-Math.Pow((v-cy)/.15,2)),2);
        var left=Influence(.35,.95);var right=Influence(.78,.91);var weight=Math.Clamp(left+right,0,1);
        var footLift=left*pose.LeftFootLift+right*pose.RightFootLift;
        var footX=x+Math.Sin(phase*Math.PI*2)*(left-right)*5*amplitude;
        return new(bodyX*(1-weight)+footX*weight,bodyY*(1-weight)+y*weight-footLift*amplitude);
    }
    public static double Envelope(double age,double duration) => Smooth(age/.18)*Smooth((duration-age)/.28);
    public static double Travel(double progress) { progress=Math.Clamp(progress,0,1); return progress*progress*progress*(progress*(progress*6-15)+10); }
}
