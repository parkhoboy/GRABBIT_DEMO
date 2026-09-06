using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Grabbit
{
    public enum MapBrush { Box, Erase, Rabbit, Flag }

    public sealed class GrabbitMapMaker : MonoBehaviour
    {
        public GrabbitMap Map { get; private set; }
        public MapBrush Brush { get; set; }
        public int Layer { get; set; }
        // 0 = X/Z at Y, 1 = X/Y at Z, 2 = Z/Y at X.
        public int Plane { get; set; }
        public string Status { get; private set; }="";
        public bool Visible { get; private set; }
        public int UndoCount => undo.Count;
        GrabbitGame game;
        readonly List<string> undo=new List<string>(),redo=new List<string>();
        bool dirtyPreview=true,library;
        bool trashLibrary;
        Vector2 libraryScroll;
        List<GrabbitMap> libraryMaps;
        int centerU,centerV;
        Vector3Int? lastPaint;
        Font font;
        GUIStyle label,small,heading,button,field;
        Transform previewRoot,model;
        Camera previewCamera;
        Material blockMaterial,rabbitMaterial,flagMaterial,darkMaterial;
        Color mint=new Color(.40f,.86f,.71f),navy=new Color(.025f,.05f,.075f),panel=new Color(.06f,.10f,.14f);
        const float Width=1600,Height=900;
        public void Initialize(GrabbitGame target) { game=target; }
        public void Open()
        {
            if(Map==null)Map=GrabbitMapStore.LoadDraft();
            Status="블록을 놓고, 토끼와 깃발을 선택해 배치하세요.";
            dirtyPreview=true;
        }
        public void SetVisible(bool value)
        {
            if(Visible&&!value)PersistDraft();
            Visible=value;
            if(previewRoot!=null)previewRoot.gameObject.SetActive(value);
            if(value)dirtyPreview=true;
        }
        public void PersistDraft()
        {
            if(Map==null||game==null)return;
            try { GrabbitMapStore.SaveDraft(Map); }
            catch(Exception e){Status="임시 저장 실패: "+e.Message;Debug.LogWarning(Status);}
        }
        void Remember()
        {
            undo.Add(JsonUtility.ToJson(Map));if(undo.Count>100)undo.RemoveAt(0);redo.Clear();
        }
        public void ReplaceMap(GrabbitMap next)
        {
            if(Map!=null)Remember();Map=next.Copy();CenterOnRabbit();Changed();
        }
        void CenterOnRabbit()
        {
            Vector3Int p=Map.hasRabbit?Map.rabbit.cell:Vector3Int.zero;
            Layer=Plane==0?p.y:Plane==1?p.z:p.x;centerU=Plane==2?p.z:p.x;centerV=Plane==0?p.z:p.y;
        }
        void Changed() { dirtyPreview=true;PersistDraft(); }
        public void UndoEdit()
        {
            if(undo.Count==0)return;redo.Add(JsonUtility.ToJson(Map));
            Map=JsonUtility.FromJson<GrabbitMap>(undo[undo.Count-1]);undo.RemoveAt(undo.Count-1);Changed();
        }
        public void RedoEdit()
        {
            if(redo.Count==0)return;undo.Add(JsonUtility.ToJson(Map));
            Map=JsonUtility.FromJson<GrabbitMap>(redo[redo.Count-1]);redo.RemoveAt(redo.Count-1);Changed();
        }
        public bool Paint(Vector3Int cell)
        {
            if(Math.Abs((long)cell.x)>10000||Math.Abs((long)cell.y)>10000||Math.Abs((long)cell.z)>10000){Status="제작 좌표 범위는 -10,000 ~ 10,000입니다.";return false;}
            bool occupied=Map.boxes.Contains(cell);
            if(Brush==MapBrush.Box)
            {
                if(occupied)return false;
                if(Map.hasRabbit&&cell==Map.rabbit.cell){Status="토끼가 있는 칸에는 블록을 놓을 수 없습니다.";return false;}
                if(Map.boxes.Count>=2048){Status="블록은 최대 2,048개입니다.";return false;}
                Remember();Map.boxes.Add(cell);
            }
            else if(Brush==MapBrush.Erase)
            {
                if(!occupied&&(!Map.hasRabbit||Map.rabbit.cell!=cell))return false;
                Remember();Map.boxes.Remove(cell);
                if(Map.hasFlag&&Map.flagBlock==cell)Map.hasFlag=false;
                if(Map.hasRabbit&&Map.rabbit.cell==cell)Map.hasRabbit=false;
            }
            else if(Brush==MapBrush.Rabbit)
            {
                var target=occupied?cell-Map.rabbit.down:cell;
                if(Map.boxes.Contains(target)){Status="토끼가 설 칸이 막혀 있습니다.";return false;}
                if(Map.hasRabbit&&Map.rabbit.cell==target)return false;
                Remember();Map.hasRabbit=true;Map.rabbit.cell=target;
            }
            else
            {
                if(!occupied){Status="깃발을 붙일 블록을 클릭하세요.";return false;}
                if(Map.boxes.Contains(cell+Map.flagNormal)){Status="깃발 앞의 칸을 비우거나 다른 면을 선택하세요.";return false;}
                if(Map.hasFlag&&Map.flagBlock==cell)return false;
                Remember();Map.hasFlag=true;Map.flagBlock=cell;
            }
            Status="배치됨  "+cell;Changed();return true;
        }
        public void SetDown(Vector3Int down)
        {
            if(!Axes.Unit(down)||Map.rabbit.down==down)return;
            Remember();Map.rabbit.Turn(down);Changed();
        }
        public void SetForward(Vector3Int forward)
        {
            if(!Axes.Unit(forward)||Axes.Dot(forward,Map.rabbit.down)!=0||Map.rabbit.forward==forward)return;
            Remember();Map.rabbit.forward=forward;Changed();
        }
        public void SetFlagNormal(Vector3Int normal)
        {
            if(!Axes.Unit(normal)||Map.flagNormal==normal)return;
            Remember();Map.flagNormal=normal;Changed();
        }
        public bool SaveMap()
        {
            try
            {
                GrabbitMapStore.Save(Map);PersistDraft();game.RefreshMaps();
                bool valid=Map.TryBoard(out _,out var message);
                Status=valid?"저장했습니다. 스테이지 선택의 숫자 카드에 추가됩니다.":"초안 저장됨 · "+message;
                return true;
            }
            catch(Exception e){Status="저장 실패: "+e.Message;return false;}
        }
        public void OpenLibrary(bool trash=false)
        {
            trashLibrary=trash;library=true;libraryScroll=Vector2.zero;
            try { libraryMaps=GrabbitMapStore.Library(trash); }
            catch(Exception e){libraryMaps=new List<GrabbitMap>();Status="목록을 읽지 못했습니다: "+e.Message;}
        }
        public bool TrashMap(string id)
        {
            try
            {
                if(!GrabbitMapStore.Trash(id)){Status="이미 삭제되었거나 저장되지 않은 맵입니다.";return false;}
                // Keep the editable draft, but a later Save creates a separate map.
                if(Map!=null&&Map.id==id){Map.id="";PersistDraft();}
                game.RefreshMaps();libraryMaps=GrabbitMapStore.Library(trashLibrary);
                Status="삭제했습니다. 휴지통에서 복구할 수 있습니다.";return true;
            }
            catch(Exception e){Status="삭제 실패: "+e.Message;return false;}
        }
        public bool RestoreMap(string id)
        {
            try
            {
                if(!GrabbitMapStore.Restore(id)){Status="휴지통에서 맵을 찾지 못했습니다.";return false;}
                game.RefreshMaps();libraryMaps=GrabbitMapStore.Library(trashLibrary);
                Status="복구했습니다. 맵 목록에서 다시 선택할 수 있습니다.";return true;
            }
            catch(Exception e){Status="복구 실패: "+e.Message;return false;}
        }
        public bool ImportJson(string json)
        {
            if(!GrabbitMap.TryParse(json,out var next,out var message)){Status=message;return false;}
            next.id="";ReplaceMap(next);Status="공유 맵을 가져왔습니다. 저장하면 새 맵으로 추가됩니다.";return true;
        }
        public bool TestMap()
        {
            if(!Map.TryBoard(out _,out var message)){Status=message;return false;}
            game.TestMap(Map);return true;
        }
        void Styles()
        {
            if(label!=null)return;
            font=Font.CreateDynamicFontFromOSFont(new[]{"Malgun Gothic","Arial"},32);
            label=new GUIStyle(GUI.skin.label){font=font,fontSize=20,alignment=TextAnchor.MiddleLeft,wordWrap=true};label.normal.textColor=new Color(.92f,.96f,.96f);
            small=new GUIStyle(label){fontSize=16};small.normal.textColor=new Color(.61f,.73f,.77f);
            heading=new GUIStyle(label){fontSize=30,fontStyle=FontStyle.Bold};
            button=new GUIStyle(label){fontSize=18,alignment=TextAnchor.MiddleCenter};
            field=new GUIStyle(GUI.skin.textField){font=font,fontSize=20,padding=new RectOffset(10,10,7,4)};
        }
        void Fill(Rect rect,Color color) { GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,true,0,color,0,7); }
        void Text(Rect rect,string text,bool tiny=false) { GUI.Label(rect,text,tiny?small:label); }
        bool Btn(Rect rect,string text,bool selected=false,bool enabled=true)
        {
            Color color=selected?mint:new Color(.12f,.19f,.24f);if(!enabled)color*=.55f;
            Fill(rect,color);button.normal.textColor=selected?navy:(enabled?Color.white:new Color(.45f,.53f,.57f));
            bool old=GUI.enabled;GUI.enabled=old&&enabled;bool click=GUI.Button(rect,text,button);GUI.enabled=old;return click;
        }
        void OnGUI()
        {
            if(!Visible||Map==null)return;
            Styles();var old=GUI.matrix;GUI.matrix=Matrix4x4.Scale(new Vector3(Screen.width/Width,Screen.height/Height,1));
            GUI.depth=-200;
            bool wasEnabled=GUI.enabled;GUI.enabled=!library;
            // Leave the right-hand camera viewport uncovered for the live 3D preview.
            Fill(new Rect(0,0,1600,148),navy);Fill(new Rect(0,145,768,755),navy);
            Fill(new Rect(768,145,832,80),navy);Fill(new Rect(768,680,832,220),navy);Fill(new Rect(1578,225,22,455),navy);
            GUI.Label(new Rect(28,14,230,48),"맵 제작",heading);
            string name=GUI.TextField(new Rect(190,22,350,38),Map.title??"",64,field);
            if(name!=Map.title){Map.title=name;PersistDraft();}
            Text(new Rect(564,22,590,40),"블록 "+Map.boxes.Count+"   ·   변경 내용은 초안으로 자동 보관",true);
            if(Btn(new Rect(1330,20,112,42),"스테이지"))game.ShowStageSelection();
            if(Btn(new Rect(1452,20,120,42),"홈"))game.ShowTitle();
            if(Btn(new Rect(28,82,104,44),"새 맵"))ReplaceMap(new GrabbitMap());
            if(Btn(new Rect(144,82,112,44),"불러오기"))OpenLibrary();
            if(Btn(new Rect(268,82,100,44),"저장",true))SaveMap();
            if(Btn(new Rect(380,82,128,44),"다른 이름 저장")){Remember();Map.id="";SaveMap();}
            if(Btn(new Rect(520,82,122,44),"JSON 복사")){GUIUtility.systemCopyBuffer=JsonUtility.ToJson(Map,true);Status="맵 JSON을 클립보드에 복사했습니다.";}
            if(Btn(new Rect(654,82,136,44),"JSON 가져오기"))ImportJson(GUIUtility.systemCopyBuffer);
            if(Btn(new Rect(812,82,108,44),"되돌리기",false,undo.Count>0))UndoEdit();
            if(Btn(new Rect(932,82,108,44),"다시 하기",false,redo.Count>0))RedoEdit();
            bool valid=Map.TryBoard(out _,out var validation);
            if(Btn(new Rect(1382,82,190,44),"▶  테스트",true))TestMap();
            Fill(new Rect(20,146,732,717),panel);
            string[] brushes={"블록","지우개","토끼","깃발"};
            for(int i=0;i<4;i++)if(Btn(new Rect(36+i*174,162,160,44),brushes[i],(int)Brush==i))Brush=(MapBrush)i;
            Text(new Rect(38,216,64,38),"단면");
            string[] planes={"XZ","XY","ZY"};
            for(int i=0;i<3;i++)if(Btn(new Rect(104+i*64,216,58,36),planes[i],Plane==i)){Plane=i;CenterOnRabbit();}
            string axis=Plane==0?"Y":Plane==1?"Z":"X";
            if(Btn(new Rect(324,216,38,36),"−"))Layer=Mathf.Max(-10000,Layer-1);
            Text(new Rect(373,216,120,36),axis+" = "+Layer);
            if(Btn(new Rect(486,216,38,36),"+"))Layer=Mathf.Min(10000,Layer+1);
            if(Btn(new Rect(550,216,174,36),"토끼 위치로"))CenterOnRabbit();
            DrawGrid();
            if(Btn(new Rect(40,798,55,34),"←"))centerU-=5;
            if(Btn(new Rect(101,798,55,34),"→"))centerU+=5;
            if(Btn(new Rect(162,798,55,34),"↓"))centerV-=5;
            if(Btn(new Rect(223,798,55,34),"↑"))centerV+=5;
            Text(new Rect(300,791,421,56),"층 변경으로 높이 조절 · 드래그로 연속 배치\n토끼: 빈칸 또는 발밑 블록 / 깃발: 블록 클릭",true);
            Text(new Rect(796,157,760,44),"3D 미리보기",false);
            if(Btn(new Rect(1300,163,50,36),"↶")){Map.yaw-=30;dirtyPreview=true;}
            if(Btn(new Rect(1356,163,50,36),"↷")){Map.yaw+=30;dirtyPreview=true;}
            if(Btn(new Rect(1412,163,70,36),"위")){Map.pitch=Mathf.Clamp(Map.pitch+15,-75,75);dirtyPreview=true;}
            if(Btn(new Rect(1488,163,70,36),"아래")){Map.pitch=Mathf.Clamp(Map.pitch-15,-75,75);dirtyPreview=true;}
            Text(new Rect(796,688,130,36),"토끼 발 방향");DirectionRow(930,688,Map.rabbit.down,SetDown);
            Text(new Rect(796,730,130,36),"토끼 시선");DirectionRow(930,730,Map.rabbit.forward,SetForward,true);
            Text(new Rect(796,772,130,36),"깃발 면");DirectionRow(930,772,Map.flagNormal,SetFlagNormal);
            Text(new Rect(796,817,760,44),valid?validation:"배치 확인: "+validation,true);
            Text(new Rect(28,863,1530,34),Status,true);
            GUI.enabled=wasEnabled;if(library)DrawLibrary();
            GUI.matrix=old;
        }
        void DirectionRow(float x,float y,Vector3Int current,Action<Vector3Int> select,bool forward=false)
        {
            for(int i=0;i<6;i++)if(Btn(new Rect(x+i*102,y,92,34),Axes.Names[i],current==Axes.All[i],!forward||Axes.Dot(Axes.All[i],Map.rabbit.down)==0))select(Axes.All[i]);
        }
        public Vector3Int GridCell(int u,int v) { return Plane==0?new Vector3Int(u,Layer,v):Plane==1?new Vector3Int(u,v,Layer):new Vector3Int(Layer,v,u); }
        void DrawGrid()
        {
            const int count=17;const float tile=29;
            float x0=130,y0=282;
            var e=Event.current;int hoveredU=-1,hoveredV=-1;
            for(int u=0;u<count;u++)for(int v=0;v<count;v++)
            {
                var cell=GridCell(centerU+u-8,centerV+8-v);
                var r=new Rect(x0+u*tile,y0+v*tile,tile-2,tile-2);
                bool box=Map.boxes.Contains(cell),rabbit=Map.hasRabbit&&Map.rabbit.cell==cell,flag=Map.hasFlag&&Map.flagBlock==cell;
                Color c=box?new Color(.22f,.55f,.48f):new Color(.09f,.15f,.20f);
                if(cell==Vector3Int.zero&&!box)c=new Color(.18f,.26f,.32f);
                if(rabbit)c=new Color(.94f,.93f,.85f);if(flag)c=new Color(.96f,.67f,.28f);
                if(r.Contains(e.mousePosition)){c=Color.Lerp(c,Color.white,.25f);hoveredU=u;hoveredV=v;}
                Fill(r,c);
                if(rabbit||flag){button.normal.textColor=navy;GUI.Label(r,rabbit?"R":"F",button);}
            }
            string horizontal=Plane==2?"Z":"X",vertical=Plane==0?"Z":"Y";
            Text(new Rect(x0,253,490,27),horizontal+"  "+(centerU-8)+"  …  "+(centerU+8)+"       /       "+vertical+"  "+(centerV-8)+"  …  "+(centerV+8),true);
            Text(new Rect(38,470,85,70),"+"+vertical+" ↑\n+"+horizontal+" →",true);
            if(e.type==EventType.MouseUp)lastPaint=null;
            if(!library&&hoveredU>=0&&(e.type==EventType.MouseDown||e.type==EventType.MouseDrag)&&e.button==0)
            {
                var cell=GridCell(centerU+hoveredU-8,centerV+8-hoveredV);
                if(lastPaint!=cell){Paint(cell);lastPaint=cell;}e.Use();
            }
            if(hoveredU>=0)Text(new Rect(630,370,106,140),GridCell(centerU+hoveredU-8,centerV+8-hoveredV).ToString(),true);
        }
        void DrawLibrary()
        {
            Fill(new Rect(0,0,1600,900),new Color(0,0,0,.80f));Fill(new Rect(390,148,820,600),panel);
            GUI.Label(new Rect(422,168,630,48),"맵 불러오기",heading);
            if(Btn(new Rect(1120,168,60,40),"×"))library=false;
            if(Btn(new Rect(422,221,166,38),"맵 목록",!trashLibrary))OpenLibrary();
            if(Btn(new Rect(600,221,166,38),"휴지통",trashLibrary))OpenLibrary(true);
            libraryMaps=libraryMaps??new List<GrabbitMap>();
            if(libraryMaps.Count==0)Text(new Rect(430,310,690,60),trashLibrary?"휴지통이 비어 있습니다.":"맵이 없습니다. 새 맵을 만들거나 휴지통에서 복구하세요.");
            libraryScroll=GUI.BeginScrollView(new Rect(416,274,770,402),libraryScroll,new Rect(0,0,730,libraryMaps.Count*58));
            for(int i=0;i<libraryMaps.Count;i++)
            {
                var map=libraryMaps[i];bool builtIn=map.id.StartsWith("builtin-",StringComparison.Ordinal);
                if(Btn(new Rect(4,i*58,574,48),map.title+(builtIn&&!trashLibrary?" 복사":""),false,!trashLibrary))
                {
                    var copy=map.Copy();if(builtIn)copy.id="";
                    ReplaceMap(copy);library=false;Status="불러왔습니다. 이전 작업은 되돌리기로 복구할 수 있습니다.";
                }
                if(Btn(new Rect(590,i*58,128,48),trashLibrary?"복구":"삭제"))
                { if(trashLibrary)RestoreMap(map.id);else TrashMap(map.id);break; }
            }
            GUI.EndScrollView();
            Text(new Rect(422,692,754,40),Status,true);
        }
        void LateUpdate()
        {
            if(!Visible||Map==null)return;
            if(dirtyPreview){RebuildPreview();dirtyPreview=false;}
            if(previewCamera!=null)previewCamera.rect=new Rect(.48f,220f/900f,.50625f,455f/900f);
        }
        Material Material(Color color)
        {
            var source=Resources.Load<Material>("GrabbitLit");var material=new Material(source);material.color=color;return material;
        }
        Transform Shape(PrimitiveType type,string name,Transform parent,Vector3 position,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.layer=30;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
            go.GetComponent<Renderer>().sharedMaterial=material;Destroy(go.GetComponent<Collider>());return go.transform;
        }
        void RebuildPreview()
        {
            if(previewRoot==null)
            {
                previewRoot=new GameObject("Map maker preview").transform;previewRoot.SetParent(transform,false);
                var cameraObject=new GameObject("Map maker camera");cameraObject.transform.SetParent(previewRoot,false);
                previewCamera=cameraObject.AddComponent<Camera>();previewCamera.orthographic=true;previewCamera.cullingMask=1<<30;
                previewCamera.backgroundColor=navy;previewCamera.clearFlags=CameraClearFlags.SolidColor;previewCamera.nearClipPlane=.1f;previewCamera.farClipPlane=50000;
                cameraObject.AddComponent<AudioListener>();
                var lamp=new GameObject("Preview light");lamp.transform.SetParent(previewRoot,false);lamp.transform.rotation=Quaternion.Euler(38,145,0);
                var light=lamp.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.7f;light.cullingMask=1<<30;
                blockMaterial=Material(mint);rabbitMaterial=Material(new Color(.95f,.96f,.88f));flagMaterial=Material(new Color(1,.64f,.24f));darkMaterial=Material(new Color(.07f,.15f,.20f));
            }
            if(model!=null){model.gameObject.SetActive(false);Destroy(model.gameObject);}
            model=new GameObject("Draft blocks, rabbit and flag").transform;model.SetParent(previewRoot,false);
            foreach(var p in Map.boxes)Shape(PrimitiveType.Cube,"Box "+p,model,p,Vector3.one*.94f,blockMaterial);
            if(Map.hasRabbit)
            {
                var rabbit=new GameObject("Rabbit start").transform;rabbit.SetParent(model,false);rabbit.localPosition=Map.rabbit.cell;
                rabbit.localRotation=Axes.Unit(Map.rabbit.down)&&Axes.Unit(Map.rabbit.forward)&&Axes.Dot(Map.rabbit.down,Map.rabbit.forward)==0?BoardView.Rotation(Map.rabbit):Quaternion.identity;
                Shape(PrimitiveType.Capsule,"Body",rabbit,new Vector3(0,-.1f,0),new Vector3(.48f,.37f,.43f),rabbitMaterial);
                Shape(PrimitiveType.Sphere,"Head",rabbit,new Vector3(0,.27f,0),Vector3.one*.48f,rabbitMaterial);
                Shape(PrimitiveType.Sphere,"Visor / forward",rabbit,new Vector3(0,.29f,.2f),new Vector3(.37f,.21f,.15f),darkMaterial);
                for(int i=-1;i<=1;i+=2)Shape(PrimitiveType.Capsule,"Ear",rabbit,new Vector3(i*.13f,.61f,0),new Vector3(.13f,.26f,.13f),rabbitMaterial);
            }
            if(Map.hasFlag)
            {
                var flag=new GameObject("Flag surface").transform;flag.SetParent(model,false);flag.localPosition=Map.flagBlock+(Vector3)Map.flagNormal*.5f;
                flag.localRotation=Axes.Unit(Map.flagNormal)?Quaternion.FromToRotation(Vector3.up,Map.flagNormal):Quaternion.identity;
                Shape(PrimitiveType.Cylinder,"Pole",flag,new Vector3(0,.38f,0),new Vector3(.055f,.38f,.055f),rabbitMaterial);
                Shape(PrimitiveType.Cube,"Flag",flag,new Vector3(.18f,.6f,0),new Vector3(.37f,.27f,.055f),flagMaterial);
            }
            var points=Map.boxes.Select(p=>(Vector3)p).ToList();if(Map.hasRabbit)points.Add(Map.rabbit.cell);if(Map.hasFlag)points.Add(Map.flagBlock+Map.flagNormal);
            if(points.Count==0)points.Add(Vector3.zero);
            var rotation=Quaternion.Euler(Map.pitch,Map.yaw,0);var inverse=Quaternion.Inverse(rotation);
            var bounds=new Bounds(inverse*points[0],Vector3.one*2.4f);foreach(var p in points)bounds.Encapsulate(new Bounds(inverse*p,Vector3.one*2.4f));
            previewCamera.transform.rotation=rotation;previewCamera.transform.position=rotation*(bounds.center-Vector3.forward*(bounds.extents.z+30));
            float aspect=(Screen.width*.50625f)/(Screen.height*455f/900f);
            previewCamera.orthographicSize=Mathf.Max(2.8f,Mathf.Max(bounds.extents.y,bounds.extents.x/aspect)*1.12f);
        }
        void OnApplicationQuit() { PersistDraft(); }
        void OnDestroy()
        {
            if(font!=null)Destroy(font);
            foreach(var material in new[]{blockMaterial,rabbitMaterial,flagMaterial,darkMaterial})if(material!=null)Destroy(material);
            if(previewRoot!=null)Destroy(previewRoot.gameObject);
        }
    }
}
