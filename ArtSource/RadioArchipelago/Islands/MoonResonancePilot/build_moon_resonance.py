"""Moon Resonance: independently authored third island, never edits prior islands.

Blender 5.2 --background --factory-startup --python build_moon_resonance.py
--route-only writes numeric contracts; --render-only reuses the exported scene.
Source Z-up metres. FBX anchors determine aligned Unity (x,z,-y).
"""
import bpy,bmesh,math,json,pathlib,sys,hashlib,random,collections,time,bisect
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt
HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[3];V4=HERE.parent/'V4'
OUT=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/MoonResonance'
PROOF=HERE/'Proof';sys.path.insert(0,str(HERE));sys.path.insert(0,str(V4));sys.path.insert(0,str(V4/'UnityExport'))
import architecture,scenery,export_emerald as ex
from route_math import make_route,metrics
RNG=random.Random(311101)
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def clamp(t):return min(1,max(0,t))
def smooth(t):t=clamp(t);return t*t*t*(10+t*(-15+6*t))
def linear(v):return v/12.92 if v<=.04045 else((v+.055)/1.055)**2.4
def col(rgb):return tuple(linear(float(c))for c in rgb[:3])+(rgb[3]if len(rgb)>3 else 1,)
POINTS,ARC,LENGTH=make_route()
KD=KDTree(len(POINTS))
for i,p in enumerate(POINTS):KD.insert((p[0],p[1],0),i)
KD.balance()
def route(s):
    s=s%LENGTH;i=min(len(POINTS)-1,bisect.bisect_right(ARC,s)-1);j=(i+1)%len(POINTS)
    t=(s-ARC[i])/(ARC[i+1]-ARC[i]);a,b=POINTS[i],POINTS[j];p=a+(b-a)*t;direction=(b-a)/np.linalg.norm(b-a)
    return Vector((float(p[0]),float(p[1]),0)),Vector((-float(direction[1]),float(direction[0]),0))
def nearest(x,y):
    _,i,_=KD.find((x,y,0));best=(1e30,0)
    for a in((i-1)%len(POINTS),i):
        b=(a+1)%len(POINTS);p=POINTS[a];v=POINTS[b]-p;t=clamp(((x-p[0])*v[0]+(y-p[1])*v[1])/np.dot(v,v));q=p+v*t;d=(q[0]-x)**2+(q[1]-y)**2
        if d<best[0]:best=(d,ARC[a]+t*(ARC[a+1]-ARC[a]))
    return math.sqrt(best[0]),best[1]
HEIGHT_KEYS=[(0,4),(55,4),(145,8),(230,14),(295,20),(380,20),(455,10),(530,7),(610,4),(LENGTH,4)]
def play(s):
    s%=LENGTH;i=min(len(HEIGHT_KEYS)-2,bisect.bisect_right([p[0]for p in HEIGHT_KEYS],s)-1)
    a,ha=HEIGHT_KEYS[i];b,hb=HEIGHT_KEYS[i+1];return ha+(hb-ha)*smooth((s-a)/(b-a))
