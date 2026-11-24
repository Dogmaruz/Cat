"""Modular user-pack preparation. Incoming ZIP archives are always read-only.

Narrow mechanical root cuts are local to the inspected spindle. Harness cuts
sit ahead of the padded case, so the original outer case and all UVs survive.
"""
from pathlib import Path
import sys,json,math,shutil,hashlib
import bpy,bmesh,numpy as np
from mathutils import Vector
sys.path.insert(0,str(Path(__file__).parent))
import pack_inspect as src

# Inspected in the shared 2.4-high source frame. These are spindle heights,
# not generic cuts through the highest horizontal extent of the case/straps.
CUTS={'Boro':1.53,'Boro_Pulse':1.78,'Fira':1.43,'Fira_Beacon':1.80,
      'Lumen':1.25,'Lumen_Glider':1.54,'Miro':1.52,'Miro_Garden':1.57,
      'Nori':1.47,'Nori_Nocturne':1.60,'Piko':1.46,'Piko_Voyager':1.66,
      'Rumi':1.65,'Rumi_Orbit':1.66,'Soli':1.45,'Soli_Helio':1.50,
      'Tiko':1.41,'Tiko_Resonator':1.77,'Vela':1.63,'Vela_Cloud':1.55}
CASE_TOP={'Boro':1.30,'Boro_Pulse':1.48,'Fira':1.37,'Fira_Beacon':1.38,
      'Lumen':1.17,'Lumen_Glider':1.42,'Miro':1.40,'Miro_Garden':1.46,
      'Nori':1.37,'Nori_Nocturne':1.44,'Piko':1.41,'Piko_Voyager':1.54,
      'Rumi':1.43,'Rumi_Orbit':1.46,'Soli':1.24,'Soli_Helio':1.45,
      'Tiko':1.30,'Tiko_Resonator':1.63,'Vela':1.46,'Vela_Cloud':1.41}

def components(bm):
 remaining=set(bm.faces);result=[]
 while remaining:
  stack=[remaining.pop()];group=[]
  while stack:
   f=stack.pop();group.append(f)
   for e in f.edges:
    for n in e.link_faces:
     if n in remaining:remaining.remove(n);stack.append(n)
  result.append(group)
 return result

def bb_faces(faces):
 vs={v for f in faces for v in f.verts}
 return [Vector([min(v.co[a] for v in vs) for a in range(3)]),Vector([max(v.co[a] for v in vs) for a in range(3)])]

def slice_split(bm,center,normal,radius=None):
 faces=[f for f in bm.faces if radius is None or any((v.co.xy-center.xy).length<radius and abs(v.co.z-center.z)<.13 for v in f.verts)]
 geom=set(faces)
 for f in faces:geom.update(f.edges);geom.update(f.verts)
 result=bmesh.ops.bisect_plane(bm,geom=list(geom),dist=1e-6,plane_co=center,plane_no=normal,clear_inner=False,clear_outer=False)
 edges=[e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge) and all(abs((v.co-center).dot(normal))<2e-5 for v in e.verts)]
 if len(edges)<3:raise RuntimeError('No mechanical/harness section found: '+str((list(center),len(edges))))
 bmesh.ops.split_edges(bm,edges=edges)
 return len(edges)

def copy_faces(bm,faces,name,material):
 # Copy a BMesh and remove the complement, retaining original UV corner data.
 bm.faces.index_update();selected={f.index for f in faces};part=bm.copy();part.faces.ensure_lookup_table()
 remove=[f for f in part.faces if f.index not in selected]
 bmesh.ops.delete(part,geom=remove,context='FACES')
 if name!='TopRotor':
  # A fallback full spindle plane may also split a shoulder loop. Both static
  # halves remain selected; welding coincident positions restores that loop.
  bmesh.ops.remove_doubles(part,verts=list(part.verts),dist=1e-6)
 isolated=[v for v in part.verts if not v.link_faces]
 if isolated:bmesh.ops.delete(part,geom=isolated,context='VERTS')
 boundaries=[e for e in part.edges if e.is_boundary]
 caps=[]
 if boundaries:
  caps=bmesh.ops.holes_fill(part,edges=boundaries,sides=0)['faces']
  uv=part.loops.layers.uv.active
  for f in caps:
   f.smooth=False
   for loop in f.loops:
    other=next((x for x in loop.vert.link_loops if x.face not in caps),None)
    if uv and other:loop[uv].uv=other[uv].uv.copy()
  bmesh.ops.triangulate(part,faces=caps)
 part.normal_update()
 mesh=bpy.data.meshes.new(name+'Geometry');part.to_mesh(mesh);part.free();mesh.update()
 ob=bpy.data.objects.new(name,mesh);bpy.context.collection.objects.link(ob);ob.data.materials.append(material)
 for f in mesh.polygons:f.use_smooth=True
 return ob,{'sealedBoundaryEdges':len(boundaries),'newCapFaces':len(caps)}

