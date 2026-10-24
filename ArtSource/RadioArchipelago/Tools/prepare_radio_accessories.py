"""Mechanical antenna seams and flexible scarf chains on the reviewed supplied rigs.

Run in a separate background Blender. Original reviewed payloads are backed up in
ignored Library before the first edit; retries always start from that backup.
Only local mechanical cuts/caps and Lumen's unwanted lower brace change topology.
"""
from __future__ import annotations
import argparse, hashlib, json, math, pathlib, shutil, sys, tempfile
import bpy, bmesh, numpy as np
from mathutils import Matrix, Quaternion, Vector

ROOT = pathlib.Path(__file__).resolve().parents[3]
OUT = ROOT / "DancingCatsClone/Library/RadioRigging/AccessoryReview"
PROCESSED = ROOT / "ArtSource/RadioArchipelago/Processed"
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
import export_prepared_rigs as exporter

CHARACTERS = ("Tiko", "Lumen", "Nori")
HINGES = {"Tiko": ((-.69,.64,1.65),.32), "Nori": ((-.84,.75,1.83),.28),
          "Lumen": ((.142,-.054,1.86),.11)}

def require(value, message):
    if not value: raise RuntimeError(message)

def centroid(face):
    return face.calc_center_median()

def bisect_local(bm, center, normal, radius):
    normal = normal.normalized()
    selected = [face for face in bm.faces if any(
        abs((v.co-center).dot(normal)) < .18 and
        ((v.co-center)-normal*(v.co-center).dot(normal)).length < radius for v in face.verts)]
    geom = set(selected)
    for face in selected: geom.update(face.edges); geom.update(face.verts)
    result = bmesh.ops.bisect_plane(bm, geom=list(geom), dist=1e-6,
        plane_co=center, plane_no=normal, clear_inner=False, clear_outer=False)
    edges = [e for e in result['geom_cut'] if isinstance(e,bmesh.types.BMEdge)
        and all(abs((v.co-center).dot(normal)) < .0001 for v in e.verts)]
    require(len(edges) >= 3, f"Mechanical section did not intersect a closed tube at {center}: {len(edges)}")
    return edges

def cap_boundaries(bm, selected_edges, normals, recess=0.):
    edges=[edge for edge in selected_edges if edge.is_valid and edge.is_boundary]
    require(edges, "No open edges to seal after mechanical cut")
    result=bmesh.ops.holes_fill(bm, edges=edges, sides=0)
    faces=result['faces']; require(faces,"Mechanical cut did not produce a sealed end")
    bm.normal_update(); uv=bm.loops.layers.uv.active
    for face in faces:
        for loop in face.loops:
            source=next((other for other in loop.vert.link_loops if other.face not in faces),None)
            if uv and source: loop[uv].uv=source[uv].uv.copy()
            loop[normals]=face.normal
    if recess:
        recessed=[]
        for face in list(faces):
            direction=-face.normal.normalized()*recess
            previous=set(bm.faces)
            extruded=bmesh.ops.extrude_face_region(bm,geom=[face])
            for vertex in extruded['geom']:
                if not isinstance(vertex,bmesh.types.BMVert):continue
                vertex.co+=direction
                # New internal cap vertices must keep the same Blink displacement.
                for layer in bm.verts.layers.shape.values():vertex[layer]+=direction
            added=[item for item in bm.faces if item not in previous]
            # Extrusion keeps a reversed input face; remove it so the old cut
            # plane has no coincident cap and the cavity stays edge-manifold.
            bmesh.ops.delete(bm,geom=[face],context='FACES_ONLY')
            bm.normal_update()
            for item in added:
                for loop in item.loops:loop[normals]=item.normal
            recessed.extend(added)
        faces=recessed
    bmesh.ops.triangulate(bm, faces=faces)
    return len(faces)

