"""Read-only geometric checks for canonical V3 island scenes (Blender 4+).

This module is safe to import in ordinary Python. Blender/mathutils are imported
only by functions that inspect a scene. Coordinates are world-space X/Y with Z
up; north is +Y. One unit is one gameplay metre. No Unity asset is inspected or
modified here, and these checks do not claim a Unity playtest.

Typical use, with an explicitly declared cyclic point sequence::

    before = geometry_snapshot(scene)
    views = []
    # Position the real orthographic camera and render each view, then:
    views.append(camera_snapshot(camera, "top"))
    # ... front, back, left, right ...
    after = geometry_snapshot(scene)
    report = validate(scene, points, road_width=6, ceiling_height=5,
        shoulder=1, route_closed=True, geometry_before=before,
        geometry_after=after, camera_views=views)
    write_report(report, "checks.json")

route_points may be XYZ/XY sequences, {x,y,z} mappings, or a mapping containing
"points" and "closed". Without route_closed=True the final point must repeat
the first. A closed declaration also tests the final-to-first mesh seam. Meshes
must have v3_role on the object or an ancestor: road, terrain, obstacle, water.
Camera/light objects may use camera/light. Evaluated render geometry is checked,
including modifiers. Hidden render meshes and non-render instances are excluded.

Road coverage is a finite raycast audit, not proof against arbitrarily tiny
gaps. The report states the probe spacing and edge inset. Obstacle surface
intersection with the swept corridor uses triangle clipping and exact planar
distances, with an inside-volume fallback for closed enclosing meshes. By
default no_overhead=True extends that corridor upward without a height limit.
Terrain/water height limits apply only to the sampled road footprint, using
nine lateral probe lanes by default. Hills outside the road remain unrestricted.
Terrain below the road must leave at least terrain_below_offset (default .08).
"""

import hashlib
import json
import math
import os
import struct


VALID_MESH_ROLES = {"road", "terrain", "obstacle", "water"}
DEFAULT_VIEWS = ("top", "front", "back", "left", "right")


def _role(obj):
    while obj is not None:
        value = obj.get("v3_role")
        if value:
            return str(value)
        obj = obj.parent
    return None


def _visible_meshes(scene):
    return sorted((obj for obj in scene.objects
                   if obj.type in {"MESH", "CURVE", "SURFACE", "FONT", "META"}
                   and not obj.hide_render), key=lambda obj: obj.name)


def _evaluated_meshes(scene):
    # The chosen scene's own depsgraph avoids accidentally checking another tab.
    layer = scene.view_layers[0]
    layer.update()
    depsgraph = layer.depsgraph
    for obj in _visible_meshes(scene):
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            vertices = [tuple(evaluated.matrix_world @ v.co) for v in mesh.vertices]
            triangles = [tuple(t.vertices) for t in mesh.loop_triangles]
            yield obj, vertices, triangles
        finally:
            evaluated.to_mesh_clear()


def geometry_snapshot(scene):
    """Hash evaluated world geometry; camera/light changes do not affect it.

    Includes mesh names, inherited roles, vertex ordering and triangle indices.
    All meshes must remain visible throughout each canonical render. This hash
    proves only equality of the supplied snapshots, not a hidden intermediate
    change; record one snapshot per view when stronger evidence is required.
    """
    import numpy as np
    digest = hashlib.sha256(b'RA4 evaluated local coordinates plus world transform\0')
    mesh_count = vertex_count = triangle_count = 0
    layer=scene.view_layers[0];layer.update();depsgraph=layer.depsgraph
    for obj in _visible_meshes(scene):
        evaluated=obj.evaluated_get(depsgraph);mesh=evaluated.to_mesh()
        try:
            mesh.calc_loop_triangles()
            coords=np.empty(len(mesh.vertices)*3,dtype='<f4');mesh.vertices.foreach_get('co',coords)
            triangles=np.empty(len(mesh.loop_triangles)*3,dtype='<i4');mesh.loop_triangles.foreach_get('vertices',triangles)
            digest.update((obj.name + "\0" + str(_role(obj)) + "\0").encode("utf-8"))
            digest.update(struct.pack('<QQ',len(mesh.vertices),len(mesh.loop_triangles)))
            digest.update(np.asarray(evaluated.matrix_world,dtype='<f8').tobytes())
            digest.update(coords.tobytes());digest.update(triangles.tobytes())
            mesh_count+=1;vertex_count+=len(mesh.vertices);triangle_count+=len(mesh.loop_triangles)
        finally:evaluated.to_mesh_clear()
    return {"sha256": digest.hexdigest(), "mesh_count": mesh_count,
            "vertex_count": vertex_count, "triangle_count": triangle_count,
            "hash_method":"evaluated_local_f32_world_matrix_f64_triangle_i32"}


