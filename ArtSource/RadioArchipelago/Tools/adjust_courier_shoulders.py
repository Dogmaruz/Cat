"""Seat Lumen's actual shoulder joints and rigid paws on the torso sides.

Call ``apply(rig, meshes)`` after the existing rounded-paw and torso/harness
rebuild. It also recesses only the generated leather lid to clear that natural
arm placement. This module does not load, save, export, or publish any asset. All
coordinates use the existing Blender Z-up, forward -Y authoring space.
"""

from __future__ import annotations

import hashlib
import json
import math

import bpy
import bmesh
import numpy as np
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree

REVISION = "lumen-aperture-centered-shoulders-v2"
STAMP = "courier_shoulder_adjustment"
FORWARD_SHIFT = 0.07
PIVOT_EMBED = 0.012
MIN_PACK_GAP = 0.005
MIN_STRAP_MARGIN = 0.012
EPSILON = 2e-6
ORIGINAL_HEADS = {
    "L": (0.2849999666213989, 0.15000000596046448, 0.8700000047683716),
    "R": (-0.2549999952316284, 0.1599999964237213, 0.8799999952316284),
}


def _require(value, message):
    if not value:
        raise RuntimeError(message)


def _points(obj):
    return np.asarray([vertex.co[:] for vertex in obj.data.vertices], dtype=np.float64)


def _digest(values):
    return hashlib.sha256(np.asarray(values).tobytes()).hexdigest()


def _skin_signature(obj):
    """Attributes which a rigid translation has no reason to alter."""
    mesh = obj.data
    return {
        "topology": _digest([tuple(p.vertices) for p in mesh.polygons]),
        "uv": {
            layer.name: _digest([loop.uv[:] for loop in layer.data])
            for layer in mesh.uv_layers
        },
        "weights": _digest(
            [(vertex.index, item.group, item.weight) for vertex in mesh.vertices for item in vertex.groups]
        ),
        "groups": [group.name for group in obj.vertex_groups],
        "materials": [material.name if material else None for material in mesh.materials],
        "material_indices": _digest([polygon.material_index for polygon in mesh.polygons]),
    }


def _bone_snapshot(rig):
    return {
        bone.name: {
            "head": list(bone.head_local),
            "tail": list(bone.tail_local),
            "matrix": [list(row) for row in bone.matrix_local],
            "parent": bone.parent.name if bone.parent else None,
        }
        for bone in rig.data.bones
    }


def _body_bvh(body):
    return BVHTree.FromPolygons(
        [vertex.co for vertex in body.data.vertices],
        [polygon.vertices for polygon in body.data.polygons],
        all_triangles=True,
    )


def _signed_distance(tree, point):
    location, normal, _, distance = tree.find_nearest(point)
    _require(location is not None, "Cannot measure torso contact")
    return float(distance * (1 if (point - location).dot(normal) > 0 else -1))


def _alignment(tree, pack, head, side):
    sign = 1 if side == "L" else -1
    hit, _, _, _ = tree.ray_cast(
        Vector((sign * 2, head.y, head.z)), Vector((-sign, 0, 0))
    )
    _require(hit is not None, f"Missing anatomical torso section for shoulder {side}")
    return {
        "head": list(head),
        "torso_side_surface": list(hit),
        "torso_lateral_embed": float(sign * (hit.x - head.x)),
        "torso_signed_distance": _signed_distance(tree, head),
        "head_forward_of_pack_front": float(min(v.co.y for v in pack.data.vertices) - head.y),
    }


def _strap_aperture(harness, side, height):
    """Inner Y interval between the real strap sections at shoulder height."""
    sign = 1 if side == "L" else -1
    intervals = []
    for polygon in harness.data.polygons:
        points = [harness.data.vertices[index].co for index in polygon.vertices]
        if sign * sum(point.x for point in points) / len(points) < 0.1:
            continue
        values = []
        for a, b in zip(points, points[1:] + points[:1]):
            if (a.z - height) * (b.z - height) < 0:
                values.append(a.y + (b.y - a.y) * (height - a.z) / (b.z - a.z))
        if len(values) >= 2:
            intervals.append((min(values), max(values)))
    merged = []
    for low, high in sorted(intervals):
        if merged and low <= merged[-1][1] + 1e-6:
            merged[-1][1] = max(merged[-1][1], high)
        else:
            merged.append([float(low), float(high)])
    gaps = [(a[1], b[0]) for a, b in zip(merged, merged[1:])]
    _require(gaps, f"Cannot measure harness aperture {side}")
    front, rear = max(gaps, key=lambda value: value[1] - value[0])
    return {"height": float(height), "front": front, "rear": rear, "strap_sections": merged}


