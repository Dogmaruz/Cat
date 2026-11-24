"""Derived radio cases without original harnesses. Never edits Incoming ZIPs.

The broad, wearer-facing return is trimmed and resurfaced. Leather loops,
mount rings and tabs are removed with it, rather than hidden under a new strap.
All front radio details and rotating mechanisms retain their original UVs.
"""
from pathlib import Path
import bpy,bmesh,json,sys,shutil,hashlib,math
import numpy as np
from mathutils import Vector
ROOT=Path(r'C:/Users/Derek/Games_develop/Cat')
HERE=Path(__file__).parent
PACKS=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks'
BACKUP=HERE/'Before'
OUT=HERE/'Prepared'
QA=HERE/'Review'
for p in (BACKUP,OUT,QA):p.mkdir(parents=True,exist_ok=True)
sys.path.insert(0,str(ROOT/'ArtSource/RadioArchipelago/Tools/Modular20261104'))
import pack_inspect as src

ENAMEL={
 'Boro':(.3,.55,.48),'Boro_Pulse':(.85,.8,.62),'Fira':(.75,.29,.1),'Fira_Beacon':(.7,.24,.15),
 'Lumen':(.62,.32,.12),'Lumen_Glider':(.86,.81,.65),'Miro':(.34,.53,.38),'Miro_Garden':(.34,.53,.38),
 'Nori':(.33,.6,.52),'Nori_Nocturne':(.08,.22,.45),'Piko':(.62,.72,.39),'Piko_Voyager':(.86,.82,.63),
 'Rumi':(.58,.45,.72),'Rumi_Orbit':(.58,.45,.72),'Soli':(.78,.32,.12),'Soli_Helio':(.78,.32,.12),
 'Tiko':(.34,.62,.52),'Tiko_Resonator':(.34,.62,.52),'Vela':(.72,.82,.84),'Vela_Cloud':(.38,.65,.79)}

def source_swatch(case,target):
 mat=case.data.materials[0]
 node=next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
 image=node.inputs['Base Color'].links[0].from_node.image
 pixels=np.empty(image.size[0]*image.size[1]*4,dtype=np.float32);image.pixels.foreach_get(pixels);pixels=pixels.reshape(image.size[1],image.size[0],4)
 uvs=np.array([list(l.uv) for l in case.data.uv_layers.active.data])
 xy=np.clip((uvs*np.array(image.size[:])).astype(int),0,np.array(image.size[:])-1)
 col=pixels[xy[:,1],xy[:,0],:3]
 # Select an actual source texel; averaging unrelated atlas UVs creates the
 # wrong colour on a resurfaced mesh even if their source faces are adjacent.
 error=((col-np.asarray(target))**2).sum(1)
 index=int(np.argmin(error))
 return Vector((xy[index]+.5)/np.array(image.size[:]))

def bb(ob):
 a=np.array([list(v.co) for v in ob.data.vertices]);return [a.min(0).tolist(),a.max(0).tolist()]

def trim(bm,center,normal,faces=None):
 geom=set(faces if faces is not None else bm.faces)
 for f in list(geom):geom.update(f.edges);geom.update(f.verts)
 r=bmesh.ops.bisect_plane(bm,geom=list(geom),dist=1e-6,plane_co=center,plane_no=normal,clear_inner=True,clear_outer=False)
 return len(r['geom_cut'])

