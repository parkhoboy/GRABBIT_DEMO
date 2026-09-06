using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Grabbit.Editor
{
    public static class GrabbitMapVerification
    {
        [MenuItem("GRABBIT/Run map maker verification")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            int checks=0;
            Action<bool,string> check=(ok,label)=>{checks++;if(!ok)throw new Exception("Map verification: "+label);};
            for(int i=0;i<5;i++)
            {
                var source=Levels.BuiltIn[i].start;var map=GrabbitMap.FromBoard(source);
                check(map.TryBoard(out var roundtrip,out _),"Built-in map validates");
                check(source.Key()==roundtrip.Key()&&source.goal.blockId==roundtrip.goal.blockId&&source.goal.normal==roundtrip.goal.normal,"Map preserves the board and flag identity");
                check(GrabbitMap.TryParse(JsonUtility.ToJson(map),out var parsed,out _)&&parsed.TryBoard(out _,out _),"Portable JSON roundtrip");
                var copy=map.Copy();copy.boxes.Clear();check(map.boxes.Count>0,"Independent editable copy");
            }
            var invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);
            invalid.boxes.Add(invalid.boxes[0]);check(!invalid.TryBoard(out _,out _),"Duplicate box refused");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.hasRabbit=false;check(!invalid.TryBoard(out _,out _),"Missing rabbit refused");
            invalid.hasRabbit=true;invalid.rabbit.cell=invalid.boxes[0];check(!invalid.TryBoard(out _,out _),"Rabbit cannot overlap a box");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.flagNormal=Vector3Int.down;invalid.boxes.Add(invalid.flagBlock+invalid.flagNormal);check(!invalid.TryBoard(out _,out _),"Blocked flag face refused");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.rabbit.down=Vector3Int.right;check(!invalid.TryBoard(out _,out _),"Unstable initial gravity is refused");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.rabbit.forward=invalid.rabbit.down;check(!invalid.TryBoard(out _,out _),"Parallel frame refused");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.rabbit.cell=invalid.flagBlock+invalid.flagNormal;check(!invalid.TryBoard(out _,out _),"Starting at the flag is refused");
            invalid=GrabbitMap.FromBoard(Levels.BuiltIn[0].start);invalid.rabbit.cell=new Vector3Int(7,7,7);check(invalid.TryBoard(out var weightless,out var warning)&&weightless.IsWeightless&&warning.Contains("무중력"),"Weightless tests remain possible with explanation");
            check(!GrabbitMap.TryParse("not json",out _,out _),"Invalid JSON refused");
            check(!GrabbitMap.TryParse("{\"version\":20}",out _,out _),"Unknown version refused");

            var go=new GameObject("Map maker isolated verification");
            try
            {
                var maker=go.AddComponent<GrabbitMapMaker>();maker.enabled=false;maker.ReplaceMap(new GrabbitMap());
                maker.Brush=MapBrush.Box;
                check(maker.Paint(Vector3Int.zero),"Place box");check(!maker.Paint(Vector3Int.zero),"Painting duplicate has no effect");
                maker.Brush=MapBrush.Rabbit;check(maker.Paint(Vector3Int.zero)&&maker.Map.rabbit.cell==Vector3Int.up,"Rabbit places on selected block surface");
                maker.Brush=MapBrush.Box;check(!maker.Paint(Vector3Int.up),"Cannot paint over rabbit");
                check(maker.Paint(Vector3Int.forward),"Place goal support");maker.Brush=MapBrush.Flag;
                check(!maker.Paint(Vector3Int.one*8),"Flag needs a block");check(maker.Paint(Vector3Int.forward),"Place flag");
                check(maker.Map.TryBoard(out _,out _),"Authored map is playable");
                string before=JsonUtility.ToJson(maker.Map);maker.Brush=MapBrush.Erase;maker.Paint(Vector3Int.forward);
                check(!maker.Map.hasFlag,"Erasing goal support clears flag");maker.UndoEdit();check(JsonUtility.ToJson(maker.Map)==before,"Undo restores whole map including flag");
                maker.RedoEdit();check(!maker.Map.hasFlag&&!maker.Map.boxes.Contains(Vector3Int.forward),"Redo restores erase");maker.UndoEdit();
                foreach(var down in Axes.All)foreach(var forward in Axes.All.Where(f=>Axes.Dot(f,down)==0))
                {
                    maker.SetDown(down);maker.SetForward(forward);
                    check(maker.Map.rabbit.down==down&&maker.Map.rabbit.forward==forward,"All 24 initial frames can be selected");
                }
                maker.Plane=0;maker.Layer=3;check(maker.GridCell(2,-4)==new Vector3Int(2,3,-4),"XZ layer coordinate");
                maker.Plane=1;check(maker.GridCell(2,-4)==new Vector3Int(2,-4,3),"XY layer coordinate");
                maker.Plane=2;check(maker.GridCell(2,-4)==new Vector3Int(3,-4,2),"ZY layer coordinate");
                before=JsonUtility.ToJson(maker.Map);check(!maker.ImportJson("invalid")&&JsonUtility.ToJson(maker.Map)==before,"Failed import preserves draft");
                check(maker.ImportJson(JsonUtility.ToJson(GrabbitMap.FromBoard(Levels.BuiltIn[1].start)))&&maker.Map.id=="","Imported map gets a new identity");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
            string folder=Path.GetFullPath("Captures/MapVerification/"+Guid.NewGuid().ToString("N"));
            var saved=GrabbitMap.FromBoard(Levels.BuiltIn[0].start,"저장 검증");
            string path=GrabbitMapStore.Save(saved,folder);check(File.Exists(path)&&Guid.TryParseExact(saved.id,"N",out _),"Save creates portable file and safe ID");
            saved.title="수정한 맵";check(GrabbitMapStore.Save(saved,folder)==path&&Directory.GetFiles(folder,"*.json").Length==1,"Save updates the same map atomically");
            var loaded=GrabbitMapStore.LoadAll(folder);check(loaded.Count==1&&loaded[0].title==saved.title&&loaded[0].TryBoard(out _,out _),"Saved map reloads correctly");
            saved.id="../../escape";string safe=GrabbitMapStore.Save(saved,folder);check(Path.GetDirectoryName(safe)==folder,"Imported ID cannot escape map directory");
            File.WriteAllText(Path.Combine(folder,Guid.NewGuid().ToString("N")+".json"),"invalid");check(GrabbitMapStore.LoadAll(folder).Count==2,"Broken file does not hide other maps");
            string originalId=saved.id,originalJson=File.ReadAllText(safe);
            check(GrabbitMapStore.Trash(originalId,folder)&&!File.Exists(safe),"Delete removes saved map from active storage");
            check(GrabbitMapStore.LoadAll(folder).Count==1&&GrabbitMapStore.Library(true,folder).Any(m=>m.id==originalId),"Deleted map is only in trash");
            check(!GrabbitMapStore.Trash(originalId,folder),"Repeated deletion is harmless");
            check(GrabbitMapStore.Restore(originalId,folder)&&File.ReadAllText(safe)==originalJson,"Restore preserves map ID and every saved field");
            check(!GrabbitMapStore.Trash("../draft",folder)&&!GrabbitMapStore.Restore("../draft",folder),"Delete and restore reject paths");
            check(GrabbitMapStore.Trash(originalId,folder),"Delete again after restore");
            GrabbitMapStore.Save(saved,folder);check(saved.id!=originalId,"Saving a deleted draft creates a new map without replacing trash");
            check(GrabbitMapStore.Restore(originalId,folder),"Original map still restores after saving its draft");
            var builtin=Levels.BuiltIn[0];
            check(GrabbitMapStore.Trash(builtin.mapId,folder)&&GrabbitMapStore.DeletedBuiltIns(folder).Contains(builtin.mapId),"Built-in deletion is persisted");
            check(!GrabbitMapStore.Library(false,folder).Any(m=>m.id==builtin.mapId)&&GrabbitMapStore.Library(true,folder).Any(m=>m.id==builtin.mapId),"Built-in moves between library tabs");
            check(GrabbitMapStore.Restore(builtin.mapId,folder)&&!GrabbitMapStore.DeletedBuiltIns(folder).Contains(builtin.mapId),"Built-in restore is persisted");
            foreach(var level in Levels.BuiltIn)GrabbitMapStore.Trash(level.mapId,folder);
            check(!GrabbitMapStore.Library(false,folder).Any(m=>m.id.StartsWith("builtin-")),"All built-in maps can be removed");
            check(GrabbitMapStore.Library(true,folder).Count(m=>m.id.StartsWith("builtin-"))==5,"All built-in maps remain recoverable");
            string report="PASSED / "+checks+" map maker assertions\nBoard/JSON roundtrip, validation, placement, 24 orientations, undo/redo, import, atomic save and reload.\nTest files: "+folder;
            File.WriteAllText("Captures/Grabbit-map-verification.txt",report);return report;
        }
    }
}

