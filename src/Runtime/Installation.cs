using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Runtime;
public static class Installation
{
    public static string Data=>Product.Data;
    private static readonly string[] Names={Product.LoaderDirectory,"MelonLoader","Mods","Plugins","UserLibs","UserData","version.dll",".divinelabs-installation.json"};
    public static bool Running=>Process.GetProcessesByName("Idle Slayer").Length>0;
    public static bool ValidGame(string path)=>File.Exists(Path.Combine(path,"Idle Slayer.exe"))&&File.Exists(Path.Combine(path,"GameAssembly.dll"));
    public static string Loader(string game)=>Directory.Exists(Path.Combine(game,Product.LoaderDirectory))?Path.Combine(game,Product.LoaderDirectory):Path.Combine(game,"MelonLoader");
    public static bool Restored(string game)=>File.Exists(Path.Combine(game,".divinelabs-manual-mode"));
    public static string FindGame()
    {
        string saved=Path.Combine(Data,"game-path.txt");if(File.Exists(saved)&&ValidGame(File.ReadAllText(saved).Trim()))return File.ReadAllText(saved).Trim();
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase){@"W:\steam",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam")};
        foreach(var hive in new[]{Registry.CurrentUser,Registry.LocalMachine})foreach(string name in new[]{@"Software\Valve\Steam",@"Software\WOW6432Node\Valve\Steam"})
        {using var key=hive.OpenSubKey(name);if(key?.GetValue("SteamPath") is string p)roots.Add(p);if(key?.GetValue("InstallPath") is string s)roots.Add(s);}
        foreach(string root in roots.ToArray())
        {string file=Path.Combine(root,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match m in Regex.Matches(File.ReadAllText(file),"\"path\"\\s+\"([^\"]+)\""))roots.Add(m.Groups[1].Value.Replace("\\\\","\\"));}
        return roots.Select(p=>Path.Combine(p,"steamapps","common","Idle Slayer")).FirstOrDefault(ValidGame)??"";
    }
    public static void RememberGame(string game)
    {if(!ValidGame(game))throw new IOException("В этой папке нет установленной игры Idle Slayer.");Directory.CreateDirectory(Data);File.WriteAllText(Path.Combine(Data,"game-path.txt"),Path.GetFullPath(game));}
    public static string Config(string game)
    {string directory=Path.Combine(game,"UserData");if(!Directory.Exists(directory)&&Directory.Exists(Path.Combine(game,".divinelabs-disabled","UserData")))directory=Path.Combine(game,".divinelabs-disabled","UserData");return Path.Combine(directory,SettingsStore.FileName);}
    public static void RequireStopped(){if(Running)throw new InvalidOperationException("Закройте игру штатно перед изменением установки или настроек.");}
    private sealed class Operation:IDisposable
    {
        private readonly Mutex mutex=new(false,@"Local\DivineLabs.IdleSlayer.Installation");
        public Operation(){try{if(!mutex.WaitOne(0))throw new InvalidOperationException("Другая копия программы уже подготавливает игру.");}catch(AbandonedMutexException){}}
        public void Dispose(){mutex.ReleaseMutex();mutex.Dispose();}
    }
    public static void CopyTree(string source,string target)
    {Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source))File.Copy(file,Path.Combine(target,Path.GetFileName(file)),true);foreach(string directory in Directory.GetDirectories(source))CopyTree(directory,Path.Combine(target,Path.GetFileName(directory)));}
    private static bool Exists(string path)=>File.Exists(path)||Directory.Exists(path);
    private static void MoveEntry(string source,string target)
    {Directory.CreateDirectory(Path.GetDirectoryName(target)!);if(Directory.Exists(source))Directory.Move(source,target);else if(File.Exists(source))File.Move(source,target);}
    private static void SnapshotEntry(string source,string target)
    {
        if(Directory.Exists(source)&&(File.GetAttributes(source)&FileAttributes.ReparsePoint)!=0){CopyTree(source,target);Directory.Delete(source);}
        else MoveEntry(source,target);
    }
    private static void RemoveNewEntry(string game,string name)
    {
        string path=Paths.Inside(game,name);
        if(Directory.Exists(path))Directory.Delete(path,(File.GetAttributes(path)&FileAttributes.ReparsePoint)==0);
        else if(File.Exists(path))File.Delete(path);
    }
    public static bool Installed(string game)=>File.Exists(Path.Combine(game,Product.LoaderDirectory,"net6","MelonLoader.dll"))&&File.Exists(Path.Combine(game,"Mods","DivineLabs.IdleSlayer.Mod.dll"));
    private static string Digest(string file){using var stream=File.OpenRead(file);return Convert.ToHexString(SHA256.HashData(stream));}
    public static void VerifyInstalled(string game,string payload)
    {
        var manifest=PackageManifest.Read(payload);
        foreach(var entry in manifest.Files)
        {
            // UserData contains seed files which are subsequently edited by the loader/user.
            if(entry.Key.StartsWith("UserData/",StringComparison.OrdinalIgnoreCase))continue;
            string file=Paths.Inside(game,entry.Key);
            if(!File.Exists(file)||!Digest(file).Equals(entry.Value,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Компоненты нужно восстановить: "+entry.Key);
        }
    }
    public static async Task<bool> EnsureReadyAsync(string game,IProgress<SetupProgress> progress=null,CancellationToken cancel=default,bool force=false,bool reuseCache=true)
    {
        if(!ValidGame(game))throw new IOException("Установите Idle Slayer в Steam или выберите папку игры.");
        if(!Compatibility.Supported(game))throw new InvalidOperationException("Эта версия игры ещё не проверена. Доступен запуск без модов.");
        if(Running){progress?.Report(new("Игра уже работает. Настройки доступны в игровом меню."));return false;}
        string payload=await Payload.EnsureAsync(progress,cancel);
        await Task.Run(()=>
        {
            using var operation=new Operation();RequireStopped();
            if(!force&&Installed(game)){try{VerifyInstalled(game,payload);RememberGame(game);progress?.Report(new("Всё готово. Можно играть.",100));return;}catch(InvalidDataException e){Product.Record(e);}}
            InstallCore(game,payload,progress,cancel,reuseCache);
        },cancel);
        return true;
    }
    private static void InstallCore(string game,string payload,IProgress<SetupProgress> progress,CancellationToken cancel,bool reuseCache)
    {
        Payload.Verify(payload,progress,cancel);EnableCore(game);string stage=Path.Combine(Data,"work","installation-"+Guid.NewGuid().ToString("N"));
        progress?.Report(new("Подготовка установки…"));CopyTree(payload,stage);
        foreach(string name in new[]{"Mods","UserData","UserLibs","Plugins"})Directory.CreateDirectory(Path.Combine(stage,name));
        if(Directory.Exists(Path.Combine(game,"UserData")))CopyTree(Path.Combine(game,"UserData"),Path.Combine(stage,"UserData"));
        string oldLoader=Loader(game),newLoader=Path.Combine(stage,Product.LoaderDirectory);
        if(reuseCache&&Directory.Exists(oldLoader))
        {
            progress?.Report(new("Перенос подготовленных компонентов игры…"));
            foreach(string folder in new[]{"Il2CppAssemblies","Dependencies/Il2CppAssemblyGenerator/Cpp2IL","Dependencies/Il2CppAssemblyGenerator/UnityDependencies"})
                if(Directory.Exists(Paths.Inside(oldLoader,folder)))CopyTree(Paths.Inside(oldLoader,folder),Paths.Inside(newLoader,folder));
            string config=Path.Combine(oldLoader,"Dependencies","Il2CppAssemblyGenerator","Config.cfg");
            if(File.Exists(config))File.Copy(config,Path.Combine(newLoader,"Dependencies","Il2CppAssemblyGenerator","Config.cfg"),true);
        }
        SettingsStore.Load(Path.Combine(stage,"UserData",SettingsStore.FileName));
        string loaderConfig=Path.Combine(stage,"UserData","Loader.cfg");
        if(File.Exists(loaderConfig))File.WriteAllText(loaderConfig,File.ReadAllText(loaderConfig).Replace("hide_console = false","hide_console = true"));
        cancel.ThrowIfCancellationRequested();RequireStopped();BackupSaves();
        string id=DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..6];
        string history=Path.Combine(game,".divinelabs-history"),baseline=Path.Combine(history,id);Directory.CreateDirectory(baseline);
        string previousRecord=Path.Combine(history,"previous.txt");string previous=File.Exists(previousRecord)?File.ReadAllText(previousRecord):null;
        var saved=new List<string>();var added=new List<string>();
        try
        {
            progress?.Report(new("Сохранение предыдущей установки…"));
            foreach(string name in Names)if(Exists(Path.Combine(game,name))){SnapshotEntry(Path.Combine(game,name),Path.Combine(baseline,name));saved.Add(name);}
            RequireStopped();progress?.Report(new("Установка DivineLabs Loader…"));
            foreach(string name in Names.Where(n=>n!="MelonLoader"&&n!=".divinelabs-installation.json"))
            {added.Add(name);if(Directory.Exists(Path.Combine(stage,name)))CopyTree(Path.Combine(stage,name),Path.Combine(game,name));else if(File.Exists(Path.Combine(stage,name)))File.Copy(Path.Combine(stage,name),Path.Combine(game,name));}
            added.Add(".divinelabs-installation.json");
            File.WriteAllText(Path.Combine(game,".divinelabs-installation.json"),JsonSerializer.Serialize(new{Version=Product.Version,Previous=id}));
            File.WriteAllText(Path.Combine(history,"previous.txt"),id);RememberGame(game);
            string manual=Path.Combine(game,".divinelabs-manual-mode");if(File.Exists(manual))File.Delete(manual);
        }
        catch
        {foreach(string name in added.AsEnumerable().Reverse())RemoveNewEntry(game,name);foreach(string name in saved)MoveEntry(Path.Combine(baseline,name),Path.Combine(game,name));if(previous!=null)File.WriteAllText(previousRecord,previous);else if(File.Exists(previousRecord))File.Delete(previousRecord);throw;}
        // Only the fresh, confined staging directory is removed. Backups are retained.
        if(!stage.StartsWith(Path.GetFullPath(Path.Combine(Data,"work"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Неверная папка подготовки.");
        Directory.Delete(stage,true);progress?.Report(new("Всё готово. Можно играть.",100));
    }
    public static void BackupSaves()
    {string source=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"AppData","LocalLow","Pablo Leban","Idle Slayer");if(!Directory.Exists(source))return;string target=Path.Combine(Data,"save-backups",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff"));Directory.CreateDirectory(target);foreach(string file in Directory.GetFiles(source,"*.sav"))File.Copy(file,Path.Combine(target,Path.GetFileName(file)));}
    public static void Disable(string game){using var operation=new Operation();DisableCore(game);}
    private static void DisableCore(string game)
    {
        RequireStopped();string disabled=Path.Combine(game,".divinelabs-disabled");
        foreach(string name in Names)if(Exists(Path.Combine(disabled,name))&&Exists(Path.Combine(game,name)))throw new IOException("Сначала верните отключённые компоненты.");
        Directory.CreateDirectory(disabled);var moved=new List<string>();
        try{foreach(string name in Names)if(Exists(Path.Combine(game,name))){MoveEntry(Path.Combine(game,name),Path.Combine(disabled,name));moved.Add(name);}}
        catch{foreach(string name in moved)MoveEntry(Path.Combine(disabled,name),Path.Combine(game,name));throw;}
    }
    public static void Enable(string game){using var operation=new Operation();EnableCore(game);}
    private static void EnableCore(string game)
    {
        RequireStopped();string disabled=Path.Combine(game,".divinelabs-disabled");if(!Directory.Exists(disabled)||!Names.Any(n=>Exists(Path.Combine(disabled,n))))return;
        foreach(string name in Names)if(Exists(Path.Combine(game,name)))throw new IOException("Установка уже подключена.");
        var moved=new List<string>();try{foreach(string name in Names)if(Exists(Path.Combine(disabled,name))){MoveEntry(Path.Combine(disabled,name),Path.Combine(game,name));moved.Add(name);}}
        catch{foreach(string name in moved)MoveEntry(Path.Combine(game,name),Path.Combine(disabled,name));throw;}
    }
    public static void Restore(string game)
    {
        using var operation=new Operation();RequireStopped();string history=Path.Combine(game,".divinelabs-history"),record=Path.Combine(history,"previous.txt");
        string baseline=Paths.Inside(history,File.ReadAllText(record).Trim());if(!Directory.Exists(baseline))throw new IOException("Резервная установка не найдена.");EnableCore(game);
        string id=DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N")[..6],current=Path.Combine(history,id);Directory.CreateDirectory(current);
        var saved=new List<string>();var restored=new List<string>();
        try{foreach(string name in Names)if(Exists(Path.Combine(game,name))){SnapshotEntry(Path.Combine(game,name),Path.Combine(current,name));saved.Add(name);}foreach(string name in Names)if(Exists(Path.Combine(baseline,name))){MoveEntry(Path.Combine(baseline,name),Path.Combine(game,name));restored.Add(name);}File.WriteAllText(record,id);File.WriteAllText(Path.Combine(game,".divinelabs-manual-mode"),"Автоматическая подготовка отключена после возврата установки.");}
        catch{foreach(string name in restored)MoveEntry(Path.Combine(game,name),Path.Combine(baseline,name));foreach(string name in saved)MoveEntry(Path.Combine(current,name),Path.Combine(game,name));throw;}
    }
    public static async Task PlayAsync(string game,bool vanilla,IProgress<SetupProgress> progress=null,CancellationToken cancel=default)
    {
        if(Running)return;
        if(vanilla)Disable(game);
        else
        {
            if(Restored(game))
            {Enable(game);if(!File.Exists(Path.Combine(game,"version.dll")))throw new IOException("Восстановленная установка не содержит загрузчик. Подготовьте компоненты или запустите без модов.");}
            else{Enable(game);await EnsureReadyAsync(game,progress,cancel);}
        }
        using var operation=new Operation();if(Running)return;progress?.Report(new("Запуск через Steam…"));
        Process.Start(new ProcessStartInfo("steam://rungameid/1353300"){UseShellExecute=true});
    }
}
