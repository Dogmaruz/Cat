"""Canonical 3D islands: actual meshes, fixed cameras, no AI view generation."""
import bpy,sys,pathlib,json,math,random,time,hashlib,argparse
from mathutils import Vector
from mathutils.kdtree import KDTree
from bpy_extras.object_utils import world_to_camera_view
ROOT=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
import architecture,scenery,terrain_road,validate_scene
WORLDS=json.loads((ROOT/'Source/worlds.json'if(ROOT/'Source/worlds.json').exists()else ROOT.parent/'V2/worlds-final.json').read_text(encoding='utf8'))
LAYOUT_FILE=ROOT/'building-layouts.json'
LAYOUTS={w['id']:w for w in json.loads(LAYOUT_FILE.read_text(encoding='utf8'))['worlds']}if LAYOUT_FILE.exists()else{}
PALETTES=[
 dict(land='#4F7645',rock='#58616A',road='#BCB6A3',water='#2F8499',leaf='#315D32',leaf_light='#72954C',style='forest'),
 dict(land='#D39A70',rock='#B5654F',road='#E3B69A',water='#39B4B7',leaf='#39796C',style='adobe'),
 dict(land='#61507D',rock='#40365D',road='#A18AB5',water='#46AABB',leaf='#736591',accent='#AC70EA',style='crystal'),
 dict(land='#AC8245',rock='#875A42',road='#CDA36F',water='#6196A9',leaf='#BF682E',leaf_light='#E3A445',style='autumn'),
 dict(land='#D9E8EC',rock='#6AABCF',road='#819BAC',water='#3E86A3',leaf='#89B9BB',style='ice'),
 dict(land='#58844D',rock='#505F58',road='#778178',water='#2DAFB0',leaf='#33824F',leaf_light='#78A54D',style='tropical'),
 dict(land='#6B7362',rock='#343E48',road='#697078',water='#346E82',leaf='#435F53',style='storm'),
 dict(land='#B5A249',rock='#AD8B5C',road='#D0B082',water='#4BA5A8',leaf='#5D8C42',accent='#E5BC41',style='farm'),
 dict(land='#6F5149',rock='#35323D',road='#8F8582',water='#BE613B',leaf='#AB493D',accent='#CF623D',style='volcanic'),
 dict(land='#DCB8BC',rock='#AB94B7',road='#AEAED1',water='#B2C3E2',leaf='#AC80B1',accent='#D3B475',style='cloud')]
KINDS=[['forest_house','forest_house','forest_workshop'],['adobe_house','adobe_house','adobe_tower'],['crystal_workshop','crystal_house','crystal_house'],['autumn_house','autumn_workshop','autumn_windmill'],['ice_house','ice_house','ice_workshop','ice_relay'],['tropical_house','tropical_house','tropical_workshop','tropical_windmill'],['storm_house','storm_house','storm_workshop','storm_windmill'],['farm_house','farm_workshop','farm_windmill'],['volcanic_house','volcanic_workshop','volcanic_relay'],['cloud_house','cloud_workshop','cloud_observatory','cloud_relay']]

def rgb(h):return tuple(int(h[i:i+2],16)/255 for i in (1,3,5))
def material(name,color,metal=0):
    m=bpy.data.materials.new(name);m.use_nodes=True
    c=rgb(color) if isinstance(color,str) else color
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    linear=lambda v:v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
    bs.inputs['Base Color'].default_value=(*(linear(v)for v in c[:3]),1)
    bs.inputs['Roughness'].default_value=.72;bs.inputs['Metallic'].default_value=metal
    m.diffuse_color=(*c[:3],1);return m
