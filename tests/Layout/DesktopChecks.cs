using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DivineLabs.IdleSlayer.Launcher;

internal static class DesktopChecks
{
    public static void Run(string output)
    {
        var window=new MaintenanceWindow(new[]{"--worker",Path.Combine(output,"no-worker.exe"),"--dir",Path.Combine(output,"no-install")});
        DivineLabs.DesktopUI.Motion.SetEnabled(window,false);
        try{
            window.Show();Pump(window);
            var language=(LanguageMenu)window.FindName("LanguagePicker");
            var trigger=(Button)language.FindName("Trigger");var popup=(Popup)language.FindName("Menu");
            trigger.Focus();trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
            Require(popup.IsOpen,"Language menu did not open");
            // The anchor receives the preview event before the Click event. It must toggle once.
            trigger.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
            Require(popup.IsOpen,"Anchor preview unexpectedly dismissed the menu");
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
            Require(!popup.IsOpen,"A second anchor click reopened the menu");
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
            ((FrameworkElement)window.FindName("WelcomeTitle")).RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice,0,MouseButton.Left){RoutedEvent=Mouse.PreviewMouseDownEvent});
            Pump(window);Require(!popup.IsOpen,"An outside click did not dismiss the menu");
            Require(!trigger.IsKeyboardFocused,"Outside dismissal left the language anchor focused");

            var source=PresentationSource.FromVisual(trigger)!;
            trigger.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Down){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump(window);
            Require(popup.IsOpen,"Keyboard opening failed");
            ((UIElement)popup.Child).RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,Key.Escape){RoutedEvent=Keyboard.PreviewKeyDownEvent});Pump(window);
            Require(!popup.IsOpen,"Escape did not dismiss the language menu");

            var picker=FindThemePicker(window)??throw new InvalidOperationException("Theme picker missing");
            var themes=(ComboBox)picker.FindName("Choice");
            themes.SelectedIndex=2;Pump(window);
            Require(DivineLabs.DesktopUI.ThemeManager.Selection=="dark"&&((SolidColorBrush)window.FindResource("Background")).Color.R<40,"Theme picker did not apply the dark palette");
            themes.SelectedIndex=1;Pump(window);
            Require(DivineLabs.DesktopUI.ThemeManager.Selection=="light"&&((SolidColorBrush)window.FindResource("Background")).Color.R>240,"Theme picker did not apply the light palette");
            Require(Math.Abs(themes.ActualWidth-trigger.ActualWidth)<.1&&Math.Abs(themes.ActualHeight-trigger.ActualHeight)<.1,"Language and theme controls have different bounds");
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
            Require(((UniformGrid)language.FindName("LanguageGrid")).Columns==3,"Wide language menu is not three columns");
            CaptureElement((FrameworkElement)popup.Child,Path.Combine(output,"language-grid-wide.png"));
            window.Width=960;Pump(window);
            trigger.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
            Require(((UniformGrid)language.FindName("LanguageGrid")).Columns==2,"Narrow language menu is not two columns");
            CaptureElement((FrameworkElement)popup.Child,Path.Combine(output,"language-grid-narrow.png"));
            foreach(var (name,code,country) in new[]{("Belarusian","be","Беларусь"),("Korean","ko","대한민국"),("Chinese","zh","中国"),("Japanese","ja","日本")}){
                ((Button)language.FindName(name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Pump(window);
                Require(SetupText.Current.EffectiveLanguage==code&&((TextBlock)language.FindName("CurrentLabel")).Text==country,"New language selection did not apply: "+code);
            }
            Capture(window,Path.Combine(output,"desktop-japanese.png"));
            ((Button)language.FindName("Russian")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.Width=1100;Pump(window);

            var primary=(Button)window.FindName("Primary");
            primary.ApplyTemplate();
            var hover=(FrameworkElement)primary.Template.FindName("Hover",primary);
            Require(hover.ActualWidth>=primary.ActualWidth-2.1&&hover.ActualHeight>=primary.ActualHeight-2.1,"Button hover does not fill its surface");
            Capture(window,Path.Combine(output,"desktop-normal.png"));

            window.WindowState=WindowState.Maximized;Pump(window);
            var actualFooter=primary.TransformToAncestor(window).TransformBounds(new Rect(primary.RenderSize));
            var work=SystemParameters.WorkArea;
            Require(window.ActualHeight<=work.Height+1&&window.ActualWidth<=work.Width+1,"Maximized window exceeds the primary monitor work area");
            Require(actualFooter.Bottom<=window.ActualHeight+1&&actualFooter.Top>=0,"Footer buttons are clipped in the maximized window");
            Capture(window,Path.Combine(output,"desktop-maximized.png"));
            File.WriteAllText(Path.Combine(output,"desktop-checks.json"),JsonSerializer.Serialize(new{
                MaxWidth=window.ActualWidth,MaxHeight=window.ActualHeight,WorkArea=work.ToString(),FooterBottom=actualFooter.Bottom,
                Checks=new[]{"Language repeat click","Outside preview click","Keyboard open and Escape","Theme picker light/dark selection","Equal language/theme bounds","Language grid two/three columns","be/ko/zh/ja selection and native country labels","Full button hover bounds","Actual maximized HWND layout and footer"},
                Input="WPF routed events in a shown HWND; not physical mouse acceptance"
            },new JsonSerializerOptions{WriteIndented=true}));
            window.WindowState=WindowState.Normal;Pump(window);
        }finally{
            typeof(MaintenanceWindow).GetField("allowClose",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,true);
            window.Close();
        }
    }
    private static void Pump(Window window)
    {window.Dispatcher.Invoke(()=>{},DispatcherPriority.ApplicationIdle);window.UpdateLayout();}
    private static void Require(bool result,string message){if(!result)throw new InvalidOperationException(message);}
    private static ThemePicker? FindThemePicker(DependencyObject root)
    {
        if(root is ThemePicker picker)return picker;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var found=FindThemePicker(VisualTreeHelper.GetChild(root,i));if(found!=null)return found;}
        return null;
    }
    private static void Capture(Window window,string path)
    {
        var root=(FrameworkElement)window.Content;
        CaptureElement(root,path);
    }
    private static void CaptureElement(FrameworkElement root,string path)
    {
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
