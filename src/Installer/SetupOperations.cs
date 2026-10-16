using System.Diagnostics;
using DivineLabs.IdleSlayer.Maintenance;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Setup;
internal sealed class Receipt
{
    public string Product {get;set;}="DivineLabs.IdleSlayer";
    public string Version {get;set;}="0.5.0";
    public string[] Files {get;set;}=Array.Empty<string>();
}
[JsonSerializable(typeof(Receipt))]
internal partial class ReceiptJson:JsonSerializerContext{}
internal static class SetupOperations
{
    private const string OwnerFile="Data/Config/.divinelabs-owner",ReceiptFile="App/Setup/installation.json",Uninstaller="App/Setup/DivineLabs.Uninstall.exe";
    internal const string RegistryKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DivineLabs.IdleSlayer";
    internal static string DefaultDirectory=>InstallTarget.DefaultDirectory;
    internal static string Inside(string root,string relative)
    {
        if(Path.IsPathRooted(relative)||relative.Contains(':'))throw new InvalidDataException("Недопустимый путь пакета.");
        string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь выходит за пределы установки.");return path;
    }
    private static string ValidateTarget(string target)
    {
        string full=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if(Directory.GetParent(full)==null||full.Equals(Environment.GetFolderPath(Environment.SpecialFolder.Windows),StringComparison.OrdinalIgnoreCase)||File.Exists(Path.Combine(full,"Idle Slayer.exe")))throw new IOException("Выберите отдельную папку программы DivineLabs.");
        return full;
    }
    private static bool Owner(string root,string name)=>File.Exists(Inside(root,name))&&File.ReadAllText(Inside(root,name))=="DivineLabs.IdleSlayer";
    private static string ReceiptPath(string root)=>File.Exists(Inside(root,ReceiptFile))?Inside(root,ReceiptFile):Inside(root,"installation.json");
    private static IEnumerable<Process> Launchers(string root)
    {
        foreach(string name in new[]{"DivineLabs.IdleSlayer.Launcher","DivineLabs.IdleSlayer"})
        foreach(var process in Process.GetProcessesByName(name))
        {
            string file=process.MainModule?.FileName;
            if(string.Equals(file,Inside(root,"DivineLabs.IdleSlayer.exe"),StringComparison.OrdinalIgnoreCase)||
               string.Equals(file,Inside(root,"App/Launcher/DivineLabs.IdleSlayer.Launcher.exe"),StringComparison.OrdinalIgnoreCase))
                yield return process;
            else process.Dispose();
        }
    }
    internal static void Install(string target,bool integrations,Action<string,int> report,bool shortcuts=true,bool desktopShortcut=true,bool menuShortcut=true)
    {
        target=ValidateTarget(target);
        foreach(var process in Launchers(target)){process.Dispose();throw new IOException("Закройте лаунчер DivineLabs перед обновлением.");}
        if(InstallTarget.Inspect(target)==InstallTargetKind.Occupied)
            throw new IOException("В папке есть посторонние файлы. Выберите отдельную папку программы или существующую установку DivineLabs.");
        // Claim the validated destination before extraction, so any interrupted attempt is retryable.
        string owner=Inside(target,OwnerFile);Directory.CreateDirectory(Path.GetDirectoryName(owner)!);
        if(!File.Exists(owner))File.WriteAllText(owner,InstallTarget.ProductId);
        string stage=Inside(target,"Data/Cache/Work/setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        var files=new List<string>();
        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("DivineLabs.ApplicationPackage")??throw new IOException("В установщике нет пакета программы."))
        using(var zip=new ZipArchive(resource,ZipArchiveMode.Read))
        {
            long total=zip.Entries.Sum(e=>e.Length),done=0;
            foreach(var entry in zip.Entries)
            {
                string path=Inside(stage,entry.FullName);
                if(entry.FullName.StartsWith("Data/",StringComparison.OrdinalIgnoreCase)||((entry.ExternalAttributes>>16)&0xf000)==0xa000)throw new IOException("Неверное содержимое установочного пакета.");
                if(entry.FullName.EndsWith('/')){Directory.CreateDirectory(path);continue;}
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);entry.ExtractToFile(path,true);files.Add(entry.FullName);done+=entry.Length;report("Распаковка программы…",total>0?(int)(80*done/total):0);
            }
        }
        if(!File.Exists(Inside(stage,"DivineLabs.IdleSlayer.exe"))||!File.Exists(Inside(stage,"App/Launcher/DivineLabs.IdleSlayer.Launcher.exe")))
            throw new IOException("В пакете отсутствует лаунчер.");
        string history=Inside(target,"Data/Backups/Application/"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..6]);Directory.CreateDirectory(history);
        Directory.CreateDirectory(Path.GetDirectoryName(Inside(stage,Uninstaller))!);File.Copy(Environment.ProcessPath!,Inside(stage,Uninstaller));files.Add(Uninstaller);
        File.WriteAllText(Inside(stage,ReceiptFile),JsonSerializer.Serialize(new Receipt{Files=files.ToArray()},ReceiptJson.Default.Receipt));files.Add(ReceiptFile);
        Directory.CreateDirectory(Path.GetDirectoryName(Inside(stage,OwnerFile))!);File.WriteAllText(Inside(stage,OwnerFile),"DivineLabs.IdleSlayer");files.Add(OwnerFile);
        var legacyFiles=new HashSet<string>(StringComparer.OrdinalIgnoreCase){"installation.json",".divinelabs-owner","DivineLabs.Uninstall.exe"};
        string oldReceipt=ReceiptPath(target);
        if(File.Exists(oldReceipt))
        {
            var receipt=JsonSerializer.Deserialize(File.ReadAllText(oldReceipt),ReceiptJson.Default.Receipt);
            if(receipt?.Product!="DivineLabs.IdleSlayer")throw new IOException("Неверный список файлов прежней установки.");
            foreach(string file in receipt.Files){Inside(target,file);if(file.StartsWith("Data/",StringComparison.OrdinalIgnoreCase)||file.StartsWith(".application-history/",StringComparison.OrdinalIgnoreCase))throw new IOException("Неверный список файлов прежней установки.");if(!files.Contains(file,StringComparer.OrdinalIgnoreCase))legacyFiles.Add(file);}
        }
        // Older portable versions have no installer receipt. Move their known distribution files only.
        foreach(string file in new[]{"README.md","LICENSE","THIRD-PARTY-NOTICES.txt","DOTNET-LICENSE.txt","DOTNET-ThirdPartyNotices.txt","D3DCompiler_47_cor3.dll","PenImc_cor3.dll","PresentationNative_cor3.dll","vcruntime140_cor3.dll","wpfgfx_cor3.dll","DivineLabs.IdleSlayer.pdb","DivineLabs.IdleSlayer.Contracts.pdb","DivineLabs.IdleSlayer.Runtime.pdb"})legacyFiles.Add(file);
        report("Создание резервной копии и установка файлов…",82);
        var saved=new List<string>();var added=new List<string>();var movedDirectories=new List<string>();
        try
        {
            foreach(string directory in new[]{"payload","licenses",".application-history"})
            {
                string old=Inside(target,directory);
                if(!Directory.Exists(old))continue;
                if((File.GetAttributes(old)&FileAttributes.ReparsePoint)!=0)throw new IOException("Прежний каталог является ссылкой. Сохраните его отдельно перед обновлением.");
                Directory.Move(old,Inside(history,directory));movedDirectories.Add(directory);
            }
            foreach(string relative in legacyFiles)if(File.Exists(Inside(target,relative)))SavePrevious(relative);
            foreach(string relative in files)
            {
                string from=Inside(stage,relative),to=Inside(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if(File.Exists(to))SavePrevious(relative);
                File.Move(from,to);added.Add(relative);
            }
            report("Проверка установленного комплекта…",94);
            foreach(string relative in files)if(!File.Exists(Inside(target,relative)))throw new IOException("Не удалось проверить файл: "+relative);
            if(integrations){report("Создание ярлыков и регистрация…",97);try{Register(target,shortcuts&&desktopShortcut,shortcuts&&menuShortcut);}catch(Exception error){Log(target,error);}}
        }
        catch
        {
            foreach(string relative in added)if(File.Exists(Inside(target,relative)))File.Delete(Inside(target,relative));
            foreach(string relative in saved){Directory.CreateDirectory(Path.GetDirectoryName(Inside(target,relative))!);File.Move(Inside(history,relative),Inside(target,relative));}
            foreach(string directory in movedDirectories)Directory.Move(Inside(history,directory),Inside(target,directory));
            throw;
        }
        Directory.Delete(Inside(target,Path.GetRelativePath(target,stage)),true);report("DivineLabs установлен. Всё готово к запуску.",100);
        void SavePrevious(string relative){string backup=Inside(history,relative);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Move(Inside(target,relative),backup);saved.Add(relative);}
    }
    internal static void Log(string root,Exception error)
    {string logs=Inside(root,"Data/Logs");Directory.CreateDirectory(logs);File.AppendAllText(Path.Combine(logs,"setup.log"),$"{DateTime.UtcNow:O} {error}{Environment.NewLine}");}
    private static void Register(string target,bool desktopShortcut,bool menuShortcut)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RegistryKey);key.SetValue("DisplayName","DivineLabs Idle Slayer Mods");key.SetValue("DisplayVersion","0.5.0");key.SetValue("Publisher","DivineLabs");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",Inside(target,"DivineLabs.IdleSlayer.exe"));key.SetValue("UninstallString","\""+Inside(target,Uninstaller)+"\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
        if(!desktopShortcut&&!menuShortcut)return;
        string Q(string text)=>"'"+text.Replace("'","''")+"'";
        string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DivineLabs Idle Slayer Mods.lnk");
        string startMenu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"DivineLabs","IdleSlayer Mods.lnk");
        string selected=string.Join(",",new[]{desktopShortcut?desktop:null,menuShortcut?startMenu:null}.Where(p=>p!=null).Select(Q));
        string command="$ErrorActionPreference='Stop'; $shell=New-Object -ComObject WScript.Shell; foreach($file in @("+selected+")){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file))|Out-Null;$link=$shell.CreateShortcut($file);$link.TargetPath="+Q(Inside(target,"DivineLabs.IdleSlayer.exe"))+";$link.WorkingDirectory="+Q(target)+";$link.IconLocation="+Q(Inside(target,"DivineLabs.IdleSlayer.exe"))+";$link.Save()}";
        var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("-NoProfile");info.ArgumentList.Add("-NonInteractive");info.ArgumentList.Add("-EncodedCommand");info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));using var p=Process.Start(info)!;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Не удалось создать ярлыки. Программу можно открыть из папки установки.");
    }
    internal static void Launch(string target)=>Process.Start(new ProcessStartInfo(Inside(target,"DivineLabs.IdleSlayer.exe")){UseShellExecute=true,WorkingDirectory=target});
    internal static string UninstallRoot()
    {
        var directory=new DirectoryInfo(Path.GetDirectoryName(Environment.ProcessPath)!);
        return directory.Name.Equals("Setup",StringComparison.OrdinalIgnoreCase)&&directory.Parent?.Name.Equals("App",StringComparison.OrdinalIgnoreCase)==true?directory.Parent.Parent!.FullName:directory.FullName;
    }
    internal static void BeginUninstall(string root,bool filesOnly,bool silent)
    {
        root=ValidateTarget(root);if(!File.Exists(ReceiptPath(root)))throw new IOException("Установка DivineLabs не найдена.");
        foreach(var p in Launchers(root))using(p){if(p.MainWindowHandle!=0)p.CloseMainWindow();if(!p.WaitForExit(15000))throw new IOException("Закройте лаунчер DivineLabs перед удалением.");}
        if(!filesOnly)
        {
            var info=new ProcessStartInfo(Inside(root,"DivineLabs.IdleSlayer.exe")){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--uninstall-components");using var p=Process.Start(info)!;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Закройте игру перед удалением программы. Установка сохранена.");
        }
        string cache=Inside(root,"Data/Cache/Work");Directory.CreateDirectory(cache);
        string helper=Path.Combine(cache,"DivineLabs-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Environment.ProcessPath!,helper);
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=silent};foreach(string a in new[]{"--cleanup","--dir",root,"--parent",Environment.ProcessId.ToString()})start.ArgumentList.Add(a);if(filesOnly)start.ArgumentList.Add("--files-only");if(silent)start.ArgumentList.Add("--silent");Process.Start(start);
    }
    internal static void UninstallNow(string root,bool filesOnly,Action<string,int> progress=null)
    {
        root=ValidateTarget(root);
        if(!File.Exists(ReceiptPath(root)))throw new IOException("Установка DivineLabs не найдена.");
        foreach(var p in Launchers(root))using(p){if(p.MainWindowHandle!=0)p.CloseMainWindow();if(!p.WaitForExit(15000))throw new IOException("Закройте лаунчер DivineLabs перед удалением.");}
        if(!filesOnly){
            var info=new ProcessStartInfo(Inside(root,"DivineLabs.IdleSlayer.exe")){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--uninstall-components");
            using var process=Process.Start(info)!;process.WaitForExit();if(process.ExitCode!=0)throw new IOException("Закройте игру перед удалением программы.");
        }
        progress?.Invoke("Удаление файлов программы…",10);
        Cleanup(root,-1,filesOnly,progress);
    }
    internal static void Cleanup(string root,int parent,bool filesOnly,Action<string,int> progress=null)
    {
        if(parent>=0)try{using var p=Process.GetProcessById(parent);if(!p.WaitForExit(30000))throw new IOException("Программа удаления ещё работает.");}catch(ArgumentException){}
        root=ValidateTarget(root);string receiptPath=ReceiptPath(root);var receipt=JsonSerializer.Deserialize(File.ReadAllText(receiptPath),ReceiptJson.Default.Receipt);
        if(receipt?.Product!="DivineLabs.IdleSlayer"||receipt.Files.Any(p=>p.StartsWith("Data/",StringComparison.OrdinalIgnoreCase)||p.StartsWith(".application-history/",StringComparison.OrdinalIgnoreCase)))throw new IOException("Неверный список файлов установки.");
        var paths=receipt.Files.Select(p=>Inside(root,p)).ToArray();
        int removed=0;
        foreach(string path in paths){if(File.Exists(path))File.Delete(path);removed++;progress?.Invoke("Удаление файлов программы…",10+(int)(80d*removed/Math.Max(1,paths.Length)));}
        File.Delete(receiptPath);
        foreach(string folder in new[]{"App","Docs"})RemoveEmpty(Inside(root,folder));
        if(!filesOnly)
        {
            using var key=Registry.CurrentUser.OpenSubKey(RegistryKey);bool matches=string.Equals(key?.GetValue("InstallLocation") as string,root,StringComparison.OrdinalIgnoreCase);key?.Close();if(matches)Registry.CurrentUser.DeleteSubKeyTree(RegistryKey,false);
            foreach(string shortcut in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DivineLabs Idle Slayer Mods.lnk"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"DivineLabs","IdleSlayer Mods.lnk")})if(File.Exists(shortcut))File.Delete(shortcut);
        }
        progress?.Invoke("Проверка удаления…",94);
        if(paths.Any(File.Exists))throw new IOException("Не все файлы программы удалены. Закройте использующие их приложения и повторите.");
        // Data, ownership, application/game/save backups and unowned files are retained.
    }
    private static void RemoveEmpty(string folder)
    {
        if(!Directory.Exists(folder)||(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return;
        foreach(string child in Directory.GetDirectories(folder))RemoveEmpty(child);
        if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);
    }
}