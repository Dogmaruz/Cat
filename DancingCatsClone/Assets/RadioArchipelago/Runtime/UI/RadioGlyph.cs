using UnityEngine;
using UnityEngine.UI;

namespace RadioArchipelago.UI
{
    /// <summary>Small, resolution independent marks used by the native interface.</summary>
    public sealed class RadioGlyph : MaskableGraphic
    {
        public enum Symbol { Signal, Play, Map, Workshop, Settings, Previous, Next, Close, Pause, Music }
        public Symbol shape;

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect r = GetPixelAdjustedRect();
            float unit = Mathf.Min(r.width, r.height);
            Vector2 center = r.center;
            void Line(float ax, float ay, float bx, float by, float width = .065f)
            {
                Vector2 a = center + new Vector2(ax, ay) * unit;
                Vector2 b = center + new Vector2(bx, by) * unit;
                Vector2 n = new Vector2(-(b-a).y, (b-a).x).normalized * unit * width * .5f;
                int start = mesh.currentVertCount;
                mesh.AddVert(a-n, color, Vector2.zero); mesh.AddVert(a+n, color, Vector2.zero);
                mesh.AddVert(b+n, color, Vector2.zero); mesh.AddVert(b-n, color, Vector2.zero);
                mesh.AddTriangle(start, start+1, start+2); mesh.AddTriangle(start, start+2, start+3);
            }
            void Arc(float radius, float begin, float end)
            {
                const int steps = 24;
                for (int i=0; i<steps; i++)
                {
                    float a = Mathf.Lerp(begin,end,i/(float)steps)*Mathf.Deg2Rad;
                    float b = Mathf.Lerp(begin,end,(i+1)/(float)steps)*Mathf.Deg2Rad;
                    Line(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,Mathf.Cos(b)*radius,Mathf.Sin(b)*radius,.045f);
                }
            }
            switch (shape)
            {
                case Symbol.Signal:
                    Arc(.43f,25,155); Arc(.29f,25,155); Line(-.28f,-.28f,0,.10f,.11f); Line(0,.10f,.28f,-.28f,.11f); Line(-.3f,-.3f,.3f,-.3f,.07f); break;
                case Symbol.Play:
                    Line(-.24f,-.22f,-.24f,.22f,.085f); Line(0,-.34f,0,.34f,.085f); Line(.24f,-.14f,.24f,.14f,.085f); break;
                case Symbol.Map:
                    Line(-.4f,-.27f,-.4f,.26f); Line(-.4f,.26f,-.14f,.38f); Line(-.14f,.38f,.14f,.23f);
                    Line(.14f,.23f,.4f,.36f); Line(.4f,.36f,.4f,-.2f); Line(.4f,-.2f,.14f,-.34f); Line(.14f,-.34f,-.14f,-.2f); Line(-.14f,-.2f,-.4f,-.27f);
                    Line(-.14f,.38f,-.14f,-.2f); Line(.14f,.23f,.14f,-.34f); break;
                case Symbol.Workshop:
                    Arc(.21f,0,295); Line(-.13f,-.15f,-.35f,-.39f,.12f); Line(.05f,.20f,.29f,.40f,.07f); break;
                case Symbol.Settings:
                    Arc(.25f,0,360); Arc(.09f,0,360);
                    for(int i=0;i<8;i++) { float a=i*Mathf.PI/4; Line(Mathf.Cos(a)*.24f,Mathf.Sin(a)*.24f,Mathf.Cos(a)*.39f,Mathf.Sin(a)*.39f,.09f); } break;
                case Symbol.Previous: Line(.13f,.3f,-.15f,0); Line(-.15f,0,.13f,-.3f); break;
                case Symbol.Next: Line(-.13f,.3f,.15f,0); Line(.15f,0,-.13f,-.3f); break;
                case Symbol.Close: Line(-.25f,-.25f,.25f,.25f); Line(-.25f,.25f,.25f,-.25f); break;
                case Symbol.Pause: Line(-.15f,-.3f,-.15f,.3f,.12f); Line(.15f,-.3f,.15f,.3f,.12f); break;
                case Symbol.Music: Line(-.2f,-.1f,-.2f,.3f); Line(-.2f,.3f,.25f,.4f); Line(.25f,.4f,.25f,0); Arc(.10f,0,360); break;
            }
        }
    }
}
