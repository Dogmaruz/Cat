"""Build clean rounded shoulder straps over the repaired courier torso.

The cloth repair removes the old fused leather/contact remnants first. This
module adds one closed, rigid body-weighted Harness mesh and leaves Character,
Blink, backpack, antenna, scarf and the original 4K image bytes untouched.
"""

from __future__ import annotations

import argparse
import json
import math
import pathlib
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT = pathlib.Path(__file__).resolve().parents[3]
REVIEW = ROOT / "DancingCatsClone/Library/RadioRigging/SmoothHarnessReview"
PROCESSED = ROOT / "ArtSource/RadioArchipelago/Processed"
sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))
from repair_smooth_torso_harness import (
    face_colors,
    flesh_color,
    image_pixels,
    paint_image,
    uv_triangle_area,
)
from prepare_radio_rigs import aim, render_setup

# Averaged original shoulder-footprint bounds, not dimensions of a replacement
# torso. The strap is projected onto the actual repaired cloth at every station.
LAYOUT = {
    "Nori": {"center": (0.182, 0.674), "radius": (0.280, 0.315), "width": 0.088},
    "Tiko": {"center": (0.077, 0.740), "radius": (0.252, 0.345), "width": 0.080},
    "Lumen": {"center": (0.109, 0.771), "radius": (0.205, 0.280), "width": 0.075},
}


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def rectangle_sums(array, height, width):
    integral = np.pad(array, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
    return (
        integral[height:, width:]
        - integral[:-height, width:]
        - integral[height:, :-width]
        + integral[:-height, :-width]
    )


def material_patch(body, kind):
    """Choose a genuine painted atlas area; never collapse a surface to a texel."""
    image = paint_image(body)
    pixels = image_pixels(image)
    rgb = pixels[::4, ::4, :3]
    metallic_pixels = image_pixels(paint_image(body, "MetallicSmoothness"))
    metallic = metallic_pixels[::4, ::4, 0]
    r, g, b = rgb[:, :, 0], rgb[:, :, 1], rgb[:, :, 2]
    if kind == "canvas":
        allowed = (g > 0.13) & (g < 0.68) & (g > r * 1.12) & (g > b * 0.96)
        target = np.array((0.11, 0.29, 0.23))
        allowed &= metallic_pixels[::4, ::4, 3] < 0.60
    elif kind == "brass":
        allowed = (r > 0.32) & (g > 0.17) & (b < g * 0.80) & (r > g * 1.03)
        target = np.array((0.59, 0.40, 0.16))
    else:
        allowed = (r > 0.08) & (r < 0.62) & (r > g * 1.10) & (g > b * 1.12) & (b > g * 0.15)
        target = np.array((0.28, 0.18, 0.10))
    if kind != "brass":
        allowed &= metallic < 0.12
    elif np.count_nonzero(allowed & (metallic > 0.40)) > 1000:
        allowed &= metallic > 0.40
    difference = np.sum((rgb - target) ** 2, axis=2)
    choices = (
        ((0.016, 0.020), (0.012, 0.016), (0.010, 0.012))
        if kind == "brass"
        else ((0.024, 0.090), (0.022, 0.065), (0.018, 0.050), (0.016, 0.040))
    )
    for width_uv, height_uv in choices:
        w, h = (
            int(math.ceil(width_uv * pixels.shape[1] / 4)) + 4,
            int(math.ceil(height_uv * pixels.shape[0] / 4)) + 4,
        )
        invalid = rectangle_sums((~allowed).astype(float), h, w) / (h * w)
        cost = rectangle_sums(difference, h, w) / (h * w) + invalid * 10
        # Reject conspicuous painted contact/stitch strokes running through a
        # patch. They would repeat as broad stripes on a newly shaped strap.
        mean = np.stack([rectangle_sums(rgb[:, :, i], h, w) / (h * w) for i in range(3)], axis=2)
        variance = rectangle_sums(np.sum(rgb * rgb, axis=2), h, w) / (h * w) - np.sum(
            mean * mean, axis=2
        )
        cost += np.maximum(0.0, variance) * (12 if kind == "leather" else 2)
        y, x = np.unravel_index(np.argmin(cost), cost.shape)
        if invalid[y, x] > 0.005:
            continue
        origin = Vector(((x + 2) * 4 / pixels.shape[1], (y + 2) * 4 / pixels.shape[0]))
        patch = pixels[y * 4 : (y + h) * 4, x * 4 : (x + w) * 4, :3]
        return (
            origin,
            Vector((width_uv, height_uv)),
            {
                "kind": kind,
                "origin_uv": list(origin),
                "extent_uv": [width_uv, height_uv],
                "pixels": [w * 4, h * 4],
                "material_coverage": float(1 - invalid[y, x]),
                "base_color_mean": patch.mean(axis=(0, 1)).tolist(),
                "base_color_std": patch.std(axis=(0, 1)).tolist(),
                "mean_metallic": float(
                    metallic_pixels[y * 4 : (y + h) * 4, x * 4 : (x + w) * 4, 0].mean()
                ),
                "mean_smoothness": float(
                    metallic_pixels[y * 4 : (y + h) * 4, x * 4 : (x + w) * 4, 3].mean()
                ),
            },
        )
    raise RuntimeError(f"No full painted {kind} atlas patch is available")


def torso_surface(character, body, sign):
    colors = face_colors(body)
    body_group = body.vertex_groups["body"].index
    body_weight = [
        sum(item.weight for item in vertex.groups if item.group == body_group)
        for vertex in body.data.vertices
    ]
    arm_weight = [
        sum(
            item.weight
            for item in vertex.groups
            if body.vertex_groups[item.group].name.startswith("arm.")
        )
        for vertex in body.data.vertices
    ]
    secondary_weight = [
        sum(
            item.weight
            for item in vertex.groups
            if body.vertex_groups[item.group].name.startswith(("antenna.", "crest.", "scarf."))
        )
        for vertex in body.data.vertices
    ]
    semantic = body.data.attributes.get("TripoSurfacePart")
    body_part = {"Nori": 1, "Tiko": 12}.get(character)
    source_faces = body.data.attributes.get("OriginalSurfaceFace")

    def retained_case(face):
        # Lumen has no Tripo semantic export. This is the same anatomical
        # source-case mask used by cloth authoring; new cloth closures remain.
        return (
            character == "Lumen"
            and not body.get("separate_pack", body.get("has_separate_pack", False))
            and source_faces is not None
            and source_faces.data[face.index].value > 0
            and 0.30 < face.center.z < 1.50
            and any(body.data.vertices[i].co.y > 0.30 for i in face.vertices)
        )

    faces = [
        face
        for face in body.data.polygons
        if 0.20 < face.center.z < 1.23
        and -0.75 < face.center.y < 0.75
        and sign * face.center.x > 0.08
        and flesh_color(character, colors[face.index])
        and (
            (character == "Lumen" and max(arm_weight[index] for index in face.vertices) < 1e-7)
            or (character != "Lumen" and min(body_weight[index] for index in face.vertices) > 0.94)
        )
        and not retained_case(face)
        and (character != "Lumen" or max(secondary_weight[index] for index in face.vertices) < 1e-7)
        and (character != "Lumen" or face.normal.x * sign > 0.15)
        and (
            semantic is None
            or body_part is None
            or semantic.data[face.index].value in (0, body_part + 1)
        )
    ]
    require(faces, "No restored cloth is available for the harness fit")
    points = [vertex.co.copy() for vertex in body.data.vertices]
    tree = BVHTree.FromPolygons(
        points, [tuple(face.vertices) for face in faces], all_triangles=True
    )

    def sample(y, z):
        point, normal, index, distance = tree.ray_cast(
            Vector((sign * 3, y, z)), Vector((-sign, 0, 0)), 6
        )
        if point is None:
            point, normal, index, distance = tree.find_nearest(Vector((sign * 0.6, y, z)))
            point = Vector((point.x, y, z))
        if normal.x * sign < 0:
            normal = -normal
        return point, normal.normalized()

    def actual_ray(y, z):
        point, normal, index, distance = tree.ray_cast(
            Vector((sign * 3, y, z)), Vector((-sign, 0, 0)), 6
        )
        return (point, normal) if point is not None else None

    sample.actual_ray = actual_ray
    return sample


def authentic_density(body, kind):
    colors = face_colors(body)
    packed = image_pixels(paint_image(body, "MetallicSmoothness"))
    original_faces = body.data.attributes.get("OriginalSurfaceFace")
    if body.get("mesh_role") == "Pack":
        require(
            original_faces is not None,
            "Separated Pack needs original-face identity for material calibration",
        )
    ratios = []
    for face, color in zip(body.data.polygons, colors):
        if body.get("mesh_role") == "Pack" and original_faces.data[face.index].value == 0:
            continue
        if face.area < 1e-8 or not (0.25 < face.center.z < 1.20 and -0.35 < face.center.y < 0.55):
            continue
        r, g, b = color
        match = (
            (g > r * 1.10 and g > b * 0.96)
            if kind == "canvas"
            else (r > g * 1.10 and g > b * 1.12 and r < 0.62)
        )
        if kind == "brass":
            match = r > 0.32 and g > 0.17 and b < g * 0.80 and r > g * 1.03
        uv = sum(
            (body.data.uv_layers.active.data[index].uv for index in face.loop_indices),
            Vector((0, 0)),
        ) / len(face.loop_indices)
        texel = packed[
            min(packed.shape[0] - 1, max(0, int(uv.y * packed.shape[0]))),
            min(packed.shape[1] - 1, max(0, int(uv.x * packed.shape[1]))),
        ]
        if kind == "brass":
            match = match and texel[0] > 0.35
        elif kind == "canvas":
            match = match and texel[0] < 0.12 and texel[3] < 0.60
        else:
            match = match and r > g * 1.30 and b > g * 0.15 and texel[0] < 0.12
        if match:
            ratio = uv_triangle_area(body.data, face) / face.area
            if 0.0001 < ratio < 2.0:
                ratios.append(ratio)
    require(len(ratios) >= 10, f"No authentic {kind} texel-density sample")
    return float(np.median(ratios)), len(ratios)


def ribbon_profile(width, thickness):
    bevel = min(thickness * 0.42, width * 0.15)
    profile = []
    for cx, cz, angle0 in (
        (width / 2 - bevel, thickness / 2 - bevel, 0),
        (-width / 2 + bevel, thickness / 2 - bevel, 90),
        (-width / 2 + bevel, -thickness / 2 + bevel, 180),
        (width / 2 - bevel, -thickness / 2 + bevel, 270),
    ):
        for angle in (angle0, angle0 + 45, angle0 + 90):
            a = math.radians(angle)
            profile.append((cx + bevel * math.cos(a), cz + bevel * math.sin(a)))
    return profile


def clear_visible_path(path, normals, width, thickness, sample, sign):
    """Retain the periodic fit while keeping the complete ribbon above cloth."""
    profile = ribbon_profile(width, thickness)
    maximum = 0.0
    for _ in range(2):
        required = []
        for i, point in enumerate(path):
            tangent = (path[(i + 1) % len(path)] - path[(i - 1) % len(path)]).normalized()
            normal = (normals[i] - tangent * normals[i].dot(tangent)).normalized()
            across = tangent.cross(normal).normalized()
            deficit = 0.0
            for cross, depth in profile:
                p = point + across * cross + normal * depth
                hit = sample.actual_ray(p.y, p.z)
                if hit is None:
                    continue
                surface, _ = hit
                deficit = max(deficit, 0.008 - sign * (p.x - surface.x))
            required.append(max(0.0, deficit))
        required = np.asarray(required)
        widened = np.maximum.reduce((required, np.roll(required, 1), np.roll(required, -1)))
        rounded = (np.roll(widened, 1) + 2 * widened + np.roll(widened, -1)) * 0.25
        shifts = np.maximum(required, rounded)
        require(float(shifts.max()) < 0.20, "Ribbon support hit a non-cloth surface")
        for point, shift in zip(path, shifts):
            point.x += sign * float(shift)
        maximum += float(shifts.max())
    return {
        "profile_points_per_station": len(profile),
        "maximum_center_outward_shift": maximum,
        "minimum_requested_visible_x_clearance": 0.008,
        "case_support_excluded": True,
    }


class HarnessMesh:
    def __init__(self):
        self.points = []
        self.faces = []
        self.uvs = []

    def tube(
        self, path, normals, width, thickness, origin, extent, density, clearance=None, sign=1
    ):
        """A closed bevelled rectangular ribbon, with no sharp cut rim."""
        old_path = path
        old_normals = normals
        lengths = np.array(
            [
                0.0,
                *np.cumsum(
                    [(path[(i + 1) % len(path)] - point).length for i, point in enumerate(path)]
                ),
            ]
        )
        total_length = float(lengths[-1])
        scale = math.sqrt(density)
        across_extent = min(float(extent.x), width * scale)
        repeats = max(1, round(scale * total_length / (2 * extent.y)))
        # Put geometry stations exactly at each real chart turn. Interpolating
        # across a missed UV turn would make a low-density stripe.
        turns = np.linspace(0, total_length, repeats * 2 + 1)[:-1].tolist()
        stations = []
        for distance in sorted(lengths[:-1].tolist() + turns):
            if stations and distance - stations[-1] < 1e-6:
                if distance in turns:
                    stations[-1] = distance
            else:
                stations.append(distance)
        path, normals = [], []
        for distance in stations:
            index = min(
                len(old_path) - 1, max(0, int(np.searchsorted(lengths, distance, side="right")) - 1)
            )
            blend = (distance - lengths[index]) / (lengths[index + 1] - lengths[index])
            path.append(old_path[index].lerp(old_path[(index + 1) % len(old_path)], float(blend)))
            normals.append(
                old_normals[index]
                .lerp(old_normals[(index + 1) % len(old_normals)], float(blend))
                .normalized()
            )
        count = len(path)
        bevel = min(thickness * 0.42, width * 0.15)
        profile = ribbon_profile(width, thickness)
        start = len(self.points)
        minimum_clearance = float("inf")
        intermediate_shift = 0.0
        for i, point in enumerate(path):
            tangent = (path[(i + 1) % count] - path[(i - 1) % count]).normalized()
            normal = normals[i].normalized()
            normal = (normal - tangent * normal.dot(tangent)).normalized()
            across = tangent.cross(normal).normalized()
            ring = [point + across * cross + normal * depth for cross, depth in profile]
            if clearance is not None:
                hits = [clearance.actual_ray(p.y, p.z) for p in ring]
                surfaces = [hit[0].x if hit is not None else None for hit in hits]
                shift = max(
                    0.0,
                    max(
                        (0.008 - sign * (p.x - x) for p, x in zip(ring, surfaces) if x is not None),
                        default=0.0,
                    ),
                )
                intermediate_shift = max(intermediate_shift, shift)
                for p, x in zip(ring, surfaces):
                    p.x += sign * shift
                    if x is not None:
                        minimum_clearance = min(minimum_clearance, sign * (p.x - x))
            self.points.extend(ring)
        side_count = len(profile)
        start_faces = len(self.faces)
        stations.append(total_length)
        for i in range(count):
            j = (i + 1) % count

            # Ping-pong is continuous at each repetition, unlike jumping between
            # unrelated atlas charts. Fine original grain remains fully sampled.
            def along(k):
                phase = (stations[k] / total_length * repeats * 2) % 2
                return phase if phase <= 1 else 2 - phase

            for k in range(side_count):
                l = (k + 1) % side_count
                self.faces.append(
                    (
                        start + i * side_count + k,
                        start + i * side_count + l,
                        start + j * side_count + l,
                        start + j * side_count + k,
                    )
                )
                # Front and back are full real leather charts. Their seam is on
                # the narrow side edge, rather than sharing one tiny atlas strip
                # around the whole cross-section perimeter.
                a, b = profile[k], profile[l]
                if abs((a[0] + b[0]) * 0.5) > width * 0.5 - bevel * 0.35:
                    cross_a = (a[1] + thickness * 0.5) / width
                    cross_b = (b[1] + thickness * 0.5) / width
                else:
                    cross_a = (a[0] + width * 0.5) / width
                    cross_b = (b[0] + width * 0.5) / width
                self.uvs.append(
                    [
                        origin + Vector((cross_a * across_extent, along(i) * extent.y)),
                        origin + Vector((cross_b * across_extent, along(i) * extent.y)),
                        origin + Vector((cross_b * across_extent, along(i + 1) * extent.y)),
                        origin + Vector((cross_a * across_extent, along(i + 1) * extent.y)),
                    ]
                )
        world_area = uv_area = 0.0
        for face, paint in zip(self.faces[start_faces:], self.uvs[start_faces:]):
            p = [self.points[index] for index in face]
            for i in (1, 2):
                world_area += (p[i] - p[0]).cross(p[i + 1] - p[0]).length * 0.5
                a, b = paint[i] - paint[0], paint[i + 1] - paint[0]
                uv_area += abs(a.x * b.y - a.y * b.x) * 0.5
        measured_density = uv_area / world_area
        require(
            0.65 < measured_density / density < 1.5,
            "Harness UV density differs excessively from authentic leather",
        )
        return {
            "source_uv_area_per_world_area": density,
            "actual_uv_area_per_world_area": measured_density,
            "world_area": world_area,
            "uv_area": uv_area,
            "longitudinal_chart_traversals": repeats * 2,
            "width_uv": across_extent,
            "length_uv_per_traversal": float(extent.y),
            "stations": count,
            "minimum_visible_x_clearance": minimum_clearance if clearance is not None else None,
            "maximum_intermediate_outward_shift": intermediate_shift,
        }


def rounded_rectangle(center, across, tangent, half_width, half_height, radius):
    points = []
    for a, b, angle in (
        (half_width - radius, half_height - radius, 0),
        (-half_width + radius, half_height - radius, 90),
        (-half_width + radius, -half_height + radius, 180),
        (half_width - radius, -half_height + radius, 270),
    ):
        for step in range(4):
            theta = math.radians(angle + step * 30)
            points.append(
                center
                + across * (a + radius * math.cos(theta))
                + tangent * (b + radius * math.sin(theta))
            )
    return points


def build_harness(character, body, rig, source_body=None):
    require(
        not any(obj.get("mesh_role") == "Harness" for obj in bpy.context.scene.objects),
        "A harness already exists",
    )
    layout = LAYOUT[character]
    kind = "canvas" if character == "Tiko" else "leather"
    require(
        not body.get("separate_pack", False) or source_body is not None,
        "Separated body needs the original Pack as explicit material-density source",
    )
    origin, extent, leather = material_patch(body, kind)
    gold_origin, gold_extent, brass = material_patch(body, "brass")
    leather_density, leather_samples = authentic_density(source_body or body, kind)
    brass_density, brass_samples = authentic_density(source_body or body, "brass")
    leather.update(
        {
            "authentic_uv_area_per_world_area": leather_density,
            "authentic_sample_faces": leather_samples,
        }
    )
    brass.update(
        {"authentic_uv_area_per_world_area": brass_density, "authentic_sample_faces": brass_samples}
    )
    builder = HarnessMesh()
    report = {
        "revision": "rounded-source-cloth-harness-v1",
        "material": leather,
        "brass": brass,
        "sides": {},
    }
    for sign in (-1, 1):
        sample = torso_surface(character, body, sign)
        path, normals = [], []
        for theta in np.linspace(0, 2 * math.pi, 48, endpoint=False):
            y = layout["center"][0] + layout["radius"][0] * math.cos(theta)
            z = layout["center"][1] + layout["radius"][1] * math.sin(theta)
            point, normal = sample(y, z)
            path.append(point + normal * 0.013)
            normals.append(normal)
        # Fit the actual torso samples with a low-order periodic curve. Painted
        # mesh facets and nearby source seams must not make a scalloped belt.
        theta = np.linspace(0, 2 * math.pi, 48, endpoint=False)
        design = np.array(
            [
                [1.0, *(value for k in range(1, 4) for value in (math.cos(k * a), math.sin(k * a)))]
                for a in theta
            ]
        )
        actual = np.array([point.x for point in path])
        fitted = design @ np.linalg.lstsq(design, actual, rcond=None)[0]
        for i, x in enumerate(fitted):
            path[i].x = float(x)
            # The upper attachment goes beneath the original scarf. Sampling
            # neighbouring hood cloth must not lay a leather ribbon over it.
            if character in ("Nori", "Tiko"):
                lower, upper = (0.85, 1.015) if character == "Nori" else (0.93, 1.105)
                amount = min(1.0, max(0.0, (path[i].z - lower) / (upper - lower)))
                path[i].x -= sign * 0.17 * amount * amount * (3 - 2 * amount)
        normals = [
            sum((normals[(i + k) % 48] for k in range(-2, 3)), Vector()).normalized()
            for i in range(48)
        ]
        visible_clearance = None
        if character == "Lumen":
            visible_clearance = clear_visible_path(
                path, normals, layout["width"], 0.016, sample, sign
            )
        strap_mapping = builder.tube(
            path,
            normals,
            layout["width"],
            0.016,
            origin,
            extent,
            leather_density,
            clearance=sample if character == "Lumen" else None,
            sign=sign,
        )
        if visible_clearance is not None:
            strap_mapping["visible_clearance_fit"] = visible_clearance
        # A real bevelled brass frame sits above the continuous strap, with the
        # same opening and original material language as the supplied pack.
        index = 25
        normal = normals[index]
        tangent = (path[(index + 1) % 48] - path[(index - 1) % 48]).normalized()
        across = tangent.cross(normal).normalized()
        center = path[index] + normal * 0.026
        buckle = rounded_rectangle(center, across, tangent, layout["width"] * 0.73, 0.068, 0.024)
        buckle_mapping = builder.tube(
            buckle, [normal] * len(buckle), 0.019, 0.024, gold_origin, gold_extent, brass_density
        )
        report["sides"][str(sign)] = {
            "stations": len(path),
            "strap_width": layout["width"],
            "strap_thickness": 0.016,
            "buckle_center": list(center),
            "maximum_section_step": max((path[(i + 1) % 48] - path[i]).length for i in range(48)),
            "maximum_periodic_fit_change": float(np.max(np.abs(fitted - actual))),
            "strap_uv_density": strap_mapping,
            "buckle_uv_density": buckle_mapping,
        }
    mesh = bpy.data.meshes.new(character + "_Harness")
    mesh.from_pydata(builder.points, [], builder.faces)
    uv = mesh.uv_layers.new(name="UVMap")
    for face, values in zip(mesh.polygons, builder.uvs):
        for index, value in zip(face.loop_indices, values):
            uv.data[index].uv = value
    for material in body.data.materials:
        mesh.materials.append(material)
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    require(all(len(edge.link_faces) == 2 for edge in bm.edges), "Harness is not closed")
    bm.to_mesh(mesh)
    bm.free()
    for face in mesh.polygons:
        face.use_smooth = True
    obj = bpy.data.objects.new(character + "_Harness", mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj["mesh_role"] = "Harness"
    obj["harness_revision"] = report["revision"]
    obj.parent = body.parent
    obj.matrix_world = body.matrix_world.copy()
    group = obj.vertex_groups.new(name="body")
    group.add(list(range(len(mesh.vertices))), 1.0, "REPLACE")
    modifier = obj.modifiers.new("CourierRig", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = False
    report.update(
        {
            "vertices": len(mesh.vertices),
            "triangles": len(mesh.polygons),
            "boundary_edges": 0,
            "rigid_weight": "body",
        }
    )
    return obj, report


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--character", choices=tuple(LAYOUT), default="Nori")
    parser.add_argument("--blend", type=pathlib.Path, help="Reviewed clean cloth candidate")
    parser.add_argument("--isolated", action="store_true", help="Review just the new harness")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])
    name = args.character
    baseline = (
        ROOT
        / "DancingCatsClone/Library/RadioRigging/SmoothCourierReview/Before"
        / name
        / (name + "_Rigged.blend")
    )
    bpy.ops.wm.open_mainfile(filepath=str(args.blend or baseline))
    for image in bpy.data.images:
        if image.source == "FILE":
            image.filepath = str(
                PROCESSED / name / image.filepath.replace("\\", "/").rsplit("/", 1)[-1]
            )
    body = next(obj for obj in bpy.context.scene.objects if obj.get("mesh_role") == "Character")
    rig = next(obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE")
    density_source = (
        next((o for o in bpy.context.scene.objects if o.get("mesh_role") == "Pack"), None)
        if body.get("separate_pack", False)
        else None
    )
    obj, report = build_harness(name, body, rig, source_body=density_source)
    folder = REVIEW / name
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "harness-checks.json").write_text(json.dumps(report, indent=2) + "\n")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / "harness-review.blend"), relative_remap=False)
    for item in list(bpy.context.scene.objects):
        if item.type in ("CAMERA", "LIGHT"):
            bpy.data.objects.remove(item, do_unlink=True)
    camera = render_setup()
    camera.data.ortho_scale = 1.7
    scene = bpy.context.scene
    scene.render.resolution_x = scene.render.resolution_y = 1000
    scene.cycles.samples = 12
    for item in bpy.context.scene.objects:
        if (args.isolated and item.type == "MESH" and item != obj) or item.get(
            "mesh_role", ""
        ).startswith("Arm."):
            item.hide_render = True
    for suffix, position in (
        ("front", (0, -5, 0.9)),
        ("side", (5, 0, 0.9)),
        ("under", (4, -2, -2)),
    ):
        camera.location = position
        aim(camera, (0, 0, 0.83))
        scene.render.filepath = str(folder / ("harness-" + suffix + ".png"))
        bpy.ops.render.render(write_still=True)
    print("SMOOTH_HARNESS", name, json.dumps(report), flush=True)


if __name__ == "__main__":
    main()
