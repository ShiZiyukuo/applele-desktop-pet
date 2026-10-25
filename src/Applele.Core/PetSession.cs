namespace Applele.Core;

/// <summary>Single owner of actor state. No queued callbacks; a cancellation changes ActionId.</summary>
public sealed class PetSession
{
    readonly Random random;
    readonly PetDefinition definition;
    readonly BehaviorCatalog? behaviors;
    readonly Dictionary<string, BehaviorScheduler> schedulers = new();
    double time, reactionStart, nextClick;
    MovementSpace space=new(500,500,0,0);
    double activeAge, activeDuration, travelX, travelY, nextRoamAt;
    ScreenNavigator? navigator;
    BehaviorChoice? active;
    bool menuOpen;
    public PetState State { get; private set; } = PetState.Idle;
    public string FormId { get; private set; }
    public FormDefinition Form => definition.Forms.Single(x => x.Id == FormId);
    public bool AutonomyPaused { get; private set; }
    public bool PointerHeld { get; private set; }
    public int ReactionIndex { get; private set; }
    public int ActionId { get; private set; }
    public double Time => time;
    public double ReactionAge => Math.Max(0, time - reactionStart);
    public string LastDecision { get; private set; } = "等待第一次自主决策";
    public BehaviorScheduler? Scheduler => behaviors == null ? null : GetScheduler();
    public BehaviorChoice? ActiveBehavior => active;
    public double BehaviorAge => activeAge;
    public double BehaviorDuration => activeDuration;
    public double MotionDeltaDip { get; private set; }
    public double MotionDeltaYDip { get; private set; }
    public bool RoamingEnabled { get; private set; }
    public bool RequestedTripActive { get; private set; }
    public string MovementLabel => RequestedTripActive ? (RoamingEnabled?"满屏溜达":"前往屏幕另一处") : active?.Spec.Id??"无";
    public double? NextRoamSeconds=>RoamingEnabled?Math.Max(0,nextRoamAt-time):null;
    double TravelProgress(double age)=>RequestedTripActive||Form.Frames?.Playback!=null?WalkPlayback.CruiseProgress(age,activeDuration,definition.Roaming.AccelerationSeconds):Gait.Travel(age/activeDuration);
    public double WalkingDistance=>State==PetState.Walking?Math.Sqrt(travelX*travelX+travelY*travelY)*TravelProgress(activeAge):0;
    public double WalkingCycleDistance=>Form.Frames?.Playback is {} settings?WalkPlayback.EffectiveCycleDistance(Math.Sqrt(travelX*travelX+travelY*travelY),settings):0;
    public double WalkingPhase=>Form.Frames?.Playback!=null?WalkingDistance/WalkingCycleDistance:activeAge/Form.Animation.WalkCycleSeconds;
    public double RemainingX=>State==PetState.Walking?travelX*(1-TravelProgress(activeAge)):0;
    public double RemainingY=>State==PetState.Walking?travelY*(1-TravelProgress(activeAge)):0;
    public int Facing { get; private set; } = 1;
    public double? NextBehaviorSeconds => Scheduler?.NextDecisionSeconds(time);
    public ReactionPhase Phase => State != PetState.Reacting ? ReactionPhase.None : ReactionAge < Form.Reaction.AnticipationSeconds ? ReactionPhase.Anticipation : ReactionAge < Form.Reaction.AnticipationSeconds + Form.Reaction.ExpressionSeconds ? ReactionPhase.Expression : ReactionPhase.Recovery;

