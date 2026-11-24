import bpy, json, pathlib, zipfile, math, sys
from mathutils import Vector
ROOT=pathlib.Path('C:/Users/Derek/Games_develop/Cat')
SRC=ROOT/'ArtSource/RadioArchipelago/ModularCast20261102/Incoming/Characters'
EX=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/_Sources/Characters'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/BodyInspection'
QA.mkdir(parents=True,exist_ok=True)
reports=[]
for archive in sorted(SRC.glob('*/*.zip')):
 name=archive.parent.name; dest=EX/name; dest.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(archive) as z:
  for entry in z.infolist():
   p=(dest/entry.filename).resolve()
   if not p.is_relative_to(dest.resolve()): raise ValueError('Unsafe archive')
   if not p.exists():
    if entry.is_dir(): p.mkdir(parents=True,exist_ok=True)
    else: p.parent.mkdir(parents=True,exist_ok=True); p.write_bytes(z.read(entry))
 bpy.ops.wm.read_factory_settings(use_empty=True)
 fbx=next(dest.glob('*.fbx')); bpy.ops.import_scene.fbx(filepath=str(fbx))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
 pts=[o.matrix_world@v.co for o in meshes for v in o.data.vertices]
 mn=[min(v[a] for v in pts) for a in range(3)]; mx=[max(v[a] for v in pts) for a in range(3)]
 report={'id':name,'boundsMin':mn,'boundsMax':mx,'objects':[{'name':o.name,'vertices':len(o.data.vertices),'faces':len(o.data.polygons),'materials':[m.name for m in o.data.materials if m],'loc':list(o.location),'scale':list(o.scale),'rot':list(o.rotation_euler)} for o in meshes]}
 reports.append(report)
 # Source FBX import normally has Z-up and face -Y. A front projection proves this.
 bpy.context.scene.render.engine='CYCLES'; bpy.context.scene.cycles.samples=12
 bpy.context.scene.render.resolution_x=512;bpy.context.scene.render.resolution_y=512;bpy.context.scene.render.resolution_percentage=100
 bpy.context.scene.world=bpy.data.worlds.new('World');bpy.context.scene.world.color=(.45,.45,.45)
 center=Vector([(mn[a]+mx[a])*.5 for a in range(3)]); height=mx[2]-mn[2]
 color=next(dest.rglob('Color.png')); normal=next(dest.rglob('Normal.png'))
 mat=bpy.data.materials.new(name+'_Source');mat.use_nodes=True
 bs=mat.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.65
 tx=mat.node_tree.nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(str(color));mat.node_tree.links.new(tx.outputs['Color'],bs.inputs['Base Color'])
 for o in meshes:o.data.materials.clear();o.data.materials.append(mat)
 def camera(pos,file):
  bpy.ops.object.camera_add(location=center+Vector(pos)*height);cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=height*1.3;bpy.context.scene.camera=cam
  bpy.context.scene.render.filepath=str(QA/(name+'_'+file+'.png'));bpy.ops.render.render(write_still=True);bpy.data.objects.remove(cam,do_unlink=True)
 for loc,power,size in [((-.7,-1.8,2),600,3),((1.5,-.6,.8),250,3),((0,1,1.5),400,2)]:
  bpy.ops.object.light_add(type='AREA',location=center+Vector(loc)*height);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=height*size;o.rotation_euler=(center-o.location).to_track_quat('-Z','Y').to_euler()
 camera((0,-3,.1),'front');camera((3,0,.1),'right')
 print('BODY_INSPECT',name,json.dumps(report),flush=True)
(QA/'source-inventory.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
