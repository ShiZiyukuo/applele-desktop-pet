using System.Text.Json;
using System.Text.Json.Serialization;

namespace Applele.Core;

public enum BehaviorKind { Walk, Hop, Dash, Shake }
public sealed record BehaviorSpec
{
    public string Id { get; init; } = "";
    public BehaviorKind Kind { get; init; }
    [JsonIgnore] public bool HasTranslation=>Kind is BehaviorKind.Walk or BehaviorKind.Dash;
    [JsonIgnore] public bool IsSpecial=>Kind!=BehaviorKind.Walk;
    public double Weight { get; init; } = 1;
    public double WaitMin { get; init; } = 18;
    public double WaitMax { get; init; } = 40;
    public double DurationMin { get; init; } = 2;
    public double DurationMax { get; init; } = 3;
    public double CooldownMin { get; init; } = 30;
    public double CooldownMax { get; init; } = 60;
    public double DistanceMin { get; init; } = 25;
    public double DistanceMax { get; init; } = 75;
    public bool StartupCooldown { get; init; }
    public PetState[] AllowedStates { get; init; } = [PetState.Idle];
    public void Validate()
    {
        double[] numbers = [Weight, WaitMin, WaitMax, DurationMin, DurationMax, CooldownMin, CooldownMax, DistanceMin, DistanceMax];
        if (string.IsNullOrWhiteSpace(Id) || !Enum.IsDefined(Kind) || numbers.Any(x => !double.IsFinite(x) || x < 0) ||
            Weight <= 0 || WaitMin < .1 || WaitMax < WaitMin || DurationMin < .4 || DurationMax < DurationMin || DurationMax > 12 ||
            CooldownMax < CooldownMin || DistanceMax < DistanceMin || DistanceMax > 250 || AllowedStates.Length == 0 || AllowedStates.Any(x => !Enum.IsDefined(x)))
            throw new InvalidDataException("行为配置不合法：" + Id);
    }
}
public sealed record BehaviorProfile
{
    public string Id { get; init; } = "";
    public double DecisionWaitMin { get; init; } = 18;
    public double DecisionWaitMax { get; init; } = 45;
    public double RestWeight { get; init; } = 78;
    public double SpecialSpacingSeconds { get; init; } = 120;
    public double EdgePreference { get; init; } = .25;
    public BehaviorSpec[] Behaviors { get; init; } = [];
    public void Validate()
    {
        double[] numbers = [DecisionWaitMin, DecisionWaitMax, RestWeight, SpecialSpacingSeconds, EdgePreference];
        if (string.IsNullOrWhiteSpace(Id) || numbers.Any(x => !double.IsFinite(x) || x < 0) || DecisionWaitMin < .1 || DecisionWaitMax < DecisionWaitMin || DecisionWaitMax > 86400 || RestWeight <= 0 || EdgePreference > 1 || Behaviors.Length == 0 || Behaviors.Select(x => x.Id).Distinct().Count() != Behaviors.Length)
            throw new InvalidDataException("性格配置不合法：" + Id);
        foreach (var behavior in Behaviors) behavior.Validate();
    }
}
public sealed record BehaviorCatalog
{
    public BehaviorProfile[] Profiles { get; init; } = [];
    public static readonly JsonSerializerOptions JsonOptions = new(PetDefinition.JsonOptions) { Converters = { new JsonStringEnumConverter() } };
    public static BehaviorCatalog Load(string path)
    {
        var catalog = JsonSerializer.Deserialize<BehaviorCatalog>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("性格配置为空。");
        catalog.Validate(); return catalog;
    }
    public void Validate()
    {
        if (Profiles.Length == 0 || Profiles.Select(x => x.Id).Distinct().Count() != Profiles.Length) throw new InvalidDataException("性格 ID 必须唯一。");
        foreach (var profile in Profiles) profile.Validate();
    }
    public BehaviorProfile Get(string id) => Profiles.SingleOrDefault(x => x.Id == id) ?? throw new InvalidDataException("找不到性格配置：" + id);
}
