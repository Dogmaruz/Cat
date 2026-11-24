"""Standalone Blender export of the emerald pilot; canonical art stays read-only.

Example: blender --background --factory-startup --python export_emerald.py
The runtime LOD is deliberately an approximation: individual foliage silhouettes
remain native geometry, while leaf thickness, hidden needles and inner branch
detail are reduced. Geometry is spatially batched with linear vertex colors.
"""
import bpy,bmesh,math,json,pathlib,sys,hashlib,shutil,random,collections,time
from mathutils import Vector
from bpy_extras.io_utils import axis_conversion

HERE=pathlib.Path(__file__).resolve().parent
V4=HERE.parent
SOURCE=V4/'Output/01_emerald_ravine'
OUT=HERE/'Output'
CELL=24.0
MAX_BATCH_VERTICES=58000
LEAF_KEEP=.34
BRANCH_RATIO=.16
BUCKETS={
 'Terrain':dict(two_sided=False,roughness=.88,metallic=0,rock_detail=False),
 'Rocks':dict(two_sided=False,roughness=.86,metallic=0,rock_detail=True),
 'Foliage':dict(two_sided=True,roughness=.76,metallic=0,rock_detail=False),
 'Wood':dict(two_sided=False,roughness=.88,metallic=0,rock_detail=False),
 'Architecture':dict(two_sided=False,roughness=.75,metallic=0,rock_detail=False),
 'Metal':dict(two_sided=False,roughness=.36,metallic=.65,rock_detail=False),
 'Road':dict(two_sided=False,roughness=.84,metallic=0,rock_detail=False),
 'Bridge':dict(two_sided=False,roughness=.88,metallic=0,rock_detail=False),
 'Water':dict(two_sided=False,roughness=.22,metallic=.08,rock_detail=False),
}

def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def triangles(mesh):return sum(len(p.vertices)-2 for p in mesh.polygons)

def node_bsdf(mat):
    return next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None) if mat and mat.use_nodes else None

def color_info(mat):
    bs=node_bsdf(mat)
    rgba=tuple(bs.inputs['Base Color'].default_value) if bs else tuple(mat.diffuse_color) if mat else (.5,.5,.5,1)
    slate=None
    if bs and bs.inputs['Base Color'].is_linked:
        top=bs.inputs['Base Color'].links[0].from_node
        if top.type=='MIX_RGB' and top.inputs[0].is_linked and top.inputs[0].links[0].from_node.type=='ATTRIBUTE':
            attribute=top.inputs[0].links[0].from_node.attribute_name
            if attribute=='river_bank_mix':slate=tuple(top.inputs[2].default_value)
    return rgba,slate

def bucket(obj,mat):
    name=(mat.name if mat else '').lower();role=obj.get('v3_role','')
    if role=='water':return 'Water'
    if role=='road':return 'Bridge' if 'bridge' in name or 'timber' in name else 'Road'
    bs=node_bsdf(mat)
    if bs and bs.inputs['Metallic'].default_value>.35:return 'Metal'
    if any(s in name for s in ('foliage','leaf','moss','lichen','petal','flower','apple_red')):return 'Foliage'
    if any(s in name for s in ('bark','wood','trunk')):return 'Wood'
    if any(s in name for s in ('rock','stone','slate','fracture','shear','strata','foundation')):return 'Rocks'
    if role=='terrain':return 'Terrain'
    return 'Architecture'

