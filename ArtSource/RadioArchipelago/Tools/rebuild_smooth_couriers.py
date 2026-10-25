"""Mirror the good distal paw surface into a genuinely round double-ended paw.

By default only authoring candidates in Library are changed. Explicit
--torso --publish promotes a complete strictly verified package to Processed;
Unity Assets are never changed. Immutable 87bc426 inputs come from Git and are
verified in SmoothCourierReview/Before before each build.
"""

from __future__ import annotations
import argparse, hashlib, json, math, pathlib, subprocess, sys, tempfile
import bpy, bmesh, numpy as np
from mathutils import Vector, Quaternion
from mathutils.bvhtree import BVHTree

ROOT = pathlib.Path(__file__).resolve().parents[3]
REVIEW = ROOT / "DancingCatsClone/Library/RadioRigging/SmoothCourierReview"
PROCESSED = ROOT / "ArtSource/RadioArchipelago/Processed"
BASELINE_REVISION = "87bc426"
REVISION = "rounded-couriers-anatomical-joints-v2"
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from prepare_radio_rigs import render_setup, aim, extract_source
from export_prepared_rigs import reset_pose


def require(value, message):
    if not value:
        raise RuntimeError(message)


def convex_area(points, centroid=False):
    values = sorted(set((round(float(x), 8), round(float(y), 8)) for x, y in points))
    if len(values) < 3:
        return (0.0, (0.0, 0.0)) if centroid else 0.0

    def cross(a, b, c):
        return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])

    lower = []
    upper = []
    for point in values:
        while len(lower) > 1 and cross(lower[-2], lower[-1], point) <= 0:
            lower.pop()
        lower.append(point)
    for point in reversed(values):
        while len(upper) > 1 and cross(upper[-2], upper[-1], point) <= 0:
            upper.pop()
        upper.append(point)
    polygon = lower[:-1] + upper[:-1]
    cross_values = [a[0] * b[1] - a[1] * b[0] for a, b in zip(polygon, polygon[1:] + polygon[:1])]
    signed = sum(cross_values)
    area = abs(signed) * 0.5
    if not centroid:
        return area
    if abs(signed) < 1e-12:
        return area, np.mean(polygon, axis=0)
    cu = sum(
        (a[0] + b[0]) * cross
        for a, b, cross in zip(polygon, polygon[1:] + polygon[:1], cross_values)
    ) / (3 * signed)
    cv = sum(
        (a[1] + b[1]) * cross
        for a, b, cross in zip(polygon, polygon[1:] + polygon[:1], cross_values)
    ) / (3 * signed)
    return area, (cu, cv)


def source_snapshot(name):
    folder = REVIEW / "Before" / name
    folder.mkdir(parents=True, exist_ok=True)
    for filename in (f"{name}_Rigged.blend", "Model.fbx", "manifest.json"):
        path = folder / filename
        reference = f"{BASELINE_REVISION}:ArtSource/RadioArchipelago/Processed/{name}/{filename}"
        if not path.exists():
            result = subprocess.run(
                ["git", "show", reference], cwd=ROOT, capture_output=True, check=True
            )
            path.write_bytes(result.stdout)
        expected = subprocess.run(
            ["git", "rev-parse", reference], cwd=ROOT, capture_output=True, text=True, check=True
        ).stdout.strip()
        payload = path.read_bytes()
        if path.suffix == ".json":
            payload = payload.replace(b"\r\n", b"\n")
        actual = hashlib.sha1(f"blob {len(payload)}\0".encode() + payload).hexdigest()
        require(actual == expected, f"Immutable baseline changed: {path}")
    return folder


