using System.Windows;
using System.Windows.Controls;
using DivineLabs.IdleSlayer.Runtime;
using DivineLabs.IdleSlayer.Maintenance;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class InstallationFinder:UserControl
{
    public static readonly DependencyProperty CurrentFolderProperty=DependencyProperty.Register(nameof(CurrentFolder),typeof(string),typeof(InstallationFinder));
    public string CurrentFolder{get=>(string)GetValue(CurrentFolderProperty);set=>SetValue(CurrentFolderProperty,value);}
    public event EventHandler<string> FolderSelected;
    private CancellationTokenSource search;
    private bool loaded;
    private volatile bool closed;
    private long request;
    public InstallationFinder(){InitializeComponent();DivineLabs.DesktopUI.ThemeManager.Attach(this);Unloaded+=(_,_)=>Stop();}
    private async void Expanded(object sender,RoutedEventArgs e)
    {
        if(loaded)return;loaded=true;long revision=++request;
        string current=CurrentFolder;var rows=await Task.Run(()=>InstalledCopies.Known(current));
        await OnUi(()=>{if(closed||revision!=request)return;Copies.ItemsSource=rows;Status.Text=SetupText.Current[rows.Count==0?"CopiesNotKnown":"CopiesKnown"];});
    }
    public void Stop(){closed=true;request++;search?.Cancel();}
    private async void FindAllClick(object sender,RoutedEventArgs e)=>await Find(null);
    private async void ChooseRootClick(object sender,RoutedEventArgs e)
    {var dialog=new OpenFolderDialog{Title=SetupText.Current["AppFolder"]};if(dialog.ShowDialog()==true)await Find(dialog.FolderName);}
    private void CancelClick(object sender,RoutedEventArgs e)=>search?.Cancel();
    private async Task Find(string folder)
    {
        if(search!=null)return;closed=false;request++;search=new();var active=search;
        FindAll.IsEnabled=ChooseRoot.IsEnabled=false;CancelSearch.Visibility=Visibility.Visible;
        Status.Text=SetupText.Current["CopySearching"];
        var progress=new Progress<int>(count=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(search==active&&!closed)Status.Text=string.Format(SetupText.Current["CopySearchProgress"],count);}));});
        try{
            string current=CurrentFolder;var known=await Task.Run(()=>InstalledCopies.Known(current));
            var result=await Task.Run(()=>InstalledCopies.Search(folder==null?InstalledCopies.LocalDisks():new[]{folder},active.Token,progress));
            await OnUi(()=>{
                if(closed)return;
                var combined=known.Concat(result.Copies).DistinctBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase).ToArray();Copies.ItemsSource=combined;
                Status.Text=string.Format(SetupText.Current[result.Cancelled?"CopySearchCancelled":"CopySearchDone"],combined.Length);
                if(result.UnavailableFolders>0)Status.Text+=" "+SetupText.Current["SomeLocationsUnavailable"];
            });
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException){Product.Record(error);await OnUi(()=>{if(!closed)Status.Text=SetupText.Current["DiscoveryFailed"];});}
        finally{await OnUi(()=>{search=null;FindAll.IsEnabled=ChooseRoot.IsEnabled=true;CancelSearch.Visibility=Visibility.Collapsed;});active.Dispose();}
    }
    private async void ChooseCopyClick(object sender,RoutedEventArgs e)
    {string folder=(string)((Button)sender).Tag;var location=await Task.Run(()=>InstallationLocations.Inspect(folder));await OnUi(()=>{if(closed)return;if(location==null){Status.Text=SetupText.Current["DiscoveryFailed"];return;}FolderSelected?.Invoke(this,folder);});}
    private async Task OnUi(Action action)
    {if(Dispatcher.HasShutdownStarted)return;try{await Dispatcher.InvokeAsync(action);}catch(TaskCanceledException)when(closed||Dispatcher.HasShutdownStarted){}}
}
