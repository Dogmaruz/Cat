"""Independent native cobblestone overlay for the exact closed emerald road.

No canonical scene or previous FBX is edited. Every paving stone is a closed,
rounded 8-10-sided solid with four bevel rings, a domed cap and opaque linear
vertex colors. It follows the existing six-meter road, including the bridge.
"""
import bpy,math,json,pathlib,random,bisect,hashlib,collections,time,sys
import numpy as np
from mathutils import Vector

ROOT=pathlib.Path(__file__).resolve().parent
OUT=ROOT/'Output'
SOURCE=ROOT.parent/'Output/01_emerald_ravine/manifest.json'
CELL=24.0
SEED=463117

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def linear(value):return value/12.92 if value<=.04045 else ((value+.055)/1.055)**2.4

class ClosedRoad:
    def __init__(self,points):
        self.points=[Vector(p) for p in points];self.arc=[0.]
        for a,b in zip(self.points,self.points[1:]+self.points[:1]):self.arc.append(self.arc[-1]+(b-a).length)
        self.length=self.arc[-1]
        self.normals=[]
        for i,p in enumerate(self.points):
            tangent=self.points[(i+1)%len(points)]-self.points[(i-1)%len(points)]
            tangent.normalize();self.normals.append(Vector((-tangent.y,tangent.x,0)))
    def sample(self,s):
        s=s%self.length;i=min(len(self.points)-1,bisect.bisect_right(self.arc,s)-1)
        t=(s-self.arc[i])/(self.arc[i+1]-self.arc[i]);j=(i+1)%len(self.points)
        return self.points[i].lerp(self.points[j],t),self.normals[i].lerp(self.normals[j],t).normalized()

class StoneBatch:
    def __init__(self,key):self.key=key;self.vertices=[];self.faces=[];self.colors=[];self.stones=0
    def add(self,vertices,faces,color,gains):
        base=len(self.vertices);self.vertices.extend(vertices)
        for face,gain in zip(faces,gains):
            self.faces.append(tuple(base+i for i in face))
            self.colors.extend([tuple(min(1,max(0,c*gain)) for c in color)+(1,)]*3)
        self.stones+=1
    def build(self,col,material):
        name=f'RA_Cobbles_{self.key[0]:+03d}_{self.key[1]:+03d}'
        mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.vertices,[],self.faces);mesh.update()
        mesh.materials.append(material)
        colors=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
        values=np.asarray(self.colors,dtype=np.float32).reshape(-1);colors.data.foreach_set('color',values)
        for poly in mesh.polygons:poly.use_smooth=True
        obj=bpy.data.objects.new(name,mesh);col.objects.link(obj);obj['surface_role']='native_cobblestone_overlay';obj['stone_count']=self.stones
        return obj

def stone(road,s,offset,rng):
    sides=rng.choice((8,9,10));half_length=rng.uniform(.220,.260);half_width=rng.uniform(.192,.228)
    height=rng.uniform(.066,.136);yaw=rng.uniform(-.38,.38);phase=rng.uniform(-.10,.10)
    ca,sa=math.cos(yaw),math.sin(yaw)
    footprint=[]
    for i in range(sides):
        angle=math.tau*i/sides+phase
        # Fuller rounded squircle outlines, rather than rectangular bricks.
        x=math.copysign(abs(math.cos(angle))**.84,math.cos(angle))*half_length
        y=math.copysign(abs(math.sin(angle))**.84,math.sin(angle))*half_width
        variation=rng.uniform(.948,1.052)
        footprint.append(((ca*x-sa*y)*variation,(sa*x+ca*y)*variation))
    extent=max(abs(p[1]) for p in footprint)
    shrink=min(1,(2.986-abs(offset))/max(extent,.001))
    footprint=[(x*shrink,y*shrink) for x,y in footprint]
    vertices=[];faces=[];gains=[]
    tilt_x,tilt_y=rng.uniform(-.012,.012),rng.uniform(-.012,.012)
    for radius,z in ((.86,-.020),(1,.010),(.97,height-.033),(.72,height-.006)):
        for x,y in footprint:
            along,cross=x*radius,y*radius;p,n=road.sample(s+along)
            q=p+n*(offset+cross)
            vertices.append((q.x,q.y,z+along*tilt_x+cross*tilt_y))
    bottom=len(vertices);p,n=road.sample(s);q=p+n*offset
    vertices.append((q.x,q.y,-.020));top=len(vertices);vertices.append((q.x,q.y,height))
    for row in range(3):
        for i in range(sides):
            j=(i+1)%sides;a=row*sides+i;b=row*sides+j;c=(row+1)*sides+j;d=(row+1)*sides+i
            faces.extend(((a,b,c),(a,c,d)));gain=(.83,.95,1.04)[row]
            gains.extend((gain,gain))
    for i in range(sides):
        j=(i+1)%sides;faces.extend(((bottom,j,i),(top,3*sides+i,3*sides+j)));gains.extend((.78,1.04))
    return vertices,faces,gains,height,shrink

