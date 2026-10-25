using Applele.Core;

namespace Applele.Windows.UI;

internal sealed class PetMenu : IDisposable
{
    readonly PetSession session;
    readonly Action redraw;
    readonly ToolStripMenuItem pause, next,roam;
    readonly List<(ToolStripMenuItem Item,Func<bool> Enabled)> dynamicItems=new();
    public ContextMenuStrip Menu { get; } = new();
    public PetMenu(PetSession session, PetDefinition definition, Action redraw, Action quit, Action showDebug, Action park,Action toggleRoaming,Action moveOnce)
    {
        this.session = session; this.redraw = redraw;
        Menu.Items.Add("返回苹果乐玩偶", null, (_, _) => { session.ReturnToPlush(); redraw(); });
        next = new ToolStripMenuItem("切换下一形态", null, (_, _) => { session.NextIllustration(); redraw(); }) { Enabled = definition.Forms.Any(x => x.InIllustrationCycle) };
        Menu.Items.Add(next);
        var formsMenu=new ToolStripMenuItem("选择形态");
        foreach(var form in definition.Forms){var id=form.Id;formsMenu.DropDownItems.Add(form.Name,null,(_,_)=>{session.SelectForm(id);redraw();});}
        Menu.Items.Add(formsMenu);
        pause = new ToolStripMenuItem("暂停自动走动", null, (_, _) => { session.PauseAutonomy(!session.AutonomyPaused); redraw(); }); Menu.Items.Add(pause);
        roam=new ToolStripMenuItem("开始满屏溜达",null,(_,_)=>toggleRoaming());Menu.Items.Add(roam);
        Menu.Items.Add("走到屏幕另一处",null,(_,_)=>moveOnce());
        Menu.Items.Add("回到屏幕边缘", null, (_, _) => park());
        Menu.Items.Add("开发状态 / 调试开关", null, (_, _) => showDebug());
        Menu.Items.Add(new ToolStripSeparator()); Menu.Items.Add("退出桌宠", null, (_, _) => quit());
        Menu.Opening += (_, _) => { pause.Text = session.AutonomyPaused ? "恢复自动走动" : "暂停自动走动";roam.Text=session.RoamingEnabled?"停止满屏溜达":"开始满屏溜达";roam.Checked=session.RoamingEnabled;foreach(var entry in dynamicItems)entry.Item.Enabled=entry.Enabled(); session.OpenMenu(); redraw(); };
        Menu.Closed += (_, _) => { session.CloseMenu(); redraw(); };
    }
    public void Show(Point location) => Menu.Show(location);
    public void AddDemo(string label,Action action,Func<bool>? enabled=null)
    {
        var item=new ToolStripMenuItem(label,null,(_,_)=>action());Menu.Items.Insert(Menu.Items.Count-2,item);
        if(enabled!=null)dynamicItems.Add((item,enabled));
    }
    public void Dispose() => Menu.Dispose();
}
