using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Grabbit
{
    // The same portable file is used by the maker, the game and external authoring tools.
    [Serializable]
    public sealed class GrabbitMap
    {
        public int version=1;
        public string id="", title="새 맵";
        public List<Vector3Int> boxes=new List<Vector3Int>();
        public bool hasRabbit, hasFlag;
        public Rabbit rabbit=new Rabbit(Vector3Int.up,Vector3Int.down,Vector3Int.forward);
        public Vector3Int flagBlock, flagNormal=Vector3Int.up;
        public float pitch=28, yaw=142;

        public GrabbitMap Copy() { return JsonUtility.FromJson<GrabbitMap>(JsonUtility.ToJson(this)); }
        public static GrabbitMap FromBoard(BoardState board,string title="새 맵")
        {
            return new GrabbitMap { title=title,boxes=board.blocks.Values.OrderBy(b=>b.id).Select(b=>b.cell).ToList(),
                hasRabbit=true,rabbit=board.rabbit,hasFlag=true,flagBlock=board.GoalBlock().cell,flagNormal=board.goal.normal };
        }
        public bool TryBoard(out BoardState board,out string message)
        {
            board=null;
            if(version!=1){message="지원하지 않는 맵 파일 버전입니다.";return false;}
            if(boxes==null||boxes.Count==0){message="블록을 하나 이상 배치하세요.";return false;}
            if(boxes.Count>2048){message="한 맵에는 블록을 최대 2,048개 배치할 수 있습니다.";return false;}
            if(new HashSet<Vector3Int>(boxes).Count!=boxes.Count){message="같은 칸에 블록이 겹쳐 있습니다.";return false;}
            if(!hasRabbit){message="토끼 시작 위치를 배치하세요.";return false;}
            if(!Axes.Unit(rabbit.down)||!Axes.Unit(rabbit.forward)||Axes.Dot(rabbit.down,rabbit.forward)!=0)
            {message="토끼의 발 방향과 시선은 서로 직각이어야 합니다.";return false;}
            if(boxes.Contains(rabbit.cell)){message="토끼 시작 칸을 비워 주세요.";return false;}
            if(!hasFlag||!boxes.Contains(flagBlock)){message="블록 위에 깃발을 배치하세요.";return false;}
            if(!Axes.Unit(flagNormal)){message="깃발이 붙는 면을 선택하세요.";return false;}
            if(boxes.Contains(flagBlock+flagNormal)){message="깃발 앞의 한 칸을 비워 주세요.";return false;}
            if(float.IsNaN(pitch)||float.IsInfinity(pitch)||float.IsNaN(yaw)||float.IsInfinity(yaw))
            {message="카메라 각도가 올바르지 않습니다.";return false;}
            var state=new BoardState { bounded=false,rabbit=rabbit };
            for(int i=0;i<boxes.Count;i++)state.blocks.Add(boxes[i],new Block(i+1,boxes[i]));
            state.goal=new Goal(state.blocks[flagBlock].id,flagNormal);
            string error=Resolver.Validate(state);
            if(error!=null){message="발 방향을 시작 위치의 가장 강한 중력 방향에 맞춰 주세요.";return false;}
            if(state.AtGoal()){message="시작 위치와 깃발 도착 위치를 다르게 배치하세요.";return false;}
            board=state;
            message=state.IsWeightless?"시작 위치가 무중력입니다. W/S/Space는 멈추고 A/D만 사용할 수 있습니다.":"테스트할 수 있습니다.";
            return true;
        }
        public static bool TryParse(string json,out GrabbitMap map,out string message)
        {
            map=null;
            if(string.IsNullOrWhiteSpace(json)||json.Length>1000000){message="맵 JSON을 확인하세요 (최대 1MB).";return false;}
            try
            {
                var candidate=JsonUtility.FromJson<GrabbitMap>(json);
                if(candidate==null||candidate.version!=1||candidate.boxes==null||candidate.boxes.Count>2048)
                    throw new FormatException("지원하는 맵 형식이 아닙니다.");
                if(candidate.boxes.Any(p=>Math.Abs((long)p.x)>10000||Math.Abs((long)p.y)>10000||Math.Abs((long)p.z)>10000))
                    throw new FormatException("제작 좌표 범위는 -10,000 ~ 10,000입니다.");
                map=candidate;message="맵을 불러왔습니다.";return true;
            }
            catch(Exception e){message="맵을 읽을 수 없습니다: "+e.Message;return false;}
        }
    }

    public static class GrabbitMapStore
    {
        [Serializable] sealed class Catalog { public List<string> deletedBuiltIns=new List<string>(); }
        public static string Folder {
            get {
#if UNITY_EDITOR
                return Path.GetFullPath(Path.Combine(Application.dataPath,"../UserMaps"));
#else
                return Path.Combine(Application.persistentDataPath,"Maps");
#endif
            }
        }
        public static string Save(GrabbitMap map,string folder=null)
        {
            folder=folder??Folder;Directory.CreateDirectory(folder);
            if(!Guid.TryParseExact(map.id,"N",out _)||File.Exists(Path.Combine(folder,"Trash",map.id+".json")))map.id=Guid.NewGuid().ToString("N");
            string path=Path.Combine(folder,map.id+".json");
            Write(path,JsonUtility.ToJson(map,true));return path;
        }
        static void Write(string path,string json)
        {
            string temp=path+".tmp";File.WriteAllText(temp,json);
            if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);
        }
        public static HashSet<string> DeletedBuiltIns(string folder=null)
        {
            string path=Path.Combine(folder??Folder,"catalog.json");
            if(!File.Exists(path))return new HashSet<string>();
            var catalog=JsonUtility.FromJson<Catalog>(File.ReadAllText(path));
            return new HashSet<string>(catalog?.deletedBuiltIns??new List<string>());
        }
        static bool BuiltIn(string id) { return Levels.BuiltIn.Any(level=>level.mapId==id); }
        static bool SetBuiltInDeleted(string id,bool deleted,string folder)
        {
            var ids=DeletedBuiltIns(folder);bool changed=deleted?ids.Add(id):ids.Remove(id);
            if(!changed)return false;
            Directory.CreateDirectory(folder);Write(Path.Combine(folder,"catalog.json"),JsonUtility.ToJson(new Catalog {deletedBuiltIns=ids.OrderBy(x=>x).ToList()},true));
            return true;
        }
        public static bool Trash(string id,string folder=null)
        {
            folder=Path.GetFullPath(folder??Folder);
            if(BuiltIn(id))return SetBuiltInDeleted(id,true,folder);
            if(!Guid.TryParseExact(id,"N",out _))return false;
            string source=Path.GetFullPath(Path.Combine(folder,id+".json"));
            string trash=Path.GetFullPath(Path.Combine(folder,"Trash"));
            string target=Path.GetFullPath(Path.Combine(trash,id+".json"));
            if(Path.GetDirectoryName(source)!=folder||Path.GetDirectoryName(target)!=trash)return false;
            if(!File.Exists(source))return false;
            Directory.CreateDirectory(trash);File.Move(source,target);return true;
        }
        public static bool Restore(string id,string folder=null)
        {
            folder=Path.GetFullPath(folder??Folder);
            if(BuiltIn(id))return SetBuiltInDeleted(id,false,folder);
            if(!Guid.TryParseExact(id,"N",out _))return false;
            string trash=Path.GetFullPath(Path.Combine(folder,"Trash"));
            string source=Path.GetFullPath(Path.Combine(trash,id+".json"));
            string target=Path.GetFullPath(Path.Combine(folder,id+".json"));
            if(Path.GetDirectoryName(source)!=trash||Path.GetDirectoryName(target)!=folder)return false;
            if(!File.Exists(source))return false;
            File.Move(source,target);return true;
        }
        public static List<GrabbitMap> Library(bool trash=false,string folder=null)
        {
            folder=folder??Folder;var deleted=DeletedBuiltIns(folder);var maps=new List<GrabbitMap>();
            for(int i=0;i<Levels.BuiltIn.Length;i++)
            {
                var level=Levels.BuiltIn[i];if(deleted.Contains(level.mapId)!=trash)continue;
                var map=GrabbitMap.FromBoard(level.start,"기본 스테이지 "+(i+1));map.id=level.mapId;map.pitch=level.pitch;map.yaw=level.yaw;maps.Add(map);
            }
            maps.AddRange(LoadAll(trash?Path.Combine(folder,"Trash"):folder));return maps;
        }
        public static List<GrabbitMap> LoadAll(string folder=null)
        {
            folder=folder??Folder;
            var result=new List<GrabbitMap>();
            if(!Directory.Exists(folder))return result;
            foreach(var path in Directory.GetFiles(folder,"*.json").OrderBy(p=>p,StringComparer.Ordinal))
            {
                if(!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"N",out _))continue;
                try { if(GrabbitMap.TryParse(File.ReadAllText(path),out var map,out _))
                    {map.id=Path.GetFileNameWithoutExtension(path);result.Add(map);} }
                catch(IOException e){Debug.LogWarning("GRABBIT map: "+e.Message);}
            }
            return result;
        }
        public static void SaveDraft(GrabbitMap map)
        { Directory.CreateDirectory(Folder);Write(Path.Combine(Folder,"draft.json"),JsonUtility.ToJson(map,true)); }
        public static GrabbitMap LoadDraft()
        {
            try { string path=Path.Combine(Folder,"draft.json");
                if(File.Exists(path)&&GrabbitMap.TryParse(File.ReadAllText(path),out var map,out _))return map; }
            catch(IOException e){Debug.LogWarning("GRABBIT draft: "+e.Message);}
            return GrabbitMap.FromBoard(Levels.BuiltIn[0].start);
        }
    }
}
