using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DivineLabs.IdleSlayer;
using DivineLabs.IdleSlayer.Runtime;
static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("ПРОЙДЕНО: "+name);}
if(args.Length>0&&args[0]=="--live"){
    if(args.Length<2)throw new ArgumentException("Укажите папку запущенной игры после --live.");
    string config=Installation.Config(args[1]);
    var before=await SessionWire.Send(config,new());var original=before.Settings.Copy();
    var changed=original.Copy();changed.Bindings["menu"]=new(){Key=120,Modifiers=2};changed.WidgetOpacity=.55f;
    var applied=await SessionWire.Send(config,new(){Revision=original.Revision,Settings=changed});
    Check(applied.Error==null&&applied.Settings.Bindings["menu"].Key==120&&SettingsStore.Read(config).WidgetOpacity==.55f,"Запущенная игра подтвердила перебинд и прозрачность");
    var observed=await SessionWire.Send(config,new());Check(observed.Settings.Revision==applied.Settings.Revision,"Чтение возвращает подтверждённую ревизию игры");
    original.Revision=observed.Settings.Revision;var restored=await SessionWire.Send(config,new(){Revision=observed.Settings.Revision,Settings=original});
    Check(restored.Error==null&&restored.Settings.Bindings["menu"]==original.Bindings["menu"],"Исходные настройки восстановлены через канал");
    return;
}
var root=Path.Combine(@"W:\DEV\.cache\temp","DivineLabs-checks-"+Guid.NewGuid());Directory.CreateDirectory(root);
Check(Settings.MultiplyIncome(-200,100)==-200,"Покупка не умножает расход");
Check(10+Settings.MultiplyIncome(2,100)+Settings.MultiplyIncome(-200,100)==10,"Доход и покупка сохраняют верный баланс");
Check(Settings.MultiplyIncome(double.MaxValue,100)==double.MaxValue,"Защита от переполнения");
Check(Settings.MultiplyIncome(25,1)==25,"Отключение через ×1");
Check(!Settings.Valid(double.NaN)&&!Settings.Valid(double.PositiveInfinity)&&!Settings.Valid(0),"Некорректные коэффициенты отклонены");
string file=Path.Combine(root,SettingsStore.FileName),legacy=Path.Combine(root,"IdleSlayerQoL.json");File.WriteAllText(legacy,"{\"Enabled\":false,\"CoinMultiplier\":25,\"DamageMultiplier\":50}");
var s=SettingsStore.Load(file);Check(s.CoinMultiplier==25&&!s.Enabled&&File.Exists(legacy),"Миграция сохраняет значения и исходник");s.CoinMultiplier=30;SettingsStore.Save(file,s);Check(SettingsStore.Read(file).CoinMultiplier==30,"Атомарная запись");File.WriteAllText(file,"broken");Check(SettingsStore.Load(file).CoinMultiplier==25,"Восстановление резервного JSON");
using(var gate=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){bool rejected=false;try{SettingsStore.Save(file,s);}catch(IOException){rejected=true;}Check(rejected,"Одновременная запись исключена");}
File.WriteAllText(file,"{\"SchemaVersion\":3}");bool future=false;try{SettingsStore.Load(file);}catch(InvalidDataException){future=true;}Check(future&&File.ReadAllText(file).Contains(":3"),"Будущая схема не перезаписывается");
string migrated=Path.Combine(root,"schema1.json");File.WriteAllText(migrated,"{\"SchemaVersion\":1,\"CoinMultiplier\":73,\"DamageMultiplier\":219,\"UiScale\":0.85}");
var upgraded=SettingsStore.Load(migrated);
Check(upgraded.SchemaVersion==2&&upgraded.CoinMultiplier==73&&upgraded.Bindings["menu"].Key==45&&File.Exists(migrated+".v1.bak"),"Схема 1 сохраняет значения, создаёт резервную копию и назначает Insert");
var duplicate=upgraded.Copy();duplicate.Bindings["coins"]=duplicate.Bindings["menu"] with {};
bool conflict=false;try{duplicate.Validate();}catch(InvalidDataException){conflict=true;}Check(conflict,"Повторные назначения отклонены");
var reserved=new KeyBinding{Key=115,Modifiers=2};bool rejectedReserved=false;try{reserved.Validate();}catch(InvalidDataException){rejectedReserved=true;}Check(rejectedReserved,"Alt+F4 нельзя назначить");
var wrongWidget=upgraded.Copy();wrongWidget.WidgetX=float.NaN;bool badWidget=false;try{wrongWidget.Validate();}catch(InvalidDataException){badWidget=true;}Check(badWidget,"Некорректные координаты виджета отклонены");
string live=Path.Combine(root,"live.json");
using(var session=new SessionHost(live)){
    session.Start();
    var initial=await SessionWire.Send(live,new());
    var next=initial.Settings.Copy();next.Bindings["menu"]=new(){Key=119,Modifiers=1};next.WidgetOpacity=.45f;next.CoinsEnabled=false;
    var update=await SessionWire.Send(live,new(){Revision=initial.Settings.Revision,Settings=next});
    Check(update.Error==null&&update.Settings.Bindings["menu"].Key==119&&SettingsStore.Read(live).WidgetOpacity==.45f&&!update.Settings.CoinsEnabled,"Живой канал подтверждает и сохраняет настройки");
    var stale=await SessionWire.Send(live,new(){Revision=initial.Settings.Revision,Settings=next});
    Check(stale.Error!=null&&stale.Settings.Revision==update.Settings.Revision,"Устаревшая команда не перезаписывает новое состояние");
    bool owned=false;try{using var other=SettingsStore.AcquireOwner(live);}catch(IOException){owned=true;}Check(owned,"Игра владеет записью настроек всю сессию");
    var incompatible=await SessionWire.Send(live,new(){Protocol=2});Check(incompatible.Error!=null,"Несовместимый протокол отклонён");
}
using(var after=SettingsStore.AcquireOwner(live)){Check(after.CanWrite,"Владение возвращается после завершения игры");}

