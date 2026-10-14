using DivineLabs.IdleSlayer;
using DivineLabs.IdleSlayer.Launcher;
static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("ПРОЙДЕНО: "+name);}
var root=Path.Combine(@"W:\DEV\.cache\temp","DivineLabs-checks-"+Guid.NewGuid());Directory.CreateDirectory(root);
Check(Settings.MultiplyIncome(-200,100)==-200,"Покупка не умножает расход");
Check(10+Settings.MultiplyIncome(2,100)+Settings.MultiplyIncome(-200,100)==10,"Доход и последующая покупка сохраняют верный баланс");
Check(Settings.MultiplyIncome(double.MaxValue,100)==double.MaxValue,"Защита от переполнения");
Check(Settings.MultiplyIncome(25,1)==25,"Отключение через ×1");
Check(!Settings.Valid(double.NaN)&&!Settings.Valid(double.PositiveInfinity)&&!Settings.Valid(0),"Некорректные коэффициенты отклонены");
string file=Path.Combine(root,SettingsStore.FileName);string legacy=Path.Combine(root,"IdleSlayerQoL.json");File.WriteAllText(legacy,"{\"Enabled\":false,\"CoinMultiplier\":25,\"DamageMultiplier\":50}");
var s=SettingsStore.Load(file);Check(s.CoinMultiplier==25&&!s.Enabled&&File.Exists(legacy),"Миграция сохраняет значения и исходник");s.CoinMultiplier=30;SettingsStore.Save(file,s);Check(SettingsStore.Read(file).CoinMultiplier==30,"Атомарная запись новых значений");File.WriteAllText(file,"broken");Check(SettingsStore.Load(file).CoinMultiplier==25,"Восстановление из последней исправной копии");
using(var gate=new FileStream(file+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None)){bool rejected=false;try{SettingsStore.Save(file,s);}catch(IOException){rejected=true;}Check(rejected,"Вторая запись исключена блокировкой");}
File.WriteAllText(file,"{\"SchemaVersion\":2}");bool future=false;try{SettingsStore.Load(file);}catch(InvalidDataException){future=true;}Check(future&&File.ReadAllText(file).Contains(":2"),"Будущая схема не перезаписывается");
var fake=Path.Combine(root,"game");Directory.CreateDirectory(Path.Combine(fake,"Mods"));File.WriteAllText(Path.Combine(fake,"Mods","user.txt"),"preserve");File.WriteAllText(Path.Combine(fake,"version.dll"),"proxy");Installation.Disable(fake);Check(!Directory.Exists(Path.Combine(fake,"Mods")),"Обычный запуск убирает маркеры загрузчика");Installation.Enable(fake);Check(File.ReadAllText(Path.Combine(fake,"Mods","user.txt"))=="preserve","Повторное включение сохраняет пользовательские файлы");
Console.WriteLine("Все проверки пройдены. Временные данные: "+root);