def inspect_meshes(meshes):
    counts=collections.Counter();normal_error=0;normal_zeros=0;min_area=float('inf');duplicate=0
    min_z=float('inf');max_z=-float('inf');colored=0;color_min=1;color_max=0
    for obj in meshes:
        mesh=obj.data;counts['meshes']+=1;counts['vertices']+=len(mesh.vertices);counts['triangles']+=len(mesh.polygons)
        verts=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',verts);verts=verts.reshape(-1,3)
        assert np.all(np.isfinite(verts))
        matrix=np.asarray(obj.matrix_world,dtype=np.float64)
        world=verts@matrix[:3,:3].T+matrix[:3,3]
        min_z=min(min_z,float(world[:,2].min()));max_z=max(max_z,float(world[:,2].max()))
        ids=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',ids);ids=ids.reshape(-1,3)
        lengths=np.linalg.norm(np.cross(verts[ids[:,1]]-verts[ids[:,0]],verts[ids[:,2]]-verts[ids[:,0]]),axis=1)
        min_area=min(min_area,float(lengths.min())*.5);assert np.all(lengths>1e-8)
        keys=[tuple(sorted(t)) for t in ids];duplicate+=len(keys)-len(set(keys))
        values=np.empty(len(mesh.corner_normals)*3,dtype=np.float32);mesh.corner_normals.foreach_get('vector',values)
        magnitudes=np.linalg.norm(values.reshape(-1,3),axis=1);assert np.all(np.isfinite(magnitudes))
        normal_zeros+=int(np.sum(magnitudes<1e-6));normal_error=max(normal_error,float(np.max(np.abs(magnitudes-1))))
        assert mesh.color_attributes;colored+=1
        attr=mesh.color_attributes.active_color;values=np.empty(len(attr.data)*4,dtype=np.float32);attr.data.foreach_get('color',values)
        rgb=values.reshape(-1,4)[:,:3];assert np.all(np.isfinite(rgb));color_min=min(color_min,float(rgb.min()));color_max=max(color_max,float(rgb.max()))
    assert duplicate==0 and normal_zeros==0
    assert min_area>1e-8 and min_z>-.03 and max_z<.145
    return dict(**counts,colored_meshes=colored,duplicate_triangles=duplicate,zero_corner_normals=normal_zeros,
                max_normal_unit_error=normal_error,min_triangle_area=min_area,min_z=min_z,max_z=max_z,
                linear_color_min=color_min,linear_color_max=color_max)

