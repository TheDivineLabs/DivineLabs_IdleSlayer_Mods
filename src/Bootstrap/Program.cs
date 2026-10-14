using System.Diagnostics;
using System.Runtime.InteropServices;
namespace DivineLabs.IdleSlayer.Bootstrap;
internal static class Program
{
    [STAThread] public static int Main(string[] args)
    {
        string root=Path.GetDirectoryName(Environment.ProcessPath)!;
        try
        {
            string launcher=Path.Combine(root,"App","Launcher","DivineLabs.IdleSlayer.Launcher.exe");
            if(!File.Exists(launcher))throw new IOException("Не найдено приложение DivineLabs. Распакуйте portable ZIP целиком или повторите установку.");
            var start=new ProcessStartInfo(launcher){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=root};
            foreach(string arg in args)start.ArgumentList.Add(arg);
            using var process=Process.Start(start)??throw new IOException("Не удалось запустить приложение DivineLabs.");
            process.WaitForExit();return process.ExitCode;
        }
        catch(Exception error)
        {
            try{string logs=Path.Combine(root,"Data","Logs");Directory.CreateDirectory(logs);File.AppendAllText(Path.Combine(logs,"bootstrap.log"),$"{DateTime.UtcNow:O} {error}{Environment.NewLine}");}catch{}
            if(args.Length==0)MessageBoxW(0,error is IOException?error.Message:"Не удалось запустить DivineLabs. Подробности: Data/Logs/bootstrap.log.","DivineLabs",0x10);
            return 2;
        }
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(IntPtr window,string text,string title,uint type);
}