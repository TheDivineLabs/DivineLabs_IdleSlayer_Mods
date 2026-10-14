using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DivineLabs.IdleSlayer;
using DivineLabs.IdleSlayer.Runtime;
static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("ПРОЙДЕНО: "+name);}
var root=Path.Combine(@"W:\DEV\.cache\temp","DivineLabs-checks-"+Guid.NewGuid());Directory.CreateDirectory(root);
Check(Settings.MultiplyIncome(-200,100)==-200,"Покупка не умножает расход");
Check(10+Settings.MultiplyIncome(2,100)+Settings.MultiplyIncome(-200,100)==10,"Доход и покупка сохраняют верный баланс");
Check(Settings.MultiplyIncome(double.MaxValue,100)==double.MaxValue,"Защита от переполнения");
Check(Settings.MultiplyIncome(25,1)==25,"Отключение через ×1");
Check(!Settings.Valid(double.NaN)&&!Settings.Valid(double.PositiveInfinity)&&!Settings.Valid(0),"Некорректные коэффициенты отклонены");
string file=Path.Combine(root,SettingsStore.FileName),legacy=Path.Combine(root,"IdleSlayerQoL.json");File.WriteAllText(legacy,"{\"Enabled\":false,\"CoinMultiplier\":25,\"DamageMultiplier\":50}");
var s=SettingsStore.Load(file);Check(s.CoinMultiplier==25&&!s.Enabled&&File.Exists(legacy),"Миграция сохраняет значения и исходник");s.CoinMultiplier=30;SettingsStore.Save(file,s);Check(SettingsStore.Read(file).CoinMultiplier==30,"Атомарная запись");File.WriteAllText(file,"broken");Check(SettingsStore.Load(file).CoinMultiplier==25,"Восстановление резервного JSON");
using(var gate=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){bool rejected=false;try{SettingsStore.Save(file,s);}catch(IOException){rejected=true;}Check(rejected,"Одновременная запись исключена");}
File.WriteAllText(file,"{\"SchemaVersion\":2}");bool future=false;try{SettingsStore.Load(file);}catch(InvalidDataException){future=true;}Check(future&&File.ReadAllText(file).Contains(":2"),"Будущая схема не перезаписывается");
var fake=Path.Combine(root,"game");Directory.CreateDirectory(Path.Combine(fake,"Mods"));File.WriteAllText(Path.Combine(fake,"Mods","user.txt"),"preserve");File.WriteAllText(Path.Combine(fake,"version.dll"),"proxy");Installation.Disable(fake);Check(!Directory.Exists(Path.Combine(fake,"Mods")),"Отключение убирает игровые маркеры");Installation.Enable(fake);Check(File.ReadAllText(Path.Combine(fake,"Mods","user.txt"))=="preserve","Включение сохраняет пользовательские файлы");
string hostile=Path.Combine(root,"hostile.zip");using(var zip=ZipFile.Open(hostile,ZipArchiveMode.Create)){using var writer=new StreamWriter(zip.CreateEntry("../escaped.txt").Open());writer.Write("must not escape");}
string extract=Path.Combine(root,"extracted");bool escaped=false;try{Payload.ExtractSafe(hostile,extract);}catch(InvalidDataException){escaped=true;}Check(escaped&&!File.Exists(Path.Combine(root,"escaped.txt")),"Архив не может записать файл вне целевой папки");
byte[] bytes=System.Text.Encoding.UTF8.GetBytes("downloaded-component");string expected=Convert.ToHexString(SHA256.HashData(bytes));
var portProbe=new System.Net.Sockets.TcpListener(IPAddress.Loopback,0);portProbe.Start();int port=((IPEndPoint)portProbe.LocalEndpoint).Port;portProbe.Stop();
using var server=new HttpListener();server.Prefixes.Add($"http://127.0.0.1:{port}/");server.Start();
async Task Serve(){var context=await server.GetContextAsync();context.Response.ContentLength64=bytes.Length;await context.Response.OutputStream.WriteAsync(bytes);context.Response.Close();}
string download=Path.Combine(root,"download.bin");var serve=Serve();await Payload.DownloadVerifiedAsync(new Uri($"http://127.0.0.1:{port}/component"),download,expected);await serve;Check(File.ReadAllBytes(download).SequenceEqual(bytes),"Загрузка проверяется по SHA256");
File.WriteAllText(download,"previous-good-file");serve=Serve();bool mismatch=false;try{await Payload.DownloadVerifiedAsync(new Uri($"http://127.0.0.1:{port}/bad"),download,new string('0',64));}catch(InvalidDataException){mismatch=true;}await serve;Check(mismatch&&File.ReadAllText(download)=="previous-good-file","Неверная загрузка не заменяет исправный файл");server.Stop();
// Optional local acceptance: no game binaries are supplied to or committed with this project.
string installedGame=Installation.FindGame();
if(Installation.ValidGame(installedGame)&&!Installation.Running)
{
    string local=Path.Combine(Product.Home,"payload");
    if(Directory.Exists(local))throw new IOException("Для локальной проверки нужна отдельная папка tests/bin без пользовательского payload.");
    Directory.CreateDirectory(local);var manifest=new PackageManifest{Version=Product.Version};
    foreach(string relative in new[]{"version.dll",Product.LoaderDirectory+"/net6/MelonLoader.dll","Mods/DivineLabs.IdleSlayer.Mod.dll","UserLibs/DivineLabs.IdleSlayer.Contracts.dll"})
    {string p=Paths.Inside(local,relative);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"test-component-"+relative);using var input=File.OpenRead(p);manifest.Files[relative]=Convert.ToHexString(SHA256.HashData(input));}
    File.WriteAllText(Path.Combine(local,"manifest.json"),JsonSerializer.Serialize(manifest));
    File.Copy(Path.Combine(installedGame,"GameAssembly.dll"),Path.Combine(fake,"GameAssembly.dll"));File.WriteAllText(Path.Combine(fake,"Idle Slayer.exe"),"local-test-placeholder");
    Directory.CreateDirectory(Path.Combine(fake,"MelonLoader"));File.WriteAllText(Path.Combine(fake,"MelonLoader","original.txt"),"legacy-preserved");Directory.CreateDirectory(Path.Combine(fake,"UserData"));SettingsStore.Save(Path.Combine(fake,"UserData",SettingsStore.FileName),new Settings{CoinMultiplier=123,DamageMultiplier=456});
    await Installation.EnsureReadyAsync(fake);
    Check(Installation.Installed(fake)&&!Directory.Exists(Path.Combine(fake,"MelonLoader")),"Миграция подключает DivineLabs_Loader вместо MelonLoader");
    Check((File.GetAttributes(Path.Combine(fake,Product.LoaderDirectory))&FileAttributes.ReparsePoint)==0,"Установка не зависит от junction на portable");
    Check(SettingsStore.Read(Installation.Config(fake)).CoinMultiplier==123,"Миграция сохраняет пользовательские коэффициенты");
    Installation.VerifyInstalled(fake,local);Directory.Move(local,local+"-moved");Check(Installation.Installed(fake),"Перенос portable не ломает игровые компоненты");
    Installation.Restore(fake);Check(File.ReadAllText(Path.Combine(fake,"MelonLoader","original.txt"))=="legacy-preserved"&&File.ReadAllText(Path.Combine(fake,"Mods","user.txt"))=="preserve","Откат возвращает прежние пользовательские файлы");
    // Test-generated payload is retained as an artifact, never removed from an unknown path.
    Directory.Move(local+"-moved",Path.Combine(root,"test-payload"));
}
else Console.WriteLine("Локальная проверка миграции пропущена: нужна установленная закрытая игра.");
Console.WriteLine("Проверки завершены. Артефакты: "+root);
