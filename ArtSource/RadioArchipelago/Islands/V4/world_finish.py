"""Real water relief and floating-world finishing meshes; never repaint views."""
import bpy,math,random,pathlib
from mathutils import Vector

def mat(name,color,metal=0,rough=.6):
    m=bpy.data.materials.get('RA4_Finish_'+name)
    if m:return m
    m=bpy.data.materials.new('RA4_Finish_'+name);m.use_nodes=True
    linear=lambda v:v/12.92 if v<=.04045 else((v+.055)/1.055)**2.4
    rgb=tuple(int(color[i:i+2],16)/255 for i in(1,3,5))
    bs=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    bs.inputs['Base Color'].default_value=(*(linear(v)for v in rgb),1);bs.inputs['Metallic'].default_value=metal;bs.inputs['Roughness'].default_value=rough
    m.diffuse_color=(*rgb,1);m['ra_color_space']='linear'
    return m
def mesh(name,verts,faces,material,col,role='terrain',smooth=False):
    me=bpy.data.meshes.new(name);me.from_pydata(verts,[],faces);me.update()
    ob=bpy.data.objects.new(name,me);col.objects.link(ob);me.materials.append(material);ob['v3_role']=role
    if smooth:
        for p in me.polygons:p.use_smooth=True
    return ob
def tube(name,points,radius,material,col,role='terrain',sides=7):
    points=[Vector(p)for p in points];vs=[];fs=[]
    for i,p in enumerate(points):
        d=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized();u=d.cross(Vector((0,0,1)))
        if u.length<.1:u=d.cross(Vector((1,0,0)))
        u.normalize();v=d.cross(u).normalized()
        for j in range(sides):vs.append(tuple(p+radius*(u*math.cos(j*math.tau/sides)+v*math.sin(j*math.tau/sides))))
    fs.append(tuple(reversed(range(sides))))
    for i in range(len(points)-1):
        for j in range(sides):a=i*sides+j;b=i*sides+(j+1)%sides;fs.append((a,b,b+sides,a+sides))
    fs.append(tuple((len(points)-1)*sides+j for j in range(sides)))
    return mesh(name,vs,fs,material,col,role,True)
def blob(name,center,scale,material,col,seed):
    rng=random.Random(seed);vs=[(center[0],center[1],center[2]+scale[2])];fs=[];n=20;rings=12
    for r in range(1,rings):
        t=math.pi*r/rings
        for j in range(n):
            a=math.tau*j/n;f=1+.035*math.sin(a*4+t*3+seed)
            vs.append((center[0]+scale[0]*math.sin(t)*math.cos(a)*f,center[1]+scale[1]*math.sin(t)*math.sin(a)*f,center[2]+scale[2]*math.cos(t)))
    bottom=len(vs);vs.append((center[0],center[1],center[2]-scale[2]))
    for j in range(n):fs.append((0,1+j,1+(j+1)%n))
    for r in range(rings-2):
        for j in range(n):a=1+r*n+j;b=1+r*n+(j+1)%n;fs.append((a,a+n,b+n,b))
    last=1+(rings-2)*n
    for j in range(n):fs.append((last+j,bottom,last+(j+1)%n))
    return mesh(name,vs,fs,material,col,'terrain',True)