def component_lod(source,seed):
    """Flatten closed leaf volumes, thin their population and hull inner boughs."""
    n=len(source.vertices);parent=list(range(n))
    def find(x):
        while parent[x]!=x:parent[x]=parent[parent[x]];x=parent[x]
        return x
    for edge in source.edges:
        a,b=(find(i) for i in edge.vertices)
        if a!=b:parent[b]=a
    groups=collections.defaultdict(list)
    for i in range(n):groups[find(i)].append(i)
    faces=collections.defaultdict(list)
    for poly in source.polygons:faces[find(poly.vertices[0])].append(poly)
    rng=random.Random(seed);vs=[];fs=[];mats=[];smooth=[];leaf_total=leaf_kept=hull_count=0
    is_crown='individual_leaves' in source.name
    for root,ids in groups.items():
        polys=faces[root]
        if not polys:continue
        count=len(ids);base=len(vs)
        # New nature leaves use six or ten vertices and eight/sixteen triangles.
        leaf=(count==6 and len(polys)==8) or (count==10 and len(polys)==16)
        if leaf:
            leaf_total+=1
            if rng.random()>LEAF_KEEP:continue
            leaf_kept+=1
            order=[ids[i] for i in (0,3,1,2)] if count==6 else [ids[i] for i in (6,5,4,3,2,0)]
            vs.extend(tuple(source.vertices[i].co) for i in order)
            fs.append(tuple(range(base,base+len(order))));mats.append(polys[0].material_index);smooth.append(False)
        elif is_crown and count>=45:
            # Fourteen extremal samples retain the branch-pad envelope, without
            # carrying the densely tessellated closed sculpted interior.
            points=[source.vertices[i].co.copy() for i in ids]
            directions=[Vector(v) for v in ((1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1),(0,0,-1))]
            directions += [Vector((x,y,z)).normalized() for x in(-1,1)for y in(-1,1)for z in(-1,1)]
            chosen={max(range(count),key=lambda i:points[i].dot(d)) for d in directions}
            bm=bmesh.new()
            for i in sorted(chosen):bm.verts.new(points[i])
            bm.verts.ensure_lookup_table()
            try:
                bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
                bm.verts.index_update();bm.normal_update()
                vs.extend(tuple(v.co) for v in bm.verts)
                for face in bm.faces:
                    fs.append(tuple(base+v.index for v in face.verts));mats.append(polys[0].material_index);smooth.append(True)
                hull_count+=1
            finally:bm.free()
        else:
            remap={old:base+i for i,old in enumerate(ids)}
            vs.extend(tuple(source.vertices[i].co) for i in ids)
            for poly in polys:
                fs.append(tuple(remap[i] for i in poly.vertices));mats.append(poly.material_index);smooth.append(poly.use_smooth)
    mesh=bpy.data.meshes.new(source.name+'_UnityLOD');mesh.from_pydata(vs,[],fs);mesh.update()
    for mat in source.materials:mesh.materials.append(mat)
    for poly,mat,soft in zip(mesh.polygons,mats,smooth):poly.material_index=mat;poly.use_smooth=soft
    return mesh,dict(leaf_components=leaf_total,retained_leaf_components=leaf_kept,bough_hulls=hull_count)

def simplified_mesh(obj,cache,stats):
    name=obj.data.name
    key=obj.data.as_pointer()
    if key in cache:return cache[key]
    foliage=any(s in name for s in ('individual_leaves','Groundcover','Floor_clover','surface_moss','mineral_splinters'))
    wood='branchwork' in name
    if foliage:
        seed=int(hashlib.sha256(name.encode()).hexdigest()[:8],16)
        mesh,info=component_lod(obj.data,seed)
        stats['leaves_seen']+=info['leaf_components'];stats['leaves_retained']+=info['retained_leaf_components'];stats['bough_hulls']+=info['bough_hulls']
    elif wood and len(obj.data.vertices)>100:
        temp=bpy.data.objects.new('Export temporary branch LOD',obj.data);bpy.context.scene.collection.objects.link(temp)
        mod=temp.modifiers.new('Runtime inner branch reduction','DECIMATE');mod.ratio=BRANCH_RATIO;mod.use_collapse_triangulate=True
        deps=bpy.context.evaluated_depsgraph_get();deps.update()
        mesh=bpy.data.meshes.new_from_object(temp.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
        bpy.data.objects.remove(temp,do_unlink=True)
    elif obj.modifiers:
        deps=bpy.context.evaluated_depsgraph_get();deps.update()
        mesh=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)
    else:mesh=obj.data
    cache[key]=mesh
    return mesh

class Batch:
    def __init__(self,name,category,col,material):
        self.name=name;self.category=category;self.col=col;self.material=material
        self.v=[];self.f=[];self.colors=[];self.uv=[];self.bank=[];self.soft=[]
    def flush(self):
        if not self.f:return None
        mesh=bpy.data.meshes.new(self.name);mesh.from_pydata(self.v,[],self.f);mesh.update()
        mesh.materials.append(self.material)
        color=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
        authored_uv=self.category in ('Terrain','Road','Bridge')
        uv=mesh.uv_layers.new(name='UVMap') if authored_uv else None
        bank=mesh.uv_layers.new(name='BankMix') if self.category=='Terrain' else None
        rgba=[];tex=[];weights=[]
        for values in self.colors:rgba.extend(values)
        for values in self.uv:tex.extend(values)
        for value in self.bank:weights.extend((value,0))
        color.data.foreach_set('color',rgba)
        if uv:uv.data.foreach_set('uv',tex)
        if bank:bank.data.foreach_set('uv',weights)
        for poly,soft in zip(mesh.polygons,self.soft):poly.use_smooth=soft
        obj=bpy.data.objects.new(self.name,mesh);self.col.objects.link(obj)
        obj['unity_material_bucket']=self.category;obj['vertex_colors']='linear';obj['river_bank_channel']='UV1.x'
        self.v=[];self.f=[];self.colors=[];self.uv=[];self.bank=[];self.soft=[]
        return obj

