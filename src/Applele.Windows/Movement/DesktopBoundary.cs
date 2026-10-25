namespace Applele.Windows.Movement;

internal static class DesktopBoundary
{
    public static Point Clamp(Point candidate, Size size, Point monitorPoint)
    {
        var area = Screen.FromPoint(monitorPoint).WorkingArea;
        return new Point(Math.Clamp(candidate.X, area.Left, Math.Max(area.Left, area.Right - size.Width)),
            Math.Clamp(candidate.Y, area.Top, Math.Max(area.Top, area.Bottom - size.Height)));
    }
    public static Point Park(Size size)
    {
        var area = (Screen.PrimaryScreen ?? Screen.AllScreens[0]).WorkingArea;
        return new Point(Math.Max(area.Left, area.Right - size.Width - 8), Math.Max(area.Top, area.Bottom - size.Height - 8));
    }
}
