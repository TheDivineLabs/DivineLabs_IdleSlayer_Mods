using System.Reflection;
using System.Globalization;
using System.Text.Json;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using DivineLabs.IdleSlayer;
[assembly: MelonInfo(typeof(ResourcesProbe),"DivineLabs • проверка ресурсов","0.4.2","DivineLabs")]
public sealed class ResourcesProbe:MelonMod
{
    private const string Folder=@"W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\resources-runtime";
    private SessionHost host;private Settings original;private Task<SessionReply> task;private SessionRequest last;private int step,index;private float next;private bool done;private double purchased,purchases;private Type menu;
    private float lastLoading;private bool startRequested;
    public override void OnLateInitializeMelon(){Application.runInBackground=true;Note("Диагностика: runInBackground=true только в процессе проверки.");}
    private static bool SceneOnly=>File.Exists(Path.Combine(Folder,"scene-only"));
    private static string Expected=>Path.Combine(Folder,"jewels-reload.json");
    private static void Note(string text){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"report.txt"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);}
    public override void OnSceneWasInitialized(int i,string name){Note("Сцена: "+name);if(name=="Title Screen"&&done&&SceneOnly){var asm=Assembly.Load("DivineLabs.IdleSlayer.Mod");bool ready=(bool)asm.GetType("DivineLabs.IdleSlayer.Mod")!.GetField("GameSceneReady",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;if(ready)Note("ОШИБКА: ресурсы доступны вне Game.");else Note("ПРОЙДЕНО: выход из Game отключил интерфейс и готовность игровых команд.");Application.Quit();}if(name=="Game"&&step==0){step=1;next=Time.realtimeSinceStartup+6;}}
    private void Send(GameCommand command){last=new(){Session=host.SessionId,Revision=host.Snapshot.Revision,Command=command};task=host.Apply(last);}
    private SessionReply Complete(){var reply=task.GetAwaiter().GetResult();if(reply.Error!=null)throw new Exception(reply.Error);return reply;}
    public override void OnUpdate()
    {
        if(step==0&&!startRequested&&UnityEngine.SceneManagement.SceneManager.GetActiveScene().name=="Title Screen"){
            var splash=UnityEngine.Object.FindObjectOfType<Il2Cpp.SplashScreen>();if(splash!=null&&splash.pressAnyKey&&!splash.isSceneTransitioning){startRequested=true;Note("Вызван штатный обработчик перехода с титульного экрана.");splash.ChangeGameScene();}}
        if(step==0&&Time.realtimeSinceStartup>lastLoading+25){lastLoading=Time.realtimeSinceStartup;Note("Ожидание сцены: "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);Capture("loading.png");}
        if(done||step==0||Time.realtimeSinceStartup<next||task!=null&&!task.IsCompleted)return;
        try{
            var p=Il2Cpp.PlayerInventory.instance;
            if(step==1){var asm=Assembly.Load("DivineLabs.IdleSlayer.Mod");host=(SessionHost)asm.GetType("DivineLabs.IdleSlayer.Mod")!.GetField("Session",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;menu=asm.GetType("DivineLabs.IdleSlayer.GameMenu")!;original=host.Snapshot.Copy();
                if(SceneOnly){Send(new());step=96;return;}
                if(File.Exists(Expected)){var values=JsonSerializer.Deserialize<double[]>(File.ReadAllText(Expected))!;if(p.jewelsOfSoul!=values[0]||p.jewelsOfSoulEarned!=values[1]||p.jewelsOfSoulPurchased!=values[2]||p.jewelsOfSoulPurchases!=values[3])throw new Exception("Камни душ не сохранились после перезапуска.");Note("ПРОЙДЕНО: камни душ и заработанный счётчик сохранились после полного перезапуска; купленные и покупки не менялись.");
                    var drop=p.drops.FirstOrDefault(d=>d!=null&&d.amount<d.GetMaxAmount());if(drop==null){Note("НЕ ПРОВЕРЕНО: сумка не имеет свободного места.");step=94;return;}
                    Send(new(){Operation="grant",Resource=ResourceKind.Material,Amount=1,MaterialId=drop.id});step=93;return;}
                Il2Cpp.SaveManager.instance.SaveGame(false,false);var disk=Il2Cpp.FileBasedPrefs.GetFileBasedPrefsSaveFileModel();double savedCoins=Il2Cpp.Utils.ParseDouble(Il2Cpp.FileBasedPrefs.GetString(disk,"Coins","0"));double bufferedCoins=Il2Cpp.Utils.ParseDouble(Il2Cpp.SaveManager.GetString("Coins"));if(savedCoins!=bufferedCoins)throw new Exception("Файл монет не совпадает с буфером после явного сохранения.");Note("ПРОЙДЕНО: сохранённые монеты совпадают с игровым буфером после явного сохранения.");
                purchased=p.jewelsOfSoulPurchased;purchases=p.jewelsOfSoulPurchases;var c=original.Copy();c.Enabled=true;task=host.Apply(new(){Revision=c.Revision,Settings=c});step=2;return;}
            if(step==2){Complete();task=null;if(File.Exists(Path.Combine(Folder,"skip-coins")))index=1;step=3;}
            if(step==3){
                if(index>=6){var c=host.Snapshot.Copy();foreach(string featureId in FeatureCatalog.FreeIds)c.SetFeatureEnabled(featureId,true);task=host.Apply(new(){Revision=c.Revision,Settings=c});step=6;return;}
                var kind=(ResourceKind)index;double amount=1;string id="";
                if(kind==ResourceKind.Coins)amount=Math.Ceiling(Math.Max(1,Math.Max(p.coins,p.lifetimeCoins)*1e-10));
                if(kind==ResourceKind.SlayerPoints)amount=Math.Ceiling(Math.Max(1,Math.Max(Il2Cpp.SlayerPoints.points,Il2Cpp.SlayerPoints.lifetime)*1e-10));
                if(kind==ResourceKind.Material){var drop=p.drops.FirstOrDefault(d=>d!=null&&!d.locked&&d.amount<d.GetMaxAmount());if(drop==null){Note("НЕ ПРОВЕРЕНО: нет материала со свободным местом.");index++;return;}id=drop.id;}
                Send(new(){Operation="grant",Resource=kind,Amount=amount,MaterialId=id});step=4;return;
            }
            if(step==4){var reply=Complete();if(reply.Result?.Saved!=true||reply.Result.Granted<=0)throw new Exception("Не подтверждено начисление "+last.Command.Resource);Note("ПРОЙДЕНО: "+last.Command.Resource+" +"+reply.Result.Granted.ToString("G8",CultureInfo.InvariantCulture)+"; запись сверена с файлом сохранения.");task=host.Apply(last);step=5;return;}
            if(step==5){Complete();task=null;index++;step=3;return;}
            if(step==6){Complete();task=null;step=7;next=Time.realtimeSinceStartup+.5f;return;}
            if(step==7){
                var power=p.powers.FirstOrDefault(x=>x!=null&&x.level<int.MaxValue-100);if(power==null)throw new Exception("Нет оборудования для проверки цены.");if(power.CalculateCost(1)!=0||power.CalculateMaxCost().cost!=0)throw new Exception("Цена оборудования не обнулена.");
                if(p.ascensionSkills.Any(x=>x!=null&&!x.IsUnlocked()&&x.cost!=0)||p.divinities.Any(x=>x!=null&&!x.unlocked&&x.cost!=0)||p.upgradesOfTime.Any(x=>x!=null&&!x.active&&x.uspCost!=0))throw new Exception("Не все цены дерева/DP/USP обнулены.");
                foreach(var craft in p.permanentCraftableItems){if(craft==null)continue;if(craft.coinsRequired!=0||craft.GetRequirements().Any(x=>x!=null&&x.amount!=0))throw new Exception("Крафт сохраняет расходы.");}
                Note("ПРОЙДЕНО: включение пяти режимов, нулевая цена оборудования/максимальной пачки, SP, DP, USP и постоянного крафта. Фактические покупки DP/USP/крафта этим прогоном не проверяются.");
                if(p.jewelsOfSoulPurchased!=purchased||p.jewelsOfSoulPurchases!=purchases)throw new Exception("Изменились платёжные счётчики.");File.WriteAllText(Expected,JsonSerializer.Serialize(new[]{p.jewelsOfSoul,p.jewelsOfSoulEarned,purchased,purchases}));
                Il2Cpp.ShopManager.instance.OpenPowers(false);Il2Cpp.ShopManager.instance.SelectStack(1);step=70;next=Time.realtimeSinceStartup+.5f;return;
            }
            if(step==70){
                var tab=Resources.FindObjectsOfTypeAll<Il2Cpp.BuyPowerTab>().FirstOrDefault(t=>t.power!=null&&t.power.level>0&&t.power.level<int.MaxValue-100&&t.buyButton!=null&&t.buyButton.interactable&&t.gameObject.activeInHierarchy);
                if(tab==null)Note("НЕ ПРОВЕРЕНО: доступная кнопка покупки оборудования не найдена.");else{int level=tab.power.level;double coins=p.coins;tab.buyButton.onClick.Invoke();if(tab.power.level!=level+1||p.coins!=coins)throw new Exception("Бесплатная покупка оборудования не сохранила баланс.");Note("ПРОЙДЕНО: фактическая покупка оборудования через обработчик кнопки, уровень +1, монеты не списаны.");}
                original.Revision=host.Snapshot.Revision;task=host.Apply(new(){Revision=original.Revision,Settings=original});step=8;return;
            }
            if(step==8){Complete();task=null;step=9;next=Time.realtimeSinceStartup+.5f;return;}
            if(step==9){menu.GetMethod("Toggle",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);menu.GetMethod("ShowResources",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);step=90;next=Time.realtimeSinceStartup+2;return;}
            if(step==96){var reply=Complete();if(reply.Result?.State==null)throw new Exception("Нет снимка ресурсов.");task=null;Note("ПРОЙДЕНО: чтение ресурсов на исправленной готовности сцены.");menu.GetMethod("Toggle",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);menu.GetMethod("ShowResources",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);step=90;next=Time.realtimeSinceStartup+2;return;}
            if(step==93){var reply=Complete();if(reply.Result?.Saved!=true||reply.Result.Granted<=0)throw new Exception("Материал не начислен.");Note("ПРОЙДЕНО: материал +1 через штатный менеджер, с лимитом сумки; запись сверена с файлом.");task=null;step=94;return;}
            if(step==94){double n=Math.Ceiling(Math.Max(1,Math.Max(p.coins,p.lifetimeCoins)*2e-10));Send(new(){Operation="grant",Resource=ResourceKind.Coins,Amount=n});step=95;return;}
            if(step==95){var reply=Complete();if(reply.Result?.Saved!=true||reply.Result.Granted<=0)throw new Exception("Монеты не подтверждены.");Note("ПРОЙДЕНО: отдельная новая выдача монет, без повторного умножения; файл совпал с игровым буфером с учётом G15.");task=null;menu.GetMethod("Toggle",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);menu.GetMethod("ShowResources",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,null);step=90;next=Time.realtimeSinceStartup+2;return;}
            if(step==90){Capture("resources.png");Note("Исходные настройки возвращены. Сохранения не откатывались. Проверка облака/другого устройства и физического ввода не выполнялась.");step=91;next=Time.realtimeSinceStartup+2;return;}
            if(step==91){done=true;Application.Quit();}
        }catch(Exception error){Note("ОШИБКА: "+error);done=true;if(host!=null&&original!=null){original.Revision=host.Snapshot.Revision;_ = host.Apply(new(){Revision=original.Revision,Settings=original});}}
    }
    private static unsafe void Capture(string name){Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,name);fixed(char* chars=path){var span=new UnityEngine.Bindings.ManagedSpanWrapper(chars,path.Length);ScreenCapture.CaptureScreenshot_Injected(ref span,1,ScreenCapture.StereoScreenCaptureMode.LeftEye);}}
}
