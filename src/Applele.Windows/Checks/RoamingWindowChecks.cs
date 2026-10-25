using Applele.Core;
using Applele.Windows.App;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class RoamingWindowChecks
{
    public static void Run(DesktopHost host,string directory,Action<bool,string> check)
    {
        var session=host.Session;var window=host.Window;session.PauseAutonomy(false);host.Park();
        var work=Screen.FromPoint(window.Location).WorkingArea;
        host.ShowMenu(window.Location);Application.DoEvents();
        host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="开始满屏溜达").PerformClick();
        host.CloseMenu();Application.DoEvents();
        check(session.RoamingEnabled && session.State==PetState.Walking,"roaming menu enables a persistent full-screen route");
        var points=new List<Point>{window.Location};
        for(int leg=0;leg<4;leg++)
        {
            for(int wait=0;wait<100 && session.State!=PetState.Walking;wait++)host.Step(.05);
            check(session.State==PetState.Walking,"roaming continues after a brief rest "+leg);
            bool contained=true;
            for(int step=0;step<1600 && session.State==PetState.Walking;step++)
            {
                host.Step(.05);contained &= window.Left>=work.Left && window.Top>=work.Top && window.Right<=work.Right && window.Bottom<=work.Bottom;
            }
            check(contained && session.State==PetState.Idle,"full route remains contained and reaches its destination "+leg);
            points.Add(window.Location);
        }
        var spanX=points.Max(x=>x.X)-points.Min(x=>x.X);var spanY=points.Max(x=>x.Y)-points.Min(x=>x.Y);
        check(spanX>(work.Width-window.Width)*.25 && spanY>(work.Height-window.Height)*.25,"native window explores substantial horizontal AND vertical screen areas");
        File.WriteAllText(Path.Combine(directory,"roaming-route.json"),System.Text.Json.JsonSerializer.Serialize(new{workArea=work.ToString(),spanX,spanY,points=points.Select(p=>new{x=p.X,y=p.Y})},PetDefinition.JsonOptions));
        for(int wait=0;wait<100 && session.State!=PetState.Walking;wait++)host.Step(.05);
        var center=window.Width/2;
        NativeMethods.SendMessage(window.Handle,0x201,new IntPtr(1),NativeMethods.PackedPoint(center,center));
        NativeMethods.SendMessage(window.Handle,0x202,IntPtr.Zero,NativeMethods.PackedPoint(center,center));
        var stopped=window.Location;host.Step(.1);
        check(session.State==PetState.Reacting && stopped==window.Location,"actual left click stops both axes of roaming immediately");
        host.ShowMenu(window.Location);Application.DoEvents();
        host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="停止满屏溜达").PerformClick();
        host.CloseMenu();Application.DoEvents();
        for(int i=0;i<50;i++)host.Step(.1);
        check(!session.RoamingEnabled && session.State==PetState.Idle && window.Location==stopped,"stop menu cancels roaming and returns to quiet behavior");
        check(host.MoveAcrossScreenOnce() && !session.RoamingEnabled,"one-trip command does not enable persistent roaming");
        for(int i=0;i<1600 && session.State==PetState.Walking;i++)host.Step(.05);
        check(session.State==PetState.Idle && !session.RequestedTripActive,"one-trip native movement completes and settles to idle");
        session.ReturnToPlush();host.Park();host.Redraw();
    }
}
