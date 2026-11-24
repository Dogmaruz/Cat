"""Read-only conservative exported-mesh flight volume sample audit."""
import bpy,sys,pathlib,json,collections
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE))
import build_moon_resonance as moon
bpy.ops.wm.open_mainfile(filepath=str(HERE/'moon_resonance_preview.blend'))
checked=0;bad=collections.Counter();examples=[];clear_min=1e20
for ob in bpy.context.scene.objects:
    if ob.type!='MESH'or not ob.name.startswith('RA_Moon_'):continue
    cat=ob.get('moon_bucket')
    if cat in('Mist','Terrain','Water','Waterfall','Foam','Vines'):continue
    for v in ob.data.vertices:
        p=ob.matrix_world@v.co;_,i,d=moon.KD.find((p.x,p.y,0));s=float(moon.ARC[i]);height=moon.play(s)
        if d<8.2:
            checked+=1;clear_min=min(clear_min,height-p.z)
            if p.z>height-.10:
                bad[cat]+=1
                if len(examples)<12:examples.append(dict(category=cat,position=list(p),distance_to_route=d,route_station=s,route_floor=height))
report=dict(method='Every actual exported mesh vertex in 8.2m half-width corridor; station nearest to dense .55m route samples',sampled_vertices=checked,min_floor_separation=clear_min,vertices_within_0_1m_of_floor_or_above=dict(bad),examples=examples,passed=not bad)
(HERE/'flight-envelope-audit.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('MOON_FLIGHT_ENVELOPE',json.dumps(report),flush=True)
