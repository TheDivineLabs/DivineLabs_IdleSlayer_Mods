using System.Text.Json;
using System.Security.Cryptography;
namespace DivineLabs.IdleSlayer;
public sealed class Settings
{
    public int SchemaVersion { get; set; } = 1;
    public bool Enabled { get; set; } = true;
    public double CoinMultiplier { get; set; } = 100;
    public double DamageMultiplier { get; set; } = 1000;
    public float UiScale { get; set; } = 1;
    public void Validate()
    {
        if (SchemaVersion != 1) throw new InvalidDataException("Неизвестная версия настроек.");
        if (!Valid(CoinMultiplier) || !Valid(DamageMultiplier)) throw new InvalidDataException("Множители должны быть числами от 1 до 1 000 000.");
        if (!float.IsFinite(UiScale) || UiScale < 0.75f || UiScale > 1.5f) throw new InvalidDataException("Масштаб должен быть от 75% до 150%.");
    }
    public static bool Valid(double value) => double.IsFinite(value) && value >= 1 && value <= 1000000;
    public static double MultiplyIncome(double amount, double factor) => amount > 0 && double.IsFinite(amount) && Valid(factor) && double.IsFinite(amount * factor) ? amount * factor : amount;
}
public static class SettingsStore
{
    public const string FileName = "DivineLabs.settings.json";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static Settings Read(string path)
    {
        var value = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Пустые настройки.");
        value.Validate(); return value;
    }
    public static Settings Load(string path, Action<string> report = null)
    {
        if (File.Exists(path))
        {
            try { return Read(path); }
            catch (Exception e) when (e is JsonException or InvalidDataException or IOException)
            {
                // Never overwrite settings from a future version with an older schema.
                if (e is InvalidDataException && e.Message.Contains("версия")) throw;
                report?.Invoke("Настройки повреждены; используется резервная копия.");
                if (File.Exists(path + ".bak")) return Read(path + ".bak");
                throw new InvalidDataException("Настройки повреждены, исправной резервной копии нет. Файл сохранён без изменений.", e);
            }
        }
        string legacy = Path.Combine(Path.GetDirectoryName(path)!, "IdleSlayerQoL.json");
        var settings = File.Exists(legacy) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(legacy))! : new Settings();
        settings.Validate(); Save(path, settings); return settings;
    }
    public static void Save(string path, Settings settings)
    {
        settings.Validate(); Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var gate = new FileStream(path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json));
        if (File.Exists(path))
        {
            bool valid = false; try { Read(path); valid = true; } catch { }
            if (valid) File.Replace(temp, path, path + ".bak"); else File.Move(temp, path, true);
        }
        else File.Move(temp, path);
    }
}
public static class Compatibility
{
    public const string GameHash = "AB968489DF8032A895898D9B90EE3E583F416D4FBC97C427D935EFA457BFB38A";
    public const string Build = "25810322";
    public static bool Supported(string game)
    { using var sha = SHA256.Create(); using var stream = File.OpenRead(Path.Combine(game,"GameAssembly.dll")); return Convert.ToHexString(sha.ComputeHash(stream)) == GameHash; }
}
