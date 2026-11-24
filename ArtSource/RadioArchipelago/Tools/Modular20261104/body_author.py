"""Non-destructive one-bone-limb authoring for the user's modular Tripo bodies.
Run in Blender background. Source ZIPs and extracted UV/PBR images are read-only.
"""
import bpy, pathlib, sys, json, math, shutil, hashlib
import numpy as np
from mathutils import Vector
ROOT=pathlib.Path('C:/Users/Derek/Games_develop/Cat')
SRC=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/_Sources/Characters'
OUT=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Characters'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/BodyRigReview'
QA.mkdir(parents=True,exist_ok=True)
H=2.1
def smooth(a,b,v):
 t=max(0,min(1,(v-a)/(b-a)));return t*t*(3-2*t)
def vec(v):return [round(float(a),7) for a in v]
def make_mat(folder,dest,name):
 for f,out in [('Color.png','BaseColor.png'),('Normal.png','Normal.png')]: shutil.copy2(next(folder.rglob(f)),dest/out)
 met=next(folder.rglob('*_metallic.PNG'));rough=next(folder.rglob('*_roughness.PNG'))
 mi=bpy.data.images.load(str(met),check_existing=True);mi.colorspace_settings.name='Non-Color'
 ri=bpy.data.images.load(str(rough),check_existing=True);ri.colorspace_settings.name='Non-Color'
 w,h=mi.size; a=np.empty(w*h*4,dtype=np.float32);mi.pixels.foreach_get(a); a=a.reshape((-1,4));b=np.empty(w*h*4,dtype=np.float32);ri.pixels.foreach_get(b);b=b.reshape((-1,4))
 packed=np.zeros((w*h,4),np.float32);packed[:,0]=a[:,0];packed[:,3]=1-b[:,0]
 im=bpy.data.images.new(name+'_MetallicSmoothness',width=w,height=h,alpha=True);im.colorspace_settings.name='Non-Color';im.pixels.foreach_set(packed.ravel());im.filepath_raw=str(dest/'MetallicSmoothness.png');im.file_format='PNG';im.save()
 m=bpy.data.materials.new(name+'_Surface');m.use_nodes=True;bs=m.node_tree.nodes.get('Principled BSDF');bs.inputs['Roughness'].default_value=.65
 for file,socket,cs in [('BaseColor.png','Base Color','sRGB'),(str(met),'Metallic','Non-Color'),(str(rough),'Roughness','Non-Color')]:
  path=dest/file if file in ['BaseColor.png'] else pathlib.Path(file)
  tx=m.node_tree.nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(str(path),check_existing=True);tx.image.colorspace_settings.name=cs;m.node_tree.links.new(tx.outputs['Color'],bs.inputs[socket])
 tx=m.node_tree.nodes.new('ShaderNodeTexImage');tx.image=bpy.data.images.load(str(dest/'Normal.png'),check_existing=True);tx.image.colorspace_settings.name='Non-Color';nm=m.node_tree.nodes.new('ShaderNodeNormalMap');nm.inputs['Strength'].default_value=.55;m.node_tree.links.new(tx.outputs['Color'],nm.inputs['Color']);m.node_tree.links.new(nm.outputs['Normal'],bs.inputs['Normal'])
 return m
def setup_render():
 scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=16;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100
 scene.world=bpy.data.worlds.new('ReviewWorld');scene.world.color=(.22,.22,.22)
 scene.view_settings.view_transform='AgX'
 for loc,power,size in [((-3,-4,5),220,4),((3,-2,2),90,4),((1,3,4),250,3)]:
  bpy.ops.object.light_add(type='AREA',location=loc);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,H*.5))-o.location).to_track_quat('-Z','Y').to_euler()
def render(name,view,campos):
 center=Vector((0,0,H*.52));bpy.ops.object.camera_add(location=campos);cam=bpy.context.object;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=H*1.3;bpy.context.scene.camera=cam;bpy.context.scene.render.filepath=str(QA/(name+'_'+view+'.png'));bpy.ops.render.render(write_still=True);bpy.data.objects.remove(cam,do_unlink=True)