def box(name,loc,dim,mat,col,role='obstacle'):
    vs=[(loc[0]+a*dim[0]/2,loc[1]+b*dim[1]/2,loc[2]+c*dim[2]/2)for a,b,c in [(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]]
    fs=[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(vs,[],fs);mesh.update()
    obj=bpy.data.objects.new(name,mesh);col.objects.link(obj);obj.data.materials.append(mat);obj['v3_role']=role
    mod=obj.modifiers.new('Soft stone corners','BEVEL');mod.width=.15;mod.segments=3
    return obj
def terrace_ring(x,y,yaw,seed,pad_scale=1):
    rng=random.Random(seed);ring=[]
    for a,b in [(-1,-.65),(-.65,-1),(.65,-1),(1,-.65),(1,.65),(.65,1),(-.65,1),(-1,.65)]:
        a*=8.7*pad_scale*rng.uniform(.94,1.02);b*=8.0*pad_scale*rng.uniform(.94,1.02)
        ring.append((x+a*math.cos(yaw)-b*math.sin(yaw),y+a*math.sin(yaw)+b*math.cos(yaw)))
    return ring
def terrace(name,x,y,floor,bottom,yaw,mat,col,seed,pad_scale=1):
    ring=terrace_ring(x,y,yaw,seed,pad_scale)
    verts=[(a,b,z)for z in(bottom,floor)for a,b in ring];n=len(ring)
    faces=[tuple(reversed(range(n))),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n)for i in range(n)]
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);col.objects.link(obj);obj.data.materials.append(mat);obj['v3_role']='obstacle'
    mod=obj.modifiers.new('Weathered terrace edges','BEVEL');mod.width=.28;mod.segments=3
    for j in range(3):
        dist=(7.6+j*.85)*pad_scale;z=floor-.2-j*.42
        step=box(name+'_entry_step_'+str(j),(x+math.sin(yaw)*dist,y-math.cos(yaw)*dist,z-.28),(3.6,1.5,.56),mat,col)
        # Mesh was authored in world coordinates, rotate around its own center.
        pivot=Vector((x+math.sin(yaw)*dist,y-math.cos(yaw)*dist,z-.28))
        for v in step.data.vertices:
            d=v.co-pivot;v.co=pivot+Vector((d.x*math.cos(yaw)-d.y*math.sin(yaw),d.x*math.sin(yaw)+d.y*math.cos(yaw),d.z))
    return obj
def tag(objects):
    for obj in objects:obj['v3_role']='obstacle'
    return objects
def inside(p,poly):
    x,y=p;hit=False
    for (ax,ay),(bx,by)in zip(poly,poly[1:]+poly[:1]):
        if (ay>y)!=(by>y) and x<(bx-ax)*(y-ay)/(by-ay)+ax:hit=not hit
    return hit
def segment_dist(p,a,b):
    dx,dy=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((p[0]-a[0])*dx+(p[1]-a[1])*dy)/max(dx*dx+dy*dy,1e-15)))
    return math.hypot(p[0]-a[0]-dx*t,p[1]-a[1]-dy*t)
def configure(scene,size):
    scene.render.resolution_x=scene.render.resolution_y=size;scene.render.resolution_percentage=100
    formats=[x.identifier for x in scene.render.image_settings.bl_rna.properties['file_format'].enum_items]
    assert 'PNG'in formats;scene.render.image_settings.file_format='PNG'
    modes=[x.identifier for x in scene.render.image_settings.bl_rna.properties['color_mode'].enum_items]
    scene.render.image_settings.color_mode='RGBA'if'RGBA'in modes else modes[0]
    scene.render.film_transparent=False
    scene.render.engine='CYCLES'
    scene.cycles.samples=64;scene.cycles.use_denoising=True
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='CUDA'
    scene.cycles.device='GPU'
    if hasattr(scene,'eevee'):
        for k,v in [('taa_render_samples',64),('taa_samples',64),('use_gtao',True)]:
            if hasattr(scene.eevee,k):setattr(scene.eevee,k,v)
    scene.world=bpy.data.worlds.new('Warm neutral studio');scene.world.use_nodes=True
    nt=scene.world.node_tree
    bg=next(n for n in nt.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.82,.85,.89,1);bg.inputs['Strength'].default_value=.6
    # A clean reference backdrop is independent of the illumination environment.
    camera_bg=nt.nodes.new('ShaderNodeBackground');camera_bg.inputs['Color'].default_value=(1.0,.97,.92,1);camera_bg.inputs['Strength'].default_value=1
    ray=nt.nodes.new('ShaderNodeLightPath');mix=nt.nodes.new('ShaderNodeMixShader');out=next(n for n in nt.nodes if n.type=='OUTPUT_WORLD')
    nt.links.new(ray.outputs['Is Camera Ray'],mix.inputs[0]);nt.links.new(bg.outputs[0],mix.inputs[1]);nt.links.new(camera_bg.outputs[0],mix.inputs[2]);nt.links.new(mix.outputs[0],out.inputs['Surface'])
    # Standard is declared in the installed Blender OCIO configuration.
    scene.view_settings.view_transform='Standard';scene.view_settings.exposure=0
    data=bpy.data.lights.new('Large soft sun','SUN');data.energy=3.0;data.angle=.12
    sun=bpy.data.objects.new('Large soft sun',data);scene.collection.objects.link(sun);sun.rotation_euler=(.45,-.55,-.5);sun['v3_role']='light'
    for name,loc,energy,color in [('Warm key',(-70,-90,120),38000,(1,.88,.73)),('Cool fill',(90,10,85),22000,(.75,.86,1))]:
        d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=85;d.color=color
        o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,0))-o.location).to_track_quat('-Z','Y').to_euler();o['v3_role']='light'
    scene.render.image_settings.compression=30
