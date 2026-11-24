"""Local harness excision. Original radio facade and mechanisms are retained.

Only narrow wearer-facing root volumes are replaced by a smooth case-coloured
shell patch. Never cuts one arbitrary global plane through an entire case.
"""
from pathlib import Path
import bpy,bmesh,json,sys,math,shutil
import numpy as np
from mathutils import Vector
P=Path(__file__).parent
sys.path.insert(0,str(P));import clean_cases as common
ROOT=common.ROOT;QA=common.QA;BACKUP=common.BACKUP;OUT=P/'Localized';OUT.mkdir(exist_ok=True)
POWER={'Rumi_Orbit':.9,'Lumen_Glider':.9,'Soli_Helio':.9,'Piko':.9,'Tiko_Resonator':.9,'Vela_Cloud':.7,'Fira_Beacon':.7,'Boro_Pulse':.55}

def boolean(ob,cutter,operation):
 bpy.context.view_layer.objects.active=ob
 mod=ob.modifiers.new('Local strap root '+operation,'BOOLEAN');mod.operation=operation;mod.solver='EXACT';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name)

def cube(lo,hi,mat):
 bpy.ops.mesh.primitive_cube_add(size=1,location=tuple((a+b)*.5 for a,b in zip(lo,hi)))
 ob=bpy.context.object;ob.name='Authoring volume';ob.dimensions=tuple(b-a for a,b in zip(lo,hi));bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 ob.data.materials.append(mat)
 return ob

def template(meta,mat,texel):
 h=meta['caseBoundsMax'][2]
 if meta['id']=='Miro_Garden':h=.75
 width=.49;power=POWER.get(meta['id'],.38);back=meta['contactPlaneY']+.005;front=meta['caseBoundsMax'][1]-.012
 verts=[];rings=[];faces=[]
 for y,shrink in [(back,.025),(back+.026,0),(front-.026,0),(front,.025)]:
  ring=[]
  for i in range(128):
   a=i*2*math.pi/128;c=math.cos(a);s=math.sin(a)
   ring.append(len(verts));verts.append((math.copysign(abs(c)**power,c)*(width-shrink),y,h*.5+math.copysign(abs(s)**power,s)*(h*.5-.01-shrink)))
  rings.append(ring)
 for a,b in zip(rings,rings[1:]):
  for i in range(128):j=(i+1)%128;faces.append((a[i],a[j],b[j],b[i]))
 faces.append(tuple(reversed(rings[0])));faces.append(tuple(rings[-1]))
 mesh=bpy.data.meshes.new('Local clean shell patch');mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new('Clean shell template',mesh);bpy.context.collection.objects.link(ob);mesh.materials.append(mat)
 uv=mesh.uv_layers.new(name='UVMap')
 for l in uv.data:l.uv=texel
 for f in mesh.polygons:f.use_smooth=len(f.vertices)==4
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 return ob

def set_patch_uv(ob,mat,uv):
 indices={i for i,m in enumerate(ob.data.materials) if m==mat}
 for f in ob.data.polygons:
  if f.material_index in indices:
   for i in f.loop_indices:ob.data.uv_layers.active.data[i].uv=uv

