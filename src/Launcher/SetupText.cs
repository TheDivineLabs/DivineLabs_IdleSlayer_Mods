using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;

namespace DivineLabs.IdleSlayer.Launcher;

// Interface copy is separate from the original dependency license documents.
public sealed class SetupText : INotifyPropertyChanged
{
    public static SetupText Current { get; } = new();
    public event PropertyChangedEventHandler PropertyChanged;
    public string Selection { get; private set; } = "auto";
    public bool English => (Selection == "auto" ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : Selection) != "ru";
    public string this[string key] => Copy.TryGetValue(key, out var value) ? English ? value.En : value.Ru : key;
    public void Select(string language)
    {
        if (language is not ("auto" or "ru" or "en")) return;
        Selection = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
    public string Progress(string original)
    {
        if(!English)return original;
        return original switch {
            "Распаковка программы…"=>"Unpacking the app…",
            "Создание резервной копии и установка файлов…"=>"Backing up and installing files…",
            "Проверка установленного комплекта…"=>"Verifying the installed package…",
            "Создание ярлыков и регистрация…"=>"Creating shortcuts and registering the app…",
            "DivineLabs установлен. Всё готово к запуску."=>"DivineLabs is installed and ready to launch.",
            "Удаление файлов программы…"=>"Removing app files…",
            "Проверка удаления…"=>"Verifying removal…",
            "Отключение компонентов и удаление программы…"=>"Disabling components and removing the app…",
            "Программа удалена."=>"The app has been removed.",
            "Подготовка…"=>"Preparing…",_=>original};
    }
    private static readonly Dictionary<string, (string Ru, string En)> Copy = new()
    {
        ["Start"] = ("Начало", "Start"), ["Licenses"] = ("Лицензии", "Licenses"),
        ["Options"] = ("Параметры", "Options"), ["Installation"] = ("Установка", "Installation"),
        ["Removal"] = ("Удаление", "Removal"), ["Done"] = ("Готово", "Finish"),
        ["Welcome"] = ("Добро пожаловать", "Welcome"), ["Pace"] = ("Играйте в своём темпе", "Play at your own pace"),
        ["WelcomeBody"] = ("Лаунчер, игровые функции и меню — в одном комплекте.", "Launcher, game features and menu — in one package."),
        ["More"] = ("Больше возможностей\nдля вашего стиля игры.", "More possibilities\nfor your play style."),
        ["Flexible"] = ("Гибкая настройка\nпод ваши предпочтения.", "Flexible settings\ntailored to you."),
        ["Discord"] = ("Сообщество Discord", "Discord community"), ["Telegram"] = ("Связаться в Telegram", "Contact on Telegram"),
        ["Team"] = ("Разработано командой DivineLabs", "Made by the DivineLabs team"),
        ["LicenseTitle"] = ("Лицензии и компоненты", "Licenses and components"),
        ["LicenseBody"] = ("Перед установкой ознакомьтесь с условиями использования.", "Review the terms before installing."),
        ["Loader"] = ("Загрузчик", "Loader"), ["Dependencies"] = ("Зависимости", "Dependencies"),
        ["Accept"] = ("Я принимаю условия Apache 2.0 и ознакомился с лицензиями компонентов.", "I accept Apache 2.0 and have reviewed the component licenses."),
        ["AcceptFirst"] = ("Примите условия", "Accept the terms"),
        ["AcceptHint"] = ("Отметьте принятие условий под текстом лицензии.", "Accept the terms below the license text to continue."),
        ["OptionsTitle"] = ("Куда установить программу?", "Where should we install the app?"),
        ["OptionsBody"] = ("Выберите папку для DivineLabs Idle Slayer Mods.", "Choose a folder for DivineLabs Idle Slayer Mods."),
        ["Browse"] = ("Изменить", "Browse"),
        ["DesktopShortcut"] = ("Создать ярлык на рабочем столе", "Create a desktop shortcut"),
        ["DesktopHint"] = ("Быстрый доступ к программе", "Quick access to the app"),
        ["MenuShortcut"] = ("Добавить в меню «Пуск»", "Add to the Start menu"),
        ["MenuHint"] = ("Находить программу среди приложений Windows", "Find the app among your Windows applications"),
        ["Space"] = ("Размер комплекта будет проверен перед установкой.", "Package size will be checked before installation."),
        ["SpaceBytes"] = ("Для комплекта потребуется около {0} МБ свободного места.", "The package needs approximately {0} MB of free space."),
        ["Installing"] = ("Устанавливаем программу", "Installing the app"),
        ["InstallBody"] = ("Копируем и проверяем файлы выбранного комплекта.", "Copying and verifying the package files."),
        ["Prepare"] = ("Подготовка", "Preparation"), ["Files"] = ("Установка файлов", "Installing files"),
        ["Verify"] = ("Проверка", "Verification"), ["Working"] = ("Выполняется…", "In progress…"),
        ["Waiting"] = ("Ожидание", "Waiting"), ["Complete"] = ("Готово  ✓", "Done  ✓"),
        ["Preparing"] = ("Подготовка…", "Preparing…"), ["Busy"] = ("Установка…", "Installing…"),
        ["ResultTitle"] = ("Всё готово к запуску", "Ready to play"),
        ["ResultBody"] = ("Программа установлена. Запустите сейчас или откройте позже.", "The app is installed. Launch it now or open it later."),
        ["Launch"] = ("Запустить →", "Launch →"), ["Back"] = ("Назад", "Back"), ["Cancel"] = ("Отмена", "Cancel"),
        ["Next"] = ("Далее →", "Next →"), ["Install"] = ("Установить →", "Install →"),
        ["Update"] = ("Обновить →", "Update →"), ["Remove"] = ("Удалить", "Uninstall"), ["Retry"] = ("Повторить", "Retry"),
        ["Failed"] = ("Не удалось завершить операцию", "The operation could not be completed"),
        ["NotFinished"] = ("Операция не завершена", "Operation not completed"), ["Log"] = ("Открыть журнал", "Open log"),
        ["LogMissing"] = ("Журнал ещё не создан. Проверьте выбранную папку.", "No log has been created yet. Check the selected folder."),
        ["CheckFailed"] = ("Не удалось проверить папку. Выберите другое расположение.", "Could not check this folder. Choose another location."),
        ["Installed"] = ("Найдена установка. Настройки сохранятся.", "An installation was found. Settings will be preserved."),
        ["Recoverable"] = ("Найдена незавершённая установка. Мастер восстановит её.", "An incomplete installation was found. Setup will recover it."),
        ["Occupied"] = ("В папке есть посторонние файлы. Выберите отдельную папку программы.", "This folder contains other files. Choose a separate app folder."),
        ["Empty"] = ("Программа будет установлена в эту папку.", "The app will be installed in this folder."),
        ["InvalidPath"] = ("Выберите корректную папку программы.", "Choose a valid app folder."),
        ["FolderTitle"] = ("DivineLabs Idle Slayer Mods — папка программы", "DivineLabs Idle Slayer Mods — app folder"),
        ["CloseBlocked"] = ("Сейчас меняются файлы. Дождитесь завершения операции.", "Files are being changed. Please wait for the operation to finish."),
        ["CancelTitle"] = ("Прервать установку?", "Cancel setup?"),
        ["CancelBody"] = ("Закрыть мастер установки? Вы сможете вернуться к настройке позже.", "Close the setup wizard? You can return to setup later."),
        ["Continue"] = ("Продолжить", "Continue"), ["Exit"] = ("Выйти", "Exit"),
        ["RemoveWelcome"] = ("До следующей игры", "Until the next game"),
        ["RemovePace"] = ("Удалим программу. Ваш прогресс останется.", "Remove the app and keep your progress."),
        ["RemoveMore"] = ("Настройки и резервные копии сохранятся.", "Settings and backups will be kept."),
        ["RemoveFlexible"] = ("Перед удалением закройте игру.", "Close the game before uninstalling."),
        ["RemoveOptions"] = ("Проверим перед удалением", "Review before uninstalling"),
        ["RemoveFolder"] = ("Будет удалена программа из этой папки.", "The app will be removed from this folder."),
        ["RetentionTitle"] = ("Ваш прогресс сохранится", "Your progress will be kept"),
        ["Retention"] = ("Сохранения игры, настройки, резервные копии и посторонние файлы не удаляются.", "Game saves, settings, backups and unrelated files will be kept."),
        ["RemoveOnly"] = ("Удаляются только файлы из списка установки.", "Only files in the installation manifest will be removed."),
        ["Removing"] = ("Удаляем программу", "Uninstalling the app"), ["RemovingFiles"] = ("Удаление файлов", "Removing files"),
        ["RemoveProgress"] = ("Отключаем компоненты и удаляем установленные файлы.", "Disabling components and removing installed files."),
        ["Removed"] = ("Программа удалена", "The app has been removed"),
        ["RemovedBody"] = ("Настройки, сохранения и резервные копии сохранены.", "Settings, game saves and backups have been kept."),
        ["RetainedPath"] = ("Сохранённые данные: ", "Retained data: "),
        ["CancelRemove"] = ("Отменить удаление?", "Cancel uninstall?"),
        ["CancelRemoveBody"] = ("Удаление не завершено. Закрыть мастер?", "Uninstalling has not completed. Close the wizard?"),
        ["LaunchFailed"] = ("Не удалось открыть программу: ", "Could not open the app: "),
        ["WorkerFailed"] = ("Не удалось запустить обслуживание.", "Could not start maintenance."),
        ["OperationFailed"] = ("Не удалось завершить операцию. Откройте журнал и повторите попытку.", "The operation failed. Open the log and retry."),
        ["Language"] = ("ЯЗЫК ИНТЕРФЕЙСА", "INTERFACE LANGUAGE"),
        ["Automatic"] = ("Автоматически", "Automatic"), ["Recommended"] = ("Рекомендуется · ", "Recommended · "),
        ["Minimize"] = ("Свернуть", "Minimize"), ["Maximize"] = ("Развернуть", "Maximize"), ["Close"] = ("Закрыть", "Close"),
    };
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{key}]") { Source = SetupText.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
