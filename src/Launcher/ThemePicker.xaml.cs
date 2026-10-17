using System.Windows;
using System.Windows.Controls;
using DivineLabs.DesktopUI;
using System.Windows.Media;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class ThemePicker:UserControl
{
    private bool refreshing;
    private sealed record Option(string Id,string Title)
    {
        public Geometry Icon=>Id switch{
            "light"=>Sun,"dark"=>Moon,_=>Monitor};
        public override string ToString()=>Title;
    }
    private static readonly Geometry Sun=Geometry.Parse("M8,4 A4,4 0 1 1 8,12 A4,4 0 1 1 8,4 M8,0 L8,2 M8,14 L8,16 M0,8 L2,8 M14,8 L16,8 M2.3,2.3 L3.7,3.7 M12.3,12.3 L13.7,13.7 M2.3,13.7 L3.7,12.3 M12.3,3.7 L13.7,2.3");
    private static readonly Geometry Moon=Geometry.Parse("M13.5,10.5 A6.5,6.5 0 1 1 5.5,2 A5.5,5.5 0 0 0 13.5,10.5 Z");
    private static readonly Geometry Monitor=Geometry.Parse("M1,2 H15 V12 H1 Z M8,12 V15 M5,15 H11");
    public ThemePicker()
    {
        InitializeComponent();Refresh();
        Loaded+=(_,_)=>{SetupText.Current.PropertyChanged+=LanguageChanged;ThemeManager.Changed+=Refresh;Refresh();};
        Unloaded+=(_,_)=>{SetupText.Current.PropertyChanged-=LanguageChanged;ThemeManager.Changed-=Refresh;};
    }
    private void LanguageChanged(object sender,System.ComponentModel.PropertyChangedEventArgs e)=>Refresh();
    private void Refresh()
    {
        refreshing=true;
        try{var options=new[]{new Option("system",SetupText.Current["ThemeSystem"]),new Option("light",SetupText.Current["ThemeLight"]),new Option("dark",SetupText.Current["ThemeDark"])};Choice.ItemsSource=options;Choice.SelectedItem=options.Single(x=>x.Id==ThemeManager.Selection);Choice.ToolTip=SetupText.Current["Theme"];}
        finally{refreshing=false;}
    }
    private void ThemeChanged(object sender,SelectionChangedEventArgs e)
    {if(refreshing||Choice.SelectedItem is not Option option)return;ThemeManager.Select(option.Id);InterfacePreferences.Save();}
}