def main():
    started=time.monotonic();OUT.mkdir(parents=True,exist_ok=True)
    frozen={path.name:sha(path) for path in (OUT/'emerald_island.fbx',OUT/'emerald_island_clean.fbx',OUT/'emerald_route.json',OUT/'emerald_materials.json',SOURCE)}
    manifest=json.loads(SOURCE.read_text(encoding='utf8'));points=manifest['road']['points'];assert len(points)==644
    road=ClosedRoad(points);rows=13;stations=round(road.length/.5);spacing=road.length/stations
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    col=bpy.data.collections.new('Emerald_native_cobblestones');scene.collection.children.link(col)
    root=bpy.data.objects.new('EmeraldCobblestones',None);col.objects.link(root)
    mat=bpy.data.materials.new('RA_Emerald_Road');mat.use_nodes=True
    bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Base Color'].default_value=(1,1,1,1);bs.inputs['Roughness'].default_value=.88
    rng=random.Random(SEED);batches={};heights=[];edge_shrink=0;max_gap=0
    grays=[(.49,.49,.47),(.46,.48,.50),(.43,.45,.47),(.52,.51,.48),(.45,.44,.42),(.50,.52,.53)]
    for row in range(rows):
        phase=(.5 if row%2 else 0)+rng.uniform(-.16,.16)
        raw=[rng.uniform(-.032,.032) for _ in range(stations)]
        jitter=[(raw[(j-1)%stations]+2*raw[j]+raw[(j+1)%stations])/4 for j in range(stations)]
        arclengths=[(j+phase)*spacing+jitter[j] for j in range(stations)]
        max_gap=max(max_gap,max((arclengths[(j+1)%stations]-arclengths[j])%road.length for j in range(stations)))
        for j,s in enumerate(arclengths):
            offset=-3+(row+.5)*6/rows+rng.uniform(-.018,.018)
            vertices,faces,gains,height,shrink=stone(road,s,offset,rng)
            center,_=road.sample(s);key=(math.floor(center.x/CELL),math.floor(center.y/CELL))
            base=rng.choice(grays);tone=rng.uniform(.94,1.06);color=tuple(linear(min(.8,c*tone)) for c in base)
            batches.setdefault(key,StoneBatch(key)).add(vertices,faces,color,gains)
            heights.append(height);edge_shrink+=shrink<.999
    meshes=[]
    for batch in batches.values():obj=batch.build(col,mat);obj.parent=root;meshes.append(obj)
    before=inspect_meshes(meshes);assert 600000<=before['triangles']<=1050000
    anchors=[]
    for name,pos,expected in [('RA_Origin',(0,0,0),(0,0,0)),('RA_X10',(10,0,0),(10,0,0)),('RA_North10',(0,10,0),(0,0,-10))]:
        obj=bpy.data.objects.new(name,None);col.objects.link(obj);obj.parent=root;obj.location=pos
        anchors.append(dict(name=name,blender=list(pos),expected_aligned_unity=list(expected)))
    for obj in scene.objects:obj.select_set(False)
    for obj in col.objects:obj.select_set(True)
    path=OUT/'emerald_cobbles.fbx';pending=OUT/'emerald_cobbles.pending.fbx'
    bpy.ops.export_scene.fbx(filepath=str(pending),use_selection=True,object_types={'MESH','EMPTY'},
        axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',
        bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',
        bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
    pending.replace(path)
    # Fresh import in the same isolated process, with original construction data
    # deleted; compare actual FBX counts and all referenced normals/colors.
    for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
    bpy.context.view_layer.update();imported=[o for o in scene.objects if o.type=='MESH'];after=inspect_meshes(imported)
    assert after['triangles']==before['triangles'] and after['meshes']==before['meshes']
    anchor_proofs=[]
    for anchor in anchors:
        obj=bpy.data.objects.get(anchor['name']);assert obj
        pos=list(obj.matrix_world.translation);error=math.dist(pos,anchor['blender']);assert error<.001
        anchor_proofs.append(dict(**anchor,roundtrip_blender=pos,error=error))
    for source in (OUT/'emerald_island.fbx',OUT/'emerald_island_clean.fbx',OUT/'emerald_route.json',OUT/'emerald_materials.json',SOURCE):
        assert frozen[source.name]==sha(source),'Frozen input changed: '+source.name
    report=dict(schema='radio_archipelago_native_cobblestones/1',pass_geometry=True,pass_roundtrip=True,
        source_route_geometry_hash=manifest['geometry_sha256'],source_route_points=len(points),
        road_width=6,route_length=road.length,closed_lap=True,rows=rows,stations=stations,
        stone_count=rows*stations,nominal_stone_size=[.48,.42],stone_sides=[8,9,10],bevel_rings=4,
        station_spacing=spacing,max_closed_lap_neighbor_gap=max_gap,edge_stones_shrunk=edge_shrink,
        top_heights=[min(heights),max(heights)],spatial_cell_meters=CELL,before=before,after=after,
        material_name='RA_Emerald_Road',vertex_color_space='linear',disable_metric_brick_shader=True,
        geometry_type='Separate closed rounded volumetric stones, not a texture or per-view image',
        anchors=anchor_proofs,coordinate_mapping='Blender(x,y,z) -> aligned Unity(x,z,-y)',
        unity_import_note='Use the same alignment as the existing island, including its proven Unity X reflection; validate anchors. Do not double-apply the parent correction.',
        fbx_file=path.name,fbx_sha256=sha(path),fbx_bytes=path.stat().st_size,
        frozen_inputs_unchanged=True,unity_runtime_test_performed=False,elapsed_seconds=round(time.monotonic()-started,2))
    (OUT/'emerald_cobbles_report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('NATIVE_COBBLES_READY',json.dumps(report),flush=True)

def clip_polygon(poly,nx,ny,bound):
    if not poly:return []
    out=[];a=poly[-1];da=nx*a[0]+ny*a[1]-bound
    for b in poly:
        db=nx*b[0]+ny*b[1]-bound
        if (da<=1e-10)!=(db<=1e-10):
            t=da/(da-db);out.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
        if db<=1e-10:out.append(b)
        a,da=b,db
    return out

def polygon_measure(poly):
    twice=sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(poly,poly[1:]+poly[:1]))
    if abs(twice)<1e-10:return 0,(0,0)
    cx=sum((a[0]+b[0])*(a[0]*b[1]-b[0]*a[1]) for a,b in zip(poly,poly[1:]+poly[:1]))/(3*twice)
    cy=sum((a[1]+b[1])*(a[0]*b[1]-b[0]*a[1]) for a,b in zip(poly,poly[1:]+poly[:1]))/(3*twice)
    return abs(twice)*.5,(cx,cy)

def principal(poly):
    array=np.asarray(poly,dtype=np.float64);center=array.mean(axis=0);cov=(array-center).T@(array-center)
    _,vectors=np.linalg.eigh(cov);axis=vectors[:,1];side=np.array((-axis[1],axis[0]))
    along=array@axis;across=array@side
    return axis,side,float(np.ptp(along)),float(np.ptp(across)),float(math.atan2(axis[1],axis[0])%math.pi)

def poisson_sites(length,seed):
    """Variable-radius Bridson sampling on a periodic strip, without rows."""
    rng=random.Random(seed);cell=.32;columns=math.ceil(length/cell);cell=length/columns
    grid=collections.defaultdict(list);sites=[];active=[]
    def radius():
        value=rng.random()
        return rng.uniform(.235,.32) if value<.18 else rng.uniform(.34,.46) if value<.59 else rng.uniform(.48,.64)
    def add(s,t,r):
        index=len(sites);sites.append((s,t,r,r*r*.34));active.append(index);grid[(int(s/cell)%columns,int((t+3)/cell))].append(index)
    def available(s,t,r):
        x,y=int(s/cell)%columns,int((t+3)/cell)
        for dx in range(-3,4):
            for dy in range(-3,4):
                for index in grid.get(((x+dx)%columns,y+dy),()):
                    a,b,other,_=sites[index];ds=(s-a+length*.5)%length-length*.5
                    if ds*ds+(t-b)**2<((r+other)*.5)**2:return False
        return True
    add(rng.random()*length,rng.uniform(-2.9,2.9),radius())
    while active:
        active_index=rng.randrange(len(active));site=sites[active[active_index]];accepted=False
        for attempt in range(18):
            r=radius();angle=rng.random()*math.tau;distance=(site[2]+r)*.5*rng.uniform(1,2)
            s=(site[0]+math.cos(angle)*distance)%length;t=site[1]+math.sin(angle)*distance
            if -2.99<t<2.99 and available(s,t,r):add(s,t,r);accepted=True;break
        if not accepted:active[active_index]=active[-1];active.pop()
        if len(sites)>35000:raise RuntimeError('Unexpectedly dense Poisson pavement')
    return sites

def voronoi_cells(sites,length):
    columns=math.ceil(length);cell=length/columns;grid=collections.defaultdict(list)
    for i,(s,t,_,_) in enumerate(sites):grid[(int(s/cell)%columns,math.floor(t))].append(i)
    result=[]
    for index,(s,t,r,w) in enumerate(sites):
        x,y=int(s/cell)%columns,math.floor(t);near=[]
        for dx in range(-2,3):
            for dy in range(-2,3):
                for other in grid.get(((x+dx)%columns,y+dy),()):
                    if other==index:continue
                    q=sites[other];ds=(q[0]-s+length*.5)%length-length*.5;dt=q[1]-t
                    near.append((ds*ds+dt*dt,ds,dt,q[3]))
        near.sort();poly=[(-2,-3-t),(2,-3-t),(2,3-t),(-2,3-t)]
        for distance,ds,dt,other_w in near[:48]:
            poly=clip_polygon(poly,ds,dt,(distance+w-other_w)*.5)
            if not poly:break
        area,center=polygon_measure(poly)
        if area>.006:result.append((s,t,poly,area,center))
    return result

def inset(poly,distance):
    output=poly[:]
    for a,b in zip(poly,poly[1:]+poly[:1]):
        dx,dy=b[0]-a[0],b[1]-a[1]
        output=clip_polygon(output,dy,-dx,dy*a[0]-dx*a[1]-distance*math.hypot(dx,dy))
    return output

def tidy(poly):
    poly=poly[:]
    while len(poly)>4:
        costs=[abs((poly[i][0]-poly[i-1][0])*(poly[(i+1)%len(poly)][1]-poly[i-1][1])
                   -(poly[i][1]-poly[i-1][1])*(poly[(i+1)%len(poly)][0]-poly[i-1][0])) for i in range(len(poly))]
        shortest=min(range(len(poly)),key=lambda i:costs[i])
        if len(poly)<=10 and costs[shortest]>.0007:break
        poly.pop(shortest)
    return poly

def rounded_cell_outline(poly,amount,rng):
    """Real plan-view corner fillets, kept inside each packed stone cell."""
    out=[]
    for i,p in enumerate(poly):
        before=poly[i-1];after=poly[(i+1)%len(poly)];cut=amount*rng.uniform(.82,1.12)
        out.append((p[0]+(before[0]-p[0])*cut,p[1]+(before[1]-p[1])*cut))
        out.append((p[0]+(after[0]-p[0])*cut,p[1]+(after[1]-p[1])*cut))
    # At most ten real perimeter sides per stone, retaining curved bevels and
    # rounded corners rather than doubling an already complex cell perimeter.
    while len(out)>10:
        costs=[abs((out[i][0]-out[i-1][0])*(out[(i+1)%len(out)][1]-out[i-1][1])
                   -(out[i][1]-out[i-1][1])*(out[(i+1)%len(out)][0]-out[i-1][0])) for i in range(len(out))]
        out.pop(min(range(len(out)),key=lambda i:costs[i]))
    return out

def cell_stone(road,s,t,poly,rng,family,rings):
    area,center=polygon_measure(poly);cx,cy=center
    axis,side,long,wide,angle=principal(poly)
    poly=[(x-cx,y-cy) for x,y in poly];s+=cx;t+=cy
    h=rng.uniform(.066,.136)
    caps={'round':.72,'squat':.88,'long_oblong':.78,'worn_triangle':.74,'irregular':.80,'wide_flat':.92}
    top_radius=caps[family]
    levels=[(.89,-.020),(1,.009),(.97,h-.030),(top_radius,h-.006)] if rings==4 else[(.89,-.020),(1,h-.030),(top_radius,h-.006)]
    vertices=[];faces=[];gains=[];n=len(poly);phase=rng.random()*math.tau
    tilt_x,tilt_y=rng.uniform(-.006,.006),rng.uniform(-.006,.006)
    for row,(radius,z) in enumerate(levels):
        for x,y in poly:
            f=1
            if row==len(levels)-1:
                a=math.atan2(y,x)
                f=1+.075*math.cos(3*a+phase) if family=='worn_triangle' else 1+.035*math.sin(5*a+phase) if family=='irregular' else 1
            a,b=x*radius*f,y*radius*f;p,normal=road.sample(s+a);q=p+normal*(t+b)
            vertices.append((q.x,q.y,z+a*tilt_x+b*tilt_y))
    p,normal=road.sample(s);q=p+normal*t;bottom=len(vertices);vertices.append((q.x,q.y,-.02));top=len(vertices);vertices.append((q.x,q.y,h))
    for row in range(len(levels)-1):
        for i in range(n):
            j=(i+1)%n;a=row*n+i;b=row*n+j;c=(row+1)*n+j;d=(row+1)*n+i
            faces.extend(((a,b,c),(a,c,d)));gains.extend((.88+row*.055,)*2)
    for i in range(n):
        j=(i+1)%n;faces.extend(((bottom,j,i),(top,(len(levels)-1)*n+i,(len(levels)-1)*n+j)));gains.extend((.80,1.035))
    return vertices,faces,gains,(long,wide,angle,h,area)

def previews_v2(road,manifest,report):
    """Actual native render closeups, never edits to exported FBX geometry."""
    scene=bpy.context.scene
    for mat in bpy.data.materials:
        if not mat.use_nodes:continue
        bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if bs:
            color=mat.node_tree.nodes.new('ShaderNodeVertexColor');color.layer_name='Color';mat.node_tree.links.new(color.outputs['Color'],bs.inputs['Base Color'])
    scene.world.use_nodes=True;bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.7,.8,.95,1);bg.inputs['Strength'].default_value=.38
    sun_data=bpy.data.lights.new('Stone side light','SUN');sun_data.energy=2.5;sun_data.angle=.12
    sun=bpy.data.objects.new('Stone side light',sun_data);scene.collection.objects.link(sun);sun.rotation_euler=(.65,-.3,-.6)
    scene.render.engine='CYCLES';scene.cycles.samples=32;scene.cycles.use_denoising=True
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
        for device in prefs.devices:device.use=device.type=='CUDA'
        scene.cycles.device='GPU'
    except Exception:pass
    scene.render.resolution_x=1600;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
    scene.render.image_settings.file_format='PNG'
    p,normal=road.sample(road.length*.32);tangent=Vector((normal.y,-normal.x,0));target=p+Vector((0,0,.08))
    for name,offset,scale in [('top',Vector((0,0,15)),8.8),('angle',-tangent*5.7-normal*.7+Vector((0,0,4.6)),10.5)]:
        data=bpy.data.cameras.new('Cobbles V2 '+name);data.type='ORTHO';data.ortho_scale=scale
        cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=target+offset
        cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam
        scene.render.filepath=str(OUT/('emerald_cobbles_v2_'+name+'.png'));bpy.ops.render.render(write_still=True)

