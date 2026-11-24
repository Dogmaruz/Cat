"""Author an editable, compact radio-orb collectible; no external assets.

Run in a fresh background Blender process. The scene is wholly self-contained;
only the asset hierarchy is exported, never the studio used for its preview.
"""
import bpy
import bmesh
import math
import pathlib
import json
import sys
from mathutils import Vector

HERE = pathlib.Path(__file__).resolve().parent
ROOT = HERE.parents[3]
ASSET = ROOT / 'DancingCatsClone/Assets/RadioArchipelago/Imported/Collectibles/SignalSpark'
DOCS = ROOT / 'Docs/Design/RadioArchipelago/CollectibleConcepts20261028/SignalSparkModel'
ASSET.mkdir(parents=True, exist_ok=True)
DOCS.mkdir(parents=True, exist_ok=True)

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for m in list(bpy.data.materials):
    if m.users == 0:
        bpy.data.materials.remove(m)

def material(name, color, metal=0, rough=.28, emission=0, glass=0):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*color, 1)
    m.use_nodes=True
    p=m.node_tree.nodes.get('Principled BSDF')
    p.inputs['Base Color'].default_value=(*color,1)
    p.inputs['Metallic'].default_value=metal
    p.inputs['Roughness'].default_value=rough
    p.inputs['Coat Weight'].default_value=.35 if metal else .12
    p.inputs['Coat Roughness'].default_value=.12
    if glass:
        p.inputs['Transmission Weight'].default_value=glass
        p.inputs['IOR'].default_value=1.32
        p.inputs['Coat Weight'].default_value=.3
    if emission:
        p.inputs['Emission Color'].default_value=(*color,1)
        p.inputs['Emission Strength'].default_value=emission
    return m

gold=material('GoldBrass',(.66,.35,.085),.88,.22)
edgegold=material('GoldHighlight',(.86,.56,.19),.88,.19)
teal=material('TealEnamel',(.012,.10,.085),.42,.20)
glass=material('GlassMint',(.71,.96,.85),0,.055,glass=1)
mint=material('SignalMint',(.31,1,.72),0,.2,5)
coregold=material('CoreGold',(.95,.52,.075),.68,.18,.18)
mote=material('MoteMint',(.2,1,.62),.15,.16,2.3)

root=bpy.data.objects.new('SignalSpark',None)
bpy.context.collection.objects.link(root)
objects=[]
def parent(o,mat=None,owner=root):
    o.parent=owner
    if mat:
        o.data.materials.append(mat)
    objects.append(o)
    return o

def mesh(name,verts,faces,mat,owner=root):
    data=bpy.data.meshes.new(name+'Mesh')
    data.from_pydata(verts,[],faces)
    data.update()
    o=bpy.data.objects.new(name,data)
    bpy.context.collection.objects.link(o)
    parent(o,mat,owner)
    for p in data.polygons:
        p.use_smooth=True
    return o

def sphere(name,position,r,mat,segments=32,rings=16,owner=root):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=segments,ring_count=rings,radius=r,location=position)
    o=bpy.context.object
    o.name=name
    for p in o.data.polygons:
        p.use_smooth=True
    return parent(o,mat,owner)

def tube(name,points,radius,mat,sides=8,closed=False,owner=root):
    n=len(points)
    verts=[]
    for i,p in enumerate(points):
        p=Vector(p)
        tangent=(Vector(points[(i+1)%n])-Vector(points[(i-1)%n])).normalized() if closed else \
            (Vector(points[min(i+1,n-1)])-Vector(points[max(0,i-1)])).normalized()
        ref=Vector((0,0,1)) if abs(tangent.z)<.92 else Vector((0,1,0))
        a=tangent.cross(ref).normalized()
        b=tangent.cross(a).normalized()
        for j in range(sides):
            u=j*math.tau/sides
            verts.append(tuple(p+radius*(a*math.cos(u)+b*math.sin(u))))
    faces=[]
    for i in range(n if closed else n-1):
        k=(i+1)%n
        for j in range(sides):
            faces.append((i*sides+j,i*sides+(j+1)%sides,k*sides+(j+1)%sides,k*sides+j))
    if not closed:
        faces.extend([tuple(reversed(range(sides))),tuple((n-1)*sides+j for j in range(sides))])
    return mesh(name,verts,faces,mat,owner)

