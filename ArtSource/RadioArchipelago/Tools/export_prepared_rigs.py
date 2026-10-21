"""Export a reviewed Radioarchipelago rig without modifying the source model.

Run in an isolated Blender process, for example::

    blender --background --factory-startup --python-exit-code 1 --python export_prepared_rigs.py -- \
        --blend path/to/Tiko/rig-review.blend --out path/to/Processed/Tiko

The reviewed mesh, UVs, normals, skin weights, bone names and Blink shape key are
retained. Original BaseColor and Normal PNG bytes are copied exactly; only the
technical Unity metallic/smoothness map is packed. The editable .blend references
the three exported maps relatively and does not depend on ignored Library files.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import shutil
import sys
import tempfile

import bpy
import numpy as np
from mathutils import Matrix, Vector

CHARACTERS = ("Tiko", "Lumen", "Nori")
ROOT = pathlib.Path(__file__).resolve().parents[3]
SOURCE = ROOT / "DancingCatsClone/Library/RadioRigging/Source"


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as source:
        for block in iter(lambda: source.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def require(condition, message):
    if not condition:
        raise RuntimeError(message)


def source_map(directory, character, suffix):
    candidates = [path for path in directory.rglob("*") if path.is_file()
                  and path.name.lower() == f"{character}_{suffix}.png".lower()]
    require(len(candidates) == 1, f"Expected one {character}_{suffix}.png, got {candidates}")
    return candidates[0]


def load_image(path, color_space):
    image = bpy.data.images.load(str(path.resolve()), check_existing=False)
    image.colorspace_settings.name = color_space
    return image


def read_red(path):
    image = load_image(path, "Non-Color")
    size = tuple(image.size)
    pixels = np.empty(size[0] * size[1] * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    red = pixels.reshape((-1, 4))[:, 0].copy()
    require(np.isfinite(red).all(), f"Non-finite pixels in {path}")
    bpy.data.images.remove(image)
    return size, red


def pack_metallic_smoothness(metal_path, rough_path, output):
    size, metal = read_red(metal_path)
    rough_size, rough = read_red(rough_path)
    require(size == rough_size, f"Metallic/roughness sizes differ: {size}, {rough_size}")
    rgba = np.zeros((size[0] * size[1], 4), dtype=np.float32)
    rgba[:, 0] = np.clip(metal, 0., 1.)
    rgba[:, 3] = 1. - np.clip(rough, 0., 1.)
    image = bpy.data.images.new("MetallicSmoothness", width=size[0], height=size[1], alpha=True)
    image.colorspace_settings.name = "Non-Color"
    image.alpha_mode = "CHANNEL_PACKED"
    image.pixels.foreach_set(rgba.ravel())
    image.file_format = "PNG"
    image.filepath_raw = str(output.resolve())
    image.save()
    bpy.data.images.remove(image)
    # Decode the saved file to verify the actual artifact, allowing 8-bit PNG
    # quantisation. Alpha carries data, so RGB must survive zero-alpha pixels.
    encoded = load_image(output, "Non-Color")
    decoded = np.empty(rgba.size, dtype=np.float32)
    encoded.pixels.foreach_get(decoded)
    maximum_error = float(np.max(np.abs(decoded.reshape((-1, 4)) - rgba)))
    require(maximum_error <= 1.01 / 255., f"Packed texture error: {maximum_error}")
    bpy.data.images.remove(encoded)
    return {"size": list(size), "channels": {"R": "metallic", "G": "0", "B": "0", "A": "1 - roughness"},
            "color_space": "linear", "maximum_roundtrip_error": maximum_error}


def make_portable_material(character, output, meshes):
    material = bpy.data.materials.new(character + "_Surface")
    # Blender 5.2 creates node materials by default and deprecates assigning this
    # property. Older compatible versions still need the explicit opt-in.
    if bpy.app.version < (5, 2, 0):
        material.use_nodes = True
    require(material.node_tree is not None, "Blender did not create a node material")
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    shader.location = (340, 20)
    surface = nodes.new("ShaderNodeOutputMaterial")
    surface.location = (650, 20)
    links.new(shader.outputs["BSDF"], surface.inputs["Surface"])
    maps = {}
    for index, (name, color_space) in enumerate((("BaseColor", "sRGB"), ("Normal", "Non-Color"),
                                                ("MetallicSmoothness", "Non-Color"))):
        image = load_image(output / (name + ".png"), color_space)
        image.name = character + "_" + name
        image.filepath = "//" + name + ".png"
        image.filepath_raw = "//" + name + ".png"
        image.alpha_mode = "CHANNEL_PACKED" if name == "MetallicSmoothness" else "STRAIGHT"
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.label = name
        texture.location = (-700, 320 - index * 380)
        maps[name] = texture
    links.new(maps["BaseColor"].outputs["Color"], shader.inputs["Base Color"])
    normal = nodes.new("ShaderNodeNormalMap")
    normal.location = (-180, -60)
    normal.inputs["Strength"].default_value = .7
    links.new(maps["Normal"].outputs["Color"], normal.inputs["Color"])
    links.new(normal.outputs["Normal"], shader.inputs["Normal"])
    separate = nodes.new("ShaderNodeSeparateColor")
    separate.mode = "RGB"
    separate.location = (-250, -360)
    links.new(maps["MetallicSmoothness"].outputs["Color"], separate.inputs["Color"])
    links.new(separate.outputs["Red"], shader.inputs["Metallic"])
    invert = nodes.new("ShaderNodeMath")
    invert.operation = "SUBTRACT"
    invert.inputs[0].default_value = 1.
    invert.location = (-180, -540)
    links.new(maps["MetallicSmoothness"].outputs["Alpha"], invert.inputs[1])
    links.new(invert.outputs[0], shader.inputs["Roughness"])
    for obj in meshes:
        obj.data.materials.clear()
        obj.data.materials.append(material)
    # Delete orphan source datablocks as well as disconnecting them. Thus every
    # file-backed image retained in the authoring project is one of our 3 maps.
    for old in list(bpy.data.materials):
        if old != material and old.users == 0:
            bpy.data.materials.remove(old)
    retained = {texture.image for texture in maps.values()}
    for image in list(bpy.data.images):
        if image not in retained and image.users == 0:
            bpy.data.images.remove(image)
    require(all(image.filepath in ("//BaseColor.png", "//Normal.png", "//MetallicSmoothness.png")
                for image in bpy.data.images if image.source == "FILE"), "External source texture remains")
    return material


def mesh_signature(obj):
    mesh = obj.data
    coordinates = np.empty(len(mesh.vertices) * 3, dtype=np.float32)
    mesh.vertices.foreach_get("co", coordinates)
    digest = hashlib.sha256(coordinates.tobytes())
    require(mesh.uv_layers.active is not None, f"Missing source UVs: {obj.name}")
    uv = np.empty(len(mesh.loops) * 2, dtype=np.float32)
    mesh.uv_layers.active.data.foreach_get("uv", uv)
    digest.update(uv.tobytes())
    require(np.isfinite(coordinates).all() and np.isfinite(uv).all(), f"Invalid geometry: {obj.name}")
    return digest.hexdigest()


def skin_weights(obj, bone_names):
    """Represent weights by bone name, independent of imported group ordering."""
    columns = {name: index for index, name in enumerate(bone_names)}
    groups = {group.index: group.name for group in obj.vertex_groups}
    weights = np.zeros((len(obj.data.vertices), len(bone_names)), dtype=np.float32)
    for vertex in obj.data.vertices:
        for entry in vertex.groups:
            name = groups[entry.group]
            require(name in columns or entry.weight <= .000001, f"Unknown skin bone {name}: {obj.name}")
            if name in columns:
                weights[vertex.index, columns[name]] = entry.weight
    require(np.isfinite(weights).all(), f"Non-finite skin weights: {obj.name}")
    require(float(np.max(np.abs(np.sum(weights, axis=1) - 1.))) < .00001,
            f"Non-normalized skin weights: {obj.name}")
    return weights


def reset_pose(rig, meshes):
    if rig.animation_data:
        rig.animation_data_clear()
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    rig.data.pose_position = "POSE"
    for obj in meshes:
        if obj.animation_data:
            obj.animation_data_clear()
        if obj.data.shape_keys:
            if obj.data.shape_keys.animation_data:
                obj.data.shape_keys.animation_data_clear()
            for key in obj.data.shape_keys.key_blocks:
                key.value = 0.
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def export_fbx(path, rig, meshes):
    for obj in bpy.context.view_layer.objects:
        obj.select_set(False)
    for obj in (rig, *meshes):
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = rig
    properties = bpy.ops.export_scene.fbx.get_rna_type().properties
    options = dict(filepath=str(path.resolve()), use_selection=True, object_types={"ARMATURE", "MESH"},
                   global_scale=1., apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL",
                   use_space_transform=True, bake_space_transform=False, axis_forward="-Z", axis_up="Y",
                   use_mesh_modifiers=False, use_tspace=True, mesh_smooth_type="OFF", use_custom_props=True,
                   add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X",
                   use_armature_deform_only=False, armature_nodetype="NULL", bake_anim=False,
                   path_mode="STRIP", embed_textures=False)
    for key, value in options.items():
        require(key in properties, f"Blender FBX exporter no longer supports {key}")
        prop = properties[key]
        if prop.type == "ENUM":
            allowed = {item.identifier for item in prop.enum_items}
            wanted = value if isinstance(value, set) else {value}
            require(wanted.issubset(allowed), f"Unsupported FBX option {key}={value}; available {allowed}")
    result = bpy.ops.export_scene.fbx(**options)
    require("FINISHED" in result and path.is_file(), f"FBX export failed: {result}")
    return options


def verify_artifacts(output, character, expected):
    """Reopen portable authoring data, then verify an independent FBX import."""
    bpy.ops.wm.open_mainfile(filepath=str(output / (character + "_Rigged.blend")))
    images = [image for image in bpy.data.images if image.source == "FILE"]
    require(len(images) == 3, f"Authoring project contains {len(images)} file images")
    image_sizes = {}
    for image in images:
        path = pathlib.Path(bpy.path.abspath(image.filepath)).resolve()
        require(path.parent == output and path.is_file(), f"Non-portable texture path: {image.filepath}")
        image.reload()
        require(image.size[0] > 0 and image.size[1] > 0, f"Cannot decode {path}")
        image_sizes[path.name] = list(image.size)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    result = bpy.ops.import_scene.fbx(filepath=str(output / "Model.fbx"), use_image_search=False,
                                      use_anim=False, automatic_bone_orientation=False,
                                      use_custom_normals=True)
    require("FINISHED" in result, "FBX verification import failed")
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    require(len(armatures) == 1, f"Roundtrip armature count: {len(armatures)}")
    require({bone.name for bone in armatures[0].data.bones} == expected["bones"], "FBX changed bone names")
    bone_matrix_error = max(abs((armatures[0].matrix_world @ bone.matrix_local)[row][column]
                               - expected["bone_matrices"][bone.name][row][column])
                            for bone in armatures[0].data.bones for row in range(4) for column in range(4))
    require(bone_matrix_error < .00001, f"FBX changed bone rest frames: {bone_matrix_error}")
    imported = {obj.name: obj for obj in bpy.context.scene.objects if obj.type == "MESH"}
    require(set(imported) == set(expected["meshes"]), "FBX changed mesh names or exported review props")
    maximum_vertex_error = 0.
    maximum_uv_error = 0.
    maximum_normal_error = 0.
    maximum_normal_angle = 0.
    maximum_blink_error = 0.
    maximum_weight_error = 0.
    for name, expected_mesh in expected["meshes"].items():
        obj = imported[name]
        require(len(obj.data.vertices) == expected_mesh["vertices"].shape[0], f"FBX changed vertex count: {name}")
        require(len(obj.data.polygons) == expected_mesh["polygons"], f"FBX changed polygon count: {name}")
        require(obj.data.uv_layers.active is not None, f"FBX lost UVs: {name}")
        vertices = np.array([obj.matrix_world @ vertex.co for vertex in obj.data.vertices], dtype=np.float32)
        vertex_error = float(np.max(np.abs(vertices - expected_mesh["vertices"])))
        maximum_vertex_error = max(maximum_vertex_error, vertex_error)
        require(vertex_error < .00001, f"FBX changed world geometry: {name} error={vertex_error}")
        uv = np.empty(len(obj.data.loops) * 2, dtype=np.float32)
        obj.data.uv_layers.active.data.foreach_get("uv", uv)
        require(uv.shape == expected_mesh["uv"].shape, f"FBX changed UV loop count: {name}")
        uv_error = float(np.max(np.abs(uv - expected_mesh["uv"])))
        maximum_uv_error = max(maximum_uv_error, uv_error)
        require(uv_error < .000001, f"FBX changed UVs: {name} error={uv_error}")
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        normals = np.array([(normal_matrix @ normal.vector).normalized() for normal in obj.data.corner_normals], dtype=np.float32)
        require(normals.shape == expected_mesh["normals"].shape, f"FBX changed normal count: {name}")
        normal_error = float(np.max(np.abs(normals - expected_mesh["normals"])))
        maximum_normal_error = max(maximum_normal_error, normal_error)
        # Split normals are encoded again by the FBX importer. Compare their
        # directions with a small angular allowance for that representation,
        # rather than demanding lossless float equality from compressed normals.
        actual_directions = normals.astype(np.float64)
        expected_directions = expected_mesh["normals"].astype(np.float64)
        actual_directions /= np.linalg.norm(actual_directions, axis=1)[:, None]
        expected_directions /= np.linalg.norm(expected_directions, axis=1)[:, None]
        normal_angle = float(np.max(np.degrees(np.arccos(np.clip(np.einsum("ij,ij->i", actual_directions, expected_directions), -1., 1.)))))
        maximum_normal_angle = max(maximum_normal_angle, normal_angle)
        require(normal_angle < .1, f"FBX changed custom normal directions: {name} angle={normal_angle} degrees")
        require(any(mod.type == "ARMATURE" and mod.object == armatures[0] for mod in obj.modifiers), f"FBX lost skin: {name}")
        weights = skin_weights(obj, expected["weight_bones"])
        weight_error = float(np.max(np.abs(weights - expected_mesh["weights"])))
        maximum_weight_error = max(maximum_weight_error, weight_error)
        require(weight_error < .00001, f"FBX changed vertex/bone weights: {name} error={weight_error}")
        blink = obj.data.shape_keys and obj.data.shape_keys.key_blocks.get("Blink")
        if expected_mesh["blink"]:
            require(blink is not None and abs(blink.value) < .000001, f"FBX lost/reset Blink: {name}")
            blink_coordinates = np.array([obj.matrix_world @ vertex.co for vertex in blink.data], dtype=np.float32)
            blink_error = float(np.max(np.abs(blink_coordinates - expected_mesh["blink_coordinates"])))
            maximum_blink_error = max(maximum_blink_error, blink_error)
            require(blink_error < .00001, f"FBX changed Blink geometry: {name} error={blink_error}")
    return {"portable_blend_reopened": True, "fbx_reimported": True, "mesh_names_match": True,
            "bone_names_match": True, "normalized_skin_weights": True,
            "maximum_weight_error": maximum_weight_error,
            "maximum_vertex_error": maximum_vertex_error, "maximum_uv_error": maximum_uv_error,
            "maximum_normal_error": maximum_normal_error, "maximum_blink_error": maximum_blink_error,
            "maximum_normal_angle_degrees": maximum_normal_angle,
            "maximum_bone_rest_matrix_error": bone_matrix_error,
            "image_sizes": image_sizes}


def export_review(blend, output, character, source):
    source_manifest_path = blend.parent / "rig-manifest.json"
    require(source_manifest_path.is_file(), f"Missing reviewed manifest: {source_manifest_path}")
    review_digest = sha256(blend)
    review_manifest_digest = sha256(source_manifest_path)
    source_manifest = json.loads(source_manifest_path.read_text(encoding="utf-8"))
    require(source_manifest["character"] == character, "Character/manifest mismatch")
    bpy.ops.wm.open_mainfile(filepath=str(blend))
    armatures = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    require(len(armatures) == 1, f"Expected one rig, found {len(armatures)}")
    rig = armatures[0]
    meshes = sorted((obj for obj in bpy.context.scene.objects if obj.type == "MESH" and "mesh_role" in obj), key=lambda obj: obj.name)
    require({obj.name for obj in meshes} == {item["name"] for item in source_manifest["meshes"]}, "Reviewed mesh set changed")
    require({bone.name for bone in rig.data.bones} == {item["name"] for item in source_manifest["bones"]}, "Reviewed bone set changed")
    described = {item["name"]: item for item in source_manifest["meshes"]}
    for obj in meshes:
        require(len(obj.data.vertices) == described[obj.name]["vertices"]
                and len(obj.data.polygons) == described[obj.name]["triangles"], f"Stale mesh manifest: {obj.name}")
        require(any(mod.type == "ARMATURE" and mod.object == rig for mod in obj.modifiers), f"Missing skin: {obj.name}")
        require(len(obj.vertex_groups) > 0, f"Missing weights: {obj.name}")
    blink_meshes = [obj for obj in meshes if obj.data.shape_keys and "Blink" in obj.data.shape_keys.key_blocks]
    require(blink_meshes, "Reviewed rig must contain Blink shape key")
    signatures = {obj.name: mesh_signature(obj) for obj in meshes}
    reset_pose(rig, meshes)
    base_source = source_map(source, character, "basecolor")
    normal_source = source_map(source, character, "normal")
    for original, name in ((base_source, "BaseColor.png"), (normal_source, "Normal.png")):
        shutil.copyfile(original, output / name)
        require(sha256(original) == sha256(output / name), f"Source texture byte mismatch: {name}")
    packing = pack_metallic_smoothness(source_map(source, character, "metallic"),
                                       source_map(source, character, "roughness"), output / "MetallicSmoothness.png")
    make_portable_material(character, output, meshes)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.
    fbx_options = export_fbx(output / "Model.fbx", rig, meshes)
    require(signatures == {obj.name: mesh_signature(obj) for obj in meshes}, "Export changed mesh geometry or UVs")
    artist_file = output / (character + "_Rigged.blend")
    # Relative paths are already anchored to the destination. Prevent save_as
    # remapping them relative to the input Review folder.
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=str(artist_file), relative_remap=False)
    bounds = [obj.matrix_world @ Vector(corner) for obj in meshes for corner in obj.bound_box]
    expected = {"bones": {bone.name for bone in rig.data.bones}, "meshes": {},
                "weight_bones": sorted(bone.name for bone in rig.data.bones),
                "bone_matrices": {bone.name: rig.matrix_world @ bone.matrix_local for bone in rig.data.bones}}
    for obj in meshes:
        uv = np.empty(len(obj.data.loops) * 2, dtype=np.float32)
        obj.data.uv_layers.active.data.foreach_get("uv", uv)
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        blink = obj.data.shape_keys.key_blocks.get("Blink") if obj.data.shape_keys else None
        expected["meshes"][obj.name] = {
            "vertices": np.array([obj.matrix_world @ vertex.co for vertex in obj.data.vertices], dtype=np.float32),
            "polygons": len(obj.data.polygons), "uv": uv,
            "weights": skin_weights(obj, expected["weight_bones"]),
            "normals": np.array([(normal_matrix @ normal.vector).normalized() for normal in obj.data.corner_normals], dtype=np.float32),
            "blink": blink is not None,
            "blink_coordinates": np.array([obj.matrix_world @ vertex.co for vertex in blink.data], dtype=np.float32) if blink else None,
        }
    mesh_count, bone_count, blink_names = len(meshes), len(rig.data.bones), [obj.name for obj in blink_meshes]
    manifest = dict(source_manifest)
    manifest.update({
        "exporter": "export_prepared_rigs.py", "blender_version": bpy.app.version_string,
        "source_review_sha256": review_digest, "source_review_manifest_sha256": review_manifest_digest,
        "model": "Model.fbx", "authoring_project": artist_file.name,
        "fbx_coordinates": "forward -Z, up Y; Blender source faces -Y, Unity import faces +Z",
        "height": max(v.z for v in bounds) - min(v.z for v in bounds),
        "bounds_blender": {"min": [min(v[i] for v in bounds) for i in range(3)], "max": [max(v[i] for v in bounds) for i in range(3)]},
        "mesh_uv_geometry_sha256": signatures,
        "skinned_mesh_count": mesh_count, "bone_count": bone_count,
        "blink_meshes": blink_names,
        "texture_packing": packing,
        "texture_sources": {"BaseColor.png": {"source_filename": base_source.name, "sha256": sha256(base_source)},
                            "Normal.png": {"source_filename": normal_source.name, "sha256": sha256(normal_source)}},
        "fbx_options": {key: sorted(value) if isinstance(value, set) else value for key, value in fbx_options.items() if key != "filepath"},
        "files": {path.name: {"bytes": path.stat().st_size, "sha256": sha256(path)} for path in
                  (output / "Model.fbx", artist_file, output / "BaseColor.png", output / "Normal.png", output / "MetallicSmoothness.png")},
    })
    manifest["verification"] = verify_artifacts(output, character, expected)
    require(review_digest == sha256(blend) and review_manifest_digest == sha256(source_manifest_path),
            "Reviewed input changed during export; wait for the author's freeze before retrying")
    (output / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    return manifest


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blend", type=pathlib.Path, required=True)
    parser.add_argument("--out", type=pathlib.Path, required=True)
    parser.add_argument("--character", choices=CHARACTERS)
    parser.add_argument("--source", type=pathlib.Path, help="Optional extracted atlas directory")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    blend = args.blend.resolve()
    output = args.out.resolve()
    character = args.character or blend.parent.name
    require(character in CHARACTERS, f"Cannot infer character from {blend}; use --character")
    require(blend.is_file(), f"Missing reviewed Blender file: {blend}")
    require(output != blend.parent and not output.is_relative_to(blend.parent), "Output must not overwrite reviewed input")
    source = (args.source or SOURCE / character).resolve()
    require(not output.is_relative_to(source), "Output must not overwrite original atlas files")
    output.parent.mkdir(parents=True, exist_ok=True)
    # Only promote a complete, verified payload. A failed retry leaves the
    # previous good payload intact rather than mixing meshes/maps/manifests.
    with tempfile.TemporaryDirectory(prefix=".radio-export-", dir=output.parent) as temporary:
        staging = pathlib.Path(temporary).resolve()
        manifest = export_review(blend, staging, character, source)
        output.mkdir(parents=True, exist_ok=True)
        for name in (*manifest["files"], "manifest.json"):
            (staging / name).replace(output / name)
    print(f"RADIO_EXPORT PASS character={character} meshes={manifest['skinned_mesh_count']} "
          f"bones={manifest['bone_count']} blink={len(manifest['blink_meshes'])} out={output}", flush=True)


if __name__ == "__main__":
    main()