def write_route(preserve_exports=False):
    OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(parents=True,exist_ok=True)
    pts=[dict(x=float(p[0]),y=0,z=-float(p[1]))for p in POINTS]
    doc=dict(schema='radio_archipelago_unity_route/1',id='03_moon_resonance',closed=True,roadWidth=6,road_width=6,point_count=len(pts),length=LENGTH,coordinate_mapping='Blender(x,y,z) -> aligned Unity(x,z,-y)',points=pts)
    if not preserve_exports:(OUT/'moon_resonance_route.json').write_text(json.dumps(doc,indent=2),encoding='utf8')
    keys=[dict(distance=float(s),height=play(float(s)))for s in np.arange(0,LENGTH,.5)];keys.append(dict(distance=LENGTH,height=4))
    if not preserve_exports:
        for name in('play_elevation.json','ground_elevation.json'):(OUT/name).write_text(json.dumps(dict(length=LENGTH,closed=True,keys=keys),indent=2),encoding='utf8')
    report=metrics(POINTS,ARC);report.update(point_count=len(pts),height_min=min(k['height']for k in keys),height_max=max(k['height']for k in keys),max_grade=max(abs((keys[i+1]['height']-keys[i]['height'])/(keys[i+1]['distance']-keys[i]['distance']))for i in range(len(keys)-1)),height_keys=HEIGHT_KEYS,first_straight=[0,35],final_straight=[638,LENGTH],flat_finish=[610,LENGTH],flight_corridor_half_width=8.2,minimum_ground_below_route=2.5)
    (HERE/'route_plan.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('MOON_ROUTE',json.dumps(report),flush=True)

ISLANDS=[
    ('Moon lagoon',(41,60),(70,50),66),
    ('First flower landing',(-6,6),(57,28),50),
    ('Silver cascade east',(109,62),(25,38),58),
    ('Resonator saddle',(25,129),(25,30),59),
    ('Ring observatory ridge',(-23,181),(57,30),76),
    ('West silver grove',(-70,141),(25,29),59),
    ('Western radio crag',(-131,36),(40,25),66),
    ('Reward lavender meadow',(-77,1),(52,24),53),
    ('Central amethyst crag',(-24,104),(18,24),64),
    ('Remote silver spire',(-135,173),(13,17),60),
    ('Remote eastern crag',(98,154),(17,20),66),
    ('Low hanging moonlet',(56,-42),(15,12),44),
    ('High distant needle',(-28,243),(12,14),70),
    ('Pearl hopping islet',tuple(route(465)[0][:2]),(7.2,6.4),25),
    ('Lavender bell islet',tuple(route(489)[0][:2]),(10.2,8.2),34),
    ('Silver hopping islet',tuple(route(516)[0][:2]),(6.5,8.0),27),
]
HOPPING_STATIONS=[465,489,516]
ROCKS=[(.48,.47,.60),(.64,.62,.74),(.75,.74,.83),(.58,.57,.68),(.82,.80,.87),(.67,.66,.78),(.85,.84,.90),(.58,.59,.69)]
CATEGORIES=['Terrain','Cliffs','Rocks','Foliage','Grass','Wood','Architecture','Metal','Water','Waterfall','Foam','Vines','Crystal','Mist']
OWN_MATERIALS={};CAP_BVH=None;GROUND_DECOR=[]
# Local shared export primitives were adapted read-only from the already verified
# complete-source-normal workflow, never imported from or written to prior art.
exec(compile((HERE/'shared_export.py').read_text(encoding='utf8'),str(HERE/'shared_export.py'),'exec'))

def material(category):
    if category not in OWN_MATERIALS:
        m=bpy.data.materials.new('RA_Moon_'+category);m.use_nodes=True;m.diffuse_color=(1,1,1,1)
        m.use_backface_culling=category not in('Foliage','Grass','Waterfall','Foam','Mist','Vines')
        bs=ex.node_bsdf(m);bs.inputs['Roughness'].default_value=.23 if category in('Water','Waterfall','Foam')else .31 if category=='Metal'else .55 if category=='Crystal'else .79
        bs.inputs['Metallic'].default_value=.65 if category=='Metal'else .12 if category=='Water'else 0
        attr=m.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
        if category=='Crystal':
            m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Emission Color']);bs.inputs['Emission Strength'].default_value=.12
            bs.inputs['Coat Weight'].default_value=.28
        if category=='Mist':bs.inputs['Roughness'].default_value=1
        if category=='Mist':m.node_tree.links.new(attr.outputs['Alpha'],bs.inputs['Alpha'])
        # Subtle real-scale shading texture: vertex colors remain export truth.
        if category in('Cliffs','Rocks','Terrain','Architecture','Wood'):
            noise=m.node_tree.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2.8 if category in('Cliffs','Rocks')else 5
            noise.inputs['Detail'].default_value=3
            bump=m.node_tree.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.24;bump.inputs['Distance'].default_value=.055 if category in('Cliffs','Rocks')else .022
            m.node_tree.links.new(noise.outputs['Fac'],bump.inputs['Height']);m.node_tree.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
        OWN_MATERIALS[category]=m
    return OWN_MATERIALS[category]

def footprint(x,y,pad=0):
    return any(((x-c[0])/(r[0]-pad))**2+((y-c[1])/(r[1]-pad))**2<.90**2 for _,c,r,_ in ISLANDS if min(r)>pad)
def lagoon_r(x,y):return math.sqrt(((x-49)/48)**2+((y-65)/31)**2)
def meadow_color(x,y):
    """Warm heather soil and violet turf interrupted by visible silver scree."""
    t=clamp(.48+.21*math.sin(x*.12+y*.13)+.16*math.sin(x*.71-y*.53))
    rgb=tuple(a*(1-t)+b*t for a,b in zip((.36,.27,.48),(.67,.49,.73)))
    scree=smooth((math.sin(x*.055-y*.047)+.40*math.sin(x*.12+y*.07)-.36)/.85)*.65
    return tuple(a*(1-scree)+b*scree for a,b in zip(rgb,(.70,.68,.77)))
def terrain_height(x,y):
    d,s=nearest(x,y);height=play(s)-2.65
    height+=(1-smooth((10-d)/4))*(.36*math.sin(x*.16+y*.09)+.22*math.cos(y*.20-x*.10))
    r=lagoon_r(x,y)
    if r<1.75:
        bank=.12+2.55*smooth((r-.72)/.37)
        height=bank+(height-bank)*smooth((r-1.28)/.47)
    # The actual basin includes a broad lip whose wet channel runs to the fall.
    if 45<y<67 and 82<x<144:
        f=(1-smooth((abs(y-55)-2.0)/2.3))*smooth((x-82)/7)
        height=height*(1-f)+.36*f
    # High spring carved into real upper ridge and out through its east cliff.
    spring=math.sqrt(((x-12)/9.2)**2+((y-177)/8.5)**2)
    if spring<1.4:height=min(height,13.9+2.9*smooth((spring-.65)/.62))
    if 12<x<53 and abs(y-174)<2.2:height=min(height,13.9)
    return height

def landmass(name,center,radii,depth,seed):
    cx,cy=center;rx,ry=radii;n=144;nr=29
    def rim(a):return 1+.065*math.sin(a*5+seed)+.035*math.cos(a*9+seed*.9)+.022*math.sin(a*17+seed*.3)
    vs=[(cx,cy,terrain_height(cx,cy))];colors=[col((.63,.57,.72))];fs=[]
    for ring in range(1,nr+1):
        t=ring/nr
        for i in range(n):
            a=math.tau*i/n;f=rim(a);x=cx+math.cos(a)*rx*t*f;y=cy+math.sin(a)*ry*t*f;z=terrain_height(x,y)
            vs.append((x,y,z));variation=clamp(.48+.21*math.sin(x*.12+y*.13)+.16*math.sin(x*.71-y*.53))
            rgb=meadow_color(x,y)
            r=lagoon_r(x,y)
            if .74<r<1.18:
                wet=.76*clamp(1-abs(r-.93)/.25);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.30,.42,.48)))
            spring=math.sqrt(((x-12)/9.2)**2+((y-177)/8.5)**2)
            if .6<spring<1.3:
                wet=.6*clamp(1-abs(spring-.95)/.4);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.34,.43,.51)))
            colors.append(col(rgb))
    for i in range(n):fs.append((0,1+i,1+(i+1)%n))
    for ring in range(nr-1):
        a0=1+ring*n;b0=a0+n
        for i in range(n):j=(i+1)%n;fs.extend(((a0+i,b0+i,b0+j),(a0+i,b0+j,a0+j)))
    top=mesh('Moon_Terrain_'+name,vs,fs,'Terrain',colors,soft=True)
    verts=[];cc=[];faces=[]
    # Thick masonry-like strata and deep faceted flutes, progressively tapering.
    levels=[(0,1),(-.8,1.012),(-1.6,.99),(-2.3,1.022),(-4.8,1.01),(-5.6,.978),(-8.3,.985),(-9.0,1.006),(-depth*.24,.94),(-depth*.255,.968),(-depth*.33,.91),(-depth*.345,.932),(-depth*.42,.85),(-depth*.435,.87),(-depth*.52,.77),(-depth*.535,.79),(-depth*.64,.65),(-depth*.655,.68),(-depth*.78,.48),(-depth*.795,.50),(-depth*.94,.25),(-depth,.07)]
    if depth<44:levels=[(drop*depth/44 if i<8 else drop,scale)for i,(drop,scale)in enumerate(levels)]
    for ring,(drop,scale)in enumerate(levels):
        for i in range(n):
            a=math.tau*i/n;f=rim(a)*(1+.018*math.sin(i*.83+ring*.42));x=cx+math.cos(a)*rx*f*scale+1.3*math.sin(ring*.4);y=cy+math.sin(a)*ry*f*scale+.9*math.cos(ring*.6)
            z=terrain_height(cx+math.cos(a)*rx*rim(a),cy+math.sin(a)*ry*rim(a))+drop
            if ring>1:z+=.35*math.sin(i*.65+ring*.4)+.25*math.sin(i*1.9+ring*.7)
            if ring==0:x=cx+math.cos(a)*rx*rim(a);y=cy+math.sin(a)*ry*rim(a)
            verts.append((x,y,z));rgb=ROCKS[(ring//2+i//11+seed)%len(ROCKS)];shade=.93+.07*math.sin(i*2.07+ring)
            cc.append(col(tuple(c*shade for c in rgb)))
    for ring in range(len(levels)-1):
        for i in range(n):j=(i+1)%n;a=ring*n+i;b=ring*n+j;c=(ring+1)*n+j;d=(ring+1)*n+i;faces.extend(((a,d,c),(a,c,b)))
    tip=len(verts);verts.append((cx+2,cy-1,terrain_height(cx,cy)-depth-3));cc.append(col((.41,.41,.53)))
    for i in range(n):j=(i+1)%n;faces.append((tip,(len(levels)-1)*n+j,(len(levels)-1)*n+i))
    return top,mesh('Moon_Cliffs_'+name,verts,faces,'Cliffs',cc,soft=False)

def unify_terrain():
    """One common cap grid eliminates coplanar seams between joining crags."""
    for ob in list(bpy.context.scene.objects):
        if ob.type=='MESH'and ob.get('moon_bucket')=='Terrain':bpy.data.objects.remove(ob,do_unlink=True)
    step=.90;x0=-181;y0=-59;nx=358;ny=357
    def inside(x,y):
        for index,(_,c,r,_)in enumerate(ISLANDS):
            xx=(x-c[0])/r[0];yy=(y-c[1])/r[1];a=math.atan2(yy,xx);seed=1130+index
            rim=1+.065*math.sin(a*5+seed)+.035*math.cos(a*9+seed*.9)+.022*math.sin(a*17+seed*.3)
            if xx*xx+yy*yy<rim*rim:return True
        return False
    points=[];edges=[]
    # Constrain every original irregular rim. CDT splits rim intersections, so
    # the union has no duplicates, stair steps, or gaps at the matching cliffs.
    for index,(_,c,r,_)in enumerate(ISLANDS):
        first=len(points);seed=1130+index
        for i in range(144):
            a=math.tau*i/144;rim=1+.065*math.sin(a*5+seed)+.035*math.cos(a*9+seed*.9)+.022*math.sin(a*17+seed*.3)
            points.append(Vector((c[0]+math.cos(a)*r[0]*rim,c[1]+math.sin(a)*r[1]*rim)));edges.append((first+i,first+(i+1)%144))
    for j in range(ny):
        for i in range(nx):
            x=x0+(i+.5)*step;y=y0+(j+.5)*step
            if inside(x,y):points.append(Vector((x,y)))
    coords,_,triangles,*_=delaunay_2d_cdt(points,edges,[],0,.00001,False)
    verts=[];faces=[];colors=[]
    for p in coords:
        x,y=p;verts.append((x,y,terrain_height(x,y)));rgb=meadow_color(x,y)
        r=lagoon_r(x,y)
        if .75<r<1.18:
            wet=.76*clamp(1-abs(r-.965)/.24);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.30,.42,.48)))
        sr=math.sqrt(((x-12)/9.2)**2+((y-177)/8.5)**2)
        if .6<sr<1.3:
            wet=.65*clamp(1-abs(sr-.95)/.4);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.34,.43,.51)))
        colors.append(col(rgb))
    for f in triangles:
        p=sum((coords[i]for i in f),Vector((0,0)))/len(f)
        if inside(*p):faces.append(tuple(f))
    return mesh('Moon_UnifiedIrregularTerrain',verts,faces,'Terrain',colors,soft=True)

