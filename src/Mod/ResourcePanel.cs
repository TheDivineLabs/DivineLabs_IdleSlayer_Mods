using System.Globalization;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
namespace DivineLabs.IdleSlayer;
internal static class ResourcePanel
{
    private static GameObject root,editor,previous,next;
    private static TextMeshProUGUI balances,result,resource,material;
    private static TMP_InputField amount,search;
    private static Button give;
    private static int kind,index;
    private static GameState state;
    private static MaterialState[] materials=Array.Empty<MaterialState>();
    private static SessionRequest pending;
    private static Task<SessionReply> task;
    private static string grantMessage;
    private static readonly Color muted=new(.65f,.70f,.78f,1);
    internal static void Create(Transform parent)
    {
        root=GameMenu.Node(parent,"Resources",208,76,688,470);
        GameMenu.Label(root.transform,"Ресурсы",0,0,450,40,29,Color.white);
        balances=GameMenu.Label(root.transform,"Обновите баланс после загрузки сцены.",0,52,680,74,16,muted);
        GameMenu.Button(root.transform,"Обновить",532,0,150,38,Inspect);
        editor=GameMenu.Node(root.transform,"Grant",0,136,688,234);
        resource=GameMenu.Button(editor.transform,"",0,0,680,38,()=>{kind=(kind+1)%ResourceChoice.All.Length;UpdateChoice();}).GetComponentInChildren<TextMeshProUGUI>();
        search=GameMenu.Field(editor.transform,0,50,420,36,false);search.onValueChanged.AddListener((UnityAction<string>)(_=>Filter()));
        previous=GameMenu.Button(editor.transform,"‹",438,50,48,36,()=>{if(materials.Length>0){index=(index+materials.Length-1)%materials.Length;UpdateChoice();}});
        next=GameMenu.Button(editor.transform,"›",496,50,48,36,()=>{if(materials.Length>0){index=(index+1)%materials.Length;UpdateChoice();}});
        material=GameMenu.Label(editor.transform,"",0,92,680,38,16,muted);
        GameMenu.Label(editor.transform,"Количество (например 100 или 1e12)",0,138,390,30,17,Color.white);
        amount=GameMenu.Field(editor.transform,0,176,300,40,true);amount.text="100";
        give=GameMenu.Button(root.transform,"Выдать",342,312,338,40,Grant).GetComponent<Button>();
        result=GameMenu.Label(root.transform,"Перед выдачей создаётся резервная копия. Лимиты материалов сохраняются.",0,386,680,78,15,muted);UpdateChoice();root.SetActive(false);
    }
    internal static void Hide(){if(root!=null)root.SetActive(false);}
    internal static void Show(){root.SetActive(true);if(task==null&&pending==null)Inspect();}
    private static string N(double n)=>n.ToString("G7",CultureInfo.InvariantCulture);
    private static void UpdateChoice()
    {
        resource.text=ResourceChoice.All[kind].Title+"  ›";bool mat=ResourceChoice.All[kind].Kind==ResourceKind.Material;
        search.gameObject.SetActive(mat);previous.SetActive(mat);next.SetActive(mat);material.text=mat?(materials.Length==0?"Материал не найден.":materials[index].Title+" · "+N(materials[index].Amount)+" / "+N(materials[index].Maximum)):ResourceChoice.All[kind].Kind==ResourceKind.JewelsOfSoul?"Локальный баланс; без квитанций Steam. Облако не подтверждено.":"Выдача не умножается функциями тренера.";
    }
    private static void Filter(){materials=state?.Materials.Where(m=>m.Title.Contains(search.text??"",StringComparison.OrdinalIgnoreCase)).ToArray()??Array.Empty<MaterialState>();index=0;UpdateChoice();}
    private static void Inspect(){if(task!=null||pending!=null)return;grantMessage=null;Send(new());}
    private static void Send(GameCommand command){pending=new(){Session=Mod.Session.SessionId,Revision=Mod.Config.Revision,Command=command};task=Mod.Session.Apply(pending);give.interactable=false;editor.SetActive(false);}
    private static void Grant()
    {
        if(task!=null)return;
        if(pending!=null){task=Mod.Session.Apply(pending);give.interactable=false;return;}
        grantMessage=null;
        try{if(state==null)throw new InvalidDataException("Сначала обновите баланс.");if(!double.TryParse(amount.text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n))throw new InvalidDataException("Укажите целое конечное количество от 1.");var cmd=new GameCommand{Operation="grant",Resource=ResourceChoice.All[kind].Kind,Amount=n,MaterialId=materials.Length>0?materials[index].Id:""};cmd.Validate();Send(cmd);}
        catch(Exception e){result.text=e.Message;}
    }
    internal static void Tick()
    {
        if(task==null||!task.IsCompleted)return;
        try{var reply=task.GetAwaiter().GetResult();bool granted=pending.Command.Operation=="grant";pending=null;result.text=reply.Error??reply.Result?.Message??"Баланс обновлён.";if(granted)grantMessage=result.text;else if(grantMessage!=null){result.text=grantMessage;grantMessage=null;}
            if(reply.Result?.State is GameState s){state=s;balances.text=$"Монеты {N(s.Coins)}\nSP {N(s.SlayerPoints)}   DP {N(s.DivinityPoints)}   USP {N(s.UltraSlayerPoints)}\nКамни душ {N(s.JewelsOfSoul)} · локально";Filter();}
            task=null;if(granted){string message=result.text;Send(new());result.text=message;return;}
        }catch(Exception e){Mod.Instance.LoggerInstance.Error(e.ToString());result.text="Нет подтверждения. Нажмите «Выдать», чтобы проверить ту же команду.";}
        task=null;give.interactable=true;editor.SetActive(pending==null);
    }
}