def crescent(name,rotation,mat,radius=.414,width=.058,depth=.023,start=75,end=205):
    verts=[]
    profile=[(-1,.6),(-.8,1),(.8,1),(1,.6),(1,-.6),(.8,-1),(-.8,-1),(-1,-.6)]
    n=48
    for i in range(n+1):
        t=i/n
        a=math.radians(start+(end-start)*t)+rotation
        w=.0015+width*(math.sin(math.pi*t)**.9)
        d=depth*(.3+.7*math.sin(math.pi*t)**.35)
        for q,y in profile:
            rr=radius+q*w
            verts.append((rr*math.cos(a),y*d,rr*math.sin(a)))
    faces=[]
    for i in range(n):
        for j in range(8):
            faces.append((i*8+j,i*8+(j+1)%8,(i+1)*8+(j+1)%8,(i+1)*8+j))
    faces.extend([tuple(reversed(range(8))),tuple(n*8+j for j in range(8))])
    return mesh(name,verts,faces,mat)

def join_semantic(prefix,name):
    group=[o for o in bpy.data.objects if o.type=='MESH' and o.name.startswith(prefix)]
    bpy.ops.object.select_all(action='DESELECT')
    for o in group:
        o.select_set(True)
    bpy.context.view_layer.objects.active=group[0]
    bpy.ops.object.join()
    o=bpy.context.object
    o.name=name
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return o

# A closed solid shell, not a cutout/sprite: very thin optical glass volume.
shell=sphere('GlassShell',(0,0,0),.337,glass,48,24)
solid=shell.modifiers.new('Optical wall 1.2 mm','SOLIDIFY')
solid.thickness=.0012
solid.offset=-1
bpy.context.view_layer.objects.active=shell
bpy.ops.object.modifier_apply(modifier=solid.name)

# Opposing hand-finished brass crescents. Teal enamel follows the same sweep,
# with a narrow raised gold braid and tiny gold fasteners.
for c,rotation in enumerate((0,math.pi)):
    crescent('GoldCrescents_'+str(c),rotation,gold)
    crescent('TealInlay_'+str(c),rotation,teal,radius=.414,width=.024,depth=.0025,start=86,end=194).location.y=-.024
    for edge in (-1,1):
        points=[]
        for i in range(41):
            t=.08+.84*i/40
            a=math.radians(75+130*t)+rotation
            r=.414+edge*(.0015+.058*math.sin(math.pi*t)**.9)*.80
            points.append((r*math.cos(a),-.025,r*math.sin(a)))
        tube('GoldCrescents_Edge',points,.0032,edgegold,4)
    for t in (.16,.31,.69,.84):
        a=math.radians(75+130*t)+rotation
        r=.414+.038*math.sin(math.pi*t)
        sphere('GoldCrescents_Rivet',(r*math.cos(a),-.026,r*math.sin(a)),.0058,edgegold,12,6)
    # Two attachment jewels on each band, matching the concept's radio fittings.
    for t in (.27,.73):
        a=math.radians(75+130*t)+rotation
        center=Vector((.367*math.cos(a),-.005,.367*math.sin(a)))
        # A sculpted boss is a flattened spheroid, with luminous inset on front.
        boss=sphere('GoldCrescents_JewelSocket',center,.040,edgegold,20,10)
        boss.scale=(1,.45,1)
        gem=sphere('TealInlay_Jewel',center+Vector((0,-.019,0)),.025,mote,16,8)
        gem.scale=(1,.47,1)

join_semantic('GoldCrescents','GoldCrescents')
join_semantic('TealInlay','TealInlay')

