using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Launcher;
internal static class Installation
{
    internal static readonly string Home=AppContext.BaseDirectory;
    internal static readonly string Data=Path.Combine(Home,"Data");
    private static readonly string[] Names={"MelonLoader","Mods","Plugins","UserLibs","UserData","version.dll"};
    internal static bool Running => Process.GetProcessesByName("Idle Slayer").Length>0;
    internal static string FindGame()
    {
        string saved=Path.Combine(Data,"game-path.txt");if(File.Exists(saved)&&ValidGame(File.ReadAllText(saved)))return File.ReadAllText(saved);
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase){@"W:\steam",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam")};
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(var key in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"})
        {using var k=hive.OpenSubKey(key);if(k?.GetValue("SteamPath") is string s)roots.Add(s);if(k?.GetValue("InstallPath") is string p)roots.Add(p);}
        foreach(string root in roots.ToArray())
        { string file=Path.Combine(root,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace("\\\\","\\")); }
        return roots.Select(p=>Path.Combine(p,"steamapps","common","Idle Slayer")).FirstOrDefault(ValidGame)??"";
    }
    internal static bool ValidGame(string p)=>File.Exists(Path.Combine(p,"Idle Slayer.exe"))&&File.Exists(Path.Combine(p,"GameAssembly.dll"));
    internal static string Config(string game)
    {string p=Path.Combine(game,"UserData");if(!Directory.Exists(p)&&Directory.Exists(Path.Combine(game,".divinelabs-disabled","UserData")))p=Path.Combine(game,".divinelabs-disabled","UserData");return Path.Combine(p,SettingsStore.FileName);}
    internal static void RequireStopped(){if(Running)throw new InvalidOperationException("Закройте игру штатно перед изменением установки или настроек.");}
    private static void CopyTree(string source,string target)
    {Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);foreach(string dir in Directory.GetDirectories(source))CopyTree(dir,Path.Combine(target,Path.GetFileName(dir)));}
    private static void MoveEntry(string source,string target)
    {Directory.CreateDirectory(Path.GetDirectoryName(target)!);if(Directory.Exists(source))Directory.Move(source,target);else if(File.Exists(source))File.Move(source,target);}
    private static void Junction(string path,string target)
    {
        string Quote(string s)=>"'"+s.Replace("'","''")+"'";
        string command="$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path "+Quote(path)+" -Target "+Quote(target)+" | Out-Null";
        var start=new ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardError=true};
        start.ArgumentList.Add("-NoProfile");start.ArgumentList.Add("-NonInteractive");start.ArgumentList.Add("-EncodedCommand");start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(command)));
        using var process=Process.Start(start)!;string error=process.StandardError.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new IOException("Не удалось подключить компоненты: "+error);
    }
    internal static bool Installed(string game)=>File.Exists(Path.Combine(game,"Mods","DivineLabs.IdleSlayer.Mod.dll"));
    internal static void Install(string game)
    {
        RequireStopped();if(!Compatibility.Supported(game))throw new InvalidOperationException("Эта версия игры не проверена. Доступен обычный запуск.");
        string payload=Path.Combine(Home,"payload");var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(payload,"manifest.json")))!;
        foreach(var entry in manifest){string file=Path.GetFullPath(Path.Combine(payload,entry.Key));if(!file.StartsWith(Path.GetFullPath(payload)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Недопустимый путь пакета.");using var stream=File.OpenRead(file);if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))!=entry.Value)throw new InvalidDataException("Повреждён файл пакета: "+entry.Key);}
        Enable(game);
        string runtime=Path.Combine(Data,"runtime",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));
        CopyTree(Path.Combine(payload,"MelonLoader"),Path.Combine(runtime,"MelonLoader"));CopyTree(Path.Combine(payload,"Mods"),Path.Combine(runtime,"Mods"));CopyTree(Path.Combine(payload,"UserLibs"),Path.Combine(runtime,"UserLibs"));
        foreach(string name in new[]{"Plugins","UserLibs","UserData"})Directory.CreateDirectory(Path.Combine(runtime,name));
        if(Directory.Exists(Path.Combine(game,"UserData")))CopyTree(Path.Combine(game,"UserData"),Path.Combine(runtime,"UserData"));
        // Local generated assemblies stay local, never in the distributable.
        string generated=Path.Combine(game,"MelonLoader","Il2CppAssemblies");if(Directory.Exists(generated))CopyTree(generated,Path.Combine(runtime,"MelonLoader","Il2CppAssemblies"));
        string oldGenerator=Path.Combine(game,"MelonLoader","Dependencies","Il2CppAssemblyGenerator");if(Directory.Exists(oldGenerator))
        {foreach(string name in new[]{"Cpp2IL","UnityDependencies"})if(Directory.Exists(Path.Combine(oldGenerator,name)))CopyTree(Path.Combine(oldGenerator,name),Path.Combine(runtime,"MelonLoader","Dependencies","Il2CppAssemblyGenerator",name));}
        foreach(string config in new[]{"Config.cfg","AssemblyGenerationInfo.json","config.json"}){string path=Path.Combine(oldGenerator,config);if(File.Exists(path))File.Copy(path,Path.Combine(runtime,"MelonLoader","Dependencies","Il2CppAssemblyGenerator",config),true);}
        string settings=Path.Combine(runtime,"UserData",SettingsStore.FileName);SettingsStore.Load(settings);
        string loaderConfig=Path.Combine(runtime,"UserData","Loader.cfg");if(File.Exists(loaderConfig))File.WriteAllText(loaderConfig,File.ReadAllText(loaderConfig).Replace("hide_console = false","hide_console = true"));
        BackupSaves();
        string baseline=Path.Combine(game,".divinelabs-history",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(baseline);
        var moved=new List<string>();var added=new List<string>();
        try
        {
            foreach(string name in Names){string from=Path.Combine(game,name);if(Directory.Exists(from)||File.Exists(from)){MoveEntry(from,Path.Combine(baseline,name));moved.Add(name);}}
            foreach(string name in Names.Where(n=>n!="version.dll")){Junction(Path.Combine(game,name),Path.Combine(runtime,name));added.Add(name);}
            File.Copy(Path.Combine(payload,"version.dll"),Path.Combine(game,"version.dll"));added.Add("version.dll");
            Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"game-path.txt"),game);File.WriteAllText(Path.Combine(Data,"previous-installation.txt"),baseline);
        }
        catch
        {foreach(string name in added){string p=Path.Combine(game,name);if(Directory.Exists(p))Directory.Delete(p);else if(File.Exists(p))File.Delete(p);}foreach(string name in moved)MoveEntry(Path.Combine(baseline,name),Path.Combine(game,name));throw;}
    }
    internal static void BackupSaves()
    {string source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Pablo Leban","Idle Slayer");if(!Directory.Exists(source))return;string to=Path.Combine(Data,"save-backups",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(to);foreach(string file in Directory.GetFiles(source,"*.sav"))File.Copy(file,Path.Combine(to,Path.GetFileName(file)));}
    internal static void Disable(string game)
    {
        RequireStopped();string target=Path.Combine(game,".divinelabs-disabled");
        foreach(string name in Names){if((File.Exists(Path.Combine(target,name))||Directory.Exists(Path.Combine(target,name)))&&(File.Exists(Path.Combine(game,name))||Directory.Exists(Path.Combine(game,name))))throw new IOException("Есть сохранённая отключённая установка. Сначала восстановите её.");}
        Directory.CreateDirectory(target);var moved=new List<string>();try{foreach(string name in Names)if(Directory.Exists(Path.Combine(game,name))||File.Exists(Path.Combine(game,name))){MoveEntry(Path.Combine(game,name),Path.Combine(target,name));moved.Add(name);}}catch{foreach(string name in moved)MoveEntry(Path.Combine(target,name),Path.Combine(game,name));throw;}
    }
    internal static void Enable(string game)
    {
        RequireStopped();string disabled=Path.Combine(game,".divinelabs-disabled");
        if(!Directory.Exists(disabled)||!Names.Any(n=>Exists(Path.Combine(disabled,n))))return;
        foreach(string name in Names)if(Exists(Path.Combine(game,name)))throw new IOException("Установка уже подключена.");
        var moved=new List<string>();
        try { foreach(string name in Names)if(Exists(Path.Combine(disabled,name))){MoveEntry(Path.Combine(disabled,name),Path.Combine(game,name));moved.Add(name);} }
        catch {foreach(string name in moved)MoveEntry(Path.Combine(game,name),Path.Combine(disabled,name));throw;}
    }
    private static bool Exists(string path)=>Directory.Exists(path)||File.Exists(path);
    internal static void Restore(string game)
    {
        RequireStopped();string record=Path.Combine(Data,"previous-installation.txt");
        string baseline=File.ReadAllText(record);string root=Path.GetFullPath(Path.Combine(game,".divinelabs-history"))+Path.DirectorySeparatorChar;
        if(!Path.GetFullPath(baseline).StartsWith(root,StringComparison.OrdinalIgnoreCase)||!Directory.Exists(baseline))throw new IOException("Резервная установка не найдена.");
        Enable(game);
        string current=Path.Combine(root,DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(current);
        var saved=new List<string>();var restored=new List<string>();
        try
        {
            foreach(string name in Names)if(Exists(Path.Combine(game,name))){MoveEntry(Path.Combine(game,name),Path.Combine(current,name));saved.Add(name);}
            foreach(string name in Names)if(Exists(Path.Combine(baseline,name))){MoveEntry(Path.Combine(baseline,name),Path.Combine(game,name));restored.Add(name);}
            File.WriteAllText(record,current);
        }
        catch
        {
            foreach(string name in restored)MoveEntry(Path.Combine(game,name),Path.Combine(baseline,name));
            foreach(string name in saved)MoveEntry(Path.Combine(current,name),Path.Combine(game,name));throw;
        }
    }
    private static void VerifyInstalled(string game)
    {
        var manifest=JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(Path.Combine(Home,"payload","manifest.json")))!;
        string root=Path.GetFullPath(game)+Path.DirectorySeparatorChar;
        foreach(var entry in manifest)
        {
            string path=Path.GetFullPath(Path.Combine(root,entry.Key));
            if(!path.StartsWith(root,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Недопустимый путь компонента.");
            if(!File.Exists(path))throw new IOException("Компонент отсутствует. Нажмите «Установить / обновить»: "+entry.Key);
            using var stream=File.OpenRead(path);
            if(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream))!=entry.Value)throw new IOException("Компонент изменён. Нажмите «Установить / обновить»: "+entry.Key);
        }
    }
    internal static void Play(string game,bool vanilla)
    {if(Running)return;if(vanilla){Disable(game);}else{if(!Compatibility.Supported(game))throw new InvalidOperationException("Версия игры не поддерживается. Выберите обычный запуск.");Enable(game);if(!Installed(game))throw new InvalidOperationException("Сначала установите компоненты DivineLabs.");VerifyInstalled(game);}Process.Start(new ProcessStartInfo("steam://rungameid/1353300"){UseShellExecute=true});}
}