def main(name):
 bpy.ops.wm.open_mainfile(filepath=str(BACKUP/name/'editable.blend'),load_ui=False)
 meta=json.loads((BACKUP/name/'Meta.json').read_text());case=bpy.data.objects['Case'];root=bpy.data.objects['BackpackRoot'];rotor=bpy.data.objects['TopRotor'];top=bpy.data.objects['TopPivot']
 for ob in list(bpy.context.scene.objects):
  if ob.name.startswith('Harness.'):bpy.data.objects.remove(ob,do_unlink=True)
 material=case.data.materials[0];patchmat=material.copy();patchmat.name='Clean matching case enamel'
 case.data.materials.append(patchmat)
 enamel=common.source_swatch(case,common.ENAMEL[name]);h=meta['caseBoundsMax'][2];pad=meta['contactPlaneY'];plane=pad+.018
 # Miro's first derivative retained almost complete loops; its real pad plane
 # was subsequently measured and lives in corrected source metadata.
 bm=bmesh.new();bm.from_mesh(case.data);uv=bm.loops.layers.uv.active
 target=Vector((0,pad,h*.5));face=min(bm.faces,key=lambda f:(f.calc_center_median()-target).length_squared)
 leather=sum((l[uv].uv.copy() for l in face.loops),Vector((0,0)))/len(face.loops)
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 common.trim(bm,Vector((0,plane,0)),Vector((0,1,0)))
 edges=[e for e in bm.edges if e.is_boundary];caps=bmesh.ops.holes_fill(bm,edges=edges,sides=0)['faces'] if edges else []
 for f in caps:
  f.material_index=1
  for l in f.loops:l[uv].uv=leather
 bmesh.ops.triangulate(bm,faces=caps);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
 # Full original front, side speakers, bevels and fixed bearing remain outside
 # these four localized root regions, unlike the rejected deep return cut.
 volumes=[]
 for side in (-1,1):
  xr=(.21,1.5) if side>0 else (-1.5,-.21)
  end=pad+.22
  volumes.append(((xr[0],-2,h*.78),(xr[1],end,h+1)))
  volumes.append(((xr[0],-2,-1),(xr[1],end,h*.23)))
  # Side loop material sometimes follows the rounded case shoulder downward.
  # Only the rear return is replaced; the source facade stays fully intact.
  xr=(.405,1.5) if side>0 else (-1.5,-.405)
  volumes.append(((xr[0],-2,h*.18),(xr[1],pad+.16,h*.84)))
 if name=='Miro_Garden':
  volumes=[v for i,v in enumerate(volumes) if i%3!=0]
  for side in (-1,1):
   xr=(.20,1.5) if side>0 else (-1.5,-.20)
   volumes.append(((xr[0],-2,.68),(xr[1],.10,1.6)))
 for lo,hi in volumes:
  cutter=cube(lo,hi,patchmat);boolean(case,cutter,'DIFFERENCE');set_patch_uv(case,patchmat,enamel)
  # A clean matching case surface fills only the excised root socket; a tiny
  # overlap gives the exact Boolean a stable common volume at the perimeter.
  wider=cube(tuple(a-.008 for a in lo),tuple(b+.008 for b in hi),patchmat)
  patch=template(meta,patchmat,enamel);boolean(patch,wider,'INTERSECT');set_patch_uv(patch,patchmat,enamel)
  boolean(case,patch,'UNION');set_patch_uv(case,patchmat,enamel)
  for ob in (cutter,wider,patch):bpy.data.objects.remove(ob,do_unlink=True)
 # Compact clean contact cushion, not a new full-depth case in front of the
 # radio facade. Its two bevel returns are entirely behind the original shell.
 common.back_panel(case,meta,plane,leather,True)
 meta['harnessNames']=[]
 for key in list(meta):
  if key.startswith('harness'):meta.pop(key)
 meta['fixedBoundsMin'],meta['fixedBoundsMax']=common.bb(case)
 meta['contactPlaneY']=pad-.012
 meta['outputTriangles']={o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in (case,rotor)}
 meta.setdefault('repairs',{})['straplessCase20261105']={'method':'localized mount socket reconstruction; original radio front and original fixed/moving mechanism retained','padTrimPlaneY':plane,'rootVolumes':[{'min':a,'max':b} for a,b in volumes]}
 folder=OUT/name;folder.mkdir(exist_ok=True);(folder/'Meta.json').write_text(json.dumps(meta,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for ob in (root,top,case,rotor):ob.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 common.render(name)
 print('LOCAL_CASE',name,'triangles',meta['outputTriangles'],flush=True)

if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(p.name for p in BACKUP.iterdir() if p.is_dir())
 for name in names:main(name)
