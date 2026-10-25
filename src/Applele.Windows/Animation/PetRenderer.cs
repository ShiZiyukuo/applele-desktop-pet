using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Diagnostics;
using Applele.Core;
using Applele.Windows.Assets;
using Applele.Windows.Windowing;

namespace Applele.Windows.Animation;

internal sealed class PetRenderer : IDisposable
{
    readonly ResourceCatalog assets;
    PlushRigCache rig;
    PortraitRigCache? portrait;
    Bitmap scratch;
    int lastAction=-1;
    byte[]? transition;
    double transitionStarted;
    bool portraitMirror;
    public LayeredSurface Surface { get; private set; }
    public double Scale { get; private set; }
    public double LastPreparationMilliseconds {get;private set;}
    public PetRenderer(ResourceCatalog assets, double scale)
    {
        this.assets = assets; Scale = scale; Surface = new LayeredSurface((int)Math.Round(assets.Definition.CanvasSizeDip * scale));
        scratch=new Bitmap(Surface.Width,Surface.Width,PixelFormat.Format32bppPArgb);
        rig=BuildRig();
    }
    PlushRigCache BuildRig()=>new(assets.Get(assets.Definition.Forms.Single(x=>x.Id==assets.Definition.InitialForm).Idle),assets.Definition.CanvasSizeDip,assets.Definition.SpriteSizeDip,Scale);
    public void ExportProceduralWalk(string directory)=>rig.Export(directory);
    public void Resize(double scale)
    {
        if (Math.Abs(scale - Scale) < .001) return;
        var next = new LayeredSurface((int)Math.Round(assets.Definition.CanvasSizeDip * scale));
        Surface.Dispose(); Surface = next; Scale = scale;
        scratch.Dispose();scratch=new Bitmap(Surface.Width,Surface.Width,PixelFormat.Format32bppPArgb);
        rig.Dispose();rig=BuildRig();transition=null;lastAction=-1;
        portrait?.Dispose();portrait=null;
    }
    public bool NeedsPreparation(FormDefinition form)=>form.Animation.Rig=="portrait"?portrait?.FormId!=form.Id:portrait!=null;
    public void PrepareForm(FormDefinition form)
    {
        if(!NeedsPreparation(form))return;
        var watch=Stopwatch.StartNew();
        portrait?.Dispose();portrait=null;
        portraitMirror=false;
        if(form.Animation.Rig=="portrait")portrait=new PortraitRigCache(form,assets,assets.Definition.CanvasSizeDip,Scale);
        LastPreparationMilliseconds=watch.Elapsed.TotalMilliseconds;
    }
    public void Draw(PetSession session)
    {
        PrepareForm(session.Form);
        if(lastAction!=session.ActionId)
        {
            transition=lastAction<0?null:FrameMixer.Capture(Surface.Canvas);transitionStarted=session.Time;lastAction=session.ActionId;
        }
        if(session.Form.Animation.Rig=="portrait")
        {
            if(session.State==PetState.Walking&&session.Form.Frames?.Playback is {} playback)portraitMirror=session.Facing!=playback.SourceFacing;
            DrawPortrait(session);
            if(portraitMirror)
            {
                using(var copy=Graphics.FromImage(scratch)){copy.CompositingMode=CompositingMode.SourceCopy;copy.DrawImageUnscaled(Surface.Canvas,0,0);}
                using var flip=Graphics.FromImage(Surface.Canvas);flip.CompositingMode=CompositingMode.SourceCopy;flip.TranslateTransform(Surface.Width,0);flip.ScaleTransform(-1,1);flip.DrawImageUnscaled(scratch,0,0);
            }
        }
        else if(session.State==PetState.Walking)
        {
            var envelope=Gait.Envelope(session.BehaviorAge,session.BehaviorDuration);
            byte[]? idle=null;
            if(envelope<.999){DrawActor(session);idle=FrameMixer.Capture(Surface.Canvas);}
            var dash=session.ActiveBehavior?.Spec.Kind==BehaviorKind.Dash;
            rig.Sample(session.BehaviorAge/(dash ? .34 : Gait.CycleSeconds)).Draw(scratch);
            using(var g=Graphics.FromImage(Surface.Canvas))
            {
                g.CompositingMode=CompositingMode.SourceCopy;g.Clear(Color.Transparent);g.CompositingMode=CompositingMode.SourceOver;
                var pivot=(float)((assets.Definition.CanvasSizeDip-38)*Scale);
                g.TranslateTransform(Surface.Width/2f,pivot);g.RotateTransform(session.Facing*(dash?7:3)*(float)envelope);
                if(dash)g.ScaleTransform(1+.04f*(float)envelope,1-.03f*(float)envelope);
                g.TranslateTransform(-Surface.Width/2f,-pivot);
                g.DrawImage(scratch,new Rectangle(0,0,Surface.Width,Surface.Width));
            }
            if(idle!=null)FrameMixer.Mix(Surface.Canvas,idle,envelope);
        }
        else DrawActor(session);
        if(transition!=null)
        {
            var t=Gait.Smooth((session.Time-transitionStarted)/.14);FrameMixer.Mix(Surface.Canvas,transition,t);if(t>=1)transition=null;
        }
    }
    void DrawPortrait(PetSession session)
    {
        var cache=portrait??throw new InvalidOperationException("Portrait cache was not prepared.");
        var profile=session.Form.Animation;
        var idle=cache.Sample(PortraitClip.Idle,session.State==PetState.Dragging?0:session.Time/profile.IdleCycleSeconds);
        if(session.State==PetState.Reacting)
            cache.Sample(session.ReactionIndex==0?PortraitClip.ReactionA:PortraitClip.ReactionB,session.ReactionAge/session.Form.Reaction.Duration).Draw(Surface.Canvas);
        else if(session.State==PetState.Walking)
        {
            var envelope=Gait.Envelope(session.BehaviorAge,session.BehaviorDuration);
            cache.Sample(PortraitClip.Walk,session.WalkingPhase).Draw(Surface.Canvas);
            if(envelope<.999){idle.Draw(scratch);FrameMixer.Mix(Surface.Canvas,FrameMixer.Capture(scratch),envelope);}
        }
        else idle.Draw(Surface.Canvas);
    }
    void DrawActor(PetSession session)
    {
        using var g = Graphics.FromImage(Surface.Canvas);
        g.CompositingMode = CompositingMode.SourceCopy; g.Clear(Color.Transparent); g.CompositingMode = CompositingMode.SourceOver;
        g.SmoothingMode = SmoothingMode.HighQuality; g.InterpolationMode = InterpolationMode.HighQualityBicubic; g.PixelOffsetMode = PixelOffsetMode.HighQuality;
        g.ScaleTransform((float)Scale, (float)Scale);
        var canvas = (float)assets.Definition.CanvasSizeDip; var size = (float)assets.Definition.SpriteSizeDip;
        var pose = session.State == PetState.Reacting ? ReactionTimeline.Sample(session.Form.Reaction, session.ReactionAge, session.ReactionIndex) : new ReactionPose(0, 0, 0, 1, 1);
        var special=session.State==PetState.Special?SpecialMotion.Sample(session.ActiveBehavior!.Spec.Kind,session.BehaviorAge,session.BehaviorDuration):new SpecialPose(0,0,0,1,1);
        if(session.State==PetState.Special)pose=new ReactionPose(0,special.Lift,special.Roll,special.ScaleX,special.ScaleY);
        var breath = session.State == PetState.Dragging ? 0 : Math.Sin(session.Time * 1.8) * .006;
        g.TranslateTransform(canvas / 2+(float)special.ShiftX, canvas - 38 - (float)pose.Lift);
        g.RotateTransform((float)pose.Rotation); g.ScaleTransform((float)pose.ScaleX, (float)(pose.ScaleY + breath));
        var idle = assets.Get(session.Form.Idle); var reaction = assets.Get(session.Form.Reactions[session.ReactionIndex]);
        if (pose.Mix < .999) DrawSprite(g, idle, size, 1 - pose.Mix);
        if (pose.Mix > .001) DrawSprite(g, reaction, size, pose.Mix);
    }
    static void DrawSprite(Graphics g, SpriteAsset sprite, float size, double opacity)
    {
        var bounds = sprite.ContentBounds; var factor = size / Math.Max(bounds.Width, bounds.Height);
        var width = bounds.Width * factor; var height = bounds.Height * factor;
        using var attributes = new ImageAttributes();
        attributes.SetColorMatrix(new ColorMatrix { Matrix33 = (float)opacity });
        attributes.SetWrapMode(WrapMode.TileFlipXY);
        g.DrawImage(sprite.Bitmap, new Rectangle((int)Math.Round(-width / 2), (int)Math.Round(-height), (int)Math.Round(width), (int)Math.Round(height)), bounds.X, bounds.Y, bounds.Width, bounds.Height, GraphicsUnit.Pixel, attributes);
    }
    public void Dispose() {portrait?.Dispose();rig.Dispose();scratch.Dispose();Surface.Dispose();}
}
