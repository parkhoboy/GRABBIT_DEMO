using UnityEngine;
using UnityEngine.UI;

namespace Grabbit
{
    // Resolution-independent menu graphics: no font glyphs or external image assets.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GrabbitUIArt : MaskableGraphic
    {
        public enum Symbol { Panel, Menu, Play, Grid, Home, Back, Close, Check, Planet, Edit }
        public Symbol symbol;
        public float radius=16;
        public int variant;
        Vector2 Point(Vector2 p) { var r=rectTransform.rect;return r.center+Vector2.Scale(p,r.size); }
        void Polygon(VertexHelper vh,Color tint,params Vector2[] points)
        {
            int first=vh.currentVertCount;
            foreach(var p in points)vh.AddVert(Point(p),tint,Vector2.zero);
            for(int i=1;i<points.Length-1;i++)vh.AddTriangle(first,first+i,first+i+1);
        }
        void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width=.06f)
        {
            var normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;
            Polygon(vh,color,a+normal,b+normal,b-normal,a-normal);
        }
        void Cube(VertexHelper vh,Vector2 p,float s)
        {
            var top=p+new Vector2(0,.5f)*s;var tl=p+new Vector2(-.5f,.25f)*s;var tr=p+new Vector2(.5f,.25f)*s;
            var bottom=p+new Vector2(0,-.5f)*s;var bl=p+new Vector2(-.5f,-.25f)*s;var br=p+new Vector2(.5f,-.25f)*s;
            Polygon(vh,color,top,tr,p,tl);Polygon(vh,color*.8f,tl,p,bottom,bl);Polygon(vh,color*.55f,p,tr,br,bottom);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            if(symbol==Symbol.Panel)
            {
                var r=rectTransform.rect;float corner=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
                vh.AddVert(r.center,color,Vector2.zero);
                for(int quadrant=0;quadrant<4;quadrant++)
                {
                    float cx=(quadrant==0||quadrant==3)?r.xMax-corner:r.xMin+corner;
                    float cy=quadrant<2?r.yMax-corner:r.yMin+corner;
                    for(int i=0;i<=8;i++)
                    {
                        float a=(quadrant*90+i*90f/8)*Mathf.Deg2Rad;
                        vh.AddVert(new Vector3(cx+Mathf.Cos(a)*corner,cy+Mathf.Sin(a)*corner),color,Vector2.zero);
                    }
                }
                for(int i=1;i<=36;i++)vh.AddTriangle(0,i,i==36?1:i+1);
                return;
            }
            switch(symbol)
            {
                case Symbol.Edit:
                    Polygon(vh,color,new Vector2(-.3f,-.17f),new Vector2(.17f,.3f),new Vector2(.31f,.16f),new Vector2(-.16f,-.31f));
                    Polygon(vh,color,new Vector2(-.34f,-.2f),new Vector2(-.2f,-.34f),new Vector2(-.39f,-.39f));
                    Stroke(vh,new Vector2(.23f,.36f),new Vector2(.37f,.22f),.08f);break;
                case Symbol.Menu:
                    for(int i=-1;i<=1;i++)Stroke(vh,new Vector2(-.36f,i*.24f),new Vector2(.36f,i*.24f),.065f);
                    break;
                case Symbol.Play:Polygon(vh,color,new Vector2(-.22f,.35f),new Vector2(.35f,0),new Vector2(-.22f,-.35f));break;
                case Symbol.Grid:
                    for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)
                    {var p=new Vector2(x*.22f,y*.22f);Polygon(vh,color,p+new Vector2(-.14f,-.14f),p+new Vector2(-.14f,.14f),p+new Vector2(.14f,.14f),p+new Vector2(.14f,-.14f));}
                    break;
                case Symbol.Home:
                    Stroke(vh,new Vector2(-.4f,.02f),new Vector2(0,.38f));Stroke(vh,new Vector2(0,.38f),new Vector2(.4f,.02f));
                    Stroke(vh,new Vector2(-.28f,.06f),new Vector2(-.28f,-.33f));Stroke(vh,new Vector2(.28f,.06f),new Vector2(.28f,-.33f));
                    Stroke(vh,new Vector2(-.28f,-.33f),new Vector2(.28f,-.33f));Stroke(vh,new Vector2(0,-.33f),new Vector2(0,-.1f));break;
                case Symbol.Back:
                    Stroke(vh,new Vector2(.36f,0),new Vector2(-.32f,0));Stroke(vh,new Vector2(-.32f,0),new Vector2(-.02f,.3f));Stroke(vh,new Vector2(-.32f,0),new Vector2(-.02f,-.3f));break;
                case Symbol.Close:
                    Stroke(vh,new Vector2(-.27f,-.27f),new Vector2(.27f,.27f));Stroke(vh,new Vector2(-.27f,.27f),new Vector2(.27f,-.27f));break;
                case Symbol.Check:
                    Stroke(vh,new Vector2(-.34f,.01f),new Vector2(-.08f,-.25f),.085f);Stroke(vh,new Vector2(-.08f,-.25f),new Vector2(.37f,.3f),.085f);break;
                case Symbol.Planet:
                    if(variant==0){Cube(vh,new Vector2(-.23f,.12f),.45f);Cube(vh,new Vector2(.22f,.12f),.45f);Cube(vh,new Vector2(0,-.14f),.45f);}
                    else if(variant==1){for(int i=0;i<3;i++)Cube(vh,new Vector2(0,-.3f+i*.29f),.43f);}
                    else if(variant==2){Cube(vh,new Vector2(-.23f,-.23f),.45f);Cube(vh,new Vector2(.23f,-.04f),.45f);Cube(vh,new Vector2(.23f,.3f),.45f);}
                    else if(variant==3){Cube(vh,new Vector2(0,-.33f),.42f);Cube(vh,new Vector2(0,.34f),.42f);}
                    else {Cube(vh,new Vector2(-.23f,.27f),.42f);Cube(vh,new Vector2(-.23f,-.03f),.42f);Cube(vh,new Vector2(.19f,-.22f),.42f);Cube(vh,new Vector2(.19f,.08f),.42f);}
                    break;
            }
        }
    }
}
