import bpy,pathlib,json,collections,math
HERE=pathlib.Path(__file__).resolve().parent
def load(path):
 bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(path),use_custom_normals=True,use_custom_props=True)
 ob=bpy.data.objects['RA_Coral_Water_+01_-02_01'];m=ob.data;out={}
 for face in m.polygons:
  ps=[ob.matrix_world@m.vertices[m.loops[i].vertex_index].co for i in face.loop_indices]
  if min(p.y for p in ps)<=-68+.0001:continue
  row=[]
  for i,p in zip(face.loop_indices,ps):
   row.append((tuple(p),tuple(m.uv_layers.active.data[i].uv),tuple(m.color_attributes.active_color.data[i].color),tuple(m.corner_normals[i].vector)))
  out[tuple(sorted(tuple(round(x,5)for x in p)for p in ps))]=sorted(row)
 return out
old=load(HERE.parent/'RooftopMechanisms20261101/STAGING/Coral/coral_oasis_island.fbx')
new=load(HERE/'STAGING/Coral/coral_oasis_island.fbx')
examples=[];maxima=[0,0,0,0]
for k,rows in old.items():
 if k not in new:continue
 diffs=[max(abs(a-b)for x,y in zip(rows,new[k])for a,b in zip(x[c],y[c]))for c in range(4)]
 maxima=[max(a,b)for a,b in zip(maxima,diffs)]
 if max(diffs)>1e-6 and len(examples)<2:examples.append(dict(old=rows,new=new[k],diffs=diffs))
r=dict(face_counts=[len(old),len(new)],common_faces=len(set(old)&set(new)),max_position_uv_color_normal_diffs=maxima,examples=examples)
(HERE/'pool_diagnosis.json').write_text(json.dumps(r,indent=2));print(json.dumps(r,indent=2),flush=True)