def camera_snapshot(camera, view_id=None):
    """Record camera evidence immediately before/after an actual render.

    Blender cameras look along local -Z; up is local +Y. The forward vector is
    world-space, so named elevation directions can be checked independently of
    the camera object's name or Euler rotation convention.
    """
    from mathutils import Vector
    forward = camera.matrix_world.to_quaternion() @ Vector((0, 0, -1))
    up = camera.matrix_world.to_quaternion() @ Vector((0, 1, 0))
    return {"view_id": str(view_id or camera.name), "name": camera.name,
            "type": camera.data.type, "position": list(camera.matrix_world.translation),
            "forward": list(forward), "up": list(up),
            "ortho_scale": float(camera.data.ortho_scale),
            "clip_start": float(camera.data.clip_start),
            "clip_end": float(camera.data.clip_end)}


def write_report(report, path):
    """Write the returned numeric evidence as UTF-8 JSON; return absolute path."""
    path = os.path.abspath(os.fspath(path))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8") as handle:
        json.dump(report, handle, indent=2, ensure_ascii=False, allow_nan=False)
        handle.write("\n")
    return path


def _xyz(value, default_z):
    if isinstance(value, dict):
        point = (float(value["x"]), float(value["y"]), float(value.get("z", default_z)))
    else:
        point = (float(value[0]), float(value[1]), float(value[2]) if len(value) > 2 else default_z)
    if not all(math.isfinite(v) for v in point):
        raise ValueError("Route coordinates must be finite")
    return point


def _distance(a, b):
    return math.sqrt(sum((a[i] - b[i]) ** 2 for i in range(3)))


def _point_segment_sq(p, a, b):
    dx, dy = b[0] - a[0], b[1] - a[1]
    den = dx * dx + dy * dy
    t = max(0.0, min(1.0, ((p[0] - a[0]) * dx + (p[1] - a[1]) * dy) / den)) if den else 0.0
    return (p[0] - a[0] - t * dx) ** 2 + (p[1] - a[1] - t * dy) ** 2


def _cross(a, b, c):
    return (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])


def _segments_intersect(a, b, c, d):
    eps = 1e-10
    ab_c, ab_d, cd_a, cd_b = _cross(a, b, c), _cross(a, b, d), _cross(c, d, a), _cross(c, d, b)
    if ab_c * ab_d < -eps and cd_a * cd_b < -eps:
        return True
    return ((abs(ab_c) <= eps and _point_segment_sq(c, a, b) <= eps) or
            (abs(ab_d) <= eps and _point_segment_sq(d, a, b) <= eps) or
            (abs(cd_a) <= eps and _point_segment_sq(a, c, d) <= eps) or
            (abs(cd_b) <= eps and _point_segment_sq(b, c, d) <= eps))


def _inside_xy(point, polygon):
    inside = False
    j = len(polygon) - 1
    for i, a in enumerate(polygon):
        b = polygon[j]
        if (a[1] > point[1]) != (b[1] > point[1]):
            x = (b[0] - a[0]) * (point[1] - a[1]) / (b[1] - a[1]) + a[0]
            if point[0] < x:
                inside = not inside
        j = i
    return inside


