using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DivineLabs.IdleSlayer;
public sealed class SessionRequest
{
    public int Protocol {get;set;}=SessionWire.Protocol;
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public string Session {get;set;}="";
    public long Revision {get;set;}
    public Settings Settings {get;set;}
    public GameCommand Command {get;set;}
}
public sealed class SessionReply
{
    public int Protocol {get;set;}=SessionWire.Protocol;
    public string Id {get;set;}
    public string Session {get;set;}="";
    public string Error {get;set;}
    public Settings Settings {get;set;}
    public GameCommandResult Result {get;set;}
}
public static class SessionWire
{
    public const int Protocol=2;
    public static string Name(string path)=>"DivineLabs.IdleSlayer."+Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())))[..24];
    public static async Task Write<T>(Stream stream,T value,CancellationToken token)
    {
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(value);
        if(data.Length>65536)throw new IOException("Слишком большое сообщение.");
        await stream.WriteAsync(BitConverter.GetBytes(data.Length),token);await stream.WriteAsync(data,token);await stream.FlushAsync(token);
    }
    private static async Task Exact(Stream s,byte[] bytes,CancellationToken ct){int offset=0;while(offset<bytes.Length){int n=await s.ReadAsync(bytes.AsMemory(offset),ct);if(n==0)throw new EndOfStreamException();offset+=n;}}
    public static async Task<T> Read<T>(Stream stream,CancellationToken token)
    {
        var size=new byte[4];await Exact(stream,size,token);int length=BitConverter.ToInt32(size);
        if(length<1||length>65536)throw new IOException("Некорректное сообщение.");
        var data=new byte[length];await Exact(stream,data,token);return JsonSerializer.Deserialize<T>(data)??throw new IOException("Пустое сообщение.");
    }
    public static async Task<SessionReply> Send(string path,SessionRequest request,CancellationToken token=default)
    {
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(request.Command!=null?15000:8000);
        using var pipe=new NamedPipeClientStream(".",Name(path),PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);await Write(pipe,request,timeout.Token);var reply=await Read<SessionReply>(pipe,timeout.Token);
        if(reply.Protocol!=Protocol||reply.Id!=request.Id)throw new IOException("Версия соединения не совпадает.");return reply;
    }
}
// Owns persistence for the entire game session. Published snapshots are never mutated.
public sealed class SessionHost:IDisposable
{
    private readonly string path;
    private readonly FileStream owner;
    private readonly SemaphoreSlim changes=new(1,1);
    private readonly CancellationTokenSource lifetime=new();
    private Settings snapshot;
    private bool disposed;
    private readonly Action<Exception> report;
    private readonly Func<GameCommand,CancellationToken,Task<GameCommandResult>> commands;
    private readonly Func<Settings,Settings,CancellationToken,Task> beforeChange;
    private readonly Dictionary<string,(GameCommand Command,GameCommandResult Result)> receipts=new();
    public string SessionId {get;}=Guid.NewGuid().ToString("N");
    public Settings Snapshot=>Volatile.Read(ref snapshot);
    public SessionHost(string config,Action<Exception> error=null,Func<GameCommand,CancellationToken,Task<GameCommandResult>> gameCommands=null,Func<Settings,Settings,CancellationToken,Task> prepareChange=null){report=error;commands=gameCommands;beforeChange=prepareChange;path=config;owner=SettingsStore.AcquireOwner(path);try{snapshot=SettingsStore.Load(path);}catch{owner.Dispose();throw;}}
    public void Start()=>_ = Task.Run(Serve);
    public async Task<SessionReply> Apply(SessionRequest request)
    {
        await changes.WaitAsync().ConfigureAwait(false);
        try{
            if(disposed)return Reply(request,"Игра завершает работу.");
            if(request.Protocol!=SessionWire.Protocol)return Reply(request,"Версия соединения не совпадает. Перезапустите игру с новым модом.");
            if(request.Command!=null){
                request.Command.Validate();
                if(!Guid.TryParseExact(request.Id,"N",out _))return Reply(request,"Некорректный идентификатор команды.");
                if(request.Settings!=null)return Reply(request,"Нельзя менять настройки и выдавать ресурсы одной командой.");
                if(receipts.TryGetValue(request.Id,out var previous)){
                    if(previous.Command!=request.Command)return Reply(request,"Идентификатор уже принадлежит другой команде.");
                    return CommandReply(request,previous.Result);
                }
                if(commands==null)return Reply(request,"Этот мод не поддерживает игровые команды. Перезапустите игру с новой сборкой.");
                if(request.Command.Operation=="grant"&&request.Session!=SessionId)return Reply(request,"Игровая сессия изменилась. Обновите баланс перед выдачей.");
                if(request.Command.Operation=="grant"&&request.Revision!=Snapshot.Revision)return Reply(request,"Настройки уже изменились. Обновите состояние и повторите.");
                if(request.Command.Operation=="grant"&&receipts.Count>=4096)return Reply(request,"Достигнут предел команд этой сессии. Перезапустите игру.");
                var result=await commands(request.Command,lifetime.Token).ConfigureAwait(false);
                if(request.Command.Operation=="grant")receipts.Add(request.Id,(request.Command,result));
                return CommandReply(request,result);
            }
            if(request.Settings==null)return Reply(request);
            if(request.Revision!=Snapshot.Revision)return Reply(request,"Настройки уже изменились. Повторите действие.");
            var next=request.Settings.Copy();next.Revision=checked(Snapshot.Revision+1);next.Validate();
            if(beforeChange!=null)await beforeChange(Snapshot,next,lifetime.Token).ConfigureAwait(false);
            await Task.Run(()=>SettingsStore.Save(path,next)).ConfigureAwait(false);
            Volatile.Write(ref snapshot,next);return Reply(request);
        }catch(Exception e) when(e is IOException or InvalidDataException or OverflowException or OperationCanceledException){report?.Invoke(e);return Reply(request,e is InvalidDataException?e.Message:request.Command!=null?"Команда не подтверждена. Проверьте результат с тем же номером и журнал игры.":"Не удалось сохранить настройки. Подробности в журнале игры.");}
        finally{changes.Release();}
    }
    private SessionReply CommandReply(SessionRequest request,GameCommandResult result)=>new(){Id=request.Id,Session=SessionId,Error=result.Error,Settings=Snapshot.Copy(),Result=result};
    private SessionReply Reply(SessionRequest r,string error=null)=>new(){Id=r.Id,Session=SessionId,Error=error,Settings=Snapshot.Copy()};
    private async Task Serve()
    {
        while(!lifetime.IsCancellationRequested){
            try{
                using var pipe=new NamedPipeServerStream(SessionWire.Name(path),PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                using var limit=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);limit.CancelAfter(20000);
                var request=await SessionWire.Read<SessionRequest>(pipe,limit.Token);var reply=await Apply(request);await SessionWire.Write(pipe,reply,limit.Token);
            }catch(Exception e) when(e is IOException or OperationCanceledException or JsonException or InvalidDataException){}
        }
    }
    public void Dispose(){lifetime.Cancel();changes.Wait();try{disposed=true;owner.Dispose();}finally{changes.Release();}}
}