    public PetSession(PetDefinition definition, Random? random = null, BehaviorCatalog? behaviors = null)
    {
        definition.Validate(); this.definition = definition; this.random = random ?? new Random(); FormId = definition.InitialForm;
        this.behaviors = behaviors;
        if(behaviors != null) { behaviors.Validate(); foreach(var form in definition.Forms) _=behaviors.Get(form.BehaviorProfileId); _=GetScheduler(); }
    }
    BehaviorScheduler GetScheduler()
    {
        if(!schedulers.TryGetValue(FormId,out var value)) {value=new BehaviorScheduler(behaviors!.Get(Form.BehaviorProfileId),time,new Random(random.Next()));schedulers.Add(FormId,value);}
        return value;
    }
    public void Tick(double delta)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        time += delta; MotionDeltaDip=0;MotionDeltaYDip=0;
        if(delta>2 && Scheduler!=null && State is PetState.Idle or PetState.Walking or PetState.Special) {Rest("长时间挂起后取消旧动作，重新等待");return;}
        if (State == PetState.Reacting && ReactionAge >= Form.Reaction.Duration)
        {
            nextClick = time + Form.Reaction.CooldownSeconds; Rest("点击动作完整结束，进入冷却");
        }
        if(PointerHeld || menuOpen || State is PetState.Dragging or PetState.Paused or PetState.Reacting) return;
        if(active != null || RequestedTripActive)
        {
            var old=activeAge;activeAge=Math.Min(activeDuration,activeAge+Math.Min(delta,.1));
            if(State==PetState.Walking)
            {
                var progress=TravelProgress(activeAge)-TravelProgress(old);
                MotionDeltaDip=travelX*progress;MotionDeltaYDip=travelY*progress;
            }
            if(activeAge>=activeDuration)
            {
                var finalX=MotionDeltaDip;var finalY=MotionDeltaYDip;var requested=RequestedTripActive;
                if(active!=null)Scheduler!.End(active.Spec,time,false);active=null;RequestedTripActive=false;
                Rest(requested?(RoamingEnabled?"已到达，短暂休息后继续探索":"已到达，恢复安静待机"):"自主动作结束，恢复安静待机",requested&&RoamingEnabled);MotionDeltaDip=finalX;MotionDeltaYDip=finalY;
            }
            return;
        }
        if(State==PetState.Idle && RoamingEnabled)
        {
            if(time>=nextRoamAt)
            {
                var choice=definition.Roaming.AllowSpecialDuringRoaming?Scheduler?.Decide(time,State,true):null;
                if(choice!=null&&StartBehavior(choice,false))return;
                StartRequestedLeg();
            }
            return;
        }
        if(State==PetState.Idle && !AutonomyPaused && Scheduler!=null)
        {
            var choice=Scheduler.Decide(time,State);
            if(choice!=null) StartBehavior(choice,false);
        }
    }
    void Enter(PetState state, string reason) { State = state; LastDecision = reason; ActionId++; }
    void CancelAutomatic()
    {
        if(active!=null) {Scheduler!.End(active.Spec,time,true);active=null;}
        RequestedTripActive=false;MotionDeltaDip=0;MotionDeltaYDip=0;
    }
    void Rest(string reason,bool keepBehaviorWait=false)
    {
        CancelAutomatic();if(!keepBehaviorWait)Scheduler?.IdleFrom(time);if(RoamingEnabled)nextRoamAt=time+Navigator.RestSeconds();
        Enter(menuOpen || AutonomyPaused ? PetState.Paused : PetState.Idle,reason);
    }
    ScreenNavigator Navigator=>navigator??=new ScreenNavigator(definition.Roaming,new Random(random.Next()));
    public void SetMovementLimits(double left,double right)=>SetMovementLimits(left,right,0,0);
    public void SetMovementLimits(double left,double right,double up,double down){var next=new MovementSpace(left,right,up,down);next.Validate();space=next;}
    public bool SetRoaming(bool enabled)
    {
        if(menuOpen || PointerHeld || State==PetState.Dragging){LastDecision="菜单或拖动结束后才能改变导航模式";return false;}
        RoamingEnabled=enabled;if(enabled)AutonomyPaused=false;
        Rest(enabled?"开始满屏溜达":"已停止满屏溜达，恢复安静性格");
        return !enabled || StartRequestedLeg();
    }
    public bool MoveAcrossScreenOnce()
    {
        if(menuOpen || PointerHeld || State==PetState.Dragging){LastDecision="当前输入尚未结束";return false;}
        RoamingEnabled=false;AutonomyPaused=false;Rest("主动前往屏幕另一处");return StartRequestedLeg();
    }
    bool StartRequestedLeg()
    {
        var plan=Navigator.Plan(space);
        if(plan==null){Rest("可见区域不足，稍后重新规划");return false;}
        travelX=plan.Value.X;travelY=plan.Value.Y;activeAge=0;activeDuration=plan.Value.Duration;RequestedTripActive=true;
        if(Math.Abs(travelX)>.01)Facing=travelX<0?-1:1;
        Enter(PetState.Walking,RoamingEnabled?"沿屏幕分区规划下一段溜达":"前往随机屏幕位置");return true;
    }
    bool StartBehavior(BehaviorChoice choice,bool demo)
    {
        if(State!=PetState.Idle || menuOpen || PointerHeld || AutonomyPaused) {LastDecision="当前状态禁止自主动作";return false;}
        if(choice.Spec.HasTranslation)
        {
            var angle=random.NextDouble()*Math.PI*2;
            var dx=space.Up+space.Down<1?(random.Next(2)==0?-1.0:1.0):Math.Cos(angle);
            var dy=space.Up+space.Down<1?0:Math.Sin(angle);
            var edgeChance=Scheduler!.Profile.EdgePreference;
            if(random.NextDouble()<edgeChance)
            {
                (double Distance,double X,double Y)[] edges=[(space.Left,-1,0),(space.Right,1,0),(space.Up,0,-1),(space.Down,0,1)];
                var edge=edges.OrderBy(x=>x.Distance).First();
                if(edge.Distance>8 && edge.Distance<choice.Distance*2){dx=edge.X;dy=edge.Y;}
            }
            (travelX,travelY)=space.Clamp(dx*choice.Distance,dy*choice.Distance);
            if(Math.Sqrt(travelX*travelX+travelY*travelY)<Math.Min(16,choice.Distance*.4))
                (travelX,travelY)=space.Clamp(-dx*choice.Distance,-dy*choice.Distance);
            if(Math.Sqrt(travelX*travelX+travelY*travelY)<1) {Scheduler.End(choice.Spec,time,true);Rest("没有移动空间，继续休息");return false;}
            if(Math.Abs(travelX)>.01)Facing=travelX<0?-1:1;
        }
        else {travelX=0;travelY=0;}
        active=choice;activeAge=0;activeDuration=choice.Duration;
        Enter(choice.Spec.HasTranslation?PetState.Walking:PetState.Special,(demo?"开发演示：":"自主选择：")+choice.Spec.Id);
        return true;
    }
    public bool DemoBehavior(string id)
    {
        var spec=Scheduler?.Profile.Behaviors.SingleOrDefault(x=>x.Id==id);
        if(spec==null) {LastDecision="未配置该行为";return false;}
        return StartBehavior(Scheduler!.Choose(spec),true);
    }
    public void StopAtBoundary() {if(State==PetState.Walking)Rest("已到屏幕边界，停止并重新等待");}
    public void Relocate() {PointerHeld=false;Rest("用户找回或显示区域改变，取消旧路线并重新等待");}
    public bool React()
    {
        if (menuOpen || State == PetState.Dragging || State == PetState.Reacting || time < nextClick)
        {
            LastDecision = menuOpen ? "菜单已打开" : State == PetState.Reacting ? "反应播放中，丢弃重复点击" : State == PetState.Dragging ? "拖动中，拒绝反应" : "点击冷却中";
            return false;
        }
        CancelAutomatic();ReactionIndex = random.Next(2); reactionStart = time; Enter(PetState.Reacting, "用户点击优先触发反应 " + (ReactionIndex == 0 ? "A" : "B")); return true;
    }
    public void HoldPointer() { PointerHeld = true; }
    public void ReleasePointer() { if(PointerHeld && State==PetState.Idle)Scheduler?.IdleFrom(time);PointerHeld = false; }
    public void BeginDrag() { PointerHeld = true; CancelAutomatic();Enter(PetState.Dragging, "拖拽取消当前动作"); }
    public void EndDrag() { PointerHeld = false; if (State == PetState.Dragging) Rest("拖拽结束，重新等待自主行为"); }
    public void PauseAutonomy(bool value) { AutonomyPaused = value; Rest(value ? "用户暂停自主行为" : "用户恢复自主行为"); }
    public void OpenMenu() { menuOpen = true; PointerHeld = false; Rest("菜单临时暂停自主行为"); }
    public void CloseMenu() { menuOpen = false; Rest("菜单关闭，重新等待自主行为"); }
    public void ReturnToPlush()=>SelectForm(definition.InitialForm);
    public bool SelectForm(string id)
    {
        if(!definition.Forms.Any(x=>x.Id==id)){LastDecision="未知形态，保持当前状态";return false;}
        CancelAutomatic();PointerHeld=false;FormId=id;nextClick=time;Rest("切换形态，取消旧动作并重新等待");return true;
    }
    public bool NextIllustration()
    {
        var forms = definition.Forms.Where(x => x.InIllustrationCycle).ToArray();
        if (forms.Length == 0) { LastDecision = "当前未配置立绘，未切换"; return false; }
        var current = Array.FindIndex(forms, x => x.Id == FormId);
        return SelectForm(forms[(current + 1) % forms.Length].Id);
    }
}
