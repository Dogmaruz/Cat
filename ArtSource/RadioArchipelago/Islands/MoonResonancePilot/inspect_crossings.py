import bpy,sys,pathlib,json
from mathutils import Vector
from mathutils.bvhtree import BVHTree
h=pathlib.Path('C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/Islands/MoonResonancePilot');sys.path.insert(0,str(h));import build_moon_resonance as m
bpy.ops.wm.open_mainfile(filepath=str(h/'moon_resonance_preview.blend'));m.prepare_cap_index()
rows=[dict(s=s,x=m.route(s)[0].x,y=m.route(s)[0].y,play=m.play(s),ground=m.actual_ground(*m.route(s)[0][:2]))for s in range(0,750,5)]
print('GAPS',json.dumps([r for r in rows if r['ground'] is None]));print('STATIONS',json.dumps([r for r in rows if 250<=r['s']<=310 or 440<=r['s']<=535]))
