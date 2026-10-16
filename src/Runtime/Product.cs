using System.Text.Json;
namespace DivineLabs.IdleSlayer.Runtime;
public static class Product
{
    public const string Version = "0.5.0";
    public const string LoaderDirectory = "DivineLabs_Loader";
    public static string ResolveHome(string executableDirectory)
    {
        var directory=new DirectoryInfo(executableDirectory);
        if(directory.Name.Equals("Launcher",StringComparison.OrdinalIgnoreCase)&&directory.Parent?.Name.Equals("App",StringComparison.OrdinalIgnoreCase)==true)
            return directory.Parent.Parent!.FullName;
        return directory.FullName;
    }
    public static string Home => ResolveHome(AppContext.BaseDirectory);
    public static string Components => Path.Combine(Home,"App","Components");
    public static string Data => Path.Combine(Home,"Data");
    public static string Config => Path.Combine(Data,"Config");
    public static string Cache => Path.Combine(Data,"Cache");
    public static string Logs => Path.Combine(Data,"Logs");
    public static string Backups => Path.Combine(Data,"Backups");
    public static string Log => Path.Combine(Logs,"launcher.log");
    public static void MigrateData()
    {
        foreach(string folder in new[]{Config,Cache,Logs,Backups})Directory.CreateDirectory(folder);
        // Move only known legacy entries. Retain conflicts for review instead of overwriting them.
        foreach(var entry in new[]{
            ("game-path.txt",Path.Combine(Config,"game-path.txt")),
            ("launcher.log",Log),("setup.log",Path.Combine(Logs,"setup.log")),
            ("downloads",Path.Combine(Cache,"Downloads")),("components",Path.Combine(Cache,"Components")),
            ("work",Path.Combine(Cache,"Work")),("save-backups",Path.Combine(Backups,"Saves"))})
        {
            string source=Path.Combine(Data,entry.Item1),target=entry.Item2;
            if(!File.Exists(source)&&!Directory.Exists(source))continue;
            if(File.Exists(target)||Directory.Exists(target))
                target=Path.Combine(Backups,"LegacyData",DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"-"+Guid.NewGuid().ToString("N"),entry.Item1);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if(Directory.Exists(source))Directory.Move(source,target);else File.Move(source,target);
        }
    }
    public static void Record(Exception error)
    { Directory.CreateDirectory(Logs);File.AppendAllText(Log,$"{DateTime.UtcNow:O} {error}{Environment.NewLine}"); }
}
public sealed record SetupProgress(string Message, double? Percent = null);
public sealed class PackageManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public Dictionary<string,string> Files { get; set; } = new();
    public static string GameRelative(string relative) => relative.StartsWith("Defaults/",StringComparison.OrdinalIgnoreCase) ? "UserData/"+relative["Defaults/".Length..] : relative;
    public static PackageManifest Read(string root)
    {
        var m=JsonSerializer.Deserialize<PackageManifest>(File.ReadAllText(Path.Combine(root,"manifest.json"))) ?? throw new InvalidDataException("Манифест компонентов пуст.");
        if(m.SchemaVersion!=1 || m.Version!=Product.Version || m.Files.Count==0)throw new InvalidDataException("Неподдерживаемая версия пакета компонентов.");
        foreach(var entry in m.Files){Paths.Inside(root,entry.Key);if(entry.Value.Length!=64 || !entry.Value.All(Uri.IsHexDigit))throw new InvalidDataException("Некорректная контрольная сумма компонента.");}
        foreach(string required in new[]{"version.dll",Product.LoaderDirectory+"/net6/MelonLoader.dll","Mods/DivineLabs.IdleSlayer.Mod.dll","UserLibs/DivineLabs.IdleSlayer.Contracts.dll"})
            if(!m.Files.ContainsKey(required))throw new InvalidDataException("В пакете нет обязательного компонента: "+required);
        return m;
    }
}
public static class Paths
{
    public static string Inside(string root,string relative)
    {
        if(string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative)||relative.Contains(':'))throw new InvalidDataException("Недопустимый путь компонента.");
        string path=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
        if(!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("Путь выходит за пределы папки компонентов.");
        return path;
    }
}
