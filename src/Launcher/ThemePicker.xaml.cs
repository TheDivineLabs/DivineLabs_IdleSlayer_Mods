using System.Windows;
using System.Windows.Controls;
using DivineLabs.DesktopUI;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class ThemePicker:UserControl
{
    private bool refreshing;
    private sealed record Option(string Id,string Title){public override string ToString()=>Title;}
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