def components(bm):
    remaining=set(bm.faces); result=[]
    while remaining:
        component=set(); stack=[remaining.pop()]
        while stack:
            face=stack.pop(); component.add(face)
            for edge in face.edges:
                for other in edge.link_faces:
                    if other in remaining: remaining.remove(other);stack.append(other)
        result.append(component)
    return result

def prepare_mesh(name, mesh):
    bm=bmesh.new(); bm.from_mesh(mesh.data)
    deform=bm.verts.layers.deform.verify()
    normal_layer=bm.loops.layers.float_vector.new('AccessorySourceNormal')
    for face, polygon in zip(bm.faces,mesh.data.polygons):
        for loop,index in zip(face.loops,polygon.loop_indices): loop[normal_layer]=mesh.data.corner_normals[index].vector
    before={'vertices':len(bm.verts),'triangles':len(bm.faces),'boundary_edges':sum(e.is_boundary for e in bm.edges)}
    removed=0
    if name=='Lumen':
        image=next(i for i in bpy.data.images if i.source=='FILE' and 'BaseColor' in i.name)
        sample=image.copy();sample.scale(512,512)
        pixels=np.array(sample.pixels[:],dtype=np.float32).reshape((512,512,4));bpy.data.images.remove(sample)
        uv=bm.loops.layers.uv.active
        def gold(face):
            coordinates=[loop[uv].uv for loop in face.loops]
            coordinates.append(sum(coordinates,Vector((0,0)))/len(coordinates))
            for coord in coordinates:
                red,green,blue=pixels[min(511,max(0,int(coord.y*512))),min(511,max(0,int(coord.x*512))),:3]
                if blue>green*.68 or red>green*1.78:return False
            return True
        a=Vector((-.26811686,.35604754,1.37));b=Vector((-.68311685,.71604753,1.45)); axis=(b-a).normalized()
        c0=a+axis*.065;c1=a+axis*.49
        bisect_local(bm,c0,axis,.11);bisect_local(bm,c1,axis,.11)
        remove=[]
        for face in bm.faces:
            p=centroid(face)-a;t=p.dot(axis);r=(p-axis*t).length
            if .065001<t<.489999 and r<.10 and gold(face): remove.append(face)
        require(len(remove)>8,f'No unwanted lower sail brace found: {len(remove)}')
        removed=len(remove);bmesh.ops.delete(bm,geom=remove,context='FACES')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
        # The supplied tube blends into its gold corner. Seal the complete
        # local boundary, including that curved intersection, not just coplanar
        # section edges. The original Lumen source has no boundary edges.
        ends=[e for e in bm.edges if e.is_boundary]
        cap_boundaries(bm,ends,normal_layer)
    point,radius=HINGES[name];point=Vector(point)
    cut=bisect_local(bm,point,Vector((0,0,1)),radius)
    pivot=sum((v.co for e in cut for v in e.verts),Vector())/(len(cut)*2)
    bmesh.ops.split_edges(bm,edges=cut)
    comps=components(bm)
    tip=Vector({'Tiko':(.3893,-.1699,2.314),'Nori':(.5148,-.5959,2.197),'Lumen':(.1419,-.05395,2.53)}[name])
    dynamic=min(comps,key=lambda c:min((centroid(f)-tip).length_squared for f in c))
    require(100<len(dynamic)<len(bm.faces)*.4,f'{name}: antenna section did not detach: {len(dynamic)}/{len(bm.faces)}; components {[len(c) for c in comps]}')
    movable={v for face in dynamic for v in face.verts}
    groups={group.name:group.index for group in mesh.vertex_groups}
    antenna={index for key,index in groups.items() if key.startswith('antenna.')}
    for vertex in bm.verts:
        weights=vertex[deform]
        if vertex in movable:
            selected={k:weights[k] for k in antenna if k in weights and weights[k]>.000001}
            if name=='Lumen' or not selected: selected={groups['antenna.01']:1.}
            total=sum(selected.values());weights.clear()
            for key,value in selected.items():weights[key]=value/total
        else:
            for index in antenna:
                if index in weights:del weights[index]
            total=sum(weights.values())
            if total<.000001:weights[groups['body']]=1.
            else:
                for key in list(weights.keys()):weights[key]/=total
    # Recess each closed cap into its own side, leaving the exterior ring
    # untouched. Coincident opposing cap planes flicker as the hinge tilts.
    ring=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z-point.z)<.0002 and
        Vector((v.co.x-point.x,v.co.y-point.y,0)).length<radius+.04 for v in e.verts)]
    cap_boundaries(bm,ring,normal_layer,recess=.004)
    if name=='Lumen':
        # Removing a textured, fused tube can leave tiny isolated gold flecks.
        # They belong to the discarded brace, not the retained sail or body.
        flecks=[face for component in components(bm) if len(component)<60 for face in component
            if all(1.30<v.co.z<1.65 and -.75<v.co.x<-.30 for v in face.verts)]
        if flecks:bmesh.ops.delete(bm,geom=flecks,context='FACES')
        loose=[v for v in bm.verts if not v.link_faces]
        if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.triangulate(bm,faces=list(bm.faces))
    boundaries=sum(e.is_boundary for e in bm.edges)
    require(boundaries==before['boundary_edges'],f'{name}: a mechanical cut left an open boundary {boundaries}/{before["boundary_edges"]}')
    require(not any(len(e.link_faces)>2 for e in bm.edges),f'{name}: a cut created non-manifold topology')
    bm.normal_update();bm.to_mesh(mesh.data);bm.free();mesh.data.update()
    attribute=mesh.data.attributes.get('AccessorySourceNormal')
    normals=[]
    for loop in mesh.data.loops:
        n=Vector(attribute.data[loop.index].vector)
        normals.append(n.normalized() if n.length>.01 else Vector((0,0,1)))
    mesh.data.normals_split_custom_set(normals)
    mesh.data.attributes.remove(attribute)
    after={'vertices':len(mesh.data.vertices),'triangles':len(mesh.data.polygons),'boundary_edges':boundaries}
    return pivot, {'before':before,'after':after,'removed_brace_triangles':removed,'hinge':list(pivot),
        'detached_antenna_faces':len(dynamic),'mechanical_cap_recess':.004}