string shutdownFile=Path.Combine(root,"shutdown.json");
using(var shutdownSession=new SessionHost(shutdownFile)){
    var previousContext=SynchronizationContext.Current;
    SynchronizationContext.SetSynchronizationContext(new NonPumpingContext());
    var change=shutdownSession.Snapshot.Copy();change.CoinMultiplier=11;
    Task<SessionReply> save=shutdownSession.Apply(new(){Revision=change.Revision,Settings=change});
    SynchronizationContext.SetSynchronizationContext(previousContext);
    Check(await Task.WhenAny(save,Task.Delay(3000))==save&&save.Result.Error==null,"Запись не зависит от игрового SynchronizationContext при завершении");
}

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
    string local=Product.Components;
    if(Directory.Exists(local))throw new IOException("Для локальной проверки нужна отдельная папка tests/bin без пользовательского payload.");
    Directory.CreateDirectory(local);var manifest=new PackageManifest{Version=Product.Version};
    foreach(string relative in new[]{"version.dll",Product.LoaderDirectory+"/net6/MelonLoader.dll","Mods/DivineLabs.IdleSlayer.Mod.dll","UserLibs/DivineLabs.IdleSlayer.Contracts.dll","Defaults/Loader.cfg"})
    {string p=Paths.Inside(local,relative);Directory.CreateDirectory(Path.GetDirectoryName(p)!);File.WriteAllText(p,"test-component-"+relative);using var input=File.OpenRead(p);manifest.Files[relative]=Convert.ToHexString(SHA256.HashData(input));}
    File.WriteAllText(Path.Combine(local,"manifest.json"),JsonSerializer.Serialize(manifest));
    File.Copy(Path.Combine(installedGame,"GameAssembly.dll"),Path.Combine(fake,"GameAssembly.dll"));File.WriteAllText(Path.Combine(fake,"Idle Slayer.exe"),"local-test-placeholder");
    Directory.CreateDirectory(Path.Combine(fake,"MelonLoader"));File.WriteAllText(Path.Combine(fake,"MelonLoader","original.txt"),"legacy-preserved");Directory.CreateDirectory(Path.Combine(fake,"UserData"));SettingsStore.Save(Path.Combine(fake,"UserData",SettingsStore.FileName),new Settings{CoinMultiplier=123,DamageMultiplier=456});
    await Installation.EnsureReadyAsync(fake);
    Check(Installation.Installed(fake)&&!Directory.Exists(Path.Combine(fake,"MelonLoader")),"Миграция подключает DivineLabs_Loader вместо MelonLoader");
    Check((File.GetAttributes(Path.Combine(fake,Product.LoaderDirectory))&FileAttributes.ReparsePoint)==0,"Установка не зависит от junction на portable");
    Check(SettingsStore.Read(Installation.Config(fake)).CoinMultiplier==123,"Миграция сохраняет пользовательские коэффициенты");
    Check(File.Exists(Path.Combine(fake,"UserData","Loader.cfg")),"Начальные настройки Defaults устанавливаются в UserData игры");
    Installation.VerifyInstalled(fake,local);Directory.Move(local,local+"-moved");Check(Installation.Installed(fake),"Перенос portable не ломает игровые компоненты");
    Installation.Restore(fake);Check(File.ReadAllText(Path.Combine(fake,"MelonLoader","original.txt"))=="legacy-preserved"&&File.ReadAllText(Path.Combine(fake,"Mods","user.txt"))=="preserve","Откат возвращает прежние пользовательские файлы");
    // Test-generated payload is retained as an artifact, never removed from an unknown path.
    Directory.Move(local+"-moved",Path.Combine(root,"test-payload"));
}
else Console.WriteLine("Локальная проверка миграции пропущена: нужна установленная закрытая игра.");
Check(Product.ResolveHome(Path.Combine(root,"App","Launcher"))==root,"Внутренний лаунчер использует корень продукта");
Console.WriteLine("Проверки завершены. Артефакты: "+root);

sealed class NonPumpingContext:SynchronizationContext { public override void Post(SendOrPostCallback d,object state){} }
