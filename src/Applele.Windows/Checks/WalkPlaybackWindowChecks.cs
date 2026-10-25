using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.App;
using Applele.Windows.Assets;

namespace Applele.Windows.Checks;

internal static class WalkPlaybackWindowChecks
{
    public static void Run(DesktopHost host,string root,string directory,Action<bool,string> check)
    {
        var output=Path.Combine(directory,"approved-walk");Directory.CreateDirectory(output);
        using var catalog=new ResourceCatalog(root);var form=catalog.Definition.Forms.Single(x=>x.Id=="ex1");
        check(form.Frames!.Playback!=null&&form.Frames.Walk.Texture.File.EndsWith("walk-approved-v2.png"),"native renderer selects the user-reviewed ex1 atlas");
        var session=host.Session;session.SetRoaming(false);session.PauseAutonomy(false);session.SelectForm("ex1");host.Park();host.Redraw();
        using var cache=new PortraitRigCache(form,catalog,320,host.Renderer.Scale);
        string Hash(Bitmap image)=>Convert.ToHexString(SHA256.HashData(FrameMixer.Capture(image)));
        foreach(var facing in new[]{1,-1})
        {
            session.SetMovementLimits(facing<0?700:0,facing>0?700:0,0,0);session.MoveAcrossScreenOnce();host.Redraw();
            for(int i=0;i<12;i++){session.Tick(.025);host.Redraw();}
            using var expected=new Bitmap(host.Renderer.Surface.Width,host.Renderer.Surface.Width,PixelFormat.Format32bppPArgb);
            cache.Sample(PortraitClip.Walk,session.WalkingPhase).Draw(expected);
            if(facing<0)expected.RotateFlip(RotateFlipType.RotateNoneFlipX);
            check(session.Facing==facing&&Hash(expected)==Hash(host.Renderer.Surface.Canvas),"walk cels face travel direction without a second animation owner: "+facing);
            host.Renderer.Surface.Save(Path.Combine(output,facing<0?"left.png":"right.png"));
        }
        // Use the real window mover and its rounding accumulator on a safe leftward route.
        host.Park();session.SetMovementLimits(700,0,0,0);session.MoveAcrossScreenOnce();host.Redraw();
        var origin=host.Window.Location;var samples=new List<object>();var last=origin;var maxPixelDelta=0;
        for(int i=0;i<32;i++)
        {
            host.Step(.05);var current=host.Window.Location;maxPixelDelta=Math.Max(maxPixelDelta,Math.Abs(current.X-last.X));last=current;
            samples.Add(new{frame=i,session.WalkingDistance,session.WalkingPhase,x=current.X,y=current.Y});
        }
        check(session.State==PetState.Walking&&origin.X-host.Window.Left>60,"actual HWND moves while the approved gait advances");
        check(Math.Abs((origin.X-host.Window.Left)/host.Renderer.Scale-session.WalkingDistance)<1.1,"real window displacement matches distance-driven pose after pixel rounding");
        check(maxPixelDelta<=Math.Ceiling(catalog.Definition.Roaming.SpeedDipPerSecond*.05*host.Renderer.Scale)+1,"cruising HWND avoids a mid-route speed burst");
        var frozen=host.Window.Location;var phase=session.WalkingPhase;session.HoldPointer();
        for(int i=0;i<8;i++)host.Step(.05);
        check(host.Window.Location==frozen&&session.WalkingPhase==phase,"holding input freezes both actual movement and the selected gait phase");session.ReleasePointer();
        check(session.React(),"a click interrupts the approved walking sequence");host.Redraw();host.Step(.1);
        check(session.State==PetState.Reacting&&host.Window.Location==frozen,"reaction owns rendering without residual route displacement");
        session.BeginDrag();host.Step(.05);check(session.State==PetState.Dragging&&host.Window.Location==frozen,"drag cancels approved walk and reaction without a competing mover");session.EndDrag();
        session.NextIllustration();host.Redraw();check(session.FormId=="ex2"&&session.State==PetState.Idle,"changing form releases the approved ex1 gait");
        session.ReturnToPlush();host.Park();host.Redraw();
        File.WriteAllText(Path.Combine(output,"verification.json"),JsonSerializer.Serialize(new{form="ex1",source=form.Frames.Walk.Texture.File,cycleDistanceDip=form.Frames.Playback!.CycleDistanceDip,maxPixelDelta,samples,limitation="distance synchronization is verified; eight AI cels still have pose/detail discontinuities, and exact continuous sole locking is not claimed"},PetDefinition.JsonOptions));
    }
}
