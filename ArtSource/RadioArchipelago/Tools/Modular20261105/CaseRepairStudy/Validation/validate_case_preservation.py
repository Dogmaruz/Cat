"""Read-only Blender CLI evidence for cleaned backpack CASES, not only their rotors.

Example:
  blender --background --python validate_case_preservation.py -- \
    --candidate ../StraplessCases/Localized --ids Miro_Garden Rumi_Orbit

The source and derivative .blend files are only read. No meshes or user scenes
are saved. Protected surfaces are sampled independently of the cleanup method.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
import numpy as np
from mathutils import Vector
from mathutils.bvhtree import BVHTree


HERE = Path(__file__).resolve().parent
STRAPLESS = HERE.parents[1] / "StraplessCases"
SAMPLES = np.asarray(((1/3, 1/3, 1/3), (.75, .125, .125),
                      (.125, .75, .125), (.125, .125, .75)), dtype=np.float64)


def arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--before", type=Path, default=STRAPLESS / "Before")
    parser.add_argument("--candidate", type=Path, default=STRAPLESS / "Localized")
    parser.add_argument("--out", type=Path, default=HERE / "case-preservation.json")
    parser.add_argument("--ids", nargs="*")
    parser.add_argument("--tolerance", type=float, default=.0002,
                        help="Surface tolerance in prepared Blender units (default 0.2 mm).")
    return parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])


def snapshot(path):
    blend_digest = hashlib.sha256(path.read_bytes()).hexdigest()
    bpy.ops.wm.open_mainfile(filepath=str(path.resolve()), load_ui=False)
    obj = bpy.data.objects.get("Case")
    if obj is None or obj.type != 'MESH':
        raise ValueError(f"{path}: missing Case mesh")
    mesh = obj.data
    mesh.calc_loop_triangles()
    positions = np.empty((len(mesh.vertices), 3), dtype=np.float64)
    mesh.vertices.foreach_get("co", positions.ravel())
    matrix = np.asarray(obj.matrix_world, dtype=np.float64)
    positions = positions @ matrix[:3, :3].T + matrix[:3, 3]
    indices = np.asarray([tuple(t.vertices) for t in mesh.loop_triangles], dtype=np.int32)
    loops = np.asarray([tuple(t.loops) for t in mesh.loop_triangles], dtype=np.int32)
    if mesh.uv_layers.active is None:
        uv = np.zeros((len(mesh.loops), 2), dtype=np.float64)
        has_uv = False
    else:
        uv = np.empty((len(mesh.loops), 2), dtype=np.float64)
        mesh.uv_layers.active.data.foreach_get("uv", uv.ravel())
        has_uv = True
    triangles = positions[indices]
    cross = np.cross(triangles[:, 1] - triangles[:, 0], triangles[:, 2] - triangles[:, 0])
    lengths = np.linalg.norm(cross, axis=1)
    normals = cross / np.maximum(lengths[:, None], 1e-20)
    rotor = bpy.data.objects.get("TopRotor")
    rotor_digest = None
    if rotor is not None and rotor.type == 'MESH':
        h = hashlib.sha256()
        for v in rotor.data.vertices:
            h.update(np.asarray(v.co, dtype='<f4').tobytes())
        for f in rotor.data.polygons:
            h.update(np.asarray(f.vertices, dtype='<i4').tobytes())
        if rotor.data.uv_layers.active:
            for loop in rotor.data.uv_layers.active.data:
                h.update(np.asarray(loop.uv, dtype='<f4').tobytes())
        h.update(np.asarray(rotor.matrix_world, dtype='<f4').tobytes())
        rotor_digest = h.hexdigest()
    return dict(path=str(path.resolve()), blendSha256=blend_digest, vertices=positions, indices=indices,
                triangles=triangles, uv=uv[loops], areas=lengths * .5,
                normals=normals, hasUV=has_uv, rotorDigest=rotor_digest,
                bvh=BVHTree.FromPolygons([tuple(v) for v in positions],
                                         [tuple(t) for t in indices], all_triangles=True))


def triangle_key(triangle, uv):
    # Positions and per-corner UVs must survive together; old vertices with a
    # flat replacement UV cannot masquerade as an untouched decorative face.
    values = np.rint(np.concatenate((triangle, uv), axis=1) * 1e6).astype('<i8')
    order = np.lexsort(tuple(values.T[::-1]))
    return hashlib.blake2b(values[order].tobytes(), digest_size=16).digest()


def barycentric(point, triangle):
    a, b, c = triangle
    u = b - a
    v = c - a
    w = point - a
    uu, uv, vv = np.dot(u, u), np.dot(u, v), np.dot(v, v)
    wu, wv = np.dot(w, u), np.dot(w, v)
    denominator = uu * vv - uv * uv
    if abs(denominator) < 1e-20:
        return None
    q = (vv * wu - uv * wv) / denominator
    r = (uu * wv - uv * wu) / denominator
    return np.asarray((1 - q - r, q, r))


def matched_uv(candidate, triangle_index, hit_point, expected, tolerance=.0005):
    weights = barycentric(np.asarray(hit_point), candidate['triangles'][triangle_index])
    if weights is None or not candidate['hasUV']:
        return False
    actual = weights @ candidate['uv'][triangle_index]
    return bool(np.linalg.norm(actual - expected) <= tolerance)


def percent(part, total):
    return None if total <= 1e-12 else 100 * float(part) / float(total)


def region_measure(source, candidate, mask, candidate_keys, tolerance, name, threshold):
    selected = np.flatnonzero(mask & (source['areas'] > 1e-12))
    total_area = float(source['areas'][selected].sum())
    exact_area = matched_area = uv_area = visible_area = visible_matched = visible_uv = 0.0
    max_surface_error = 0.0
    lost_visible = []
    for i in selected:
        tri, uv, area = source['triangles'][i], source['uv'][i], float(source['areas'][i])
        if triangle_key(tri, uv) in candidate_keys:
            exact_area += area
        for weights in SAMPLES:
            point = weights @ tri
            expected_uv = weights @ uv
            sample_area = area / len(SAMPLES)
            hit, normal, index, distance = candidate['bvh'].find_nearest(Vector(point), tolerance)
            if hit is not None:
                # A new flat cap just below a relief is not the same surface.
                alignment = abs(float(np.dot(source['normals'][i], candidate['normals'][index])))
                if alignment >= .95:
                    matched_area += sample_area
                    max_surface_error = max(max_surface_error, float(distance))
                    if matched_uv(candidate, index, hit, expected_uv):
                        uv_area += sample_area

            # Compare only samples genuinely visible from the radio/front (+Y)
            # side in the source, not hidden interior faces or rear mount tabs.
            start = Vector(point + (0, 3, 0))
            direction = Vector((0, -1, 0))
            source_hit = source['bvh'].ray_cast(start, direction, 6)
            if source_hit[0] is None or (source_hit[0] - Vector(point)).length > tolerance * 2:
                continue
            visible_area += sample_area
            candidate_hit = candidate['bvh'].ray_cast(start, direction, 6)
            if candidate_hit[0] is not None and (candidate_hit[0] - Vector(point)).length <= tolerance:
                visible_matched += sample_area
                if matched_uv(candidate, candidate_hit[2], candidate_hit[0], expected_uv):
                    visible_uv += sample_area
            else:
                lost_visible.append((sample_area, point.copy()))

    loss_evidence = None
    if lost_visible:
        locations = np.asarray([point for area, point in lost_visible])
        weights = np.asarray([area for area, point in lost_visible])
        loss_evidence = dict(minimum=locations.min(axis=0).tolist(), maximum=locations.max(axis=0).tolist(),
                            areaWeightedCenter=np.average(locations, axis=0, weights=weights).tolist(),
                            largestSamples=[dict(area=area, point=point.tolist()) for area, point in
                                sorted(lost_visible, key=lambda value: value[0], reverse=True)[:16]])
    return dict(region=name, minimumProtectedY=float(threshold), sourceTriangles=int(len(selected)),
                sourceArea=total_area, untouchedTriangleArea=exact_area,
                untouchedTriangleAreaPercent=percent(exact_area, total_area),
                matchingSurfaceArea=matched_area,
                matchingSurfaceAreaPercent=percent(matched_area, total_area),
                matchingSurfaceAndUVAreaPercent=percent(uv_area, total_area),
                sourceFrontVisibleArea=visible_area,
                retainedFrontVisibleAreaPercent=percent(visible_matched, visible_area),
                retainedFrontVisibleSurfaceAndUVPercent=percent(visible_uv, visible_area),
                maximumMatchedSurfaceError=max_surface_error,
                missingFrontVisibleSamples=loss_evidence)


def compare(name, before_folder, candidate_folder, tolerance):
    meta = json.loads((before_folder / 'Meta.json').read_text(encoding='utf-8'))
    source = snapshot(before_folder / 'editable.blend')
    candidate = snapshot(candidate_folder / 'editable.blend')
    keys = {triangle_key(t, uv) for t, uv in zip(candidate['triangles'], candidate['uv'])}
    # The same source metadata is used for all candidates, never their rewritten
    # bounds or contact plane. This prevents a damaged derivative redefining the
    # protected region to exclude its missing front facade.
    low_y, high_y = meta['caseBoundsMin'][1], meta['caseBoundsMax'][1]
    adaptive = max(0.0, low_y + (high_y - low_y) * .60)
    minimum_y = source['triangles'][:, :, 1].min(axis=1)
    regions = [region_measure(source, candidate, minimum_y >= -1e-6, keys, tolerance,
                              'all_original_case_triangles_at_Y_ge_0', 0.0),
               region_measure(source, candidate, minimum_y >= adaptive - 1e-6, keys, tolerance,
                              'conservative_front_depth_band', adaptive)]
    broad, protected = regions
    # This extra diagnostic excludes only a fixed outer rim and roof/floor band,
    # where intentional removal of original strap attachments can otherwise
    # dominate broad area totals. Only its FRONT-VISIBLE metric describes the
    # decorative panel: back-facing source padding may still lie at positive Y.
    minimum, maximum = np.asarray(meta['caseBoundsMin']), np.asarray(meta['caseBoundsMax'])
    center_x = (minimum[0] + maximum[0]) * .5
    half_x = (maximum[0] - minimum[0]) * .42
    low_z = minimum[2] + (maximum[2] - minimum[2]) * .08
    high_z = maximum[2] - (maximum[2] - minimum[2]) * .10
    core = ((minimum_y >= -1e-6) &
            (np.abs(source['triangles'][:, :, 0] - center_x).max(axis=1) <= half_x) &
            (source['triangles'][:, :, 2].min(axis=1) >= low_z) &
            (source['triangles'][:, :, 2].max(axis=1) <= high_z))
    central = region_measure(source, candidate, core, keys, tolerance,
                             'central_panel_excluding_outer_8percent_X_and_8to10percent_Z', 0.0)
    central['sourceRegionBounds'] = dict(minimumX=float(center_x-half_x), maximumX=float(center_x+half_x),
                                        minimumZ=float(low_z), maximumZ=float(high_z))
    regions.append(central)
    # This is a deliberately conservative geometry-preservation gate, not a
    # claim that new rear padding is well sculpted or has no strap remnants.
    passed = (protected['sourceArea'] > 0 and
              (protected['matchingSurfaceAreaPercent'] or 0) >= 99.5 and
              (protected['matchingSurfaceAndUVAreaPercent'] or 0) >= 99.0 and
              (protected['retainedFrontVisibleAreaPercent'] or 0) >= 99.5 and
              (protected['retainedFrontVisibleSurfaceAndUVPercent'] or 0) >= 99.0 and
              (broad['retainedFrontVisibleAreaPercent'] or 0) >= 99.5 and
              (broad['retainedFrontVisibleSurfaceAndUVPercent'] or 0) >= 99.0)
    return dict(id=name, before=source['path'], candidate=candidate['path'],
                sourceBlendSha256=source['blendSha256'], candidateBlendSha256=candidate['blendSha256'],
                sourceCaseVertices=len(source['vertices']), candidateCaseVertices=len(candidate['vertices']),
                sourceCaseTriangles=len(source['triangles']), candidateCaseTriangles=len(candidate['triangles']),
                sourceTotalCaseArea=float(source['areas'].sum()),
                candidateTotalCaseArea=float(candidate['areas'].sum()),
                sourceCaseBoundsMinimum=np.min(source['vertices'], axis=0).tolist(),
                sourceCaseBoundsMaximum=np.max(source['vertices'], axis=0).tolist(),
                candidateCaseBoundsMinimum=np.min(candidate['vertices'], axis=0).tolist(),
                candidateCaseBoundsMaximum=np.max(candidate['vertices'], axis=0).tolist(),
                rotorUnchanged=source['rotorDigest'] == candidate['rotorDigest'],
                protectedFacadeGeometryPassed=bool(passed),
                centralFrontVisibleFacadePreserved=bool(central['sourceFrontVisibleArea'] > 0 and
                    (central['retainedFrontVisibleAreaPercent'] or 0) >= 99.5 and
                    (central['retainedFrontVisibleSurfaceAndUVPercent'] or 0) >= 99.0),
                regions=regions)


def main():
    args = arguments()
    names = args.ids or sorted(p.name for p in args.candidate.iterdir()
                               if p.is_dir() and (p / 'editable.blend').exists())
    rows = []
    for name in names:
        try:
            row = compare(name, args.before / name, args.candidate / name, args.tolerance)
            primary = row['regions'][1]
            print('CASE_PRESERVATION', name, 'pass=' + str(row['protectedFacadeGeometryPassed']),
                  'surface=' + str(primary['matchingSurfaceAreaPercent']),
                  'visible=' + str(primary['retainedFrontVisibleAreaPercent']), flush=True)
        except Exception as error:
            row = dict(id=name, protectedFacadeGeometryPassed=False, error=repr(error))
            print('CASE_PRESERVATION_ERROR', name, repr(error), flush=True)
        rows.append(row)
        report = dict(passed=bool(rows) and all(r['protectedFacadeGeometryPassed'] for r in rows),
                      sourceFolder=str(args.before.resolve()), candidateFolder=str(args.candidate.resolve()),
                      surfaceTolerance=args.tolerance, uvTolerance=.0005,
                      samplesPerOriginalTriangle=len(SAMPLES), entries=rows,
                      limitation='A pass proves approximate preservation of protected original front surfaces, UVs and front visibility only. It does not certify clean rear strap removal, smoothness, color, style, appropriate padding depth, complete original side surfaces, performance or artistic quality. The separate rotor flag is never sufficient for a case preservation pass.')
        args.out.parent.mkdir(parents=True, exist_ok=True)
        args.out.write_text(json.dumps(report, indent=2, allow_nan=False), encoding='utf-8')
    print('CASE_PRESERVATION_REPORT', args.out.resolve(), flush=True)


if __name__ == '__main__':
    main()
