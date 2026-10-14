using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
[assembly: MelonInfo(typeof(DivineLabs.IdleSlayer.Mod), "DivineLabs • Idle Slayer", "0.3.0", "DivineLabs")]
[assembly: MelonGame("Pablo Leban", "Idle Slayer")]
namespace DivineLabs.IdleSlayer;
public sealed class Mod : MelonMod
{
    internal static Settings Config = new();
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
            Config = SettingsStore.Load(ConfigPath, LoggerInstance.Warning);
            StartupCompatibility.Install(HarmonyInstance, game);
            Patch("PlayerInventory", "AddCoins", nameof(Coins));
            Patch("EnemyGameObject", "TakeDamage", nameof(Damage));
            var input = AccessTools.Method(typeof(Il2Cpp.InputManager), "Update");
            if (input == null) throw new MissingMethodException("InputManager.Update");
            HarmonyInstance.Patch(input, prefix: new HarmonyMethod(typeof(Mod), nameof(InputAllowed)));
            foreach (string method in new[] { "OnPointerDown", "OnPointerUp" })
            { var m = AccessTools.Method(typeof(Il2Cpp.JumpPanel), method); if(m != null) HarmonyInstance.Patch(m, prefix: new HarmonyMethod(typeof(Mod), nameof(InputAllowed))); }
            foreach(string action in new[]{"CheckAction","TryJump"})
            { var m=AccessTools.Method(typeof(Il2Cpp.PlayerMovement),action);if(m!=null)HarmonyInstance.Patch(m,prefix:new HarmonyMethod(typeof(Mod),nameof(InputAllowed))); }
            Ready = true; LoggerInstance.Msg($"Готово. Монеты ×{Config.CoinMultiplier}, урон ×{Config.DamageMultiplier}. Меню — F8.");
        }
        catch (Exception e) { HarmonyInstance.UnpatchSelf(); LoggerInstance.Error("Не удалось включить модификации: " + e); }
    }
    private void Patch(string type, string method, string prefix)
    {
        var t=Assembly.Load("Assembly-CSharp").GetType("Il2Cpp."+type)!;
        var m=t.GetMethods().Single(x=>x.Name==method && x.GetParameters().Length>0 && x.GetParameters()[0].ParameterType==typeof(double));
        HarmonyInstance.Patch(m, prefix:new HarmonyMethod(typeof(Mod),prefix));
    }
    private static void Coins(ref double __0) => __0 = Settings.MultiplyIncome(__0, Config.Enabled ? Config.CoinMultiplier : 1);
    private static void Damage(ref double __0) => __0 = Settings.MultiplyIncome(__0, Config.Enabled ? Config.DamageMultiplier : 1);
    private static bool InputAllowed() => !GameMenu.Open;
    public override void OnSceneWasInitialized(int index, string name)
    { if(Ready && name=="Game") { try { GameMenu.Create(); } catch(Exception e) { LoggerInstance.Error("Ошибка создания меню: "+e); } } }
    public override void OnUpdate()
    { if(Ready) GameMenu.Tick(); }
    internal static void Save(Settings settings)
    { SettingsStore.Save(ConfigPath,settings); Config=settings; }
}