# Luminous floating helix: physically present geometry around a warm nucleus.
core=sphere('SignalCore',(0,0,0),.095,coregold,32,16)
points=[]
for i in range(161):
    t=i/160
    a=math.tau*(2.15*t+.1)
    r=.155+.038*math.sin(math.pi*t)
    points.append((r*math.cos(a),r*math.sin(a),-.235+.47*t))
tube('SignalSpiral',points,.0075,mint,6)
for c in (-1,1):
    z=.251*c
    sphere('SignalSpiral_Tip',(0,.02,z),.012,mint,16,8)

# A crisp wave across the front of the golden nucleus reads as radio energy
# even when the collectible is a tiny object on the finish meadow.
wave=[]
for i in range(81):
    x=-.114+.228*i/80
    envelope=math.exp(-((x/.053)**2))
    z=.055*envelope*math.sin(x*132)
    wave.append((x,-.106,z))
tube('SignalSpiral_Wave',wave,.005,mint,6)
join_semantic('SignalSpiral','SignalSpiral')

orbit=bpy.data.objects.new('OrbitMotes',None)
bpy.context.collection.objects.link(orbit)
orbit.parent=root
# Wire-like orbits use a low-intensity emissive material, independently visible.
orbitmat=material('OrbitMint',(.12,.72,.45),.1,.25,1.2)
for k,tilt in enumerate((.35,1.1)):
    points=[]
    for i in range(96):
        a=math.tau*i/96
        x=.392*math.cos(a)
        z=.392*math.sin(a)*math.cos(tilt)
        y=.392*math.sin(a)*math.sin(tilt)
        points.append((x,y,z))
    tube('OrbitMotes_Ring_'+str(k),points,.0017,orbitmat,4,True,orbit)
for i,(a,r) in enumerate(((18,.017),(75,.020),(142,.011),(216,.019),(281,.014),(332,.009))):
    a=math.radians(a)
    tilt=.6 if i%2==0 else 1.05
    pos=(.392*math.cos(a),.392*math.sin(a)*math.sin(tilt),.392*math.sin(a)*math.cos(tilt))
    sphere('OrbitMotes_Pearl_'+str(i),pos,r,mote,16,8,orbit)

join_semantic('OrbitMotes_Pearl','OrbitMotes_Pearls')
join_semantic('OrbitMotes_Ring','OrbitMotes_Rails')

# The glass deliberately has an outer and an inward-facing inner boundary.
# All other closed connected components need outward normals for Unity culling.
for o in root.children_recursive:
    if o.type != 'MESH' or o.name == 'GlassShell':
        continue
    bm=bmesh.new()
    bm.from_mesh(o.data)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bm.to_mesh(o.data)
    bm.free()

# Bake only geometry transforms, keeping all semantic animation nodes intact.
bpy.ops.object.select_all(action='DESELECT')
asset_meshes=[o for o in root.children_recursive if o.type=='MESH']
for o in asset_meshes:
    o.select_set(True)
bpy.context.view_layer.objects.active=asset_meshes[0]
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)

triangles=sum(sum(len(p.vertices)-2 for p in o.data.polygons) for o in asset_meshes)
bounds=[o.matrix_world@Vector(corner) for o in asset_meshes for corner in o.bound_box]
minimum=[min(p[i] for p in bounds) for i in range(3)]
maximum=[max(p[i] for p in bounds) for i in range(3)]
report=dict(triangles=triangles,objects=[dict(name=o.name,materials=[m.name for m in o.data.materials],vertices=len(o.data.vertices)) for o in asset_meshes],min=minimum,max=maximum,source=str(HERE/'signal_spark.blend'),fbx=str(ASSET/'signal_spark.fbx'))
(HERE/'model_report.json').write_text(json.dumps(report,indent=2),encoding='utf-8')

# Export only the reusable object hierarchy. Studio lights/cameras are not FBX.
bpy.ops.object.select_all(action='DESELECT')
root.select_set(True)
for o in root.children_recursive:
    o.select_set(True)
