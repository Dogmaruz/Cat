"""Independent exported-FBX validation, not only in-memory authoring state."""
import bpy, pathlib,json,math,hashlib
import numpy as np
ROOT=pathlib.Path('C:/Users/Derek/Games_develop/Cat')
OUT=ROOT/'ArtSource/RadioArchipelago/ProcessedModular/Characters'
QA=ROOT/'Docs/Design/RadioArchipelago/ModularCast20261104/BodyRigReview'
reports=[]
for folder in sorted(OUT.iterdir()):
 if not folder.is_dir():continue
 name=folder.name;meta=json.loads((folder/'Meta.json').read_text(encoding='utf-8'))
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(folder/'Model.fbx'))
 rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE');obj=next(o for o in bpy.context.scene.objects if o.type=='MESH')
 names=set(b.name for b in rig.data.bones);expected={'root','body','head','arm.L','arm.R','foot.L','foot.R'}
 assert names==expected,(name,names)
 assert len(obj.data.vertices)==meta['vertices'],name
 assert len(obj.data.polygons)==meta['polygons'],name
 assert obj.data.uv_layers and len(obj.data.uv_layers.active.data)==len(obj.data.loops),name
 assert obj.data.shape_keys.key_blocks['Blink'].value==0,name
 maximum=0;errors=[]
 for v in obj.data.vertices:
  total=sum(g.weight for g in v.groups);maximum=max(maximum,len(v.groups))
  if abs(total-1)>1e-4:errors.append(v.index)
  assert all(math.isfinite(a) for a in v.co),name
 assert not errors,(name,len(errors))
 torsoIds={g.index for g in obj.vertex_groups if g.name in ('root','body','head')}
 points=np.array([list(obj.matrix_world@v.co) for v in obj.data.vertices if sum(g.weight for g in v.groups if g.group in torsoIds)>.7]);sections=[]
 for z in [.25,.35,.45,.55,.65,.75,.85,.90,.95]:
  band=points[abs(points[:,2]-z*2.1)<.02*2.1]
  if len(band):sections.append({'height':z*2.1,'halfWidth':float(np.percentile(abs(band[:,0]),98)),'frontY':float(np.percentile(band[:,1],2)),'backY':float(np.percentile(band[:,1],98))})
 meta['bodyCrossSections']=sections
 if len(meta.get('blink',{}).get('eyes',[]))==2:meta['eyesCenter']=((np.array(meta['blink']['eyes'][0]['center'])+np.array(meta['blink']['eyes'][1]['center']))*.5).tolist()
 (folder/'Meta.json').write_text(json.dumps(meta,indent=2),encoding='utf-8')
 for side in ('L','R'):
  bn=rig.data.bones['arm.'+side]
  assert len(bn.children)==0,(name,bn.name,'unexpected child/elbow')
  assert bn.parent.name=='body'
  foot=rig.data.bones['foot.'+side]
  assert len(foot.children)==0,(name,foot.name,'unexpected knee')
 item={'id':name,'exportedFbxPassed':True,'vertices':len(obj.data.vertices),'faces':len(obj.data.polygons),'bones':sorted(names),'maxInfluences':maximum,'weightSumErrors':len(errors),'blinkRest':obj.data.shape_keys.key_blocks['Blink'].value,'sourceTopologyCountsPreserved':True,'uvLoops':len(obj.data.uv_layers.active.data),'singleBoneLimbs':True,'fbxSha256':hashlib.sha256((folder/'Model.fbx').read_bytes()).hexdigest()}
 reports.append(item);print('BODY_EXPORT_VALIDATED',name,flush=True)
(QA/'export-validation.json').write_text(json.dumps({'passed':len(reports)==10,'bodyCount':len(reports),'reports':reports},indent=2),encoding='utf-8')
