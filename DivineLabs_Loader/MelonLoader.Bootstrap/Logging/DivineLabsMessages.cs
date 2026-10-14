namespace MelonLoader.Bootstrap.Logging;
// Console localization only. Original messages remain intact in diagnostic files.
internal static class DivineLabsMessages
{
    private static readonly (string From,string To)[] Phrases = {
        ("Preferences Loaded!","Настройки загружены."),("Preferences Saved!","Настройки сохранены."),
        ("Loading Mods...","Загрузка модулей..."),("Loading Plugins...","Загрузка расширений..."),("Loading UserLibs...","Загрузка библиотек..."),
        ("Loading Il2CppAssemblyGenerator...","Подготовка связки с Unity..."),("Contacting RemoteAPI...","Проверка метаданных совместимости..."),
        ("Assembly Generation Successful!","Подготовка сборок завершена."),("Assembly Generation Needed!","Требуется подготовка сборок."),
        ("Assembly is up to date. No Generation Needed.","Сборки актуальны. Повторная подготовка не требуется."),
        ("Checking GameAssembly...","Проверка сборки игры..."),("Executing Cpp2IL...","Анализ IL2CPP..."),
        ("Game Name: ","Игра: "),("Game Developer: ","Разработчик игры: "),("Unity Version: ","Версия Unity: "),
        ("Game Version: ","Версия игры: "),("Game Type: ","Тип игры: "),("Game Arch: ","Архитектура игры: "),
        ("Runtime Type: ","Среда исполнения: "),("Command-Line: ","Параметры запуска: "),("Hash Code: ","Контрольная сумма: "),
        ("Melon Assembly loaded: ","Загружен модуль: "),("Support Module Loaded: ","Загружен модуль совместимости: "),
        ("SHA256 Hash: ","SHA256: "),("Using HostFXR Path: ","HostFXR: "),("Using .NET runtime: ","Среда .NET: "),
        ("Using Cpp2IL Version: ","Версия Cpp2IL: "),("Using Il2CppInterop Version = ","Версия Il2CppInterop: "),
        ("Using Unity Dependencies Version = ","Версия библиотек Unity: "),("Downloading ","Загрузка: "),
        ("Processing ","Подготовка: "),(" is up to date."," актуален."),(" Mods loaded."," модулей загружено."),
        (" Mod loaded."," модуль загружен."),(" Plugins loaded."," расширений загружено."),(" UserLibs loaded."," библиотек загружено."),
        ("MelonLoader v","DivineLabs Loader • "),("Open-Beta","адаптированная сборка"),
        ("[WARNING]","[ПРЕДУПРЕЖДЕНИЕ]"),("[ERROR]","[ОШИБКА]")
    };
    internal static string Translate(string message)
    {
        if(string.IsNullOrWhiteSpace(message)) return message;
        foreach(char c in message) if(c>='А'&&c<='я'||c=='ё'||c=='Ё') return message;
        string result=message;bool translated=false;
        foreach(var pair in Phrases)if(result.Contains(pair.From,StringComparison.Ordinal)){result=result.Replace(pair.From,pair.To,StringComparison.Ordinal);translated=true;}
        if(translated)return result;
        if(!message.Any(char.IsLetter))return message;
        if(message.StartsWith("OS:"))return message.Replace("OS:","ОС:");
        return "Техническое сообщение компонента — подробности в журнале.";
    }
}
