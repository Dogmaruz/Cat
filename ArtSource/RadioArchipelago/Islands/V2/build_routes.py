"""Deterministic technical route references for Radio Archipelago v2.

These diagrams are REFERENCE ONLY, not final illustrations or Unity geometry.
Run with the bundled Python; Pillow may be loaded from the existing plot-deps.
No models, network access, or additional packages are used.
"""

from __future__ import annotations

import json
import math
import sys
from copy import deepcopy
from pathlib import Path
from xml.sax.saxutils import escape

PLOT_DEPS = Path(r"C:\Users\Derek\Games_develop\Cat\DancingCatsClone\Library\OriginalChartCalibration\plot-deps")
if PLOT_DEPS.exists():
    sys.path.insert(0, str(PLOT_DEPS))
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
OUT = ROOT / "RouteGuides"
SIZE = 1400
UNITS = 1000
ROAD_WIDTH = 30.0
BG = "#192127"


def landmark(name, kind, x, y, rx, ry, color, angle=0):
    return dict(name=name, kind=kind, center=[x, y], radii=[rx, ry], color=color, angle=angle)


WORLDS = [
    dict(id="01_emerald_ravine", name="Emerald Ravine", terrain="#3e7059", rim="#769e72", road="#e6d3a6",
         silhouette=[(90,320),(140,150),(320,100),(460,135),(500,145),(610,130),(730,80),(880,165),(930,320),(870,440),(930,610),(840,800),(650,840),(520,770),(400,885),(210,850),(100,735),(80,580),(165,490),(95,425)],
         route=[(170,700),(265,635),(365,635),(460,680),(555,645),(595,545),(550,455),(445,425),(345,390),(290,310),(320,220),(415,190),(515,240),(600,310),(705,325),(795,270)],
         journey=["Mossy approach and forest glade", "Slate ravine crossing and interior woodland", "High fern shelves and open ridge"],
         landmarks=[landmark("forest glade","field",275,510,75,55,"#84a96c",-15),landmark("slate ravine","ravine",600,535,105,30,"#263e49",25),landmark("log bridge banks","bridge_banks",596,535,34,20,"#70533d",-65),landmark("fern ridge","field",665,190,64,32,"#6e915a",15)]),
    dict(id="02_coral_oasis", name="Coral Oasis", terrain="#b67e65", rim="#e5b188", road="#f5dcae",
         silhouette=[(80,300),(170,150),(350,130),(450,70),(620,120),(820,100),(930,220),(900,420),(950,580),(850,760),(650,820),(550,930),(350,870),(180,800),(100,650),(130,480)],
         route=[(180,275),(285,250),(380,305),(390,405),(310,480),(275,580),(345,685),(475,725),(600,680),(650,570),(630,465),(700,365),(790,325),(840,240)],
         journey=["Coral sandstone gate", "Interior turquoise oasis and palm shelf", "Adobe settlement and butte saddle"],
         landmarks=[landmark("sandstone buttes","rocks",235,365,58,60,"#dc9a79",20),landmark("turquoise oasis","pond",470,570,102,71,"#39aeb0",-12),landmark("small adobe cluster","village",765,535,74,43,"#edd0a0",-12),landmark("eastern butte","rocks",765,220,48,45,"#d89670",5)]),
    dict(id="03_amethyst_grotto", name="Amethyst Grotto", terrain="#4e4975", rim="#8a79ad", road="#d8cde5",
         outlineMode="faceted", silhouette=[(180,100),(350,125),(440,80),(610,140),(760,100),(855,220),(810,315),(930,420),(860,560),(920,740),(760,860),(605,815),(475,905),(300,850),(195,900),(105,755),(165,620),(100,460),(155,350),(100,245)],
         route=[(200,770),(315,730),(395,645),(370,545),(285,465),(270,365),(345,270),(460,245),(575,285),(660,365),(650,465),(565,550),(565,650),(655,715),(765,685),(815,580)],
         journey=["Indigo rock floor", "Open-ended arch and crystal gallery", "Lower amethyst gardens"],
         landmarks=[landmark("indigo rock wall","rocks",190,310,37,82,"#343550",-15),landmark("amethyst gallery","crystals",470,390,85,60,"#ad7fce",5),landmark("short open-ended arch","arch",317,309,34,22,"#817090",-48),landmark("lower crystal garden","crystals",695,560,53,62,"#b59de0",-18)]),
    dict(id="04_autumn_windmills", name="Autumn Windmills", terrain="#926047", rim="#c69261", road="#e9c9a0",
         silhouette=[(90,200),(240,120),(400,160),(510,100),(700,160),(870,300),(900,450),(850,540),(930,670),(810,840),(650,900),(480,850),(330,900),(180,810),(80,650),(120,480),(60,350)],
         route=[(190,250),(310,265),(400,335),(430,450),(520,525),(650,530),(735,605),(730,715),(630,770),(515,730),(430,650),(315,620),(220,675)],
         journey=["Russet orchard approach", "Timber windmill hills and brook", "Harvest meadow and sheltered pond"],
         landmarks=[landmark("russet orchard","field",255,420,85,55,"#c58c46",-12),landmark("timber windmill","windmill",590,345,48,54,"#674739",10),landmark("brook","stream",540,425,60,18,"#74a2b0",-8),landmark("harvest meadow","field",590,640,68,46,"#b79755",10),landmark("autumn tree copse","rocks",260,760,64,37,"#bf653c",-15)]),
    dict(id="05_frost_moon", name="Frost Moon", terrain="#83a9bd", rim="#c9e2e7", road="#f4ecdf",
         outlineMode="faceted", silhouette=[(90,640),(205,490),(125,330),(245,200),(365,210),(450,75),(670,70),(810,130),(900,300),(805,430),(915,605),(810,780),(620,850),(460,790),(295,910),(120,850)],
         route=[(180,755),(220,635),(310,565),(425,570),(510,650),(625,675),(740,605),(760,485),(695,390),(580,365),(500,290),(530,195),(640,160),(755,205)],
         journey=["Fractured ice plain", "Blue ice arch and moon basin", "Silver beacon on the high shelf"],
         landmarks=[landmark("fractured ice sheets","ice",300,445,98,57,"#b1d3dc",-20),landmark("moon basin","pond",620,530,70,80,"#4a7d9a",10),landmark("short ice arch","arch",755,500,32,21,"#d8e7ef",80),landmark("silver beacon","beacon",660,270,35,43,"#e0ebee",0)]),
    dict(id="06_tropical_cascade", name="Tropical Cascade", terrain="#427c66", rim="#79b487", road="#e7d1a2",
         silhouette=[(150,80),(345,70),(470,140),(620,160),(700,280),(810,360),(820,510),(900,650),(840,820),(715,900),(560,850),(440,910),(300,800),(320,660),(190,600),(100,450),(130,280),(80,160)],
         route=[(220,150),(335,210),(385,320),(345,430),(370,540),(480,590),(600,555),(700,610),(740,720),(690,815)],
         journey=["Jade canopy ridge", "Interior waterfall terraces", "Hut clearing above the lower pool"],
         landmarks=[landmark("jade canopy","field",225,355,62,95,"#265b47",15),landmark("upper waterfall pool","pond",520,310,68,46,"#63b8b9",10),landmark("tiered waterfalls","falls",540,420,65,65,"#7fcbd0",8),landmark("lower cascade pool","pond",540,720,72,60,"#389aa4",-15),landmark("jungle hut clearing","hut",740,475,42,42,"#a58452",0)]),
    dict(id="07_storm_coast", name="Storm Coast", terrain="#46565a", rim="#7a8987", road="#d8d0b7",
         outlineMode="faceted", silhouette=[(60,400),(150,225),(300,220),(375,95),(535,65),(660,160),(825,175),(925,315),(880,410),(850,485),(925,620),(815,760),(655,705),(530,835),(350,795),(240,710),(110,710),(85,550)],
         route=[(165,580),(225,470),(335,420),(455,450),(530,540),(610,600),(720,595),(790,510),(785,390),(695,305),(580,285),(490,230),(460,150)],
         journey=["Black basalt headland", "Sheltered inlet and storm grass valley", "Semaphore ridge"],
         landmarks=[landmark("basalt outcrops","rocks",220,335,63,50,"#2b363c",-10),landmark("sheltered inlet","pond",620,455,65,50,"#377887",-20),landmark("storm grass valley","field",405,600,73,53,"#6f805e",-12),landmark("semaphore","semaphore",630,205,28,35,"#b69468",5)]),
    dict(id="08_sunflower_terraces", name="Sunflower Terraces", terrain="#9d8d4c", rim="#d1bd6d", road="#f1d7a1",
         silhouette=[(80,680),(130,500),(85,350),(180,190),(355,150),(470,90),(650,80),(810,125),(905,270),(865,450),(920,570),(815,720),(630,740),(500,840),(350,900),(150,850)],
         route=[(175,720),(285,735),(380,665),(420,545),(510,455),(635,450),(750,380),(785,265),(715,180),(600,190),(515,275),(405,310),(310,320),(250,375),(255,460)],
         journey=["Low sunflower terraces", "Red barn saddle", "Fountain orchard and upper flower fields"],
         landmarks=[landmark("sunflower terraces","field",265,570,81,55,"#d3b542",-20),landmark("red barn","barn",610,600,55,38,"#a95741",12),landmark("fountain orchard","fountain",625,320,47,44,"#61a5a1",0),landmark("upper flower field","field",435,205,62,35,"#d4b647",5)]),
    dict(id="09_volcanic_garden", name="Volcanic Garden", terrain="#65524d", rim="#947565", road="#d9bea2",
         outlineMode="faceted", rotateDegrees=180, silhouette=[(100,250),(250,180),(400,215),(520,110),(685,100),(840,175),(930,300),(865,465),(930,610),(850,800),(670,870),(500,845),(350,910),(200,850),(90,690),(130,510),(60,380)],
         route=[(165,600),(210,470),(305,390),(415,420),(470,525),(450,635),(510,735),(630,750),(720,675),(730,560),(665,470),(655,355),(735,285),(835,310)],
         journey=["Charcoal ash terraces", "Steam fissure and sheltered red garden", "Warm stone ridge"],
         landmarks=[landmark("charcoal rocks","rocks",245,270,75,41,"#3b3436",-10),landmark("steam fissure","fissure",555,410,28,92,"#b26951",-18),landmark("red-leaf tree garden","tree",585,625,60,57,"#ba5c4d",-8),landmark("warm stone terrace","field",795,465,44,65,"#966b55",-10)]),
    dict(id="10_cloud_observatory", name="Cloud Observatory", terrain="#a38aab", rim="#d4b4c0", road="#f1d9bb",
         rotateDegrees=90, silhouette=[(110,150),(290,100),(420,210),(620,80),(810,120),(900,255),(850,430),(920,570),(820,740),(650,810),(520,900),(350,860),(200,750),(130,590),(80,440),(140,300)],
         route=[(185,240),(300,280),(370,385),(350,495),(390,600),(500,660),(620,645),(695,560),(680,450),(585,375),(580,270),(660,190),(780,220),(825,325)],
         journey=["Peach arrival terraces", "Lavender cloudwell valley", "Airy gold open observatory"],
         landmarks=[landmark("peach terraces","field",235,480,60,67,"#d4a18c",-12),landmark("cloudwell","pond",525,525,74,66,"#7da8bd",5),landmark("open gold observatory","observatory",730,340,42,43,"#d5b178",0),landmark("lavender upper terrace","field",460,275,40,55,"#b4a1cb",20)]),
]


