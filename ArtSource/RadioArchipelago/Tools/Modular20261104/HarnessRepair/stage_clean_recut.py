from pathlib import Path
p=Path('ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/recut_miro.py')
s=p.read_text(encoding='utf-8-sig').replace("OUT=R/'ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/Staged'","OUT=R/'ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/StagedClean'")
s=s.replace("original=R/'ArtSource/RadioArchipelago/ProcessedModular/Backpacks'/name","original=R/'ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/Before'/name")
s=s.replace("bm=bmesh.new();bm.from_mesh(case.data)","""bm=bmesh.new();bm.from_mesh(case.data)
 beforemeta=json.loads((original/'Meta.json').read_text());oldplane=beforemeta['contactPlaneY']-.025*beforemeta['rawToPreparedScale']
 obsolete=[f for f in bm.faces if all(abs(v.co.y-oldplane)<.00002 for v in f.verts)]
 bmesh.ops.delete(bm,geom=obsolete,context='FACES');bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00001)
 print('OBSOLETE_CAPS_REMOVED',name,len(obsolete),flush=True)""")
s=s.replace("meta['repairs']['sourceStrapReextraction20261104']=", "meta['caseBoundsMax'][2]=.83\n meta['repairs']['obsoleteCutCapsRemoved']=len(obsolete)\n meta['repairs']['sourceStrapReextraction20261104']=")
s=s.replace("name+'_case_reextracted.png'","name+'_case_reextracted_clean.png'")
Path('ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/recut_miro_clean.py').write_text(s,encoding='utf-8')