def load(name):
 bpy.ops.wm.read_factory_settings(use_empty=True);directory=src.extract(name)
 bpy.ops.import_scene.fbx(filepath=str(next(directory.glob('*.fbx'))),use_image_search=False,use_anim=False)
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1,(name,len(meshes))
 ob=meshes[0];bpy.context.view_layer.objects.active=ob;ob.select_set(True);bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
 v=np.array([list(v.co) for v in ob.data.vertices]);lo=v.min(0);hi=v.max(0);center=Vector(((lo[0]+hi[0])/2,(lo[1]+hi[1])/2,lo[2]));scale=2.4/(hi[2]-lo[2])
 for v in ob.data.vertices:v.co=(v.co-center)*scale
 ob.location=(0,0,0);ob.data.update();return ob,directory

def find_spindle(v,z,name=None):
 pts=v[(abs(v[:,2]-z)<.025)&(v[:,1]>.02)]
 if len(pts)<3:raise RuntimeError('Missing positive-Y spindle samples')
 # Neck sits behind the shoulder loops. Pick the compact positive-Y cluster.
 if name=='Lumen':
  # The low sail corner shares this height; its actual swivel is the straight
  # negative-X mast (right in the +Y rear camera). Never use the canvas tip.
  pts=pts[pts[:,0]<pts[:,0].min()+.15]
 y=np.quantile(pts[:,1],.65);pts=pts[pts[:,1]>y-.08]
 return Vector((float(np.median(pts[:,0])),float(np.median(pts[:,1])),z))

def write_textures(directory,output):
 # Keep actual source fidelity here; platform import settings later cap to 2K.
 paths={}
 for key,base in [('color','BaseColor.png'),('normal','Normal.png')]:
  p=next(p for p in directory.rglob('*') if p.is_file() and p.suffix.lower()=='.png' and (('color' in p.stem.lower() and 'normal' not in p.stem.lower()) if key=='color' else 'normal' in p.stem.lower()))
  shutil.copy2(p,output/base);paths[key]=str(output/base)
 metal=next(p for p in directory.rglob('*') if 'metallic' in p.name.lower());rough=next(p for p in directory.rglob('*') if 'roughness' in p.name.lower())
 m=bpy.data.images.load(str(metal),check_existing=True);r=bpy.data.images.load(str(rough),check_existing=True)
 if tuple(r.size)!=tuple(m.size):r.scale(*m.size)
 count=m.size[0]*m.size[1];mp=np.empty(count*4,dtype=np.float32);rp=np.empty(count*4,dtype=np.float32)
 m.pixels.foreach_get(mp);r.pixels.foreach_get(rp);a=np.zeros((count,4),dtype=np.float32);a[:,0]=mp.reshape(-1,4)[:,0];a[:,3]=1-rp.reshape(-1,4)[:,0]
 img=bpy.data.images.new('MetallicSmoothness',width=m.size[0],height=m.size[1],alpha=True);img.colorspace_settings.name='Non-Color';img.pixels.foreach_set(a.ravel());img.filepath_raw=str(output/'MetallicSmoothness.png');img.file_format='PNG';img.save()
 return paths

def normalize_parts(obs,pivot):
 lo,hi=src.bounds(obs[0]);lo=Vector(lo);hi=Vector(hi)
 # Actual case excludes the removed forward harness and moving mechanism.
 scale=1/max(.001,hi.x-lo.x);center=Vector(((lo.x+hi.x)/2,(lo.y+hi.y)/2,lo.z))
 for ob in obs:
  for v in ob.data.vertices:v.co=(v.co-center)*scale
  ob.data.update()
 return (pivot-center)*scale,scale,center

