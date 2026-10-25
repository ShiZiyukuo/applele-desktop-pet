using Applele.Core;

internal static class PersonalityChecks
{
    public static void Run(PetDefinition definition,BehaviorCatalog production,Action<string,Action> run)
    {
        void Check(bool ok,string message="personality assertion failed"){if(!ok)throw new InvalidOperationException(message);}
        PetSession New()=>new(definition,new Random(823),production);
        PetSession FastRoamer(bool gestures,BehaviorKind kind)
        {
            var spec=production.Get("playful-plush").Behaviors.Single(x=>x.Kind==kind) with{WaitMin=.1,WaitMax=.1,StartupCooldown=false,DurationMin=.6,DurationMax=.6};
            var profile=production.Profiles[0] with{DecisionWaitMin=.1,DecisionWaitMax=.1,RestWeight=.000000001,Behaviors=[spec]};
            return new(definition with{Roaming=definition.Roaming with{AllowSpecialDuringRoaming=gestures}},new Random(823),production with{Profiles=[profile,..production.Profiles.Skip(1)]});
        }
        void FinishLeg(PetSession s){for(int i=0;i<2000&&s.RequestedTripActive;i++)s.Tick(.05);Check(s.State==PetState.Idle);}
        run("continuous roaming can take a rare gesture at a rest stop then resumes a fresh route",()=>{
            var s=FastRoamer(true,BehaviorKind.Shake);s.SetRoaming(true);FinishLeg(s);
            for(int i=0;i<70&&s.State==PetState.Idle;i++)s.Tick(.05);
            Check(s.State==PetState.Special&&s.ActiveBehavior?.Spec.Kind==BehaviorKind.Shake&&s.RoamingEnabled);
            for(int i=0;i<100&&s.State!=PetState.Walking;i++)s.Tick(.05);
            Check(s.State==PetState.Walking&&s.RequestedTripActive&&s.RoamingEnabled);
        });
        run("roaming gesture policy can be disabled without interrupting full-screen navigation",()=>{
            var s=FastRoamer(false,BehaviorKind.Shake);s.SetRoaming(true);FinishLeg(s);
            for(int i=0;i<70&&s.State==PetState.Idle;i++)s.Tick(.05);
            Check(s.State==PetState.Walking&&s.RequestedTripActive&&s.ActiveBehavior==null);
        });
        run("roaming rest decisions exclude ordinary short walks",()=>{
            var s=FastRoamer(true,BehaviorKind.Walk);s.SetRoaming(true);FinishLeg(s);
            for(int i=0;i<70&&s.State==PetState.Idle;i++)s.Tick(.05);
            Check(s.State==PetState.Walking&&s.RequestedTripActive&&s.ActiveBehavior==null);
        });
        run("dash travels a bounded short distance with accelerated gait ownership then settles",()=>{
            var s=New();s.SetMovementLimits(300,300,200,200);Check(s.DemoBehavior("silly-dash"));
            Check(s.State==PetState.Walking&&s.ActiveBehavior?.Spec.IsSpecial==true);var choice=s.ActiveBehavior!;
            double x=0,y=0;for(int i=0;i<30;i++){s.Tick(.05);x+=s.MotionDeltaDip;y+=s.MotionDeltaYDip;}
            Check(Math.Abs(Math.Sqrt(x*x+y*y)-choice.Distance)<.001&&s.State==PetState.Idle);
            Check(s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="silly-dash").CooldownSeconds>230);
        });
        run("all rare gestures share an interval after completion or cancellation",()=>{
            foreach(var id in new[]{"silly-hop","silly-dash","silly-shake"})
            {
                var s=New();Check(s.DemoBehavior(id));s.Tick(.1);s.React();
                var statuses=s.Scheduler!.Status(s.Time,PetState.Idle);
                foreach(var spec in s.Scheduler.Profile.Behaviors.Where(x=>x.IsSpecial))Check(statuses.Single(x=>x.Id==spec.Id).CooldownSeconds>=150);
                Check(s.State==PetState.Reacting&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0&&s.ActiveBehavior==null);
            }
        });
        run("every rare gesture yields to dragging, pause and form changes without residual motion",()=>{
            foreach(var id in new[]{"silly-hop","silly-dash","silly-shake"})foreach(var interrupt in new Action<PetSession>[] {s=>s.BeginDrag(),s=>s.PauseAutonomy(true),s=>s.NextIllustration()})
            {
                var s=New();Check(s.DemoBehavior(id));s.Tick(.1);interrupt(s);s.Tick(.1);
                Check(s.ActiveBehavior==null&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);
            }
        });
        run("shake remains local, distinct from hopping, and returns exactly to neutral",()=>{
            var neutral=new SpecialPose(0,0,0,1,1);double shift=0;
            foreach(var kind in new[]{BehaviorKind.Hop,BehaviorKind.Shake})
            {
                Check(SpecialMotion.Sample(kind,0,1.2)==neutral&&SpecialMotion.Sample(kind,1.2,1.2)==neutral);
                for(int i=0;i<=120;i++)
                {
                    var pose=SpecialMotion.Sample(kind,i*.01,1.2);
                    Check(double.IsFinite(pose.Lift)&&Math.Abs(pose.Roll)<=8&&pose.ScaleX>.9&&pose.ScaleY>=.9);
                    if(kind==BehaviorKind.Shake)shift=Math.Max(shift,Math.Abs(pose.ShiftX));
                }
            }
            Check(shift>3);var s=New();Check(s.DemoBehavior("silly-shake"));
            for(int i=0;i<35;i++){s.Tick(.05);Check(s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);}Check(s.State==PetState.Idle);
        });
        run("finding the pet cancels an old route while preserving pause and roam intention",()=>{
            var s=New();s.SetRoaming(true);s.Tick(.1);s.Relocate();
            Check(s.State==PetState.Idle&&s.RoamingEnabled&&!s.RequestedTripActive&&s.MotionDeltaDip==0&&s.NextRoamSeconds>=1.2);
            s.Tick(.5);Check(s.State==PetState.Idle);s.PauseAutonomy(true);s.Relocate();Check(s.State==PetState.Paused&&s.RoamingEnabled);
        });
        run("rare behavior weights remain two in total and portraits remain restrained",()=>{
            var profile=production.Get("playful-plush");Check(Math.Abs(profile.Behaviors.Where(x=>x.IsSpecial).Sum(x=>x.Weight)-2)<1e-8);
            Check(profile.Behaviors.Where(x=>x.IsSpecial).All(x=>x.StartupCooldown&&x.CooldownMin>=180));
            Check(production.Profiles.Skip(1).All(x=>x.Behaviors.All(b=>!b.IsSpecial)));
        });
        run("a 48-hour simulation bounds movement and prevents back-to-back rare actions",()=>{
            var s=New();double x=400,y=300,lastSpecial=double.NegativeInfinity,lastDuration=0;int previous=-1,specials=0;
            for(int i=0;i<1728000;i++)
            {
                s.SetMovementLimits(Math.Max(0,x),Math.Max(0,800-x),Math.Max(0,y),Math.Max(0,600-y));s.Tick(.1);x+=s.MotionDeltaDip;y+=s.MotionDeltaYDip;
                Check(x>=-.001&&x<=800.001&&y>=-.001&&y<=600.001);
                if(s.ActionId!=previous){previous=s.ActionId;if(s.ActiveBehavior?.Spec.IsSpecial==true){Check(s.Time-lastSpecial>=150+lastDuration-.11);lastSpecial=s.Time;lastDuration=s.BehaviorDuration;specials++;}}
            }
            Check(specials>10&&specials<200);
        });
    }
}
