def prepare_cap_index():
    global CAP_BVH
    vertices=[];faces=[]
    for obj in bpy.context.scene.objects:
        if obj.type!='MESH'or obj.get('moon_bucket')!='Terrain':continue
        offset=len(vertices);vertices.extend(obj.matrix_world@v.co for v in obj.data.vertices)
        faces.extend(tuple(offset+i for i in poly.vertices)for poly in obj.data.polygons)
    CAP_BVH=BVHTree.FromPolygons(vertices,faces,all_triangles=True)

def actual_ground(x,y):
    if CAP_BVH is None:raise RuntimeError('Actual terrain index must precede decoration.')
    point,normal,face,distance=CAP_BVH.ray_cast(Vector((x,y,1000)),Vector((0,0,-1)),2000)
    return point.z if point is not None else None

def track_decor(objects,x,y,label,placed_ground=None):
    GROUND_DECOR.append((list(objects),x,y,label,terrain_height(x,y)if placed_ground is None else placed_ground))

def audit_ground_decor():
    report=dict(checked_groups=0,removed_groups=0,removed_objects=0,relocated_groups=0,max_vertical_correction=0,removed=[],actual_top_method='BVH over actual triangulated Terrain caps, never nominal footprint/height')
    for objects,x,y,label,nominal in GROUND_DECOR:
        report['checked_groups']+=1;height=actual_ground(x,y)
        if height is None:
            report['removed_groups']+=1;report['removed'].append(dict(kind=label,x=x,y=y,reason='No actual Terrain top beneath root'))
            for obj in objects:
                if obj.name in bpy.data.objects:bpy.data.objects.remove(obj,do_unlink=True);report['removed_objects']+=1
            continue
        delta=height-nominal;report['max_vertical_correction']=max(report['max_vertical_correction'],abs(delta))
        if abs(delta)>.001:
            report['relocated_groups']+=1
            for obj in objects:
                if obj.name in bpy.data.objects:obj.location.z+=delta
    return report

def material(category):
    if category not in OWN_MATERIALS:
        m=bpy.data.materials.new('RA_Moon_'+category);m.use_nodes=True;m.diffuse_color=(1,1,1,1)
        bs=ex.node_bsdf(m);bs.inputs['Roughness'].default_value=.25 if category in('Water','Waterfall','Foam')else .36 if category=='Metal'else .78
        bs.inputs['Metallic'].default_value=.65 if category=='Metal'else 0
        attr=m.node_tree.nodes.new('ShaderNodeVertexColor');attr.layer_name='Color';m.node_tree.links.new(attr.outputs['Color'],bs.inputs['Base Color'])
        OWN_MATERIALS[category]=m
    return OWN_MATERIALS[category]

def mesh(name,verts,faces,category,colors,uvs=None,soft=False):
    data=bpy.data.meshes.new(name);data.from_pydata(verts,[],faces);data.update();data.materials.append(material(category))
    rgba=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER')
    if len(colors)==4 and isinstance(colors[0],(int,float)):colors=[colors]*len(verts)
    rgba.data.foreach_set('color',[v for loop in data.loops for v in colors[loop.vertex_index]])
    uv=data.uv_layers.new(name='UVMap')
    uv.data.foreach_set('uv',[v for loop in data.loops for v in(uvs[loop.vertex_index]if uvs else(verts[loop.vertex_index][0],verts[loop.vertex_index][1]))])
    for poly in data.polygons:poly.use_smooth=soft
    obj=bpy.data.objects.new(name,data);bpy.context.scene.collection.objects.link(obj);obj['moon_bucket']=category
    return obj

