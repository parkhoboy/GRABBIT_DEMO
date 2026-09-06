using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Grabbit
{
    public enum BlockKind { MovableGravity }
    public enum Command { Slam, W, A, S, D }
    public enum Reject { None, OutOfBounds, Occupied, CliffBlocked, InvalidSupport, NoGravity, ResolveLimit }
    public enum MotionKind { Walk, Turn, Rotate, Blocks }
    [Serializable] public struct Block
    {
        public int id;
        public Vector3Int cell;
        public BlockKind kind;
        public Block(int id, Vector3Int cell, BlockKind kind = BlockKind.MovableGravity) { this.id=id; this.cell=cell; this.kind=kind; }
    }
    [Serializable] public struct Rabbit
    {
        public Vector3Int cell, down, forward;
        public Rabbit(Vector3Int p, Vector3Int d, Vector3Int f) { cell=p; down=d; forward=f; }
        public Vector3Int Up { get { return -down; } }
        public Vector3Int Right { get { return Axes.Cross(Up, forward); } }
        public void Turn(Vector3Int next)
        {
            if(next != down && next != -down)
            {
                var k=Axes.Cross(down,next);
                forward=Axes.Cross(k,forward)+k*Axes.Dot(k,forward);
            }
            down=next;
        }
    }
    public static class Axes
    {
        public static readonly Vector3Int[] All={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,Vector3Int.forward,Vector3Int.back};
        public static readonly string[] Names={"+X","-X","+Y","-Y","+Z","-Z"};
        public static int Dot(Vector3Int a,Vector3Int b) { return a.x*b.x+a.y*b.y+a.z*b.z; }
        public static Vector3Int Cross(Vector3Int a,Vector3Int b) { return new Vector3Int(a.y*b.z-a.z*b.y,a.z*b.x-a.x*b.z,a.x*b.y-a.y*b.x); }
        public static bool Unit(Vector3Int a) { return Math.Abs(a.x)+Math.Abs(a.y)+Math.Abs(a.z)==1; }
        public static string Label(Vector3Int a) { int i=Array.IndexOf(All,a); return i<0?"--":Names[i]; }
    }
    [Serializable] public struct Goal { public int blockId; public Vector3Int normal; public Goal(int id,Vector3Int n) { blockId=id; normal=n; } }
    public sealed class BoardState
    {
        public Vector3Int min = new Vector3Int(-8,-8,-8), max = new Vector3Int(8,8,8);
        public bool bounded=true;
        public Dictionary<Vector3Int,Block> blocks = new Dictionary<Vector3Int,Block>();
        public Rabbit rabbit;
        public Goal goal;
        public int turns;
        public bool cleared;
        public bool Inside(Vector3Int p) { return !bounded||(p.x>=min.x&&p.y>=min.y&&p.z>=min.z&&p.x<=max.x&&p.y<=max.y&&p.z<=max.z); }
        public bool Occupied(Vector3Int p) { return Inside(p)&&blocks.ContainsKey(p); }
        public BoardState Clone() { return new BoardState { min=min,max=max,bounded=bounded,blocks=new Dictionary<Vector3Int,Block>(blocks),rabbit=rabbit,goal=goal,turns=turns,cleared=cleared }; }
        public bool IsWeightless { get { return Gravity.Choose(this)==Vector3Int.zero; } }
        public Block GoalBlock() { return blocks.Values.First(b=>b.id==goal.blockId); }
        public bool AtGoal() { var b=GoalBlock(); return !IsWeightless&&rabbit.cell==b.cell+goal.normal&&rabbit.down==-goal.normal; }
        public string Key(bool includeTurn=false)
        {
            return string.Join(";",blocks.Values.OrderBy(b=>b.id).Select(b=>b.id+":"+b.cell+":"+b.kind))+"|"+rabbit.cell+"|"+rabbit.down+"|"+rabbit.forward+"|"+cleared+(includeTurn?"|"+turns:"");
        }
    }
    public struct GravityReading
    {
        public Vector3Int direction;
        public int score, distance, count;
        public float Strength { get { return score; } }
    }
    public static class Gravity
    {
        public static int CountRun(BoardState b,Vector3Int first,Vector3Int d)
        {
            int count=0; while(b.Occupied(first)) { count++; first+=d; } return count;
        }
        public static GravityReading Read(BoardState b,Vector3Int p,Vector3Int d)
        {
            // A run contributes only when its first block shares a face with the rabbit cell.
            int count=CountRun(b,p+d,d);
            return new GravityReading { direction=d,distance=count>0?1:0,count=count,score=count };
        }
        public static GravityReading[] All(BoardState b)
        {
            var readings=Axes.All.Select(d=>Read(b,b.rabbit.cell,d)).ToArray();
            // Cancel opposite runs on each axis before comparing different axes.
            for(int i=0;i<readings.Length;i+=2)
            {
                int difference=readings[i].count-readings[i+1].count;
                readings[i].score=Math.Max(0,difference);
                readings[i+1].score=Math.Max(0,-difference);
            }
            return readings;
        }
        public static bool CanTransfer(BoardState b,Vector3Int direction)
        {
            var readings=All(b);int maximum=readings.Max(x=>x.score);
            return maximum>0&&Axes.Dot(direction,b.rabbit.down)==0
                &&readings.Any(x=>x.direction==b.rabbit.down&&x.score==maximum)
                &&readings.Any(x=>x.direction==direction&&x.score==maximum);
        }
        public static Vector3Int Choose(BoardState b)
        {
            var readings=All(b);
            int max=readings.Max(r=>r.score);
            if(max==0) return Vector3Int.zero;
            foreach(var r in readings) if(r.direction==b.rabbit.down&&r.score==max) return r.direction;
            return readings.First(r=>r.score==max).direction;
        }
    }
    public sealed class Motion
    {
        public MotionKind kind;
        public Rabbit before, after;
        public Dictionary<int,Vector3Int> blockFrom, blockTo;
        public Motion(MotionKind kind,Rabbit before,Rabbit after) { this.kind=kind; this.before=before; this.after=after; }
    }
    public sealed class ActionResult
    {
        public BoardState state;
        public Reject reason;
        public List<Motion> motions = new List<Motion>();
        public bool Success { get { return reason==Reject.None&&state!=null; } }
    }
    public static class Resolver
    {
        public static ActionResult Try(BoardState source,Command command)
        {
            var result=new ActionResult { state=source.Clone() };
            var b=result.state; var r=b.rabbit;
            // Keep the orientation as a reference in zero gravity, but no walking or slamming.
            if(b.IsWeightless&&command!=Command.A&&command!=Command.D)return Fail(result,Reject.NoGravity);
            if(command==Command.Slam)
            {
                var run=new List<Block>();
                var q=r.cell+r.down;
                while(b.Occupied(q)) { run.Add(b.blocks[q]); q+=r.down; }
                if(run.Count==0) return Fail(result,Reject.InvalidSupport);
                var sourceCells=new HashSet<Vector3Int>(run.Select(x=>x.cell));
                foreach(var block in run)
                {
                    var next=block.cell+r.down;
                    if(!b.Inside(next)) return Fail(result,Reject.OutOfBounds);
                    if(next==r.cell||(b.Occupied(next)&&!sourceCells.Contains(next))) return Fail(result,Reject.Occupied);
                }
                b.rabbit.cell+=r.down;
                var motion=new Motion(MotionKind.Blocks,r,b.rabbit) { blockFrom=run.ToDictionary(x=>x.id,x=>x.cell),blockTo=run.ToDictionary(x=>x.id,x=>x.cell+r.down) };
                foreach(var block in run) b.blocks.Remove(block.cell);
                foreach(var block in run) { var moved=block; moved.cell+=r.down; b.blocks.Add(moved.cell,moved); }
                result.motions.Add(motion);
            }
            else if(command==Command.A||command==Command.D)
            {
                b.rabbit.forward=command==Command.D?r.Right:-r.Right;
                result.motions.Add(new Motion(MotionKind.Turn,r,b.rabbit));
            }
            else
            {
                var t=command==Command.W?r.forward:-r.forward;
                var next=r.cell+t;
                if(!b.Inside(next)) return Fail(result,Reject.OutOfBounds);
                if(b.Occupied(next))
                {
                    if(command!=Command.W||!Gravity.CanTransfer(b,r.forward))return Fail(result,Reject.Occupied);
                    // The adjacent block is the new floor. Rotate within the empty rabbit cell.
                    b.rabbit.Turn(r.forward);
                    result.motions.Add(new Motion(MotionKind.Rotate,r,b.rabbit));
                }
                else
                {
                    if(!b.Occupied(next+r.down)) return Fail(result,Reject.CliffBlocked);
                    b.rabbit.cell=next;
                    result.motions.Add(new Motion(MotionKind.Walk,r,b.rabbit));
                }
            }
            var reject=ResolveStable(b,result.motions);
            if(reject!=Reject.None) return Fail(result,reject);
            b.turns++;
            b.cleared=b.AtGoal();
            return result;
        }
        static ActionResult Fail(ActionResult r,Reject reason) { r.reason=reason; r.state=null; r.motions.Clear(); return r; }
        // Only the trial state is modified. Rendering never supplies coordinates.
        public static Reject ResolveStable(BoardState trial,List<Motion> events=null,int limit=1024)
        {
            if(limit<=0) return Reject.ResolveLimit;
            var r=trial.rabbit;
            var d=Gravity.Choose(trial);
            // Zero gravity is a valid resulting state, so entering it is not rolled back.
            if(d==Vector3Int.zero) return Reject.None;
            if(d!=r.down)
            {
                trial.rabbit.Turn(d);
                if(events!=null) events.Add(new Motion(MotionKind.Rotate,r,trial.rabbit));
            }
            // All candidates already touch this cell. Rotation cannot change the sampled runs.
            return Reject.None;
        }
        public static string Validate(BoardState b)
        {
            if(!Axes.Unit(b.rabbit.down)||!Axes.Unit(b.rabbit.forward)||Axes.Dot(b.rabbit.down,b.rabbit.forward)!=0) return "Invalid rabbit frame";
            if(!b.Inside(b.rabbit.cell)||b.Occupied(b.rabbit.cell)) return "Invalid rabbit cell";
            var ids=new HashSet<int>();
            foreach(var pair in b.blocks)
                if(pair.Key!=pair.Value.cell||!b.Inside(pair.Key)||!ids.Add(pair.Value.id)) return "Invalid block coordinates or duplicate ID";
            if(!ids.Contains(b.goal.blockId)||!Axes.Unit(b.goal.normal)) return "Invalid goal";
            var goalCell=b.GoalBlock().cell+b.goal.normal;
            if(!b.Inside(goalCell)||b.Occupied(goalCell)) return "Goal face is blocked";
            var gravity=Gravity.Choose(b);
            if(gravity!=Vector3Int.zero&&(!b.Occupied(b.rabbit.cell+b.rabbit.down)||gravity!=b.rabbit.down)) return "Unstable starting pose";
            return null;
        }
        public static string Message(Reject r)
        {
            switch(r)
            {
                case Reject.CliffBlocked:return "발밑이 이어지는 칸으로만 걸을 수 있어요.";
                case Reject.NoGravity:return "착지할 중력이 없어 이 행동은 취소돼요.";
                case Reject.OutOfBounds:return "행성의 이동 경계에 도달했어요.";
                case Reject.Occupied:return "블록이 길을 막고 있어요.";
                default:return "안정된 지지면을 찾을 수 없어요. 다른 길을 찾아보세요.";
            }
        }
    }
    public sealed class GameSession
    {
        public BoardState Current { get; private set; }
        public BoardState Initial { get; private set; }
        readonly Stack<BoardState> history=new Stack<BoardState>();
        public int UndoCount { get { return history.Count; } }
        public GameSession(BoardState initial)
        {
            string error=Resolver.Validate(initial);
            if(error!=null) throw new ArgumentException(error);
            Initial=initial.Clone(); Current=initial.Clone();
        }
        public ActionResult Preview(Command command) { return Resolver.Try(Current,command); }
        public ActionResult Act(Command command)
        {
            var result=Preview(command);
            if(result.Success) { history.Push(Current.Clone()); Current=result.state; }
            return result;
        }
        public bool Undo() { if(history.Count==0) return false; Current=history.Pop(); return true; }
        public void Restart() { Current=Initial.Clone(); history.Clear(); }
    }
}