def shader_polish():
    for mat in bpy.data.materials:
        if not mat.use_nodes:continue
        nt=mat.node_tree;bs=next((n for n in nt.nodes if n.type=='BSDF_PRINCIPLED'),None)
        if not bs or bs.inputs['Base Color'].is_linked:continue
        color=tuple(bs.inputs['Base Color'].default_value)
        # Scenery builder accepts display RGB; Blender shader inputs are linear.
        if mat.name.startswith('RA_'):
            linear=lambda v:v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
            color=tuple(linear(v)for v in color[:3])+(1,)
            bs.inputs['Base Color'].default_value=color
        if bs.inputs['Metallic'].default_value>.35:continue
        noise=nt.nodes.new('ShaderNodeTexNoise');noise.inputs['Scale'].default_value=2.8;noise.inputs['Detail'].default_value=2.2
        ramp=nt.nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[0].color=tuple(v*.82 for v in color[:3])+(1,)
        ramp.color_ramp.elements[1].position=.82;ramp.color_ramp.elements[1].color=tuple(min(1,v*1.1+.005)for v in color[:3])+(1,)
        nt.links.new(noise.outputs['Fac'],ramp.inputs[0]);nt.links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
def visual_signature():
    data=[]
    for m in sorted(bpy.data.materials,key=lambda x:x.name):
        if not m.use_nodes:continue
        nodes=[]
        for n in m.node_tree.nodes:
            vals=[]
            for s in n.inputs:
                if hasattr(s,'default_value'):
                    v=s.default_value
                    try:v=list(v)
                    except TypeError:v=v if isinstance(v,(str,float,int,bool))else str(v)
                    vals.append((s.name,v))
            nodes.append((n.name,n.bl_idname,vals))
        data.append((m.name,nodes,[(l.from_node.name,l.from_socket.name,l.to_node.name,l.to_socket.name)for l in m.node_tree.links]))
    return hashlib.sha256(json.dumps(data,sort_keys=True).encode()).hexdigest()
