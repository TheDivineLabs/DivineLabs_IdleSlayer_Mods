using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Maintenance;

public sealed record InstallationLocation(string Folder,string Version);
public static class InstallationLocations
{
    private const string Catalog=@"Software\DivineLabs\IdleSlayer\Installations";
    private const string Legacy=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\DivineLabs.IdleSlayer";
    public static string Normalize(string folder)
    {string full=Path.GetFullPath(folder);return full==Path.GetPathRoot(full)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);}
    private static string Id(string folder)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(folder).ToUpperInvariant())));
    public static InstallationLocation Inspect(string folder)
    {
        try{
            folder=Normalize(folder);
            if(!File.Exists(Path.Combine(folder,"DivineLabs.IdleSlayer.exe")))return null;
            string receipt=Path.Combine(folder,"App","Setup","installation.json");if(!File.Exists(receipt))receipt=Path.Combine(folder,"installation.json");
            if(!File.Exists(receipt))return null;
            using var json=JsonDocument.Parse(File.ReadAllText(receipt));
            if(!json.RootElement.TryGetProperty("Product",out var product)||product.GetString()!="DivineLabs.IdleSlayer")return null;
            return new(folder,json.RootElement.TryGetProperty("Version",out var version)?version.GetString()??"":"");
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or JsonException or InvalidOperationException){return null;}
    }
    public static void Remember(string folder)
    {
        var install=Inspect(folder);if(install==null)return;
        using var key=Registry.CurrentUser.CreateSubKey(Catalog+@"\"+Id(install.Folder));key.SetValue("Path",install.Folder);key.SetValue("Version",install.Version);
    }
    public static void Forget(string folder)=>Registry.CurrentUser.DeleteSubKeyTree(Catalog+@"\"+Id(folder),false);
    public static IReadOnlyList<InstallationLocation> Known()
    {
        var result=new Dictionary<string,InstallationLocation>(StringComparer.OrdinalIgnoreCase);
        void Add(string path){if(string.IsNullOrWhiteSpace(path))return;var install=Inspect(path);if(install!=null)result.TryAdd(install.Folder,install);}
        try{
            using var catalog=Registry.CurrentUser.OpenSubKey(Catalog);
            if(catalog!=null)foreach(string id in catalog.GetSubKeyNames()){
                try{using var entry=catalog.OpenSubKey(id);Add(entry?.GetValue("Path") as string);}
                catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){}
            }
            using var legacy=Registry.CurrentUser.OpenSubKey(Legacy);Add(legacy?.GetValue("InstallLocation") as string);
        }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){}
        return result.Values.OrderBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