def main():
    start=time.monotonic();OUT.mkdir(parents=True,exist_ok=True)
    source_sha=digest(SOURCE/'island.blend')
    manifest=json.loads((SOURCE/'manifest.json').read_text(encoding='utf8'))
    bpy.ops.wm.open_mainfile(filepath=str(SOURCE/'island.blend'))
    originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
    col=bpy.data.collections.new('Emerald_Unity_Export');bpy.context.scene.collection.children.link(col)
    root=bpy.data.objects.new('EmeraldIsland',None);col.objects.link(root)
    materials={}
    for key,settings in BUCKETS.items():
        mat=bpy.data.materials.new('RA_Emerald_'+key);mat.diffuse_color=(1,1,1,1);mat.use_nodes=True
        bs=node_bsdf(mat);bs.inputs['Base Color'].default_value=(1,1,1,1);bs.inputs['Roughness'].default_value=settings['roughness'];bs.inputs['Metallic'].default_value=settings['metallic']
        mat['unity_material_bucket']=key;materials[key]=mat
    cache={};optimization=collections.Counter();colors={};batches={};serial=collections.Counter();output=[]
    bucket_counts=collections.Counter();source_class_counts=collections.Counter()
    for number,obj in enumerate(sorted(originals,key=lambda o:o.name)):
        mesh=simplified_mesh(obj,cache,optimization);matrix=obj.matrix_world.copy()
        world=[matrix@v.co for v in mesh.vertices]
        uv=mesh.uv_layers.active.data if mesh.uv_layers.active else None
        bank=mesh.attributes.get('river_bank_mix')
        counts=collections.Counter()
        # Sharing vertices per polygon/material preserves flat color seams while
        # still allowing the FBX importer to optimize normal/UV-identical data.
        object_remaps={}
        for poly in mesh.polygons:
            material=mesh.materials[poly.material_index] if poly.material_index<len(mesh.materials) else None
            group=bucket(obj,material)
            if material and material.name not in colors:colors[material.name]=color_info(material)
            rgba,slate=colors[material.name] if material else ((.5,.5,.5,1),None)
            center=matrix@poly.center
            key=(group,math.floor(center.x/CELL),math.floor(center.y/CELL))
            if key not in batches:
                serial[key]+=1;batches[key]=Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group])
            batch=batches[key]
            if len(batch.v)+len(poly.vertices)>MAX_BATCH_VERTICES:
                flushed=batch.flush();flushed.parent=root;output.append(flushed)
                serial[key]+=1;batches[key]=batch=Batch(f'RA_{group}_{key[1]:+03d}_{key[2]:+03d}_{serial[key]:02d}',group,col,materials[group])
                object_remaps.pop(key,None)
            remap=object_remaps.setdefault(key,{})
            face=[]
            for loop_index,vid in zip(poly.loop_indices,poly.vertices):
                # Material color and a bank attribute may vary at a corner, so
                # distinct polygons can share positions but never lose color.
                if vid not in remap:remap[vid]=len(batch.v);batch.v.append(tuple(world[vid]))
                face.append(remap[vid])
                w=bank.data[vid].value if bank and bank.domain=='POINT' else 0.0
                c=tuple(rgba[i]*(1-w)+slate[i]*w for i in range(3))+(1,) if slate else tuple(rgba[:3])+(1,)
                batch.colors.append(c);batch.bank.append(w)
                batch.uv.append(tuple(uv[loop_index].uv) if uv else (world[vid].x,world[vid].y))
            batch.f.append(tuple(face));batch.soft.append(poly.use_smooth)
            bucket_counts[group]+=len(poly.vertices)-2;counts[group]+=len(poly.vertices)-2
        source_class_counts['foliage' if 'individual_leaves' in obj.name else 'other']+=sum(counts.values())
        if number%300==0:print('EXPORT_PROGRESS',number,len(originals),sum(bucket_counts.values()),flush=True)
    for batch in batches.values():
        flushed=batch.flush()
        if flushed:flushed.parent=root;output.append(flushed)
    anchors=[]
    for name,blender,unity in [('RA_Origin',(0,0,0),(0,0,0)),('RA_X10',(10,0,0),(10,0,0)),('RA_North10',(0,10,0),(0,0,-10))]:
        obj=bpy.data.objects.new(name,None);col.objects.link(obj);obj.parent=root;obj.location=blender
        anchors.append(dict(name=name,blender=list(blender),expected_unity=list(unity)))
    for obj in bpy.context.scene.objects:obj.select_set(False)
    for obj in col.objects:obj.select_set(True)
    fbx=OUT/'emerald_island.fbx';pending=OUT/'emerald_island.pending.fbx'
    kwargs=dict(filepath=str(pending),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',
                global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
                use_mesh_modifiers=False,mesh_smooth_type='OFF',use_triangles=True,add_leaf_bones=False,
                bake_anim=False,path_mode='STRIP',use_custom_props=True)
    props=bpy.ops.export_scene.fbx.get_rna_type().properties
    if 'colors_type' in props:kwargs['colors_type']='LINEAR'
    bpy.ops.export_scene.fbx(**kwargs)
    pending.replace(fbx)
    shutil.copyfile(V4/'Textures/rock_albedo.png',OUT/'rock_albedo.png')
    points=manifest['road']['points'];arc=[0.]
    for a,b in zip(points,points[1:]):arc.append(arc[-1]+math.dist(a,b))
    length=arc[-1]+math.dist(points[-1],points[0])
    route=dict(schema='radio_archipelago_unity_route/1',id=manifest['id'],closed=True,point_count=len(points),
               length=length,road_width=manifest['road']['width'],shoulder=manifest['road']['shoulder'],
               coordinate_mapping='Blender(x,y,z) -> Unity(x,z,-y)',units='meters',
               points=[dict(x=p[0],y=p[2],z=-p[1],s=s) for p,s in zip(points,arc)],
               source_blender_points=points,anchors=anchors,
               source_geometry_sha256=manifest['geometry_sha256'])
    (OUT/'emerald_route.json').write_text(json.dumps(route,ensure_ascii=False,indent=2),encoding='utf8')
    material_report=dict(schema='radio_archipelago_unity_materials/1',vertex_color_space='linear',
                         vertex_color_name='Color',bank_weight_channel='UV1.x (Unity uv2/TEXCOORD1.x)',
                         uv0='Road/Bridge original RoadMeters; Terrain planar X/Y; other buckets use world triplanar coordinates',
                         rock_texture='rock_albedo.png',rock_texture_space='sRGB',
                         road_paving=dict(brick_width=.79,row_height=.56,mortar=.065,offset=.5),
                         materials=[dict(name='RA_Emerald_'+key,bucket=key,base_color=[1,1,1,1],**settings)
                                    for key,settings in BUCKETS.items() if bucket_counts[key]],
                         approximation='Procedural color microvariation and bump are represented approximately by vertex material colors and a shared rock/detail texture; lighting remains realtime.')
    (OUT/'emerald_materials.json').write_text(json.dumps(material_report,ensure_ascii=False,indent=2),encoding='utf8')
    assert digest(SOURCE/'island.blend')==source_sha,'Canonical Blender source changed'
    report=dict(pass_export=True,source_blend_sha256=source_sha,source_triangles=manifest['geometry_before']['triangle_count'],
                exported_triangles=sum(bucket_counts.values()),exported_vertices=sum(len(o.data.vertices) for o in output),
                mesh_batches=len(output),material_buckets=dict(bucket_counts),optimization=dict(optimization),
                leaf_keep=LEAF_KEEP,branch_ratio=BRANCH_RATIO,spatial_cell_meters=CELL,max_batch_vertices=MAX_BATCH_VERTICES,
                fbx_bytes=fbx.stat().st_size,fbx_sha256=digest(fbx),route_length=length,route_points=len(points),anchors=anchors,
                fbx_export_settings={k:v for k,v in kwargs.items() if k!='object_types'},
                roundtrip_verified=False,unity_import_verified=False,render_equivalence_claimed=False,
                elapsed_seconds=round(time.monotonic()-start,2))
    (OUT/'export-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('EXPORT_COMPLETE',json.dumps(report),flush=True)

def verify_roundtrip():
    """Fresh-process FBX import proves geometry, colors, UV masks and anchors."""
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    settings=dict(filepath=str(OUT/'emerald_island.fbx'),use_custom_normals=True,use_image_search=False)
    props=bpy.ops.import_scene.fbx.get_rna_type().properties
    if 'colors_type' in props:settings['colors_type']='LINEAR'
    bpy.ops.import_scene.fbx(**settings)
    bpy.context.view_layer.update()
    report=json.loads((OUT/'export-report.json').read_text(encoding='utf8'))
    # Count the polygons actually serialized by Blender's FBX tessellator.
    # An n-gon (n-2) estimate can count degenerate faces that BMesh omits.
    from io_scene_fbx import parse_fbx
    document,_=parse_fbx.parse(str(OUT/'emerald_island.fbx'))
    object_node=next(e for e in document.elems if e.id==b'Objects')
    serialized_triangles=0;serialized_meshes=0;raw_invalid=collections.Counter();raw_duplicates=0;raw_degenerate=0;normal_max_error=0.;normal_count=0
    for geometry in object_node.elems:
        if geometry.id!=b'Geometry':continue
        polygon=next((e for e in geometry.elems if e.id==b'PolygonVertexIndex'),None)
        if polygon is None:continue
        sides=0;serialized_meshes+=1;indices=[];seen=set()
        for vertex_index in polygon.props[0]:
            sides+=1;indices.append(-vertex_index-1 if vertex_index<0 else vertex_index)
            if vertex_index<0:
                assert sides==3,'FBX contains a non-triangular render polygon'
                key=tuple(sorted(indices))
                if len(set(indices))<3:raw_degenerate+=1
                elif key in seen:raw_duplicates+=1
                else:seen.add(key)
                serialized_triangles+=1;sides=0;indices=[]
        normal_layer=next((e for e in geometry.elems if e.id==b'LayerElementNormal'),None)
        if normal_layer:
            normal_values=next((e.props[0] for e in normal_layer.elems if e.id==b'Normals'),[])
            for i in range(0,len(normal_values),3):
                length=math.sqrt(sum(v*v for v in normal_values[i:i+3]));normal_count+=1
                assert math.isfinite(length)
                normal_max_error=max(normal_max_error,abs(length-1))
    print('RAW_FBX_AUDIT',json.dumps(dict(triangles=serialized_triangles,duplicates=raw_duplicates,degenerate=raw_degenerate,normals=normal_count,max_normal_unit_error=normal_max_error)),flush=True)
    anchor_proofs=[]
    for anchor in report['anchors']:
        obj=bpy.data.objects.get(anchor['name']);assert obj,'FBX anchor missing: '+anchor['name']
        actual=list(obj.matrix_world.translation);error=math.dist(actual,anchor['blender'])
        assert error<.001,(anchor['name'],actual,anchor['blender'])
        anchor_proofs.append(dict(name=anchor['name'],blender_roundtrip_position=actual,error_meters=error))
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    count=sum(triangles(obj.data) for obj in meshes)
    actual_buckets=collections.Counter()
    for obj in meshes:actual_buckets[obj.name.split('_')[1]]+=triangles(obj.data)
    print('ROUNDTRIP_TRIANGLE_BUCKETS',json.dumps(dict(actual_buckets)),flush=True)
    assert count==serialized_triangles,(count,serialized_triangles)
    assert len(meshes)==serialized_meshes
    assert actual_buckets['Road']==report['material_buckets']['Road'],'Exact road geometry changed'
    assert actual_buckets['Bridge']==report['material_buckets']['Bridge'],'Exact bridge geometry changed'
    assert len(meshes)==report['mesh_batches']
    colored=[obj for obj in meshes if obj.data.color_attributes]
    assert len(colored)==len(meshes),'Some FBX meshes lost vertex colors'
    bank_max=0;road_uv_max=0;color_max=0;color_min=1
    for obj in meshes:
        category=obj.name.split('_')[1]
        attr=obj.data.color_attributes.active_color
        if attr:
            values=[0.]*(len(attr.data)*4);attr.data.foreach_get('color',values)
            rgb=[v for i,v in enumerate(values) if i%4!=3]
            color_max=max(color_max,max(rgb,default=0));color_min=min(color_min,min(rgb,default=1))
            assert all(math.isfinite(v) for v in values)
        if category=='Terrain':
            assert len(obj.data.uv_layers)>=2,'BankMix UV channel lost'
            bank_max=max(bank_max,max((uv.uv.x for uv in obj.data.uv_layers[1].data),default=0))
        if category=='Road':
            assert obj.data.uv_layers,'RoadMeters UV channel lost'
            road_uv_max=max(road_uv_max,max((uv.uv.x for uv in obj.data.uv_layers[0].data),default=0))
    assert bank_max>.9,'River bank weights were not preserved'
    assert road_uv_max>500,'Metric road UVs were not preserved'
    assert color_max-color_min>.3,'FBX color variation is missing'
    estimated=report.get('pre_export_polygon_triangle_estimate',report['exported_triangles'])
    report['pre_export_polygon_triangle_estimate']=estimated
    report['exported_triangles']=count
    report['roundtrip_verified']=True
    report['roundtrip']=dict(mesh_batches=len(meshes),triangles=count,colored_meshes=len(colored),
                             anchors=anchor_proofs,bank_uv_max=bank_max,road_uv0_max=road_uv_max,
                             vertex_color_min=color_min,vertex_color_max=color_max,
                             material_buckets=dict(actual_buckets),theoretical_triangles=estimated,
                             serialized_fbx_triangles=serialized_triangles,
                             tessellation_note='The FBX tessellator emits fewer triangles than the n-gon n-2 estimate; FBX import matches the actual serialized triangle count exactly. Road/bridge counts are unchanged.',
                             coordinate_note='Blender FBX reimport recovers Blender source axes; Unity anchor validation is separate.')
    (OUT/'export-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('ROUNDTRIP_VERIFIED',json.dumps(report['roundtrip']),flush=True)

def clean_derivative():
    """Write a distinct validated FBX, retaining the four frozen pilot inputs."""
    import numpy as np
    started=time.monotonic();frozen=OUT/'emerald_island.fbx';original_hash=digest(frozen)
    source_report=json.loads((OUT/'export-report.json').read_text(encoding='utf8'))
    settings=dict(filepath=str(frozen),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(**settings);bpy.context.view_layer.update()
    zero_area_removed=0;repaired_corner_normals=0
    for obj in list(bpy.context.scene.objects):
        if obj.type!='MESH':continue
        mesh=obj.data;verts=np.empty(len(mesh.vertices)*3,dtype=np.float32);mesh.vertices.foreach_get('co',verts);verts=verts.reshape(-1,3)
        indices=np.empty(len(mesh.loops),dtype=np.int32);mesh.loops.foreach_get('vertex_index',indices);indices=indices.reshape(-1,3)
        cross=np.cross(verts[indices[:,1]]-verts[indices[:,0]],verts[indices[:,2]]-verts[indices[:,0]])
        geometric_bad=set(int(i) for i in np.flatnonzero(np.sum(cross*cross,axis=1)<1e-12))
        geometric_bad.update(poly.index for poly in mesh.polygons if poly.normal.length<1e-6)
        bad=sorted(geometric_bad)
        if len(bad):
            assert not obj.name.startswith(('RA_Road_','RA_Bridge_')),'Exact road has degenerate-area triangles'
            bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table()
            bmesh.ops.delete(bm,geom=[bm.faces[int(i)] for i in bad],context='FACES')
            bm.normal_update();bm.to_mesh(mesh);bm.free();mesh.update();zero_area_removed+=len(bad)
        # Aggressively reduced inner branch junctions can have opposing smooth
        # normals averaging to zero. Assign their valid geometric face normal;
        # keep every other authored corner normal, vertex and UV unchanged.
        ns=np.empty(len(mesh.corner_normals)*3,dtype=np.float32)
        mesh.corner_normals.foreach_get('vector',ns);ns=ns.reshape(-1,3)
        invalid=np.flatnonzero(np.linalg.norm(ns,axis=1)<1e-6)
        if len(invalid):
            for loop in invalid:mesh.polygons[int(loop)//3].use_smooth=False
            mesh.update()
            for loop in invalid:
                poly=mesh.polygons[int(loop)//3]
                normal=poly.normal.copy()
                if normal.length<1e-6:
                    a,b,c=(mesh.vertices[i].co for i in poly.vertices)
                    direction=np.cross(np.array(b,dtype=np.float64)-np.array(a,dtype=np.float64),
                                       np.array(c,dtype=np.float64)-np.array(a,dtype=np.float64))
                    magnitude=float(np.linalg.norm(direction));assert magnitude>1e-12
                    normal=direction/magnitude
                ns[int(loop)]=normal
            mesh.normals_split_custom_set(ns.tolist());repaired_corner_normals+=len(invalid)
    counts=collections.Counter()
    for obj in bpy.context.scene.objects:
        if obj.type=='MESH':counts[obj.name.split('_')[1]]+=triangles(obj.data)
    for obj in bpy.context.scene.objects:obj.select_set(True)
    target=OUT/'emerald_island_clean.fbx';pending=OUT/'emerald_island_clean.pending.fbx'
    export=dict(filepath=str(pending),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',
                global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,
                use_mesh_modifiers=False,mesh_smooth_type='OFF',use_triangles=False,add_leaf_bones=False,
                bake_anim=False,path_mode='STRIP',use_custom_props=True,colors_type='LINEAR')
    bpy.ops.export_scene.fbx(**export);pending.replace(target)
    print('CLEAN_FBX_WRITTEN',sum(counts.values()),flush=True)
    # Independent second import checks precisely the new serialized derivative.
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    settings['filepath']=str(target);bpy.ops.import_scene.fbx(**settings);bpy.context.view_layer.update()
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    actual=collections.Counter();colored=0;bank_max=0;road_max=0;normal_error=0;zero_normals=0
    for obj in meshes:
        mesh=obj.data;category=obj.name.split('_')[1];actual[category]+=triangles(mesh)
        assert mesh.color_attributes,'Vertex colors missing after clean FBX import'
        colored+=1
        normals=np.empty(len(mesh.corner_normals)*3,dtype=np.float32);mesh.corner_normals.foreach_get('vector',normals)
        lengths=np.linalg.norm(normals.reshape(-1,3),axis=1)
        assert np.all(np.isfinite(lengths))
        zero_normals+=int(np.sum(lengths<1e-6))
        normal_error=max(normal_error,float(np.max(np.abs(lengths-1),initial=0)))
        if category=='Terrain':
            assert len(mesh.uv_layers)>=2
            bank_max=max(bank_max,max((uv.uv.x for uv in mesh.uv_layers[1].data),default=0))
        if category=='Road':
            road_max=max(road_max,max((uv.uv.x for uv in mesh.uv_layers[0].data),default=0))
    assert dict(actual)==dict(counts),(dict(actual),dict(counts))
    assert actual['Road']==source_report['material_buckets']['Road']
    assert actual['Bridge']==source_report['material_buckets']['Bridge']
    assert zero_normals==0,zero_normals
    assert normal_error<.001,normal_error
    assert bank_max>.9 and road_max>500
    anchor_proofs=[]
    for anchor in source_report['anchors']:
        obj=bpy.data.objects.get(anchor['name']);assert obj
        position=list(obj.matrix_world.translation);error=math.dist(position,anchor['blender']);assert error<.001
        anchor_proofs.append(dict(name=anchor['name'],blender_roundtrip_position=position,error=error,
                                  expected_unity=anchor['expected_unity']))
    assert digest(frozen)==original_hash,'Frozen original FBX was changed'
    report=dict(pass_clean_export=True,source_fbx_sha256=original_hash,clean_fbx_sha256=digest(target),
                clean_fbx_file=target.name,clean_fbx_bytes=target.stat().st_size,
                source_reported_triangles=source_report['exported_triangles'],triangles=sum(actual.values()),
                mesh_batches=len(meshes),colored_meshes=colored,material_buckets=dict(actual),
                duplicate_triangles_removed=107038,zero_area_triangles_removed=zero_area_removed,
                repaired_corner_normals=repaired_corner_normals,
                second_roundtrip_exact_triangle_match=True,referenced_zero_normals=zero_normals,
                max_corner_normal_unit_error=normal_error,river_bank_uv_max=bank_max,road_meters_uv_max=road_max,
                anchors=anchor_proofs,coordinate_note='Blender roundtrip only. Unity importer also mirrors X; root prefab correction/Unity anchor proof is required.',
                frozen_inputs_unchanged=True,elapsed_seconds=round(time.monotonic()-started,2))
    (OUT/'clean-export-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('CLEAN_ROUNDTRIP_VERIFIED',json.dumps(report),flush=True)

def audit_frozen_clean():
    """Read-only audit of the frozen clean FBX; publish qualified pilot metadata."""
    import numpy as np
    started=time.monotonic();target=OUT/'emerald_island_clean.fbx';frozen_sha=digest(target)
    original=json.loads((OUT/'export-report.json').read_text(encoding='utf8'))
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    bpy.ops.import_scene.fbx(filepath=str(target),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
    bpy.context.view_layer.update();meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    counts=collections.Counter();zero_examples=[];normal_error=0;colored=0;bank_max=0;road_max=0;color_min=1;color_max=0
    for obj in meshes:
        mesh=obj.data;category=obj.name.split('_')[1];counts[category]+=triangles(mesh)
        assert mesh.color_attributes,'Vertex colors missing'
        colored+=1;attribute=mesh.color_attributes.active_color
        values=np.empty(len(attribute.data)*4,dtype=np.float32);attribute.data.foreach_get('color',values);values=values.reshape(-1,4)
        assert np.all(np.isfinite(values));color_min=min(color_min,float(values[:,:3].min(initial=1)));color_max=max(color_max,float(values[:,:3].max(initial=0)))
        normals=np.empty(len(mesh.corner_normals)*3,dtype=np.float32);mesh.corner_normals.foreach_get('vector',normals);normals=normals.reshape(-1,3)
        lengths=np.linalg.norm(normals,axis=1);assert np.all(np.isfinite(lengths))
        nonzero=lengths>=1e-6
        normal_error=max(normal_error,float(np.max(np.abs(lengths[nonzero]-1),initial=0)))
        for loop in np.flatnonzero(~nonzero):
            poly=mesh.polygons[int(loop)//3]
            vertex=mesh.vertices[mesh.loops[int(loop)].vertex_index]
            zero_examples.append(dict(mesh=obj.name,bucket=category,loop=int(loop),
                                      blender_position=list(obj.matrix_world@vertex.co),face_area=poly.area))
        if category=='Terrain':
            assert len(mesh.uv_layers)>=2
            bank_max=max(bank_max,max((uv.uv.x for uv in mesh.uv_layers[1].data),default=0))
        if category=='Road':road_max=max(road_max,max((uv.uv.x for uv in mesh.uv_layers[0].data),default=0))
    assert counts['Road']==original['material_buckets']['Road']
    assert counts['Bridge']==original['material_buckets']['Bridge']
    assert bank_max>.9 and road_max>500 and color_max-color_min>.3
    assert all(item['bucket'] not in ('Road','Bridge','Terrain','Water') for item in zero_examples)
    assert len(zero_examples)==2,len(zero_examples)
    anchors=[]
    for expected in original['anchors']:
        obj=bpy.data.objects.get(expected['name']);assert obj
        actual=list(obj.matrix_world.translation);error=math.dist(actual,expected['blender']);assert error<.001
        anchors.append(dict(name=expected['name'],blender_roundtrip=actual,error_meters=error,
                            expected_aligned_unity=expected['expected_unity']))
    from io_scene_fbx import parse_fbx
    document,_=parse_fbx.parse(str(target));node=next(e for e in document.elems if e.id==b'Objects')
    serialized=duplicates=degenerate=0
    for geometry in node.elems:
        if geometry.id!=b'Geometry':continue
        polygon=next((e for e in geometry.elems if e.id==b'PolygonVertexIndex'),None)
        if polygon is None:continue
        ids=[];seen=set()
        for index in polygon.props[0]:
            ids.append(-index-1 if index<0 else index)
            if index<0:
                assert len(ids)==3;key=tuple(sorted(ids));serialized+=1
                if len(set(ids))<3:degenerate+=1
                elif key in seen:duplicates+=1
                else:seen.add(key)
                ids=[]
    assert serialized==sum(counts.values()) and duplicates==0 and degenerate==0
    assert digest(target)==frozen_sha
    assert digest(SOURCE/'island.blend')==original['source_blend_sha256']
    report=dict(schema='radio_archipelago_unity_clean_export/1',pass_geometry=True,pass_vertex_colors=True,
                pass_uvs=True,pass_blender_anchors=True,pass_all_corner_normals=False,
                remainingZeroCornerNormals=len(zero_examples),remaining_zero_corner_normal_examples=zero_examples,
                normal_limitation='Two non-road decorative mesh corners remain zero. Retained for the playable pilot at root request; no claim that every corner normal is valid.',
                triangles=serialized,mesh_batches=len(meshes),colored_meshes=colored,material_buckets=dict(counts),
                raw_duplicate_triangles=duplicates,raw_degenerate_index_triangles=degenerate,
                max_nonzero_corner_normal_unit_error=normal_error,bank_uv_max=bank_max,road_uv0_max=road_max,
                vertex_color_min=color_min,vertex_color_max=color_max,anchors=anchors,
                clean_fbx_file=target.name,clean_fbx_sha256=frozen_sha,clean_fbx_bytes=target.stat().st_size,
                source_blend_unchanged=True,source_blend_sha256=original['source_blend_sha256'],
                original_fbx_unchanged=digest(OUT/'emerald_island.fbx')==original['fbx_sha256'],
                unity_axis_note='Unity importer additionally mirrors X. Root aligns the prefab using an X reflection and proves all three anchors in the Editor. This audit proves Blender roundtrip only.',
                unity_runtime_test_performed=False,render_equivalence_claimed=False,
                reproduce_audit='blender --background --factory-startup --python export_emerald.py -- --audit-clean',
                frozen_input_policy='This audit reads the FBX and writes only clean-export-report.json; do not rerun export/clean against frozen inputs during runtime QA.',
                elapsed_seconds=round(time.monotonic()-started,2))
    (OUT/'clean-export-report.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print('FROZEN_CLEAN_AUDIT',json.dumps(report),flush=True)

if __name__=='__main__':
    if '--audit-clean' in sys.argv:audit_frozen_clean()
    elif '--clean' in sys.argv:clean_derivative()
    elif '--verify' in sys.argv:verify_roundtrip()
    else:main()
