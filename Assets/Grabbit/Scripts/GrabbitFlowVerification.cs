#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Grabbit.Editor
{
    public sealed class GrabbitFlowVerification : MonoBehaviour
    {
        public bool Done;
        public string Report="Running";
        GrabbitGame game;
        Mouse mouse,previousMouse;
        Keyboard keyboard,previousKeyboard;
        InputSettings previousSettings;
        int checks;
        bool failed;
        readonly StringBuilder output=new StringBuilder();
        void Check(bool condition,string label)
        {
            checks++;if(condition)return;failed=true;output.AppendLine("FAIL: "+label);Debug.LogError("GRABBIT FLOW: "+label);
        }
        public void Begin(GrabbitGame target) { game=target;StartCoroutine(Run()); }
        IEnumerator Click(Button button)
        {
            Check(button!=null&&button.gameObject.activeInHierarchy&&button.interactable,"Button is available: "+button.name);
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
            var point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            yield return ClickAt(point);
        }
        IEnumerator ClickAt(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point });yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point }.WithButton(MouseButton.Left));yield return null;yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState { position=point });yield return null;yield return null;
        }
        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;yield return null;
        }
        IEnumerator Settled()
        {
            float until=Time.realtimeSinceStartup+4;
            while(game.IsAnimating&&Time.realtimeSinceStartup<until)yield return null;
            Check(!game.IsAnimating&&game.ViewConsistency()=="OK","Animation completes consistently");yield return null;
        }
        IEnumerator Run()
        {
            previousSettings=InputSystem.settings;InputSystem.settings=Instantiate(previousSettings);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previousMouse=Mouse.current;previousKeyboard=Keyboard.current;
            mouse=InputSystem.AddDevice<Mouse>("GrabbitFlowMouse");keyboard=InputSystem.AddDevice<Keyboard>("GrabbitFlowKeyboard");
            Check(game.Screen==GameScreen.Title,"Application begins on title screen");
            string original=game.Session.Current.Key(true);
            yield return Press(Key.W,Key.Space,Key.R,Key.Digit3);
            Check(game.Screen==GameScreen.Title&&game.Session.Current.Key(true)==original,"Gameplay and stage hotkeys do not leak into title screen");
            yield return Click(game.UI.StartButton);
            Check(game.Screen==GameScreen.StageSelect,"Actual start-button click opens stage selection");
            Check(game.UI.StageButtons.Length>=5&&game.UI.StageButtons.All(b=>b.interactable),"Five built-in stages and any custom stages are available");
            yield return Click(game.UI.StageButtons[0]);
            Check(game.Screen==GameScreen.Playing&&game.LevelIndex==0,"Actual stage-card click starts game");
            Check(game.UI.GameRoot.GetComponentsInChildren<Text>(true).Length==0,"In-game menu button has no text");
            Check(game.GetComponentsInChildren<Text>().Length==0,"Gameplay has no visible text");
            yield return Click(game.UI.MenuButton);
            Check(game.MenuOpen&&game.UI.PopupRoot.gameObject.activeInHierarchy,"Actual icon click opens popup");
            original=game.Session.Current.Key(true);
            yield return Press(Key.W,Key.Space,Key.R,Key.Z,Key.Digit5);
            yield return ClickAt(new Vector2(UnityEngine.Screen.width*.05f,UnityEngine.Screen.height*.1f));
            Check(game.MenuOpen&&game.Session.Current.Key(true)==original,"Modal blocks gameplay keys and background clicks");
            yield return Click(game.UI.ResumeButton);
            Check(!game.MenuOpen&&game.Screen==GameScreen.Playing&&game.Session.Current.Key(true)==original,"Resume button preserves game state");
            yield return Press(Key.Escape);Check(game.MenuOpen,"Escape opens popup");
            yield return Click(game.UI.CloseButton);Check(!game.MenuOpen,"Close icon resumes game");
            output.AppendLine("PASS real mouse clicks: title, stage selection, icon menu, modal blocking and resume.");

            game.Perform(Command.Slam);yield return new WaitForSecondsRealtime(.07f);game.OpenMenu();
            var pose=game.View.rabbit.position;var rotation=game.View.rabbit.rotation;
            var blocks=game.View.blocks.ToDictionary(p=>p.Key,p=>p.Value.position);
            yield return new WaitForSecondsRealtime(.35f);
            Check(game.IsAnimating&&Vector3.Distance(game.View.rabbit.position,pose)<.0001f&&Quaternion.Angle(game.View.rabbit.rotation,rotation)<.001f,"Pause freezes the rabbit during a slam");
            Check(blocks.All(p=>Vector3.Distance(game.View.blocks[p.Key].position,p.Value)<.0001f),"Pause freezes all moving blocks");
            yield return Click(game.UI.ResumeButton);yield return Settled();
            Check(game.Session.Current.rabbit.cell==new Vector3Int(-2,0,-1),"Resume finishes the same slam at the correct cell");
            game.Restart();yield return null;game.Perform(Command.Slam);yield return new WaitForSecondsRealtime(.06f);game.OpenMenu();
            int token=game.AnimationToken;yield return Click(game.UI.StagesButton);
            Check(game.Screen==GameScreen.StageSelect&&!game.MenuOpen&&!game.IsAnimating&&game.AnimationToken>token,"Stage return cancels paused animation");
            yield return new WaitForSecondsRealtime(.8f);Check(game.Screen==GameScreen.StageSelect,"Cancelled animation cannot change returned screen");
            yield return Click(game.UI.StageButtons[1]);yield return Click(game.UI.MenuButton);yield return Click(game.UI.TitleButton);
            Check(game.Screen==GameScreen.Title&&!game.MenuOpen,"Menu return opens the title screen");
            yield return Click(game.UI.StartButton);yield return Click(game.UI.HomeButton);
            Check(game.Screen==GameScreen.Title,"Stage selection home icon opens title");
            output.AppendLine("PASS pause/resume mid-slam, stage return, title return and stale-animation cancellation.");

            var routes=new[]{
                new[]{Command.D,Command.W,Command.W,Command.W,Command.W,Command.A,Command.W,Command.W},
                new[]{Command.Slam,Command.D,Command.W,Command.W,Command.W},
                new[]{Command.Slam,Command.W,Command.D,Command.W,Command.W,Command.W},
                new[]{Command.D,Command.W,Command.W,Command.W},
                new[]{Command.W,Command.Slam,Command.W,Command.D,Command.W,Command.W,Command.W}
            };
            game.ShowStageSelection();yield return null;
            for(int i=0;i<routes.Length;i++)
            {
                yield return Click(game.UI.StageButtons[i]);
                Check(game.Session.Current.turns==0&&!game.Session.Current.cleared,"Stage entry always starts fresh");
                foreach(var command in routes[i])
                {
                    Check(game.Perform(command),"Clear route accepted: stage "+(i+1)+" "+command);yield return Settled();
                }
                Check(game.Session.Current.cleared&&game.IsCompleting,"Goal starts clear return after animation");
                Check(!game.Perform(Command.Slam),"Clear transition blocks extra actions");
                if(i==0)
                {
                    game.OpenMenu();yield return new WaitForSecondsRealtime(1);
                    Check(game.Screen==GameScreen.Playing&&game.MenuOpen,"Popup pauses a pending clear return");
                    yield return Click(game.UI.ResumeButton);
                }
                float until=Time.realtimeSinceStartup+3;
                while(game.Screen==GameScreen.Playing&&Time.realtimeSinceStartup<until)yield return null;
                Check(game.Screen==GameScreen.StageSelect&&game.IsStageComplete(i),"Stage "+(i+1)+" automatically returns with completion mark");
                Check(game.UI.StageButtons[i].transform.Find("Complete").gameObject.activeInHierarchy,"Completion check is visible on stage card");
            }
            yield return Click(game.UI.StageButtons[0]);Check(!game.Session.Current.cleared&&game.Session.Current.turns==0,"Completed stage can be replayed from the beginning");
            output.AppendLine("PASS all five authored clear routes, paused clear timer, completion marks and replay.");
            game.ShowTitle();Cleanup();
            Report=(failed?"FAILED":"PASSED")+" / "+checks+" flow assertions\n"+output;Done=true;Debug.Log("GRABBIT FLOW VERIFICATION\n"+Report);
        }
        void Cleanup()
        {
            if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);mouse=null;
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);keyboard=null;
            if(previousMouse!=null&&previousMouse.added)previousMouse.MakeCurrent();
            if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
            if(previousSettings!=null){var temporary=InputSystem.settings;InputSystem.settings=previousSettings;previousSettings=null;Destroy(temporary);}
        }
        void OnDestroy() { Cleanup(); }
    }
}
#endif
