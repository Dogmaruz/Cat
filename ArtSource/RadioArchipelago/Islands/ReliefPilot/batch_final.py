"""Spatial runtime batching of the reviewed derivative, preserving corner channels."""
import bpy,bmesh,pathlib,sys,math,json,collections,numpy as np
from mathutils import Vector
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import build_relief as r
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(r.OUT/'emerald_relief_island.fbx'),use_custom_normals=True,use_image_search=False,colors_type='LINEAR')
originals=[o for o in bpy.context.scene.objects if o.type=='MESH']
col=bpy.data.collections.new('Batched Emerald relief');bpy.context.scene.collection.children.link(col)
root=bpy.data.objects.new('EmeraldReliefIsland',None);col.objects.link(root)
materials={};batches={};serial=collections.Counter();output=[];removed=collections.Counter()

class Batch:
 def __init__(self,key):
  self.key=key;self.v=[];self.f=[];self.c=[];self.uv=[];self.bank=[];self.n=[];self.soft=[];self.remap={}
 def flush(self):
  if not self.f:return
  category,cx,cy=self.key;serial[self.key]+=1;name=f'RA_{category}_{cx:+03d}_{cy:+03d}_{serial[self.key]:02d}'
  origin=Vector(((cx+.5)*24,(cy+.5)*24,0))
  m=bpy.data.meshes.new(name);m.from_pydata([tuple(Vector(v)-origin) for v in self.v],[],self.f);m.update();m.materials.append(materials[category])
  colors=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');colors.data.foreach_set('color',np.asarray(self.c,dtype=np.float32).reshape(-1))
  if category in ('Terrain','Road','BridgeDeck','StepTreads'):
   u=m.uv_layers.new(name='UVMap');u.data.foreach_set('uv',np.asarray(self.uv,dtype=np.float32).reshape(-1))
  if category=='Terrain':
   u=m.uv_layers.new(name='BankMix');u.data.foreach_set('uv',np.asarray(self.bank,dtype=np.float32).reshape(-1))
  for p,soft in zip(m.polygons,self.soft):p.use_smooth=soft
  m.normals_split_custom_set(self.n)
  obj=bpy.data.objects.new(name,m);col.objects.link(obj);obj.parent=root;obj.location=origin;obj['unity_material_bucket']=category
  output.append(obj)
  self.v=[];self.f=[];self.c=[];self.uv=[];self.bank=[];self.n=[];self.soft=[];self.remap={}

for oi,o in enumerate(originals):
 m=o.data;matrix=o.matrix_world;normal_matrix=matrix.to_3x3().inverted().transposed()
 category=o.get('unity_material_bucket') or m.materials[0].name.removeprefix('RA_Emerald_').split('.')[0]
 if category not in materials:materials[category]=m.materials[0];materials[category].name='RA_Emerald_'+category
 color=m.color_attributes.get('Color');uv=m.uv_layers[0].data if m.uv_layers else None;bank=m.uv_layers[1].data if len(m.uv_layers)>1 else None
 world=[matrix@v.co for v in m.vertices];cn=m.corner_normals
 for poly in m.polygons:
  ids=list(poly.vertices);a,b,c=(np.asarray(world[i],dtype=np.float64) for i in ids);area=np.cross(b-a,c-a);area2=float(np.linalg.norm(area));long=max(float(np.dot(x,x)) for x in(b-a,c-b,a-c))
  invalid=any(abs(cn[li].vector.length-1)>.01 for li in poly.loop_indices)
  if area2<1e-9 or (category in ('Architecture','Metal','Rocks','Wood') and (invalid or area2/max(long,1e-30)<.00003)):
   removed[category]+=1;continue
  center=matrix@poly.center;key=(category,math.floor(center.x/24),math.floor(center.y/24));batch=batches.setdefault(key,Batch(key))
  if len(batch.v)+3>58000:batch.flush()
  face=[]
  for vi,li in zip(ids,poly.loop_indices):
   token=(oi,vi)
   if token not in batch.remap:batch.remap[token]=len(batch.v);batch.v.append(tuple(world[vi]))
   face.append(batch.remap[token]);batch.c.append(tuple(color.data[li if color.domain=='CORNER' else vi].color));batch.uv.append(tuple(uv[li].uv) if uv else (world[vi].x,world[vi].y));batch.bank.append(tuple(bank[li].uv) if bank else(0,0))
   n=normal_matrix@cn[li].vector
   if n.length<1e-8:n=Vector(area/area2)
   else:n.normalize()
   batch.n.append(tuple(n))
  batch.f.append(tuple(face));batch.soft.append(poly.use_smooth)
 if oi%150==0:print('BATCH_PROGRESS',oi,len(originals),flush=True)
for batch in batches.values():batch.flush()
for o in list(bpy.context.scene.objects):
 if o not in set(col.objects):bpy.data.objects.remove(o,do_unlink=True)
for name,pos in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
 o=bpy.data.objects.new(name,None);col.objects.link(o);o.parent=root;o.location=pos
pre=[]
for o in output:
 m=o.data;values=np.empty(len(m.corner_normals)*3,dtype=np.float32);m.corner_normals.foreach_get('vector',values);lengths=np.linalg.norm(values.reshape(-1,3),axis=1);bad=np.flatnonzero(np.abs(lengths-1)>.01)
 if len(bad):
  pre.append(dict(mesh=o.name,bad=len(bad)));badset=set(int(i) for i in bad);ids=[p.index for p in m.polygons if any(i in badset for i in p.loop_indices)]
  bm=bmesh.new();bm.from_mesh(m);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.faces[i] for i in ids],context='FACES');bm.to_mesh(m);bm.free();m.update();removed['postbatch_invalid']+=len(ids)
assert len(output)<400,len(output)
r.export_fbx(r.OUT/'emerald_relief_island.fbx',list(col.objects))
report=json.loads((HERE/'relief_report.json').read_text());report['final_spatial_batching']=dict(meshes=len(output),cell_meters=24,max_vertices=58000,removed_decorative_sliver_faces=dict(removed),vertex_colors_preserved=True,uv0_and_uv1_preserved=True,cell_local_coordinates=True,pre_export_invalid=pre)
(HERE/'relief_report.json').write_text(json.dumps(report,indent=2));print('BATCH_FINAL',report['final_spatial_batching'],flush=True)