def populate(idx,col,land,route_data):
    rng=random.Random(3100+idx);pal=PALETTES[idx];pts=land['route_points'];outline=[tuple(p[:2])for p in land['outline']]
    if outline[0]==outline[-1]:outline.pop()
    kd=KDTree(len(pts))
    for i,p in enumerate(pts):kd.insert(Vector(p),i)
    kd.balance()
    def road_dist(p):return kd.find(Vector((p[0],p[1],0)))[2]
    def shore(p):return min(segment_dist(p,a,b)for a,b in zip(outline,outline[1:]+outline[:1]))
    minx,maxx=min(p[0]for p in outline),max(p[0]for p in outline);miny,maxy=min(p[1]for p in outline),max(p[1]for p in outline)
    candidates=[];compact_candidates=[]
    for x in range(math.ceil(minx),math.floor(maxx),4):
        for y in range(math.ceil(miny),math.floor(maxy),4):
            if not inside((x,y),outline):continue
            if road_dist((x,y))>20 and shore((x,y))>13:candidates.append((x,y))
            if road_dist((x,y))>14 and shore((x,y))>8:compact_candidates.append((x,y))
    assert len(candidates)>5,'No safe technology pads'
    desired=[((p['center'][0]-500)*.18,(500-p['center'][1])*.18)for p in route_data['technologyBuildingPads']]
    desired.append((maxx*.6,miny*.6));occupied=[];buildings=[]
    foundation=material('Technology foundations',pal['rock'])
    for j,kind in enumerate(KINDS[idx]):
        compact='relay'in kind
        pad_scale=.65 if compact else 1;model_scale=1.75 if compact else 2.15
        target=desired[j%len(desired)]
        planned=LAYOUTS.get(WORLDS[idx]['id'],{}).get('buildings',[])
        if planned:
            assert planned[j]['kind']==kind,'Building plan differs from authored architecture'
            x,y=planned[j]['center_xy']
        else:
            good=[p for p in(compact_candidates if compact else candidates)if all(math.dist(p,q)>(r+8 if compact else 24)for q,r in occupied)]
            assert good,'No separated building pad '+kind+' #'+str(j)
            x,y=min(good,key=lambda p:math.dist(p,target)-.1*road_dist(p))
        h=land['height_at'];pad=8.7*pad_scale
        close=kd.find(Vector((x,y,0)))[0];yaw=planned[j]['yaw_toward_actual_route']if planned else math.atan2(close.x-x,-(close.y-y))
        # Probe the actual rotated terrace footprint, including its interior.
        # An axis-aligned box misses uphill corners after the building turns.
        ring=terrace_ring(x,y,yaw,idx*10+j,pad_scale)
        probes=[(x,y)]+ring+[((a[0]+b[0])/2,(a[1]+b[1])/2)for a,b in zip(ring,ring[1:]+ring[:1])]
        for dx in range(math.floor(min(p[0]for p in ring)),math.ceil(max(p[0]for p in ring))+1):
            for dy in range(math.floor(min(p[1]for p in ring)),math.ceil(max(p[1]for p in ring))+1):
                if inside((dx,dy),ring):probes.append((dx,dy))
        zs=[h(px,py)for px,py in probes];floor=max(zs)+.25;bottom=min(zs)-.35
        base=terrace(f'Tech_{j}_grounded_terrace',x,y,floor,bottom,yaw,foundation,col,idx*10+j,pad_scale)
        objects=tag(architecture.build_building(kind,(x,y,floor),yaw=yaw,scale=model_scale,palette={'style':pal['style']},seed=idx*10+j,collection=col))
        for obj in objects:obj['building_index']=j
        occupied.append(((x,y),9 if compact else 15));buildings.append(dict(kind=kind,position=[x,y,floor],yaw=yaw,scale=model_scale))
    def safe(p,radius):return inside(p,outline)and shore(p)>radius+1 and road_dist(p)>6+radius+1.2 and all(math.dist(p,q)>r+radius for q,r in occupied)
    landmark_kind=['forest_moss_mound','desert_fins','ice_spires','forest_moss_mound','ice_spires','tropical_cistern','storm_columns','forest_moss_mound','volcanic_vent','cloud_orrery'][idx]
    landmark_records=[]
    for j in range(2 if idx in(0,1,2,4,6,8)else 1):
        choices=[]
        for s in(2.7,2.25,1.8):
            radius=4.5*s
            for x in range(math.ceil(minx),math.floor(maxx),3):
                for y in range(math.ceil(miny),math.floor(maxy),3):
                    if safe((x,y),radius):choices.append((road_dist((x,y))+.12*shore((x,y)),x,y,s))
            if choices:break
        if not choices:break
        _,x,y,s=max(choices);z=land['height_at'](x,y)-.12
        objs=tag(scenery.make_landmark(landmark_kind,(x,y,z),scale=s,palette=pal,seed=idx*17+j,collection=col))
        occupied.append(((x,y),4.5*s+1));landmark_records.append(dict(kind=landmark_kind,position=[x,y,z],scale=s))
    print('LANDMARKS',idx+1,landmark_records,flush=True)
    tree_counts=[170,24,0,110,15,100,22,35,20,40];rock_counts=[80,100,70,50,90,55,120,30,110,55]
    tree_kind=['broadleaf','palm','shrub','autumn','pine','palm','pine','apple','bonsai','mauve'][idx]
    for category,count in [('tree',tree_counts[idx]),('rock',rock_counts[idx])]:
        made=0
        for attempt in range(count*55):
            if made>=count:break
            x,y=rng.uniform(minx,maxx),rng.uniform(miny,maxy)
            kind=tree_kind
            if category=='tree' and idx==0:kind=rng.choices(['broadleaf','pine','shrub'],[.65,.2,.15])[0]
            scale=rng.uniform(1.5,3.2)if category=='tree'else rng.uniform(2.0,4.1)
            radius=(3.0 if kind=='palm' else 2.2)*scale if category=='tree' else 2*scale
            if not safe((x,y),radius):continue
            z=land['height_at'](x,y)
            if category=='tree':objects=scenery.make_tree((x,y,z),scale=scale,palette=pal,seed=idx*1000+made,kind=kind,collection=col)
            else:objects=scenery.make_rock((x,y,z-.2),scale=scale,palette=pal,seed=idx*1000+made,kind=['slate','sandstone','slate','boulder','glacier','basalt','basalt','sandstone','basalt','boulder'][idx],collection=col)
            tag(objects);made+=1
    extras={2:('crystal',62),7:('sunflower',38),8:('crystal',15),9:('shrub',16)}
    if idx in extras:
        kind,count=extras[idx];made=0
        for attempt in range(count*50):
            if made>=count:break
            x,y=rng.uniform(minx,maxx),rng.uniform(miny,maxy);scale=rng.uniform(1.2,2.2)
            if not safe((x,y),3*scale):continue
            z=land['height_at'](x,y)
            if kind=='crystal':objs=scenery.make_crystal((x,y,z),scale=scale,palette=pal,seed=made+idx*700,collection=col)
            elif kind=='shrub':objs=scenery.make_tree((x,y,z),scale=scale,palette=pal,seed=made+idx*700,kind=kind,collection=col)
            else:objs=scenery.make_prop(kind,(x,y,z),scale=scale,palette=pal,seed=made+idx*700,collection=col)
            tag(objs);made+=1
    # Low biome-specific undergrowth varies the scale hierarchy without
    # placing a single decorative object inside the protected road corridor.
    small_kind=['fern','mushroom','mushroom','pumpkin','mushroom','fern','mushroom','wheat','mushroom','flowers'][idx]
    for j in range(85):
        for attempt in range(30):
            x,y=rng.uniform(minx,maxx),rng.uniform(miny,maxy);s=rng.uniform(.9,1.7)
            if not safe((x,y),2.0*s):continue
            tag(scenery.make_prop(small_kind,(x,y,land['height_at'](x,y)),scale=s,palette=pal,seed=idx*500+j,collection=col));break
    # Large off-path world landmark; no overhead objects exist anywhere above road.
    for lm in route_data.get('landmarks',[]):
        if lm['kind']not in('fountain','falls'):continue
        x,y=(lm['center'][0]-500)*.18,(500-lm['center'][1])*.18
        if not safe((x,y),7):continue
        if lm['kind']=='falls':kind='waterfall';z=-7.8;scale=3.0
        else:kind='cloudwell'if idx==9 else'fountain';z=land['height_at'](x,y);scale=3.2
        tag(scenery.make_prop(kind,(x,y,z),scale=scale,palette=pal,seed=idx,collection=col))
    return buildings

