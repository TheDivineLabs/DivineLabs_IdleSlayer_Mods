using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DivineLabs.IdleSlayer.Launcher;
public static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        if(args.Length>0 && args[0]=="--verify-install") {try{Installation.Install(Installation.FindGame());Environment.Exit(0);}catch(Exception e){Directory.CreateDirectory(Installation.Data);File.WriteAllText(Path.Combine(Installation.Data,"install-error.txt"),e.ToString());Environment.Exit(2);}return;}
        if(args.Length>0 && args[0]=="--verify-restore") { string game=Installation.FindGame();Installation.Disable(game);Installation.Enable(game);Installation.Restore(game);Installation.Restore(game);return; }
        if(args.Length>0 && args[0]=="--play") { Installation.Play(Installation.FindGame(),false); return; }
        new Application().Run(new MainWindow());
    }
}
internal sealed class MainWindow:Window
{
    private string game;
    private readonly TextBlock location=new(),state=new(),message=new();
    private readonly TextBox coins=new(),damage=new();
    private readonly CheckBox enabled=new(){Content="Множители включены"};
    private readonly StackPanel settingsPanel=new();
    private readonly List<Button> mutationButtons=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private bool busy,wasRunning;
    private bool supported;
    public MainWindow()
    {
        Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/DivineLabs.png"));Title="DivineLabs • Idle Slayer";Width=1020;Height=760;MinWidth=850;MinHeight=650;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=new SolidColorBrush(Color.FromRgb(14,18,28));Foreground=Brushes.White;FontFamily=new FontFamily("Segoe UI");FontSize=15;
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Content=scroll;
        var body=new StackPanel{Margin=new Thickness(34)};scroll.Content=body;
        body.Children.Add(new TextBlock{Text="DIVINELABS",FontSize=34,FontWeight=FontWeights.Bold,Foreground=Brushes.DeepSkyBlue});
        body.Children.Add(new TextBlock{Text="IDLE SLAYER  /  ПЕРСОНАЛЬНЫЕ НАСТРОЙКИ",Margin=new Thickness(0,8,0,24),FontSize=16});
        body.Children.Add(state);location.TextWrapping=TextWrapping.Wrap;location.Margin=new Thickness(0,8,0,12);location.Foreground=Brushes.LightSlateGray;body.Children.Add(location);
        var top=new WrapPanel();body.Children.Add(top);
        Add(top,"Выбрать папку",()=>{var dialog=new OpenFolderDialog{Title="Папка Idle Slayer"};if(dialog.ShowDialog()==true){if(!Installation.ValidGame(dialog.FolderName))throw new IOException("Здесь нет Idle Slayer.exe и GameAssembly.dll.");game=dialog.FolderName;Directory.CreateDirectory(Installation.Data);File.WriteAllText(Path.Combine(Installation.Data,"game-path.txt"),game);LoadConfig();}},true);
        Add(top,"Установить / обновить",()=>Installation.Install(game),true);
        Add(top,"Вернуть прошлую установку",()=>Installation.Restore(game),true);
        body.Children.Add(new Border{Height=1,Background=Brushes.DimGray,Margin=new Thickness(0,18,0,18)});
        settingsPanel.Children.Add(new TextBlock{Text="Функции",FontSize=23,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        enabled.Margin=new Thickness(0,0,0,16);enabled.Foreground=Brushes.White;settingsPanel.Children.Add(enabled);
        Row("Монеты • только получаемый доход",coins);Row("Урон • обычные враги",damage);
        var actions=new WrapPanel();settingsPanel.Children.Add(actions);Add(actions,"Сохранить настройки",Save,true);
        Add(actions,"Вернуть ×100 / ×1000",()=>{coins.Text="100";damage.Text="1000";Save();},true);
        body.Children.Add(settingsPanel);
        body.Children.Add(new TextBlock{Text="В игре: F8 — меню. Покупки списывают обычную стоимость.",Margin=new Thickness(0,12,0,24),Foreground=Brushes.LightSlateGray});
        var play=new WrapPanel();body.Children.Add(play);
        Add(play,"▶  ИГРАТЬ",()=>{Save();Installation.Play(game,false);},true);
        Add(play,"Запустить без модов",()=>Installation.Play(game,true),true);
        Add(play,"Журнал программы",()=>{string file=Path.Combine(Installation.Data,"launcher.log");if(File.Exists(file))Process.Start(new ProcessStartInfo(file){UseShellExecute=true});else message.Text="Журнал программы пока пуст.";},false);
        Add(play,"Журнал игры",()=>{string file=Path.Combine(game,"MelonLoader","Latest.log");if(File.Exists(file))Process.Start(new ProcessStartInfo(file){UseShellExecute=true});},false);
        Add(play,"Компоненты",()=>MessageBox.Show(this,"DivineLabs IdleSlayer Mods\nСобственные лаунчер, меню и функции.\n\nЗагрузчик основан на MelonLoader 0.7.3 (Apache-2.0).\nUnity, .NET, Harmony и другие компоненты сохраняют лицензии и авторство.\nПодробности: THIRD-PARTY-NOTICES.txt рядом с приложением.","Компоненты"),false);
        message.TextWrapping=TextWrapping.Wrap;message.Margin=new Thickness(0,20,0,0);body.Children.Add(message);
        game=Installation.FindGame();try{LoadConfig();}catch(Exception e){Report(e);}
        Refresh();timer.Tick+=(_,_)=>{bool running=Installation.Running;if(wasRunning&&!running){try{LoadConfig();}catch(Exception e){Report(e);}}wasRunning=running;Refresh(false);};timer.Start();
    }
    private void Report(Exception error)
    { Directory.CreateDirectory(Installation.Data);File.AppendAllText(Path.Combine(Installation.Data,"launcher.log"),DateTime.UtcNow+" "+error+Environment.NewLine);message.Text=System.Text.RegularExpressions.Regex.IsMatch(error.Message,@"^[А-ЯЁа-яё]")?error.Message:"Не удалось выполнить действие. Подробности — в журнале программы."; }
    private void Row(string title,TextBox input)
    {settingsPanel.Children.Add(new TextBlock{Text=title,Margin=new Thickness(0,0,0,8)});var row=new WrapPanel{Margin=new Thickness(0,0,0,18)};input.Width=170;input.Height=34;input.FontSize=19;input.Margin=new Thickness(0,0,12,0);row.Children.Add(input);foreach(int n in new[]{1,10,100,1000}){int value=n;Add(row,"×"+value,()=>input.Text=value.ToString(CultureInfo.InvariantCulture),false);}settingsPanel.Children.Add(row);}
    private void Add(Panel panel,string label,Action action,bool mutation)
    {var button=new Button{Content=label,Padding=new Thickness(15,8,15,8),Margin=new Thickness(0,0,10,8),Background=new SolidColorBrush(Color.FromRgb(35,49,69)),Foreground=Brushes.White,BorderBrush=Brushes.SlateGray};panel.Children.Add(button);if(mutation)mutationButtons.Add(button);button.Click+=async(_,_)=>{if(busy)return;busy=true;Refresh(false);try{if(mutation)Installation.RequireStopped();await Dispatcher.InvokeAsync(()=>{},DispatcherPriority.Render);action();message.Text="Готово.";Refresh();}catch(Exception e){Report(e);}finally{busy=false;Refresh(false);}};}
    private void LoadConfig()
    {if(!Installation.ValidGame(game))return;Settings config;if(Installation.Running){string path=Installation.Config(game);config=File.Exists(path)?SettingsStore.Read(path):new Settings();}else config=SettingsStore.Load(Installation.Config(game));coins.Text=config.CoinMultiplier.ToString(CultureInfo.InvariantCulture);damage.Text=config.DamageMultiplier.ToString(CultureInfo.InvariantCulture);enabled.IsChecked=config.Enabled;}
    private void Save()
    {Installation.RequireStopped();if(!Installation.ValidGame(game))throw new IOException("Выберите папку игры.");var s=SettingsStore.Load(Installation.Config(game));s.Enabled=enabled.IsChecked==true;s.CoinMultiplier=double.Parse(coins.Text.Replace(',','.'),CultureInfo.InvariantCulture);s.DamageMultiplier=double.Parse(damage.Text.Replace(',','.'),CultureInfo.InvariantCulture);SettingsStore.Save(Installation.Config(game),s);}
    private void Refresh(bool checkVersion=true)
    {bool valid=Installation.ValidGame(game);if(checkVersion&&valid)supported=Compatibility.Supported(game);bool running=Installation.Running;settingsPanel.IsEnabled=valid&&!running&&!busy;foreach(var b in mutationButtons)b.IsEnabled=!running&&!busy;location.Text=valid?game:"Игра не найдена. Укажите папку установки.";state.Text=running?"● Игра работает • настройки доступны в игровом меню":!valid?"○ Выберите игру":!supported?"○ Версия не проверена • доступен обычный запуск":Installation.Installed(game)?"● DivineLabs установлен • сборка 25810322 поддерживается":"○ Игра совместима • установите компоненты DivineLabs";}
}
