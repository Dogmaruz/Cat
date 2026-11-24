"""Matched-camera source/derivative CASE renders, with rotor and separate straps hidden."""
import argparse
import json
import sys
from pathlib import Path
import bpy
from mathutils import Vector

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[1] / 'StraplessCases'
sys.path.insert(0, str(ROOT))
import clean_cases as lighting

parser = argparse.ArgumentParser()
parser.add_argument('--candidate', type=Path, required=True)
parser.add_argument('--ids', nargs='+', required=True)
parser.add_argument('--out', type=Path, required=True)
args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
args.out.mkdir(parents=True, exist_ok=True)
for name in args.ids:
    meta = json.loads((ROOT/'Before'/name/'Meta.json').read_text())
    height = meta['caseBoundsMax'][2]
    target = Vector((0,0,height*.50))
    for version, path in [('source', ROOT/'Before'/name/'editable.blend'),
                          ('candidate', args.candidate/name/'editable.blend')]:
        bpy.ops.wm.open_mainfile(filepath=str(path), load_ui=False)
        for obj in list(bpy.context.scene.objects):
            if obj.type in ('LIGHT','CAMERA'):
                bpy.data.objects.remove(obj, do_unlink=True)
            elif obj.type == 'MESH':
                obj.hide_render = obj.name != 'Case'
        camera = lighting.src.light_camera()
        scene = bpy.context.scene; scene.cycles.samples = 8
        scene.render.resolution_x=600; scene.render.resolution_y=720
        camera.data.ortho_scale=max(1.55,height*1.28)
        camera.location=target+Vector((3,6,2.4))
        camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
        scene.render.filepath=str(args.out/(name+'_'+version+'_radio.png'))
        bpy.ops.render.render(write_still=True)
        print('CASE_COMPARISON_RENDER',name,version,flush=True)
