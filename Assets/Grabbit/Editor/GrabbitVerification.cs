using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Grabbit.Editor
{
    public static class GrabbitVerification
    {
        static int checks;
        static readonly StringBuilder report=new StringBuilder();
        public static string LastReport;
        static Vector3Int P(int x,int y=0,int z=0) { return new Vector3Int(x,y,z); }
        static BoardState Board(params Vector3Int[] cells)
        {
            var b=new BoardState { rabbit=new Rabbit(P(0),Vector3Int.down,Vector3Int.forward) };
            int id=1;foreach(var p in cells)b.blocks.Add(p,new Block(id++,p));
            if(cells.Length>0)b.goal=new Goal(1,Vector3Int.back);
            return b;
        }
        static void Check(bool condition,string name)
        {
            checks++;if(!condition)throw new Exception("GRABBIT CHECK FAILED: "+name);
        }
        static void Equal<T>(T a,T b,string name) { Check(EqualityComparer<T>.Default.Equals(a,b),name+" expected="+b+" actual="+a); }
        [MenuItem("GRABBIT/Run core verification")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            checks=0;report.Clear();
            try
            {
                foreach(var d in Axes.All)
                {
                    var touching=Board(d,d*2,d*3);
                    Equal(Gravity.Read(touching,P(0),d).count,3,"All six adjacent faces count a straight run");
                    var gap=Board(d*2,d*3,d*4,d*5);
                    Equal(Gravity.Read(gap,P(0),d).score,0,"One empty cell excludes a long run on every face");
                    Equal(Gravity.Read(gap,P(0),d).distance,0,"A remote run is not a candidate");
                    var broken=Board(d,d*3,d*4);
                    Equal(Gravity.Read(broken,P(0),d).count,1,"First gap stops the run");
                    var side=Axes.All.First(a=>Axes.Dot(a,d)==0);
                    Equal(Gravity.Read(Board(d,d+side,d+side*2),P(0),d).count,1,"Side branches do not add gravity");
                }
                var b=Board(P(0,-1),P(1));Equal(Gravity.Choose(b),Vector3Int.down,"Tie retains down");
                b.rabbit.down=Vector3Int.up;Equal(Gravity.Choose(b),Vector3Int.right,"Tie falls back to deterministic axis order");
                Equal(Gravity.Choose(Board()),Vector3Int.zero,"No adjacent block means no gravity");
                b=Board(P(0,-1),P(0,2),P(0,3),P(0,4));
                Equal(Resolver.ResolveStable(b),Reject.None,"Remote ceiling resolves");
                Equal(b.rabbit.cell,P(0),"Remote ceiling cannot pull rabbit across gap");
                Equal(b.rabbit.down,Vector3Int.down,"Remote ceiling cannot rotate rabbit");
                report.AppendLine("PASS six-face adjacency, no gap attraction, straight runs and ties.");

                b=Board(P(0,-1));var result=Resolver.Try(b,Command.Slam);
                Check(result.Success,"Single support block slam succeeds");
                Equal(result.state.rabbit.cell,P(0,-1),"Single block carries rabbit one cell");
                Equal(result.state.blocks[P(0,-2)].id,1,"Single block moved one cell");
                Equal(result.motions.Count,1,"No delayed fall after the shared slam");
                Equal(result.motions[0].after.cell,P(0,-1),"Shared motion contains rabbit destination");
                b=Board(P(0,-1),P(0,-2),P(1,-1));result=Resolver.Try(b,Command.Slam);
                Check(result.Success,"Two block run slam");
                Equal(result.state.blocks[P(1,-1)].id,3,"Side-connected block stays put");
                Equal(result.state.rabbit.cell,P(0,-1),"Rabbit moves with the two block run");
                Equal(result.state.rabbit.down,Vector3Int.down,"Shorter adjacent side cannot beat supporting run");

                b=Board(P(0,-1),P(0,-2),P(1),P(2));result=Resolver.Try(b,Command.Slam);
                Equal(result.state.rabbit.cell,P(0,-1),"Old-cell attraction fixture translated");
                Equal(result.state.rabbit.down,Vector3Int.down,"Attraction at old cell is ignored");
                b=Board(P(0,-1),P(0,-2),P(1),P(2),P(1,-1),P(2,-1),P(3,-1));
                Equal(Gravity.Choose(b),Vector3Int.down,"Before slam the old cell retains down");
                result=Resolver.Try(b,Command.Slam);Check(result.Success,"New-cell gravity fixture succeeds");
                Equal(result.state.rabbit.cell,P(0,-1),"Gravity turn does not translate again");
                Equal(result.state.rabbit.down,Vector3Int.right,"New-cell longer run rotates the rabbit");
                Equal(result.motions.Count,2,"Exactly one shared move then one rotation");
                Equal(result.motions[0].kind,MotionKind.Blocks,"Translation is first");
                Equal(result.motions[1].kind,MotionKind.Rotate,"Rotation is second");
                Equal(result.motions[1].before.cell,P(0,-1),"Rotation begins at translated cell");
                report.AppendLine("PASS rabbit/run co-motion, single block slam, side separation and gravity sampled after translation.");

                foreach(var d in Axes.All)foreach(var f in Axes.All.Where(a=>Axes.Dot(a,d)==0))
                {
                    b=Board(d,d*2);b.rabbit=new Rabbit(P(0),d,f);
                    var right=Axes.Cross(-d,f);
                    var a=Resolver.Try(b,Command.A);var turn=Resolver.Try(b,Command.D);
                    Check(a.Success&&turn.Success,"Yaw allowed on an isolated support");
                    Equal(a.state.rabbit.cell,P(0),"A stays in the cell");Equal(turn.state.rabbit.cell,P(0),"D stays in the cell");
                    Equal(a.state.rabbit.forward,-right,"A turns left in local frame");Equal(turn.state.rabbit.forward,right,"D turns right in local frame");
                    Equal(turn.state.rabbit.down,d,"Yaw preserves gravity");
                    var full=b;
                    for(int i=0;i<4;i++)full=Resolver.Try(full,Command.D).state;
                    Equal(full.rabbit.forward,f,"Four yaw turns return to original facing");
                    Equal(Resolver.Try(turn.state,Command.A).state.rabbit.forward,f,"Opposite yaw turns cancel");
                    var slab=Board(d,d+f,d-f);slab.rabbit=b.rabbit;
                    Equal(Resolver.Try(slab,Command.W).state.rabbit.cell,f,"W advances in all local frames");
                    Equal(Resolver.Try(slab,Command.S).state.rabbit.cell,-f,"S backs up in all local frames");
                    var slam=Resolver.Try(b,Command.Slam);
                    Check(slam.Success,"Slam works under all six down axes");
                    Equal(slam.state.rabbit.cell,d,"Slam displacement follows local down");
                    foreach(var block in b.blocks.Values)
                        Equal(slam.state.blocks.Values.First(x=>x.id==block.id).cell,block.cell+d,"Whole run translates rigidly on every axis");
                    foreach(var next in Axes.All)
                    {
                        var r=b.rabbit;r.Turn(next);
                        Check(Axes.Unit(r.down)&&Axes.Unit(r.forward)&&Axes.Unit(r.Right),"Gravity rotation maintains unit frame");
                        Equal(Axes.Dot(r.down,r.forward),0,"Gravity rotation maintains perpendicular frame");
                        if(next==-d)Equal(r.forward,f,"180 degree forward preserved");
                    }
                }
                b=Board(P(0,-1));Equal(Resolver.Try(b,Command.W).reason,Reject.CliffBlocked,"W cannot walk off unsupported edge");
                Equal(Resolver.Try(b,Command.S).reason,Reject.CliffBlocked,"S cannot walk off unsupported edge");
                b=Board(P(0,-1),P(1,-1));
                var facing=Resolver.Try(b,Command.D).state;
                Equal(Resolver.Try(facing,Command.W).state.rabbit.cell,P(1),"W uses the new facing after D");
                report.AppendLine("PASS W/S walking and A/D stationary yaw in every local gravity frame.");

                b=Board(P(0,-1),P(0,-2));b.min=P(-8,-2,-8);
                var session=new GameSession(b);string key=session.Current.Key(true);
                Equal(session.Act(Command.Slam).reason,Reject.OutOfBounds,"Boundary rejects entire slam");
                Equal(session.Current.Key(true),key,"Rejected action leaves rabbit and all blocks unchanged");
                Equal(session.UndoCount,0,"Rejected action adds no history");
                b=Board(P(0,-1),P(0,-2),P(0,-4),P(0,-5));result=Resolver.Try(b,Command.Slam);
                Check(result.Success,"Run merge slam succeeds");
                Equal(result.state.blocks[P(0,-4)].id,3,"Previously separated block stays stationary");
                Equal(Gravity.Read(result.state,result.state.rabbit.cell,Vector3Int.down).count,4,"Moved run merges into adjacent run");
                b=Board(P(0,-1));b.goal=new Goal(1,Vector3Int.up);b.cleared=true;
                result=Resolver.Try(b,Command.Slam);
                Check(result.Success&&result.state.AtGoal(),"Goal follows block and experimentation continues after goal");

                foreach(var level in Levels.All)
                {
                    Equal(Resolver.Validate(level.start),null,level.title+" initial valid");
                    Check(level.start.blocks.Values.All(x=>x.kind==BlockKind.MovableGravity),"Every authored block is movable");
                    Check(Resolver.Try(level.start,Command.Slam).Success,"Initial slam works on every stage");
                    session=new GameSession(level.start);key=session.Current.Key(true);
                    foreach(var command in level.exercise)
                    {
                        string source=session.Current.Key(true);var preview=session.Preview(command);
                        Equal(session.Current.Key(true),source,"Preview is non-mutating");
                        result=session.Act(command);Check(result.Success,level.title+" exercise "+command);
                        Equal(result.state.Key(true),preview.state.Key(true),"Preview and committed action match");
                    }
                    while(session.UndoCount>0)session.Undo();
                    Equal(session.Current.Key(true),key,"Undo restores full state including facing and chunks");
                    session.Act(Command.Slam);session.Restart();
                    Equal(session.Current.Key(true),key,"Restart restores initial snapshot");
                    Equal(session.UndoCount,0,"Restart clears history");
                    report.AppendLine("PASS stage: "+level.title);
                }
                session=new GameSession(Levels.All[3].start);session.Act(Command.D);session.Act(Command.W);
                Equal(session.Current.rabbit.cell,P(1,1),"Stage 4 rabbit directly below the separated ceiling");
                Equal(Gravity.Read(session.Current,session.Current.rabbit.cell,Vector3Int.up).score,0,"Stage 4 ceiling produces zero gravity");
                Equal(session.Current.rabbit.down,Vector3Int.down,"Stage 4 rabbit remains on floor");

                // Regression for the formerly invisible Y=-3 boundary on stage 2.
                session=new GameSession(Levels.All[1].start);key=session.Current.Key(true);
                for(int i=0;i<256;i++)Check(session.Act(Command.Slam).Success,"Stage 2 repeated slam "+(i+1));
                Equal(session.Current.rabbit.cell,P(0,-255),"Repeated descent has no authored bottom boundary");
                Check(!session.Current.bounded,"Game stages use an unbounded integer grid");
                for(int i=0;i<3;i++)Equal(session.Current.blocks.Values.First(x=>x.id==i+1).cell,P(0,-i-256),"Support column follows every repeated slam");
                foreach(var block in session.Initial.blocks.Values.Where(x=>x.id>3))
                    Equal(session.Current.blocks.Values.First(x=>x.id==block.id).cell,block.cell,"Side structure stays put during repeated descent");
                while(session.UndoCount>0)session.Undo();Equal(session.Current.Key(true),key,"Repeated slams undo exactly");
                session.Act(Command.Slam);session.Act(Command.Slam);
                var tied=session.Current.Clone();
                Equal(tied.rabbit.cell,P(0,-1),"Second slam reaches the 3-versus-3 junction");
                Equal(Gravity.All(tied).First(x=>x.direction==Vector3Int.right).score,3,"Junction side force is three");
                Equal(Gravity.All(tied).First(x=>x.direction==Vector3Int.down).score,3,"Junction floor force is three");
                Check(session.Act(Command.D).Success,"Turn head toward the side run");
                Equal(session.Current.rabbit.down,Vector3Int.down,"Turning head alone does not switch floors");
                var beforeTransfer=session.Current.Key(true);var previewTransfer=session.Preview(Command.W);
                result=session.Act(Command.W);Check(result.Success,"W enters an equally strong adjacent surface");
                Equal(result.state.Key(true),previewTransfer.state.Key(true),"Transfer preview matches commit");
                Equal(result.state.rabbit.cell,P(0,-1),"Surface transfer remains in the empty rabbit cell");
                Equal(result.state.rabbit.down,Vector3Int.right,"Surface transfer adopts the chosen side as down");
                Equal(result.state.rabbit.forward,Vector3Int.up,"Surface transfer looks toward the previous up direction");
                Check(session.Undo(),"Transfer can be undone");Equal(session.Current.Key(true),beforeTransfer,"Transfer Undo restores facing and gravity");
                result=Resolver.Try(tied,Command.Slam);Check(result.Success,"Space still descends at a cross-axis tie");
                Equal(result.state.rabbit.cell,P(0,-2),"Space does not switch to the side at a tie");

                foreach(var d in Axes.All)foreach(var f in Axes.All.Where(x=>Axes.Dot(x,d)==0))
                {
                    b=Board(d,d*2,d*3,f,f*2,f*3);b.rabbit=new Rabbit(P(0),d,f);
                    result=Resolver.Try(b,Command.W);Check(result.Success,"Perpendicular equal-force transfer in every frame");
                    Equal(result.state.rabbit.cell,P(0),"Transfer never overlaps the target block");
                    Equal(result.state.rabbit.down,f,"Chosen adjacent face becomes floor");
                    Equal(result.state.rabbit.forward,-d,"Old up becomes new forward");
                    Equal(result.motions.Count,1,"Surface transfer is one rotation");
                    Equal(result.motions[0].kind,MotionKind.Rotate,"Surface transfer has no walking motion");
                    foreach(var block in b.blocks.Values)Equal(result.state.blocks[block.cell].id,block.id,"Transfer leaves all blocks stationary");
                    b.blocks.Remove(f*3);Equal(Resolver.Try(b,Command.W).reason,Reject.Occupied,"Weaker surfaces cannot be selected");
                }
                b=Board(P(0,-1),P(0,-2),P(0,-3),P(-1),P(-2),P(-3));b.rabbit.forward=Vector3Int.left;
                result=Resolver.Try(b,Command.W);
                Equal(result.state.rabbit.Up,Vector3Int.right,"User's left-column example has head +X");
                Equal(result.state.rabbit.forward,Vector3Int.up,"User's left-column example faces +Y");
                report.AppendLine("PASS unbounded repeated descent and W surface transfer on perpendicular ties.");

                foreach(var d in Axes.All)
                {
                    b=Board(d,d*2,d*3,-d,-d*2,-d*3);
                    Equal(Gravity.Choose(b),Vector3Int.zero,"Equal opposite runs cancel on every axis");
                    Equal(Resolver.ResolveStable(b),Reject.None,"Weightlessness is a valid state");
                    Check(b.IsWeightless,"Opposing tie is marked weightless");
                    foreach(var command in new[]{Command.W,Command.S,Command.Slam})
                        Equal(Resolver.Try(b,command).reason,Reject.NoGravity,"No locomotion in weightlessness");
                    var side=Axes.All.First(x=>Axes.Dot(x,d)==0);
                    var sideBlock=new Block(b.blocks.Count+1,side);b.blocks.Add(side,sideBlock);
                    Equal(Resolver.ResolveStable(b),Reject.None,"Remaining cross-axis force resolves");
                    Equal(b.rabbit.down,side,"Other axis wins when the opposed axis cancels");
                    b=Board(d,d*2,d*3,-d);
                    Equal(Gravity.All(b).First(x=>x.direction==d).score,2,"Three minus one leaves two units");
                    Equal(Gravity.Choose(b),d,"Unequal opposing force points toward the longer run");
                    b.blocks.Add(side,new Block(5,side));b.blocks.Add(side*2,new Block(6,side*2));b.rabbit.down=side;
                    Equal(Gravity.Choose(b),side,"Net two ties unopposed two without a contact bonus");
                }
                b=Board(P(0,-1),P(0,-1,1),P(0,-2,1),P(0,-3,1),P(0,1,1),P(0,2,1),P(0,3,1));
                session=new GameSession(b);key=session.Current.Key(true);result=session.Act(Command.W);
                Check(result.Success&&session.Current.IsWeightless,"Walking into cancellation commits the zero-gravity state");
                Equal(session.Current.rabbit.cell,P(0,0,1),"Weightless destination is not rolled back");
                string floating=session.Current.Key(true);int history=session.UndoCount;
                foreach(var command in new[]{Command.W,Command.S,Command.Slam})Equal(session.Act(command).reason,Reject.NoGravity,"Weightless commands rejected");
                Equal(session.Current.Key(true),floating,"Rejected floating movement preserves the state");Equal(session.UndoCount,history,"Rejected floating movement has no Undo entry");
                Check(session.Act(Command.A).Success&&session.Current.IsWeightless,"Looking around does not create gravity");
                session.Undo();session.Undo();Equal(session.Current.Key(true),key,"Undo leaves weightlessness correctly");
                report.AppendLine("PASS per-axis subtraction, alternate-axis rotation, persistent weightlessness and recovery.");
                var random=new System.Random(907);int accepted=0,rejected=0;
                for(int trial=0;trial<3000;trial++)
                {
                    b=Board(P(0,-1));int count=random.Next(1,45);
                    for(int i=0;i<count;i++)
                    {
                        var p=P(random.Next(-3,4),random.Next(-3,4),random.Next(-3,4));
                        if(p==P(0)||b.blocks.ContainsKey(p))continue;
                        b.blocks.Add(p,new Block(b.blocks.Count+1,p));
                    }
                    Equal(Resolver.ResolveStable(b),Reject.None,"Random adjacent board stabilizes without falling");
                    string source=b.Key(true);var ids=b.blocks.Values.Select(x=>x.id).OrderBy(x=>x).ToArray();
                    foreach(Command command in Enum.GetValues(typeof(Command)))
                    {
                        result=Resolver.Try(b,command);Equal(b.Key(true),source,"Random action does not mutate source");
                        if(!result.Success){rejected++;continue;}accepted++;
                        var end=result.state;
                        Check(ids.SequenceEqual(end.blocks.Values.Select(x=>x.id).OrderBy(x=>x)),"Random block IDs conserved");
                        Check(end.Inside(end.rabbit.cell)&&!end.Occupied(end.rabbit.cell),"Random rabbit stays in an empty in-bounds cell");
                        Check(end.blocks.All(x=>x.Key==x.Value.cell&&end.Inside(x.Key)),"Random blocks remain in bounds and keyed correctly");
                        Check(end.IsWeightless||(end.Occupied(end.rabbit.cell+end.rabbit.down)&&Gravity.Choose(end)==end.rabbit.down),"Random result is weightless or has dominant adjacent support");
                        Check(Axes.Unit(end.rabbit.forward)&&Axes.Dot(end.rabbit.down,end.rabbit.forward)==0,"Random frame remains orthonormal");
                        var displacement=command==Command.Slam?b.rabbit.down:command==Command.W&&!b.Occupied(b.rabbit.cell+b.rabbit.forward)?b.rabbit.forward:command==Command.S?-b.rabbit.forward:Vector3Int.zero;
                        Equal(end.rabbit.cell,b.rabbit.cell+displacement,"Random actions never add a gravity fall");
                    }
                }
                report.AppendLine("PASS seed 907: 3000 layouts, "+accepted+" accepted / "+rejected+" rejected actions.");
                report.AppendLine("TOTAL PASS: "+checks+" assertions.");
                LastReport=report.ToString();return LastReport;
            }
            catch(Exception e){LastReport=report+"FAIL: "+e;throw;}
        }
        [MenuItem("GRABBIT/Create or open expedition")]
        public static void OpenGame()
        {
            const string path="Assets/Scenes/Grabbit.unity";
            if(System.IO.File.Exists(path)){EditorSceneManager.OpenScene(path);return;}
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var game=new GameObject("GRABBIT").AddComponent<GrabbitGame>();
            // Preview is created only in play mode, keeping the saved scene compact and deterministic.
            var camera=new GameObject("Preview Camera").AddComponent<Camera>();camera.transform.SetParent(game.transform);camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=5;camera.backgroundColor=BoardView.Navy;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.transform.position=new Vector3(-8,7,-10);camera.transform.LookAt(Vector3.zero);
            EditorSceneManager.SaveScene(scene,path);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(path,true)};
            Selection.activeGameObject=game.gameObject;
        }
    }
}


