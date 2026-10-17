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
                Checks=new[]{"Language repeat click","Outside preview click","Keyboard open and Escape","Full button hover bounds","Actual maximized HWND layout and footer"},
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
    private static void Capture(Window window,string path)
    {
        var root=(FrameworkElement)window.Content;
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(path);encoder.Save(file);
    }
}
