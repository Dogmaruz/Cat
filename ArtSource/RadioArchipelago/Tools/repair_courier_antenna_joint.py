"""Seat Tiko's complete coupler on a concentric, closed swivel bearing.

Called after the rounded courier pipeline as ``apply(rig, body)``.  Only the
incorrect accessory cap, a 12 mm strip of the socket, and antenna weights/rest
bones change.  No image, arm, scarf, backpack, or other mesh is modified.
"""
from __future__ import annotations

import math
import pathlib
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

REVISION = "tiko-concentric-swivel-v1"
OLD_Z = 1.65
BEARING_Z = 1.45
HALF_BAND = .006
LIP = .002
RECESS = .008
RADIAL_CLEARANCE = .0004
SEGMENTS = 64
TOOLS = pathlib.Path(__file__).resolve().parent
if str(TOOLS) not in sys.path:
    sys.path.insert(0, str(TOOLS))
from prepare_radio_accessories import bisect_local, cap_boundaries, components


def require(value, message):
    if not value:
        raise RuntimeError(message)


def coordinates(mesh):
    return np.array([v.co[:] for v in mesh.data.vertices], dtype=np.float64)


def _normal(loop, layer):
    n = Vector(loop[layer])
    return n.normalized() if n.length > .01 else loop.face.normal.copy()


def _circle(points):
    xy = np.array([p[:2] for p in points])
    fit = np.linalg.lstsq(np.column_stack((2 * xy, np.ones(len(xy)))),
                          np.sum(xy * xy, axis=1), rcond=None)[0]
    center = Vector((float(fit[0]), float(fit[1]), BEARING_Z))
    radius = math.sqrt(float(fit[2] + np.dot(fit[:2], fit[:2])))
    return center, radius


def _section(bm, height, center):
    points = []
    for edge in bm.edges:
        a, b = (v.co for v in edge.verts)
        if (a.z - height) * (b.z - height) < 0:
            p = a.lerp(b, (height - a.z) / (b.z - a.z))
            if math.hypot(p.x - center.x, p.y - center.y) < .14:
                points.append(p)
    require(len(points) >= 16, "Tiko socket section not found")
    return points


