"""Local, source-fitted torso repair. Run in a disposable background Blender.

Public entry point: repair_body_harness(character, body, rig, source_body=None).
Only the supplied Character object is changed; paw objects and source maps are
never modified. Source originals are read from the immutable pre-cut handoff.
"""
from __future__ import annotations
import argparse, hashlib, json, math, pathlib, subprocess, sys, zipfile
import bpy, bmesh, numpy as np
from mathutils import Vector

ROOT = pathlib.Path(__file__).resolve().parents[3]
REVIEW = ROOT / 'DancingCatsClone/Library/RadioRigging/SmoothTorsoReview'
ORIGINAL = ROOT / 'DancingCatsClone/Library/RadioRigging/ArmClearanceReview'
BASELINE = ROOT / 'DancingCatsClone/Library/RadioRigging/SmoothCourierReview/Before'
PROCESSED = ROOT / 'ArtSource/RadioArchipelago/Processed'
_PIXELS = {}
_SEMANTIC = {}
_BODY_UV = {}
_LEGACY_ARM_FACES = {}
_LEATHER_BLOCKS = {}


def source_parts(character, source):
    """Transfer supplied Tripo semantic surfaces, never importer polygon order.

    Source faces may have been subdivided. Their centers still lie on the same
    triangles, so the full segmented surface supplies labels by a measured
    geometric correspondence. This also identifies the body-weighted scarf
    collar, which cannot safely be recognized from bones or dark texture pixels.
    """
    key=(character,source.data.as_pointer())
    if key in _SEMANTIC:return _SEMANTIC[key]
    if character not in('Nori','Tiko'):
        result=np.full(len(source.data.polygons),-1,int);_SEMANTIC[key]=result;return result
    from mathutils import Matrix
    from mathutils.bvhtree import BVHTree
    review=ROOT/'DancingCatsClone/Library/RadioRigging/Review'/(character+'2')/'source-inspection.blend'
    before=set(bpy.data.objects)
    if review.is_file():
        with bpy.data.libraries.load(str(review),link=False)as(available,loaded):
            loaded.objects=[name for name in available.objects if name.startswith('tripo_part_')]
        parts=loaded.objects
    else:
        reference=ROOT/'Docs/Design/RadioArchipelago/ModelReferences'/character/(character+'2.zip')
        if not reference.is_file():reference=reference.with_name(character+'_2.zip')
        require(reference.is_file(),f'Missing supplied semantic part archive: {reference}')
        folder=REVIEW/'SemanticSource'/(character+'2');folder.mkdir(parents=True,exist_ok=True)
        with zipfile.ZipFile(reference)as archive:
            for member in archive.infolist():
                target=(folder/member.filename).resolve();require(target.is_relative_to(folder.resolve()),'Unsafe semantic source archive path')
                if member.is_dir():target.mkdir(parents=True,exist_ok=True)
                elif not target.is_file():target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(archive.read(member))
        bpy.ops.import_scene.fbx(filepath=str(next(folder.glob('*.fbx'))),use_image_search=False,use_anim=False)
        bpy.context.view_layer.update();parts=[o for o in bpy.data.objects if o not in before and o.type=='MESH']
        for obj in parts:
            world=obj.matrix_world.copy()
            for v in obj.data.vertices:v.co=world@v.co
            obj.parent=None;obj.matrix_world=Matrix.Identity(4)
        points=[v.co.copy()for obj in parts for v in obj.data.vertices]
        low=np.min([tuple(p)for p in points],axis=0);high=np.max([tuple(p)for p in points],axis=0)
        center=Vector(((low[0]+high[0])*.5,(low[1]+high[1])*.5,low[2]));scale=2.6/(high[2]-low[2])
        for obj in parts:
            for v in obj.data.vertices:v.co=(v.co-center)*scale
        feet=[v.co for obj in parts for v in obj.data.vertices if v.co.z<.16]
        offset=Vector(((min(v.x for v in feet)+max(v.x for v in feet))*.5,(min(v.y for v in feet)+max(v.y for v in feet))*.5,0))
        for obj in parts:
            for v in obj.data.vertices:v.co-=offset
    points=[];triangles=[];labels=[]
    for obj in parts:
        part=int(obj.name.split('part_')[1].split('.')[0]);start=len(points)
        points.extend(v.co.copy()for v in obj.data.vertices)
        for face in obj.data.polygons:triangles.append([start+i for i in face.vertices]);labels.append(part)
    tree=BVHTree.FromPolygons(points,triangles,all_triangles=True)
    result=[];distances=[]
    for face in source.data.polygons:
        point,normal,index,distance=tree.find_nearest(face.center)
        require(index is not None,'Semantic correspondence missed a source face')
        result.append(labels[index]);distances.append(distance)
    folder=REVIEW/character;folder.mkdir(parents=True,exist_ok=True)
    outliers=[{'face':i,'part':result[i],'distance':distance,'center':list(source.data.polygons[i].center)}for i,distance in enumerate(distances)if distance>.00015]
    (folder/'semantic-outliers.json').write_text(json.dumps(outliers,indent=2)+'\n')
    # Authored eye/beacon refinement may have intentionally moved the old raw
    # surface. Local cloth/scarf correspondence must retain source precision.
    local=[distance for face,distance in zip(source.data.polygons,distances)if .2<face.center.z<1.15 and abs(face.center.x)>.25]
    require(max(local)<.00015,f'Local semantic correspondence drift {max(local):.7f} exceeds supplied surface quantization')
    for obj in list(bpy.data.objects):
        if obj not in before:
            data=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
            if data and data.users==0 and isinstance(data,bpy.types.Mesh):bpy.data.meshes.remove(data)
    result=np.asarray(result,int);_SEMANTIC[key]=result
    folder=REVIEW/character;folder.mkdir(parents=True,exist_ok=True)
    (folder/'semantic-correspondence.json').write_text(json.dumps({'method':'source-face-center to supplied normalized segmented triangles','faces':len(result),'maximum_surface_distance':max(distances),'part_face_counts':{str(i):int(np.sum(result==i))for i in sorted(set(result))},'source_face_part_numbers':result.tolist()},indent=2)+'\n')
    return result


def require(value, message):
    if not value:
        raise RuntimeError(message)


def original_body(character):
    path = ORIGINAL / character / 'Before' / f'{character}_Rigged.blend'
    revision='d8019f2a8b00f1c6481c1a086056ebc543c6f1e9'
    reference=f'{revision}:ArtSource/RadioArchipelago/Processed/{character}/{character}_Rigged.blend'
    expected=subprocess.run(['git','rev-parse',reference],cwd=ROOT,capture_output=True,text=True,check=True).stdout.strip()
    if not path.is_file():
        payload=subprocess.run(['git','show',reference],cwd=ROOT,capture_output=True,check=True).stdout
        path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(payload)
    payload=path.read_bytes();actual=hashlib.sha1(f'blob {len(payload)}\0'.encode()+payload).hexdigest()
    require(actual==expected,f'Immutable pre-cut source does not match pinned {revision}: {path}')
    with bpy.data.libraries.load(str(path), link=False) as (available, loaded):
        names = [name for name in available.objects if name == character + '_Character']
        require(len(names) == 1, f'Cannot identify immutable Character in {path}')
        loaded.objects = names
    return loaded.objects[0]


def legacy_right_arm_faces(character):
    if character in _LEGACY_ARM_FACES:return _LEGACY_ARM_FACES[character]
    path=BASELINE/character/f'{character}_Rigged.blend';reference=f'87bc426:ArtSource/RadioArchipelago/Processed/{character}/{character}_Rigged.blend'
    expected=subprocess.run(['git','rev-parse',reference],cwd=ROOT,capture_output=True,text=True,check=True).stdout.strip()
    if not path.is_file():path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(subprocess.run(['git','show',reference],cwd=ROOT,capture_output=True,check=True).stdout)
    payload=path.read_bytes();require(hashlib.sha1(f'blob {len(payload)}\0'.encode()+payload).hexdigest()==expected,'Historical arm ownership snapshot changed')
    # Blender cannot append the currently open file; a verified disposable copy
    # supplies the historical ownership even after the live paws were replaced.
    reference_path=REVIEW/'References'/f'{character}-87bc426.blend';reference_path.parent.mkdir(parents=True,exist_ok=True)
    if not reference_path.is_file()or reference_path.read_bytes()!=payload:reference_path.write_bytes(payload)
    before=set(bpy.data.objects);before_materials=set(bpy.data.materials);before_images=set(bpy.data.images)
    with bpy.data.libraries.load(str(reference_path),link=False)as(available,loaded):loaded.objects=[character+'_Arm.R_Cut']
    obj=loaded.objects[0];require(obj is not None,'Historical right paw has no ownership record')
    result={item.value-1 for item in obj.data.attributes['OriginalSurfaceFace'].data if item.value>0}
    for obj in list(bpy.data.objects):
        if obj not in before:
            data=obj.data;bpy.data.objects.remove(obj,do_unlink=True)
            if isinstance(data,bpy.types.Mesh)and data.users==0:bpy.data.meshes.remove(data)
    for material in list(bpy.data.materials):
        if material not in before_materials and material.users==0:bpy.data.materials.remove(material)
    for image in list(bpy.data.images):
        if image not in before_images and image.users==0:bpy.data.images.remove(image)
    _LEGACY_ARM_FACES[character]=result;return result


def paint_image(obj, name='BaseColor'):
    images = [node.image for material in obj.data.materials if material and material.use_nodes
              for node in material.node_tree.nodes
              if node.type == 'TEX_IMAGE' and node.image and name.lower() in node.image.name.lower()]
    require(images, f'Missing {name} map on {obj.name}')
    return images[0]


def resolve_source_maps(character,source):
    for material in source.data.materials:
        for node in material.node_tree.nodes if material and material.use_nodes else[]:
            if node.type!='TEX_IMAGE'or not node.image:continue
            role=next((name for name in('BaseColor','Normal','MetallicSmoothness')if name.lower()in node.image.name.lower()),None)
            if role:node.image.filepath=str(PROCESSED/character/(role+'.png'))


def image_pixels(image):
    key = (str(pathlib.Path(bpy.path.abspath(image.filepath)).resolve()), image.size[0], image.size[1],image.colorspace_settings.name)
    if key not in _PIXELS:
        pixels = np.empty(len(image.pixels), np.float32)
        image.pixels.foreach_get(pixels)
        _PIXELS[key] = pixels.reshape((image.size[1], image.size[0], 4))
    return _PIXELS[key]


def face_colors(obj,image_name='BaseColor',channels=3):
    pixels = image_pixels(paint_image(obj,image_name)); height, width = pixels.shape[:2]
    result = []
    for face in obj.data.polygons:
        uv = sum((obj.data.uv_layers.active.data[i].uv for i in face.loop_indices), Vector((0, 0))) / len(face.loop_indices)
        x = max(0, min(width - 1, int(uv.x * width))); y = max(0, min(height - 1, int(uv.y * height)))
        result.append(pixels[y, x, :channels].copy())
    return np.asarray(result)


def flesh_color(character, color):
    r, g, b = color
    if character == 'Nori':
        return .035 < b < .65 and b > r * 1.15 and b > g * 1.06 and r < .38
    if character == 'Tiko':
        return r > .38 and g > .30 and .91 < r / max(g, .001) < 1.35 and b / max(g, .001) > .70
    return r > .35 and g > .23 and b < g * .83 and r<g*1.65


def protected_surface_faces(character,source):
    parts=source_parts(character,source)
    if character=='Nori':return set(np.flatnonzero(np.isin(parts,(4,5))).tolist())
    if character=='Tiko':return set(np.flatnonzero(np.isin(parts,(0,3,5,8,11,14))).tolist())
    if character=='Lumen':
        # This supplied model has no Tripo part export. Its radio lies behind
        # the torso; protect the complete rear assembly before classifying any
        # gold-colored faces as yellow cloth. Vertex membership also protects
        # the shared front edge of each retained hardware triangle.
        return {f.index for f in source.data.polygons if .30<f.center.z<1.50 and any(source.data.vertices[i].co.y>.30 for i in f.vertices)}
    return set()


def hardware_color(character,color):
    r,g,b=color
    if character=='Nori':return r>b*.94 and g>b*.65
    if character=='Tiko':return g>r*1.1 or (b<g*.70 and r>g*.90)
    return (r>g*1.38 and b<g*.9) or (r<.34 and r>b*1.25)


def source_vertex_arm_weights(source):
    groups = {group.index: group.name for group in source.vertex_groups}
    return np.array([sum(g.weight for g in vertex.groups if groups[g.group].startswith('arm.'))
                     for vertex in source.data.vertices])


def source_vertex_body_weights(source):
    groups={group.index:group.name for group in source.vertex_groups}
    return np.array([sum(g.weight for g in vertex.groups if groups[g.group]=='body') for vertex in source.data.vertices])


def uv_triangle_area(mesh, face):
    uv = [mesh.uv_layers.active.data[i].uv for i in face.loop_indices]
    return sum(abs((uv[i].x - uv[0].x) * (uv[i + 1].y - uv[0].y)
                   - (uv[i].y - uv[0].y) * (uv[i + 1].x - uv[0].x)) * .5
               for i in range(1, len(uv) - 1))


def inspect_source(character, body, source):
    colors = face_colors(source); arm = source_vertex_arm_weights(source);protected=protected_surface_faces(character,source)
    cap_ids = body.data.attributes.get('OriginalSurfaceFace')
    require(cap_ids is not None, 'The baseline body needs OriginalSurfaceFace IDs.')
    report = {'character': character, 'body_faces': len(body.data.polygons), 'source_faces': len(source.data.polygons), 'sides': {}}
    for sign in (-1, 1):
        cap = [face for face, tag in zip(body.data.polygons, cap_ids.data)
               if tag.value == 0 and sign * face.center.x > .1 and .2 < face.center.z < 1.2]
        healthy = [face for face, color in zip(source.data.polygons, colors)
                   if sign * face.center.x > .22 and .23 < face.center.z < 1.22
                   and -.65 < face.center.y < .70 and flesh_color(character, color)
                   and face.index not in protected
                   and max(arm[i] for i in face.vertices) < .001
                   and sign * face.normal.x > .16]
        density = [uv_triangle_area(source.data, face) / face.area for face in healthy if face.area > 1e-8]
        report['sides'][str(sign)] = {'cap_faces': len(cap), 'cap_bounds': bounds([vertex.co for face in cap for vertex in (body.data.vertices[i] for i in face.vertices)]),
                                     'healthy_faces': len(healthy), 'healthy_bounds': bounds([face.center for face in healthy]),
                                     'uv_area_per_world_area_median': float(np.median(density)) if density else None,
                                     'healthy_mean_color': np.mean(colors[[face.index for face in healthy]], axis=0).tolist() if healthy else None}
    return report


def bounds(points):
    if not points:
        return None
    array = np.asarray([tuple(point) for point in points])
    return [array.min(axis=0).tolist(), array.max(axis=0).tolist()]


def _basis(y, z):
    return np.array([1, y, z, y*y, y*z, z*z, y*y*y, y*y*z, y*z*z, z*z*z], float)


def _dy(y, z):
    return np.array([0, 1, 0, 2*y, z, 0, 3*y*y, 2*y*z, z*z, 0], float)


def _dz(y, z):
    return np.array([0, 0, 1, 0, y, 2*z, 0, y*y, 2*y*z, 3*z*z], float)


