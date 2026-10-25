"""Local Nori case-contact repair, after cloth fitting and before Harness fitting.

The original case lip forced the new cloth through a 126-degree folded wall.
Only its classified contact anchors and adjacent new cloth follow the already
accepted torso field. Main case, scarf, head, feet, UV and skin weights stay fixed.
"""

from __future__ import annotations

import numpy as np
from mathutils.bvhtree import BVHTree

ANCHORS = frozenset((2681, 2688, 5370, 5378, 5380, 5382, 5383, 5384, 5387))
CONTACT_FACES = frozenset(
    (2137, 2138, 2140, 2143, 11731, 11732, 11733, 11736, 11739, 11746, 11747, 11758, 11759)
)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def smooth(value):
    value = max(0.0, min(1.0, value))
    return value**3 * (value * (value * 6 - 15) + 10)


def repair_rear_contact(character, body, torso_report):
    if character != "Nori":
        return {"character": character, "moved_vertices": 0}
    mesh = body.data
    vertex_ids = mesh.attributes["OriginalSurfaceVertex"]
    face_ids = mesh.attributes["OriginalSurfaceFace"]
    parts = mesh.attributes["TripoSurfacePart"]
    before = np.asarray([v.co[:] for v in mesh.vertices])
    normals = np.asarray([n.vector[:] for n in mesh.corner_normals])
    shape_before = (
        {key.name: np.asarray([v.co[:] for v in key.data]) for key in mesh.shape_keys.key_blocks}
        if mesh.shape_keys
        else {}
    )
    protected = {
        i
        for face in mesh.polygons
        if face_ids.data[face.index].value - 1 == 11743 or parts.data[face.index].value in (5, 6)
        for i in face.vertices
    }
    groups = {group.index: group.name for group in body.vertex_groups}
    protected |= {
        v.index
        for v in mesh.vertices
        if any(
            (groups[item.group].startswith("foot.") or groups[item.group] == "head")
            and item.weight > 0.01
            for item in v.groups
        )
    }
    params = torso_report["patches"]["-1"]["source_fit"]
    coefficients = np.asarray(params["coefficients"])
    origin = np.asarray(params["origin_yz"])
    moved = set()
    for vertex in mesh.vertices:
        if vertex.index in protected:
            continue
        point = vertex.co.copy()
        source = vertex_ids.data[vertex.index].value - 1
        if source in ANCHORS:
            amount = 1.0
        elif source < 0 and point.x < -0.4 and 0.33 < point.y < 0.52 and 0.74 < point.z < 0.91:
            amount = (
                smooth((point.y - 0.33) / 0.07)
                * smooth((point.z - 0.74) / 0.04)
                * smooth((0.91 - point.z) / 0.04)
            )
        else:
            continue
        a, b = (np.asarray(point[1:]) - origin) / 0.55
        basis = np.asarray(
            (1, a, b, a * a, a * b, b * b, a * a * a, a * a * b, a * b * b, b * b * b)
        )
        delta = (float(basis @ coefficients) - point.x) * amount
        if abs(delta) < 1e-8:
            continue
        vertex.co.x += delta
        moved.add(vertex.index)
        if mesh.shape_keys:
            for key in mesh.shape_keys.key_blocks:
                key.data[vertex.index].co.x = shape_before[key.name][vertex.index, 0] + delta
    mesh.update()
    changed_faces = {face.index for face in mesh.polygons if any(i in moved for i in face.vertices)}
    affected_source = {
        face_ids.data[i].value - 1 for i in changed_faces if face_ids.data[i].value > 0
    }
    require(
        affected_source <= CONTACT_FACES,
        f"Rear repair escaped its classified case-contact lip: {affected_source - CONTACT_FACES}",
    )
    average = np.zeros_like(before)
    for face in mesh.polygons:
        for index in face.vertices:
            average[index] += np.asarray(face.normal[:]) * face.area
    average /= np.maximum(np.linalg.norm(average, axis=1)[:, None], 1e-20)
    for face in mesh.polygons:
        if face.index not in changed_faces:
            continue
        if face_ids.data[face.index].value - 1 == 11743 or parts.data[face.index].value in (5, 6):
            continue
        for loop in face.loop_indices:
            normals[loop] = average[mesh.loops[loop].vertex_index]
    mesh.normals_split_custom_set(normals.tolist())
    after = np.asarray([v.co[:] for v in mesh.vertices])
    require(
        np.array_equal(before[sorted(protected)], after[sorted(protected)]),
        "Protected Nori anatomy moved",
    )
    patch_side = mesh.attributes["SmoothTorsoPatchSide"]
    final = {}
    for sign in (-1, 1):
        faces = [f for f in mesh.polygons if patch_side.data[f.index].value == sign]
        final[str(sign)] = {
            "faces": len(faces),
            "world_area": sum(f.area for f in faces),
            "opposite_x_area": sum(f.area for f in faces if f.normal.x * sign < 0),
        }
    return {
        "revision": "nori-classified-rear-contact-v1",
        "moved_vertices": len(moved),
        "moved_source_vertices": sorted(
            vertex_ids.data[i].value - 1 for i in moved if vertex_ids.data[i].value > 0
        ),
        "affected_source_faces": sorted(affected_source),
        "maximum_move": float(np.max(np.abs(after - before))),
        "main_case_scarf_head_feet_position_delta": 0,
        "final_patch_geometry": final,
        "prior_patch_measurements_stage": "before local rear-contact repair",
    }


