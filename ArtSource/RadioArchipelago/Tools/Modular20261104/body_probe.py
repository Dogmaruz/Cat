import bpy
p='C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Characters/Boro/Boro_Rigged.blend'
bpy.ops.wm.open_mainfile(filepath=p)
o=next(o for o in bpy.data.objects if o.type=='MESH')
print([(k.name,k.value,len(k.data)) for k in o.data.shape_keys.key_blocks])
ks=o.data.shape_keys.key_blocks
print('diffs',sum((a.co-b.co).length>1e-5 for a,b in zip(ks[0].data,ks[1].data)))
print('meshmatch',sum((a.co-b.co).length>1e-5 for a,b in zip(o.data.vertices,ks[0].data)))
