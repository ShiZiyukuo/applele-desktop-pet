using Applele.Core;
using Applele.Windows.Assets;

namespace Applele.Windows.Animation;

/// <summary>Only the active portrait is cached. Every clip is derived from that form's own source and settings.</summary>
internal sealed class PortraitRigCache : IDisposable
{
    readonly Dictionary<PortraitClip,Bitmap[]> clips=new();
    readonly CharacterFrameSet? artwork;
    public string FormId {get;}
    public PortraitRigCache(FormDefinition form,ResourceCatalog catalog,double canvas,double scale)
    {
        FormId=form.Id;artwork=form.Frames;
        try
        {
            if(artwork!=null){BuildArtwork(form,catalog,canvas,scale);return;}
            var source=catalog.Get(form.Idle);
            foreach(var clip in Enum.GetValues<PortraitClip>())
            {
                int count=clip==PortraitClip.Idle?12:clip==PortraitClip.Walk?24:16;
                var frames=new Bitmap[count];clips.Add(clip,frames);
                for(int i=0;i<count;i++)
                {
                    var phase=i/(double)(clip is PortraitClip.ReactionA or PortraitClip.ReactionB?count-1:count);
                    frames[i]=MeshFrameFactory.Create(source,canvas,form.Animation.SpriteSizeDip,scale,(u,v,w,h)=>PortraitMotion.Deform(form.Animation,form.Reaction,clip,phase,u,v,w,h),6,12);
                }
            }
        }
        catch{Dispose();throw;}
    }
    void BuildArtwork(FormDefinition form,ResourceCatalog catalog,double canvas,double scale)
    {
        var settings=artwork!;
        using var actions=new FrameAtlas(catalog.Resolve(settings.Actions.Texture),settings.Actions);
        using var walk=new FrameAtlas(catalog.Resolve(settings.Walk.Texture),settings.Walk);
        using var continuation=settings.WalkContinuation==null?null:new FrameAtlas(catalog.Resolve(settings.WalkContinuation.Texture),settings.WalkContinuation);
        var height=actions.ReferenceHeight(settings.NeutralFrame);
        var idle=new Bitmap[12];clips.Add(PortraitClip.Idle,idle);
        for(int i=0;i<idle.Length;i++)
        {
            var phase=i/(double)idle.Length*Math.PI*2;
            idle[i]=actions.Render(settings.NeutralFrame,height,canvas,form.Animation.SpriteSizeDip,scale,
                form.Animation.IdleLift*(1-Math.Cos(phase))*.5,form.Animation.IdleRoll*Math.Sin(phase),1+.003*Math.Sin(phase));
        }
        void Build(PortraitClip clip,FrameAtlas atlas,int[] indices,double reference)
        {
            var frames=new Bitmap[indices.Length];clips.Add(clip,frames);
            for(int i=0;i<frames.Length;i++)frames[i]=atlas.Render(indices[i],reference,canvas,form.Animation.SpriteSizeDip,scale);
        }
        var walkHeights=Enumerable.Range(0,walk.Cels.Count).Select(walk.ReferenceHeight).Order().ToArray();
        var nextHeights=continuation==null?[]:Enumerable.Range(0,continuation.Cels.Count).Select(continuation.ReferenceHeight).Order().ToArray();
        var stepping=new Bitmap[settings.WalkOrder.Length];clips.Add(PortraitClip.Walk,stepping);
        for(int i=0;i<stepping.Length;i++)
        {
            var index=settings.WalkOrder[i];var atlas=index<walk.Cels.Count?walk:continuation!;
            var local=index<walk.Cels.Count?index:index-walk.Cels.Count;var heights=index<walk.Cels.Count?walkHeights:nextHeights;
            stepping[i]=atlas.Render(local,heights[heights.Length/2],canvas,form.Animation.SpriteSizeDip,scale);
        }
        Build(PortraitClip.ReactionA,actions,settings.ReactionA,height);Build(PortraitClip.ReactionB,actions,settings.ReactionB,height);
    }
    public CachedPose Sample(PortraitClip clip,double phase)
    {
        var interpolate=artwork==null||clip==PortraitClip.Idle||(clip==PortraitClip.Walk?artwork.BlendWalkFrames:artwork.BlendReactionFrames);
        var frames=clips[clip];var sample=AnimationSampling.Sample(phase,frames.Length,clip is PortraitClip.Idle or PortraitClip.Walk,interpolate);
        return new(frames[sample.First],frames[sample.Second],sample.Blend);
    }
    public void Dispose(){foreach(var frames in clips.Values)foreach(var frame in frames)frame?.Dispose();clips.Clear();}
}