def finish_harness_contact(character, body, harness):
    if character != "Nori":
        return {"character": character, "adjusted": False}
    mesh = harness.data
    # The visible rear branch crosses the body and continues inside it; its
    # upper boundary must not terminate as a free-standing leather tongue.
    for vertex in mesh.vertices:
        p = vertex.co
        if p.x < 0:
            p.x += 0.12 * smooth((p.y - 0.34) / 0.12) * smooth((p.z - 0.72) / 0.11)
    mesh.update()
    cloth = body.data
    parts = cloth.attributes["TripoSurfacePart"]
    sides = cloth.attributes["SmoothTorsoPatchSide"]
    triangles = [
        tuple(face.vertices)
        for face in cloth.polygons
        if parts.data[face.index].value == 2 or sides.data[face.index].value in (-1, 1)
    ]
    tree = BVHTree.FromPolygons([v.co for v in cloth.vertices], triangles, all_triangles=True)
    start = np.asarray([v.co[:] for v in mesh.vertices])
    initial = {}
    for iteration in range(3):
        for vertex in mesh.vertices:
            p = vertex.co
            if not (p.x < 0 and 0.24 < p.y < 0.48 and 0.30 < p.z < 0.50):
                continue
            point, normal, face, distance = tree.find_nearest(p)
            if point is None or distance > 0.04 or normal.x > -0.25:
                continue
            signed = (p - point).dot(normal)
            initial.setdefault(vertex.index, signed)
            deficit = max(0.0, 0.007 - signed)
            if deficit > 0:
                available = max(
                    0.0, 0.020 - float(np.linalg.norm(np.asarray(p) - start[vertex.index]))
                )
                vertex.co += normal * min(deficit, available)
        mesh.update()
    after = []
    for index in initial:
        point, normal, face, distance = tree.find_nearest(mesh.vertices[index].co)
        after.append((mesh.vertices[index].co - point).dot(normal))
    maximum = float(
        np.max(np.linalg.norm(np.asarray([v.co[:] for v in mesh.vertices]) - start, axis=1))
    )
    profile_clearance = finish_visible_face_clearance(body, harness)
    return {
        "adjusted": True,
        "rear_endpoint_inset_applied": True,
        "lower_clearance_sample_count": len(initial),
        "lower_clearance_before": min(initial.values(), default=None),
        "lower_clearance_after": min(after, default=None),
        "maximum_lower_outward_offset": maximum,
        "right_harness_geometry_changed": False,
        "visible_profile_clearance": profile_clearance,
    }


def finish_visible_face_clearance(body, harness):
    """Keep the lower visible ribbon faces above cloth, including their interiors."""
    cloth = body.data
    parts = cloth.attributes["TripoSurfacePart"]
    sides = cloth.attributes["SmoothTorsoPatchSide"]
    triangles = [
        tuple(face.vertices)
        for face in cloth.polygons
        if parts.data[face.index].value == 2 or sides.data[face.index].value in (-1, 1)
    ]
    tree = BVHTree.FromPolygons([v.co for v in cloth.vertices], triangles, all_triangles=True)
    mesh = harness.data
    original = np.asarray([v.co[:] for v in mesh.vertices])

    def selected(face):
        p = face.center
        return p.x < 0 and 0.23 < p.y < 0.49 and 0.29 < p.z < 0.51

    faces = [face for face in mesh.polygons if selected(face)]

    def samples(face):
        a, b, c = [mesh.vertices[i].co for i in face.vertices]
        return (a, b, c, (a + b) * 0.5, (b + c) * 0.5, (c + a) * 0.5, (a + b + c) / 3)

    def margin(point):
        from mathutils import Vector

        skin, normal, index, distance = tree.ray_cast(
            Vector((-3.0, point.y, point.z)), Vector((1.0, 0.0, 0.0)), 6.0
        )
        return skin.x - point.x if skin is not None else None

    before = [m for face in faces for p in samples(face) if (m := margin(p)) is not None]
    for _ in range(3):
        required = {}
        for face in faces:
            deficits = [max(0.0, 0.010 - m) for p in samples(face) if (m := margin(p)) is not None]
            shift = max(deficits, default=0.0)
            for index in face.vertices:
                required[index] = max(required.get(index, 0.0), shift)
        if max(required.values(), default=0.0) < 1e-7:
            break
        for index, shift in required.items():
            available = max(0.0, 0.040 - abs(mesh.vertices[index].co.x - original[index, 0]))
            mesh.vertices[index].co.x -= min(shift, available)
        mesh.update()
    after = [m for face in faces for p in samples(face) if (m := margin(p)) is not None]
    return {
        "sampled_faces": len(faces),
        "samples_per_face": 7,
        "minimum_visible_x_clearance_before": min(before, default=None),
        "minimum_visible_x_clearance_after": min(after, default=None),
        "maximum_outward_x_shift": float(
            np.max(np.abs(np.asarray([v.co[:] for v in mesh.vertices]) - original))
        ),
    }
