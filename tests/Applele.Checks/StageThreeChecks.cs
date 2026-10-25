using Applele.Core;

internal static class StageThreeChecks
{
    public static void Run(PetDefinition definition,BehaviorCatalog behaviors,Action<string,Action> run)
    {
        void Check(bool ok,string reason="assertion failed"){if(!ok)throw new InvalidOperationException(reason);}
        var forms=definition.Forms.Where(x=>x.InIllustrationCycle).ToArray();
        run("four real forms bind distinct reference files, animation styles and personalities",()=>{
            Check(forms.Select(x=>x.Id).SequenceEqual(new[]{"ex1","ex2","alter-ex1","alter-ex2"}));
            Check(forms.Select(x=>x.Idle.File).SequenceEqual(new[]{"resources/sprites/03_ex1.png","resources/sprites/04_ex2.png","resources/sprites/05_alter_ex1.png","resources/sprites/06_alter_ex2.png"}));
            Check(forms.Select(x=>x.Animation.StyleId).Distinct().Count()==4 && forms.Select(x=>x.BehaviorProfileId).Distinct().Count()==4);
            Check(forms.All(x=>x.Animation.Rig=="portrait" && x.Reactions.Length==0 && x.Animation.Anchors.Length>=2));
        });
        run("production form cycle excludes plush, cancels actions and wraps to elite one",()=>{
            var s=new PetSession(definition,new Random(94),behaviors);
            for(int i=0;i<9;i++){s.React();Check(s.NextIllustration());Check(s.FormId==forms[i%4].Id && s.State==PetState.Idle);}
            s.ReturnToPlush();Check(s.FormId=="plush");
        });
        run("all portraits have finite local motion and distinct A/B feedback",()=>{
            var fingerprints=new HashSet<string>();
            foreach(var form in forms)
            {
                var phase=(form.Reaction.AnticipationSeconds+form.Reaction.ExpressionSeconds*.5)/form.Reaction.Duration;
                var a=PortraitMotion.Deform(form.Animation,form.Reaction,PortraitClip.ReactionA,phase,.5,.5,130,240);
                var b=PortraitMotion.Deform(form.Animation,form.Reaction,PortraitClip.ReactionB,phase,.5,.5,130,240);
                Check(Math.Abs(a.X-b.X)+Math.Abs(a.Y-b.Y)>.2);fingerprints.Add($"{a.X:F5},{a.Y:F5},{b.X:F5},{b.Y:F5}");
                foreach(var clip in Enum.GetValues<PortraitClip>())for(int t=0;t<=100;t++)
                {
                    var p=PortraitMotion.Deform(form.Animation,form.Reaction,clip,t/100.0,.4,.6,130,240);
                    Check(double.IsFinite(p.X)&&double.IsFinite(p.Y)&&Math.Abs(p.X+13)<25&&Math.Abs(p.Y+96)<35);
                }
            }
            Check(fingerprints.Count==4,"four forms shared the same animation");
        });
        run("portrait reaction endpoints return to the same neutral silhouette",()=>{
            foreach(var form in forms)foreach(var clip in new[]{PortraitClip.ReactionA,PortraitClip.ReactionB})foreach(var uv in new[]{(.2,.2),(.5,.5),(.8,.8)})
            {
                var first=PortraitMotion.Deform(form.Animation,form.Reaction,clip,0,uv.Item1,uv.Item2,130,240);
                var last=PortraitMotion.Deform(form.Animation,form.Reaction,clip,1,uv.Item1,uv.Item2,130,240);
                Check(Math.Abs(first.X-last.X)<.00001&&Math.Abs(first.Y-last.Y)<.00001);
            }
        });
        run("portrait switching preserves pause and roaming intention without stale movement",()=>{
            var s=new PetSession(definition,new Random(70),behaviors);s.SetMovementLimits(700,700,400,400);s.SetRoaming(true);s.SelectForm("ex1");
            Check(s.RoamingEnabled&&s.State==PetState.Idle&&s.RequestedTripActive==false&&s.NextRoamSeconds>=1.2);
            s.PauseAutonomy(true);s.NextIllustration();Check(s.FormId=="ex2"&&s.State==PetState.Paused&&s.MotionDeltaDip==0&&s.MotionDeltaYDip==0);
            s.PauseAutonomy(false);for(int i=0;i<70;i++)s.Tick(.05);Check(s.State==PetState.Walking&&s.FormId=="ex2");
        });
        run("form cooldowns are independent and quieter forms do not borrow plush hops",()=>{
            var s=new PetSession(definition,new Random(4),behaviors);s.SelectForm("ex1");s.DemoBehavior("short-walk");s.React();
            var cooldown=s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="short-walk").CooldownSeconds;Check(cooldown>=45);
            s.SelectForm("ex2");Check(s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="short-walk").CooldownSeconds==0&&!s.DemoBehavior("silly-hop"));
            s.SelectForm("ex1");Check(s.Scheduler!.Status(s.Time,PetState.Idle).Single(x=>x.Id=="short-walk").CooldownSeconds==cooldown);
            Check(forms.All(x=>behaviors.Get(x.BehaviorProfileId).RestWeight>behaviors.Get("playful-plush").RestWeight));
        });
        run("unknown form and malformed animation settings are rejected safely",()=>{
            var s=new PetSession(definition);var id=s.ActionId;Check(!s.SelectForm("missing")&&s.FormId=="plush"&&s.ActionId==id);
            void Invalid(FormAnimationSettings profile){try{profile.Validate();}catch(InvalidDataException){return;}throw new Exception("invalid animation accepted");}
            Invalid(forms[0].Animation with{WalkCycleSeconds=double.NaN});Invalid(forms[0].Animation with{Rig="unknown"});
            Invalid(forms[0].Animation with{Anchors=[new(){RadiusX=0}]});
        });
    }
}
