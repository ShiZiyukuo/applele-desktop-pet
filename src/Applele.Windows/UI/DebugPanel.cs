using Applele.Core;
using Applele.Windows.Windowing;

namespace Applele.Windows.UI;

internal sealed class DebugPanel : Form
{
    readonly TextBox status = new() { Dock=DockStyle.Fill,Font=new Font("Microsoft YaHei UI",10),Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,WordWrap=true };
    public DebugPanel()
    {
        Text = "苹果乐 · 开发状态"; Size = new Size(620, 580); MinimumSize=new Size(460,360);Padding=new Padding(14);FormBorderStyle = FormBorderStyle.SizableToolWindow;
        Controls.Add(status);
    }
    public void RefreshState(PetSession session)
    {
        var reasons=session.Scheduler==null?"无调度器":string.Join("\n",session.Scheduler.Status(session.Time,session.State).Select(x=>$"{x.Id}：{x.Reason} / 等待 {x.WaitSeconds:F1}s / 冷却 {x.CooldownSeconds:F1}s"));
        var history=session.Scheduler==null?"":string.Join("\n",session.Scheduler.History.TakeLast(3));
        var navigation=session.RoamingEnabled?(session.State==PetState.Walking?"满屏溜达 / 行进中":$"满屏溜达 / 下段等待 {session.NextRoamSeconds:F1}s"):"安静性格";
        var source=session.Form.Frames!=null?"AI补画：8帧迈步、A/B各4帧；腿脚有推断补绘，待机为程序呼吸":session.Form.Animation.Rig=="portrait"?"单张立绘的局部程序动画，无真正人体走路帧":"24 帧缓存的玩偶网格步态，非手绘动画";
        var reaction=session.ReactionIndex==0?session.Form.Animation.ReactionAName:session.Form.Animation.ReactionBName;
        var text = $"形态：{session.Form.Name}\n状态：{session.State} / {session.Phase} / {reaction}\n动画配置：{session.Form.Animation.StyleId}\n动作：{session.MovementLabel}；版本：{session.ActionId}\n导航：{navigation}\n目标剩余：X {session.RemainingX:F0} / Y {session.RemainingY:F0} DIP\n自动行为暂停：{session.AutonomyPaused}；指针按住：{session.PointerHeld}\n安静模式下次决策：{(session.NextBehaviorSeconds is double t ? t.ToString("F1") + " 秒" : "无")}\n最近决策：{session.LastDecision}\n\n{reasons}\n\n{history}\n\n来源：{source}";
        text=text.Replace("\n",Environment.NewLine);if(status.Text==text)return;
        var firstLine=status.IsHandleCreated?NativeMethods.SendMessage(status.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32():0;
        var start=status.SelectionStart;var length=status.SelectionLength;
        status.Text=text;status.Select(Math.Min(start,text.Length),Math.Min(length,text.Length-Math.Min(start,text.Length)));
        if(status.IsHandleCreated)NativeMethods.SendMessage(status.Handle,0xB6,IntPtr.Zero,new IntPtr(firstLine-NativeMethods.SendMessage(status.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32()));
    }
}
