using System.Security.Cryptography;
using System.Text.Json;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.App;
using Applele.Windows.UI;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class PersonalityWindowChecks
{
    public static void Run(DesktopHost host,string directory,Action<bool,string> check)
    {
        var session=host.Session;var window=host.Window;var output=Path.Combine(directory,"personality");Directory.CreateDirectory(output);
        session.PauseAutonomy(false);session.SetRoaming(false);session.ReturnToPlush();host.Park();
        var center=window.Width/2;
        void Message(int message,int buttons=0,int offset=0)=>NativeMethods.SendMessage(window.Handle,message,new IntPtr(buttons),NativeMethods.PackedPoint(center+offset,center));
        var id=session.ActionId;Message(0x205);
        check(session.FormId=="plush"&&session.ActionId==id,"orphan right-button release cannot switch form or cancel another gesture");
        Message(0x201,1);Message(0x204,3);Message(0x205,1);
        check(window.Capture&&session.PointerHeld&&session.FormId=="plush","left gesture retains capture when the secondary right button is released");
        Message(0x202);check(session.State==PetState.Reacting&&session.FormId=="plush"&&!window.Capture,"two-button chord completes exactly the original left-click reaction");
        session.ReturnToPlush();host.Redraw();Message(0x204,2);Message(0x201,3);Message(0x202,2);
        check(window.Capture&&session.PointerHeld&&session.State==PetState.Idle,"right gesture ignores secondary left press and release");
        Message(0x205);check(session.FormId=="ex1"&&session.State!=PetState.Reacting&&!window.Capture,"two-button chord completes exactly one original right-click switch");
        session.ReturnToPlush();host.Redraw();Message(0x204,2);window.Capture=false;Message(0x205);
        check(session.FormId=="plush"&&!session.PointerHeld&&!window.Capture,"capture loss cancels a right press and ignores its later release");
        Message(0x201,1);Message(0x200,1,-30);Message(0x204,3);Message(0x205,1);
        check(session.State==PetState.Dragging&&window.Capture,"secondary right button cannot interrupt an active drag");
        Message(0x202);check(session.State==PetState.Idle&&session.FormId=="plush"&&!window.Capture,"chorded drag releases without accidental reaction or switch");
        host.SetRoaming(true);host.Step(.3);host.Park();var parked=window.Location;
        check(session.RoamingEnabled&&!session.RequestedTripActive&&session.State==PetState.Idle&&session.NextRoamSeconds>=1.2,"find-pet command cancels old roaming route and schedules a fresh rest");
        host.Step(.5);check(window.Location==parked&&session.State==PetState.Idle,"pet stays parked during the post-find rest instead of resuming its old route");
        session.PauseAutonomy(true);host.Park();host.Step(.5);
        check(session.State==PetState.Paused&&session.RoamingEnabled&&window.Location==parked,"finding the pet preserves an explicit movement pause and roaming intent");
        session.SetRoaming(false);session.PauseAutonomy(false);
        var clips=new List<object>();
        foreach(var behavior in new[]{("silly-dash","演示一次冲刺（调试）",BehaviorKind.Dash),("silly-shake","演示一次抖动（调试）",BehaviorKind.Shake)})
        {
            var area=Screen.FromPoint(window.Location).WorkingArea;window.Location=new Point(area.Left+(area.Width-window.Width)/2,area.Top+(area.Height-window.Height)/2);
            session.Relocate();host.Redraw();host.ShowMenu(window.Location);Application.DoEvents();
            var item=host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text==behavior.Item2);check(item.Enabled,"plush exposes configured action preview: "+behavior.Item1);
            item.PerformClick();host.CloseMenu();Application.DoEvents();
            check(session.ActiveBehavior?.Spec.Kind==behavior.Item3,"preview starts its own configured action after menu closes: "+behavior.Item1);
            var start=window.Location;var hashes=new HashSet<string>();bool transparent=true;
            for(int frame=0;frame<6;frame++)
            {
                host.Step(.1);host.Renderer.Surface.Save(Path.Combine(output,$"{behavior.Item1}-{frame:00}.png"));
                hashes.Add(Convert.ToHexString(SHA256.HashData(FrameMixer.Capture(host.Renderer.Surface.Canvas))));
                transparent &=host.Renderer.Surface.AlphaAt(0,0)==0&&host.Renderer.Surface.AlphaAt(window.Width-1,window.Height-1)==0;
            }
            check(hashes.Count>=5&&transparent,"preview visibly animates while retaining alpha: "+behavior.Item1);
            check(behavior.Item3==BehaviorKind.Dash?window.Location!=start:window.Location==start,"only the dash moves the actual desktop window: "+behavior.Item1);
            for(int i=0;i<30&&session.ActiveBehavior!=null;i++)host.Step(.05);
            check(session.State==PetState.Idle&&session.ActiveBehavior==null,"preview settles to idle without overlapping actions: "+behavior.Item1);
            clips.Add(new{id=behavior.Item1,frameHashes=hashes.ToArray()});
        }
        session.SelectForm("ex1");host.Redraw();host.ShowMenu(window.Location);Application.DoEvents();
        check(host.ContextMenu.Items.OfType<ToolStripMenuItem>().Where(x=>x.Text?.Contains("（调试）")==true).All(x=>!x.Enabled),"all plush-only gesture previews are disabled for portraits");host.CloseMenu();
        using(var panel=new DebugPanel())
        {
            session.ReturnToPlush();panel.RefreshState(session);panel.Show();Application.DoEvents();
            var text=panel.Controls.OfType<TextBox>().Single();
            check(text.Multiline&&text.ReadOnly&&text.ScrollBars==ScrollBars.Vertical&&text.Lines.Any(x=>x.Contains("silly-dash"))&&text.Lines.Any(x=>x.Contains("silly-shake")),"scrollable debug panel lists every behavior without clipping its diagnostics");
            panel.Size=panel.MinimumSize;NativeMethods.SendMessage(text.Handle,0xB6,IntPtr.Zero,new IntPtr(8));var first=NativeMethods.SendMessage(text.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32();
            session.Tick(.1);panel.RefreshState(session);
            check(NativeMethods.SendMessage(text.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()==first,"live diagnostic refresh preserves the reader's scroll position");panel.Close();
        }
        File.WriteAllText(Path.Combine(output,"frame-hashes.json"),JsonSerializer.Serialize(clips,PetDefinition.JsonOptions));session.ReturnToPlush();host.Park();
    }
}
