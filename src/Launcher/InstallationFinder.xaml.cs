using System.Windows;
using System.Windows.Controls;
using DivineLabs.IdleSlayer.Runtime;
using DivineLabs.IdleSlayer.Maintenance;
using Microsoft.Win32;
using System.Diagnostics;
namespace DivineLabs.IdleSlayer.Launcher;
public enum InstallationFinderMode { Install, Uninstall }
public partial class InstallationFinder:UserControl
{
    public static readonly DependencyProperty CurrentFolderProperty=DependencyProperty.Register(nameof(CurrentFolder),typeof(string),typeof(InstallationFinder));
    public string CurrentFolder{get=>(string)GetValue(CurrentFolderProperty);set=>SetValue(CurrentFolderProperty,value);}
    public event EventHandler<string> FolderSelected;
    public event EventHandler<bool> RemovalBusyChanged;
    public string Worker {get;set;}="";
    public InstallationFinderMode Mode {get;private set;}
    public Visibility FolderSelectionVisibility=>Mode==InstallationFinderMode.Uninstall?Visibility.Collapsed:Visibility.Visible;
    public void Configure(InstallationFinderMode mode)
    {
        Mode=mode;
        Description.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding("["+(mode==InstallationFinderMode.Uninstall?"CopiesRemoveBody":"CopiesBody")+"]"){Source=SetupText.Current});
        if(mode==InstallationFinderMode.Uninstall){if(IsLoaded)((Expander)Content).IsExpanded=true;else Loaded+=(_,_)=>((Expander)Content).IsExpanded=true;}
    }
    public bool IsRemoving {get;private set;}
    private string removalFolder;
    private CancellationTokenSource search;
    private bool loaded;
    private volatile bool closed;
    private long request;
    public InstallationFinder(){InitializeComponent();DivineLabs.DesktopUI.ThemeManager.Attach(this);Unloaded+=(_,_)=>Stop();}
    private async void Expanded(object sender,RoutedEventArgs e)
    {
        if(loaded)return;loaded=true;long revision=++request;
        string current=CurrentFolder;var rows=await Task.Run(()=>InstalledCopies.Known(current));
        await OnUi(()=>{if(closed||revision!=request)return;var visible=VisibleCopies(rows,current);Copies.ItemsSource=visible;Status.Text=SetupText.Current[visible.Length==0?"CopiesNotKnown":Mode==InstallationFinderMode.Uninstall?"CopiesRemovalKnown":"CopiesKnown"];});
    }
    public void Stop(){closed=true;request++;search?.Cancel();}
    private async void FindAllClick(object sender,RoutedEventArgs e)=>await Find(null);
    private async void ChooseRootClick(object sender,RoutedEventArgs e)
    {var dialog=new OpenFolderDialog{Title=SetupText.Current["AppFolder"]};if(dialog.ShowDialog()==true)await Find(dialog.FolderName);}
    private void CancelClick(object sender,RoutedEventArgs e)=>search?.Cancel();
    private async Task Find(string folder)
    {
        if(search!=null||IsRemoving)return;CancelRemovalClick(this,new());closed=false;request++;search=new();var active=search;
        FindAll.IsEnabled=ChooseRoot.IsEnabled=false;CancelSearch.Visibility=Visibility.Visible;
        Status.Text=SetupText.Current["CopySearching"];
        var progress=new Progress<int>(count=>{if(!Dispatcher.HasShutdownStarted)Dispatcher.BeginInvoke(new Action(()=>{if(search==active&&!closed)Status.Text=string.Format(SetupText.Current["CopySearchProgress"],count);}));});
        try{
            string current=CurrentFolder;var known=await Task.Run(()=>InstalledCopies.Known(current));
            var result=await Task.Run(()=>InstalledCopies.Search(folder==null?InstalledCopies.LocalDisks():new[]{folder},active.Token,progress));
            await OnUi(()=>{
                if(closed)return;
                var combined=VisibleCopies(known.Concat(result.Copies).DistinctBy(x=>x.Folder,StringComparer.OrdinalIgnoreCase),current);Copies.ItemsSource=combined;
                Status.Text=string.Format(SetupText.Current[result.Cancelled?"CopySearchCancelled":"CopySearchDone"],combined.Length);
                if(result.UnavailableFolders>0)Status.Text+=" "+SetupText.Current["SomeLocationsUnavailable"];
            });
        }catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException){Product.Record(error);await OnUi(()=>{if(!closed)Status.Text=SetupText.Current["DiscoveryFailed"];});}
        finally{await OnUi(()=>{search=null;FindAll.IsEnabled=ChooseRoot.IsEnabled=true;CancelSearch.Visibility=Visibility.Collapsed;});active.Dispose();}
    }
    private async void ChooseCopyClick(object sender,RoutedEventArgs e)
    {if(Mode==InstallationFinderMode.Uninstall)return;string folder=(string)((Button)sender).Tag;var location=await Task.Run(()=>InstallationLocations.Inspect(folder));await OnUi(()=>{if(closed)return;if(location==null){Status.Text=SetupText.Current["DiscoveryFailed"];return;}FolderSelected?.Invoke(this,folder);});}
    private InstallationLocation[] VisibleCopies(IEnumerable<InstallationLocation> rows,string current)=>rows.Where(row=>!IsCurrentCopy(row.Folder,current)).ToArray();
    private bool IsCurrentCopy(string folder,string current)=>Mode==InstallationFinderMode.Uninstall&&!string.IsNullOrWhiteSpace(current)&&string.Equals(InstallationLocations.Normalize(folder),InstallationLocations.Normalize(current),StringComparison.OrdinalIgnoreCase);
    private void RemoveCopyClick(object sender,RoutedEventArgs e)
    {
        if(search!=null||IsRemoving||IsCurrentCopy((string)((Button)sender).Tag,CurrentFolder))return;
        removalFolder=(string)((Button)sender).Tag;RemovalPath.Text=removalFolder;
        RemovalConfirmation.Visibility=Visibility.Visible;RemovalLog.Visibility=Visibility.Collapsed;
        Copies.IsEnabled=FindAll.IsEnabled=ChooseRoot.IsEnabled=false;
        CancelRemoval.Focus();
    }
    private void CancelRemovalClick(object sender,RoutedEventArgs e)
    {
        if(IsRemoving)return;
        RemovalConfirmation.Visibility=Visibility.Collapsed;removalFolder=null;
        Copies.IsEnabled=FindAll.IsEnabled=ChooseRoot.IsEnabled=true;
    }
    private void RemovalLogClick(object sender,RoutedEventArgs e)
    {
        string path=Path.Combine(RemovalPath.Text,"Data","Logs","setup.log");
        if(File.Exists(path))Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
    private async void ConfirmRemovalClick(object sender,RoutedEventArgs e)
    {
        if(IsRemoving||removalFolder==null||search!=null)return;
        string folder=removalFolder,worker=Worker;
        if(!File.Exists(worker)){Status.Text=SetupText.Current["WorkerFailed"];return;}
        IsRemoving=true;RemovalBusyChanged?.Invoke(this,true);ConfirmRemoval.IsEnabled=CancelRemoval.IsEnabled=false;
        Status.Text=SetupText.Current["RemovingCopy"];string report=Path.Combine(Path.GetTempPath(),"DivineLabs-remove-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            await Task.Run(async ()=>{
                var start=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true};
                foreach(string argument in new[]{"--remove-copy","--silent","--dir",folder,"--report",report})start.ArgumentList.Add(argument);
                using var process=Process.Start(start)??throw new IOException("Worker did not start");
                await process.WaitForExitAsync();
                if(process.ExitCode!=0)throw new IOException(OperationReport.TryRead(report,out _,out string message)?message:"Removal failed");
                if(InstallationLocations.Inspect(folder)!=null)throw new IOException("Installation still exists");
            });
            await OnUi(()=>{
                Copies.ItemsSource=Copies.Items.Cast<InstallationLocation>().Where(x=>!string.Equals(x.Folder,folder,StringComparison.OrdinalIgnoreCase)).ToArray();
                Status.Text=SetupText.Current["CopyRemoved"];RemovalConfirmation.Visibility=RemovalLog.Visibility=Visibility.Collapsed;
            });
        }catch(Exception error){Product.Record(error);await OnUi(()=>{Status.Text=SetupText.Current["CopyRemovalFailed"];RemovalLog.Visibility=File.Exists(Path.Combine(folder,"Data","Logs","setup.log"))?Visibility.Visible:Visibility.Collapsed;});}
        finally{
            await OnUi(()=>{IsRemoving=false;RemovalBusyChanged?.Invoke(this,false);ConfirmRemoval.IsEnabled=CancelRemoval.IsEnabled=true;
                if(RemovalConfirmation.Visibility!=Visibility.Visible)CancelRemovalClick(this,new());});
            try{File.Delete(report);}catch(IOException){}catch(UnauthorizedAccessException){}
        }
    }
    private async Task OnUi(Action action)
    {if(Dispatcher.HasShutdownStarted)return;try{await Dispatcher.InvokeAsync(action);}catch(TaskCanceledException)when(closed||Dispatcher.HasShutdownStarted){}}
}
