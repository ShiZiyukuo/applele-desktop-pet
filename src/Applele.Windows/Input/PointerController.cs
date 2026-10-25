using Applele.Core;
using Applele.Windows.Movement;
using Applele.Windows.Windowing;
using System.Diagnostics;

namespace Applele.Windows.Input;

internal sealed class PointerController
{
    readonly DesktopWindow window;
    readonly PetSession session;
    readonly Action redraw;
    readonly Action<bool> rightClick;
    Point downScreen, downWindow;
    bool pressed, dragging,rightHeld;
    long rightStarted;
    public PointerController(DesktopWindow window, PetSession session, Action redraw, Action<bool> rightClick)
    {
        this.window = window; this.session = session; this.redraw = redraw; this.rightClick = rightClick;
        window.AcceptButtonMessage=AcceptButton;
        window.MouseDown += OnDown; window.MouseMove += OnMove; window.MouseUp += OnUp;
        window.MouseCaptureChanged += (_, _) => { if ((pressed||rightHeld) && !window.Capture) Cancel(); };
    }
    bool AcceptButton(MouseButtons button,bool down)=>down
        ? !pressed&&!rightHeld&&(button==MouseButtons.Left||button==MouseButtons.Right)
        : (button==MouseButtons.Left&&pressed)||(button==MouseButtons.Right&&rightHeld);
    void OnDown(object? sender, MouseEventArgs e)
    {
        // The first pressed button owns this gesture until release or capture cancellation.
        if(pressed||rightHeld)return;
        if(e.Button==MouseButtons.Right){rightHeld=true;rightStarted=Stopwatch.GetTimestamp();session.HoldPointer();window.Capture=true;return;}
        if (e.Button != MouseButtons.Left) return;
        downScreen = window.PointToScreen(e.Location); downWindow = window.Location;
        pressed = true; dragging = false; session.HoldPointer(); window.Capture = true;
    }
    void OnMove(object? sender, MouseEventArgs e)
    {
        if (!pressed) return;
        var point = window.PointToScreen(e.Location); var dx = point.X - downScreen.X; var dy = point.Y - downScreen.Y;
        var threshold = Math.Max(4, 4 * window.DeviceDpi / 96);
        if (!dragging && Math.Abs(dx) + Math.Abs(dy) < threshold) return;
        if (!dragging) { dragging = true; session.BeginDrag(); }
        window.Location = DesktopBoundary.Clamp(new Point(downWindow.X + dx, downWindow.Y + dy), window.Size, point); redraw();
    }
    void OnUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Right) {if(!rightHeld)return;var openMenu=Stopwatch.GetElapsedTime(rightStarted).TotalSeconds>=.45; Cancel(); rightClick(openMenu); return; }
        if (e.Button != MouseButtons.Left || !pressed) return;
        var wasDrag = dragging; pressed = false; dragging = false; window.Capture = false;
        session.ReleasePointer(); if (wasDrag) session.EndDrag(); else session.React(); redraw();
    }
    public void Cancel()
    {
        pressed = false; dragging = false;rightHeld=false; window.Capture = false; session.ReleasePointer(); session.EndDrag(); redraw();
    }
}
