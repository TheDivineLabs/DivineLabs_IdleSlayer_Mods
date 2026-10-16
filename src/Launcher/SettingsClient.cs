using DivineLabs.IdleSlayer.Runtime;
namespace DivineLabs.IdleSlayer.Launcher;
internal sealed class SettingsClient
{
    internal string Game="";
    internal Settings Current=new();
    internal bool Connected;
    internal string ServerSession="";
    internal SessionRequest Command(GameCommand command)=>new(){Session=ServerSession,Revision=Current.Revision,Command=command};
    internal async Task<SessionReply> Execute(SessionRequest request)
    {
        await gate.WaitAsync();
        try{if(!Installation.Running)throw new IOException("Запустите игру и дождитесь игровой сцены.");var reply=await SessionWire.Send(Installation.Config(Game),request);Current=reply.Settings;ServerSession=reply.Session;Connected=true;return reply;}
        catch{Connected=false;throw;}finally{gate.Release();}
    }
    private readonly SemaphoreSlim gate=new(1,1);
    public async Task Refresh()
    {
        await gate.WaitAsync();
        try{
            if(!Installation.ValidGame(Game)){Connected=false;return;}
            if(Installation.Running){var reply=await SessionWire.Send(Installation.Config(Game),new());if(reply.Error!=null)throw new InvalidDataException(reply.Error);Current=reply.Settings;ServerSession=reply.Session;Connected=true;}
            else{Connected=false;Current=await Task.Run(()=>{using var owner=SettingsStore.AcquireOwner(Installation.Config(Game));return SettingsStore.Load(Installation.Config(Game));});}
        }catch{Connected=false;throw;}finally{gate.Release();}
    }
    public async Task Change(Action<Settings> change)
    {
        await gate.WaitAsync();
        try{
            if(!Installation.ValidGame(Game))throw new IOException("Сначала выберите установленную игру.");
            if(Installation.Running){
                var next=Current.Copy();change(next);next.Validate();
                var reply=await SessionWire.Send(Installation.Config(Game),new(){Revision=Current.Revision,Settings=next});
                Current=reply.Settings;ServerSession=reply.Session;Connected=true;if(reply.Error!=null)throw new InvalidDataException(reply.Error);
            }else{
                Current=await Task.Run(()=>{using var owner=SettingsStore.AcquireOwner(Installation.Config(Game));Installation.RequireStopped();var next=SettingsStore.Load(Installation.Config(Game));change(next);next.Revision++;next.Validate();SettingsStore.Save(Installation.Config(Game),next);return next;});
            }
        }catch(Exception e){if(Installation.Running&&e is not InvalidDataException)Connected=false;throw;}finally{gate.Release();}
    }
}