using DivineLabs.IdleSlayer.Maintenance;
namespace DivineLabs.IdleSlayer.Runtime;

public sealed record CopySearchResult(IReadOnlyList<InstallationLocation> Copies,int VisitedFolders,int UnavailableFolders,bool Cancelled);
public static class InstalledCopies
{
    public static IReadOnlyList<InstallationLocation> Known(string currentFolder=null)
    {
        var copies=InstallationLocations.Known().ToDictionary(x=>x.Folder,StringComparer.OrdinalIgnoreCase);
        void Add(string folder){var item=InstallationLocations.Inspect(folder);if(item!=null)copies.TryAdd(item.Folder,item);}
        if(!string.IsNullOrWhiteSpace(currentFolder))Add(currentFolder);
        Add(Product.Home);
        Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DivineLabs","Idle Slayer Mods"));
        return copies.Values.OrderBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public static IEnumerable<string> LocalDisks()=>DriveInfo.GetDrives().Where(d=>d.DriveType==DriveType.Fixed&&d.IsReady).Select(d=>d.RootDirectory.FullName);
    public static CopySearchResult Search(IEnumerable<string> roots,CancellationToken cancel=default,IProgress<int> progress=null)
    {
        var copies=new Dictionary<string,InstallationLocation>(StringComparer.OrdinalIgnoreCase);
        var visited=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var pending=new Stack<string>(roots.Reverse());int unavailable=0;
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(pending.TryPop(out string folder)){
            if(cancel.IsCancellationRequested)return Finish(true);
            try{
                folder=InstallationLocations.Normalize(folder);
                if(!visited.Add(folder)||(File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;
                var item=InstallationLocations.Inspect(folder);
                if(item!=null)copies.TryAdd(item.Folder,item);
                foreach(string child in Directory.EnumerateDirectories(folder)){
                    if(cancel.IsCancellationRequested)return Finish(true);
                    // Skip known system/cache/backup trees; do not follow links or scan installed payloads.
                    string name=Path.GetFileName(child);
                    if(Skip.Contains(name)||item!=null&&Payload.Contains(name))continue;
                    pending.Push(child);
                }
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException){unavailable++;}
            if(watch.ElapsedMilliseconds>=150){progress?.Report(visited.Count);watch.Restart();}
        }
        return Finish(false);
        CopySearchResult Finish(bool cancelled)=>new(copies.Values.OrderBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).ToArray(),visited.Count,unavailable,cancelled);
    }
    private static readonly HashSet<string> Skip=new(StringComparer.OrdinalIgnoreCase){
        "Windows","$Recycle.Bin","System Volume Information","Recovery","$WINDOWS.~BT","$WinREAgent",
        ".git",".cache","node_modules",".nuget",".gradle","Cache","Caches","Temp","tmp","Backups",".application-history"
    };
    private static readonly HashSet<string> Payload=new(StringComparer.OrdinalIgnoreCase){"App","Data","Docs","payload","licenses"};
}
