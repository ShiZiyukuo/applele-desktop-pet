using System.Text.Json;
using System.Diagnostics;
using Applele.Core;
using Applele.Windows.App;
using Applele.Windows.Movement;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class StageOneChecks
{
    public static void Run(DesktopHost host, string reportDirectory, IntPtr originalForeground)
    {
        Directory.CreateDirectory(reportDirectory); var checks = new List<object>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException("Windows 验收失败：" + name); checks.Add(new { name, passed = true }); }
        var window = host.Window; var session = host.Session;
        try
        {
            Application.DoEvents(); host.Redraw();
            var ex = NativeMethods.GetWindowLongPtr(window.Handle, -20).ToInt64();
            Check((ex & NativeMethods.WsExLayered) != 0 && (ex & NativeMethods.WsExNoActivate) != 0 && (ex & NativeMethods.WsExToolWindow) != 0, "native layered/noactivate/toolwindow styles");
            Check(NativeMethods.GetForegroundWindow() == originalForeground, "startup does not change keyboard foreground");
            Check(window.TopMost && window.FormBorderStyle == FormBorderStyle.None && !window.ShowInTaskbar, "topmost borderless hidden-from-taskbar");
            var surface = host.Renderer.Surface; var width = surface.Width;
            Check(surface.AlphaAt(0,0) == 0 && surface.AlphaAt(width-1,width-1) == 0, "canvas corners have zero alpha");
            var opaque = new Point(width/2,width/2);
            Check(surface.Hits(opaque.X,opaque.Y,20), "character center has opaque pixels");
            int Hit(int x,int y) => NativeMethods.SendMessage(window.Handle,NativeMethods.WmNcHitTest,IntPtr.Zero,NativeMethods.PackedPoint(window.Left+x,window.Top+y)).ToInt32();
            Check(Hit(0,0)==NativeMethods.HtTransparent && Hit(opaque.X,opaque.Y)==NativeMethods.HtClient,"native alpha hit testing matches silhouette");
            Check(NativeMethods.WindowFromPoint(new NativeMethods.Point(window.Left,window.Top))!=window.Handle,"Windows routes transparent pixels away from pet");
            Check(NativeMethods.WindowFromPoint(new NativeMethods.Point(window.Left+opaque.X,window.Top+opaque.Y))==window.Handle,"Windows resolves opaque pixels to pet");
            Check(NativeMethods.SendMessage(window.Handle,NativeMethods.WmMouseActivate,IntPtr.Zero,IntPtr.Zero).ToInt32()==NativeMethods.MaNoActivate,"mouse activation refused");
            host.Renderer.Surface.Save(Path.Combine(reportDirectory,"idle.png"));
            void Message(int id, int x, int y, int buttons=0) => NativeMethods.SendMessage(window.Handle,id,new IntPtr(buttons),NativeMethods.PackedPoint(x,y));
            void Click() { Message(0x201,opaque.X,opaque.Y,1);Message(0x202,opaque.X,opaque.Y); }
            Click(); Check(session.State==PetState.Reacting,"Win32 left-click triggers reaction");
            var actionId=session.ActionId;session.Tick(.7);Click();Check(session.ActionId==actionId,"native repeated clicks cannot restart active reaction");
            Message(0x201,opaque.X,opaque.Y,1); Message(0x200,opaque.X-30,opaque.Y-20,1);
            Check(session.State==PetState.Dragging && window.Capture,"native drag preempts reaction and captures input");
            Message(0x202,opaque.X-30,opaque.Y-20);Check(session.State==PetState.Idle && !window.Capture,"drag release does not trigger click or retain capture");
            var boundary=DesktopBoundary.Clamp(new Point(int.MaxValue,int.MaxValue),window.Size,window.Location);
            var work=Screen.FromPoint(window.Location).WorkingArea;
            Check(boundary.X+window.Width<=work.Right && boundary.Y+window.Height<=work.Bottom,"drag boundary contains whole canvas");
            host.ShowMenu(window.Location);Application.DoEvents();Check(session.State==PetState.Paused,"opening actual menu suspends actor");
            host.CloseMenu();Application.DoEvents();Check(session.State==PetState.Idle,"closing actual menu restores actor");
            // Drive both stochastic outcomes through their full timelines, recording actual rendered output.
            foreach(var variant in new[]{0,1}) {
                var selected=false;
                for(int i=0;i<100;i++) {session.Tick(2);session.React();if(session.ReactionIndex==variant){selected=true;break;}}
                Check(selected,"reaction variant reachable "+variant);
                host.Redraw();session.Tick(.45);host.Redraw();host.Renderer.Surface.Save(Path.Combine(reportDirectory,"reaction-"+variant+".png"));
                Check(host.Renderer.Surface.AlphaAt(0,0)==0,"reaction retains transparent canvas "+variant);
                session.Tick(2);Check(session.State==PetState.Idle,"reaction returns to idle "+variant);
            }
            foreach(var dpi in new[]{1.0,1.25,1.5,2.0}) {
                host.Renderer.Resize(dpi);window.Size=host.Renderer.Surface.Canvas.Size;host.Renderer.Draw(session);host.Renderer.Surface.Present(window.Handle,window.Location);
                Check(host.Renderer.Surface.Width==(int)Math.Round(320*dpi) && host.Renderer.Surface.AlphaAt(0,0)==0,"render and alpha at DPI scale "+dpi);
            }
            host.Renderer.Resize(window.DeviceDpi/96.0);window.Size=host.Renderer.Surface.Canvas.Size;host.Park();
            StageTwoWindowChecks.Run(host,reportDirectory,Check);
            RoamingWindowChecks.Run(host,reportDirectory,Check);
            StageThreeWindowChecks.Run(host,reportDirectory,Check);
            StageFourWindowChecks.Run(host,AppContext.BaseDirectory,reportDirectory,Check);
            PersonalityWindowChecks.Run(host,reportDirectory,Check);
            CharacterFrameWindowChecks.Run(host,AppContext.BaseDirectory,reportDirectory,Check);
            WalkPlaybackWindowChecks.Run(host,AppContext.BaseDirectory,reportDirectory,Check);
            session.PauseAutonomy(true);host.StartRuntime();
            var warmup=Stopwatch.StartNew();
            while(warmup.Elapsed.TotalSeconds<1) {Application.DoEvents();Thread.Sleep(10);}
            using var process=Process.GetCurrentProcess();
            var cpuStart=process.TotalProcessorTime.TotalMilliseconds;
            var gdiStart=NativeMethods.GetGuiResources(process.Handle,0);
            var sample=Stopwatch.StartNew();
            while(sample.Elapsed.TotalSeconds<5) {Application.DoEvents();Thread.Sleep(10);}
            process.Refresh();
            var gdiEnd=NativeMethods.GetGuiResources(process.Handle,0);
            File.WriteAllText(Path.Combine(reportDirectory,"runtime-sample.json"),JsonSerializer.Serialize(new{seconds=sample.Elapsed.TotalSeconds,cpuMilliseconds=process.TotalProcessorTime.TotalMilliseconds-cpuStart,workingSetMiB=process.WorkingSet64/(1024.0*1024),gdiStart,gdiEnd,idleTargetFps=15,scope="5-second idle sample with actual application timer; not a long-term performance guarantee"},PetDefinition.JsonOptions));
            Check(gdiEnd<=gdiStart+2,$"idle GDI handle count stays stable after warmup ({gdiStart} -> {gdiEnd})");
            host.ContextMenu.Items[host.ContextMenu.Items.Count-1].PerformClick();Application.DoEvents();
            Check(window.IsDisposed,"exit menu closes window and disposes owned rendering resources");
            var clr=process.Modules.Cast<ProcessModule>().Single(x=>string.Equals(x.ModuleName,"coreclr.dll",StringComparison.OrdinalIgnoreCase)).FileName;
            File.WriteAllText(Path.Combine(reportDirectory,"windows-character-frames.json"),JsonSerializer.Serialize(new {phase=5,feature="AI-redrawn human walking and click cels with procedural idle and autonomous personality",passed=checks.Count,failed=0,runtime=new{description=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,coreclrPath=clr,usesAppLocalRuntime=Path.GetDirectoryName(clr)==Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory),is64Bit=Environment.Is64BitProcess},checks,limitations=new[]{"Automated checks run on the Codex isolated desktop, not the user desktop","Win32 messages are sent to the real pet HWND without moving the physical pointer","DPI backing surfaces are tested; mixed-DPI physical monitors require hardware testing","Character cels are AI-redrawn keyframes with inferred unseen legs and boots, not official animation or a full hand-animated sequence","Idle breathing, plush gait and clip transitions remain procedural","App-local CLR loading is verified; no clean Windows VM was available"}},PetDefinition.JsonOptions));
        }
        finally { if(!window.IsDisposed)window.Close(); }
    }
}
