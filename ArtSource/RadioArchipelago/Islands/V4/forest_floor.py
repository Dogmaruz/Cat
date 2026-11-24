"""Composed native understory clusters; immutable linked mesh data, real objects.

decorate() owns only its newly linked objects. It never edits the terrain,
route, buildings or existing scenery. Low plants fill nested beds around shrub
cores, stone groups, shore ribbons and the backs of radio houses.
"""
import math
import random
from collections import Counter
import bpy
from mathutils import Vector
from mathutils.kdtree import KDTree
import scenery as S
import nature_detail as N

CONFIG = {
    0: dict(groups=52, shrubs=200, ground=500, rocks=44, shrub='shrub'),
    3: dict(groups=42, shrubs=150, ground=380, rocks=34, shrub='shrub'),
    5: dict(groups=46, shrubs=170, ground=420, rocks=38, shrub='shrub'),
    7: dict(groups=36, shrubs=90, ground=320, rocks=22, shrub='shrub'),
    9: dict(groups=40, shrubs=110, ground=320, rocks=26, shrub='mauve_shrub'),
}


def _inside(p,polygon):
    x,y=p;result=False
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:result=not result
    return result


def _segment(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1]
    t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/max(1e-14,dx*dx+dy*dy)))
    return math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dy*t)


def _nearest_polyline(points,closed=True):
    tree=KDTree(len(points))
    for i,p in enumerate(points):tree.insert((p[0],p[1],0),i)
    tree.balance()
    def distance(p):
        _,index,_=tree.find((p[0],p[1],0))
        pairs=[(points[j%len(points)],points[(j+1)%len(points)]) for j in range(index-2,index+3)
               if closed or 0<=j<len(points)-1]
        return min((_segment(p,a,b) for a,b in pairs),default=math.dist(p,points[index][:2]))
    return distance,tree


def _river_sections(land):
    source=land.get('river_v4') or []
    if isinstance(source,dict):source=source.get('rivers',[source])
    result=[]
    for record in source:
        if not isinstance(record,dict):continue
        points=record.get('points',record.get('centerline',record.get('path',record.get('samples',[]))))
        def xy(p):return (p['x'],p['y']) if isinstance(p,dict) else tuple(p[:2])
        try:points=[xy(p) for p in points]
        except (TypeError,KeyError):continue
        if len(points)<2:continue
        half=float(record.get('half_width',record.get('width',record.get('channel_width',3))/2))
        distance,_=_nearest_polyline(points,closed=False)
        result.append((distance,half))
    return result


def _clover_template(palette,seed,col):
    """Low, closed volumetric leaves form a carpet rather than a flat decal."""
    rng=random.Random(seed);mesh=S._Mesh()
    green=S._c(palette,'leaf/foliage/green',(.22,.42,.12))
    light=S._c(palette,'leaf_light',tuple(min(.95,c*1.35) for c in green))
    mats=[S._mat('Floor_leaf_deep',S._tint(green,.78),.91),
          S._mat('Floor_leaf',green,.85),S._mat('Floor_leaf_sun',light,.78)]
    for tuft in range(38):
        a=tuft*2.399963;r=math.sqrt(rng.random())*1.06
        base=Vector((math.cos(a)*r,math.sin(a)*r,rng.uniform(.02,.065)))
        for leaf in range(3):
            theta=a+leaf*math.tau/3;length=rng.uniform(.16,.26)
            N._leaflet(mesh,base,(math.cos(theta),math.sin(theta),rng.uniform(.08,.25)),
                       length,length*.43,(tuft+leaf)%3,roll=rng.uniform(-.2,.2),bend=.018,rounded=True)
    N._bound(mesh,1.4,.27,bottom=.005)
    return [mesh.object('V4_Floor_clover_carpet', (0,0,0),1,mats,col,False)]