def runtime_density():
    """Keep complete volumetric leaves; reduce hidden branch tessellation."""
    report=dict(leaf_components_before=0,leaf_components_retained=0,leaf_triangles_removed=0,branch_objects_reduced=0)
    for ob in list(bpy.context.scene.objects):
        if ob.type!='MESH':continue
        if 'individual_leaves'in ob.name:
            data=ob.data;n=len(data.vertices);parent=list(range(n))
            def find(x):
                while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
                return x
            for edge in data.edges:
                a,b=map(find,edge.vertices)
                if a!=b:parent[b]=a
            groups=collections.defaultdict(list)
            for i in range(n):groups[find(i)].append(i)
            rng=random.Random(8821+n);remove=[]
            for ids in groups.values():
                if len(ids)not in(6,10):continue
                report['leaf_components_before']+=1
                if rng.random()<.38:report['leaf_components_retained']+=1
                else:remove.extend(ids);report['leaf_triangles_removed']+=8 if len(ids)==6 else 16
            bm=bmesh.new();bm.from_mesh(data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.verts[i]for i in remove],context='VERTS');bm.to_mesh(data);bm.free();data.update()
        elif 'branchwork'in ob.name:
            mod=ob.modifiers.new('Moon mobile inner branch tessellation','DECIMATE');mod.ratio=.55;mod.use_collapse_triangulate=True;report['branch_objects_reduced']+=1
    return report

