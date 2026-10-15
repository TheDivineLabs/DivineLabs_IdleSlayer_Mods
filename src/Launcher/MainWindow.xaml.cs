using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DivineLabs.DesktopUI;
using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class MainWindow:Window
{
    private readonly SettingsClient client=new();
    private readonly List<FeatureItem> features=FeatureCatalog.All.Select(d=>new FeatureItem(d)).ToList();
    private readonly DispatcherTimer poll=new(){Interval=TimeSpan.FromSeconds(2)};
    private readonly DispatcherTimer searchDelay=new(){Interval=TimeSpan.FromMilliseconds(180)};
    private readonly CancellationTokenSource lifetime=new();
    private bool busy,polling,supported,ready,refreshing,editing;
    private string category="all",capture;
    private long renderedRevision=-1;
    public MainWindow()
    {
        InitializeComponent();Style=(Style)FindResource(typeof(Window));
        Icon=new BitmapImage(new Uri("pack://application:,,,/DivineLabs.IdleSlayer.Launcher;component/DivineLabs.png"));
        PreviewKeyDown+=CaptureKey;PreviewMouseDown+=CaptureMouse;
        Loaded+=async(_,_)=>{
            client.Game=Installation.FindGame();await TryRefresh();await Run(()=>Prepare());
            poll.Start();SelectPage("home");
        };
        Closing+=(_,e)=>{if(busy){e.Cancel=true;Notice.Text="Дождитесь завершения текущей операции.";return;}lifetime.Cancel();poll.Stop();searchDelay.Stop();};
        poll.Tick+=async(_,_)=>{
            if(polling||busy||editing||capture!=null)return;polling=true;
            try{await client.Refresh();Render(false);}catch{Connection.Text=Installation.Running?"Нет связи с игрой":"Игра не запущена";}
            finally{polling=false;RefreshState();}
        };
        searchDelay.Tick+=(_,_)=>{searchDelay.Stop();Filter();};
        Render(true);RefreshState();
    }
    private async Task TryRefresh(){try{await client.Refresh();Render(true);}catch(Exception e){ShowError(e);}}
    internal void ShowError(Exception e){Product.Record(e);Notice.Text=e is OperationCanceledException?"Нет связи с игрой. Изменение не подтверждено.":System.Text.RegularExpressions.Regex.IsMatch(e.Message,@"^[А-ЯЁа-яё]")?e.Message:"Не удалось выполнить действие. Подробности сохранены в журнале.";}
    private async Task Change(Action<Settings> action)
    {
        if(refreshing||editing)return;editing=true;RefreshState();
        try{await client.Change(action);Notice.Text="Настройки применены.";Render(true);}
        catch(Exception e){ShowError(e);Render(true);}
        finally{editing=false;RefreshState();}
    }
    private void Render(bool force)
    {
        if(!force&&renderedRevision==client.Current.Revision)return;
        refreshing=true;
        try{
            var s=client.Current;renderedRevision=s.Revision;
            foreach(var f in features)f.Refresh(s);
            AllEnabled.IsChecked=s.Enabled;WidgetVisible.IsChecked=s.WidgetVisible;ActiveListVisible.IsChecked=s.ActiveListVisible;ReduceMotion.IsChecked=s.ReduceMotion;
            OpacitySlider.Value=s.WidgetOpacity*100;ScaleSlider.Value=s.UiScale*100;
            Motion.SetEnabled(this,!s.ReduceMotion&&SystemParameters.ClientAreaAnimation);
            KeyList.ItemsSource=FeatureCatalog.Actions.Select(id=>new{Id=id,Title=FeatureCatalog.ActionTitle(id),Label=s.Bindings[id].ToString()}).ToArray();
            Summary.Text=s.Enabled?string.Join("     /     ",FeatureCatalog.All.Select(f=>f.Title+" "+(s.FeatureEnabled(f.Id)?"×"+s.Multiplier(f.Id).ToString("G6",CultureInfo.InvariantCulture):"выкл."))):"Все функции выключены";
            MenuHint.Text=$"Меню в игре — {s.Bindings["menu"]}. Покупки списывают обычную стоимость.";
            Filter();
        }finally{refreshing=false;}
    }
    private void Filter()
    {
        if(Search==null)return;
        string query=Search.Text.Trim();
        FeatureList.ItemsSource=features.Where(f=>(category=="all"||category=="favorites"&&client.Current.Favorites.Contains(f.Id)||category==f.Category)&&
            (f.Title.Contains(query,StringComparison.OrdinalIgnoreCase)||f.Category.Contains(query,StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    private void FilterChanged(object sender,TextChangedEventArgs e){if(searchDelay==null)return;searchDelay.Stop();searchDelay.Start();}
    private void CategoryClick(object sender,RoutedEventArgs e){category=(string)((Button)sender).Tag;Filter();}
    private void Navigate(object sender,RoutedEventArgs e)=>SelectPage((string)((Button)sender).Tag);
    private void SelectPage(string name)
    {
        foreach(var view in new FrameworkElement[]{HomePage,FeaturesPage,SettingsPage})view.Visibility=Visibility.Collapsed;
        var page=name=="features"?(FrameworkElement)FeaturesPage:name=="settings"?SettingsPage:HomePage;
        page.Visibility=Visibility.Visible;Motion.Enter(page);
        foreach(var b in new[]{NavHome,NavFeatures,NavSettings})b.BorderBrush=(System.Windows.Media.Brush)FindResource((string)b.Tag==name?"Accent":"Background");
    }
    private async void AllClick(object s,RoutedEventArgs e){bool v=AllEnabled.IsChecked==true;await Change(c=>c.Enabled=v);}
    private async void FeatureEnabledClick(object sender,RoutedEventArgs e){var b=(CheckBox)sender;string id=(string)b.Tag;bool value=b.IsChecked==true;await Change(c=>c.SetFeatureEnabled(id,value));}
    private async void FavoriteClick(object sender,RoutedEventArgs e){string id=(string)((Button)sender).Tag;await Change(c=>c.Favorites=c.Favorites.Contains(id)?c.Favorites.Where(v=>v!=id).ToArray():c.Favorites.Append(id).ToArray());}
    private async void PresetClick(object sender,RoutedEventArgs e){var b=(Button)sender;double v=double.Parse(((string)b.Content)[1..],CultureInfo.InvariantCulture);await SetNumber((string)b.Tag,v);}
    private async Task SetNumber(string id,double value){await Change(c=>c.SetMultiplier(id,value));}
    private async void NumberCommit(object sender,KeyboardFocusChangedEventArgs e)=>await Commit((TextBox)sender);
    private async void NumberKey(object sender,KeyEventArgs e){if(e.Key==Key.Enter){e.Handled=true;await Commit((TextBox)sender);}}
    private async Task Commit(TextBox input)
    {
        if(refreshing||editing)return;string id=(string)input.Tag;var f=features.Single(x=>x.Id==id);
        if(!double.TryParse(input.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!Settings.Valid(value)){f.Error=Settings.MultiplierHint;f.Notify();return;}
        f.Error="";if(value==client.Current.Multiplier(id)){f.Notify();return;}await SetNumber(id,value);
    }
    private async void WidgetClick(object s,RoutedEventArgs e){bool v=WidgetVisible.IsChecked==true;await Change(c=>c.WidgetVisible=v);}
    private async void ActiveListClick(object s,RoutedEventArgs e){bool v=ActiveListVisible.IsChecked==true;await Change(c=>c.ActiveListVisible=v);}
    private async void MotionClick(object s,RoutedEventArgs e){bool v=ReduceMotion.IsChecked==true;await Change(c=>c.ReduceMotion=v);}
    private async Task CommitVisual(){float alpha=(float)OpacitySlider.Value/100,scale=(float)ScaleSlider.Value/100;await Change(c=>{c.WidgetOpacity=alpha;c.UiScale=scale;});}
    private async void VisualCommit(object s,MouseButtonEventArgs e)=>await CommitVisual();
    private async void VisualKeyCommit(object s,KeyEventArgs e){if(e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End)await CommitVisual();}
    private async void ResetWidget(object s,RoutedEventArgs e)=>await Change(c=>{c.WidgetX=1;c.WidgetY=0;});
    private void CaptureClick(object sender,RoutedEventArgs e){capture=(string)((Button)sender).Tag;Notice.Text="Нажмите сочетание для «"+FeatureCatalog.ActionTitle(capture)+"». Esc — отмена.";Keyboard.Focus((Button)sender);}
    private static int Modifiers()=>((Keyboard.Modifiers&ModifierKeys.Control)!=0?1:0)|((Keyboard.Modifiers&ModifierKeys.Alt)!=0?2:0)|((Keyboard.Modifiers&ModifierKeys.Shift)!=0?4:0);
    private async void CaptureKey(object sender,KeyEventArgs e)
    {
        if(capture==null)return;e.Handled=true;if(e.IsRepeat)return;Key key=e.Key==Key.System?e.SystemKey:e.Key;
        if(key==Key.Escape){capture=null;Notice.Text="Назначение отменено.";return;}
        if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift)return;
        if((Keyboard.Modifiers&ModifierKeys.Windows)!=0){Notice.Text="Сочетания Windows недоступны.";return;}
        await Assign(new(){Key=key==Key.Back?0:KeyInterop.VirtualKeyFromKey(key),Modifiers=key==Key.Back?0:Modifiers()});
    }
    private async void CaptureMouse(object sender,MouseButtonEventArgs e)
    {
        if(capture==null||e.ChangedButton is MouseButton.Left or MouseButton.Right)return;
        e.Handled=true;await Assign(new(){Key=e.ChangedButton==MouseButton.XButton1?5:e.ChangedButton==MouseButton.XButton2?6:4,Modifiers=Modifiers()});
    }
    private async Task Assign(KeyBinding binding){string id=capture;capture=null;await Change(c=>c.Bindings[id]=binding);}
    private IProgress<SetupProgress> Reporter()=>new Progress<SetupProgress>(p=>{Detail.Text=p.Message;Progress.Visibility=Visibility.Visible;Progress.IsIndeterminate=p.Percent==null;if(p.Percent!=null)Progress.Value=p.Percent.Value;});
    private async Task Prepare(bool force=false)
    {
        if(!Installation.ValidGame(client.Game))return;
        supported=await Task.Run(()=>Compatibility.Supported(client.Game));
        if(!supported||Installation.Running)return;
        if(Installation.Restored(client.Game)&&!force){Detail.Text="Возвращена предыдущая установка. Для обновления используйте «Проверить / восстановить» в настройках.";return;}
        ready=await Installation.EnsureReadyAsync(client.Game,Reporter(),lifetime.Token,force);await TryRefresh();
    }
    private async Task Run(Func<Task> action)
    {
        if(busy)return;busy=true;RefreshState();
        try{await action();}catch(Exception e){ShowError(e);}finally{busy=false;Progress.Visibility=Visibility.Collapsed;RefreshState();}
    }
    private void RefreshState()
    {
        bool valid=Installation.ValidGame(client.Game),running=Installation.Running;
        Connection.Text=running?(client.Connected?"● Игра подключена":"○ Нет связи с игрой"):"○ Игра не запущена";
        State.Text=busy?"Подготовка…":running?"Игра запущена":!valid?"Найдём вашу игру":!supported?"Нужна проверка совместимости":ready?"Готово к игре":Installation.Restored(client.Game)?"Возвращена предыдущая установка":"Нужна подготовка";
        if(!valid)Detail.Text="Установите Idle Slayer через Steam или выберите папку в настройках.";
        else if(!supported&&!busy)Detail.Text="Для неизвестной версии игры доступен запуск без модов.";
        else if(ready&&!busy&&!running)Detail.Text="Все компоненты готовы. Настройки применятся при запуске.";
        GamePath.Text=valid?client.Game:"Папка игры не выбрана";
        Play.IsEnabled=valid&&!busy&&!running&&supported&&(ready||Installation.Restored(client.Game));
        Vanilla.IsEnabled=valid&&!busy&&!running;
        Choose.IsEnabled=Repair.IsEnabled=Restore.IsEnabled=!busy&&!running;
        FeaturesPage.IsEnabled=valid&&!busy&&!editing&&(!running||client.Connected);
        KeyList.IsEnabled=WidgetVisible.IsEnabled=ActiveListVisible.IsEnabled=ReduceMotion.IsEnabled=OpacitySlider.IsEnabled=ScaleSlider.IsEnabled=valid&&!busy&&!editing&&(!running||client.Connected);
    }
    private async void PlayClick(object s,RoutedEventArgs e)=>await Run(()=>Installation.PlayAsync(client.Game,false,Reporter(),lifetime.Token));
    private async void VanillaClick(object s,RoutedEventArgs e)=>await Run(()=>Installation.PlayAsync(client.Game,true,Reporter(),lifetime.Token));
    private async void RepairClick(object s,RoutedEventArgs e)=>await Run(()=>Prepare(true));
    private async void RestoreClick(object s,RoutedEventArgs e)=>await Run(async()=>{await Task.Run(()=>Installation.Restore(client.Game));ready=false;Detail.Text="Предыдущая установка восстановлена. Сохранения не откатывались.";await TryRefresh();});
    private async void ChooseGame(object s,RoutedEventArgs e){var dialog=new OpenFolderDialog{Title="Установленная игра Idle Slayer"};if(dialog.ShowDialog()!=true)return;await Run(async()=>{Installation.RememberGame(dialog.FolderName);client.Game=dialog.FolderName;ready=false;await TryRefresh();await Prepare();});}
    private void OpenHome(object s,RoutedEventArgs e)=>Process.Start(new ProcessStartInfo(Product.Home){UseShellExecute=true});
    private void OpenLog(object s,RoutedEventArgs e){if(File.Exists(Product.Log))Process.Start(new ProcessStartInfo(Product.Log){UseShellExecute=true});else Notice.Text="Журнал пока пуст.";}
}