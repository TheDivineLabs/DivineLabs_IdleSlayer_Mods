using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using Microsoft.Win32;
using DivineLabs.DesktopUI;
using DivineLabs.IdleSlayer.Maintenance;
namespace DivineLabs.IdleSlayer.Launcher;

public partial class MaintenanceWindow : Window
{
    private readonly string worker;
    private readonly bool uninstall;
    private readonly List<MaintenanceStep> steps;
    private readonly List<LicenseDocument> documents=new();
    private bool busy,done,allowClose;
    private long requiredBytes;
    private int lastPercent=-1;
    private string lastStatus="Подготовка…";
    private InstallTargetKind targetKind;
    private bool targetCheckFailed;
    private IInputElement previousFocus;
    private volatile bool closed;
    private int page;
    private long pathRequest;
    private sealed record LicenseDocument(string Title,string Text,string Group)
    { public override string ToString()=>Title; }

    public MaintenanceWindow(string[] args)
    {
        string Arg(string name,string fallback=""){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        worker=Arg("--worker");uninstall=args.Contains("--uninstall");
        InterfacePreferences.Load();InitializeComponent();Style=(Style)FindResource(typeof(Window));
        CopyFinder.Worker=worker;
        CopyFinder.RemovalBusyChanged+=(_,removing)=>{
            busy=removing;TargetPath.IsEnabled=Browse.IsEnabled=FullRemoval.IsEnabled=!removing;
            if(!removing)InlineNotice.Text="";
            LanguagePicker.IsEnabled=!removing;Navigation();
        };
        DesktopWindow.Attach(this);ThemeManager.Attach(this);
        Icon=new BitmapImage(new Uri("pack://application:,,,/DivineLabs.IdleSlayer.Launcher;component/DivineLabs.png"));
        Motion.SetEnabled(this,SystemParameters.ClientAreaAnimation);
        steps=(uninstall?new[]{"Начало","Параметры","Удаление","Готово"}:new[]{"Начало","Лицензии","Параметры","Установка","Готово"})
            .Select((title,index)=>new MaintenanceStep(title,index)).ToList();
        Steps.ItemsSource=steps;
        ThemeManager.Changed+=ThemeChanged;
        Closed+=(_,_)=>ThemeManager.Changed-=ThemeChanged;
        TargetPath.Text=Arg("--dir",InstallTarget.DefaultDirectory);
        LoadLicenses();OwnTab.IsChecked=true;
        long.TryParse(Arg("--required-bytes"),out requiredBytes);
        if(uninstall){
            CopyFinder.Configure(InstallationFinderMode.Uninstall);
            WelcomeTitle.Text="До следующей игры";
            WelcomeText.Text="Удалим программу. Ваш прогресс останется.";
            WelcomeDetail.Text="Настройки и резервные копии сохранятся.";
            WelcomeNote.Text="Перед удалением закройте игру.";
            OptionsTitle.Text="Проверим перед удалением";OptionsDescription.Text="Будет удалена программа из этой папки.";
            Retention.Text="Сохранения игры, настройки, резервные копии и посторонние файлы не удаляются.";
            RetentionBox.Visibility=Visibility.Visible;ShortcutOptions.Visibility=Browse.Visibility=RequiredSpace.Visibility=Visibility.Collapsed;
            TargetPath.IsReadOnly=true;FilesLabel.Text="Удаление файлов";ProgressTitle.Text="Удаляем программу";ProgressDescription.Text="Отключаем компоненты и удаляем установленные файлы.";
        }
        ApplyLanguage();
        ShowPage(0,false);
        Closing+=(_,e)=>{
            if(busy){e.Cancel=true;InlineNotice.Text=T("CloseBlocked");return;}
            if(!done&&!allowClose&&IsVisible){e.Cancel=true;ShowCancel();}
        };
        Closed+=(_,_)=>{closed=true;CopyFinder.Stop();if(!uninstall)return;try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(worker)!,"maintenance.complete"),"DivineLabs");}catch(IOException){}catch(UnauthorizedAccessException){}};
        Loaded+=(_,_)=>{
            var area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);
            Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);
            Left=Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height));
            Motion.Enter(this);
        };
    }
    private static string T(string key)=>SetupText.Current[key];
    private void LanguageChanged(object sender,EventArgs e)=>ApplyLanguage();
    private void ApplyLanguage()
    {
        string[] titles=uninstall?new[]{"Start","Options","Removal","Done"}:new[]{"Start","Licenses","Options","Installation","Done"};
        for(int i=0;i<steps.Count;i++)steps[i].Rename(T(titles[i]));
        RequiredSpace.Text=requiredBytes>0?string.Format(T("SpaceBytes"),Math.Ceiling(requiredBytes/1048576d).ToString("N0")):T("Space");
        if(uninstall){
            WelcomeTitle.Text=T("RemoveWelcome");WelcomeText.Text=T("RemovePace");WelcomeBody.Text=T("RemoveMore");
            WelcomeDetail.Text=T("RemoveMore");WelcomeNote.Text=T("RemoveFlexible");
            OptionsTitle.Text=T("RemoveOptions");OptionsDescription.Text=T("RemoveFolder");
            Retention.Text=T(RemoveData?"FullRemovalHint":"Retention");RetentionTitle.Text=T(RemoveData?"FullRemoval":"RetentionTitle");FilesLabel.Text=T("RemovingFiles");ProgressTitle.Text=T("Removing");ProgressDescription.Text=T("RemoveProgress");
        }
        ResultTitle.Text=T(uninstall?"Removed":"ResultTitle");ResultDescription.Text=T(uninstall?RemoveData?"FullRemovedBody":"RemovedBody":"ResultBody");
        CancelTitle.Text=T(uninstall?"CancelRemove":"CancelTitle");CancelDescription.Text=T(uninstall?"CancelRemoveBody":"CancelBody");
        Status.Text=SetupText.Current.Progress(lastStatus);
        if(done)ResultPath.Text=uninstall?RemoveData?T("FullRemovedPath")+TargetPath.Text:T("RetainedPath")+Path.Combine(TargetPath.Text,"Data"):TargetPath.Text;
        UpdatePathHint();UpdateProgressLabels();Navigation();
    }
    private void StepHeaderSizeChanged(object sender,SizeChangedEventArgs e)
    {
        if(steps==null||steps.Count<2)return;
        double inset=StepHeader.ActualWidth/(steps.Count*2);
        StepBase.Margin=StepTrack.Margin=new Thickness(inset,19,inset,0);
    }
    private static string Embedded(string name)
    {using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);if(stream==null)return "Текст лицензии недоступен. Проверьте комплект.";using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private void LoadLicenses()
    {
        documents.Add(new("DivineLabs — MIT License",Embedded("DivineLabs.License"),"own"));
        documents.Add(new("Сведения о компонентах",Embedded("DivineLabs.Notices"),"deps"));
        foreach(string name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n=>n.StartsWith("DivineLabs.Licenses.",StringComparison.Ordinal)).OrderBy(n=>n))
            documents.Add(new(name["DivineLabs.Licenses.".Length..].Replace(".txt",""),Embedded(name),name.EndsWith(".MelonLoader.txt",StringComparison.Ordinal)?"loader":"deps"));
    }
    private void ThemeChanged(){foreach(var step in steps)step.Refresh();UpdateProgressLabels();}
    private void LicenseTabChanged(object sender,RoutedEventArgs e)
    {
        if(LicenseSelector==null||documents.Count==0)return;
        string group=(string)((RadioButton)sender).Tag;
        LicenseSelector.ItemsSource=documents.Where(d=>d.Group==group).ToList();
        LicenseSelector.Visibility=group=="deps"?Visibility.Visible:Visibility.Collapsed;LicenseSelector.SelectedIndex=0;
    }
    private void LicenseChanged(object sender,SelectionChangedEventArgs e)
    {
        if(LicenseText!=null&&LicenseSelector.SelectedItem is LicenseDocument document){
            LicenseText.Text=document.Title+Environment.NewLine+Environment.NewLine+document.Text;
            LicenseText.ScrollToHome();
        }
    }
    private void AcceptanceChanged(object sender,RoutedEventArgs e){if(Primary!=null&&page==1)Navigation();}
    private void Navigation()
    {
        bool gate=page==1&&Accepted.IsChecked!=true;
        Back.Visibility=page is 1 or 2||page==3&&!busy?Visibility.Visible:Visibility.Collapsed;
        Cancel.Visibility=done?Visibility.Collapsed:Visibility.Visible;
        Finish.Visibility=done&&!uninstall?Visibility.Visible:Visibility.Collapsed;
        Cancel.IsEnabled=Back.IsEnabled=!busy;Primary.IsEnabled=!busy&&!gate;
        Primary.Style=(Style)FindResource(gate?"LicenseGate":"Primary");
        Primary.ToolTip=gate?T("AcceptHint"):null;
        Primary.Content=T(gate?"AcceptFirst":done?uninstall?"Done":"Launch":page==2?uninstall?"Remove":Existing?"Update":"Install":page==3?busy?"Busy":"Retry":"Next");
    }
    private void ShowPage(int next,bool animate=true)
    {
        int direction=next>=page?1:-1;page=next;
        var pages=new FrameworkElement[]{WelcomePage,LicensePage,OptionsPage,ProgressPage,ResultPage};
        for(int index=0;index<pages.Length;index++)pages[index].Visibility=index==page?Visibility.Visible:Visibility.Collapsed;
        Cover.Visibility=page==0?Visibility.Visible:Visibility.Collapsed;
        CoverColumn.Width=new GridLength(page==0?330:0);
        MainPane.Margin=page==0?new Thickness(34,24,34,20):new Thickness(56,24,56,20);
        int current=uninstall&&page>0?page-1:page;
        foreach(var step in steps)step.Update(current);
        Motion.Value(StepTrack,current*100d/(steps.Count-1),animate);
        Navigation();
        if(animate)Motion.Slide(pages[page],direction);
    }
    private bool Existing=>File.Exists(Path.Combine(TargetPath.Text,"DivineLabs.IdleSlayer.exe"));
    private async void PathChanged(object sender,TextChangedEventArgs e)
    {
        if(PathHint==null)return;
        long request=++pathRequest;string target=TargetPath.Text;InstallTargetKind kind=InstallTargetKind.Occupied;bool failed=false;
        try{kind=await Task.Run(()=>InstallTarget.Inspect(target));}
        catch(Exception error)when(error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){failed=true;}
        if(closed||Dispatcher.HasShutdownStarted)return;
        try{
            await Dispatcher.InvokeAsync(()=>{
                if(closed||request!=pathRequest)return;
                targetKind=kind;targetCheckFailed=failed;UpdatePathHint();
                if(Primary!=null&&page==2)Navigation();
            });
        }catch(OperationCanceledException){}
    }
    private bool RemoveData=>uninstall&&FullRemoval.IsChecked==true;
    private void RemovalModeChanged(object sender,RoutedEventArgs e){if(steps!=null)ApplyLanguage();}
    private void UpdatePathHint()
    {
        PathHint.Text=T(targetCheckFailed?"CheckFailed":uninstall?RemoveData?"RemoveOnlyFull":"RemoveOnly":targetKind switch{
            InstallTargetKind.Installed=>"Installed",InstallTargetKind.Recoverable=>"Recoverable",InstallTargetKind.Occupied=>"Occupied",_=>"Empty"});
        PathHint.Foreground=(Brush)FindResource(targetCheckFailed||targetKind==InstallTargetKind.Occupied?"ErrorBrush":"Muted");
    }
    private void BrowseClick(object sender,RoutedEventArgs e)
    {var dialog=new OpenFolderDialog{Title=T("FolderTitle")};if(dialog.ShowDialog()==true)TargetPath.Text=dialog.FolderName;}
    private void CopyFolderSelected(object sender,string folder){if(!uninstall)TargetPath.Text=folder;}
    private void BackClick(object sender,RoutedEventArgs e){InlineNotice.Text="";ShowPage(page==3?2:page==2&&uninstall?0:page-1);}
    private void CancelClick(object sender,RoutedEventArgs e)=>Close();
    private void FinishClick(object sender,RoutedEventArgs e){if(done&&!busy)Close();}
    private void ShowCancel()
    {
        if(CancelOverlay.Visibility==Visibility.Visible)return;
        previousFocus=Keyboard.FocusedElement;MainPane.IsEnabled=false;LanguagePicker.IsEnabled=false;
        CancelOverlay.Visibility=Visibility.Visible;Motion.Enter(CancelOverlay);Continue.Focus();
    }
    private void ContinueClick(object sender,RoutedEventArgs e)
    {CancelOverlay.Visibility=Visibility.Collapsed;MainPane.IsEnabled=true;LanguagePicker.IsEnabled=true;previousFocus?.Focus();}
    private void ExitClick(object sender,RoutedEventArgs e){allowClose=true;Close();}
    private void CancelOverlayKeyDown(object sender,KeyEventArgs e)
    {if(e.Key==Key.Escape){e.Handled=true;ContinueClick(sender,e);}}
    private void MinimizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void MaximizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void LogClick(object sender,RoutedEventArgs e)
    {string log=Path.Combine(TargetPath.Text,"Data","Logs","setup.log");if(File.Exists(log))Process.Start(new ProcessStartInfo(log){UseShellExecute=true});else InlineNotice.Text=T("LogMissing");}
    private void ProgressState(int value,string message)
    {
        lastStatus=message;Status.Text=SetupText.Current.Progress(message);Progress.IsIndeterminate=value<0;
        if(value<0)return;
        lastPercent=value;
        Motion.Value(Progress,value);Percent.Text=value+"%";
        double previous=ProgressRing.Value;ProgressRing.BeginAnimation(SetupProgressRing.ValueProperty,null);ProgressRing.Value=value;
        if(Motion.GetEnabled(this)&&SystemParameters.ClientAreaAnimation)
            ProgressRing.BeginAnimation(SetupProgressRing.ValueProperty,new DoubleAnimation(previous,value,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop});
        UpdateProgressLabels();
    }
    private void UpdateProgressLabels()
    {
        int value=lastPercent;
        SetStage(PrepareState,value>=5?"Complete":"Working");
        SetStage(FilesState,value>=94?"Complete":value>=5?"Working":"Waiting");
        SetStage(VerifyState,value>=100?"Complete":value>=94?"Working":"Waiting");
    }
    private static void SetStage(TextBlock label,string state)
    {label.Text=T(state);label.Foreground=new SolidColorBrush((Color)ColorConverter.ConvertFromString(state=="Complete"?"#24855C":state=="Working"?"#6658E8":"#8891A2"));}
    private async void PrimaryClick(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        if(done){if(!uninstall){try{Process.Start(new ProcessStartInfo(Path.Combine(TargetPath.Text,"DivineLabs.IdleSlayer.exe")){UseShellExecute=true});}catch(Exception error){InlineNotice.Text=T("LaunchFailed")+error.Message;return;}}Close();return;}
        if(page==0){ShowPage(uninstall?2:1);return;}
        if(page==1){if(Accepted.IsChecked==true)ShowPage(2);return;}
        int requestedPage=page;string requestedPath=TargetPath.Text;
        string target;
        try{if(string.IsNullOrWhiteSpace(TargetPath.Text))throw new ArgumentException();target=Path.GetFullPath(TargetPath.Text);}
        catch(Exception error)when(error is ArgumentException or NotSupportedException or PathTooLongException){InlineNotice.Text=T("InvalidPath");return;}
        if(!uninstall){
            try{if(await Task.Run(()=>InstallTarget.Inspect(target))==InstallTargetKind.Occupied){InlineNotice.Text=T("Occupied");return;}}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException){InlineNotice.Text=T("CheckFailed");return;}
        }
        if(busy||closed||page!=requestedPage||TargetPath.Text!=requestedPath)return;
        CopyFinder.Stop();busy=true;FullRemoval.IsEnabled=false;InlineNotice.Text="";ErrorPanel.Visibility=Visibility.Collapsed;Progress.Visibility=Visibility.Visible;
        Progress.IsIndeterminate=true;Progress.Value=0;ProgressRing.Value=0;Percent.Text="…";Status.Text=T("Preparing");
        lastPercent=-1;UpdateProgressLabels();ShowPage(3);
        string report=Path.Combine(AppContext.BaseDirectory,"operation-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            var start=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true};
            foreach(string argument in new[]{"--silent","--no-launch","--dir",target,"--report",report})start.ArgumentList.Add(argument);
            if(uninstall){start.ArgumentList.Add("--uninstall-now");if(RemoveData)start.ArgumentList.Add("--remove-data");}
            else{if(Shortcuts.IsChecked!=true)start.ArgumentList.Add("--no-desktop-shortcut");if(StartMenuShortcut.IsChecked!=true)start.ArgumentList.Add("--no-start-menu-shortcut");}
            using var process=Process.Start(start)??throw new IOException(T("WorkerFailed"));
            while(!process.HasExited){
                var snapshot=await Task.Run(()=>{bool read=OperationReport.TryRead(report,out int percent,out string message);return(read,percent,message);});
                if(snapshot.read)ProgressState(snapshot.percent,snapshot.message);
                await Task.Delay(150);
            }
            await process.WaitForExitAsync();
            if(process.ExitCode!=0){
                var failure=await Task.Run(()=>OperationReport.TryRead(report,out _,out string text)&&text.StartsWith("Ошибка: ",StringComparison.Ordinal)?text[8..]:T("OperationFailed"));
                throw new IOException(failure);
            }
            done=true;InlineNotice.Text="";ProgressState(100,T("Complete"));ApplyLanguage();
        }catch(Exception error){Status.Text=T("NotFinished");Progress.Visibility=Visibility.Collapsed;Percent.Text="!";ErrorText.Text=error.Message;ErrorPanel.Visibility=Visibility.Visible;}
        finally{busy=false;FullRemoval.IsEnabled=!done;ShowPage(done?4:3);}
    }
}
