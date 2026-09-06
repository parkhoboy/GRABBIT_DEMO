#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Grabbit.Editor
{
    public sealed class GrabbitPlayVerification : MonoBehaviour
    {
        public string Report="Running";
        public bool Done;
        readonly StringBuilder output=new StringBuilder();
        GrabbitGame game;
        Keyboard device,previous;
        InputSettings previousSettings;
        int checks;
        bool failed;
        void Check(bool condition,string label)
        {
            checks++;
            if(condition)return;
            failed=true;output.AppendLine("FAIL "+label);Debug.LogError("GRABBIT PLAY CHECK: "+label);
        }
        public void Begin(GrabbitGame target) { game=target;StartCoroutine(Run()); }
        IEnumerator Settled()
        {
            float deadline=Time.realtimeSinceStartup+5;
            while(game.IsAnimating&&Time.realtimeSinceStartup<deadline)yield return null;
            Check(!game.IsAnimating,"Animation completed within 5 seconds");
            Check(game.ViewConsistency()=="OK","Model and visual consistency");
            yield return null;
        }
        IEnumerator SharedSlam(int level)
        {
            game.LoadLevel(level);yield return new WaitForSecondsRealtime(.1f);
            var initial=game.Session.Current.Clone();
            var preview=game.Session.Preview(Command.Slam);
            var motion=preview.motions.First();int id=motion.blockFrom.Keys.First();
            var rabbitStart=game.View.rabbit.position;
            int movingFrames=0,rotationFrames=0;
            Check(game.Perform(Command.Slam),"Shared slam starts on stage "+(level+1));
            float deadline=Time.realtimeSinceStartup+5;
            while(game.IsAnimating&&Time.realtimeSinceStartup<deadline)
            {
                var displacement=game.View.blocks[id].position-(Vector3)motion.blockFrom[id];
                bool translated=Vector3.Distance(game.View.blocks[id].position,motion.blockTo[id])<.0001f;
                if(!translated)
                {
                    Check(Vector3.Distance(game.View.rabbit.position,rabbitStart+displacement)<.0001f,"Rabbit and supporting run move together on every sampled frame");
                    Check(Quaternion.Angle(game.View.rabbit.rotation,BoardView.Rotation(initial.rabbit))<.001f,"Gravity rotation waits until translation finishes");
                    foreach(var block in motion.blockFrom)
                        Check(Vector3.Distance(game.View.blocks[block.Key].position,(Vector3)block.Value+displacement)<.0001f,"Whole run shares a rigid displacement");
                    if(displacement.sqrMagnitude>.0001f)movingFrames++;
                }
                else if(Quaternion.Angle(game.View.rabbit.rotation,BoardView.Rotation(initial.rabbit))>.1f)
                {
                    rotationFrames++;
                    foreach(var block in motion.blockTo)
                        Check(Vector3.Distance(game.View.blocks[block.Key].position,block.Value)<.0001f,"Blocks are settled before gravity rotation");
                }
                foreach(var block in initial.blocks.Values.Where(x=>!motion.blockFrom.ContainsKey(x.id)))
                    Check(Vector3.Distance(game.View.blocks[block.id].position,block.cell)<.0001f,"Side-connected blocks stay still during slam");
                yield return null;
            }
            yield return Settled();
            Check(movingFrames>0,"Observed intermediate shared translation frames");
            if(level==2)
            {
                Check(rotationFrames>0,"Observed rotation after translated support settled");
                Check(game.Session.Current.rabbit.cell==new Vector3Int(0,0,0)&&game.Session.Current.rabbit.down==Vector3Int.right,"Stage 3 turns at the translated cell");
            }
            Check(game.Session.Current.Key(true)==preview.state.Key(true),"Shared slam equals preview");
        }
        IEnumerator Press(params Key[] keys)
        {
            InputSystem.QueueStateEvent(device,new KeyboardState(keys));yield return null;yield return null;
            yield return Settled();
            InputSystem.QueueStateEvent(device,new KeyboardState());yield return null;yield return null;
        }
        BoardState CancellationFixture(bool sideForce)
        {
            var board=new BoardState { bounded=false,rabbit=new Rabbit(Vector3Int.zero,Vector3Int.down,Vector3Int.forward) };
            var cells=new System.Collections.Generic.List<Vector3Int> { new Vector3Int(0,-1,0) };
            for(int i=1;i<=3;i++){cells.Add(new Vector3Int(0,i,1));cells.Add(new Vector3Int(0,-i,1));}
            if(sideForce)cells.Add(new Vector3Int(1,0,1));
            foreach(var cell in cells)board.blocks.Add(cell,new Block(board.blocks.Count+1,cell));
            board.goal=new Goal(1,Vector3Int.back);return board;
        }
        void LoadFixture(BoardState board)
        {
            var original=Levels.All[1].start;
            try { Levels.All[1].start=board;game.LoadLevel(1); }
            finally { Levels.All[1].start=original; }
        }
        IEnumerator GravityRules()
        {
            game.LoadLevel(1);yield return new WaitForSecondsRealtime(.1f);
            yield return Press(Key.Space);yield return Press(Key.Space);
            Check(game.Session.Current.rabbit.cell==new Vector3Int(0,-1,0)&&game.Session.Current.rabbit.down==Vector3Int.down,"Second Space reaches stage 2's equal-force junction");
            yield return Press(Key.D);
            var beforeTransfer=game.Session.Current.Key(true);
            Check(game.Session.Current.rabbit.forward==Vector3Int.right&&game.Session.Current.rabbit.down==Vector3Int.down,"D faces the tied side without changing gravity");
            yield return Press(Key.W);
            Check(game.Session.Current.rabbit.cell==new Vector3Int(0,-1,0)&&game.Session.Current.rabbit.down==Vector3Int.right&&game.Session.Current.rabbit.forward==Vector3Int.up,"Actual W switches to the equally strong side face without entering the block");
            yield return Press(Key.Z);Check(game.Session.Current.Key(true)==beforeTransfer,"Undo restores the full pre-transfer pose");
            Check(game.Perform(Command.W),"Transfer animation restarts");yield return new WaitForSecondsRealtime(.07f);game.Undo();
            Check(game.Session.Current.Key(true)==beforeTransfer&&game.ViewConsistency()=="OK","Undo during surface rotation restores model and visuals");
            yield return new WaitForSecondsRealtime(.4f);Check(game.Session.Current.Key(true)==beforeTransfer,"Cancelled transfer has no delayed effects");
            game.Restart();yield return null;
            for(int i=1;i<=24;i++)
            {
                yield return Press(Key.Space);
                Check(game.Session.Current.rabbit.cell==new Vector3Int(0,1-i,0),"Repeated actual Space press "+i+" is not blocked by hidden bounds");
                var point=game.View.viewCamera.WorldToViewportPoint(game.View.rabbit.position+game.View.rabbit.up*.5f);
                Check(point.x>.1f&&point.x<.9f&&point.y>.1f&&point.y<.9f,"Camera keeps rabbit visible during repeated descent");
            }
            game.Restart();Check(game.Session.Current.turns==0&&game.ViewConsistency()=="OK","Restart recovers after long descent");
            LoadFixture(CancellationFixture(false));yield return new WaitForSecondsRealtime(.1f);
            string grounded=game.Session.Current.Key(true);yield return Press(Key.W);
            Check(game.Session.Current.IsWeightless&&game.Session.Current.rabbit.cell==new Vector3Int(0,0,1),"Walking into opposing 3/3 forces leaves rabbit weightless at the destination");
            Check(game.View.arrowsRoot.childCount==0,"No movement arrows are displayed in zero gravity");
            string floating=game.Session.Current.Key(true);int history=game.Session.UndoCount;
            yield return Press(Key.W);yield return Press(Key.S);yield return Press(Key.Space);
            Check(game.Session.Current.Key(true)==floating&&game.Session.UndoCount==history,"Actual movement keys cannot move or push in zero gravity");
            yield return Press(Key.D);Check(game.Session.Current.IsWeightless&&game.Session.Current.rabbit.cell==new Vector3Int(0,0,1),"Looking around does not move a floating rabbit");
            yield return Press(Key.Z);yield return Press(Key.Z);
            Check(game.Session.Current.Key(true)==grounded&&!game.Session.Current.IsWeightless,"Undo recovers from zero gravity");
            yield return Press(Key.W);yield return Press(Key.R);
            Check(game.Session.Current.Key(true)==grounded&&game.ViewConsistency()=="OK","Restart recovers from zero gravity");
            LoadFixture(CancellationFixture(true));yield return new WaitForSecondsRealtime(.1f);yield return Press(Key.W);
            Check(!game.Session.Current.IsWeightless&&game.Session.Current.rabbit.down==Vector3Int.right,"A remaining side force rotates the rabbit after the vertical forces cancel");
            output.AppendLine("PASS actual keys: 24 repeated slams, tied surface transfer, mid-rotation Undo, per-axis cancellation, weightlessness and recovery.");
        }
        IEnumerator Run()
        {
            yield return SharedSlam(0);yield return SharedSlam(2);
            output.AppendLine("PASS frame-by-frame rabbit/chunk co-motion and delayed gravity rotation.");
            for(int i=0;i<Levels.All.Length;i++)
            {
                game.LoadLevel(i);yield return null;
                foreach(var c in Levels.All[i].exercise)
                {
                    var expected=game.Session.Preview(c);game.ShowPreview(c);
                    Check(expected.Success,"Exercise preview accepted "+i+" "+c);
                    Check(game.Perform(c),"Exercise command accepted "+i+" "+c);
                    if(expected.Success)Check(game.Session.Current.Key(true)==expected.state.Key(true),"Rendered command equals preview");
                    Check(!game.Perform(Command.D),"Extra input ignored during animation");
                    yield return Settled();
                    if(game.Session.Current.cleared)break;
                }
                Check(!game.View.GetComponentsInChildren<Component>(true).Any(c=>c is TextMesh||c is UnityEngine.UI.Text||(c!=null&&c.GetType().FullName.StartsWith("TMPro.TextMeshPro"))),"No on-screen text components on stage "+(i+1));
                Check(game.Session.Current.blocks.Values.All(b=>b.kind==BlockKind.MovableGravity),"All stage blocks movable");
                if(game.Session.Current.cleared)
                {
                    float deadline=Time.realtimeSinceStartup+3;
                    while(game.Screen==GameScreen.Playing&&Time.realtimeSinceStartup<deadline)yield return null;
                    Check(game.Screen==GameScreen.StageSelect&&game.IsStageComplete(i),"Clear returns to stage selection");
                    game.LoadLevel(i);yield return null;
                }
                Check(game.Perform(Command.A),"Can turn on an active game screen");yield return Settled();
                game.Undo();Check(game.ViewConsistency()=="OK","Undo after exercise restores visuals");
                output.AppendLine("PASS animated exercise, text-free board and clear return for stage "+(i+1));
            }
            game.LoadLevel(2);yield return null;
            string initialKey=game.Session.Current.Key(true);
            game.Perform(Command.Slam);yield return new WaitForSeconds(.06f);
            int token=game.AnimationToken;game.Undo();
            Check(game.AnimationToken>token,"Undo invalidates animation token");
            Check(game.Session.Current.Key(true)==initialKey&&game.ViewConsistency()=="OK","Undo mid-slam restores rabbit and all blocks");
            yield return new WaitForSeconds(.8f);
            Check(game.Session.Current.Key(true)==initialKey&&game.ViewConsistency()=="OK","No stale callback after Undo");
            game.Perform(Command.Slam);yield return new WaitForSeconds(.37f);game.Restart();
            Check(game.Session.Current.Key(true)==initialKey&&game.Session.UndoCount==0&&game.ViewConsistency()=="OK","Restart mid-gravity rotation");
            yield return new WaitForSeconds(.8f);
            Check(game.Session.Current.Key(true)==initialKey&&game.ViewConsistency()=="OK","No stale callback after Restart");
            game.Perform(Command.Slam);yield return new WaitForSeconds(.10f);game.LoadLevel(3);
            yield return new WaitForSeconds(.8f);
            Check(game.LevelIndex==3&&game.Session.Current.turns==0&&game.ViewConsistency()=="OK","Switching stage cancels pending animation");
            output.AppendLine("PASS Undo, Restart and stage switching during animation.");

            game.LoadLevel(0);yield return null;
            previousSettings=InputSystem.settings;InputSystem.settings=Instantiate(previousSettings);
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            previous=Keyboard.current;device=InputSystem.AddDevice<Keyboard>("GrabbitVerificationKeyboard");
            var startCell=game.Session.Current.rabbit.cell;
            InputSystem.QueueStateEvent(device,new KeyboardState(Key.D));yield return new WaitForSeconds(.8f);
            Check(game.Session.Current.turns==1,"Held key creates exactly one action");
            Check(game.Session.Current.rabbit.cell==startCell&&game.Session.Current.rabbit.forward==Vector3Int.right,"Actual D key rotates right without moving");
            InputSystem.QueueStateEvent(device,new KeyboardState());yield return null;yield return null;
            yield return Press(Key.W);
            Check(game.Session.Current.rabbit.cell==startCell+Vector3Int.right,"Actual W follows new facing");
            yield return Press(Key.S);
            Check(game.Session.Current.rabbit.cell==startCell&&game.Session.Current.rabbit.forward==Vector3Int.right,"Actual S moves backward while preserving facing");
            yield return Press(Key.A);
            Check(game.Session.Current.rabbit.cell==startCell&&game.Session.Current.rabbit.forward==Vector3Int.forward,"Actual A rotates left without moving");
            game.Restart();yield return Press(Key.W,Key.D);
            Check(game.Session.Current.turns==1&&game.Session.Current.rabbit.cell==startCell+Vector3Int.forward&&game.Session.Current.rabbit.forward==Vector3Int.forward,"W precedes D in simultaneous input");
            yield return Press(Key.R,Key.Z,Key.Space,Key.W);
            Check(game.Session.Current.turns==0&&game.Session.UndoCount==0,"Restart precedes Undo Slam and Walk");
            yield return Press(Key.Space);
            Check(game.Session.Current.rabbit.cell==startCell+Vector3Int.down,"Actual Space carries rabbit with a single block");
            yield return Press(Key.Z,Key.Space,Key.W);
            Check(game.Session.Current.turns==0&&game.Session.Current.rabbit.cell==startCell,"Undo precedes Slam and Walk");
            game.ShowStageSelection();yield return null;yield return Press(Key.Digit4);
            Check(game.LevelIndex==3,"Numeric 4 selects gap test stage");
            yield return Press(Key.D);yield return Press(Key.W);
            Check(game.Session.Current.rabbit.cell==new Vector3Int(1,1,0)&&game.Session.Current.rabbit.down==Vector3Int.down,"Actual keys under distant stage 4 ceiling do not trigger attraction");
            game.ShowStageSelection();yield return null;yield return Press(Key.Digit5);Check(game.LevelIndex==4,"Numeric 5 selects fifth stage");
            game.ShowStageSelection();yield return null;yield return Press(Key.Digit1);Check(game.LevelIndex==0,"Numeric 1 starts the first stage from selection");
            yield return GravityRules();
            InputSystem.RemoveDevice(device);device=null;if(previous!=null)previous.MakeCurrent();RestoreSettings();
            output.AppendLine("PASS actual Input System keys: W/S, A/D, Space, Z/R, hold/priority and stage shortcuts.");
            game.ShowTitle();
            Report=(failed?"FAILED":"PASSED")+" / "+checks+" play-mode assertions\n"+output;
            Done=true;Debug.Log("GRABBIT PLAY VERIFICATION\n"+Report);
        }
        void RestoreSettings() { if(previousSettings==null)return;var temporary=InputSystem.settings;InputSystem.settings=previousSettings;previousSettings=null;Destroy(temporary); }
        void OnDestroy() { RestoreSettings();if(device!=null&&device.added)InputSystem.RemoveDevice(device);if(previous!=null&&previous.added)previous.MakeCurrent(); }
    }
}
#endif


