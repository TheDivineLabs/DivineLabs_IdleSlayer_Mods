using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class ClientsView:UserControl
{
    private bool busy;
    private AppDiscovery latest;
    public ClientsView(){InitializeComponent();DivineLabs.DesktopUI.ThemeManager.Attach(this);}
    internal async void RefreshIfEmpty(){if(Apps.ItemsSource==null)await Find();}
    private async void FindClick(object sender,RoutedEventArgs e)=>await Find();
    private async void BrowseClick(object sender,RoutedEventArgs e)
    {var dialog=new OpenFolderDialog{Title=SetupText.Current["AppFolder"]};if(dialog.ShowDialog()==true)await Find(dialog.FolderName);}
    private async Task Find(string folder=null)
    {
        if(busy)return;busy=true;Discover.IsEnabled=Browse.IsEnabled=false;Status.Text=SetupText.Current["Discovering"];
        try{latest=await Task.Run(()=>InstalledApps.Find(folder));Apps.ItemsSource=latest.Apps;RefreshLanguage();}
        catch(Exception e)when(e is IOException or UnauthorizedAccessException or ArgumentException){Product.Record(e);Status.Text=SetupText.Current["DiscoveryFailed"];}
        finally{busy=false;Discover.IsEnabled=Browse.IsEnabled=true;}
    }
    internal void RefreshLanguage()
    {
        if(latest==null)return;
        Status.Text=latest.Apps.Count==0?SetupText.Current["NoClients"]:string.Format(SetupText.Current["AppsFound"],latest.Apps.Count);
        if(latest.UnavailableLocations>0)Status.Text+=" "+SetupText.Current["SomeLocationsUnavailable"];
    }
    private void OpenClick(object sender,RoutedEventArgs e)
    {
        try{string folder=(string)((Button)sender).Tag;if(Directory.Exists(folder))Process.Start(new ProcessStartInfo("explorer.exe"){UseShellExecute=false,ArgumentList={folder}});else Status.Text=SetupText.Current["DiscoveryFailed"];}
        catch(Exception error)when(error is IOException or System.ComponentModel.Win32Exception){Product.Record(error);Status.Text=SetupText.Current["DiscoveryFailed"];}
    }
}