def _pack_clearance(points, edges, arm_faces, pack_points, pack_tree):
    nearest = [pack_tree.find_nearest(Vector(point)) for point in points]
    arm_tree = BVHTree.FromPolygons([Vector(point) for point in points], arm_faces, all_triangles=True)
    minimum = min(
        min(value[3] for value in nearest),
        min(arm_tree.find_nearest(Vector(point))[3] for point in pack_points),
    )
    crossings = 0
    for ia, ib in edges:
        a, b = Vector(points[ia]), Vector(points[ib])
        direction = b - a
        if direction.length <= 1e-9:
            continue
        hit, _, _, _ = pack_tree.ray_cast(a, direction.normalized(), direction.length)
        crossings += hit is not None
    inside = sum(
        (Vector(point) - location).dot(normal) < -1e-7
        for point, (location, normal, _, _) in zip(points, nearest)
    )
    return {
        "minimum_vertex_surface_distance": float(minimum),
        "arm_edge_pack_crossings": crossings,
        "arm_vertices_inside_pack": inside,
    }


def _protected_pack_signature(pack):
    """All retained contact and source case faces, excluding only our lid."""
    mesh = pack.data
    cap = mesh.attributes["IndependentHiddenCap"]
    original = mesh.attributes["OriginalSurfaceFace"]
    contact = mesh.attributes["RadioContactOriginalFace"]
    uv = mesh.uv_layers.active
    records = []
    for face in mesh.polygons:
        if cap.data[face.index].value == 2:
            continue
        corners = []
        for index in face.loop_indices:
            vertex = mesh.vertices[mesh.loops[index].vertex_index]
            corners.append((tuple(vertex.co), tuple(uv.data[index].uv), tuple((item.group, item.weight) for item in vertex.groups)))
        first = min(range(len(corners)), key=lambda index: corners[index])
        records.append((original.data[face.index].value, contact.data[face.index].value, cap.data[face.index].value, tuple(corners[first:] + corners[:first])))
    return hashlib.sha256(repr(sorted(records)).encode()).hexdigest(), len(records)


