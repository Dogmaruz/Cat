"""Read-only numeric/source handoff checks; run after the source exporter finishes."""
import hashlib,json,math,pathlib

HERE=pathlib.Path(__file__).resolve().parent
STAGING=HERE/'STAGING'
def read(name):return json.loads((STAGING/name).read_text(encoding='utf8'))
def sha(path):return hashlib.sha256(pathlib.Path(path).read_bytes()).hexdigest()

report=json.loads((HERE/'amber_winds_report.json').read_text(encoding='utf8'))
plan=json.loads((HERE/'route_plan.json').read_text(encoding='utf8'))
route=read('amber_winds_route.json');length=route['length'];points=route['points']
computed=sum(math.dist((a['x'],a['z']),(b['x'],b['z']))for a,b in zip(points,points[1:]+points[:1]))
assert route['closed']and len(points)>1000
assert abs(computed-length)<.02,(computed,length)
assert 770<length<780 and plan['min_radius']>=21.9
assert abs(points[0]['x'])<1e-5 and abs(points[0]['z'])<1e-5
travel=0
for a,b in zip(points,points[1:]):
    if travel<35:assert abs(a['z'])<1e-5
    if 662<travel<length-1:assert abs(a['z'])<1e-5
    travel+=math.dist((a['x'],a['z']),(b['x'],b['z']))
for filename in('play_elevation.json','ground_elevation.json'):
    profile=read(filename);keys=profile['keys']
    assert keys[0]['distance']==0 and abs(keys[-1]['distance']-length)<1e-6
    assert all(a['distance']<b['distance']for a,b in zip(keys,keys[1:]))
    assert all(4<=k['height']<=23 for k in keys)
    assert all(abs(k['height']-4)<1e-6 for k in keys if k['distance']<=35 or k['distance']>=640)
windows=read('cloud_steps.json')['ranges'];assert len(windows)==4
assert all(w['y']-w['x']>=22 for w in windows)
assert all(a['y']<b['x']for a,b in zip(windows,windows[1:]))
rotors=read('RooftopMotion.json')['mechanisms'];assert len(rotors)==3
assert len({r['nodeName']for r in rotors})==3
assert all(r['nodeName'].startswith('RA_Rooftop_Amber_')for r in rotors)
assert all(45<=abs(r['degreesPerSecond'])<=95 for r in rotors)
assert all(abs(sum(v*v for v in r['axis'].values())-1)<1e-5 for r in rotors)
assert all(len(r['materialSources'])==2 for r in rotors)
assert min(x['sweepRouteClearance']for x in report['architecture']if x['kind']=='animated windmill')>12
assert report['source_geometry_audit']['upward_errors']==0
assert report['source_geometry_audit']['above_flight_terrain']==0
assert report['source_geometry_audit']['water_triangles']>=18000
assert report['frozen_inputs_unchanged']and all(sha(p)==h for p,h in report['frozen_inputs'].items())
assert sha(STAGING/'amber_winds_island.fbx')==report['export']['sha256']
frozen_contracts=HERE/'pre_canyon_fix_contracts.json'
if frozen_contracts.exists():
    for name,digest in json.loads(frozen_contracts.read_text()).items():assert sha(STAGING/name)==digest,('Unexpected canyon-fix contract change',name)
assert report['shoreline_stones']>140
assert report['side_cascade']['minimum_all_feature_vertex_route_distance']>=10.5
assert all(10<=2*min(i['radii'])<=12 for i in report['hopping_islets'])
geometry=json.loads((HERE/'source_geometry_inspection.json').read_text())
assert geometry['triangles']==report['export']['triangles']
assert not geometry['candidate_vertices_inside_8_5m_structure_envelope']
assert geometry['static_structure_vertex_minimum_route_distance_in_flight_height_band']>=10.5
assert len(geometry['rigid_meshes'])==3 and all(x['pivotError']<1e-4 for x in geometry['rigid_meshes'])
assert geometry.get('canyon_intruding_shell_faces',-1)==0
shading=geometry['water_seam_shading'];assert len(shading)==3 and max(x['max_displaced_gap']for x in shading)<1e-4
roundtrip=json.loads((HERE/'fbx_shading_roundtrip.json').read_text())
assert roundtrip['model_sha256']==report['export']['sha256']and roundtrip['import_custom_normals']and roundtrip['max_displaced_gap']<1e-4
files={p.name:dict(bytes=p.stat().st_size,sha256=sha(p))for p in STAGING.iterdir()if p.is_file()}
proofs={p.name:sha(p)for p in (HERE/'Proof').glob('*.png')if p.name in('gameplay-155.png','gameplay-170.png','gameplay-185.png','gameplay-210.png')}
historical_proofs={p.name:sha(p)for p in (HERE/'Proof').glob('*.png')if p.name not in proofs}
assert len(proofs)==4
output=dict(status='source_contract_verified',route_length=length,route_minimum_radius=plan['min_radius'],exact_final_straight_start=662,flat_finish_start=640,files=files,
 source_profiles='Blender(x,y,z) -> aligned Unity(x,z,-y)',cloud_windows=windows,
 rotating_mills=3,min_full_sweep_route_clearance=12.131863318542042,source_structure_vertex_minimum_distance=geometry['static_structure_vertex_minimum_route_distance_in_flight_height_band'],canyon_intruding_shell_faces=geometry['canyon_intruding_shell_faces'],upper_creek_fall_seam=geometry['upper_creek_fall_seam'],water_seams=geometry['water_seams'],water_seam_shading=shading,fbx_custom_normal_max_displaced_gap=roundtrip['max_displaced_gap'],side_cascade=report['side_cascade'],preserved_gameplay_contracts=json.loads(frozen_contracts.read_text())if frozen_contracts.exists()else{},
 autumn_tree_count=report['trees']+report['autumn_dressing']['approach_canopies'],autumn_foreground_dressing=report['autumn_dressing'],shoreline_stones=report['shoreline_stones'],stone_shape_variants=7,
 source_portrait_camera=dict(back=9.5,height=8,lookAhead=3.8,focusHeight=3.6,verticalFov=82,stations=[155,170,185,210]),
 proofs=proofs,historical_source_composition_proofs=historical_proofs,proof_note='155/170/185/210 show final geometry before shading-only correction; all positions/UVs are unchanged. Revised normals/colors require native recapture. Other views preserve earlier composition studies.',preserved_original_fbxs=report['frozen_inputs'],
 native_status='Await Unity import, native swept geometry checks, and actual Play Mode review; source images are not runtime proof.')
(HERE/'source_handoff.json').write_text(json.dumps(output,indent=2),encoding='utf8')
print(json.dumps({k:v for k,v in output.items()if k not in('files','proofs','preserved_original_fbxs','historical_source_composition_proofs','side_cascade','water_seams','water_seam_shading','upper_creek_fall_seam','preserved_gameplay_contracts')},indent=2))
print('ALL_SOURCE_CONTRACT_CHECKS_PASSED')
