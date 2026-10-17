using System.ComponentModel;
using System.Windows.Media;
using DivineLabs.DesktopUI;
namespace DivineLabs.IdleSlayer.Launcher;
internal sealed class MaintenanceStep : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;
    public string Title { get; private set; }
    public int Index { get; }
    private int current=-1;
    public MaintenanceStep(string title,int index){Title=title;Index=index;}
    public bool IsActive=>Index==current;
    public bool IsReached=>Index<=current;
    public string Number=>Index<current?"✓":(Index+1).ToString();
    public Brush Background=>Index<=current?Accent:Canvas;
    public Brush Foreground=>Index<=current?Brushes.White:Muted;
    public Brush Border=>Index<=current?Accent:Line;
    public Brush Label=>IsActive?Text:Muted;
    private static Brush Canvas=>new SolidColorBrush(ThemeManager.Dark?Color.FromRgb(20,21,31):Color.FromRgb(250,251,254));
    private static readonly Brush Accent=new SolidColorBrush(Color.FromRgb(102,88,232));
    private static Brush Muted=>new SolidColorBrush(ThemeManager.Dark?Color.FromRgb(179,185,208):Color.FromRgb(102,115,137));
    private static Brush Line=>new SolidColorBrush(ThemeManager.Dark?Color.FromRgb(56,59,80):Color.FromRgb(222,227,237));
    private static Brush Text=>new SolidColorBrush(ThemeManager.Dark?Color.FromRgb(240,241,252):Color.FromRgb(27,38,56));
    public void Refresh()=>PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));
    public void Update(int value){if(current==value)return;current=value;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(null));}
    public void Rename(string title){Title=title;PropertyChanged?.Invoke(this,new PropertyChangedEventArgs(nameof(Title)));}
}
