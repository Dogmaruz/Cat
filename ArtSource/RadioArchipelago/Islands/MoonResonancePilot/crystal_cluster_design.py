"""Hexagonal amethyst druses, deterministic geometry independent of world RNG."""
import math, random
from mathutils import Vector

def append_crystal(vertices, faces, colors, origin, height, radius, lean, azimuth, rng, linear):
    axis=Vector((math.sin(lean)*math.cos(azimuth),math.sin(lean)*math.sin(azimuth),math.cos(lean)))
    u=Vector((0,0,1)).cross(axis)
    if u.length<.01:u=Vector((1,0,0))
    u.normalize();v=axis.cross(u).normalized();phase=rng.uniform(0,math.tau)
    # Six true planar prism facets, a wide embedded foot and tapered shoulders.
    # Stocky irregular proportions and fanning silhouettes replace long poles.
    shoulder=rng.uniform(.60,.71);tip_shift=rng.uniform(-.10,.10)*radius
    base=len(vertices)
    for z,r in [(-.04,1.07),(.10,1.02),(shoulder,.78)]:
        for k in range(6):
            angle=phase+k*math.tau/6
            vertices.append(tuple(origin+axis*(height*z)+(u*math.cos(angle)+v*math.sin(angle))*radius*r))
    vertices.append(tuple(origin+axis*height+u*tip_shift));tip=base+18
    palette=[(.56,.42,.80),(.77,.66,.94),(.90,.83,.99),(.64,.57,.87),(.80,.72,.96),(.49,.43,.72)]
    tint=rng.uniform(.93,1.06)
    def face(ids,side,top=False):
        faces.append(tuple(ids));rgb=palette[side%6]
        if top:rgb=tuple(c*.75+.25 for c in rgb)
        colors.extend([tuple(linear(min(.99,c*tint))for c in rgb)+(1,)]*len(ids))
    face([base+k for k in reversed(range(6))],5)
    for ring in range(2):
        for k in range(6):
            j=(k+1)%6;face([base+ring*6+k,base+ring*6+j,base+(ring+1)*6+j,base+(ring+1)*6+k],k)
    for k in range(6):face([base+12+k,base+12+(k+1)%6,tip],k,True)

def cluster(api,x,y,height,count,seed,ground=None):
    ground=ground or api.actual_ground
    rng=random.Random(seed);z=ground(x,y)
    if z is None:return None
    vertices=[];faces=[];colors=[];shards=[]
    station=api.nearest(x,y)[1];_,normal=api.route(station)
    fan_phase=math.atan2(normal.y,normal.x)
    # All stems overlap a single central mineral root, not parallel separate rows.
    for j in range(count):
        a=rng.uniform(0,math.tau)if j==0 else fan_phase+math.tau*(j-1)/max(1,count-1)+rng.uniform(-.18,.18)
        h=height if j==0 else height*rng.uniform(.58,.88)
        radius=h/rng.uniform(3.7,5.2)
        lean=rng.uniform(.02,.12)if j==0 else rng.uniform(.55,.88)
        offset=0 if j==0 else height*.20*rng.uniform(.72,1.02)
        xx=x+math.cos(a)*offset;yy=y+math.sin(a)*offset;zz=ground(xx,yy)
        if zz is None:return None
        origin=Vector((xx,yy,zz-.23))
        append_crystal(vertices,faces,colors,origin,h,radius,lean,a,rng,api.linear)
        shards.append(dict(height=h,radius=radius,lean_degrees=math.degrees(lean),root=list(origin)))
    # Compact, irregular mineral matrix makes the overlapping roots read as one druse.
    core=len(vertices);radius=height*.24
    for ring,(height_offset,scale)in enumerate([(-.45,.78),(.12,1),(.38,.55)]):
        for k in range(12):
            a=k*math.tau/12;rr=radius*scale*(1+.12*math.sin(k*2.3+seed))
            vertices.append((x+math.cos(a)*rr,y+math.sin(a)*rr,z+height_offset))
    def matrix_face(ids,k):
        faces.append(tuple(ids));tone=(.38,.32,.51)if k%2 else(.49,.41,.63)
        colors.extend([tuple(api.linear(c)for c in tone)+(1,)]*len(ids))
    matrix_face([core+k for k in reversed(range(12))],1)
    for ring in range(2):
        for k in range(12):j=(k+1)%12;matrix_face([core+ring*12+k,core+ring*12+j,core+(ring+1)*12+j,core+(ring+1)*12+k],k)
    matrix_face([core+24+k for k in range(12)],0)
    clearance=min(api.nearest(p[0],p[1])[0]for p in vertices)
    if clearance<8.55:return None
    # Sample actual facet edges too, to guard against cutting a winding air corridor.
    edges={tuple(sorted((face[k],face[(k+1)%len(face)])))for face in faces for k in range(len(face))}
    for a,b in edges:
        pa,pb=Vector(vertices[a]),Vector(vertices[b]);steps=max(1,math.ceil((pb-pa).length/.6))
        for k in range(1,steps):
            p=pa.lerp(pb,k/steps)
            if api.nearest(p.x,p.y)[0]<8.5:return None
    supports=[ground(x+math.cos(a)*radius*.7,y+math.sin(a)*radius*.7)for a in[0,1.57,3.14,4.71]]
    if any(p is None for p in supports)or max(supports)-min(supports)>1.5:return None
    return dict(vertices=vertices,faces=faces,colors=colors,position=[x,y,z],count=count,shards=shards,minimum_route_distance=clearance)