def _recess_generated_lid(rig, roles):
    """Clear the natural arm sweep by recessing only the added contact lid.

    Its old planar side triangles need a planar retriangulation after bending;
    otherwise thin inherited triangles fold despite the smooth displacement.
    Original case, controls, grille and retained contact stay byte-exact in
    geometry, UVs and weights. No image pixels are written.
    """
    from mathutils.geometry import delaunay_2d_cdt
    from repair_courier_harness import authentic_density
    from repair_smooth_torso_harness import atlas_patch

    pack = roles["Pack"]
    mesh = pack.data
    protected, protected_faces = _protected_pack_signature(pack)
    density, _ = authentic_density(pack, "leather")
    swept_points, swept_faces = [], []
    for side in ("L", "R"):
        obj = roles["Arm." + side]
        head = rig.data.bones["arm." + side].head_local
        sign = 1 if side == "L" else -1
        for angle in range(-10, 61, 5):
            rotation = Quaternion(Vector((0, 1, 0)), math.radians(-sign * angle))
            offset = len(swept_points)
            swept_points.extend(head + rotation @ (vertex.co - head) for vertex in obj.data.vertices)
            swept_faces.extend(tuple(offset + index for index in face.vertices) for face in obj.data.polygons)
    swept = BVHTree.FromPolygons(swept_points, swept_faces, all_triangles=True)
    cap_attribute = mesh.attributes["IndependentHiddenCap"]
    native = BVHTree.FromPolygons(
        [vertex.co for vertex in mesh.vertices],
        [face.vertices for face in mesh.polygons if cap_attribute.data[face.index].value != 2],
        all_triangles=True,
    )
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.faces.ensure_lookup_table()
    cap = bm.faces.layers.int["IndependentHiddenCap"]
    original = bm.faces.layers.int["OriginalSurfaceFace"]
    uv = bm.loops.layers.uv.active
    normal = bm.loops.layers.float_vector.new("ShoulderRecessKeptNormal")
    for face, polygon in zip(bm.faces, mesh.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[normal] = mesh.corner_normals[index].vector
    before_vertices, before_faces = len(bm.verts), len(bm.faces)
    for limit, cuts in ((0.045, 2), (0.035, 1)):
        edges = [edge for edge in bm.edges if all(face[cap] == 2 for face in edge.link_faces)
                 and edge.calc_length() > limit and min(vertex.co.y for vertex in edge.verts) < 0.205
                 and min(vertex.co.z for vertex in edge.verts) < 1.13]
        bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, use_grid_fill=True)
    offsets = [(0.0, 0.0, 1.0)]
    for radius in (0.02, 0.04, 0.06):
        amount = radius / 0.075
        weight = 1 - amount * amount * (3 - 2 * amount)
        for angle in np.linspace(0, 2 * math.pi, 12, endpoint=False):
            offsets.append((radius * math.cos(angle), radius * math.sin(angle), weight))
    changes = []
    for vertex in bm.verts:
        point = vertex.co.copy()
        if point.y >= 0.42 or not all(face[cap] == 2 for face in vertex.link_faces):
            continue
        delta = 0.0
        for dx, dz, weight in offsets:
            hit, _, _, _ = swept.ray_cast(Vector((point.x + dx, 0.65, point.z + dz)), Vector((0, -1, 0)))
            if hit is not None:
                delta = max(delta, max(0.0, hit.y + 0.020 - 0.11) * weight)
        cover, _, _, _ = native.ray_cast(Vector((point.x, -0.4, point.z)), Vector((0, 1, 0)))
        if cover is not None:
            delta = min(delta, max(0.0, cover.y - 0.014 - 0.11))
        amount = max(0.0, min(1.0, (point.y - 0.11) / 0.31))
        delta *= 1 - amount * amount * (3 - 2 * amount)
        if delta > 1e-6:
            vertex.co.y += delta
            changes.append((point, vertex.co.copy()))

    # Retriangulate both generated planar side charts in their actual YZ
    # coordinates. The original source mesh is outside these selected faces.
    charts = [
        {face for face in bm.faces if face[cap] == 2 and all(vertex.co.x < -0.4949 for vertex in face.verts)},
        {face for face in bm.faces if face[cap] == 2 and all(vertex.co.x > 0.2849 for vertex in face.verts)},
    ]
    paint_reports, retopology = [], []

    def paint(selected, depth=0):
        coordinates = np.asarray([(vertex.co.y, vertex.co.z) for face in selected for vertex in face.verts])
        low, high = coordinates.min(0), coordinates.max(0)
        scale = math.sqrt(density)
        triangles = [[(np.asarray((vertex.co.y, vertex.co.z)) - low) * scale for vertex in face.verts] for face in selected]
        try:
            origin, description = atlas_patch("Lumen", pack, float(high[0] - low[0]) * scale,
                                              float(high[1] - low[1]) * scale, (0.54, 0.25, 0.045),
                                              triangles, False, "radio_leather")
        except RuntimeError:
            _require(depth < 10 and len(selected) > 1, "Cannot paint the generated recess with native leather")
            axis = int(np.argmax(high - low))
            ordered = sorted(selected, key=lambda face: (np.mean([(vertex.co.y, vertex.co.z)[axis] for vertex in face.verts]), tuple(sorted(tuple(vertex.co) for vertex in face.verts))))
            middle = len(ordered) // 2
            paint(ordered[:middle], depth + 1)
            paint(ordered[middle:], depth + 1)
            return
        paint_reports.append(description)
        for face in selected:
            for loop in face.loops:
                loop[uv].uv = origin + Vector(tuple((np.asarray((loop.vert.co.y, loop.vert.co.z)) - low) * scale))

    for chart in charts:
        _require(chart, "Expected planar sides on the generated Lumen lid")
        sign = -1 if sum(vertex.co.x for face in chart for vertex in face.verts) < 0 else 1
        vertices = sorted({vertex for face in chart for vertex in face.verts}, key=lambda vertex: tuple(vertex.co))
        indices = {vertex: index for index, vertex in enumerate(vertices)}
        rim = {edge for face in chart for edge in face.edges if sum(neighbor in chart for neighbor in edge.link_faces) == 1}
        adjacent = {}
        for edge in rim:
            a, b = edge.verts
            adjacent.setdefault(a, []).append(b)
            adjacent.setdefault(b, []).append(a)
        _require(all(len(values) == 2 for values in adjacent.values()), "Generated side chart has a non-loop boundary")
        start = min(adjacent, key=lambda vertex: tuple(vertex.co))
        loop, previous, current = [start], None, start
        while True:
            following = min((vertex for vertex in adjacent[current] if vertex != previous), key=lambda vertex: tuple(vertex.co))
            if following == start:
                break
            loop.append(following)
            previous, current = current, following
        _require(len(loop) == len(adjacent), "Generated side chart has several boundary loops")
        coordinates = [Vector((vertex.co.y, vertex.co.z)) for vertex in vertices]
        boundary = [indices[vertex] for vertex in loop]
        signed = sum(coordinates[a].x * coordinates[b].y - coordinates[b].x * coordinates[a].y for a, b in zip(boundary, boundary[1:] + boundary[:1]))
        if signed < 0:
            boundary.reverse()
        _, _, triangles, originals, _, _ = delaunay_2d_cdt(
            coordinates, sorted(tuple(sorted((indices[edge.verts[0]], indices[edge.verts[1]]))) for edge in rim),
            [boundary], 1, 1e-7, True,
        )
        _require(all(len(ids) == 1 for ids in originals), "Planar recess boundary crossed itself")
        mapping = [vertices[ids[0]] for ids in originals]
        count = len(chart)
        bmesh.ops.delete(bm, geom=list(chart), context="FACES_ONLY")
        rebuilt = []
        for triangle in triangles:
            face = bm.faces.new([mapping[index] for index in (reversed(triangle) if sign < 0 else triangle)])
            face[cap], face[original], face.smooth = 2, 0, True
            rebuilt.append(face)
        paint(rebuilt)
        retopology.append({"side": "R" if sign < 0 else "L", "before_faces": count, "after_faces": len(rebuilt)})
    loose = [edge for edge in bm.edges if not edge.link_faces]
    if loose:
        bmesh.ops.delete(bm, geom=loose, context="EDGES")
    bmesh.ops.triangulate(bm, faces=[face for face in bm.faces if len(face.verts) > 3])
    bm.normal_update()
    for face in bm.faces:
        if face[cap] == 2:
            for loop in face.loops:
                loop[normal] = loop.vert.normal
    _require(not any(not edge.is_manifold for edge in bm.edges), "Generated recess opened the closed pack")
    folded = sum(face[cap] == 2 and ((all(vertex.co.x < -0.4949 for vertex in face.verts) and face.normal.x > 0)
                 or (all(vertex.co.x > 0.2849 for vertex in face.verts) and face.normal.x < 0)) for face in bm.faces)
    _require(folded == 0, "Generated planar side still has folded triangles")
    report = {
        "scope": "IndependentHiddenCap==2 generated leather only",
        "vertices_before": before_vertices, "triangles_before": before_faces,
        "vertices_after": len(bm.verts), "triangles_after": len(bm.faces),
        "modified_generated_vertices": len(changes),
        "maximum_recess_depth": max(b.y - a.y for a, b in changes),
        "original_changed_bounds": [np.asarray([a[:] for a, _ in changes]).min(0).tolist(), np.asarray([a[:] for a, _ in changes]).max(0).tolist()],
        "protected_source_and_retained_contact_faces": protected_faces,
        "protected_source_and_retained_contact_delta": 0.0,
        "boundary_edges": 0, "nonmanifold_edges": 0, "folded_planar_side_faces": folded,
        "planar_retopology": retopology, "native_leather_paint": paint_reports,
    }
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    values = mesh.attributes["ShoulderRecessKeptNormal"]
    mesh.normals_split_custom_set([item.vector for item in values.data])
    mesh.attributes.remove(values)
    _require(_protected_pack_signature(pack)[0] == protected, "Recess changed protected case/contact geometry, UVs or weights")
    report["protected_signature"] = protected
    return report