class SourceSurfaceFit:
    """Regularized Hermite polynomial fitted to authentic surrounding skin.

    Coordinates and first derivatives both come from the supplied mesh. No
    assumed sphere/ellipsoid dimensions or artist-entered torso radii are used.
    """
    def __init__(self, character, source, sign, box, colors):
        self.sign = sign
        self.origin = np.array([(box[0][1]+box[1][1])*.5, (box[0][2]+box[1][2])*.5])
        self.scale = np.array([.55, .55])
        arm = source_vertex_arm_weights(source);body_weight=source_vertex_body_weights(source);protected=protected_surface_faces(character,source); rows=[]; values=[]; weights=[]; centers=[]
        for face, color in zip(source.data.polygons, colors):
            p = face.center
            if face.index in protected:continue
            if not (sign*p.x > .22 and .22 < p.z < 1.23 and -.65 < p.y < .70 and flesh_color(character, color)):
                continue
            if max(arm[i] for i in face.vertices) >= .001 or min(body_weight[i] for i in face.vertices)<.98:
                continue
            # The missing/fused shoulder footprint is not valid training skin.
            if box[0][1]-.025 < p.y < box[1][1]+.025 and box[0][2]-.035 < p.z < box[1][2]+.035:
                continue
            normal = sum((source.data.corner_normals[i].vector for i in face.loop_indices), Vector()).normalized()
            if sign*normal.x < .25:
                continue
            y,z=(np.array([p.y,p.z])-self.origin)/self.scale
            distance=y*y+z*z
            if distance > 3.8:
                continue
            weight=math.exp(-distance*1.2)*min(2., max(.2, math.sqrt(face.area/.0003)))
            rows.append(_basis(y,z));values.append(p.x);weights.append(weight);centers.append((p.x,p.y,p.z))
            # Normal gives the measured first derivatives of x(y,z).
            rows.extend([_dy(y,z),_dz(y,z)])
            values.extend([-normal.y/normal.x*self.scale[0],-normal.z/normal.x*self.scale[1]])
            weights.extend([weight*.17,weight*.17])
        require(len(centers)>30, f'Insufficient authentic surrounding skin for {character} side {sign}: {len(centers)}')
        matrix=np.array(rows);target=np.array(values);base=np.array(weights);robust=np.ones(len(target))
        regularization=np.diag([0,0,0,.012,.012,.012,.055,.055,.055,.055])
        for _ in range(5):
            weight=np.sqrt(base*robust)
            self.coefficients=np.linalg.lstsq(np.vstack([matrix*weight[:,None],regularization]),np.r_[target*weight,np.zeros(10)],rcond=None)[0]
            residual=np.abs(matrix@self.coefficients-target)
            threshold=max(.025,float(np.median(residual))*2.5)
            robust=np.minimum(1,threshold/np.maximum(residual,1e-10))
        self.report={'training_faces':len(centers),'source_position_fit_rms':float(np.sqrt(np.mean((matrix[::3]@self.coefficients-target[::3])**2))),
                     'origin_yz':self.origin.tolist(),'coefficients':self.coefficients.tolist()}

    def point(self, y, z):
        a,b=(np.array([y,z])-self.origin)/self.scale
        return Vector((float(_basis(a,b)@self.coefficients),y,z))

    def normal(self,y,z):
        a,b=(np.array([y,z])-self.origin)/self.scale
        dy=float(_dy(a,b)@self.coefficients)/self.scale[0]
        dz=float(_dz(a,b)@self.coefficients)/self.scale[1]
        return Vector((self.sign,-self.sign*dy,-self.sign*dz)).normalized()


def _rectangle_sums(array, height, width):
    summed=np.pad(array,((1,0),(1,0))).cumsum(0).cumsum(1)
    return summed[height:,width:]-summed[:-height,width:]-summed[height:,:-width]+summed[:-height,:-width]


def body_uv_mask(character,source,height,width):
    """Semantic body fabric only; a matching head color is not a material match."""
    key=(character,source.data.as_pointer(),height,width)
    if key in _BODY_UV:return _BODY_UV[key]
    labels=source_parts(character,source);mask=np.zeros((height,width),bool)
    allowed={12}if character=='Tiko'else{1}
    if character=='Lumen':
        colors=face_colors(source);weights=source_vertex_body_weights(source);arm=source_vertex_arm_weights(source);protected=protected_surface_faces(character,source)
        valid={f.index for f in source.data.polygons if f.index not in protected and .30<f.center.z<1.12 and flesh_color(character,colors[f.index])and min(weights[i]for i in f.vertices)>.95 and max(arm[i]for i in f.vertices)<.02}
    else:valid={f.index for f in source.data.polygons if labels[f.index]in allowed}
    for face in source.data.polygons:
        if face.index not in valid:continue
        uv=np.asarray([tuple(source.data.uv_layers.active.data[i].uv)for i in face.loop_indices])*[width,height]
        for i in range(1,len(uv)-1):
            a,b,c=uv[[0,i,i+1]];low=np.floor(np.min([a,b,c],axis=0)).astype(int);high=np.ceil(np.max([a,b,c],axis=0)).astype(int)
            xmin=max(0,low[0]);xmax=min(width-1,high[0]);ymin=max(0,low[1]);ymax=min(height-1,high[1])
            if xmax<xmin or ymax<ymin:continue
            yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1];den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-12:continue
            p=((b[1]-c[1])*(xx+.125-c[0])+(c[0]-b[0])*(yy+.125-c[1]))/den
            q=((c[1]-a[1])*(xx+.125-c[0])+(a[0]-c[0])*(yy+.125-c[1]))/den
            mask[ymin:ymax+1,xmin:xmax+1]|=(p>=0)&(q>=0)&(p+q<=1)
    interior=mask.copy();interior[1:-1,1:-1]&=mask[:-2,1:-1]&mask[2:,1:-1]&mask[1:-1,:-2]&mask[1:-1,2:]
    _BODY_UV[key]=interior;return interior


def atlas_patch(character, source, width_uv, height_uv, target_color, triangles_uv=None,body_only=False,material_region=None):
    """Find a full, existing cloth rectangle, at the measured source texel density."""
    image=paint_image(source);pixels=image_pixels(image);height,width=pixels.shape[:2]
    step=4;rgb=pixels[::step,::step,:3]
    r,g,b=rgb[:,:,0],rgb[:,:,1],rgb[:,:,2]
    if character=='Nori':
        allowed=(b>.035)&(b<.65)&(b>r*1.15)&(b>g*1.06)&(r<.38)
    elif character=='Tiko':
        allowed=(r>.38)&(g>.30)&(r>g*.91)&(r<g*1.35)&(b>g*.70)
    else:
        allowed=(r>.35)&(g>.23)&(b<g*.83)&(r<g*1.65)
    if material_region in('radio_leather','radio_leather_contact'):
        # A contact panel belongs to the radio case, not the yellow character.
        # The old non-body branch still selected flesh colours, so exposed
        # closure triangles looked like torn body fabric in Unity.
        packed=image_pixels(paint_image(source,'MetallicSmoothness'))[::step,::step,:]
        normal_rgb=image_pixels(paint_image(source,'Normal'))[::step,::step,:3]
        roughness=1-packed[:,:,3]
        allowed=(r>.42)&(r<.62)&(g>.19)&(g<.30)&(b>.015)&(b<.085)&(r>g*1.65)&(r<g*2.65)&(packed[:,:,0]<.04)&(normal_rgb[:,:,2]>.96)&(roughness>.56)&(roughness<.82)
        if material_region=='radio_leather_contact':allowed=(r>.12)&(r<.72)&(g>.045)&(r>g*1.45)&(r<g*3.4)&(b<g*.87)&(packed[:,:,0]<.06)&(normal_rgb[:,:,2]>.90)&(roughness>.40)
        else:
            # Validate entire native 4x4 blocks, not one sampled texel. A
            # single pale atlas island on an otherwise brown chart caused
            # visible white scratches down the case lid's rounded edges.
            cache_key=(str(pathlib.Path(bpy.path.abspath(image.filepath)).resolve()),width,height,image.colorspace_settings.name)
            if cache_key not in _LEATHER_BLOCKS:
                nr,ng,nb=pixels[:,:,0],pixels[:,:,1],pixels[:,:,2]
                native_packed=image_pixels(paint_image(source,'MetallicSmoothness'));native_normal=image_pixels(paint_image(source,'Normal'));native_rough=1-native_packed[:,:,3]
                # Brown leather may contain its native dark grain. Reject
                # pale paint, yellow cloth and metallic hardware, while the
                # colour cost still favours the same warm brown throughout.
                healthy=(nr>.18)&(nr<.75)&(ng>.06)&(ng<.43)&(nb<.19)&(nr>ng*1.3)&(nr<ng*4.5)&(native_packed[:,:,0]<.12)&(native_rough>.30)
                require(height%step==0 and width%step==0,'Case map needs a four-pixel grid')
                allowed=healthy.reshape(height//step,step,width//step,step).all(axis=(1,3))
                _LEATHER_BLOCKS[cache_key]=allowed
            allowed=_LEATHER_BLOCKS[cache_key]
    if body_only:
        allowed&=body_uv_mask(character,source,*allowed.shape)
        if character=='Tiko':allowed&=(b>g*.83)&(r<g*1.25)
        normal_rgb=image_pixels(paint_image(source,'Normal'))[::step,::step,:3]
        packed=image_pixels(paint_image(source,'MetallicSmoothness'))[::step,::step,:]
        roughness=1-packed[:,:,3]
        rough_target=.52549 if character=='Lumen'else .74
        rough_low,rough_high=(.45,.65)if character=='Lumen'else(.66,.82)
        allowed&=(normal_rgb[:,:,2]>.95)&(packed[:,:,0]<.03)&(roughness>rough_low)&(roughness<rough_high)
    w=max(4,int(math.ceil(width_uv*width/step))+4);h=max(4,int(math.ceil(height_uv*height/step))+4)
    require(w<rgb.shape[1] and h<rgb.shape[0], 'Requested coherent torso UV patch exceeds atlas.')
    difference=np.sum((rgb-np.array(target_color))**2,axis=2)
    if body_only:difference+=np.sum((normal_rgb[:,:,:2]-.5)**2,axis=2)*.25+(normal_rgb[:,:,2]-1)**2*.25+(roughness-rough_target)**2*.5+packed[:,:,0]**2*2
    footprint=np.ones((h,w),bool)
    if triangles_uv is not None:
        footprint[:]=0
        for triangle in triangles_uv:
            points=np.asarray([[u*width/step+2,v*height/step+2]for u,v in triangle])
            xmin=max(0,int(np.floor(points[:,0].min())));xmax=min(w-1,int(np.ceil(points[:,0].max())))
            ymin=max(0,int(np.floor(points[:,1].min())));ymax=min(h-1,int(np.ceil(points[:,1].max())))
            if xmax<xmin or ymax<ymin:continue
            yy,xx=np.mgrid[ymin:ymax+1,xmin:xmax+1];a,b,c=points
            den=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
            if abs(den)<1e-10:continue
            p=((b[1]-c[1])*(xx+.5-c[0])+(c[0]-b[0])*(yy+.5-c[1]))/den
            q=((c[1]-a[1])*(xx+.5-c[0])+(a[0]-c[0])*(yy+.5-c[1]))/den
            footprint[ymin:ymax+1,xmin:xmax+1]|=((p>=-.02)&(q>=-.02)&(p+q<=1.02))
        # Native-density micro triangles can be smaller than a sampled texel.
        # Validate a padded coherent rectangle around them without enlarging
        # their actual UV coordinates or collapsing them onto a single texel.
        if footprint.sum()<=20:footprint[:]=True
    if material_region=='radio_leather':
        padded=np.pad(footprint,1);footprint=np.logical_or.reduce([padded[y:y+h,x:x+w]for y in range(3)for x in range(3)])
    def correlate(array):
        if triangles_uv is None:return _rectangle_sums(array,h,w)
        shape=[1<<int(math.ceil(math.log2(a+b-1)))for a,b in zip(array.shape,footprint.shape)]
        convolution=np.fft.irfftn(np.fft.rfftn(array,s=shape)*np.fft.rfftn(footprint[::-1,::-1],s=shape),s=shape)
        return convolution[h-1:array.shape[0],w-1:array.shape[1]]
    invalid=np.clip(correlate((~allowed).astype(float))/footprint.sum(),0,1)
    cost=correlate(difference)/footprint.sum()+invalid*8
    y,x=np.unravel_index(np.argmin(cost),cost.shape)
    uv=Vector(((x+2)*step/width,(y+2)*step/height))
    patch=pixels[y*step:(y+h)*step,x*step:(x+w)*step,:3]
    selected=patch[::step,::step,:3][footprint.astype(bool)]
    report={'origin_uv':list(uv),'extent_uv':[width_uv,height_uv],'pixels':[w*step,h*step],
            'cloth_coverage':float(1-invalid[y,x]),'base_color_mean':selected.mean(axis=0).tolist(),
            'base_color_std':selected.std(axis=0).tolist(),'footprint_fraction':float(footprint.mean()),'semantic_body_fabric_only':body_only,'material_region':material_region}
    if body_only:
        report['normal_rgb_mean']=normal_rgb[y:y+h,x:x+w][footprint].mean(axis=0).tolist()
        report['metallic_mean']=float(packed[y:y+h,x:x+w,0][footprint].mean())
        report['roughness_mean']=float(roughness[y:y+h,x:x+w][footprint].mean())
    require(report['cloth_coverage']>(.999999 if material_region=='radio_leather'else .94), f'Cannot find a coherent source-cloth atlas area: {report}')
    return uv,report


def heal_tiko_paint_leaks(source,faces,uv):
    """Rehome only tiny cloth triangles that touch a foreign atlas island.

    Large chart placement uses a sampled footprint. This final native-pixel
    guard prevents a small red scarf island from occupying its allowed border
    fraction. UV translation preserves every repaired triangle's texel density.
    """
    color=image_pixels(paint_image(source));normal=image_pixels(paint_image(source,'Normal'));packed=image_pixels(paint_image(source,'MetallicSmoothness'))
    height,width=color.shape[:2];bad=[]
    for face in faces:
        points=np.asarray([tuple(loop[uv].uv)for loop in face.loops]);samples=list(points)+[points.mean(axis=0)]
        samples.extend((points[i]+points[(i+1)%len(points)])*.5 for i in range(len(points)))
        unsafe=False
        for point in samples:
            x=max(0,min(width-1,int(point[0]*width)));y=max(0,min(height-1,int(point[1]*height)));r,g,b=color[y,x,:3]
            if r>g*1.30 or b<g*.81 or normal[y,x,2]<.92 or packed[y,x,0]>.08:unsafe=True;break
        if unsafe:bad.append((face,points))
    if not bad:return []
    r,g,b=color[:,:,0],color[:,:,1],color[:,:,2];roughness=1-packed[:,:,3]
    low_body=body_uv_mask('Tiko',source,math.ceil(height/4),math.ceil(width/4));body=np.repeat(np.repeat(low_body,4,axis=0),4,axis=1)[:height,:width]
    healthy=body&(r>.38)&(g>.30)&(b>g*.83)&(r<g*1.25)&(normal[:,:,2]>.95)&(packed[:,:,0]<.03)&(roughness>.66)&(roughness<.82)
    require(height%4==0 and width%4==0,'The supplied body fabric maps need a four-pixel sampling grid')
    blocks=healthy.reshape(height//4,4,width//4,4).all(axis=(1,3));rgb=color[::4,::4,:3];target=np.asarray((.75,.67,.58))
    cost=np.sum((rgb-target)**2,axis=2)+(roughness[::4,::4]-.74)**2*.5+packed[::4,::4,0]**2*2
    windows={};report=[]
    for face,points in bad:
        low=points.min(axis=0);extent=points.max(axis=0)-low;w=max(3,int(math.ceil(extent[0]*width/4))+2);h=max(3,int(math.ceil(extent[1]*height/4))+2)
        if(h,w)not in windows:
            invalid=_rectangle_sums((~blocks).astype(np.int32),h,w);scores=_rectangle_sums(cost,h,w)/(h*w);scores[invalid!=0]=np.inf
            y,x=np.unravel_index(np.argmin(scores),scores.shape);require(np.isfinite(scores[y,x]),'No fully healthy native body swatch for a cloth-edge triangle')
            windows[(h,w)]=np.asarray(((x+1)*4/width,(y+1)*4/height))
        origin=windows[(h,w)]
        for loop,point in zip(face.loops,points):loop[uv].uv=Vector(tuple(point-low+origin))
        report.append({'face':face.index,'healthy_window_pixels':[w*4,h*4],'original_uv_area_preserved':True,'native_pixel_material_coverage':1.})
    return report


def heal_lumen_paint_leaks(source,faces,uv):
    """Preserve UV area while removing foreign texels at native chart edges.

    The chart search is deliberately coarse. Validate every covered native
    texel here, including triangle corners; an attractive centroid cannot
    certify a fabric triangle that crosses an atlas island.
    """
    color=image_pixels(paint_image(source));normal=image_pixels(paint_image(source,'Normal'));packed=image_pixels(paint_image(source,'MetallicSmoothness'))
    height,width=color.shape[:2];roughness=1-packed[:,:,3];r,g,b=color[:,:,0],color[:,:,1],color[:,:,2]
    healthy=(r>.35)&(g>.23)&(b<g*.83)&(r<g*1.65)&(normal[:,:,2]>.95)&(packed[:,:,0]<.03)&(roughness>.45)&(roughness<.65)
    bad=[]
    for face in faces:
        points=np.asarray([tuple(loop[uv].uv)for loop in face.loops]);samples=[points,points.mean(axis=0)[None,:]]
        for i in range(1,len(points)-1):
            a,bb,c=points[[0,i,i+1]]*[width,height];low=np.floor(np.min([a,bb,c],axis=0)).astype(int);high=np.ceil(np.max([a,bb,c],axis=0)).astype(int)
            yy,xx=np.mgrid[max(0,low[1]):min(height-1,high[1])+1,max(0,low[0]):min(width-1,high[0])+1];xx=xx.ravel()+.5;yy=yy.ravel()+.5
            den=(bb[1]-c[1])*(a[0]-c[0])+(c[0]-bb[0])*(a[1]-c[1])
            if abs(den)<1e-12:continue
            p=((bb[1]-c[1])*(xx-c[0])+(c[0]-bb[0])*(yy-c[1]))/den;q=((c[1]-a[1])*(xx-c[0])+(a[0]-c[0])*(yy-c[1]))/den;inside=(p>=0)&(q>=0)&(p+q<=1)
            samples.append(np.column_stack((xx[inside]/width,yy[inside]/height)))
        samples=np.concatenate(samples);x=np.clip((samples[:,0]*width).astype(int),0,width-1);y=np.clip((samples[:,1]*height).astype(int),0,height-1)
        if not healthy[y,x].all():bad.append((face,points))
    if not bad:return []
    low_body=body_uv_mask('Lumen',source,math.ceil(height/4),math.ceil(width/4));body=np.repeat(np.repeat(low_body,4,axis=0),4,axis=1)[:height,:width]
    healthy&=body
    require(height%4==0 and width%4==0,'The supplied body fabric maps need a four-pixel sampling grid')
    blocks=healthy.reshape(height//4,4,width//4,4).all(axis=(1,3));target=np.asarray((.7568628,.6039216,.2627451))
    cost=np.sum((color[::4,::4,:3]-target)**2,axis=2)+(roughness[::4,::4]-.52549)**2*.5+packed[::4,::4,0]**2*2
    windows={};report=[]
    for face,points in bad:
        low=points.min(axis=0);extent=points.max(axis=0)-low;w=max(3,int(math.ceil(extent[0]*width/4))+2);h=max(3,int(math.ceil(extent[1]*height/4))+2)
        if(h,w)not in windows:
            invalid=_rectangle_sums((~blocks).astype(np.int32),h,w);scores=_rectangle_sums(cost,h,w)/(h*w);scores[invalid!=0]=np.inf
            y,x=np.unravel_index(np.argmin(scores),scores.shape);require(np.isfinite(scores[y,x]),'No fully healthy native yellow-body swatch for a cloth-edge triangle')
            windows[(h,w)]=np.asarray(((x+1)*4/width,(y+1)*4/height))
        for loop,point in zip(face.loops,points):loop[uv].uv=Vector(tuple(point-low+windows[(h,w)]))
        report.append({'face':face.index,'healthy_window_pixels':[w*4,h*4],'original_uv_area_preserved':True,'native_pixel_material_coverage':1.})
    return report


def _restore_original(body,source):
    mesh=body.data;face_ids=mesh.attributes['OriginalSurfaceFace'];vertex_ids=mesh.attributes['OriginalSurfaceVertex']
    restored=set(body.get('restored_source_vertices',[]));original_normals=[]
    for vertex,item in zip(mesh.vertices,vertex_ids.data):
        original=item.value-1
        if original in restored:
            delta=source.data.vertices[original].co-vertex.co
            vertex.co+=delta
            if mesh.shape_keys:
                for key in mesh.shape_keys.key_blocks:key.data[vertex.index].co+=delta
    for face,item in zip(mesh.polygons,face_ids.data):
        if item.value:
            old=source.data.polygons[item.value-1];lookup={source.data.loops[i].vertex_index:i for i in old.loop_indices}
            for loop in face.loop_indices:
                original=vertex_ids.data[mesh.loops[loop].vertex_index].value-1
                require(original in lookup,'Original body identity is inconsistent before local repair.')
                old_loop=lookup[original]
                mesh.uv_layers.active.data[loop].uv=source.data.uv_layers.active.data[old_loop].uv
                original_normals.append(source.data.corner_normals[old_loop].vector.copy())
        else:
            original_normals.extend(mesh.corner_normals[i].vector.copy() for i in face.loop_indices)
    mesh.update();mesh.normals_split_custom_set(original_normals)


def _crossings(loop):
    return _point_crossings([np.array((v.co.y,v.co.z)) for v in loop])


def _point_crossings(points):
    result=[]
    def cross(a,b):return a[0]*b[1]-a[1]*b[0]
    for i,a in enumerate(points):
        b=points[(i+1)%len(points)]
        for j in range(i+2,len(points)):
            if(j+1)%len(points)==i:continue
            c=points[j];d=points[(j+1)%len(points)];den=cross(b-a,d-c)
            if abs(den)<1e-12:continue
            t=cross(c-a,d-c)/den;u=cross(c-a,b-a)/den
            if 1e-7<t<1-1e-7 and 1e-7<u<1-1e-7:result.append((i,j))
    return result


def _fast_crossings(points):
    points=np.asarray(points,float);n=len(points);i,j=np.triu_indices(n,2);keep=(j+1)%n!=i;i=i[keep];j=j[keep]
    following=np.roll(points,-1,axis=0);a=points[i];b=following[i];c=points[j];d=following[j]
    def cross(left,right):return left[:,0]*right[:,1]-left[:,1]*right[:,0]
    denominator=cross(b-a,d-c);valid=abs(denominator)>1e-12
    t=np.divide(cross(c-a,d-c),denominator,out=np.zeros_like(denominator),where=valid)
    u=np.divide(cross(c-a,b-a),denominator,out=np.zeros_like(denominator),where=valid)
    selected=valid&(t>1e-7)&(t<1-1e-7)&(u>1e-7)&(u<1-1e-7)
    return list(zip(i[selected].tolist(),j[selected].tolist()))


def _route_private_boundary(loop,bm,vid,fit):
    """Test small hidden-cloth detours while retaining every source anchor."""
    points=np.asarray([(v.co.y,v.co.z)for v in loop]);private=np.asarray([v[vid]==0 for v in loop]);n=len(loop)
    initial=len(_fast_crossings(points));steps=[]
    starts=[i for i in range(n)if private[i]and not private[(i-1)%n]]
    runs=[]
    for start in starts:
        indices=[];i=start
        while private[i]:indices.append(i);i=(i+1)%n
        run=[(start-1)%n]+indices+[i];arc=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points[run],axis=0),axis=1))]
        t=arc[1:-1]/arc[-1];runs.append((indices,t))
    limit=min(.06,float(np.ptp(points,axis=0).max())*.10)
    for _ in range(6):
        crossings=_fast_crossings(points)
        if not crossings:break
        involved={k for a,b in crossings for k in(a,(a+1)%n,b,(b+1)%n)};best=None
        for indices,t in runs:
            if not involved.intersection(indices):continue
            for profile,bump in(('C2',64*t**3*(1-t)**3),('parabolic',4*t*(1-t))):
                for axis in(1,0):
                    for amplitude in np.r_[-np.linspace(limit/10,limit,10),np.linspace(limit/10,limit,10)]:
                        candidate=points.copy();candidate[indices,axis]+=amplitude*bump;count=len(_fast_crossings(candidate))
                        if count>=len(crossings):continue
                        score=(count,float(amplitude*amplitude*np.dot(bump,bump)))
                        if best is None or score<best[0]:best=(score,candidate,{'profile':profile,'axis':'z'if axis==1 else'y','amplitude':float(amplitude),'vertices':len(indices),'remaining_crossings':count})
        if best is None:break
        _,points,step=best;steps.append(step)
    remaining=len(_fast_crossings(points))
    if remaining==0:
        for i,vertex in enumerate(loop):
            if private[i]:
                vertex.co=fit.point(float(points[i,0]),float(points[i,1]))
                for layer in bm.verts.layers.shape.values():vertex[layer]=vertex.co
    return {'initial_crossings':initial,'remaining_crossings':remaining,'steps':steps,'source_vertices_moved':0}


