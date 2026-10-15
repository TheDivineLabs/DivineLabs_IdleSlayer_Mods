using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace DivineLabs.IdleSlayer;
public sealed class SessionRequest
{
    public int Protocol {get;set;}=1;
    public string Id {get;set;}=Guid.NewGuid().ToString("N");
    public long Revision {get;set;}
    public Settings Settings {get;set;}
}
public sealed class SessionReply
{
    public int Protocol {get;set;}=1;
    public string Id {get;set;}
    public string Error {get;set;}
    public Settings Settings {get;set;}
}
public static class SessionWire
{
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
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(2500);
        using var pipe=new NamedPipeClientStream(".",Name(path),PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(timeout.Token);await Write(pipe,request,timeout.Token);var reply=await Read<SessionReply>(pipe,timeout.Token);
        if(reply.Protocol!=1||reply.Id!=request.Id)throw new IOException("Версия соединения не совпадает.");return reply;
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
    public Settings Snapshot=>Volatile.Read(ref snapshot);
    public SessionHost(string config,Action<Exception> error=null){report=error;path=config;owner=SettingsStore.AcquireOwner(path);try{snapshot=SettingsStore.Load(path);}catch{owner.Dispose();throw;}}
    public void Start()=>_ = Task.Run(Serve);
    public async Task<SessionReply> Apply(SessionRequest request)
    {
        await changes.WaitAsync().ConfigureAwait(false);
        try{
            if(disposed)return Reply(request,"Игра завершает работу.");
            if(request.Protocol!=1)return Reply(request,"Версия соединения не совпадает.");
            if(request.Settings==null)return Reply(request);
            if(request.Revision!=Snapshot.Revision)return Reply(request,"Настройки уже изменились. Повторите действие.");
            var next=request.Settings.Copy();next.Revision=checked(Snapshot.Revision+1);next.Validate();
            await Task.Run(()=>SettingsStore.Save(path,next)).ConfigureAwait(false);
            Volatile.Write(ref snapshot,next);return Reply(request);
        }catch(Exception e) when(e is IOException or InvalidDataException or OverflowException){report?.Invoke(e);return Reply(request,e is InvalidDataException?e.Message:"Не удалось сохранить настройки. Подробности в журнале игры.");}
        finally{changes.Release();}
    }
    private SessionReply Reply(SessionRequest r,string error=null)=>new(){Id=r.Id,Error=error,Settings=Snapshot.Copy()};
    private async Task Serve()
    {
        while(!lifetime.IsCancellationRequested){
            try{
                using var pipe=new NamedPipeServerStream(SessionWire.Name(path),PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(lifetime.Token);
                using var limit=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);limit.CancelAfter(3000);
                var request=await SessionWire.Read<SessionRequest>(pipe,limit.Token);var reply=await Apply(request);await SessionWire.Write(pipe,reply,limit.Token);
            }catch(Exception e) when(e is IOException or OperationCanceledException or JsonException or InvalidDataException){}
        }
    }
    public void Dispose(){lifetime.Cancel();changes.Wait();try{disposed=true;owner.Dispose();}finally{changes.Release();}}
}