def audit(rig, meshes):
    """Check real evaluated arm skins at rest and all accepted wave extremes.

    Complete paws must fit between the real strap boundaries at shoulder
    height and clear actual pack geometry. Proximal capsule samples must cross
    the torso surface, proving attachment instead of accepting a hidden gap.
    """
    roles = {obj.get("mesh_role"): obj for obj in meshes}
    body, pack = roles["Character"], roles["Pack"]
    tree = _body_bvh(body)
    pack_points = _points(pack)
    pack_tree = BVHTree.FromPolygons([Vector(point) for point in pack_points],
                                    [face.vertices for face in pack.data.polygons], all_triangles=True)
    saved_pose = {bone.name: bone.matrix_basis.copy() for bone in rig.pose.bones}
    result = {"revision": REVISION, "shoulders": {}, "poses": {}}
    try:
        for bone in rig.pose.bones:
            bone.matrix_basis = Matrix.Identity(4)
        bpy.context.view_layer.update()
        for side in ("L", "R"):
            bone = rig.data.bones["arm." + side]
            result["shoulders"][side] = _alignment(tree, pack, bone.head_local, side)
            result["shoulders"][side]["harness_aperture"] = _strap_aperture(roles["Harness"], side, bone.head_local.z)
            _require(
                PIVOT_EMBED - EPSILON <= result["shoulders"][side]["torso_lateral_embed"] <= PIVOT_EMBED + EPSILON,
                f"Shoulder {side} is not embedded in the torso side",
            )
            _require(result["shoulders"][side]["torso_signed_distance"] < 0, f"Shoulder {side} is outside torso")
        for angle in (-10, 0, 30, 60):
            for side in ("L", "R"):
                bone = rig.pose.bones["arm." + side]
                sign = 1 if side == "L" else -1
                axis = bone.bone.matrix_local.to_quaternion().inverted() @ Vector((0, 1, 0))
                bone.rotation_mode = "QUATERNION"
                bone.rotation_quaternion = Quaternion(axis, math.radians(-sign * angle))
            bpy.context.view_layer.update()
            row = {}
            for side in ("L", "R"):
                obj = roles["Arm." + side]
                head = rig.data.bones["arm." + side].head_local
                rest = _points(obj)
                edges = np.asarray([edge.vertices[:] for edge in obj.data.edges])
                baseline_lengths = np.linalg.norm(rest[edges[:, 0]] - rest[edges[:, 1]], axis=1)
                evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
                data = evaluated.to_mesh()
                try:
                    actual = np.asarray([vertex.co[:] for vertex in data.vertices])
                finally:
                    evaluated.to_mesh_clear()
                _require(len(actual) == len(rest), f"Evaluated topology changed on {obj.name}")
                edge_error = float(np.max(np.abs(np.linalg.norm(actual[edges[:, 0]] - actual[edges[:, 1]], axis=1) - baseline_lengths)))
                pack_check = _pack_clearance(actual, edges, [face.vertices for face in obj.data.polygons], pack_points, pack_tree)
                aperture = result["shoulders"][side]["harness_aperture"]
                strap_margin = min(float(actual[:, 1].min()) - aperture["front"], aperture["rear"] - float(actual[:, 1].max()))
                # The first 0.07 units beyond the hinge include the proximal
                # hemisphere of the accepted capsule, never the distal paw.
                axis = np.asarray(obj["paw_axis"])
                proximal = (rest - np.asarray(head)) @ axis < 0.07
                signed = [_signed_distance(tree, Vector(point)) for point in actual[proximal]]
                inside = sum(distance < 0 for distance in signed)
                outside = sum(distance > 0 for distance in signed)
                _require(pack_check["minimum_vertex_surface_distance"] >= MIN_PACK_GAP and pack_check["arm_edge_pack_crossings"] == 0 and pack_check["arm_vertices_inside_pack"] == 0,
                         f"Paw {side} intersects actual backpack at {angle} degrees: {pack_check}")
                _require(strap_margin >= MIN_STRAP_MARGIN, f"Paw {side} crosses the harness aperture at {angle} degrees: {strap_margin}")
                _require(edge_error < EPSILON, f"Paw {side} stretches at {angle} degrees: {edge_error}")
                _require(inside >= 20 and outside >= 20, f"Proximal paw {side} does not cross torso surface at {angle} degrees")
                _require(min(signed) < -0.015, f"Paw {side} has insufficient torso contact at {angle} degrees")
                row[side] = {
                    "pack_clearance": pack_check,
                    "harness_aperture_margin": strap_margin,
                    "maximum_edge_length_change": edge_error,
                    "proximal_vertices_inside_torso": inside,
                    "proximal_vertices_outside_torso": outside,
                    "proximal_deepest_contact": min(signed),
                    "proximal_nearest_surface_distance": min(abs(distance) for distance in signed),
                }
            result["poses"][str(angle)] = row
    finally:
        for name, matrix in saved_pose.items():
            rig.pose.bones[name].matrix_basis = matrix
        bpy.context.view_layer.update()
    return result


