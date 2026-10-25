using Applele.Core;

internal static class CharacterFrameChecks
{
    public static void Run(PetDefinition definition,Action<string,Action> run)
    {
        void Check(bool ok,string reason="character frame assertion failed"){if(!ok)throw new InvalidOperationException(reason);}
        var forms=definition.Forms.Where(x=>x.InIllustrationCycle).ToArray();
        run("all four portraits declare distinct authored walk and click atlases while preserving original cutouts",()=>{
            Check(forms.All(x=>x.Frames!=null));var textures=forms.SelectMany(x=>new[]{x.Frames!.Actions.Texture.File,x.Frames.Walk.Texture.File}.Concat(x.Frames.WalkContinuation==null?[]:new[]{x.Frames.WalkContinuation.Texture.File})).ToArray();
            Check(textures.Distinct().Count()==textures.Length&&textures.Length>=8&&textures.All(x=>x.StartsWith("resources/animations/")));
            Check(forms.All(x=>x.Idle.File.StartsWith("resources/sprites/")&&x.Frames!.WalkOrder.Length==8&&x.Frames.ReactionA.Length==4&&x.Frames.ReactionB.Length==4));
            Check(definition.Forms.Single(x=>x.Id=="plush").Frames==null);
        });
        run("held artwork cels show the last neutral recovery cel before a reaction finishes",()=>{
            var seen=new HashSet<int>();for(int i=0;i<100;i++){var frame=AnimationSampling.Sample(i/100.0,4,false,false);seen.Add(frame.First);Check(frame.First==frame.Second&&frame.Blend==0);}
            Check(seen.SetEquals([0,1,2,3])&&AnimationSampling.Sample(.99,4,false,false).First==3);
            Check(AnimationSampling.Sample(2,4,false,false).First==3&&AnimationSampling.Sample(-2,4,false,false).First==0);
        });
        run("eight walking cels loop in order without crossfading unrelated articulated limbs",()=>{
            var indices=Enumerable.Range(0,8).Select(i=>AnimationSampling.Sample((i+.1)/8,8,true,false).First);
            Check(indices.SequenceEqual(Enumerable.Range(0,8)));
            Check(AnimationSampling.Sample(1.01,8,true,false).First==0&&AnimationSampling.Sample(-.01,8,true,false).First==7);
        });
        run("frame configuration rejects missing, duplicated and out-of-range motion cels",()=>{
            var frameSet=new CharacterFrameSet{Actions=new(){Texture=new(){File="actions.png"}},Walk=new(){Texture=new(){File="walk.png"}}};frameSet.Validate();
            void Invalid(CharacterFrameSet candidate){try{candidate.Validate();}catch(InvalidDataException){return;}throw new Exception("invalid cels accepted");}
            Invalid(frameSet with{NeutralFrame=8});Invalid(frameSet with{WalkOrder=[0,1,2]});Invalid(frameSet with{ReactionB=[0,1,2,3]});
            Invalid(frameSet with{ReactionA=[0,1,1,1]});Invalid(frameSet with{Walk=frameSet.Walk with{Columns=0}});
        });
        run("original procedural portrait rendering remains an explicit configuration fallback",()=>{
            foreach(var form in forms)(form with{Frames=null}).Validate();
            var plush=definition.Forms.Single(x=>x.Id=="plush");
            try{(plush with{Frames=forms[0].Frames}).Validate();throw new Exception("plush accepted human animation frames");}catch(InvalidDataException){}
        });
    }
}
