using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace DivineLabs.IdleSlayer;
// Only hide loader installation markers from the game's IL2CPP filesystem API.
// CLR filesystem calls, save files, authentication and normal exit remain untouched.
internal static class StartupCompatibility
{
    private static string gameRoot;
    private static readonly HashSet<string> Markers = new(StringComparer.OrdinalIgnoreCase)
    { "Mods", "MelonLoader", "UserData", "UserLibs", "Plugins", "BepInEx", "dotnet", "Melon Loader", "version.dll", "winhttp.dll", "opengl32.dll", "doorstop_config.ini", ".doorstop_version", "changelog.txt" };
    internal static void Install(HarmonyLib.Harmony harmony, string game)
    {
        gameRoot=Path.GetFullPath(game).TrimEnd(Path.DirectorySeparatorChar);
        harmony.Patch(AccessTools.Method(typeof(Il2CppSystem.IO.Directory),"Exists",new[]{typeof(string)}), prefix:new HarmonyMethod(typeof(StartupCompatibility),nameof(Exists)));
        harmony.Patch(AccessTools.Method(typeof(Il2CppSystem.IO.File),"Exists",new[]{typeof(string)}), prefix:new HarmonyMethod(typeof(StartupCompatibility),nameof(Exists)));
        foreach(var method in typeof(Il2CppSystem.IO.Directory).GetMethods().Where(m=>m.Name=="GetDirectories" && m.ReturnType==typeof(Il2CppStringArray)))
            harmony.Patch(method,postfix:new HarmonyMethod(typeof(StartupCompatibility),nameof(Directories)));
        Mod.Instance.LoggerInstance.Msg("Совместимость запуска подключена.");
    }
    private static bool IsMarker(string path)
    {
        if(string.IsNullOrEmpty(path)) return false;
        try { string full=Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
            return string.Equals(Path.GetDirectoryName(full),gameRoot,StringComparison.OrdinalIgnoreCase) && Markers.Contains(Path.GetFileName(full)); }
        catch { return false; }
    }
    private static bool Exists(string __0,ref bool __result)
    { if(!IsMarker(__0)) return true; __result=false; return false; }
    private static void Directories(ref Il2CppStringArray __result)
    { if(__result!=null) __result=new Il2CppStringArray(__result.Where(x=>!IsMarker(x)).ToArray()); }
}