def apply(rig, meshes):
    """Translate Lumen's two rest bones and complete capsule meshes together.

    Fresh inputs must retain the known pre-adjustment pivots. A revision stamp
    makes repeat calls a validated no-op and rejects partial/double movement.
    The returned JSON-compatible report is suitable for the caller's manifest.
    """
    meshes = list(meshes)
    roles = {obj.get("mesh_role"): obj for obj in meshes}
    _require(set(roles) == {"Character", "Pack", "Harness", "Arm.L", "Arm.R"}, "Expected Lumen's five registered skins")
    _require(roles["Character"].name.startswith("Lumen_"), "This shoulder adjustment is specific to Lumen")
    _require(bpy.context.mode == "OBJECT", "Shoulder adjustment requires Object mode")
    _require(all(np.max(np.abs(np.asarray(obj.matrix_world) - np.asarray(rig.matrix_world))) < EPSILON for obj in meshes), "Rig and meshes must share authoring coordinates")
    _require(all(np.max(np.abs(np.asarray(bone.matrix_basis) - np.eye(4))) < EPSILON for bone in rig.pose.bones), "Reset the pose before changing shoulder rest bones")
    if rig.get(STAMP):
        stored = json.loads(rig[STAMP])
        _require(stored.get("revision") == REVISION, "Unknown shoulder adjustment already applied")
        for side in ("L", "R"):
            actual = rig.data.bones["arm." + side].head_local
            _require((actual - Vector(stored["translations"][side]["new_head"])).length < EPSILON, "Stamped shoulder pivot moved unexpectedly")
        result = audit(rig, meshes)
        result["already_applied"] = True
        result["translations"] = stored["translations"]
        result["generated_pack_recess"] = stored["generated_pack_recess"]
        _require(_protected_pack_signature(roles["Pack"])[0] == stored["generated_pack_recess"]["protected_signature"], "Stamped protected case changed")
        return result

    old_bones = _bone_snapshot(rig)
    original = {role: _points(obj) for role, obj in roles.items()}
    original_keys = {
        role: {key.name: np.asarray([item.co[:] for item in key.data]) for key in obj.data.shape_keys.key_blocks}
        for role, obj in roles.items() if obj.data.shape_keys
    }
    signatures = {role: _skin_signature(obj) for role, obj in roles.items()}
    original_normals = {
        role: np.asarray([item.vector[:] for item in obj.data.corner_normals])
        for role, obj in roles.items()
    }
    tree, pack = _body_bvh(roles["Character"]), roles["Pack"]
    translations, before = {}, {}
    for side in ("L", "R"):
        bone = rig.data.bones["arm." + side]
        _require((bone.head_local - Vector(ORIGINAL_HEADS[side])).length < EPSILON, f"Unexpected original shoulder {side}; refusing to accumulate translation")
        _require(not bone.children and not bone.use_connect, f"Unexpected connected shoulder hierarchy {side}")
        obj = roles["Arm." + side]
        group = obj.vertex_groups["arm." + side].index
        _require(all(len(vertex.groups) == 1 and vertex.groups[0].group == group and abs(vertex.groups[0].weight - 1) < 1e-7 for vertex in obj.data.vertices), f"Paw {side} is not rigidly weighted")
        before[side] = _alignment(tree, pack, bone.head_local, side)
        target = bone.head_local + Vector((0, -FORWARD_SHIFT, 0))
        section = _alignment(tree, pack, target, side)
        sign = 1 if side == "L" else -1
        target.x = section["torso_side_surface"][0] - sign * PIVOT_EMBED
        delta = target - bone.head_local
        translations[side] = {"delta": list(delta), "old_head": list(bone.head_local), "new_head": list(target), "old_tail": list(bone.tail_local), "new_tail": list(bone.tail_local + delta)}

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for side, item in translations.items():
        bone = rig.data.edit_bones["arm." + side]
        delta = Vector(item["delta"])
        bone.head += delta
        bone.tail += delta
    bpy.ops.object.mode_set(mode="OBJECT")
    for side, item in translations.items():
        obj = roles["Arm." + side]
        delta = Vector(item["delta"])
        if obj.data.shape_keys:
            for key in obj.data.shape_keys.key_blocks:
                for vertex in key.data:
                    vertex.co += delta
        else:
            for vertex in obj.data.vertices:
                vertex.co += delta
        obj["paw_equator"] = list(Vector(obj["paw_equator"]) + delta)
        obj.data.update()
    bpy.context.view_layer.update()

    pack_recess = _recess_generated_lid(rig, roles)
    bpy.context.view_layer.update()

    preservation = {}
    for role, obj in roles.items():
        if role == "Pack":
            preservation[role] = {"authorized_generated_lid_recess": True,
                                  "protected_source_and_retained_contact_delta": 0.0,
                                  "protected_geometry_uv_weights_unchanged": True}
            continue
        delta = np.asarray(translations[role[-1]]["delta"]) if role.startswith("Arm.") else np.zeros(3)
        error = float(np.max(np.abs(_points(obj) - original[role] - delta)))
        _require(error < EPSILON, f"Unexpected geometry change on {role}: {error}")
        current = _skin_signature(obj)
        _require(current == signatures[role], f"Protected skin attributes changed on {role}")
        normal_error = float(np.max(np.abs(np.asarray([item.vector[:] for item in obj.data.corner_normals]) - original_normals[role])))
        _require(normal_error < 0.0001, f"Corner normals changed on {role}: {normal_error}")
        keys = {}
        for key in obj.data.shape_keys.key_blocks if obj.data.shape_keys else []:
            key_error = float(np.max(np.abs(np.asarray([item.co[:] for item in key.data]) - original_keys[role][key.name] - delta)))
            _require(key_error < EPSILON, f"Shape key changed on {role}/{key.name}")
            keys[key.name] = key_error
        preservation[role] = {"maximum_residual_from_rigid_translation": error, "maximum_corner_normal_component_change": normal_error, "protected_attributes_unchanged": True, "shape_key_residuals": keys}
    new_bones = _bone_snapshot(rig)
    unchanged = [name for name in old_bones if name not in ("arm.L", "arm.R")]
    _require(all(old_bones[name] == new_bones[name] for name in unchanged), "A non-arm rest bone changed")
    result = audit(rig, meshes)
    total = sum(len(obj.data.polygons) for obj in meshes)
    _require(total <= 30000, f"Complete Lumen exceeded the 30k budget: {total}")
    result.update({"already_applied": False, "translations": translations, "before": before, "preservation": preservation, "unchanged_rest_bones": unchanged, "unchanged_roles": ["Character", "Harness"], "all_other_roles_maximum_delta": 0.0, "generated_pack_recess": pack_recess, "total_triangles": total})
    rig[STAMP] = json.dumps({"revision": REVISION, "translations": translations, "generated_pack_recess": pack_recess}, sort_keys=True)
    return result
