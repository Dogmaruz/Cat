"""Internal directional guides only; reads a canonical BLEND and never saves it.

Run with Blender --background <canonical island.blend> --python <this file>.
Camera azimuths are cardinal, all use the same 28-degree downward angle and
orthographic pixel/world scale. Geometry, materials and lights stay untouched.
"""
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ROOT = Path('C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/Islands/V4')
SOURCE = ROOT / 'Output/01_emerald_ravine/island.blend'
OUT = Path(__file__).resolve().parent
if Path(bpy.data.filepath).resolve() != SOURCE.resolve():
    raise RuntimeError('Open the canonical emerald island before running this script.')

sys.path.insert(0, str(ROOT))
import validate_scene


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def material_signature():
    # The script never accesses writable material fields. This identity records
    # original datablock membership and shader-node values to detect accidents.
    records = []
    for mat in sorted(bpy.data.materials, key=lambda m: m.name):
        nodes = []
        if mat.use_nodes and mat.node_tree:
            for node in sorted(mat.node_tree.nodes, key=lambda n: n.name):
                values = []
                for socket in node.inputs:
                    if hasattr(socket, 'default_value'):
                        value = socket.default_value
                        if isinstance(value, (bool, int, float, str)):
                            values.append((socket.identifier, value))
                        elif hasattr(value, '__iter__'):
                            try:
                                values.append((socket.identifier, list(value)))
                            except TypeError:
                                pass
                nodes.append((node.name, node.bl_idname, values))
        records.append((mat.name, tuple(mat.diffuse_color), mat.use_nodes, nodes))
    return hashlib.sha256(json.dumps(records, sort_keys=True).encode()).hexdigest()


scene = bpy.context.scene
source_before = file_hash(SOURCE)
geometry_before = validate_scene.geometry_snapshot(scene)
materials_before = material_signature()
manifest = json.loads((SOURCE.parent / 'manifest.json').read_text(encoding='utf-8'))
if geometry_before['sha256'] != manifest['geometry_sha256']:
    raise RuntimeError('Canonical geometry differs from its supplied render manifest.')

meshes = [obj for obj in scene.objects if obj.type == 'MESH' and not obj.hide_render]
corners = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
low = Vector(tuple(min(v[i] for v in corners) for i in range(3)))
high = Vector(tuple(max(v[i] for v in corners) for i in range(3)))
center = (low + high) * .5
angle = math.radians(28)
distance = max(high - low) * 4
directions = {'front': (0, -1), 'right': (1, 0), 'left': (-1, 0), 'back': (0, 1)}
cameras = {}
required_scale = 0
for name, (dx, dy) in directions.items():
    data = bpy.data.cameras.new('Internal_geometry_guide_' + name)
    camera = bpy.data.objects.new(data.name, data)
    scene.collection.objects.link(camera)
    camera.location = center + Vector((dx * math.cos(angle), dy * math.cos(angle), math.sin(angle))) * distance
    camera.rotation_euler = (center - camera.location).to_track_quat('-Z', 'Y').to_euler()
    data.type = 'ORTHO'
    data.clip_start = .1
    data.clip_end = distance * 4
    camera['v3_role'] = 'camera'
    bpy.context.view_layer.update()
    local = [camera.matrix_world.inverted() @ p for p in corners]
    span_x = max(p.x for p in local) - min(p.x for p in local)
    span_y = max(p.y for p in local) - min(p.y for p in local)
    required_scale = max(required_scale, span_x, span_y)
    cameras[name] = camera

shared_scale = required_scale * 1.12
scene.render.resolution_x = scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
# Retain the source engine/samples/world/lights/materials; only choose the
# already-supported local CUDA compute device for a background render.
if scene.render.engine == 'CYCLES':
    prefs = bpy.context.preferences.addons['cycles'].preferences
    prefs.compute_device_type = 'CUDA'
    prefs.get_devices()
    cuda = [device for device in prefs.devices if device.type == 'CUDA']
    if cuda:
        for device in prefs.devices:
            device.use = device.type == 'CUDA'
        scene.cycles.device = 'GPU'

views = []
for name, camera in cameras.items():
    destination = OUT / ('guide_' + name + '.png')
    if destination.exists():
        raise FileExistsError('Refusing to replace existing guide: ' + str(destination))
    camera.data.ortho_scale = shared_scale
    scene.camera = camera
    scene.render.filepath = str(destination)
    bpy.context.view_layer.update()
    bpy.ops.render.render(write_still=True)
    record = validate_scene.camera_snapshot(camera, name)
    record.update(path=str(destination), downward_angle_degrees=28, width=1024, height=1024)
    views.append(record)
    print('INTERNAL_GUIDE_RENDERED ' + name + ' ' + str(destination), flush=True)

geometry_after = validate_scene.geometry_snapshot(scene)
materials_after = material_signature()
source_after = file_hash(SOURCE)
if geometry_after['sha256'] != geometry_before['sha256']:
    raise RuntimeError('Geometry changed while rendering internal guides.')
if materials_after != materials_before or source_before != source_after:
    raise RuntimeError('Canonical material/source preservation check failed.')
report = dict(
    purpose='Internal geometry guidance for image generation; not final requested concept art',
    canonical_source=str(SOURCE), source_file_sha256=source_after,
    source_file_unchanged=True, geometry_unchanged=True, materials_unchanged=True,
    geometry_sha256=geometry_after['sha256'], material_signature=materials_after,
    orthographic_scale=shared_scale, downward_angle_degrees=28,
    bounds=dict(min=list(low), max=list(high), center=list(center)),
    views=views,
)
(OUT / 'guide-manifest.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print('INTERNAL_GUIDES_PASS: no BLEND save; source, geometry and materials unchanged.', flush=True)
