"""Read supplied ZIP/FBX model metadata without importing or modifying assets."""
from __future__ import annotations

import argparse
import hashlib
import json
import pathlib
import struct
import zipfile
import zlib
from array import array


class FbxReader:
    def __init__(self, data: bytes):
        if not data.startswith(b"Kaydara FBX Binary  \x00\x1a\x00"):
            raise ValueError("Only binary FBX is supported")
        self.data = data
        self.version = struct.unpack_from("<I", data, 23)[0]
        self.wide = self.version >= 7500
        self.header = 25 if self.wide else 13
        self.pos = 27

    def prop(self, preserve_array: bool):
        t = chr(self.data[self.pos])
        self.pos += 1
        formats = {"Y": "h", "C": "?", "I": "i", "F": "f", "D": "d", "L": "q"}
        if t in formats:
            fmt = "<" + formats[t]
            value = struct.unpack_from(fmt, self.data, self.pos)[0]
            self.pos += struct.calcsize(fmt)
            return value
        if t in ("S", "R"):
            count = struct.unpack_from("<I", self.data, self.pos)[0]
            self.pos += 4
            value = self.data[self.pos:self.pos + count]
            self.pos += count
            return value.decode("utf-8", errors="replace") if t == "S" else {"bytes": count}
        if t in "fdlibc":
            count, encoding, size = struct.unpack_from("<III", self.data, self.pos)
            self.pos += 12
            payload = self.data[self.pos:self.pos + size]
            self.pos += size
            if not preserve_array:
                return {"array_type": t, "count": count, "encoding": encoding, "stored_bytes": size}
            payload = zlib.decompress(payload) if encoding else payload
            a = array({"f": "f", "d": "d", "l": "q", "i": "i", "b": "b", "c": "b"}[t])
            a.frombytes(payload)
            return a
        raise ValueError(f"Unknown FBX property {t!r} at {self.pos - 1}")

    def node(self):
        start = self.pos
        fmt = "<QQQB" if self.wide else "<IIIB"
        end, count, prop_bytes, name_len = struct.unpack_from(fmt, self.data, start)
        self.pos += self.header
        if not end:
            return None
        name = self.data[self.pos:self.pos + name_len].decode("utf-8", errors="replace")
        self.pos += name_len
        preserve = name in ("Vertices", "PolygonVertexIndex")
        props = [self.prop(preserve) for _ in range(count)]
        children = []
        while self.pos < end:
            child = self.node()
            if child is None:
                break
            children.append(child)
        self.pos = end
        return {"name": name, "props": props, "children": children}

    def roots(self):
        nodes = []
        while self.pos + self.header <= len(self.data):
            n = self.node()
            if n is None:
                break
            nodes.append(n)
        return nodes


def walk(nodes):
    for n in nodes:
        yield n
        yield from walk(n["children"])


def clean_name(value):
    return value.split("\x00", 1)[0] if isinstance(value, str) else value


