using Applele.Core;
using Applele.Windows.Windowing;

namespace Applele.Windows.Movement;

internal sealed class AutomaticMovement
{
    double remainderX,remainderY;
    int action=-1;
    public void SetLimits(DesktopWindow window,PetSession session,double scale)
    {
        var center=new Point(window.Left+window.Width/2,window.Top+window.Height/2);
        var area=Screen.FromPoint(center).WorkingArea;
        session.SetMovementLimits(Math.Max(0,(window.Left-area.Left)/scale),Math.Max(0,(area.Right-window.Right)/scale),
            Math.Max(0,(window.Top-area.Top)/scale),Math.Max(0,(area.Bottom-window.Bottom)/scale));
    }
    public void Apply(DesktopWindow window,PetSession session,double scale)
    {
        if(session.ActionId!=action){remainderX=0;remainderY=0;action=session.ActionId;}
        remainderX+=session.MotionDeltaDip*scale;remainderY+=session.MotionDeltaYDip*scale;
        var dx=(int)Math.Truncate(remainderX);var dy=(int)Math.Truncate(remainderY);remainderX-=dx;remainderY-=dy;
        if(dx==0 && dy==0)return;
        var candidate=new Point(window.Left+dx,window.Top+dy);
        var safe=DesktopBoundary.Clamp(candidate,window.Size,new Point(window.Left+window.Width/2,window.Top+window.Height/2));
        window.Location=safe;if(safe!=candidate){remainderX=0;remainderY=0;session.StopAtBoundary();}
    }
}