bpy.context.view_layer.objects.active=root
bpy.ops.export_scene.fbx(filepath=str(ASSET/'signal_spark.fbx'),use_selection=True,
    object_types={'MESH','EMPTY'},apply_unit_scale=True,apply_scale_options='FBX_SCALE_NONE',
    axis_forward='-Z',axis_up='Y',use_mesh_modifiers=True,mesh_smooth_type='FACE',
    add_leaf_bones=False,bake_anim=False,path_mode='AUTO')

# Actual-model proof: softly lit warm studio; cycles provides true glass.
scene=bpy.context.scene
scene.render.engine='CYCLES'
scene.cycles.samples=64
scene.cycles.use_denoising=True
scene.cycles.max_bounces=10
scene.cycles.transmission_bounces=8
scene.render.resolution_x=1400
scene.render.resolution_y=1400
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.view_settings.view_transform='AgX'
scene.world.color=(.15,.18,.17)
scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.63,.73,.69,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.4

def area(name,loc,power,size,color,aim=(0,0,0)):
    d=bpy.data.lights.new(name,'AREA')
    d.energy=power
    d.shape='DISK'
    d.size=size
    d.color=color
    o=bpy.data.objects.new(name,d)
    bpy.context.collection.objects.link(o)
    o.location=loc
    o.rotation_euler=(Vector(aim)-o.location).to_track_quat('-Z','Y').to_euler()

area('Large warm softbox',(-1,-1.4,1.9),160,1.15,(1,.87,.64))
area('Mint rim',(1,.5,1.2),170,.9,(.55,1,.82))
area('Ivory fill',(.5,-1.4,.45),55,1.4,(.82,.94,1))
area('Back rim',(-.7,.6,.15),100,.65,(1,.91,.71))
floor=material('Studio Ivory',(.70,.68,.60),0,.55)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.52))
bpy.context.object.name='Studio Floor - not exported'
bpy.context.object.data.materials.append(floor)
# Slightly brighter pool beneath the suspended collectible.
d=bpy.data.lights.new('Soft local spark pool','POINT')
d.energy=2.7
d.color=(.24,1,.62)
d.shadow_soft_size=.3
o=bpy.data.objects.new(d.name,d)
bpy.context.collection.objects.link(o)
o.location=(0,0,-.32)

camdata=bpy.data.cameras.new('Product Camera')
cam=bpy.data.objects.new('Product Camera',camdata)
bpy.context.collection.objects.link(cam)
cam.location=(.47,-2.1,.38)
aim=Vector((0,0,-.01))
cam.rotation_euler=(aim-cam.location).to_track_quat('-Z','Y').to_euler()
camdata.type='ORTHO'
camdata.ortho_scale=1.32
camdata.lens=55
scene.camera=cam

# Blender 5's compositor is a standalone node group, avoiding a heavyweight
# scene-wide real-time post-process requirement in the Unity asset itself.
try:
    group=bpy.data.node_groups.new('Spark Proof Glare','CompositorNodeTree')
    group.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
    layer=group.nodes.new('CompositorNodeRLayers')
    glare=group.nodes.new('CompositorNodeGlare')
    glare.inputs['Type'].default_value='Fog Glow'
    glare.inputs['Quality'].default_value='High'
    glare.inputs['Threshold'].default_value=2.0
    glare.inputs['Strength'].default_value=.18
    output=group.nodes.new('NodeGroupOutput')
    group.links.new(layer.outputs['Image'],glare.inputs['Image'])
    group.links.new(glare.outputs['Image'],output.inputs['Image'])
    scene.compositing_node_group=group
except Exception as e:
    print('Optional proof glare unavailable:',e)

# Save an editable file including the clearly named studio, plus portable FBX.
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'signal_spark.blend'))
scene.render.filepath=str(DOCS/'signal-spark-model-beauty.png')
if '--skip-render' not in sys.argv:
    bpy.ops.render.render(write_still=True)
print('SIGNAL_SPARK_MODEL_READY',json.dumps(report))