def add_details(idx,palette,land,col):
    rng=random.Random(8090+idx);created=[]
    foam=mat('seafoam','#CBE9D8',rough=.35)
    # Raised finite foam strokes stay below the primary bridge/road plane.
    for ob in list(col.objects):
        if ob.get('v3_role')!='water' or len(ob.data.vertices)<20:continue
        if ob.get('water_kind')=='river'or ob.get('ra_river_detail'):continue
        vs=[ob.matrix_world@v.co for v in ob.data.vertices];c=vs[0]
        rx=(max(v.x for v in vs)-min(v.x for v in vs))/2;ry=(max(v.y for v in vs)-min(v.y for v in vs))/2
        for j in range(34):
            r=math.sqrt(rng.random())*.72;q=c+(rng.choice(vs[1:])-c)*r
            x,y=q.x,q.y
            length=rng.uniform(.2,max(.21,min(1.1,min(rx,ry)*.16)));angle=rng.uniform(0,math.tau)
            pts=[(x+math.cos(angle)*length*t,y+math.sin(angle)*length*t+.05*math.sin(t*math.pi),c.z+.055)for t in(-.5,-.25,0,.25,.5)]
            created.append(tube('Water surface current',pts,rng.uniform(.018,.045),foam,col,'water'))
    if idx==9:
        colors=[mat('cloud_pearl','#F6E3DB',rough=.9),mat('cloud_lilac','#D7C7E3',rough=.9),mat('cloud_light','#FFF1E6',rough=.9)]
        outline=land['outline'];stride=max(1,len(outline)//24)
        for j in range(0,len(outline),stride):
            x,y=outline[j][:2];z=rng.uniform(-20,-8)
            for k in range(5):
                scale=(rng.uniform(3,6.0),rng.uniform(3,5.5),rng.uniform(2,4.3))
                center=(x*rng.uniform(.94,1.04)+rng.uniform(-3,3),y*rng.uniform(.94,1.04)+rng.uniform(-3,3),z+rng.uniform(-4,2))
                created.append(blob('Pearl cloud bank',center,scale,colors[(j+k)%3],col,j*7+k))
        gold=mat('celestial_path_inlay','#D7B16B',metal=.7,rough=.29)
        route=[Vector(p)for p in land['route_points']];count=len(route)
        normals=[]
        for i,p in enumerate(route):
            tangent=(route[(i+1)%count]-route[(i-1)%count]).normalized();normals.append(Vector((-tangent.y,tangent.x,0)))
        for sign in(-1,1):
            vs=[];fs=[]
            for p,n in zip(route,normals):
                for side in(-1,1):
                    q=p+n*(sign*(land['road_width']/2-.13)+side*.055)
                    vs.append((q.x,q.y,.003))
            top=len(vs);vs.extend((x,y,-.006)for x,y,z in list(vs))
            for i in range(count):
                a=2*i;b=2*((i+1)%count)
                fs.extend(((a,a+1,b+1,b),(a+top,b+top,b+1+top,a+1+top),(a,b,b+top,a+top),(a+1,a+1+top,b+1+top,b+1)))
            created.append(mesh('Celestial flush gold border',vs,fs,gold,col,'obstacle'))
        for i in range(0,count,34):
            p=route[i];vs=[]
            for z in(-.005,.004):
                for j in range(8):
                    a=j*math.tau/8;r=.40 if j%2==0 else .095
                    vs.append((p.x+math.cos(a)*r,p.y+math.sin(a)*r,z))
            fs=[tuple(reversed(range(8))),tuple(range(8,16))]+[(j,(j+1)%8,(j+1)%8+8,j+8)for j in range(8)]
            created.append(mesh('Celestial flush four point star',vs,fs,gold,col,'obstacle'))
    # Broad humus/moss variation supplies a forest floor rather than a single
    # pastel lawn. Procedural color is fixed in the saved scene for every view.
    if idx in(0,3,5,7,9):
        for material in bpy.data.materials:
            if not material.name.startswith('RA3_Ground_land_')or not material.use_nodes:continue
            nt=material.node_tree;bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED');base=tuple(bs.inputs['Base Color'].default_value)
            tc=nt.nodes.new('ShaderNodeTexCoord');macro=nt.nodes.new('ShaderNodeTexNoise');macro.inputs['Scale'].default_value=.075;macro.inputs['Detail'].default_value=4
            nt.links.new(tc.outputs['Object'],macro.inputs['Vector'])
            ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.18;ramp.color_ramp.elements[1].position=.83
            ramp.color_ramp.elements[0].color=tuple(v*.30 for v in base[:3])+(1,)
            ramp.color_ramp.elements[1].color=tuple(min(1,v*1.18)for v in base[:3])+(1,)
            nt.links.new(macro.outputs['Fac'],ramp.inputs[0]);nt.links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
            micro=nt.nodes.new('ShaderNodeTexNoise');micro.inputs['Scale'].default_value=3.4;micro.inputs['Detail'].default_value=2
            bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.37;bump.inputs['Distance'].default_value=.09
            nt.links.new(tc.outputs['Object'],micro.inputs['Vector']);nt.links.new(micro.outputs['Fac'],bump.inputs['Height']);nt.links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    texture_file=pathlib.Path(__file__).resolve().parent/'Textures/rock_albedo.png'
    if texture_file.exists():
        texture=bpy.data.images.load(str(texture_file),check_existing=True);texture.pack()
        for material in list(bpy.data.materials):
            name=material.name.lower()
            rock_material=('ground_'in name and any(k in name for k in('cliff','fracture','shear','rock_')))
            rock_material=rock_material or('ra4_nature_rock_'in name)or'technology foundations'in name
            if not rock_material or not material.use_nodes:continue
            nt=material.node_tree;bs=next(n for n in nt.nodes if n.type=='BSDF_PRINCIPLED')
            previous=bs.inputs['Base Color'].links[0].from_socket if bs.inputs['Base Color'].is_linked else None
            coord=nt.nodes.new('ShaderNodeTexCoord');scale=nt.nodes.new('ShaderNodeVectorMath');scale.operation='SCALE';scale.inputs['Scale'].default_value=.11
            tex=nt.nodes.new('ShaderNodeTexImage');tex.image=texture
            assert'BOX'in[x.identifier for x in tex.bl_rna.properties['projection'].enum_items]
            tex.projection='BOX';tex.projection_blend=.3
            nt.links.new(coord.outputs['Object'],scale.inputs[0]);nt.links.new(scale.outputs[0],tex.inputs['Vector'])
            gray=nt.nodes.new('ShaderNodeRGBToBW');nt.links.new(tex.outputs['Color'],gray.inputs[0])
            ramp=nt.nodes.new('ShaderNodeValToRGB');ramp.color_ramp.elements[0].position=.035;ramp.color_ramp.elements[0].color=(.36,.36,.36,1)
            ramp.color_ramp.elements[1].position=.52;ramp.color_ramp.elements[1].color=(1.28,1.28,1.28,1)
            nt.links.new(gray.outputs[0],ramp.inputs[0]);mix=nt.nodes.new('ShaderNodeMixRGB');mix.blend_type='MULTIPLY';mix.inputs[0].default_value=.55 if idx==9 else .8
            mix.inputs[1].default_value=bs.inputs['Base Color'].default_value
            if previous:nt.links.new(previous,mix.inputs[1])
            nt.links.new(ramp.outputs['Color'],mix.inputs[2]);nt.links.new(mix.outputs[0],bs.inputs['Base Color'])
            prior_normal=bs.inputs['Normal'].links[0].from_socket if bs.inputs['Normal'].is_linked else None
            bump=nt.nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.22;bump.inputs['Distance'].default_value=.045
            if prior_normal:nt.links.new(prior_normal,bump.inputs['Normal'])
            nt.links.new(gray.outputs[0],bump.inputs['Height']);nt.links.new(bump.outputs[0],bs.inputs['Normal'])
    return {'mesh_count':len(created),'all_geometry_real':True,'rock_texture':str(texture_file)}
