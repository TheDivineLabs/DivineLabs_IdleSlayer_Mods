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
        foreach(double scale in new[]{1d,1.5d,2d}){
            var window=new MainWindow();
            foreach(string name in new[]{"HomePage","FeaturesPage","SettingsPage"}){
                foreach(string other in new[]{"HomePage","FeaturesPage","SettingsPage"})((FrameworkElement)window.FindName(other)).Visibility=other==name?Visibility.Visible:Visibility.Collapsed;
                ((FrameworkElement)window.FindName(name)).IsEnabled=true;
                Render(window,1100,740,scale,Path.Combine(output,name+"-"+scale.ToString(System.Globalization.CultureInfo.InvariantCulture)+".png"));
            }
            window.Close();
        }
        foreach(bool uninstall in new[]{false,true}){
            var arguments=new List<string>{"--worker",Path.Combine(output,"unused.exe"),"--dir",@"W:\DEV\tools\DivineLabs\IdleSlayer"};if(uninstall)arguments.Add("--uninstall");
            var window=new MaintenanceWindow(arguments.ToArray());Render(window,760,600,1,Path.Combine(output,uninstall?"Uninstaller.png":"Installer.png"));window.Close();
        }
        Console.WriteLine("WPF layout renders complete: "+output);
        app.Shutdown();
    }
    private static void Render(Window window,int width,int height,double scale,string path)
    {
        var root=(FrameworkElement)window.Content;root.Width=width-root.Margin.Left-root.Margin.Right;root.Height=height-root.Margin.Top-root.Margin.Bottom;if(root is Panel panel)panel.Background=window.Background;
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        if(!root.IsMeasureValid||!root.IsArrangeValid)throw new Exception("Layout did not settle");
        var bmp=new RenderTargetBitmap((int)(width*scale),(int)(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bmp.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var file=File.Create(path);encoder.Save(file);
    }
}