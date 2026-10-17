using System.Text.Json;
using DivineLabs.IdleSlayer.Maintenance;
using DivineLabs.IdleSlayer.Runtime;
internal static class CopyDiscoveryChecks
{
    internal static void Run()
    {
        string root=Path.Combine(@"W:\DEV\.cache\temp","DivineLabs-copy-discovery-"+Guid.NewGuid().ToString("N"));
        string first=Create("one"),second=Create(@"nested\another\two"),legacy=Create("legacy",true);
        string nested=Create(@"one\another-copy");
        Create(@"Backups\not-active");
        string fake=Path.Combine(root,"fake");Directory.CreateDirectory(fake);File.WriteAllText(Path.Combine(fake,"DivineLabs.IdleSlayer.exe"),"fixture");
        var result=InstalledCopies.Search(new[]{root,first});
        if(result.Copies.Count!=4||!result.Copies.Any(x=>x.Folder==second)||!result.Copies.Any(x=>x.Folder==legacy)||!result.Copies.Any(x=>x.Folder==nested))throw new Exception("Nested/legacy copy discovery or deduplication failed");
        using var cancel=new CancellationTokenSource();cancel.Cancel();
        if(!InstalledCopies.Search(new[]{root},cancel.Token).Cancelled)throw new Exception("Search cancellation failed");
        if(InstallationLocations.Normalize(@"W:\")!=@"W:\")throw new Exception("Drive root was converted to a drive-relative path");
        try{
            InstallationLocations.Remember(first);InstallationLocations.Remember(second);
            var known=InstallationLocations.Known();
            if(!known.Any(x=>x.Folder==first)||!known.Any(x=>x.Folder==second))throw new Exception("Catalog lost a different installation");
            InstallationLocations.Forget(first);
            if(!InstallationLocations.Known().Any(x=>x.Folder==second))throw new Exception("Removing one location removed another");
            File.Delete(Path.Combine(second,"DivineLabs.IdleSlayer.exe"));
            if(InstallationLocations.Known().Any(x=>x.Folder==second))throw new Exception("Stale installation is still shown");
        }finally{InstallationLocations.Forget(first);InstallationLocations.Forget(second);}
        Console.WriteLine("Passed: nested copies, legacy receipts, deduplication, skipped backups, cancellation, per-location catalog and stale filtering. "+root);
        string Create(string relative,bool old=false){
            string folder=Path.Combine(root,relative);Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"DivineLabs.IdleSlayer.exe"),"fixture");
            string receipt=Path.Combine(folder,old?"installation.json":@"App\Setup\installation.json");Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
            File.WriteAllText(receipt,JsonSerializer.Serialize(new{Product="DivineLabs.IdleSlayer",Version="0.5.0"}));return folder;
        }
    }
}
