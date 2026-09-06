using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Grabbit
{
    public sealed class BoardView : MonoBehaviour
    {
        [NonSerialized] public Dictionary<int,Transform> blocks = new Dictionary<int,Transform>();
        public Transform rabbit, goal, previewRoot, arrowsRoot;
        public Camera viewCamera;
        public Vector3 center;
        public float radius;
        readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        Mesh cubeMesh;
        public static readonly Color Mint=new Color(.40f,.91f,.80f);
        public static readonly Color Gold=new Color(1f,.79f,.28f);
        public static readonly Color Navy=new Color(.025f,.045f,.075f);
        public static Quaternion Rotation(Rabbit r) { return Quaternion.LookRotation(r.forward,r.Up); }
        public static Vector3 RabbitPosition(Rabbit r) { return (Vector3)r.cell+(Vector3)r.down*.47f; }
        public Material Material(string name,Color color,bool unlit=false)
        {
            if(materials.TryGetValue(name,out var found)) return found;
            var m=new Material(Shader.Find(unlit?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));
            m.name="Grabbit "+name;
            m.SetColor("_BaseColor",color);
            if(!unlit) { m.SetFloat("_Smoothness",.35f); m.SetFloat("_Metallic",.18f); }
            materials.Add(name,m); return m;
        }
        Transform Empty(string name,Transform parent)
        {
            var t=new GameObject(name).transform; t.SetParent(parent,false); return t;
        }
        public Transform Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material mat)
        {
            var g=GameObject.CreatePrimitive(type); g.name=name;
            g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;
            var collider=g.GetComponent<Collider>();
            if(Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            var renderer=g.GetComponent<MeshRenderer>();renderer.sharedMaterial=mat;
            return g.transform;
        }
        public Transform Cube(string name,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool bevel=false)
        {
            if(!bevel) return Shape(name,PrimitiveType.Cube,parent,pos,scale,mat);
            var t=Empty(name,parent);t.localPosition=pos;t.localScale=scale;
            var mf=t.gameObject.AddComponent<MeshFilter>();
            if(cubeMesh==null) cubeMesh=BeveledCube();
            mf.sharedMesh=cubeMesh;t.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;return t;
        }
        // Six chamfered face plates plus eight bevel corners form a closed rounded cube.
        Mesh BeveledCube()
        {
            var v=new List<Vector3>();var tris=new List<int>();
            Action<Vector3[]> poly=points=>{
                int start=v.Count;v.AddRange(points);
                for(int i=1;i<points.Length-1;i++){tris.Add(start);tris.Add(start+i);tris.Add(start+i+1);}
            };
            float h=.5f,k=.448f;
            foreach(var axisI in Axes.All)
            {
                Vector3 n=axisI;
                Vector3 a=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.up;
                Vector3 b=Vector3.Cross(n,a);
                poly(new[]{n*h-a*k-b*k,n*h+a*k-b*k,n*h+a*k+b*k,n*h-a*k+b*k});
            }
            for(int axis=0;axis<3;axis++) for(int s=-1;s<=1;s+=2) for(int t=-1;t<=1;t+=2)
            {
                Vector3 n=Vector3.zero,m=Vector3.zero,a=Vector3.zero;
                n[(axis+1)%3]=s;m[(axis+2)%3]=t;a[axis]=k;
                var p=new[]{n*h+m*k-a,n*k+m*h-a,n*k+m*h+a,n*h+m*k+a};
                if(Vector3.Dot(Vector3.Cross(p[1]-p[0],p[2]-p[0]),n+m)<0) Array.Reverse(p);
                poly(p);
            }
            for(int x=-1;x<=1;x+=2) for(int y=-1;y<=1;y+=2) for(int z=-1;z<=1;z+=2)
            {
                var p=new[]{new Vector3(x*h,y*k,z*k),new Vector3(x*k,y*h,z*k),new Vector3(x*k,y*k,z*h)};
                if(Vector3.Dot(Vector3.Cross(p[1]-p[0],p[2]-p[0]),new Vector3(x,y,z))<0) Array.Reverse(p);
                poly(p);
            }
            var mesh=new Mesh {name="Grabbit chamfered cube"};
            mesh.SetVertices(v);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
        public void Build(BoardState state,Camera camera)
        {
            viewCamera=camera;
            var shell=Material("porcelain",new Color(.64f,.80f,.81f));
            var plate=Material("movable face",new Color(.21f,.47f,.49f));
            var inset=Material("inset",new Color(.055f,.12f,.17f));
            var glow=Material("mint light",Mint,true);
            var low=(Vector3)state.rabbit.cell;var high=low;
            foreach(var block in state.blocks.Values)
            {
                var root=Empty("Block "+block.id+" "+block.kind,transform);root.position=block.cell;blocks.Add(block.id,root);
                Cube("Chamfered chassis",root,Vector3.zero,Vector3.one*.965f,shell,true);
                foreach(var dirI in Axes.All)
                {
                    Vector3 normal=dirI;
                    var face=Empty("Face "+Axes.Label(dirI),root);
                    face.localRotation=Quaternion.LookRotation(normal,Mathf.Abs(normal.y)>.5f?Vector3.forward:Vector3.up);
                    face.localPosition=normal*.49f;
                    Cube("Inset panel",face,Vector3.zero,new Vector3(.72f,.72f,.014f),plate,true);
                    Cube("Status groove",face,new Vector3(0,-.265f,.015f),new Vector3(.25f,.023f,.02f),glow);
                    for(int a=-1;a<=1;a+=2) for(int b=-1;b<=1;b+=2)
                        Shape("Rivet",PrimitiveType.Sphere,face,new Vector3(a*.30f,b*.30f,.017f),Vector3.one*.028f,inset);
                    Cube("North mark",face,new Vector3(0,.21f,.016f),new Vector3(.024f,.09f,.02f),glow);
                    Cube("West mark",face,new Vector3(-.21f,0,.016f),new Vector3(.09f,.024f,.02f),glow);
                    Cube("East mark",face,new Vector3(.21f,0,.016f),new Vector3(.09f,.024f,.02f),glow);
                }
                low=Vector3.Min(low,block.cell);high=Vector3.Max(high,block.cell);
            }
            center=(low+high)*.5f+Vector3.up*.3f;radius=Mathf.Max(3.4f,(high-low).magnitude*.62f);
            rabbit=Empty("GRABBIT / astronaut rabbit",transform);BuildRabbit(rabbit);
            goal=Empty("Goal / attached flag",transform);BuildGoal(goal);
            arrowsRoot=Empty("Forward and backward",transform);
            previewRoot=Empty("Action preview",transform);
            Snap(state);
        }
        void BuildRabbit(Transform root)
        {
            var white=Material("suit",new Color(.93f,.96f,.91f));
            var soft=Material("suit shadow",new Color(.52f,.68f,.74f));
            var black=Material("visor",new Color(.015f,.030f,.055f));
            var gold=Material("expression",Gold,true);var mint=Material("oxygen",Mint,true);
            Shape("Body",PrimitiveType.Sphere,root,new Vector3(0,.31f,0),new Vector3(.42f,.49f,.30f),white);
            Shape("Helmet",PrimitiveType.Sphere,root,new Vector3(0,.65f,.015f),new Vector3(.52f,.46f,.40f),white);
            Shape("Visor",PrimitiveType.Sphere,root,new Vector3(0,.65f,.18f),new Vector3(.39f,.32f,.08f),black);
            for(int s=-1;s<=1;s+=2)
            {
                var ear=Shape("Rabbit ear",PrimitiveType.Capsule,root,new Vector3(.14f*s,.99f,.005f),new Vector3(.12f,.22f,.105f),white);
                ear.localRotation=Quaternion.Euler(0,0,-s*9);
                var inner=Shape("Ear inset",PrimitiveType.Capsule,root,new Vector3(.14f*s,1.0f,.054f),new Vector3(.061f,.145f,.025f),black);
                inner.localRotation=ear.localRotation;
                Shape("Glove",PrimitiveType.Capsule,root,new Vector3(s*.255f,.30f,.02f),new Vector3(.13f,.16f,.14f),white);
                Shape("Boot",PrimitiveType.Sphere,root,new Vector3(s*.13f,.065f,.065f),new Vector3(.19f,.15f,.29f),white);
                Cube("Boot sole",root,new Vector3(s*.13f,.018f,.08f),new Vector3(.16f,.034f,.21f),soft,true);
                Cube("Eye",root,new Vector3(s*.09f,.685f,.222f),new Vector3(.036f,.077f,.018f),gold,true);
            }
            Cube("Smile",root,new Vector3(0,.585f,.225f),new Vector3(.095f,.022f,.018f),gold,true);
            Cube("Smile left",root,new Vector3(-.06f,.605f,.223f),new Vector3(.025f,.041f,.018f),gold,true);
            Cube("Smile right",root,new Vector3(.06f,.605f,.223f),new Vector3(.025f,.041f,.018f),gold,true);
            Cube("Chest console",root,new Vector3(0,.33f,.146f),new Vector3(.19f,.17f,.045f),soft,true);
            Shape("Chest core",PrimitiveType.Sphere,root,new Vector3(0,.33f,.175f),new Vector3(.086f,.086f,.024f),mint);
            Cube("Life support pack",root,new Vector3(0,.35f,-.20f),new Vector3(.30f,.34f,.16f),soft,true);
            Cube("Oxygen stripe",root,new Vector3(0,.35f,-.286f),new Vector3(.20f,.07f,.02f),mint);
            Shape("Tail",PrimitiveType.Sphere,root,new Vector3(0,.19f,-.19f),Vector3.one*.17f,white);
        }
        void BuildGoal(Transform root)
        {
            var gold=Material("flag",Gold,true);var metal=Material("flag pole",new Color(.73f,.77f,.70f));
            Ring(root,Vector3.up*.018f,.38f,gold,64,.018f);
            Shape("Beacon pad",PrimitiveType.Cylinder,root,new Vector3(0,.025f,0),new Vector3(.42f,.019f,.42f),Material("goal pad",new Color(.22f,.22f,.16f)));
            Shape("Flag pole",PrimitiveType.Cylinder,root,new Vector3(.25f,.36f,.18f),new Vector3(.025f,.36f,.025f),metal);
            var flag=Cube("Pennant",root,new Vector3(.40f,.60f,.18f),new Vector3(.29f,.21f,.027f),gold,true);
            Cube("Pennant symbol",flag,new Vector3(0,0,-.6f),new Vector3(.25f,.25f,.08f),Material("flag ink",new Color(.24f,.16f,.04f),true));
        }
        public void Ring(Transform parent,Vector3 center,float radius,Material material,int points=48,float width=.014f)
        {
            var t=Empty("Orbit ring",parent);
            var line=t.gameObject.AddComponent<LineRenderer>();line.useWorldSpace=false;line.loop=true;line.positionCount=points;line.widthMultiplier=width;line.sharedMaterial=material;
            line.shadowCastingMode=ShadowCastingMode.Off;line.receiveShadows=false;
            for(int i=0;i<points;i++){float a=i*Mathf.PI*2/points;line.SetPosition(i,center+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius));}
        }
        public void Snap(BoardState state)
        {
            foreach(var b in state.blocks.Values) blocks[b.id].position=b.cell;
            rabbit.position=RabbitPosition(state.rabbit);rabbit.rotation=Rotation(state.rabbit);rabbit.localScale=Vector3.one;
            UpdateGoal(state);UpdateArrows(state);
        }
        public void UpdateGoal(BoardState state)
        {
            var n=state.goal.normal;
            goal.position=(Vector3)state.GoalBlock().cell+(Vector3)n*.50f;
            goal.rotation=Quaternion.LookRotation(Mathf.Abs(n.z)==1?Vector3.right:Vector3.forward,n);
        }
        void ClearChildren(Transform t)
        {
            for(int i=t.childCount-1;i>=0;i--){var g=t.GetChild(i).gameObject;g.SetActive(false);if(Application.isPlaying)Destroy(g);else DestroyImmediate(g);}
        }
        public void UpdateArrows(BoardState state)
        {
            ClearChildren(arrowsRoot);
            if(state.IsWeightless)return;
            var r=state.rabbit;
            var directions=new[]{r.forward,-r.forward};
            for(int i=0;i<2;i++)
            {
                var p=(Vector3)r.cell+(Vector3)r.down*.43f+(Vector3)directions[i]*.78f;
                var marker=Empty(i==0?"Forward":"Backward",arrowsRoot);marker.position=p;
                var line=marker.gameObject.AddComponent<LineRenderer>();
                line.positionCount=3;line.useWorldSpace=true;line.widthMultiplier=.025f;
                line.sharedMaterial=i==0?Material("forward arrow",Gold,true):Material("backward arrow",Mint,true);
                Vector3 d=directions[i],side=Vector3.Cross(r.Up,d);
                line.SetPositions(new[]{p-d*.07f+side*.07f,p+d*.06f,p-d*.07f-side*.07f});
            }
        }
        public void Preview(ActionResult result)
        {
            ClearChildren(previewRoot);
            if(result==null||!result.Success) return;
            var gold=Material("preview",Gold,true);
            var ghost=Empty("Landing target",previewRoot);ghost.position=RabbitPosition(result.state.rabbit);ghost.rotation=Rotation(result.state.rabbit);
            Ring(ghost,Vector3.up*.05f,.39f,gold,48,.035f);
            var line=ghost.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=gold;line.useWorldSpace=false;line.widthMultiplier=.021f;line.positionCount=2;
            line.SetPositions(new[]{Vector3.up*.1f,Vector3.up*.55f});
            foreach(var motion in result.motions) if(motion.kind==MotionKind.Blocks)
                foreach(var kv in motion.blockTo) WireCube(kv.Value,previewRoot,gold);
        }
        void WireCube(Vector3 p,Transform parent,Material mat)
        {
            for(int axis=0;axis<3;axis++)for(int s=-1;s<=1;s+=2)for(int t=-1;t<=1;t+=2)
            {
                Vector3 a=Vector3.zero;a[(axis+1)%3]=s*.51f;a[(axis+2)%3]=t*.51f;
                Vector3 b=a;a[axis]=-.51f;b[axis]=.51f;
                var line=Empty("Predicted block",parent).gameObject.AddComponent<LineRenderer>();
                line.sharedMaterial=mat;line.widthMultiplier=.02f;line.positionCount=2;line.SetPositions(new[]{p+a,p+b});
            }
        }
        public void Space(Transform parent,Camera camera)
        {
            var random=new System.Random(905);
            var stars=Empty("Quiet star field",parent);
            stars.SetParent(camera.transform,false);
            var mesh=new Mesh { name="Starfield" };
            var vertices=new List<Vector3>();var triangles=new List<int>();var colors=new List<Color>();
            for(int i=0;i<540;i++)
            {
                float x=(float)(random.NextDouble()-.5)*45,y=(float)(random.NextDouble()-.5)*28,size=i%19==0?.043f:.017f;
                int n=vertices.Count;
                vertices.Add(new Vector3(x-size,y,48));vertices.Add(new Vector3(x,y+size*1.5f,48));vertices.Add(new Vector3(x+size,y,48));vertices.Add(new Vector3(x,y-size*1.5f,48));
                triangles.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();
            stars.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            stars.gameObject.AddComponent<MeshRenderer>().sharedMaterial=Material("starlight",new Color(.23f,.37f,.46f),true);
            var orbit=Empty("Orbital paths",parent);orbit.position=center+Vector3.down*(radius*.85f);
            Ring(orbit,Vector3.zero,radius*1.4f,Material("orbit dark",new Color(.045f,.10f,.14f),true),128,.013f);
            Ring(orbit,Vector3.zero,radius*1.56f,Material("orbit faint",new Color(.035f,.075f,.11f),true),128,.008f);
        }
        void OnDestroy()
        {
            foreach(var m in materials.Values) if(m!=null){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}
            if(cubeMesh!=null){if(Application.isPlaying)Destroy(cubeMesh);else DestroyImmediate(cubeMesh);}
        }
    }
}