def _independent_cloth_boundary(loop,bm,protected_faces,fid,vid,modified,patch_layer,deform,body,fit):
    """Leave the supplied scarf fixed and tuck independent torso skin under it.

    Only the hidden connection strip follows the real folded scarf edge. The
    visible side skin has its own simple boundary, fitted to the surrounding
    torso instead of inheriting the old fusion cuts in the collar.
    """
    protected=[any(f[fid]-1 in protected_faces for f in bm.edges.get((v,loop[(i+1)%len(loop)])).link_faces)for i,v in enumerate(loop)]
    if not any(protected):return loop,[],[]
    if all(protected):
        from mathutils.geometry import convex_hull_2d
        points=[Vector((v.co.y,v.co.z))for v in loop];hull=[points[i]for i in convex_hull_2d(points)]
        require(len(hull)>=3,'Retained garment loop has no finite source footprint')
        old_area=sum(a.x*b.y-b.x*a.y for a,b in zip(points,points[1:]+points[:1]));hull_area=sum(a.x*b.y-b.x*a.y for a,b in zip(hull,hull[1:]+hull[:1]))
        if old_area*hull_area<0:hull.reverse()
        start=min(range(len(hull)),key=lambda i:(hull[i]-points[0]).length_squared);hull=hull[start:]+hull[:start]
        cumulative=[0.]
        for a,b in zip(hull,hull[1:]+hull[:1]):cumulative.append(cumulative[-1]+(b-a).length)
        old_lengths=[0.]
        for a,b in zip(loop,loop[1:]+loop[:1]):old_lengths.append(old_lengths[-1]+(b.co-a.co).length)
        result=[];strips=[]
        for index in range(len(loop)):
            distance=old_lengths[index]/old_lengths[-1]*cumulative[-1];edge=min(len(hull)-1,int(np.searchsorted(cumulative,distance,side='right')-1))
            fraction=(distance-cumulative[edge])/(cumulative[edge+1]-cumulative[edge]);point=hull[edge].lerp(hull[(edge+1)%len(hull)],fraction)
            vertex=bm.verts.new(fit.point(point.x,point.y));vertex[vid]=0;vertex[modified]=1;vertex[deform][body.vertex_groups['body'].index]=1.
            for layer in bm.verts.layers.shape.values():vertex[layer]=vertex.co
            result.append(vertex)
        for i,a in enumerate(loop):
            b=loop[(i+1)%len(loop)];vertices=[a,b,result[(i+1)%len(loop)],result[i]];neighbor=bm.edges.get((a,b)).link_faces[0]
            if any(l.vert is a and l.link_loop_next.vert is b for l in neighbor.loops):vertices.reverse()
            face=bm.faces.new(vertices);face[fid]=0;face[patch_layer]=fit.sign;face.smooth=True;strips.append(face)
        return result,strips,[{'method':'independent source-footprint hull behind retained exterior','source_edge_count':len(loop),'source_bounds':bounds([v.co for v in loop])}]
    start=next(i for i,p in enumerate(protected)if not p)
    ordered=loop[start:]+loop[:start];flags=protected[start:]+protected[:start]
    result=list(ordered);strips=[];runs=[];i=0
    while i<len(ordered):
        if not flags[i]:i+=1;continue
        first=i
        while i<len(ordered)and flags[i]:i+=1
        end=i;old=[ordered[j%len(ordered)]for j in range(first,end+1)]
        a,b=old[0].co.copy(),old[-1].co.copy();distances=[0.]
        for left,right in zip(old,old[1:]):distances.append(distances[-1]+(right.co-left.co).length)
        new=[old[0]]
        for j,vertex in enumerate(old[1:-1],1):
            fraction=distances[j]/distances[-1];point=a.lerp(b,fraction);point.x=fit.point(point.y,point.z).x
            duplicate=bm.verts.new(point);duplicate[vid]=0;duplicate[modified]=1;duplicate[deform][body.vertex_groups['body'].index]=1.
            for layer in bm.verts.layers.shape.values():duplicate[layer]=point
            result[(first+j)%len(ordered)]=duplicate;new.append(duplicate)
        new.append(old[-1])
        for j in range(len(old)-1):
            vertices=[old[j],old[j+1],new[j+1],new[j]]
            vertices=[v for k,v in enumerate(vertices)if k==0 or v is not vertices[k-1]]
            if len(vertices)>1 and vertices[0]is vertices[-1]:vertices.pop()
            if len(set(vertices))<3:continue
            edge=bm.edges.get((old[j],old[j+1]));neighbor=edge.link_faces[0]
            if any(l.vert is old[j]and l.link_loop_next.vert is old[j+1]for l in neighbor.loops):vertices.reverse()
            face=bm.faces.new(vertices);face[fid]=0;face[patch_layer]=fit.sign;face.smooth=True;strips.append(face)
        runs.append({'source_edge_count':len(old)-1,'independent_vertices':len(old)-2,'source_bounds':bounds([v.co for v in old])})
    return result,strips,runs


