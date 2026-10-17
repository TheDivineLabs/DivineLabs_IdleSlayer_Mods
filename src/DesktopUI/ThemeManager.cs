using Microsoft.Win32;
using System.Windows;
using System.Windows.Media;

namespace DivineLabs.DesktopUI;

public static class ThemeManager
{
    private static readonly List<WeakReference<FrameworkElement>> Windows=new();
    public static string Selection {get;private set;}="system";
    public static event Action Changed;
    public static bool Dark=>Selection=="dark"||Selection=="system"&&SystemDark;
    private static bool SystemDark {
        get {try{using var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");return key?.GetValue("AppsUseLightTheme") is int value&&value==0;}catch(Exception e)when(e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException){return false;}}
    }
    static ThemeManager()
    {
        SystemEvents.UserPreferenceChanged+=(_,_)=>{
            if(Selection!="system")return;
            Application.Current?.Dispatcher.BeginInvoke(new Action(Refresh));
        };
    }
    public static void Attach(FrameworkElement window)
    {
        Windows.RemoveAll(reference=>!reference.TryGetTarget(out _));
        Windows.Add(new(window));Apply(window);
        void Remove()=>Windows.RemoveAll(reference=>!reference.TryGetTarget(out var target)||target==window);
        if(window is Window host)host.Closed+=(_,_)=>Remove();
        else {
            window.Unloaded+=(_,_)=>Remove();
            window.Loaded+=(_,_)=>{if(!Windows.Any(reference=>reference.TryGetTarget(out var target)&&target==window))Windows.Add(new(window));Apply(window);};
        }
    }
    public static void Select(string theme)
    {if(theme is not ("system" or "light" or "dark"))return;Selection=theme;Refresh();}
    private static void Refresh()
    {
        foreach(var reference in Windows.ToArray())if(reference.TryGetTarget(out var window))Apply(window);
        Changed?.Invoke();
    }
    private static void Apply(FrameworkElement window)
    {
        bool dark=Dark;
        foreach(var (key,light,night) in Palette){var brush=new SolidColorBrush((Color)ColorConverter.ConvertFromString(dark?night:light));brush.Freeze();window.Resources[key]=brush;}
    }
    private static readonly (string Key,string Light,string Dark)[] Palette={
        ("Background","#FAFBFE","#14151F"),("Surface","#F0F2F8","#252735"),
        ("WhiteSurface","#FFFFFF","#1D1F2C"),("HeaderSurface","#F4F6FC","#191B27"),
        ("Line","#DEE3ED","#383B50"),("Text","#1B2638","#F0F1FC"),
        ("Muted","#667389","#B3B9D0"),("Accent","#6658E8","#A99BFF"),
        ("AccentSolid","#6658E8","#7765ED"),("SelectedSurface","#F2EFFF","#34304F"),
        ("HoverSurface","#EEEAFD","#37314F"),("PressedSurface","#DDD7F7","#484064"),
        ("HoverBorder","#A69CF5","#A99BFF"),("ButtonHoverBrush","#166658E8","#229E8FFF"),
        ("ScrollTrack","#EDF0F6","#292C3C"),("ScrollThumb","#9AA5B5","#68738D"),
        ("ScrollHover","#7D899D","#95A1BA"),("ProgressTrack","#E2E7F0","#35394B"),
        ("ErrorBrush","#A32C42","#FF94AA"),("SuccessBrush","#24855C","#72D6AC"),
        ("ErrorSurface","#FFF0F1","#412936"),("SuccessSurface","#E7F6EF","#243E36")
    };
}
