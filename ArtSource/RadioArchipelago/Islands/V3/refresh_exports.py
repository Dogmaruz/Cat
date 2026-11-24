"""Re-export saved scenes with portable material colors, without rerendering."""
import bpy,sys,pathlib,json,hashlib
ROOT=pathlib.Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT))
import build_worlds
args=sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[]
chosen=args[0].split(',')if args else[]
report_file=ROOT/'export-materials.json'
previous=json.loads(report_file.read_text(encoding='utf8')).get('worlds',[])if report_file.exists()else[]
reports={item['id']:item for item in previous}
for world in build_worlds.WORLDS:
    ident=world['id']
    if chosen and not any(ident.startswith(x)for x in chosen):continue
    folder=ROOT/'Output'/ident
    if not(folder/'island.blend').is_file():raise FileNotFoundError(ident+' has no saved source')
    bpy.ops.wm.open_mainfile(filepath=str(folder/'island.blend'))
    before=build_worlds.validate_scene.geometry_snapshot(bpy.context.scene)
    visual=build_worlds.visual_signature()
    policy=build_worlds.export_glb(folder/'island.glb',bpy.data.collections[ident])
    assert build_worlds.visual_signature()==visual,'Canonical Blender materials were changed'
    after=build_worlds.validate_scene.geometry_snapshot(bpy.context.scene)
    assert before['sha256']==after['sha256'],'Canonical scene geometry was changed'
    manifest=json.loads((folder/'manifest.json').read_text(encoding='utf8'))
    assert manifest['geometry_sha256']==before['sha256'],'Saved scene differs from render evidence'
    manifest['glb_material_export']=policy
    manifest['glb_sha256']=hashlib.sha256((folder/'island.glb').read_bytes()).hexdigest()
    (folder/'manifest.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    reports[ident]=dict(id=ident,geometry_sha256=before['sha256'],**policy,**{'pass':True})
    print('EXPORT_REFRESHED',ident,policy,flush=True)
report_file.write_text(json.dumps({'pass':len(reports)==10,'worlds':[reports[key]for key in sorted(reports)]},ensure_ascii=False,indent=2),encoding='utf8')