def prepare_bones(name,rig,mesh,pivot):
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
    bones=rig.data.edit_bones
    yaw=bones.new('antenna.yaw');yaw.head=pivot;yaw.tail=pivot+Vector((0,0,.18));yaw.parent=bones['body'];yaw.use_deform=True
    first=bones['antenna.01'];first.parent=yaw;first.head=pivot
    if name=='Lumen':first.tail=pivot+Vector((0,0,.28))
    branches=[]
    for side in ('L','R'):
        first=bones.get(f'scarf.{side}.01');second=bones.get(f'scarf.{side}.02')
        if first is None:continue
        a,m,z=first.head.copy(),first.tail.copy(),second.tail.copy()
        points=[a,a.lerp(m,.5),m,m.lerp(z,.5),z]
        for i in range(4):
            bone=bones.get(f'scarf.{side}.{i+1:02d}') or bones.new(f'scarf.{side}.{i+1:02d}')
            bone.head=points[i];bone.tail=points[i+1];bone.parent=bones['body'] if i==0 else bones[f'scarf.{side}.{i:02d}'];bone.use_deform=True
        branches.append((side,points))
    bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
    for side,points in branches:
        prefix=f'scarf.{side}.'
        old={g.index for g in mesh.vertex_groups if g.name.startswith(prefix)}
        all_groups=[mesh.vertex_groups.get(f'{prefix}{i+1:02d}') or mesh.vertex_groups.new(name=f'{prefix}{i+1:02d}') for i in range(4)]
        for vertex in mesh.data.vertices:
            total=sum(w.weight for w in vertex.groups if w.group in old)
            if total<.000001:continue
            choices=[]
            for i,(a,b) in enumerate(zip(points,points[1:])):
                t=max(0.,min(1.,(vertex.co-a).dot(b-a)/(b-a).length_squared))
                choices.append(((vertex.co-(a+(b-a)*t)).length_squared,i,t))
            _,i,t=min(choices)
            for group in all_groups:group.remove([vertex.index])
            if i==3:all_groups[i].add([vertex.index],total,'REPLACE')
            else:
                if 1-t>.000001:all_groups[i].add([vertex.index],total*(1-t),'REPLACE')
                if t>.000001:all_groups[i+1].add([vertex.index],total*t,'REPLACE')

