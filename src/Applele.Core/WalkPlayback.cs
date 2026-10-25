namespace Applele.Core;

public sealed record WalkPlaybackSettings
{
    public double CycleDistanceDip {get;init;}=84;
    public int SourceFacing {get;init;}=1;
    public bool FitWholeSteps {get;init;}=true;
    public void Validate()
    {
        if(!double.IsFinite(CycleDistanceDip)||CycleDistanceDip<24||CycleDistanceDip>180||SourceFacing is not (-1 or 1))
            throw new InvalidDataException("距离步态的周期距离或素材朝向无效。");
    }
}

/// <summary>Root displacement and playback share one travel curve; no wall-clock gait while blocked.</summary>
public static class WalkPlayback
{
    public static double CruiseProgress(double age,double duration,double acceleration)
    {
        if(!double.IsFinite(age)||!double.IsFinite(duration)||!double.IsFinite(acceleration)||duration<=0||acceleration<=0)
            throw new ArgumentOutOfRangeException(nameof(duration));
        age=Math.Clamp(age,0,duration);var ramp=Math.Min(acceleration,duration*.25);var area=duration-ramp;
        if(age<ramp)return .5*age*age/ramp/area;
        if(age>duration-ramp){var remaining=duration-age;return 1-.5*remaining*remaining/ramp/area;}
        return (age-ramp*.5)/area;
    }
    public static double EffectiveCycleDistance(double legDistance,WalkPlaybackSettings settings)
    {
        if(!double.IsFinite(legDistance)||legDistance<0)throw new ArgumentOutOfRangeException(nameof(legDistance));
        if(!settings.FitWholeSteps||legDistance<1)return settings.CycleDistanceDip;
        // Each half cycle ends at the opposite contact pose. Finish on contact, not mid-swing.
        var halfSteps=Math.Max(1,Math.Round(legDistance/(settings.CycleDistanceDip*.5)));
        return legDistance*2/halfSteps;
    }
}
