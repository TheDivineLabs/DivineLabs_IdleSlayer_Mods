using System.Text.Json;
using System.Security.Cryptography;
namespace DivineLabs.IdleSlayer;
public sealed class Settings
{
    public int SchemaVersion {get;set;}=2;
    public long Revision {get;set;}
    public bool Enabled {get;set;}=true;
    public bool CoinsEnabled {get;set;}=true;
    public bool DamageEnabled {get;set;}=true;
    public double CoinMultiplier {get;set;}=100;
    public double DamageMultiplier {get;set;}=1000;
    public float UiScale {get;set;}=1;
    public bool ReduceMotion {get;set;}
    public bool WidgetVisible {get;set;}=true;
    public float WidgetOpacity {get;set;}=.85f;
    public float WidgetX {get;set;}=1;
    public float WidgetY {get;set;}
    public string[] Favorites {get;set;}=Array.Empty<string>();
    public Dictionary<string,KeyBinding> Bindings {get;set;}=FeatureCatalog.Actions.ToDictionary(a=>a,a=>new KeyBinding{Key=a=="menu"?45:0});
    public Settings Copy()=>JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if(SchemaVersion!=2)throw new InvalidDataException("Неизвестная версия настроек.");
        if(Revision<0)throw new InvalidDataException("Некорректная ревизия настроек.");
        if(!Valid(CoinMultiplier)||!Valid(DamageMultiplier))throw new InvalidDataException("Множители должны быть числами от 1 до 1 000 000.");
        if(!float.IsFinite(UiScale)||UiScale<.75f||UiScale>1.5f)throw new InvalidDataException("Масштаб должен быть от 75% до 150%.");
        if(!float.IsFinite(WidgetOpacity)||WidgetOpacity<.2f||WidgetOpacity>1||!float.IsFinite(WidgetX)||WidgetX<0||WidgetX>1||!float.IsFinite(WidgetY)||WidgetY<0||WidgetY>1)
            throw new InvalidDataException("Некорректные параметры виджета.");
        if(Favorites==null||Favorites.Distinct().Count()!=Favorites.Length||Favorites.Any(f=>!FeatureCatalog.All.Any(d=>d.Id==f)))throw new InvalidDataException("Неизвестная избранная функция.");
        if(Bindings==null||Bindings.Count!=FeatureCatalog.Actions.Length||FeatureCatalog.Actions.Any(a=>!Bindings.ContainsKey(a)))throw new InvalidDataException("Неполный список клавиш.");
        foreach(var key in Bindings.Values){if(key==null)throw new InvalidDataException("Некорректная клавиша.");key.Validate();}
        if(Bindings["menu"].Key==0)throw new InvalidDataException("Для меню нужна клавиша.");
        if(Bindings.Values.Where(b=>b.Key!=0).GroupBy(b=>(b.Key,b.Modifiers)).Any(g=>g.Count()>1))throw new InvalidDataException("Сочетание уже назначено другому действию.");
    }
    public static bool Valid(double v)=>double.IsFinite(v)&&v>=1&&v<=1000000;
    public static double MultiplyIncome(double amount,double factor)=>amount>0&&double.IsFinite(amount)&&Valid(factor)&&double.IsFinite(amount*factor)?amount*factor:amount;
}
public static class SettingsStore
{
    public const string FileName="DivineLabs.settings.json";
    private static readonly JsonSerializerOptions Json=new(){WriteIndented=true};
    public static Settings Read(string path)
    {
        string text=File.ReadAllText(path);
        using var doc=JsonDocument.Parse(text);
        int schema=doc.RootElement.TryGetProperty("SchemaVersion",out var v)?v.GetInt32():1;
        if(schema is not (1 or 2))throw new InvalidDataException("Неизвестная версия настроек.");
        var value=JsonSerializer.Deserialize<Settings>(text)??throw new InvalidDataException("Пустые настройки.");
        value.SchemaVersion=2;value.Validate();return value;
    }
    public static Settings Load(string path,Action<string> report=null)
    {
        if(File.Exists(path)){
            try{
                var s=Read(path);
                using var doc=JsonDocument.Parse(File.ReadAllText(path));
                if(!doc.RootElement.TryGetProperty("SchemaVersion",out var v)||v.GetInt32()==1){if(!File.Exists(path+".v1.bak"))File.Copy(path,path+".v1.bak");Save(path,s);}
                return s;
            }catch(Exception e) when(e is JsonException or InvalidDataException or IOException){
                if(e is InvalidDataException&&e.Message.Contains("версия"))throw;
                report?.Invoke("Настройки повреждены; используется резервная копия.");
                if(File.Exists(path+".bak"))return Read(path+".bak");
                throw new InvalidDataException("Настройки повреждены, исправной резервной копии нет. Файл сохранён без изменений.",e);
            }
        }
        string legacy=Path.Combine(Path.GetDirectoryName(path)!, "IdleSlayerQoL.json");
        var settings=File.Exists(legacy)?Read(legacy):new Settings();Save(path,settings);return settings;
    }

    public static FileStream AcquireOwner(string path){Directory.CreateDirectory(Path.GetDirectoryName(path)!);return new FileStream(path+".owner",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
    public static void Save(string path,Settings settings)
    {
        settings.Validate();Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var gate=new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
        string temp=path+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(settings,Json));
        if(File.Exists(path)){
            bool valid=false;try{Read(path);valid=true;}catch{}
            if(valid)File.Replace(temp,path,path+".bak");else File.Move(temp,path,true);
        }else File.Move(temp,path);
    }
}
public static class Compatibility
{
    public const string GameHash="AB968489DF8032A895898D9B90EE3E583F416D4FBC97C427D935EFA457BFB38A";
    public const string Build="25810322";
    public static bool Supported(string game){using var sha=SHA256.Create();using var stream=File.OpenRead(Path.Combine(game,"GameAssembly.dll"));return Convert.ToHexString(sha.ComputeHash(stream))==GameHash;}
}