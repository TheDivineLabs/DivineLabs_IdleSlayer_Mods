using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DivineLabs.DesktopUI;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class LanguageMenu : UserControl
{
    public event EventHandler LanguageChanged;
    public LanguageMenu() { InitializeComponent(); Refresh(); }
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
    { SetupText.Current.Select((string)((Button)sender).Tag); Refresh(); Menu.IsOpen = false; LanguageChanged?.Invoke(this, EventArgs.Empty); }
}
