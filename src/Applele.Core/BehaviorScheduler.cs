namespace Applele.Core;

public sealed record BehaviorChoice(BehaviorSpec Spec, double Duration, double Distance);
public sealed record BehaviorStatus(string Id, string Reason, double CooldownSeconds, double WaitSeconds);

/// <summary>Decides only when explicitly asked by an idle actor. No timers or callbacks own actor state.</summary>
public sealed class BehaviorScheduler
{
    readonly Random random;
    readonly Dictionary<string, double> readyAt = new(), cooldownUntil = new();
    readonly Queue<string> history = new();
    double nextDecision, specialUntil;
    public BehaviorProfile Profile { get; }
    public string LastDecision { get; private set; } = "初始化随机等待";
    public IEnumerable<string> History => history;
    public BehaviorScheduler(BehaviorProfile profile, double now, Random random)
    {
        profile.Validate(); Profile = profile; this.random = random;
        foreach (var spec in profile.Behaviors) cooldownUntil[spec.Id] = spec.StartupCooldown ? now + Range(spec.CooldownMin, spec.CooldownMax) : now;
        IdleFrom(now);
    }
    double Range(double min, double max) => min + random.NextDouble() * (max - min);
    void Record(string reason)
    {
        LastDecision = reason; history.Enqueue(reason); while (history.Count > 12) history.Dequeue();
    }
    public double NextDecisionSeconds(double now) => Math.Max(0, nextDecision - now);
    public void IdleFrom(double now)
    {
        nextDecision = now + Range(Profile.DecisionWaitMin, Profile.DecisionWaitMax);
        foreach (var spec in Profile.Behaviors) readyAt[spec.Id] = now + Range(spec.WaitMin, spec.WaitMax);
    }
    public BehaviorStatus[] Status(double now, PetState state) => Profile.Behaviors.Select(spec =>
    {
        var cooldown = Math.Max(0, cooldownUntil[spec.Id] - now);
        if (spec.IsSpecial) cooldown = Math.Max(cooldown, specialUntil - now);
        var wait = Math.Max(0, readyAt[spec.Id] - now);
        var reason = !spec.AllowedStates.Contains(state) ? "当前状态不允许" : cooldown > 0 ? "冷却中" : wait > 0 ? "最短等待未到" : "可参加加权决策";
        return new BehaviorStatus(spec.Id, reason, Math.Max(0,cooldown), wait);
    }).ToArray();
    public BehaviorChoice? Decide(double now, PetState state,bool specialsOnly=false)
    {
        if (now < nextDecision) return null;
        var statuses = Status(now, state);
        var eligible = Profile.Behaviors.Where(spec => (!specialsOnly||spec.IsSpecial)&&statuses.Any(x => x.Id == spec.Id && x.Reason == "可参加加权决策")).ToArray();
        var roll = random.NextDouble() * (Profile.RestWeight + eligible.Sum(x => x.Weight));
        if (roll < Profile.RestWeight)
        {
            Record(eligible.Length == 0 ? "所有动作仍在等待或冷却，继续休息" : "本次权重选择安静待机"); IdleFrom(now); return null;
        }
        roll -= Profile.RestWeight;
        foreach (var spec in eligible)
        {
            roll -= spec.Weight;
            if (roll <= 0) { Record("权重选择 " + spec.Id); return Choose(spec); }
        }
        IdleFrom(now); return null;
    }
    public BehaviorChoice Choose(BehaviorSpec spec) => new(spec, Range(spec.DurationMin, spec.DurationMax), Range(spec.DistanceMin, spec.DistanceMax));
    public void End(BehaviorSpec spec, double now, bool cancelled)
    {
        cooldownUntil[spec.Id] = now + Range(spec.CooldownMin, spec.CooldownMax);
        if (spec.IsSpecial) specialUntil = now + Profile.SpecialSpacingSeconds;
        Record(spec.Id + (cancelled ? " 被中断，仍消耗冷却" : " 已结束")); IdleFrom(now);
    }
}
