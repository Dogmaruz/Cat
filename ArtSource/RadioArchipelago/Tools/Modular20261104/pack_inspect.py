from pathlib import Path
import bpy, json, zipfile, sys, math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[4]
IN=ROOT/'ArtSource/RadioArchipelago/ModularCast20261102/Incoming/Backpacks'
OUT=ROOT/'ArtSource/RadioArchipelago/ProcessedModular'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/PackQA'
QA.mkdir(parents=True,exist_ok=True)

def bounds(ob):
 p=[ob.matrix_world@Vector(v) for v in ob.bound_box]
 return [[min(q[a] for q in p) for a in range(3)],[max(q[a] for q in p) for a in range(3)]]

def extract(name):
 folder=OUT/'_Sources/Backpacks'/name;folder.mkdir(parents=True,exist_ok=True)
 with zipfile.ZipFile(IN/name/(name+'.zip')) as z:
  for entry in z.infolist():
   dest=(folder/entry.filename).resolve()
   assert dest.is_relative_to(folder.resolve())
   if entry.is_dir():dest.mkdir(parents=True,exist_ok=True)
   elif not dest.exists():
    dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(z.read(entry))
 return folder

def material(directory):
 m=bpy.data.materials.new('PackSource');m.use_nodes=True
 s=m.node_tree.nodes.get('Principled BSDF');s.inputs['Roughness'].default_value=.48
 for key,socket in [('color','Base Color'),('normal','Normal'),('roughness','Roughness'),('metallic','Metallic')]:
  p=next(p for p in directory.rglob('*') if p.is_file() and p.suffix.lower()=='.png' and (('color' in p.stem.lower() and 'normal' not in p.stem.lower()) if key=='color' else key in p.stem.lower()))
  tex=m.node_tree.nodes.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(p),check_existing=True)
  if key!='color':tex.image.colorspace_settings.name='Non-Color'
  if key=='normal':
   n=m.node_tree.nodes.new('ShaderNodeNormalMap');n.inputs['Strength'].default_value=.6
   m.node_tree.links.new(tex.outputs['Color'],n.inputs['Color']);m.node_tree.links.new(n.outputs['Normal'],s.inputs['Normal'])
  else:m.node_tree.links.new(tex.outputs['Color'],s.inputs[socket])
 return m

def light_camera():
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=12
 scene.render.resolution_x=420;scene.render.resolution_y=520;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('PackReviewWorld');scene.world.color=(.35,.35,.35)
 scene.view_settings.view_transform='AgX'
 for pos,power,size in [((3,-4,6),650,5),((-3,-1,3),350,4),((1,3,5),700,4)]:
  bpy.ops.object.light_add(type='AREA',location=pos);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,1))-o.location).to_track_quat('-Z','Y').to_euler()
 bpy.ops.object.camera_add();c=bpy.context.object;c.data.type='ORTHO';scene.camera=c
 return c

def main():
 report=[]
 names=[p.name for p in sorted(IN.iterdir()) if p.is_dir()]
 args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
 if args:names=args
 for name in names:
  bpy.ops.wm.read_factory_settings(use_empty=True)
  directory=extract(name);fbx=next(directory.glob('*.fbx'))
  bpy.ops.import_scene.fbx(filepath=str(fbx),use_image_search=False,use_anim=False)
  meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
  for ob in meshes:
   bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);ob.select_set(False)
  b=[bounds(o) for o in meshes];lo=Vector([min(x[0][a] for x in b) for a in range(3)]);hi=Vector([max(x[1][a] for x in b) for a in range(3)])
  s=2.4/(hi.z-lo.z);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
  for ob in meshes:
   for v in ob.data.vertices:v.co=(v.co-center)*s
   ob.location=(0,0,0);ob.data.materials.clear();ob.data.materials.append(material(directory))
  bpy.context.view_layer.update()
  data={'id':name,'sourceBounds':[list(lo),list(hi)],'objects':[]}
  for ob in meshes:
   data['objects'].append({'name':ob.name,'bounds':bounds(ob),'vertices':len(ob.data.vertices),'faces':len(ob.data.polygons),'parts':len(ob.data.materials)})
  report.append(data);print('PACK_INSPECT',json.dumps(data),flush=True)
  c=light_camera();c.data.ortho_scale=2.9
  for view,pos in [('back',(3.2,6,2.8)),('front',(-3.2,-6,2.8))]:
   c.location=pos;c.rotation_euler=(Vector((0,0,1.15))-c.location).to_track_quat('-Z','Y').to_euler()
   bpy.context.scene.render.filepath=str(QA/(name+'_'+view+'.png'));bpy.ops.render.render(write_still=True)
  (QA/'source-inspection.json').write_text(json.dumps(report,indent=2))
if __name__=='__main__':main()
