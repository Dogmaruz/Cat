import bpy,pathlib,sys,json,numpy as np
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE));import polish_islets as p
PATH=p.a.OUT/'emerald_abyss_island.fbx';bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=str(PATH),use_custom_normals=True,use_image_search=False,colors_type='LINEAR');removed=[]
for o in bpy.context.scene.objects:
 if o.type!='MESH':continue
 m=o.data;norms=np.empty(len(m.loops)*3,np.float64);m.corner_normals.foreach_get('vector',norms);norms=norms.reshape(-1,3);bad=set(int(i) for i in np.flatnonzero(np.abs(np.linalg.norm(norms,axis=1)-1)>.001))
 if not bad:continue
 assert o.get('unity_material_bucket')=='Architecture',o.name
 badfaces=[face.index for face in m.polygons if any(li in bad for li in face.loop_indices)];assert len(badfaces)<=2,(o.name,badfaces)
 keep=[face for face in m.polygons if face.index not in badfaces];loops=np.array([li for face in keep for li in face.loop_indices],np.int32);verts=np.empty(len(m.vertices)*3,np.float64);m.vertices.foreach_get('co',verts);verts=verts.reshape(-1,3);new=bpy.data.meshes.new(m.name+'_without_two_degenerates');new.from_pydata(verts,[],[tuple(face.vertices) for face in keep]);new.update()
 for mat in m.materials:new.materials.append(mat)
 cv=np.empty(len(m.color_attributes['Color'].data)*4,np.float32);m.color_attributes['Color'].data.foreach_get('color',cv);cv=cv.reshape(-1,4)[loops];attr=new.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',cv.reshape(-1))
 for old in m.uv_layers:
  uv=np.empty(len(old.data)*2,np.float32);old.data.foreach_get('uv',uv);uv=uv.reshape(-1,2)[loops];layer=new.uv_layers.new(name=old.name);layer.data.foreach_set('uv',uv.reshape(-1))
 for face,old in zip(new.polygons,keep):face.use_smooth=old.use_smooth;face.material_index=old.material_index
 vals=norms[loops];vals/=np.linalg.norm(vals,axis=1)[:,None];new.normals_split_custom_set(vals);o.data=new;removed.append(dict(mesh=o.name,removed_faces=badfaces))
bpy.ops.object.select_all(action='SELECT');bpy.ops.export_scene.fbx(filepath=str(PATH),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=False,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
(HERE/'decor_normal_cleanup.json').write_text(json.dumps(removed,indent=2));print('DECO_TWO_FACES_REMOVED',removed,flush=True)
