import bpy,json,pathlib,collections
p=pathlib.Path('C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/Islands')
bpy.ops.wm.open_mainfile(filepath=str(p/'V4/Output/01_emerald_ravine/island.blend'))
r=[]
for o in bpy.context.scene.objects:
 if o.type=='MESH':
  r.append(dict(name=o.name,mesh=o.data.name,parent=o.parent.name if o.parent else None,location=list(o.matrix_world.translation),props=dict(o.items()),verts=len(o.data.vertices)))
(p/'ReliefPilot/source_inventory.json').write_text(json.dumps(r,indent=2))
print('INVENTORY_READY',len(r),flush=True)
