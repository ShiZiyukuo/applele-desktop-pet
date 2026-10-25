using System.Drawing.Imaging;
using System.Security.Cryptography;
using System.Text.Json;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.App;
using Applele.Windows.Assets;

namespace Applele.Windows.Checks;

internal static class CharacterFrameWindowChecks
{
    public static void Run(DesktopHost host,string root,string directory,Action<bool,string> check)
    {
        using var catalog=new ResourceCatalog(root);var output=Path.Combine(directory,"character-frames");Directory.CreateDirectory(output);
        var fixture=Path.Combine(output,"staggered-gutters-fixture.png");
        using(var bitmap=new Bitmap(256,256,PixelFormat.Format32bppArgb))
        {
            using var g=Graphics.FromImage(bitmap);g.Clear(Color.Transparent);using var brush=new SolidBrush(Color.DarkRed);
            foreach(var rectangle in new[]{new Rectangle(20,20,70,120),new Rectangle(160,20,70,80),new Rectangle(20,165,70,75),new Rectangle(160,125,70,115)})g.FillRectangle(brush,rectangle);
            bitmap.Save(fixture,ImageFormat.Png);
        }
        using(var atlas=new FrameAtlas(fixture,new(){Texture=new(){File="fixture.png"},Columns=2,Rows=2,UseTransparentGutters=true}))
            check(atlas.Cels.Count==4&&atlas.Cels[0].Content.Height>110&&atlas.Cels[3].Content.Height>110,"staggered atlas rows preserve whole cels using column-first transparent gutters");
        var reports=new List<object>();
        foreach(var form in catalog.Definition.Forms.Where(x=>x.Frames!=null))
        {
            var settings=form.Frames!;var formOutput=Path.Combine(output,form.Id);Directory.CreateDirectory(formOutput);
            using var actions=new FrameAtlas(catalog.Resolve(settings.Actions.Texture),settings.Actions);
            using var walk=new FrameAtlas(catalog.Resolve(settings.Walk.Texture),settings.Walk);
            using var continuation=settings.WalkContinuation==null?null:new FrameAtlas(catalog.Resolve(settings.WalkContinuation.Texture),settings.WalkContinuation);
            check(actions.Cels.Count==8&&walk.Cels.Count+(continuation?.Cels.Count??0)==8,"authored action and walk textures contain eight clean separate cels: "+form.Id);
            using var cache=new PortraitRigCache(form,catalog,catalog.Definition.CanvasSizeDip,1.25);
            var hashes=new HashSet<string>();var legs=new HashSet<string>();
            using(var sheet=new Bitmap(4*400,2*400,PixelFormat.Format32bppArgb))
            {
                using var g=Graphics.FromImage(sheet);g.Clear(Color.FromArgb(38,41,46));
                for(int i=0;i<8;i++)
                {
                    using var image=new Bitmap(400,400,PixelFormat.Format32bppPArgb);cache.Sample(PortraitClip.Walk,(i+.1)/8).Draw(image);
                    image.Save(Path.Combine(formOutput,$"walk-{i:00}.png"),ImageFormat.Png);g.DrawImageUnscaled(image,(i%4)*400,(i/4)*400);
                    var bytes=FrameMixer.Capture(image);hashes.Add(Convert.ToHexString(SHA256.HashData(bytes)));
                    using var lower=image.Clone(new Rectangle(0,235,400,125),PixelFormat.Format32bppPArgb);legs.Add(Convert.ToHexString(SHA256.HashData(FrameMixer.Capture(lower))));
                    bool valid=true;for(int y=0;y<400;y++)for(int x=0;x<400;x++)
                    {
                        int n=(y*400+x)*4;var a=bytes[n+3];
                        if(((x==0||y==0||x==399||y==399)&&a!=0)||bytes[n]>a||bytes[n+1]>a||bytes[n+2]>a)valid=false;
                    }
                    check(valid,"authored walk cel keeps transparent canvas edges and valid alpha: "+form.Id+"/"+i);
                }
                sheet.Save(Path.Combine(formOutput,"walk-contact-sheet.png"),ImageFormat.Png);
            }
            check(hashes.Count>=6&&legs.Count>=6,"walking genuinely changes lower-body artwork across the cycle: "+form.Id);
            var reference=actions.ReferenceHeight(settings.NeutralFrame);
            var neutralHeights=new[]{0,3,4,7}.Select(actions.ReferenceHeight).ToArray();
            check(neutralHeights.All(x=>Math.Abs(x/reference-1)<.1),"neutral action cels retain comparable body scale: "+form.Id);
            using(var sheet=new Bitmap(4*400,2*400,PixelFormat.Format32bppArgb))
            {
                using var g=Graphics.FromImage(sheet);g.Clear(Color.FromArgb(246,246,246));
                for(int i=0;i<8;i++)
                {
                    using var frame=actions.Render(i,reference,320,form.Animation.SpriteSizeDip,1.25);
                    frame.Save(Path.Combine(formOutput,$"action-{i:00}.png"),ImageFormat.Png);g.DrawImageUnscaled(frame,(i%4)*400,(i/4)*400);
                }
                sheet.Save(Path.Combine(formOutput,"action-contact-sheet.png"),ImageFormat.Png);
            }
            host.Session.SelectForm(form.Id);host.Redraw();host.Step(.2);var position=host.Window.Location;
            for(int variant=0;variant<2;variant++)
            {
                bool selected=false;for(int attempt=0;attempt<100;attempt++){host.Session.Tick(2);host.Session.React();if(host.Session.ReactionIndex==variant){selected=true;break;}}
                check(selected,"authored click sequence is reachable: "+form.Id+"/"+variant);host.Redraw();host.Step(.35);
                check(host.Session.State==PetState.Reacting&&host.Window.Location==position,"authored click retains exclusive state ownership without moving the window: "+form.Id+"/"+variant);
                host.Session.Tick(form.Reaction.Duration);host.Redraw();check(host.Session.State==PetState.Idle,"authored click recovers to idle: "+form.Id+"/"+variant);
            }
            reports.Add(new{id=form.Id,provenance=settings.Provenance,walkFrames=8,reactionAFrames=4,reactionBFrames=4,distinctWalkImages=hashes.Count,distinctLegImages=legs.Count,neutralHeights,
                actions=actions.Cels.Select(c=>new{bodyX=c.BodyX,groundY=c.GroundY,width=c.Content.Width,height=c.Content.Height}),walk=walk.Cels.Concat(continuation?.Cels??[]).Select(c=>new{bodyX=c.BodyX,groundY=c.GroundY,width=c.Content.Width,height=c.Content.Height})});
        }
        check(reports.Count>0,"at least one form uses actual redrawn character cels");
        File.WriteAllText(Path.Combine(output,"atlas-verification.json"),JsonSerializer.Serialize(reports,PetDefinition.JsonOptions));host.Session.ReturnToPlush();host.Park();
    }
}