BUILD_SITES=[('cloud_house',(-16,27),1.65,-.12),('cloud_house',(80,35),1.65,-.55),('cloud_observatory',(-24,165),1.7,0)]
def add_architecture():
    palette=dict(style='cloud',wall='#F1ECEB',roof='#A97561',wood='#766B84',stone='#A6A3B8',metal='#D2AE76',accent='#80BCBC',trim='#DBD4E5',glass='#46627F')
    report=[]
    for index,(kind,(x,y),scale,yaw)in enumerate(BUILD_SITES):
        z=actual_ground(x,y)
        if z is None:raise RuntimeError('Architecture unsupported: '+kind)
        # A proper inhabited plaster observatory carries the receiver. Its
        # broad copper dome and lower annexes replace the former open cage.
        actual_kind='cloud_workshop'if kind=='cloud_observatory'else kind
        actual_scale=2.0 if kind=='cloud_observatory'else scale
        objs=architecture.build_building(actual_kind,location=(x,y,z-.08),yaw=yaw,scale=actual_scale,palette=palette,seed=410+index)
        if kind=='cloud_observatory':
            builder=architecture._Builder('cloud_observatory',palette,819,None)
            for sign in(-1,1):
                ax=sign*6.9;builder.box('observatory pearl annex foundation',(ax,.9,.28),(5.3,5.6,.55),'stone',.16)
                builder.box('observatory solid pearl plaster annex',(ax,.9,2.35),(4.9,5.2,4.15),'wall',.26)
                builder.box('observatory low copper annex roof',(ax,.9,4.54),(5.2,5.5,.28),'roof',.12)
                builder.window(ax,-1.73,1.12,1.40,2.05,shutters=False)
                builder.speaker(ax,3.56,2.23,.70)
                builder.torus('annex roof receiver rail',(ax,.9,4.76),1.6,.045,'metal',n=48)
            objs.extend(builder.finish((x,y,z-.08),yaw,1.0))
        track_decor(objs,x,y,kind,z-.08)
        if kind=='cloud_house':
            # Large frontal speaker porthole and delicate curved terrace rails.
            yy=y-4.34;xoff=x+2.5
            for r in(1.28,1.12):tube('Moon_CottageCopperSpeakerRing',[(xoff+r*math.cos(a),yy,z+3.7+r*math.sin(a))for a in np.linspace(0,math.tau,65)],.10,'Metal',(.81,.64,.40),10)
            tube('Moon_CottageSpeakerDarkInset',[(xoff,yy+.12,z+3.7),(xoff,yy+.035,z+3.7)],1.03,'Architecture',(.19,.23,.31),32)
            for h in np.linspace(-.75,.75,6):
                w=math.sqrt(1.08**2-h*h);tube('Moon_CottageSpeakerHorizontalSlat',[(xoff-w,yy-.05,z+3.7+h),(xoff+w,yy-.05,z+3.7+h)],.055,'Metal',(.67,.52,.32),8)
            for r,h in[(7.0,.75),(7.0,1.35)]:
                pts=[(x+r*math.cos(a),y+r*.75*math.sin(a),z+h)for a in np.linspace(-.1,math.pi*1.05,65)]
                tube('Moon_CottageBalconyCurvedRail',pts,.055,'Metal',(.78,.65,.46),8)
            for a in np.linspace(0,math.pi,15):
                xx=x+7*math.cos(a);yy=y+5.25*math.sin(a);tube('Moon_CottageBalconyPost',[(xx,yy,z+.2),(xx,yy,z+1.4)],.06,'Metal',(.75,.64,.48),8)
        report.append(dict(kind=kind,position=[x,y,z],scale=actual_scale,base_actual_kind=actual_kind,distance_to_route=nearest(x,y)[0],objects=len(objs)))
    x,y=-24,165;z=actual_ground(x,y)+20.5
    for shift,r in[(0,7.8),(.32,7.5)]:
        tube('Moon_ImposingObservatoryReceiver',[(x+r*math.cos(a),y+shift,z+r*math.sin(a))for a in np.linspace(0,math.tau,145)],.25,'Metal',(.82,.72,.57),12)
    # Receiver remains open; widely spaced copper calibration tabs catch daylight.
    for a in np.linspace(0,math.tau,12,endpoint=False):
        tube('Moon_ReceiverCalibrationTab',[(x+7.25*math.cos(a),y-.04,z+7.25*math.sin(a)),(x+8.05*math.cos(a),y-.04,z+8.05*math.sin(a))],.13,'Metal',(.94,.82,.58),8)
    tube('Moon_ReceiverAxialMast',[(x,y,z-13),(x,y,z+8.5)],.22,'Metal',(.74,.62,.45),12)
    for sign in(-1,1):tube('Moon_ReceiverCradle',[(x+sign*2.9,y,z-8.4),(x+sign*4.4,y,z-6.2),(x+sign*5.3,y,z-5.5)],.20,'Metal',(.72,.61,.48),10)
    return report

def add_resonators():
    # The same design drives future source builds and the isolated asset patch.
    # Its private deterministic RNG does not perturb trees, flowers or buildings.
    from crystal_cluster_design import build_sources
    return build_sources(sys.modules[__name__])

