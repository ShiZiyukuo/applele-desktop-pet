using Applele.Core;

internal static class WalkPlaybackChecks
{
    public static void Run(PetDefinition definition,BehaviorCatalog behavior,Action<string,Action> run)
    {
        void Check(bool value,string message="walking assertion failed"){if(!value)throw new InvalidOperationException(message);}
        PetSession New(int seed=381){var session=new PetSession(definition,new Random(seed),behavior);session.SelectForm("ex1");session.SetMovementLimits(800,800,500,500);return session;}
        run("approved walking art uses its own distance playback while other forms retain distinct resources",()=>{
            var form=definition.Forms.Single(x=>x.Id=="ex1");Check(form.Frames!.Walk.Texture.File.EndsWith("walk-approved-v2.png")&&form.Frames.Playback!=null);
            Check(definition.Forms.Where(x=>x.Id!="ex1").All(x=>x.Frames?.Playback==null));
        });
        run("cruising eases at endpoints without the previous mid-route speed surge",()=>{
            double P(double t)=>WalkPlayback.CruiseProgress(t,10,.28);
            Check(P(0)==0&&P(10)==1&&P(-1)==0&&P(11)==1);
            Check(P(.001)/.001<.001&&(1-P(9.999))/.001<.001);
            var speeds=new[]{1.0,3,5,8}.Select(t=>(P(t+.01)-P(t))/.01).ToArray();Check(speeds.Max()-speeds.Min()<1e-10);
            Check(speeds.Max()<1.04/10);
        });
        run("fitted travel ends on a whole half-step and remains finite in tiny spaces",()=>{
            var settings=new WalkPlaybackSettings();foreach(var distance in new[]{0.0,.5,1,18,84,125,800,2500})
            {
                var cycle=WalkPlayback.EffectiveCycleDistance(distance,settings);Check(double.IsFinite(cycle)&&cycle>0);
                if(distance>=1)Check(Math.Abs(distance/cycle*2-Math.Round(distance/cycle*2))<1e-9);
            }
            Check(WalkPlayback.EffectiveCycleDistance(17,settings with{FitWholeSteps=false})==84);
        });
        run("walk phase follows two-axis displacement rather than elapsed time",()=>{
            var session=New();Check(session.SetRoaming(true));double distance=0;
            for(int i=0;i<100;i++){session.Tick(.025);distance+=Math.Sqrt(session.MotionDeltaDip*session.MotionDeltaDip+session.MotionDeltaYDip*session.MotionDeltaYDip);Check(session.State==PetState.Walking);Check(Math.Abs(session.WalkingDistance-distance)<1e-8);Check(Math.Abs(session.WalkingPhase*session.WalkingCycleDistance-distance)<1e-8);}
        });
        run("pointer hold freezes both the route and approved frame phase",()=>{
            var session=New();session.SetRoaming(true);session.Tick(.1);var phase=session.WalkingPhase;session.HoldPointer();
            for(int i=0;i<10;i++){session.Tick(.1);Check(session.MotionDeltaDip==0&&session.MotionDeltaYDip==0&&session.WalkingPhase==phase);}
            session.ReleasePointer();session.Tick(.1);Check(session.WalkingPhase>phase);
        });
        run("click drag pause boundary and form changes cancel approved walking without stale translation",()=>{
            Action<PetSession>[] actions=[s=>s.React(),s=>s.BeginDrag(),s=>s.PauseAutonomy(true),s=>s.StopAtBoundary(),s=>s.NextIllustration()];
            foreach(var cancel in actions){var session=New();session.SetRoaming(true);session.Tick(.1);cancel(session);session.Tick(.05);Check(session.State!=PetState.Walking&&session.MotionDeltaDip==0&&session.MotionDeltaYDip==0&&session.WalkingDistance==0);}
        });
        run("navigation duration includes its ramp so cruise speed matches configuration",()=>{
            var navigator=new ScreenNavigator(definition.Roaming,new Random(11));var leg=navigator.Plan(new(800,800,500,500))!.Value;
            var distance=Math.Sqrt(leg.X*leg.X+leg.Y*leg.Y);var mid=leg.Duration*.5;
            var speed=distance*(WalkPlayback.CruiseProgress(mid+.001,leg.Duration,definition.Roaming.AccelerationSeconds)-WalkPlayback.CruiseProgress(mid,leg.Duration,definition.Roaming.AccelerationSeconds))/.001;
            Check(Math.Abs(speed-definition.Roaming.SpeedDipPerSecond)<1e-6);
        });
        run("nonfinite walking configuration and progress cannot reach rendering",()=>{
            foreach(var bad in new[]{double.NaN,double.PositiveInfinity,-1.0})
            {
                try{(new WalkPlaybackSettings{CycleDistanceDip=bad}).Validate();throw new Exception("invalid stride accepted");}catch(InvalidDataException){}
                try{WalkPlayback.EffectiveCycleDistance(bad,new());throw new Exception("invalid distance accepted");}catch(ArgumentOutOfRangeException){}
            }
            try{(definition.Roaming with{AccelerationSeconds=0}).Validate();throw new Exception("invalid acceleration accepted");}catch(InvalidDataException){}
        });
    }
}
