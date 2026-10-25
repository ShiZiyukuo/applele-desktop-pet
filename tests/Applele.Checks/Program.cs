using System.Text.Json;
using Applele.Core;

var results = new List<object>(); int failures = 0;
void Check(bool value, string reason = "assertion failed") { if (!value) throw new InvalidOperationException(reason); }
void Run(string name, Action test)
{
    try { test(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
    catch (Exception e) { failures++; results.Add(new { name, passed = false, error = e.Message }); Console.WriteLine("FAIL " + name + ": " + e.Message); }
}
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
var definition = PetDefinition.Load(Path.Combine(root, "config", "pet.json"));
PetSession New(int seed = 400) => new(definition, new Random(seed));
Run("default plush is separate from the four configured illustrations", () => { var s = New(); Check(s.FormId == "plush" && s.State == PetState.Idle && definition.Forms.Count(x=>x.InIllustrationCycle)==4); });
Run("reaction has anticipation, expression, recovery and returns to idle", () => {
    var s = New(); Check(s.React() && s.Phase == ReactionPhase.Anticipation);
    s.Tick(.17); Check(s.Phase == ReactionPhase.Expression); s.Tick(.85); Check(s.Phase == ReactionPhase.Recovery);
    s.Tick(.33); Check(s.State == PetState.Idle && s.Phase == ReactionPhase.None);
});
Run("reaction rejects every overlapping click without restarting", () => {
    var s = New(); s.React(); var id = s.ActionId; var reaction = s.ReactionIndex;
    for (int i = 0; i < 120; i++) { s.Tick(.01); Check(!s.React() && s.ActionId == id && s.ReactionIndex == reaction); }
});
Run("post-action cooldown permits subsequent independently randomized reactions", () => {
    var s = New(); var seen = new HashSet<int>();
    for (int i = 0; i < 100; i++) { Check(s.React()); seen.Add(s.ReactionIndex); s.Tick(1.4); Check(!s.React()); s.Tick(.31); }
    Check(seen.SetEquals([0,1]));
});
Run("drag cancels current action and never resumes stale reaction", () => {
    var s = New(); s.React(); s.BeginDrag(); var id = s.ActionId; s.Tick(10); Check(s.State == PetState.Dragging && !s.React());
    s.EndDrag(); Check(s.State == PetState.Idle && s.ActionId > id); s.Tick(20); Check(s.State == PetState.Idle);
});
Run("pausing autonomous movement still permits manual reactions", () => {
    var s = New(); s.PauseAutonomy(true); Check(s.State == PetState.Paused); Check(s.React());
    s.Tick(2); Check(s.State == PetState.Paused); s.BeginDrag(); s.EndDrag(); Check(s.State == PetState.Paused);
});
Run("menu cancels actions, blocks clicks and preserves user pause", () => {
    var s = New(); s.React(); s.OpenMenu(); Check(s.State == PetState.Paused && !s.React());
    s.CloseMenu(); Check(s.State == PetState.Idle); s.PauseAutonomy(true); s.OpenMenu(); s.CloseMenu(); Check(s.State == PetState.Paused);
});
Run("return-to-plush cancels reaction and drag state", () => {
    var s = New(); s.React(); s.ReturnToPlush(); Check(s.State == PetState.Idle && s.FormId == "plush");
    s.BeginDrag(); s.ReturnToPlush(); Check(s.State == PetState.Idle);
});
Run("illustration cycle excludes plush and loops four entries", () => {
    var all = definition with { Forms = [definition.Forms[0], .. Enumerable.Range(1,4).Select(i => definition.Forms[0] with { Id = "ex" + i, InIllustrationCycle = true })] };
    var s = new PetSession(all);
    for (int i = 0; i < 13; i++) { Check(s.NextIllustration()); Check(s.FormId == "ex" + (i % 4 + 1)); }
    s.ReturnToPlush(); Check(s.FormId == "plush");
});
Run("reaction envelope is continuous, finite and ends at original pose", () => {
    var r = definition.Forms[0].Reaction;
    foreach (int v in new[]{0,1}) {
        for (double t = 0; t < r.Duration + .1; t += .001) {
            var p = ReactionTimeline.Sample(r,t,v); Check(double.IsFinite(p.Rotation) && p.Mix >= 0 && p.Mix <= 1 && p.ScaleX > .8 && p.ScaleY > .8);
        }
        foreach (double cut in new[]{r.AnticipationSeconds,r.AnticipationSeconds+r.ExpressionSeconds,r.Duration}) {
            var a=ReactionTimeline.Sample(r,cut-.000001,v); var b=ReactionTimeline.Sample(r,cut+.000001,v);
            Check(Math.Abs(a.Mix-b.Mix)<.001 && Math.Abs(a.Lift-b.Lift)<.001 && Math.Abs(a.Rotation-b.Rotation)<.001);
        }
        Check(ReactionTimeline.Sample(r,r.Duration,v)==new ReactionPose(0,0,0,1,1));
    }
});
Run("configuration rejects invalid durations, names, duplicate forms and NaN", () => {
    void Invalid(Action a) { try { a(); } catch (InvalidDataException) { return; } throw new Exception("accepted invalid configuration"); }
    Invalid(() => (definition with {CanvasSizeDip=double.NaN}).Validate());
    Invalid(() => (definition with {Forms=[definition.Forms[0],definition.Forms[0]]}).Validate());
    Invalid(() => (definition.Forms[0] with {Reaction=new(){ExpressionSeconds=double.PositiveInfinity}}).Validate());
    Invalid(() => (definition.Forms[0] with {Reactions=[]}).Validate());
});
Run("invalid clock deltas are rejected", () => {
    var s=New(); foreach(var t in new[]{-.1,double.NaN,double.PositiveInfinity}) { try {s.Tick(t);throw new Exception("accepted invalid clock");}catch(ArgumentOutOfRangeException){} }
});
Run("long mixed-input simulation cannot deadlock or overlap actions", () => {
    var s=New(); var rng=new Random(42);
    for(int i=0;i<864000;i++) {
        s.Tick(.1); switch(rng.Next(1000)) {case 0:s.BeginDrag();break;case 1:s.EndDrag();break;case 2:s.PauseAutonomy(!s.AutonomyPaused);break;case 3:s.OpenMenu();s.CloseMenu();break;case 4:s.ReturnToPlush();break;default:if(i%13==0)s.React();break;}
        Check(Enum.IsDefined(s.State));
    }
});
StageTwoChecks.Run(definition,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")),Run);
RoamingChecks.Run(definition,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")),Run);
StageThreeChecks.Run(definition,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")),Run);
StageFourChecks.Run(Run);
PersonalityChecks.Run(definition,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")),Run);
CharacterFrameChecks.Run(definition,Run);
WalkPlaybackChecks.Run(definition,BehaviorCatalog.Load(Path.Combine(root,"config","behavior.json")),Run);
var summary = new { phase = 5, feature="authored AI character keyframes and autonomous personality",passed = results.Count - failures, failed = failures, behaviorSimulation=StageTwoChecks.SimulationStats,results };
var option = Array.IndexOf(args,"--report"); if(option>=0 && option+1<args.Length) {var path=Path.GetFullPath(args[option+1]);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,JsonSerializer.Serialize(summary,PetDefinition.JsonOptions));}
Console.WriteLine($"{results.Count-failures} passed / {failures} failed");
return failures==0?0:1;