def subtract_box(case,lo,hi):
 bpy.ops.mesh.primitive_cube_add(size=1,location=tuple((a+b)*.5 for a,b in zip(lo,hi)))
 cutter=bpy.context.object;cutter.name='Temporary mount removal volume';cutter.dimensions=tuple(b-a for a,b in zip(lo,hi))
 bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
 bpy.context.view_layer.objects.active=case
 mod=case.modifiers.new('Excise old mount','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cutter
 bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.data.objects.remove(cutter,do_unlink=True)

def back_panel(case,meta,plane,texel,cushion=False):
 # A fully closed, rounded padded plate replaces the harness-bearing return.
 # Three bevel rings give a broad soft edge rather than a cut-off slab.
 top=meta['caseBoundsMax'][2];width=.475;height=top*.47;center=top*.50
 power={'Rumi_Orbit':.9,'Lumen_Glider':.9,'Soli_Helio':.9,'Piko':.9,'Tiko_Resonator':.9,'Vela_Cloud':.7,'Fira_Beacon':.7,'Boro_Pulse':.55}.get(meta['id'],.38)
 if meta['id']=='Miro_Garden':height=.35;center=.37
 front=meta['contactPlaneY']+.035
 if cushion:
  width*=.81;height*=.85;plane=front+.012;front=meta['contactPlaneY']-.012
 rings=[];verts=[];faces=[]
 for y,inset in [(plane+.005,.035),(front+.024,0),(front,.025)]:
  ring=[]
  for i in range(96):
   t=i*2*math.pi/96;c=math.cos(t);s=math.sin(t)
   x=math.copysign(abs(c)**power,c)*(width-inset)
   z=center+math.copysign(abs(s)**power,s)*(height-inset)
   ring.append(len(verts));verts.append((x,y,z))
  rings.append(ring)
 for a,b in zip(rings,rings[1:]):
  for i in range(96):j=(i+1)%96;faces.append((a[i],a[j],b[j],b[i]))
 faces.append(tuple(reversed(rings[0])));faces.append(tuple(rings[-1]))
 mesh=bpy.data.meshes.new('Clean padded contact plate');mesh.from_pydata(verts,[],faces);mesh.update()
 ob=bpy.data.objects.new('BackPanel',mesh);bpy.context.collection.objects.link(ob);ob.data.materials.append(case.data.materials[0]);ob.parent=case.parent
 layer=mesh.uv_layers.new(name=case.data.uv_layers.active.name)
 for l in layer.data:l.uv=texel
 for f in mesh.polygons:f.use_smooth=len(f.vertices)==4
 # Recalculate orientation from a closed volume before joining.
 bm=bmesh.new();bm.from_mesh(mesh);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bmesh.ops.triangulate(bm,faces=list(bm.faces));bm.to_mesh(mesh);bm.free()
 bpy.ops.object.select_all(action='DESELECT');case.select_set(True);ob.select_set(True);bpy.context.view_layer.objects.active=case;bpy.ops.object.join()
 return front

def new_bearing(case,meta,roof,texel):
 p=Vector(meta['topPivot']);base=min(roof-.025,p.z-.06);parts=[]
 # Broad root, short mast and a real top collar. All UVs use the original
 # bearing metal texel; the moving source mechanism itself is untouched.
 for radius,z0,z1 in [(.068,base,base+.026),(.036,base+.016,p.z),(.057,p.z-.018,p.z+.004)]:
  bpy.ops.mesh.primitive_cylinder_add(vertices=32,radius=radius,depth=z1-z0,location=(p.x,p.y,(z0+z1)*.5))
  o=bpy.context.object;o.data.materials.append(case.data.materials[0]);parts.append(o)
  for l in o.data.uv_layers.active.data:l.uv=texel
  for f in o.data.polygons:f.use_smooth=len(f.vertices)==4
  mod=o.modifiers.new('Soft machined collar edge','BEVEL');mod.width=.006;mod.segments=3;bpy.context.view_layer.objects.active=o;bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.ops.object.select_all(action='DESELECT');case.select_set(True)
 for o in parts:o.select_set(True)
 bpy.context.view_layer.objects.active=case;bpy.ops.object.join()

def cut_case(name,folder,meta):
 case=bpy.data.objects['Case'];before=len(case.data.polygons)
 # The body-contact surface is exchanged for one clean back return. This
 # excises embossed fragments/UV scars as well as disconnected strap meshes.
 plane=.10
 if name=='Miro_Garden':plane=.18
 bm=bmesh.new();bm.from_mesh(case.data)
 uv=bm.loops.layers.uv.active
 padpoint=Vector((0,meta['contactPlaneY'],meta['caseBoundsMax'][2]*.5))
 padface=min(bm.faces,key=lambda f:(f.calc_center_median()-padpoint).length_squared)
 texel=sum((l[uv].uv.copy() for l in padface.loops),Vector((0,0)))/len(padface.loops)
 pivot=Vector(meta['topPivot'])
 postface=min(bm.faces,key=lambda f:(f.calc_center_median()-(pivot-Vector((0,0,.03)))).length_squared)
 bearingtexel=sum((l[uv].uv.copy() for l in postface.loops),Vector((0,0)))/len(postface.loops)
 shellpoint=Vector((.49,(meta['contactPlaneY']+meta['caseBoundsMax'][1])*.5,meta['caseBoundsMax'][2]*.5))
 shellface=min(bm.faces,key=lambda f:(f.calc_center_median()-shellpoint).length_squared)
 shelltexel=sum((l[uv].uv.copy() for l in shellface.loops),Vector((0,0)))/len(shellface.loops)
 shelltexel=source_swatch(case,ENAMEL[name])
 bearingtexel=source_swatch(case,(.68,.48,.20))
 # Remove old cap polygons first; they belong to the previous strap split.
 bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 trim(bm,Vector((0,plane,0)),Vector((0,1,0)))
 # First seal the global plane. Local mount removal then uses solid Boolean
 # cutters: partial BMesh cuts can leave T junctions on dense generated meshes.
 top=meta['caseBoundsMax'][2]
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 uv=bm.loops.layers.uv.active
 # Pick a plain contact-pad texel, not a buckle, for newly resurfaced regions.
 boundary=[e for e in bm.edges if e.is_boundary]
 caps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)['faces'] if boundary else []
 for f in caps:
  f.smooth=False
  for l in f.loops:l[uv].uv=texel
 bmesh.ops.triangulate(bm,faces=caps)
 roof=top*.97 if name!='Miro_Garden' else .73
 trim(bm,Vector((0,0,roof)),Vector((0,0,-1)))
 boundary=[e for e in bm.edges if e.is_boundary]
 roofcaps=bmesh.ops.holes_fill(bm,edges=boundary,sides=0)['faces'] if boundary else []
 for f in roofcaps:
  f.smooth=False
  for l in f.loops:l[uv].uv=shelltexel
 bmesh.ops.triangulate(bm,faces=roofcaps)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(case.data);bm.free();case.data.update()
 back_panel(case,meta,plane,shelltexel)
 front=back_panel(case,meta,plane,texel,True)
 new_bearing(case,meta,roof,bearingtexel)
 # Existing facade shading is retained. Resurfaced contact plate is flat and
 # runs into the rounded source shell, so it has no lumpy cut-off roots.
 for ob in list(bpy.context.scene.objects):
  if ob.name.startswith(('Harness.','StrapFront.','StrapUpper.')):bpy.data.objects.remove(ob,do_unlink=True)
 return {'caseFacesBefore':before,'caseFacesAfter':len(case.data.polygons),'bodyContactPlaneY':front,'trimPlaneY':plane,'newCleanCapTriangles':len(caps), 'plainPadUv':list(texel)}

