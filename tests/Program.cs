using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using DivineLabs.IdleSlayer;
using DivineLabs.IdleSlayer.Runtime;
static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("ПРОЙДЕНО: "+name);}
if(args.Contains("--copies")){CopyDiscoveryChecks.Run();return;}
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
string retryRoot=Path.Combine(root,"interrupted");
Directory.CreateDirectory(Path.Combine(retryRoot,"Data","Logs"));
File.WriteAllText(Path.Combine(retryRoot,"Data","Logs","setup.log"),"at DivineLabs.IdleSlayer.Setup.SetupOperations.Install");
string retryStage=Path.Combine(retryRoot,"Data","Cache","Work","setup-"+Guid.NewGuid().ToString("N"),"App","Components");
Directory.CreateDirectory(retryStage);
File.WriteAllText(Path.Combine(retryStage,"manifest.json"),"{\"Files\":{\"Mods/DivineLabs.IdleSlayer.Mod.dll\":\"hash\"}}");
Check(DivineLabs.IdleSlayer.Maintenance.InstallTarget.Inspect(retryRoot)==DivineLabs.IdleSlayer.Maintenance.InstallTargetKind.Recoverable,"Остатки установки 0.4.0 распознаются без удаления");
File.WriteAllText(Path.Combine(retryRoot,"personal.txt"),"preserve");
Check(DivineLabs.IdleSlayer.Maintenance.InstallTarget.Inspect(retryRoot)==DivineLabs.IdleSlayer.Maintenance.InstallTargetKind.Occupied,"Посторонние файлы исключают автоматическое принятие папки");
File.Delete(Path.Combine(retryRoot,"personal.txt"));
string marked=Path.Combine(root,"marked","Data","Config");
Directory.CreateDirectory(marked);File.WriteAllText(Path.Combine(marked,".divinelabs-owner"),DivineLabs.IdleSlayer.Maintenance.InstallTarget.ProductId);
Check(DivineLabs.IdleSlayer.Maintenance.InstallTarget.Inspect(Path.Combine(root,"marked"))==DivineLabs.IdleSlayer.Maintenance.InstallTargetKind.Installed,"Отмеченная прерванная установка допускает повтор");
string foreign=Path.Combine(root,"foreign","Data","Logs");Directory.CreateDirectory(foreign);File.WriteAllText(Path.Combine(foreign,"setup.log"),"unrelated");
Check(DivineLabs.IdleSlayer.Maintenance.InstallTarget.Inspect(Path.Combine(root,"foreign"))==DivineLabs.IdleSlayer.Maintenance.InstallTargetKind.Occupied,"Произвольный Data с чужим журналом не считается нашей установкой");
Check(DivineLabs.IdleSlayer.Maintenance.InstallTarget.DefaultDirectory.StartsWith(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)+Path.DirectorySeparatorChar),"Новая установка предлагается в пользовательской папке программ");
string progressFile=Path.Combine(root,"operation.txt");
Check(DivineLabs.IdleSlayer.Maintenance.OperationReport.TryWrite(progressFile,"Распаковка",10),"Первый статус установки опубликован");
using(var heldReport=new FileStream(progressFile,FileMode.Open,FileAccess.Read,FileShare.Read)){
    Check(!DivineLabs.IdleSlayer.Maintenance.OperationReport.TryWrite(progressFile,"Занято",20),"Заблокированный статус не прерывает установку исключением");
}
using(var sharedReport=new FileStream(progressFile,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){
    Check(DivineLabs.IdleSlayer.Maintenance.OperationReport.TryWrite(progressFile,"Новый статус",30),"Чтение статуса не блокирует атомарную замену");
}
int invalidProgress=0,observedProgress=0;
var progressWriter=Task.Run(()=>{for(int i=0;i<500;i++)DivineLabs.IdleSlayer.Maintenance.OperationReport.TryWrite(progressFile,"Этап "+i,i%101);});
while(!progressWriter.IsCompleted){
    if(DivineLabs.IdleSlayer.Maintenance.OperationReport.TryRead(progressFile,out int percent,out string message)){
        observedProgress++;if(percent<0||percent>100||message.Length==0)invalidProgress++;
    }
    await Task.Delay(1);
}
await progressWriter;
Check(observedProgress>0&&invalidProgress==0,"Параллельный опрос получает только целые статусы без конфликта доступа");
Check(DivineLabs.IdleSlayer.Maintenance.OperationReport.TryWrite(progressFile,"Готово",100)&&DivineLabs.IdleSlayer.Maintenance.OperationReport.TryRead(progressFile,out int finalPercent,out string finalMessage)&&finalPercent==100&&finalMessage=="Готово","Финальный статус доступен после операции");
Check(Settings.MultiplyIncome(-200,100)==-200,"Покупка не умножает расход");
Check(10+Settings.MultiplyIncome(2,100)+Settings.MultiplyIncome(-200,100)==10,"Доход и покупка сохраняют верный баланс");
Check(Settings.MultiplyIncome(double.MaxValue,100)==double.MaxValue,"Защита от переполнения");
Check(Settings.MultiplyIncome(25,1)==25,"Отключение через ×1");
Check(Settings.MultiplyIncome(2,1e100,1e90)==2&&Settings.MultiplyIncome(2,100,1000)==200,"Начисление учитывает свободный диапазон игрового баланса");
Check(Settings.Valid(1e60)&&Settings.Valid(double.MaxValue)&&Settings.MultiplyIncome(2,1e60)==2e60,"Большие конечные множители разрешены без прежнего лимита");
Check(Settings.MultiplyIncome(1e250,1e100)==1e250&&Settings.MultiplyIncome(-17,1e60)==-17,"Большие множители не передают переполнение и не меняют списания");
string largeFile=Path.Combine(root,"large.json");var large=new Settings{CoinMultiplier=1e60,DamageMultiplier=1e70,SoulsEnabled=true,SoulMultiplier=1e55,ActiveListVisible=false};
SettingsStore.Save(largeFile,large);var largeRead=SettingsStore.Read(largeFile);
Check(largeRead.CoinMultiplier==1e60&&largeRead.SoulMultiplier==1e55&&largeRead.SoulsEnabled&&!largeRead.ActiveListVisible,"Большие числа, души и активный список сохраняются в JSON");
string oldV2=Path.Combine(root,"old-v2.json");var oldMap=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new Settings()))!;oldMap["Bindings"]!.AsObject().Remove("souls");oldMap.AsObject().Remove("SoulsEnabled");oldMap.AsObject().Remove("SoulMultiplier");oldMap.AsObject().Remove("ActiveListVisible");
File.WriteAllText(oldV2,oldMap.ToJsonString());var newV2=SettingsStore.Load(oldV2);
Check(!newV2.SoulsEnabled&&newV2.Bindings["souls"].Key==0&&newV2.ActiveListVisible&&File.Exists(oldV2+".pre-souls.bak"),"Старые настройки v2 сохраняются перед добавлением душ и активного списка");

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
    var incompatible=await SessionWire.Send(live,new(){Protocol=99});Check(incompatible.Error!=null,"Несовместимый протокол отклонён");
}
using(var after=SettingsStore.AcquireOwner(live)){Check(after.CanWrite,"Владение возвращается после завершения игры");}

