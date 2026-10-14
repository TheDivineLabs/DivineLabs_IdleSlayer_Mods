using System.Globalization;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
namespace DivineLabs.IdleSlayer;
internal static class GameMenu
{
    private static GameObject root, overlay;
    private static TMP_InputField coins, damage;
    private static TextMeshProUGUI status, enabledText;
    private static TMP_FontAsset font;
    private static CanvasScaler scaler;
    private static RectTransform panelRect;
    private static bool cursorVisible;
    private static CursorLockMode cursorLock;
    internal static bool Open { get; private set; }
    private static readonly Color Background=new(0.055f,0.07f,0.11f,1), Surface=new(0.10f,0.13f,0.20f,1), Accent=new(0.25f,0.72f,0.95f,1);
    internal static void Create()
    {
        if(root!=null) return;
        font=TMP_FontAsset.CreateFontAsset(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf"),0,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
        if(font==null)font=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f=>f!=null && f.HasCharacter((int)'Я'));
        if(font==null) throw new InvalidOperationException("Шрифт игры ещё не загружен.");
        root=new GameObject("DivineLabs.UI"); Object.DontDestroyOnLoad(root);
        var canvas=root.AddComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32760;
        scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=0.5f;
        root.AddComponent<GraphicRaycaster>();
        var opener=Button(root.transform,"DIVINELABS  •  F8",0,0,190,40,Toggle);
        var o=opener.GetComponent<RectTransform>();o.anchorMin=o.anchorMax=new Vector2(1,1);o.pivot=new Vector2(1,1);o.anchoredPosition=new Vector2(-16,-16);
        overlay=Node(root.transform,"Overlay",0,0,0,0);
        var or=overlay.GetComponent<RectTransform>();or.anchorMin=Vector2.zero;or.anchorMax=Vector2.one;or.offsetMin=or.offsetMax=Vector2.zero;
        overlay.AddComponent<Image>().color=new Color(0,0,0,0.72f);
        var panel=Node(overlay.transform,"Panel",0,0,600,480);var pr=panel.GetComponent<RectTransform>();panelRect=pr;pr.anchorMin=pr.anchorMax=new Vector2(.5f,.5f);pr.pivot=new Vector2(.5f,.5f);pr.anchoredPosition=Vector2.zero;
        panel.AddComponent<Image>().color=Background;
        Label(panel.transform,"DIVINELABS",24,20,480,36,28,Accent);
        Label(panel.transform,"IDLE SLAYER  /  НАСТРОЙКИ",24,60,520,28,17,Color.white);
        Button(panel.transform,"×",546,18,32,32,Toggle);
        enabledText=Button(panel.transform,"",24,104,552,38,()=>{ TrySave(!Mod.Config.Enabled); }).GetComponentInChildren<TextMeshProUGUI>();
        Label(panel.transform,"Монеты • множитель дохода",24,160,540,27,19,Color.white);
        coins=Field(panel.transform,24,194,128,38);
        Presets(panel.transform,168,194,true);
        Label(panel.transform,"Урон • обычные враги",24,248,540,27,19,Color.white);
        damage=Field(panel.transform,24,282,128,38);
        Presets(panel.transform,168,282,false);
        Button(panel.transform,"Применить",24,340,264,42,()=>TrySave(Mod.Config.Enabled));
        Button(panel.transform,"×100 / ×1000",304,340,272,42,()=>{coins.text="100";damage.text="1000";TrySave(Mod.Config.Enabled);});
        Button(panel.transform,"−",24,397,36,28,()=>Scale(-.1f));
        Button(panel.transform,"+",66,397,36,28,()=>Scale(.1f));
        Label(panel.transform,"Масштаб меню",112,397,200,28,16,Color.white);
        status=Label(panel.transform,"Изменения действуют сразу. Цены остаются обычными.",24,435,552,30,15,Color.white);
        Refresh(); overlay.SetActive(false);Open=false;
        Mod.Instance.LoggerInstance.Msg("Меню DivineLabs создано. Открыть: F8.");
    }
    private static void Scale(float delta)
    { try {var c=Copy();c.UiScale=Math.Clamp(c.UiScale+delta,.75f,1.5f);Mod.Save(c);Refresh();}catch(Exception e){status.text=e.Message;} }
    private static Settings Copy()=>new(){Enabled=Mod.Config.Enabled,CoinMultiplier=Mod.Config.CoinMultiplier,DamageMultiplier=Mod.Config.DamageMultiplier,UiScale=Mod.Config.UiScale};
    private static void Refresh()
    { coins.text=Mod.Config.CoinMultiplier.ToString(CultureInfo.InvariantCulture);damage.text=Mod.Config.DamageMultiplier.ToString(CultureInfo.InvariantCulture);enabledText.text=Mod.Config.Enabled?"Множители включены  •  выключить":"Множители выключены  •  включить";scaler.referenceResolution=new Vector2(1280,720);FitPanel(); }
    private static void TrySave(bool enabled)
    {
        try
        { var c=Copy();c.Enabled=enabled;c.CoinMultiplier=double.Parse(coins.text.Replace(',','.'),CultureInfo.InvariantCulture);c.DamageMultiplier=double.Parse(damage.text.Replace(',','.'),CultureInfo.InvariantCulture);Mod.Save(c);Refresh();status.text="Сохранено. Настройки применены."; }
        catch(Exception e){status.text=e is FormatException?"Введите числа от 1 до 1 000 000.":e.Message;}
    }
    private static void FitPanel(){if(panelRect==null)return;var canvasRect=root.GetComponent<RectTransform>();float fit=Math.Min((canvasRect.rect.width-24)/600,(canvasRect.rect.height-24)/480);float scale=Math.Min(Mod.Config.UiScale,fit);panelRect.localScale=new Vector3(scale,scale,1);}
    internal static void Tick(){ if(root!=null){FitPanel();if(WindowHotkey.F8Pressed())Toggle();} }
    private static void Toggle()
    {
        Open=!Open;overlay.SetActive(Open);
        if(Open){cursorVisible=Cursor.visible;cursorLock=Cursor.lockState;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;Refresh();}
        else{Cursor.visible=cursorVisible;Cursor.lockState=cursorLock;}
    }
    private static void Presets(Transform parent,float x,float y,bool money)
    { double[] values={1,10,100,1000}; for(int i=0;i<values.Length;i++){double v=values[i];Button(parent,"×"+v,x+i*104,y,96,38,()=>{if(money)coins.text=v.ToString(CultureInfo.InvariantCulture);else damage.text=v.ToString(CultureInfo.InvariantCulture);TrySave(Mod.Config.Enabled);});} }
    private static GameObject Node(Transform parent,string name,float x,float y,float w,float h)
    { var go=new GameObject(name);var rect=go.AddComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=new Vector2(x,-y);rect.sizeDelta=new Vector2(w,h);return go; }
    private static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size,Color color)
    { var go=Node(parent,"Text",x,y,w,h);var t=go.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.raycastTarget=false;t.enableWordWrapping=true;return t; }
    private static GameObject Button(Transform parent,string text,float x,float y,float w,float h,Action action)
    { var go=Node(parent,"Button",x,y,w,h);var image=go.AddComponent<Image>();image.color=Surface;var b=go.AddComponent<Button>();b.targetGraphic=image;b.onClick.AddListener((UnityAction)action);var t=Label(go.transform,text,6,2,w-12,h-4,17,Color.white);t.alignment=TextAlignmentOptions.Center;return go; }
    private static TMP_InputField Field(Transform parent,float x,float y,float w,float h)
    {
        var go=Node(parent,"Number",x,y,w,h);var img=go.AddComponent<Image>();img.color=Surface;
        var viewport=Node(go.transform,"Viewport",8,3,w-16,h-6);viewport.AddComponent<RectMask2D>();
        var text=Label(viewport.transform,"",0,0,w-16,h-6,20,Color.white);text.alignment=TextAlignmentOptions.MidlineLeft;
        var field=go.AddComponent<TMP_InputField>();field.textViewport=viewport.GetComponent<RectTransform>();field.textComponent=text;field.targetGraphic=img;field.contentType=TMP_InputField.ContentType.DecimalNumber;field.characterLimit=12;field.caretColor=Accent;field.customCaretColor=true;return field;
    }
}