def render(name):
 for o in list(bpy.context.scene.objects):
  if o.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(o,do_unlink=True)
 c=src.light_camera();sc=bpy.context.scene;sc.cycles.samples=8
 sc.render.resolution_x=440;sc.render.resolution_y=540
 # Show the contact side without the tall rotor obscuring the case repair.
 rotor=bpy.data.objects['TopRotor'];rotor.hide_render=True
 top=bb(bpy.data.objects['Case'])[1][2];target=Vector((0,0,top*.50));c.data.ortho_scale=max(1.55,top*1.28)
 for view,pos in [('contact',(3,-5,2.4)),('side',(-6,0,2)),('radio',(3,6,2.4))]:
  c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler()
  sc.render.filepath=str(QA/(name+'_'+view+'.png'));bpy.ops.render.render(write_still=True)
 rotor.hide_render=False
 for o in list(bpy.context.scene.objects):
  if o.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(o,do_unlink=True)

def main(name):
 original=PACKS/name;backup=BACKUP/name
 if not backup.exists():shutil.copytree(original,backup)
 bpy.ops.wm.open_mainfile(filepath=str(backup/'editable.blend'),load_ui=False)
 meta=json.loads((backup/'Meta.json').read_text())
 report=cut_case(name,backup,meta)
 folder=OUT/name;folder.mkdir(parents=True,exist_ok=True)
 case=bpy.data.objects['Case'];rotor=bpy.data.objects['TopRotor'];root=bpy.data.objects['BackpackRoot'];top=bpy.data.objects['TopPivot']
 meta['harnessNames']=[]
 for k in list(meta):
  if k.startswith('harness'):meta.pop(k)
 meta['fixedBoundsMin'],meta['fixedBoundsMax']=bb(case)
 meta['contactPlaneY']=report['bodyContactPlaneY']
 meta['outputTriangles']={o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in (case,rotor)}
 meta.setdefault('repairs',{})['straplessCase20261105']=report
 (folder/'Meta.json').write_text(json.dumps(meta,indent=2))
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for o in (root,case,top,rotor):o.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 render(name)
 (folder/'validation.json').write_text(json.dumps(report,indent=2))
 print('STRAPLESS_CASE',name,json.dumps(report),flush=True)

if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(p.name for p in PACKS.iterdir() if p.is_dir())
 for name in names:main(name)