def _inside(point,polygon):
    x,y=point;inside=False
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        if(a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:inside=not inside
    return inside


class SmoothBoundaryResidual:
    """Compact C2 Hermite field: measured boundary values AND first derivatives.

    Unlike nearest-anchor blending this is one differentiable surface. Its
    compact support leaves the accepted interior fit exactly unchanged farther
    than the local collar radius. Original source vertices remain fixed.
    """
    def __init__(self,fit,points,residual,normals):
        self.fit=fit;self.points=np.asarray(points,float);self.radius=.18
        n=len(points);kernel,gradient,hessian=self.kernel(self.points)
        r=self.radius;matrix=np.block([[kernel,-r*gradient[:,:,0],-r*gradient[:,:,1]],
             [r*gradient[:,:,0],-r*r*hessian[:,:,0,0],-r*r*hessian[:,:,0,1]],
             [r*gradient[:,:,1],-r*r*hessian[:,:,1,0],-r*r*hessian[:,:,1,1]]])
        derivatives=[]
        for(y,z),normal in zip(points,normals):
            base=fit.normal(y,z)
            if abs(normal.x)<.12:normal=base
            derivatives.append((-normal.y/normal.x+base.y/base.x,-normal.z/normal.x+base.z/base.x))
        derivatives=np.asarray(derivatives)
        target=np.r_[residual,r*derivatives[:,0],r*derivatives[:,1]]
        self.coefficients=np.linalg.lstsq(matrix+np.eye(n*3)*.000002,target,rcond=1e-9)[0]
        error=matrix@self.coefficients-target
        self.report={'method':'compact-C2-Hermite-Wendland','radius':r,'constraints':n,'maximum_value_error':float(np.max(abs(error[:n]))),
                     'maximum_derivative_error':float(np.max(abs(error[n:]))/r)}

    def kernel(self,points):
        delta=np.asarray(points)[:,None,:]-self.points[None,:,:];distance=np.linalg.norm(delta,axis=2)/self.radius
        left=np.maximum(0,1-distance);kernel=left**4*(4*distance+1)
        gradient=-20*left[:,:,None]**3*delta/self.radius**2
        identity=np.eye(2)[None,None,:,:];outer=delta[:,:,:,None]*delta[:,:,None,:]
        hessian=-20*left[:,:,None,None]**3*identity/self.radius**2+60*left[:,:,None,None]**2*outer/(self.radius**4*np.maximum(distance[:,:,None,None],1e-12))
        return kernel,gradient,hessian

    def evaluate(self,y,z):
        kernel,gradient,hessian=self.kernel(np.asarray([[y,z]]));n=len(self.points);r=self.radius
        a,b,c=np.split(self.coefficients,3)
        value=float(kernel[0]@a-r*gradient[0,:,0]@b-r*gradient[0,:,1]@c)
        dy=float(gradient[0,:,0]@a-r*hessian[0,:,0,0]@b-r*hessian[0,:,0,1]@c)
        dz=float(gradient[0,:,1]@a-r*hessian[0,:,1,0]@b-r*hessian[0,:,1,1]@c)
        return value,dy,dz

    def point(self,y,z):
        point=self.fit.point(y,z);point.x+=self.evaluate(y,z)[0];return point

    def normal(self,y,z):
        _,ry,rz=self.evaluate(y,z);base=self.fit.normal(y,z);dy=-base.y/base.x+ry;dz=-base.z/base.x+rz;sign=self.fit.sign
        return Vector((sign,-sign*dy,-sign*dz)).normalized()


def relax_nori_body_transition(mesh,source,fit,parts,protected_faces):
    """Screened biharmonic lower cloth with a soft immutable-source prior.

    The real feet, upper body and all supplied garments stay fixed. Geometric
    rings supply continuity; AI-authored shading normals are not derivative
    constraints on the repaired surface.
    """
    points=np.asarray([tuple(v.co)for v in mesh.vertices]);original_points=points.copy()
    source_points=np.asarray([tuple(v.co)for v in source.data.vertices]);ids=mesh.attributes['OriginalSurfaceVertex'];face_ids=mesh.attributes['OriginalSurfaceFace'];patch=mesh.attributes['SmoothTorsoPatchSide']
    normals_before=np.asarray([tuple(n.vector)for n in mesh.corner_normals]);groups={g.index:g.name for g in source.vertex_groups}
    foot=np.asarray([sum(g.weight for g in v.groups if groups[g.group].startswith('foot.'))for v in source.data.vertices])
    source_roles=[set()for _ in source.data.vertices]
    for polygon,part in zip(source.data.polygons,parts):
        for i in polygon.vertices:source_roles[i].add(int(part))
    protected_vertices={i for f in source.data.polygons if f.index in protected_faces for i in f.vertices}
    def move_x(index,new_x):
        delta=float(new_x-mesh.vertices[index].co.x);mesh.vertices[index].co.x=float(new_x)
        if mesh.shape_keys:
            for key in mesh.shape_keys.key_blocks:key.data[index].co.x+=delta
    faded=0
    for vertex,item in zip(mesh.vertices,ids.data):
        old=item.value-1;p=vertex.co
        if old<0 or old in protected_vertices or source_points[old,2]<=.25 or foot[old]>=.35:continue
        if not(p.x<-.2 and .12<p.y<.59 and p.z<.60):continue
        delta=p.x-source_points[old,0]
        if abs(delta)<1e-8:continue
        t=max(0,min(1,(p.z-.36)/.24));blend=t*t*t*(10-15*t+6*t*t)
        move_x(vertex.index,source_points[old,0]+delta*blend);faded+=1
    mesh.update();points=np.asarray([tuple(v.co)for v in mesh.vertices]);active={i for f in mesh.polygons if patch.data[f.index].value==-1 for i in f.vertices}
    def movable(index):
        old=ids.data[index].value-1;p=points[index]
        if old<0:return index in active and p[2]<.70
        return old not in protected_vertices and p[0]<-.2 and .10<p[1]<.60 and .25<p[2]<.56 and foot[old]<.35 and source_roles[old]=={1}
    unknown=sorted(i for i in range(len(points))if movable(i));lookup={i:j for j,i in enumerate(unknown)}
    adjacency=[set()for _ in mesh.vertices]
    for edge in mesh.edges:a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
    base=np.asarray([fit.point(p[1],p[2]).x for p in points]);residual=points[:,0]-base;rows=[];values=[]
    for i in set(unknown)|{j for i in unknown for j in adjacency[i]}:
        neighbors=[j for j in adjacency[i]if abs(points[j,0]-points[i,0])<.18]
        if not neighbors:continue
        weights=np.asarray([1/max(np.linalg.norm(points[j,1:]-points[i,1:])**2,.0001)for j in neighbors]);weights/=weights.sum()
        entries={i:1.};entries.update({j:-float(w)for j,w in zip(neighbors,weights)});row=np.zeros(len(unknown));target=0.
        for j,value in entries.items():
            if j in lookup:row[lookup[j]]+=value
            else:target-=value*residual[j]
        rows.append(row);values.append(target)
    for i in unknown:
        old=ids.data[i].value-1;strength=.8 if old>=0 else .08;row=np.zeros(len(unknown));row[lookup[i]]=strength;rows.append(row)
        values.append(float(source_points[old,0]-base[i])*strength if old>=0 else 0.)
    solution=np.linalg.lstsq(rows,values,rcond=None)[0]
    for i,j in lookup.items():move_x(i,base[i]+solution[j])
    mesh.update();points=np.asarray([tuple(v.co)for v in mesh.vertices]);source_normals=np.zeros_like(source_points)
    for polygon in source.data.polygons:
        for loop in polygon.loop_indices:source_normals[source.data.loops[loop].vertex_index]+=np.asarray(source.data.corner_normals[loop].vector)*polygon.area
    source_normals/=np.maximum(np.linalg.norm(source_normals,axis=1)[:,None],1e-20);cache={};normals=normals_before.copy()
    def final_normal(index):
        if index in cache:return cache[index]
        neighborhood={index};front={index}
        for _ in range(3):
            front={j for i in front for j in adjacency[i]}-neighborhood;neighborhood.update(front)
        p=points[index];rows=[];targets=[];h=.08
        for i in neighborhood:
            q=points[i];dy,dz=(q[1:]-p[1:])/h;distance=np.linalg.norm(q[1:]-p[1:]);weight=math.exp(-(distance/.075)**2)
            if distance>.14 or abs(q[0]-p[0])>.16:continue
            rows.append(np.asarray([1,dy,dz,dy*dy,dy*dz,dz*dz])*math.sqrt(weight));targets.append(q[0]*math.sqrt(weight))
        regular=np.diag([0,0,0,.0001,.0001,.0001]);coeff=np.linalg.lstsq(np.vstack([rows,regular]),np.r_[targets,np.zeros(6)],rcond=None)[0]
        cache[index]=np.asarray(Vector((-1,coeff[1]/h,coeff[2]/h)).normalized());return cache[index]
    for polygon,item in zip(mesh.polygons,face_ids.data):
        p=polygon.center
        if item.value-1 in protected_faces or not(p.x<-.2 and -.5<p.y<.8 and .23<p.z<.72)or patch.data[polygon.index].value not in(-1,0):continue
        previous=source.data.polygons[item.value-1]if item.value>0 else None
        previous_loops={source.data.loops[j].vertex_index:j for j in previous.loop_indices}if previous else{}
        for loop in polygon.loop_indices:
            index=mesh.loops[loop].vertex_index;old=ids.data[index].value-1;point=points[index]
            alpha=max(0,min(1,(.72-point[2])/.20));alpha=alpha*alpha*(3-2*alpha)
            unchanged=old>=0 and np.linalg.norm(point-source_points[old])<1e-7
            if unchanged and old in previous_loops:target=np.asarray(source.data.corner_normals[previous_loops[old]].vector)
            elif unchanged:target=source_normals[old]
            else:target=final_normal(index)
            normal=normals_before[loop]*(1-alpha)+target*alpha;normals[loop]=normal/np.linalg.norm(normal)
    mesh.normals_split_custom_set(normals.tolist())
    actual_foot_drift=max((np.linalg.norm(points[v.index]-source_points[item.value-1])for v,item in zip(mesh.vertices,ids.data)if item.value>0 and(source_points[item.value-1,2]<=.25 or foot[item.value-1]>=.35)),default=0.)
    require(actual_foot_drift<1e-7,'Lower cloth smoothing changed an actual foot')
    return {'method':'screened biharmonic residual with immutable-source position prior','source_prior':.8,'new_skin_prior':.08,
            'unknown_source_vertices':sum(ids.data[i].value>0 for i in unknown),'unknown_new_vertices':sum(ids.data[i].value==0 for i in unknown),
            'body_transition_faded_vertices':faded,'maximum_solver_displacement':float(np.max(abs(points[unknown,0]-original_points[unknown,0]))),
            'actual_foot_maximum_position_drift':float(actual_foot_drift),'upper_original_body_fixed_above_z':.56,'source_shading_normal_derivative_constraints':0}


def repair_body_harness(character,body,rig,source_body=None):
    """Curvature-fit side skin with a simple, constrained planar footprint."""
    _PIXELS.clear()
    owns_source=source_body is None;source=source_body or original_body(character)
    resolve_source_maps(character,source)
    _restore_original(body,source);inspection=inspect_source(character,body,source);colors=face_colors(source)
    fit={sign:SourceSurfaceFit(character,source,sign,inspection['sides'][str(sign)]['cap_bounds'],colors)for sign in(-1,1)}
    body_weight=source_vertex_body_weights(source);arm=source_vertex_arm_weights(source)
    parts=source_parts(character,source);protected_faces=protected_surface_faces(character,source)
    input_source_faces={item.value-1 for item in body.data.attributes['OriginalSurfaceFace'].data if item.value>0}
    protected_absent_input=sorted(protected_faces-input_source_faces);protected_faces.intersection_update(input_source_faces)
    anatomical_protected_faces=set(protected_faces)
    if protected_absent_input:
        owners=legacy_right_arm_faces(character);source_groups={g.index:g.name for g in source.vertex_groups}
        right=np.asarray([sum(g.weight for g in v.groups if source_groups[g.group]=='arm.R')for v in source.data.vertices])
        require(all(i in owners and flesh_color('Nori',colors[i])and min(right[j]for j in source.data.polygons[i].vertices)>=.58 for i in protected_absent_input),
                'Rear-assembly protection encountered a missing source face that is not a proven historical navy right paw')
    metallic=face_colors(source,'MetallicSmoothness')[:,0]if character=='Lumen'else None
    def hardware_face(index):
        if index in protected_faces:return False
        if character=='Tiko':return parts[index]in(2,13,22)
        if character=='Lumen':
            return bool(flesh_color('Nori',colors[index])or(not flesh_color(character,colors[index])and hardware_color(character,colors[index]))or metallic[index]>.35)
        return hardware_color(character,colors[index])
    protected_vertices={i for f in source.data.polygons if f.index in protected_faces for i in f.vertices}
    baseline_groups={g.index:g.name for g in body.vertex_groups}
    baseline_weights={}
    for polygon,item in zip(body.data.polygons,body.data.attributes['OriginalSurfaceFace'].data):
        if item.value-1 not in protected_faces:continue
        for i in polygon.vertices:
            vertex=body.data.vertices[i];original=body.data.attributes['OriginalSurfaceVertex'].data[i].value-1
            baseline_weights[(item.value-1,original)]={baseline_groups[g.group]:g.weight for g in vertex.groups}
    groups={g.index:g.name for g in source.vertex_groups}
    safe=np.array([sum(g.weight for g in v.groups if groups[g.group]in('root','body','arm.L','arm.R'))>.98 for v in source.data.vertices])
    foot_rings={v.index:0 for v in source.data.vertices if any(groups[g.group].startswith('foot.')and g.weight>.02 for g in v.groups)}
    adjacency=[set()for _ in source.data.vertices]
    for edge in source.data.edges:
        a,b=edge.vertices;adjacency[a].add(b);adjacency[b].add(a)
    frontier=list(foot_rings)
    for ring in range(1,4):
        following=[]
        for index in frontier:
            for other in adjacency[index]:
                if other not in foot_rings:foot_rings[other]=ring;following.append(other)
        frontier=following
    collar_safe=np.array([sum(g.weight for g in v.groups if groups[g.group]in('root','body','arm.L','arm.R'))>.80
                          and v.co.z<1.085 and v.co.y>.25 for v in source.data.vertices])
    hardware=[];local_hardware_eligible=set();obsolete_paw_roots=[]
    for f,color in zip(source.data.polygons,colors):
        if f.index not in input_source_faces:continue
        if f.index in protected_faces:continue
        local=all(abs(source.data.vertices[i].co.x)>.22 and -.26<source.data.vertices[i].co.y<.46 and .28<source.data.vertices[i].co.z<1.16 for i in f.vertices)
        if character=='Lumen':
            sign=1 if f.center.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds'];cy=(box[0][1]+box[1][1])*.5;cz=(box[0][2]+box[1][2])*.5
            ry=(box[1][1]-box[0][1])*.5+.09;rz=(box[1][2]-box[0][2])*.5+.16
            local=all(sign*source.data.vertices[i].co.x>min(abs(box[0][0]),abs(box[1][0]))-.05 and ((source.data.vertices[i].co.y-cy)/ry)**2+((source.data.vertices[i].co.z-cz)/rz)**2<1.5 for i in f.vertices)
        if local and hardware_face(f.index):local_hardware_eligible.add(f.index)
        lower_leather=all(source.data.vertices[i].co.z<.40 and sum(g.weight for g in source.data.vertices[i].groups if groups[g.group]in('body','foot.L','foot.R'))>.98 for i in f.vertices)
        body_or_leather_collar=min(body_weight[i]for i in f.vertices)>.95 or all(collar_safe[i]for i in f.vertices)or lower_leather
        residual_wrist=character=='Tiko'and parts[f.index]==22
        obsolete_paw=character=='Lumen'and flesh_color('Nori',color)
        if local and hardware_face(f.index)and(body_or_leather_collar or residual_wrist or obsolete_paw)and(max(arm[i]for i in f.vertices)<.05 or residual_wrist or obsolete_paw):
            hardware.append(f.index)
            if obsolete_paw:obsolete_paw_roots.append(f.index)
    contact_allowance=set(hardware)
    if character=='Lumen':
        edges={};neighbors=[set()for _ in source.data.polygons]
        for face in source.data.polygons:
            for edge in face.edge_keys:
                key=tuple(sorted(edge))
                for previous in edges.get(key,[]):neighbors[face.index].add(previous);neighbors[previous].add(face.index)
                edges.setdefault(key,[]).append(face.index)
        frontier=set(hardware)
        for _ in range(2):
            following={n for i in frontier for n in neighbors[i]}-contact_allowance
            contact_allowance.update(following);frontier=following
    mesh=body.data;bm=bmesh.new();bm.from_mesh(mesh)
    normal_layer=bm.loops.layers.float_vector.new('SmoothTorsoSourceNormal')
    for face,polygon in zip(bm.faces,mesh.polygons):
        for loop,index in zip(face.loops,polygon.loop_indices):loop[normal_layer]=mesh.corner_normals[index].vector
    fid=bm.faces.layers.int['OriginalSurfaceFace'];vid=bm.verts.layers.int['OriginalSurfaceVertex']
    part_layer=bm.faces.layers.int.get('TripoSurfacePart')or bm.faces.layers.int.new('TripoSurfacePart')
    for face in bm.faces:face[part_layer]=int(parts[face[fid]-1])+1 if face[fid]>0 else 0
    patch_layer=bm.faces.layers.int.new('SmoothTorsoPatchSide');modified=bm.verts.layers.int.new('SmoothTorsoModified')
    underlap_layer=bm.faces.layers.int.new('SmoothTorsoUnderlap')
    fit_weight=bm.verts.layers.float.new('SmoothTorsoFitWeight');final_normal=bm.verts.layers.float_vector.new('SmoothTorsoFinalNormal')
    uv=bm.loops.layers.uv.active;deform=bm.verts.layers.deform.verify();hardware_set=set(hardware)
    baseline_boundaries={edge for edge in bm.edges if edge.is_boundary}
    bmesh.ops.delete(bm,geom=[f for f in bm.faces if(f[fid]==0 and .2<f.calc_center_median().z<1.2)or(f[fid]>0 and f[fid]-1 in hardware_set)],context='FACES')
    def clothface(face):return face[fid]>0 and face[fid]-1 not in protected_faces and(flesh_color(character,colors[face[fid]-1])or(character=='Tiko'and parts[face[fid]-1]==12))
    sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
    from radio_root_caps import _ordered_boundary_loops
    def body_loops():
        raw=_ordered_boundary_loops(bm,[e for e in bm.edges if e.is_boundary]);loops=[]
        while raw:
            loop=raw.pop();seen={};split=False
            for i,vertex in enumerate(loop):
                if vertex in seen:
                    j=seen[vertex];raw.extend([loop[j:i],loop[:j]+loop[i:]]);split=True;break
                seen[vertex]=i
            if not split and len(loop)>=3:loops.append(loop)
        result=[]
        for loop in loops:
            edges=[bm.edges.get((v,loop[(i+1)%len(loop)]))for i,v in enumerate(loop)]
            if not any(edge not in baseline_boundaries for edge in edges):continue
            local=False
            for vertex in loop:
                p=vertex.co;sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
                if sign*p.x>min(abs(box[0][0]),abs(box[1][0]))-.15 and box[0][1]-.15<p.y<box[1][1]+.18 and box[0][2]-.2<p.z<box[1][2]+.2:local=True;break
            if not local:continue
            result.append(loop)
        return result
    trim=[];history=[]
    for iteration in range(40):
        loops=body_loops();remove=set();summary=[];blocked=[]
        for loop in loops:
            crossing=_crossings(loop);summary.append({'length':len(loop),'bounds':bounds([v.co for v in loop]),'crossings':len(crossing)})
            existing=bm.faces.get(loop)if len(set(loop))==len(loop)else None
            if existing is not None and len(loop)<=6:
                remove.add(existing);continue
            if len(loop)>20:
                polygon=[(v.co.y,v.co.z)for v in loop];sign=1 if sum(v.co.x for v in loop)>0 else-1
                for face in bm.faces:
                    if not clothface(face)or not all(v[vid]>0 and safe[v[vid]-1]for v in face.verts):continue
                    p=face.calc_center_median()
                    if sign*p.x>.25 and _inside((p.y,p.z),polygon):remove.add(face)
            for a,b in crossing:
                for index in(a,b):
                    edge=bm.edges.get((loop[index],loop[(index+1)%len(loop)]))
                    for face in edge.link_faces:
                        contact_fragment=character=='Lumen'and face[fid]-1 in contact_allowance and not flesh_color(character,colors[face[fid]-1])
                        if face[fid]>0 and face[fid]-1 not in protected_faces and(character=='Nori'or clothface(face)or face[fid]-1 in local_hardware_eligible or contact_fragment)and all(v[vid]>0 and(safe[v[vid]-1]or(collar_safe[v[vid]-1]and flesh_color(character,colors[face[fid]-1])))for v in face.verts):remove.add(face)
                        else:blocked.append({'source_face':face[fid]-1,'bounds':bounds([v.co for v in face.verts]),'vertices':[{'source':v[vid]-1,'safe':bool(v[vid]>0 and safe[v[vid]-1]),'groups':{groups[g.group]:g.weight for g in source.data.vertices[v[vid]-1].groups}if v[vid]>0 else{}}for v in face.verts]})
            # A simple projected polygon can still border the old folded skin
            # or a torn leather skirt. Remove that narrow source collar too;
            # a smooth replacement cannot hide a retained inward-facing ear.
            for index,vertex in enumerate(loop):
                edge=bm.edges.get((vertex,loop[(index+1)%len(loop)]))
                for face in edge.link_faces:
                    old=face[fid]-1
                    if old<0 or old in protected_faces:continue
                    p=face.calc_center_median();sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
                    local=box[0][1]-.12<p.y<box[1][1]+.12 and box[0][2]-.12<p.z<box[1][2]+.12
                    allowed=all(v[vid]>0 and(safe[v[vid]-1]or collar_safe[v[vid]-1])for v in face.verts)
                    warm=hardware_face(old)if character=='Nori'else old in local_hardware_eligible
                    cloth=flesh_color(character,colors[old])
                    if local and allowed and((cloth and(face.normal.x*sign<.2 or face.normal.dot(fit[sign].normal(p.y,p.z))<.35))or warm):
                        remove.add(face)
                        if warm and old not in hardware_set:hardware.append(old);hardware_set.add(old)
        history.append({'iteration':iteration,'loops':summary,'remove_faces':len(remove),'blocked':blocked})
        if not remove:break
        trim.extend(face[fid]-1 for face in remove if face[fid]>0)
        bmesh.ops.delete(bm,geom=list(remove),context='FACES')
        orphan=[v for v in bm.verts if not v.link_faces]
        if orphan:bmesh.ops.delete(bm,geom=orphan,context='VERTS')
    loops=body_loops();remaining=sum(len(_crossings(loop))for loop in loops)
    retained_exterior=set()
    if character=='Lumen':
        # Stop the bounded two-ring hardware cleanup at the retained exterior.
        # Its real folds are garment/pack geometry, not the body's heightfield.
        for loop in loops:
            for i,vertex in enumerate(loop):
                for face in bm.edges.get((vertex,loop[(i+1)%len(loop)])).link_faces:
                    if face[fid]>0 and not clothface(face):retained_exterior.add(face[fid]-1)
        protected_faces.update(retained_exterior)
        protected_vertices.update(i for face in source.data.polygons if face.index in retained_exterior for i in face.vertices)
        for polygon,item in zip(mesh.polygons,mesh.attributes['OriginalSurfaceFace'].data):
            if item.value-1 not in retained_exterior:continue
            for i in polygon.vertices:
                vertex=mesh.vertices[i];original=mesh.attributes['OriginalSurfaceVertex'].data[i].value-1
                baseline_weights[(item.value-1,original)]={baseline_groups[g.group]:g.weight for g in vertex.groups}
    folder=REVIEW/character;folder.mkdir(parents=True,exist_ok=True)
    removal={'hardware_source_face_ids':hardware,'trimmed_cloth_source_face_ids':trim,'history':history,'remaining_crossings':remaining}
    (folder/'cloth-removal.json').write_text(json.dumps(removal,indent=2)+'\n')
    protected_charts=[]
    for loop in loops:
        crossings=_crossings(loop)
        if not crossings:continue
        for a,b in crossings:
            edges=[bm.edges.get((loop[i],loop[(i+1)%len(loop)]))for i in(a,b)]
            require(any(any(face[fid]-1 in protected_faces for face in edge.link_faces)for edge in edges),'A non-protected cloth rim still crosses itself')
        protected_charts.append({'length':len(loop),'side':1 if sum(v.co.x for v in loop)>0 else-1,'source_overhang_crossings':len(crossings)})
    removal['protected_source_charts']=protected_charts
    (folder/'cloth-removal.json').write_text(json.dumps(removal,indent=2)+'\n')
    source_color=np.zeros((len(source.data.vertices),3));counts=np.zeros(len(source_color))
    for f,color in zip(source.data.polygons,colors):
        for i in f.vertices:source_color[i]+=color;counts[i]+=1
    source_color/=np.maximum(counts[:,None],1)
    cloth_vertices={v for face in bm.faces if clothface(face)for v in face.verts}
    changes=set();patches=[]
    from mathutils.geometry import delaunay_2d_cdt
    for loop in loops:
        if len(loop)<3:continue
        sign=1 if sum(v.co.x for v in loop)>0 else-1
        require(all(sign*v.co.x>.02 for v in loop),'A local closure crossed the protected center body.')
        original_boundary=[{'source_vertex':v[vid]-1,'position':list(v.co),'neighbors':[f[fid]-1 for f in bm.edges.get((v,loop[(i+1)%len(loop)])).link_faces]}for i,v in enumerate(loop)]
        loop,strips,runs=_independent_cloth_boundary(loop,bm,protected_faces,fid,vid,modified,patch_layer,deform,body,fit[sign])
        if strips:
            for face in strips:face[underlap_layer]=1
            bmesh.ops.triangulate(bm,faces=strips)
            patches.extend(f for f in bm.faces if f[patch_layer]==sign and f not in patches)
            protected_charts.append({'side':sign,'independent_cloth_runs':runs})
        independent_crossings=_crossings(loop)
        if independent_crossings and character=='Lumen':
            route=_route_private_boundary(loop,bm,vid,fit[sign]);protected_charts.append({'side':sign,'private_cloth_detour':route})
            independent_crossings=_crossings(loop)
        if independent_crossings:
            (folder/'underlap-crossing.json').write_text(json.dumps({'character':character,'side':sign,'source_boundary':original_boundary,
                'independent_boundary':[{'source_vertex':v[vid]-1,'position':list(v.co)}for v in loop],
                'crossing_edge_pairs':independent_crossings,'protected_source_faces':sorted(protected_faces),'runs':runs},indent=2)+'\n')
        require(not independent_crossings,'Independent torso boundary still folds in the side chart; see underlap-crossing.json')
        polygon=[(v.co.y,v.co.z)for v in loop];coords=[Vector(p)for p in polygon];inputs=list(loop)
        box=bounds([v.co for v in loop]);collar=.10
        for vertex in bm.verts:
            original=vertex[vid]-1;p=vertex.co
            if original<0 or original in protected_vertices or not safe[original] or vertex not in cloth_vertices or sign*p.x<.2:continue
            outside=max(box[0][1]-p.y,p.y-box[1][1],box[0][2]-p.z,p.z-box[1][2],0)
            if outside>collar:continue
            t=max(0,1-outside/collar);weight=t*t*(3-2*t);old_x=p.x
            foot_t=max(0,min(1,(foot_rings.get(original,3)-1)/2));weight*=foot_t*foot_t*(3-2*foot_t)
            p.x+=(fit[sign].point(p.y,p.z).x-p.x)*weight;vertex.co=p
            if abs(p.x-old_x)>1e-9:
                vertex[modified]=1;vertex[fit_weight]=1-(1-vertex[fit_weight])*(1-weight);changes.add(original)
            for layer in bm.verts.layers.shape.values():vertex[layer].x+=p.x-old_x
        chart=np.asarray(polygon)
        for u in np.arange(chart[:,0].min()+.018,chart[:,0].max(),.032):
            for z in np.arange(chart[:,1].min()+.018,chart[:,1].max(),.032):
                if _inside((u,z),polygon):coords.append(Vector((float(u),float(z))));inputs.append(None)
        indices=list(range(len(loop)));signed=sum(a[0]*b[1]-b[0]*a[1]for a,b in zip(polygon,polygon[1:]+polygon[:1]))
        if signed<0:indices.reverse()
        output=delaunay_2d_cdt(coords,[(i,(i+1)%len(loop))for i in range(len(loop))],[indices],1,1e-7,True)
        out_coords,_,triangles,origverts,_,_=output;mapping=[]
        boundary_yz=np.asarray([(v.co.y,v.co.z)for v in loop]);boundary_residual=np.asarray([v.co.x-fit[sign].point(v.co.y,v.co.z).x for v in loop])
        boundary_normals=[]
        for vertex in loop:
            p=vertex.co;base=fit[sign].normal(p.y,p.z);source_normals=[Vector(l[normal_layer])for l in vertex.link_loops if clothface(l.face)]
            measured=(sum(source_normals,Vector())/len(source_normals)).normalized()if source_normals else base
            weight=vertex[fit_weight];boundary_normals.append(measured.lerp(base,weight).normalized())
        residual_field=SmoothBoundaryResidual(fit[sign],boundary_yz,boundary_residual,boundary_normals)
        for vertex,normal in zip(loop,boundary_normals):vertex[final_normal]=normal
        for point,originals in zip(out_coords,origverts):
            boundary=[i for i in originals if i<len(loop)]
            require(len(boundary)<=1,f'CDT merged distinct authentic boundary vertices: {boundary}, {[(list(loop[i].co),loop[i][vid]-1) for i in boundary]}, chartpoint{list(point)}')
            if boundary:vertex=loop[boundary[0]]
            else:
                position=residual_field.point(point.x,point.y)
                vertex=bm.verts.new(position);vertex[vid]=0;vertex[modified]=1
                vertex[final_normal]=residual_field.normal(point.x,point.y)
                vertex[deform][body.vertex_groups['body'].index]=1.
                for layer in bm.verts.layers.shape.values():vertex[layer]=vertex.co
            mapping.append(vertex)
        for triangle in triangles:
            vertices=[mapping[i]for i in triangle]
            if sign<0:vertices.reverse()
            try:face=bm.faces.new(vertices)
            except ValueError:continue
            face[fid]=0;face[patch_layer]=sign;face.smooth=True;patches.append(face)
    # Local detached hardware sheets can survive as isolated scraps; an old
    # fusion ear can also overlap a new cap and create a three-face edge. Their
    # removal closes the repaired skin instead of adding duplicate cap faces.
    cleanup=set();visited=set()
    def local_scrap(face):
        if face[fid]<=0 or face[fid]-1 in protected_faces or clothface(face):return False
        p=face.calc_center_median();sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
        return sign*p.x>.2 and box[0][1]-.12<p.y<box[1][1]+.14 and box[0][2]-.12<p.z<box[1][2]+.12
    for face in list(bm.faces):
        if face in visited:continue
        component={face};queue=[face];visited.add(face)
        while queue:
            for edge in queue.pop().edges:
                for neighbor in edge.link_faces:
                    if neighbor not in visited:visited.add(neighbor);component.add(neighbor);queue.append(neighbor)
        if len(component)<=2 and all(local_scrap(f)for f in component):cleanup.update(component)
    for face in bm.faces:
        if face[fid]<=0 or face[fid]-1 in protected_faces:continue
        p=face.calc_center_median();sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
        local=sign*p.x>.2 and box[0][1]-.12<p.y<box[1][1]+.14 and box[0][2]-.20<p.z<box[1][2]+.12
        if local and any(e.is_boundary for e in face.edges)and any(len(e.link_faces)>2 and any(n[patch_layer]for n in e.link_faces)for e in face.edges):cleanup.add(face)
    cleanup_ids=[f[fid]-1 for f in cleanup]
    if cleanup:bmesh.ops.delete(bm,geom=list(cleanup),context='FACES')
    orphan=[v for v in bm.verts if not v.link_faces]
    if orphan:bmesh.ops.delete(bm,geom=orphan,context='VERTS')
    fold_repair=[]
    if character=='Tiko':
        bm.normal_update();folds=set()
        for face in bm.faces:
            old=face[fid]-1
            if old<0 or parts[old]!=12:continue
            p=face.calc_center_median();sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
            local=sign*p.x>min(abs(box[0][0]),abs(box[1][0]))-.08 and box[0][1]-.025<p.y<box[1][1]+.025 and box[0][2]-.025<p.z<box[1][2]+.025
            pure_body=all(v[vid]>0 and body_weight[v[vid]-1]>.9999 for v in face.verts)
            if local and pure_body and face.normal.x*sign<-.3:folds.add(face)
        if folds:
            region=set(folds);frontier=set(folds)
            for _ in range(2):
                following={neighbor for face in frontier for edge in face.edges for neighbor in edge.link_faces
                           if neighbor not in region and(neighbor[patch_layer]or(neighbor[fid]>0 and parts[neighbor[fid]-1]==12))}
                region.update(following);frontier=following
            rim={edge for face in region for edge in face.edges if any(neighbor not in region for neighbor in edge.link_faces)}
            fold_repair=[face[fid]-1 for face in region if face[fid]>0]
            bmesh.ops.delete(bm,geom=list(region),context='FACES')
            for loop in _ordered_boundary_loops(bm,[edge for edge in rim if edge.is_valid and edge.is_boundary]):
                require(not _crossings(loop),'Tiko local fold collar remains non-simple')
                sign=1 if sum(v.co.x for v in loop)>0 else-1;polygon=[Vector((v.co.y,v.co.z))for v in loop]
                signed=sum(a.x*b.y-b.x*a.y for a,b in zip(polygon,polygon[1:]+polygon[:1]));indices=list(range(len(loop)))
                if signed<0:indices.reverse()
                points,_,triangles,originals,_,_=delaunay_2d_cdt(polygon,[(i,(i+1)%len(loop))for i in range(len(loop))],[indices],1,1e-8,True)
                mapping=[]
                for point,ids in zip(points,originals):
                    require(len(ids)==1,'Tiko fold refinement changed the original collar anchors');mapping.append(loop[ids[0]])
                for triangle in triangles:
                    vertices=[mapping[i]for i in triangle]
                    if sign<0:vertices.reverse()
                    face=bm.faces.new(vertices);face[fid]=0;face[patch_layer]=sign;face.smooth=True;patches.append(face)
            orphan=[v for v in bm.verts if not v.link_faces]
            if orphan:bmesh.ops.delete(bm,geom=orphan,context='VERTS')
    patches=[f for f in patches if f.is_valid]
    bm.normal_update()
    paint_faces=list(patches);repainted_source=[];paint_side={f:f[patch_layer]for f in patches};paint_selection=[]
    contact_support={sign:min((sign*v.co.x for face in patches if face[patch_layer]==sign and not face[underlap_layer]for v in face.verts),default=.2)for sign in(-1,1)}
    contact_bounds={sign:bounds([v.co for face in patches if face[patch_layer]==sign and not face[underlap_layer]for v in face.verts])for sign in(-1,1)}
    if character in('Tiko','Lumen'):
        # Heal the source's baked fusion-contact pigment together with the cap.
        # Sampling another cream region on only the new faces leaves a visible
        # oval even when the geometry is smooth.
        for face in bm.faces:
            old=face[fid]-1
            protected_paint=protected_faces if character=='Tiko'else anatomical_protected_faces
            if old<0 or old in protected_paint or(character=='Tiko'and parts[old]!=12):continue
            p=face.calc_center_median();sign=1 if p.x>0 else-1;box=inspection['sides'][str(sign)]['cap_bounds']
            local=sign*p.x>min(abs(box[0][0]),abs(box[1][0]))-.12 and box[0][1]-.09<p.y<box[1][1]+.09 and box[0][2]-.075<p.z<box[1][2]+.075
            actual_body=all(body_weight[v[vid]-1]>.95 for v in face.verts if v[vid]>0)
            if character=='Lumen'or sign<0:
                # Restoring the old source can make obsolete cap bounds cross
                # X=0. The new skin's signed support is the actual contact ROI.
                local=sign*p.x>max(.08,contact_support[sign]-.08)and box[0][1]-.09<p.y<box[1][1]+.09 and box[0][2]-.075<p.z<box[1][2]+.075
                actual_body=all(v[deform].get(body.vertex_groups['body'].index,0)>.95 for v in face.verts)
            if character=='Lumen':
                # The repaired contact extends beyond the obsolete flat cap in
                # Y and Z too. Its visible skin footprint defines all three
                # paint coordinates; the immutable anatomical pack mask remains
                # excluded before this UV-only healing step.
                new_box=contact_bounds[sign]
                local=sign*p.x>max(.08,contact_support[sign]-.08)and new_box[0][1]-.09<p.y<new_box[1][1]+.09 and new_box[0][2]-.075<p.z<new_box[1][2]+.075
            if character=='Lumen':actual_body=actual_body and metallic[old]<.05
            paint_selection.append({'source_face':old,'center':list(p),'local':bool(local),'body':bool(actual_body),'signed_skin_support':contact_support[sign],'source_vertices':[v[vid]-1 for v in face.verts],'weights':[float(body_weight[v[vid]-1])for v in face.verts if v[vid]>0]})
            if local and actual_body:paint_faces.append(face);paint_side[face]=sign;repainted_source.append(old)
    if character in('Tiko','Lumen'):(folder/'paint-selection.json').write_text(json.dumps(paint_selection,indent=2)+'\n')
    report_patches={}
    for sign in(-1,1):
        faces=[f for f in paint_faces if paint_side[f]==sign];require(faces,f'No patch for side {sign}')
        box=bounds([v.co for f in faces for v in f.verts]);area=sum(f.calc_area()for f in faces)
        density=inspection['sides'][str(sign)]['uv_area_per_world_area_median'];charts=[]
        def paint_chart(chart,role,depth=0):
            if not chart:return
            chart_area=sum(f.calc_area()for f in chart);vertices=sorted({v for f in chart for v in f.verts},key=lambda v:tuple(v.co))
            if role=='hidden-clothing-underlap':
                xyz=np.asarray([tuple(v.co)for v in vertices]);center=xyz.mean(axis=0);_,axes=np.linalg.eigh((xyz-center).T@(xyz-center));axes=axes[:,-2:]
                for axis in range(2):
                    if axes[np.argmax(abs(axes[:,axis])),axis]<0:axes[:,axis]*=-1
                coordinates={v:(np.asarray(v.co)-center)@axes for v in vertices};projection='local tangent UV only'
            else:coordinates={v:np.asarray((v.co.y,v.co.z))for v in vertices};projection='side YZ'
            flat=np.asarray(list(coordinates.values()));low=flat.min(axis=0);high=flat.max(axis=0)
            projected=sum(abs(np.linalg.det(np.asarray([coordinates[f.verts[1]]-coordinates[f.verts[0]],coordinates[f.verts[2]]-coordinates[f.verts[0]]])))*.5 for f in chart)
            scale=math.sqrt(density*chart_area/projected)
            triangles_uv=[[(coordinates[v]-low)*scale for v in f.verts]for f in chart]
            try:
                if role=='hidden-clothing-underlap'and chart_area/max(projected,1e-12)>1.25:raise RuntimeError('Split a curved hidden ribbon into local tangent UV charts')
                target=inspection['sides'][str(sign)]['healthy_mean_color']
                if character=='Tiko':
                    healthy=[colors[f.index]for f in source.data.polygons if parts[f.index]==12 and colors[f.index][2]>colors[f.index][1]*.83 and colors[f.index][0]<colors[f.index][1]*1.25]
                    target=np.median(healthy,axis=0)
                elif character=='Lumen':target=(.7568628,.6039216,.2627451)
                origin,paint=atlas_patch(character,source,float(high[0]-low[0])*scale,float(high[1]-low[1])*scale,target,triangles_uv,body_only=character in('Tiko','Lumen'))
            except RuntimeError:
                limit=12 if role=='hidden-clothing-underlap'else 6
                if character not in('Lumen','Tiko')or depth>=limit or len(chart)<2:raise
                axis=int(np.argmax(high-low));ordered=sorted(chart,key=lambda f:np.mean([coordinates[v][axis]for v in f.verts]));middle=len(ordered)//2
                paint_chart(ordered[:middle],role,depth+1);paint_chart(ordered[middle:],role,depth+1);return
            for face in chart:
                for loop in face.loops:
                    coordinate=(coordinates[loop.vert]-low)*scale;loop[uv].uv=origin+Vector((float(coordinate[0]),float(coordinate[1])))
            charts.append({'role':role,'projection':projection,'faces':len(chart),'world_area':chart_area,'uv_area_per_world_area':density,'paint':paint})
        if character=='Lumen':
            paint_chart([f for f in faces if not f[underlap_layer]],'visible-skin')
            paint_chart([f for f in faces if f[underlap_layer]],'hidden-clothing-underlap')
        else:paint_chart(faces,'continuous-skin')
        for face in faces:
            if not face[patch_layer]:continue
            for loop in face.loops:
                p=loop.vert.co
                normal=Vector(loop.vert[final_normal]);loop[normal_layer]=normal if normal.length>.5 else fit[sign].normal(p.y,p.z)
        report_patches[str(sign)]={'faces':len(faces),'world_area':area,'opposite_x_area':sum(f.calc_area()for f in faces if f.normal.x*sign<0),
                                  'paint':charts[0]['paint'],'uv_charts':charts,'source_fit':fit[sign].report}
    material_leaks=heal_tiko_paint_leaks(source,paint_faces,uv)if character=='Tiko'else heal_lumen_paint_leaks(source,paint_faces,uv)if character=='Lumen'else []
    for face in bm.faces:
        if face[patch_layer]or face[fid]<=0:continue
        if clothface(face):
            for loop in face.loops:
                if loop.vert[modified]:
                    p=loop.vert.co;target=fit[1 if p.x>0 else-1].normal(p.y,p.z);weight=loop.vert[fit_weight]
                    loop[normal_layer]=Vector(loop[normal_layer]).lerp(target,weight).normalized()
    from mathutils.bvhtree import BVHTree
    tree=BVHTree.FromBMesh(bm);coverage={}
    for sign in(-1,1):
        box=inspection['sides'][str(sign)]['cap_bounds'];hits=0;total=0
        for y in np.linspace(box[0][1]+.04,box[1][1]-.04,7):
            for z in np.linspace(box[0][2]+.04,box[1][2]-.04,9):
                point,normal,index,distance=tree.ray_cast(Vector((sign*2,y,z)),Vector((-sign,0,0)),4);total+=1
                if point and sign*point.x>.25 and sign*normal.x>.05:hits+=1
        coverage[str(sign)]={'good':hits,'total':total}
    topology={'boundary_edges':sum(edge.is_boundary for edge in bm.edges),'nonmanifold_edges':sum(not edge.is_manifold for edge in bm.edges),'edges_more_than_two_faces':sum(len(edge.link_faces)>2 for edge in bm.edges),
              'introduced_boundary_edges':sum(edge.is_boundary and edge not in baseline_boundaries for edge in bm.edges)}
    bm.to_mesh(mesh);bm.free();mesh.update();normals=mesh.attributes['SmoothTorsoSourceNormal']
    mesh.normals_split_custom_set([Vector(v.vector).normalized()for v in normals.data]);mesh.attributes.remove(normals)
    lower_transition=relax_nori_body_transition(mesh,source,fit[-1],parts,protected_faces)if character=='Nori'else None
    body['smooth_torso_revision']='source-cdt-curvature-cloth-v4'
    actual_vertices={item.value-1:vertex for vertex,item in zip(mesh.vertices,mesh.attributes['OriginalSurfaceVertex'].data)if item.value>0}
    protected_drift=max(((actual_vertices[i].co-source.data.vertices[i].co).length for i in protected_vertices if i in actual_vertices),default=0.)
    protected_missing=[i for i in protected_faces if i not in{item.value-1 for item in mesh.attributes['OriginalSurfaceFace'].data if item.value>0}]
    require(not protected_missing and protected_drift<1e-7,f'Protected scarf surface was changed: faces{protected_missing[:8]}, drift{protected_drift}')
    protected_uv=0.;protected_angle=0.;protected_weights=0.;protected_shape=0.;weight_outliers=[]
    source_groups={g.index:g.name for g in source.vertex_groups};current_groups={g.index:g.name for g in body.vertex_groups}
    for original in protected_vertices:
        if original not in actual_vertices:continue
        vertex=actual_vertices[original];old=source.data.vertices[original]
        if mesh.shape_keys and source.data.shape_keys:
            for key in mesh.shape_keys.key_blocks:
                oldkey=source.data.shape_keys.key_blocks.get(key.name)
                if oldkey:protected_shape=max(protected_shape,((key.data[vertex.index].co-vertex.co)-(oldkey.data[original].co-old.co)).length)
    for polygon,item in zip(mesh.polygons,mesh.attributes['OriginalSurfaceFace'].data):
        if item.value-1 not in protected_faces:continue
        old=source.data.polygons[item.value-1];lookup={source.data.loops[i].vertex_index:i for i in old.loop_indices}
        for loop in polygon.loop_indices:
            original=mesh.attributes['OriginalSurfaceVertex'].data[mesh.loops[loop].vertex_index].value-1;previous=lookup[original]
            vertex=mesh.vertices[mesh.loops[loop].vertex_index]
            before=baseline_weights[(item.value-1,original)];after={current_groups[g.group]:g.weight for g in vertex.groups}
            drift=max((abs(before.get(k,0)-after.get(k,0))for k in before.keys()|after.keys()),default=0.);protected_weights=max(protected_weights,drift)
            if drift>1e-7:weight_outliers.append({'source_face':item.value-1,'original':original,'before':before,'after':after})
            if not(character=='Lumen'and item.value-1 in repainted_source and item.value-1 not in anatomical_protected_faces):
                protected_uv=max(protected_uv,(mesh.uv_layers.active.data[loop].uv-source.data.uv_layers.active.data[previous].uv).length)
            protected_angle=max(protected_angle,math.degrees(mesh.corner_normals[loop].vector.angle(source.data.corner_normals[previous].vector)))
    require(protected_uv<1e-7 and protected_angle<.1 and protected_weights<1e-7 and protected_shape<1e-7,f'Protected scarf data changed: UV{protected_uv}, normal{protected_angle}, weights{protected_weights}, shape{protected_shape}, examples{weight_outliers[:3]}')
    report={'revision':body['smooth_torso_revision'],'character':character,'patches':report_patches,'coverage':coverage,'trimmed_source_faces':len(trim),
            'removed_hardware_faces':len(hardware),'modified_source_vertices':len(changes),'paws_untouched':True,'harness_status':'awaiting rounded helper',
            'protected_source_charts':protected_charts,
            'removed_local_scrap_source_faces':cleanup_ids,
            'retained_exterior_source_faces':sorted(retained_exterior),
            'retopologized_local_fold_source_faces':sorted(fold_repair),
            'repainted_local_body_source_faces':sorted(repainted_source),
            'body_contact_paint_bounds':contact_bounds,
            'removed_obsolete_paw_root_source_faces':sorted(obsolete_paw_roots),
            'repaired_native_pixel_material_leaks':material_leaks,
            'lower_body_transition':lower_transition,
            'topology':topology,
            'semantic_protection':{'source_faces':len(protected_faces),'source_vertices':len(protected_vertices),'maximum_position_drift':protected_drift,'missing_faces':len(protected_missing),
                                   'source_face_ids_absent_before_this_repair':protected_absent_input,
                                   'local_body_contact_uv_exceptions':sorted(i for i in repainted_source if i in protected_faces and i not in anatomical_protected_faces),
                                   'geometry_uv_normals_blink_baseline':'d8019f2a8b00f1c6481c1a086056ebc543c6f1e9','skin_weights_baseline':'supplied input Character (87bc426 source snapshot)',
                                   'maximum_uv_drift':protected_uv,'maximum_normal_angle_degrees':protected_angle,'maximum_weight_drift':protected_weights,'maximum_shape_delta_drift':protected_shape},
            'review_status':'candidate, not promoted'}
    if character=='Lumen':
        independent_pack,independent_report=split_lumen_radio_contact(body,rig,source,report_patches['-1']['uv_charts'][0]['uv_area_per_world_area'])
        report['independent_radio']=independent_report
        report['semantic_protection']['validation_stage']='Native surface guard before the separately reported back/contact retopology; radio source surfaces transfer to Pack'
        report['topology']={'boundary_edges':independent_report['body_topology']['boundary'],'nonmanifold_edges':independent_report['body_topology']['nonmanifold'],
                            'edges_more_than_two_faces':0,'introduced_boundary_edges':0}
    if owns_source:
        data=source.data;bpy.data.objects.remove(source,do_unlink=True)
        if data.users==0:bpy.data.meshes.remove(data)
    return report



def smooth_lumen_back_shading(body):
    """Join the accepted side shading to the actual closed back geometry.

    A rear height chart cannot represent a slightly front-facing side tangent.
    Keep that authentic boundary normal and blend across two new mesh rings.
    Interior normals come from the final triangles, including the end fades.
    """
    mesh=body.data;mesh.update();under=mesh.attributes['SmoothTorsoUnderlap'];origin=mesh.attributes['OriginalSurfaceFace']
    cap={f.index for f in mesh.polygons if origin.data[f.index].value==0 and under.data[f.index].value
         and all(mesh.vertices[i].co.y>=.099999 and .449999<=mesh.vertices[i].co.z<=1.53 for i in f.vertices)}
    require(cap,'Independent rear skin is missing')
    positions=np.asarray([tuple(v.co)for v in mesh.vertices]);normals=[n.vector.copy()for n in mesh.corner_normals]
    linked=[[]for _ in mesh.vertices];adj=[set()for _ in mesh.vertices];geometric=np.zeros((len(mesh.vertices),3))
    for face in mesh.polygons:
        for li in face.loop_indices:linked[mesh.loops[li].vertex_index].append((face.index,li))
        if face.index in cap:
            for i in face.vertices:geometric[i]+=np.asarray(face.normal)*face.area
            for a,b in face.edge_keys:adj[a].add(b);adj[b].add(a)
    boundary={};rings={}
    for i in{j for fi in cap for j in mesh.polygons[fi].vertices}:
        outside=[(fi,li)for fi,li in linked[i]if fi not in cap]
        if outside:
            fi,li=max(outside,key=lambda item:mesh.polygons[item[0]].area);boundary[i]=np.asarray(normals[li]);rings[i]=0
    require(boundary,'Rear shading has no authentic side boundary')
    frontier=set(boundary)
    for ring in(1,2):
        following={j for i in frontier for j in adj[i]if j not in rings}
        for j in following:rings[j]=ring
        frontier=following
    ids=np.asarray(list(boundary));points=positions[ids];old=np.asarray([boundary[i]for i in ids]);desired={}
    for i in{j for fi in cap for j in mesh.polygons[fi].vertices}:
        if i in boundary:desired[i]=Vector(tuple(boundary[i]));continue
        normal=geometric[i]/max(np.linalg.norm(geometric[i]),1e-12);ring=rings.get(i,3)
        if ring<=2:
            distance=np.sum((points-positions[i])**2,axis=1);nearest=np.argsort(distance)[:3];weights=1/np.maximum(distance[nearest],1e-10);source=(old[nearest]*weights[:,None]).sum(0)/weights.sum();blend=.60 if ring==1 else .85;normal=normal*blend+source*(1-blend);normal/=max(np.linalg.norm(normal),1e-12)
        desired[i]=Vector(tuple(normal))
    for fi in cap:
        for li in mesh.polygons[fi].loop_indices:normals[li]=desired[mesh.loops[li].vertex_index]
    mesh.normals_split_custom_set(normals)
    return{'faces':len(cap),'authentic_boundary_vertices':len(boundary),'transition_rings':2,'geometry_uv_weights_blink_changed':False}


def finish_lumen_pack_contact(pack,source,density):
    """Finish the fused case/body join with a real closed, bevelled leather lid.

    The native outer radio and its controls are retained. The irregular old
    front closure stays inside this compact case part; no character, shoulder,
    harness, antenna, eye or original outer-case vertex is changed.
    """
    from mathutils import Matrix
    mesh=pack.data;bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table()
    cap=bm.faces.layers.int['IndependentHiddenCap'];fid=bm.faces.layers.int['OriginalSurfaceFace'];vid=bm.verts.layers.int['OriginalSurfaceVertex'];face_origin=bm.faces.layers.int['RadioContactOriginalFace'];uv=bm.loops.layers.uv.active
    normals=bm.loops.layers.float_vector.new('PackContactKeptNormal')
    for face,polygon in zip(bm.faces,mesh.polygons):
        for loop,li in zip(face.loops,polygon.loop_indices):loop[normals]=mesh.corner_normals[li].vector
    old_contact={f for f in bm.faces if f[cap]or f[face_origin]>0}
    old_vertices=set(bm.verts);old_faces=set(bm.faces)
    # Source contact occupies X[-.470,.258], Z[.598,1.249],
    # Y[.149,.375]. This entirely closed case lid covers that cut, with a
    # generous 85-mm rounded rim rather than a stretched triangle fan.
    center=Vector((-.105,.265,.924));half=Vector((.390,.155,.345))
    bmesh.ops.create_cube(bm,size=2,matrix=Matrix.Translation(center)@Matrix.Diagonal((*half,1.)))
    edges=[e for e in bm.edges if all(v not in old_vertices for v in e.verts)]
    bmesh.ops.bevel(bm,geom=edges,offset=.085,segments=4,profile=.5,affect='EDGES',clamp_overlap=True)
    new_faces={f for f in bm.faces if f not in old_faces};edges=list({e for f in new_faces for e in f.edges})
    bmesh.ops.subdivide_edges(bm,edges=edges,cuts=2,use_grid_fill=True)
    new_faces={f for f in bm.faces if f not in old_faces}
    polygons=[f for f in new_faces if len(f.verts)>3]
    if polygons:bmesh.ops.triangulate(bm,faces=polygons,quad_method='BEAUTY',ngon_method='BEAUTY')
    new_faces={f for f in bm.faces if f not in old_faces};deform=bm.verts.layers.deform.verify();body_group=pack.vertex_groups['body'].index
    print('PACK_LID_GEOMETRY',json.dumps({'native':len(old_faces),'new_lid':len(new_faces),'total':len(bm.faces)}),flush=True)
    require(len(bm.faces)<9000,'Pack lid exceeded its local triangle budget')
    for v in bm.verts:
        if v not in old_vertices:v[vid]=0;v[deform].clear();v[deform][body_group]=1.
    for f in new_faces:f[cap]=2;f[fid]=0;f.smooth=True
    bm.faces.index_update();bm.normal_update();paint_reports=[]
    def paint(faces,axes,region,depth=0):
        coordinates=np.asarray([(v.co[axes[0]],v.co[axes[1]])for f in faces for v in f.verts]);lo=coordinates.min(0);hi=coordinates.max(0)
        projected=sum(abs((f.verts[1].co[axes[0]]-f.verts[0].co[axes[0]])*(f.verts[2].co[axes[1]]-f.verts[0].co[axes[1]])-(f.verts[1].co[axes[1]]-f.verts[0].co[axes[1]])*(f.verts[2].co[axes[0]]-f.verts[0].co[axes[0]]))*.5 for f in faces)
        scale=math.sqrt(density*sum(f.calc_area()for f in faces)/max(projected,1e-12));tris=[[(np.asarray((v.co[axes[0]],v.co[axes[1]]))-lo)*scale for v in f.verts]for f in faces]
        try:offset,description=atlas_patch('Lumen',source,float(hi[0]-lo[0])*scale,float(hi[1]-lo[1])*scale,(.54,.25,.045),tris,False,region)
        except RuntimeError:
            require(depth<16 and len(faces)>1,'Native radio leather cannot fit contact panel');axis=axes[int(np.argmax(hi-lo))];ordered=sorted(faces,key=lambda f:(f.calc_center_median()[axis],f.index));middle=len(ordered)//2;paint(ordered[:middle],axes,region,depth+1);paint(ordered[middle:],axes,region,depth+1);return
        paint_reports.append(description)
        for face in faces:
            for loop in face.loops:loop[uv].uv=offset+Vector(tuple((np.asarray((loop.vert.co[axes[0]],loop.vert.co[axes[1]]))-lo)*scale))
    groups={}
    for face in sorted(old_contact|new_faces,key=lambda f:f.index):
        axis=int(np.argmax(np.abs(face.normal[:])));groups.setdefault((axis,face.normal[axis]>0,face in new_faces),[]).append(face)
    for(axis,sign,new),faces in groups.items():paint(faces,[i for i in range(3)if i!=axis],'radio_leather'if new else'radio_leather_contact')
    # Native case normals stay exact. Only the added lid receives new smooth
    # geometric normals, so its broad panels are flat and bevels stay round.
    for face in new_faces:
        for loop in face.loops:loop[normals]=loop.vert.normal
    report={'revision':'closed-bevelled-native-leather-lid-v3','retained_contact_faces':len(old_contact),'new_lid_faces':len(new_faces),'panel_front_y':.11,'bevel_radius':.085,'lid_center':list(center),'lid_half_extents':list(half),'new_leather_native_blocks_fully_covered':True,'original_outer_case_vertices_changed':0,'boundary_edges':sum(e.is_boundary for e in bm.edges),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'triangles':len(bm.faces),'vertices':len(bm.verts),'native_leather_charts':paint_reports}
    require(report['boundary_edges']==0 and report['nonmanifold_edges']==0,'Pack contact and added lid must remain closed')
    require(report['triangles']<9000,'Pack lid exceeded its local triangle budget')
    bm.to_mesh(mesh);bm.free();mesh.update();attribute=mesh.attributes['PackContactKeptNormal'];mesh.normals_split_custom_set([p.vector for p in attribute.data]);mesh.attributes.remove(attribute)
    return report


def split_lumen_radio_contact(body, rig, source, density):
    """Retain the complete radio shell and close independent authentic torso skin."""
    from radio_root_caps import _ordered_boundary_loops
    from mathutils.geometry import delaunay_2d_cdt
    r = sys.modules[__name__]
    protected=r.protected_surface_faces('Lumen',source);antenna_groups={g.index for g in source.vertex_groups if g.name.startswith('antenna.')};accessory_faces={f.index for f in source.data.polygons if any(g.group in antenna_groups and g.weight>1e-5 for i in f.vertices for g in source.data.vertices[i].groups)};protected-=accessory_faces;report={};body['separate_pack']=True
    # Complete original connected brass/leather parts, including the round radio
    # controls that extend in front of the old broad rear-assembly guard.
    colors=r.face_colors(body);metals=r.face_colors(body,'MetallicSmoothness')[:,0];original=body.data.attributes['OriginalSurfaceFace'];selected={f.index for f in body.data.polygons if original.data[f.index].value-1 in protected};eligible=set()
    for f in body.data.polygons:
        old=original.data[f.index].value-1;rr,gg,bb=colors[f.index]
        if old>=0 and old not in accessory_faces and f.center.y>.06 and .42<f.center.z<1.50 and(metals[f.index]>.08 or(rr>gg*1.5 and bb<gg*.8)):eligible.add(f.index)
    edges={};neighbors=[set()for _ in body.data.polygons]
    for f in body.data.polygons:
        for edge in f.edge_keys:
            edge=tuple(sorted(edge))
            for other in edges.get(edge,[]):neighbors[f.index].add(other);neighbors[other].add(f.index)
            edges.setdefault(edge,[]).append(f.index)
    todo=list(selected)
    while todo:
        for other in neighbors[todo.pop()]:
            if other not in selected and other in eligible:selected.add(other);todo.append(other)
    protected={original.data[i].value-1 for i in selected};report['radio_original_source_faces']=len(protected)
    def start(obj):
        mesh=obj.data;bm=bmesh.new();bm.from_mesh(mesh);bm.faces.ensure_lookup_table();bm.verts.ensure_lookup_table();normal=bm.loops.layers.float_vector.new('IndependentKeptNormal')
        for face,polygon in zip(bm.faces,mesh.polygons):
            for loop,li in zip(face.loops,polygon.loop_indices):loop[normal]=mesh.corner_normals[li].vector
        return bm,normal,bm.faces.layers.int['OriginalSurfaceFace'],bm.verts.layers.int['OriginalSurfaceVertex'],bm.loops.layers.uv.active,bm.verts.layers.deform.verify()
    def finish(obj,bm,normal):
        polygons=[f for f in bm.faces if len(f.verts)>3]
        if polygons:bmesh.ops.triangulate(bm,faces=polygons,quad_method='BEAUTY',ngon_method='BEAUTY')
        orphan=[v for v in bm.verts if not v.link_faces]
        if orphan:bmesh.ops.delete(bm,geom=orphan,context='VERTS')
        topology={'boundary':sum(e.is_boundary for e in bm.edges),'nonmanifold':sum(not e.is_manifold for e in bm.edges),'triangles':sum(len(f.verts)-2 for f in bm.faces)};bm.to_mesh(obj.data);bm.free();obj.data.update();attribute=obj.data.attributes[normal];obj.data.normals_split_custom_set([v.vector for v in attribute.data]);obj.data.attributes.remove(attribute);return topology
    def paint(obj,bm,faces,uv,normal):
        def chart(faces,depth=0):
            coords=np.array([(v.co.x,v.co.z)for f in faces for v in f.verts]);low=coords.min(0);high=coords.max(0);world=sum(f.calc_area()for f in faces);projected=sum(abs((f.verts[1].co.x-f.verts[0].co.x)*(f.verts[2].co.z-f.verts[0].co.z)-(f.verts[1].co.z-f.verts[0].co.z)*(f.verts[2].co.x-f.verts[0].co.x))*.5 for f in faces);scale=np.sqrt(density*world/max(projected,1e-12));triangles=[[(np.array((v.co.x,v.co.z))-low)*scale for v in f.verts]for f in faces]
            try:origin,_=r.atlas_patch('Lumen',source,float(high[0]-low[0])*scale,float(high[1]-low[1])*scale,(.7568628,.6039216,.2627451),triangles,True)
            except RuntimeError:
                r.require(depth<10 and len(faces)>1,'Independent body UV cannot fit native cloth');axis=int(np.argmax(high-low));ordered=sorted(faces,key=lambda f:f.calc_center_median()[0 if axis==0 else 2]);middle=len(ordered)//2;chart(ordered[:middle],depth+1);chart(ordered[middle:],depth+1);return
            for f in faces:
                for loop in f.loops:loop[uv].uv=origin+Vector(tuple((np.array((loop.vert.co.x,loop.vert.co.z))-low)*scale))
        chart(faces);r.heal_lumen_paint_leaks(source,faces,uv)
    pack=body.copy();pack.data=body.data.copy();pack.name='Lumen_IndependentPack';pack['mesh_role']='Pack';bpy.context.scene.collection.objects.link(pack);pack.shape_key_clear()
    pbm,pn,pfid,pvid,puv,pdef=start(pack);capmark=pbm.faces.layers.int.new('IndependentHiddenCap');contact_origin=pbm.verts.layers.int.new('RadioContactOriginalVertex');contact_face_origin=pbm.faces.layers.int.new('RadioContactOriginalFace');remove=[f for f in pbm.faces if f[pfid]-1 not in protected];bmesh.ops.delete(pbm,geom=remove,context='FACES')
    for iteration in range(30):
        loops=_ordered_boundary_loops(pbm,[e for e in pbm.edges if e.is_boundary]);trim=set()
        for loop in loops:
            coords=np.array([(v.co.x,v.co.z)for v in loop]);cross=r._fast_crossings(coords)
            boundaryset=set(loop);trim.update(f for f in pbm.faces if set(f.verts)<=boundaryset and all(v.co.y<.40 for v in f.verts))
            for pair in cross:
                for i in pair:
                    edge=pbm.edges.get((loop[i],loop[(i+1)%len(loop)]));trim.update(f for f in edge.link_faces if all(v.co.y<.43 for v in f.verts))
        if not trim:break
        bmesh.ops.delete(pbm,geom=list(trim),context='FACES')
    else:raise RuntimeError('Pack contact collar did not become a simple front contour')
    closed=[];pcuts=[]
    for loop in _ordered_boundary_loops(pbm,[e for e in pbm.edges if e.is_boundary]):
        print('PACK_CONTACT_LOOP',json.dumps({'vertices':len(loop),'bounds':r.bounds([v.co for v in loop]),'xz_crossings':len(r._fast_crossings(np.array([(v.co.x,v.co.z)for v in loop])))}),flush=True)
        for v in loop:
            if abs(v.co.y-.32)>1e-7:
                if v[pvid]>0:v[contact_origin]=v[pvid];v[pvid]=0
                for neighbor in v.link_faces:
                    if neighbor[pfid]>0:neighbor[contact_face_origin]=neighbor[pfid];neighbor[pfid]=0
            v.co.y=.32
        coords=np.array([(v.co.x,v.co.z)for v in loop]);r.require(not r._fast_crossings(coords),'Radio planar cut must have a simple contour');order=list(range(len(loop)))
        if sum(a[0]*b[1]-b[0]*a[1]for a,b in zip(coords,np.roll(coords,-1,axis=0)))<0:order.reverse()
        points,_,triangles,original,_,_=delaunay_2d_cdt([Vector(tuple(p))for p in coords],[(i,(i+1)%len(loop))for i in range(len(loop))],[order],1,1e-8,True);mapping=[loop[ids[0]]for ids in original];first=[]
        for tri in triangles:
            vertices=[mapping[i]for i in tri];face=pbm.faces.new(vertices);face[pfid]=0;face[capmark]=1;face.smooth=True;closed.append(face);first.append(face)
        pbm.normal_update()
        if sum(f.normal.y*f.calc_area()for f in first)>0:
            for f in first:f.normal_flip()
        pcuts.append({'vertices':len(loop),'bounds':r.bounds([v.co for v in loop])})
    pbm.normal_update();print('PACK_PLANAR_CLOSURE',len(closed),flush=True)
    def pack_paint(faces,depth=0):
        coords=np.array([(v.co.x,v.co.z)for f in faces for v in f.verts]);low=coords.min(0);high=coords.max(0);scale=math.sqrt(density);triangles=[[(np.array((v.co.x,v.co.z))-low)*scale for v in f.verts]for f in faces]
        try:origin,_=r.atlas_patch('Lumen',source,float(high[0]-low[0])*scale,float(high[1]-low[1])*scale,(.60,.40,.16),triangles,False)
        except RuntimeError:
            r.require(depth<10 and len(faces)>1,'Radio back panel cannot fit source material');axis=int(np.argmax(high-low));ordered=sorted(faces,key=lambda f:f.calc_center_median()[0 if axis==0 else 2]);mid=len(ordered)//2;pack_paint(ordered[:mid],depth+1);pack_paint(ordered[mid:],depth+1);return
        for f in faces:
            for l in f.loops:l[puv].uv=origin+Vector(tuple((np.array((l.vert.co.x,l.vert.co.z))-low)*scale));l[pn]=Vector((0,-1,0))
    pack_paint(closed)
    report['pack_cuts']=pcuts;report['pack_topology']=finish(pack,pbm,'IndependentKeptNormal');report['pack_blink_keys']=0
    report['pack_contact_finish']=finish_lumen_pack_contact(pack,source,density)
    report['pack_topology']={'boundary':report['pack_contact_finish']['boundary_edges'],'nonmanifold':report['pack_contact_finish']['nonmanifold_edges'],'triangles':report['pack_contact_finish']['triangles']}
    bm,normal,fid,vid,uv,deform=start(body);accessory=bm.faces.layers.int.new('IndependentAccessoryGuard')
    for f in bm.faces:f[accessory]=int(f[fid]-1 in accessory_faces)
    removedpack=[f for f in bm.faces if f[fid]-1 in protected];bmesh.ops.delete(bm,geom=removedpack,context='FACES');openedges=[e for e in bm.edges if e.is_boundary];boundpoints=[v.co for e in openedges for v in e.verts];zlow=.45;zhigh=max(v.z for v in boundpoints)+.005;zhigh=min(1.52,zhigh);ycut=.10
    report['contact_window']={'zlow':zlow,'zhigh':zhigh,'ycut':ycut}
    for plane,axis in((zlow,2),(zhigh,2),(ycut,1)):
        candidates=[f for f in bm.faces if not f[accessory]and max(v.co.y for v in f.verts)>.075 and min(v.co.z for v in f.verts)<zhigh+.01 and max(v.co.z for v in f.verts)>zlow-.01];geometry=set(candidates)|{e for f in candidates for e in f.edges}|{v for f in candidates for v in f.verts};beforeverts=set(bm.verts);beforefaces={f:tuple(f.verts)for f in bm.faces};co=[0.,0.,0.];co[axis]=plane;n=[0.,0.,0.];n[axis]=1.
        bmesh.ops.bisect_plane(bm,geom=list(geometry),plane_co=co,plane_no=n,dist=1e-7,use_snap_center=False)
        for v in bm.verts:
            if v not in beforeverts:v[vid]=0
        for f in bm.faces:
            if f not in beforefaces or tuple(f.verts)!=beforefaces[f]:f[fid]=0
    dead=[f for f in bm.faces if not f[accessory]and f.calc_center_median().y>ycut+1e-7 and zlow-1e-7<f.calc_center_median().z<zhigh+1e-7];report['removed_bad_back_faces']=len(dead);bmesh.ops.delete(bm,geom=dead,context='FACES');bm.normal_update();loops=_ordered_boundary_loops(bm,[e for e in bm.edges if e.is_boundary]);report['body_cut_loops']=[{'vertices':len(loop),'bounds':r.bounds([v.co for v in loop]),'xz_crossings':len(r._fast_crossings(np.array([(v.co.x,v.co.z)for v in loop])))}for loop in loops];print('BODY_CUT',json.dumps(report),flush=True)
    class Flat:
        sign=1
        def point(self,u,v):return Vector((.28*.55,u,v))
        def normal(self,u,v):return Vector((1,0,0))
    made=[];patch=bm.faces.layers.int['SmoothTorsoPatchSide'];under=bm.faces.layers.int['SmoothTorsoUnderlap'];scale=.55
    for loop in loops:
        coords=np.array([(v.co.x,v.co.z)for v in loop]);r.require(not r._fast_crossings(coords),'Independent rear opening must be single valued in XZ')
        normals=[]
        for vertex in loop:
            values=[Vector(l[normal])for f in vertex.link_faces for l in f.loops if l.vert is vertex];n=sum(values,Vector((0,0,0))).normalized();normals.append(Vector((max(.12,n.y),n.x,n.z)).normalized())
        class BackProfile:
            def __init__(self):
                self.zlo=float(coords[:,1].min());self.zhi=float(coords[:,1].max());self.xyz=np.array([tuple(v.co)for v in loop]);self.ns=np.array([(n.y,n.x,n.z)for n in normals]);self.bottom=self.xyz[abs(self.xyz[:,2]-self.zlo)<1e-5];self.top=self.xyz[abs(self.xyz[:,2]-self.zhi)<1e-5]
            def height(self,x,z):
                zz=float(np.clip(z,self.zlo+1e-6,self.zhi-1e-6));hits=[]
                for i,(a,b)in enumerate(zip(self.xyz,np.roll(self.xyz,-1,axis=0))):
                    if(a[2]<=zz<b[2])or(b[2]<=zz<a[2]):
                        t=(zz-a[2])/(b[2]-a[2]);p=a*(1-t)+b*t;n=self.ns[i]*(1-t)+self.ns[(i+1)%len(self.ns)]*t;hits.append((p,n))
                hits.sort(key=lambda item:item[0][0]);left,nl=hits[0];right,nr=hits[-1];width=max(right[0]-left[0],1e-5);t=(x-left[0])/width;sl=max(.15,min(1.5,-nl[0]/max(nl[1],.12)));sr=-max(.15,min(1.5,nr[0]/max(nr[1],.12)))
                secant=(right[1]-left[1])/width;sl=max(.05,-nl[0]/max(nl[1],.08)-secant);rr=max(.05,nr[0]/max(nr[1],.08)+secant);q=t*(1-t);amplitude=width*(sl*(1-t)+rr*t);peak=min(.22,max(.08,width*.27));bend=max(0,width*(sl+rr)*.5/peak-4);y=left[1]*(1-t)+right[1]*t+q*amplitude/(1+bend*q)
                for rows,distance in((self.bottom,z-self.zlo),(self.top,self.zhi-z)):
                    if len(rows)<3:continue
                    row=rows[np.argsort(rows[:,0])];xx=row[0,0]+t*(row[-1,0]-row[0,0]);boundary=float(np.interp(xx,row[:,0],row[:,1]));weight=float(np.clip(distance/.10,0,1));weight=weight**3*(10-15*weight+6*weight*weight);y=boundary*(1-weight)+y*weight
                return y
            def evaluate(self,u,v):return((self.height(u/scale,v/scale)-.28)*scale,0.,0.)
            def normal(self,u,v):
                x=u/scale;z=v/scale;h=.0001;dy=(self.height(x+h,z)-self.height(x-h,z))/(2*h);dz=(self.height(x,z+h)-self.height(x,z-h))/(2*h);return Vector((1,-dy,-dz)).normalized()
        field=BackProfile()
        points=list(coords);mapping=list(loop);low=coords.min(0);high=coords.max(0)
        for x in np.arange(low[0]+.004,high[0],.010):
            for z in np.arange(low[1]+.004,high[1],.010):
                p=np.array((x,z))
                if r._inside(p,coords)and np.min(np.linalg.norm(coords-p,axis=1))>.004:points.append(p);mapping.append(None)
        outer=list(range(len(loop)))
        if sum(a[0]*b[1]-b[0]*a[1]for a,b in zip(coords,np.roll(coords,-1,axis=0)))<0:outer.reverse()
        pts,_,triangles,orig,_,_=delaunay_2d_cdt([Vector(tuple(p))for p in points],[(i,(i+1)%len(loop))for i in range(len(loop))],[outer],1,1e-8,True);vertices=[]
        for p,ids in zip(pts,orig):
            r.require(len(ids)==1,'Rear cap unexpectedly crossed its boundary');i=ids[0];v=mapping[i]
            if v is None:
                h=.28+field.evaluate(float(p.x)*scale,float(p.y)*scale)[0]/scale;v=bm.verts.new((p.x,h,p.y));v[vid]=0;v[deform][body.vertex_groups['body'].index]=1.
                for layer in bm.verts.layers.shape.values():v[layer]=v.co
            vertices.append(v)
        expanded=[]
        def split_collinear(tri):
            for j in range(3):
                a,b=tri[j],tri[(j+1)%3];other=tri[(j+2)%3];va,vb=vertices[a],vertices[b]
                if va not in loop or vb not in loop:continue
                ia,ib=loop.index(va),loop.index(vb);pa=np.array((va.co.x,va.co.z));pb=np.array((vb.co.x,vb.co.z));d=pb-pa;length=float(d@d)
                if length<1e-16:continue
                for direction in(1,-1):
                    chain=[];k=(ia+direction)%len(loop)
                    while k!=ib:chain.append(loop[k]);k=(k+direction)%len(loop)
                    if not chain:continue
                    ts=[float((np.array((v.co.x,v.co.z))-pa)@d/length)for v in chain]
                    if not all(1e-7<t<1-1e-7 and np.linalg.norm((np.array((v.co.x,v.co.z))-pa)-t*d)<1e-7 for v,t in zip(chain,ts)):continue
                    chain=sorted(chain,key=lambda v:float((np.array((v.co.x,v.co.z))-pa)@d));indices=[a]+[vertices.index(v)for v in chain]+[b]
                    for p,q in zip(indices,indices[1:]):split_collinear([p,q,other])
                    return
            expanded.append(tri)
        for tri in triangles:split_collinear(list(tri))
        faces=[]
        for tri in expanded:
            f=bm.faces.new([vertices[i]for i in tri]);f[fid]=0;f[patch]=-1 if f.calc_center_median().x<0 else 1;f[under]=1;f.smooth=True;faces.append(f)
        edge=bm.edges.get((loop[0],loop[1]));old=next(f for f in edge.link_faces if f not in faces);new=next(f for f in edge.link_faces if f in faces);a,b=loop[:2]
        if any(l.vert is a and l.link_loop_next.vert is b for l in old.loops)==any(l.vert is a and l.link_loop_next.vert is b for l in new.loops):
            for f in faces:f.normal_flip()
        bm.normal_update()
        for f in faces:
            for l in f.loops:
                p=l.vert.co;n=field.normal(p.x*scale,p.z*scale);n=Vector((n.y,n.x,n.z));l[normal]=n if n.dot(f.normal)>0 else-n
        paint(body,bm,faces,uv,normal);made.extend(faces)
    for loop in _ordered_boundary_loops(bm,[e for e in bm.edges if e.is_boundary]):
        r.require(len(loop)<=6,'Unexpected large residual body opening');coords=np.array([(v.co.x,v.co.y)for v in loop]);order=list(range(len(loop)))
        if sum(a[0]*b[1]-b[0]*a[1]for a,b in zip(coords,np.roll(coords,-1,axis=0)))<0:order.reverse()
        pts,_,triangles,orig,_,_=delaunay_2d_cdt([Vector(tuple(p))for p in coords],[(i,(i+1)%len(loop))for i in range(len(loop))],[order],1,1e-9,True);mapping=[loop[ids[0]]for ids in orig];first=[]
        for tri in triangles:
            f=bm.faces.new([mapping[i]for i in tri]);f[fid]=0;f[patch]=1 if f.calc_center_median().x>0 else-1;f[under]=1;first.append(f);made.append(f)
        edge=bm.edges.get((loop[0],loop[1]));outside=next(f for f in edge.link_faces if f not in first);inside=next(f for f in edge.link_faces if f in first);a,b=loop[:2]
        if any(l.vert is a and l.link_loop_next.vert is b for l in outside.loops)==any(l.vert is a and l.link_loop_next.vert is b for l in inside.loops):
            for f in first:f.normal_flip()
        bm.normal_update();lo=coords.min(0);hi=coords.max(0);origin,_=r.atlas_patch('Lumen',source,float(hi[0]-lo[0])*math.sqrt(density),float(hi[1]-lo[1])*math.sqrt(density),(.7568628,.6039216,.2627451),[[(np.array((v.co.x,v.co.y))-lo)*math.sqrt(density)for v in f.verts]for f in first],True)
        for f in first:
            for l in f.loops:l[uv].uv=origin+Vector(tuple((np.array((l.vert.co.x,l.vert.co.y))-lo)*math.sqrt(density)));l[normal]=f.normal
    report['body_topology']=finish(body,bm,'IndependentKeptNormal');report['new_body_faces']=len(made);report['body_blink_keys']=[k.name for k in body.data.shape_keys.key_blocks]if body.data.shape_keys else[]
    report['back_shading_join']=r.smooth_lumen_back_shading(body)

    require(report['body_topology']['boundary'] == 0 and report['body_topology']['nonmanifold'] == 0, 'Independent torso is not closed')
    require(report['pack_topology']['boundary'] == 0 and report['pack_topology']['nonmanifold'] == 0, 'Independent radio is not closed')
    return pack, report

def render_candidate(character,body,rig,folder,quick=False):
    """Close geometry and true-texture evidence, with pending paw objects hidden."""
    sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
    from prepare_radio_rigs import render_setup,aim
    from export_prepared_rigs import reset_pose
    objects=[obj for obj in bpy.context.scene.objects if obj.type=='MESH' and 'mesh_role' in obj]
    reset_pose(rig,objects)
    for obj in list(bpy.context.scene.objects):
        if obj.type in('CAMERA','LIGHT'):bpy.data.objects.remove(obj,do_unlink=True)
    camera=render_setup();scene=bpy.context.scene
    scene.render.resolution_x=720 if quick else 1400;scene.render.resolution_y=720 if quick else 1400;scene.cycles.samples=6 if quick else 32
    scene.render.image_settings.file_format='PNG';scene.render.film_transparent=True
    clay=bpy.data.materials.new('Owned smooth torso clay review');clay.use_nodes=True
    principled=clay.node_tree.nodes.get('Principled BSDF');principled.inputs['Base Color'].default_value=(.56,.61,.65,1)
    principled.inputs['Roughness'].default_value=.58
    hidden={obj:obj.hide_render for obj in objects}
    for obj in objects:
        if str(obj.get('mesh_role','')).startswith('Arm.'):obj.hide_render=True
    views=[('side',Vector((4,0,.04))),('under',Vector((4,-2,-2)))] if quick else [('front',Vector((0,-4,.05))),('side',Vector((4,0,.04))),('back',Vector((0,4,.05))),('under',Vector((4,-2,-2)))]
    try:
        for mode in(('clay',) if quick else ('beauty','clay')):
            scene.view_layers[0].material_override=clay if mode=='clay' else None
            camera.data.ortho_scale=1.45
            for sign in((-1,1)if quick else(1,)):
                target=Vector((sign*.62,.19,.73));side='right'if sign>0 else'left'
                for view,direction in views:
                    offset=direction.copy();offset.x*=sign;camera.location=target+offset;aim(camera,target)
                    scene.render.filepath=str(folder/f'{"quick-" if quick else ""}torso-{side}-{view}-{mode}.png');bpy.ops.render.render(write_still=True)
        scene.view_layers[0].material_override=None
        if not quick:
            camera.data.ortho_scale=3.1;camera.location=(0,-6,1.2);aim(camera,(0,0,1.2))
            scene.render.filepath=str(folder/'torso-full-front-beauty.png');bpy.ops.render.render(write_still=True)
    finally:
        scene.view_layers[0].material_override=None
        for obj,value in hidden.items():obj.hide_render=value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--character', choices=('Nori', 'Tiko', 'Lumen'), default='Nori')
    parser.add_argument('--analyze-only', action='store_true')
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--quick', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    character = args.character
    baseline = BASELINE / character / f'{character}_Rigged.blend'
    if not baseline.is_file():
        baseline = PROCESSED / character / f'{character}_Rigged.blend'
    bpy.ops.wm.open_mainfile(filepath=str(baseline))
    body = next(obj for obj in bpy.context.scene.objects if obj.type == 'MESH' and obj.get('mesh_role') == 'Character')
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == 'ARMATURE')
    source = original_body(character)
    # Immutable appended originals keep portable texture paths relative to the
    # old project. Resolve them to the unchanged maps of the character package.
    for image in bpy.data.images:
        if image.source == 'FILE':
            role=next((name for name in('BaseColor','MetallicSmoothness','Normal') if name.lower() in image.name.lower()),None)
            if role:image.filepath = str(PROCESSED / character / (role+'.png'))
    folder = REVIEW / character; folder.mkdir(parents=True, exist_ok=True)
    if args.analyze_only:
        report = inspect_source(character, body, source)
        (folder / 'source-inspection.json').write_text(json.dumps(report, indent=2) + '\n')
        print('SMOOTH_TORSO_INSPECTION', json.dumps(report), flush=True)
    else:
        report = repair_body_harness(character, body, rig, source)
        source_mesh=source.data;bpy.data.objects.remove(source,do_unlink=True)
        if source_mesh.users==0:bpy.data.meshes.remove(source_mesh)
        (folder / 'torso-checks.json').write_text(json.dumps(report, indent=2) + '\n')
        bpy.context.preferences.filepaths.save_version = 0
        bpy.ops.wm.save_as_mainfile(filepath=str(folder / 'torso-review.blend'), relative_remap=False)
        if args.render or args.quick:render_candidate(character,body,rig,folder,args.quick)


if __name__ == '__main__':
    main()