def verify(name,rig,mesh):
    names=[b.name for b in rig.data.bones];weights=exporter.skin_weights(mesh,names)
    require(np.max((weights>.000001).sum(1))<=4,'More than four significant skin influences')
    coords=np.array([v.co[:] for v in mesh.data.vertices]);edges=np.array([e.vertices[:] for e in mesh.data.edges])
    lengths=np.linalg.norm(coords[edges[:,1]]-coords[edges[:,0]],axis=1);valid=lengths>1e-4
    original=coords.copy();pose=rig.pose.bones['antenna.yaw'];results={}
    for degrees in (0,90,180,270):
        pose.rotation_mode='QUATERNION';pose.rotation_quaternion=Quaternion(pose.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,1)),math.radians(degrees))
        bpy.context.view_layer.update();evaluated=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());temporary=evaluated.to_mesh()
        actual=np.array([v.co[:] for v in temporary.vertices]);evaluated.to_mesh_clear()
        deformed_lengths=np.linalg.norm(actual[edges[:,1]]-actual[edges[:,0]],axis=1)
        ratio=deformed_lengths[valid]/lengths[valid]
        results[str(degrees)]={'max_edge_stretch':float(ratio.max()),'p99_edge_stretch':float(np.quantile(ratio,.99)),
            'max_absolute_edge_change':float(np.abs(deformed_lengths-lengths).max()),'finite':bool(np.isfinite(actual).all())}
        require(results[str(degrees)]['finite'] and ratio.max()<1.002 and np.abs(deformed_lengths-lengths).max()<.00001,
            f'{name}: rigid yaw stretches a skin edge {results[str(degrees)]}')
    pose.rotation_quaternion=Quaternion();bpy.context.view_layer.update()
    require(mesh.data.shape_keys and mesh.data.shape_keys.key_blocks.get('Blink'),'Blink was lost')
    return {'rigid_yaw_qa':results,'max_significant_weights':int((weights>.000001).sum(1).max()),'blink_preserved':True}

