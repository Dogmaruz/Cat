"""Render and inspect the existing source without touching its frozen FBX."""
import bpy
import bmesh
import json
import pathlib
import hashlib
from mathutils import Vector

HERE=pathlib.Path(__file__).resolve().parent
ROOT=HERE.parents[3]
DOCS=ROOT/'Docs/Design/RadioArchipelago/CollectibleConcepts20261028/SignalSparkModel'
FBX=ROOT/'DancingCatsClone/Assets/RadioArchipelago/Imported/Collectibles/SignalSpark/signal_spark.fbx'
before=hashlib.sha256(FBX.read_bytes()).hexdigest()
bpy.ops.wm.open_mainfile(filepath=str(HERE/'signal_spark.blend'))
root=bpy.data.objects['SignalSpark']
results=[]
for o in root.children_recursive:
    if o.type!='MESH':
        continue
    bm=bmesh.new()
    bm.from_mesh(o.data)
    degenerate=sum(f.calc_area()<1e-12 for f in bm.faces)
    boundary=sum(e.is_boundary for e in bm.edges)
    nonmanifold=sum(not e.is_manifold for e in bm.edges)
    signed_volume=bm.calc_volume(signed=True)
    assert degenerate==0,(o.name,'degenerate',degenerate)
    assert boundary==0,(o.name,'boundary',boundary)
    assert nonmanifold==0,(o.name,'nonmanifold',nonmanifold)
    assert signed_volume>0,(o.name,'inward',signed_volume)
    results.append(dict(name=o.name,boundary=boundary,nonmanifold=nonmanifold,degenerate=degenerate,signed_volume=signed_volume))
    bm.free()

scene=bpy.context.scene
scene.view_settings.exposure=-.25
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.3
group=bpy.data.node_groups.new('Signal Spark Gentle Glow','CompositorNodeTree')
group.interface.new_socket(name='Image',in_out='OUTPUT',socket_type='NodeSocketColor')
layer=group.nodes.new('CompositorNodeRLayers')
glare=group.nodes.new('CompositorNodeGlare')
glare.inputs['Type'].default_value='Fog Glow'
glare.inputs['Quality'].default_value='High'
glare.inputs['Threshold'].default_value=2.5
glare.inputs['Strength'].default_value=.15
glare.inputs['Size'].default_value=.3
output=group.nodes.new('NodeGroupOutput')
group.links.new(layer.outputs['Image'],glare.inputs['Image'])
group.links.new(glare.outputs['Image'],output.inputs['Image'])
scene.compositing_node_group=group
scene.render.filepath=str(DOCS/'signal-spark-model-beauty.png')
bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'signal_spark.blend'))
bpy.ops.render.render(write_still=True)
after=hashlib.sha256(FBX.read_bytes()).hexdigest()
assert before==after,'Frozen FBX changed during proof'
(DOCS/'model-geometry-checks.json').write_text(json.dumps(dict(success=True,mesh_count=len(results),fbx_sha256=after,checks=results),indent=2),encoding='utf-8')
print('SIGNAL_SPARK_GEOMETRY_PASS',len(results),after)
