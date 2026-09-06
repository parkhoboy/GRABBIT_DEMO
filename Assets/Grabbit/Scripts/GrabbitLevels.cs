using System;
using System.Collections.Generic;
using UnityEngine;

namespace Grabbit
{
    public sealed class Level
    {
        public string mapId;
        public string title;
        public BoardState start;
        public Command[] exercise;
        public float pitch=28, yaw=142;
        public Level(string title,BoardState start,params Command[] exercise)
        { this.title=title;this.start=start;this.exercise=exercise; }
    }
    public static class Levels
    {
        static Vector3Int P(int x,int y,int z=0) { return new Vector3Int(x,y,z); }
        static BoardState New(Vector3Int rabbit)
        {
            return new BoardState { bounded=false,rabbit=new Rabbit(rabbit,Vector3Int.down,Vector3Int.forward) };
        }
        static void Add(BoardState b,int x,int y,int z=0)
        {
            var p=P(x,y,z);
            if(b.blocks.ContainsKey(p)) throw new ArgumentException("Duplicate level cell: "+p);
            b.blocks.Add(p,new Block(b.blocks.Count+1,p));
        }
        static void GoalAt(BoardState b,Vector3Int block,Vector3Int normal) { b.goal=new Goal(b.blocks[block].id,normal); }
        public static Level[] BuiltIn { get; }=Build();
        public static Level[] All { get; private set; }=(Level[])BuiltIn.Clone();
        public static void RefreshMaps()
        {
            var deleted=GrabbitMapStore.DeletedBuiltIns();
            var levels=new List<Level>();
            foreach(var level in BuiltIn)if(!deleted.Contains(level.mapId))levels.Add(level);
            foreach(var map in GrabbitMapStore.LoadAll())
                if(map.TryBoard(out var board,out _))
                    levels.Add(new Level(map.title,board) { mapId=map.id,pitch=map.pitch,yaw=map.yaw });
            All=levels.ToArray();
        }
        static Level[] Build()
        {
            var levels=new List<Level>();
            var b=New(P(-2,1,-1));
            for(int x=-2;x<=2;x++) for(int z=-1;z<=1;z++) Add(b,x,0,z);
            GoalAt(b,P(2,0,1),Vector3Int.up);
            levels.Add(new Level("전후 이동 / 제자리 회전 / 한 블록 내려찍기",b,
                Command.D,Command.W,Command.W,Command.A,Command.W,Command.S,Command.Slam));

            b=New(P(0,1));
            Add(b,0,0);Add(b,0,-1);Add(b,0,-2);
            for(int x=1;x<=3;x++) Add(b,x,-1);
            Add(b,3,-1,1);Add(b,2,-1,1);
            GoalAt(b,P(3,-1),Vector3Int.up);
            levels.Add(new Level("토끼와 세 블록 동반 이동 / 옆가지 분리",b,
                Command.Slam,Command.D,Command.W,Command.W,Command.W,Command.Slam));

            b=New(P(0,1));
            Add(b,0,0);Add(b,0,-1);
            for(int x=1;x<=2;x++) for(int y=1;y<=3;y++) for(int z=0;z<=1;z++) Add(b,x,y,z);
            // This longer row touches only the rabbit's cell AFTER the slam.
            for(int x=1;x<=3;x++) for(int z=0;z<=1;z++) Add(b,x,0,z);
            GoalAt(b,P(1,3,1),Vector3Int.left);
            levels.Add(new Level("이동한 셀에서 중력 재계산 / 벽 회전",b,
                Command.Slam,Command.W,Command.D,Command.W,Command.W,Command.W));

            b=New(P(0,1));
            for(int x=0;x<=3;x++)Add(b,x,0);
            for(int x=1;x<=3;x++) for(int y=3;y<=5;y++) for(int z=0;z<=1;z++) Add(b,x,y,z);
            GoalAt(b,P(3,0),Vector3Int.up);
            // The reachable floor goal keeps the separated ceiling available for the gap test.
            var ceiling=new Level("빈칸 하나 너머 천장 / 중력 영향 없음",b,
                Command.D,Command.W,Command.S,Command.A,Command.Slam);
            ceiling.pitch=-18; levels.Add(ceiling);

            b=New(P(0,1,-1));
            Add(b,0,0,-1);Add(b,0,-1,-1);Add(b,0,0);Add(b,0,-1);
            for(int x=1;x<=2;x++) for(int y=1;y<=3;y++) for(int z=-1;z<=1;z++) Add(b,x,y,z);
            for(int x=1;x<=3;x++) for(int z=-1;z<=1;z++) Add(b,x,0,z);
            GoalAt(b,P(1,3,1),Vector3Int.left);
            levels.Add(new Level("모든 블록 가동 / 복합 경로",b,
                Command.W,Command.Slam,Command.W,Command.D,Command.W,Command.W,Command.W));
            for(int i=0;i<levels.Count;i++)levels[i].mapId="builtin-"+(i+1);
            return levels.ToArray();
        }
    }
}
