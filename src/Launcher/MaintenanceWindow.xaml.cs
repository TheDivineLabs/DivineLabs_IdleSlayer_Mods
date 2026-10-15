using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using DivineLabs.DesktopUI;
using DivineLabs.IdleSlayer.Maintenance;

namespace DivineLabs.IdleSlayer.Launcher;

public partial class MaintenanceWindow : Window
{
    private readonly string worker;
    private readonly bool uninstall;
    private bool busy, done;
    private int page;
    private sealed record LicenseDocument(string Title, string Text)
    { public override string ToString() => Title; }

    public MaintenanceWindow(string[] args)
    {
        string Arg(string name, string fallback = "")
        {
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        worker = Arg("--worker");
        uninstall = args.Contains("--uninstall");
        InitializeComponent();
        Style = (Style)FindResource(typeof(Window));
        Icon = new BitmapImage(new Uri("pack://application:,,,/DivineLabs.IdleSlayer.Launcher;component/DivineLabs.png"));
        Title = "DivineLabs Idle Slayer Mods";
        Motion.SetEnabled(this, SystemParameters.ClientAreaAnimation);
        TargetPath.Text = Arg("--dir");
        LoadLicenses();
        if (uninstall)
        {
            ModeLabel.Text = "УДАЛЕНИЕ";
            WelcomeTitle.Text = "До следующей игры";
            WelcomeText.Text = "Мастер удалит DivineLabs Idle Slayer Mods и отключит его игровые компоненты.";
            WelcomeDetail.Text = "Настройки, сохранения и резервные копии останутся";
            WelcomeNote.Text = "Перед удалением закройте игру.";
            OptionsTitle.Text = "Проверим перед удалением";
            OptionsDescription.Text = "Будет удалена программа из этой папки.";
            RetentionTitle.Text = "Ваш прогресс сохранится";
            Retention.Text = "Сохранения игры, настройки DivineLabs, резервные копии и посторонние файлы не удаляются.";
            Shortcuts.Visibility = Browse.Visibility = Visibility.Collapsed;
            TargetPath.IsReadOnly = true;
            ProgressTitle.Text = "Удаляем программу";
            ProgressDescription.Text = "Отключаем компоненты и удаляем установленные файлы.";
        }
        ShowPage(0, false);
        Closing += (_, e) =>
        {
            if (!busy) return;
            e.Cancel = true;
            InlineNotice.Text = "Сейчас меняются файлы. Дождитесь завершения операции.";
        };
        Closed += (_, _) =>
        {
            if (!uninstall) return;
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(worker)!, "maintenance.complete"), "DivineLabs"); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        };
        Loaded += (_, _) => Motion.Enter(this);
    }

    private static string Embedded(string name)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (stream == null) return "Текст лицензии недоступен. Проверьте комплект программы.";
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private void LoadLicenses()
    {
        var documents = new List<LicenseDocument>
        {
            new("DivineLabs — Apache License 2.0", Embedded("DivineLabs.License")),
            new("Компоненты и происхождение", Embedded("DivineLabs.Notices"))
        };
        foreach (string name in Assembly.GetExecutingAssembly().GetManifestResourceNames()
            .Where(n => n.StartsWith("DivineLabs.Licenses.", StringComparison.Ordinal)).OrderBy(n => n))
            documents.Add(new(name["DivineLabs.Licenses.".Length..].Replace(".txt", ""), Embedded(name)));
        LicenseSelector.ItemsSource = documents;
        LicenseSelector.SelectedIndex = 0;
    }

    private void LicenseChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LicenseText != null && LicenseSelector.SelectedItem is LicenseDocument document)
        { LicenseText.Text = document.Text; LicenseText.ScrollToHome(); }
    }

    private void AcceptanceChanged(object sender, RoutedEventArgs e)
    {
        if (Primary != null && page == 1) Primary.IsEnabled = Accepted.IsChecked == true;
    }

    private void ShowPage(int next, bool animate = true)
    {
        page = next;
        var pages = new FrameworkElement[] { WelcomePage, LicensePage, OptionsPage, ProgressPage, ResultPage };
        for (int index = 0; index < pages.Length; index++) pages[index].Visibility = index == page ? Visibility.Visible : Visibility.Collapsed;
        string[] titles = uninstall ? new[] { "Начало", "Параметры", "Удаление", "Готово" } :
            new[] { "Начало", "Лицензии", "Параметры", "Установка", "Готово" };
        int current = uninstall && page > 0 ? page - 1 : page;
        var accent = (Brush)FindResource("Accent");
        Steps.ItemsSource = titles.Select((title, index) => new
        {
            Title = title,
            Number = index < current ? "✓" : (index + 1).ToString(),
            Background = index <= current ? accent : Brushes.Transparent,
            Border = index <= current ? accent : (Brush)FindResource("Line"),
            Foreground = index <= current ? Brushes.White : (Brush)FindResource("Muted"),
            Label = index == current ? (Brush)FindResource("Text") : (Brush)FindResource("Muted")
        }).ToArray();
        Back.Visibility = page is 1 or 2 || page == 3 && !busy ? Visibility.Visible : Visibility.Collapsed;
        Cancel.Visibility = done ? Visibility.Collapsed : Visibility.Visible;
        Cancel.IsEnabled = !busy;
        Back.IsEnabled = !busy;
        Primary.IsEnabled = !busy && (page != 1 || Accepted.IsChecked == true);
        Primary.Content = done ? uninstall ? "Готово" : "Запустить →" :
            page == 2 ? uninstall ? "Удалить" : Existing ? "Обновить →" : "Установить →" :
            page == 3 ? "Повторить" : "Далее →";
        if (animate) Motion.Enter(pages[page]);
    }

    private bool Existing => File.Exists(Path.Combine(TargetPath.Text, "DivineLabs.IdleSlayer.exe"));
    private void PathChanged(object sender, TextChangedEventArgs e)
    {
        if (PathHint == null) return;
        PathHint.Text = uninstall ? "Удаляются только файлы из списка установки." :
            Existing ? "Найдена установка. Мастер обновит программу." : "Программа будет установлена в эту папку.";
        if (Primary != null && page == 2) Primary.Content = uninstall ? "Удалить" : Existing ? "Обновить →" : "Установить →";
    }

    private void BrowseClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "DivineLabs Idle Slayer Mods — папка программы" };
        if (dialog.ShowDialog() == true) TargetPath.Text = dialog.FolderName;
    }

    private void BackClick(object sender, RoutedEventArgs e)
    {
        InlineNotice.Text = "";
        ShowPage(page == 3 ? 2 : page == 2 && uninstall ? 0 : page - 1);
    }
    private void CancelClick(object sender, RoutedEventArgs e) => Close();

    private void LogClick(object sender, RoutedEventArgs e)
    {
        string log = Path.Combine(TargetPath.Text, "Data", "Logs", "setup.log");
        if (File.Exists(log)) Process.Start(new ProcessStartInfo(log) { UseShellExecute = true });
        else InlineNotice.Text = "Журнал ещё не создан. Проверьте папку установки и доступ к ней.";
    }

    private async void PrimaryClick(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        if (done)
        {
            if (!uninstall)
            {
                try { Process.Start(new ProcessStartInfo(Path.Combine(TargetPath.Text, "DivineLabs.IdleSlayer.exe")) { UseShellExecute = true }); }
                catch (Exception error) { InlineNotice.Text = "Не удалось открыть программу: " + error.Message; return; }
            }
            Close();
            return;
        }
        if (page == 0) { ShowPage(uninstall ? 2 : 1); return; }
        if (page == 1) { if (Accepted.IsChecked == true) ShowPage(2); return; }
        if (string.IsNullOrWhiteSpace(TargetPath.Text))
        { InlineNotice.Text = "Выберите папку программы."; return; }
        string target;
        try { target = Path.GetFullPath(TargetPath.Text); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        { InlineNotice.Text = "Укажите корректный путь к папке программы."; return; }

        busy = true;
        InlineNotice.Text = "";
        ErrorPanel.Visibility = Visibility.Collapsed;
        Progress.Visibility = Visibility.Visible;
        Progress.IsIndeterminate = true;
        Percent.Text = "";
        Status.Text = "Подготовка…";
        ShowPage(3);
        string report = Path.Combine(AppContext.BaseDirectory, "operation-" + Guid.NewGuid().ToString("N") + ".txt");
        try
        {
            var start = new ProcessStartInfo(worker) { UseShellExecute = false, CreateNoWindow = true };
            foreach (string argument in new[] { "--silent", "--no-launch", "--dir", target, "--report", report }) start.ArgumentList.Add(argument);
            if (uninstall) start.ArgumentList.Add("--uninstall-now");
            else if (Shortcuts.IsChecked != true) start.ArgumentList.Add("--no-shortcuts");
            using var process = Process.Start(start) ?? throw new IOException("Не удалось запустить обслуживание.");
            while (!process.HasExited)
            {
                var snapshot = await Task.Run(() =>
                {
                    bool read = OperationReport.TryRead(report, out int percent, out string message);
                    return (read, percent, message);
                });
                if (snapshot.read)
                {
                    Status.Text = snapshot.message;
                    Progress.IsIndeterminate = snapshot.percent < 0;
                    if (snapshot.percent >= 0) { Progress.Value = snapshot.percent; Percent.Text = snapshot.percent + "%"; }
                }
                await Task.Delay(150);
            }
            await process.WaitForExitAsync();
            if (process.ExitCode != 0)
                throw new IOException("Закройте игру и лаунчер. Проверьте свободное место и выбранную папку. Затем повторите операцию; точная причина есть в журнале.");
            done = true;
            ResultTitle.Text = uninstall ? "Программа удалена" : "Всё готово к запуску";
            ResultDescription.Text = uninstall ? "DivineLabs Idle Slayer Mods удалён. Настройки, сохранения и резервные копии сохранены." :
                "DivineLabs Idle Slayer Mods установлен. Откройте лаунчер и начните игру.";
            ResultPath.Text = uninstall ? "Сохранённые данные: " + Path.Combine(target, "Data") : target;
        }
        catch (Exception error)
        {
            Status.Text = "Операция не завершена";
            Progress.Visibility = Visibility.Collapsed;
            Percent.Text = "";
            ErrorText.Text = error.Message;
            ErrorPanel.Visibility = Visibility.Visible;
        }
        finally
        {
            busy = false;
            ShowPage(done ? 4 : 3);
        }
    }
}