def cameras(scene,objects):
    bounds=[obj.matrix_world@Vector(c)for obj in objects if obj.type=='MESH'for c in obj.bound_box]
    low=Vector(tuple(min(p[i]for p in bounds)for i in range(3)));high=Vector(tuple(max(p[i]for p in bounds)for i in range(3)))
    center=(low+high)/2;span=max(high-low)*1.18;distance=span*3
    directions={'front':Vector((0,-1,0)),'right':Vector((1,0,0)),'left':Vector((-1,0,0)),'back':Vector((0,1,0)),'top':Vector((0,0,1)),'isometric':Vector((1,-1,1.05)).normalized()}
    result={}
    for view,direction in directions.items():
        data=bpy.data.cameras.new('Camera_'+view)
        types=[x.identifier for x in data.bl_rna.properties['type'].enum_items];assert'ORTHO'in types;data.type='ORTHO'
        data.ortho_scale=span*(1.12 if view=='isometric' else 1);data.clip_start=.1;data.clip_end=distance*4
        cam=bpy.data.objects.new('Camera_'+view,data);scene.collection.objects.link(cam);cam.location=center+direction*distance
        # Vertical tracking has an ambiguous roll; fix map north to image up.
        cam.rotation_euler=(0,0,0)if view=='top'else(-direction).to_track_quat('-Z','Y').to_euler();cam['v3_role']='camera';result[view]=cam
    bpy.context.view_layer.update()
    for view,cam in result.items():
        for _ in range(4):
            ndc=[world_to_camera_view(scene,cam,p)for p in bounds];ex=max(max(abs(p.x-.5),abs(p.y-.5))for p in ndc)
            if ex<=.465:break
            cam.data.ortho_scale*=ex/.45
    # True elevations and top share one pixel/world scale.
    shared=max(result[x].data.ortho_scale for x in('front','right','left','back','top'))
    for x in('front','right','left','back','top'):result[x].data.ortho_scale=shared
    return result,dict(min=list(low),max=list(high),center=list(center),shared_ortho_scale=shared)

