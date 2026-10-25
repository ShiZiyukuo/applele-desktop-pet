using Applele.Core;

internal static class StageTwoChecks
{
    internal static object? SimulationStats {get;private set;}
    internal static void Run(PetDefinition definition, BehaviorCatalog production, Action<string,Action> run)
    {
        void Check(bool ok,string reason="assertion failed") {if(!ok)throw new InvalidOperationException(reason);}
        var fast = new BehaviorCatalog {Profiles=[production.Profiles[0] with {DecisionWaitMin=.1,DecisionWaitMax=.15,RestWeight=.000001,Behaviors=[production.Profiles[0].Behaviors[0] with {WaitMin=.1,WaitMax=.1,DurationMin=.8,DurationMax=.8,CooldownMin=1,CooldownMax=1,StartupCooldown=false}]},..production.Profiles.Skip(1)]};
        PetSession New(BehaviorCatalog? catalog=null,int seed=12)=>new(definition,new Random(seed),catalog??fast);
        void Advance(PetSession s,double duration) {for(double t=0;t<duration;t+=.02)s.Tick(.02);}
        run("autonomy waits before starting and finishes an eased short walk",()=>{
            var s=New();s.Tick(.09);Check(s.State==PetState.Idle);Advance(s,.1);Check(s.State==PetState.Walking);
            double distance=0;for(int i=0;i<50;i++){s.Tick(.02);distance+=Math.Abs(s.MotionDeltaDip);}
            Check(s.State==PetState.Idle && distance>=30 && distance<=78);Check(s.NextBehaviorSeconds>0);
        });
        run("click immediately cancels walking and consumes its cooldown",()=>{
            var s=New();Advance(s,.2);Check(s.State==PetState.Walking);Check(s.React());
            Check(s.ActiveBehavior==null && s.MotionDeltaDip==0 && s.Scheduler!.Status(s.Time,PetState.Idle)[0].CooldownSeconds>0);
            s.Tick(.1);Check(s.State==PetState.Reacting && s.MotionDeltaDip==0);
        });
        run("dragging blocks movement; release resamples the waiting period",()=>{
            var s=New();Advance(s,.2);s.BeginDrag();Advance(s,5);Check(s.State==PetState.Dragging && s.MotionDeltaDip==0);
            s.EndDrag();Check(s.State==PetState.Idle && s.NextBehaviorSeconds>=.1);s.Tick(.05);Check(s.State==PetState.Idle);
        });
        run("holding pointer stops movement before drag threshold",()=>{
            var s=New();Advance(s,.2);s.HoldPointer();Advance(s,1);Check(s.State==PetState.Walking && s.MotionDeltaDip==0);
            s.ReleasePointer();s.Tick(.02);Check(Math.Abs(s.MotionDeltaDip)>0);
        });
        run("pause, menu and form cancellation never resume stale autonomous actions",()=>{
            var s=New();Advance(s,.2);s.PauseAutonomy(true);Advance(s,10);Check(s.State==PetState.Paused && s.ActiveBehavior==null);
            s.PauseAutonomy(false);Check(s.NextBehaviorSeconds>=.1);s.OpenMenu();Advance(s,1);s.CloseMenu();Check(s.NextBehaviorSeconds>=.1);
            s.ReturnToPlush();Check(s.MotionDeltaDip==0 && !s.PointerHeld);
        });
        run("clipped travel cannot leave available movement bounds",()=>{
            var s=New();s.SetMovementLimits(0,7);Check(s.DemoBehavior("short-walk"));double total=0;
            for(int i=0;i<60;i++){s.Tick(.02);total+=s.MotionDeltaDip;}Check(total>=0 && total<=7.001);
            s.SetMovementLimits(0,0);Check(!s.DemoBehavior("short-walk") && s.State==PetState.Idle);
        });
        run("special hop has no translation and returns to idle",()=>{
            var s=New(production);Check(s.DemoBehavior("silly-hop") && s.State==PetState.Special);
            Advance(s,1.5);Check(s.State==PetState.Idle && s.MotionDeltaDip==0);
            Check(s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="silly-hop").CooldownSeconds>=170);
        });
        run("special behavior is interrupted by clicks or drag without overlap",()=>{
            var s=New(production);s.DemoBehavior("silly-hop");s.React();Check(s.State==PetState.Reacting && s.ActiveBehavior==null);
            s.BeginDrag();Check(s.State==PetState.Dragging);s.EndDrag();Check(s.NextBehaviorSeconds>=20);
        });
        run("sleep/resume cancels old movement without teleporting or catch-up actions",()=>{
            var s=New();Advance(s,.2);s.Tick(1000);Check(s.State==PetState.Idle && s.MotionDeltaDip==0 && s.NextBehaviorSeconds>=.1);
        });
        run("gait alternates feet and deforms locally rather than rigidly translating",()=>{
            var a=Gait.Sample(.25);var b=Gait.Sample(.75);
            Check(a.LeftFootLift>12 && a.RightFootLift==0 && b.RightFootLift>12 && b.LeftFootLift==0);
            double Distance(RigPoint x,RigPoint y)=>Math.Sqrt(Math.Pow(x.X-y.X,2)+Math.Pow(x.Y-y.Y,2));
            var d1=Distance(Gait.Deform(.35,.95,232,220,0),Gait.Deform(.5,.4,232,220,0));
            var d2=Distance(Gait.Deform(.35,.95,232,220,.75),Gait.Deform(.5,.4,232,220,.75));
            Check(Math.Abs(d1-d2)>3,"mesh behaved as a rigid whole-image transform");
            Check(Gait.Envelope(0,3)==0 && Gait.Envelope(3,3)==0 && Gait.Travel(0)==0 && Gait.Travel(1)==1);
        });
        run("cooldown diagnostics explain why a behavior cannot fire",()=>{
            var s=New(production);var hop=s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="silly-hop");
            Check(hop.Reason=="冷却中" && hop.CooldownSeconds>=180);
            Check(s.Scheduler.Status(0,PetState.Dragging).All(x=>x.Reason=="当前状态不允许"));
        });
        run("24-hour default simulation stays mostly quiet with occasional distinct special behaviors",()=>{
            var s=New(production,903);int idle=0,walks=0,hops=0,dashes=0,shakes=0,last=-1;
            var maxStep=production.Profiles[0].Behaviors.Where(x=>x.HasTranslation).Max(x=>x.DistanceMax*1.875*.1/x.DurationMin)+.001;
            for(int i=0;i<864000;i++) {
                s.Tick(.1);if(s.State==PetState.Idle)idle++;
                if(s.ActionId!=last){last=s.ActionId;switch(s.ActiveBehavior?.Spec.Kind){case BehaviorKind.Walk:walks++;break;case BehaviorKind.Hop:hops++;break;case BehaviorKind.Dash:dashes++;break;case BehaviorKind.Shake:shakes++;break;}}
                Check(Math.Abs(s.MotionDeltaDip)<maxStep && Math.Abs(s.MotionDeltaYDip)<maxStep);
            }
            Check(idle/864000.0>.96 && walks>50 && walks<1500 && hops>0 && dashes>0 && shakes>0 && hops+dashes+shakes<100,$"idle={idle/864000.0:P2}, walks={walks}, hops={hops}, dashes={dashes}, shakes={shakes}");
            SimulationStats=new {hours=24,idlePercent=idle/8640.0,walks,hops,dashes,shakes,seed=903};
        });
        run("independent seeded schedulers reproduce 10000 ticks",()=>{
            var a=New(production);var b=New(production);for(int i=0;i<10000;i++){a.Tick(.1);b.Tick(.1);Check(a.State==b.State && a.ActionId==b.ActionId && a.MotionDeltaDip==b.MotionDeltaDip);}
        });
        run("invalid behavior weights, timing and duplicated IDs are rejected",()=>{
            void Invalid(BehaviorProfile p){try{p.Validate();}catch(InvalidDataException){return;}throw new Exception("invalid behavior accepted");}
            var p=production.Profiles[0];Invalid(p with {RestWeight=double.NaN});
            Invalid(p with {Behaviors=[p.Behaviors[0],p.Behaviors[0]]});
            Invalid(p with {Behaviors=[p.Behaviors[0] with {DurationMin=5,DurationMax=1}]});
            Invalid(p with {Behaviors=[p.Behaviors[0] with {Weight=-1}]});
        });
        run("walking uses a smooth speed envelope instead of constant translation",()=>{
            Check(Gait.Travel(.01)<.00002 && 1-Gait.Travel(.99)<.00002);
            Check(Gait.Travel(.6)-Gait.Travel(.5)>Gait.Travel(.1)-Gait.Travel(0));
        });
    }
}
