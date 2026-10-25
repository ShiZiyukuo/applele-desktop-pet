using Applele.Core;

internal static class RoamingChecks
{
    public static void Run(PetDefinition definition,BehaviorCatalog catalog,Action<string,Action> run)
    {
        void Check(bool ok,string reason="assertion failed"){if(!ok)throw new InvalidOperationException(reason);}
        PetSession New(int seed=771)=>new(definition,new Random(seed),catalog);
        void Advance(PetSession s,double seconds){for(double t=0;t<seconds;t+=.05)s.Tick(.05);}
        run("screen planner covers both axes and four regions without leaving bounds",()=>{
            var navigator=new ScreenNavigator(definition.Roaming,new Random(72));var seen=new HashSet<int>();double x=800,y=450,minX=x,maxX=x,minY=y,maxY=y;
            for(int i=0;i<4;i++){
                var plan=navigator.Plan(new MovementSpace(x,1600-x,y,900-y));Check(plan.HasValue);var leg=plan!.Value;seen.Add(leg.Quadrant);x+=leg.X;y+=leg.Y;
                Check(x>=0&&x<=1600&&y>=0&&y<=900);minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
            }
            Check(seen.Count==4 && maxX-minX>500 && maxY-minY>280);
        });
        run("roaming travels across the screen then schedules another leg",()=>{
            var s=New();s.SetMovementLimits(800,800,450,450);Check(s.SetRoaming(true));
            double x=0,y=0;for(int i=0;i<2000 && s.State==PetState.Walking;i++){s.Tick(.05);x+=s.MotionDeltaDip;y+=s.MotionDeltaYDip;}
            Check(Math.Abs(x)>200&&Math.Abs(y)>100 && s.State==PetState.Idle && s.RoamingEnabled && s.NextRoamSeconds>=1.2);
            Advance(s,3.2);Check(s.State==PetState.Walking && s.RequestedTripActive);
        });
        run("one requested trip returns to quiet mode without persistent roaming",()=>{
            var s=New();s.SetMovementLimits(700,700,400,400);Check(s.MoveAcrossScreenOnce());
            for(int i=0;i<2000&&s.State==PetState.Walking;i++)s.Tick(.05);
            Check(s.State==PetState.Idle && !s.RoamingEnabled && !s.RequestedTripActive && s.NextBehaviorSeconds>=20);
        });
        run("stop roaming cancels translation and restores quiet scheduling",()=>{
            var s=New();s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);Advance(s,1);Check(s.SetRoaming(false));
            Advance(s,5);Check(!s.RoamingEnabled&&s.State==PetState.Idle&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);
        });
        run("click cancels roaming leg and resumes only after reaction and rest",()=>{
            var s=New();s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);Check(s.React());
            s.Tick(.5);Check(s.State==PetState.Reacting&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);
            Advance(s,1);Check(s.State==PetState.Idle&&s.RoamingEnabled&&s.NextRoamSeconds>1);
            Advance(s,3.2);Check(s.State==PetState.Walking);
        });
        run("dragging, pause and menu preserve roam intention but prevent movement",()=>{
            var s=New();s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);s.BeginDrag();Advance(s,5);
            Check(s.State==PetState.Dragging&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0&&!s.SetRoaming(true));
            s.EndDrag();Check(s.NextRoamSeconds>=1.2);s.PauseAutonomy(true);Advance(s,10);Check(s.State==PetState.Paused&&s.RoamingEnabled);
            s.PauseAutonomy(false);Check(s.NextRoamSeconds>=1.2);s.OpenMenu();Advance(s,5);Check(!s.MoveAcrossScreenOnce());s.CloseMenu();Check(s.NextRoamSeconds>=1.2);
        });
        run("screen shrink and suspension cancel current route without teleporting",()=>{
            var s=New();s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);s.SetMovementLimits(0,0,0,0);s.StopAtBoundary();
            Advance(s,4);Check(s.State==PetState.Idle&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);
            s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);s.Tick(1000);Check(s.State==PetState.Idle&&s.NextRoamSeconds>=1.2);
        });
        run("tiny and one-dimensional screens do not create invalid navigation vectors",()=>{
            var navigator=new ScreenNavigator(definition.Roaming,new Random(3));Check(navigator.Plan(new MovementSpace(0,0,0,0))==null);
            for(int i=0;i<20;i++){var leg=navigator.Plan(new MovementSpace(0,0,100,100));Check(leg.HasValue&&leg.Value.X==0&&double.IsFinite(leg.Value.Duration));}
            try{(definition.Roaming with {SpeedDipPerSecond=double.NaN}).Validate();throw new Exception("accepted invalid navigation settings");}catch(InvalidDataException){}
        });
        run("three-hour roaming simulation stays bounded while reaching all four regions",()=>{
            var s=New(98);double x=400,y=250,minX=x,maxX=x,minY=y,maxY=y;s.SetMovementLimits(x,1600-x,y,900-y);s.SetRoaming(true);
            for(int i=0;i<108000;i++){
                s.SetMovementLimits(Math.Max(0,x),Math.Max(0,1600-x),Math.Max(0,y),Math.Max(0,900-y));s.Tick(.1);x+=s.MotionDeltaDip;y+=s.MotionDeltaYDip;
                Check(x>=-.001&&x<=1600.001&&y>=-.001&&y<=900.001);
                minX=Math.Min(minX,x);maxX=Math.Max(maxX,x);minY=Math.Min(minY,y);maxY=Math.Max(maxY,y);
            }
            Check(maxX-minX>1400&&maxY-minY>750);
        });
    }
}