def _polygon_segment_distance_sq(polygon, a, b):
    if _inside_xy(a, polygon) or _inside_xy(b, polygon):
        return 0.0
    result = float("inf")
    for i, c in enumerate(polygon):
        d = polygon[(i + 1) % len(polygon)]
        if _segments_intersect(a, b, c, d):
            return 0.0
        result = min(result, _point_segment_sq(c, a, b),
                     _point_segment_sq(a, c, d), _point_segment_sq(b, c, d))
    return result


def _clip_z(polygon, plane, keep_above):
    result = []
    previous = polygon[-1]
    previous_inside = (previous[2] >= plane) if keep_above else (previous[2] <= plane)
    for current in polygon:
        current_inside = (current[2] >= plane) if keep_above else (current[2] <= plane)
        if current_inside != previous_inside:
            t = (plane - previous[2]) / (current[2] - previous[2])
            result.append(tuple(previous[i] + t * (current[i] - previous[i]) for i in range(3)))
        if current_inside:
            result.append(current)
        previous, previous_inside = current, current_inside
    return result


def _bbox(vertices):
    return (tuple(min(p[i] for p in vertices) for i in range(3)),
            tuple(max(p[i] for p in vertices) for i in range(3)))


def _candidate_segments(bounds, segments, half_width):
    lo, hi = bounds
    return [(i, a, b) for i, (a, b) in enumerate(segments)
            if max(a[0], b[0]) + half_width >= lo[0]
            and min(a[0], b[0]) - half_width <= hi[0]
            and max(a[1], b[1]) + half_width >= lo[1]
            and min(a[1], b[1]) - half_width <= hi[1]]


def _closed_mesh(triangles):
    edges = {}
    for tri in triangles:
        for i in range(3):
            edge = tuple(sorted((tri[i], tri[(i + 1) % 3])))
            edges[edge] = edges.get(edge, 0) + 1
    return bool(edges) and all(count == 2 for count in edges.values())


def _inside_volume(bvh, point, travel):
    from mathutils import Vector
    direction = Vector((0.932736, 0.271937, 0.236321)).normalized()
    origin = Vector(point)
    count = 0
    remaining = travel
    # Odd/even crossings; the oblique ray avoids authored axis-aligned edges.
    while remaining > 0 and count < 10000:
        location, normal, index, distance = bvh.ray_cast(origin, direction, remaining)
        if location is None:
            break
        count += 1
        advance = distance + 1e-5
        origin = location + direction * 1e-5
        remaining -= advance
    return bool(count % 2)


