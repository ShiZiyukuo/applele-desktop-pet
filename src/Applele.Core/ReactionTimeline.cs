namespace Applele.Core;

public readonly record struct ReactionPose(double Mix, double Lift, double Rotation, double ScaleX, double ScaleY);

/// <summary>Three authored timing phases. Static reference sprites are crossfaded; these are procedural poses, not drawn animation frames.</summary>
public static class ReactionTimeline
{
    static double Ease(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    public static ReactionPose Sample(ReactionSettings settings, double age, int variant)
    {
        var a = settings.AnticipationSeconds; var b = settings.ExpressionSeconds; var c = settings.RecoverySeconds;
        if (age < 0 || age >= a + b + c) return new(0, 0, 0, 1, 1);
        if (age < a)
        {
            var windup = Math.Sin(Math.PI * age / a);
            return new(0, -2 * windup, 0, 1 + .055 * windup, 1 - .06 * windup);
        }
        if (age < a + b)
        {
            var t = (age - a) / b; var fade = Ease(t / .18); var envelope = Math.Sin(Math.PI * t);
            return variant == 0
                ? new(fade, 16 * envelope, Math.Sin(t * Math.PI * 4) * 10 * envelope, 1 + .035 * envelope, 1 - .025 * envelope)
                : new(fade, 5 * envelope, -6 * envelope, 1 - .018 * envelope, 1 + .025 * envelope);
        }
        var recovery = Ease((age - a - b) / c);
        return new(1 - recovery, 0, 0, 1, 1);
    }
}
