"""Close supplied flipper roots without reusing their surface diagonals.

The outer surface, its vertices, UVs and saved corner normals remain untouched.
Only a narrow hidden inset collar and its triangulated cap are authored here.
The caller assigns every new vertex to the corresponding arm bone.
"""
from __future__ import annotations

import bmesh
from mathutils import Vector


def _ordered_boundary_loops(bm, edges):
    bm.verts.index_update()
    boundary = set(edges)
    for edge in edges:
        if not edge.is_valid or not edge.is_boundary:
            raise ValueError("Root cap input must contain only open boundary edges")
    remaining = set(edges)
    loops = []
    while remaining:
        first = min(remaining, key=lambda edge: tuple(sorted(vertex.index for vertex in edge.verts)))
        source = first.link_loops[0]
        ordered = [source.link_loop_next.vert]
        current = first
        while True:
            if current not in remaining:
                raise ValueError("A root boundary revisits an edge before closing")
            remaining.remove(current)
            source = current.link_loops[0]
            ordered.append(source.vert)

            # Follow the incident FACE fan, rather than choosing a neighboring
            # edge by vertex degree. The supplied Tiko has two surface fans
            # touching at a vertex; their boundary degree is four, while each
            # fan still has one unambiguous incoming and outgoing half-edge.
            walk = source.link_loop_prev
            steps = 0
            while walk.edge not in boundary:
                if not walk.edge.is_manifold or not walk.edge.is_contiguous:
                    raise ValueError("The supplied flipper has an open or inconsistent face fan")
                walk = walk.link_loop_radial_next.link_loop_prev
                steps += 1
                if steps > len(bm.faces):
                    raise ValueError("A root face fan does not reach another boundary")
            if walk.link_loop_next.vert != ordered[-1]:
                raise ValueError("The supplied flipper has inconsistent boundary winding")
            current = walk.edge
            if current == first:
                if ordered[-1] != ordered[0]:
                    raise ValueError("A root boundary does not close at its initial vertex")
                ordered.pop()
                break
        if len(ordered) < 3:
            raise ValueError("A root boundary needs at least three edges")
        # Every step is opposite its original face. A pinched loop may visit
        # the same source vertex twice; each visit receives its own inner
        # vertex, so the supplied surface never needs splitting or welding.
        loops.append(ordered)
    return loops


def close_root_caps(bm, boundary_edges, side, cap_layer, uv_layer, normal_layer, cap_uv):
    """Return new triangular cap/collar faces, preserving the supplied surface.

    ``side`` is ``"L"`` for the positive-X flipper and ``"R"`` for negative X.
    ``cap_layer`` is a face-int layer; the UV and saved-normal layers are loop
    layers. New vertices are intentionally not welded to other source geometry.
    """
    if side not in ("L", "R"):
        raise ValueError("Flipper side must be L or R")
    edges = list(dict.fromkeys(boundary_edges))
    if not edges:
        return []
    loops = _ordered_boundary_loops(bm, edges)
    source_faces = set(bm.faces)
    source_vertices = set(bm.verts)
    inward = -1 if side == "L" else 1
    cap_normal = Vector((inward, 0, 0))
    outer_normals = {}
    for ordered in loops:
        for vertex in ordered:
            value = sum((loop[normal_layer] for loop in vertex.link_loops), Vector())
            outer_normals[vertex] = value.normalized() if value.length_squared > 1e-12 else cap_normal.copy()

    inner_vertices = set()
    for ordered in loops:
        extreme = min(vertex.co.x for vertex in ordered) if side == "L" else max(vertex.co.x for vertex in ordered)
        plane = extreme + inward * .035
        inner = []
        for vertex in ordered:
            point = vertex.co.copy()
            point.x = plane
            inner.append(bm.verts.new(point))
            inner_vertices.add(inner[-1])
        for index, vertex in enumerate(ordered):
            following = (index + 1) % len(ordered)
            bm.faces.new((vertex, ordered[following], inner[following], inner[index]))
        bm.faces.new(inner)

    authored = [face for face in bm.faces if face not in source_faces]
    bmesh.ops.triangulate(bm, faces=authored, quad_method="BEAUTY", ngon_method="BEAUTY")
    # Bisect/subdivide produces collinear boundary samples. Remove degenerate
    # triangles only through the NEW inner topology; source seams are immutable.
    bmesh.ops.dissolve_degenerate(
        bm, dist=1e-6,
        edges=[edge for edge in bm.edges if all(vertex in inner_vertices for vertex in edge.verts)],
    )
    authored = [face for face in bm.faces if face not in source_faces]
    bmesh.ops.triangulate(bm, faces=authored, quad_method="BEAUTY", ngon_method="BEAUTY")
    authored = [face for face in bm.faces if face not in source_faces]

    uv_offsets = (Vector((0, 0)), Vector((.00002, 0)), Vector((0, .00002)))
    for face in authored:
        face[cap_layer] = 1
        face.smooth = True
        for corner, loop in enumerate(face.loops):
            loop[uv_layer].uv = cap_uv + uv_offsets[corner]
            # Match the original seam's shading, blending into the inward cap.
            loop[normal_layer] = outer_normals.get(loop.vert, cap_normal)

    if any(not face.is_valid for face in source_faces) or any(not vertex.is_valid for vertex in source_vertices):
        raise RuntimeError("Root closure unexpectedly changed the supplied surface")
    if any(edge.is_boundary for edge in bm.edges):
        raise RuntimeError("Root closure left an open boundary")
    if any(len(edge.link_faces) > 2 for edge in bm.edges):
        raise RuntimeError("Root closure introduced a nonmanifold edge")
    if any(face.calc_area() < 1e-12 for face in authored):
        raise RuntimeError("Root closure contains a degenerate triangle")
    return authored
