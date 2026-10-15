using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
[assembly: MelonInfo(typeof(DivineLabs.IdleSlayer.Mod), "DivineLabs • Idle Slayer", "0.4.2", "DivineLabs")]
[assembly: MelonGame("Pablo Leban", "Idle Slayer")]
namespace DivineLabs.IdleSlayer;
public sealed class Mod : MelonMod
{
    internal static SessionHost Session;
    internal static Settings Config => Session?.Snapshot ?? new Settings();
    internal static readonly System.Collections.Concurrent.ConcurrentQueue<string> Messages=new();
    internal static string ConfigPath;
    internal static Mod Instance;
    internal static bool Ready;
    public override void OnInitializeMelon()
    {
        Instance = this;
        try
        {
            string game = Path.GetDirectoryName(Environment.ProcessPath)!;
            if (!Compatibility.Supported(game)) { LoggerInstance.Warning("Версия игры не проверена. Модификации отключены; запустите обычную игру через лаунчер."); return; }
            ConfigPath = Path.Combine(game, "UserData", SettingsStore.FileName);
            Session = new SessionHost(ConfigPath,e=>LoggerInstance.Error(e.ToString()));
            StartupCompatibility.Install(HarmonyInstance, game);
            Patch("PlayerInventory", "AddCoins", nameof(Coins));
            Patch("SlayerPoints", "AddSouls", nameof(Souls));
            Patch("EnemyGameObject", "TakeDamage", nameof(Damage));
            var input = AccessTools.Method(typeof(Il2Cpp.InputManager), "Update");
            if (input == null) throw new MissingMethodException("InputManager.Update");
            HarmonyInstance.Patch(input, prefix: new HarmonyMethod(typeof(Mod), nameof(InputAllowed)));
            foreach (string method in new[] { "OnPointerDown", "OnPointerUp" })
            { var m = AccessTools.Method(typeof(Il2Cpp.JumpPanel), method); if(m != null) HarmonyInstance.Patch(m, prefix: new HarmonyMethod(typeof(Mod), nameof(InputAllowed))); }
            foreach(string action in new[]{"CheckAction","TryJump"})
            { var m=AccessTools.Method(typeof(Il2Cpp.PlayerMovement),action);if(m!=null)HarmonyInstance.Patch(m,prefix:new HarmonyMethod(typeof(Mod),nameof(InputAllowed))); }
            Session.Start(); Ready = true; LoggerInstance.Msg($"Готово. Монеты ×{Config.CoinMultiplier}, урон ×{Config.DamageMultiplier}. Меню — {Config.Bindings["menu"]}.");
        }
        catch (Exception e) { Session?.Dispose();Session=null; HarmonyInstance.UnpatchSelf(); LoggerInstance.Error("Не удалось включить модификации: " + e); }
    }
    private void Patch(string type, string method, string prefix)
    {
        var t=Assembly.Load("Assembly-CSharp").GetType("Il2Cpp."+type)!;
        var m=t.GetMethods().Single(x=>x.Name==method && x.GetParameters().Length>0 && x.GetParameters()[0].ParameterType==typeof(double));
        HarmonyInstance.Patch(m, prefix:new HarmonyMethod(typeof(Mod),prefix));
    }
    private static void Coins(Il2Cpp.PlayerInventory __instance,ref double __0)
    {
        var c=Config;if(!c.Enabled||!c.CoinsEnabled)return;
        // AddCoins adds the award to both balances; test both sums before handing it back.
        double available=Math.Min(double.MaxValue-Math.Max(0,__instance.coins),double.MaxValue-Math.Max(0,__instance.lifetimeCoins));
        __0=Settings.MultiplyIncome(__0,c.CoinMultiplier,available);
    }
    private static void Damage(ref double __0){var c=Config;__0=Settings.MultiplyIncome(__0,c.Enabled&&c.DamageEnabled?c.DamageMultiplier:1);}
    private static void Souls(ref double __0,bool __1)
    {
        // The game already applied its soul bonuses; dontAdd=true is only a projection.
        if(__1)return;
        var c=Config;__0=Settings.MultiplyIncome(__0,c.Enabled&&c.SoulsEnabled?c.SoulMultiplier:1);
    }
    private static bool InputAllowed() => !GameMenu.BlocksGameInput;
    public override void OnSceneWasInitialized(int index, string name)
    { if(Ready && name=="Game") { try { GameMenu.Create(); } catch(Exception e) { LoggerInstance.Error("Ошибка создания меню: "+e); } } }
    public override void OnUpdate()
    { if(Ready) GameMenu.Tick(); }
    internal static async void Change(Action<Settings> action)
    {
        try{var c=Config.Copy();long revision=c.Revision;action(c);c.Validate();var result=await Session.Apply(new(){Revision=revision,Settings=c});Messages.Enqueue(result.Error??"Настройки применены.");}
        catch(Exception e){Messages.Enqueue(e.Message);Instance.LoggerInstance.Error(e.ToString());}
    }
    public override void OnApplicationQuit(){Ready=false;Session?.Dispose();}
}