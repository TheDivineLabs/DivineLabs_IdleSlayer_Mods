using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
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
    private bool busy,done;
    private volatile bool closed;
    private int page;
    private long pathRequest;
    private sealed record LicenseDocument(string Title,string Text,string Group)
    { public override string ToString()=>Title; }

    public MaintenanceWindow(string[] args)
    {
        string Arg(string name,string fallback=""){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        worker=Arg("--worker");uninstall=args.Contains("--uninstall");
        InitializeComponent();Style=(Style)FindResource(typeof(Window));
        Icon=new BitmapImage(new Uri("pack://application:,,,/DivineLabs.IdleSlayer.Launcher;component/DivineLabs.png"));
        Motion.SetEnabled(this,SystemParameters.ClientAreaAnimation);
        steps=(uninstall?new[]{"Начало","Параметры","Удаление","Готово"}:new[]{"Начало","Лицензии","Параметры","Установка","Готово"})
            .Select((title,index)=>new MaintenanceStep(title,index)).ToList();
        Steps.ItemsSource=steps;
        TargetPath.Text=Arg("--dir",InstallTarget.DefaultDirectory);
        LoadLicenses();OwnTab.IsChecked=true;
        RequiredSpace.Text=long.TryParse(Arg("--required-bytes"),out long bytes)&&bytes>0
            ?$"Для комплекта потребуется около {Math.Ceiling(bytes/1048576d):N0} МБ свободного места."
            :"Размер комплекта будет проверен перед установкой.";
        if(uninstall){
            WelcomeTitle.Text="До следующей игры";
            WelcomeText.Text="Удалим программу. Ваш прогресс останется.";
            WelcomeDetail.Text="Настройки и резервные копии сохранятся.";
            WelcomeNote.Text="Перед удалением закройте игру.";
            OptionsTitle.Text="Проверим перед удалением";OptionsDescription.Text="Будет удалена программа из этой папки.";
            Retention.Text="Сохранения игры, настройки, резервные копии и посторонние файлы не удаляются.";
            RetentionBox.Visibility=Visibility.Visible;ShortcutOptions.Visibility=Browse.Visibility=RequiredSpace.Visibility=Visibility.Collapsed;
            TargetPath.IsReadOnly=true;FilesLabel.Text="Удаление файлов";ProgressTitle.Text="Удаляем программу";ProgressDescription.Text="Отключаем компоненты и удаляем установленные файлы.";
        }
        ShowPage(0,false);
        Closing+=(_,e)=>{if(busy){e.Cancel=true;InlineNotice.Text="Сейчас меняются файлы. Дождитесь завершения операции.";}};
        Closed+=(_,_)=>{closed=true;if(!uninstall)return;try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(worker)!,"maintenance.complete"),"DivineLabs");}catch(IOException){}catch(UnauthorizedAccessException){}};
        Loaded+=(_,_)=>{
            var area=SystemParameters.WorkArea;MinWidth=Math.Min(MinWidth,area.Width);MinHeight=Math.Min(MinHeight,area.Height);
            Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);
            Left=Math.Clamp(Left,area.Left,Math.Max(area.Left,area.Right-Width));Top=Math.Clamp(Top,area.Top,Math.Max(area.Top,area.Bottom-Height));
            Motion.Enter(this);
        };
    }
    private static string Embedded(string name)
    {using var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name);if(stream==null)return "Текст лицензии недоступен. Проверьте комплект.";using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private void LoadLicenses()
    {
        documents.Add(new("Apache License 2.0",Embedded("DivineLabs.License"),"own"));
        documents.Add(new("Сведения о компонентах",Embedded("DivineLabs.Notices"),"deps"));
        foreach(string name in Assembly.GetExecutingAssembly().GetManifestResourceNames().Where(n=>n.StartsWith("DivineLabs.Licenses.",StringComparison.Ordinal)).OrderBy(n=>n))
            documents.Add(new(name["DivineLabs.Licenses.".Length..].Replace(".txt",""),Embedded(name),name.EndsWith(".MelonLoader.txt",StringComparison.Ordinal)?"loader":"deps"));
    }
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
            LicenseText.Text=document.Title+Environment.NewLine+Environment.NewLine+
                string.Join(Environment.NewLine,document.Text.Replace("\r\n","\n").Split('\n').Select(line=>line.Trim()));
            LicenseText.ScrollToHome();
        }
    }
    private void AcceptanceChanged(object sender,RoutedEventArgs e){if(Primary!=null&&page==1)Navigation();}
    private void Navigation()
    {
        bool gate=page==1&&Accepted.IsChecked!=true;
        Back.Visibility=page is 1 or 2||page==3&&!busy?Visibility.Visible:Visibility.Collapsed;
        Cancel.Visibility=done?Visibility.Collapsed:Visibility.Visible;
        Cancel.IsEnabled=Back.IsEnabled=!busy;Primary.IsEnabled=!busy&&!gate;
        Primary.Style=(Style)FindResource(gate?"LicenseGate":"Primary");
        Primary.ToolTip=gate?"Отметьте принятие условий под текстом лицензии.":null;
        Primary.Content=gate?"Примите условия":done?uninstall?"Готово":"Запустить →":page==2?uninstall?"Удалить":Existing?"Обновить →":"Установить →":page==3?busy?"Установка…":"Повторить":"Далее →";
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
                PathHint.Text=failed?"Не удалось проверить папку. Выберите другое расположение.":uninstall?"Удаляются только файлы из списка установки.":kind switch{
                    InstallTargetKind.Installed=>"Найдена установка. Настройки сохранятся.",
                    InstallTargetKind.Recoverable=>"Найдена незавершённая установка. Мастер восстановит её.",
                    InstallTargetKind.Occupied=>"В папке есть посторонние файлы. Выберите отдельную папку программы.",
                    _=>"Программа будет установлена в эту папку."};
                PathHint.Foreground=(Brush)FindResource(failed||kind==InstallTargetKind.Occupied?"ErrorBrush":"Muted");
                if(Primary!=null&&page==2)Navigation();
            });
        }catch(OperationCanceledException){}
    }
    private void BrowseClick(object sender,RoutedEventArgs e)
    {var dialog=new OpenFolderDialog{Title="DivineLabs Idle Slayer Mods — папка программы"};if(dialog.ShowDialog()==true)TargetPath.Text=dialog.FolderName;}
    private void BackClick(object sender,RoutedEventArgs e){InlineNotice.Text="";ShowPage(page==3?2:page==2&&uninstall?0:page-1);}
    private void CancelClick(object sender,RoutedEventArgs e)=>Close();
    private void MinimizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState.Minimized;
    private void MaximizeClick(object sender,RoutedEventArgs e)=>WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized;
    private void LogClick(object sender,RoutedEventArgs e)
    {string log=Path.Combine(TargetPath.Text,"Data","Logs","setup.log");if(File.Exists(log))Process.Start(new ProcessStartInfo(log){UseShellExecute=true});else InlineNotice.Text="Журнал ещё не создан. Проверьте выбранную папку.";}
    private void ProgressState(int value,string message)
    {
        Status.Text=message;Progress.IsIndeterminate=value<0;
        if(value<0)return;
        Motion.Value(Progress,value);Percent.Text=value+"%";
        double previous=ProgressRing.Value;ProgressRing.BeginAnimation(SetupProgressRing.ValueProperty,null);ProgressRing.Value=value;
        if(Motion.GetEnabled(this)&&SystemParameters.ClientAreaAnimation)
            ProgressRing.BeginAnimation(SetupProgressRing.ValueProperty,new DoubleAnimation(previous,value,TimeSpan.FromMilliseconds(180)){FillBehavior=FillBehavior.Stop});
        PrepareState.Text=value>=5?"✓ Готово":"Выполняется…";
        FilesState.Text=value>=94?"✓ Готово":value>=5?"Выполняется…":"Ожидание";
        VerifyState.Text=value>=100?"✓ Готово":value>=94?"Выполняется…":"Ожидание";
    }
    private async void PrimaryClick(object sender,RoutedEventArgs e)
    {
        if(busy)return;
        if(done){if(!uninstall){try{Process.Start(new ProcessStartInfo(Path.Combine(TargetPath.Text,"DivineLabs.IdleSlayer.exe")){UseShellExecute=true});}catch(Exception error){InlineNotice.Text="Не удалось открыть программу: "+error.Message;return;}}Close();return;}
        if(page==0){ShowPage(uninstall?2:1);return;}
        if(page==1){if(Accepted.IsChecked==true)ShowPage(2);return;}
        int requestedPage=page;string requestedPath=TargetPath.Text;
        string target;
        try{if(string.IsNullOrWhiteSpace(TargetPath.Text))throw new ArgumentException();target=Path.GetFullPath(TargetPath.Text);}
        catch(Exception error)when(error is ArgumentException or NotSupportedException or PathTooLongException){InlineNotice.Text="Выберите корректную папку программы.";return;}
        if(!uninstall){
            try{if(await Task.Run(()=>InstallTarget.Inspect(target))==InstallTargetKind.Occupied){InlineNotice.Text="В выбранной папке есть посторонние файлы. Выберите отдельную папку программы.";return;}}
            catch(Exception error)when(error is IOException or UnauthorizedAccessException){InlineNotice.Text="Не удалось проверить выбранную папку. Измените расположение.";return;}
        }
        if(busy||closed||page!=requestedPage||TargetPath.Text!=requestedPath)return;
        busy=true;InlineNotice.Text="";ErrorPanel.Visibility=Visibility.Collapsed;Progress.Visibility=Visibility.Visible;
        Progress.IsIndeterminate=true;Progress.Value=0;ProgressRing.Value=0;Percent.Text="…";Status.Text="Подготовка…";
        PrepareState.Text="Выполняется…";FilesState.Text=VerifyState.Text="Ожидание";ShowPage(3);
        string report=Path.Combine(AppContext.BaseDirectory,"operation-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            var start=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true};
            foreach(string argument in new[]{"--silent","--no-launch","--dir",target,"--report",report})start.ArgumentList.Add(argument);
            if(uninstall)start.ArgumentList.Add("--uninstall-now");
            else{if(Shortcuts.IsChecked!=true)start.ArgumentList.Add("--no-desktop-shortcut");if(StartMenuShortcut.IsChecked!=true)start.ArgumentList.Add("--no-start-menu-shortcut");}
            using var process=Process.Start(start)??throw new IOException("Не удалось запустить обслуживание.");
            while(!process.HasExited){
                var snapshot=await Task.Run(()=>{bool read=OperationReport.TryRead(report,out int percent,out string message);return(read,percent,message);});
                if(snapshot.read)ProgressState(snapshot.percent,snapshot.message);
                await Task.Delay(150);
            }
            await process.WaitForExitAsync();
            if(process.ExitCode!=0){
                var failure=await Task.Run(()=>OperationReport.TryRead(report,out _,out string text)&&text.StartsWith("Ошибка: ",StringComparison.Ordinal)?text[8..]:"Не удалось завершить операцию. Откройте журнал и повторите попытку.");
                throw new IOException(failure);
            }
            done=true;InlineNotice.Text="";ResultTitle.Text=uninstall?"Программа удалена":"Всё готово к запуску";
            ResultDescription.Text=uninstall?"Настройки, сохранения и резервные копии сохранены.":"DivineLabs Idle Slayer Mods установлен. Откройте лаунчер и начните игру.";
            ResultPath.Text=uninstall?"Сохранённые данные: "+Path.Combine(target,"Data"):target;
        }catch(Exception error){Status.Text="Операция не завершена";Progress.Visibility=Visibility.Collapsed;Percent.Text="!";ErrorText.Text=error.Message;ErrorPanel.Visibility=Visibility.Visible;}
        finally{busy=false;ShowPage(done?4:3);}
    }
}
