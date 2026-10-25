using System.Text.Json;
using Applele.Windows.App;
using Applele.Windows.Windowing;

namespace Applele.Windows;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        var checks = args.Contains("--check")||args.Contains("--check-art")||args.Contains("--check-walk");
        using var instance = new Mutex(true, checks ? "Local\\Applele.Rewrite.Stage1.Checks" : "Local\\Applele.Rewrite.Desktop", out var first);
        if (!first) { if (!checks) MessageBox.Show("苹果乐已经运行，可在托盘菜单中找回或退出。", "苹果乐"); return 2; }
        var root = AppContext.BaseDirectory;
        var rootOption = Array.IndexOf(args, "--resource-root"); if (rootOption >= 0 && rootOption + 1 < args.Length) root = Path.GetFullPath(args[rootOption + 1]);
        try
        {
            var previousForeground = NativeMethods.GetForegroundWindow();
            using var host = new DesktopHost(root, checks);
            if (checks)
            {
                var reportOption = Array.IndexOf(args, "--report");
                var report = reportOption >= 0 && reportOption + 1 < args.Length ? Path.GetFullPath(args[reportOption + 1]) : Path.Combine(root, "checks");
                if(args.Contains("--check-art")||args.Contains("--check-walk"))
                {
                    var results=new List<object>();
                    try
                    {
                        Directory.CreateDirectory(report);
                        void Check(bool ok,string name){if(!ok)throw new InvalidOperationException(name);results.Add(new{name,passed=true});}
                        if(args.Contains("--check-walk"))Checks.WalkPlaybackWindowChecks.Run(host,root,report,Check);
                        else Checks.CharacterFrameWindowChecks.Run(host,root,report,Check);
                        File.WriteAllText(Path.Combine(report,"art-frame-checks.json"),JsonSerializer.Serialize(new{passed=results.Count,failed=0,results},Applele.Core.PetDefinition.JsonOptions));
                    }
                    finally{host.Window.Close();}
                }
                else Checks.StageOneChecks.Run(host, report, previousForeground);
                return 0;
            }
            if(args.Contains("--demo-walk"))host.Window.BeginInvoke(()=>host.DemoBehavior("short-walk"));
            if(args.Contains("--roam"))host.Window.BeginInvoke(()=>host.SetRoaming(true));
            Application.Run(host); return 0;
        }
        catch (Exception e)
        {
            var error = JsonSerializer.Serialize(new { error = e.GetType().Name, e.Message, detail = e.ToString() }, new JsonSerializerOptions { WriteIndented = true });
            try { File.WriteAllText(Path.Combine(root, "startup-error.json"), error); } catch (Exception writeError) when(writeError is IOException or UnauthorizedAccessException) { }
            if (!checks) MessageBox.Show("桌宠未能启动或发生错误：\n" + e.Message + "\n\n可以检查 startup-error.json。", "苹果乐", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