def design(api):
    # Existing landmark bedrock rises well above the Terrain cap. New, shorter
    # druses grow from its real top rather than becoming buried inside that rock.
    import bpy
    from mathutils.bvhtree import BVHTree
    vertices=[];faces=[]
    for ob in bpy.context.scene.objects:
        if ob.type!='MESH'or ob.get('moon_bucket')not in('Terrain','Rocks'):continue
        start=len(vertices);vertices.extend(ob.matrix_world@v.co for v in ob.data.vertices)
        faces.extend(tuple(start+i for i in p.vertices)for p in ob.data.polygons)
    solid=BVHTree.FromPolygons(vertices,faces,all_triangles=False)
    def landmark_ground(x,y):
        hit,_,_,_=solid.ray_cast(Vector((x,y,1000)),Vector((0,0,-1)),2000)
        return hit.z if hit is not None else None
    result=[]
    landmarks=[(-26,107,11.8,7),(-20,116,8.2,5),(-34,99,6.8,4),(128,75,9.2,6),(-108,26,7.0,5)]
    for i,(x,y,h,n)in enumerate(landmarks):
        group=cluster(api,x,y,h,n,4001+i,landmark_ground)
        if group:group.update(kind='landmark');result.append(group)
    for i,s in enumerate(range(22,737,29)):
        if 440<s<550:continue
        p,n=api.route(s);found=None
        count=2+i%6;height=4.2+(i%5)*.55
        for side in((-1 if i%2 else 1),(1 if i%2 else -1)):
            for offset in(13.5,16,19,22):
                q=p+n*(side*offset)
                if api.lagoon_r(q.x,q.y)<1.07:continue
                if any(math.hypot(q.x-x,q.y-y)<r for x,y,r in[(-16,27,17),(80,35,17),(-24,165,27)]):continue
                if any(math.hypot(q.x-g['position'][0],q.y-g['position'][1])<12 for g in result):continue
                group=cluster(api,q.x,q.y,height,count,5000+i)
                if group:
                    group.update(kind='route-side',station=s,lateral=side*offset);found=group;break
            if found:break
        if found:result.append(found)
    return result

def build_sources(api):
    groups=design(api);report=[]
    for i,g in enumerate(groups):
        ob=api.mesh('Moon_HexagonalAmethystDruse_'+str(i),g['vertices'],g['faces'],'Crystal',[(1,1,1,1)]*len(g['vertices']))
        ob.data.color_attributes.active_color.data.foreach_set('color',[v for c in g['colors']for v in c])
        report.append({k:v for k,v in g.items()if k not in('vertices','faces','colors')})
    return report
