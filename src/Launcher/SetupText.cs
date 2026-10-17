using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Text.Json;

namespace DivineLabs.IdleSlayer.Launcher;

// Interface copy is separate from the original dependency license documents.
public sealed partial class SetupText : INotifyPropertyChanged
{
    static SetupText(){foreach(var pair in ExtraCopy())Copy.Add(pair.Key,pair.Value);}
    public static SetupText Current { get; } = new();
    public event PropertyChangedEventHandler PropertyChanged;
    public string Selection { get; private set; } = "auto";
    public static readonly string[] Languages={"ru","en","uk","be","de","es","ko","zh","ja"};
    public static string ResolveLanguage(string language)=>Languages.Contains(language)?language:"en";
    public string EffectiveLanguage=>ResolveLanguage(Selection=="auto"?CultureInfo.CurrentUICulture.TwoLetterISOLanguageName:Selection);
    public string this[string key] => Copy.TryGetValue(key,out var value)?EffectiveLanguage=="ru"?value.Ru:EffectiveLanguage=="en"?value.En:Translations[EffectiveLanguage].GetValueOrDefault(key,value.En):key;
    public string Translate(string original)
    {var key=Copy.FirstOrDefault(pair=>pair.Value.Ru==original).Key;return key==null?original:this[key];}
    public static IEnumerable<string> Keys=>Copy.Keys;
    public static bool HasTranslation(string language,string key)=>language is "ru" or "en"?Copy.ContainsKey(key):Translations.TryGetValue(language,out var values)&&values.ContainsKey(key);
    private static readonly Dictionary<string,Dictionary<string,string>> Translations=LoadTranslations();
    private static Dictionary<string,Dictionary<string,string>> LoadTranslations()
    {
        var result=new Dictionary<string,Dictionary<string,string>>();
        foreach(string language in Languages.Where(language=>language is not ("ru" or "en"))){
            using var stream=typeof(SetupText).Assembly.GetManifestResourceStream("DivineLabs.Locales."+language+".json")??throw new InvalidDataException("Missing locale: "+language);
            result[language]=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)??throw new InvalidDataException("Empty locale: "+language);
        }
        return result;
    }
    public void Select(string language)
    {
        if (language!="auto"&&!Languages.Contains(language)) return;
        Selection = language;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
    public string Progress(string original)
    {
        return Translate(original);
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
        ["Accept"] = ("Я принимаю условия MIT DivineLabs и ознакомился с лицензиями компонентов.", "I accept the DivineLabs MIT license and have reviewed the component licenses."),
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
        ["Theme"]=("Тема оформления","Appearance"),["ThemeSystem"]=("Системная","System"),["ThemeLight"]=("Светлая","Light"),["ThemeDark"]=("Тёмная","Dark"),
        ["Unpacking"]=("Распаковка программы…","Unpacking the app…"),["BackingUp"]=("Создание резервной копии и установка файлов…","Backing up and installing files…"),
        ["VerifyingPackage"]=("Проверка установленного комплекта…","Verifying the installed package…"),["Integrations"]=("Создание ярлыков и регистрация…","Creating shortcuts and registering the app…"),
        ["InstallComplete"]=("DivineLabs установлен. Всё готово к запуску.","DivineLabs is installed and ready to launch."),["RemovingApp"]=("Удаление файлов программы…","Removing app files…"),
        ["VerifyRemoval"]=("Проверка удаления…","Verifying removal…"),["DisablingApp"]=("Отключение компонентов и удаление программы…","Disabling components and removing the app…"),
        ["Components"]=("Сведения о компонентах","Component information"),
        ["Clients"]=("Приложения DivineLabs","DivineLabs apps"),["ClientsBody"]=("Найдите установленные приложения DivineLabs. Поиск не меняет и не удаляет их файлы.","Find installed DivineLabs apps. Discovery does not change or remove their files."),
        ["Discover"]=("Найти приложения","Find apps"),["ScanFolder"]=("Проверить папку","Scan folder"),["OpenFolder"]=("Открыть папку","Open folder"),
        ["Discovering"]=("Ищем установленные приложения…","Looking for installed apps…"),["NoClients"]=("Приложения не найдены. Для portable выберите папку с приложением или папку DivineLabs.","No apps found. For portable apps, select the app folder or your DivineLabs folder."),
        ["DiscoveryFailed"]=("Не удалось выполнить поиск. Проверьте доступ к папке и повторите.","Discovery failed. Check folder access and retry."),["AppsFound"]=("Найдено приложений: {0}","Apps found: {0}"),
        ["AppFolder"]=("Папка приложений DivineLabs","DivineLabs apps folder"),["CurrentApp"]=("Текущее приложение","Current app"),["InstalledApp"]=("Установлено","Installed"),["PortableApp"]=("Portable","Portable"),
        ["SomeLocationsUnavailable"]=("Некоторые расположения недоступны для чтения.","Some locations could not be read."),
    };
}

[MarkupExtensionReturnType(typeof(string))]
public sealed class TrExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{key}]") { Source = SetupText.Current, Mode = BindingMode.OneWay }.ProvideValue(serviceProvider);
}
