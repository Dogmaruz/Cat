"""Independent exact-polyline building pad planner; no Blender/Unity imports.

Reads V2 closed guides and the literal KINDS list in build_worlds.py. Reproduces
terrain_road's .8m road and 2.2m outline resampling. Candidate constraints are
unchanged: normal center >20m road/>13m shore, compact >14m/>8m, normals >24m
apart, compact >23m from normals. Bitset backtracking searches all feasible
normal cliques until the time budget, assigning building types to guide targets.
Finer grids are only attempted if the complete coarser-grid search proves no
solution. Centers are a layout proposal, not proof about final mesh extents.
"""
import argparse
import ast
import itertools
import json
import math
from pathlib import Path
import time


def resample(points, spacing):
    points = [tuple(p[:2]) for p in points]
    if len(points) > 1 and math.dist(points[0], points[-1]) < 1e-5:
        points.pop()
    lengths = [math.dist(points[i], points[(i + 1) % len(points)]) for i in range(len(points))]
    total = sum(lengths)
    count = max(8, math.ceil(total / spacing))
    edge = 0
    start = 0
    output = []
    for i in range(count):
        s = i * total / count
        while edge < len(lengths) - 1 and start + lengths[edge] < s:
            start += lengths[edge]
            edge += 1
        t = (s - start) / max(lengths[edge], 1e-10)
        a, b = points[edge], points[(edge + 1) % len(points)]
        output.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return output


def inside(p, polygon):
    result = False
    previous = polygon[-1]
    for current in polygon:
        if (current[1] > p[1]) != (previous[1] > p[1]) and p[0] < (previous[0] - current[0]) * (p[1] - current[1]) / (previous[1] - current[1]) + current[0]:
            result = not result
        previous = current
    return result


def segments(points):
    output = []
    for a, b in zip(points, points[1:] + points[:1]):
        dx, dy = b[0] - a[0], b[1] - a[1]
        output.append((a[0], a[1], dx, dy, dx * dx + dy * dy))
    return output


def nearest(p, edges):
    best = float("inf")
    best_point = None
    for ax, ay, dx, dy, den in edges:
        t = max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - ay) * dy) / den)) if den else 0
        q = (ax + t * dx, ay + t * dy)
        d2 = (p[0] - q[0]) ** 2 + (p[1] - q[1]) ** 2
        if d2 < best:
            best, best_point = d2, q
    return math.sqrt(best), best_point


def candidate_grid(outline, road_edges, shore_edges, step):
    minx, maxx = min(p[0] for p in outline), max(p[0] for p in outline)
    miny, maxy = min(p[1] for p in outline), max(p[1] for p in outline)
    normals, compact = [], []
    # Match root's phase exactly: ceil(bound), then a 4/2/1 metre increment.
    for x in range(math.ceil(minx), math.floor(maxx), step):
        for y in range(math.ceil(miny), math.floor(maxy), step):
            p = (x, y)
            if not inside(p, outline):
                continue
            shore, _ = nearest(p, shore_edges)
            if shore <= 8:
                continue
            road, near = nearest(p, road_edges)
            item = {"center": p, "road_distance": road, "shore_distance": shore, "nearest_route": near}
            if road > 14 and shore > 8:
                compact.append(item)
            if road > 20 and shore > 13:
                normals.append(item)
    return normals, compact


