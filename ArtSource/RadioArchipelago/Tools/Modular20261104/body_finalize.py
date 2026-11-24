"""Final open-eye rest pose, native renders and exported-rig checks."""
import pathlib,sys,json,bpy,math,hashlib
import numpy as np
from mathutils import Vector,Quaternion
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
import body_author as A
reports=[]
selected=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else None
for folder in sorted(A.OUT.iterdir()):
 if not folder.is_dir():continue
 if selected and folder.name not in selected:continue
 name=folder.name;bpy.ops.wm.open_mainfile(filepath=str(folder/(name+'_Rigged.blend')))
 obj=next(o for o in bpy.context.scene.objects if o.type=='MESH');rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
 for pb in rig.pose.bones:pb.rotation_mode='QUATERNION';pb.rotation_quaternion=(1,0,0,0)
 meta=json.loads((folder/'Meta.json').read_text(encoding='utf-8'))
 if not obj.data.shape_keys:meta['blink']=A.blink_shape(obj)
 assert obj.data.shape_keys and 'Blink' in obj.data.shape_keys.key_blocks,name+' missing Blink'
 obj.data.shape_keys.key_blocks['Blink'].value=0
 meta['blinkRestValue']=0;meta['recommendedArmRestDegreesDown']=55
 torsoIds={g.index for g in obj.vertex_groups if g.name in ('root','body','head')}
 points=np.array([v.co[:] for v in obj.data.vertices if sum(g.weight for g in v.groups if g.group in torsoIds)>.7]);sections=[]
 for z in [.25,.35,.45,.55,.65,.75,.85,.90,.95]:
  band=points[abs(points[:,2]-z*A.H)<.02*A.H]
  if len(band):sections.append({'height':z*A.H,'halfWidth':float(np.percentile(abs(band[:,0]),98)),'frontY':float(np.percentile(band[:,1],2)),'backY':float(np.percentile(band[:,1],98))})
 meta['bodyCrossSections']=sections
 if len(meta.get('blink',{}).get('eyes',[]))==2:meta['eyesCenter']=((np.array(meta['blink']['eyes'][0]['center'])+np.array(meta['blink']['eyes'][1]['center']))*.5).tolist()
 meta['sourceTextureDimensions']={n:list(bpy.data.images.load(str(folder/n),check_existing=True).size) for n in ['BaseColor.png','Normal.png','MetallicSmoothness.png']}
 # Preserve the true neutral T-pose in the editable source and FBX.
 bpy.ops.wm.save_as_mainfile(filepath=str(folder/(name+'_Rigged.blend')))
 bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);obj.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=str(folder/'Model.fbx'),use_selection=True,object_types={'ARMATURE','MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,use_armature_deform_only=False,mesh_smooth_type='OFF',path_mode='AUTO')
 A.setup_render();A.render(name,'front',(0,-7,A.H*.53));A.render(name,'right',(7,0,A.H*.53))
 for side in ['L','R']:
  pb=rig.pose.bones['arm.'+side];axis=pb.bone.matrix_local.inverted().to_3x3()@Vector((0,1,0));pb.rotation_quaternion=Quaternion(axis,math.radians(-32 if side=='L' else 32))
 A.render(name,'raised',(2,-7,A.H*.60))
 for side in ['L','R']:rig.pose.bones['arm.'+side].rotation_quaternion=(1,0,0,0)
 obj.data.shape_keys.key_blocks['Blink'].value=1;A.render(name,'blink',(0,-7,A.H*.53));obj.data.shape_keys.key_blocks['Blink'].value=0
 meta['outputSha256']={n:hashlib.sha256((folder/n).read_bytes()).hexdigest() for n in ['Model.fbx','BaseColor.png','Normal.png','MetallicSmoothness.png']}
 (folder/'Meta.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
 reports.append(meta);print('BODY_FINAL',name,flush=True)
reports=[json.loads(p.read_text(encoding='utf-8')) for p in sorted(A.OUT.glob('*/Meta.json'))]
(A.QA/'rig-report.json').write_text(json.dumps(reports,indent=2),encoding='utf-8')