def connected_components(vertices, polygon_indices):
    """Topological components; does not weld nearby positions or modify geometry."""
    parents = list(range(len(vertices) // 3))
    def find(i):
        while parents[i] != i:
            parents[i] = parents[parents[i]]
            i = parents[i]
        return i
    polygon = []
    for value in polygon_indices:
        polygon.append(value if value >= 0 else -value - 1)
        if value < 0:
            a = find(polygon[0])
            for i in polygon[1:]:
                b = find(i)
                if a != b:
                    parents[b] = a
            polygon.clear()
    groups = {}
    for i in range(len(parents)):
        groups.setdefault(find(i), []).append(i)
    components = []
    for indices in groups.values():
        components.append({"vertices": len(indices), "bounds_min": [min(vertices[i * 3 + a] for i in indices) for a in range(3)], "bounds_max": [max(vertices[i * 3 + a] for i in indices) for a in range(3)]})
    return sorted(components, key=lambda c: c["vertices"], reverse=True)


def inspect_fbx(data):
    reader = FbxReader(data)
    nodes = reader.roots()
    objects = next(n for n in nodes if n["name"] == "Objects")["children"]
    result = {"fbx_version": reader.version, "geometries": [], "models": [], "materials": [], "deformers": [], "animation_stacks": [], "textures": []}
    for n in objects:
        p = n["props"]
        desc = {"id": p[0], "name": clean_name(p[1]) if len(p) > 1 else "", "type": p[2] if len(p) > 2 else ""}
        if n["name"] == "Geometry":
            desc["layers"] = [c["name"] for c in n["children"] if c["name"].startswith("LayerElement")]
            vertices = next((c["props"][0] for c in n["children"] if c["name"] == "Vertices"), [])
            polygons = next((c["props"][0] for c in n["children"] if c["name"] == "PolygonVertexIndex"), [])
            desc["vertices"] = len(vertices) // 3
            desc["polygon_corners"] = len(polygons)
            desc["polygons"] = sum(v < 0 for v in polygons)
            desc["triangles_after_fan"] = len(polygons) - 2 * desc["polygons"]
            if vertices:
                desc["bounds_min"] = [min(vertices[i::3]) for i in range(3)]
                desc["bounds_max"] = [max(vertices[i::3]) for i in range(3)]
            desc["blendshape_children"] = [c["name"] for c in n["children"] if "Shape" in c["name"]]
            desc["connected_components"] = connected_components(vertices, polygons)
            result["geometries"].append(desc)
        elif n["name"] == "Model":
            desc["transforms"] = [c["props"] for c in walk(n["children"]) if c["name"] == "P" and c["props"][0] in ("Lcl Translation", "Lcl Rotation", "Lcl Scaling", "PreRotation", "PostRotation")]
            result["models"].append(desc)
        elif n["name"] == "Material":
            desc["properties"] = [c["props"] for c in walk(n["children"]) if c["name"] == "P"]
            result["materials"].append(desc)
        elif n["name"] == "Deformer":
            result["deformers"].append(desc)
        elif n["name"] == "AnimationStack":
            result["animation_stacks"].append(desc)
        elif n["name"] in ("Texture", "Video"):
            desc["paths"] = {c["name"]: c["props"][0] for c in n["children"] if c["name"] in ("FileName", "Filename", "RelativeFilename")}
            desc["embedded_content"] = [c["props"][0] for c in n["children"] if c["name"] == "Content"]
            result["textures"].append(desc)
    result["node_type_counts"] = {key: sum(n["name"] == key for n in objects) for key in sorted({n["name"] for n in objects})}
    result["global_settings"] = [n["props"] for n in walk(nodes) if n["name"] == "P" and n["props"] and n["props"][0] in ("UnitScaleFactor", "OriginalUnitScaleFactor", "UpAxis", "UpAxisSign", "FrontAxis", "FrontAxisSign", "CoordAxis", "CoordAxisSign")]
    return result


def inspect_zip(path):
    with path.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    result = {"path": str(path), "archive_bytes": path.stat().st_size, "sha256": digest, "files": []}
    with zipfile.ZipFile(path) as archive:
        for entry in archive.infolist():
            item = {"name": entry.filename, "bytes": entry.file_size}
            suffix = pathlib.PurePosixPath(entry.filename).suffix.lower()
            if suffix == ".png":
                with archive.open(entry) as stream:
                    header = stream.read(33)
                if header.startswith(b"\x89PNG\r\n\x1a\n"):
                    width, height, depth, color = struct.unpack_from(">IIBB", header, 16)
                    item["png"] = {"width": width, "height": height, "bit_depth": depth, "color_type": color}
            elif suffix == ".fbx":
                item["fbx"] = inspect_fbx(archive.read(entry))
            result["files"].append(item)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("root", type=pathlib.Path)
    parser.add_argument("--output", type=pathlib.Path)
    args = parser.parse_args()
    result = {name: inspect_zip(args.root / name / (name + ".zip")) for name in ("Tiko", "Lumen", "Nori")}
    text = json.dumps(result, indent=2, ensure_ascii=False)
    if args.output:
        args.output.write_text(text + "\n", encoding="utf-8")
    else:
        print(text)


if __name__ == "__main__":
    main()
