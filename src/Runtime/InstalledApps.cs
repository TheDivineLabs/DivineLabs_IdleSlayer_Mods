using System.Diagnostics;
using Microsoft.Win32;

namespace DivineLabs.IdleSlayer.Runtime;

public sealed record InstalledApp(string Name,string Folder,string Version,string Kind);
public sealed record AppDiscovery(IReadOnlyList<InstalledApp> Apps,int UnavailableLocations);
public static class InstalledApps
{
    public static AppDiscovery Find(string additionalFolder=null)
    {
        var found=new Dictionary<string,InstalledApp>(StringComparer.OrdinalIgnoreCase);int unavailable=0;
        void Remember(string name,string folder,string version,string kind){
            if(string.IsNullOrWhiteSpace(folder))return;
            try{folder=DivineLabs.IdleSlayer.Maintenance.InstallationLocations.Normalize(folder.Trim('"'));if(Directory.Exists(folder))found.TryAdd(folder,new(name,folder,version??"",kind));}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){unavailable++;}
        }
        foreach(var copy in DivineLabs.IdleSlayer.Maintenance.InstallationLocations.Known())Remember("DivineLabs Idle Slayer Mods",copy.Folder,copy.Version,"installed");
        foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}){
            try{
                using var root=RegistryKey.OpenBaseKey(hive,view);using var uninstall=root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
                if(uninstall==null)continue;
                foreach(string id in uninstall.GetSubKeyNames())try{
                    using var entry=uninstall.OpenSubKey(id);string name=entry?.GetValue("DisplayName") as string??"",publisher=entry?.GetValue("Publisher") as string??"";
                    if(!publisher.Equals("DivineLabs",StringComparison.OrdinalIgnoreCase)&&!name.StartsWith("DivineLabs ",StringComparison.OrdinalIgnoreCase))continue;
                    Remember(name,entry?.GetValue("InstallLocation") as string,entry?.GetValue("DisplayVersion") as string,"installed");
                }catch(Exception e)when(e is System.Security.SecurityException or UnauthorizedAccessException or IOException){unavailable++;}
            }catch(Exception e)when(e is System.Security.SecurityException or UnauthorizedAccessException or IOException){unavailable++;}
        }
        void Scan(string folder){
            if(string.IsNullOrWhiteSpace(folder)||!Directory.Exists(folder))return;
            try{
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)return;
                foreach(string file in Directory.EnumerateFiles(folder,"*.exe",SearchOption.TopDirectoryOnly)){
                    // Never execute candidates or trust an arbitrary EXE filename as product identity.
                    var version=FileVersionInfo.GetVersionInfo(file);
                    if(!string.Equals(version.CompanyName,"DivineLabs",StringComparison.OrdinalIgnoreCase)||string.IsNullOrWhiteSpace(version.ProductName))continue;
                    Remember(version.ProductName,folder,version.ProductVersion,"portable");break;
                }
            }catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){unavailable++;}
        }
        void ScanRoot(string folder){
            Scan(folder);
            try{if(Directory.Exists(folder)&&(File.GetAttributes(folder)&FileAttributes.ReparsePoint)==0)foreach(string child in Directory.EnumerateDirectories(folder))Scan(child);}
            catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Security.SecurityException){unavailable++;}
        }
        foreach(string folder in new[]{
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","DivineLabs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"DivineLabs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"DivineLabs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"DivineLabs")})ScanRoot(folder);
        Scan(Product.Home);
        if(additionalFolder!=null)ScanRoot(Path.GetFullPath(additionalFolder));
        return new(found.Values.OrderBy(x=>x.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).ToArray(),unavailable);
    }
}
