using System.Text.Json;
namespace DivineLabs.IdleSlayer.Runtime;
public static class Product
{
    public const string Version = "0.2.0";
    public const string LoaderDirectory = "DivineLabs_Loader";
    public static string Home => AppContext.BaseDirectory;
    public static string Data => Path.Combine(Home,"Data");
    public static string Log => Path.Combine(Data,"launcher.log");
    public static void Record(Exception error)
    { Directory.CreateDirectory(Data);File.AppendAllText(Log,$"{DateTime.UtcNow:O} {error}{Environment.NewLine}"); }
}
public sealed record SetupProgress(string Message, double? Percent = null);
public sealed class PackageManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string Version { get; set; } = "";
    public Dictionary<string,string> Files { get; set; } = new();
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