def decorate(idx,palette,land,col):
    """Return placement evidence and mesh counts for biome-appropriate beds.

    Native shape bounds: shrubs2.15*s, low props1.4*s, rocks1.95*s. We reserve
    six units from the route centreline plus the full asset radius and .25.
    Every candidate also passes exact polygon-shore and building-disc checks.
    """
    if idx not in CONFIG:return dict(groups=0,placements=0,mesh_objects=0,reason='biome uses exposed geology')
    cfg=CONFIG[idx];rng=random.Random(44100+idx)
    outline=[tuple(p[:2]) for p in land['outline']]
    if outline[0]==outline[-1]:outline.pop()
    route=[tuple(p[:2]) for p in land['route_points']]
    road_distance,road_tree=_nearest_polyline(route)
    shore_distance,_=_nearest_polyline(outline)
    rivers=_river_sections(land)
    buildings=[]
    for building in land.get('buildings',[]):
        position=building.get('position',building.get('center_xy'))
        if position is None:continue
        buildings.append((tuple(position[:2]),8.0 if 'relay' in building.get('kind','') else 12.0,
                          float(building.get('yaw',0))))
    height=land['height_at'];protected=land.get('road_width',8)/2+land.get('shoulder',2)
    rejection=Counter()
    def safe(p,radius,probe=False):
        if not _inside(p,outline) or shore_distance(p)<radius+.20:
            rejection['shore']+=1;return None
        if road_distance(p)<protected+radius+.25:
            rejection['road']+=1;return None
        if any(math.dist(p,center)<br+radius+.12 for center,br,_ in buildings):
            rejection['building']+=1;return None
        if any(distance(p)<half+radius+.6 for distance,half in rivers):
            rejection['river']+=1;return None
        z=height(*p)
        if not math.isfinite(z) or z<-.25:
            rejection['wet_ground']+=1;return None
        if probe:
            # Root the entire low carpet on a gently varying ground footprint.
            # Its base is slightly embedded, avoiding hovering and coplanar discs.
            zs=[z]+[height(p[0]+math.cos(a)*radius*.65,p[1]+math.sin(a)*radius*.65)
                    for a in (0,math.pi/2,math.pi,3*math.pi/2)]
            if not all(math.isfinite(v) for v in zs) or min(zs)<-.25 or max(zs)-min(zs)>.38:
                rejection['steep_low_bed']+=1;return None
            z=min(zs)-.012
        return z
    minx,maxx=min(p[0]for p in outline),max(p[0]for p in outline)
    miny,maxy=min(p[1]for p in outline),max(p[1]for p in outline)
    zones={key:[] for key in ('house','bend','shore','interior')}
    for x in range(math.ceil(minx),math.floor(maxx),3):
        for y in range(math.ceil(miny),math.floor(maxy),3):
            p=(x+rng.uniform(-.65,.65),y+rng.uniform(-.65,.65))
            z=safe(p,.65)
            if z is None:continue
            rd,sd=road_distance(p),shore_distance(p)
            near=min((math.dist(p,q)-r for q,r,_ in buildings),default=999)
            if near<9:zone='house';score=14-near
            elif sd<8:zone='shore';score=9-sd
            elif rd<20:zone='bend';score=20-rd
            else:zone='interior';score=min(rd,30)*.25
            zones[zone].append((score+rng.random()*6,p))
    for values in zones.values():values.sort(reverse=True)
    anchors=[];anchor_zones=[]
    # Interleave spatial intentions rather than exhausting a highest-score patch.
    for iteration in range(cfg['groups']*4):
        zone=('house','bend','shore','interior')[iteration%4]
        while zones[zone]:
            _,p=zones[zone].pop(0)
            if all(math.dist(p,q)>6.8 for q in anchors):
                anchors.append(p);anchor_zones.append(zone);break
        if len(anchors)>=cfg['groups']:break
    pool={};created=[];counts=Counter();placements=[];small_centres=[];shrub_centres=[]
    def asset(kind,variant):
        key=(kind,variant)
        if key in pool:
            objects=[]
            for source in pool[key]:
                obj=source.copy();obj.data=source.data;col.objects.link(obj);objects.append(obj)
        else:
            seed=710+variant*83
            if kind=='clover':objects=_clover_template(palette,seed,col)
            elif kind==cfg['shrub']:objects=S.make_tree((0,0,0),palette=palette,seed=seed,kind=kind,collection=col)
            elif kind=='slate':objects=S.make_rock((0,0,0),palette=palette,seed=seed,kind='slate',collection=col)
            else:objects=S.make_prop(kind,(0,0,0),palette=palette,seed=seed,collection=col)
            pool[key]=objects
        return objects
    def plant(kind,p,scale,group,category):
        radius=(2.15 if category=='shrubs' else 1.95 if category=='rocks' else 1.4)*scale
        z=safe(p,radius,probe=category=='ground')
        if z is None:return False
        centers=shrub_centres if category=='shrubs' else small_centres
        if category!='rocks' and any(math.dist(p,q)<(.72 if category=='ground' else 1.15)*(scale+s)
                                   for q,s in centers):return False
        objects=asset(kind,counts[category]%(3 if category in ('shrubs','rocks') else 2))
        yaw=rng.uniform(-math.pi,math.pi)
        for obj in objects:
            obj.location=(p[0],p[1],z-.035 if category=='rocks' else z)
            obj.scale=(scale,)*3;obj.rotation_euler.z=yaw
            obj['v3_role']='obstacle';obj['v4_floor_group']=group;obj['v4_floor_kind']=kind
        if category!='rocks':centers.append((p,scale))
        created.extend(objects);counts[category]+=1
        placements.append(dict(kind=kind,category=category,position=[p[0],p[1],z],scale=scale,
                               radius=radius,group=group,road_clearance=road_distance(p)-radius))
        return True
    # Revisit each deliberately selected bed, adding nested layers progressively.
    for turn in range(max(cfg['shrubs'],cfg['ground'],cfg['rocks'])*9):
        if all(counts[k]>=cfg[k] for k in ('shrubs','ground','rocks')):break
        if not anchors:break
        group=turn%len(anchors);center=anchors[group]
        layer=('shrubs','ground','ground','ground','rocks')[turn//len(anchors)%5]
        if counts[layer]>=cfg[layer]:continue
        if layer=='shrubs':
            distance=rng.uniform(.35,3.6);scale=rng.uniform(.48,.92);kind=cfg['shrub']
        elif layer=='rocks':
            distance=rng.uniform(.3,2.5);scale=rng.uniform(.48,1.0);kind='slate'
        else:
            distance=rng.uniform(1.1,6.2);scale=rng.uniform(.65,1.20)
            choice=rng.random()
            if idx==7:kind='flowers' if choice<.28 else 'grass_clump' if choice<.55 else 'clover'
            else:kind='clover' if choice<.55 else 'ferns' if choice<.77 else 'groundcover'
        angle=rng.uniform(0,math.tau);p=(center[0]+math.cos(angle)*distance,center[1]+math.sin(angle)*distance)
        plant(kind,p,scale,group,layer)
    unique={obj.data.name:obj.data for obj in created}
    triangles=lambda mesh:sum(len(face.vertices)-2 for face in mesh.polygons)
    return dict(groups=len(anchors),group_zones=dict(Counter(anchor_zones)),placements=len(placements),
                counts=dict(counts),targets={k:cfg[k] for k in ('shrubs','ground','rocks')},
                mesh_objects=len(created),unique_meshes=len(unique),mesh_templates=len(pool),
                unique_triangles=sum(triangles(mesh) for mesh in unique.values()),
                linked_render_triangles=sum(triangles(obj.data) for obj in created),
                min_protected_route_clearance=min((p['road_clearance'] for p in placements),default=None),
                river_sections_protected=len(rivers),rejections=dict(rejection),placement_records=placements)