def add_stones():
    count=0
    for attempt in range(2500):
        x=RNG.uniform(-164,133);y=RNG.uniform(-16,207);d,s=nearest(x,y)
        if not footprint(x,y,3)or d<8.3 or lagoon_r(x,y)<1.02:continue
        z=actual_ground(x,y)
        if z is None:continue
        scale=RNG.uniform(.5,1.45);objs=scenery.make_rock((x,y,z-.18),scale=scale,seed=5110+attempt,kind='boulder',palette={'rock':(.68,.67,.77)})
        for ob in objs:ob['moon_bucket']='Rocks'
        track_decor(objs,x,y,'weathered silvery boulder',z-.18);count+=1
        if count>=108:break
    # Strong layered crags carry the crystal group instead of isolated neon poles.
    for index,(x,y,r,h)in enumerate([(-26,106,10,13),(-3,134,7,17),(-85,180,6,11),(121,88,6,13)]):
        z=actual_ground(x,y)
        if z is None or nearest(x,y)[0]<r+8:continue
        n=55;vs=[];fs=[];cc=[]
        for k in range(18):
            t=k/17;rr=r*(1-.58*t)*(1+.06*math.sin(k*2.4))
            for j in range(n):
                a=math.tau*j/n;f=1+.07*math.sin(a*7+index);vs.append((x+rr*f*math.cos(a),y+rr*.78*f*math.sin(a),z+h*t+.14*math.sin(j*.7)));cc.append(col(ROCKS[(k//2+j//9+index)%len(ROCKS)]))
        for k in range(17):
            for j in range(n):jj=(j+1)%n;fs.append((k*n+j,k*n+jj,(k+1)*n+jj,(k+1)*n+j))
        fs.append(tuple(reversed(range(n))));fs.append(tuple(17*n+j for j in range(n)))
        mesh('Moon_LayeredResonatorBedrock',vs,fs,'Rocks',cc)
    return count

def add_trees():
    count=0
    for attempt in range(4500):
        x=RNG.uniform(-161,132);y=RNG.uniform(-14,211);scale=RNG.uniform(1.45,2.0);d,s=nearest(x,y)
        if not footprint(x,y,5)or d<9.2+2.15*scale or lagoon_r(x,y)<1.10:continue
        if any(math.hypot(x-cx,y-cy)<13 for _,(cx,cy),_,_ in BUILD_SITES):continue
        if any(math.hypot(x-cx,y-cy)<7 for cx,cy in[(-26,107),(-3,134),(128,75)]):continue
        z=actual_ground(x,y)
        if z is None:continue
        kind='pine'if count%7==0 else'broadleaf'
        pal={'leaf':(.43,.47,.62),'leaf_light':(.72,.73,.83),'bark':(.43,.40,.48)}if kind=='pine'else{'leaf':(.57,.60,.71),'leaf_light':(.87,.87,.92),'bark':(.48,.44,.49)}
        objs=scenery.make_tree((x,y,z-.04),scale=scale,seed=6000+attempt,kind=kind,palette=pal)
        track_decor(objs,x,y,'silvery '+kind,z-.04);count+=1
        if count>=48:break
    return count

def flower_patch(x,y,scale,seed):
    rng=random.Random(seed);vs=[];fs=[];cc=[]
    def face(points,rgb):
        b=len(vs);vs.extend(points);cc.extend([col(rgb)]*len(points));fs.append(tuple(range(b,b+len(points))))
    for k in range(13):
        xx=x+rng.uniform(-1,1)*scale;yy=y+rng.uniform(-1,1)*scale;z=actual_ground(xx,yy)
        if z is None or lagoon_r(xx,yy)<.98:continue
        h=rng.uniform(.28,.75)*scale;green=(.37,.41,.53)
        face([(xx-.018,yy,z),(xx+.018,yy,z),(xx+.014,yy,z+h),(xx-.014,yy,z+h)],green)
        petal=[(.71,.43,.83),(.96,.88,.97),(.46,.47,.78)][seed%3];r=.15*scale
        for j in range(5):
            a=j*math.tau/5;dx,dy=math.cos(a),math.sin(a)
            face([(xx,yy,z+h),(xx+dx*r-dy*r*.48,yy+dy*r+dx*r*.48,z+h+.02),(xx+dx*r*1.75,yy+dy*r*1.75,z+h-.04),(xx+dx*r+dy*r*.48,yy+dy*r-dx*r*.48,z+h+.02)],petal)
        for j in range(2):
            a=seed*.3+j*2.6;dx,dy=math.cos(a),math.sin(a)
            face([(xx,yy,z+h*.4),(xx+dx*.22-dy*.07,yy+dy*.22+dx*.07,z+h*.5),(xx+dx*.4,yy+dy*.4,z+h*.7),(xx+dx*.22+dy*.07,yy+dy*.22-dx*.07,z+h*.5)],green)
    return mesh('Moon_PearlPeriwinkleWildflowers',vs,fs,'Foliage',cc)

def lavender_tuft(x,y,scale,seed):
    rng=random.Random(seed);vs=[];fs=[];cc=[]
    for j in range(23):
        a=j*2.39996;rr=math.sqrt(rng.random())*.7*scale;xx=x+rr*math.cos(a);yy=y+rr*math.sin(a);zz=actual_ground(xx,yy)
        if zz is None:continue
        h=rng.uniform(.45,1.05)*scale;lean=.16*scale;side=Vector((math.cos(a)*.055*scale,math.sin(a)*.055*scale,0));base=Vector((xx,yy,zz));tip=base+Vector((math.cos(a)*lean,math.sin(a)*lean,h));b=len(vs)
        vs.extend(map(tuple,[base-side,base+side,tip]));fs.append((b,b+1,b+2));cc.extend([col((.32,.34,.47)),col((.47,.34,.61)),col((.68,.45,.79))])
        # Volumetric lavender florets, not texture cards.
        for k in range(3):
            pp=tip-Vector((0,0,.10*k*scale));r=.065*scale;b=len(vs)
            vs.extend([tuple(pp+Vector(v)*r)for v in[(1,0,0),(0,1,0),(-1,0,0),(0,-1,0),(0,0,1.5),(0,0,-1.5)]])
            for q in range(4):fs.extend([(b+q,b+(q+1)%4,b+4),(b+(q+1)%4,b+q,b+5)])
            cc.extend([col((.56+.035*k,.34+.035*k,.73+.025*k))]*6)
    return mesh('Moon_LavenderGrassFlowerSpikes',vs,fs,'Grass',cc)

def add_gardens():
    count=0
    for attempt in range(5000):
        x=RNG.uniform(-164,135);y=RNG.uniform(-18,209);d,s=nearest(x,y)
        if not footprint(x,y,1.8)or d<6.8 or lagoon_r(x,y)<1.04:continue
        if math.sin(x*.059+y*.043)<-.35:continue
        z=actual_ground(x,y)
        if z is None:continue
        if any(math.hypot(x-cx,y-cy)<6.8 for _,(cx,cy),_,_ in BUILD_SITES):continue
        scale=RNG.uniform(.75,1.55);flower_patch(x,y,scale,attempt);lavender_tuft(x+.4,y+.3,scale,attempt)
        count+=1
        if count>=370:break
    return count

def add_hopping_gardens():
    report=[]
    for index,s in enumerate(HOPPING_STATIONS):
        p,n=route(s);site=ISLANDS[13+index];rx,ry=site[2];groups=0
        for j in range(9):
            a=j*2.39996+index;r=.36+.055*(j%5);x=p.x+rx*r*math.cos(a);y=p.y+ry*r*math.sin(a);z=actual_ground(x,y)
            if z is None:continue
            flower_patch(x,y,.75 if j%2 else 1.05,9120+index*100+j)
            lavender_tuft(x+.18,y+.12,.65 if j%2 else .85,9230+index*100+j);groups+=1
        for j in range(4):
            a=j*math.tau/4+.55;r=.56;x=p.x+rx*r*math.cos(a);y=p.y+ry*r*math.sin(a);z=actual_ground(x,y)
            if z is None:continue
            objs=scenery.make_rock((x,y,z-.09),scale=.45+.07*j,seed=9370+index*100+j,kind='boulder',palette={'rock':(.64,.60,.74)})
            for ob in objs:ob['moon_bucket']='Rocks'
            track_decor(objs,x,y,'small hopping islet stone',z-.09)
        report.append(dict(name=site[0],station=s,source_center=[p.x,p.y,actual_ground(p.x,p.y)],route_floor=play(s),aligned_unity_route_center=[p.x,play(s),-p.y],radii=list(site[2]),rock_depth=site[3],garden_groups=groups))
    (HERE/'hopping-islets.json').write_text(json.dumps(dict(route_unchanged=True,cloud_step_window=[442,550],islets=report),indent=2),encoding='utf8')
    return report

def pool_with_outlet(name,cx,cy,rx,ry,channel_y,width,end_x,z):
    points=[];edges=[];n=144
    for i in range(n):
        a=math.tau*i/n;points.append(Vector((cx+rx*math.cos(a),cy+ry*math.sin(a))));edges.append((i,(i+1)%n))
    first=len(points)
    points.extend(Vector(p)for p in[(cx,channel_y-width/2),(end_x,channel_y-width/2),(end_x,channel_y+width/2),(cx,channel_y+width/2)])
    edges.extend((first+i,first+(i+1)%4)for i in range(4))
    def inside(x,y):return ((x-cx)/rx)**2+((y-cy)/ry)**2<1.000001 or(cx<=x<=end_x and abs(y-channel_y)<=width*.500001)
    step=1.1 if rx>20 else .55
    for y in np.arange(cy-ry,cy+ry,step):
        for x in np.arange(cx-rx,end_x,step):
            if inside(x,y):points.append(Vector((float(x),float(y))))
    coords,_,ff,*_=delaunay_2d_cdt(points,edges,[],0,.00001,False)
    verts=[(p.x,p.y,z)for p in coords];faces=[];colors=[]
    for p in coords:
        r=clamp(math.hypot((p.x-cx)/rx,(p.y-cy)/ry));colors.append(col(tuple(a*(1-r)+b*r for a,b in zip((.14,.44,.60),(.25,.65,.70)))))
    for f in ff:
        p=sum((coords[i]for i in f),Vector((0,0)))/len(f)
        if inside(*p):faces.append(tuple(f))
    return mesh(name,verts,faces,'Water',colors,[(p.x*.1,p.y*.1)for p in coords],True)

def add_water():
    # The pool and its feeder share one clipped planar surface, with no overlap.
    def cliff_lip(y,start,end):
        supported=[float(x)for x in np.arange(start,end,.2)if actual_ground(float(x),y)is not None]
        if not supported:raise RuntimeError('Waterfall has no physical cliff lip')
        lo=max(supported);hi=lo+.2
        for _ in range(15):
            mid=(lo+hi)*.5
            if actual_ground(mid,y)is None:hi=mid
            else:lo=mid
        return(lo+hi)*.5
    low=cliff_lip(55,100,150);high=cliff_lip(174,20,60)
    pool_with_outlet('Moon_LargeShallowBlueLagoonAndOutlet',49,65,48*.91,31*.91,55,3.9,low+.5,.82)
    pool_with_outlet('Moon_UpperRidgeSpringAndOutlet',12,177,8.8,8.0,174,3.6,high+.5,14.44)
    falls=[[(low-.4,55,.82),(low+.5,55,.75),(low+1.8,55,-.7),(low+2.5,55,-7),(low+2.5,55,-20),(low+3.5,55,-38),(low+2.5,55,-58)],[(high-.4,174,14.44),(high+.5,174,14.2),(high+1.8,174,11),(high+2.0,174,3),(high+3.0,174,-12),(high+2.0,174,-32),(high+3.0,174,-54)]]
    (HERE/'waterfall-lip-audit.json').write_text(json.dumps(dict(method='Actual Terrain BVH horizontal extent at each stream centre, binary-refined to the matching cliff rim',low_lip=[low,55,.82],high_lip=[high,174,14.44],basin_floor=.12,lagoon_surface=.82),indent=2),encoding='utf8')
    for index,points in enumerate(falls):
        vv=[];ff=[];uv=[];cc=[];n=24;width=5.2 if index==0 else 3.7
        for row,p in enumerate(points):
            for j in range(n+1):
                t=j/n;vv.append((p[0]+.10*math.sin(t*25+row),p[1]+(t-.5)*width,p[2]+.04*math.sin(t*19+row)));uv.append((t,row*.8));cc.append(col(tuple(a*(1-abs(t-.5))+b*abs(t-.5)for a,b in zip((.31,.63,.75),(.78,.89,.94)))))
        for row in range(len(points)-1):
            for j in range(n):a=row*(n+1)+j;b=a+n+1;ff.append((a,a+1,b+1,b))
        mesh('Moon_FlowingWaterfall_'+str(index),vv,ff,'Waterfall',cc,uv,True)
        for j in range(6):tube('Moon_WaterfallWhiteFlowRibbons',[(p[0]+.06,p[1]+(j-2.5)*width/7,p[2]+.035)for p in points],.035 if j%2 else.065,'Foam',(.77,.88,.94),6)
    for index,(cx,cy,rx,ry,z)in enumerate([(49,65,44,28,.86),(12,177,8.8,8,14.48)]):
        for j in range(15 if index==0 else 6):
            a=j*2.39996;r=.20+.046*j
            tube('Moon_SilverLagoonWavelets',[(cx+rx*r*math.cos(a+f),cy+ry*r*math.sin(a+f),z)for f in np.linspace(-.25,.25,18)],.036,'Foam',(.65,.81,.88),5)
    # Exact actual surface shoreline stones and damp bank roots, BVH checked.
    count=0
    for index,(cx,cy,rx,ry,z)in enumerate([(49,65,48,31,.92),(12,177,9.2,8.5,14.55)]):
        for j in range(65 if index==0 else 24):
            a=j*math.tau/(65 if index==0 else 24);lo,hi=.6,1.3
            for _ in range(18):
                r=(lo+hi)*.5;x=cx+rx*r*math.cos(a);y=cy+ry*r*math.sin(a);h=actual_ground(x,y)
                if h is None or h>z:hi=r
                else:lo=r
            r=(lo+hi)*.5;x=cx+rx*r*math.cos(a);y=cy+ry*r*math.sin(a);h=actual_ground(x,y)
            if h is None or nearest(x,y)[0]<7.3:continue
            objs=scenery.make_rock((x,y,h-.18),scale=RNG.uniform(.45,1.05),seed=3210+j+index*100,kind='boulder',palette={'rock':(.44,.49,.58)})
            for ob in objs:ob['moon_bucket']='Rocks'
            track_decor(objs,x,y,'physical wet shoreline rock',h-.18);count+=1
    return len(falls),count

def add_roots():
    for index,(_,center,radii,depth)in enumerate(ISLANDS):
        for j in range(3):
            a=1.1+j*1.8+index;x=center[0]+math.cos(a)*radii[0]*.90;y=center[1]+math.sin(a)*radii[1]*.90;z=terrain_height(x,y)-1.8
            tube('Moon_HangingSilverRoots',[(x+math.sin(t*4+index)*.6,y+math.cos(t*4+j)*.5,z-t*(9+j*2))for t in np.linspace(0,1,18)],.11 if j%2 else.17,'Vines',(.40,.42,.53),7)

def add_mist():
    # Separate irregular puffs, never four overlapping elongated slabs. These
    # low scenic clouds are independent of the runtime hopping cloud steps.
    specs=[(-176,42,-55,14,9,6),(-151,107,-49,9,7,5),(-114,123,-63,15,11,7),(-77,76,-71,12,8,6),(-48,40,-76,18,11,8),(-5,42,-76,13,8,6),(27,20,-66,16,9,7),(66,-30,-57,12,8,6),(111,12,-62,16,10,7),(153,50,-54,11,8,6),(140,107,-59,14,9,6),(107,136,-67,10,7,5),(54,177,-66,16,10,7),(6,218,-72,13,9,6),(-42,225,-73,18,11,8),(-95,195,-60,12,8,6),(-158,166,-57,13,9,6),(-124,-14,-58,10,7,5),(-76,-39,-65,15,10,7),(-21,-41,-62,11,8,6)]
    for index,(cx,cy,cz,sx,sy,sz)in enumerate(specs):
        n=48;nr=20;vs=[(cx,cy,cz+sz)];fs=[];cc=[col((.83,.80,.92,.23+.04*math.sin(index)))]
        for k in range(1,nr):
            p=math.pi*k/nr
            for i in range(n):
                a=math.tau*i/n;f=1+.12*math.sin(a*3+index)*math.sin(p)**2+.06*math.sin(a*7+p*3+index)
                vs.append((cx+sx*math.sin(p)*math.cos(a)*f,cy+sy*math.sin(p)*math.sin(a)*f,cz+sz*math.cos(p)*(1+.10*math.sin(a*3+index)*math.sin(p))))
                cc.append(col((.77+.06*math.cos(p),.73+.07*math.cos(p),.88+.04*math.cos(p),.23+.04*math.sin(index))))
        for i in range(n):fs.append((0,1+i,1+(i+1)%n))
        for k in range(nr-2):
            for i in range(n):j2=(i+1)%n;a=1+k*n+i;b=1+k*n+j2;c=1+(k+1)*n+j2;d=1+(k+1)*n+i;fs.append((a,d,c,b))
        tip=len(vs);vs.append((cx,cy,cz-sz));cc.append(col((.71,.66,.84,.23+.04*math.sin(index))))
        for i in range(n):fs.append((tip,1+(nr-2)*n+(i+1)%n,1+(nr-2)*n+i))
        mesh('Moon_DetachedLowCloudPuff_'+str(index),vs,fs,'Mist',cc,soft=True)
    (HERE/'separated-mist-clouds.json').write_text(json.dumps(dict(style='Separated varied 3D irregular puffs; no joined strip',puffs=[dict(center=[x,y,z],radii=[rx,ry,rz])for x,y,z,rx,ry,rz in specs]),indent=2),encoding='utf8')
    return len(specs)

def source_audits(objects):
    # Complete authored geometry is audited before any spatial split.
    out=dict(terrain_triangles=0,water_triangles=0,upward_errors=0,above_flight_terrain=0,material_names=['RA_Moon_'+c for c in CATEGORIES])
    for ob in objects:
        if ob.type!='MESH'or ob.get('moon_bucket')not in('Terrain','Water'):continue
        cat=ob.get('moon_bucket');out['terrain_triangles'if cat=='Terrain'else'water_triangles']+=sum(len(p.vertices)-2 for p in ob.data.polygons)
        for poly in ob.data.polygons:
            if poly.normal.z<-.0001:out['upward_errors']+=1
        if cat=='Terrain':
            for v in ob.data.vertices:
                p=ob.matrix_world@v.co;d,s=nearest(p.x,p.y)
                if d<8.2 and p.z>play(s)-2.2:out['above_flight_terrain']+=1
    return out

def add_tile_guide():
    # Proof-only construction; never present in the FBX environment export.
    for s in np.arange(0,LENGTH,3.25):
        p,n=route(s);p.z=play(s)
        bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.65,depth=.24,location=p-Vector((0,0,.12)))
        ob=bpy.context.object;ob.name='PROOF_ONLY_RuntimeTile';ob.data.materials.append(material('Metal'))
        for v in ob.data.vertices:v.co.z*=1
        bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.60,depth=.21,location=p-Vector((0,0,.055)))
        ob=bpy.context.object;ob.name='PROOF_ONLY_TileMint';m=bpy.data.materials.get('ProofMint')
        if not m:
            m=bpy.data.materials.new('ProofMint');m.use_nodes=True;ex.node_bsdf(m).inputs['Base Color'].default_value=col((.32,.85,.78));ex.node_bsdf(m).inputs['Roughness'].default_value=.31
        ob.data.materials.append(m)

def render_proof():
    scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Moon lavender daylight');scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.61,.59,.74,1);bg.inputs['Strength'].default_value=.60
    ld=bpy.data.lights.new('Moon warm readable sun','SUN');ld.energy=3.1;ld.angle=.14
    ob=bpy.data.objects.new(ld.name,ld);scene.collection.objects.link(ob);ob.rotation_euler=(.42,-.42,-.65)
    ld=bpy.data.lights.new('Moon broad cool sky fill','AREA');ld.energy=45000;ld.shape='DISK';ld.size=200
    ob=bpy.data.objects.new(ld.name,ld);scene.collection.objects.link(ob);ob.location=(-80,80,130)
    scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
        for d in prefs.devices:d.use=d.type=='CUDA'
        scene.cycles.device='GPU'
    except:pass
    scene.render.resolution_x=1800;scene.render.resolution_y=1250;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
    add_tile_guide()
    for label,target,offset,scale in [('overview',(-20,87,-7),(250,-320,260),365),('lagoon',(53,64,-3),(78,-96,83),165),('ring_observatory',(-24,166,29),(55,-72,43),88),('hopping_islets',(-101,86,-4),(63,-82,67),129),('top',(-20,100,0),(0,0,400),335),('radio_cottage',(-16,27,4),(23,-31,18),57),('upper_spring',(15,174,9),(52,-49,32),86)]:
        data=bpy.data.cameras.new('Moon '+label);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=Vector(target)+Vector(offset);cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(PROOF/(label+'.png'));bpy.ops.render.render(write_still=True);print('MOON_PROOF',label,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'moon_resonance_preview.blend'))

def build():
    started=time.monotonic();route_files=[OUT/name for name in('moon_resonance_route.json','play_elevation.json','ground_elevation.json')];route_hashes={str(p):sha(p)for p in route_files if p.exists()};write_route(preserve_exports=len(route_hashes)==3)
    frozen={str(p):sha(p)for p in [HERE.parent/'CoralOasisPilot/build_coral_oasis.py',V4/'architecture.py',V4/'scenery.py',V4/'nature_detail.py',REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss/emerald_abyss_island.fbx',REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/CoralOasis/coral_oasis_island.fbx']}
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
    for i,(name,center,radii,depth)in enumerate(ISLANDS):landmass(name,center,radii,depth,1130+i)
    unify_terrain();prepare_cap_index();print('MOON_TERRAIN',flush=True)
    rocks=add_stones();buildings=add_architecture();crystals=add_resonators();trees=add_trees();print('MOON_TREES',trees,flush=True)
    flowers=add_gardens();hopping=add_hopping_gardens();falls,shore=add_water();add_roots();mist=add_mist();decor=audit_ground_decor()
    (HERE/'decor-support-checks.json').write_text(json.dumps(decor,indent=2),encoding='utf8')
    density=runtime_density();print('MOON_DENSITY',density,flush=True)
    originals=list(bpy.context.scene.objects);audit=source_audits(originals)
    (HERE/'source_geometry_audit.json').write_text(json.dumps(audit,indent=2),encoding='utf8');assert not audit['upward_errors'];assert not audit['above_flight_terrain']
    print('MOON_SOURCE',len(originals),audit,flush=True);export=export_scene(originals)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'moon_resonance_preview.blend'))
    report=dict(id='03_moon_resonance',frozen_inputs=frozen,frozen_inputs_unchanged=all(sha(p)==h for p,h in frozen.items()),route_exports_unchanged=all(sha(p)==h for p,h in route_hashes.items()),route_export_hashes=route_hashes,route_length=LENGTH,source_play_height_range=[4,20],terrain_under_raw_profile=2.65,protected_air_corridor_half_width=8.2,architecture=buildings,resonators=crystals,trees=trees,rock_clusters=rocks,flower_clusters=flowers,hopping_islets=hopping,waterfalls=falls,shoreline_stones=shore,mist_banks=mist,decor_support=decor,runtime_density=density,source_geometry_audit=audit,landmasses=[dict(name=a,center=b,radii=c,depth=d)for a,b,c,d in ISLANDS],export=export,elapsed_seconds=time.monotonic()-started)
    assert report['frozen_inputs_unchanged'];assert report['route_exports_unchanged'];(HERE/'moon_resonance_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('MOON_EXPORT_COMPLETE',json.dumps(report),flush=True)
    render_proof()

if __name__=='__main__':
    if '--route-only'in sys.argv:write_route()
    elif '--render-only'in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=str(HERE/'moon_resonance_preview.blend'));render_proof()
    else:build()