def mirror_distal(name, obj, rig):
    """Use real source distal geometry/UV; weld its reflected half at a ring."""
    mesh = obj.data
    side = obj["mesh_role"][-1]
    bone = rig.data.bones["arm." + side]
    head = bone.head_local.copy()
    ids = mesh.attributes.get("OriginalSurfaceFace")
    good = [polygon for polygon, item in zip(mesh.polygons, ids.data) if item.value > 0]
    require(good, "Original distal surface is not labelled")
    points = np.array([mesh.vertices[i].co[:] for polygon in good for i in polygon.vertices])
    weights = np.array(
        [polygon.area / len(polygon.vertices) for polygon in good for _ in polygon.vertices]
    )
    weights /= weights.sum()
    center = (points * weights[:, None]).sum(0)
    cov = (points - center).T @ ((points - center) * weights[:, None])
    eigenvalues, eigenvectors = np.linalg.eigh(cov)
    axis = Vector(eigenvectors[:, -1])
    if axis.dot(Vector(center) - head) < 0:
        axis = -axis
    projected = (points - center) @ np.array(axis)
    low, high = np.quantile(projected, [0.002, 0.998])
    u = axis.cross(Vector((0, 1, 0))).normalized()
    v = axis.cross(u).normalized()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    normal = bm.loops.layers.float_vector.new("DistalNormal")
    old_face = bm.faces.layers.int.get("OriginalSurfaceFace")
    for face, polygon in zip(bm.faces, mesh.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[normal] = mesh.corner_normals[index].vector
    bmesh.ops.delete(bm, geom=[face for face in bm.faces if face[old_face] == 0], context="FACES")
    # The old supplied paw is fused underneath almost to the tip. Keep only
    # its genuinely complete round tip, rather than retaining any root rim.
    old_boundary = {vertex for edge in bm.edges if edge.is_boundary for vertex in edge.verts}
    limit = (
        max((vertex.co - Vector(center)).dot(axis) for vertex in old_boundary) + 0.006
        if old_boundary
        else float(low)
    )
    scores = []
    for distance in np.linspace(
        max(float(low) + (high - low) * 0.20, limit), float(high) - 0.045, 48
    ):
        section = []
        for edge in bm.edges:
            a, b = edge.verts
            ta = (a.co - Vector(center)).dot(axis) - distance
            tb = (b.co - Vector(center)).dot(axis) - distance
            if ta * tb < 0:
                point = a.co.lerp(b.co, ta / (ta - tb))
                section.append((point.dot(u), point.dot(v)))
        scores.append((convex_area(section), float(distance)))
    cut = max(scores)[1]
    require(high - cut > 0.04, "No complete round source tip remains")
    plane = Vector(center) + axis * cut
    bmesh.ops.bisect_plane(
        bm,
        geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=1e-7,
        plane_co=plane,
        plane_no=axis,
        clear_inner=True,
        clear_outer=False,
    )
    bm.normal_update()
    boundary = {vertex for edge in bm.edges if edge.is_boundary for vertex in edge.verts}
    require(len(boundary) > 8, "Distal bisect did not make a proper equator")
    require(
        all(abs((vertex.co - plane).dot(axis)) < 0.00001 for vertex in boundary),
        f"Old jagged root reached distal half {side}: axis {axis[:]}, range {low,high}, plane {cut}, boundary distances {sorted((vertex.co-plane).dot(axis)for vertex in boundary)[-12:]}",
    )
    # Straighten the source finger's curved centreline before reflecting it.
    # Otherwise two curved good tips make an S/peanut despite radial symmetry.
    raw_height = max((vertex.co - plane).dot(axis) for vertex in bm.verts)
    samples = []
    for t in np.linspace(0, raw_height - 0.0005, 20):
        section = []
        for edge in bm.edges:
            a, b = edge.verts
            ta = (a.co - plane).dot(axis) - t
            tb = (b.co - plane).dot(axis) - t
            if abs(ta) < 1e-6:
                section.append((a.co.dot(u), a.co.dot(v)))
            if ta * tb < 0:
                point = a.co.lerp(b.co, ta / (ta - tb))
                section.append((point.dot(u), point.dot(v)))
        area, position = convex_area(section, True)
        if area > 1e-9:
            samples.append((float(t), *position))
    require(len(samples) > 8, "Source tip centreline is incomplete")
    samples = np.array(samples)
    base_u, base_v = samples[0, 1:]
    plane += u * (base_u - plane.dot(u)) + v * (base_v - plane.dot(v))
    for vertex in bm.verts:
        t = (vertex.co - plane).dot(axis)
        cu = float(np.interp(t, samples[:, 0], samples[:, 1]))
        cv = float(np.interp(t, samples[:, 0], samples[:, 2]))
        vertex.co -= u * (cu - base_u) + v * (cv - base_v)
    angles = []
    radii = []
    raw_half = [vertex.co - plane for vertex in bm.verts]
    raw_angles = np.array([math.atan2(offset.dot(v), offset.dot(u)) for offset in raw_half])
    raw_radii = np.array([(offset - axis * offset.dot(axis)).length for offset in raw_half])
    for vertex in boundary:
        radial = vertex.co - plane
        angle = math.atan2(radial.dot(v), radial.dot(u))
        angles.append(angle)
        nearby = np.abs((raw_angles - angle + math.pi) % (2 * math.pi) - math.pi) < math.pi / 10
        radii.append(max(radial.length, float(raw_radii[nearby].max())) * 1.005)
    order = np.argsort(angles)
    angles = np.array(angles)[order]
    radii = np.array(radii)[order]
    xp = np.r_[angles[-1] - 2 * math.pi, angles, angles[0] + 2 * math.pi]
    fp = np.r_[radii[-1], radii, radii[0]]
    distal_vertices = list(bm.verts)
    half_length = max((vertex.co - plane).dot(axis) for vertex in distal_vertices)
    transition = min(0.11, half_length * 0.52)
    for vertex in distal_vertices:
        offset = vertex.co - plane
        t = offset.dot(axis)
        radial = offset - axis * t
        if t < transition and radial.length > 1e-8:
            angle = math.atan2(radial.dot(v), radial.dot(u))
            radius = float(np.interp(angle, xp, fp))
            factor = max(0, min(1, t / transition))
            blend = factor * factor * (3 - 2 * factor)
            vertex.co = (
                plane + axis * t + radial.normalized() * (radius + (radial.length - radius) * blend)
            )
    desired = max(0.43, float(high - low))
    scale = desired / (2 * half_length)
    # Some fused source fingertips are too short or contain a contact bump.
    # Re-form their retained half as a smooth rounded cylinder before the
    # reflection, preserving the actual surface connectivity and its UV paint.
    # The two halves still meet at one shared ring, without a UV strip or cap.
    if name in ("Tiko", "Lumen"):
        ring = np.array(
            [[(vertex.co - plane).dot(u), (vertex.co - plane).dot(v)] for vertex in boundary]
        )
        cov = ring.T @ ring / len(ring)
        values, vectors = np.linalg.eigh(cov)
        radii2 = np.sqrt(np.maximum(values * 2, 1e-6))
        target_half = desired * 0.5
        tip_height = min(target_half * 0.64, float(np.sqrt(np.prod(radii2))) * 1.05)
        straight = max(0.0, target_half - tip_height)
        for vertex in distal_vertices:
            offset = vertex.co - plane
            t = max(0.0, offset.dot(axis))
            radial = offset - axis * t
            if radial.length < 1e-7:
                continue
            xy = np.array([radial.dot(u), radial.dot(v)])
            direction = xy / np.linalg.norm(xy)
            local = vectors.T @ direction
            radius = 1 / math.sqrt(float(np.sum((local / radii2) ** 2)))
            target_t = t / half_length * target_half
            rounded = max(0.0, (target_t - straight) / tip_height)
            profile = math.sqrt(max(0.0, 1 - rounded * rounded))
            # A tiny nonzero pole preserves distinct original vertices without
            # creating zero-area faces; Subsurf closes the rounded outline.
            vertex.co = plane + axis * t + radial.normalized() * max(0.00005, radius * profile)
    distal_faces = list(bm.faces)
    mirrored = {}
    for vertex in distal_vertices:
        mirrored[vertex] = (
            vertex
            if vertex in boundary
            else bm.verts.new(vertex.co - axis * (2 * (vertex.co - plane).dot(axis)))
        )
    uv = bm.loops.layers.uv.active
    for face in distal_faces:
        reflected = bm.faces.new([mirrored[vertex] for vertex in reversed(face.verts)])
        reflected.smooth = True
        for loop, original in zip(reflected.loops, reversed(list(face.loops))):
            loop[uv].uv = original[uv].uv
            value = original[normal]
            loop[normal] = value - axis * (2 * value.dot(axis))
    bm.normal_update()
    for face in bm.faces:
        face.smooth = True
        for loop in face.loops:
            distance = abs((loop.vert.co - plane).dot(axis))
            if distance < transition:
                blend = distance / transition
                blend = blend * blend * (3 - 2 * blend)
                loop[normal] = loop.vert.normal.lerp(loop[normal], blend).normalized()
            if loop.vert in boundary or loop.vert in {mirrored[vertex] for vertex in boundary}:
                n = loop[normal]
                n -= axis * n.dot(axis)
                loop[normal] = n.normalized()
    require(all(len(edge.link_faces) == 2 for edge in bm.edges), "Mirrored paw is not closed")
    # Match the old paw's intended length, with its round proximal end buried
    # slightly past the anatomical shoulder. The end is no longer a cap plane.
    root_target = head - axis * 0.065
    for vertex in bm.verts:
        offset = vertex.co - plane
        t = offset.dot(axis)
        radial = offset - axis * t
        vertex.co = root_target + axis * (desired * 0.5 + t * scale) + radial
    for face in bm.faces:
        for loop in face.loops:
            n = loop[normal]
            axial = n.dot(axis)
            loop[normal] = (n - axis * axial + axis * (axial / scale)).normalized()
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    attribute = mesh.attributes["DistalNormal"]
    mesh.normals_split_custom_set([Vector(value.vector).normalized() for value in attribute.data])
    mesh.attributes.remove(attribute)
    for group in obj.vertex_groups:
        group.remove(list(range(len(mesh.vertices))))
    obj.vertex_groups["arm." + side].add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    obj["paw_construction"] = "actual-distal-half-mirrored-equator"
    obj["paw_axis"] = list(axis)
    obj["paw_equator"] = list(root_target + axis * (desired * 0.5))
    obj["paw_length"] = desired
    if name in ("Tiko", "Lumen"):
        obj["paw_ellipse_u"] = list(u * float(vectors[0, 0]) + v * float(vectors[1, 0]))
        obj["paw_ellipse_v"] = list(u * float(vectors[0, 1]) + v * float(vectors[1, 1]))
        obj["paw_ellipse_radii"] = radii2.tolist()
        obj["paw_tip_height"] = tip_height
    obj.name = name + "_Arm." + side + "_Rounded"
    return {
        "side": side,
        "length": desired,
        "source_half_length": half_length,
        "length_adjustment": scale,
        "equator_vertices": len(boundary),
        "vertices": len(mesh.vertices),
        "triangles": len(mesh.polygons),
        "axis": list(axis),
        "shoulder": list(head),
        "root_tip": list(root_target),
    }


def retopologize_folded_tip(obj):
    """Remove fused-contact folds using the measured source cross-section.

    A coherent existing navy-cloth atlas region supplies the genuine paint at
    the measured source density. The original 4K images remain unchanged.
    """
    source = obj.data
    axis = Vector(obj["paw_axis"])
    center = Vector(obj["paw_equator"])
    u = Vector(obj["paw_ellipse_u"])
    v = Vector(obj["paw_ellipse_v"])
    ru, rv = obj["paw_ellipse_radii"]
    half = obj["paw_length"] * 0.5
    tip = obj["paw_tip_height"]
    straight = half - tip
    points = []
    parameters = []
    rings = []
    segments = 32
    for t in np.linspace(0, half, 12)[:-1]:
        rounded = max(0.0, (float(t) - straight) / tip)
        profile = math.sqrt(max(0.0, 1 - rounded * rounded))
        ring = []
        for angle in np.linspace(0, 2 * math.pi, segments, endpoint=False):
            point = (
                center
                + axis * float(t)
                + (u * (ru * math.cos(angle)) + v * (rv * math.sin(angle))) * profile
            )
            ring.append(len(points))
            points.append(point)
            parameters.append((float(angle) / (2 * math.pi), float(t) / half))
        rings.append(ring)
    pole = len(points)
    points.append(center + axis * half)
    parameters.append((0.0, 1.0))
    faces = []
    for a, b in zip(rings, rings[1:]):
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((a[i], a[j], b[j], b[i]))
    for i in range(segments):
        faces.append((rings[-1][i], rings[-1][(i + 1) % segments], pole))
    # Reuse exactly the equator, reflecting every other UV-bearing vertex.
    mirrored = {i: i for i in rings[0]}
    for i, point in enumerate(list(points)):
        if i in mirrored:
            continue
        mirrored[i] = len(points)
        points.append(point - axis * (2 * (point - center).dot(axis)))
        parameters.append(parameters[i])
    faces.extend(tuple(mirrored[i] for i in reversed(face)) for face in list(faces))
    data = bpy.data.meshes.new(source.name + "_CleanTip")
    data.from_pydata(points, [], faces)
    data.update()
    layer = data.uv_layers.new(name=source.uv_layers.active.name)
    # The supplied paw has several atlas islands. Interpolating their UVs at
    # new retopology vertices crosses orange hardware elsewhere in the atlas.
    # Use one measured, genuine navy cloth rectangle for this repaired surface.
    from repair_smooth_torso_harness import atlas_patch, face_colors, uv_triangle_area, flesh_color

    colors = face_colors(obj)
    cloth = [
        face
        for face in source.polygons
        if flesh_color("Nori", colors[face.index]) and face.area > 1e-8
    ]
    ratios = [uv_triangle_area(source, face) / face.area for face in cloth]
    density = float(np.median([value for value in ratios if 0.0001 < value < 2.0]))
    circumference = 2 * math.pi * math.sqrt((ru * ru + rv * rv) * 0.5)
    area = sum(face.area for face in data.polygons)
    scale = math.sqrt(density * area / (2 * circumference * half))
    width, height = circumference * scale, half * scale
    origin, report = atlas_patch(
        "Nori", obj, width, height, np.median(colors[[face.index for face in cloth]], axis=0)
    )
    for face in data.polygons:
        values = [parameters[index] for index in face.vertices]
        angles = [a for a, t in values if t < 0.9999]
        seam = angles and max(angles) - min(angles) > 0.5
        unwrapped = [a + 1 if seam and a < 0.5 else a for a in angles]
        pole_angle = float(np.mean(unwrapped)) if unwrapped else 0.0
        for index, (a, t) in zip(face.loop_indices, values):
            if t > 0.9999:
                a = pole_angle
            elif seam and a < 0.5:
                a += 1
            layer.data[index].uv = origin + Vector((a * width, t * height))
    report["measured_source_uv_area_per_world_area"] = density
    report["repaired_uv_area_per_world_area"] = scale * scale * 2 * circumference * half / area
    obj["paw_retopology_paint"] = json.dumps(report)
    for material in source.materials:
        data.materials.append(material)
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj.data = data
    obj["paw_construction"] = "retopologized-source-half-mirrored-equator"


def matched_pair(name, paws, rig, reports):
    """Use the cleaner source half for both sides, keeping shoulder placements."""
    source_side = max(reports, key=lambda report: report["source_half_length"] / report["length"])[
        "side"
    ]
    source = next(o for o in paws if o["mesh_role"] == "Arm." + source_side)
    other = next(o for o in paws if o != source)
    other_side = other["mesh_role"][-1]
    if name == "Lumen":
        retopologize_folded_tip(source)
    bpy.context.view_layer.objects.active = source
    source.select_set(True)
    subdivision = source.modifiers.new("RoundedPawFinish", "SUBSURF")
    subdivision.levels = 1
    subdivision.render_levels = 1
    if hasattr(subdivision, "uv_smooth"):
        subdivision.uv_smooth = "PRESERVE_BOUNDARIES"
    if hasattr(subdivision, "use_custom_normals"):
        subdivision.use_custom_normals = False
    source.modifiers.move(len(source.modifiers) - 1, 0)
    bpy.ops.object.modifier_apply(modifier=subdivision.name)
    source.select_set(False)
    for polygon in source.data.polygons:
        polygon.use_smooth = True
    source.data.normals_split_custom_set([(0, 0, 0)] * len(source.data.loops))
    simplify_paw(source, 1700)
    for group in source.vertex_groups:
        group.remove(list(range(len(source.data.vertices))))
    group = source.vertex_groups.get("arm." + source_side) or source.vertex_groups.new(
        name="arm." + source_side
    )
    group.add(list(range(len(source.data.vertices))), 1.0, "REPLACE")
    source_head = rig.data.bones["arm." + source_side].head_local
    other_head = rig.data.bones["arm." + other_side].head_local
    data = source.data.copy()
    bm = bmesh.new()
    bm.from_mesh(data)
    normal = bm.loops.layers.float_vector.new("MatchedNormal")
    for face, polygon in zip(bm.faces, data.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            n = data.corner_normals[index].vector
            loop[normal] = Vector((-n.x, n.y, n.z))
    for vertex in bm.verts:
        p = vertex.co - source_head
        vertex.co = other_head + Vector((-p.x, p.y, p.z))
    bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    other.data = data
    attribute = data.attributes["MatchedNormal"]
    data.normals_split_custom_set([Vector(value.vector) for value in attribute.data])
    data.attributes.remove(attribute)
    for group in other.vertex_groups:
        group.remove(list(range(len(data.vertices))))
    group = other.vertex_groups.get("arm." + other_side) or other.vertex_groups.new(
        name="arm." + other_side
    )
    group.add(list(range(len(data.vertices))), 1.0, "REPLACE")
    other["paw_construction"] = "mirrored-clean-distal-template"
    other["paw_template_side"] = source_side
    other["paw_simplification"] = source["paw_simplification"]
    axis = Vector(source["paw_axis"])
    other["paw_axis"] = [-axis.x, axis.y, axis.z]
    other["paw_length"] = source["paw_length"]
    equator = Vector(source["paw_equator"]) - source_head
    other["paw_equator"] = list(other_head + Vector((-equator.x, equator.y, equator.z)))
    return source_side


def simplify_paw(obj, target_triangles):
    """Reduce the approved smooth surface, then restore exact half symmetry."""
    triangulate_paw(obj)
    original = [vertex.co.copy() for vertex in obj.data.vertices]
    original_faces = [tuple(face.vertices) for face in obj.data.polygons]
    original_tree = BVHTree.FromPolygons(original, original_faces, all_triangles=True)
    old_count = len(original_faces)
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    modifier = obj.modifiers.new("RoundedPawBudget", "DECIMATE")
    modifier.ratio = min(1.0, target_triangles / old_count)
    modifier.use_collapse_triangulate = True
    obj.modifiers.move(len(obj.modifiers) - 1, 0)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)
    mesh = obj.data
    axis = Vector(obj["paw_axis"])
    plane = Vector(obj["paw_equator"])
    bm = bmesh.new()
    bm.from_mesh(mesh)
    normal = bm.loops.layers.float_vector.new("BudgetNormal")
    for face, polygon in zip(bm.faces, mesh.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[normal] = mesh.corner_normals[index].vector
    bmesh.ops.bisect_plane(
        bm,
        geom=list(bm.verts) + list(bm.edges) + list(bm.faces),
        dist=1e-7,
        plane_co=plane,
        plane_no=axis,
        clear_inner=True,
        clear_outer=False,
    )
    bm.normal_update()
    boundary = {vertex for edge in bm.edges if edge.is_boundary for vertex in edge.verts}
    require(
        boundary and all(abs((vertex.co - plane).dot(axis)) < 1e-5 for vertex in boundary),
        "Simplification opened the distal surface",
    )
    for vertex in boundary:
        vertex.co -= axis * (vertex.co - plane).dot(axis)
    bmesh.ops.remove_doubles(bm, verts=list(boundary), dist=2e-6)
    bmesh.ops.dissolve_degenerate(bm, dist=2e-6, edges=list(bm.edges))
    bm.normal_update()
    boundary = {vertex for edge in bm.edges if edge.is_boundary for vertex in edge.verts}
    original_vertices = list(bm.verts)
    original_faces = list(bm.faces)
    mirrored = {
        vertex: (
            vertex
            if vertex in boundary
            else bm.verts.new(vertex.co - axis * (2 * (vertex.co - plane).dot(axis)))
        )
        for vertex in original_vertices
    }
    uv = bm.loops.layers.uv.active
    for face in original_faces:
        duplicate = bm.faces.new([mirrored[vertex] for vertex in reversed(face.verts)])
        duplicate.smooth = True
        for loop, source in zip(duplicate.loops, reversed(list(face.loops))):
            loop[uv].uv = source[uv].uv
            n = source[normal]
            loop[normal] = n - axis * (2 * n.dot(axis))
    for face in bm.faces:
        for loop in face.loops:
            if loop.vert in boundary:
                n = loop[normal]
                loop[normal] = (n - axis * n.dot(axis)).normalized()
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    layer = mesh.attributes["BudgetNormal"]
    mesh.normals_split_custom_set([Vector(item.vector).normalized() for item in layer.data])
    mesh.attributes.remove(layer)
    points = [vertex.co.copy() for vertex in mesh.vertices]
    tree = BVHTree.FromPolygons(
        points, [tuple(face.vertices) for face in mesh.polygons], all_triangles=True
    )
    error = max(
        max(tree.find_nearest(point)[3] for point in original),
        max(original_tree.find_nearest(point)[3] for point in points),
    )
    require(error < 0.004, f"Paw simplification changed the approved silhouette: {error}")
    require(len(mesh.polygons) <= 2000, f"Rounded paw budget exceeded: {len(mesh.polygons)}")
    obj["paw_simplification"] = json.dumps(
        {
            "before_triangles": old_count,
            "after_triangles": len(mesh.polygons),
            "maximum_bidirectional_surface_distance": error,
        }
    )


def shoulder_pose(rig, angle):
    for side in ("L", "R"):
        bone = rig.pose.bones["arm." + side]
        sign = 1 if bone.bone.head_local.x > 0 else -1
        axis = bone.bone.matrix_local.to_quaternion().inverted() @ Vector((0, 1, 0))
        bone.rotation_mode = "QUATERNION"
        bone.rotation_quaternion = Quaternion(axis, math.radians(-sign * angle))
    bpy.context.view_layer.update()


def render_candidate(name, rig, objects, folder, close_only=False, context_only=False):
    for obj in list(bpy.context.scene.objects):
        if obj.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(obj, do_unlink=True)
    camera = render_setup()
    scene = bpy.context.scene
    scene.cycles.samples = 20
    scene.render.resolution_x = 1400
    scene.render.resolution_y = 1400
    camera.data.ortho_scale = 3.1
    reset_pose(rig, objects)
    paw = next(obj for obj in objects if obj.get("mesh_role") == "Arm.L")
    target = Vector(paw["paw_equator"])
    camera.data.ortho_scale = 0.85
    if not context_only:
        for obj in objects:
            obj.hide_render = obj != paw
        for view, direction in [("front", (0, -4, 0)), ("rear", (0, 4, 0)), ("below", (0, -1, -4))]:
            camera.location = target + Vector(direction)
            aim(camera, target)
            scene.render.filepath = str(folder / f"capsule-close-{view}.png")
            bpy.ops.render.render(write_still=True)
    for obj in objects:
        obj.hide_render = False
    if close_only:
        return
    camera.data.ortho_scale = 2.1
    for angle in (-10, 30, 60):
        reset_pose(rig, objects)
        shoulder_pose(rig, angle)
        for view, loc in [
            ("front", (0, -6, 0.9)),
            ("side", (6, 0, 0.9)),
            ("rear", (0, 6, 0.9)),
            ("below", (4, -2, -2.3)),
        ]:
            camera.location = loc
            aim(camera, (0, 0, 0.9))
            scene.render.filepath = str(folder / f"paw-probe-{angle}-{view}.png")
            bpy.ops.render.render(write_still=True)
    reset_pose(rig, objects)


def triangulate_paw(obj):
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
    layer = bm.loops.layers.float_vector.new("RoundedFinalNormal")
    for face, polygon in zip(bm.faces, mesh.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[layer] = mesh.corner_normals[index].vector
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()
    layer = mesh.attributes["RoundedFinalNormal"]
    mesh.normals_split_custom_set([Vector(item.vector).normalized() for item in layer.data])
    mesh.attributes.remove(layer)


_INHERITED_BODY_BOUNDARIES = {}


def boundary_identity(mesh):
    """Identify genuine open edges independently of vertex/face ordering."""
    bm = bmesh.new()
    bm.from_mesh(mesh)
    original = bm.verts.layers.int.get("OriginalSurfaceVertex")
    profile = {}
    for edge in bm.edges:
        if not edge.is_boundary:
            continue
        vertices = sorted(edge.verts, key=lambda v: v[original] if original else tuple(v.co))
        ids = tuple(v[original] for v in vertices) if original else ()
        key = (
            ("source", ids)
            if ids and min(ids) > 0
            else ("position", tuple(tuple(round(c, 6) for c in v.co) for v in vertices))
        )
        profile[key] = np.asarray([v.co[:] for v in vertices])
    bm.free()
    return profile


def inherited_body_boundaries(name, before):
    path = before / f"{name}_Rigged.blend"
    key = str(path.resolve())
    if key not in _INHERITED_BODY_BOUNDARIES:
        old_objects = set(bpy.data.objects)
        old_meshes = set(bpy.data.meshes)
        with bpy.data.libraries.load(str(path), link=False) as (available, loaded):
            names = [item for item in available.objects if item.endswith("_Character")]
            require(len(names) == 1, "Cannot identify immutable baseline body")
            loaded.objects = names
        profile = boundary_identity(loaded.objects[0].data)
        for obj in set(bpy.data.objects) - old_objects:
            bpy.data.objects.remove(obj, do_unlink=True)
        for mesh in set(bpy.data.meshes) - old_meshes:
            if mesh.users == 0:
                bpy.data.meshes.remove(mesh)
        _INHERITED_BODY_BOUNDARIES[key] = profile
    return _INHERITED_BODY_BOUNDARIES[key]


def geometry_checks(name, rig, objects, before):
    """Measure final rest meshes and the actual evaluated shoulder extremes."""
    result = {
        "revision": REVISION,
        "baseline_revision": BASELINE_REVISION,
        "paws": {},
        "textures": {},
    }
    baseline = json.loads((before / "manifest.json").read_text())
    for filename in ("BaseColor.png", "Normal.png", "MetallicSmoothness.png"):
        path = PROCESSED / name / filename
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        require(
            digest == baseline["files"][filename]["sha256"], f"Original texture changed: {path}"
        )
        result["textures"][filename] = {"sha256": digest, "unchanged": True}
    reset_pose(rig, objects)
    for obj in objects:
        mesh = obj.data
        require(
            len(mesh.vertices) > 0 and len(mesh.polygons) > 0, f"Empty registered skin: {obj.name}"
        )
        points = np.array([vertex.co[:] for vertex in mesh.vertices])
        require(np.isfinite(points).all(), f"Non-finite {obj.name}")
        require(
            all(len(face.vertices) == 3 for face in mesh.polygons), f"Non-triangular {obj.name}"
        )
        uv = np.array([loop.uv[:] for loop in mesh.uv_layers.active.data])
        require(np.isfinite(uv).all(), f"Non-finite UV on {obj.name}")
        bm = bmesh.new()
        bm.from_mesh(mesh)
        boundary = sum(edge.is_boundary for edge in bm.edges)
        nonmanifold = sum(len(edge.link_faces) != 2 for edge in bm.edges)
        bm.free()
        if obj.get("mesh_role") == "Character":
            result["body_boundary_edges"] = boundary
            result["body_nonmanifold_edges"] = nonmanifold
            allowed = baseline["arm_clearance_qa"]["body_boundary_edges"]
            require(
                boundary <= allowed,
                f"New open body edges on {name}: {boundary}, inherited allowance {allowed}",
            )
            require(
                nonmanifold == boundary,
                f"Loose or shared body edges on {name}: {nonmanifold} nonmanifold, {boundary} borders",
            )
            if boundary:
                inherited = inherited_body_boundaries(name, before)
                actual = boundary_identity(mesh)
                require(len(actual) == boundary, "Duplicate body boundary identities")
                for key, positions in actual.items():
                    require(
                        key in inherited, f"New body hole on {name}: edge {key} was not inherited"
                    )
                    require(
                        np.max(np.abs(positions - inherited[key])) < 1e-6,
                        f"Inherited open edge moved on {name}: {key}",
                    )
            result["body_topology_gate"] = {
                "inherited_boundary_allowance": allowed,
                "new_open_edges": 0,
                "nonmanifold_without_boundary": 0,
                "inherited_edge_identity_and_positions_verified": True,
            }
            continue
        if obj.get("mesh_role") == "Harness":
            group = obj.vertex_groups["body"].index
            require(nonmanifold == 0, "Rebuilt harness has an open or shared boundary")
            require(
                all(
                    len(vertex.groups) == 1
                    and vertex.groups[0].group == group
                    and abs(vertex.groups[0].weight - 1) < 1e-7
                    for vertex in mesh.vertices
                ),
                "Rebuilt harness is not rigidly attached to the body",
            )
            result["harness"] = {
                "vertices": len(mesh.vertices),
                "triangles": len(mesh.polygons),
                "boundary_edges": boundary,
                "nonmanifold_edges": nonmanifold,
                "rigid_weight": "body",
            }
            continue
        if not obj.get("mesh_role", "").startswith("Arm."):
            # A separately preserved scarf collar may retain the original
            # body/scarf blend instead of rigid paw skinning. Its real border
            # count is reported, never confused with an arm or hidden as zero.
            for vertex in mesh.vertices:
                require(
                    abs(sum(item.weight for item in vertex.groups) - 1.0) < 1e-5,
                    f"Unnormalised additional skin: {obj.name}",
                )
                require(
                    all(
                        not obj.vertex_groups[item.group].name.startswith("arm.")
                        for item in vertex.groups
                        if item.weight > 1e-7
                    ),
                    f"Additional skin retained an arm influence: {obj.name}",
                )
            if obj.get("mesh_role") == "Pack":
                require(nonmanifold == 0, f"Separated pack is not closed: {obj.name}")
            result.setdefault("additional_meshes", {})[obj["mesh_role"]] = {
                "vertices": len(mesh.vertices),
                "triangles": len(mesh.polygons),
                "boundary_edges": boundary,
                "nonmanifold_edges": nonmanifold,
                "normalised_skin_weights": True,
            }
            continue
        side = obj["mesh_role"][-1]
        binding = "arm." + side
        group = obj.vertex_groups[binding].index
        require(
            all(
                len(vertex.groups) == 1
                and vertex.groups[0].group == group
                and abs(vertex.groups[0].weight - 1) < 1e-7
                for vertex in mesh.vertices
            ),
            f"Non-rigid binding {obj.name}",
        )
        require(nonmanifold == 0, f"Unclosed rounded paw {obj.name}: {nonmanifold}")
        center = Vector(obj["paw_equator"])
        inward = sum(face.normal.dot(face.center - center) < -1e-7 for face in mesh.polygons)
        require(inward == 0, f"Folded inward face remains on {obj.name}: {inward}")
        edges = np.array([edge.vertices[:] for edge in mesh.edges])
        length = np.linalg.norm(points[edges[:, 0]] - points[edges[:, 1]], axis=1)
        poses = {}
        for angle in (-10, 30, 60):
            shoulder_pose(rig, angle)
            evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
            data = evaluated.to_mesh()
            actual = np.array([vertex.co[:] for vertex in data.vertices])
            evaluated.to_mesh_clear()
            error = float(
                np.max(
                    np.abs(
                        np.linalg.norm(actual[edges[:, 0]] - actual[edges[:, 1]], axis=1) - length
                    )
                )
            )
            require(error < 2e-6, f"Rounded paw stretched: {obj.name} {angle}: {error}")
            poses[str(angle)] = {"maximum_edge_change": error}
        reset_pose(rig, objects)
        result["paws"][side] = {
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.polygons),
            "boundary_edges": boundary,
            "nonmanifold_edges": nonmanifold,
            "inward_radial_faces": inward,
            "rigid_weight": binding,
            "construction": obj.get("paw_construction"),
            "poses": poses,
        }
        if obj.get("paw_simplification"):
            result["paws"][side]["simplification"] = json.loads(obj["paw_simplification"])
        if obj.get("paw_retopology_paint"):
            result["paws"][side]["paint"] = json.loads(obj["paw_retopology_paint"])
    require(
        any(obj.data.shape_keys and obj.data.shape_keys.key_blocks.get("Blink") for obj in objects),
        "Blink was lost",
    )
    result["blink_preserved"] = True
    return result


def process(name, do_render=False, publish=False, repair_torso=False):
    before = source_snapshot(name)
    folder = REVIEW / name
    folder.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(before / f"{name}_Rigged.blend"))
    for image in bpy.data.images:
        if image.source == "FILE":
            role = next(
                (
                    role
                    for role in ("MetallicSmoothness", "BaseColor", "Normal")
                    if role.lower() in image.name.lower()
                ),
                None,
            )
            filename = (
                role + ".png" if role else image.filepath.replace("\\", "/").rsplit("/", 1)[-1]
            )
            image.filepath = str(PROCESSED / name / filename)
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    objects = [
        obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj
    ]
    # Capture the verified immutable body before authoring. Blender cannot
    # append from the same .blend file it currently has open.
    baseline_body = next(obj for obj in objects if obj["mesh_role"] == "Character")
    _INHERITED_BODY_BOUNDARIES[str((before / f"{name}_Rigged.blend").resolve())] = (
        boundary_identity(baseline_body.data)
    )
    reset_pose(rig, objects)
    reports = []
    for obj in objects:
        if obj["mesh_role"].startswith("Arm."):
            reports.append(mirror_distal(name, obj, rig))
    selected = matched_pair(
        name, [obj for obj in objects if obj["mesh_role"].startswith("Arm.")], rig, reports
    )
    for obj in objects:
        if obj["mesh_role"].startswith("Arm."):
            triangulate_paw(obj)
    torso_report = None
    if repair_torso:
        from repair_smooth_torso_harness import repair_body_harness

        body = next(obj for obj in objects if obj["mesh_role"] == "Character")
        torso_report = repair_body_harness(name, body, rig)
        reports.append({"torso": torso_report})
        from repair_courier_rear_contact import repair_rear_contact, finish_harness_contact

        torso_report["rear_contact_repair"] = repair_rear_contact(name, body, torso_report)
        from repair_courier_harness import build_harness

        density_source = None
        if body.get("separate_pack", False):
            density_source = next(
                obj for obj in bpy.context.scene.objects if obj.get("mesh_role") == "Pack"
            )
        harness, harness_report = build_harness(name, body, rig, source_body=density_source)
        torso_report["rebuilt_harness"] = harness_report
        torso_report["harness_status"] = (
            "rebuilt from restored cloth; contact clearance evaluated separately"
        )
        torso_report["review_status"] = (
            "candidate at authoring; portable and Unity acceptance recorded separately"
        )
        torso_report["harness_contact_finish"] = finish_harness_contact(name, body, harness)
        from repair_courier_contact_uv import repair_contact_uv

        torso_report["localized_contact_uv"] = repair_contact_uv(name, body)
        objects = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj
        ]
    attachment_report = {}
    if repair_torso:
        reset_pose(rig, objects)
        if name == "Tiko":
            from repair_courier_case_edges import apply as repair_case_edges
            from repair_courier_antenna_joint import apply as repair_antenna_joint

            attachment_report["case_edges"] = repair_case_edges(body, rig, character=name)
            attachment_report["antenna_joint"] = repair_antenna_joint(rig, body)
        elif name == "Lumen":
            from adjust_courier_shoulders import apply as adjust_shoulders

            attachment_report["shoulders"] = adjust_shoulders(rig, objects)
        objects = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj
        ]
    reports.append({
        "matched_pair_template": selected,
        "coordinate_stage": "source paw construction before final anatomical attachment adjustments",
    })
    if attachment_report:
        reports.append({"attachment_adjustments": attachment_report})
    qa = geometry_checks(name, rig, objects, before)
    if attachment_report:
        qa["attachment_adjustments"] = attachment_report
        qa["inherited_report_scope"] = (
            "Torso and source accessory reports describe their construction stages. "
            "Final attachments and any local topology changes are measured by "
            "attachment_adjustments and the top-level geometry checks."
        )
    qa["total_triangles"] = sum(len(obj.data.polygons) for obj in objects)
    if torso_report is not None:
        qa["torso"] = torso_report
        require(qa["total_triangles"] <= 30000, "Complete courier exceeded the 30k triangle budget")
    bpy.context.preferences.filepaths.save_version = 0
    blend = folder / ("smooth-review.blend" if repair_torso else "paw-probe.blend")
    bpy.ops.wm.save_as_mainfile(filepath=str(blend), relative_remap=False)
    (folder / "paw-probe.json").write_text(json.dumps(reports, indent=2) + "\n")
    (folder / "smooth-checks.json").write_text(json.dumps(qa, indent=2) + "\n")
    manifest = json.loads((before / "manifest.json").read_text())
    for key in ("arm_clearance_revision", "arm_clearance_qa", "verification", "files"):
        manifest.pop(key, None)
    manifest.update(
        {
            "rounded_courier_revision": REVISION,
            "rounded_courier_qa": qa,
            "meshes": [
                {
                    "name": obj.name,
                    "role": obj["mesh_role"],
                    "vertices": len(obj.data.vertices),
                    "triangles": len(obj.data.polygons),
                }
                for obj in objects
            ],
            "triangles": sum(len(obj.data.polygons) for obj in objects),
            "bones": [
                {
                    "name": bone.name,
                    "parent": bone.parent.name if bone.parent else None,
                    "head": list(bone.head_local),
                    "tail": list(bone.tail_local),
                }
                for bone in rig.data.bones
            ],
        }
    )
    (folder / "rig-manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
    if do_render:
        render_candidate(name, rig, objects, folder)
    if publish:
        require(repair_torso, "Publication requires the reviewed torso repair too")
        import export_prepared_rigs as exporter

        extract_source(name)
        with tempfile.TemporaryDirectory(prefix=".radio-rounded-", dir=PROCESSED) as temporary:
            staging = pathlib.Path(temporary)
            result = exporter.export_review(blend, staging, name, exporter.SOURCE / name)
            for texture in ("BaseColor.png", "Normal.png", "MetallicSmoothness.png"):
                require(
                    hashlib.sha256((staging / texture).read_bytes()).hexdigest()
                    == qa["textures"][texture]["sha256"],
                    f"Export changed an original 4K map: {name}/{texture}",
                )
            for filename in (*result["files"], "manifest.json"):
                (staging / filename).replace(PROCESSED / name / filename)
    print("SMOOTH_COURIER", name, json.dumps(qa), flush=True)
    return qa


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--character", choices=("Nori", "Tiko", "Lumen"), default="Nori")
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--close-only", action="store_true")
    parser.add_argument("--context-only", action="store_true")
    parser.add_argument(
        "--torso", action="store_true", help="Include the separately reviewed torso/harness module"
    )
    parser.add_argument(
        "--publish",
        action="store_true",
        help="Strictly export and promote an approved complete candidate",
    )
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])
    name = args.character
    folder = REVIEW / name
    if args.context_only:
        bpy.ops.wm.open_mainfile(
            filepath=str(folder / ("smooth-review.blend" if args.torso else "paw-probe.blend"))
        )
        rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        objects = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj
        ]
        render_candidate(name, rig, objects, folder, False, True)
        return
    process(name, args.render and not args.close_only, args.publish, args.torso)
    if args.render and args.close_only:
        rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
        objects = [
            obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj
        ]
        render_candidate(name, rig, objects, folder, True)


if __name__ == "__main__":
    main()