def export_glb(path,col):
    for o in bpy.context.scene.objects:o.select_set(False)
    for o in col.objects:
        if o.type=='MESH':o.select_set(True)
    props=bpy.ops.export_scene.gltf.get_rna_type().properties
    import io_scene_gltf2
    formats=[x[0]for x in io_scene_gltf2.get_format_items(bpy.context.scene,bpy.context)];assert'GLB'in formats
    kw={'filepath':str(path),'export_format':'GLB','use_selection':True}
    for key,value in [('export_apply',True),('export_cameras',False),('export_lights',False),('export_animations',False)]:
        if key in props:kw[key]=value
    # glTF cannot represent Blender procedural noise/brick node networks.
    # Export temporary constant-color copies rather than silently white shaders.
    # The saved Blender source and every canonical material are retained.
    old_slots=[];copies={};colors=[]
    try:
        for mesh in {o.data for o in col.objects if o.type=='MESH'}:
            original=list(mesh.materials);old_slots.append((mesh,original))
            for i,mat in enumerate(original):
                if not mat:continue
                key=mat.as_pointer()
                if key not in copies:
                    flat=mat.copy();flat.name=mat.name+'_GLB_color'
                    if flat.use_nodes:
                        bs=next((n for n in flat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)
                        if bs:
                            for name in('Base Color','Normal','Roughness','Metallic'):
                                for link in list(bs.inputs[name].links):flat.node_tree.links.remove(link)
                            colors.append(list(bs.inputs['Base Color'].default_value))
                    copies[key]=flat
                mesh.materials[i]=copies[key]
        bpy.ops.export_scene.gltf(**kw)
    finally:
        for mesh,mats in old_slots:
            for i,mat in enumerate(mats):mesh.materials[i]=mat
        for flat in copies.values():bpy.data.materials.remove(flat)
    return dict(policy='constant linear base colors; procedural texture remains in BLEND',material_count=len(colors),distinct_base_colors=len({tuple(round(v,6)for v in c)for c in colors}))

def build_world(idx,size=1280,preview=False):
    started=time.monotonic();w=WORLDS[idx];ident=w['id'];print('BUILD',ident,flush=True)
    # This script is launched only with Blender --background --factory-startup.
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    for store in (bpy.data.meshes,bpy.data.materials,bpy.data.collections,bpy.data.cameras,bpy.data.lights):
        for data in list(store):store.remove(data)
    scene=bpy.context.scene;configure(scene,size);col=bpy.data.collections.new(ident);scene.collection.children.link(col)
    route_file=ROOT/'Source/Routes'/(ident+'.json')
    if not route_file.exists():route_file=ROOT.parent/'V2/RouteGuidesClosed'/ident/'route.json'
    route_data=json.loads(route_file.read_text(encoding='utf8'))
    land=terrain_road.build_landscape(route_data,PALETTES[idx],collection=col,seed=idx+27,world_scale=.18,road_width=8,shoulder=2,world_id=ident)
    buildings=populate(idx,col,land,route_data);shader_polish();bpy.context.view_layer.update()
    cams,bounds=cameras(scene,list(col.objects));before=validate_scene.geometry_snapshot(scene);mat_before=visual_signature()
    out=ROOT/('Preview'if preview else'Output')/ident;out.mkdir(parents=True,exist_ok=True)
    pre=validate_scene.validate(scene,land['route_points'],road_width=8,shoulder=2,ceiling_height=5,route_closed=True,camera_views=[validate_scene.camera_snapshot(cam,view)for view,cam in cams.items()],require_render_evidence=False,sample_step=.6)
    (out/'preflight.json').write_text(json.dumps(pre,indent=2),encoding='utf8')
    if not pre['pass']:
        print('PREFLIGHT FAILED',[(c['name'],c)for c in pre['checks']if not c['pass']],flush=True);raise RuntimeError('Geometry audit failed '+ident)
    records=[];snapshots=[]
    for view,cam in cams.items():
        if preview and view!='isometric':continue
        scene.camera=cam;bpy.context.view_layer.update();record=validate_scene.camera_snapshot(cam,view)
        record['geometry_sha256']=before['sha256'];record['material_sha256']=mat_before
        record['matrix_world']=[list(row)for row in cam.matrix_world]
        scene.render.filepath=str(out/(view+'.png'));bpy.ops.render.render(write_still=True)
        snapshots.append(validate_scene.geometry_snapshot(scene));records.append(record);print('RENDERED',ident,view,flush=True)
    after=validate_scene.geometry_snapshot(scene);assert mat_before==visual_signature(),'Material changed between views'
    validation=validate_scene.validate(scene,land['route_points'],road_width=8,shoulder=2,ceiling_height=5,route_closed=True,geometry_before=before,geometry_after=after,geometry_per_view=snapshots,camera_views=records,require_render_evidence=not preview,sample_step=.6)
    (out/'validation.json').write_text(json.dumps(validation,ensure_ascii=False,indent=2),encoding='utf8')
    if not validation['pass']:raise RuntimeError('Final geometry/render audit failed '+ident)
    scene.camera=cams['isometric'];bpy.ops.wm.save_as_mainfile(filepath=str(out/'island.blend'))
    export_glb(out/'island.glb',col)
    manifest=dict(id=ident,title=w['title'],source='canonical Blender mesh rendered without generative image edits',blender_version=bpy.app.version_string,geometry_sha256=before['sha256'],material_sha256=mat_before,camera_views=records,bounds=bounds,palette=PALETTES[idx],buildings=buildings,road=dict(width=8,shoulder=2,z=0,closed=True,points=land['route_points'],bridge_intervals=land['bridge_intervals']),geometry_before=before,geometry_after=after,render_seconds=round(time.monotonic()-started,2),unity_playtest=False)
    (out/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8');print('COMPLETE',ident,manifest['render_seconds'],flush=True)

if __name__=='__main__':
    args=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[]
    p=argparse.ArgumentParser();p.add_argument('--world',default='all');p.add_argument('--size',type=int,default=1280);p.add_argument('--preview',action='store_true');a=p.parse_args(args)
    selectors=a.world.split(',')
    chosen=range(10)if a.world=='all'else[i for i,w in enumerate(WORLDS)if any(w['id']==s or w['id'].startswith(s)for s in selectors)]
    for idx in chosen:build_world(idx,a.size,a.preview)
