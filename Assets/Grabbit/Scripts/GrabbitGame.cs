using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Grabbit
{
    public enum GameScreen { Title, StageSelect, Playing, Maker }
    public sealed class GrabbitGame : MonoBehaviour
    {
        public int startingLevel;
        public GameSession Session { get; private set; }
        public int LevelIndex { get; private set; }
        public bool IsAnimating { get; private set; }
        public BoardView View { get; private set; }
        public int AnimationToken { get; private set; }
        public GameScreen Screen { get; private set; }
        public bool MenuOpen { get; private set; }
        public bool IsCompleting { get; private set; }
        public GrabbitUI UI { get; private set; }
        readonly System.Collections.Generic.HashSet<string> completed=new System.Collections.Generic.HashSet<string>();
        public GrabbitMapMaker Maker { get; private set; }
        public bool TestingMap { get; private set; }
        Level activeLevel;
        int inputBlockedFrame;
        Camera cameraRig;
        Transform generated;
        AudioSource audioSource;
        AudioClip stepSound,slamSound,turnSound,deniedSound,clearSound;
        float yaw,pitch,zoom=1;
        bool dragging;
        void Awake() { Initialize(); }
        void OnEnable() { if(Application.isPlaying && Session==null) Initialize(); }
        public void Initialize()
        {
            if(Session!=null)return;
            Levels.RefreshMaps();
            for(int i=transform.childCount-1;i>=0;i--) Dispose(transform.GetChild(i).gameObject);
            audioSource=gameObject.GetComponent<AudioSource>();
            if(audioSource==null)audioSource=gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake=false;audioSource.volume=.12f;
            if(Application.isPlaying)
            {
                stepSound=Tone("footstep",420,.065f);slamSound=Tone("slam",96,.19f);turnSound=Tone("gravity",680,.2f);deniedSound=Tone("blocked",145,.1f);clearSound=Tone("beacon",880,.5f);
            }
            var ui=new GameObject("Screen flow",typeof(RectTransform));ui.transform.SetParent(transform,false);
            UI=ui.AddComponent<GrabbitUI>();UI.Build(this);
            Maker=gameObject.AddComponent<GrabbitMapMaker>();Maker.Initialize(this);
            if(Levels.All.Length>0)LoadLevel(Mathf.Clamp(startingLevel,0,Levels.All.Length-1));
            else LoadBoard(Levels.BuiltIn[0]);
            ShowTitle();
        }
        void Dispose(GameObject go) { go.SetActive(false);if(Application.isPlaying)Destroy(go);else DestroyImmediate(go); }
        public void LoadLevel(int index)
        {
            if(Levels.All.Length==0){ShowStageSelection();return;}
            TestingMap=false;
            LevelIndex=Mathf.Clamp(index,0,Levels.All.Length-1);startingLevel=LevelIndex;
            LoadBoard(Levels.All[LevelIndex]);
        }
        void LoadBoard(Level level)
        {
            CancelAnimation();
            if(generated!=null)Dispose(generated.gameObject);
            generated=new GameObject("Generated mechanism lab").transform;generated.SetParent(transform,false);
            activeLevel=level;Session=new GameSession(level.start);dragging=false;
            var camObj=new GameObject("Expedition Camera");camObj.transform.SetParent(generated,false);
            cameraRig=camObj.AddComponent<Camera>();camObj.tag="MainCamera";cameraRig.orthographic=true;cameraRig.clearFlags=CameraClearFlags.SolidColor;
            cameraRig.backgroundColor=BoardView.Navy;cameraRig.nearClipPlane=.1f;cameraRig.farClipPlane=150;
            camObj.AddComponent<AudioListener>();
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.42f,.51f,.64f);RenderSettings.fog=false;
            Light("Key / warm",new Vector3(38,150,0),new Color(1,.91f,.76f),1.7f);
            Light("Rim / cool",new Vector3(135,-50,0),new Color(.42f,.72f,1),1.4f);
            var board=new GameObject("Puzzle / integer grid");board.transform.SetParent(generated,false);View=board.AddComponent<BoardView>();
            View.Build(Session.Current,cameraRig);
            ResetCamera();View.Space(generated,cameraRig);
            SetScreen(GameScreen.Playing);
        }
        string StageKey(int index) { return Levels.All[index].mapId??("builtin-"+index); }
        public bool IsStageComplete(int index) { return index>=0&&index<Levels.All.Length&&completed.Contains(StageKey(index)); }
        public void RefreshMaps() { Levels.RefreshMaps();UI.RebuildStages(); }
        public void ShowMapMaker()
        {
            CancelAnimation();TestingMap=false;SetScreen(GameScreen.Maker);Maker.Open();
        }
        public void TestMap(GrabbitMap map)
        {
            if(!map.TryBoard(out var board,out _))return;
            Maker.PersistDraft();TestingMap=true;
            LoadBoard(new Level(map.title,board) { pitch=map.pitch,yaw=map.yaw });
        }
        void SetScreen(GameScreen next)
        {
            Screen=next;MenuOpen=false;dragging=false;inputBlockedFrame=Time.frameCount;
            if(generated!=null)generated.gameObject.SetActive(next!=GameScreen.Maker);
            if(Maker!=null)Maker.SetVisible(next==GameScreen.Maker);
            ClearPreview();UI.Show(next,false);
            PositionCamera();
            if(next==GameScreen.Title&&cameraRig!=null)cameraRig.orthographicSize*=1.45f;
        }
        public void ShowTitle() { LeaveGame(GameScreen.Title); }
        public void ShowStageSelection() { LeaveGame(GameScreen.StageSelect); }
        void LeaveGame(GameScreen next)
        {
            CancelAnimation();
            if(View!=null&&Session!=null){View.Snap(Session.Current);View.arrowsRoot.gameObject.SetActive(true);}
            SetScreen(next);
        }
        public void OpenMenu()
        {
            if(Screen!=GameScreen.Playing||MenuOpen)return;
            MenuOpen=true;dragging=false;ClearPreview();UI.Show(Screen,true);
        }
        public void ResumeGame()
        {
            if(Screen!=GameScreen.Playing||!MenuOpen)return;
            MenuOpen=false;dragging=false;inputBlockedFrame=Time.frameCount;UI.Show(Screen,false);
        }
        void Light(string name,Vector3 angles,Color color,float intensity)
        {
            var go=new GameObject(name);go.transform.SetParent(generated,false);go.transform.rotation=Quaternion.Euler(angles);
            var light=go.AddComponent<Light>();light.type=LightType.Directional;light.color=color;light.intensity=intensity;
            light.shadows=LightShadows.Soft;light.shadowStrength=.75f;light.shadowBias=.03f;
        }
        public void ResetCamera() { yaw=activeLevel.yaw;pitch=activeLevel.pitch;zoom=1;PositionCamera(); }
        void PositionCamera()
        {
            if(cameraRig==null||View==null)return;
            var rotation=Quaternion.Euler(pitch,yaw,0);
            var inverse=Quaternion.Inverse(rotation);
            var low=new Vector3(float.MaxValue,float.MaxValue,0);
            var high=new Vector3(float.MinValue,float.MinValue,0);
            Action<Vector3,float> include=(point,padding)=>
            {
                var p=inverse*(point-View.center);
                low.x=Mathf.Min(low.x,p.x-padding);low.y=Mathf.Min(low.y,p.y-padding);
                high.x=Mathf.Max(high.x,p.x+padding);high.y=Mathf.Max(high.y,p.y+padding);
            };
            var current=Session.Current;
            foreach(var block in current.blocks.Values) include(block.cell,.72f);
            include(current.rabbit.cell,1.1f);
            include(current.GoalBlock().cell+current.goal.normal,.95f);
            float aspect=Mathf.Max(.5f,cameraRig.aspect);
            float ortho=Mathf.Max(2.6f,Mathf.Max((high.y-low.y)/1.6f,(high.x-low.x)/(aspect*1.6f)))*zoom;
            var framing=new Vector3((low.x+high.x)*.5f,(low.y+high.y)*.5f,0);
            cameraRig.transform.rotation=rotation;
            cameraRig.transform.position=View.center+rotation*framing-rotation*Vector3.forward*30;
            cameraRig.orthographicSize=ortho;
        }
        void Update()
        {
            if(Session==null)return;
            if(Screen==GameScreen.Maker)return;
            var k=Keyboard.current;
            if(k!=null&&k.escapeKey.wasPressedThisFrame)
            {
                if(Screen==GameScreen.Playing){if(MenuOpen)ResumeGame();else OpenMenu();}
                else if(Screen==GameScreen.StageSelect)ShowTitle();
                return;
            }
            if(Time.frameCount<=inputBlockedFrame)return;
            if(Screen==GameScreen.Title)
            {
                if(k!=null&&k.enterKey.wasPressedThisFrame)ShowStageSelection();
                return;
            }
            if(Screen==GameScreen.StageSelect)
            {
                if(k!=null)for(int i=0;i<Mathf.Min(9,Levels.All.Length);i++)
                    if(k[(Key)((int)Key.Digit1+i)].wasPressedThisFrame){LoadLevel(i);return;}
                return;
            }
            if(MenuOpen)return;
            if(k!=null)
            {
                // Screen changes and modal dialogs never feed inputs into gameplay.
                if(k.rKey.wasPressedThisFrame) { Restart();return; }
                if(k.zKey.wasPressedThisFrame) { Undo();return; }
                if(k.cKey.wasPressedThisFrame)ResetCamera();
                if(!IsAnimating&&!IsCompleting)
                {
                    if(k.spaceKey.wasPressedThisFrame)Perform(Command.Slam);
                    else if(k.wKey.wasPressedThisFrame)Perform(Command.W);
                    else if(k.aKey.wasPressedThisFrame)Perform(Command.A);
                    else if(k.sKey.wasPressedThisFrame)Perform(Command.S);
                    else if(k.dKey.wasPressedThisFrame)Perform(Command.D);
                    else if(k.leftShiftKey.wasPressedThisFrame||k.rightShiftKey.wasPressedThisFrame)ShowPreview(Command.Slam);
                    if(k.leftShiftKey.wasReleasedThisFrame||k.rightShiftKey.wasReleasedThisFrame)ClearPreview();
                }
            }
            var m=Mouse.current;
            if(m!=null)
            {
                if(m.leftButton.wasPressedThisFrame)dragging=!UI.PointerOverMenuButton(m.position.ReadValue());
                if(m.leftButton.wasReleasedThisFrame)dragging=false;
                if(dragging&&m.leftButton.isPressed)
                {
                    var d=m.delta.ReadValue();yaw+=d.x*.20f;pitch=Mathf.Clamp(pitch-d.y*.16f,-70,75);PositionCamera();
                }
                var scroll=m.scroll.ReadValue().y;
                if(Mathf.Abs(scroll)>.01f){zoom=Mathf.Clamp(zoom-scroll*.0008f,.67f,1.5f);PositionCamera();}
            }
        }
        void KeepRabbitVisible()
        {
            if(cameraRig==null||View==null)return;
            var v=cameraRig.WorldToViewportPoint(View.rabbit.position+View.rabbit.up*.5f);
            float dx=v.x-Mathf.Clamp(v.x,.16f,.84f),dy=v.y-Mathf.Clamp(v.y,.18f,.82f);
            cameraRig.transform.position+=cameraRig.transform.right*(dx*2*cameraRig.orthographicSize*cameraRig.aspect)
                +cameraRig.transform.up*(dy*2*cameraRig.orthographicSize);
        }
        public bool Perform(Command command)
        {
            if(Screen!=GameScreen.Playing||MenuOpen||IsAnimating||IsCompleting)return false;
            ClearPreview();
            var result=Session.Act(command);
            if(!result.Success){Sound(deniedSound);return false;}
            IsAnimating=true;Sound(command==Command.Slam?slamSound:stepSound);
            int token=++AnimationToken;
            StartCoroutine(Animate(result,token));
            return true;
        }
        IEnumerator Animate(ActionResult result,int token)
        {
            View.arrowsRoot.gameObject.SetActive(false);
            foreach(var motion in result.motions)
            {
                float duration=motion.kind==MotionKind.Rotate?.32f:motion.kind==MotionKind.Blocks?.28f:.22f;
                if(motion.kind==MotionKind.Rotate)Sound(turnSound);
                for(float elapsed=0;elapsed<duration;)
                {
                    if(token!=AnimationToken)yield break;
                    if(MenuOpen){yield return null;continue;}
                    float t=Mathf.SmoothStep(0,1,elapsed/duration);
                    if(motion.kind==MotionKind.Blocks)
                    {
                        // Rabbit and supporting run share exactly the same translation and clock.
                        foreach(var p in motion.blockTo) View.blocks[p.Key].position=Vector3.Lerp(motion.blockFrom[p.Key],p.Value,t);
                        View.rabbit.position=Vector3.Lerp(BoardView.RabbitPosition(motion.before),BoardView.RabbitPosition(motion.after),t);
                        View.rabbit.rotation=BoardView.Rotation(motion.before);
                        View.goal.position=View.blocks[result.state.goal.blockId].position+(Vector3)result.state.goal.normal*.5f;
                    }
                    else if(motion.kind==MotionKind.Rotate)
                    {
                        Vector3 axis=motion.before.down==-motion.after.down?(Vector3)motion.before.forward:(Vector3)Axes.Cross(motion.before.down,motion.after.down);
                        float angle=motion.before.down==-motion.after.down?180:90;
                        var turn=Quaternion.AngleAxis(angle*t,axis);
                        View.rabbit.rotation=turn*BoardView.Rotation(motion.before);
                        View.rabbit.position=(Vector3)motion.after.cell+turn*(Vector3)motion.before.down*.47f;
                    }
                    else
                    {
                        View.rabbit.position=Vector3.Lerp(BoardView.RabbitPosition(motion.before),BoardView.RabbitPosition(motion.after),t);
                        View.rabbit.rotation=Quaternion.Slerp(BoardView.Rotation(motion.before),BoardView.Rotation(motion.after),t);
                        if(motion.kind==MotionKind.Walk)View.rabbit.position+=(Vector3)motion.before.Up*(Mathf.Sin(t*Mathf.PI)*.075f);
                    }
                    KeepRabbitVisible();
                    // Start measuring after the first rendered pose, excluding the stage-load frame.
                    yield return null;
                    if(!MenuOpen)elapsed+=Time.deltaTime;
                }
                while(MenuOpen)yield return null;
                if(token!=AnimationToken)yield break;
                if(motion.kind==MotionKind.Blocks)foreach(var p in motion.blockTo)View.blocks[p.Key].position=p.Value;
                View.rabbit.position=BoardView.RabbitPosition(motion.after);View.rabbit.rotation=BoardView.Rotation(motion.after);
            }
            if(token!=AnimationToken)yield break;
            View.Snap(Session.Current);View.arrowsRoot.gameObject.SetActive(true);IsAnimating=false;
            KeepRabbitVisible();
            if(Session.Current.cleared)
            {
                IsCompleting=true;Sound(clearSound);
                for(float elapsed=0;elapsed<.7f;)
                {
                    if(token!=AnimationToken)yield break;
                    if(MenuOpen){yield return null;continue;}
                    yield return null;
                    if(!MenuOpen)elapsed+=Time.deltaTime;
                }
                while(MenuOpen)yield return null;
                if(token!=AnimationToken)yield break;
                if(!TestingMap)completed.Add(activeLevel.mapId??StageKey(LevelIndex));ShowStageSelection();
            }
        }
        void CancelAnimation() { ++AnimationToken;StopAllCoroutines();IsAnimating=false;IsCompleting=false; }
        public void Undo()
        {
            if(Screen!=GameScreen.Playing||MenuOpen)return;
            CancelAnimation();Session?.Undo();ClearPreview();
            if(View==null)return;
            View.Snap(Session.Current);View.arrowsRoot.gameObject.SetActive(true);
            KeepRabbitVisible();
        }
        public void Restart()
        {
            if(Screen!=GameScreen.Playing||MenuOpen)return;
            CancelAnimation();Session.Restart();ClearPreview();
            View.Snap(Session.Current);View.arrowsRoot.gameObject.SetActive(true);ResetCamera();
        }
        public void ShowPreview(Command command)
        {
            if(Screen!=GameScreen.Playing||MenuOpen||IsAnimating||IsCompleting)return;
            View.Preview(Session.Preview(command));
        }
        public void ClearPreview() { if(View!=null)View.Preview(null); }
        void Sound(AudioClip clip) { if(clip!=null&&audioSource!=null)audioSource.PlayOneShot(clip); }
        AudioClip Tone(string name,float frequency,float duration)
        {
            int rate=22050,length=(int)(rate*duration);var samples=new float[length];
            for(int i=0;i<length;i++)
            {
                float t=(float)i/rate;
                float envelope=Mathf.Sin(Mathf.PI*i/length)*Mathf.Exp(-t*7);
                samples[i]=(Mathf.Sin(t*frequency*Mathf.PI*2)+.25f*Mathf.Sin(t*frequency*2*Mathf.PI*2))*envelope*.35f;
            }
            var clip=AudioClip.Create(name,length,1,rate,false);clip.SetData(samples,0);return clip;
        }
        public string ViewConsistency()
        {
            if(IsAnimating)return "Animating";
            foreach(var b in Session.Current.blocks.Values)if(Vector3.Distance(View.blocks[b.id].position,b.cell)>.001f)return "Block "+b.id+" mismatch";
            if(Vector3.Distance(View.rabbit.position,BoardView.RabbitPosition(Session.Current.rabbit))>.001f)return "Rabbit position mismatch";
            if(Quaternion.Angle(View.rabbit.rotation,BoardView.Rotation(Session.Current.rabbit))>.05f)return "Rabbit frame mismatch";
            if(Vector3.Distance(View.goal.position,(Vector3)Session.Current.GoalBlock().cell+(Vector3)Session.Current.goal.normal*.5f)>.001f)return "Goal mismatch";
            return "OK";
        }
        void OnDestroy()
        {
            foreach(var clip in new[]{stepSound,slamSound,turnSound,deniedSound,clearSound})if(clip!=null)Destroy(clip);
        }
    }
}
