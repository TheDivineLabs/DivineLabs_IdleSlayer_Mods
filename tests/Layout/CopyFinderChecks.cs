using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivineLabs.IdleSlayer.Launcher;
using DivineLabs.DesktopUI;

internal static class CopyFinderChecks
{
    internal static void Run(string output,string? worker=null,bool uninstall=false)
    {
        foreach(string language in SetupText.Languages)foreach(string key in SetupText.Keys)if(!SetupText.HasTranslation(language,key))throw new Exception("Missing translation: "+language+":"+key);
        string fixture=Path.Combine(output,"copy-fixture");string first=Create("first"),second=Create(@"deep\second");
        var args=new List<string>{"--worker",worker??Path.Combine(output,"no-worker.exe"),"--dir",first};if(uninstall)args.Add("--uninstall");
        var window=new MaintenanceWindow(args.ToArray());string removed=uninstall?second:first,kept=uninstall?first:second;
        Motion.SetEnabled(window,false);
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        try{
            window.Show();typeof(MaintenanceWindow).GetMethod("ShowPage",flags)!.Invoke(window,new object[]{2,false});
            var finder=(InstallationFinder)window.FindName("CopyFinder");
            var expander=(Expander)finder.Content;
            if(uninstall)PumpUntil(()=>finder.IsLoaded&&finder.IsVisible&&expander.IsExpanded);
            if(uninstall&&(!finder.IsVisible||!expander.IsExpanded||finder.Mode!=InstallationFinderMode.Uninstall))throw new Exception("Uninstaller copy finder is hidden or collapsed");
            expander.IsExpanded=true;
            PumpUntil(()=>((ItemsControl)finder.FindName("Copies")).ItemsSource!=null);
            var search=(Task)typeof(InstallationFinder).GetMethod("Find",flags)!.Invoke(finder,new object[]{fixture})!;
            PumpUntil(()=>search.IsCompleted);search.GetAwaiter().GetResult();
            var rows=((ItemsControl)finder.FindName("Copies")).Items.Cast<DivineLabs.IdleSlayer.Maintenance.InstallationLocation>().ToArray();
            if(!rows.Any(x=>x.Folder==second)||rows.Any(x=>x.Folder==first)==uninstall)throw new Exception("Finder current/other installation filtering failed");
            typeof(InstallationFinder).GetMethod("ChooseCopyClick",flags)!.Invoke(finder,new object[]{new Button{Tag=second},new RoutedEventArgs()});
            PumpUntil(()=>((TextBox)window.FindName("TargetPath")).Text==kept);
            if(uninstall){
                if(!((TextBox)window.FindName("TargetPath")).IsReadOnly||finder.FolderSelectionVisibility!=Visibility.Collapsed)throw new Exception("Uninstaller can retarget its main operation");
                typeof(InstallationFinder).GetMethod("RemoveCopyClick",flags)!.Invoke(finder,new object[]{new Button{Tag=first},new RoutedEventArgs()});
                if(((FrameworkElement)finder.FindName("RemovalConfirmation")).Visibility!=Visibility.Collapsed)throw new Exception("Finder allows removing the current installation");
            }
            if((int)typeof(MaintenanceWindow).GetField("page",flags)!.GetValue(window)! !=2)throw new Exception("Choosing an installation changed the wizard stage");
            typeof(InstallationFinder).GetMethod("RemoveCopyClick",flags)!.Invoke(finder,new object[]{new Button{Tag=removed},new RoutedEventArgs()});
            if(((TextBlock)finder.FindName("RemovalPath")).Text!=removed)throw new Exception("Confirmation shows wrong target");
            if(!File.Exists(Path.Combine(removed,"DivineLabs.IdleSlayer.exe")))throw new Exception("Opening confirmation deleted files");
            ((Button)finder.FindName("CancelRemoval")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(((FrameworkElement)finder.FindName("RemovalConfirmation")).Visibility!=Visibility.Collapsed)throw new Exception("Cancel did not dismiss confirmation");
            if(worker!=null){
                typeof(InstallationFinder).GetMethod("RemoveCopyClick",flags)!.Invoke(finder,new object[]{new Button{Tag=removed},new RoutedEventArgs()});
                using(var locked=new FileStream(Path.Combine(removed,"DivineLabs.IdleSlayer.exe"),FileMode.Open,FileAccess.Read,FileShare.None)){
                    ((Button)finder.FindName("ConfirmRemoval")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PumpUntil(()=>!finder.IsRemoving);
                    if(((FrameworkElement)finder.FindName("RemovalConfirmation")).Visibility!=Visibility.Visible||!File.Exists(Path.Combine(removed,"DivineLabs.IdleSlayer.exe")))throw new Exception("Failed removal lost retry state");
                    if(!((Button)window.FindName("Primary")).IsEnabled)throw new Exception("Failed removal kept wizard blocked");
                }
                ((Button)finder.FindName("ConfirmRemoval")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(!finder.IsRemoving||((Button)window.FindName("Primary")).IsEnabled)throw new Exception("Removal did not block installation controls");
                window.Close();if(!window.IsVisible)throw new Exception("Window closed during removal");
                PumpUntil(()=>!finder.IsRemoving);
                if(((TextBlock)window.FindName("InlineNotice")).Text.Length!=0)throw new Exception("Completed removal left a stale busy notice");
                if(File.Exists(Path.Combine(removed,"DivineLabs.IdleSlayer.exe")))throw new Exception("Native worker failed to remove selected copy");
                if(((ItemsControl)finder.FindName("Copies")).Items.Cast<DivineLabs.IdleSlayer.Maintenance.InstallationLocation>().Any(x=>x.Folder==removed))throw new Exception("Removed copy remains visible");
                if(!File.Exists(Path.Combine(kept,"DivineLabs.IdleSlayer.exe"))||File.ReadAllText(Path.Combine(removed,"Data/Config/keep.txt"))!="retain")throw new Exception("Removal changed other copy or settings");
                if(((TextBox)window.FindName("TargetPath")).Text!=kept)throw new Exception("Removal changed main target");
            }
            foreach(string theme in new[]{"light","dark"}){
                ThemeManager.Select(theme);window.UpdateLayout();window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                var root=(FrameworkElement)window.Content;
                var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,(uninstall?"uninstaller":"installer")+"-copy-finder-"+theme+".png"));encoder.Save(file);
            }
            File.WriteAllText(Path.Combine(output,"copy-finder-checks.json"),JsonSerializer.Serialize(new{Mode=uninstall?"Uninstall":"Install",Copies=rows,Selected=kept,Removed=worker==null?null:removed,Translations=SetupText.Keys.Count(),PhysicalInput="WPF events/reflection; no physical clicks"},new JsonSerializerOptions{WriteIndented=true}));
        }finally{typeof(MaintenanceWindow).GetField("allowClose",flags)!.SetValue(window,true);window.Close();}
        string Create(string relative){string folder=Path.GetFullPath(Path.Combine(fixture,relative));Directory.CreateDirectory(Path.Combine(folder,"App","Setup"));Directory.CreateDirectory(Path.Combine(folder,"Data/Config"));File.WriteAllText(Path.Combine(folder,"Data/Config/keep.txt"),"retain");File.WriteAllText(Path.Combine(folder,"DivineLabs.IdleSlayer.exe"),"fixture");File.WriteAllText(Path.Combine(folder,"App","Setup","installation.json"),JsonSerializer.Serialize(new{Product="DivineLabs.IdleSlayer",Version="0.5.1",Files=new[]{"DivineLabs.IdleSlayer.exe"}}));return folder;}
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(!condition()){
            if(watch.ElapsedMilliseconds>60000)throw new TimeoutException("UI operation did not finish");
            var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
        }
    }
}
