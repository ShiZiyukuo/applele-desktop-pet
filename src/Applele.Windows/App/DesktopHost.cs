using System.Diagnostics;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.Assets;
using Applele.Windows.Input;
using Applele.Windows.Movement;
using Applele.Windows.UI;
using Applele.Windows.Windowing;

namespace Applele.Windows.App;

internal sealed class DesktopHost : ApplicationContext
{
    readonly ResourceCatalog catalog;
    readonly PetMenu menu;
    readonly PointerController pointer;
    readonly NotifyIcon? tray;
    readonly System.Windows.Forms.Timer timer = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly AutomaticMovement movement=new();
    DebugPanel? debug;
    double lastTime, lastBounds;
    bool exiting;
    public DesktopWindow Window { get; }
    public PetSession Session { get; }
    public PetRenderer Renderer { get; }
    internal double AnimationClockSeconds=>clock.Elapsed.TotalSeconds;
    public ContextMenuStrip ContextMenu => menu.Menu;
    public DesktopHost(string root, bool checkMode)
    {
        catalog = new ResourceCatalog(root); Session = new PetSession(catalog.Definition,null,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")));
        Window = new DesktopWindow(); MainForm = Window;
        _ = Window.Handle; Renderer = new PetRenderer(catalog, Window.DeviceDpi / 96.0);
        Window.Size = Renderer.Surface.Canvas.Size; Window.Location = DesktopBoundary.Park(Window.Size);
        Window.AlphaHitTest = (x, y) => Renderer.Surface.Hits(x, y, catalog.Definition.HitAlphaThreshold);
        menu = new PetMenu(Session, catalog.Definition, Redraw, ExitThread, ToggleDebug, Park,
            ()=>Window.BeginInvoke(()=>SetRoaming(!Session.RoamingEnabled)),()=>Window.BeginInvoke(()=>MoveAcrossScreenOnce()));
        menu.AddDemo("演示一次蹦跶（调试）",()=>QueueDemo("silly-hop"),()=>Session.Scheduler?.Profile.Behaviors.Any(x=>x.Id=="silly-hop")==true);
        menu.AddDemo("演示一次冲刺（调试）",()=>QueueDemo("silly-dash"),()=>Session.Scheduler?.Profile.Behaviors.Any(x=>x.Id=="silly-dash")==true);
        menu.AddDemo("演示一次抖动（调试）",()=>QueueDemo("silly-shake"),()=>Session.Scheduler?.Profile.Behaviors.Any(x=>x.Id=="silly-shake")==true);
        pointer = new PointerController(Window, Session, Redraw, RightClick);
        Window.DpiChanged += (_, _) => { pointer.Cancel();Session.Relocate();WithoutAnimationTime(()=>Renderer.Resize(Window.DeviceDpi / 96.0)); Window.Size = Renderer.Surface.Canvas.Size; Clamp(); Redraw(); };
        Window.FormClosed += (_, _) => DisposeOwned();
        timer.Tick += (_, _) => Tick();
        if (!checkMode)
        {
            tray = new NotifyIcon { Text = "苹果乐 · 右键菜单 / 双击找回", Icon = SystemIcons.Information, Visible = true };
            tray.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowMenu(Cursor.Position); };
            tray.DoubleClick += (_, _) => Park();
            Window.Shown += (_, _) => { Redraw(); StartRuntime(); };
        }
        Window.Show(); Redraw();
    }
    void RightClick(bool longPress)
    {
        if (longPress || (Control.ModifierKeys & Keys.Shift) != 0 || !Session.NextIllustration()) ShowMenu(Cursor.Position);
        else Redraw();
    }
    public void ShowMenu(Point point) { pointer.Cancel(); menu.Show(point); }
    public void CloseMenu() => menu.Menu.Close();
    public void Redraw()
    {
        if (exiting) return;
        if(Renderer.NeedsPreparation(Session.Form))WithoutAnimationTime(()=>Renderer.PrepareForm(Session.Form));
        Renderer.Draw(Session); Renderer.Surface.Present(Window.Handle, Window.Location);
        var title="苹果乐 · "+Session.Form.Name;if(Window.Text!=title)Window.Text=title;
    }
    void WithoutAnimationTime(Action prepare)
    {
        var running=clock.IsRunning;clock.Stop();
        try{prepare();}finally{if(running)clock.Start();}
    }
    public void StartRuntime() {lastTime=clock.Elapsed.TotalSeconds;timer.Start();}
    void QueueDemo(string id)=>Window.BeginInvoke(()=>DemoBehavior(id));
    public bool DemoBehavior(string id){movement.SetLimits(Window,Session,Renderer.Scale);var result=Session.DemoBehavior(id);Redraw();return result;}
    public bool SetRoaming(bool enabled){movement.SetLimits(Window,Session,Renderer.Scale);var result=Session.SetRoaming(enabled);Redraw();return result;}
    public bool MoveAcrossScreenOnce(){movement.SetLimits(Window,Session,Renderer.Scale);var result=Session.MoveAcrossScreenOnce();Redraw();return result;}
    public void Step(double delta)
    {
        movement.SetLimits(Window,Session,Renderer.Scale);Session.Tick(delta);movement.Apply(Window,Session,Renderer.Scale);Redraw();
    }
    void Tick()
    {
        var now = clock.Elapsed.TotalSeconds; Step(now - lastTime); lastTime = now;
        timer.Interval = 1000 / (Session.State is PetState.Reacting or PetState.Walking or PetState.Special ? catalog.Definition.ActionFps : catalog.Definition.IdleFps);
        if (now - lastBounds > 1 && !Window.Capture) { Clamp(); lastBounds = now; }
        if (debug is { IsDisposed: false }) debug.RefreshState(Session);
    }
    void Clamp()
    {
        var safe=DesktopBoundary.Clamp(Window.Location,Window.Size,new Point(Window.Left+Window.Width/2,Window.Top+Window.Height/2));
        if(safe==Window.Location)return;
        pointer.Cancel();Session.Relocate();Window.Location=safe;
    }
    public void Park() { pointer.Cancel();Session.Relocate();Window.Location = DesktopBoundary.Park(Window.Size); Redraw(); }
    void ToggleDebug()
    {
        if (debug is { IsDisposed: false }) { debug.Close(); return; }
        debug = new DebugPanel(); debug.RefreshState(Session); debug.Show();
    }
    void DisposeOwned()
    {
        if (exiting) return; exiting = true;
        timer.Stop(); timer.Dispose(); tray?.Dispose(); menu.Dispose(); debug?.Dispose(); Renderer.Dispose(); catalog.Dispose();
    }
    protected override void ExitThreadCore() { Window.Close(); DisposeOwned(); base.ExitThreadCore(); }
    protected override void Dispose(bool disposing) { if (disposing) DisposeOwned(); base.Dispose(disposing); }
}
