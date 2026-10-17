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
    internal static void Run(string output)
    {
        foreach(string language in SetupText.Languages)foreach(string key in SetupText.Keys)if(!SetupText.HasTranslation(language,key))throw new Exception("Missing translation: "+language+":"+key);
        string fixture=Path.Combine(output,"copy-fixture");string first=Create("first"),second=Create(@"deep\second");
        var window=new MaintenanceWindow(new[]{"--worker",Path.Combine(output,"no-worker.exe"),"--dir",first});
        Motion.SetEnabled(window,false);
        var flags=BindingFlags.NonPublic|BindingFlags.Instance;
        try{
            window.Show();typeof(MaintenanceWindow).GetMethod("ShowPage",flags)!.Invoke(window,new object[]{2,false});
            var finder=(InstallationFinder)window.FindName("CopyFinder");
            var expander=(Expander)finder.Content;expander.IsExpanded=true;
            PumpUntil(()=>((ItemsControl)finder.FindName("Copies")).ItemsSource!=null);
            var search=(Task)typeof(InstallationFinder).GetMethod("Find",flags)!.Invoke(finder,new object[]{fixture})!;
            PumpUntil(()=>search.IsCompleted);search.GetAwaiter().GetResult();
            var rows=((ItemsControl)finder.FindName("Copies")).Items.Cast<DivineLabs.IdleSlayer.Maintenance.InstallationLocation>().ToArray();
            if(!rows.Any(x=>x.Folder==first)||!rows.Any(x=>x.Folder==second))throw new Exception("Finder does not show both installations");
            typeof(InstallationFinder).GetMethod("ChooseCopyClick",flags)!.Invoke(finder,new object[]{new Button{Tag=second},new RoutedEventArgs()});
            PumpUntil(()=>((TextBox)window.FindName("TargetPath")).Text==second);
            if((int)typeof(MaintenanceWindow).GetField("page",flags)!.GetValue(window)! !=2)throw new Exception("Choosing an installation changed the wizard stage");
            foreach(string theme in new[]{"light","dark"}){
                ThemeManager.Select(theme);window.UpdateLayout();window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);
                var root=(FrameworkElement)window.Content;
                var bitmap=new RenderTargetBitmap((int)root.ActualWidth,(int)root.ActualHeight,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,"installer-copy-finder-"+theme+".png"));encoder.Save(file);
            }
            File.WriteAllText(Path.Combine(output,"copy-finder-checks.json"),JsonSerializer.Serialize(new{Copies=rows,Selected=second,Translations=SetupText.Keys.Count(),PhysicalInput="WPF events/reflection; no physical clicks"},new JsonSerializerOptions{WriteIndented=true}));
        }finally{typeof(MaintenanceWindow).GetField("allowClose",flags)!.SetValue(window,true);window.Close();}
        string Create(string relative){string folder=Path.GetFullPath(Path.Combine(fixture,relative));Directory.CreateDirectory(Path.Combine(folder,"App","Setup"));File.WriteAllText(Path.Combine(folder,"DivineLabs.IdleSlayer.exe"),"fixture");File.WriteAllText(Path.Combine(folder,"App","Setup","installation.json"),JsonSerializer.Serialize(new{Product="DivineLabs.IdleSlayer",Version="0.5.0"}));return folder;}
    }
    private static void PumpUntil(Func<bool> condition)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();
        while(!condition()){
            if(watch.ElapsedMilliseconds>10000)throw new TimeoutException("UI operation did not finish");
            var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);
        }
    }
}
