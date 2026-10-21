"""Read-only Blender checks on the saved authoring rigs; never saves a .blend."""
from __future__ import annotations

import hashlib
import json
import math
import pathlib

import bpy

ROOT = pathlib.Path(__file__).resolve().parents[3]
REVIEW = ROOT / "DancingCatsClone/Library/RadioRigging/Review"


def hash_file(path):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def check(name):
    path = REVIEW / name / "rig-review.blend"
    bpy.ops.wm.open_mainfile(filepath=str(path))
    errors = []
    result = {"character": name, "blend_sha256": hash_file(path), "meshes": []}
    for obj in bpy.context.scene.objects:
        if obj.type != "MESH":
            continue
        mesh = obj.data
        continuous=obj.get("arm_attachment_mode")=="continuous-source"
        info = {"name": obj.name, "role": obj.get("mesh_role"), "vertices": len(mesh.vertices), "triangles": len(mesh.polygons),"arm_attachment_mode":obj.get("arm_attachment_mode")}
        body = obj.vertex_groups.get("body")
        cap = mesh.attributes.get("SocketCap")
        cap_indices = {i for face in mesh.polygons if cap and cap.data[face.index].value for i in face.vertices}
        signatures = set()
        duplicates = 0
        edge_use = {}
        for face in mesh.polygons:
            key = tuple(sorted(face.vertices))
            duplicates += key in signatures
            signatures.add(key)
            for edge in face.edge_keys:
                edge_use[edge] = edge_use.get(edge, 0) + 1
        info["duplicate_faces"] = duplicates
        info["boundary_edges"] = sum(count == 1 for count in edge_use.values())
        info["edges_with_more_than_two_faces"] = sum(count > 2 for count in edge_use.values())
        info["tiny_area_triangles"] = sum(face.area < 1e-12 for face in mesh.polygons)
        info["cap_vertices"] = len(cap_indices)
        max_error = 0
        max_influences = 0
        for vertex in mesh.vertices:
            if not all(math.isfinite(v) for v in vertex.co):
                errors.append(f"Nonfinite vertex: {obj.name}:{vertex.index}")
            weights = {obj.vertex_groups[g.group].name: g.weight for g in vertex.groups if g.weight > 1e-7}
            max_error = max(max_error, abs(sum(weights.values()) - 1))
            max_influences = max(max_influences, len(weights))
            if obj.get("mesh_role", "").startswith("Arm.") and vertex.index in cap_indices and obj.get("arm_attachment_mode")!="shoulder":
                if abs(weights.get("body", 0) - 1) > 1e-5 or any(value > 1e-5 for key, value in weights.items() if key.startswith("arm.")):
                    errors.append(f"Unpinned cap: {obj.name}:{vertex.index}")
            elif not obj.get("mesh_role", "").startswith("Arm.") and not continuous:
                if any(value > 1e-6 for key, value in weights.items() if key.startswith("arm.")):
                    errors.append(f"Arm weight leaked outside hand: {obj.name}:{vertex.index}")
        info["maximum_weight_sum_error"] = max_error
        info["maximum_skin_influences"] = max_influences
        if duplicates:
            errors.append(f"Duplicate faces: {obj.name}:{duplicates}")
        if obj.get("mesh_role", "").startswith("Arm."):
            if info["boundary_edges"] or info["edges_with_more_than_two_faces"]:
                errors.append(f"Arm root is not edge-closed: {obj.name}")
            if info["tiny_area_triangles"]:
                errors.append(f"Degenerate hand triangles: {obj.name}")
        if max_error > 1e-5 or max_influences > 4:
            errors.append(f"Invalid skin weights: {obj.name}")
        if continuous and cap:
            errors.append(f"Unexpected cap geometry on continuous source mesh: {obj.name}")
        result["meshes"].append(info)
    rigs = [o for o in bpy.context.scene.objects if o.type == "ARMATURE"]
    if len(rigs) != 1:
        errors.append(f"Expected one rig, got {len(rigs)}")
    else:
        result["bones"] = len(rigs[0].data.bones)
        result["eye_specs"] = json.loads(rigs[0].get("blink_eye_specs", "[]"))
    result["triangles"] = sum(item["triangles"] for item in result["meshes"])
    result["errors"] = errors
    result["pass"] = not errors
    return result


results = {name: check(name) for name in ("Tiko", "Lumen", "Nori")}
output = REVIEW.parent / "authoring-checks.json"
output.write_text(json.dumps(results, indent=2), encoding="utf-8")
for name, result in results.items():
    print(f"RADIO_AUTHORING {name} pass={result['pass']} triangles={result['triangles']} errors={result['errors']}", flush=True)
if not all(result["pass"] for result in results.values()):
    raise RuntimeError("Authoring rig validation failed; see authoring-checks.json")
