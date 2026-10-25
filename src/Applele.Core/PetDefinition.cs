using System.Text.Json;

namespace Applele.Core;

public enum PetState { Idle, Walking, Reacting, Dragging, Paused, Special }
public enum ReactionPhase { None, Anticipation, Expression, Recovery }

public sealed record ReactionSettings
{
    public double AnticipationSeconds { get; init; } = .16;
    public double ExpressionSeconds { get; init; } = .85;
    public double RecoverySeconds { get; init; } = .32;
    public double CooldownSeconds { get; init; } = .30;
    public double Duration => AnticipationSeconds + ExpressionSeconds + RecoverySeconds;
    public void Validate()
    {
        double[] values = [AnticipationSeconds, ExpressionSeconds, RecoverySeconds, CooldownSeconds];
        if (values.Any(x => !double.IsFinite(x) || x < 0) || AnticipationSeconds < .05 || ExpressionSeconds < .1 || RecoverySeconds < .05 || Duration > 10)
            throw new InvalidDataException("点击动作时间配置不合法。");
    }
}

public sealed record SpriteDefinition
{
    public string File { get; init; } = "";
    public string Provenance { get; init; } = "ai-extracted-static-reference";
}

public sealed record FormDefinition
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public bool InIllustrationCycle { get; init; }
    public string BehaviorProfileId { get; init; } = "playful-plush";
    public FormAnimationSettings Animation { get; init; } = new();
    public CharacterFrameSet? Frames {get;init;}
    public SpriteDefinition Idle { get; init; } = new();
    public SpriteDefinition[] Reactions { get; init; } = [];
    public ReactionSettings Reaction { get; init; } = new();
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id) || string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Idle.File) ||
            (Animation.Rig=="plush"?Reactions.Length!=2:Reactions.Length is not (0 or 2)) || Reactions.Any(x => string.IsNullOrWhiteSpace(x.File)))
            throw new InvalidDataException("形态需要名称和默认图片；玩偶还需要两张反应图，立绘可从单张图生成程序反馈。");
        Reaction.Validate();
        Animation.Validate();
        Frames?.Validate();
        if(Frames!=null&&Animation.Rig!="portrait")throw new InvalidDataException("人物关键帧只能用于人物形态。");
    }
}

public sealed record PetDefinition
{
    public NavigationSettings Roaming { get; init; } = new();
    public string InitialForm { get; init; } = "plush";
    public double CanvasSizeDip { get; init; } = 320;
    public double SpriteSizeDip { get; init; } = 232;
    public int IdleFps { get; init; } = 15;
    public int ActionFps { get; init; } = 30;
    public int HitAlphaThreshold { get; init; } = 20;
    public FormDefinition[] Forms { get; init; } = [];
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };
    public static PetDefinition Load(string path)
    {
        var data = JsonSerializer.Deserialize<PetDefinition>(File.ReadAllText(path), JsonOptions) ?? throw new InvalidDataException("配置为空。");
        data.Validate(); return data;
    }
    public void Validate()
    {
        Roaming.Validate();
        if (!double.IsFinite(CanvasSizeDip) || !double.IsFinite(SpriteSizeDip) || CanvasSizeDip < 160 || CanvasSizeDip > 800 || SpriteSizeDip < 80 || SpriteSizeDip > CanvasSizeDip - 40 || IdleFps < 1 || IdleFps > 30 || ActionFps < 10 || ActionFps > 60 || HitAlphaThreshold < 1 || HitAlphaThreshold > 254)
            throw new InvalidDataException("窗口/帧率/命中配置无效。");
        foreach (var form in Forms) form.Validate();
        if (Forms.Length == 0 || Forms.Select(x => x.Id).Distinct().Count() != Forms.Length || !Forms.Any(x => x.Id == InitialForm))
            throw new InvalidDataException("形态 ID 必须唯一，初始形态必须存在。");
    }
}
