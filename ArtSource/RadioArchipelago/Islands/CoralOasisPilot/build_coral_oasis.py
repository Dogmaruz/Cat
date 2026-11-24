"""Owned second-island scene; all earlier island sources remain read-only.

Run Blender --background --factory-startup --python this_file -- --route-only
or omit --route-only to create the complete scene and FBX export.
"""
import bpy, bmesh, math, json, pathlib, sys, hashlib, random, collections, time, bisect
import numpy as np
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree

HERE=pathlib.Path(__file__).resolve().parent
REPO=HERE.parents[3]
V4=HERE.parent/'V4'
OUT=REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/CoralOasis'
PROOF=HERE/'Proof'
sys.path.insert(0,str(V4));sys.path.insert(0,str(V4/'UnityExport'))
import architecture, scenery, export_emerald as ex

RNG=random.Random(300026)
def sha(p):return hashlib.sha256(pathlib.Path(p).read_bytes()).hexdigest()
def clamp(t):return min(1,max(0,t))
def smooth(t):t=clamp(t);return t*t*t*(10+t*(-15+6*t))
def linear(v):return v/12.92 if v<=.04045 else((v+.055)/1.055)**2.4
def col(rgb):return tuple(linear(float(c))for c in rgb[:3])+(rgb[3]if len(rgb)>3 else 1,)

def make_route():
    controls=np.array([(0,-74),(35,-74),(70,-74),(95,-48),(92,-18),(65,-2),(43,14),(48,45),(60,67),(51,88),(28,100),(-12,97),(-43,85),(-45,52),(-24,31),(-35,1),(-63,-11),(-91,-2),(-125,-24),(-124,-52),(-104,-74),(-74,-74),(-44,-74),(-15,-74)],dtype=float)
    dense=[]
    for i in range(len(controls)):
        p0,p1,p2,p3=[controls[j%len(controls)]for j in(i-1,i,i+1,i+2)]
        for t in np.linspace(0,1,128,endpoint=False):
            dense.append(((1-t)**3*p0+(3*t**3-6*t*t+4)*p1+(-3*t**3+3*t*t+3*t+1)*p2+t**3*p3)/6)
    dense=np.array(dense);d=np.linalg.norm(np.roll(dense,-1,axis=0)-dense,axis=1)
    scale=714.0/sum(d);dense*=scale;d*=scale
    arc=np.r_[0,np.cumsum(d)];stations=np.arange(0,arc[-1],.55)
    points=np.array([[np.interp(s,arc,np.r_[dense[:,j],dense[0,j]])for j in range(2)]for s in stations])
    ds=np.linalg.norm(np.roll(points,-1,axis=0)-points,axis=1);arc2=np.r_[0,np.cumsum(ds)]
    return points,arc2,float(arc2[-1])

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

HEIGHT_KEYS=[(0,4),(65,4),(145,9),(225,12),(290,16.8),(375,16.8),(455,8),(510,6),(595,4),(LENGTH,4)]
def play(s):
    s%=LENGTH;i=min(len(HEIGHT_KEYS)-2,bisect.bisect_right([p[0]for p in HEIGHT_KEYS],s)-1)
    a,ha=HEIGHT_KEYS[i];b,hb=HEIGHT_KEYS[i+1];return ha+(hb-ha)*smooth((s-a)/(b-a))

