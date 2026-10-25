"""Match Nori's left replacement cloth to the adjacent original body material.

Six immutable source IDs were classified in actual Unity closeups. Their old
atlas coordinates cross leather/contact paint. The left replacement cloth also
used a brighter, smoother atlas island than the adjacent original torso. Native
UV chart translations match that local body fabric while preserving grain
density and orientation. No image, position, normal, weight or scarf UV is edited.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import pathlib
import sys

import bpy
import numpy as np
from mathutils import Vector

sys.path.insert(0, str(pathlib.Path(__file__).resolve().parent))

NORI_SOURCE_FACES = frozenset(
    (
        9586,
        9587,
        9295,
        9596,
        9597,
        9921,
        998,
        1006,
        1007,
        1015,
        2147,
        4109,
        4119,
        4182,
        4183,
        4385,
        4393,
        4394,
        4399,
        4400,
        4401,
        4402,
        4403,
        4406,
        4407,
        4409,
        4411,
        4412,
        4492,
        4496,
        6169,
        6170,
        11216,
        11241,
        11892,
        12211,
        12220,
        12222,
        12223,
        12235,
        12259,
        12262,
    )
)


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def rectangle_sums(array, height, width):
    summed = np.pad(array, ((1, 0), (1, 0))).cumsum(0).cumsum(1)
    return (
        summed[height:, width:]
        - summed[:-height, width:]
        - summed[height:, :-width]
        + summed[:-height, :-width]
    )


def body_fabric_mask(mesh, height, width, excluded):
    """Rasterize only the already proven semantic BodyPart1 UV islands."""
    parts = mesh.attributes.get("TripoSurfacePart")
    require(parts is not None, "Nori source semantic tags are required")
    mask = np.zeros((height, width), bool)
    for face in mesh.polygons:
        if face.index in excluded or parts.data[face.index].value != 2:
            continue
        points = np.asarray([mesh.uv_layers.active.data[i].uv[:] for i in face.loop_indices])
        points *= (width, height)
        for corner in range(1, len(points) - 1):
            a, b, c = points[[0, corner, corner + 1]]
            low = np.maximum(0, np.floor(np.min((a, b, c), axis=0)).astype(int))
            high = np.minimum(
                (width - 1, height - 1), np.ceil(np.max((a, b, c), axis=0)).astype(int)
            )
            if np.any(high < low):
                continue
            xmin, ymin = low
            xmax, ymax = high
            yy, xx = np.mgrid[ymin : ymax + 1, xmin : xmax + 1]
            denominator = (b[1] - c[1]) * (a[0] - c[0]) + (c[0] - b[0]) * (a[1] - c[1])
            if abs(denominator) < 1e-12:
                continue
            p = (
                (b[1] - c[1]) * (xx + 0.5 - c[0]) + (c[0] - b[0]) * (yy + 0.5 - c[1])
            ) / denominator
            q = (
                (c[1] - a[1]) * (xx + 0.5 - c[0]) + (a[0] - c[0]) * (yy + 0.5 - c[1])
            ) / denominator
            mask[ymin : ymax + 1, xmin : xmax + 1] |= (p >= 0) & (q >= 0) & (p + q <= 1)
    mask[1:-1, 1:-1] &= mask[:-2, 1:-1] & mask[2:, 1:-1] & mask[1:-1, :-2] & mask[1:-1, 2:]
    return mask


def immutable_mesh_digest(mesh):
    digest = hashlib.sha256()
    digest.update(np.asarray([v.co[:] for v in mesh.vertices], dtype=np.float32).tobytes())
    digest.update(
        np.asarray([n.vector[:] for n in mesh.corner_normals], dtype=np.float32).tobytes()
    )
    for face in mesh.polygons:
        digest.update(np.asarray(face.vertices[:], dtype=np.int32).tobytes())
    for vertex in mesh.vertices:
        digest.update(repr([(g.group, g.weight) for g in vertex.groups]).encode())
    if mesh.shape_keys:
        for key in mesh.shape_keys.key_blocks:
            digest.update(key.name.encode())
            digest.update(np.asarray([v.co[:] for v in key.data], dtype=np.float32).tobytes())
    return digest.hexdigest()


def repair_contact_uv(character, body):
    """Apply only the independently identified body UV repair; return its proof."""
    if character != "Nori":
        return {"character": character, "changed_source_faces": []}
    from repair_smooth_torso_harness import image_pixels, paint_image

    mesh = body.data
    source_ids = mesh.attributes.get("OriginalSurfaceFace")
    require(source_ids is not None, "Original face identity was lost")
    faces = [f for f in mesh.polygons if source_ids.data[f.index].value - 1 in NORI_SOURCE_FACES]
    require(
        len(faces) == len(NORI_SOURCE_FACES),
        "Expected all classified original Nori contact triangles",
    )
    require(
        all(mesh.attributes["TripoSurfacePart"].data[f.index].value == 2 for f in faces),
        "A contact repair reached another semantic surface",
    )
    before_digest = immutable_mesh_digest(mesh)
    uv_before = np.asarray([item.uv[:] for item in mesh.uv_layers.active.data])
    color = image_pixels(paint_image(body, "BaseColor"))
    normal = image_pixels(paint_image(body, "Normal"))
    packed = image_pixels(paint_image(body, "MetallicSmoothness"))
    height, width = color.shape[:2]
    require(height % 4 == 0 and width % 4 == 0, "Expected original four-pixel-aligned source maps")
    body_mask = body_fabric_mask(mesh, height // 4, width // 4, {f.index for f in faces})
    semantic = np.repeat(np.repeat(body_mask, 4, axis=0), 4, axis=1)[:height, :width]
    r, g, b = color[:, :, :3].transpose((2, 0, 1))
    roughness = 1 - packed[:, :, 3]
    healthy = (
        semantic
        & (b > r * 1.15)
        & (b > g * 1.06)
        & (b > 0.035)
        & (b < 0.65)
        & (r < 0.38)
        & (normal[:, :, 2] > 0.95)
        & (packed[:, :, 0] < 0.03)
        & (roughness > 0.74)
        & (roughness < 0.80)
    )
    blocks = healthy.reshape(height // 4, 4, width // 4, 4).all(axis=(1, 3))
    sampled = color[::4, ::4, :3]
    # Measured adjacent BodyPart1 faces 11604/2354, not the brighter hood/paws.
    target_color = (0.055, 0.161, 0.290)
    cost = np.sum((sampled - target_color) ** 2, axis=2)
    cost += (roughness[::4, ::4] - 0.761) ** 2 * 0.5
    cache = {}
    report = []
    side = mesh.attributes.get("SmoothTorsoPatchSide")
    require(side is not None, "The replacement cloth side attribute is required")
    replacement = [
        f
        for f in mesh.polygons
        if source_ids.data[f.index].value == 0 and side.data[f.index].value == -1
    ]
    selected = faces + replacement
    adjacency = {f.index: set() for f in selected}
    edge_faces = {}
    for face in selected:
        corners = {mesh.loops[i].vertex_index: uv_before[i] for i in face.loop_indices}
        for i, a in enumerate(face.vertices):
            b = face.vertices[(i + 1) % len(face.vertices)]
            key = tuple(sorted((a, b)))
            for other, previous in edge_faces.get(key, []):
                if all(np.max(np.abs(corners[v] - previous[v])) < 1e-7 for v in key):
                    adjacency[face.index].add(other)
                    adjacency[other].add(face.index)
            edge_faces.setdefault(key, []).append((face.index, corners))
    remaining = set(adjacency)
    charts = []
    while remaining:
        component = {min(remaining)}
        front = set(component)
        while front:
            front = {j for i in front for j in adjacency[i]} - component
            component |= front
        remaining -= component
        charts.append([mesh.polygons[i] for i in sorted(component)])

    def translate_chart(chart, depth=0):
        loops = [i for face in chart for i in face.loop_indices]
        points = uv_before[loops]
        low = points.min(axis=0)
        extent = points.max(axis=0) - low
        w = max(3, int(math.ceil(extent[0] * width / 4)) + 2)
        h = max(3, int(math.ceil(extent[1] * height / 4)) + 2)
        if (h, w) not in cache:
            invalid = rectangle_sums((~blocks).astype(np.int32), h, w)
            scores = rectangle_sums(cost, h, w) / (h * w)
            scores[invalid != 0] = np.inf
            y, x = np.unravel_index(np.argmin(scores), scores.shape)
            cache[(h, w)] = (
                np.asarray(((x + 1) * 4 / width, (y + 1) * 4 / height))
                if np.isfinite(scores[y, x])
                else None
            )
        origin = cache[(h, w)]
        if origin is None:
            require(
                len(chart) > 1 and depth < 14, "No healthy native-density local body cloth chart"
            )
            axis = int(np.argmax(extent))
            ordered = sorted(chart, key=lambda f: uv_before[list(f.loop_indices), axis].mean())
            middle = len(ordered) // 2
            translate_chart(ordered[:middle], depth + 1)
            translate_chart(ordered[middle:], depth + 1)
            return
        for loop, point in zip(loops, points):
            mesh.uv_layers.active.data[loop].uv = Vector(tuple(point - low + origin))
        result = np.asarray([mesh.uv_layers.active.data[i].uv[:] for i in loops])
        old_edges = points[1:] - points[0]
        new_edges = result[1:] - result[0]
        require(
            np.max(np.abs(old_edges - new_edges)) < 1e-7,
            "UV translation changed native triangle density",
        )
        report.append(
            {
                "faces": [f.index for f in chart],
                "source_faces": [source_ids.data[f.index].value - 1 for f in chart],
                "corner_count": len(loops),
                "healthy_window_pixels": [w * 4, h * 4],
                "native_material_coverage": 1.0,
                "maximum_uv_edge_error": float(np.max(np.abs(old_edges - new_edges))),
            }
        )

    for chart in charts:
        translate_chart(chart)
    uv_after = np.asarray([item.uv[:] for item in mesh.uv_layers.active.data])
    changed = {loop for face in selected for loop in face.loop_indices}
    protected = [
        loop
        for face in mesh.polygons
        if mesh.attributes["TripoSurfacePart"].data[face.index].value in (5, 6)
        for loop in face.loop_indices
    ]
    outside = [i for i in range(len(uv_before)) if i not in changed]
    require(
        np.array_equal(uv_before[outside], uv_after[outside]),
        "UV repair escaped the left replacement cloth/contact scope",
    )
    require(np.array_equal(uv_before[protected], uv_after[protected]), "Protected scarf UV changed")
    require(
        before_digest == immutable_mesh_digest(mesh),
        "UV repair changed geometry, normals, weights or shape keys",
    )
    return {
        "character": character,
        "revision": "nori-local-body-pbr-match-v2",
        "changed_source_faces": sorted(NORI_SOURCE_FACES),
        "replacement_cloth_faces": len(replacement),
        "local_body_reference_source_faces": [11604, 2354],
        "target_color": list(target_color),
        "target_roughness": 0.761,
        "faces": report,
        "immutable_mesh_sha256": before_digest,
        "geometry_normals_weights_shapes_delta": 0,
        "outside_uv_delta": 0,
        "protected_scarf_uv_delta": 0,
        "texture_pixels_changed": False,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend", required=True)
    parser.add_argument("--out", required=True)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else [])
    source = pathlib.Path(args.blend).resolve()
    out = pathlib.Path(args.out).resolve()
    out.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(source))
    body = next(o for o in bpy.context.scene.objects if o.get("mesh_role") == "Character")
    report = repair_contact_uv("Nori", body)
    (out / "contact-uv-checks.json").write_text(json.dumps(report, indent=2) + "\n")
    for filename in ("rig-manifest.json", "smooth-checks.json"):
        path = source.parent / filename
        payload = json.loads(path.read_text())
        if filename == "rig-manifest.json":
            payload["rounded_courier_qa"]["localized_contact_uv"] = report
        else:
            payload["localized_contact_uv"] = report
        (out / filename).write_text(json.dumps(payload, indent=2) + "\n")
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "smooth-review.blend"), relative_remap=False)
    print(
        "NORI_CONTACT_UV PASS",
        json.dumps(
            {
                "revision": report["revision"],
                "source_faces": report["changed_source_faces"],
                "replacement_cloth_faces": report["replacement_cloth_faces"],
                "native_density_charts": len(report["faces"]),
                "immutable_mesh_sha256": report["immutable_mesh_sha256"],
                "protected_scarf_uv_delta": report["protected_scarf_uv_delta"],
            }
        ),
        flush=True,
    )


if __name__ == "__main__":
    main()
