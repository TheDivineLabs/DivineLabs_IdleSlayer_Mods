using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DivineLabs.DesktopUI;
using DivineLabs.IdleSlayer.Launcher;
using DivineLabs.IdleSlayer.Runtime;

internal static class AppearanceChecks
{
    internal static void Run(string output)
    {
        var missing=(from language in SetupText.Languages from key in SetupText.Keys where !SetupText.HasTranslation(language,key) select language+":"+key).ToArray();
        if(missing.Length>0)throw new Exception("Missing translations: "+string.Join(",",missing));
        if(SetupText.ResolveLanguage("it")!="en"||SetupText.ResolveLanguage("ja")!="ja"||SetupText.ResolveLanguage("be")!="be")throw new Exception("Language detection fallback failed");
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        foreach(string theme in new[]{"light","dark"})foreach(string language in SetupText.Languages){
            ThemeManager.Select(theme);SetupText.Current.Select(language);
            foreach(bool uninstall in new[]{false,true}){
                var args=new List<string>{"--worker",Path.Combine(output,"no-worker.exe"),"--dir",Path.Combine(output,"no-install")};if(uninstall)args.Add("--uninstall");
                var window=new MaintenanceWindow(args.ToArray());
                Motion.SetEnabled(window,false);
                foreach(int page in uninstall?new[]{0,2,3,4}:new[]{0,1,2,3,4}){
                    typeof(MaintenanceWindow).GetMethod("ShowPage",flags)!.Invoke(window,new object[]{page,false});
                    Capture(window,$"{(uninstall?"Uninstaller":"Installer")}-{page}-{language}-{theme}",output);
                    if(page==1){var license=((TextBox)window.FindName("LicenseText")).Text;if(!license.Contains("Copyright (c) 2026 DivineLabs")||!license.Contains("MIT License"))throw new Exception("Own MIT license was not embedded");}
                }
                window.Close();
            }
            var launcher=new MainWindow();Motion.SetEnabled(launcher,false);
            foreach(string page in new[]{"HomePage","SettingsPage","FeaturesPage","ResourcesPage","ClientsPage"}){
                foreach(string other in new[]{"HomePage","SettingsPage","FeaturesPage","ResourcesPage","ClientsPage"})((FrameworkElement)launcher.FindName(other)).Visibility=other==page?Visibility.Visible:Visibility.Collapsed;
                ((FrameworkElement)launcher.FindName(page)).IsEnabled=true;
                Capture(launcher,$"Launcher-{page}-{language}-{theme}",output);
            }
            launcher.Close();
        }
        string fixture=Path.GetFullPath(Path.Combine(output,"discovery-fixture"));Directory.CreateDirectory(fixture);
        File.Copy(typeof(MainWindow).Assembly.Location,Path.Combine(fixture,"Client.exe"),true);
        string unrelated=Path.Combine(fixture,"unrelated");Directory.CreateDirectory(unrelated);File.WriteAllText(Path.Combine(unrelated,"DivineLabs-fake.exe"),"not an application");
        var discovery=InstalledApps.Find(fixture);
        if(!discovery.Apps.Any(app=>app.Folder.Equals(fixture,StringComparison.OrdinalIgnoreCase)))throw new Exception("Portable app metadata discovery failed");
        if(discovery.Apps.Any(app=>app.Folder.Equals(unrelated,StringComparison.OrdinalIgnoreCase)))throw new Exception("Discovery trusted an EXE filename without product metadata");
        File.WriteAllText(Path.Combine(output,"appearance-checks.json"),JsonSerializer.Serialize(new{Languages=SetupText.Languages,Keys=SetupText.Keys.Count(),Themes=new[]{"light","dark","system (selection only)"},Surfaces="Installer, uninstaller and launcher",Discovery=discovery.Apps,PhysicalInput="Not exercised"},new JsonSerializerOptions{WriteIndented=true}));
        ThemeManager.Select("light");SetupText.Current.Select("ru");
    }
    private static void Capture(Window window,string name,string output)
    {
        var root=(FrameworkElement)window.Content;root.Width=1100;root.Height=790;
        root.Measure(new Size(1100,790));root.Arrange(new Rect(0,0,1100,790));root.UpdateLayout();
        window.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.Render);root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(1100,790,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var file=File.Create(Path.Combine(output,name+".png"));encoder.Save(file);
    }
}
