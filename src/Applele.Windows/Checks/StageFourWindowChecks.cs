using System.Diagnostics;
using System.Drawing.Imaging;
using System.Text.Json;
using Applele.Core;
using Applele.Windows.Animation;
using Applele.Windows.App;
using Applele.Windows.Assets;
using Applele.Windows.Windowing;

namespace Applele.Windows.Checks;

internal static class StageFourWindowChecks
{
    public static void Run(DesktopHost host,string root,string directory,Action<bool,string> check)
    {
        var output=Path.Combine(directory,"polish");Directory.CreateDirectory(output);
        using(var a=new Bitmap(2,2,PixelFormat.Format32bppPArgb))
        using(var b=new Bitmap(2,2,PixelFormat.Format32bppPArgb))
        using(var result=new Bitmap(2,2,PixelFormat.Format32bppPArgb))
        {
            b.SetPixel(0,0,Color.FromArgb(128,255,0,0));FrameMixer.Blend(result,a,b,.5);
            var bytes=FrameMixer.Capture(result);
            check(bytes[2]==64&&bytes[3]==64&&bytes[0]==0&&bytes[1]==0,"premultiplied interpolation preserves translucent red without a dark halo");
            check(bytes.Skip(4).All(x=>x==0),"interpolated transparent background remains exactly zero");
        }
        var session=host.Session;var renderer=host.Renderer;var scale=renderer.Scale;
        session.SetRoaming(false);session.PauseAutonomy(true);host.StartRuntime();
        var measurements=new List<object>();using var process=Process.GetCurrentProcess();
        var definitions=PetDefinition.Load(Path.Combine(root,"config","pet.json"));
        using var assets=new ResourceCatalog(root);
        foreach(var form in definitions.Forms.Where(x=>x.InIllustrationCycle))
        {
            session.SelectForm(form.Id);
            var clock=host.AnimationClockSeconds;var watch=Stopwatch.StartNew();host.Redraw();
            var build=renderer.LastPreparationMilliseconds;var wall=watch.Elapsed.TotalMilliseconds;var counted=(host.AnimationClockSeconds-clock)*1000;
            check(counted<=Math.Max(100,wall-build+60),"cache preparation does not consume animation time: "+form.Id);
            host.Step(.2);
            var signatures=new HashSet<string>();
            for(int i=0;i<6;i++)
            {
                host.Step(.04);var pixels=FrameMixer.Capture(renderer.Surface.Canvas);
                signatures.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(pixels)));
            }
            check(signatures.Count>=5,"idle has smooth intermediate poses instead of repeating sparse keyframes: "+form.Id);
            using(var sheet=new Bitmap(renderer.Surface.Width*2,renderer.Surface.Width,PixelFormat.Format32bppArgb))
            {
                using var g=Graphics.FromImage(sheet);g.Clear(Color.FromArgb(245,245,245));
                using var dark=new SolidBrush(Color.FromArgb(35,38,43));g.FillRectangle(dark,renderer.Surface.Width,0,renderer.Surface.Width,renderer.Surface.Width);
                g.DrawImageUnscaled(renderer.Surface.Canvas,0,0);g.DrawImageUnscaled(renderer.Surface.Canvas,renderer.Surface.Width,0);
                sheet.Save(Path.Combine(output,form.Id+"-light-dark.png"),ImageFormat.Png);
            }
            using(var cache=new PortraitRigCache(form,assets,definitions.CanvasSizeDip,2))
            using(var surface=new Bitmap(640,640,PixelFormat.Format32bppPArgb))
            {
                foreach(var clip in Enum.GetValues<PortraitClip>())
                {
                    cache.Sample(clip,.47).Draw(surface);var pixels=FrameMixer.Capture(surface);bool edge=true,premultiplied=true;
                    for(int y=0;y<640;y++)for(int x=0;x<640;x++)
                    {
                        var n=(y*640+x)*4;var alpha=pixels[n+3];
                        if((x==0||y==0||x==639||y==639)&&alpha!=0)edge=false;
                        if(pixels[n]>alpha||pixels[n+1]>alpha||pixels[n+2]>alpha)premultiplied=false;
                    }
                    check(edge&&premultiplied,"200 percent DPI clip has transparent edges and valid alpha: "+form.Id+"/"+clip);
                }
            }
            var cpu=process.TotalProcessorTime.TotalMilliseconds;var timer=Stopwatch.StartNew();
            while(timer.Elapsed.TotalSeconds<1){Application.DoEvents();Thread.Sleep(5);}
            process.Refresh();measurements.Add(new{id=form.Id,cachePreparationMilliseconds=build,idleSeconds=timer.Elapsed.TotalSeconds,cpuMilliseconds=process.TotalProcessorTime.TotalMilliseconds-cpu,workingSetMiB=process.WorkingSet64/1048576.0,gdi=NativeMethods.GetGuiResources(process.Handle,0)});
        }
        session.ReturnToPlush();host.Redraw();var initialGdi=NativeMethods.GetGuiResources(process.Handle,0);
        for(int i=0;i<8;i++){session.NextIllustration();host.Redraw();}
        session.ReturnToPlush();host.Redraw();var finalGdi=NativeMethods.GetGuiResources(process.Handle,0);
        check(finalGdi<=initialGdi+2,"repeated form switches release portrait GDI resources");
        File.WriteAllText(Path.Combine(output,"portrait-performance.json"),JsonSerializer.Serialize(new{scope="one second per form, not a long-term performance guarantee",measurements,initialGdi,finalGdi},PetDefinition.JsonOptions));
        renderer.Resize(scale);host.Park();session.PauseAutonomy(false);
    }
}