def empty(name,parent=None,pos=(0,0,0)):
 ob=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(ob);ob.location=pos
 if parent:ob.parent=parent
 return ob

def repair_beacon_bearing(rotor,case,spindle):
 """Put the 3-cup rotor on a real horizontal bearing, not a rotating pole stub."""
 mat=rotor.data.materials[0];uvs=rotor.data.uv_layers.active.data
 nearest=min(rotor.data.polygons,key=lambda f:(f.center-Vector((spindle.x,spindle.y,2.04))).length_squared)
 uv=uvs[nearest.loop_start].uv.copy()
 bm=bmesh.new();bm.from_mesh(rotor.data)
 remove=[f for f in bm.faces if f.calc_center_median().z<2.09 and (f.calc_center_median().xy-spindle.xy).length<.061]
 bmesh.ops.delete(bm,geom=remove,context='FACES')
 loose=[v for v in bm.verts if not v.link_faces]
 if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
 edge=[e for e in bm.edges if e.is_boundary]
 caps=bmesh.ops.holes_fill(bm,edges=edge,sides=0)['faces']
 layer=bm.loops.layers.uv.active
 for f in caps:
  for loop in f.loops:loop[layer].uv=uv
 bmesh.ops.triangulate(bm,faces=caps);bm.normal_update();bm.to_mesh(rotor.data);bm.free();rotor.data.update()
 addons=[]
 bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.028,depth=2.13-CUTS['Fira_Beacon']+.015,location=(spindle.x,spindle.y,(2.13+CUTS['Fira_Beacon'])/2-.005));addons.append(bpy.context.object)
 bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=12,radius=.047,location=(spindle.x,spindle.y,2.13));addons.append(bpy.context.object)
 for a in addons:
  a.data.materials.append(mat)
  if not a.data.uv_layers:a.data.uv_layers.new(name='UVMap')
  for loop in a.data.uv_layers.active.data:loop.uv=uv
  for f in a.data.polygons:f.use_smooth=True
 bpy.ops.object.select_all(action='DESELECT')
 for a in [case]+addons:a.select_set(True)
 bpy.context.view_layer.objects.active=case;bpy.ops.object.join()
 return {'fixedPostExtendedToHub':2.13,'removedRotatingPoleFaces':len(remove),'horizontalBearingRadius':.047}

def preview(name,obs,pivot,stage):
 c=src.light_camera();bpy.context.scene.render.resolution_x=480;bpy.context.scene.render.resolution_y=600
 b=[src.bounds(o) for o in obs];lo=Vector([min(x[0][a] for x in b) for a in range(3)]);hi=Vector([max(x[1][a] for x in b) for a in range(3)])
 target=Vector((0,0,(hi.z+lo.z)/2));c.data.ortho_scale=max(hi.z-lo.z,(hi.x-lo.x)*1.25)*1.17
 for view,pos in [('back',(3,6,3)),('front',(-3,-6,3))]:
  c.location=target+Vector(pos);c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(src.QA/(name+'_'+stage+'_'+view+'.png'));bpy.ops.render.render(write_still=True)
 # Deliberately non-rest rotor angle: proves there is real independently moving geometry.
 pivot.rotation_euler[1 if name=='Fira_Beacon' else 2]=math.radians(75)
 c.location=target+Vector((3,6,3));c.rotation_euler=(target-c.location).to_track_quat('-Z','Y').to_euler();bpy.context.scene.render.filepath=str(src.QA/(name+'_'+stage+'_turned.png'));bpy.ops.render.render(write_still=True);pivot.rotation_euler=(0,0,0)
 for ob in list(bpy.context.scene.objects):
  if ob.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(ob,do_unlink=True)

