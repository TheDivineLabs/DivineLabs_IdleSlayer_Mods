using System.Collections.Concurrent;
using System.Globalization;
using UnityEngine;
namespace DivineLabs.IdleSlayer;
internal static class GameActions
{
    private sealed class Job
    {
        internal GameCommand Command;
        internal TaskCompletionSource<GameCommandResult> Reply=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationTokenRegistration Cancellation;
        internal DateTime Expires=DateTime.UtcNow.AddSeconds(8);
        internal Task Backup;
    }
    private static readonly ConcurrentQueue<Job> queue=new();
    private static Job current;
    internal static bool InGrant {get;private set;}
    internal static bool BackupReady {get;private set;}
    internal static Task<GameCommandResult> Submit(GameCommand command,CancellationToken cancel=default)
    {
        command.Validate();var job=new Job{Command=command};
        job.Cancellation=cancel.Register(()=>job.Reply.TrySetCanceled(cancel));queue.Enqueue(job);return job.Reply.Task;
    }
    internal static async Task PrepareSettings(Settings previous,Settings next,CancellationToken cancel)
    {
        if(next.Enabled&&FeatureCatalog.FreeIds.Any(id=>next.FeatureEnabled(id))&&(!previous.Enabled||FeatureCatalog.FreeIds.Any(id=>next.FeatureEnabled(id)&&!previous.FeatureEnabled(id)))){
            var result=await Submit(new(){Operation="backup"},cancel).ConfigureAwait(false);
            if(result.Error!=null)throw new IOException(result.Error);
        }
    }
    internal static void Tick()
    {
        if(current==null&&!queue.TryDequeue(out current))return;
        var job=current;
        if(job.Reply.Task.IsCompleted){Finish(null);return;}
        if(DateTime.UtcNow>job.Expires){Finish(new(){Error="Команда не выполнена: истекло время ожидания игрового потока."});return;}
        try{
            RequireReady();
            if(job.Command.Operation=="inspect"){Finish(new(){State=Inspect()});return;}
            if(job.Command.Operation=="grant")ValidateGrant(job.Command);
            if(job.Backup==null){
                Il2Cpp.SaveManager.instance.SaveGame(false,false);
                string source=Application.persistentDataPath;
                string destination=Path.Combine(Path.GetDirectoryName(Mod.ConfigPath)!,"DivineLabs.Backups","Resources",DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff")+"-"+Guid.NewGuid().ToString("N"));
                job.Backup=Task.Run(()=>BackupFiles(source,destination));return;
            }
            if(!job.Backup.IsCompleted)return;
            job.Backup.GetAwaiter().GetResult();BackupReady=true;
            if(job.Command.Operation=="backup"){Finish(new(){Message="Резервная копия создана.",Saved=true});return;}
            ValidateGrant(job.Command);
            bool changed=false;double delta=0;
            try{
                var before=SavedValues(job.Command);
                InGrant=true;changed=true;delta=Grant(job.Command);changed=delta>0;
                var expected=SavedValues(job.Command);
                Il2Cpp.SaveManager.instance.SaveGame(false,false);
                var disk=Il2Cpp.FileBasedPrefs.GetFileBasedPrefsSaveFileModel();
                foreach(var pair in expected){
                    string raw=Il2Cpp.FileBasedPrefs.GetString(disk,pair.Key,"");string buffered=Il2Cpp.SaveManager.GetString(pair.Key);
                    if(string.IsNullOrEmpty(raw)||string.IsNullOrEmpty(buffered))throw new IOException("Не подтверждена запись ресурса: "+pair.Key);
                    double persisted=Il2Cpp.Utils.ParseDouble(raw),serialized=Il2Cpp.Utils.ParseDouble(buffered);
                    if(persisted!=serialized||!GrantMath.SameSerializedNumber(pair.Value,persisted))throw new IOException("Не подтверждена запись ресурса: "+pair.Key+"; live="+pair.Value.ToString("R",CultureInfo.InvariantCulture)+"; buffer="+serialized.ToString("R",CultureInfo.InvariantCulture)+"; disk="+persisted.ToString("R",CultureInfo.InvariantCulture));
                }
                var primary=expected.First();double saved=Il2Cpp.Utils.ParseDouble(Il2Cpp.FileBasedPrefs.GetString(disk,primary.Key,""));
                if(changed&&saved<=before[primary.Key])throw new IOException("Игровое форматирование округлило выдачу; сохранение начисления не подтверждено.");
                if(changed)delta=saved-before[primary.Key];
                Finish(new(){Changed=changed,Saved=true,Granted=delta,Message=delta>0?"Начислено: "+delta.ToString("G8",CultureInfo.InvariantCulture)+". Сохранено локально.":"Начисления нет: достигнут лимит ресурса или выдача недоступна."});
            }catch(Exception error){
                Mod.Instance.LoggerInstance.Error(error.ToString());
                Finish(new(){Changed=changed,Granted=delta,Error=changed?"Баланс изменён в сессии, но сохранение не подтверждено. Не повторяйте выдачу; проверьте журнал.":"Команда не завершена. Проверьте баланс и журнал перед новой выдачей."});
            }finally{InGrant=false;}
        }catch(Exception error){Mod.Instance.LoggerInstance.Error(error.ToString());Finish(new(){Error=error is InvalidDataException?error.Message:"Выдача не выполнялась: не удалось подготовить резервную копию или игровое состояние. Подробности в журнале."});}
    }
    private static void Finish(GameCommandResult result)
    {
        var job=current;current=null;job.Cancellation.Dispose();if(result!=null)job.Reply.TrySetResult(result);
    }
    internal static void SceneExited()=>BackupReady=false;
    internal static void Stop()
    {
        if(current!=null){current.Reply.TrySetCanceled();current.Cancellation.Dispose();current=null;}
        while(queue.TryDequeue(out var job)){job.Reply.TrySetCanceled();job.Cancellation.Dispose();}
    }
    private static void RequireReady()
    {
        if(!Mod.GameSceneReady||Il2Cpp.PlayerInventory.instance==null||Il2Cpp.SaveManager.instance==null)throw new InvalidDataException("Дождитесь загрузки игровой сцены.");
        if(Il2Cpp.SaveOwnership.HasConflict())throw new InvalidDataException("Игра обнаружила конфликт владельца сохранения. Сначала разрешите синхронизацию.");
    }
    private static void BackupFiles(string source,string destination)
    {
        string[] files=Directory.GetFiles(source,"*.sav");
        if(files.Length==0)throw new IOException("Файлы сохранения не найдены; выдача отменена.");
        Directory.CreateDirectory(destination);
        foreach(string file in files){File.Copy(file,Path.Combine(destination,Path.GetFileName(file)));}
    }
    private static Il2Cpp.Drop Material(string id)=>Il2Cpp.PlayerInventory.instance.drops.FirstOrDefault(x=>x!=null&&x.id==id)??throw new InvalidDataException("Материал больше не найден в текущей игре.");
    private static void ValidateGrant(GameCommand command)
    {
        if(!Mod.Config.Enabled)throw new InvalidDataException("Сначала включите общий переключатель функций.");
        command.Validate();var p=Il2Cpp.PlayerInventory.instance;
        switch(command.Resource){
            case ResourceKind.Coins:GrantMath.Add(p.coins,command.Amount);GrantMath.Add(p.lifetimeCoins,command.Amount);break;
            case ResourceKind.SlayerPoints:
                GrantMath.Add(Il2Cpp.SlayerPoints.points,command.Amount);double total=GrantMath.Add(Il2Cpp.SlayerPoints.lifetime,command.Amount);
                if(!double.IsFinite((total+Il2Cpp.SlayerPoints.pre)*4+12))throw new InvalidDataException("Количество SP переполнит формулу душ.");break;
            case ResourceKind.DivinityPoints:GrantMath.Add(p.divinityPoints,command.Amount);break;
            case ResourceKind.UltraSlayerPoints:GrantMath.Add(Il2Cpp.UltraSlayerPoints.points,command.Amount);GrantMath.CheckUltraTotal(Il2Cpp.UltraSlayerPoints.lifetime+command.Amount);break;
            case ResourceKind.JewelsOfSoul:GrantMath.Add(p.jewelsOfSoul,command.Amount);GrantMath.Add(p.jewelsOfSoulEarned,command.Amount);break;
            case ResourceKind.Material:
                var drop=Material(command.MaterialId);GrantMath.Add(drop.amount,Math.Min(command.Amount,Math.Max(0,drop.GetMaxAmount()-drop.amount)));
                if(Il2Cpp.DropsManager.instance==null)throw new InvalidDataException("Менеджер материалов ещё не готов.");break;
        }
    }
    internal static void SaveNumber(string key,double value)
    {
        if(!double.IsFinite(value))throw new InvalidDataException("Игра вернула некорректное число: "+key);
        string text=value.ToString("R",CultureInfo.InvariantCulture);
        if(Il2Cpp.Utils.ParseDouble(text)!=value)throw new InvalidDataException("Игра не смогла прочитать записываемое число.");
        Il2Cpp.SaveManager.SetString(key,text);
    }
    private static double Grant(GameCommand command)
    {
        var p=Il2Cpp.PlayerInventory.instance;double n=command.Amount,before;
        switch(command.Resource){
            case ResourceKind.Coins:before=p.coins;p.AddCoins(n);return p.coins-before;
            case ResourceKind.SlayerPoints:
                before=Il2Cpp.SlayerPoints.points;
                Il2Cpp.SlayerPoints.points=GrantMath.Add(before,n);Il2Cpp.SlayerPoints.lifetime=GrantMath.Add(Il2Cpp.SlayerPoints.lifetime,n);
                SaveNumber("Slayer Points",Il2Cpp.SlayerPoints.points);SaveNumber("Lifetime Slayer Points",Il2Cpp.SlayerPoints.lifetime+Il2Cpp.SlayerPoints.pre);
                Il2Cpp.UltraSlayerPoints.SlayerPointsGained(n);
                Il2Cpp.SkillTreeManager.instance?.SlayerPointsChanged?.Invoke();return Il2Cpp.SlayerPoints.points-before;
            case ResourceKind.DivinityPoints:
                before=p.divinityPoints;p.divinityPoints=GrantMath.Add(before,n);SaveNumber("Divinity Points",p.divinityPoints);
                Il2Cpp.DivinitiesManager.instance?.CheckAvailableDivinities();return p.divinityPoints-before;
            case ResourceKind.UltraSlayerPoints:
                before=Il2Cpp.UltraSlayerPoints.points;Il2Cpp.UltraSlayerPoints.points=GrantMath.Add(before,n);SaveNumber("Ultra Slayer Points",Il2Cpp.UltraSlayerPoints.points);
                Il2Cpp.UltraSlayerPoints.CalculateLifetimeUltraSlayerPoints();return Il2Cpp.UltraSlayerPoints.points-before;
            case ResourceKind.JewelsOfSoul:
                before=p.jewelsOfSoul;p.jewelsOfSoul=GrantMath.Add(before,n);p.jewelsOfSoulEarned=GrantMath.Add(p.jewelsOfSoulEarned,n);
                SaveNumber("Jewels of Soul",p.jewelsOfSoul);SaveNumber("Jewels of Soul Earned",p.jewelsOfSoulEarned);
                Il2Cpp.JewelsOfSoulManager.instance?.RefreshJewelsOfSoulLeft();return p.jewelsOfSoul-before;
            case ResourceKind.Material:
                var drop=Material(command.MaterialId);before=drop.amount;
                Il2Cpp.DropsManager.instance.AddDrop(n,drop,false,false,false,true,false,false);return drop.amount-before;
            default:throw new InvalidDataException("Неизвестный ресурс.");
        }
    }
    private static Dictionary<string,double> SavedValues(GameCommand command)
    {
        var p=Il2Cpp.PlayerInventory.instance;
        return command.Resource switch{
            ResourceKind.Coins=>new(){{"Coins",p.coins},{"Lifetime Coins",p.lifetimeCoins}},
            ResourceKind.SlayerPoints=>new(){{"Slayer Points",Il2Cpp.SlayerPoints.points},{"Lifetime Slayer Points",Il2Cpp.SlayerPoints.lifetime+Il2Cpp.SlayerPoints.pre}},
            ResourceKind.DivinityPoints=>new(){{"Divinity Points",p.divinityPoints}},
            ResourceKind.UltraSlayerPoints=>new(){{"Ultra Slayer Points",Il2Cpp.UltraSlayerPoints.points}},
            ResourceKind.JewelsOfSoul=>new(){{"Jewels of Soul",p.jewelsOfSoul},{"Jewels of Soul Earned",p.jewelsOfSoulEarned}},
            ResourceKind.Material=>new(){{command.MaterialId,Material(command.MaterialId).amount}},
            _=>throw new InvalidDataException("Неизвестный ресурс.")};
    }
    private static double Finite(double value){if(!double.IsFinite(value))throw new InvalidDataException("Игровой баланс не является конечным числом.");return value;}
    private static GameState Inspect()
    {
        var p=Il2Cpp.PlayerInventory.instance;
        return new(){Coins=Finite(p.coins),SlayerPoints=Finite(Il2Cpp.SlayerPoints.points),DivinityPoints=Finite(Il2Cpp.DivinitiesManager.instance?.CalculateDivinityPointsLeft()??p.divinityPoints),UltraSlayerPoints=Finite(Il2Cpp.UltraSlayerPoints.USPAvailable()),JewelsOfSoul=Finite(p.jewelsOfSoul),
            Materials=p.drops.Where(d=>d!=null).Select(d=>new MaterialState{Id=d.id,Title=string.IsNullOrWhiteSpace(d.localizedName)?d.name:d.localizedName,Amount=Finite(d.amount),Maximum=Finite(d.GetMaxAmount()),Locked=d.locked}).OrderBy(d=>d.Title,StringComparer.CurrentCulture).ToArray()};
    }
}
