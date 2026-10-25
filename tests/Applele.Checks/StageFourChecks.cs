using Applele.Core;

internal static class StageFourChecks
{
    public static void Run(Action<string,Action> run)
    {
        void Check(bool value){if(!value)throw new InvalidOperationException("animation sampling assertion failed");}
        double Value(double phase)
        {
            var s=AnimationSampling.Sample(phase,12,true);
            return Math.Sin(s.First*Math.PI/6)*(1-s.Blend)+Math.Sin(s.Second*Math.PI/6)*s.Blend;
        }
        run("cached idle motion changes between key poses and crosses the loop seam continuously",()=>{
            Check(Math.Abs(Value(1.0/24)-.25)<1e-8);
            Check(Math.Abs(Value(1-1e-7)-Value(1+1e-7))<1e-5);
            Check(Math.Abs(Value(-.03)-Value(.97))<1e-8);
            Check(Math.Abs(Value(1000000.125)-Value(.125))<1e-8);
        });
        run("non-looped reactions retain the final pose rather than replaying",()=>{
            foreach(var phase in new[]{-4.0,0,.01,.5,.99,1,9})
            {
                var s=AnimationSampling.Sample(phase,16,false);
                var value=s.First*(1-s.Blend)+s.Second*s.Blend;
                Check(Math.Abs(value-Math.Clamp(phase,0,1)*15)<1e-8);
            }
        });
        run("invalid animation sampling cannot produce an unsafe bitmap index",()=>{
            foreach(var phase in new[]{double.NaN,double.NegativeInfinity,double.PositiveInfinity})
            {try{AnimationSampling.Sample(phase,12,true);throw new Exception("invalid phase accepted");}catch(ArgumentOutOfRangeException){}}
            foreach(var count in new[]{0,1,-3})
            {try{AnimationSampling.Sample(.2,count,true);throw new Exception("invalid cache accepted");}catch(ArgumentOutOfRangeException){}}
        });
    }
}
