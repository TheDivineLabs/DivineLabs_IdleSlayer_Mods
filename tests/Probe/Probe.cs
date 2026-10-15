using System.Reflection;
using System.Diagnostics;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using DivineLabs.IdleSlayer;
[assembly: MelonInfo(typeof(Probe),"DivineLabs • проверка", "0.4.0","DivineLabs")]
public sealed class Probe:MelonMod
{
    private const string Folder=@"W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\runtime-0.4";
    private static readonly List<double> elapsed=new(1200);
    private static readonly List<long> allocated=new(1200);
    private static long started,bytes;
    private delegate int PollKeys(Settings config,bool suppress,bool capture,out KeyBinding key);
    private static bool profiling,debited;
    private static double debit;
    private int step,oldStack,oldWidth,oldHeight;
    private FullScreenMode oldMode;
    private float next,deadline,loading=25;
    private Type menu;
    private SessionHost host;
    private Settings original;
    private Task<SessionReply> pending;
    private bool completed;
    private static void Note(string text){Directory.CreateDirectory(Folder);File.AppendAllText(Path.Combine(Folder,"report.txt"),DateTime.UtcNow.ToString("O")+" "+text+Environment.NewLine);}
    public override void OnSceneWasInitialized(int index,string name){if(name=="Game"&&step==0){step=1;next=Time.realtimeSinceStartup+5;}}
    public override void OnUpdate()
    {
        if(completed)return;
        if(step==0&&Time.realtimeSinceStartup>loading){loading+=30;Capture("loading.png");Note("Сцена: "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);}
        if(step==0||Time.realtimeSinceStartup<next)return;
        try{
            if(step==1){
                var assembly=Assembly.Load("DivineLabs.IdleSlayer.Mod");var mod=assembly.GetType("DivineLabs.IdleSlayer.Mod")!;
                menu=assembly.GetType("DivineLabs.IdleSlayer.GameMenu")!;host=(SessionHost)mod.GetField("Session",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                original=host.Snapshot.Copy();Note("Вход в Game; схема "+original.SchemaVersion+", клавиша "+original.Bindings["menu"]);
                var hotkeys=assembly.GetType("DivineLabs.IdleSlayer.WindowHotkey")!;
                var poll=(PollKeys)hotkeys.GetMethod("Poll",BindingFlags.NonPublic|BindingFlags.Static)!.CreateDelegate(typeof(PollKeys));
                for(int i=0;i<100;i++)poll(host.Snapshot,false,false,out _);
                long startBytes=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)poll(host.Snapshot,false,false,out _);long delta=GC.GetAllocatedBytesForCurrentThread()-startBytes;
                Note("Горячие клавиши: "+delta+" выделенных байт на 1000 вызовов; foreground="+hotkeys.GetProperty("Foreground",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null));
                HarmonyInstance.Patch(mod.GetMethod("OnUpdate")!,prefix:new HarmonyMethod(typeof(Probe),nameof(BeginSample)),postfix:new HarmonyMethod(typeof(Probe),nameof(EndSample)));
                Call("Toggle");Capture("menu.png");
                bool blocked=!(bool)mod.GetMethod("InputAllowed",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;
                if(!blocked)throw new Exception("Ввод игры не заблокирован открытым меню.");
                Note("Открытая панель блокирует игровые обработчики.");
                var list=(System.Collections.IEnumerable)menu.GetField("cards",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                var enumerator=list.GetEnumerator();enumerator.MoveNext();var card=enumerator.Current!;
                var field=(TMP_InputField)card.GetType().GetField("Input",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(card)!;
                field.text="0";field.onEndEdit.Invoke("0");if(host.Snapshot.CoinMultiplier!=original.CoinMultiplier)throw new Exception("Неверный ввод применён.");
                field.text=original.CoinMultiplier.ToString(System.Globalization.CultureInfo.InvariantCulture);Note("Нулевой множитель отклонён обработчиком поля.");
                profiling=true;step=2;next=Time.realtimeSinceStartup+6;return;
            }
            if(step==2){
                Profile("Меню открыто");Capture("menu.png");step=20;next=Time.realtimeSinceStartup+1;return;
            }
            if(step==20){Call("ShowPage",true);Capture("settings.png");
                oldWidth=Screen.width;oldHeight=Screen.height;oldMode=Screen.fullScreenMode;Screen.SetResolution(960,600,FullScreenMode.Windowed);
                step=3;next=Time.realtimeSinceStartup+2;return;
            }
            if(step==3){
                Capture("settings-960x600.png");Screen.SetResolution(oldWidth,oldHeight,oldMode);Call("Toggle");profiling=true;
                step=4;next=Time.realtimeSinceStartup+6;return;
            }
            if(step==4){
                Profile("Меню закрыто");Capture("widget.png");
                if(File.Exists(Path.Combine(Folder,"layout-only"))){Note("Повторная проверка компоновки завершена; покупки и урон не повторялись.");completed=true;HarmonyInstance.UnpatchSelf();return;}
                var nextConfig=host.Snapshot.Copy();nextConfig.CoinMultiplier=100;nextConfig.DamageMultiplier=1000;nextConfig.CoinsEnabled=nextConfig.DamageEnabled=nextConfig.Enabled=true;
                pending=host.Apply(new(){Revision=nextConfig.Revision,Settings=nextConfig});step=5;next=Time.realtimeSinceStartup+.2f;return;
            }
            if(step==5){
                if(!pending.IsCompleted)return;if(pending.Result.Error!=null)throw new Exception(pending.Result.Error);
                oldStack=Il2Cpp.ShopManager.instance.stackSelected;Il2Cpp.ShopManager.instance.OpenPowers(false);Il2Cpp.ShopManager.instance.SelectStack(1);
                HarmonyInstance.Patch(AccessTools.Method(typeof(Il2Cpp.PlayerInventory),"AddCoins"),prefix:new HarmonyMethod(typeof(Probe),nameof(Debit)){priority=HarmonyLib.Priority.Last});
                step=6;next=Time.realtimeSinceStartup+1;return;
            }
            if(step==6){
                var inventory=Il2Cpp.PlayerInventory.instance;
                var tab=Resources.FindObjectsOfTypeAll<Il2Cpp.BuyPowerTab>().Where(t=>t.power!=null&&t.buyButton!=null&&t.power.CalculateCost(1)>0&&t.power.CalculateCost(1)<=inventory.coins).OrderBy(t=>t.power.CalculateCost(1)).FirstOrDefault();
                if(tab==null)Note("НЕ ПОДТВЕРЖДЕНО: нет доступной покупки.");
                else{
                    double before=inventory.coins,cost=tab.power.CalculateCost(1);int level=tab.power.level;debited=false;debit=0;tab.buyButton.onClick.Invoke();
                    if(!debited||tab.power.level!=level+1||Math.Abs(debit+cost)>Math.Max(1e-8,cost*1e-10)||Math.Abs(inventory.coins-(before-cost))>Math.Max(1e-8,before*1e-10))throw new Exception("Покупка не соответствует обычной стоимости.");
                    Note("Покупка через обработчик кнопки: уровень +1, списана обычная стоимость.");
                }
                step=7;deadline=Time.realtimeSinceStartup+45;next=Time.realtimeSinceStartup+1;return;
            }
            if(step==7){
                var enemy=Resources.FindObjectsOfTypeAll<Il2Cpp.EnemyGameObject>().FirstOrDefault(e=>e.gameObject.activeInHierarchy&&e.enemyStats.currentHealth>0);
                if(enemy==null&&Time.realtimeSinceStartup<deadline){next=Time.realtimeSinceStartup+1;return;}
                if(enemy!=null){
                    double hp=enemy.enemyStats.currentHealth;enemy.TakeDamage(hp/2000,Il2Cpp.EnemyDeathCause.SwordKilled,null);double after=enemy.enemyStats.currentHealth;
                    if(Math.Abs(after-hp/2)>Math.Max(1e-8,hp*1e-7))throw new Exception("Урон не соответствует ×1000.");
                    Note("Урон ×1000 подтверждён на активном враге.");
                }else Note("НЕ ПОДТВЕРЖДЕНО: активный враг не найден.");
                Il2Cpp.ShopManager.instance.SelectStack(oldStack);
                original.Revision=host.Snapshot.Revision;pending=host.Apply(new(){Revision=original.Revision,Settings=original});step=8;return;
            }
            if(step==8){
                if(!pending.IsCompleted)return;if(pending.Result.Error!=null)throw new Exception(pending.Result.Error);
                Note("Исходные настройки восстановлены. Сохранения игры не откатывались. Физические клики и клавиши этим тестом не проверяются.");
                completed=true;HarmonyInstance.UnpatchSelf();
            }
        }catch(Exception e){Note("ОШИБКА: "+e);profiling=false;completed=true;HarmonyInstance.UnpatchSelf();if(original!=null){original.Revision=host.Snapshot.Revision;_ = host.Apply(new(){Revision=original.Revision,Settings=original});}if(oldWidth>0)Screen.SetResolution(oldWidth,oldHeight,oldMode);}
    }
    private void Call(string name,params object[] args)=>menu.GetMethod(name,BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,args);
    private static void BeginSample(){if(profiling){bytes=GC.GetAllocatedBytesForCurrentThread();started=Stopwatch.GetTimestamp();}}
    private static void EndSample(){if(profiling){long ticks=Stopwatch.GetTimestamp()-started;long allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;if(elapsed.Count<1000){elapsed.Add(ticks*1000d/Stopwatch.Frequency);allocated.Add(allocatedBytes);}}}
    private static void Profile(string label){profiling=false;if(elapsed.Count>0){var ordered=elapsed.OrderBy(x=>x).ToArray();Note(label+": n="+elapsed.Count+", avg="+elapsed.Average().ToString("F4")+" ms, p95="+ordered[(int)((ordered.Length-1)*.95)].ToString("F4")+" ms, bytes/frame="+allocated.Average().ToString("F1"));}elapsed.Clear();allocated.Clear();}
    private static unsafe void Capture(string name){Directory.CreateDirectory(Folder);string path=Path.Combine(Folder,name);fixed(char* chars=path){var span=new UnityEngine.Bindings.ManagedSpanWrapper(chars,path.Length);ScreenCapture.CaptureScreenshot_Injected(ref span,1,ScreenCapture.StereoScreenCaptureMode.LeftEye);}}
    private static void Debit(double __0){if(__0<0){debited=true;debit+=__0;}}
}