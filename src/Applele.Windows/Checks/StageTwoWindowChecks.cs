using Applele.Core;
using Applele.Windows.App;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class StageTwoWindowChecks
{
    public static void Run(DesktopHost host,string directory,Action<bool,string> check)
    {
        var session=host.Session;var window=host.Window;
        host.Park();session.ReturnToPlush();host.Renderer.ExportProceduralWalk(Path.Combine(directory,"procedural-walk"));
        check(host.DemoBehavior("short-walk") && session.State==PetState.Walking,"walking starts with actual mesh renderer");
        var start=window.Location;
        for(int i=0;i<15;i++)host.Step(.04);
        check(window.Location!=start,"walking changes actual native window position");
        host.Renderer.Surface.Save(Path.Combine(directory,"walking.png"));
        check(host.Renderer.Surface.AlphaAt(0,0)==0,"walking maintains transparent corner pixels");
        var center=window.Width/2;
        NativeMethods.SendMessage(window.Handle,0x201,new IntPtr(1),NativeMethods.PackedPoint(center,center));
        NativeMethods.SendMessage(window.Handle,0x202,IntPtr.Zero,NativeMethods.PackedPoint(center,center));
        var stopped=window.Location;host.Step(.1);
        check(session.State==PetState.Reacting && window.Location==stopped,"native click stops actual walking immediately");
        session.ReturnToPlush();check(host.DemoBehavior("silly-hop"),"special hop can be previewed independently");
        for(int i=0;i<8;i++)host.Step(.05);
        host.Renderer.Surface.Save(Path.Combine(directory,"hop.png"));
        check(session.State==PetState.Special && window.Location==stopped,"hop animates locally without moving desktop window");
        session.BeginDrag();host.Step(.1);check(session.ActiveBehavior==null && session.MotionDeltaDip==0,"drag cancels special renderer ownership");session.EndDrag();
        session.PauseAutonomy(true);check(!host.DemoBehavior("short-walk"),"paused autonomy rejects debug walking too");
        for(int i=0;i<30;i++)host.Step(.1);check(window.Location==stopped,"paused pet keeps actual position");
        session.PauseAutonomy(false);host.ShowMenu(window.Location);Application.DoEvents();
        check(!host.DemoBehavior("short-walk"),"open context menu blocks autonomous preview");host.CloseMenu();Application.DoEvents();
        check(session.NextBehaviorSeconds>=20,"menu close resamples a real configured waiting interval");
        host.ShowMenu(window.Location);Application.DoEvents();
        host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="走到屏幕另一处").PerformClick();
        host.CloseMenu();Application.DoEvents();
        check(session.State==PetState.Walking,"actual demo menu callback starts after context menu has closed");
        session.PauseAutonomy(true);host.Step(.1);session.PauseAutonomy(false);
        var area=Screen.FromPoint(window.Location).WorkingArea;
        check(window.Left>=area.Left && window.Right<=area.Right && window.Top>=area.Top && window.Bottom<=area.Bottom,"all animated motion remains inside monitor work area");
        session.ReturnToPlush();host.Redraw();
    }
}