def write_route():
    OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(parents=True,exist_ok=True)
    pts=[dict(x=float(p[0]),y=0,z=-float(p[1]))for p in POINTS]
    doc=dict(schema='radio_archipelago_unity_route/1',id='02_coral_oasis',closed=True,roadWidth=6,road_width=6,point_count=len(pts),length=LENGTH,coordinate_mapping='Blender(x,y,z) -> aligned Unity(x,z,-y)',points=pts)
    (OUT/'coral_oasis_route.json').write_text(json.dumps(doc,indent=2),encoding='utf8')
    keys=[dict(distance=float(s),height=play(float(s)))for s in np.arange(0,LENGTH,.5)];keys.append(dict(distance=LENGTH,height=keys[0]['height']))
    for name in('play_elevation.json','ground_elevation.json'):(OUT/name).write_text(json.dumps(dict(length=LENGTH,closed=True,keys=keys),indent=2),encoding='utf8')
    radii=[];worst=[]
    for i in range(len(POINTS)):
        a=POINTS[(i-2)%len(POINTS)];b=POINTS[i];c=POINTS[(i+2)%len(POINTS)]
        ab=np.linalg.norm(b-a);bc=np.linalg.norm(c-b);ac=np.linalg.norm(c-a);cross=abs(np.cross(b-a,c-a))
        if cross>1e-7:
            radius=float(ab*bc*ac/(2*cross));radii.append(radius);worst.append(dict(radius=radius,distance=float(ARC[i]),position=list(map(float,b))))
    report=dict(length=LENGTH,points=len(pts),min_planar_radius=min(radii),height_min=min(k['height']for k in keys),height_max=max(k['height']for k in keys),max_sample_grade=max(abs((keys[i+1]['height']-keys[i]['height'])/(keys[i+1]['distance']-keys[i]['distance']))for i in range(len(keys)-1)),stations=[dict(distance=s,position=list(route(s)[0]),height=play(s))for s in range(0,714,25)])
    report['tightest_bends']=sorted(worst,key=lambda x:x['radius'])[:10]
    (HERE/'route_plan.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('CORAL_ROUTE',json.dumps(report),flush=True)

ISLANDS=[
    ('Oasis garden',(28,-47),(53,35),46),
    ('Relay villa',(95,-28),(40,34),53),
    ('Ravine lips',(35,7),(22,30),48),
    ('High oasis',(57,57),(33,28),59),
    ('Signal sanctuary',(2,92),(52,25),64),
    ('West terraced mesa',(-43,50),(24,32),54),
    ('Palm village',(-111,-27),(31,35),47),
    ('Final garden',(-47,-77),(45,19),42),
    ('Canyon shard',(-65,0),(20,13),42),
    ('Spring ledge',(67,-7),(12,10),29),
    ('Distant east garden',(151,32),(17,14),47),
    ('Distant west needle',(-151,53),(12,11),52),
]
ROCKS=[(.62,.28,.16),(.76,.38,.23),(.88,.49,.30),(.69,.31,.19),(.82,.43,.27),(.93,.58,.36)]
CATEGORIES=['Terrain','Cliffs','Rocks','Foliage','Grass','Wood','Architecture','Metal','Water','Waterfall','Foam','Vines']
OWN_MATERIALS={}
CAP_BVH=None
GROUND_DECOR=[]

def prepare_cap_index():
    global CAP_BVH
    vertices=[];faces=[]
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH'or obj.get('coral_bucket')!='Terrain':continue
        offset=len(vertices);vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
        faces.extend(tuple(offset+i for i in poly.vertices)for poly in obj.data.polygons)
    CAP_BVH=BVHTree.FromPolygons(vertices,faces,all_triangles=True)

def actual_ground(x,y):
    if CAP_BVH is None:raise RuntimeError('Actual terrain index must precede decoration.')
    point,normal,face,distance=CAP_BVH.ray_cast(Vector((x,y,1000)),Vector((0,0,-1)),2000)
    return point.z if point is not None else None

def track_decor(objects,x,y,label,placed_ground=None):
    GROUND_DECOR.append((list(objects),x,y,label,terrain_height(x,y)if placed_ground is None else placed_ground))

def audit_ground_decor():
    report=dict(checked_groups=0,removed_groups=0,removed_objects=0,relocated_groups=0,max_vertical_correction=0,removed=[],actual_top_method='BVH over actual triangulated Terrain caps, never nominal footprint/height')
    for objects,x,y,label,nominal in GROUND_DECOR:
        report['checked_groups']+=1;height=actual_ground(x,y)
        if height is None:
            report['removed_groups']+=1;report['removed'].append(dict(kind=label,x=x,y=y,reason='No actual Terrain top beneath root'))
            for obj in objects:
                if obj.name in bpy.data.objects:bpy.data.objects.remove(obj,do_unlink=True);report['removed_objects']+=1
            continue
        delta=height-nominal;report['max_vertical_correction']=max(report['max_vertical_correction'],abs(delta))
        if abs(delta)>.001:
            report['relocated_groups']+=1
            for obj in objects:
                if obj.name in bpy.data.objects:obj.location.z+=delta
    return report
def material(category):
    if category not in OWN_MATERIALS:
        m=bpy.data.materials.new('RA_Coral_'+category);m.use_nodes=True;m.diffuse_color=(1,1,1,1)
        bs=ex.node_bsdf(m);bs.inputs['Roughness'].default_value=.25 if category in('Water','Waterfall','Foam')else .36 if category=='Metal'else .78
        bs.inputs['Metallic'].default_value=.65 if category=='Metal'else 0
        attr=m.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
        OWN_MATERIALS[category]=m
    return OWN_MATERIALS[category]

def mesh(name,verts,faces,category,colors,uvs=None,soft=False):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();data.materials.append(material(category))
    rgba=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    if len(colors)==4 and isinstance(colors[0],(int,float)):colors=[colors]*len(verts)
    rgba.data.foreach_set('color',[v for loop in data.loops for v in colors[loop.vertex_index]])
    uv=data.uv_layers.new(name='UVMap')
    uv.data.foreach_set('uv',[v for loop in data.loops for v in(uvs[loop.vertex_index]if uvs else(verts[loop.vertex_index][0],verts[loop.vertex_index][1]))])
    for poly in data.polygons:poly.use_smooth=soft
    obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj);obj['coral_bucket']=category
    return obj

