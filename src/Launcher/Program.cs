using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
public static class Program
{
    [STAThread] public static void Main(string[] args)
    {
        Product.MigrateData();
        if(args.Length>0)
        {
            try
            {
                int pos=Array.IndexOf(args,"--game");string game=pos>=0&&pos+1<args.Length?args[pos+1]:Installation.FindGame();
                switch(args[0])
                {
                    case "--verify-package":Payload.Verify(Payload.EnsureAsync().GetAwaiter().GetResult());return;
                    case "--prepare":case "--verify-install":Installation.EnsureReadyAsync(game,force:true,reuseCache:!args.Contains("--fresh")).GetAwaiter().GetResult();return;
                    case "--play":Installation.PlayAsync(game,false).GetAwaiter().GetResult();return;
                    case "--disable":case "--uninstall-components":Installation.Disable(game);return;
                    case "--restore":Installation.Restore(game);return;
                }
            }
            catch(Exception e){Product.Record(e);Environment.ExitCode=2;return;}
        }
        using var mutex=new Mutex(true,@"Local\DivineLabs.IdleSlayer.Launcher",out bool first);
        if(!first){foreach(var p in Process.GetProcessesByName("DivineLabs.IdleSlayer.Launcher"))if(p.Id!=Environment.ProcessId&&p.MainWindowHandle!=0){ShowWindow(p.MainWindowHandle,9);SetForegroundWindow(p.MainWindowHandle);break;}return;}
        var app=new Application();app.DispatcherUnhandledException+=(_,e)=>{Product.Record(e.Exception);MessageBox.Show("Не удалось выполнить действие. Подробности сохранены в журнале программы.","DivineLabs",MessageBoxButton.OK,MessageBoxImage.Error);e.Handled=true;};app.Run(new MainWindow());
    }
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr window,int command);
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
}
internal sealed class MainWindow:Window
{
    private string game="";
    private readonly TextBlock location=new(),state=new(),message=new(),version=new();
    private readonly TextBox coins=new(),damage=new();
    private readonly CheckBox enabled=new(){Content="Множители включены"};
    private readonly StackPanel settings=new();
    private readonly ProgressBar progress=new(){Height=5,Maximum=100,Visibility=Visibility.Collapsed};
    private readonly List<Button> mutations=new();
    private readonly CancellationTokenSource lifetime=new();
    private readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromSeconds(2)};
    private bool busy,supported,wasRunning,ready;
    private Button play;
    public MainWindow()
    {
        Icon=new BitmapImage(new Uri("pack://application:,,,/DivineLabs.png"));Title="DivineLabs • Idle Slayer";Width=1040;Height=800;MinWidth=880;MinHeight=690;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=Brush(14,18,28);Foreground=Brushes.White;FontFamily=new FontFamily("Segoe UI");FontSize=15;
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Content=scroll;
        var body=new StackPanel{Margin=new Thickness(32)};scroll.Content=body;
        var header=new DockPanel{Margin=new Thickness(0,0,0,22)};
        header.Children.Add(new Image{Source=Icon,Width=64,Height=64,Margin=new Thickness(0,0,18,0)});
        var title=new StackPanel();title.Children.Add(new TextBlock{Text="DIVINELABS",FontSize=32,FontWeight=FontWeights.Bold,Foreground=Brush(64,184,242)});title.Children.Add(new TextBlock{Text="IDLE SLAYER MODS  ·  "+Product.Version,Foreground=Brushes.LightSlateGray});header.Children.Add(title);body.Children.Add(header);
        var statusCard=new Border{Background=Brush(23,30,44),CornerRadius=new CornerRadius(12),Padding=new Thickness(20),Margin=new Thickness(0,0,0,20)};
        var statusBody=new StackPanel();statusCard.Child=statusBody;state.FontSize=21;state.FontWeight=FontWeights.SemiBold;statusBody.Children.Add(state);
        version.Foreground=Brushes.LightSlateGray;version.Margin=new Thickness(0,8,0,0);statusBody.Children.Add(version);
        location.Foreground=Brushes.LightSlateGray;location.TextWrapping=TextWrapping.Wrap;location.Margin=new Thickness(0,6,0,0);statusBody.Children.Add(location);
        message.TextWrapping=TextWrapping.Wrap;message.Margin=new Thickness(0,12,0,8);statusBody.Children.Add(message);statusBody.Children.Add(progress);body.Children.Add(statusCard);
        var primary=new WrapPanel{Margin=new Thickness(0,0,0,18)};
        play=Add(primary,"▶  Играть",()=>Run(async()=>{Save();await Installation.PlayAsync(game,false,Reporter(),lifetime.Token);}),true);play.Background=Brush(27,115,160);play.FontWeight=FontWeights.SemiBold;
        Add(primary,"Запустить без модов",()=>Run(()=>Installation.PlayAsync(game,true,Reporter(),lifetime.Token)),true);
        Add(primary,"Проверить / восстановить",()=>Run(()=>Prepare(true)),true);body.Children.Add(primary);
        settings.Children.Add(new TextBlock{Text="Функции",FontSize=23,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        enabled.Foreground=Brushes.White;enabled.Margin=new Thickness(0,0,0,16);settings.Children.Add(enabled);
        Row("Монеты · множитель получаемого дохода",coins);Row("Урон · обычные враги",damage);
        var saves=new WrapPanel();Add(saves,"Сохранить",()=>Run(()=>{Save();message.Text="Настройки сохранены.";return Task.CompletedTask;}),true);
        Add(saves,"×100 / ×1000",()=>Run(()=>{coins.Text="100";damage.Text="1000";Save();message.Text="Настройки сохранены.";return Task.CompletedTask;}),true);settings.Children.Add(saves);body.Children.Add(settings);
        body.Children.Add(new TextBlock{Text="В игре: F8 — меню. Покупки списывают обычную стоимость.",Foreground=Brushes.LightSlateGray,Margin=new Thickness(0,14,0,22)});
        var tools=new WrapPanel();
        Add(tools,"Выбрать игру",SelectGame,true);
        Add(tools,"Вернуть прошлую установку",()=>Run(async()=>{await Task.Run(()=>Installation.Restore(game));ready=false;LoadConfig();message.Text="Предыдущая установка восстановлена. Сохранения игры не откатывались.";}),true);
        Add(tools,"Папка программы",()=>{Process.Start(new ProcessStartInfo(Product.Home){UseShellExecute=true});return Task.CompletedTask;},false);
        Add(tools,"Журнал",()=>{string file=File.Exists(Product.Log)?Product.Log:Path.Combine(Installation.Loader(game),"Latest.log");if(File.Exists(file))Process.Start(new ProcessStartInfo(file){UseShellExecute=true});else message.Text="Журнал пока пуст.";return Task.CompletedTask;},false);
        Add(tools,"Компоненты",()=>{MessageBox.Show(this,"DivineLabs IdleSlayer Mods "+Product.Version+"\n\nСобственные лаунчер, установщик и игровые функции.\nDivineLabs_Loader — адаптированный MelonLoader 0.7.3 (Apache-2.0).\n\n.NET, Unity, Harmony и Il2CppInterop сохраняют своё авторство и лицензии. Полные тексты находятся в папке licenses и THIRD-PARTY-NOTICES.txt.","Компоненты DivineLabs");return Task.CompletedTask;},false);body.Children.Add(tools);
        Loaded+=async(_,_)=>{game=Installation.FindGame();try{LoadConfig();}catch(Exception e){Report(e);}await Run(()=>Prepare());};
        Closing+=(_,_)=>{lifetime.Cancel();timer.Stop();};
        timer.Tick+=async(_,_)=>{bool running=Installation.Running;bool ended=wasRunning&&!running;wasRunning=running;if(ended){try{LoadConfig();}catch(Exception e){Report(e);}if(!ready)await Run(()=>Prepare());}Refresh();};timer.Start();Refresh();
    }
    private static SolidColorBrush Brush(byte r,byte g,byte b)=>new(Color.FromRgb(r,g,b));
    private IProgress<SetupProgress> Reporter()=>new Progress<SetupProgress>(p=>{message.Text=p.Message;progress.Visibility=Visibility.Visible;progress.IsIndeterminate=p.Percent==null;if(p.Percent!=null)progress.Value=p.Percent.Value;});
    private async Task Prepare(bool force=false)
    {
        if(!Installation.ValidGame(game)){message.Text="Установите Idle Slayer в Steam или выберите папку игры. Всё остальное программа подготовит автоматически.";return;}
        supported=await Task.Run(()=>Compatibility.Supported(game));
        if(!supported){message.Text="Для этой версии пока доступен запуск без модов. Совместимые компоненты включатся после проверки версии игры.";return;}
        if(Installation.Restored(game)&&!force){message.Text="Предыдущая установка восстановлена. Автоматическое обновление остановлено; для новой версии нажмите «Проверить / восстановить».";return;}
        ready=await Installation.EnsureReadyAsync(game,Reporter(),lifetime.Token,force);LoadConfig();
    }
    private async Task SelectGame()
    {var dialog=new OpenFolderDialog{Title="Установленная игра Idle Slayer"};if(dialog.ShowDialog()!=true)return;await Run(async()=>{Installation.RememberGame(dialog.FolderName);game=dialog.FolderName;ready=false;LoadConfig();await Prepare();});}
    private async Task Run(Func<Task> action)
    {
        if(busy)return;busy=true;Refresh();
        try{await action();}catch(OperationCanceledException){message.Text="Подготовка отменена. Можно повторить её позже.";}catch(Exception e){ready=false;Report(e);}
        finally{busy=false;progress.Visibility=Visibility.Collapsed;Refresh();}
    }
    private void Report(Exception error)
    {Product.Record(error);message.Text=System.Text.RegularExpressions.Regex.IsMatch(error.Message,@"^[А-ЯЁа-яё]")?error.Message:"Не удалось завершить подготовку. Проверьте подключение к интернету и повторите попытку. Подробности — в журнале.";}
    private Button Add(Panel panel,string text,Func<Task> action,bool mutation)
    {var button=new Button{Content=text,Padding=new Thickness(16,9,16,9),Margin=new Thickness(0,0,10,8),Background=Brush(35,49,69),Foreground=Brushes.White,BorderThickness=new Thickness(0)};if(mutation)mutations.Add(button);button.Click+=async(_,_)=>{try{await action();}catch(Exception e){Report(e);}};panel.Children.Add(button);return button;}
    private void Row(string text,TextBox input)
    {settings.Children.Add(new TextBlock{Text=text,Margin=new Thickness(0,0,0,8)});var row=new WrapPanel{Margin=new Thickness(0,0,0,18)};input.Width=170;input.Height=36;input.FontSize=19;input.Margin=new Thickness(0,0,12,0);row.Children.Add(input);foreach(int n in new[]{1,10,100,1000}){int value=n;Add(row,"×"+value,()=>{input.Text=value.ToString(CultureInfo.InvariantCulture);return Task.CompletedTask;},false);}settings.Children.Add(row);}
    private void LoadConfig()
    {if(!Installation.ValidGame(game))return;string path=Installation.Config(game);var c=Installation.Running?(File.Exists(path)?SettingsStore.Read(path):new Settings()):SettingsStore.Load(path);coins.Text=c.CoinMultiplier.ToString(CultureInfo.InvariantCulture);damage.Text=c.DamageMultiplier.ToString(CultureInfo.InvariantCulture);enabled.IsChecked=c.Enabled;}
    private void Save()
    {Installation.RequireStopped();if(!Installation.ValidGame(game))throw new IOException("Выберите установленную игру.");var c=SettingsStore.Load(Installation.Config(game));if(!double.TryParse(coins.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double money)||!double.TryParse(damage.Text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double hit))throw new InvalidDataException("Введите числа от 1 до 1 000 000.");c.Enabled=enabled.IsChecked==true;c.CoinMultiplier=money;c.DamageMultiplier=hit;SettingsStore.Save(Installation.Config(game),c);}
    private void ShowLaunchProgress()
    {
        try
        {
            string path=Path.Combine(Installation.Loader(game),"Latest.log");if(!File.Exists(path))return;
            using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);if(input.Length>65536)input.Seek(-65536,SeekOrigin.End);using var reader=new StreamReader(input);string text=reader.ReadToEnd();
            if(text.Contains("Меню DivineLabs создано")){state.Text="Игра готова";message.Text="F8 открывает игровое меню. Настройки доступны в самой игре.";progress.Visibility=Visibility.Collapsed;return;}
            state.Text="Подготовка запуска игры";progress.Visibility=Visibility.Visible;progress.IsIndeterminate=true;
            message.Text=text.Contains("Loading Mods")?"Загрузка игры. На стартовом экране нажмите Enter.":text.Contains("Executing Cpp2IL")?"Подготовка связки с Unity. Первый запуск может занять несколько минут.":text.Contains("Downloading")?"Загрузчик скачивает необходимые компоненты первого запуска…":"Запуск загрузчика DivineLabs…";
        }
        catch(IOException){}
    }
    private void Refresh()
    {
        bool valid=Installation.ValidGame(game),running=Installation.Running;
        settings.IsEnabled=valid&&!running&&!busy;foreach(var button in mutations)button.IsEnabled=!running&&!busy;
        play.IsEnabled=valid&&!running&&!busy&&(ready||Installation.Restored(game));
        state.Text=busy?"Подготовка DivineLabs…":running?"Игра работает":!valid?"Найдём вашу игру":!supported?"Нужна проверка совместимости":ready?"Всё готово к игре":Installation.Restored(game)?"Возвращена предыдущая установка":"Компоненты требуют подготовки";
        if(running&&!busy)ShowLaunchProgress();
        version.Text=supported?"Idle Slayer 7.3.4  ·  Steam "+Compatibility.Build:"Windows x64  ·  Steam";location.Text=valid?game:"Папка игры ещё не выбрана";
    }
}
