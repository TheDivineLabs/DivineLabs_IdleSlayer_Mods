using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
public static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if(args.Contains("--maintenance")){new Application().Run(new MaintenanceWindow(args));return;}
        Product.MigrateData();
        if(args.Length>0)
        {
            try
            {
                int pos=Array.IndexOf(args,"--game");string game=pos>=0&&pos+1<args.Length?args[pos+1]:Installation.FindGame();
                switch(args[0])
                {
                    case "--verify-package":Payload.Verify(Payload.EnsureAsync().GetAwaiter().GetResult());return;
                    case "--prepare":case "--verify-install":Installation.EnsureReadyAsync(game,force:true,reuseCache:!args.Contains("--fresh")).GetAwaiter().GetResult();return;
                    case "--play":Installation.PlayAsync(game,false).GetAwaiter().GetResult();return;
                    case "--disable":case "--uninstall-components":Installation.Disable(game);return;
                    case "--restore":Installation.Restore(game);return;
                }
            }
            catch(Exception e){Product.Record(e);Environment.ExitCode=2;return;}
        }
        using var mutex=new Mutex(true,@"Local\DivineLabs.IdleSlayer.Launcher",out bool first);
        if(!first){foreach(var p in Process.GetProcessesByName("DivineLabs.IdleSlayer.Launcher"))if(p.Id!=Environment.ProcessId&&p.MainWindowHandle!=0){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);break;}return;}
        var app=new Application();app.DispatcherUnhandledException+=(_,e)=>{Product.Record(e.Exception);if(app.MainWindow is MainWindow main)main.ShowError(e.Exception);e.Handled=true;};app.Run(new MainWindow());
    }
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