def process(name,render):
    folder=OUT/name;folder.mkdir(parents=True,exist_ok=True)
    backup=folder/'Before';backup.mkdir(exist_ok=True)
    current=json.loads((PROCESSED/name/'manifest.json').read_text())
    if not (backup/f'{name}_Rigged.blend').exists() and current.get('accessory_revision')=='mechanical-yaw-and-four-bone-scarves-v2':
        # A fresh checkout already contains the reviewed accessory rig. Do not
        # treat it as an unmodified baseline and add duplicate hinge bones.
        bpy.ops.wm.open_mainfile(filepath=str(PROCESSED/name/f'{name}_Rigged.blend'))
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('mesh_role')=='Character')
        verify(name,rig,mesh)
        if render:render_review(name,rig,mesh,folder)
        print('RADIO_ACCESSORY_AUTHORING PASS already prepared',name,flush=True)
        return
    for filename in (f'{name}_Rigged.blend','Model.fbx','manifest.json'):
        target=backup/filename
        if not target.exists():shutil.copyfile(PROCESSED/name/filename,target)
    bpy.ops.wm.open_mainfile(filepath=str(backup/f'{name}_Rigged.blend'))
    for image in bpy.data.images:
        if image.source=='FILE':image.filepath=str(PROCESSED/name/image.filepath.replace('\\','/').rsplit('/',1)[-1])
    rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
    mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('mesh_role')=='Character')
    exporter.reset_pose(rig,[mesh])
    if render:render_review(name,rig,mesh,folder,before=True)
    pivot,qa=prepare_mesh(name,mesh);prepare_bones(name,rig,mesh,pivot);qa.update(verify(name,rig,mesh))
    if render:render_review(name,rig,mesh,folder)
    exporter.reset_pose(rig,[mesh]);bpy.context.preferences.filepaths.save_version=0
    blend=folder/'rig-review.blend';bpy.ops.wm.save_as_mainfile(filepath=str(blend),relative_remap=False)
    manifest=json.loads((backup/'manifest.json').read_text())
    manifest.update({'meshes':[{'name':mesh.name,'role':'Character','vertices':len(mesh.data.vertices),'triangles':len(mesh.data.polygons)}],
        'triangles':len(mesh.data.polygons),'bones':[{'name':b.name,'parent':b.parent.name if b.parent else None,'head':list(b.head_local),'tail':list(b.tail_local)} for b in rig.data.bones],
        'accessory_revision':'mechanical-yaw-and-four-bone-scarves-v2','accessory_qa':qa})
    (folder/'rig-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    (folder/'authoring-checks.json').write_text(json.dumps(qa,indent=2)+'\n')
    with tempfile.TemporaryDirectory(prefix='.radio-accessory-export-',dir=PROCESSED) as temporary:
        staging=pathlib.Path(temporary)
        result=exporter.export_review(blend,staging,name,exporter.SOURCE/name)
        for filename in (*result['files'],'manifest.json'):(staging/filename).replace(PROCESSED/name/filename)
    print('RADIO_ACCESSORY_AUTHORING PASS',name,json.dumps(qa),flush=True)

def render_review(name,rig,mesh,folder,before=False):
    # Source geometry/materials, ordinary Blender lights, no generated imagery.
    from prepare_radio_rigs import render_setup,aim
    for obj in list(bpy.context.scene.objects):
        if obj.type in ('LIGHT','CAMERA'):bpy.data.objects.remove(obj,do_unlink=True)
    camera=render_setup();scene=bpy.context.scene;scene.cycles.samples=16
    scene.render.resolution_x=700;scene.render.resolution_y=720;camera.data.ortho_scale=3.3 if name=='Lumen' else 4.3
    camera.location=(4,6,2.2) if name=='Lumen' else (3,-6,2.1);aim(camera,(0,0,1.35))
    for angle in ((0,)if before else (0,90,180)):
        bone=rig.pose.bones.get('antenna.yaw')
        if bone:
            bone.rotation_mode='QUATERNION';bone.rotation_quaternion=Quaternion(bone.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,1)),math.radians(angle))
        bpy.context.view_layer.update();scene.render.filepath=str(folder/('before.png'if before else f'yaw-{angle:03d}.png'));bpy.ops.render.render(write_still=True)

def render_current(name):
    """Refresh proof images without exporting or touching frozen runtime assets."""
    folder=OUT/name;folder.mkdir(parents=True,exist_ok=True)
    baseline=folder/'Before'/f'{name}_Rigged.blend'
    for before,path in ((True,baseline),(False,PROCESSED/name/f'{name}_Rigged.blend')):
        if not path.exists():continue
        bpy.ops.wm.open_mainfile(filepath=str(path))
        for image in bpy.data.images:
            if image.source=='FILE':image.filepath=str(PROCESSED/name/image.filepath.replace('\\','/').rsplit('/',1)[-1])
        rig=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
        mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.get('mesh_role')=='Character')
        exporter.reset_pose(rig,[mesh]);render_review(name,rig,mesh,folder,before)

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--character',choices=CHARACTERS);parser.add_argument('--render',action='store_true');parser.add_argument('--review-only',action='store_true')
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else [])
    for name in ((args.character,) if args.character else CHARACTERS):
        if args.review_only:render_current(name)
        else:process(name,args.render)

if __name__=='__main__':main()