def dist(a, b):
    return math.hypot(a[0] - b[0], a[1] - b[1])


def catmull_rom(points, closed=False, steps=30):
    """Centripetal Catmull-Rom, endpoint extension, no circular route closure."""
    pts = [tuple(map(float, p)) for p in points]
    if closed:
        expanded = [pts[-1]] + pts + [pts[0], pts[1]]
        count = len(pts)
    else:
        a, b = pts[0], pts[1]
        z, y = pts[-1], pts[-2]
        expanded = [(2*a[0]-b[0], 2*a[1]-b[1])] + pts + [(2*z[0]-y[0], 2*z[1]-y[1])]
        count = len(pts) - 1
    result = []
    for i in range(count):
        p0, p1, p2, p3 = expanded[i:i+4]
        t0 = 0.0
        t1 = t0 + math.sqrt(max(dist(p0,p1), 1e-8))
        t2 = t1 + math.sqrt(max(dist(p1,p2), 1e-8))
        t3 = t2 + math.sqrt(max(dist(p2,p3), 1e-8))
        def blend(a,b,ta,tb,t):
            return tuple(((tb-t)*a[k]+(t-ta)*b[k])/(tb-ta) for k in (0,1))
        for j in range(steps):
            t = t1 + (t2-t1)*j/steps
            a1 = blend(p0,p1,t0,t1,t)
            a2 = blend(p1,p2,t1,t2,t)
            a3 = blend(p2,p3,t2,t3,t)
            b1 = blend(a1,a2,t0,t2,t)
            b2 = blend(a2,a3,t1,t3,t)
            result.append(blend(b1,b2,t1,t2,t))
    result.append(result[0] if closed else pts[-1])
    return result