def terrain_height(x,y):
    distance,s=nearest(x,y);height=play(s)-2.4
    # Low rolling sand relief is suppressed throughout the protected flight corridor.
    height+=(1-smooth((9-distance)/5))*(.28*math.sin(x*.17+y*.11)+.19*math.cos(y*.23-x*.09))
    # A coherent basin: the water is lower than its actual modeled shoreline.
    pond=((x-29)/29)**2+((y+44)/17)**2
    if pond<1:height-=2.3*(1-smooth((math.sqrt(pond)-.72)/.28))
    if -75<y<-57 and abs(x-44)<3.3:
        cut=(1-smooth((abs(x-44)-1.5)/1.8))*smooth((-57-y)/3)
        height=height*(1-cut)-.12*cut
    spring=((x-67)/7.8)**2+((y+7)/6.0)**2
    if spring<1.4:height=6.2+.9*smooth((math.sqrt(spring)-.60)/.58)
    if -15<y<-10 and abs(x-68)<1.8:height=min(height,6.2)
    return height

def footprint(x,y,pad=0):
    return any(((x-c[0])/(r[0]-pad))**2+((y-c[1])/(r[1]-pad))**2 < .90**2 for _,c,r,_ in ISLANDS if min(r)>pad)

def landmass(name,center,radii,depth,seed):
    rng=random.Random(seed);cx,cy=center;rx,ry=radii;n=128;nr=24
    def rim(a):return 1+.045*math.sin(a*5+seed)+.032*math.cos(a*9+seed*.9)+.018*math.sin(a*17+seed*.3)
    vs=[(cx,cy,terrain_height(cx,cy))];colors=[col((.89,.65,.41))];fs=[]
    for ring in range(1,nr+1):
        t=ring/nr
        for i in range(n):
            a=math.tau*i/n;f=rim(a);x=cx+math.cos(a)*rx*t*f;y=cy+math.sin(a)*ry*t*f;z=terrain_height(x,y)
            vs.append((x,y,z));variation=.5+.28*math.sin(x*.12+y*.13)+.17*math.sin(x*.77-y*.55)
            rgb=tuple(a*(1-variation)+b*variation for a,b in zip((.79,.51,.31),(.96,.76,.49)))
            pond_distance=((x-29)/29)**2+((y+44)/17)**2
            if .54<pond_distance<1.12:
                wet=.60*(1-abs(pond_distance-.82)/.33)
                wet=clamp(wet);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.28,.43,.31)))
            spring_distance=((x-67)/7.8)**2+((y+7)/6.0)**2
            if .55<spring_distance<1.6:
                wet=.64*clamp(1-abs(spring_distance-.98)/.62)
                rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.31,.43,.28)))
            colors.append(col(rgb))
    for i in range(n):fs.append((0,1+i,1+(i+1)%n))
    for ring in range(nr-1):
        a0=1+ring*n;b0=a0+n
        for i in range(n):j=(i+1)%n;fs.extend(((a0+i,b0+i,b0+j),(a0+i,b0+j,a0+j)))
    top=mesh('Coral_Terrain_'+name,vs,fs,'Terrain',colors,soft=True)
    # Closed stratified rock volume shares its top boundary with the terrain cap.
    verts=[];cc=[];faces=[]
    levels=[(0,1),(-.75,1.015),(-1.6,.99),(-2.1,1.025),(-4.4,1.01),(-5.0,.965),(-7.4,.97),(-8.2,.99),(-11.9,.89),(-12.5,.915),(-16.7,.82),(-17.5,.84),(-22.5,.72),(-23.2,.74),(-29,.58),(-30,.60),(-depth*.82,.37),(-depth,.055)]
    for ring,(drop,scale) in enumerate(levels):
        for i in range(n):
            a=math.tau*i/n;f=rim(a)*(1+.015*math.sin(i*1.9+ring*2));x=cx+math.cos(a)*rx*f*scale+.8*math.sin(ring*.8);y=cy+math.sin(a)*ry*f*scale+.65*math.cos(ring*.9)
            z=terrain_height(cx+math.cos(a)*rx*rim(a),cy+math.sin(a)*ry*rim(a))+drop
            if ring>1:z+=.28*math.sin(i*.65+ring*.4)+.20*math.sin(i*1.9+ring*.7)
            if ring==0:x=cx+math.cos(a)*rx*rim(a);y=cy+math.sin(a)*ry*rim(a)
            verts.append((x,y,z));rgb=ROCKS[(ring//2+i//9+seed)%len(ROCKS)];shade=.92+.08*math.sin(i*2.07+ring)
            cc.append(col(tuple(c*shade for c in rgb)))
    for ring in range(len(levels)-1):
        for i in range(n):j=(i+1)%n;a=ring*n+i;b=ring*n+j;c=(ring+1)*n+j;d=(ring+1)*n+i;faces.extend(((a,d,c),(a,c,b)))
    # The exactly matching Terrain cap above closes this border. A single
    # nonplanar n-gon here would bridge over its sculpted basin and bury props.
    tip=len(verts);verts.append((cx+1.4,cy-.7,terrain_height(cx,cy)-depth-2.6));cc.append(col((.48,.25,.18)))
    for i in range(n):j=(i+1)%n;faces.append((tip,(len(levels)-1)*n+j,(len(levels)-1)*n+i))
    body=mesh('Coral_Cliffs_'+name,verts,faces,'Cliffs',cc,soft=False)
    return top,body

def tube(name,points,radius,category,rgb,n=8):
    points=[Vector(p)for p in points];vs=[];fs=[]
    for i,p in enumerate(points):
        direction=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();side=direction.cross(Vector((0,0,1)))
        if side.length<.001:side=Vector((1,0,0))
        side.normalize();up=direction.cross(side).normalized()
        for j in range(n):vs.append(tuple(p+radius*(side*math.cos(j*math.tau/n)+up*math.sin(j*math.tau/n))))
    fs.append(tuple(reversed(range(n))))
    for i in range(len(points)-1):
        for j in range(n):k=(j+1)%n;fs.append((i*n+j,i*n+k,(i+1)*n+k,(i+1)*n+j))
    fs.append(tuple((len(points)-1)*n+j for j in range(n)))
    return mesh(name,vs,fs,category,col(rgb),soft=True)

def add_geology():
    # Sculptural fins stay beyond the full steering/jump and camera corridor.
    for i,(x,y,scale) in enumerate([(-17,36,1.55),(18,24,1.8),(22,36,1.45),(29,47,1.65),(-61,74,1.25),(82,63,1.4),(-123,-10,1.2),(66,-53,.85)]):
        if nearest(x,y)[0]<10:continue
        z=terrain_height(x,y)-.3
        parts=scenery.make_rock((x,y,z),scale=scale*2.1,seed=5070+i,kind='spire',palette={'rock':(.77,.37,.22)})
        for obj in parts:obj['coral_bucket']='Rocks'
        track_decor(parts,x,y,'decorative rock fin')
    count=0
    for attempt in range(1300):
        x=RNG.uniform(-143,138);y=RNG.uniform(-91,115);distance,s=nearest(x,y)
        if not footprint(x,y,3) or distance<8.7 or ((x-29)/34)**2+((y+44)/21)**2<1:continue
        scale=RNG.uniform(.35,1.25)
        parts=scenery.make_rock((x,y,terrain_height(x,y)-.18),scale=scale,seed=8020+attempt,kind='boulder',palette={'rock':(.79,.43,.27)})
        for obj in parts:obj['coral_bucket']='Rocks'
        track_decor(parts,x,y,'loose boulder cluster')
        count+=1
        if count>=105:break
    return count

def add_architecture():
    sites=[('adobe_house',(-6,-39),1.1,.15),('adobe_house',(118,-18),1.25,-.9),('adobe_tower',(1,73),1.25,0)]
    report=[]
    for index,(kind,(x,y),scale,yaw)in enumerate(sites):
        z=terrain_height(x,y)-.05
        objs=architecture.build_building(kind,location=(x,y,z),yaw=yaw,scale=scale,seed=1260+index)
        for obj in objs:obj['coral_structure']=kind
        report.append(dict(kind=kind,position=[x,y,z],scale=scale,distance_to_route=nearest(x,y)[0],objects=len(objs)))
    # An unmistakable large copper receiver and spoke hub above the watchtower.
    x,y=1,73;base=terrain_height(x,y)+12.2
    for side,radius in enumerate((3.7,3.45)):
        pts=[(x+radius*math.cos(a),y+side*.16,base+radius*math.sin(a))for a in np.linspace(0,math.tau,97)]
        tube('Coral_RadioReceiverRing_'+str(side),pts,.13,'Metal',(.82,.57,.21),10)
    for angle in np.linspace(0,math.tau,8,endpoint=False):
        tube('Coral_RadioReceiverSpoke',[(x,y,base),(x+3.5*math.cos(angle),y,base+3.5*math.sin(angle))],.075,'Metal',(.66,.44,.17),8)
    tube('Coral_RadioReceiverMast',[(x,y,base-4.4),(x,y,base+4.1)],.13,'Metal',(.71,.49,.23),10)
    return report

def add_palms():
    count=0
    for attempt in range(2200):
        x=RNG.uniform(-140,135);y=RNG.uniform(-90,112);scale=RNG.uniform(1.3,2.15);d,s=nearest(x,y)
        if not footprint(x,y,5) or d<8+2.95*scale:continue
        if ((x-29)/34)**2+((y+44)/21)**2<1:continue
        if any(math.hypot(x-cx,y-cy)<(9 if 'tower'in kind else 10)for kind,(cx,cy),_,_ in [('tower',(1,73),1,0),('house',(-6,-39),1,0),('house',(118,-18),1,0)]):continue
        objs=scenery.make_tree((x,y,terrain_height(x,y)-.08),scale=scale,seed=9330+attempt,kind='palm',palette={'foliage':(.12,.41,.29),'bark':(.43,.28,.18)})
        for obj in objs:obj['coral_plant']=True
        track_decor(objs,x,y,'palm')
        count+=1
        if count>=74:break
    return count

def flower_cluster(name,x,y,z,scale,seed):
    rng=random.Random(seed);vs=[];fs=[];colors=[]
    def face(points,color):
        b=len(vs);vs.extend(points);colors.extend([col(color)]*len(points));fs.append(tuple(range(b,b+len(points))))
    for flower in range(9):
        xx=x+rng.uniform(-1,1)*scale;yy=y+rng.uniform(-1,1)*scale;h=rng.uniform(.25,.55)*scale
        zz=actual_ground(xx,yy)
        if zz is None:continue
        green=(.25,.43,.22);top=(xx,yy,zz+h)
        face([(xx-.025,yy,zz),(xx+.025,yy,zz),(xx+.018,yy,zz+h),(xx-.018,yy,zz+h)],green)
        for j in range(7):
            a=j*math.tau/7;dx,dy=math.cos(a),math.sin(a);r=.17*scale
            petal=(.97,.70,.17)if seed%3==0 else(.94,.82,.65)if seed%3==1 else(.80,.30,.42)
            face([top,(xx+dx*r-dy*r*.4,yy+dy*r+dx*r*.4,zz+h+.025),(xx+dx*r*1.6,yy+dy*r*1.6,zz+h-.04),(xx+dx*r+dy*r*.4,yy+dy*r-dx*r*.4,zz+h+.025)],petal)
        for leaf in range(2):
            a=seed*.3+leaf*3;dx,dy=math.cos(a),math.sin(a)
            face([(xx,yy,zz+h*.5),(xx+dx*.20*scale-dy*.08,yy+dy*.20*scale+dx*.08,zz+h*.4),(xx+dx*.34*scale,yy+dy*.34*scale,zz+h*.65),(xx+dx*.20*scale+dy*.08,yy+dy*.20*scale-dx*.08,zz+h*.4)],green)
    return mesh(name,vs,fs,'Foliage',colors,soft=False)

def add_gardens():
    count=0
    for attempt in range(3000):
        x=RNG.uniform(-142,137);y=RNG.uniform(-92,112);d,s=nearest(x,y)
        if not footprint(x,y,2)or d<6.1 or ((x-29)/33)**2+((y+44)/20)**2<1:continue
        # Flowers/low scrub form larger readable islands, rather than isolated pixels.
        if math.sin(x*.061+y*.042)<-.15:continue
        scale=RNG.uniform(.7,1.35)
        flower_cluster('Coral_WildflowerPatch',x,y,terrain_height(x,y),scale,attempt)
        if attempt%3==0:
            objs=scenery.make_prop('grass_clump',(x+1,y-.6,terrain_height(x+1,y-.6)-.03),scale=.8,seed=attempt)
            for obj in objs:obj['coral_bucket']='Grass'
            track_decor(objs,x+1,y-.6,'grass clump')
        count+=1
        if count>=260:break
    # Occasional broken remnants only; never a continuous ground road.
    for s in(31,78,183,218,336,489,574,624):
        p,n=route(s)
        for j in range(7):
            q=p+n*(RNG.uniform(5.8,7.5)*(1 if j%2 else-1));t=Vector((n.y,-n.x,0));q+=t*RNG.uniform(-2.8,2.8);q.z=terrain_height(q.x,q.y)-.08
            objs=scenery.make_rock(tuple(q),scale=RNG.uniform(.22,.45),seed=int(s*100+j),kind='boulder',palette={'rock':(.75,.52,.35)})
            for obj in objs:obj['coral_bucket']='Rocks'
            track_decor(objs,q.x,q.y,'broken paving remnant')
    return count

def add_water():
    # A gently tessellated turquoise bowl surface with actual modeled sandy shores.
    verts=[(29,-44,.45)];uv=[(.5,.5)];colors=[col((.08,.66,.70))];faces=[];n=96;nr=9
    for ring in range(1,nr+1):
        t=ring/nr
        for j in range(n):
            a=j*math.tau/n;w=1+.025*math.sin(a*7);x=29+28*math.cos(a)*t*w;y=-44+16*math.sin(a)*t*w;z=.45+.025*math.sin(x*.8+y*.9)*t
            verts.append((x,y,z));uv.append((x*.12,y*.12));colors.append(col(tuple(v*(1-t*.12)for v in(.12,.77,.78))))
    for j in range(n):faces.append((0,1+j,1+(j+1)%n))
    for r in range(nr-1):
        a=1+r*n;b=a+n
        for j in range(n):k=(j+1)%n;faces.append((a+j,b+j,b+k,a+k))
    mesh('Coral_Oasis_TurquoiseWater',verts,faces,'Water',colors,uv,True)
    # Open spring feeding the high fall and the low bowl's visible outlet channel.
    for name,cx,cy,rx,ry,z in [('HighSpring',67,-7,7.8,6.0,6.72)]:
        vv=[(cx,cy,z)]+[(cx+rx*math.cos(a),cy+ry*math.sin(a),z)for a in np.linspace(0,math.tau,65)[:-1]]
        ff=[(0,1+i,1+(i+1)%64)for i in range(64)]
        mesh('Coral_'+name,vv,ff,'Water',col((.10,.74,.77)),[(v[0]*.12,v[1]*.12)for v in vv],True)
    vv=[];ff=[];uv=[]
    for row,y in enumerate(np.linspace(-57,-74,25)):
        for sign in(-1,1):vv.append((44+sign*1.65,y,.45));uv.append(((sign+1)/2,row*.15))
    for row in range(24):a=row*2;ff.append((a,a+2,a+3,a+1))
    mesh('Coral_Oasis_OpenOutlet',vv,ff,'Water',col((.12,.75,.77)),uv,True)
    # Foam/wavelets are thin raised ribbons whose UVs scroll independently in Unity.
    for wave in range(12):
        a=wave*2.39996;r=.24+.045*wave
        pts=[]
        for f in np.linspace(-.35,.35,21):
            aa=a+f;pts.append((29+26*r*math.cos(aa),-44+15*r*math.sin(aa),.49))
        tube('Coral_Oasis_Wavelet',pts,.04,'Foam',(.55,.91,.86),5)
    waterfalls=[[(67,-12,6.7),(69,-13,6.5),(70,-14,5.7),(70,-15,2),(70,-15,-6),(69,-15,-18),(70,-16,-34)],[(41,-72,.45),(43,-73,.32),(44,-74,-1.0),(44,-74,-7),(45,-74,-18),(45,-75,-31)]]
    for index,points in enumerate(waterfalls):
        verts=[];uv=[];colors=[];fs=[];width=2.3 if index==0 else 3.4;n=14
        for row,p in enumerate(points):
            for j in range(n+1):
                t=j/n;x=p[0]+(t-.5)*width;y=p[1]+.08*math.sin(t*26+row);z=p[2]+.06*math.sin(t*23+row*.6)
                verts.append((x,y,z));uv.append((t,row*.78));rgb=tuple(a*(1-abs(t-.5))+b*abs(t-.5)for a,b in zip((.11,.68,.73),(.65,.91,.86)));colors.append(col(rgb))
        for row in range(len(points)-1):
            for j in range(n):a=row*(n+1)+j;b=a+n+1;fs.append((a,b,b+1,a+1))
        mesh('Coral_LivingWaterfall_'+str(index),verts,fs,'Waterfall',colors,uv,True)
        for j in range(4):
            pts=[(p[0]+(j-1.5)*width/5,p[1]-.045,p[2]+.045)for p in points]
            tube('Coral_FallFoam_'+str(index),pts,.035 if j%2 else .065,'Foam',(.69,.94,.90),5)
    return len(waterfalls)

def add_layered_buttes():
    specs=[(19,30,7.3,9.2,25),(27,42,6.1,7.4,30),(78,51,7.4,8.8,23),(-8,70,7.8,7.0,17),(-61,43,6.3,8.0,24),(-94,-29,6.2,7.0,20),(-12,-50,5.0,5.8,13)]
    report=[]
    for index,(cx,cy,rx,ry,height)in enumerate(specs):
        d,s=nearest(cx,cy)
        if d<max(rx,ry)+9.5:continue
        base=terrain_height(cx,cy)-.4;n=44;vs=[];fs=[];colors=[]
        rings=[(0,1.10),(.04,1.11),(.10,.99),(.15,1.02),(.21,.93),(.30,.95),(.34,.99),(.42,.87),(.47,.91),(.56,.77),(.61,.82),(.70,.68),(.75,.72),(.84,.57),(.88,.61),(.94,.48),(1,.43)]
        for ring,(h,f)in enumerate(rings):
            for j in range(n):
                a=j*math.tau/n;w=1+.075*math.sin(a*5+index)+.035*math.sin(a*9+index*.7)
                x=cx+rx*f*math.cos(a)*w+.5*math.sin(h*2);y=cy+ry*f*math.sin(a)*w+.4*math.cos(h*3);z=base+h*height+.17*math.sin(j*.77+index)*(h if ring else 0)
                vs.append((x,y,z));rgb=ROCKS[(ring//2+j//13+index)%len(ROCKS)];colors.append(col(rgb))
        for ring in range(len(rings)-1):
            for j in range(n):k=(j+1)%n;fs.append((ring*n+j,ring*n+k,(ring+1)*n+k,(ring+1)*n+j))
        fs.append(tuple(reversed(range(n))));fs.append(tuple((len(rings)-1)*n+j for j in range(n)))
        mesh('Coral_LayeredSandstoneButte_'+str(index),vs,fs,'Rocks',colors,soft=False)
        report.append(dict(center=[cx,cy,base],height=height,radii=[rx,ry],route_clearance=d-max(rx,ry)))
    # Grouped shoreline rocks break the turquoise basin's edge into real geology.
    for j in range(43):
        a=j*math.tau/43+.035*math.sin(j*3);lo,hi=.55,1.05
        for iteration in range(20):
            radius=(lo+hi)*.5;x=29+29*radius*math.cos(a);y=-44+17*radius*math.sin(a)
            if terrain_height(x,y)>.56:hi=radius
            else:lo=radius
        radius=(lo+hi)*.5;x=29+29*radius*math.cos(a);y=-44+17*radius*math.sin(a)
        if nearest(x,y)[0]<7:continue
        z=terrain_height(x,y)-.2
        objects=scenery.make_rock((x,y,z),scale=.55+RNG.random()*.85,seed=1920+j,kind='boulder',palette={'rock':(.79,.45,.29)})
        for obj in objects:obj['coral_bucket']='Rocks'
        track_decor(objects,x,y,'oasis shoreline stone')
    return report

def add_spring_banks():
    rng=random.Random(730039);count=0
    for j in range(23):
        angle=j*math.tau/23+.04*math.sin(j*2.3);lo,hi=.65,1.30
        for iteration in range(18):
            radius=(lo+hi)*.5;x=67+7.8*radius*math.cos(angle);y=-7+6.0*radius*math.sin(angle);height=actual_ground(x,y)
            if height is None or height>6.82:hi=radius
            else:lo=radius
        radius=(lo+hi)*.5;x=67+7.8*radius*math.cos(angle);y=-7+6.0*radius*math.sin(angle);height=actual_ground(x,y)
        if height is None or nearest(x,y)[0]<7:continue
        objects=scenery.make_rock((x,y,height-.20),scale=rng.uniform(.38,.76),seed=7300+j,kind='boulder',palette={'rock':(.71,.45,.28)})
        for obj in objects:obj['coral_bucket']='Cliffs'
        track_decor(objects,x,y,'structural spring rim stone',height);count+=1
    return count

def add_roots():
    for index,(_,center,radii,depth)in enumerate(ISLANDS):
        for j in range(3):
            a=1.8+j*1.7+index;x=center[0]+math.cos(a)*radii[0]*.85;y=center[1]+math.sin(a)*radii[1]*.85;z=terrain_height(x,y)-2
            points=[(x+math.sin(t*4+index)*.8,y+math.cos(t*4+j)*.7,z-t*(8+j*2))for t in np.linspace(0,1,18)]
            tube('Coral_HangingRoots',points,.12 if j%2 else .18,'Vines',(.26,.36,.19),7)

class ExportBatch:
    def __init__(self,name,category):self.name=name;self.category=category;self.v=[];self.f=[];self.c=[];self.uv=[];self.soft=[]
    def flush(self):
        if not self.f:return None
        data=bpy.data.meshes.new(self.name);data.from_pydata(self.v,[],self.f);data.update();data.materials.append(material(self.category))
        colors=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');colors.data.foreach_set('color',[v for c in self.c for v in c])
        uv=data.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[v for p in self.uv for v in p])
        for poly,soft in zip(data.polygons,self.soft):poly.use_smooth=soft
        ob=bpy.data.objects.new(self.name,data);bpy.context.scene.collection.objects.link(ob);ob['coral_bucket']=self.category
        self.v=[];self.f=[];self.c=[];self.uv=[];self.soft=[]
        return ob

def export_scene(originals):
    root=bpy.data.objects.new('CoralOasisIsland',None);bpy.context.scene.collection.objects.link(root)
    batches={};serial=collections.Counter();outputs=[];counts=collections.Counter()
    deps=bpy.context.evaluated_depsgraph_get();deps.update()
    for number,obj in enumerate(originals):
        if obj.type!='MESH':continue
        data=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)if obj.modifiers else obj.data
        # Orient complete closed SOURCE solids before splitting them into spatial
        # batches. Recalculating open batch fragments can invert arbitrary cells.
        source_bm=bmesh.new();source_bm.from_mesh(data)
        if source_bm.faces and all(edge.is_manifold for edge in source_bm.edges):
            bmesh.ops.recalc_face_normals(source_bm,faces=list(source_bm.faces))
            source_bm.to_mesh(data);data.update()
        source_bm.free()
        world=[obj.matrix_world@v.co for v in data.vertices];uv=data.uv_layers.active.data if data.uv_layers.active else None;color=data.color_attributes.active_color
        remaps={}
        for poly in data.polygons:
            mat=data.materials[poly.material_index]if poly.material_index<len(data.materials)else None
            category=obj.get('coral_bucket')or ex.bucket(obj,mat)
            if category=='Road':continue
            if category not in CATEGORIES:category='Architecture'
            rgba=ex.color_info(mat)[0]
            center=obj.matrix_world@poly.center;key=(category,math.floor(center.x/36),math.floor(center.y/36))
            if key not in batches:serial[key]+=1;batches[key]=ExportBatch(f'RA_Coral_{category}_{key[1]:+03}_{key[2]:+03}_{serial[key]:02}',category)
            batch=batches[key]
            if len(batch.v)+len(poly.vertices)>54000:
                out=batch.flush();out.parent=root;outputs.append(out);serial[key]+=1;batches[key]=batch=ExportBatch(f'RA_Coral_{category}_{key[1]:+03}_{key[2]:+03}_{serial[key]:02}',category);remaps.pop(key,None)
            remap=remaps.setdefault(key,{})
            face=[]
            corners=list(zip(poly.loop_indices,poly.vertices))
            if len(corners)>=3:
                a,b,c=[world[vid]for _,vid in corners[:3]]
                reverse=(b-a).cross(c-a).z<0 if category in('Terrain','Water')else obj.matrix_world.to_3x3().determinant()<0
                if reverse:corners.reverse()
            for loop,vid in corners:
                if vid not in remap:remap[vid]=len(batch.v);batch.v.append(tuple(world[vid]))
                face.append(remap[vid]);batch.c.append(tuple(color.data[loop].color)if color and color.domain=='CORNER'else tuple(color.data[vid].color)if color else tuple(rgba[:3])+(1,));batch.uv.append(tuple(uv[loop].uv)if uv else(world[vid].x,world[vid].y))
            batch.f.append(tuple(face));batch.soft.append(poly.use_smooth);counts[category]+=len(poly.vertices)-2
        if number%250==0:print('CORAL_BATCH',number,len(originals),dict(counts),flush=True)
    for batch in batches.values():
        out=batch.flush()
        if out:out.parent=root;outputs.append(out)
    for name,p in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
        ob=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(ob);ob.parent=root;ob.location=p;outputs.append(ob)
    for obj in originals:
        if obj.name in bpy.data.objects:bpy.data.objects.remove(obj,do_unlink=True)
    for obj in outputs:
        if obj.type!='MESH':continue
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bad=[f for f in bm.faces if f.calc_area()<1e-10]
        if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
        # Keep the authored outward/upward index ordering after spatial splitting.
        # BMesh normal_update computes shading normals without flipping faces.
        bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for obj in outputs:obj.select_set(True)
    path=OUT/'coral_oasis_island.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
    return dict(mesh_batches=len(outputs)-3,triangles=sum(len(o.data.polygons)for o in outputs if o.type=='MESH'),material_triangles=dict(counts),bytes=path.stat().st_size,sha256=sha(path))

def render_proof():
    scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Coral Oasis apricot daylight');scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.57,.73,.91,1);bg.inputs['Strength'].default_value=.60
    ld=bpy.data.lights.new('Coral warm sunlight','SUN');ld.energy=3.4;ld.angle=.11
    ob=bpy.data.objects.new(ld.name,ld);scene.collection.objects.link(ob);ob.rotation_euler=(.47,-.48,-.7)
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
        for d in prefs.devices:d.use=d.type=='CUDA'
        scene.cycles.device='GPU'
    except:pass
    scene.render.resolution_x=1800;scene.render.resolution_y=1250;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
    for label,target,offset,scale in [('overview',(0,5,-4),(235,-280,220),340),('oasis',(35,-38,1),(65,-85,72),125),('radio_tower',(1,79,19),(45,-58,43),85),('top',(0,12,0),(0,0,330),325),('course_oasis',(36,-52,5),(16,-24,13),52),('course_canyon',(39,27,13),(4,-24,14),57)]:
        data=bpy.data.cameras.new('Coral '+label);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=Vector(target)+Vector(offset);cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(PROOF/(label+'.png'));bpy.ops.render.render(write_still=True);print('CORAL_PROOF',label,flush=True)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'coral_oasis_preview.blend'))

