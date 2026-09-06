using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Grabbit
{
    public sealed class GrabbitUI : MonoBehaviour
    {
        public RectTransform TitleRoot { get; private set; }
        public RectTransform StageRoot { get; private set; }
        public RectTransform GameRoot { get; private set; }
        public RectTransform PopupRoot { get; private set; }
        public Button StartButton { get; private set; }
        public Button HomeButton { get; private set; }
        public Button MenuButton { get; private set; }
        public Button ResumeButton { get; private set; }
        public Button StagesButton { get; private set; }
        public Button TitleButton { get; private set; }
        public Button CloseButton { get; private set; }
        public Button MakerButton { get; private set; }
        public Button EditMapButton { get; private set; }
        public Button[] StageButtons { get; private set; }
        readonly Color ink=new Color(.025f,.055f,.075f);
        readonly Color surface=new Color(.065f,.115f,.15f);
        readonly Color muted=new Color(.52f,.67f,.71f);
        readonly Color white=new Color(.93f,.97f,.95f);
        GrabbitGame game;
        Font font;
        GameObject[] checks;
        RectTransform stageCards;
        Button previousPage,nextPage;
        Button emptyMapsButton;
        int page;
        RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);
            r.anchorMin=r.anchorMax=new Vector2(.5f,.5f);r.sizeDelta=size;r.anchoredPosition=position;return r;
        }
        void Stretch(RectTransform r,Vector2 min,Vector2 max) { r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero; }
        RectTransform Group(string name)
        {
            var r=Rect(name,transform,Vector2.zero,Vector2.zero);Stretch(r,Vector2.zero,Vector2.one);return r;
        }
        GrabbitUIArt Art(string name,Transform parent,Vector2 size,Vector2 position,GrabbitUIArt.Symbol symbol,Color tint)
        {
            var r=Rect(name,parent,size,position);var graphic=r.gameObject.AddComponent<GrabbitUIArt>();graphic.symbol=symbol;graphic.color=tint;graphic.raycastTarget=false;return graphic;
        }
        Text Label(string name,Transform parent,string value,int size,Vector2 box,Vector2 pos,Color tint,TextAnchor alignment=TextAnchor.MiddleCenter)
        {
            var r=Rect(name,parent,box,pos);var label=r.gameObject.AddComponent<Text>();label.font=font;label.text=value;label.fontSize=size;
            label.color=tint;label.alignment=alignment;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;
        }
        void Anchor(RectTransform r,float x,float y,Vector2 offset) { r.anchorMin=r.anchorMax=new Vector2(x,y);r.anchoredPosition=offset; }
        void Backdrop(RectTransform parent,Color tint)
        {
            var g=Art("Backdrop",parent,Vector2.zero,Vector2.zero,GrabbitUIArt.Symbol.Panel,tint);g.radius=0;g.raycastTarget=true;Stretch(g.rectTransform,Vector2.zero,Vector2.one);
        }
        Button Button(string name,Transform parent,Vector2 size,Vector2 pos,Color background,UnityAction action)
        {
            var art=Art(name,parent,size,pos,GrabbitUIArt.Symbol.Panel,Color.white);art.raycastTarget=true;
            var b=art.gameObject.AddComponent<Button>();b.targetGraphic=art;
            var colors=b.colors;colors.normalColor=background;colors.highlightedColor=Color.Lerp(background,Color.white,.12f);colors.pressedColor=Color.Lerp(background,ink,.22f);colors.selectedColor=colors.highlightedColor;colors.fadeDuration=.1f;b.colors=colors;
            b.navigation=new Navigation { mode=Navigation.Mode.None };b.onClick.AddListener(action);return b;
        }
        void Icon(Button button,GrabbitUIArt.Symbol symbol,Color tint,Vector2 pos,float size=30)
        { Art("Icon",button.transform,Vector2.one*size,pos,symbol,tint); }
        Button MenuRow(string name,string label,float y,GrabbitUIArt.Symbol icon,bool primary,UnityAction action,Transform parent)
        {
            var b=Button(name,parent,new Vector2(424,66),new Vector2(0,y),primary?BoardView.Mint:surface,action);
            Icon(b,icon,primary?ink:BoardView.Mint,new Vector2(-164,0),28);
            Label("Label",b.transform,label,23,new Vector2(320,50),new Vector2(20,0),primary?ink:white).fontStyle=FontStyle.Bold;
            return b;
        }
        public void Build(GrabbitGame target)
        {
            game=target;font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},40);
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1600,900);scaler.matchWidthOrHeight=.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            var inputObject=new GameObject("Screen input");inputObject.transform.SetParent(transform,false);inputObject.AddComponent<EventSystem>();
            var input=inputObject.AddComponent<InputSystemUIInputModule>();input.AssignDefaultActions();input.move=null;input.submit=null;input.cancel=null;
            TitleRoot=Group("Title screen");Backdrop(TitleRoot,new Color(.012f,.026f,.045f,.55f));
            var title=Label("Logo",TitleRoot,"GRABBIT",100,new Vector2(900,130),Vector2.zero,white);title.fontStyle=FontStyle.Bold;Anchor(title.rectTransform,.5f,.84f,Vector2.zero);
            StartButton=Button("StartButton",TitleRoot,new Vector2(116,80),Vector2.zero,BoardView.Mint,game.ShowStageSelection);
            Anchor((RectTransform)StartButton.transform,.5f,.155f,Vector2.zero);Icon(StartButton,GrabbitUIArt.Symbol.Play,ink,Vector2.zero,38);
            MakerButton=Button("MapMakerButton",TitleRoot,Vector2.one*62,Vector2.zero,surface,game.ShowMapMaker);
            Anchor((RectTransform)MakerButton.transform,1,0,new Vector2(-68,64));Icon(MakerButton,GrabbitUIArt.Symbol.Edit,BoardView.Mint,Vector2.zero,32);

            StageRoot=Group("Stage selection");Backdrop(StageRoot,new Color(.025f,.05f,.075f,.98f));
            HomeButton=Button("HomeButton",StageRoot,Vector2.one*62,Vector2.zero,surface,game.ShowTitle);Anchor((RectTransform)HomeButton.transform,0,1,new Vector2(68,-64));Icon(HomeButton,GrabbitUIArt.Symbol.Home,white,Vector2.zero,30);
            var stageMaker=Button("MapMakerButton",StageRoot,Vector2.one*62,Vector2.zero,surface,game.ShowMapMaker);Anchor((RectTransform)stageMaker.transform,1,1,new Vector2(-68,-64));Icon(stageMaker,GrabbitUIArt.Symbol.Edit,BoardView.Mint,Vector2.zero,32);
            emptyMapsButton=Button("CreateFirstMap",StageRoot,Vector2.one*100,Vector2.zero,surface,game.ShowMapMaker);Icon(emptyMapsButton,GrabbitUIArt.Symbol.Edit,BoardView.Mint,Vector2.zero,48);
            previousPage=Button("PreviousPage",StageRoot,Vector2.one*60,new Vector2(-60,0),surface,()=>{page--;UpdateStagePage();});Anchor((RectTransform)previousPage.transform,.5f,.12f,new Vector2(-50,0));Icon(previousPage,GrabbitUIArt.Symbol.Back,white,Vector2.zero);
            nextPage=Button("NextPage",StageRoot,Vector2.one*60,new Vector2(60,0),surface,()=>{page++;UpdateStagePage();});Anchor((RectTransform)nextPage.transform,.5f,.12f,new Vector2(50,0));Icon(nextPage,GrabbitUIArt.Symbol.Back,white,Vector2.zero);nextPage.transform.Find("Icon").localRotation=Quaternion.Euler(0,0,180);
            RebuildStages();
            GameRoot=Group("Game icons");
            MenuButton=Button("MenuButton",GameRoot,Vector2.one*64,Vector2.zero,surface,game.OpenMenu);Anchor((RectTransform)MenuButton.transform,1,1,new Vector2(-62,-58));Icon(MenuButton,GrabbitUIArt.Symbol.Menu,white,Vector2.zero,31);
            EditMapButton=Button("ReturnToMapMaker",GameRoot,Vector2.one*64,Vector2.zero,surface,game.ShowMapMaker);Anchor((RectTransform)EditMapButton.transform,0,1,new Vector2(62,-58));Icon(EditMapButton,GrabbitUIArt.Symbol.Edit,BoardView.Mint,Vector2.zero,32);

            PopupRoot=Group("Pause popup");Backdrop(PopupRoot,new Color(.005f,.015f,.025f,.76f));
            var panel=Art("Dialog",PopupRoot,new Vector2(520,422),Vector2.zero,GrabbitUIArt.Symbol.Panel,new Color(.035f,.075f,.105f));panel.radius=25;panel.raycastTarget=true;
            Label("Heading",panel.transform,"메뉴",32,new Vector2(340,60),new Vector2(0,145),white);
            CloseButton=Button("CloseButton",panel.transform,Vector2.one*40,new Vector2(214,162),surface,game.ResumeGame);Icon(CloseButton,GrabbitUIArt.Symbol.Close,white,Vector2.zero,22);
            ResumeButton=MenuRow("ResumeButton","게임으로 돌아가기",54,GrabbitUIArt.Symbol.Play,true,game.ResumeGame,panel.transform);
            StagesButton=MenuRow("StagesButton","스테이지로 돌아가기",-30,GrabbitUIArt.Symbol.Grid,false,game.ShowStageSelection,panel.transform);
            TitleButton=MenuRow("TitleButton","메뉴로 돌아가기",-114,GrabbitUIArt.Symbol.Home,false,game.ShowTitle,panel.transform);
            Show(GameScreen.Title,false);
        }
        public bool PointerOverMenuButton(Vector2 position)
        { return GameRoot.gameObject.activeInHierarchy&&(RectTransformUtility.RectangleContainsScreenPoint((RectTransform)MenuButton.transform,position)
            ||(EditMapButton.gameObject.activeSelf&&RectTransformUtility.RectangleContainsScreenPoint((RectTransform)EditMapButton.transform,position))); }
        public void RebuildStages()
        {
            if(stageCards!=null){stageCards.gameObject.SetActive(false);Destroy(stageCards.gameObject);}
            stageCards=Rect("Stage cards",StageRoot,Vector2.zero,Vector2.zero);Stretch(stageCards,Vector2.zero,Vector2.one);
            StageButtons=new Button[Levels.All.Length];checks=new GameObject[StageButtons.Length];
            for(int i=0;i<StageButtons.Length;i++)
            {
                int index=i;var b=Button("Stage"+(i+1)+"Button",stageCards,Vector2.zero,Vector2.zero,surface,()=>game.LoadLevel(index));StageButtons[i]=b;
                float left=.075f+(i%5)*.174f;Stretch((RectTransform)b.transform,new Vector2(left,.33f),new Vector2(left+.155f,.67f));
                Label("Number",b.transform,(i+1).ToString("00"),64,new Vector2(190,100),Vector2.zero,white).fontStyle=FontStyle.Bold;
                var check=Art("Complete",b.transform,Vector2.one*27,Vector2.zero,GrabbitUIArt.Symbol.Check,BoardView.Mint);Anchor(check.rectTransform,1,1,new Vector2(-30,-36));checks[i]=check.gameObject;
            }
            UpdateStagePage();
        }
        void UpdateStagePage()
        {
            int last=(StageButtons.Length-1)/5;page=Mathf.Clamp(page,0,last);
            emptyMapsButton.gameObject.SetActive(StageButtons.Length==0);
            for(int i=0;i<StageButtons.Length;i++){StageButtons[i].gameObject.SetActive(i/5==page);checks[i].SetActive(game.IsStageComplete(i));}
            previousPage.gameObject.SetActive(last>0);nextPage.gameObject.SetActive(last>0);previousPage.interactable=page>0;nextPage.interactable=page<last;
        }
        public void Show(GameScreen screen,bool popup)
        {
            TitleRoot.gameObject.SetActive(screen==GameScreen.Title);StageRoot.gameObject.SetActive(screen==GameScreen.StageSelect);
            GameRoot.gameObject.SetActive(screen==GameScreen.Playing&&!popup);PopupRoot.gameObject.SetActive(screen==GameScreen.Playing&&popup);
            EditMapButton.gameObject.SetActive(game.TestingMap);
            if(EventSystem.current!=null)EventSystem.current.SetSelectedGameObject(null);
            UpdateStagePage();
        }
        void OnDestroy() { if(font!=null)Destroy(font); }
    }
}
