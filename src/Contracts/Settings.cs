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
    public bool SoulsEnabled {get;set;}
    public bool FreeCoins {get;set;}
    public bool FreeSP {get;set;}
    public bool FreeDP {get;set;}
    public bool FreeUSP {get;set;}
    public bool FreeCraft {get;set;}
    public double CoinMultiplier {get;set;}=100;
    public double DamageMultiplier {get;set;}=1000;
    public double SoulMultiplier {get;set;}=100;
    public float UiScale {get;set;}=1;
    public bool ReduceMotion {get;set;}
    public bool WidgetVisible {get;set;}=true;
    public bool ActiveListVisible {get;set;}=true;
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
        if(!Valid(CoinMultiplier)||!Valid(DamageMultiplier)||!Valid(SoulMultiplier))throw new InvalidDataException(MultiplierHint);
        if(!float.IsFinite(UiScale)||UiScale<.75f||UiScale>1.5f)throw new InvalidDataException("Масштаб должен быть от 75% до 150%.");
        if(!float.IsFinite(WidgetOpacity)||WidgetOpacity<.2f||WidgetOpacity>1||!float.IsFinite(WidgetX)||WidgetX<0||WidgetX>1||!float.IsFinite(WidgetY)||WidgetY<0||WidgetY>1)
            throw new InvalidDataException("Некорректные параметры виджета.");
        if(Favorites==null||Favorites.Distinct().Count()!=Favorites.Length||Favorites.Any(f=>!FeatureCatalog.All.Any(d=>d.Id==f)))throw new InvalidDataException("Неизвестная избранная функция.");
        if(Bindings==null||Bindings.Count!=FeatureCatalog.Actions.Length||FeatureCatalog.Actions.Any(a=>!Bindings.ContainsKey(a)))throw new InvalidDataException("Неполный список клавиш.");
        foreach(var key in Bindings.Values){if(key==null)throw new InvalidDataException("Некорректная клавиша.");key.Validate();}
        if(Bindings["menu"].Key==0)throw new InvalidDataException("Для меню нужна клавиша.");
        if(Bindings.Values.Where(b=>b.Key!=0).GroupBy(b=>(b.Key,b.Modifiers)).Any(g=>g.Count()>1))throw new InvalidDataException("Сочетание уже назначено другому действию.");
    }
    public const string MultiplierHint="Введите конечное число от 1, например 1000 или 1e60.";
    public bool FeatureEnabled(string id)=>id switch{"coins"=>CoinsEnabled,"damage"=>DamageEnabled,"souls"=>SoulsEnabled,"free-coins"=>FreeCoins,"free-sp"=>FreeSP,"free-dp"=>FreeDP,"free-usp"=>FreeUSP,"free-craft"=>FreeCraft,_=>throw new ArgumentException("Неизвестная функция.",nameof(id))};
    public double Multiplier(string id)=>id switch{"coins"=>CoinMultiplier,"damage"=>DamageMultiplier,"souls"=>SoulMultiplier,_=>throw new ArgumentException("Неизвестная функция.",nameof(id))};
    public void SetFeatureEnabled(string id,bool value){switch(id){case "coins":CoinsEnabled=value;break;case "damage":DamageEnabled=value;break;case "souls":SoulsEnabled=value;break;case "free-coins":FreeCoins=value;break;case "free-sp":FreeSP=value;break;case "free-dp":FreeDP=value;break;case "free-usp":FreeUSP=value;break;case "free-craft":FreeCraft=value;break;default:throw new ArgumentException("Неизвестная функция.",nameof(id));}}
    public void SetMultiplier(string id,double value){switch(id){case "coins":CoinMultiplier=value;break;case "damage":DamageMultiplier=value;break;case "souls":SoulMultiplier=value;break;default:throw new ArgumentException("Неизвестная функция.",nameof(id));}}
    public static bool Valid(double v)=>double.IsFinite(v)&&v>=1;
    public static double MultiplyIncome(double amount,double factor,double available=double.MaxValue)
    {
        if(amount<=0||!double.IsFinite(amount)||!Valid(factor))return amount;
        double result=amount*factor;
        return double.IsFinite(result)&&result<=available?result:amount;
    }
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
        // Extend the existing v2 binding map only for the newly introduced action.
        if(value.Bindings!=null)foreach(string action in new[]{"souls"}.Concat(FeatureCatalog.FreeIds))if(!value.Bindings.ContainsKey(action))value.Bindings.Add(action,new KeyBinding());
        value.SchemaVersion=2;value.Validate();return value;
    }
    public static Settings Load(string path,Action<string> report=null)
    {
        if(File.Exists(path)){
            try{
                var s=Read(path);
                using var doc=JsonDocument.Parse(File.ReadAllText(path));
                if(!doc.RootElement.TryGetProperty("SchemaVersion",out var v)||v.GetInt32()==1){if(!File.Exists(path+".v1.bak"))File.Copy(path,path+".v1.bak");Save(path,s);}
                else if(doc.RootElement.TryGetProperty("Bindings",out var bindings)&&bindings.ValueKind==JsonValueKind.Object&&!bindings.TryGetProperty("souls",out _)){
                    if(!File.Exists(path+".pre-souls.bak"))File.Copy(path,path+".pre-souls.bak");Save(path,s);
                }
                else if(doc.RootElement.TryGetProperty("Bindings",out var oldBindings)&&oldBindings.ValueKind==JsonValueKind.Object&&FeatureCatalog.FreeIds.Any(id=>!oldBindings.TryGetProperty(id,out _))){
                    if(!File.Exists(path+".pre-resources.bak"))File.Copy(path,path+".pre-resources.bak");Save(path,s);
                }
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