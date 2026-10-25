namespace Applele.Core;

public sealed record NavigationSettings
{
    public double SpeedDipPerSecond { get; init; } = 95;
    public double PauseMinSeconds { get; init; } = 1.2;
    public double PauseMaxSeconds { get; init; } = 3;
    public double EdgeInsetDip { get; init; } = 10;
    public double MaxLegSeconds { get; init; } = 45;
    public bool AllowSpecialDuringRoaming { get; init; }=true;
    public double AccelerationSeconds {get;init;}=.28;
    public void Validate()
    {
        double[] values=[SpeedDipPerSecond,PauseMinSeconds,PauseMaxSeconds,EdgeInsetDip,MaxLegSeconds,AccelerationSeconds];
        if(values.Any(x=>!double.IsFinite(x)) || SpeedDipPerSecond<20 || SpeedDipPerSecond>240 || PauseMinSeconds<.3 || PauseMaxSeconds<PauseMinSeconds || PauseMaxSeconds>30 || EdgeInsetDip<0 || EdgeInsetDip>100 || MaxLegSeconds<1 || MaxLegSeconds>120 || AccelerationSeconds<.05||AccelerationSeconds>1)
            throw new InvalidDataException("满屏溜达的速度、等待或边界配置不合法。");
    }
}
public readonly record struct MovementSpace(double Left,double Right,double Up,double Down)
{
    public void Validate()
    {
        if(new[]{Left,Right,Up,Down}.Any(x=>!double.IsFinite(x)||x<0))throw new ArgumentOutOfRangeException(nameof(Left));
    }
    public (double X,double Y) Clamp(double x,double y)=>(Math.Clamp(x,-Left,Right),Math.Clamp(y,-Up,Down));
}
public readonly record struct NavigationLeg(double X,double Y,double Duration,int Quadrant);

/// <summary>Visits shuffled screen quadrants rather than bouncing on one horizontal line.</summary>
public sealed class ScreenNavigator
{
    readonly Random random;
    readonly Queue<int> quadrants=new();
    readonly NavigationSettings settings;
    int previous=-1;
    public ScreenNavigator(NavigationSettings settings,Random random){settings.Validate();this.settings=settings;this.random=random;}
    public double RestSeconds()=>settings.PauseMinSeconds+random.NextDouble()*(settings.PauseMaxSeconds-settings.PauseMinSeconds);
    int NextQuadrant()
    {
        if(quadrants.Count==0)
        {
            int[] order=[0,1,2,3];for(int i=order.Length-1;i>0;i--){int j=random.Next(i+1);(order[i],order[j])=(order[j],order[i]);}
            if(order[0]==previous)(order[0],order[1])=(order[1],order[0]);
            foreach(int q in order)quadrants.Enqueue(q);
        }
        previous=quadrants.Dequeue();return previous;
    }
    public NavigationLeg? Plan(MovementSpace space)
    {
        space.Validate();var width=space.Left+space.Right;var height=space.Up+space.Down;
        if(width<2 && height<2)return null;
        var insetX=Math.Min(settings.EdgeInsetDip,width*.1);var insetY=Math.Min(settings.EdgeInsetDip,height*.1);
        var left=-space.Left+insetX;var right=space.Right-insetX;var top=-space.Up+insetY;var bottom=space.Down-insetY;
        var bandX=(right-left)/3;var bandY=(bottom-top)/3;var quadrant=NextQuadrant();
        var x0=(quadrant&1)==0?left:right-bandX;var x1=(quadrant&1)==0?left+bandX:right;
        var y0=(quadrant&2)==0?top:bottom-bandY;var y1=(quadrant&2)==0?top+bandY:bottom;
        var minDistance=Math.Sqrt(width*width+height*height)*.24;
        double x=0,y=0,length=0;
        for(int attempt=0;attempt<12;attempt++)
        {
            x=x0+random.NextDouble()*(x1-x0);y=y0+random.NextDouble()*(y1-y0);length=Math.Sqrt(x*x+y*y);
            if(length>=minDistance)break;
        }
        if(length<minDistance)
        {
            x=Math.Abs(x0)>Math.Abs(x1)?x0:x1;y=Math.Abs(y0)>Math.Abs(y1)?y0:y1;length=Math.Sqrt(x*x+y*y);
        }
        if(length<1)return null;
        var duration=Math.Max(.8,length/settings.SpeedDipPerSecond+settings.AccelerationSeconds);
        if(duration>settings.MaxLegSeconds){var factor=settings.MaxLegSeconds/duration;x*=factor;y*=factor;duration=settings.MaxLegSeconds;}
        return new NavigationLeg(x,y,duration,quadrant);
    }
}
