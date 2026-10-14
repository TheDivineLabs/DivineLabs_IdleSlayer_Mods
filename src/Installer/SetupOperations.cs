using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Setup;
internal sealed class Receipt
{
    public string Product {get;set;}="DivineLabs.IdleSlayer";
    public string Version {get;set;}="0.2.0";
    public string[] Files {get;set;}=Array.Empty<string>();
}
[JsonSerializable(typeof(Receipt))]
internal partial class ReceiptJson:JsonSerializerContext{}
internal static class SetupOperations
{
    private const string OwnerFile=".divinelabs-owner";
    internal const string RegistryKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DivineLabs.IdleSlayer";
    internal static string DefaultDirectory=>Directory.Exists(@"W:\DEV")?@"W:\DEV\tools\DivineLabs\IdleSlayer":Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DivineLabs","IdleSlayer");
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
    internal static void Install(string target,bool integrations,Action<string,int> report)
    {
        target=ValidateTarget(target);
        foreach(var p in Process.GetProcessesByName("DivineLabs.IdleSlayer"))
            if(string.Equals(p.MainModule?.FileName,Path.Combine(target,"DivineLabs.IdleSlayer.exe"),StringComparison.OrdinalIgnoreCase))throw new IOException("Закройте лаунчер DivineLabs перед обновлением.");
        if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any()&&!File.Exists(Path.Combine(target,"DivineLabs.IdleSlayer.exe"))&&!File.Exists(Path.Combine(target,"installation.json"))&&!(File.Exists(Path.Combine(target,OwnerFile))&&File.ReadAllText(Path.Combine(target,OwnerFile))=="DivineLabs.IdleSlayer"))throw new IOException("Выберите пустую папку или существующую установку DivineLabs.");
        Directory.CreateDirectory(target);string stage=Inside(target,".setup-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
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
        if(!File.Exists(Path.Combine(stage,"DivineLabs.IdleSlayer.exe")))throw new IOException("В пакете отсутствует лаунчер.");
        // Preserve application updates separately from game/save backups.
        string history=Inside(target,".application-history/"+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(history);
        files.Add("DivineLabs.Uninstall.exe");File.Copy(Environment.ProcessPath!,Inside(stage,"DivineLabs.Uninstall.exe"));
        File.WriteAllText(Inside(stage,OwnerFile),"DivineLabs.IdleSlayer");files.Add(OwnerFile);
        File.WriteAllText(Inside(stage,"installation.json"),JsonSerializer.Serialize(new Receipt{Files=files.Where(p=>p!=OwnerFile).ToArray()},ReceiptJson.Default.Receipt));
        files.Add("installation.json");
        string oldPayload=Inside(target,"payload/MelonLoader"),savedPayload=Inside(history,"payload/MelonLoader");bool legacyMoved=false;
        var saved=new List<string>();var added=new List<string>();
        try
        {
            if(Directory.Exists(oldPayload)){Directory.CreateDirectory(Path.GetDirectoryName(savedPayload)!);Directory.Move(oldPayload,savedPayload);legacyMoved=true;}
            foreach(string relative in files)
            {
                string from=Inside(stage,relative),to=Inside(target,relative);Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                if(File.Exists(to)){string backup=Inside(history,relative);Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Move(to,backup);saved.Add(relative);}
                File.Move(from,to);added.Add(relative);
            }
            if(integrations){try{Register(target);}catch(Exception error){Directory.CreateDirectory(Path.Combine(target,"Data"));File.AppendAllText(Path.Combine(target,"Data","setup.log"),error+Environment.NewLine);}}
        }
        catch
        {
            foreach(string relative in added)if(File.Exists(Inside(target,relative)))File.Delete(Inside(target,relative));
            foreach(string relative in saved)File.Move(Inside(history,relative),Inside(target,relative));if(legacyMoved)Directory.Move(savedPayload,oldPayload);throw;
        }
        Directory.Delete(Inside(target,Path.GetFileName(stage)),true);report("DivineLabs установлен. Всё готово к запуску.",100);
    }
    private static void Register(string target)
    {
        using var key=Registry.CurrentUser.CreateSubKey(RegistryKey);key.SetValue("DisplayName","DivineLabs IdleSlayer Mods");key.SetValue("DisplayVersion","0.2.0");key.SetValue("Publisher","DivineLabs");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",Path.Combine(target,"DivineLabs.IdleSlayer.exe"));key.SetValue("UninstallString","\""+Path.Combine(target,"DivineLabs.Uninstall.exe")+"\" --uninstall");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);
        string Q(string text)=>"'"+text.Replace("'","''")+"'";
        string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DivineLabs IdleSlayer Mods.lnk");
        string startMenu=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"DivineLabs","IdleSlayer Mods.lnk");
        string command="$ErrorActionPreference='Stop'; $shell=New-Object -ComObject WScript.Shell; foreach($file in @("+Q(desktop)+","+Q(startMenu)+")){[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($file))|Out-Null;$link=$shell.CreateShortcut($file);$link.TargetPath="+Q(Path.Combine(target,"DivineLabs.IdleSlayer.exe"))+";$link.WorkingDirectory="+Q(target)+";$link.IconLocation="+Q(Path.Combine(target,"DivineLabs.IdleSlayer.exe"))+";$link.Save()}";
        var info=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("-NoProfile");info.ArgumentList.Add("-NonInteractive");info.ArgumentList.Add("-EncodedCommand");info.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));using var p=Process.Start(info)!;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Не удалось создать ярлыки. Программу можно открыть из папки установки.");
    }
    internal static void Launch(string target)=>Process.Start(new ProcessStartInfo(Path.Combine(target,"DivineLabs.IdleSlayer.exe")){UseShellExecute=true,WorkingDirectory=target});
    internal static void BeginUninstall(string root,bool filesOnly,bool silent)
    {
        root=ValidateTarget(root);if(!File.Exists(Path.Combine(root,"installation.json")))throw new IOException("Установка DivineLabs не найдена.");
        foreach(var p in Process.GetProcessesByName("DivineLabs.IdleSlayer"))if(string.Equals(p.MainModule?.FileName,Path.Combine(root,"DivineLabs.IdleSlayer.exe"),StringComparison.OrdinalIgnoreCase)){p.CloseMainWindow();if(!p.WaitForExit(15000))throw new IOException("Закройте лаунчер DivineLabs перед удалением.");}
        if(!filesOnly)
        {var info=new ProcessStartInfo(Path.Combine(root,"DivineLabs.IdleSlayer.exe")){UseShellExecute=false,CreateNoWindow=true};info.ArgumentList.Add("--uninstall-components");using var p=Process.Start(info)!;p.WaitForExit();if(p.ExitCode!=0)throw new IOException("Закройте игру перед удалением программы. Установка сохранена.");}
        string cache=Directory.Exists(@"W:\DEV")?@"W:\DEV\.cache\temp":Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DivineLabs","Cache");Directory.CreateDirectory(cache);
        string helper=Path.Combine(cache,"DivineLabs-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Environment.ProcessPath!,helper);
        var start=new ProcessStartInfo(helper){UseShellExecute=false,CreateNoWindow=silent};foreach(string a in new[]{"--cleanup","--dir",root,"--parent",Environment.ProcessId.ToString()})start.ArgumentList.Add(a);if(filesOnly)start.ArgumentList.Add("--files-only");if(silent)start.ArgumentList.Add("--silent");Process.Start(start);
    }
    internal static void Cleanup(string root,int parent,bool filesOnly)
    {
        try{using var p=Process.GetProcessById(parent);if(!p.WaitForExit(30000))throw new IOException("Программа удаления ещё работает.");}catch(ArgumentException){}
        root=ValidateTarget(root);var receipt=JsonSerializer.Deserialize(File.ReadAllText(Path.Combine(root,"installation.json")),ReceiptJson.Default.Receipt);
        if(receipt?.Product!="DivineLabs.IdleSlayer")throw new IOException("Неверный список файлов установки.");
        if(receipt.Files.Any(p=>p.StartsWith("Data/",StringComparison.OrdinalIgnoreCase)||p.StartsWith(".application-history/",StringComparison.OrdinalIgnoreCase)))throw new IOException("Неверный список файлов установки.");
        var paths=receipt.Files.Select(p=>Inside(root,p)).ToArray();
        foreach(string path in paths)if(File.Exists(path))File.Delete(path);
        File.Delete(Path.Combine(root,"installation.json"));
        if(!filesOnly)
        {
            using var key=Registry.CurrentUser.OpenSubKey(RegistryKey);bool matches=string.Equals(key?.GetValue("InstallLocation") as string,root,StringComparison.OrdinalIgnoreCase);key?.Close();if(matches)Registry.CurrentUser.DeleteSubKeyTree(RegistryKey,false);
            foreach(string shortcut in new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"DivineLabs IdleSlayer Mods.lnk"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"DivineLabs","IdleSlayer Mods.lnk")})if(File.Exists(shortcut))File.Delete(shortcut);
        }
        // Retain Data and application/game/save backups; only owned binaries are removed.
    }
}
