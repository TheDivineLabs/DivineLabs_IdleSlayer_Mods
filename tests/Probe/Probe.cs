using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using Il2CppTMPro;
using DivineLabs.IdleSlayer;
[assembly: MelonInfo(typeof(Probe),"DivineLabs • проверка", "0.1.0","DivineLabs")]
public sealed class Probe:MelonMod
{
    private bool lastOpen;private float damageDeadline;private int oldWidth,oldHeight;private FullScreenMode oldMode;private float nextLoadingCapture=20;private int step;private float next;private Type menu,mod;private string folder,config;private Settings original;private int oldStack;private static bool purchaseObserved;private static double debit;private bool completed;
    public override void OnSceneWasInitialized(int i,string name)
    {if(name=="Game"&&step==0){step=1;next=Time.realtimeSinceStartup+4;}}
    private void Note(string text){LoggerInstance.Msg(text);File.AppendAllText(Path.Combine(folder,"report.txt"),text+Environment.NewLine);}
    public override void OnUpdate()
    {
        if(step==0 && Time.realtimeSinceStartup>nextLoadingCapture){nextLoadingCapture+=30;try{string f=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,"UserData","DivineLabsValidation");Directory.CreateDirectory(f);Capture(Path.Combine(f,"loading.png"));LoggerInstance.Msg("Сцена: "+UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);}catch(Exception e){LoggerInstance.Error(e.Message);}}
        if(completed && menu!=null){bool open=(bool)menu.GetProperty("Open",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;if(open!=lastOpen){lastOpen=open;Note("Изменение состояния меню: "+open);Capture(Path.Combine(folder,"menu-toggle.png"));}}
        if(step==0||completed||Time.realtimeSinceStartup<next)return;
        try
        {
            if(step==1)
            {
                folder=Path.Combine(Path.GetDirectoryName(Environment.ProcessPath)!,"UserData","DivineLabsValidation");Directory.CreateDirectory(folder);
                config=Path.Combine(Path.GetDirectoryName(folder)!,SettingsStore.FileName);original=SettingsStore.Read(config);
                var a=Assembly.Load("DivineLabs.IdleSlayer.Mod");menu=a.GetType("DivineLabs.IdleSlayer.GameMenu")!;mod=a.GetType("DivineLabs.IdleSlayer.Mod")!;
                menu.GetMethod("Toggle",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);Note("Меню открыто после загрузки Game.");step=2;next=Time.realtimeSinceStartup+2;return;
            }
            if(step==2)
            {
                try { Capture(Path.Combine(folder,"menu.png")); } catch(Exception e) { Note("Снимок недоступен: "+e.Message); }
                var c=(TMP_InputField)menu.GetField("coins",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                var d=(TMP_InputField)menu.GetField("damage",BindingFlags.NonPublic|BindingFlags.Static)!.GetValue(null)!;
                var save=menu.GetMethod("TrySave",BindingFlags.NonPublic|BindingFlags.Static)!;
                c.text="0";save.Invoke(null,new object[]{true});if(SettingsStore.Read(config).CoinMultiplier!=original.CoinMultiplier)throw new Exception("Невалидное значение было применено");Note("Некорректный ввод отклонён.");
                c.text="100";d.text="1000";save.Invoke(null,new object[]{true});if(SettingsStore.Read(config).CoinMultiplier!=100)throw new Exception("Настройки не сохранены");Note("Ввод ×100/×1000 сохранён через обработчик меню.");
                var allowed=(bool)mod.GetMethod("InputAllowed",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null)!;if(allowed)throw new Exception("Ввод игры не заблокирован");Note("Блокировка игровых действий при открытом меню подтверждена.");
                menu.GetMethod("Toggle",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);
                oldStack=Il2Cpp.ShopManager.instance.stackSelected;Il2Cpp.ShopManager.instance.OpenPowers(false);Il2Cpp.ShopManager.instance.SelectStack(1);
                HarmonyInstance.Patch(AccessTools.Method(typeof(Il2Cpp.PlayerInventory),"AddCoins"),prefix:new HarmonyMethod(typeof(Probe),nameof(Debit)){priority=HarmonyLib.Priority.Last});
                step=3;next=Time.realtimeSinceStartup+2;return;
            }
            if(step==3)
            {
                var inv=Il2Cpp.PlayerInventory.instance;
                var tab=Resources.FindObjectsOfTypeAll<Il2Cpp.BuyPowerTab>().Where(t=>t.power!=null&&t.buyButton!=null&&t.power.CalculateCost(1)>0&&t.power.CalculateCost(1)<=inv.coins).OrderBy(t=>t.power.CalculateCost(1)).FirstOrDefault();
                if(tab!=null)
                {
                    double before=inv.coins,cost=tab.power.CalculateCost(1);int level=tab.power.level;purchaseObserved=false;debit=0;
                    tab.buyButton.onClick.Invoke();
                    if(!purchaseObserved||tab.power.level!=level+1||Math.Abs(debit+cost)>Math.Max(1e-8,cost*1e-10)||Math.Abs(inv.coins-(before-cost))>Math.Max(1e-8,before*1e-10))throw new Exception("Реальная покупка не прошла проверку стоимости");
                    Note("Покупка через событие кнопки магазина: уровень +1, списано ровно "+cost+", множитель расходов отсутствует.");
                }
                else Note("Покупка не проверена: нет доступной по цене кнопки.");
                step=5;damageDeadline=Time.realtimeSinceStartup+60;next=Time.realtimeSinceStartup+1;return;
            }
            if(step==5){
                var enemy=Resources.FindObjectsOfTypeAll<Il2Cpp.EnemyGameObject>().FirstOrDefault(e=>e.gameObject.activeInHierarchy&&e.enemyStats.currentHealth>0);
                if(enemy!=null)
                {double hp=enemy.enemyStats.currentHealth;enemy.TakeDamage(hp/2000,Il2Cpp.EnemyDeathCause.SwordKilled,null);double after=enemy.enemyStats.currentHealth;if(Math.Abs(after-hp/2)>Math.Max(1e-8,hp*1e-7))throw new Exception("Урон не соответствует ×1000");Note("Урон ×1000 проверен на активном враге: здоровье "+hp+" → "+after);}
                else {if(Time.realtimeSinceStartup<damageDeadline){next=Time.realtimeSinceStartup+1;return;}Note("Урон не проверен: активный враг не найден.");}
                Restore();menu.GetMethod("Toggle",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,null);Note("Исходные настройки восстановлены. Сохранение игры не откатывалось.");step=4;next=Time.realtimeSinceStartup+2;return;
            }
            if(step==4){try { Capture(Path.Combine(folder,"menu-final.png")); } catch {}oldWidth=Screen.width;oldHeight=Screen.height;oldMode=Screen.fullScreenMode;Screen.SetResolution(960,600,FullScreenMode.Windowed);step=6;next=Time.realtimeSinceStartup+2;return;}
            if(step==6){Capture(Path.Combine(folder,"menu-resized.png"));Screen.SetResolution(oldWidth,oldHeight,oldMode);Note("Проверка завершена; размер окна восстановлен.");completed=true;lastOpen=true;HarmonyInstance.UnpatchSelf();}
        }
        catch(Exception e){LoggerInstance.Error(e.ToString());if(folder!=null)Note("ОШИБКА: "+e);try{Restore();}catch{}completed=true;HarmonyInstance.UnpatchSelf();}
    }
    private void Restore(){if(Il2Cpp.ShopManager.instance!=null)Il2Cpp.ShopManager.instance.SelectStack(oldStack);if(original!=null&&mod!=null)mod.GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,new object[]{original});}
    private static unsafe void Capture(string path){fixed(char* chars=path){var span=new UnityEngine.Bindings.ManagedSpanWrapper(chars,path.Length);ScreenCapture.CaptureScreenshot_Injected(ref span,1,ScreenCapture.StereoScreenCaptureMode.LeftEye);}}
    private static void Debit(double __0){if(__0<0){purchaseObserved=true;debit+=__0;}}
}
