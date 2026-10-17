using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using DivineLabs.DesktopUI;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class LanguageMenu : UserControl
{
    public event EventHandler LanguageChanged;
    private Window owner;
    private bool keyboardOpened;
    public LanguageMenu()
    {
        InitializeComponent();ThemeManager.Attach(this);Refresh();
        Loaded+=(_,_)=>{
            owner=Window.GetWindow(this);
            if(owner==null)return;
            owner.PreviewMouseDown+=OwnerMouseDown;owner.Deactivated+=OwnerDeactivated;
            owner.LocationChanged+=OwnerMoved;owner.SizeChanged+=OwnerResized;
        };
        Unloaded+=(_,_)=>{
            Menu.IsOpen=false;
            if(owner==null)return;
            owner.PreviewMouseDown-=OwnerMouseDown;owner.Deactivated-=OwnerDeactivated;
            owner.LocationChanged-=OwnerMoved;owner.SizeChanged-=OwnerResized;owner=null;
        };
    }
    private void ToggleMenu(object sender,RoutedEventArgs e)
    {keyboardOpened=false;Menu.IsOpen=!Menu.IsOpen;}
    private void TriggerKeyDown(object sender,KeyEventArgs e)
    {
        if(e.Key is Key.Enter or Key.Space or Key.Down){e.Handled=true;keyboardOpened=true;Menu.IsOpen=e.Key==Key.Down||!Menu.IsOpen;if(Menu.IsOpen)Auto.Focus();}
        else if(e.Key==Key.Escape&&Menu.IsOpen){e.Handled=true;CloseMenu(true);}
    }
    private void MenuKeyDown(object sender,KeyEventArgs e)
    {if(e.Key==Key.Escape){e.Handled=true;CloseMenu(true);}}
    private void CloseMenu(bool restoreFocus=false)
    {
        bool hadFocus=IsKeyboardFocusWithin||(Menu.Child is UIElement child&&child.IsKeyboardFocusWithin);
        Menu.IsOpen=false;if(restoreFocus)Trigger.Focus();else if(hadFocus)Keyboard.ClearFocus();
    }
    private void OwnerMouseDown(object sender,MouseButtonEventArgs e)
    {
        if(!Menu.IsOpen)return;
        if(e.OriginalSource is DependencyObject source&&(IsInside(source,Trigger)||IsInside(source,Menu.Child)))return;
        CloseMenu();
    }
    private static bool IsInside(DependencyObject source,DependencyObject root)
    {
        for(var current=source;current!=null;){
            if(current==root)return true;
            current=current is Visual?VisualTreeHelper.GetParent(current):current is ContentElement content?ContentOperations.GetParent(content):LogicalTreeHelper.GetParent(current);
        }
        return false;
    }
    private void OwnerDeactivated(object sender,EventArgs e)=>CloseMenu();
    private void OwnerMoved(object sender,EventArgs e)=>CloseMenu();
    private void OwnerResized(object sender,SizeChangedEventArgs e)=>CloseMenu();
    public void Refresh()
    {
        var text = SetupText.Current;
        string Native(string language)=>language switch{"ru"=>"Россия","uk"=>"Україна","be"=>"Беларусь","de"=>"Deutschland","es"=>"España","ko"=>"대한민국","zh"=>"中国","ja"=>"日本",_=>"United Kingdom"};
        string Flag(string language)=>language switch{"ru"=>"FlagRu","uk"=>"FlagUk","be"=>"FlagBy","de"=>"FlagDe","es"=>"FlagEs","ko"=>"FlagKr","zh"=>"FlagCn","ja"=>"FlagJp",_=>"FlagEn"};
        CurrentLabel.Text = text.Selection == "auto" ? text["Automatic"] : Native(text.EffectiveLanguage);
        CurrentFlag.Source = (ImageSource)FindResource(text.Selection == "auto" ? "FlagAuto" : Flag(text.EffectiveLanguage));
        RuCheck.Visibility = text.Selection == "ru" ? Visibility.Visible : Visibility.Hidden;
        EnCheck.Visibility = text.Selection == "en" ? Visibility.Visible : Visibility.Hidden;
        UkCheck.Visibility = text.Selection == "uk" ? Visibility.Visible : Visibility.Hidden;
        DeCheck.Visibility = text.Selection == "de" ? Visibility.Visible : Visibility.Hidden;
        EsCheck.Visibility = text.Selection == "es" ? Visibility.Visible : Visibility.Hidden;
        ByCheck.Visibility = text.Selection == "be" ? Visibility.Visible : Visibility.Hidden;
        KrCheck.Visibility = text.Selection == "ko" ? Visibility.Visible : Visibility.Hidden;
        CnCheck.Visibility = text.Selection == "zh" ? Visibility.Visible : Visibility.Hidden;
        JpCheck.Visibility = text.Selection == "ja" ? Visibility.Visible : Visibility.Hidden;
        Trigger.ToolTip=CurrentLabel.Text;
        Auto.Background = (Brush)FindResource(text.Selection == "auto" ? "SelectedSurface" : "WhiteSurface");
        Recommended.Text = text["Recommended"] + Native(SetupText.ResolveLanguage(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName));
    }
    private void MenuOpened(object sender, EventArgs e)
    {
        Refresh();int columns=owner!=null&&owner.ActualWidth<1000?2:3;
        LanguageGrid.Columns=columns;MenuPanel.Width=columns*164+18;
        Menu.HorizontalOffset=Trigger.ActualWidth-MenuPanel.Width;
        Motion.Enter((FrameworkElement)Menu.Child);
    }
    private void SelectLanguage(object sender, RoutedEventArgs e)
    { SetupText.Current.Select((string)((Button)sender).Tag); InterfacePreferences.Save(); Refresh(); CloseMenu(keyboardOpened); LanguageChanged?.Invoke(this, EventArgs.Empty); }
}
