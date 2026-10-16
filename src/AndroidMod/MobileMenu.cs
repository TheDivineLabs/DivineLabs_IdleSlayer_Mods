using System.Globalization;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object=UnityEngine.Object;
namespace DivineLabs.IdleSlayer;
// Android-only surface: no user32, keyboard hooks, Windows fonts or external overlay permission.
internal static class GameMenu
{
    private static readonly Color Background=new(.063f,.078f,.11f,1),Surface=new(.102f,.133f,.188f,1),Accent=new(.447f,.812f,1,1),Muted=new(.643f,.69f,.761f,1);
    private static GameObject root,widgetRoot,overlay,functions,resources,preferences,content,widget,activeList;
    private static RectTransform panel,widgetRect,listRect;
    private static TMP_FontAsset font;
    private static CanvasGroup fade,widgetFade,listFade;
    private static TMP_InputField search,amount,materialSearch;
    private static TextMeshProUGUI status,global,categoryText,resourceChoice,balances,materialHint,listText,visibleText,listToggle,motionText,opacityText,scaleText;
    private static readonly List<Card> cards=new();
    private static readonly List<Button> buttons=new();
    private static readonly string[] categories=new[]{"Все категории"}.Concat(FeatureCatalog.All.Select(f=>f.Category).Distinct()).ToArray();
    private static bool favorites,refreshing;
    private static readonly MobileTouchState touchState=new();
    private static Vector2 dragOffset;
    private static long revision=-1;
    private static int width,height,layoutFrames,lastRelease=-1,category,resourceIndex,materialIndex;
    private static Rect safeArea;
    private static float transition=-1,startAlpha,targetAlpha;
    private static Task<SessionReply> pending;
    private static SessionRequest pendingRequest;
    private static GameState state;
    private static MaterialState[] materials=Array.Empty<MaterialState>();
    private static string receipt;
    internal static bool Open {get;private set;}
    internal static bool BlocksGameInput=>Open||Input.touchCount>=3||touchState.BlocksInput(Time.frameCount)||Time.frameCount==lastRelease||PointerOnWidget();
    private sealed class Card{internal FeatureDefinition Feature;internal GameObject Node;internal TMP_InputField Input;internal TextMeshProUGUI Toggle,Favorite,Error;}
    internal static void Create()
    {
        if(root!=null)return;
        font=TMP_Settings.defaultFontAsset??Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f=>f!=null&&f.atlasTexture!=null);
        if(font==null)throw new InvalidOperationException("Игровой шрифт не загружен; меню DivineLabs создать не удалось.");
        GameVisuals.Initialize();root=Canvas("DivineLabs.Android.Menu",32760);widgetRoot=Canvas("DivineLabs.Android.Widget",32761);
        overlay=Node(root.transform,"Overlay",0,0,0,0);var fill=overlay.GetComponent<RectTransform>();fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;overlay.AddComponent<Image>().color=new Color(0,0,0,.6f);
        var body=Node(overlay.transform,"Panel",0,0,736,1100);panel=body.GetComponent<RectTransform>();panel.anchorMin=panel.anchorMax=panel.pivot=new(.5f,.5f);panel.anchoredPosition=Vector2.zero;SurfaceImage(body,Background);fade=overlay.AddComponent<CanvasGroup>();
        GameVisuals.Logo(body.transform,20,12,68);Label(body.transform,"DivineLabs",100,18,460,60,34,Accent);Button(body.transform,"×",620,8,96,88,Toggle);
        Button(body.transform,"Функции",24,104,216,72,()=>Page(functions));Button(body.transform,"Ресурсы",260,104,216,72,()=>{Page(resources);Inspect();});Button(body.transform,"Настройки",496,104,216,72,()=>Page(preferences));
        functions=Node(body.transform,"Functions",24,196,688,740);resources=Node(body.transform,"Resources",24,196,688,740);preferences=Node(body.transform,"Settings",24,196,688,740);
        search=Field(functions.transform,0,0,688,72);search.onValueChanged.AddListener((UnityAction<string>)(_=>Filter()));
        categoryText=Button(functions.transform,"",0,88,432,72,()=>{category=(category+1)%categories.Length;categoryText.text=categories[category];Filter();}).GetComponentInChildren<TextMeshProUGUI>();
        Button(functions.transform,"Избранное",448,88,240,72,()=>{favorites=!favorites;Filter();});
        var viewport=Node(functions.transform,"Viewport",0,176,688,564);viewport.AddComponent<RectMask2D>();SurfaceImage(viewport,Background);
        content=Node(viewport.transform,"Cards",0,0,676,1000);var scroll=viewport.AddComponent<ScrollRect>();scroll.viewport=viewport.GetComponent<RectTransform>();scroll.content=content.GetComponent<RectTransform>();scroll.horizontal=false;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=36;
        foreach(var feature in FeatureCatalog.All)CreateCard(feature);
        balances=Label(resources.transform,"Обновите баланс после загрузки игры.",0,0,688,112,25,Muted);Button(resources.transform,"Обновить баланс",0,124,688,72,Inspect);
        resourceChoice=Button(resources.transform,"",0,212,688,72,()=>{resourceIndex=(resourceIndex+1)%ResourceChoice.All.Length;Choice();}).GetComponentInChildren<TextMeshProUGUI>();
        materialSearch=Field(resources.transform,0,300,384,72);materialSearch.onValueChanged.AddListener((UnityAction<string>)(_=>FilterMaterials()));
        Button(resources.transform,"‹",400,300,136,72,()=>MoveMaterial(-1));Button(resources.transform,"›",552,300,136,72,()=>MoveMaterial(1));
        materialHint=Label(resources.transform,"",0,390,688,104,24,Muted);Label(resources.transform,"Количество · например 100 или 1e12",0,506,688,44,25,Color.white);
        amount=Field(resources.transform,0,562,464,80);amount.text="100";Button(resources.transform,"Выдать",480,562,208,80,Grant);
        global=Button(preferences.transform,"",0,0,688,88,()=>Mod.Change(c=>c.Enabled=!c.Enabled)).GetComponentInChildren<TextMeshProUGUI>();
        visibleText=Button(preferences.transform,"",0,104,688,80,()=>Mod.Change(c=>c.WidgetVisible=!c.WidgetVisible)).GetComponentInChildren<TextMeshProUGUI>();
        listToggle=Button(preferences.transform,"",0,200,688,80,()=>Mod.Change(c=>c.ActiveListVisible=!c.ActiveListVisible)).GetComponentInChildren<TextMeshProUGUI>();
        motionText=Button(preferences.transform,"",0,296,688,80,()=>Mod.Change(c=>c.ReduceMotion=!c.ReduceMotion)).GetComponentInChildren<TextMeshProUGUI>();
        opacityText=Label(preferences.transform,"",0,392,400,72,25,Color.white);Button(preferences.transform,"−",432,392,112,72,()=>Mod.Change(c=>c.WidgetOpacity=Math.Clamp(c.WidgetOpacity-.1f,.2f,1)));Button(preferences.transform,"+",576,392,112,72,()=>Mod.Change(c=>c.WidgetOpacity=Math.Clamp(c.WidgetOpacity+.1f,.2f,1)));
        scaleText=Label(preferences.transform,"",0,488,400,72,25,Color.white);Button(preferences.transform,"−",432,488,112,72,()=>Mod.Change(c=>c.UiScale=Math.Clamp(c.UiScale-.05f,.75f,1.5f)));Button(preferences.transform,"+",576,488,112,72,()=>Mod.Change(c=>c.UiScale=Math.Clamp(c.UiScale+.05f,.75f,1.5f)));
        Button(preferences.transform,"Сбросить расположение виджета",0,584,688,88,()=>Mod.Change(c=>{c.WidgetX=1;c.WidgetY=0;c.WidgetVisible=true;}));
        status=Label(body.transform,"Меню: кнопка на виджете или касание тремя пальцами.",24,956,688,122,22,Muted);
        widget=Button(widgetRoot.transform,"≡",0,0,220,104,WidgetClick);widgetRect=widget.GetComponent<RectTransform>();widgetFade=widget.AddComponent<CanvasGroup>();GameVisuals.Logo(widget.transform,8,10,84);widget.GetComponentInChildren<TextMeshProUGUI>().rectTransform.anchoredPosition=new(52,-2);
        activeList=Node(widgetRoot.transform,"ActiveFunctions",0,0,376,100);listRect=activeList.GetComponent<RectTransform>();SurfaceImage(activeList,Background).raycastTarget=false;listFade=activeList.AddComponent<CanvasGroup>();listText=Label(activeList.transform,"",12,10,352,84,22,Color.white);listText.enableWordWrapping=true;
        fade.alpha=0;overlay.SetActive(false);Page(functions);Refresh();
    }
    private static void CreateCard(FeatureDefinition feature)
    {
        var card=new Card{Feature=feature,Node=Node(content.transform,feature.Id,0,0,676,feature.HasMultiplier?344:236)};SurfaceImage(card.Node,Surface);
        Label(card.Node.transform,feature.Title,16,12,402,76,28,Color.white);
        card.Favorite=Button(card.Node.transform,"",428,12,88,76,()=>Mod.Change(c=>c.Favorites=c.Favorites.Contains(feature.Id)?c.Favorites.Where(x=>x!=feature.Id).ToArray():c.Favorites.Append(feature.Id).ToArray())).GetComponentInChildren<TextMeshProUGUI>();
        card.Toggle=Button(card.Node.transform,"",532,12,128,76,()=>Mod.Change(c=>c.SetFeatureEnabled(feature.Id,!c.FeatureEnabled(feature.Id)))).GetComponentInChildren<TextMeshProUGUI>();
        Label(card.Node.transform,feature.Description,16,100,644,100,23,Muted);
        if(feature.HasMultiplier){card.Input=Field(card.Node.transform,16,210,252,76);card.Input.onEndEdit.AddListener((UnityAction<string>)(text=>Commit(card,text)));double[] presets={1,100,1000};for(int i=0;i<3;i++){double n=presets[i];Button(card.Node.transform,"×"+n,284+i*128,210,120,76,()=>Mod.Change(c=>c.SetMultiplier(feature.Id,n)));}}
        card.Error=Label(card.Node.transform,"",16,feature.HasMultiplier?292:202,644,44,20,new Color(1,.67f,.72f,1));cards.Add(card);
    }
    private static void Commit(Card card,string text)
    {
        if(refreshing)return;
        if(!double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n)||!Settings.Valid(n)){card.Error.text=Settings.MultiplierHint;return;}
        card.Error.text="";if(n!=Mod.Config.Multiplier(card.Feature.Id))Mod.Change(c=>c.SetMultiplier(card.Feature.Id,n));
    }
    private static void Filter()
    {
        if(search==null||content==null)return;string query=search.text??"";float y=0;
        foreach(var card in cards){bool show=(category==0||card.Feature.Category==categories[category])&&(!favorites||Mod.Config.Favorites.Contains(card.Feature.Id))&&(card.Feature.Title.Contains(query,StringComparison.OrdinalIgnoreCase)||card.Feature.Category.Contains(query,StringComparison.OrdinalIgnoreCase));card.Node.SetActive(show);if(show){var r=card.Node.GetComponent<RectTransform>();r.anchoredPosition=new(0,-y);y+=r.sizeDelta.y+16;}}
        content.GetComponent<RectTransform>().sizeDelta=new(676,Math.Max(564,y));
    }
    private static void Refresh()
    {
        if(root==null)return;refreshing=true;
        try{var c=Mod.Config;revision=c.Revision;foreach(var card in cards){card.Toggle.text=c.FeatureEnabled(card.Feature.Id)?"Вкл":"Выкл";card.Favorite.text=c.Favorites.Contains(card.Feature.Id)?"★":"☆";if(card.Input!=null&&!card.Input.isFocused)card.Input.text=c.Multiplier(card.Feature.Id).ToString(CultureInfo.InvariantCulture);}
            categoryText.text=categories[category];global.text=c.Enabled?"Все функции включены":"Все функции выключены";visibleText.text=c.WidgetVisible?"Виджет: показан":"Виджет: скрыт";listToggle.text=c.ActiveListVisible?"Активный список: показан":"Активный список: скрыт";motionText.text=c.ReduceMotion?"Анимации: минимум":"Анимации: плавные";opacityText.text="Прозрачность: "+Math.Round(c.WidgetOpacity*100)+"%";scaleText.text="Масштаб: "+Math.Round(c.UiScale*100)+"%";
            foreach(var button in buttons){var colors=button.colors;colors.fadeDuration=c.ReduceMotion?0:.12f;button.colors=colors;}
            widget.SetActive(c.WidgetVisible);widgetFade.alpha=c.WidgetOpacity;listFade.alpha=c.WidgetOpacity;var rows=FeatureCatalog.All.Where(f=>c.Enabled&&c.FeatureEnabled(f.Id)).Select(f=>FeatureCatalog.ActiveTitle(f.Id)+" — "+(f.HasMultiplier?"×"+Number(c.Multiplier(f.Id)):"вкл")).ToArray();listText.text=string.Join("\n",rows);listRect.sizeDelta=new(376,24+rows.Length*62);listText.rectTransform.sizeDelta=new(352,rows.Length*62);activeList.SetActive(c.ActiveListVisible&&rows.Length>0);Filter();layoutFrames=3;Fit();Choice();
        }finally{refreshing=false;}
    }
    private static string Number(double n){try{return Il2Cpp.AbbreviationUtility.AbbreviateNumber(n,false,1000);}catch{return n.ToString("G6",CultureInfo.InvariantCulture);}}
    private static void Page(GameObject page){functions.SetActive(page==functions);resources.SetActive(page==resources);preferences.SetActive(page==preferences);}
    private static void Toggle(){Open=!Open;if(!Open)lastRelease=Time.frameCount;startAlpha=fade.alpha;targetAlpha=Open?1:0;overlay.SetActive(true);fade.interactable=fade.blocksRaycasts=Open;if(Mod.Config.ReduceMotion){fade.alpha=targetAlpha;overlay.SetActive(Open);transition=-1;}else transition=0;}
    internal static void SetAvailable(bool available){if(root==null)return;if(!available){Open=false;touchState.Reset(Time.frameCount);overlay.SetActive(false);fade.alpha=0;transition=-1;}root.SetActive(available);widgetRoot.SetActive(available);if(available)Refresh();}
    private static void Fit()
    {
        width=Screen.width;height=Screen.height;safeArea=Screen.safeArea;var area=root.GetComponent<RectTransform>().rect;
        float availableWidth=area.width*safeArea.width/Math.Max(1,width),availableHeight=area.height*safeArea.height/Math.Max(1,height);
        float scale=Math.Max(.1f,Math.Min(Mod.Config.UiScale,Math.Min((availableWidth-24)/736,(availableHeight-24)/1100)));panel.localScale=new(scale,scale,1);panel.anchoredPosition=new((safeArea.center.x/Math.Max(1,width)-.5f)*area.width,(safeArea.center.y/Math.Max(1,height)-.5f)*area.height);
        var wr=widgetRoot.GetComponent<RectTransform>().rect;float left=safeArea.xMin/Math.Max(1,width)*wr.width+12,top=(1-safeArea.yMax/Math.Max(1,height))*wr.height+12;
        float w=Math.Max(0,wr.width*safeArea.width/Math.Max(1,width)-244),h=Math.Max(0,wr.height*safeArea.height/Math.Max(1,height)-128);if(!touchState.PointerId.HasValue)widgetRect.anchoredPosition=new(left+Mod.Config.WidgetX*w,-top-Mod.Config.WidgetY*h);PositionList();
    }
    private static void PositionList(){var area=widgetRoot.GetComponent<RectTransform>().rect;float y=-widgetRect.anchoredPosition.y+116;if(y+listRect.sizeDelta.y>area.height-12)y=-widgetRect.anchoredPosition.y-listRect.sizeDelta.y-12;listRect.anchoredPosition=new(Math.Clamp(widgetRect.anchoredPosition.x+220-376,12,Math.Max(12,area.width-388)),-Math.Clamp(y,12,Math.Max(12,area.height-listRect.sizeDelta.y-12)));}
    private const int MousePointer=-1;
    private static bool OnWidget(Vector2 point)=>widget!=null&&widget.activeInHierarchy&&RectTransformUtility.RectangleContainsScreenPoint(widgetRect,point,null);
    private static bool PointerOnWidget()
    {
        for(int i=0;i<Input.touchCount;i++)
        {
            var touch=Input.GetTouch(i);
            if(OnWidget(touch.position))return true;
        }
        return Input.touchCount==0&&Input.GetMouseButton(0)&&OnWidget(Input.mousePosition);
    }
    private static Vector2 CanvasPoint(Vector2 screen)
    {
        var parent=widgetRoot.GetComponent<RectTransform>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out Vector2 local);
        return new(local.x+parent.rect.width/2,parent.rect.height/2-local.y);
    }
    private static void BeginDrag(int id,Vector2 screen)
    {
        if(!OnWidget(screen))return;
        var point=CanvasPoint(screen);
        if(touchState.BeginPointer(id,point.x,point.y,Time.frameCount))
            dragOffset=point-new Vector2(widgetRect.anchoredPosition.x,-widgetRect.anchoredPosition.y);
    }
    private static void MoveDrag(int id,Vector2 screen)
    {
        var point=CanvasPoint(screen);
        if(!touchState.MovePointer(id,point.x,point.y))return;
        var area=widgetRoot.GetComponent<RectTransform>().rect;
        var safe=Screen.safeArea;
        float left=safe.xMin/Math.Max(1,Screen.width)*area.width+12;
        float right=safe.xMax/Math.Max(1,Screen.width)*area.width-232;
        float top=(1-safe.yMax/Math.Max(1,Screen.height))*area.height+12;
        float bottom=(1-safe.yMin/Math.Max(1,Screen.height))*area.height-116;
        Vector2 p=point-dragOffset;
        widgetRect.anchoredPosition=new(Math.Clamp(p.x,left,Math.Max(left,right)),-Math.Clamp(p.y,top,Math.Max(top,bottom)));
        PositionList();
    }
    private static void EndDrag(int id)
    {
        if(!touchState.EndPointer(id,Time.frameCount))return;
        var area=widgetRoot.GetComponent<RectTransform>().rect;
        var safe=Screen.safeArea;
        float left=safe.xMin/Math.Max(1,Screen.width)*area.width+12;
        float top=(1-safe.yMax/Math.Max(1,Screen.height))*area.height+12;
        float w=Math.Max(1,area.width*safe.width/Math.Max(1,Screen.width)-244);
        float h=Math.Max(1,area.height*safe.height/Math.Max(1,Screen.height)-128);
        float x=Math.Clamp((widgetRect.anchoredPosition.x-left)/w,0,1);
        float y=Math.Clamp((-widgetRect.anchoredPosition.y-top)/h,0,1);
        Mod.Change(c=>{c.WidgetX=x;c.WidgetY=y;});
    }
    private static void HandleTouches()
    {
        if(touchState.ObserveTouches(Input.touchCount,Time.frameCount))
        {
            layoutFrames=3;
            Toggle();
        }
        if(touchState.GestureActive)return;
        if(touchState.PointerId is int id)
        {
            if(id==MousePointer)
            {
                MoveDrag(id,Input.mousePosition);
                if(!Input.GetMouseButton(0))EndDrag(id);
                return;
            }
            for(int i=0;i<Input.touchCount;i++)
            {
                var touch=Input.GetTouch(i);
                if(touch.fingerId!=id)continue;
                if(touch.phase==TouchPhase.Canceled){touchState.CancelPointer(Time.frameCount);layoutFrames=3;}
                else{MoveDrag(id,touch.position);if(touch.phase==TouchPhase.Ended)EndDrag(id);}
                return;
            }
            // Android canceled or removed the owning pointer; do not adopt another finger.
            touchState.CancelPointer(Time.frameCount);layoutFrames=3;
            return;
        }
        for(int i=0;i<Input.touchCount;i++)
        {
            var touch=Input.GetTouch(i);
            if(touch.phase==TouchPhase.Began)BeginDrag(touch.fingerId,touch.position);
        }
        if(Input.touchCount==0&&Input.GetMouseButtonDown(0))BeginDrag(MousePointer,Input.mousePosition);
    }
    private static void WidgetClick()
    {
        // uGUI can deliver onClick before the loader's update callback for this frame.
        HandleTouches();
        if(touchState.ConsumeTap(Time.frameCount))Toggle();
    }
    internal static void Tick()
    {
        if(root==null||!Mod.GameSceneReady)return;if(revision!=Mod.Config.Revision)Refresh();if(width!=Screen.width||height!=Screen.height||safeArea!=Screen.safeArea){if(touchState.PointerId.HasValue)touchState.CancelPointer(Time.frameCount);layoutFrames=3;}if(layoutFrames>0){Fit();layoutFrames--;}
        if(transition>=0){transition+=Time.unscaledDeltaTime;float t=Math.Min(1,transition/.18f);fade.alpha=Mathf.Lerp(startAlpha,targetAlpha,1-(1-t)*(1-t)*(1-t));if(t>=1){transition=-1;overlay.SetActive(Open);}}
        HandleTouches();CompleteCommand();while(Mod.Messages.TryDequeue(out string message))status.text=message;
    }
    private static void Choice(){if(resourceChoice==null)return;resourceChoice.text=ResourceChoice.All[resourceIndex].Title+"  ›";bool material=ResourceChoice.All[resourceIndex].Kind==ResourceKind.Material;materialSearch.gameObject.SetActive(material);materialHint.text=material?(materials.Length==0?"Материалы не найдены.":materials[materialIndex].Title+" · "+Number(materials[materialIndex].Amount)+" / "+Number(materials[materialIndex].Maximum)):ResourceChoice.All[resourceIndex].Kind==ResourceKind.JewelsOfSoul?"Локальные заработанные камни. Перенос через облако не подтверждён.":"Множители не применяются повторно. USP и материалы сохраняют игровые ограничения.";}
    private static void MoveMaterial(int delta){if(materials.Length>0){materialIndex=(materialIndex+materials.Length+delta)%materials.Length;Choice();}}
    private static void FilterMaterials(){materials=state?.Materials.Where(m=>m.Title.Contains(materialSearch.text??"",StringComparison.OrdinalIgnoreCase)).ToArray()??Array.Empty<MaterialState>();materialIndex=0;Choice();}
    private static void Inspect(){if(pending!=null)return;if(pendingRequest!=null){status.text="Сначала проверьте результат прежней выдачи кнопкой «Выдать».";return;}receipt=null;Send(new());}
    private static void Send(GameCommand command){pendingRequest=new(){Session=Mod.Session.SessionId,Revision=Mod.Config.Revision,Command=command};pending=Mod.Session.Apply(pendingRequest);status.text="Выполнение…";}
    private static void Grant()
    {
        if(pending!=null)return;if(pendingRequest!=null){pending=Mod.Session.Apply(pendingRequest);return;}
        try{if(state==null)throw new InvalidDataException("Сначала обновите баланс.");if(!double.TryParse(amount.text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out double n))throw new InvalidDataException("Введите целое конечное количество от 1.");var command=new GameCommand{Operation="grant",Resource=ResourceChoice.All[resourceIndex].Kind,Amount=n,MaterialId=materials.Length>0?materials[materialIndex].Id:""};command.Validate();receipt=null;Send(command);}catch(Exception e){status.text=e.Message;}
    }
    private static void CompleteCommand()
    {
        if(pending==null||!pending.IsCompleted)return;
        try{var reply=pending.GetAwaiter().GetResult();bool grant=pendingRequest.Command.Operation=="grant";pending=null;pendingRequest=null;status.text=reply.Error??reply.Result?.Message??"Баланс обновлён.";
            if(reply.Result?.State is GameState s){state=s;balances.text=$"Монеты: {Number(s.Coins)}\nSP {Number(s.SlayerPoints)} · DP {Number(s.DivinityPoints)} · USP {Number(s.UltraSlayerPoints)}\nКамни душ: {Number(s.JewelsOfSoul)}";FilterMaterials();}
            if(grant){receipt=status.text;Send(new());}else if(receipt!=null){status.text=receipt;receipt=null;}
        }catch(Exception e){Mod.Instance.LoggerInstance.Error(e.ToString());pending=null;status.text="Результат не подтверждён. «Выдать» проверит прежнюю команду без повторного начисления.";}
    }
    private static GameObject Canvas(string name,int order){var go=new GameObject(name);Object.DontDestroyOnLoad(go);var c=go.AddComponent<Canvas>();c.renderMode=RenderMode.ScreenSpaceOverlay;c.sortingOrder=order;var scale=go.AddComponent<CanvasScaler>();scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new(736,1280);scale.matchWidthOrHeight=.5f;go.AddComponent<GraphicRaycaster>();return go;}
    private static GameObject Node(Transform parent,string name,float x,float y,float w,float h){var go=new GameObject(name);var r=go.AddComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new(0,1);r.anchoredPosition=new(x,-y);r.sizeDelta=new(w,h);return go;}
    private static Image SurfaceImage(GameObject go,Color color){var image=go.AddComponent<Image>();image.color=color;GameVisuals.Round(image);return image;}
    private static TextMeshProUGUI Label(Transform parent,string text,float x,float y,float w,float h,float size,Color color){var go=Node(parent,"Text",x,y,w,h);var t=go.AddComponent<TextMeshProUGUI>();t.font=font;t.fontSize=size;t.text=text;t.color=color;t.enableWordWrapping=true;t.raycastTarget=false;return t;}
    private static GameObject Button(Transform parent,string text,float x,float y,float w,float h,Action action){var go=Node(parent,"Button",x,y,w,h);var img=SurfaceImage(go,Surface);var button=go.AddComponent<Button>();button.targetGraphic=img;var colors=button.colors;colors.m_NormalColor=Color.white;colors.m_HighlightedColor=new(.8f,.9f,1,1);colors.m_PressedColor=new(.5f,.7f,.9f,1);colors.fadeDuration=.12f;button.colors=colors;button.onClick.AddListener((UnityAction)action);buttons.Add(button);var label=Label(go.transform,text,6,2,w-12,h-4,25,Color.white);label.alignment=TextAlignmentOptions.Center;return go;}
    private static TMP_InputField Field(Transform parent,float x,float y,float w,float h){var go=Node(parent,"Input",x,y,w,h);var image=SurfaceImage(go,Background);var viewport=Node(go.transform,"Viewport",12,4,w-24,h-8);viewport.AddComponent<RectMask2D>();var text=Label(viewport.transform,"",0,0,w-24,h-8,28,Color.white);text.alignment=TextAlignmentOptions.MidlineLeft;var field=go.AddComponent<TMP_InputField>();field.textViewport=viewport.GetComponent<RectTransform>();field.textComponent=text;field.targetGraphic=image;field.contentType=TMP_InputField.ContentType.Standard;field.characterLimit=340;field.caretColor=Accent;field.customCaretColor=true;return field;}
}