def prepare(name):
 ob,directory=load(name);before={'vertices':len(ob.data.vertices),'polygons':len(ob.data.polygons),'triangles':sum(len(f.vertices)-2 for f in ob.data.polygons)}
 v=np.array([list(v.co) for v in ob.data.vertices]);cut=CUTS[name];spindle=find_spindle(v,cut,name)
 bm=bmesh.new();bm.from_mesh(ob.data)
 neckEdges=slice_split(bm,spindle,Vector((0,0,1)),.55 if name not in ('Nori','Tiko') else .28)
 groups=components(bm);tip=max(bm.verts,key=lambda v:v.co.z)
 rotor=[f for g in groups if any(tip in f.verts for f in g) or (bb_faces(g)[0].z>=cut-.0001 and bb_faces(g)[0].y>-.04) for f in g]
 if len(rotor)>len(bm.faces)*.6:
  # An unusually broad mount must be severed across its complete narrow neck.
  # Other static ribbons are welded back in copy_faces, preserving their shape.
  bm.free();bm=bmesh.new();bm.from_mesh(ob.data)
  neckEdges=slice_split(bm,spindle,Vector((0,0,1)))
  groups=components(bm);tip=max(bm.verts,key=lambda v:v.co.z)
  rotor=[f for g in groups if any(tip in f.verts for f in g) or (bb_faces(g)[0].z>=cut-.0001 and bb_faces(g)[0].y>-.04) for f in g]
 if len(rotor)<60 or len(rotor)>len(bm.faces)*.6:raise RuntimeError(f'{name} invalid rotor selection {len(rotor)}/{len(bm.faces)}')
 rotorSet=set(rotor);static=[f for f in bm.faces if f not in rotorSet]
 rotorObj,rotorCaps=copy_faces(bm,rotor,'TopRotor',src.material(directory))
 staticObj,_=copy_faces(bm,static,'FixedTemporary',rotorObj.data.materials[0]);bm.free()
 # The central lower pad supplies the foremost physical case plane; shoulder
 # loop ribbons are cut just in front of it, not across the case front panel.
 a=np.array([list(v.co) for v in staticObj.data.vertices]);q=a[(abs(a[:,0])<.24)&(a[:,2]>.30)&(a[:,2]<CASE_TOP[name]*.80)]
 # A few stray strap tips cross the narrow central sample on Voyager; a small
 # robust percentile rejects those tips without moving the actual padded wall.
 plane=float(np.quantile(q[:,1],.02) if name=='Piko_Voyager' else q[:,1].min())-.025
 bm=bmesh.new();bm.from_mesh(staticObj.data)
 slice_split(bm,Vector((0,plane,0)),Vector((0,1,0)))
 groups=components(bm);case=[];hl=[];hr=[]
 for g in groups:
  lo,hi=bb_faces(g)
  if hi.y<=plane+2e-5 and len(g)>3:
   (hl if (lo.x+hi.x)/2<0 else hr).extend(g)
  else:case.extend(g)
 if not hl or not hr:
  # Some generated harnesses join their two loops with a lower chest bridge.
  # Separate that bridge on the midline, giving fitting two true loop controls.
  slice_split(bm,Vector((0,0,0)),Vector((1,0,0)))
  groups=components(bm);case=[];hl=[];hr=[]
  for g in groups:
   lo,hi=bb_faces(g)
   if hi.y<=plane+2e-5 and len(g)>3:(hl if (lo.x+hi.x)/2<0 else hr).extend(g)
   else:case.extend(g)
 if not hl or not hr:raise RuntimeError(f'{name} harness did not separate L={len(hl)} R={len(hr)}')
 mat=rotorObj.data.materials[0]
 caseObj,caseCaps=copy_faces(bm,case,'Case',mat);left,leftCaps=copy_faces(bm,hl,'Harness.L',mat);right,rightCaps=copy_faces(bm,hr,'Harness.R',mat);bm.free()
 bpy.data.objects.remove(ob,do_unlink=True);bpy.data.objects.remove(staticObj,do_unlink=True)
 obs=[caseObj,left,right,rotorObj]
 pivot=spindle.copy()
 bearingRepair=None
 if name=='Fira_Beacon':
  # The 3-cup rotor's horizontal bearing is at the petal hub, above its pole.
  pivot.z=2.13
  bearingRepair=repair_beacon_bearing(rotorObj,caseObj,spindle)
 pivot,scale,sourceCenter=normalize_parts(obs,pivot)
 root=empty('BackpackRoot');top=empty('TopPivot',root,pivot)
 for o in obs:
  o.parent=top if o==rotorObj else root
  if o==rotorObj:
   for v in o.data.vertices:v.co-=pivot
  # Mobile-friendly export derived from the detailed retained source.
  tris=sum(len(f.vertices)-2 for f in o.data.polygons)
  budget=18000 if o==caseObj else 6000 if o==rotorObj else 3000
  if tris>budget:
   bpy.context.view_layer.objects.active=o;mod=o.modifiers.new('Mobile silhouette preserving reduction','DECIMATE');mod.ratio=budget/tris;mod.use_collapse_triangulate=True;bpy.ops.object.modifier_apply(modifier=mod.name)
 bpy.context.view_layer.update()
 output=src.OUT/'Backpacks'/name;output.mkdir(parents=True,exist_ok=True);write_textures(directory,output)
 # All numeric locations below describe Blender axes. The Unity conversion is
 # written explicitly and round-trip validation is provided separately.
 fixedBounds=src.bounds(caseObj)
 bodyPoints=[v.co for v in caseObj.data.vertices if v.co.z<=(CASE_TOP[name]-sourceCenter.z)*scale]
 cb=[[min(p[i] for p in bodyPoints) for i in range(3)],[max(p[i] for p in bodyPoints) for i in range(3)]]
 hb=[src.bounds(left),src.bounds(right)]
 shoulders=[];bottom=[]
 for o in (left,right):
  pts=[v.co for v in o.data.vertices];upper=max(pts,key=lambda p:p.z);lower=min(pts,key=lambda p:p.z)
  shoulders.append(list(upper));bottom.append(list(lower))
 meta={'id':name,'kind':'Backpack','frame':'Blender Z-up / face -Y; FBX -Z-forward Y-up','fixedMeshName':'Case','movableMeshName':'TopRotor','topPivotName':'TopPivot','harnessNames':['Harness.L','Harness.R'],
       'caseBoundsMin':cb[0],'caseBoundsMax':cb[1], 'fixedBoundsMin':fixedBounds[0],'fixedBoundsMax':fixedBounds[1],
       'contactPlaneY':(plane+.025-sourceCenter.y)*scale,'rawToPreparedScale':scale,
       'harnessBoundsMin':[min(h[0][i] for h in hb) for i in range(3)],'harnessBoundsMax':[max(h[1][i] for h in hb) for i in range(3)],
       'harnessShouldersL':shoulders[0],'harnessShouldersR':shoulders[1],'harnessBottomL':bottom[0],'harnessBottomR':bottom[1],
       'referenceBodyWidth':1.0,'referenceBodyDepth':max(.5,abs(min(h[0][1] for h in hb)-cb[0][1])),
       'topPivot':list(pivot),'topPivotUnity':[pivot.x,pivot.z,-pivot.y],
       'spinAxisUnity':[0,0,1] if name=='Fira_Beacon' else [0,1,0],
       'source':before,'outputTriangles':{o.name:sum(len(f.vertices)-2 for f in o.data.polygons) for o in obs},
       'repairs':{'localizedSpindleCutEdges':neckEdges,'rotorCaps':rotorCaps,'caseCaps':caseCaps,'leftHarnessCaps':leftCaps,'rightHarnessCaps':rightCaps,'horizontalBearing':bearingRepair},
       'sourceArchiveSha256':hashlib.sha256((src.IN/name/(name+'.zip')).read_bytes()).hexdigest()}
 (output/'Meta.json').write_text(json.dumps(meta,indent=2))
 for image in bpy.data.images:
  if image.source=='FILE':
   try:image.pack()
   except RuntimeError:pass
 bpy.ops.wm.save_as_mainfile(filepath=str(output/'editable.blend'))
 bpy.ops.object.select_all(action='DESELECT')
 for o in [root,top]+obs:o.select_set(True)
 bpy.context.view_layer.objects.active=root
 bpy.ops.export_scene.fbx(filepath=str(output/'Model.fbx'),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_custom_props=True)
 preview(name,obs,top,'prepared')
 print('PACK_PREPARED',json.dumps(meta),flush=True)
 return meta

if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(CUTS)
 results=[]
 for name in names:
  try:results.append(prepare(name))
  except Exception as e:
   import traceback;traceback.print_exc();results.append({'id':name,'error':repr(e)})
  (src.QA/'prepared-report.json').write_text(json.dumps(results,indent=2))
