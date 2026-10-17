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
        InitializeComponent();Refresh();
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
        CurrentLabel.Text = text.Selection == "auto" ? text["Automatic"] : text.English ? "English" : "Русский";
        CurrentFlag.Source = (ImageSource)FindResource(text.Selection == "auto" ? "FlagAuto" : text.English ? "FlagEn" : "FlagRu");
        RuCheck.Visibility = text.Selection == "ru" ? Visibility.Visible : Visibility.Hidden;
        EnCheck.Visibility = text.Selection == "en" ? Visibility.Visible : Visibility.Hidden;
        Auto.Background = (Brush)FindResource(text.Selection == "auto" ? "SelectedSurface" : "WhiteSurface");
        Recommended.Text = text["Recommended"] + (CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "Русский" : "English");
    }
    private void MenuOpened(object sender, EventArgs e) { Refresh(); Motion.Enter((FrameworkElement)Menu.Child); }
    private void SelectLanguage(object sender, RoutedEventArgs e)
    { SetupText.Current.Select((string)((Button)sender).Tag); Refresh(); CloseMenu(keyboardOpened); LanguageChanged?.Invoke(this, EventArgs.Empty); }
}
