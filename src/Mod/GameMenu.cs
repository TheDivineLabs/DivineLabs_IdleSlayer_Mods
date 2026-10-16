using System.Globalization;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DivineLabs.IdleSlayer;
internal static class GameMenu
{
    private static GameObject root,overlay,widgetRoot,widget,functions,preferences,content,activeList;
    private static TMP_FontAsset font;
    private static TMP_InputField search;
    private static TextMeshProUGUI status,global,visibleText,motionText,opacityText,scaleText,activeListText,activeListToggle;
    private static CanvasGroup fade,widgetFade;
    private static RectTransform panel,widgetRect,activeListRect;
    private static readonly List<Card> cards=new();
    private static readonly List<Button> buttons=new();
    private static readonly Dictionary<string,TextMeshProUGUI> bindLabels=new();
    private static readonly List<TMP_InputField> fields=new();
    private static readonly Color Background=new(.063f,.078f,.11f,1),Surface=new(.102f,.133f,.188f,1),Accent=new(.447f,.812f,1,1),Muted=new(.643f,.69f,.761f,1);
    private static bool refreshing,cursorVisible,favoritesOnly,settingsPage,dragging,previousMouse,dragMoved;
    private static CursorLockMode cursorLock;
    private static string capture;
    private static int categoryIndex;
    private static readonly string[] categories=new[]{"Все категории"}.Concat(FeatureCatalog.All.Select(f=>f.Category).Distinct()).ToArray();
    private static TextMeshProUGUI categoryLabel;
    private static long revision=-1;
    private static int width,height,layoutFrames,blockedReleaseFrame=-1;
    private static float transition=-1,fromAlpha,toAlpha;
    private static Vector2 dragOffset,dragOrigin;
    internal static bool Open{get;private set;}
    internal static bool BlocksGameInput=>Open||dragging||Time.frameCount==blockedReleaseFrame||PointerOnWidget;
    private static bool PointerOnWidget=>widget!=null&&widget.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(widgetRect,Input.mousePosition,null);
    private sealed class Card
    {
        internal FeatureDefinition Definition;internal GameObject Node;internal TMP_InputField Input;
        internal TextMeshProUGUI Toggle,Key,Error;internal Image Favorite;
    }
    internal static void Create()
    {
        if(root!=null)return;
        font=TMP_FontAsset.CreateFontAsset(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts),"segoeui.ttf"),0,64,8,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024);
        if(font==null)throw new InvalidOperationException("Шрифт интерфейса не загружен.");
        GameVisuals.Initialize();root=Canvas("DivineLabs.Menu",32760);widgetRoot=Canvas("DivineLabs.Widget",32761);
        overlay=Node(root.transform,"Overlay",0,0,0,0);Stretch(overlay);overlay.AddComponent<Image>().color=new Color(0,0,0,.60f);
        var body=Node(overlay.transform,"Panel",0,0,920,600);panel=body.GetComponent<RectTransform>();panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;var panelImage=body.AddComponent<Image>();panelImage.color=Background;GameVisuals.Round(panelImage);
        fade=overlay.AddComponent<CanvasGroup>();fade.alpha=0;
        GameVisuals.Logo(body.transform,24,15,42);Label(body.transform,"DivineLabs",78,18,700,32,23,Accent);
        Button(body.transform,"×",854,16,42,38,Toggle);
        var sidebar=Node(body.transform,"Sidebar",0,74,184,526);sidebar.AddComponent<Image>().color=new Color(.05f,.065f,.09f,1);
        Button(sidebar.transform,"Функции",16,20,152,42,()=>ShowPage(false));
        Button(sidebar.transform,"Ресурсы",16,72,152,42,ShowResources);
        Button(sidebar.transform,"Настройки",16,124,152,42,()=>ShowPage(true));
        Button(sidebar.transform,"Избранное",16,196,152,42,()=>{favoritesOnly=!favoritesOnly;ShowPage(false);Filter();});
        Label(sidebar.transform,"IDLE SLAYER\nDivineLabs 0.5.0",18,434,150,58,13,Muted);
        functions=Node(body.transform,"Functions",208,76,688,470);
        Label(functions.transform,"Функции",0,0,320,40,29,Color.white);
        global=Button(functions.transform,"",440,0,248,38,()=>Mod.Change(c=>c.Enabled=!c.Enabled)).GetComponentInChildren<TextMeshProUGUI>();
        categoryLabel=Button(functions.transform,"Все категории",464,52,224,38,()=>{categoryIndex=(categoryIndex+1)%categories.Length;categoryLabel.text=categories[categoryIndex];Filter();}).GetComponentInChildren<TextMeshProUGUI>();
        search=Field(functions.transform,0,52,448,38,false);search.onValueChanged.AddListener((UnityAction<string>)(_=>{if(!refreshing)Filter();}));
        var viewport=Node(functions.transform,"Viewport",0,104,688,354);viewport.AddComponent<RectMask2D>();viewport.AddComponent<Image>().color=Background;
        content=Node(viewport.transform,"Cards",0,0,676,500);
        var scroll=viewport.AddComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content.GetComponent<RectTransform>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
        foreach(var def in FeatureCatalog.All)BuildCard(def);
        ResourcePanel.Create(body.transform);
        preferences=Node(body.transform,"Settings",208,76,688,470);
        Label(preferences.transform,"Настройки",0,0,600,40,29,Color.white);
        Label(preferences.transform,"КЛАВИШИ",0,58,300,26,14,Accent);
        var keyViewport=Node(preferences.transform,"Keys",0,94,342,284);keyViewport.AddComponent<RectMask2D>();keyViewport.AddComponent<Image>().color=Background;
        var keyContent=Node(keyViewport.transform,"Bindings",0,0,338,FeatureCatalog.Actions.Length*48);
        var keyScroll=keyViewport.AddComponent<ScrollRect>();keyScroll.viewport=keyViewport.GetComponent<RectTransform>();keyScroll.content=keyContent.GetComponent<RectTransform>();keyScroll.horizontal=false;keyScroll.movementType=ScrollRect.MovementType.Clamped;
        for(int i=0;i<FeatureCatalog.Actions.Length;i++){
            string action=FeatureCatalog.Actions[i];float y=i*48;
            Label(keyContent.transform,FeatureCatalog.ActionTitle(action),0,y,164,44,14,Color.white);
            bindLabels[action]=Button(keyContent.transform,"",166,y,170,38,()=>Capture(action)).GetComponentInChildren<TextMeshProUGUI>();
        }
        Label(preferences.transform,"ВИДЖЕТ И ИНТЕРФЕЙС",366,58,310,26,14,Accent);
        visibleText=Button(preferences.transform,"",366,94,310,38,()=>Mod.Change(c=>c.WidgetVisible=!c.WidgetVisible)).GetComponentInChildren<TextMeshProUGUI>();
        motionText=Button(preferences.transform,"",366,142,310,38,()=>Mod.Change(c=>c.ReduceMotion=!c.ReduceMotion)).GetComponentInChildren<TextMeshProUGUI>();
        opacityText=Label(preferences.transform,"",366,194,220,30,17,Color.white);
        Button(preferences.transform,"−",588,190,38,36,()=>Mod.Change(c=>c.WidgetOpacity=Math.Clamp(c.WidgetOpacity-.05f,.2f,1)));
        Button(preferences.transform,"+",636,190,38,36,()=>Mod.Change(c=>c.WidgetOpacity=Math.Clamp(c.WidgetOpacity+.05f,.2f,1)));
        scaleText=Label(preferences.transform,"",366,242,220,30,17,Color.white);
        Button(preferences.transform,"−",588,238,38,36,()=>Mod.Change(c=>c.UiScale=Math.Clamp(c.UiScale-.05f,.75f,1.5f)));
        Button(preferences.transform,"+",636,238,38,36,()=>Mod.Change(c=>c.UiScale=Math.Clamp(c.UiScale+.05f,.75f,1.5f)));
        activeListToggle=Button(preferences.transform,"",366,294,310,38,()=>Mod.Change(c=>c.ActiveListVisible=!c.ActiveListVisible)).GetComponentInChildren<TextMeshProUGUI>();
        Button(preferences.transform,"Сбросить расположение",366,342,310,38,()=>Mod.Change(c=>{c.WidgetX=1;c.WidgetY=0;}));
        Label(preferences.transform,"Esc — отмена записи клавиши.\nBackspace — убрать назначение.",0,396,660,58,15,Muted);
        status=Label(body.transform,"Изменения применяются автоматически.",208,552,680,38,15,Muted);
        widget=Node(widgetRoot.transform,"Widget",0,0,138,46);widgetRect=widget.GetComponent<RectTransform>();var widgetImage=widget.AddComponent<Image>();widgetImage.color=Background;GameVisuals.Round(widgetImage);
        widgetFade=widget.AddComponent<CanvasGroup>();GameVisuals.Logo(widget.transform,10,3,40);
        Button(widget.transform,"≡",82,5,48,36,()=>{if(!dragMoved)Toggle();});
        activeList=Node(widgetRoot.transform,"ActiveFunctions",0,0,284,100);activeListRect=activeList.GetComponent<RectTransform>();
        var listBackground=activeList.AddComponent<Image>();listBackground.color=Background;listBackground.raycastTarget=false;GameVisuals.Round(listBackground);
        activeList.AddComponent<CanvasGroup>().alpha=Mod.Config.WidgetOpacity;
        activeListText=Label(activeList.transform,"",12,8,260,84,15,Color.white);activeListText.enableWordWrapping=false;
        Open=false;overlay.SetActive(false);ShowPage(false);WindowHotkey.Initialize();Refresh();
        Mod.Instance.LoggerInstance.Msg("Меню DivineLabs создано. Открыть: "+Mod.Config.Bindings["menu"]);
    }
    private static void BuildCard(FeatureDefinition def)
    {
        var go=Node(content.transform,def.Id,0,0,676,def.HasMultiplier?230:170);var surface=go.AddComponent<Image>();surface.color=Surface;GameVisuals.Round(surface);
        var card=new Card{Definition=def,Node=go};
        Label(go.transform,def.Title,20,16,400,30,24,Color.white);
        var favorite=Button(go.transform,"",550,12,46,36,()=>Mod.Change(c=>c.Favorites=c.Favorites.Contains(def.Id)?c.Favorites.Where(x=>x!=def.Id).ToArray():c.Favorites.Append(def.Id).ToArray()));card.Favorite=GameVisuals.Star(favorite.transform,10,5,26);
        card.Toggle=Button(go.transform,"",600,12,58,36,()=>Mod.Change(c=>{c.SetFeatureEnabled(def.Id,!c.FeatureEnabled(def.Id));})).GetComponentInChildren<TextMeshProUGUI>();
        Label(go.transform,def.Description,20,56,634,48,16,Muted);
        if(def.HasMultiplier){
        card.Input=Field(go.transform,20,112,140,40,true);
        card.Input.onEndEdit.AddListener((UnityAction<string>)(text=>Commit(card,text)));
        double[] presets={1,10,100,1000};
        for(int i=0;i<presets.Length;i++){double value=presets[i];Button(go.transform,"×"+value,178+i*120,112,110,40,()=>SetNumber(def.Id,value));}
        }
        card.Key=Button(go.transform,"",20,def.HasMultiplier?170:112,300,36,()=>Capture(def.Action)).GetComponentInChildren<TextMeshProUGUI>();
        card.Error=Label(go.transform,"",332,def.HasMultiplier?170:112,324,42,14,new Color(1,.67f,.72f,1));
        cards.Add(card);
    }
    private static void Commit(Card card,string text)
    {
        if(refreshing)return;
        if(!double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double value)||!Settings.Valid(value)){card.Error.text="Число от 1, например 1e60";return;}
        card.Error.text="";if(value!=Mod.Config.Multiplier(card.Definition.Id))SetNumber(card.Definition.Id,value);
    }
    private static void SetNumber(string id,double value)=>Mod.Change(c=>{c.SetMultiplier(id,value);});
    private static void Capture(string action){capture=action;status.text="Нажмите сочетание: "+FeatureCatalog.ActionTitle(action);WindowHotkey.Reset();}
    private static void Filter()
    {
        string query=search.text?.Trim()??"";float y=0;
        foreach(var card in cards){
            bool show=(categoryIndex==0||card.Definition.Category==categories[categoryIndex])&&(!favoritesOnly||Mod.Config.Favorites.Contains(card.Definition.Id))&&(card.Definition.Title.Contains(query,StringComparison.OrdinalIgnoreCase)||card.Definition.Category.Contains(query,StringComparison.OrdinalIgnoreCase));
            card.Node.SetActive(show);if(show){card.Node.GetComponent<RectTransform>().anchoredPosition=new Vector2(0,-y);y+=card.Node.GetComponent<RectTransform>().rect.height+16;}
        }
        content.GetComponent<RectTransform>().sizeDelta=new Vector2(676,Math.Max(y,354));
    }
    private static void ShowPage(bool settings)
    {
        settingsPage=settings;functions.SetActive(!settings);preferences.SetActive(settings);ResourcePanel.Hide();
    }
    private static void ShowResources(){functions.SetActive(false);preferences.SetActive(false);ResourcePanel.Show();}
    private static void Refresh()
    {
        if(root==null)return;refreshing=true;
        try{
            var c=Mod.Config;if(revision!=c.Revision)WindowHotkey.Reset();revision=c.Revision;
            global.text=c.Enabled?"● Все функции включены":"○ Все функции выключены";
            foreach(var card in cards){bool enabled=c.FeatureEnabled(card.Definition.Id);
                card.Toggle.text=enabled?"Вкл":"Выкл";card.Favorite.color=c.Favorites.Contains(card.Definition.Id)?Accent:Muted;
                if(card.Input!=null&&!card.Input.isFocused)card.Input.text=c.Multiplier(card.Definition.Id).ToString(CultureInfo.InvariantCulture);
                card.Key.text=c.Bindings[card.Definition.Action].ToString();
            }
            foreach(var action in FeatureCatalog.Actions)bindLabels[action].text=c.Bindings[action].ToString();
            visibleText.text=c.WidgetVisible?"Виджет: показан":"Виджет: скрыт";
            motionText.text=c.ReduceMotion?"Анимации: минимум":"Анимации: плавные";
            opacityText.text="Прозрачность · "+Math.Round(c.WidgetOpacity*100)+"%";
            scaleText.text="Масштаб · "+Math.Round(c.UiScale*100)+"%";
            foreach(var button in buttons){var colors=button.colors;colors.fadeDuration=c.ReduceMotion||WindowHotkey.ReducedMotion?0:.12f;button.colors=colors;}
            widget.SetActive(c.WidgetVisible);widgetFade.alpha=c.WidgetOpacity;
            activeListToggle.text=c.ActiveListVisible?"Активные функции: показаны":"Активные функции: скрыты";
            var rows=FeatureCatalog.All.Where(f=>c.Enabled&&c.FeatureEnabled(f.Id)).Select(f=>FeatureCatalog.ActiveTitle(f.Id)+" — "+(f.HasMultiplier?"×"+FormatMultiplier(c.Multiplier(f.Id)):"вкл")).ToArray();
            activeListText.text=string.Join("\n",rows);
            float listHeight=16+rows.Length*24;activeListRect.sizeDelta=new Vector2(284,listHeight);activeListText.rectTransform.sizeDelta=new Vector2(260,rows.Length*24);
            activeList.GetComponent<CanvasGroup>().alpha=c.WidgetOpacity;
            activeList.SetActive(c.ActiveListVisible&&rows.Length>0);
            layoutFrames=3;Fit();Filter();
        }finally{refreshing=false;}
    }
    private static void Fit()
    {
        width=Screen.width;height=Screen.height;
        var area=root.GetComponent<RectTransform>().rect;
        float scale=Math.Min(Mod.Config.UiScale,Math.Min((area.width-24)/920,(area.height-24)/600));
        panel.localScale=new Vector3(scale,scale,1);
        var wr=widgetRoot.GetComponent<RectTransform>().rect;
        if(!dragging)widgetRect.anchoredPosition=new Vector2(12+Mod.Config.WidgetX*Math.Max(0,wr.width-162),-12-Mod.Config.WidgetY*Math.Max(0,wr.height-70));
        PositionActiveList();
    }
    internal static void SetAvailable(bool available)
    {
        if(root==null)return;
        if(!available){if(Open)Toggle();dragging=false;capture=null;}
        root.SetActive(available);widgetRoot.SetActive(available);
        if(available){WindowHotkey.Reset();Refresh();}
    }
    internal static void Tick()
    {
        if(root==null||!Mod.GameSceneReady)return;
        if(revision!=Mod.Config.Revision)Refresh();
        if(width!=Screen.width||height!=Screen.height){width=Screen.width;height=Screen.height;layoutFrames=3;}
        if(layoutFrames>0){Fit();layoutFrames--;}
        bool typing=false;
        if(Open)foreach(var field in fields)if(field.isFocused){typing=true;break;}
        int actions=WindowHotkey.Poll(Mod.Config,typing,capture!=null,out var key);
        if(capture!=null&&key!=null){
            if(key.Key==27){capture=null;status.text="Назначение отменено.";}
            else{string action=capture;capture=null;if(key.Key==8)key=new();Mod.Change(c=>c.Bindings[action]=key);}
        }else{
            if((actions&1)!=0)Toggle();
            if((actions&2)!=0)Mod.Change(c=>c.WidgetVisible=!c.WidgetVisible);
            if((actions&4)!=0)Mod.Change(c=>c.Enabled=!c.Enabled);
            for(int i=3;i<FeatureCatalog.Actions.Length;i++){string id=FeatureCatalog.Actions[i];if((actions&(1<<i))!=0)Mod.Change(c=>c.SetFeatureEnabled(id,!c.FeatureEnabled(id)));}
        }
        if(transition>=0){
            transition+=Time.unscaledDeltaTime;float t=Math.Min(1,transition/.18f);t=1-(1-t)*(1-t)*(1-t);fade.alpha=Mathf.Lerp(fromAlpha,toAlpha,t);
            if(transition>=.18f){transition=-1;fade.alpha=toAlpha;if(!Open)overlay.SetActive(false);}
        }
        ResourcePanel.Tick();Drag();
        while(Mod.Messages.TryDequeue(out string message))status.text=message;
    }
    private static void Toggle()
    {
        Open=!Open;capture=null;
        if(Open){cursorVisible=Cursor.visible;cursorLock=Cursor.lockState;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;overlay.SetActive(true);Refresh();}
        else{Cursor.visible=cursorVisible;Cursor.lockState=cursorLock;}
        fade.interactable=fade.blocksRaycasts=Open;
        if(Mod.Config.ReduceMotion||WindowHotkey.ReducedMotion){transition=-1;fade.alpha=Open?1:0;overlay.SetActive(Open);}
        else{fromAlpha=fade.alpha;toAlpha=Open?1:0;transition=0;}
    }
    private static string FormatMultiplier(double value)
    {
        try{return Il2Cpp.AbbreviationUtility.AbbreviateNumber(value,false,1000);}
        catch{return value.ToString("G5",CultureInfo.InvariantCulture);}
    }
    private static void PositionActiveList()
    {
        var area=widgetRoot.GetComponent<RectTransform>().rect;
        float listHeight=activeListRect.sizeDelta.y,top=-widgetRect.anchoredPosition.y;
        float y=top+54;if(y+listHeight>area.height-12)y=top-listHeight-8;
        activeListRect.anchoredPosition=new Vector2(Math.Clamp(widgetRect.anchoredPosition.x+138-284,12,Math.Max(12,area.width-296)),-Math.Clamp(y,12,Math.Max(12,area.height-listHeight-12)));
    }
    private static void SaveWidgetPosition()
    {
        var area=widgetRoot.GetComponent<RectTransform>().rect;
        float x=(widgetRect.anchoredPosition.x-12)/Math.Max(1,area.width-162),y=(-widgetRect.anchoredPosition.y-12)/Math.Max(1,area.height-70);
        Mod.Change(c=>{c.WidgetX=Math.Clamp(x,0,1);c.WidgetY=Math.Clamp(y,0,1);});
    }
    private static void Drag()
    {
        bool down=Input.GetMouseButton(0);
        if(!WindowHotkey.Foreground||!widget.activeInHierarchy){
            if(dragging){dragging=false;dragMoved=true;SaveWidgetPosition();}
            previousMouse=down;return;
        }
        if(!down&&!dragging){previousMouse=false;return;}
        var parent=widgetRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,Input.mousePosition,null,out Vector2 local);
        Vector2 point=new(local.x+parent.rect.width/2,parent.rect.height/2-local.y);
        if(down&&!previousMouse){
            dragMoved=false;
            if(PointerOnWidget){dragging=true;dragOrigin=point;dragOffset=point-new Vector2(widgetRect.anchoredPosition.x,-widgetRect.anchoredPosition.y);}
        }
        if(dragging){
            dragMoved|=(point-dragOrigin).sqrMagnitude>16;
            if(dragMoved){Vector2 p=point-dragOffset;widgetRect.anchoredPosition=new Vector2(Math.Clamp(p.x,12,Math.Max(12,parent.rect.width-150)),-Math.Clamp(p.y,12,Math.Max(12,parent.rect.height-58)));PositionActiveList();}
            if(!down){dragging=false;blockedReleaseFrame=Time.frameCount;if(dragMoved)SaveWidgetPosition();}
        }
        previousMouse=down;
    }
    private static GameObject Canvas(string name,int order)
    {
        var go=new GameObject(name);Object.DontDestroyOnLoad(go);var canvas=go.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=order;
        var scaler=go.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();return go;
    }
    private static void Stretch(GameObject go){var r=go.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
    internal static GameObject Node(Transform parent,string name,float x,float y,float w,float h){var go=new GameObject(name);var r=go.AddComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return go;}
    internal static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size,Color color){var go=Node(parent,"Text",x,y,w,h);var t=go.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=color;t.raycastTarget=false;t.enableWordWrapping=true;return t;}
    internal static GameObject Button(Transform parent,string text,float x,float y,float w,float h,Action action)
    {
        var go=Node(parent,"Button",x,y,w,h);var image=go.AddComponent<Image>();image.color=Surface;GameVisuals.Round(image);var b=go.AddComponent<Button>();b.targetGraphic=image;
        var colors=b.colors;colors.m_NormalColor=Color.white;colors.m_HighlightedColor=new Color(.7f,.85f,1,1);colors.m_PressedColor=new Color(.45f,.65f,.8f,1);colors.fadeDuration=.12f;b.colors=colors;
        buttons.Add(b);b.onClick.AddListener((UnityAction)action);var label=Label(go.transform,text,6,2,w-12,h-4,16,Color.white);label.alignment=TextAlignmentOptions.Center;return go;
    }
    internal static TMP_InputField Field(Transform parent,float x,float y,float w,float h,bool number)
    {
        var go=Node(parent,"Input",x,y,w,h);var img=go.AddComponent<Image>();img.color=new Color(.06f,.09f,.13f,1);GameVisuals.Round(img);
        var viewport=Node(go.transform,"Viewport",10,3,w-20,h-6);viewport.AddComponent<RectMask2D>();
        var text=Label(viewport.transform,"",0,0,w-20,h-6,number?21:18,Color.white);text.alignment=TextAlignmentOptions.MidlineLeft;
        var field=go.AddComponent<TMP_InputField>();field.textViewport=viewport.GetComponent<RectTransform>();field.textComponent=text;field.targetGraphic=img;field.contentType=TMP_InputField.ContentType.Standard;field.characterLimit=number?340:80;field.caretColor=Accent;field.customCaretColor=true;fields.Add(field);return field;
    }
}