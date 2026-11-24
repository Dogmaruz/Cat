"""Fresh Blender/FBX read-back. No Unity or packaged build is launched."""
import bpy,json,pathlib,hashlib,sys,collections,math
from mathutils import Vector,Matrix
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build_moon_resonance as moon
for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
path=moon.OUT/'moon_resonance_island.fbx'
bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,colors_type='LINEAR')
objects=list(bpy.context.scene.objects);named={ob.name:ob for ob in objects}
anchors={n:tuple(named[n].matrix_world.translation)for n in('RA_Origin','RA_X10','RA_North10')}
origin=Vector(anchors['RA_Origin']);east=(Vector(anchors['RA_X10'])-origin)/10;north=(Vector(anchors['RA_North10'])-origin)/10;up=east.cross(north).normalized()
basis=Matrix((east,north,up)).transposed();inv=basis.inverted()
counts=collections.Counter();up_errors=collections.Counter();colors_missing=[];mist_min=1e20;mist_max=-1e20;meshes=[];baddegenerate=0
for ob in objects:
    if ob.type!='MESH':continue
    meshes.append(ob);category=ob.data.materials[0].name.removeprefix('RA_Moon_');counts[category]+=len(ob.data.polygons)
    if not ob.data.color_attributes.active_color:colors_missing.append(ob.name)
    if category in('Terrain','Water'):
        norm=ob.matrix_world.to_3x3().inverted().transposed()
        for p in ob.data.polygons:
            if (norm@p.normal).dot(up)<-1e-5:up_errors[category]+=1
            if p.area<1e-10:baddegenerate+=1
    if category=='Mist':
        for v in ob.data.vertices:
            p=inv@(ob.matrix_world@v.co-origin);mist_min=min(mist_min,p.z);mist_max=max(mist_max,p.z)
report=dict(schema='radio_archipelago_moon_fresh_fbx_audit/1',sha256=hashlib.sha256(path.read_bytes()).hexdigest(),bytes=path.stat().st_size,mesh_batches=len(meshes),triangles=sum(counts.values()),category_triangles=dict(counts),anchors=anchors,anchor_metres=dict(east=east.length,north=north.length,orthogonality=east.dot(north)),terrain_water_upward_errors=dict(up_errors),missing_linear_corner_colors=colors_missing,degenerate_terrain_water=baddegenerate,mist_source_z_range=[mist_min,mist_max],proof_only_tiles_exported=any('PROOF_ONLY'in ob.name for ob in objects),material_names=sorted({m.name for ob in meshes for m in ob.data.materials}),source_to_aligned_unity='(x,y,z) -> (x,z,-y)')
report['pass']=len(meshes)<320 and sum(counts.values())<1900000 and not up_errors and not colors_missing and not baddegenerate and not report['proof_only_tiles_exported']and abs(east.length-1)<1e-4 and abs(north.length-1)<1e-4 and abs(east.dot(north))<1e-5 and mist_max<-40
(HERE/'export_verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('MOON_FRESH_FBX_AUDIT',json.dumps(report),flush=True)
if not report['pass']:raise RuntimeError('Moon FBX contract audit failed')

# Two actual-FBX render angles use explicit native shader backface culling.
# No missing surface can be hidden by two-sided rock rendering.
for mat in bpy.data.materials:
    if not mat.name.startswith('RA_Moon_'):continue
    category=mat.name.removeprefix('RA_Moon_');mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links;bs=moon.ex.node_bsdf(mat);out=next(n for n in nodes if n.type=='OUTPUT_MATERIAL')
    attr=nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';links.new(attr.outputs['Color'],bs.inputs['Base Color'])
    bs.inputs['Roughness'].default_value=.28 if category in('Water','Waterfall','Foam')else .35 if category=='Metal'else .78
    bs.inputs['Metallic'].default_value=.65 if category=='Metal'else 0
    if category=='Mist':links.new(attr.outputs['Alpha'],bs.inputs['Alpha'])
    if category in('Cliffs','Terrain','Rocks','Architecture','Metal','Wood','Crystal','Water'):
        mat.use_backface_culling=True;geo=nodes.new('ShaderNodeNewGeometry');transparent=nodes.new('ShaderNodeBsdfTransparent');mix=nodes.new('ShaderNodeMixShader')
        links.new(geo.outputs['Backfacing'],mix.inputs[0]);links.new(bs.outputs[0],mix.inputs[1]);links.new(transparent.outputs[0],mix.inputs[2]);links.new(mix.outputs[0],out.inputs['Surface'])
scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Moon FBX verification sky');scene.world.use_nodes=True
bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.57,.62,.79,1);bg.inputs['Strength'].default_value=.7
ld=bpy.data.lights.new('FBX verification sun','SUN');ld.energy=3.3;ld.angle=.13;ob=bpy.data.objects.new(ld.name,ld);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.4,-.6)
scene.render.engine='CYCLES';scene.cycles.samples=16;scene.cycles.use_denoising=True
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
    for d in prefs.devices:d.use=d.type=='CUDA'
    scene.cycles.device='GPU'
except:pass
scene.render.resolution_x=1600;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
def world(source):return origin+basis@Vector(source)
for label,target,offset,scale in [('fbx_culled_front',(-18,88,-5),(270,-320,215),365),('fbx_culled_reverse',(-18,90,-8),(-270,340,135),375)]:
    data=bpy.data.cameras.new(label);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(label,data);scene.collection.objects.link(cam);cam.location=world(Vector(target)+Vector(offset));target=world(target);cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(moon.PROOF/(label+'.png'));bpy.ops.render.render(write_still=True);print('MOON_FBX_CULLED_PROOF',label,flush=True)
