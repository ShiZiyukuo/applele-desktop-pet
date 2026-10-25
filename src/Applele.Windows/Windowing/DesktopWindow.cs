namespace Applele.Windows.Windowing;

internal sealed class DesktopWindow : Form
{
    public Func<int, int, bool>? AlphaHitTest { get; set; }
    public Func<MouseButtons,bool,bool>? AcceptButtonMessage {get;set;}
    public DesktopWindow()
    {
        Text = "苹果乐 · 满屏溜达版"; FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false; TopMost = true; StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }
    protected override bool ShowWithoutActivation => true;
    protected override CreateParams CreateParams
    {
        get { var cp = base.CreateParams; cp.ExStyle |= NativeMethods.WsExLayered | NativeMethods.WsExNoActivate | NativeMethods.WsExToolWindow; return cp; }
    }
    protected override void OnPaintBackground(PaintEventArgs e) { }
    protected override void OnPaint(PaintEventArgs e) { }
    protected override void WndProc(ref Message message)
    {
        var button=message.Msg switch
        {
            0x201 or 0x202 or 0x203=>MouseButtons.Left,
            0x204 or 0x205 or 0x206=>MouseButtons.Right,
            0x207 or 0x208 or 0x209=>MouseButtons.Middle,
            0x20B or 0x20C or 0x20D=>((message.WParam.ToInt64()>>16)&0xffff)==1?MouseButtons.XButton1:MouseButtons.XButton2,
            _=>MouseButtons.None
        };
        if(button!=MouseButtons.None&&AcceptButtonMessage?.Invoke(button,message.Msg is not (0x202 or 0x205 or 0x208 or 0x20C))==false)
        {
            // WinForms otherwise releases capture for a secondary button before the gesture owner finishes.
            message.Result=button is MouseButtons.XButton1 or MouseButtons.XButton2?new IntPtr(1):IntPtr.Zero;return;
        }
        if (message.Msg == NativeMethods.WmMouseActivate) { message.Result = new IntPtr(NativeMethods.MaNoActivate); return; }
        if (message.Msg == NativeMethods.WmNcHitTest)
        {
            var packed = message.LParam.ToInt64();
            var local = PointToClient(new Point(unchecked((short)(packed & 0xffff)), unchecked((short)((packed >> 16) & 0xffff))));
            message.Result = new IntPtr(Capture || AlphaHitTest?.Invoke(local.X, local.Y) == true ? NativeMethods.HtClient : NativeMethods.HtTransparent); return;
        }
        base.WndProc(ref message);
    }
}