def validate(scene, route_points, road_width=6.0, ceiling_height=5.0, *,
             shoulder=1.0, road_z=0.0, route_closed=None,
             sample_step=0.35, edge_inset=0.05, height_tolerance=0.025,
             max_grade=0.02, closure_tolerance=0.001, no_overhead=True,
             terrain_below_offset=0.08, surface_probe_lanes=9,
             geometry_before=None, geometry_after=None, geometry_per_view=None,
             camera_views=None, required_views=DEFAULT_VIEWS,
             camera_direction_tolerance_degrees=0.5,
             require_render_evidence=True, max_examples=24):
    """Return JSON-compatible checks; never edits objects or changes a camera.

    Five orthographic views: top looks -Z/up +Y; front is south and looks +Y;
    back is north and looks -Y; left is west and looks +X; right looks -X.
    Set require_render_evidence=False for a pre-render geometry audit; its
    report explicitly records that render evidence was not required. Production
    completion should keep the default True. Per-view geometry snapshots may
    be supplied in addition to before/after for stronger render provenance.
    """
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree

    positive = {"road_width": road_width, "ceiling_height": ceiling_height,
                "sample_step": sample_step, "height_tolerance": height_tolerance}
    if any(not math.isfinite(v) or v <= 0 for v in positive.values()):
        raise ValueError("Widths, heights, tolerances and sample spacing must be positive finite values")
    if shoulder < 0 or edge_inset < 0 or edge_inset >= road_width / 2:
        raise ValueError("Shoulder must be nonnegative; edge inset must be within half the road")
    if terrain_below_offset < 0 or not math.isfinite(terrain_below_offset) or surface_probe_lanes < 3:
        raise ValueError("Terrain offset must be nonnegative and surface_probe_lanes at least three")
    if isinstance(route_points, dict):
        if route_closed is None:
            route_closed = route_points.get("closed")
        route_points = route_points["points"]
    points = [_xyz(point, road_z) for point in route_points]
    explicit_closed = len(points) >= 2 and _distance(points[0], points[-1]) <= closure_tolerance
    if explicit_closed:
        points.pop()
    declared_closed = bool(route_closed is True or explicit_closed)
    if len(points) < 3:
        raise ValueError("A road loop needs at least three distinct XY points")
    segments = list(zip(points, points[1:] + points[:1]))
    lengths = [math.hypot(b[0] - a[0], b[1] - a[1]) for a, b in segments]
    if min(lengths) <= 1e-8:
        raise ValueError("Consecutive route points must have distinct XY coordinates")
    checks = []

    def check(name, passed, **evidence):
        checks.append({"name": name, "pass": bool(passed), **evidence})

    check("route_loop_closed", declared_closed, explicit_closing_point=explicit_closed,
          authored_closed_flag=route_closed, final_edge_length=lengths[-1],
          route_point_count=len(points), loop_length=sum(lengths))
    route_error = max(abs(p[2] - road_z) for p in points)
    check("route_flat_at_road_z", route_error <= height_tolerance,
          max_abs_z_error=route_error, tolerance=height_tolerance)
    crossings = []
    for i, (a, b) in enumerate(segments):
        for j in range(i + 2, len(segments)):
            if i == 0 and j == len(segments) - 1:
                continue
            if _segments_intersect(a, b, *segments[j]):
                crossings.append([i, j])
    check("route_has_no_self_crossings", not crossings,
          crossing_count=len(crossings), segment_pairs=crossings[:max_examples])
    instances = [item.object.name for item in scene.view_layers[0].depsgraph.object_instances
                 if item.is_instance and not item.object.hide_render]
    check("render_instances_realized", not instances, instance_count=len(instances),
          examples=instances[:max_examples])
    mesh_records = []
    untagged = []
    for obj, vertices, triangles in _evaluated_meshes(scene):
        role = _role(obj)
        if role not in VALID_MESH_ROLES:
            untagged.append({"object": obj.name, "role": role})
        if vertices:
            mesh_records.append((obj.name, role, vertices, triangles, _bbox(vertices)))
    check("all_render_geometry_has_role", not untagged, invalid_count=len(untagged),
          examples=untagged[:max_examples])
    road_vertices, road_triangles = [], []
    for name, role, vertices, triangles, bounds in mesh_records:
        if role == "road":
            offset = len(road_vertices)
            road_vertices.extend(vertices)
            road_triangles.extend(tuple(i + offset for i in t) for t in triangles)
    check("road_geometry_exists", bool(road_triangles), vertex_count=len(road_vertices),
          triangle_count=len(road_triangles))
    road_bvh = BVHTree.FromPolygons(road_vertices, road_triangles, all_triangles=True) if road_triangles else None
    # Average neighboring tangents at vertices, and interpolate along each span.
    normals = []
    for i, p in enumerate(points):
        prev, nxt = points[i - 1], points[(i + 1) % len(points)]
        dx, dy = nxt[0] - prev[0], nxt[1] - prev[1]
        den = math.hypot(dx, dy)
        if den <= 1e-10:
            raise ValueError("Route folds back on itself at a vertex")
        normals.append((-dy / den, dx / den))
    samples = []
    for i, (a, b) in enumerate(segments):
        steps = max(1, math.ceil(lengths[i] / sample_step))
        for j in range(steps):
            t = j / steps
            p = tuple(a[k] + (b[k] - a[k]) * t for k in range(3))
            n = tuple(normals[i][k] * (1 - t) + normals[(i + 1) % len(points)][k] * t for k in range(2))
            den = math.hypot(*n)
            samples.append((p, (n[0] / den, n[1] / den)))
    misses, errors, slopes = [], [], []
    profile = {"center": [], "left": [], "right": []}
    half_test = road_width / 2 - edge_inset
    ray_top = max(road_z + ceiling_height + 1,
                  max((bounds[1][2] + 1 for _, role, _, _, bounds in mesh_records if role == "road"), default=1))
    ray_distance = max(10, ray_top - road_z + ceiling_height + 1)
    for index, (p, normal) in enumerate(samples):
        for label, offset in (("center", 0), ("left", half_test), ("right", -half_test)):
            origin = Vector((p[0] + offset * normal[0], p[1] + offset * normal[1], ray_top))
            hit = road_bvh.ray_cast(origin, Vector((0, 0, -1)), ray_distance) if road_bvh else (None, None, None, None)
            if hit[0] is None:
                misses.append({"sample": index, "probe": label, "xy": list(origin[:2])})
                profile[label].append(None)
            else:
                z = hit[0].z
                profile[label].append(z)
                if abs(z - road_z) > height_tolerance or hit[1].z < 0.99:
                    errors.append({"sample": index, "probe": label, "z": z, "normal_z": hit[1].z})
    for label, values in profile.items():
        for i, z in enumerate(values):
            previous = values[i - 1]
            if z is not None and previous is not None:
                step = math.hypot(samples[i][0][0] - samples[i - 1][0][0],
                                  samples[i][0][1] - samples[i - 1][0][1])
                slopes.append(abs(z - previous) / max(step, 1e-8))
    hit_heights = [z for values in profile.values() for z in values if z is not None]
    check("road_coverage_center_left_right", not misses and bool(road_triangles),
          sample_count=len(samples), ray_count=len(samples) * 3, missing_count=len(misses),
          max_sample_spacing=sample_step, tested_width=half_test * 2,
          edge_inset=edge_inset, examples=misses[:max_examples])
    check("road_height_and_normal", not errors and bool(hit_heights),
          min_hit_z=min(hit_heights) if hit_heights else None,
          max_hit_z=max(hit_heights) if hit_heights else None,
          max_abs_height_error=max((abs(z - road_z) for z in hit_heights), default=None),
          invalid_count=len(errors), tolerance=height_tolerance,
          examples=errors[:max_examples])
    check("road_height_profile_continuity", bool(slopes) and max(slopes) <= max_grade,
          max_measured_grade=max(slopes, default=None), allowed_grade=max_grade,
          seam_included=True)
    def audit_surface_projection(role, allowed_z):
        # Only the actual road footprint is protected by this height rule.
        # Hills, ponds and fountains outside it may legitimately rise above Z=0.
        selected = [record for record in mesh_records if record[1] == role]
        vertices, triangles, triangle_objects = [], [], []
        for name, _, object_vertices, object_triangles, _ in selected:
            offset = len(vertices)
            vertices.extend(object_vertices)
            triangles.extend(tuple(i + offset for i in t) for t in object_triangles)
            triangle_objects.extend([name] * len(object_triangles))
        violations, heights, missing = [], [], 0
        if triangles:
            bvh = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
            top_z = max(p[2] for p in vertices) + 1
            travel = top_z - min(p[2] for p in vertices) + 2
            for sample_index, (point, normal) in enumerate(samples):
                for lane in range(surface_probe_lanes):
                    offset = -half_test + (2 * half_test * lane / (surface_probe_lanes - 1))
                    origin = Vector((point[0] + offset * normal[0], point[1] + offset * normal[1], top_z))
                    location, _, triangle_index, _ = bvh.ray_cast(origin, Vector((0, 0, -1)), travel)
                    if location is None:
                        # Missing terrain/water is legal below a bridge deck.
                        missing += 1
                        continue
                    heights.append(location.z)
                    if location.z > allowed_z + 1e-5:
                        violations.append({"object": triangle_objects[triangle_index],
                                           "sample": sample_index, "lane": lane,
                                           "xy": list(location[:2]), "z": location.z})
        return {"object_count": len(selected), "scope": "sampled_actual_road_footprint",
                "tested_width": half_test * 2, "edge_inset": edge_inset,
                "sample_count": len(samples), "lanes_per_sample": surface_probe_lanes,
                "ray_count": len(samples) * surface_probe_lanes if triangles else 0,
                "hit_count": len(heights), "missing_count": missing,
                "maximum_hit_z": max(heights, default=None),
                "maximum_allowed_z": allowed_z, "numerical_tolerance": 1e-5,
                "violation_count": len(violations), "examples": violations[:max_examples]}
    terrain_evidence = audit_surface_projection("terrain", road_z - terrain_below_offset)
    check("terrain_below_road", not terrain_evidence["violation_count"], **terrain_evidence)
    water_evidence = audit_surface_projection("water", road_z - height_tolerance)
    check("water_below_road", not water_evidence["violation_count"], **water_evidence)
    half_width = road_width / 2 + shoulder
    floor = road_z + height_tolerance
    top = None if no_overhead else road_z + ceiling_height
    collisions, inspected_triangles, candidate_objects = [], 0, 0
    for name, role, vertices, triangles, bounds in mesh_records:
        if role in {"road", "terrain", "water"} or bounds[1][2] < floor or (top is not None and bounds[0][2] > top):
            continue
        candidates = _candidate_segments(bounds, segments, half_width)
        if candidates:
            # A conservative XY disk can reject foliage AABB corner overlaps
            # without inspecting millions of individual leaves. Every vertex,
            # hence every triangle, lies inside this disk; no corridor test is
            # omitted unless the entire disk is outside the swept segment.
            center=((bounds[0][0]+bounds[1][0])/2,(bounds[0][1]+bounds[1][1])/2)
            radius=math.sqrt(max((v[0]-center[0])**2+(v[1]-center[1])**2 for v in vertices))+1e-6
            candidates=[item for item in candidates if _point_segment_sq(center,item[1],item[2])<(half_width+radius)**2]
        if not candidates:
            continue
        candidate_objects += 1
        found = None
        for triangle in triangles:
            polygon = [vertices[i] for i in triangle]
            if max(p[2] for p in polygon) < floor:
                continue
            polygon = _clip_z(polygon, floor, True)
            if polygon and top is not None:
                polygon = _clip_z(polygon, top, False)
            if not polygon:
                continue
            inspected_triangles += 1
            poly_bounds = _bbox(polygon)
            for segment_index, a, b in candidates:
                lo, hi = poly_bounds
                if max(a[0], b[0]) + half_width < lo[0] or min(a[0], b[0]) - half_width > hi[0] or max(a[1], b[1]) + half_width < lo[1] or min(a[1], b[1]) - half_width > hi[1]:
                    continue
                d2 = _polygon_segment_distance_sq(polygon, a, b)
                if d2 < half_width * half_width - 1e-8:
                    found = {"object": name, "role": role, "segment": segment_index,
                             "surface_distance_to_centerline": math.sqrt(d2),
                             "triangle_z_range": [poly_bounds[0][2], poly_bounds[1][2]],
                             "method": "clipped_triangle_vs_swept_capsule"}
                    break
            if found:
                break
        if found is None and _closed_mesh(triangles):
            bvh = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
            test_z = (max(floor, bounds[0][2]) + min(bounds[1][2], top if top is not None else bounds[1][2])) / 2
            for segment_index, a, b in candidates:
                p = ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, test_z)
                if all(bounds[0][k] <= p[k] <= bounds[1][k] for k in range(3)) and _inside_volume(bvh, p, _distance(*bounds) * 3 + 10):
                    found = {"object": name, "role": role, "segment": segment_index,
                             "method": "closed_mesh_contains_corridor_center", "point": list(p)}
                    break
        if found:
            collisions.append(found)
    check("swept_corridor_clearance", not collisions, protected_width=half_width * 2,
          required_ceiling_height=ceiling_height, no_overhead=no_overhead,
          tested_z_min=floor, tested_z_max=top, obstacle_collision_count=len(collisions),
          candidate_object_count=candidate_objects, tested_triangle_count=inspected_triangles,
          examples=collisions[:max_examples])
    directions = {"top": (0, 0, -1), "front": (0, 1, 0), "back": (0, -1, 0),
                  "left": (1, 0, 0), "right": (-1, 0, 0)}
    camera_records = list(camera_views or [])
    missing_views = [view for view in required_views if view not in {v.get("view_id") for v in camera_records}]
    camera_errors = []
    for camera in camera_records:
        if camera.get("type") != "ORTHO" or camera.get("ortho_scale", 0) <= 0:
            camera_errors.append({"view_id": camera.get("view_id"), "error": "camera must be orthographic with positive scale"})
        expected = directions.get(camera.get("view_id"))
        if expected:
            forward = camera.get("forward", (0, 0, 0))
            den = math.sqrt(sum(c * c for c in forward))
            dot = sum(forward[i] * expected[i] for i in range(3)) / max(den, 1e-20)
            angle = math.degrees(math.acos(max(-1, min(1, dot))))
            if angle > camera_direction_tolerance_degrees:
                camera_errors.append({"view_id": camera.get("view_id"), "direction_error_degrees": angle})
            expected_up = (0, 1, 0) if camera.get("view_id") == "top" else (0, 0, 1)
            up = camera.get("up", (0, 0, 0))
            up_den = math.sqrt(sum(c * c for c in up))
            up_dot = sum(up[i] * expected_up[i] for i in range(3)) / max(up_den, 1e-20)
            up_angle = math.degrees(math.acos(max(-1, min(1, up_dot))))
            if up_angle > camera_direction_tolerance_degrees:
                camera_errors.append({"view_id": camera.get("view_id"), "up_error_degrees": up_angle})
            position = camera.get("position", (0, 0, 0))
            center = tuple(sum(p[k] for p in points) / len(points) for k in range(3))
            # The camera must stand opposite its look direction from the route.
            if sum((position[k] - center[k]) * expected[k] for k in range(3)) >= 0:
                camera_errors.append({"view_id": camera.get("view_id"), "error": "camera is on the wrong side of the route"})
    check("five_orthographic_camera_views", not camera_errors and (not missing_views or not require_render_evidence),
          render_evidence_required=require_render_evidence, required_views=list(required_views),
          recorded_count=len(camera_records), missing_views=missing_views,
          error_count=len(camera_errors), examples=camera_errors[:max_examples], cameras=camera_records)
    live_snapshot = geometry_snapshot(scene)
    snapshots = [s for s in [geometry_before, *(geometry_per_view or []), geometry_after, live_snapshot] if s is not None]
    hashes = [s.get("sha256") if isinstance(s, dict) else str(s) for s in snapshots]
    valid_hashes = bool(hashes) and all(isinstance(h, str) and len(h) == 64 for h in hashes)
    evidence_present = geometry_before is not None and geometry_after is not None
    same_geometry = valid_hashes and len(set(hashes)) == 1
    check("same_geometry_across_renders", (same_geometry and evidence_present) if require_render_evidence else (same_geometry if snapshots else True),
          render_evidence_required=require_render_evidence, before_and_after_present=evidence_present,
          snapshot_count=len(snapshots), distinct_hash_count=len(set(hashes)), hashes=hashes,
          live_geometry=live_snapshot)
    return {"schema": "radio_archipelago_v3_geometry_checks/1", "pass": all(c["pass"] for c in checks),
            "validation_kind": "evaluated_blender_geometry_and_supplied_render_evidence",
            "unity_playtest_performed": False,
            "parameters": {"road_z": road_z, "road_width": road_width, "shoulder": shoulder,
                           "ceiling_height": ceiling_height, "sample_step": sample_step,
                           "edge_inset": edge_inset, "height_tolerance": height_tolerance,
                           "terrain_below_offset": terrain_below_offset,
                           "surface_probe_lanes": surface_probe_lanes},
            "limits": ["Road raycasts sample center and both edges; gaps smaller than probe spacing can be missed.",
                       "Collider response, gameplay tile placement, chart timing and character animation were not simulated.",
                       "Render metadata must be captured by the rendering script; this validator does not itself render.",
                       "Geometry hashes exclude materials, lights and cameras and compare only supplied snapshots.",
                       "Collection/geometry-node instances must be realized into tagged meshes before this audit."],
            "checks": checks}
