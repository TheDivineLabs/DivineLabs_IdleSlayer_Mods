using System.Diagnostics;
using DivineLabs.IdleSlayer.Maintenance;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
namespace DivineLabs.IdleSlayer.Setup;
internal static class Program
{
    [STAThread]public static int Main(string[] args)
    {
        string Arg(string name,string fallback=""){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        bool silent=args.Contains("--silent"),filesOnly=args.Contains("--files-only"),uninstall=args.Contains("--uninstall")||
            Path.GetFileName(Environment.ProcessPath).Equals("DivineLabs.Uninstall.exe",StringComparison.OrdinalIgnoreCase);
        string target=Arg("--dir",uninstall?SetupOperations.UninstallRoot():SetupOperations.DefaultDirectory),report=Arg("--report");
        void Report(string message,int percent) => OperationReport.TryWrite(report,message,percent);
        try{
            if(args.Contains("--cleanup")){SetupOperations.Cleanup(target,int.Parse(Arg("--parent")),filesOnly,Report);Report("Программа удалена.",100);return 0;}
            if(args.Contains("--uninstall-now")){Report("Отключение компонентов и удаление программы…",-1);SetupOperations.UninstallNow(target,filesOnly,Report);Report("Программа удалена.",100);return 0;}
            if(!silent)return RunInterface(target,uninstall);
            if(uninstall){SetupOperations.BeginUninstall(target,filesOnly,true,report);return 0;}
            SetupOperations.Install(target,!args.Contains("--no-integrations"),Report,!args.Contains("--no-shortcuts"),!args.Contains("--no-desktop-shortcut"),!args.Contains("--no-start-menu-shortcut"));
            if(!args.Contains("--no-launch"))SetupOperations.Launch(target);return 0;
        }catch(Exception e){try{SetupOperations.Log(target,e);Report("Ошибка: "+Explain(e),-1);}catch{}
            if(!silent)MessageBoxW(0,"Не удалось открыть интерфейс обслуживания. Подробности в Data/Logs/setup.log.","DivineLabs Idle Slayer Mods",0x10);return 2;
        }
    }
    private static string Explain(Exception error)
    {
        if(error is UnauthorizedAccessException)return "Нет доступа к выбранной папке. Выберите пользовательскую папку программы или проверьте права.";
        int code=error.HResult&0xffff;
        if(code is 32 or 33)return "Файл занят другой программой. Закройте лаунчер и повторите установку.";
        if(code==112)return "Недостаточно свободного места на выбранном диске.";
        if(error.Message.Length>0&&error.Message[0]>='А'&&error.Message[0]<='я')return error.Message;
        return "Не удалось завершить операцию. Откройте журнал текущей установки.";
    }
    private static int RunInterface(string target,bool uninstall)
    {
        string scratch=Directory.Exists(@"W:\DEV\.cache\temp")?@"W:\DEV\.cache\temp":Path.GetTempPath();
        CleanCompleted(scratch);
        string work=Path.Combine(scratch,"DivineLabs-maintenance-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(work);
        long requiredBytes=0;
        try{
            using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("DivineLabs.ApplicationPackage")??throw new IOException("В установщике нет пакета программы."))
            using(var zip=new ZipArchive(resource,ZipArchiveMode.Read)){
                requiredBytes=zip.Entries.Sum(entry=>entry.Length)+new FileInfo(Environment.ProcessPath!).Length;
                foreach(var entry in zip.Entries){
                    if(!entry.FullName.StartsWith("App/Launcher/",StringComparison.Ordinal)||entry.FullName.EndsWith('/'))continue;
                    string path=SetupOperations.Inside(work,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path);
                }
            }
            string worker=Path.Combine(work,"worker.exe");File.Copy(Environment.ProcessPath!,worker);
            var start=new ProcessStartInfo(Path.Combine(work,"App","Launcher","DivineLabs.IdleSlayer.Launcher.exe")){UseShellExecute=false,RedirectStandardError=!uninstall};
            foreach(string arg in new[]{"--maintenance","--worker",worker,"--dir",target,"--required-bytes",requiredBytes.ToString(System.Globalization.CultureInfo.InvariantCulture)})start.ArgumentList.Add(arg);
            if(uninstall)start.ArgumentList.Add("--uninstall");
            using var ui=Process.Start(start)??throw new IOException("Не удалось открыть интерфейс обслуживания.");if(uninstall)return 0;
            var diagnostic=ui.StandardError.ReadToEndAsync();ui.WaitForExit();
            string details=diagnostic.GetAwaiter().GetResult();
            if(ui.ExitCode!=0)throw new IOException("Интерфейс обслуживания завершился с ошибкой (код "+ui.ExitCode+")."+Environment.NewLine+details);
            return 0;
        }finally{
            // This directory is freshly created by this invocation and contains no user data.
            string full=Path.GetFullPath(work),parent=Path.GetFullPath(scratch).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
            if(!uninstall&&full.StartsWith(parent,StringComparison.OrdinalIgnoreCase)&&!Directory.EnumerateFileSystemEntries(work,"*",SearchOption.AllDirectories).Any(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)){
                try{Directory.Delete(full,true);}catch(IOException){}
            }
        }
    }
    private static void CleanCompleted(string scratch)
    {
        foreach(string path in Directory.GetDirectories(scratch,"DivineLabs-maintenance-*")){
            try{
                string name=Path.GetFileName(path);
                if(!Guid.TryParseExact(name["DivineLabs-maintenance-".Length..],"N",out _))continue;
                if(!File.Exists(Path.Combine(path,"maintenance.complete")))continue;
                string root=Path.GetFullPath(path),parent=Path.GetFullPath(scratch).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                if(!root.StartsWith(parent,StringComparison.OrdinalIgnoreCase)||(File.GetAttributes(root)&FileAttributes.ReparsePoint)!=0)continue;
                if(Directory.EnumerateFileSystemEntries(root,"*",SearchOption.AllDirectories).Any(p=>(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0))continue;
                Directory.Delete(root,true);
            }catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int MessageBoxW(IntPtr h,string text,string title,uint type);
}