def build():
    started=time.monotonic();write_route()
    frozen={str(p):sha(p)for p in [V4/'Output/02_coral_oasis/island.blend',V4/'architecture.py',V4/'scenery.py',REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldAbyss/emerald_abyss_island.fbx']}
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    for i,(name,center,radii,depth)in enumerate(ISLANDS):landmass(name,center,radii,depth,801+i)
    prepare_cap_index()
    print('CORAL_TERRAIN',flush=True);rocks=add_geology();buttes=add_layered_buttes();buildings=add_architecture();palms=add_palms();flowers=add_gardens();falls=add_water();add_roots()
    spring_rim_stones=add_spring_banks();decor_support=audit_ground_decor()
    (HERE/'decor-support-checks.json').write_text(json.dumps(decor_support,indent=2),encoding='utf8')
    originals=list(bpy.context.scene.objects);print('CORAL_SOURCE',len(originals),flush=True)
    export=export_scene(originals)
    bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'coral_oasis_preview.blend'))
    report=dict(id='02_coral_oasis',frozen_inputs=frozen,frozen_inputs_unchanged=all(sha(p)==h for p,h in frozen.items()),route_length=LENGTH,source_play_height_range=[4,16.8],source_max_grade=.207,terrain_under_raw_profile=2.4,protected_air_corridor_half_width=6.1,architecture=buildings,buttes=buttes,palms=palms,rock_clusters=rocks,flower_clusters=flowers,waterfalls=falls,spring_rim_stones=spring_rim_stones,decor_support=decor_support,landmasses=[dict(name=a,center=b,radii=c,depth=d)for a,b,c,d in ISLANDS],export=export,elapsed_seconds=time.monotonic()-started)
    assert report['frozen_inputs_unchanged'];(HERE/'coral_oasis_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('CORAL_EXPORT_COMPLETE',json.dumps(report),flush=True)
    render_proof()

if __name__=='__main__':
    if '--route-only'in sys.argv:write_route()
    elif '--render-only'in sys.argv:
        bpy.ops.wm.open_mainfile(filepath=str(HERE/'coral_oasis_preview.blend'));render_proof()
    else:build()