def main_v2():
    started=time.monotonic();OUT.mkdir(exist_ok=True)
    frozen_paths=[OUT/'emerald_cobbles.fbx',OUT/'emerald_cobbles_report.json',OUT/'emerald_island.fbx',OUT/'emerald_island_clean.fbx',SOURCE]
    frozen={str(path):sha(path) for path in frozen_paths}
    manifest=json.loads(SOURCE.read_text(encoding='utf8'));road=ClosedRoad(manifest['road']['points']);rng=random.Random(814602)
    sites=poisson_sites(road.length,814602);print('V2_POISSON_SITES',len(sites),flush=True)
    cells=voronoi_cells(sites,road.length)
    # One restrained centroid relaxation removes needle slivers without making
    # equal-sized hexagons. Power weights and variable radii remain unchanged.
    relaxed=[]
    for (s,t,r,w),(_,_,_,_,center) in zip(sites,cells):relaxed.append(((s+center[0]*.12)%road.length,min(2.99,max(-2.99,t+center[1]*.12)),r,w))
    if len(relaxed)==len(sites):sites=relaxed;cells=voronoi_cells(sites,road.length)
    pieces=[]
    for s,t,poly,area,center in cells:
        pending=[poly]
        for iteration in range(4):
            split=[];done=True
            for polygon in pending:
                axis,side,long,wide,_=principal(polygon)
                if long>.93 or wide>.79:
                    _,c=polygon_measure(polygon);bound=float(axis[0]*c[0]+axis[1]*c[1]);
                    a=clip_polygon(polygon,float(axis[0]),float(axis[1]),bound);b=clip_polygon(polygon,-float(axis[0]),-float(axis[1]),-bound)
                    split.extend(p for p in(a,b) if len(p)>=3);done=False
                else:split.append(polygon)
            pending=split
            if done:break
        for polygon in pending:
            joint=rng.uniform(.015,.030);shape=tidy(inset(polygon,joint))
            if len(shape)>=3 and polygon_measure(shape)[0]>.007:pieces.append((s,t,shape,joint))
    family_names=['round','squat','long_oblong','worn_triangle','irregular','wide_flat']
    rounded=[]
    cuts={'round':.24,'squat':.18,'long_oblong':.22,'worn_triangle':.12,'irregular':.20,'wide_flat':.15}
    for index,(s,t,poly,joint) in enumerate(pieces):
        axis,side,long,wide,angle=principal(poly)
        family='long_oblong' if long/max(wide,.01)>1.9 else family_names[index%6]
        rounded.append((s,t,rounded_cell_outline(poly,cuts[family],rng),joint,family))
    pieces=rounded
    side_sum=sum(len(p[2]) for p in pieces);rings=4 if side_sum*8<1080000 else 3
    for obj in list(bpy.context.scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    col=bpy.data.collections.new('Emerald organic Paris cobbles V2');scene.collection.children.link(col)
    root=bpy.data.objects.new('EmeraldCobblestonesV2',None);col.objects.link(root)
    mat=bpy.data.materials.new('RA_Emerald_Road');mat.use_nodes=True;bs=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED');bs.inputs['Roughness'].default_value=.89
    batches={};families=collections.Counter();measurements=[];orientation=collections.Counter()
    gray=[(.49,.49,.47),(.45,.47,.50),(.43,.45,.47),(.53,.52,.49),(.46,.45,.43),(.49,.51,.53)]
    for index,(s,t,poly,joint,family) in enumerate(pieces):
        axis,side,long,wide,angle=principal(poly)
        verts,faces,gains,metrics=cell_stone(road,s,t,poly,rng,family,rings)
        center,_=road.sample(s);key=(math.floor(center.x/CELL),math.floor(center.y/CELL));color=rng.choice(gray);tone=rng.uniform(.92,1.07)
        batches.setdefault(key,StoneBatch(key)).add(verts,faces,tuple(linear(c*tone) for c in color),gains)
        families[family]+=1;measurements.append(metrics);orientation[int(angle/math.pi*12)%12]+=1
    # A real dark mortar bed prevents the retained pale base road showing as
    # broad cream cracks. It remains below every cobble and gameplay platform.
    bed=StoneBatch((999,999));points=road.points;v=[];f=[]
    for p,n in zip(points,road.normals):v.extend(((p.x-3*n.x,p.y-3*n.y,.002),(p.x+3*n.x,p.y+3*n.y,.002)))
    for i in range(len(points)):
        j=(i+1)%len(points);f.extend(((2*i,2*j,2*j+1),(2*i,2*j+1,2*i+1)))
    bed.add(v,f,tuple(linear(c) for c in(.29,.30,.30)),[1]*len(f));batches[(999,999)]=bed
    meshes=[]
    for batch in batches.values():obj=batch.build(col,mat);obj.parent=root;meshes.append(obj)
    before=inspect_meshes(meshes);assert before['triangles']<=1100000,before
    anchors=[]
    for name,pos,expected in [('RA_Origin',(0,0,0),(0,0,0)),('RA_X10',(10,0,0),(10,0,0)),('RA_North10',(0,10,0),(0,0,-10))]:
        obj=bpy.data.objects.new(name,None);col.objects.link(obj);obj.parent=root;obj.location=pos;anchors.append(dict(name=name,blender=list(pos),expected_aligned_unity=list(expected)))
    for obj in scene.objects:obj.select_set(False)
    for obj in col.objects:obj.select_set(True)
    path=OUT/'emerald_cobbles_v2.fbx';pending=OUT/'emerald_cobbles_v2.pending.fbx'
    bpy.ops.export_scene.fbx(filepath=str(pending),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,
        apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,
        mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
    pending.replace(path)
    for obj in list(scene.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');bpy.context.view_layer.update()
    after=inspect_meshes([o for o in scene.objects if o.type=='MESH']);assert after['triangles']==before['triangles']
    for anchor in anchors:
        obj=bpy.data.objects.get(anchor['name']);anchor['roundtrip_blender']=list(obj.matrix_world.translation)
        anchor['error']=math.dist(anchor['blender'],anchor['roundtrip_blender']);assert anchor['error']<.001
    data=np.asarray(measurements)
    report=dict(schema='radio_archipelago_organic_cobblestones/2',pass_geometry=True,pass_roundtrip=True,algorithm='Periodic variable-radius Poisson sampling, weighted power Voronoi clipping, restrained centroid relaxation, local cell splitting and compact joint insets',
        stone_count=len(pieces),poisson_sites=len(sites),geometry_families=dict(families),orientation_bins_15_degrees=dict(orientation),
        length_quantiles=list(np.quantile(data[:,0],[0,.1,.5,.9,1])),width_quantiles=list(np.quantile(data[:,1],[0,.1,.5,.9,1])),
        area_quantiles=list(np.quantile(data[:,4],[0,.1,.5,.9,1])),joint_inset_range=[.015,.030],bevel_rings=rings,
        source_route_points=644,closed_periodic_lap=True,road_width=6,route_length=road.length,mortar_bed_height=.002,
        before=before,after=after,anchors=anchors,material_name='RA_Emerald_Road',vertex_color_space='linear',disable_metric_brick_shader=True,
        fbx_file=path.name,fbx_sha256=sha(path),fbx_bytes=path.stat().st_size,unity_runtime_test_performed=False,
        preview_files=['emerald_cobbles_v2_top.png','emerald_cobbles_v2_angle.png'],frozen_inputs_unchanged=True)
    assert len(families)>=6 and len(orientation)==12
    for source,value in frozen.items():assert sha(pathlib.Path(source))==value
    previews_v2(road,manifest,report)
    report['elapsed_seconds']=round(time.monotonic()-started,2)
    (OUT/'emerald_cobbles_v2_report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('ORGANIC_COBBLES_V2_READY',json.dumps(report),flush=True)

if __name__=='__main__':main_v2() if '--v2' in sys.argv else main()
