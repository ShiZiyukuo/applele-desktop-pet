using System.Security.Cryptography;
using System.Text.Json;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.App;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class StageThreeWindowChecks
{
    public static void Run(DesktopHost host,string directory,Action<bool,string> check)
    {
        var session=host.Session;var window=host.Window;session.PauseAutonomy(false);session.SetRoaming(false);session.ReturnToPlush();host.Redraw();
        var output=Path.Combine(directory,"portraits");Directory.CreateDirectory(output);var hashes=new List<object>();var idleHashes=new HashSet<string>();
        string Hash()=>Convert.ToHexString(SHA256.HashData(FrameMixer.Capture(host.Renderer.Surface.Canvas)));
        void RightClick(){NativeMethods.SendMessage(window.Handle,0x204,new IntPtr(2),NativeMethods.PackedPoint(window.Width/2,window.Height/2));NativeMethods.SendMessage(window.Handle,0x205,IntPtr.Zero,NativeMethods.PackedPoint(window.Width/2,window.Height/2));}
        var ids=new[]{"ex1","ex2","alter-ex1","alter-ex2"};
        for(int i=0;i<4;i++)
        {
            var position=window.Location;RightClick();host.Step(.2);var id=ids[i];
            check(session.FormId==id&&window.Location==position,"native right click selects correct illustration without jumping: "+id);
            var idle=Hash();idleHashes.Add(idle);host.Renderer.Surface.Save(Path.Combine(output,id+"-idle.png"));
            check(host.Renderer.Surface.AlphaAt(0,0)==0&&host.Renderer.Surface.AlphaAt(window.Width-1,window.Height-1)==0,"portrait alpha corners are transparent: "+id);
            var point=window.PointToScreen(new Point(3,window.Height/2));
            check(NativeMethods.SendMessage(window.Handle,NativeMethods.WmNcHitTest,IntPtr.Zero,NativeMethods.PackedPoint(point.X,point.Y)).ToInt32()==NativeMethods.HtTransparent,"portrait transparent margin passes mouse through: "+id);
            check(host.DemoBehavior("short-walk"),"portrait has its own walking state and renderer: "+id);
            for(int t=0;t<12;t++)host.Step(.05);
            var walk=Hash();host.Renderer.Surface.Save(Path.Combine(output,id+"-walk.png"));check(walk!=idle,"portrait walk differs from idle: "+id);
            var variants=new string[2];
            for(int variant=0;variant<2;variant++)
            {
                bool selected=false;for(int attempt=0;attempt<100;attempt++){session.Tick(2);session.React();if(session.ReactionIndex==variant){selected=true;break;}}
                check(selected,"portrait reaction selected: "+id+"/"+variant);host.Redraw();
                session.Tick(session.Form.Reaction.AnticipationSeconds+session.Form.Reaction.ExpressionSeconds*.5);host.Redraw();
                variants[variant]=Hash();host.Renderer.Surface.Save(Path.Combine(output,id+"-reaction-"+variant+".png"));
                session.Tick(session.Form.Reaction.Duration+.05);check(session.State==PetState.Idle,"portrait reaction settles without stale walking: "+id+"/"+variant);
            }
            check(variants[0]!=variants[1]&&variants.All(x=>x!=idle),"portrait owns distinct A/B click feedback: "+id);
            hashes.Add(new{id,idle,walk,reactionA=variants[0],reactionB=variants[1]});
            session.React(); // Next iteration switches away from an active reaction.
        }
        check(idleHashes.Count==4,"four illustrations actually render four different images");RightClick();host.Step(.2);check(session.FormId=="ex1","native illustration cycle wraps to elite one and excludes plush");
        var before=session.FormId;NativeMethods.SendMessage(window.Handle,0x204,new IntPtr(2),NativeMethods.PackedPoint(window.Width/2,window.Height/2));Thread.Sleep(480);
        NativeMethods.SendMessage(window.Handle,0x205,IntPtr.Zero,NativeMethods.PackedPoint(window.Width/2,window.Height/2));Application.DoEvents();
        check(host.ContextMenu.Visible&&session.FormId==before,"long right press opens menu without changing form");
        check(!host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="演示一次蹦跶（调试）").Enabled,"portrait menu disables unconfigured plush-only behavior");host.CloseMenu();
        host.SetRoaming(true);for(int t=0;t<10;t++)host.Step(.05);RightClick();host.Step(.05);
        check(session.FormId=="ex2"&&session.RoamingEnabled&&!session.RequestedTripActive,"switching during full-screen roaming cancels the route and preserves mode");
        for(int t=0;t<75&&session.State!=PetState.Walking;t++)host.Step(.05);
        check(session.State==PetState.Walking&&session.FormId=="ex2","new form resumes full-screen roaming after its rest");
        host.ShowMenu(window.Location);Application.DoEvents();host.ContextMenu.Items.OfType<ToolStripMenuItem>().Single(x=>x.Text=="返回苹果乐玩偶").PerformClick();host.CloseMenu();session.SetRoaming(false);host.Step(.2);
        check(session.FormId=="plush"&&session.State==PetState.Idle,"return-to-plush menu restores original plush and cancels portrait state");
        File.WriteAllText(Path.Combine(output,"frame-hashes.json"),JsonSerializer.Serialize(hashes,PetDefinition.JsonOptions));host.Park();
    }
}
