using System;
using UnityEngine;

namespace RadioArchipelago.World
{
    public sealed partial class IslandFinishGlade
    {
        public IslandFinishTheme Theme { get; private set; }
        public int CoralClusterCount { get; private set; }
        public int ShellCount { get; private set; }
        public int CrystalClusterCount { get; private set; }
        public int CrystalCount { get; private set; }
        public int AutumnFlowerCount { get; private set; }
        public int FallenLeafCount { get; private set; }

        private Color ThemeGround(Color original,float variation)
        {
            switch (Theme)
            {
                case IslandFinishTheme.CoralShore: return Color.Lerp(new Color(.69f,.54f,.35f),new Color(.94f,.83f,.61f),variation);
                case IslandFinishTheme.MoonCrystalGarden: return Color.Lerp(new Color(.25f,.32f,.43f),new Color(.49f,.55f,.65f),variation);
                case IslandFinishTheme.AmberAutumn: return Color.Lerp(new Color(.46f,.30f,.12f),new Color(.73f,.55f,.23f),variation);
                default: return original;
            }
        }

        private Color ThemeRock(Color original)
        {
            if (Theme == IslandFinishTheme.EmeraldMeadow) return original;
            float shade = Mathf.Clamp01((original.r+original.g+original.b)/1.22f);
            switch (Theme)
            {
                case IslandFinishTheme.CoralShore: return new Color(.64f,.58f,.46f)*shade;
                case IslandFinishTheme.MoonCrystalGarden: return new Color(.44f,.48f,.61f)*shade;
                default: return new Color(.52f,.43f,.29f)*shade;
            }
        }

        private void BuildThemedPlants(MeshData data,System.Random random)
        {
            int attempts = Mathf.CeilToInt(Length*(Theme == IslandFinishTheme.CoralShore ? 6 : Theme == IslandFinishTheme.MoonCrystalGarden ? 4 : 12));
            for (int i=0;i<attempts;i++)
            {
                float z=Range(random,-1.5f,Length+2), sign=i%2==0 ? -1 : 1;
                float x=sign*Range(random,FlatHalfWidth+.45f,HalfWidth(z)-.25f);
                if (!SampleTop(x,z,out float y)) continue;
                if (Theme == IslandFinishTheme.CoralShore)
                {
                    Color sea=Linear(Color.Lerp(new Color(.18f,.40f,.32f),new Color(.32f,.61f,.48f),Range(random,0,1)));
                    sea.a=0;
                    for (int blade=0;blade<5;blade++)
                    {
                        float angle=Range(random,0,Mathf.PI*2), height=Range(random,.25f,.67f);
                        Vector3 direction=new Vector3(Mathf.Cos(angle)*.25f,1,Mathf.Sin(angle)*.25f).normalized;
                        AddPetal(data,new Vector3(x,y-.012f,z),direction,height,.035f,height*.15f,sea,Linear(new Color(.47f,.69f,.46f)));
                    }
                }
                else if (Theme == IslandFinishTheme.MoonCrystalGarden)
                {
                    Color stem=Linear(new Color(.39f,.47f,.65f)), tip=Linear(new Color(.70f,.83f,.94f));
                    stem.a=0;
                    Vector3 root=new Vector3(x,y,z), top=root+Vector3.up*Range(random,.20f,.42f);
                    AddStem(data,root,top,.012f,stem);
                    AddPetalRing(data,top,5,Range(random,.12f,.19f),.035f,.025f,Range(random,0,6),stem,tip);
                }
                else
                {
                    Color straw=Linear(Color.Lerp(new Color(.55f,.32f,.09f),new Color(.94f,.69f,.18f),Range(random,0,1)));
                    straw.a=0;
                    for (int blade=0;blade<4;blade++)
                    {
                        float angle=Range(random,0,6.283f), height=Range(random,.24f,.63f);
                        AddPetal(data,new Vector3(x,y-.01f,z),new Vector3(Mathf.Cos(angle)*.35f,1,Mathf.Sin(angle)*.35f).normalized,
                            height,.025f,height*.12f,straw,Linear(new Color(.93f,.75f,.31f)));
                    }
                }
            }
        }