def resample(points, interval=4):
    distances = [0.0]
    for a,b in zip(points,points[1:]):
        distances.append(distances[-1]+dist(a,b))
    length = distances[-1]
    out=[]
    k=0
    n=math.ceil(length/interval)
    for j in range(n+1):
        s = length*j/n
        while k+1<len(distances)-1 and distances[k+1]<s:
            k+=1
        f=(s-distances[k])/max(distances[k+1]-distances[k],1e-9)
        a,b=points[k],points[k+1]
        out.append((a[0]+f*(b[0]-a[0]),a[1]+f*(b[1]-a[1]),s))
    return out,length


def point_segment_distance(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]
    f=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/max(dx*dx+dy*dy,1e-8)))
    return dist(p,(a[0]+f*dx,a[1]+f*dy))


def point_inside(p,poly):
    inside=False
    x,y=p[:2]
    for a,b in zip(poly,poly[1:]):
        if ((a[1]>y)!=(b[1]>y)) and (x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]):
            inside=not inside
    return inside


def intersections(points):
    def cross(a,b,c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    total=0
    for i in range(len(points)-1):
        a,b=points[i],points[i+1]
        for j in range(i+3,len(points)-1):
            c,d=points[j],points[j+1]
            if cross(a,b,c)*cross(a,b,d)<0 and cross(c,d,a)*cross(c,d,b)<0:
                total+=1
    return total


def turns(samples):
    # Evaluate turns across an 8-unit window and suppress tiny inflections.
    signs=[]
    radii=[]
    for i in range(2,len(samples)-2):
        a,b,c=samples[i-2],samples[i],samples[i+2]
        ab,bc,ac=dist(a,b),dist(b,c),dist(a,c)
        cross=(b[0]-a[0])*(c[1]-b[1])-(b[1]-a[1])*(c[0]-b[0])
        if abs(cross)>1e-8:
            radii.append(ab*bc*ac/(2*abs(cross)))
        heading=math.atan2(c[1]-b[1],c[0]-b[0])-math.atan2(b[1]-a[1],b[0]-a[0])
        heading=(heading+math.pi)%(2*math.pi)-math.pi
        sign=(1 if heading>0 else -1) if abs(heading)>math.radians(.75) else 0
        if sign and (not signs or signs[-1]!=sign):
            signs.append(sign)
    return max(0,len(signs)-1),min(radii,default=1e9),signs


def shape_polygon(feature, n=18):
    x,y=feature["center"]
    rx,ry=feature["radii"]
    angle=math.radians(feature["angle"])
    kind=feature["kind"]
    points=[]
    for i in range(n):
        t=2*math.pi*i/n
        wobble=1+0.09*math.sin(3*t+.9)+.055*math.cos(5*t-.6)
        if kind in ("ravine","stream","fissure","falls"):
            wobble=1+.11*math.sin(4*t)
        u,v=rx*math.cos(t)*wobble,ry*math.sin(t)*wobble
        points.append((x+u*math.cos(angle)-v*math.sin(angle),y+u*math.sin(angle)+v*math.cos(angle)))
    return points


def svg_points(points):
    return " ".join(f"{p[0]:.2f},{p[1]:.2f}" for p in points)


def draw_feature(draw,feature,scale,svg):
    def xy(p): return tuple(round(v*scale) for v in p)
    def poly(p,fill,outline=None,width=1):
        draw.polygon([xy(q) for q in p],fill=fill)
        if outline:
            draw.line([xy(q) for q in p+[p[0]]],fill=outline,width=max(1,round(width*scale)),joint="curve")
        svg.append(f'<polygon points="{svg_points(p)}" fill="{fill}"'+(f' stroke="{outline}" stroke-width="{width}"' if outline else '')+'/>')
    p=shape_polygon(feature)
    col=feature["color"]
    kind=feature["kind"]
    x,y=feature["center"]
    rx,ry=feature["radii"]
    poly(p,col)
    if kind in ("rocks","crystals","ice"):
        for n,(dx,dy,k) in enumerate([(-.36,.13,.34),(.26,-.3,.4),(.37,.44,.28)]):
            cx,cy=x+dx*rx,y+dy*ry
            q=[(cx-rx*k,cy+ry*k*.4),(cx-rx*k*.55,cy-ry*k*.8),(cx+rx*k*.5,cy-ry*k),(cx+rx*k,cy+ry*k*.35)]
            poly(q, {"rocks":"#6f6d76","crystals":"#c9a4eb","ice":"#d3e7ed"}[kind])
    elif kind in ("village","hut","barn"):
        offsets=[(-.35,-.18),(.35,-.05),(0,.45)] if kind=="village" else [(0,0)]
        for dx,dy in offsets:
            cx,cy=x+rx*dx,y+ry*dy
            w=rx*.36 if kind=="village" else rx*.65
            h=ry*.25 if kind=="village" else ry*.48
            poly([(cx-w,cy-h),(cx+w,cy-h),(cx+w,cy+h),(cx-w,cy+h)],"#ead3a7" if kind=="village" else "#7c4c3b", "#443c3b",1.5)
    elif kind=="field":
        # A few broad field bands; these are not road tiles.
        for k in (-.48,0,.48):
            yy=y+ry*k
            q=[(x-rx*.68,yy),(x+rx*.67,yy+ry*.10)]
            band="#"+"".join(f"{round(int(col[k:k+2],16)*.82+255*.18):02x}" for k in (1,3,5))
            draw.line([xy(z) for z in q],fill=band,width=max(1,round(2*scale)))
            svg.append(f'<polyline points="{svg_points(q)}" stroke="{band}" stroke-width="2"/>')
    elif kind in ("windmill","beacon","semaphore","observatory","fountain","tree"):
        if kind in ("observatory","fountain"):
            r=min(rx,ry)*.62
            draw.ellipse([xy((x-r,y-r)),xy((x+r,y+r))],outline="#f3dfb0",width=round(5*scale))
            svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="none" stroke="#f3dfb0" stroke-width="5"/>')
            if kind=="observatory":
                for t in (0,math.pi/2,math.pi,math.pi*1.5):
                    cx,cy=x+r*math.cos(t),y+r*math.sin(t)
                    poly([(cx-5,cy-5),(cx+5,cy-5),(cx+5,cy+5),(cx-5,cy+5)],"#edcf93")
        elif kind=="tree":
            for dx,dy in [(-.25,.05),(.2,-.25),(.24,.28)]:
                r=rx*.42
                cx,cy=x+dx*rx,y+dy*ry
                draw.ellipse([xy((cx-r,cy-r)),xy((cx+r,cy+r))],fill="#d47855")
                svg.append(f'<circle cx="{cx}" cy="{cy}" r="{r}" fill="#d47855"/>')
        else:
            poly([(x-12,y+ry*.6),(x+12,y+ry*.6),(x+8,y-ry*.45),(x-8,y-ry*.45)],"#e5cda0")
            if kind=="windmill":
                for t in (.5,.5+math.pi/2):
                    q=[(x-rx*.8*math.cos(t),y-ry*.8*math.sin(t)),(x+rx*.8*math.cos(t),y+ry*.8*math.sin(t))]
                    draw.line([xy(z) for z in q],fill="#e6d5b8",width=round(6*scale))
                    svg.append(f'<polyline points="{svg_points(q)}" stroke="#e6d5b8" stroke-width="6"/>')
    elif kind=="falls":
        for k in (-.4,0,.4):
            q=[(x-rx*.6,y+ry*k),(x+rx*.6,y+ry*k+ry*.12)]
            draw.line([xy(z) for z in q],fill="#d1eff0",width=round(6*scale))
            svg.append(f'<polyline points="{svg_points(q)}" stroke="#d1eff0" stroke-width="6"/>')


def render(world,outline,route,target):
    # Supersampled raster and identical SVG. Pure technical top-down reference.
    ss=2
    image=Image.new("RGB",(SIZE*ss,SIZE*ss),BG)
    draw=ImageDraw.Draw(image)
    scale=SIZE*ss/UNITS
    xy=lambda p:(round(p[0]*scale),round(p[1]*scale))
    svg=[f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {UNITS} {UNITS}" width="{SIZE}" height="{SIZE}">',
         f'<title>{escape(world["name"])} — reference-only top-down route guide</title>',
         '<metadata>referenceOnly=true; continuous open road; not final art; no baked text or arrows; terrain colors are schematic.</metadata>',
         f'<rect width="1000" height="1000" fill="{BG}"/>']
    # A generous edge rim makes shoreline clearance evident.
    draw.polygon([xy(p) for p in outline],fill=world["terrain"])
    draw.line([xy(p) for p in outline],fill=world["rim"],width=round(12*scale),joint="curve")
    svg.append(f'<polygon points="{svg_points(outline)}" fill="{world["terrain"]}" stroke="{world["rim"]}" stroke-width="12" stroke-linejoin="round"/>')
    for feature in world["landmarks"]:
        draw_feature(draw,feature,scale,svg)
    # Single continuous physical corridor. No stepping stones or tile seams.
    for width,color in ((ROAD_WIDTH+9,"#263036"),(ROAD_WIDTH+3,world["rim"]),(ROAD_WIDTH,world["road"])):
        draw.line([xy(p) for p in route],fill=color,width=round(width*scale),joint="curve")
        r=width*scale/2
        for p in (route[0],route[-1]):
            px,py=xy(p)
            draw.ellipse((px-r,py-r,px+r,py+r),fill=color)
        svg.append(f'<polyline points="{svg_points(route)}" fill="none" stroke="{color}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>')
    # Short arch/bridge is shown as banks flanking the visible road; no long tunnel.
    for feature in world["landmarks"]:
        if feature["kind"] not in ("arch","bridge_banks"):
            continue
        center=feature["center"]
        k=min(range(len(route)),key=lambda i:dist(center,route[i]))
        p=route[k]
        a,b=route[max(0,k-3)],route[min(len(route)-1,k+3)]
        length=max(dist(a,b),1e-6)
        tangent=((b[0]-a[0])/length,(b[1]-a[1])/length)
        normal=(-tangent[1],tangent[0])
        for side in (-1,1):
            cx,cy=p[0]+normal[0]*side*(ROAD_WIDTH/2+9),p[1]+normal[1]*side*(ROAD_WIDTH/2+9)
            q=[(cx+tangent[0]*u+normal[0]*v,cy+tangent[1]*u+normal[1]*v) for u,v in [(-18,-6),(18,-6),(18,6),(-18,6)]]
            draw.polygon([xy(z) for z in q],fill=feature["color"])
            svg.append(f'<polygon points="{svg_points(q)}" fill="{feature["color"]}"/>')
    # Endpoints are technical dots only, never arrows or text.
    for p in (route[0],route[-1]):
        r=4*scale
        px,py=xy(p)
        draw.ellipse((px-r,py-r,px+r,py+r),fill="#4d5657")
        svg.append(f'<circle cx="{p[0]:.2f}" cy="{p[1]:.2f}" r="4" fill="#4d5657"/>')
    svg.append('</svg>')
    image.resize((SIZE,SIZE),Image.Resampling.LANCZOS).save(target/"route-guide.png")
    (target/"route-guide.svg").write_text("\n".join(svg),encoding="utf-8")


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    for authored_world in WORLDS:
        world=deepcopy(authored_world)
        angle=world.get("rotateDegrees",0)
        if angle:
            t=math.radians(angle)
            def rotate(p):
                x,y=p[0]-500,p[1]-500
                return (500+x*math.cos(t)-y*math.sin(t),500+x*math.sin(t)+y*math.cos(t))
            world["silhouette"]=[rotate(p) for p in world["silhouette"]]
            world["route"]=[rotate(p) for p in world["route"]]
            for feature in world["landmarks"]:
                feature["center"]=list(rotate(feature["center"]))
                feature["angle"]+=angle
        target=OUT/world["id"]
        target.mkdir(parents=True,exist_ok=True)
        outline=(world["silhouette"]+[world["silhouette"][0]]) if world.get("outlineMode")=="faceted" else catmull_rom(world["silhouette"],closed=True,steps=16)
        route=catmull_rom(world["route"],steps=36)
        # Authored route points are inset uniformly only if a narrow shore lobe needs it.
        inset_scale=1.0
        for attempt in range(8):
            clearance=min(min(point_segment_distance(p,a,b) for a,b in zip(outline,outline[1:])) for p in route)
            inside=all(point_inside(p,outline) for p in route)
            if inside and clearance>=ROAD_WIDTH/2+34:
                break
            route=[(500+(p[0]-500)*.97,500+(p[1]-500)*.97) for p in route]
            inset_scale*=.97
        samples,length=resample(route,4)
        changes,min_radius,signs=turns(samples)
        crosses=intersections(route[::3]+[route[-1]])
        edge_clearance=clearance-ROAD_WIDTH/2
        assert inside, f'{world["id"]}: road leaves island'
        assert edge_clearance>=34, f'{world["id"]}: shoreline margin {edge_clearance:.1f}'
        assert crosses==0, f'{world["id"]}: {crosses} crossings'
        assert changes>=3, f'{world["id"]}: only {changes} alternating turn signs'
        assert min_radius>=45, f'{world["id"]}: tight minimum radius {min_radius:.1f}'
        meters_per_unit=450/length
        data={
            "schemaVersion":"2.0", "referenceOnly":True, "id":world["id"], "name":world["name"],
            "purpose":"Technical schematic for new v2 art exploration, not final art, Unity scene, or production driving geometry.",
            "coordinates":{"units":"guide units", "canvas":[1000,1000], "origin":"top-left", "x":"right", "y":"down", "upAxisForEventualUnity":"Y", "suggestedUnityMapping":"(guideX, guideY) -> (X, Z), with Z sign chosen for camera composition"},
            "route":{"type":"single continuous open ribbon", "closed":False, "interpolation":"centripetal Catmull-Rom, alpha=0.5", "roadWidthGuideUnits":ROAD_WIDTH,
                     "authoredControlPoints":[list(p) for p in world["route"]], "shorelineInsetScaleAboutCanvasCenter":round(inset_scale,6),
                     "effectiveControlPoints":[[round(500+(p[0]-500)*inset_scale,3),round(500+(p[1]-500)*inset_scale,3)] for p in world["route"]], "lengthGuideUnits":round(length,3),
                     "start":[round(v,3) for v in route[0]], "end":[round(v,3) for v in route[-1]],
                     "samples":[{"s":round(p[2],3),"t":round(p[2]/length,6),"x":round(p[0],3),"y":round(p[1],3)} for p in samples],
                     "arcLengthSamplingIntervalApprox":4, "selfCrossings":crosses, "alternatingTurnSignChanges":changes,
                     "turnSignsImageCoordinates":signs, "minimumMeasuredBendRadiusGuideUnits":round(min_radius,2),
                     "minimumRoadEdgeToShoreGuideUnits":round(edge_clearance,2),
                     "turnIntent":"Long straights and flowing S bends with broad 20/30 degree local deflections; broad continuous arcs around landscape features. No tight hairpins, perimeter ring, or same-grade crossing."},
            "island":{"outlineInterpolation":"faceted closed polygon" if world.get("outlineMode")=="faceted" else "closed centripetal Catmull-Rom", "authoredOutlinePoints":[list(p) for p in world["silhouette"]], "outlineSamples":[[round(p[0],3),round(p[1],3)] for p in outline]},
            "landmarks":world["landmarks"], "journeySubzones":world["journey"],
            "scaleExample":{"referenceOnly":True,"targetTraversalSeconds":75,"assumedConstantSpeedMetersPerSecond":6,
                            "exampleRouteLengthMeters":450,"metersPerGuideUnit":round(meters_per_unit,6),
                            "exampleRoadWidthMeters":round(ROAD_WIDTH*meters_per_unit,2),
                            "note":"Illustrative conversion only. Actual traversal timing depends on camera, movement, elevation, pauses, and final Unity route. Timing is not guaranteed."},
            "artHandoff":{"noBakedLabels":True,"noArrows":True,"endpointDotsMayBeOmitted":True,"schematicColorsAreNotMandatory":True,
                          "road":"One wide uninterrupted physical road with a natural edge; never convert into stepping-stone tiles.",
                          "landscape":"Three readable landscape subzones; irregular expansive terrain around the road; route repeatedly enters the island interior.",
                          "passages":"Only short bridges or open-ended arches where indicated; keep road entry and exit visible. No long invisible tunnel.",
                          "grade":"Initial reference has no same-grade crossing and no forced shared traversal."}}
        (target/"route.json").write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding="utf-8")
        render(world,outline,route,target)
        print(f'{world["id"]}: length={length:.1f}, turnChanges={changes}, radius>={min_radius:.1f}, roadEdgeShoreMargin={edge_clearance:.1f}, crossings={crosses}')


if __name__=="__main__":
    main()