def solve(normals, compact, normal_count, compact_count, desired, budget):
    if compact_count > 1:
        raise ValueError("Current worlds support one compact relay per layout")
    count = len(normals)
    all_compact = (1 << len(compact)) - 1
    adjacency = [0] * count
    compatible_compact = []
    for i, a in enumerate(normals):
        for j in range(i + 1, count):
            if math.dist(a["center"], normals[j]["center"]) > 24:
                adjacency[i] |= 1 << j
                adjacency[j] |= 1 << i
        mask = 0
        for j, b in enumerate(compact):
            if math.dist(a["center"], b["center"]) > 23:
                mask |= 1 << j
        compatible_compact.append(mask)
    start = time.monotonic()
    best, best_score = None, float("inf")
    leaves = nodes = 0
    timed_out = False

    def score(item, target):
        return math.dist(item["center"], target) - .1 * item["road_distance"]

    def visit(selected, candidates, compact_mask):
        nonlocal best, best_score, leaves, nodes, timed_out
        nodes += 1
        if nodes % 1024 == 0 and time.monotonic() - start > budget:
            timed_out = True
            return
        remaining = normal_count - len(selected)
        if candidates.bit_count() < remaining or (compact_count and not compact_mask):
            return
        if not remaining:
            leaves += 1
            compact_item = None
            compact_score = 0
            if compact_count:
                choices = [j for j in range(len(compact)) if compact_mask & (1 << j)]
                choice = min(choices, key=lambda j: score(compact[j], desired[normal_count]))
                compact_item = compact[choice]
                compact_score = score(compact_item, desired[normal_count])
            for ordered in itertools.permutations(selected):
                total = compact_score + sum(score(normals[j], desired[i]) for i, j in enumerate(ordered))
                if total < best_score:
                    best_score = total
                    best = [normals[j] for j in ordered] + ([compact_item] if compact_item else [])
            return
        while candidates:
            bit = candidates & -candidates
            candidates ^= bit
            i = bit.bit_length() - 1
            visit(selected + [i], candidates & adjacency[i], compact_mask & compatible_compact[i])
            if timed_out:
                return
    visit([], (1 << count) - 1, all_compact)
    return {"layout": best, "score": best_score if best else None, "search_exhausted": not timed_out,
            "visited_nodes": nodes, "feasible_unlabelled_layouts": leaves,
            "search_seconds": round(time.monotonic() - start, 3)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--seconds-per-grid", type=float, default=6)
    parser.add_argument("--world", help="Optional one world ID")
    args = parser.parse_args()
    base = args.base.resolve()
    module = ast.parse((base / "build_worlds.py").read_text(encoding="utf-8"))
    kinds = next(ast.literal_eval(node.value) for node in module.body if isinstance(node, ast.Assign) and any(isinstance(target, ast.Name) and target.id == "KINDS" for target in node.targets))
    worlds = json.loads((base.parent / "V2" / "worlds-final.json").read_text(encoding="utf-8"))
    xy = lambda p: ((float(p[0]) - 500) * .18, (500 - float(p[1])) * .18)
    reports = []
    for index, world in enumerate(worlds):
        if args.world and args.world != world["id"]:
            continue
        path = base.parent / "V2" / "RouteGuidesClosed" / world["id"] / "route.json"
        guide = json.loads(path.read_text(encoding="utf-8"))
        source = guide["route"].get("samples") or guide["route"]["controlPoints"]
        road = resample([xy((p["x"], p["y"])) if isinstance(p, dict) else xy(p) for p in source], .8)
        outline = resample([xy(p) for p in guide["island"]["outlineSamples"]], 2.2)
        road_edges, shore_edges = segments(road), segments(outline)
        desired = [xy(p["center"]) for p in guide["technologyBuildingPads"]]
        desired.append((max(p[0] for p in outline) * .6, min(p[1] for p in outline) * .6))
        requested = kinds[index]
        normal_count = sum("relay" not in kind for kind in requested)
        compact_count = len(requested) - normal_count
        attempts = []
        solution = None
        for step in (4, 2, 1):
            normals, compact = candidate_grid(outline, road_edges, shore_edges, step)
            outcome = solve(normals, compact, normal_count, compact_count, desired, args.seconds_per_grid)
            layout = outcome.pop("layout")
            attempts.append({"grid_step": step, "normal_candidates": len(normals), "compact_candidates": len(compact), **outcome})
            if layout:
                solution = []
                for j, item in enumerate(layout):
                    center = item["center"]
                    delta = (item["nearest_route"][0] - center[0], item["nearest_route"][1] - center[1])
                    solution.append({"kind": requested[j], "center_xy": list(center),
                        "road_centerline_distance": round(item["road_distance"], 6),
                        "shore_distance": round(item["shore_distance"], 6),
                        "protected_corridor_center_margin": round(item["road_distance"] - 6, 6),
                        "nearest_route_xy": list(item["nearest_route"]),
                        "yaw_toward_actual_route": math.atan2(delta[0], -delta[1]),
                        "nearest_other_building_center": round(min((math.dist(center, other["center"]) for k, other in enumerate(layout) if k != j), default=0), 6)})
                break
            if not outcome["search_exhausted"]:
                break
        report = {"id": world["id"], "title": world["title"], "feasible": bool(solution),
                  "requested_kinds": requested, "attempts": attempts, "buildings": solution or [],
                  "normal_constraints": {"road_distance_gt": 20, "shore_distance_gt": 13, "normal_center_distance_gt": 24},
                  "compact_constraints": {"road_distance_gt": 14, "shore_distance_gt": 8, "normal_center_distance_gt": 23}}
        reports.append(report)
        print(json.dumps({"id": world["id"], "feasible": report["feasible"], "centers": [item["center_xy"] for item in report["buildings"]], "attempts": attempts}), flush=True)
    output = {"schema": "radio_archipelago_v3_building_center_plan/1", "pass": all(r["feasible"] for r in reports),
              "road_width": 8, "shoulder_each_side": 2, "protected_corridor_width": 12,
              "distance_method": "exact nearest point on each segment of actual resampled road and shoreline",
              "unity_tested": False, "mesh_extent_validation_performed": False,
              "limits": "Centers preserve specified constraints. Final transformed building/terrace meshes must still pass swept-corridor validation. Z/foundation height must be derived from actual terrain height_at.",
              "worlds": reports}
    filename = "building-layouts.json" if not args.world else "building-layout-" + args.world + ".json"
    (base / filename).write_text(json.dumps(output, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


if __name__ == "__main__":
    main()
