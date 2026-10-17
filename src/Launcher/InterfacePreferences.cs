using Microsoft.Win32;
using DivineLabs.DesktopUI;

namespace DivineLabs.IdleSlayer.Launcher;

// Shared by portable, installer and the temporary uninstaller, for this Windows user.
internal static class InterfacePreferences
{
    private const string Key=@"Software\DivineLabs\IdleSlayer\Interface";
    private static bool loaded;
    internal static void Load()
    {
        if(AppContext.TryGetSwitch("DivineLabs.Interface.Transient",out bool transient)&&transient)return;
        if(loaded)return;loaded=true;
        try{using var key=Registry.CurrentUser.OpenSubKey(Key);ThemeManager.Select(key?.GetValue("Theme") as string??"system");SetupText.Current.Select(key?.GetValue("Language") as string??"auto");}
        catch(Exception e)when(e is System.Security.SecurityException or UnauthorizedAccessException or IOException){ }
    }
    internal static void Save()
    {
        if(AppContext.TryGetSwitch("DivineLabs.Interface.Transient",out bool transient)&&transient)return;
        try{using var key=Registry.CurrentUser.CreateSubKey(Key);key.SetValue("Theme",ThemeManager.Selection);key.SetValue("Language",SetupText.Current.Selection);}
        catch(Exception e)when(e is System.Security.SecurityException or UnauthorizedAccessException or IOException){ }
    }
}
