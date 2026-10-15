using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DivineLabs.DesktopUI;
namespace DivineLabs.IdleSlayer.Launcher;
public partial class MaintenanceWindow:Window
{
    private readonly string worker;
    private readonly bool uninstall;
    private bool busy,done;
    private string report;
    public MaintenanceWindow(string[] args)
    {
        string Arg(string name,string fallback=""){int i=Array.IndexOf(args,name);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
        worker=Arg("--worker");uninstall=args.Contains("--uninstall");
        InitializeComponent();Style=(Style)FindResource(typeof(Window));Icon=new BitmapImage(new Uri("pack://application:,,,/DivineLabs.IdleSlayer.Launcher;component/DivineLabs.png"));
        TargetPath.Text=Arg("--dir");Title="DivineLabs · "+(uninstall?"Удаление":"Установка");
        if(uninstall){Eyebrow.Text="УДАЛЕНИЕ";Heading.Text="Удалить DivineLabs?";Subtitle.Text="Программа и её игровые компоненты будут удалены.";Retention.Visibility=Visibility.Visible;Shortcuts.Visibility=Browse.Visibility=Visibility.Collapsed;TargetPath.IsReadOnly=true;Primary.Content="Удалить программу";Cancel.Content="Оставить";Status.Text="Закройте игру перед удалением.";}
        Closing+=(_,e)=>{if(busy){e.Cancel=true;Status.Text="Дождитесь завершения операции с файлами.";}};
        Closed+=(_,_)=>{if(uninstall){try{File.WriteAllText(Path.Combine(Path.GetDirectoryName(worker)!,"maintenance.complete"),"DivineLabs");}catch(IOException){}}};
        Loaded+=(_,_)=>Motion.Enter(this);
    }
    private void PathChanged(object s,TextChangedEventArgs e){if(Primary==null||uninstall)return;bool exists=File.Exists(Path.Combine(TargetPath.Text,"DivineLabs.IdleSlayer.exe"));Primary.Content=exists?"Обновить →":"Установить →";}
    private void BrowseClick(object s,RoutedEventArgs e){var dialog=new OpenFolderDialog{Title="Папка программы DivineLabs"};if(dialog.ShowDialog()==true)TargetPath.Text=dialog.FolderName;}
    private void CancelClick(object s,RoutedEventArgs e)=>Close();
    private void LogClick(object s,RoutedEventArgs e){string log=Path.Combine(TargetPath.Text,"Data","Logs","setup.log");if(File.Exists(log))Process.Start(new ProcessStartInfo(log){UseShellExecute=true});}
    private async void PrimaryClick(object s,RoutedEventArgs e)
    {
        if(done){if(!uninstall)Process.Start(new ProcessStartInfo(Path.Combine(TargetPath.Text,"DivineLabs.IdleSlayer.exe")){UseShellExecute=true});Close();return;}
        if(busy)return;busy=true;Primary.IsEnabled=Cancel.IsEnabled=Browse.IsEnabled=TargetPath.IsEnabled=Shortcuts.IsEnabled=false;Progress.Visibility=Visibility.Visible;Progress.IsIndeterminate=true;Log.Visibility=Visibility.Collapsed;
        report=Path.Combine(AppContext.BaseDirectory,"operation-"+Guid.NewGuid().ToString("N")+".txt");
        try{
            string target=TargetPath.Text;bool shortcuts=Shortcuts.IsChecked==true;
            var start=new ProcessStartInfo(worker){UseShellExecute=false,CreateNoWindow=true};
            foreach(string arg in new[]{"--silent","--no-launch","--dir",target,"--report",report})start.ArgumentList.Add(arg);
            if(uninstall)start.ArgumentList.Add("--uninstall-now");else if(!shortcuts)start.ArgumentList.Add("--no-shortcuts");
            using var process=Process.Start(start)??throw new IOException("Не удалось запустить обслуживание.");
            while(!process.HasExited){
                if(File.Exists(report)){try{var lines=await File.ReadAllLinesAsync(report);if(lines.Length>=2){Status.Text=lines[1];if(int.TryParse(lines[0],out int percent)){Progress.IsIndeterminate=percent<0;if(percent>=0)Progress.Value=percent;}}}catch(IOException){}}
                await Task.Delay(150);
            }
            await process.WaitForExitAsync();
            if(process.ExitCode!=0)throw new IOException("Операция не завершена. Исправьте причину из журнала и повторите.");
            done=true;Heading.Text=uninstall?"DivineLabs удалён.":"Всё готово.";
            Status.Text=uninstall?"Программа удалена. Настройки и резервные копии сохранены.":"Можно запускать игру через DivineLabs.";
            Primary.Content=uninstall?"Готово":"Запустить →";Cancel.Visibility=Visibility.Collapsed;Progress.IsIndeterminate=false;Progress.Value=100;
        }catch(Exception error){Status.Text=error.Message;Log.Visibility=Visibility.Visible;Primary.Content="Повторить";Progress.Visibility=Visibility.Collapsed;}
        finally{busy=false;Primary.IsEnabled=Cancel.IsEnabled=true;Browse.IsEnabled=TargetPath.IsEnabled=Shortcuts.IsEnabled=!done&&!uninstall;}
    }
}