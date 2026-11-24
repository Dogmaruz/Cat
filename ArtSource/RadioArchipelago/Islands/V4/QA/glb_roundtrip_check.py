"""Read-only V4 GLB round-trip QA in an isolated background Blender process.

Usage: blender -b --factory-startup --threads 2 --python <this file>
Only QA/glb-roundtrip.json is written. Production assets are only read.
"""
import argparse
import collections
import contextlib
import datetime
import hashlib
import io
import json
import math
import pathlib
import struct
import sys
import traceback

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree

ROOT=pathlib.Path(__file__).resolve().parent.parent
PLANE_TOLERANCE=.0001
WELD_DECIMALS=5


def _components(graph):
    unseen=set(graph)
    count=0
    while unseen:
        count+=1
        stack=[unseen.pop()]
        while stack:
            for other in graph[stack.pop()]:
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
    return count


def _clear_disposable_scene():
    for obj in list(bpy.data.objects):bpy.data.objects.remove(obj,do_unlink=True)
    for store in(bpy.data.meshes,bpy.data.materials,bpy.data.images):
        for data in list(store):store.remove(data)


def _world(ident,bounds_tolerance):
    path=ROOT/'Output'/ident/'island.glb'
    manifest_path=path.parent/'manifest.json'
    if not path.is_file()or not manifest_path.is_file():
        raise FileNotFoundError('Completed GLB and manifest required: '+ident)
    manifest=json.loads(manifest_path.read_text(encoding='utf8'))
    blob=path.read_bytes()
    digest=hashlib.sha256(blob).hexdigest()
    magic,version,declared_size=struct.unpack_from('<4sII',blob,0)
    if magic!=b'glTF'or version!=2 or declared_size!=len(blob):
        raise ValueError('Invalid GLB header: '+ident)
    json_length,json_type=struct.unpack_from('<II',blob,12)
    if json_type!=0x4E4F534A:raise ValueError('Missing GLB JSON chunk')
    source=json.loads(blob[20:20+json_length].decode('utf8'))
    _clear_disposable_scene()
    with contextlib.redirect_stdout(io.StringIO()):
        result=bpy.ops.import_scene.gltf(filepath=str(path),merge_vertices=True,import_select_created_objects=False)
    bpy.context.view_layer.update()
    meshes=[obj for obj in bpy.context.scene.objects if obj.type=='MESH']
    low=[math.inf]*3
    high=[-math.inf]*3
    nonfinite=0
    vertex_count=0
    triangle_count=0
    for obj in meshes:
        obj.data.calc_loop_triangles()
        triangle_count+=len(obj.data.loop_triangles)
        for vertex in obj.data.vertices:
            p=obj.matrix_world@vertex.co
            vertex_count+=1
            if not all(math.isfinite(c)for c in p):
                nonfinite+=1
                continue
            for axis in range(3):
                low[axis]=min(low[axis],p[axis])
                high[axis]=max(high[axis],p[axis])
    delta=[abs(low[a]-manifest['bounds']['min'][a])for a in range(3)]+[abs(high[a]-manifest['bounds']['max'][a])for a in range(3)]
    road_objects=[obj for obj in meshes if 'SINGLE_CLOSED_FLUSH_ROAD'in obj.name]
    topverts=[]
    topfaces=[]
    positions={}
    original_vertices=[]
    original_topfaces=[]
    plane_error=0.
    top_area=0.
    for obj in road_objects:
        objverts=[obj.matrix_world@vertex.co for vertex in obj.data.vertices]
        base=len(original_vertices)
        original_vertices.extend(objverts)
        for poly in obj.data.polygons:
            pv=[objverts[i]for i in poly.vertices]
            if not all(abs(p.z)<=PLANE_TOLERANCE for p in pv):continue
            original_topfaces.append(tuple(base+i for i in poly.vertices))
            plane_error=max(plane_error,max(abs(p.z)for p in pv))
            top_area+=abs(sum(pv[i].x*pv[(i+1)%len(pv)].y-pv[(i+1)%len(pv)].x*pv[i].y for i in range(len(pv))))/2
            welded=[]
            for p in pv:
                key=tuple(round(c,WELD_DECIMALS)for c in p)
                if key not in positions:
                    positions[key]=len(topverts)
                    topverts.append(tuple(p))
                welded.append(positions[key])
            topfaces.append(tuple(welded))
    edge_counts=collections.Counter(tuple(sorted((f[i],f[(i+1)%len(f)])))for f in topfaces for i in range(len(f)))
    adjacency=collections.defaultdict(set)
    for a,b in edge_counts:
        adjacency[a].add(b)
        adjacency[b].add(a)
    boundary_graph=collections.defaultdict(set)
    for(a,b),count in edge_counts.items():
        if count==1:
            boundary_graph[a].add(b)
            boundary_graph[b].add(a)
    deck_components=_components(adjacency)
    boundary_components=_components(boundary_graph)
    boundary_degree_two=all(len(v)==2 for v in boundary_graph.values())
    euler=len(topverts)-len(edge_counts)+len(topfaces)
    nonmanifold=sum(count>2 for count in edge_counts.values())
    route=manifest['road']['points']
    lap=sum(math.dist(route[i][:2],route[(i+1)%len(route)][:2])for i in range(len(route)))
    area_width=top_area/lap if lap else 0
    coverage=0
    coverage_total=0
    hit_errors=[]
    if original_topfaces:
        # Zero BVH inflation is intentional: epsilon changes returned hit Z.
        bvh=BVHTree.FromPolygons(original_vertices,original_topfaces,all_triangles=False)
        for i,p in enumerate(route):
            q=route[(i+1)%len(route)]
            for t in(0.,.5):
                x=p[0]*(1-t)+q[0]*t
                y=p[1]*(1-t)+q[1]*t
                hit,normal,index,distance=bvh.ray_cast(Vector((x,y,1)),Vector((0,0,-1)),2)
                coverage_total+=1
                if hit is not None and abs(hit.z)<=PLANE_TOLERANCE:
                    coverage+=1
                    hit_errors.append(abs(hit.z))
    imported_colors=[]
    for mat in bpy.data.materials:
        bs=next((n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED'),None)if mat.use_nodes else None
        color=list(bs.inputs['Base Color'].default_value)if bs else list(mat.diffuse_color)
        imported_colors.append({'name':mat.name,'rgba_linear':color})
    finite_colors=all(all(math.isfinite(c)and 0<=c<=1 for c in m['rgba_linear'])for m in imported_colors)
    unique_colors={tuple(round(c,5)for c in m['rgba_linear'])for m in imported_colors}
    colored_count=sum(any(abs(c-1)>.001 for c in m['rgba_linear'][:3])for m in imported_colors)
    factors=[m.get('pbrMetallicRoughness',{}).get('baseColorFactor',[1,1,1,1])for m in source.get('materials',[])]
    width=float(manifest['road']['width'])
    shoulder=float(manifest['road'].get('shoulder',1))
    checks={
        'import_finished':'FINISHED'in result,
        'all_mesh_coordinates_finite':nonfinite==0,
        'bounds_agree_within_tolerance':bool(meshes)and max(delta)<=bounds_tolerance,
        'primary_road_found':bool(road_objects),
        'deck_flat_z0':bool(topfaces)and plane_error<=PLANE_TOLERANCE,
        'deck_single_connected_component':deck_components==1,
        'deck_closed_annulus':boundary_components==2 and boundary_degree_two and euler==0 and nonmanifold==0,
        'closed_route_coverage':coverage==coverage_total and coverage_total>0,
        'v4_road_width_6m_and_shoulder_1m':abs(width-6)<1e-6 and abs(shoulder-1)<1e-6,
        'actual_deck_area_width_matches_6m':abs(area_width-width)<=.03,
        'base_material_colors_finite':finite_colors,
        'base_material_colors_present':len(unique_colors)>=3 and colored_count>=3,
        'production_glb_unchanged':hashlib.sha256(path.read_bytes()).hexdigest()==digest,
    }
    return {
        'id':ident,'input_glb':str(path),'input_sha256':digest,'byte_size':len(blob),
        'manifest_sha256':hashlib.sha256(manifest_path.read_bytes()).hexdigest(),
        'import_result':sorted(result),'mesh_count':len(meshes),'vertices_after_import_merge':vertex_count,
        'triangles':triangle_count,'nonfinite_vertex_count':nonfinite,
        'bounds':{'imported_min':low,'imported_max':high,'manifest_min':manifest['bounds']['min'],'manifest_max':manifest['bounds']['max'],'axis_absolute_differences':delta,'maximum_difference_meters':max(delta)},
        'road':{'object_names':[o.name for o in road_objects],'deck_vertices_after_position_weld':len(topverts),'deck_faces':len(topfaces),'maximum_plane_error_meters':plane_error,'connected_components':deck_components,'boundary_loops':boundary_components,'all_boundary_vertex_degrees_two':boundary_degree_two,'nonmanifold_deck_edges':nonmanifold,'euler_characteristic':euler,'route_coverage_hits':coverage,'route_coverage_tests':coverage_total,'route_coverage_max_plane_error_meters':max(hit_errors)if hit_errors else None,'nominal_width_meters':width,'nominal_shoulder_meters':shoulder,'nominal_road_z':manifest['road']['z'],'centerline_lap_length_meters':lap,'actual_planar_deck_area_square_meters':top_area,'actual_area_divided_by_lap_width_meters':area_width},
        'materials':{'imported_material_count':len(imported_colors),'distinct_imported_base_colors':len(unique_colors),'nonwhite_imported_material_count':colored_count,'all_imported_base_colors_finite':finite_colors,'glb_material_count':len(source.get('materials',[])),'glb_distinct_base_color_factors':len({tuple(c)for c in factors}),'embedded_image_count':len(source.get('images',[])),'imported_base_colors':imported_colors},
        'checks':checks,'pass':all(checks.values()),
    }


def main():
    if not bpy.app.background:raise RuntimeError('Run only in isolated background Blender')
    parser=argparse.ArgumentParser()
    parser.add_argument('--worlds',default='01_emerald_ravine,02_coral_oasis')
    parser.add_argument('--bounds-tolerance',type=float,default=.03)
    args=parser.parse_args(sys.argv[sys.argv.index('--')+1:]if'--'in sys.argv else[])
    scope=args.worlds.split(',')
    # No polling and no half-complete report while production is exporting.
    for ident in scope:
        for filename in('island.glb','manifest.json'):
            if not(ROOT/'Output'/ident/filename).is_file():
                raise FileNotFoundError('Waiting for completed production asset: '+ident+'/'+filename)
    report={
        'schema':'radio-archipelago-v4-glb-roundtrip-1',
        'created_utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
        'blender_version':bpy.app.version_string,'isolated_factory_startup':True,
        'threads':2,'renders_performed':False,
        'axis_conversion':'Official glTF importer converts glTF Y-up to Blender Z-up; bounds compare manifest Blender world coordinates.',
        'tolerances':{'bounds_meters':args.bounds_tolerance,'deck_plane_meters':PLANE_TOLERANCE,'weld_meters':10**-WELD_DECIMALS},
        'scope':scope,
        'limitations':['GLB base-color availability is checked; complex procedural shaders and AI rock texture are intentionally not promised as baked GLB appearance.','Unity runtime behavior, collision behavior and runtime optimization are not tested.','Actual import proof covers only the listed completed files and exact recorded SHA-256 inputs.'],
        'method_note':'Original imported triangles with zero BVH inflation test coverage; positional welding is used only for annulus topology.',
        'worlds':[],
    }
    for ident in scope:
        try:item=_world(ident,args.bounds_tolerance)
        except Exception:
            item={'id':ident,'pass':False,'error':traceback.format_exc()}
        report['worlds'].append(item)
        print('V4_ROUNDTRIP',ident,'PASS',item['pass'],'failed_checks',[k for k,v in item.get('checks',{}).items()if not v],flush=True)
        if 'bounds'in item:print('V4_EVIDENCE',ident,'bounds_error',item['bounds']['maximum_difference_meters'],'actual_width',item['road']['actual_area_divided_by_lap_width_meters'],'route_hits',item['road']['route_coverage_hits'],'route_tests',item['road']['route_coverage_tests'],'distinct_colors',item['materials']['distinct_imported_base_colors'],flush=True)
    report['pass']=all(item['pass']for item in report['worlds'])
    path=ROOT/'QA'/'glb-roundtrip.json'
    path.write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print('V4_ROUNDTRIP_REPORT',str(path),'PASS',report['pass'],flush=True)
    if not report['pass']:raise SystemExit(2)


if __name__=='__main__':main()

