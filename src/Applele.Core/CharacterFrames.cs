using System.Text.Json.Serialization;

namespace Applele.Core;

public sealed record SpriteAtlasDefinition
{
    public SpriteDefinition Texture {get;init;}=new();
    public int Columns {get;init;}=4;
    public int Rows {get;init;}=2;
    public bool UseTransparentGutters {get;init;}
    [JsonIgnore]public int Count=>Columns*Rows;
    public void Validate()
    {
        if(string.IsNullOrWhiteSpace(Texture.File)||Columns<1||Columns>16||Rows<1||Rows>16||Count>128)
            throw new InvalidDataException("人物动作精灵图配置无效。");
    }
}

/// <summary>Actual authored image cels. Their AI provenance and inferred costume portions stay explicit.</summary>
public sealed record CharacterFrameSet
{
    public string Provenance {get;init;}="AI-redrawn key cels, not official or hand-animated footage";
    public SpriteAtlasDefinition Actions {get;init;}=new();
    public SpriteAtlasDefinition Walk {get;init;}=new();
    public SpriteAtlasDefinition? WalkContinuation {get;init;}
    public WalkPlaybackSettings? Playback {get;init;}
    public int NeutralFrame {get;init;}
    public int[] WalkOrder {get;init;}=[0,1,2,3,4,5,6,7];
    public int[] ReactionA {get;init;}=[0,1,2,3];
    public int[] ReactionB {get;init;}=[4,5,6,7];
    public bool BlendWalkFrames {get;init;}
    public bool BlendReactionFrames {get;init;}
    public void Validate()
    {
        Actions.Validate();Walk.Validate();WalkContinuation?.Validate();Playback?.Validate();
        if(string.IsNullOrWhiteSpace(Provenance)||NeutralFrame<0||NeutralFrame>=Actions.Count)throw new InvalidDataException("人物动作来源或中立帧无效。");
        void Clip(int[] frames,int count,int minimum)
        {
            if(frames.Length<minimum||frames.Length>64||frames.Any(x=>x<0||x>=count)||frames.Distinct().Count()<minimum)
                throw new InvalidDataException("人物动作帧索引不足、重复或越界。");
        }
        Clip(WalkOrder,Walk.Count+(WalkContinuation?.Count??0),6);Clip(ReactionA,Actions.Count,3);Clip(ReactionB,Actions.Count,3);
        if(ReactionA.SequenceEqual(ReactionB))throw new InvalidDataException("人物两种点击动作不能是同一序列。");
    }
}