def landmass(name,center,radii,depth,seed):
    rng=random.Random(seed);cx,cy=center;rx,ry=radii;n=128;nr=24
    def rim(a):return 1+.045*math.sin(a*5+seed)+.032*math.cos(a*9+seed*.9)+.018*math.sin(a*17+seed*.3)
    vs=[(cx,cy,terrain_height(cx,cy))];colors=[col((.89,.65,.41))];fs=[]
    for ring in range(1,nr+1):
        t=ring/nr
        for i in range(n):
            a=math.tau*i/n;f=rim(a);x=cx+math.cos(a)*rx*t*f;y=cy+math.sin(a)*ry*t*f;z=terrain_height(x,y)
            vs.append((x,y,z));variation=.5+.28*math.sin(x*.12+y*.13)+.17*math.sin(x*.77-y*.55)
            rgb=tuple(a*(1-variation)+b*variation for a,b in zip((.79,.51,.31),(.96,.76,.49)))
            pond_distance=((x-29)/29)**2+((y+44)/17)**2
            if .54<pond_distance<1.12:
                wet=.60*(1-abs(pond_distance-.82)/.33)
                wet=clamp(wet);rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.28,.43,.31)))
            spring_distance=((x-67)/7.8)**2+((y+7)/6.0)**2
            if .55<spring_distance<1.6:
                wet=.64*clamp(1-abs(spring_distance-.98)/.62)
                rgb=tuple(a*(1-wet)+b*wet for a,b in zip(rgb,(.31,.43,.28)))
            colors.append(col(rgb))
    for i in range(n):fs.append((0,1+i,1+(i+1)%n))
    for ring in range(nr-1):
        a0=1+ring*n;b0=a0+n
        for i in range(n):j=(i+1)%n;fs.extend(((a0+i,b0+i,b0+j),(a0+i,b0+j,a0+j)))
    top=mesh('Moon_Terrain_'+name,vs,fs,'Terrain',colors,soft=True)
    # Closed stratified rock volume shares its top boundary with the terrain cap.
    verts=[];cc=[];faces=[]
    levels=[(0,1),(-.75,1.015),(-1.6,.99),(-2.1,1.025),(-4.4,1.01),(-5.0,.965),(-7.4,.97),(-8.2,.99),(-11.9,.89),(-12.5,.915),(-16.7,.82),(-17.5,.84),(-22.5,.72),(-23.2,.74),(-29,.58),(-30,.60),(-depth*.82,.37),(-depth,.055)]
    for ring,(drop,scale) in enumerate(levels):
        for i in range(n):
            a=math.tau*i/n;f=rim(a)*(1+.015*math.sin(i*1.9+ring*2));x=cx+math.cos(a)*rx*f*scale+.8*math.sin(ring*.8);y=cy+math.sin(a)*ry*f*scale+.65*math.cos(ring*.9)
            z=terrain_height(cx+math.cos(a)*rx*rim(a),cy+math.sin(a)*ry*rim(a))+drop
            if ring>1:z+=.28*math.sin(i*.65+ring*.4)+.20*math.sin(i*1.9+ring*.7)
            if ring==0:x=cx+math.cos(a)*rx*rim(a);y=cy+math.sin(a)*ry*rim(a)
            verts.append((x,y,z));rgb=ROCKS[(ring//2+i//9+seed)%len(ROCKS)];shade=.92+.08*math.sin(i*2.07+ring)
            cc.append(col(tuple(c*shade for c in rgb)))
    for ring in range(len(levels)-1):
        for i in range(n):j=(i+1)%n;a=ring*n+i;b=ring*n+j;c=(ring+1)*n+j;d=(ring+1)*n+i;faces.extend(((a,d,c),(a,c,b)))
    # The exactly matching Terrain cap above closes this border. A single
    # nonplanar n-gon here would bridge over its sculpted basin and bury props.
    tip=len(verts);verts.append((cx+1.4,cy-.7,terrain_height(cx,cy)-depth-2.6));cc.append(col((.48,.25,.18)))
    for i in range(n):j=(i+1)%n;faces.append((tip,(len(levels)-1)*n+j,(len(levels)-1)*n+i))
    body=mesh('Moon_Cliffs_'+name,verts,faces,'Cliffs',cc,soft=False)
    return top,body

def tube(name,points,radius,category,rgb,n=8):
    points=[Vector(p)for p in points];vs=[];fs=[]
    for i,p in enumerate(points):
        direction=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();side=direction.cross(Vector((0,0,1)))
        if side.length<.001:side=Vector((1,0,0))
        side.normalize();up=direction.cross(side).normalized()
        for j in range(n):vs.append(tuple(p+radius*(side*math.cos(j*math.tau/n)+up*math.sin(j*math.tau/n))))
    fs.append(tuple(reversed(range(n))))
    for i in range(len(points)-1):
        for j in range(n):k=(j+1)%n;fs.append((i*n+j,i*n+k,(i+1)*n+k,(i+1)*n+j))
    fs.append(tuple((len(points)-1)*n+j for j in range(n)))
    return mesh(name,vs,fs,category,col(rgb),soft=True)

class ExportBatch:
    def __init__(self,name,category):self.name=name;self.category=category;self.v=[];self.f=[];self.c=[];self.uv=[];self.soft=[]
    def flush(self):
        if not self.f:return None
        data=bpy.data.meshes.new(self.name);data.from_pydata(self.v,[],self.f);data.update();data.materials.append(material(self.category))
        colors=data.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');colors.data.foreach_set('color',[v for c in self.c for v in c])
        uv=data.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',[v for p in self.uv for v in p])
        for poly,soft in zip(data.polygons,self.soft):poly.use_smooth=soft
        ob=bpy.data.objects.new(self.name,data);bpy.context.scene.collection.objects.link(ob);ob['moon_bucket']=self.category
        self.v=[];self.f=[];self.c=[];self.uv=[];self.soft=[]
        return ob

def export_scene(originals):
    root=bpy.data.objects.new('MoonResonanceIsland',None);bpy.context.scene.collection.objects.link(root)
    batches={};serial=collections.Counter();outputs=[];counts=collections.Counter()
    deps=bpy.context.evaluated_depsgraph_get();deps.update()
    for number,obj in enumerate(originals):
        if obj.type!='MESH':continue
        data=bpy.data.meshes.new_from_object(obj.evaluated_get(deps),preserve_all_data_layers=True,depsgraph=deps)if obj.modifiers else obj.data
        # Orient complete closed SOURCE solids before splitting them into spatial
        # batches. Recalculating open batch fragments can invert arbitrary cells.
        source_bm=bmesh.new();source_bm.from_mesh(data)
        if source_bm.faces and all(edge.is_manifold for edge in source_bm.edges):
            bmesh.ops.recalc_face_normals(source_bm,faces=list(source_bm.faces))
            source_bm.to_mesh(data);data.update()
        source_bm.free()
        world=[obj.matrix_world@v.co for v in data.vertices];uv=data.uv_layers.active.data if data.uv_layers.active else None;color=data.color_attributes.active_color
        remaps={}
        for poly in data.polygons:
            mat=data.materials[poly.material_index]if poly.material_index<len(data.materials)else None
            category=obj.get('moon_bucket')or ex.bucket(obj,mat)
            if category=='Road':continue
            if category not in CATEGORIES:category='Architecture'
            rgba=ex.color_info(mat)[0]
            center=obj.matrix_world@poly.center;key=(category,math.floor(center.x/48),math.floor(center.y/48))
            if key not in batches:serial[key]+=1;batches[key]=ExportBatch(f'RA_Moon_{category}_{key[1]:+03}_{key[2]:+03}_{serial[key]:02}',category)
            batch=batches[key]
            if len(batch.v)+len(poly.vertices)>54000:
                out=batch.flush();out.parent=root;outputs.append(out);serial[key]+=1;batches[key]=batch=ExportBatch(f'RA_Moon_{category}_{key[1]:+03}_{key[2]:+03}_{serial[key]:02}',category);remaps.pop(key,None)
            remap=remaps.setdefault(key,{})
            face=[]
            corners=list(zip(poly.loop_indices,poly.vertices))
            if len(corners)>=3:
                a,b,c=[world[vid]for _,vid in corners[:3]]
                reverse=(b-a).cross(c-a).z<0 if category in('Terrain','Water')else obj.matrix_world.to_3x3().determinant()<0
                if reverse:corners.reverse()
            for loop,vid in corners:
                if vid not in remap:remap[vid]=len(batch.v);batch.v.append(tuple(world[vid]))
                face.append(remap[vid]);batch.c.append(tuple(color.data[loop].color)if color and color.domain=='CORNER'else tuple(color.data[vid].color)if color else tuple(rgba[:3])+(1,));batch.uv.append(tuple(uv[loop].uv)if uv else(world[vid].x,world[vid].y))
            batch.f.append(tuple(face));batch.soft.append(poly.use_smooth);counts[category]+=len(poly.vertices)-2
        if number%250==0:print('MOON_BATCH',number,len(originals),dict(counts),flush=True)
    for batch in batches.values():
        out=batch.flush()
        if out:out.parent=root;outputs.append(out)
    for name,p in [('RA_Origin',(0,0,0)),('RA_X10',(10,0,0)),('RA_North10',(0,10,0))]:
        ob=bpy.data.objects.new(name,None);bpy.context.scene.collection.objects.link(ob);ob.parent=root;ob.location=p;outputs.append(ob)
    for obj in originals:
        if obj.name in bpy.data.objects:bpy.data.objects.remove(obj,do_unlink=True)
    for obj in outputs:
        if obj.type!='MESH':continue
        bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.triangulate(bm,faces=list(bm.faces));bad=[f for f in bm.faces if f.calc_area()<1e-10]
        if bad:bmesh.ops.delete(bm,geom=bad,context='FACES')
        # Keep the authored outward/upward index ordering after spatial splitting.
        # BMesh normal_update computes shading normals without flipping faces.
        bm.normal_update();bm.to_mesh(obj.data);bm.free();obj.data.update()
    bpy.ops.object.select_all(action='DESELECT');root.select_set(True)
    for obj in outputs:obj.select_set(True)
    path=OUT/'moon_resonance_island.fbx'
    bpy.ops.export_scene.fbx(filepath=str(path),use_selection=True,object_types={'MESH','EMPTY'},axis_forward='-Z',axis_up='Y',global_scale=1,apply_unit_scale=True,apply_scale_options='FBX_SCALE_UNITS',bake_space_transform=True,use_mesh_modifiers=False,use_triangles=True,mesh_smooth_type='OFF',bake_anim=False,add_leaf_bones=False,use_custom_props=True,path_mode='STRIP',colors_type='LINEAR')
    return dict(mesh_batches=len(outputs)-3,triangles=sum(len(o.data.polygons)for o in outputs if o.type=='MESH'),material_triangles=dict(counts),bytes=path.stat().st_size,sha256=sha(path))
