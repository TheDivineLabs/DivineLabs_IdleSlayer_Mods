using System.Reflection;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace DivineLabs.IdleSlayer;
internal static class FreePurchases
{
    private sealed record Price<T>(T Item,double Original);
    private static Price<Il2Cpp.AscensionSkill>[] skills=Array.Empty<Price<Il2Cpp.AscensionSkill>>();
    private static Price<Il2Cpp.Divinity>[] divinities=Array.Empty<Price<Il2Cpp.Divinity>>();
    private static Price<Il2Cpp.UpgradeOfTime>[] times=Array.Empty<Price<Il2Cpp.UpgradeOfTime>>();
    private static Price<Il2Cpp.CraftableItem>[] crafts=Array.Empty<Price<Il2Cpp.CraftableItem>>();
    private static readonly Dictionary<IntPtr,Il2CppReferenceArray<Il2Cpp.MaterialRequirement>> zeroRequirements=new();
    private static IntPtr inventory;
    private static long revision=-1;
    private static bool coins,sp,dp,usp,craft,faulted;
    private static Task<GameCommandResult> preparing;
    private static bool Active=>!faulted&&Mod.GameSceneReady&&GameActions.BackupReady&&Il2Cpp.PlayerInventory.instance!=null&&Il2Cpp.PlayerInventory.instance.Pointer==inventory;
    internal static bool Coins=>Active&&coins;
    internal static bool SP=>Active&&sp;
    internal static bool DP=>Active&&dp;
    internal static bool USP=>Active&&usp;
    internal static bool Craft=>Active&&craft;
    internal static void Install(HarmonyLib.Harmony harmony)
    {
        void Patch(Type type,string name,string prefix=null,string postfix=null,string finalizer=null){
            var methods=type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static).Where(m=>m.Name==name).ToArray();
            if(methods.Length==0)throw new MissingMethodException(type.FullName,name);
            foreach(var method in methods)harmony.Patch(method,prefix==null?null:new HarmonyMethod(typeof(FreePurchases),prefix),postfix==null?null:new HarmonyMethod(typeof(FreePurchases),postfix),null,finalizer==null?null:new HarmonyMethod(typeof(FreePurchases),finalizer),null);
        }
        Patch(typeof(Il2Cpp.Power),"CalculateCost",nameof(PowerCost));
        Patch(typeof(Il2Cpp.Power),"CalculateMaxCost",nameof(MaxPowerCost));
        Patch(typeof(Il2Cpp.Upgrade),"GetCost",nameof(UpgradeCost));
        Patch(typeof(Il2Cpp.DivinitiesManager),"ActivateDivinity",nameof(BeginDivinity),null,nameof(EndDivinity));
        Patch(typeof(Il2Cpp.UpgradeOfTime),"Unlock",nameof(BeginTime),null,nameof(EndTime));
        Patch(typeof(Il2Cpp.UpgradeOfTime),"Deactivate",null,nameof(RefreshEvent));
        Patch(typeof(Il2Cpp.DivinitiesManager),"DeactivateDivinity",null,nameof(RefreshEvent));
        Patch(typeof(Il2Cpp.DivinitiesManager),"ResetDivinities",null,nameof(RefreshEvent));
        Patch(typeof(Il2Cpp.SkillTreeManager),"BuyAscensionSkill",null,nameof(RefreshEvent));
        Patch(typeof(Il2Cpp.PermanentCraftableItem),"GetRequirements",null,nameof(Requirements));
        Patch(typeof(Il2Cpp.TemporaryCraftableItem),"GetRequirements",null,nameof(Requirements));
        Patch(typeof(Il2Cpp.PermanentCraftableItem),"Craft",nameof(BeginCraft),null,nameof(EndCraft));
        Patch(typeof(Il2Cpp.TemporaryCraftableItem),"Craft",nameof(BeginCraft),null,nameof(EndCraft));
        Patch(typeof(Il2Cpp.TemporaryCraftableItem),"HowManyCanCraft",nameof(CraftBatch));
    }
    internal static void Tick()
    {
        if(faulted)return;
        try{TickCore();}catch(Exception error){faulted=true;try{Restore();}catch(Exception restore){Mod.Instance.LoggerInstance.Error(restore.ToString());}Mod.Instance.LoggerInstance.Error(error.ToString());Mod.Messages.Enqueue("Бесплатные покупки отключены после ошибки. Подробности в журнале.");Mod.Change(c=>{foreach(string id in FeatureCatalog.FreeIds)c.SetFeatureEnabled(id,false);});}
    }
    private static void TickCore()
    {
        if(!Mod.GameSceneReady)return;
        var p=Il2Cpp.PlayerInventory.instance;if(p==null){Restore();inventory=IntPtr.Zero;return;}
        if(p.ascensionSkills==null||p.divinities==null||p.upgradesOfTime==null||p.permanentCraftableItems==null||p.temporaryCraftableItems==null)return;
        if(inventory!=p.Pointer){
            Restore();inventory=p.Pointer;
            skills=p.ascensionSkills.Where(x=>x!=null).Select(x=>new Price<Il2Cpp.AscensionSkill>(x,x.cost)).ToArray();
            divinities=p.divinities.Where(x=>x!=null).Select(x=>new Price<Il2Cpp.Divinity>(x,x.cost)).ToArray();
            times=p.upgradesOfTime.Where(x=>x!=null).Select(x=>new Price<Il2Cpp.UpgradeOfTime>(x,x.uspCost)).ToArray();
            crafts=p.permanentCraftableItems.Where(x=>x!=null).Select(x=>new Price<Il2Cpp.CraftableItem>(x,x.coinsRequired)).Concat(p.temporaryCraftableItems.Where(x=>x!=null).Select(x=>new Price<Il2Cpp.CraftableItem>(x,x.coinsRequired))).ToArray();
            revision=-1;
        }
        var config=Mod.Config;
        if(config.Enabled&&(config.FreeCoins||config.FreeSP||config.FreeDP||config.FreeUSP||config.FreeCraft)&&!GameActions.BackupReady){
            if(preparing==null)preparing=GameActions.Submit(new(){Operation="backup"});
            if(!preparing.IsCompleted)return;
            if(preparing.IsFaulted||preparing.IsCanceled||preparing.Result.Error!=null){if(revision!=config.Revision)Mod.Messages.Enqueue("Бесплатные покупки не включены: не создана резервная копия.");revision=config.Revision;return;}
        }
        if(revision==config.Revision)return;
        revision=config.Revision;coins=config.Enabled&&config.FreeCoins;sp=config.Enabled&&config.FreeSP;dp=config.Enabled&&config.FreeDP;usp=config.Enabled&&config.FreeUSP;craft=config.Enabled&&config.FreeCraft;ApplyPrices();
    }
    private static void ApplyPrices()
    {
        foreach(var p in skills)p.Item.cost=SP&&!p.Item.IsUnlocked()?0:p.Original;
        foreach(var p in divinities)p.Item.cost=DP&&!p.Item.unlocked?0:p.Original;
        foreach(var p in times)p.Item.uspCost=USP&&!p.Item.active?0:p.Original;
        foreach(var p in crafts)p.Item.coinsRequired=Craft?0:p.Original;
    }
    internal static void Restore()
    {
        coins=sp=dp=usp=craft=false;
        foreach(var p in skills)if(p.Item!=null)p.Item.cost=p.Original;
        foreach(var p in divinities)if(p.Item!=null)p.Item.cost=p.Original;
        foreach(var p in times)if(p.Item!=null)p.Item.uspCost=p.Original;
        foreach(var p in crafts)if(p.Item!=null)p.Item.coinsRequired=p.Original;
        skills=Array.Empty<Price<Il2Cpp.AscensionSkill>>();divinities=Array.Empty<Price<Il2Cpp.Divinity>>();times=Array.Empty<Price<Il2Cpp.UpgradeOfTime>>();crafts=Array.Empty<Price<Il2Cpp.CraftableItem>>();zeroRequirements.Clear();revision=-1;preparing=null;
    }
    private static void RefreshEvent()=>ApplyPrices();
    private static bool PowerCost(ref double __result){if(!Coins)return true;__result=0;return false;}
    private static bool MaxPowerCost(Il2Cpp.Power __instance,ref Il2Cpp.Power.MaxCost __result)
    {
        if(!Coins)return true;__result=new(){accumulated=Math.Max(0,Math.Min(100,int.MaxValue-__instance.level)),cost=0};return false;
    }
    private static bool UpgradeCost(Il2Cpp.Upgrade __instance,ref double __result)
    {
        if(!Coins||__instance.paidUpgrade||__instance.storyRewardUpgrade)return true;__result=0;return false;
    }
    private sealed class DivinityPurchase{internal Il2Cpp.Divinity Item;internal double Cost;internal bool Owned;}
    private static bool BeginDivinity(string __0,out DivinityPurchase __state)
    {
        __state=null;if(!DP)return true;
        var price=divinities.FirstOrDefault(p=>p.Item.id==__0);if(price==null||price.Item.unlocked)return true;
        try{GrantMath.Add(Il2Cpp.PlayerInventory.instance.divinityPoints,price.Original);}
        catch(InvalidDataException error){Mod.Messages.Enqueue(error.Message);return false;}
        __state=new(){Item=price.Item,Cost=price.Original,Owned=price.Item.unlocked};return true;
    }
    private static Exception EndDivinity(Exception __exception,DivinityPurchase __state)
    {
        if(__state!=null){
            try{if(!__state.Owned&&__state.Item.unlocked){var p=Il2Cpp.PlayerInventory.instance;p.divinityPoints=GrantMath.Add(p.divinityPoints,__state.Cost);GameActions.SaveNumber("Divinity Points",p.divinityPoints);}}
            catch(Exception error){Mod.Messages.Enqueue("Проверьте баланс DP и журнал: компенсация покупки не подтверждена.");Mod.Instance.LoggerInstance.Error(error.ToString());}
            finally{__state.Item.cost=__state.Cost;ApplyPrices();}
        }return __exception;
    }
    private sealed class TimePurchase{internal Il2Cpp.UpgradeOfTime Item;internal double Cost;internal bool Owned;}
    private static bool BeginTime(Il2Cpp.UpgradeOfTime __instance,out TimePurchase __state)
    {
        __state=null;if(!USP||__instance.active)return true;
        var price=times.FirstOrDefault(p=>p.Item.Pointer==__instance.Pointer);if(price==null)return true;
        try{GrantMath.Add(Il2Cpp.UltraSlayerPoints.points,price.Original);GrantMath.CheckUltraTotal(Il2Cpp.UltraSlayerPoints.lifetime+price.Original);}
        catch(InvalidDataException error){Mod.Messages.Enqueue(error.Message);return false;}
        __state=new(){Item=__instance,Cost=price.Original,Owned=__instance.active};return true;
    }
    private static Exception EndTime(Exception __exception,TimePurchase __state)
    {
        if(__state!=null){
            try{if(!__state.Owned&&__state.Item.active){Il2Cpp.UltraSlayerPoints.points=GrantMath.Add(Il2Cpp.UltraSlayerPoints.points,__state.Cost);GameActions.SaveNumber("Ultra Slayer Points",Il2Cpp.UltraSlayerPoints.points);Il2Cpp.UltraSlayerPoints.CalculateLifetimeUltraSlayerPoints();}}
            catch(Exception error){Mod.Messages.Enqueue("Проверьте баланс USP и журнал: компенсация покупки не подтверждена.");Mod.Instance.LoggerInstance.Error(error.ToString());}
            finally{__state.Item.uspCost=__state.Cost;ApplyPrices();}
        }return __exception;
    }
    private static Il2CppReferenceArray<Il2Cpp.MaterialRequirement> Zero(Il2CppReferenceArray<Il2Cpp.MaterialRequirement> original)
    {
        if(original==null)return null;
        if(zeroRequirements.TryGetValue(original.Pointer,out var result))return result;
        result=new(original.Length);
        for(int i=0;i<original.Length;i++){var item=original[i];result[i]=item==null?null:new Il2Cpp.MaterialRequirement{material=item.material,amount=0,divinityPoints=item.divinityPoints};}
        zeroRequirements.Add(original.Pointer,result);return result;
    }
    private static void Requirements(ref Il2CppReferenceArray<Il2Cpp.MaterialRequirement> __result){if(Craft)__result=Zero(__result);}
    private static bool CraftBatch(ref double __result){if(!Craft)return true;__result=100;return false;}
    private sealed class CraftPurchase{internal Il2Cpp.CraftableItem Item;internal Il2CppReferenceArray<Il2Cpp.MaterialRequirement> Requirements;}
    private static void BeginCraft(Il2Cpp.CraftableItem __instance,out CraftPurchase __state)
    {
        __state=null;if(!Craft)return;__state=new(){Item=__instance,Requirements=__instance.requirements};__instance.requirements=Zero(__instance.requirements);
    }
    private static Exception EndCraft(Exception __exception,CraftPurchase __state){if(__state!=null)__state.Item.requirements=__state.Requirements;return __exception;}
}