// Resource commands are one-shot operations, fenced by game session and request ID.
bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
Check(Reject(()=>new GameCommand{Operation="grant",Amount=double.PositiveInfinity}.Validate())&&Reject(()=>new GameCommand{Operation="grant",Amount=1.5}.Validate())&&Reject(()=>new GameCommand{Operation="grant",Resource=(ResourceKind)99,Amount=1}.Validate()),"Некорректная выдача отклонена до игрового потока");
Check(Reject(()=>GrantMath.Add(double.MaxValue,double.MaxValue))&&Reject(()=>GrantMath.Add(1e100,1))&&GrantMath.Add(20,3)==23,"Выдача защищена от переполнения и потери значения при округлении");
Check(GrantMath.SameSerializedNumber(1234567890123456,1234567890123460)&&!GrantMath.SameSerializedNumber(1e30,1e30+1e20),"Проверка сохранения учитывает G15 игры, но отвергает устаревший баланс");
Check(Reject(()=>GrantMath.CheckUltraTotal(10000)),"USP ограничен конечностью формулы игры");
string resourceFile=Path.Combine(root,"commands.json");int grants=0;
string firstSession;
using(var commands=new SessionHost(resourceFile,gameCommands:(command,cancel)=>Task.FromResult(new GameCommandResult{Changed=true,Saved=true,Granted=++grants}))){
    firstSession=commands.SessionId;
    var request=new SessionRequest{Session=commands.SessionId,Revision=commands.Snapshot.Revision,Command=new(){Operation="grant",Amount=7}};
    var first=await commands.Apply(request);var repeat=await commands.Apply(request);
    Check(first.Error==null&&repeat.Result.Granted==first.Result.Granted&&grants==1,"Повтор номера команды возвращает квитанцию без повторной выдачи");
    request.Command=request.Command with{Amount=8};Check((await commands.Apply(request)).Error!=null&&grants==1,"Тот же номер с другим количеством отклонён");
    var old=new SessionRequest{Session="old",Command=new(){Operation="grant",Amount=1}};Check((await commands.Apply(old)).Error!=null&&grants==1,"Чужая игровая сессия не может повторить выдачу");
    var staleCommand=new SessionRequest{Session=commands.SessionId,Revision=99,Command=new(){Operation="grant",Amount=1}};Check((await commands.Apply(staleCommand)).Error!=null&&grants==1,"Команда со старой ревизией не выполняется");
}
using(var restarted=new SessionHost(resourceFile,gameCommands:(command,cancel)=>Task.FromResult(new GameCommandResult{Granted=++grants}))){Check((await restarted.Apply(new(){Session=firstSession,Command=new(){Operation="grant",Amount=1}})).Error!=null&&grants==1,"После перезапуска старая команда не выдаёт ресурс заново");}
string oldResources=Path.Combine(root,"old-resources.json");var resourceMap=System.Text.Json.Nodes.JsonNode.Parse(JsonSerializer.Serialize(new Settings()))!;foreach(string id in FeatureCatalog.FreeIds)resourceMap["Bindings"]!.AsObject().Remove(id);File.WriteAllText(oldResources,resourceMap.ToJsonString());var resourceSettings=SettingsStore.Load(oldResources);
Check(FeatureCatalog.FreeIds.All(id=>!resourceSettings.FeatureEnabled(id)&&resourceSettings.Bindings.ContainsKey(id))&&File.Exists(oldResources+".pre-resources.bak"),"Настройки до ресурсов мигрируют с резервной копией; бесплатные покупки выключены");

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
    Directory.CreateDirectory(local);var manifest=new PackageManifest{Version=Product.ComponentVersion};
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