def _bearing_patch(source_triangles):
    """Select an actual mint-metal atlas rectangle without changing its pixels."""
    def pixels(kind):
        source = next(i for i in bpy.data.images if kind in i.name and i.source == "FILE")
        copy = source.copy()
        copy.scale(512, 512)
        values = np.array(copy.pixels[:], dtype=np.float64).reshape((512, 512, 4))
        bpy.data.images.remove(copy)
        return values
    rgb = pixels("BaseColor")[:, :, :3]
    metal = pixels("MetallicSmoothness")[:, :, 0]
    samples = []
    for _, uv in source_triangles:
        p = sum(uv, Vector((0, 0))) / 3
        samples.append((min(511, max(0, int(p.y * 512))),
                        min(511, max(0, int(p.x * 512)))))
    target = np.median([rgb[y, x] for y, x in samples], axis=0)
    target_metal = float(np.median([metal[y, x] for y, x in samples]))
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    allowed = (g > r * 1.04) & (g > b * .98) & (g > .12) & (g < .72)
    allowed &= np.abs(metal - target_metal) < .18
    h, w = 8, 24
    def sums(values):
        integral = np.pad(values, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
        return integral[h:, w:] - integral[:-h, w:] - integral[h:, :-w] + integral[:-h, :-w]
    invalid = sums((~allowed).astype(float)) / (h * w)
    cost = sums(np.sum((rgb - target) ** 2, axis=2)) / (h * w) + invalid * 100
    y, x = np.unravel_index(int(np.argmin(cost)), cost.shape)
    require(invalid[y, x] == 0, "No complete mint-metal bearing atlas patch found")
    return Vector(((x + 1) / 512, (y + 1) / 512)), Vector(((w - 2) / 512, (h - 2) / 512))


def _bridge(bm, boundary, center, radius, height, group, normal_layer, patch):
    """Zipper a source boundary to a regular circular bearing without remeshing."""
    uv_layer = bm.loops.layers.uv.active
    deform = bm.verts.layers.deform.verify()
    old = sorted({v for edge in boundary for v in edge.verts},
                 key=lambda v: math.atan2(v.co.y - center.y, v.co.x - center.x))
    require(len(old) == len(boundary), "Bearing source boundary is not one closed loop")
    theta = [math.atan2(v.co.y - center.y, v.co.x - center.x) for v in old]
    start = theta[0]
    phases = [a - start for a in theta] + [math.tau]
    old_normal = {}
    material = old[0].link_faces[0].material_index
    for v in old:
        loop = v.link_loops[0]
        old_normal[v] = _normal(loop, normal_layer)
    def circular_ring(z):
        result = []
        for k in range(SEGMENTS):
            angle = start + math.tau * k / SEGMENTS
            v = bm.verts.new((center.x + radius * math.cos(angle),
                              center.y + radius * math.sin(angle), z))
            v[deform][group] = 1.
            for layer in bm.verts.layers.shape.values():
                v[layer] = v.co
            result.append(v)
        return result
    # Source tapers end outside the overlap.  Both walls must stay cylindrical
    # throughout the overlap, including between their terminal rim planes.
    transition_height = BEARING_Z - LIP if height > BEARING_Z else BEARING_Z + LIP
    ring = circular_ring(transition_height)
    added = []
    i = j = 0
    while i < len(old) or j < SEGMENTS:
        next_old = phases[i + 1] if i < len(old) else math.inf
        next_new = math.tau * (j + 1) / SEGMENTS if j < SEGMENTS else math.inf
        if next_old < next_new:
            verts = (old[i % len(old)], old[(i + 1) % len(old)], ring[j % SEGMENTS])
            i += 1
        else:
            verts = (old[i % len(old)], ring[(j + 1) % SEGMENTS], ring[j % SEGMENTS])
            j += 1
        face = bm.faces.new(verts)
        added.append(face)
    terminal = circular_ring(height)
    for k in range(SEGMENTS):
        added.append(bm.faces.new((ring[k], ring[(k + 1) % SEGMENTS],
                                   terminal[(k + 1) % SEGMENTS], terminal[k])))
    ring = terminal
    for face in added:
        radial = face.calc_center_median() - center
        radial.z = 0
        face.normal_update()
        if face.normal.dot(radial) < 0:
            face.normal_flip()
        face.material_index = material
        face.smooth = True
        angles = [math.atan2(loop.vert.co.y - center.y, loop.vert.co.x - center.x) % math.tau
                  for loop in face.loops]
        if max(angles) - min(angles) > math.pi:
            angles = [angle + math.tau if angle < math.pi else angle for angle in angles]
        for loop, angle in zip(face.loops, angles):
            v = loop.vert
            if uv_layer:
                origin, extent = patch
                loop[uv_layer].uv = origin + Vector((extent.x * angle / math.tau,
                    extent.y * (v.co.z - BEARING_Z + HALF_BAND) / (2 * HALF_BAND)))
            if v in old_normal:
                loop[normal_layer] = old_normal[v]
            else:
                n = v.co - center
                n.z = 0
                loop[normal_layer] = n.normalized()
    edges = [edge for edge in bm.edges if edge.is_boundary and
             all(v in ring for v in edge.verts)]
    require(len(edges) == SEGMENTS, "Bearing circular rim is incomplete")
    cap_boundaries(bm, edges, normal_layer, recess=RECESS)
    return ring


def _evaluated(body):
    bpy.context.view_layer.update()
    evaluated = body.evaluated_get(bpy.context.evaluated_depsgraph_get())
    temporary = evaluated.to_mesh()
    result = np.array([v.co[:] for v in temporary.vertices])
    evaluated.to_mesh_clear()
    return result


def _surface_clearance(body, actual, dynamic, center):
    """Measure the evaluated walls, not just their ideal terminal circles."""
    local = (np.linalg.norm(actual[:, :2] - np.array(center[:2]), axis=1) < .14)
    local &= (actual[:, 2] > BEARING_Z - .018) & (actual[:, 2] < BEARING_Z + .018)
    partitions = ([], [])
    for triangle in body.data.loop_triangles:
        ids = list(triangle.vertices)
        if all(local[i] for i in ids):
            partitions[int(dynamic[ids[0]])].append(ids)
    points = [Vector(p) for p in actual]
    fixed, moving = [BVHTree.FromPolygons(points, faces, all_triangles=True)
                     for faces in partitions]
    require(fixed and moving, "Bearing surface trees are empty")
    crossings = fixed.overlap(moving)
    require(not crossings, f"Fixed/moving bearing triangles intersect: {len(crossings)}")
    gaps = []
    for height in np.linspace(BEARING_Z - LIP + 1e-5, BEARING_Z + LIP - 1e-5, 5):
        origin = Vector((center.x, center.y, float(height)))
        for degrees in range(0, 360, 10):
            a = math.radians(degrees)
            direction = Vector((math.cos(a), math.sin(a), 0))
            fixed_hit = fixed.ray_cast(origin, direction, .14)
            moving_hit = moving.ray_cast(origin, direction, .14)
            require(fixed_hit[0] is not None and moving_hit[0] is not None,
                    "A bearing wall has a through edge")
            gaps.append(fixed_hit[3] - moving_hit[3])
    require(min(gaps) > .00025, f"Actual bearing walls lose their radial gap: {min(gaps)}")
    return {"min_actual_wall_radial_clearance": float(min(gaps)),
            "bearing_surface_intersections": len(crossings), "wall_ray_samples": len(gaps)}


def audit(rig, body, center, radius):
    """Exercise actual Blender skinning, including the seated root during lean."""
    saved = {p.name: (p.matrix_basis.copy(), p.rotation_mode) for p in rig.pose.bones}
    shape = [(key, key.value) for key in body.data.shape_keys.key_blocks] if body.data.shape_keys else []
    try:
        for p in rig.pose.bones:
            p.matrix_basis = Matrix.Identity(4)
        for key, _ in shape:
            key.value = 0
        rest = coordinates(body)
        body.data.calc_loop_triangles()
        edges = np.array([edge.vertices[:] for edge in body.data.edges])
        lengths = np.linalg.norm(rest[edges[:, 1]] - rest[edges[:, 0]], axis=1)
        valid = lengths > 1e-5
        groups = {g.index: g.name for g in body.vertex_groups}
        dynamic = np.array([sum(w.weight for w in v.groups
                                if groups[w.group].startswith("antenna.")) > .9999
                            for v in body.data.vertices])
        pure_yaw = np.array([sum(w.weight for w in v.groups if groups[w.group] == "antenna.yaw") > .9999
                             for v in body.data.vertices])
        seated_edges = pure_yaw[edges[:, 0]] & pure_yaw[edges[:, 1]] & valid
        require(not np.any(dynamic[edges[:, 0]] != dynamic[edges[:, 1]]),
                "An edge still spans the fixed and rotating bearing")
        require(not any(any(dynamic[i] for i in face.vertices) and
                        not all(dynamic[i] for i in face.vertices) for face in body.data.polygons),
                "A triangle still spans the fixed and rotating bearing")
        fixed_cap = np.array([not dynamic[v.index] and abs(v.co.z - (BEARING_Z + LIP - RECESS)) < 2e-5
                              and math.hypot(v.co.x - center.x, v.co.y - center.y) < radius + .001
                              for v in body.data.vertices])
        moving_cap = np.array([dynamic[v.index] and abs(v.co.z - (BEARING_Z - LIP + RECESS)) < 2e-5
                               and math.hypot(v.co.x - center.x, v.co.y - center.y) < radius + .001
                               for v in body.data.vertices])
        require(fixed_cap.sum() >= SEGMENTS and moving_cap.sum() >= SEGMENTS,
                "Bearing caps are missing")
        yaw = rig.pose.bones["antenna.yaw"]
        cases = {}
        for degrees in (0, 45, 90, 145, 180, 225, 270, 315, 360):
            for lean in (False, True):
                for p in rig.pose.bones:
                    p.matrix_basis = Matrix.Identity(4)
                yaw.rotation_mode = "QUATERNION"
                yaw.rotation_quaternion = Quaternion(yaw.bone.matrix_local.to_quaternion().inverted()
                                                       @ Vector((0, 0, 1)), math.radians(degrees))
                if lean:
                    # The runtime distributes its bend budget along five bones.
                    for i in range(1, 6):
                        p = rig.pose.bones[f"antenna.{i:02d}"]
                        p.rotation_mode = "QUATERNION"
                        axis = p.bone.matrix_local.to_quaternion().inverted() @ Vector((1, .3, 0)).normalized()
                        p.rotation_quaternion = Quaternion(axis, math.radians(4))
                actual = _evaluated(body)
                changed = np.linalg.norm(actual[edges[:, 1]] - actual[edges[:, 0]], axis=1)
                ratio = changed[valid] / lengths[valid]
                cap_gap = float(actual[moving_cap, 2].min() - actual[fixed_cap, 2].max())
                fixed_error = float(np.linalg.norm(actual[~dynamic] - rest[~dynamic], axis=1).max())
                result = {"finite": bool(np.isfinite(actual).all()),
                          "max_edge_stretch": float(ratio.max()),
                          "fixed_max_displacement": fixed_error,
                          "cap_vertical_clearance": cap_gap,
                          "seated_max_edge_change": float(np.abs(changed[seated_edges] - lengths[seated_edges]).max())}
                result.update(_surface_clearance(body, actual, dynamic, center))
                if not lean:
                    result["max_absolute_edge_change"] = float(np.abs(changed - lengths).max())
                    require(result["max_absolute_edge_change"] < 3e-6,
                            f"Rigid yaw deforms an edge at {degrees}: {result}")
                require(result["finite"] and fixed_error < 3e-6 and cap_gap > .0119,
                        f"Unseated swivel at yaw {degrees}, lean {lean}: {result}")
                require(result["seated_max_edge_change"] < 3e-6, "Lean deforms the seated coupler")
                require(ratio.max() < (1.15 if lean else 1.002),
                        f"Antenna skin stretches excessively: {result}")
                cases[f"yaw-{degrees:03d}" + ("-lean" if lean else "")] = result
        rest_pose_error = float(np.linalg.norm(_rest_evaluated(rig, body) - rest, axis=1).max())
        require(rest_pose_error < 3e-6, "Changing the inverse bind moved the rest mesh")
        return {"cases": cases, "fixed_to_rotating_triangles": 0,
                "rest_pose_max_error": rest_pose_error,
                "minimum_bearing_radial_clearance": radius * math.cos(math.pi / SEGMENTS)
                                                       - (radius - RADIAL_CLEARANCE),
                "blink_preserved": bool(body.data.shape_keys and body.data.shape_keys.key_blocks.get("Blink"))}
    finally:
        for p in rig.pose.bones:
            p.rotation_mode = saved[p.name][1]
            p.matrix_basis = saved[p.name][0]
        for key, value in shape:
            key.value = value
        bpy.context.view_layer.update()


def _rest_evaluated(rig, body):
    for p in rig.pose.bones:
        p.matrix_basis = Matrix.Identity(4)
    return _evaluated(body)


def apply(rig, body):
    """Return deterministic measurements; leave artifact export to the caller."""
    if "Tiko" not in rig.name:
        return {"applied": False, "reason": "Tiko-only joint repair"}
    if body.get("antenna_joint_revision") == REVISION:
        center = Vector(body["antenna_bearing_center"])
        return {"applied": False, "revision": REVISION,
                "audit": audit(rig, body, center, body["antenna_bearing_radius"])}
    require(rig.data.bones.get("antenna.yaw") is not None, "Prepared accessory rig required")
    before = coordinates(body)
    old_pivot = list(rig.data.bones["antenna.yaw"].head_local)
    old_lean = list(rig.data.bones["antenna.01"].head_local)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    boundary_before = sum(edge.is_boundary for edge in bm.edges)
    normals = bm.loops.layers.float_vector.new("AntennaBearingNormal")
    for face, polygon in zip(bm.faces, body.data.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[normals] = body.data.corner_normals[index].vector
    old_center = Vector((-.713168, .656827, OLD_Z))
    def old_cap_vertex(v):
        return (min(abs(v.co.z - z) for z in (OLD_Z - .004, OLD_Z, OLD_Z + .004)) < 3e-6
                and math.hypot(v.co.x - old_center.x, v.co.y - old_center.y) < .37)
    caps = [face for face in bm.faces if all(old_cap_vertex(v) for v in face.verts)]
    require(300 <= len(caps) <= 420, f"Unexpected original cap topology: {len(caps)}")
    removed_caps = len(caps)
    bmesh.ops.delete(bm, geom=caps, context="FACES_ONLY")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    seam = [v for v in bm.verts if abs(v.co.z - OLD_Z) < 3e-6 and
            math.hypot(v.co.x - old_center.x, v.co.y - old_center.y) < .37]
    bmesh.ops.remove_doubles(bm, verts=seam, dist=2e-6)
    require(sum(e.is_boundary for e in bm.edges) == boundary_before,
            "Undoing the incorrect coupler cut opened its shell")
    center, radius = _circle(_section(bm, BEARING_Z, Vector((-.58, .552, BEARING_Z))))
    for height in (BEARING_Z - HALF_BAND, BEARING_Z + HALF_BAND):
        bisect_local(bm, Vector((center.x, center.y, height)), Vector((0, 0, 1)), .14)
    strip = [face for face in bm.faces if BEARING_Z - HALF_BAND + 2e-6 < face.calc_center_median().z
             < BEARING_Z + HALF_BAND - 2e-6 and all(
                 math.hypot(v.co.x - center.x, v.co.y - center.y) < .15 for v in face.verts)]
    require(20 <= len(strip) <= 220, f"Unexpected bearing strip topology: {len(strip)}")
    removed_strip = len(strip)
    source_triangles = []
    uv_layer = bm.loops.layers.uv.active
    for face in strip:
        loops = list(face.loops)
        for i in range(1, len(loops) - 1):
            triangle = (loops[0], loops[i], loops[i + 1])
            source_triangles.append(([loop.vert.co.copy() for loop in triangle],
                                     [loop[uv_layer].uv.copy() for loop in triangle]))
    patch = _bearing_patch(source_triangles)
    bmesh.ops.delete(bm, geom=strip, context="FACES_ONLY")
    # FACES_ONLY retains the old strip's now-wire cross edges.  They are not
    # rendered, but must not connect the fixed and moving skin islands.
    wires = [e for e in bm.edges if not e.link_faces and all(
        1.43 < v.co.z < 1.66 and math.hypot(v.co.x - center.x, v.co.y - center.y) < .40
        for v in e.verts)]
    if wires:
        bmesh.ops.delete(bm, geom=wires, context="EDGES")
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context="VERTS")
    tip = Vector((.3893, -.1699, 2.314))
    dynamic_faces = min(components(bm), key=lambda comp: min(
        (face.calc_center_median() - tip).length_squared for face in comp))
    movable = {v for face in dynamic_faces for v in face.verts}
    require(500 < len(dynamic_faces) < 1500, "Complete antenna coupler did not detach")
    groups = {g.name: g.index for g in body.vertex_groups}
    yaw_group = body.vertex_groups.get("antenna.yaw") or body.vertex_groups.new(name="antenna.yaw")
    antenna = {i for name, i in groups.items() if name.startswith("antenna.")}
    antenna.add(yaw_group.index)
    deform = bm.verts.layers.deform.verify()
    for v in bm.verts:
        weights = v[deform]
        if v in movable:
            selected = {k: weights[k] for k in antenna if k in weights and weights[k] > 1e-6}
            total = sum(selected.values())
            selected = {k: value / total for k, value in selected.items()} if total > 1e-6 else {groups["antenna.01"]: 1.}
            blend = max(0., min(1., (v.co.z - 1.76) / .14))
            weights.clear()
            if blend < 1:
                weights[yaw_group.index] = 1. - blend
            for k, value in selected.items():
                if blend * value > 1e-7:
                    weights[k] = weights.get(k, 0.) + blend * value
        else:
            for k in antenna:
                if k in weights:
                    del weights[k]
            total = sum(weights.values())
            if total < 1e-6:
                weights[groups["body"]] = 1.
            else:
                for k in list(weights.keys()):
                    weights[k] /= total
    for fixed, height in ((True, BEARING_Z - HALF_BAND), (False, BEARING_Z + HALF_BAND)):
        boundary = [e for e in bm.edges if e.is_boundary and all(
            abs(v.co.z - height) < 3e-6 and math.hypot(v.co.x - center.x, v.co.y - center.y) < .15
            for v in e.verts)]
        require(len(boundary) >= 20, "Bearing source loop is missing")
        _bridge(bm, boundary, center, radius if fixed else radius - RADIAL_CLEARANCE,
                BEARING_Z + LIP if fixed else BEARING_Z - LIP,
                groups["body"] if fixed else yaw_group.index, normals, patch)
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    require(sum(e.is_boundary for e in bm.edges) == boundary_before,
            "Bearing repair introduced an open boundary")
    require(not any(len(e.link_faces) > 2 for e in bm.edges), "Bearing is non-manifold")
    bm.normal_update()
    bm.to_mesh(body.data)
    bm.free()
    body.data.update()
    attribute = body.data.attributes["AntennaBearingNormal"]
    body.data.normals_split_custom_set([
        Vector(d.vector).normalized() if Vector(d.vector).length > .01 else Vector((0, 0, 1))
        for d in attribute.data])
    body.data.attributes.remove(attribute)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bones = rig.data.edit_bones
    bones["antenna.yaw"].head = center
    bones["antenna.yaw"].tail = center + Vector((0, 0, .18))
    bones["antenna.01"].head = Vector((-.80, .72, 1.76))
    bpy.ops.object.mode_set(mode="OBJECT")
    result = {"applied": True, "revision": REVISION, "old_yaw_pivot": old_pivot,
              "new_yaw_pivot": list(center), "old_lean_pivot": old_lean,
              "new_lean_pivot": list(rig.data.bones["antenna.01"].head_local),
              "bearing_radius": radius, "bearing_strip_height": 2 * HALF_BAND,
              "bearing_lip_overlap": 2 * LIP, "removed_old_cap_triangles": removed_caps,
              "bearing_uv_patch": {"origin": list(patch[0]), "extent": list(patch[1])},
              "removed_socket_strip_triangles": removed_strip,
              "before_vertices": len(before), "after_vertices": len(body.data.vertices),
              "unchanged_boundary_edges": boundary_before,
              "audit": audit(rig, body, center, radius)}
    body["antenna_joint_revision"] = REVISION
    body["antenna_bearing_center"] = list(center)
    body["antenna_bearing_radius"] = radius
    return result