def blink_shape(obj):
 # The source's eyes are painted into the mesh UV map. Detect only front-facing
 # navy islands in the central face; never add floating replacement eye geometry.
 p=np.array([v.co[:] for v in obj.data.vertices]); col=bpy.data.images.load(str(next(pathlib.Path(obj['sourceFolder']).rglob('Color.png'))),check_existing=True);w,h=col.size;pixels=np.empty(w*h*4,np.float32);col.pixels.foreach_get(pixels);pixels=pixels.reshape((h,w,4))
 dark=np.zeros(len(p),dtype=bool);uv=obj.data.uv_layers.active
 if uv is None:return {'created':False,'reason':'no UV'}
 for loop in obj.data.loops:
  i=loop.vertex_index;x,y,z=p[i]
  if abs(x)>.2*H or not .50*H<z<.84*H or y>-.15*H:continue
  u,v=uv.data[loop.index].uv;c=pixels[min(h-1,max(0,int(v*h))),min(w-1,max(0,int(u*w))),:3]
  # Rumi/Vela deliberately have brighter blue eyes; their approved source paint
  # must not be recolored merely to satisfy the dark-navy detector.
  confident=(c[0]<.25 and c[1]<.32 and c[2]<.70 and c[2]>c[0]*1.10) if obj.name.startswith(('Rumi','Vela')) else (max(c)<.23 and c[2]>c[0]*1.03)
  if confident:dark[i]=True
 ids=np.where(dark)[0];details=[]
 if len(ids)<30:return {'created':False,'reason':'no confident eye pixels','vertices':len(ids)}
 basis=obj.shape_key_add(name='Basis');blink=obj.shape_key_add(name='Blink');blink.value=0;total=0
 for sign in (-1,1):
  selected=ids[p[ids,0]*sign>0]
  if len(selected)<10:continue
  q=p[selected];c=np.median(q,axis=0);rx=max(.018,(np.percentile(q[:,0],97)-np.percentile(q[:,0],3))*.5);rz=max(.035,(np.percentile(q[:,2],97)-np.percentile(q[:,2],3))*.5)
  # Feather into surrounding face so eyelid collapse has no tear or shading seam.
  for i,(x,y,z) in enumerate(p):
   if abs(x-c[0])>rx*2 or abs(z-c[2])>rz*1.7 or abs(y-c[1])>.055*H:continue
   r=max(abs(x-c[0])/max(rx*1.15,.001),abs(z-c[2])/max(rz*1.2,.001));f=1-smooth(.75,1.5,r)
   if f>0:blink.data[i].co.z=z+(c[2]-z)*.94*f;total+=1
  details.append({'center':vec(c),'radiusX':float(rx),'radiusZ':float(rz),'darkVertices':len(selected)})
 return {'created':len(details)==2,'eyes':details,'affectedVertices':total}
