namespace DivineLabs.IdleSlayer;
public enum ResourceKind { Coins, SlayerPoints, DivinityPoints, UltraSlayerPoints, Material, JewelsOfSoul }
public sealed record ResourceChoice(ResourceKind Kind,string Title)
{
    public static readonly ResourceChoice[] All={new(ResourceKind.Coins,"Монеты"),new(ResourceKind.SlayerPoints,"Slayer Points · SP"),new(ResourceKind.DivinityPoints,"Очки божественности · DP"),new(ResourceKind.UltraSlayerPoints,"Ultra Slayer Points · USP"),new(ResourceKind.Material,"Материал"),new(ResourceKind.JewelsOfSoul,"Камни душ · локальный баланс")};
    public override string ToString()=>Title;
}
public sealed record GameCommand
{
    public string Operation {get;init;}="inspect";
    public ResourceKind Resource {get;init;}
    public double Amount {get;init;}
    public string MaterialId {get;init;}="";
    public void Validate()
    {
        if(Operation is not ("inspect" or "grant" or "backup"))throw new InvalidDataException("Неизвестная игровая команда.");
        if(Operation!="grant")return;
        if(!Enum.IsDefined(typeof(ResourceKind),Resource)||!double.IsFinite(Amount)||Amount<1||Math.Truncate(Amount)!=Amount)throw new InvalidDataException("Укажите целое конечное количество от 1.");
        if(Resource==ResourceKind.Material&&(string.IsNullOrWhiteSpace(MaterialId)||MaterialId.Length>128||MaterialId.Any(char.IsControl)))throw new InvalidDataException("Выберите материал из списка игры.");
    }
}
public sealed class MaterialState
{
    public string Id {get;set;}="";
    public string Title {get;set;}="";
    public double Amount {get;set;}
    public double Maximum {get;set;}
    public bool Locked {get;set;}
    public override string ToString()=>Title+(Locked?" · не открыт":"");
}
public sealed class GameState
{
    public double Coins {get;set;}
    public double SlayerPoints {get;set;}
    public double DivinityPoints {get;set;}
    public double UltraSlayerPoints {get;set;}
    public double JewelsOfSoul {get;set;}
    public MaterialState[] Materials {get;set;}=Array.Empty<MaterialState>();
}
public sealed class GameCommandResult
{
    public string Message {get;set;}="";
    public string Error {get;set;}
    public bool Changed {get;set;}
    public bool Saved {get;set;}
    public double Granted {get;set;}
    public GameState State {get;set;}
}
public static class GrantMath
{
    // IL2CPP default ToString uses G15 in several native save paths.
    public static bool SameSerializedNumber(double live,double saved)=>double.IsFinite(live)&&double.IsFinite(saved)&&Math.Abs(live-saved)<=Math.Max(1e-12,Math.Abs(live)*1e-14);
    public static double Add(double balance,double amount)
    {
        if(!double.IsFinite(balance)||balance<0||!double.IsFinite(amount)||amount<0)throw new InvalidDataException("Игровой баланс или количество некорректны.");
        double result=balance+amount;
        if(!double.IsFinite(result))throw new InvalidDataException("Количество вызовет переполнение игрового баланса.");
        if(amount>0&&result<=balance)throw new InvalidDataException("Количество слишком мало относительно баланса: double округляет это начисление.");
        return result;
    }
    public static void CheckUltraTotal(double total)
    {
        if(!double.IsFinite(total)||total<0||!double.IsFinite(600000*Math.Pow(1.23,total+1)))throw new InvalidDataException("Такое количество USP переполнит формулу следующего очка. Уменьшите количество.");
    }
}
