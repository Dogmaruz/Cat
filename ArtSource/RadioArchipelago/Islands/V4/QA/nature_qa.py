"""Isolated Blender CLI geometry checks and detail render for V4 nature."""
import sys,pathlib,json,math,collections
import bpy
from mathutils import Vector
ROOT=pathlib.Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT))
import scenery
for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
scene=bpy.context.scene
pal=dict(leaf='#3C762E',leaf_light='#87A643',rock='#657278',style='forest')
cases=[('broadleaf',lambda:scenery.make_tree((-4,2,0),kind='broadleaf',palette=pal,seed=12),2.15),
       ('pine',lambda:scenery.make_tree((0,2,0),kind='pine',palette=pal,seed=35),2.15),
       ('palm',lambda:scenery.make_tree((4,2,0),kind='palm',palette=pal,seed=83),2.95),
       ('slate_moss',lambda:scenery.make_rock((-4,-3,0),kind='slate',palette=pal,seed=7),1.95),
       ('groundcover',lambda:scenery.make_prop('groundcover',(0,-3,0),palette=pal,seed=13),1.4),
       ('ferns',lambda:scenery.make_prop('ferns',(4,-3,0),palette=pal,seed=11),1.4)]
stats=[]
for name,build,radius in cases:
    objects=build();verts=faces=tris=0;max_radius=0;max_z=-999;badedges=0
    for obj in objects:
        verts+=len(obj.data.vertices);faces+=len(obj.data.polygons)
        tris+=sum(len(p.vertices)-2 for p in obj.data.polygons)
        for v in obj.data.vertices:
            max_radius=max(max_radius,math.hypot(v.co.x,v.co.y));max_z=max(max_z,v.co.z)
        edges=collections.Counter(tuple(sorted((p.vertices[i],p.vertices[(i+1)%len(p.vertices)]))) for p in obj.data.polygons for i in range(len(p.vertices)))
        badedges+=sum(count!=2 for count in edges.values())
    assert max_radius<=radius+1e-5,(name,max_radius,radius)
    assert not badedges,(name,badedges)
    if name=='groundcover':assert max_z<=.80001
    stats.append(dict(kind=name,objects=len(objects),vertices=verts,faces=faces,triangles=tris,max_local_radius=max_radius,max_z=max_z,nonmanifold_edges=badedges))
# Full biome and seed variations prove bounded, deterministic primitive builders.
for kind in ('broadleaf','autumn','mauve','apple','bonsai','shrub','mauve_shrub','pine','winter','palm'):
    objs=scenery.make_tree((0,0,-100),palette=pal,kind=kind,seed=48)
    radius=2.95 if kind=='palm' else 2.15
    assert all(math.hypot(v.co.x,v.co.y)<=radius+1e-5 for o in objs for v in o.data.vertices),kind
    for o in objs:bpy.data.objects.remove(o,do_unlink=True)
for kind in ('boulder','slate','basalt','sandstone','glacier'):
    objs=scenery.make_rock((0,0,-100),palette=pal,kind=kind,seed=32)
    assert all(math.hypot(v.co.x,v.co.y)<=1.95001 for o in objs for v in o.data.vertices),kind
    for o in objs:bpy.data.objects.remove(o,do_unlink=True)
for mat in bpy.data.materials:
    if mat.name.startswith('RA4_Nature_'):assert mat.get('ra_color_space')=='linear'
ROOT.joinpath('QA').mkdir(exist_ok=True)
(ROOT/'QA/nature-detail-stats.json').write_text(json.dumps(stats,indent=2),encoding='utf8')
# Pale gallery plinth, warm key and neutral fill reveal actual leaf silhouettes.
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-.1));plane=bpy.context.object
mat=bpy.data.materials.new('QA_ground');mat.diffuse_color=(.31,.32,.28,1);mat.use_nodes=True
next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs['Base Color'].default_value=(.31,.32,.28,1)
plane.data.materials.append(mat)
scene.world.use_nodes=True
bg=next(n for n in scene.world.node_tree.nodes if n.type=='BACKGROUND');bg.inputs['Color'].default_value=(.75,.83,1,1);bg.inputs['Strength'].default_value=.35
for name,loc,energy,size in [('Key',(-7,-8,14),1800,8),('Fill',(8,4,12),1400,7)]:
    d=bpy.data.lights.new(name,'AREA');d.energy=energy;d.shape='DISK';d.size=size
    o=bpy.data.objects.new(name,d);scene.collection.objects.link(o);o.location=loc;o.rotation_euler=(Vector((0,0,2))-o.location).to_track_quat('-Z','Y').to_euler()
d=bpy.data.cameras.new('QA camera');d.type='ORTHO';d.ortho_scale=17
o=bpy.data.objects.new('QA camera',d);scene.collection.objects.link(o);o.location=(11,-18,15);o.rotation_euler=(Vector((0,0,2.2))-o.location).to_track_quat('-Z','Y').to_euler();scene.camera=o
scene.render.engine='CYCLES';scene.cycles.samples=48;scene.cycles.use_denoising=True
try:
    prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
    for device in prefs.devices:device.use=device.type=='CUDA'
    scene.cycles.device='GPU'
except Exception:pass
scene.render.resolution_x=1800;scene.render.resolution_y=1400;scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ROOT/'QA/nature-detail.png')
bpy.ops.render.render(write_still=True)
print('NATURE_DETAIL_STATS',json.dumps(stats),flush=True)