def author(name):
 folder=SRC/name;dest=OUT/name;dest.mkdir(parents=True,exist_ok=True)
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(next(folder.glob('*.fbx'))))
 meshes=[o for o in bpy.context.scene.objects if o.type=='MESH'];assert len(meshes)==1
 obj=meshes[0];obj.name=name+'_Body';bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 p=np.array([v.co[:] for v in obj.data.vertices]);mn=p.min(axis=0);mx=p.max(axis=0);scale=H/(mx[2]-mn[2]);center=np.array([(mn[0]+mx[0])*.5,(mn[1]+mx[1])*.5,mn[2]]);p=(p-center)*scale
 for v,co in zip(obj.data.vertices,p):v.co=co
 obj['sourceFolder']=str(folder);obj.data.materials.clear();obj.data.materials.append(make_mat(folder,dest,name))
 for poly in obj.data.polygons:poly.use_smooth=True
 # Uniform Tripo quad sampling makes the median of outer-arm vertices a stable
 # arm axis. Root positions are measured against the adjacent torso silhouette.
 maxx=np.max(np.abs(p[:,0]));outer=p[(abs(p[:,0])>maxx*.78)&(p[:,2]>.25*H)&(p[:,2]<.70*H)];az=float(np.median(outer[:,2]));ay=float(np.median(outer[:,1]))
 radii=[]
 for z in [az-.10*H,az+.10*H]:
  section=p[abs(p[:,2]-z)<.015*H];radii.append(float(np.percentile(abs(section[:,0]),98)))
 sx=float(sum(radii)/len(radii))*.97;ax=maxx-.04*H
 feet=[]
 for side in [-1,1]:
  q=p[(p[:,2]<.075*H)&(p[:,0]*side>.02*H)];feet.append([float(np.median(q[:,0])),float(np.median(q[:,1])),.115*H])
 bpy.ops.object.armature_add(enter_editmode=True,location=(0,0,0));rig=bpy.context.object;rig.name=name+'_Rig';bpy.ops.armature.select_all(action='SELECT');bpy.ops.armature.delete()
 def bone(n,a,b,parent=None,deform=True):
  bn=rig.data.edit_bones.new(n);bn.head=a;bn.tail=b;bn.use_deform=deform
  if parent:bn.parent=rig.data.edit_bones[parent]
  return bn
 bone('root',(0,0,0),(0,0,.20*H));bone('body',(0,0,.25*H),(0,0,.57*H),'root');bone('head',(0,0,.57*H),(0,0,.84*H),'body')
 # Character-left is positive Blender X when facing -Y.
 bone('arm.L',(sx,ay,az),(ax,ay,az),'body');bone('arm.R',(-sx,ay,az),(-ax,ay,az),'body')
 for label,f in [('foot.R',feet[0]),('foot.L',feet[1])]:bone(label,f,(f[0],f[1]-.02,.025*H),'body')
 bpy.ops.object.mode_set(mode='OBJECT');obj.parent=rig;mod=obj.modifiers.new('OneBoneLimbSkin','ARMATURE');mod.object=rig
 groups={n:obj.vertex_groups.new(name=n) for n in ['root','body','head','arm.L','arm.R','foot.L','foot.R']}
 counts={k:0 for k in groups};maxweights=0
 for i,(x,y,z) in enumerate(p):
  # Local shoulder blend runs through the existing rings; all distal arm vertices
  # have exactly one deform influence, with no elbow in the skeleton.
  arm=smooth(sx-.035*H,sx+.045*H,abs(x))*(1-smooth(.065*H,.14*H,abs(z-az)))
  foot=(1-smooth(.075*H,.155*H,z))*smooth(.01*H,.05*H,abs(x))
  arm=min(1,arm);foot=min(1-arm,foot);remain=1-arm-foot
  head=smooth(.53*H,.66*H,z)*remain
  ws={'arm.L' if x>0 else 'arm.R':arm,'foot.L' if x>0 else 'foot.R':foot,'head':head,'body':remain-head}
  active=0
  for n,w in ws.items():
   if w>1e-6:groups[n].add([i],float(w),'REPLACE');counts[n]+=1;active+=1
  maxweights=max(maxweights,active)
 blink=blink_shape(obj)
 bpy.context.view_layer.update()
 # Match old courier idle authored rest: short hands angle down from their actual
 # shoulders. This remains a rest pose for the motion system, not an elbow bend.
 meta={'id':name,'version':1,'coordinateSystem':'Blender Z up; face -Y; FBX -Z/Y','height':H,'sourceScale':scale,'sourceCenter':vec(center),'boundsMin':vec(p.min(axis=0)),'boundsMax':vec(p.max(axis=0)),'shoulders':{'left':vec((sx,ay,az)),'right':vec((-sx,ay,az))},'hips':{'left':feet[1],'right':feet[0]},'headCenter':vec((0,0,H*.74)),'neck':vec((0,0,H*.61)),'backpackMount':vec((0,float(p[:,1].max()),H*.50)),'bodyHalfWidth':sx,'bodyDepth':float(p[:,1].max()-p[:,1].min()),'armHeight':az,'bones':list(groups),'vertices':len(p),'polygons':len(obj.data.polygons),'triangles':sum(len(f.vertices)-2 for f in obj.data.polygons),'maxInfluences':maxweights,'weightVertexCounts':counts,'singleBonePerLimb':True,'noElbowOrKneeBones':True,'blink':blink,'preservedUV':True,'preservedSourceTopology':True}
 (dest/'Meta.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
 # Save source master before adding review lights and cameras.
 for im in bpy.data.images:
  if im.source=='FILE':
   try:im.pack()
   except:pass
 bpy.ops.wm.save_as_mainfile(filepath=str(dest/(name+'_Rigged.blend')))
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(dest/'Model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,mesh_smooth_type='OFF',path_mode='AUTO')
 setup_render();render(name,'front',(0,-7,H*.53));render(name,'right',(7,0,H*.53))
 from mathutils import Quaternion
 for side in ['L','R']:
  pb=rig.pose.bones['arm.'+side];pb.rotation_mode='QUATERNION';axis=pb.bone.matrix_local.inverted().to_3x3()@Vector((0,1,0));pb.rotation_quaternion=Quaternion(axis,math.radians(-32 if side=='L' else 32))
 render(name,'raised',(2,-7,H*.60))
 for side in ['L','R']:rig.pose.bones['arm.'+side].rotation_quaternion=(1,0,0,0)
 print('BODY_RIG_PASS',name,json.dumps(meta),flush=True)
 return meta
if __name__=='__main__':
 names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [p.name for p in sorted(SRC.iterdir()) if p.is_dir()]
 results=[author(n) for n in names]
 (QA/'rig-report.json').write_text(json.dumps(results,indent=2),encoding='utf-8')
