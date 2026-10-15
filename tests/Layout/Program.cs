using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivineLabs.IdleSlayer.Launcher;
internal static class Program
{
    [STAThread]static void Main(string[] args)
    {
        string output=args.Length>0?args[0]:@"W:\DEV\artifacts\DivineLabs_IdleSlayer_Mods\design-0.4";Directory.CreateDirectory(output);
        var app=new Application();
        if(!args.Contains("--maintenance-only")) foreach(double scale in new[]{1d,1.5d,2d}){
            var window=new MainWindow();
            foreach(string name in new[]{"HomePage","FeaturesPage","SettingsPage"}){
                foreach(string other in new[]{"HomePage","FeaturesPage","SettingsPage"})((FrameworkElement)window.FindName(other)).Visibility=other==name?Visibility.Visible:Visibility.Collapsed;
                ((FrameworkElement)window.FindName(name)).IsEnabled=true;
                Render(window,1100,740,scale,Path.Combine(output,name+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));
            }
            window.Close();
        }
        foreach(bool uninstall in new[]{false,true}){
            var arguments=new List<string>{"--worker",Path.Combine(output,"unused.exe"),"--dir",DivineLabs.IdleSlayer.Maintenance.InstallTarget.DefaultDirectory};if(uninstall)arguments.Add("--uninstall");
            var window=new MaintenanceWindow(arguments.ToArray());
            DivineLabs.DesktopUI.Motion.SetEnabled(window,false);
            var show=typeof(MaintenanceWindow).GetMethod("ShowPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
            foreach(int page in uninstall?new[]{0,2,3,4}:new[]{0,1,2,3,4}){
                if(page==4)typeof(MaintenanceWindow).GetField("done",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,true);
                typeof(MaintenanceWindow).GetField("busy",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(window,page==3);
                show.Invoke(window,new object[]{page,false});
                if(page==3)typeof(MaintenanceWindow).GetMethod("ProgressState",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(window,new object[]{62,"Распаковка программы…"});
                if(!uninstall&&page==1){
                    var primary=(Button)window.FindName("Primary");var accepted=(CheckBox)window.FindName("Accepted");
                    if(primary.IsEnabled)throw new Exception("License consent gate is bypassed");
                    if(((System.Windows.Media.SolidColorBrush)primary.Background).Color!=System.Windows.Media.Color.FromRgb(217,77,102))throw new Exception("Unaccepted license does not show the required red state");
                    accepted.IsChecked=true;if(!primary.IsEnabled)throw new Exception("License acceptance does not enable continuation");
                    accepted.IsChecked=false;
                    if(((TextBox)window.FindName("LicenseText")).Text.Length<1000)throw new Exception("License resource is missing");
                }
                foreach(double scale in new[]{1d,1.5d,2d})
                    Render(window,1100,790,scale,Path.Combine(output,(uninstall?"Uninstaller":"Installer")+"-"+page+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));
            }
            window.Close();
        }
        Console.WriteLine("WPF layout renders complete: "+output);
        app.Shutdown();
    }
    private static void Render(Window window,int width,int height,double scale,string path)
    {
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Background);
        var root=(FrameworkElement)window.Content;root.Width=width-root.Margin.Left-root.Margin.Right;root.Height=height-root.Margin.Top-root.Margin.Bottom;if(root is Panel panel)panel.Background=window.Background;
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Render);root.UpdateLayout();
        if(!root.IsMeasureValid||!root.IsArrangeValid)throw new Exception("Layout did not settle");
        var bmp=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bmp.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);
    }
}