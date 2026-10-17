using System.Text.Json;
using DivineLabs.IdleSlayer.Setup;
using DivineLabs.IdleSlayer.Maintenance;

string scratch=Path.Combine(@"W:\DEV\.cache\temp","DivineLabs-removal-check-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(scratch);
string first=Create("remove"),second=Create("keep");
try{
    InstallationLocations.Remember(first);InstallationLocations.Remember(second);
    SetupOperations.RemoveCopy(first,(_,_)=>{});
    Require(!File.Exists(Path.Combine(first,"DivineLabs.IdleSlayer.exe")),"Selected program survived");
    Require(!File.Exists(Path.Combine(first,"App/Setup/installation.json")),"Receipt survived successful removal");
    foreach(string file in Retained())Require(File.ReadAllText(Path.Combine(first,file))=="retain","Lost retained data: "+file);
    Require(InstallationLocations.Inspect(second)!=null,"Other installation was changed");
    Require(InstallationLocations.Known().Any(x=>x.Folder==second),"Other installation disappeared from catalog");
    Require(!InstallationLocations.Known().Any(x=>x.Folder==first),"Removed copy remains in catalog");
    string invalid=Create("invalid");
    File.WriteAllText(Path.Combine(invalid,"App/Setup/installation.json"),JsonSerializer.Serialize(new Receipt{Files=new[]{"DivineLabs.IdleSlayer.exe","App/../Data/Config/settings.json"}}));
    Rejected(invalid);Require(File.Exists(Path.Combine(invalid,"DivineLabs.IdleSlayer.exe")),"Invalid receipt partly deleted app");
    Require(File.ReadAllText(Path.Combine(invalid,"Data/Config/settings.json"))=="retain","Normalized traversal deleted settings");
    string locked=Create("locked");
    using(var handle=new FileStream(Path.Combine(locked,"Docs/owned.txt"),FileMode.Open,FileAccess.Read,FileShare.None))Rejected(locked);
    Require(File.Exists(Path.Combine(locked,"DivineLabs.IdleSlayer.exe")),"Locked file caused partial deletion before preflight");
    SetupOperations.RemoveCopy(locked,(_,_)=>{});
    string running=Create("running"),executable=Path.Combine(running,"DivineLabs.IdleSlayer.exe");
    File.Copy(Path.Combine(Environment.SystemDirectory,"ping.exe"),executable,true);
    using(var process=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(executable,"-t 127.0.0.1"){UseShellExecute=false,CreateNoWindow=true})){
        try{Require(!process.HasExited,"Running-copy fixture did not start");Rejected(running);Require(File.Exists(executable),"Running copy was removed");}
        finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
    }
    string linked=Create("linked"),outside=Path.Combine(scratch,"outside");Directory.CreateDirectory(outside);
    File.WriteAllText(Path.Combine(outside,"owned.txt"),"retain");
    Directory.Delete(Path.Combine(linked,"Docs"),true);Directory.CreateSymbolicLink(Path.Combine(linked,"Docs"),outside);
    Rejected(linked);Require(File.ReadAllText(Path.Combine(outside,"owned.txt"))=="retain","Followed directory link");
    Require(File.Exists(Path.Combine(linked,"DivineLabs.IdleSlayer.exe")),"Link rejection partly deleted app");
    Console.WriteLine("Passed: selected-only removal, retained settings/saves/backups/unowned files, catalog isolation, normalized traversal rejection, locked-file preflight/retry, running-copy rejection, reparse rejection. "+scratch);
}finally{InstallationLocations.Forget(first);InstallationLocations.Forget(second);}

string Create(string name){
    string folder=Path.Combine(scratch,name);
    foreach(string file in new[]{"DivineLabs.IdleSlayer.exe","DivineLabs.Uninstall.exe","App/Launcher/app.dll","Docs/owned.txt"}.Concat(Retained())){
        string path=Path.Combine(folder,file);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllText(path,file=="Docs/owned.txt"?"owned":"retain");
    }
    string receipt=Path.Combine(folder,"App/Setup/installation.json");Directory.CreateDirectory(Path.GetDirectoryName(receipt)!);
    File.WriteAllText(receipt,JsonSerializer.Serialize(new Receipt{Files=new[]{"DivineLabs.IdleSlayer.exe","DivineLabs.Uninstall.exe","App/Launcher/app.dll","Docs/owned.txt"}}));return folder;
}
string[] Retained()=>new[]{"Data/Config/settings.json","Data/Backups/Saves/save.sav","Data/Backups/Application/old.exe","save.sav","Docs/user-notes.txt"};
void Rejected(string folder){bool rejected=false;try{SetupOperations.RemoveCopy(folder,(_,_)=>{});}catch(IOException){rejected=true;}Require(rejected,"Unsafe removal was accepted: "+folder);}
void Require(bool condition,string message){if(!condition)throw new Exception(message);}