        private void BuildThemedAccents(MeshData data,System.Random random)
        {
            int groups=Mathf.CeilToInt(Length/1.65f);
            for (int i=0;i<groups;i++)
                for (int side=-1;side<=1;side+=2)
                {
                    float z=Mathf.Lerp(-.8f,Length+1,(i+.5f)/groups)+Range(random,-.4f,.4f);
                    float min=FlatHalfWidth+.9f, max=HalfWidth(z)-.55f;
                    if (max<min) continue;
                    float x=side*Range(random,min,max);
                    if (!SampleTop(x,z,out float y)) continue;
                    Vector3 root=new Vector3(x,y,z);
                    if (Theme == IslandFinishTheme.CoralShore)
                    {
                        AddCoralCluster(data,root,Range(random,.55f,1.0f),random);
                        if ((i&1)==0) AddShell(data,root+new Vector3(-side*.32f,.025f,.50f),Range(random,.25f,.40f),random);
                    }
                    else if (Theme == IslandFinishTheme.MoonCrystalGarden)
                        AddCrystalCluster(data,root,random);
                    else
                    {
                        for (int bloom=0;bloom<3;bloom++)
                            AddAutumnFlower(data,root+new Vector3(side*Range(random,-.15f,.15f),0,bloom*.27f),random);
                    }
                }
            // Ground details are genuinely low and do not turn into obstacles
            // under the floating currency or the courier's collection trail.
            if (Theme == IslandFinishTheme.AmberAutumn)
            {
                for (int i=0;i<Mathf.CeilToInt(Length*13);i++)
                {
                    float z=Range(random,-1.5f,Length+2),x=Range(random,-HalfWidth(z)+.5f,HalfWidth(z)-.5f);
                    if (!SampleTop(x,z,out float y)) continue;
                    float angle=Range(random,0,Mathf.PI*2),size=Range(random,.13f,.28f);
                    Color root=Linear(Color.Lerp(new Color(.48f,.17f,.055f),new Color(.88f,.48f,.075f),Range(random,0,1)));
                    AddPetal(data,new Vector3(x,y+.018f,z),new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle)),size,size*.44f,size*.13f,root,Linear(new Color(.98f,.70f,.19f)));
                    FallenLeafCount++;
                    if (Mathf.Abs(x)<FlatHalfWidth) MaxCorridorPlantHeight=Mathf.Max(MaxCorridorPlantHeight,.018f+size*.20f);
                }
            }
        }

        private void AddCoralCluster(MeshData data,Vector3 root,float height,System.Random random)
        {
            Color rootColor=Linear(Color.Lerp(new Color(.63f,.20f,.18f),new Color(.90f,.40f,.25f),Range(random,0,1)));
            Color tip=Linear(new Color(.99f,.70f,.48f));
            for (int stem=0;stem<3;stem++)
            {
                float zoff=(stem-1)*.18f;
                Vector3 start=root+new Vector3(0,0,zoff), joint=start+Vector3.up*height*.50f;
                Vector3 end=start+new Vector3(Mathf.Sign(root.x)*(.08f+stem*.07f),height*(.75f+stem*.12f),zoff*.55f);
                AddTaperedBranch(data,start,joint,.055f,.045f,rootColor,tip);
                AddTaperedBranch(data,joint,end,.045f,.018f,rootColor,tip);
                for (int branch=-1;branch<=1;branch+=2)
                {
                    Vector3 fork=joint+new Vector3(Mathf.Sign(root.x)*.12f,height*.22f,branch*height*.28f);
                    AddTaperedBranch(data,joint,fork,.033f,.018f,rootColor,tip);
                    AddTaperedBranch(data,fork,fork+Vector3.up*height*.25f,.018f,.009f,rootColor,tip);
                }
            }
            CoralClusterCount++;
        }

        private void AddShell(MeshData data,Vector3 root,float radius,System.Random random)
        {
            float spin=Range(random,0,Mathf.PI*2);
            Color baseColor=Linear(new Color(.71f,.51f,.39f)), tipColor=Linear(new Color(.97f,.86f,.69f));
            Vector3 direction=new Vector3(Mathf.Cos(spin),.15f,Mathf.Sin(spin)).normalized;
            for (int i=-4;i<=4;i++)
            {
                Vector3 ray=Quaternion.AngleAxis(i*13,Vector3.up)*direction;
                AddPetal(data,root,ray,radius*(1-Mathf.Abs(i)*.045f),radius*.075f,radius*.27f,baseColor,tipColor);
            }
            ShellCount++;
        }

        private void AddCrystalCluster(MeshData data,Vector3 root,System.Random random)
        {
            int count=3+random.Next(5);
            float spin=Range(random,0,Mathf.PI*2);
            for (int i=0;i<count;i++)
            {
                float angle=spin+i*6.283f/count, height=Range(random,.55f,1.40f)*(i==0 ? 1.25f : 1);
                float radius=height*Range(random,.15f,.22f);
                Vector3 axis=new Vector3(Mathf.Sign(root.x)*Range(random,.10f,.42f),1,Mathf.Sin(angle)*Range(random,.08f,.48f)).normalized;
                Vector3 origin=root+new Vector3(Mathf.Sign(root.x)*radius*.6f,0,Mathf.Sin(angle)*radius*.7f);
                AddHexCrystal(data,origin,axis,height,radius,angle,random);
            }
            CrystalClusterCount++;
        }

        private void AddHexCrystal(MeshData data,Vector3 root,Vector3 axis,float height,float radius,float spin,System.Random random)
        {
            Quaternion rotation=Quaternion.FromToRotation(Vector3.up,axis);
            Color pale=Linear(Color.Lerp(new Color(.38f,.58f,.83f),new Color(.70f,.62f,.92f),Range(random,0,1)));
            var bottomRing=new Vector3[6]; var shoulder=new Vector3[6];
            for (int side=0;side<6;side++)
            {
                float a=spin+side*Mathf.PI/3;
                bottomRing[side]=root+rotation*new Vector3(Mathf.Cos(a)*radius*1.04f,0,Mathf.Sin(a)*radius*1.04f);
                shoulder[side]=root+rotation*new Vector3(Mathf.Cos(a)*radius*.83f,height*.73f,Mathf.Sin(a)*radius*.83f);
            }
            // Separate each prism face so normal reconstruction preserves real
            // flat hexagonal facets rather than a smoothed pencil silhouette.
            for (int side=0;side<6;side++)
            {
                int next=(side+1)%6;
                Color facet=Color.Lerp(pale,Linear(new Color(.80f,.92f,1)),side%3*.15f);
                int face=data.Vertices.Count;
                data.Vertex(bottomRing[side],facet,new Vector2(0,0)); data.Vertex(shoulder[side],facet,new Vector2(0,.73f));
                data.Vertex(shoulder[next],facet,new Vector2(1,.73f)); data.Vertex(bottomRing[next],facet,new Vector2(1,0));
                data.Quad(face,face+1,face+2,face+3,0);
                int crown=data.Vertices.Count;
                data.Vertex(shoulder[side],facet,new Vector2(0,.73f));
                data.Vertex(root+axis*height,Linear(new Color(.86f,.95f,1)),new Vector2(.5f,1));
                data.Vertex(shoulder[next],facet,new Vector2(1,.73f)); data.Triangle(crown,crown+1,crown+2,0);
                int underside=data.Vertices.Count;
                data.Vertex(bottomRing[next],pale,Vector2.zero); data.Vertex(root-axis*.018f,pale,Vector2.zero);
                data.Vertex(bottomRing[side],pale,Vector2.zero); data.Triangle(underside,underside+1,underside+2,0);
            }
            CrystalCount++;
        }

        private void AddAutumnFlower(MeshData data,Vector3 root,System.Random random)
        {
            float height=Range(random,.35f,.62f),radius=Range(random,.19f,.28f),spin=Range(random,0,6.283f);
            Vector3 top=root+Vector3.up*height;
            AddStem(data,root,top,.012f,Linear(new Color(.39f,.30f,.10f)));
            Color baseColor=Linear(Color.Lerp(new Color(.72f,.20f,.055f),new Color(.94f,.54f,.07f),Range(random,0,1)));
            AddPetalRing(data,top,8,radius,radius*.23f,radius*.07f,spin,baseColor,Linear(new Color(1,.78f,.24f)));
            AddFlowerCentre(data,top,radius*.25f,radius*.20f,Linear(new Color(.31f,.15f,.045f)),Linear(new Color(.65f,.35f,.06f)));
            AutumnFlowerCount++;
        }

        private static void AddTaperedBranch(MeshData data,Vector3 root,Vector3 tip,float startRadius,float endRadius,Color rootColor,Color tipColor)
        {
            const int sides=7;
            Vector3 along=(tip-root).normalized;
            Vector3 across=Vector3.Cross(along,Mathf.Abs(along.y)>.9f ? Vector3.right : Vector3.up).normalized;
            Vector3 second=Vector3.Cross(across,along).normalized;
            int first=data.Vertices.Count;
            for (int ring=0;ring<2;ring++)
                for (int i=0;i<sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;
                    data.Vertex((ring==0 ? root : tip)+(across*Mathf.Cos(a)+second*Mathf.Sin(a))*(ring==0 ? startRadius : endRadius),
                        ring==0 ? rootColor : tipColor,new Vector2((float)i/sides,ring));
                }
            int end=data.Vertex(tip,tipColor,Vector2.one),start=data.Vertex(root,rootColor,Vector2.zero);
            for (int i=0;i<sides;i++)
            {
                int n=(i+1)%sides;
                data.Quad(first+i,first+sides+i,first+sides+n,first+n,0);
                data.Triangle(first+sides+i,end,first+sides+n,0);
                data.Triangle(first+n,start,first+i,0);
            }
        }
    }
}
