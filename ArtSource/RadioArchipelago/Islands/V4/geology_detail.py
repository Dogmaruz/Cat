"""Closed, deterministic fractured cliff meshes for the canonical V4 terrain.

No operators or scene resets. Rim outcrops overlap the main terrain volume;
they are actual meshes, not planes, texture displacement or camera-dependent
silhouettes. All positive-height forms conservatively clear the full 12 m
play corridor. The returned anchors expose actual positions for cliff nature.
"""
import math
import random
import bpy
import bmesh
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TAU = math.tau


def _hex(value):
    value = value.lstrip('#')
    return tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4))


def _tone(color, gain=1, mix=0):
    return tuple(min(1, max(0, c * gain * (1 - mix) + mix)) for c in _hex(color))


def _mesh(name, verts, faces, materials, material_ids, collection):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(verts, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    for material in materials:
        mesh.materials.append(material)
    for poly, index in zip(mesh.polygons, material_ids):
        poly.material_index = index
    obj['v3_role'] = 'terrain'
    obj['radio_archipelago_role'] = 'fractured_geology'
    return obj


def _fractured_column(name, center, tangent, outward, widths, bottom, top,
                      rng, materials, collection, cap_material=0, root=False):
    """An irregular eight-sided rock column, sealed on both ends.

    Independent shear and ledges per stratum make broad fracture planes.
    End rings retain area: no long tetrahedral pyramid or single apex.
    """
    sides = 8 if not root else 7
    phase = rng.uniform(-.13, .13)
    radii = [rng.uniform(.82, 1.14) for _ in range(sides)]
    if root:
        levels = [(0, .23), (.12, .43), (.28, .57), (.44, .78),
                  (.57, .69), (.73, .96), (.85, .87), (1, .72)]
    else:
        levels = [(0, .38), (.11, .55), (.27, .81), (.43, .77),
                  (.58, 1.03), (.73, .96), (.87, .91), (1, .72)]
    levels=[(fraction if i in(0,len(levels)-1)else fraction+rng.uniform(-.035,.035),
             radius*rng.uniform(.88,1.12))for i,(fraction,radius)in enumerate(levels)]
    verts = []
    for layer, (fraction, radius) in enumerate(levels):
        # Fracture offsets persist vertically and break into real narrow ledges.
        shear_t = rng.uniform(-.12, .12) * widths[0]
        shear_n = (fraction - .5) * widths[1] * .22 + rng.uniform(-.1, .1) * widths[1]
        for i in range(sides):
            angle = TAU * i / sides + phase
            a = math.cos(angle) * radii[i] * widths[0] * radius + shear_t
            b = math.sin(angle) * radii[i] * widths[1] * radius + shear_n
            z = bottom + (top - bottom) * fraction
            if layer not in (0, len(levels) - 1):
                z += math.sin(i * 1.73 + layer * .47 + phase) * .35
            if layer == len(levels) - 1:
                z += rng.uniform(-.48, .12) if top > 0 else -rng.uniform(0, .25)
            verts.append((center[0] + tangent[0] * a + outward[0] * b,
                          center[1] + tangent[1] * a + outward[1] * b, z))
    faces, ids = [], []
    for layer in range(len(levels) - 1):
        for i in range(sides):
            j = (i + 1) % sides
            a, b, c, d = layer * sides + i, layer * sides + j, (layer + 1) * sides + j, (layer + 1) * sides + i
            # Rock planes run vertically through several strata. Avoid a
            # regular checkerboard of alternating horizontal coloured blocks.
            material = 1 if i%5==0 else (2 if (i*3+int(phase*100))%7==0 else 0)
            if (i + layer) % 3 == 0:
                faces.extend(((a, b, c), (a, c, d)))
                ids.extend((material, material if i % 2 else 2))
            else:
                faces.append((a, b, c, d))
                ids.append(material)
    bottom_center = len(verts)
    verts.append(tuple(sum(v[k] for v in verts[:sides]) / sides for k in range(3)))
    top_center = len(verts)
    verts.append(tuple(sum(v[k] for v in verts[-sides - 1:-1]) / sides for k in range(3)))
    last = (len(levels) - 1) * sides
    for i in range(sides):
        j = (i + 1) % sides
        faces.append((bottom_center, j, i)); ids.append(1)
        faces.append((top_center, last + i, last + j)); ids.append(cap_material)
    if tangent[0]*outward[1]-tangent[1]*outward[0]<0:
        faces=[tuple(reversed(face))for face in faces]
    obj = _mesh(name, verts, faces, materials, ids, collection)
    obj['geology_form'] = 'hanging_fractured_root' if root else 'embedded_rim_buttress'
    return obj


def _moss_cap(name, center, radii, rng, material, collection):
    sides = 9
    angles = [TAU * i / sides + rng.uniform(-.08, .08) for i in range(sides)]
    ring = [(center[0] + math.cos(a) * radii[0] * rng.uniform(.78, 1.12),
             center[1] + math.sin(a) * radii[1] * rng.uniform(.78, 1.12)) for a in angles]
    verts = [(x, y, center[2] - .26) for x, y in ring]
    verts += [(x, y, center[2] + rng.uniform(-.08, .12)) for x, y in ring]
    verts += [(center[0], center[1], center[2] + .24)]
    faces = [tuple(reversed(range(sides)))]
    for i in range(sides):
        j = (i + 1) % sides
        faces.extend(((i, j, j + sides, i + sides), (sides * 2, i + sides, j + sides)))
    obj = _mesh(name, verts, faces, [material], [0] * len(faces), collection)
    obj['geology_form'] = 'irregular_surface_moss_or_weathered_scree'
    return obj


def _hull_xy(points):
    points=sorted(set((round(p[0],6),round(p[1],6))for p in points))
    def cross(o,a,b):return(a[0]-o[0])*(b[1]-o[1])-(a[1]-o[1])*(b[0]-o[0])
    lower=[]
    for point in points:
        while len(lower)>=2 and cross(lower[-2],lower[-1],point)<=0:lower.pop()
        lower.append(point)
    upper=[]
    for point in reversed(points):
        while len(upper)>=2 and cross(upper[-2],upper[-1],point)<=0:upper.pop()
        upper.append(point)
    return lower[:-1]+upper[:-1]


def _inside_poly(p,polygon):
    inside=False;previous=polygon[-1]
    for current in polygon:
        if(current[1]>p[1])!=(previous[1]>p[1])and p[0]<(previous[0]-current[0])*(p[1]-current[1])/(previous[1]-current[1])+current[0]:inside=not inside
        previous=current
    return inside


def _projection_safe(vertices,road_distance,clear):
    """Dense conservative footprint sampling with a .75m Lipschitz margin."""
    polygon=_hull_xy(vertices)
    for a,b in zip(polygon,polygon[1:]+polygon[:1]):
        count=max(1,math.ceil(math.dist(a,b)/.5))
        for j in range(count+1):
            t=j/count
            if road_distance(a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t)<clear+.75:return False
    x=min(p[0]for p in polygon)
    while x<=max(p[0]for p in polygon):
        y=min(p[1]for p in polygon)
        while y<=max(p[1]for p in polygon):
            if _inside_poly((x,y),polygon)and road_distance(x,y)<clear+.75:return False
            y+=1
        x+=1
    return True


def _broken_mass(name,center,width,depth,height,angle,rng,materials,collection,
                 road_distance=None,clear=6,cap_material=0):
    """Chisel-cut, obliquely sheared convex rock with broad irregular ends.

    Aspect ratios and tilt vary per mass, rather than repeating vertical logs.
    Actual hull facets create planes, with a thin bevel on sharp mineral edges.
    """
    vertices=[];leanx=rng.uniform(-.34,.34);leany=rng.uniform(-.27,.27)
    rings=[(-1,rng.uniform(.60,.90)),(-.33,1),(.38,rng.uniform(.82,1.07)),(1,rng.uniform(.48,.84))]
    sides=rng.choice((6,7,8));phase=rng.uniform(0,TAU)
    for z,radius in rings:
        for j in range(sides):
            a=j*TAU/sides+phase+rng.uniform(-.09,.09)
            x=math.cos(a)*width*radius*rng.uniform(.87,1.12)+z*height*leanx
            y=math.sin(a)*depth*radius*rng.uniform(.87,1.12)+z*height*leany
            zz=z*height+math.sin(a*2+phase)*height*.09+rng.uniform(-.08,.08)*height
            vertices.append((center[0]+x*math.cos(angle)-y*math.sin(angle),
                             center[1]+x*math.sin(angle)+y*math.cos(angle),center[2]+zz))
    safe=True if road_distance is None else _projection_safe(vertices,road_distance,clear)
    if not safe:vertices=[(x,y,min(z,-.18))for x,y,z in vertices]
    bm=bmesh.new()
    for vertex in vertices:bm.verts.new(vertex)
    bm.verts.ensure_lookup_table()
    hull=bmesh.ops.convex_hull(bm,input=list(bm.verts),use_existing_faces=False)
    loose=[v for v in bm.verts if not v.link_faces]
    if loose:bmesh.ops.delete(bm,geom=loose,context='VERTS')
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    # Broad fracture planes share a weathering tone; subdivided microrelief
    # inherits it instead of becoming a random mosaic of tiny coloured faces.
    for face in bm.faces:
        value=rng.random()
        face.material_index=cap_material if face.normal.z>.60 and rng.random()<.60 else(1 if value<.20 else 2 if value>.78 else 0)
    old_vertices=set(bm.verts)
    long_edges=[edge for edge in bm.edges if edge.calc_length()>2.25]
    if long_edges:
        bmesh.ops.subdivide_edges(bm,edges=long_edges,cuts=2,use_grid_fill=True)
        bm.normal_update()
        # Actual shallow fracture relief on broad planes: this breaks the
        # enormous flat convex-hull facets before restrained edge abrasion.
        # Mostly inward erosion keeps the conservative play-corridor guard.
        for vertex in bm.verts:
            if vertex not in old_vertices:
                vertex.co+=vertex.normal*rng.uniform(-.075,.025)
                if not safe:vertex.co.z=min(vertex.co.z,-.18)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    original=bm.copy()
    sharp=[edge for edge in bm.edges if edge.is_manifold and edge.calc_face_angle(0)>.32]
    bmesh.ops.bevel(bm,geom=sharp,offset=rng.uniform(.10,.22),segments=2,profile=.5,
        affect='EDGES',clamp_overlap=True,harden_normals=True,material=2)
    # Acute convex hull cuts can collapse the last bevel corner numerically.
    # Bake and weld those sub-millimetre remnants so exported geometry itself
    # stays closed and nondegenerate, rather than relying on a modifier stack.
    bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.0001)
    bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.0001)
    bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    boundary=[edge for edge in bm.edges if edge.is_boundary]
    if boundary:
        bmesh.ops.holes_fill(bm,edges=boundary,sides=0)
        bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces))
    bevel_applied=True
    if any(not edge.is_manifold for edge in bm.edges)or any(face.calc_area()<1e-9 for face in bm.faces):
        # Rare extremely acute hulls cannot carry a finite-width bevel without
        # a collapsed corner. Keep their original closed fracture cut instead.
        bm.free();bm=original;original=None;bevel_applied=False
    if original is not None:original.free()
    if bm.calc_volume(signed=True)<0:
        bmesh.ops.reverse_faces(bm,faces=list(bm.faces))
    bm.verts.index_update();bm.faces.index_update()
    verts=[tuple(v.co)for v in bm.verts];faces=[tuple(v.index for v in f.verts)for f in bm.faces]
    ids=[f.material_index for f in bm.faces]
    bm.free()
    obj=_mesh(name,verts,faces,materials,ids,collection)
    obj['geology_form']='oblique_fractured_rock_mass';obj['safe_above_road']=safe
    obj['edge_abrasion_baked']=bevel_applied
    return obj


def build_geology(outline, cap_outline, height_at, road_distance, palette,
                  material_factory, collection, seed=0, world_id='',
                  protected_half_width=6,reserved_centers=None,base_mesh=None):
    rng = random.Random(seed * 7919 + 601)
    rock = palette.get('rock', '#52616B')
    land = palette.get('land', '#587344')
    ice = 'frost' in world_id
    coral = any(s in world_id for s in ('coral', 'sunflower'))
    cloud = 'cloud' in world_id
    volcanic = 'volcanic' in world_id
    materials = [material_factory('V4_fractured_cliff', _tone(rock, 1.03), bump=.16, noise_scale=.24),
                 material_factory('V4_deep_fracture', _tone(rock, .79), bump=.1, noise_scale=2.6),
                 material_factory('V4_shear_plane', _tone(rock, 1.02, .065), bump=.11, noise_scale=1.3),
                 material_factory('V4_rim_cover', _tone(land, .88), bump=.1, noise_scale=2.1)]
    objects, anchors = [], []
    base_bvh=None
    if base_mesh is not None:
        base_mesh.data.calc_loop_triangles()
        base_bvh=BVHTree.FromPolygons([base_mesh.matrix_world@v.co for v in base_mesh.data.vertices],
            [tuple(t.vertices)for t in base_mesh.data.loop_triangles],all_triangles=True)
    def cliff_anchor(point,outward,z,protrude):
        if base_bvh is None:return(point[0]+outward[0]*protrude,point[1]+outward[1]*protrude,z)
        origin=Vector((point[0]+outward[0]*40,point[1]+outward[1]*40,z))
        hit,normal,index,d=base_bvh.ray_cast(origin,Vector((-outward[0],-outward[1],0)),160)
        if hit is None:return(point[0]-outward[0]*abs(z)*.35,point[1]-outward[1]*abs(z)*.35,z)
        return(hit.x+outward[0]*protrude,hit.y+outward[1]*protrude,z)
    for material in materials[:3]:
        nodes=material.node_tree.nodes;links=material.node_tree.links
        coord=next(n for n in nodes if n.type=='TEX_COORD');bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
        fine=nodes.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=4.8;fine.inputs['Detail'].default_value=3.5
        links.new(coord.outputs['Object'],fine.inputs['Vector'])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.32;bump.inputs['Distance'].default_value=.12
        links.new(fine.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
        rough=nodes.new('ShaderNodeMapRange');rough.inputs['From Min'].default_value=0;rough.inputs['From Max'].default_value=1;rough.inputs['To Min'].default_value=.62;rough.inputs['To Max'].default_value=.96
        links.new(fine.outputs['Fac'],rough.inputs['Value']);links.new(rough.outputs['Result'],bs.inputs['Roughness'])
    # Broad, staggered geological masses overlap at different depths. A few
    # slender fractures remain as accents; there is no radial column palisade.
    i=0
    while i<len(outline):
        point=outline[i];previous=outline[i-1];following=outline[(i+1)%len(outline)]
        dx,dy=following[0]-previous[0],following[1]-previous[1];length=max(math.hypot(dx,dy),1e-6)
        tangent=(dx/length,dy/length);outward=(tangent[1],-tangent[0]);angle=math.atan2(tangent[1],tangent[0])+rng.uniform(-.45,.45)
        for layer in range(rng.choice((2,3,4))):
            along=rng.uniform(-5,5)
            width=rng.uniform(4.6,8.4)*.75;depth=rng.uniform(3.6,6.3)*.75;height=rng.uniform(5.5,10)*.75
            anchor_point=(point[0]+tangent[0]*along,point[1]+tangent[1]*along)
            center=cliff_anchor(anchor_point,outward,-rng.uniform(5,11)-layer*rng.uniform(5,9),depth*.16)
            obj=_broken_mass('V4_'+world_id+f'_ERODED_CLIFF_{i:03d}_{layer}',center,width,depth,height,
                angle+rng.uniform(-.28,.28),rng,materials,collection,road_distance,protected_half_width,3 if layer==0 else 0)
            objects.append(obj)
            if layer==0:
                top=max(v.co.z for v in obj.data.vertices);bottom=min(v.co.z for v in obj.data.vertices)
                anchors.append({'xy':list(center[:2]),'top_z':top,'bottom_z':bottom,'outward':list(outward),'safe_above_road':obj['safe_above_road']})
        # Short transverse blades imply geological shear between the large
        # masses; their axes lean and their vertical positions never align.
        if i%3==0:
            center=cliff_anchor(point,outward,-rng.uniform(2,7),.3)
            objects.append(_broken_mass('V4_'+world_id+f'_SLATE_SHEAR_{i:03d}',center,rng.uniform(3,5.3),rng.uniform(1.2,2.2),rng.uniform(3.8,6),angle+.4,rng,materials,collection,road_distance,protected_half_width,2))
        # Small overlapping ledges and broken slabs sit on actual raycast
        # cliff surfaces, rather than floating beyond a tapered base. Their
        # dimensions and spacing avoid a decorative necklace of equal beads.
        for plate in range(rng.randint(3,6)):
            along=rng.uniform(-7,7);z=-rng.uniform(1.2,31)
            point_on_edge=(point[0]+tangent[0]*along,point[1]+tangent[1]*along)
            center=cliff_anchor(point_on_edge,outward,z,.35)
            width=rng.uniform(1.7,3.8);depth=rng.uniform(.65,1.45);height=rng.uniform(.85,2.4)
            obj=_broken_mass('V4_'+world_id+f'_FRACTURE_LEDGE_{i:03d}_{plate}',center,width,depth,height,
                angle+rng.uniform(-.38,.38),rng,materials,collection,road_distance,protected_half_width,3 if plate==0 else 0)
            objects.append(obj)
            if plate==0 and obj['safe_above_road'] and rng.random()<.6:
                obj.data.calc_loop_triangles();ledge_bvh=BVHTree.FromPolygons([v.co.copy()for v in obj.data.vertices],
                    [tuple(t.vertices)for t in obj.data.loop_triangles],all_triangles=True)
                top=max(v.co.z for v in obj.data.vertices)
                hit,normal,index,d=ledge_bvh.ray_cast(Vector((center[0],center[1],top+1)),Vector((0,0,-1)),20)
                if hit is not None:
                    cover=_moss_cap('V4_'+world_id+f'_MOSS_IN_CREVICE_{i:03d}',(hit.x,hit.y,hit.z-.12),
                        (width*.4,depth*.5),rng,materials[3],collection)
                    if _projection_safe([v.co for v in cover.data.vertices],road_distance,protected_half_width):objects.append(cover)
                    else:
                        mesh=cover.data;bpy.data.objects.remove(cover,do_unlink=True);bpy.data.meshes.remove(mesh)
        i+=rng.choice((4,5,6,7))
    # Interior rock ridges rise from safe off-road ground, with several
    # interlocked oblique blocks instead of an isolated conical boulder.
    reserved=list(reserved_centers or [])
    try:
        import json,pathlib
        plan=json.loads((pathlib.Path(__file__).parent/'building-layouts.json').read_text(encoding='utf8'))
        for world in plan['worlds']:
            if world['id']==world_id and not reserved:reserved=[b['center_xy']for b in world['buildings']]
    except(OSError,KeyError,ValueError):pass
    candidates=[]
    minx,maxx=min(p[0]for p in outline),max(p[0]for p in outline);miny,maxy=min(p[1]for p in outline),max(p[1]for p in outline)
    for x in range(math.ceil(minx)+5,math.floor(maxx)-5,5):
        for y in range(math.ceil(miny)+5,math.floor(maxy)-5,5):
            if _inside_poly((x,y),outline)and road_distance(x,y)>13.5 and height_at(x,y)>-.05 and all(math.dist((x,y),p)>17 for p in reserved):
                candidates.append((x,y))
    selected=[]
    for j in range(4):
        good=[p for p in candidates if all(math.dist(p,q)>19 for q in selected)]
        if not good:break
        center=max(good,key=lambda p:min([math.dist(p,q)for q in selected]or[18])+.42*road_distance(*p)+rng.random()*.3)
        selected.append(center);angle=rng.uniform(0,TAU);tangent=(math.cos(angle),math.sin(angle));base=height_at(*center)
        for k in range(4):
            along=(k-1.5)*rng.uniform(2.7,3.9)
            position=(center[0]+tangent[0]*along,center[1]+tangent[1]*along,base+rng.uniform(1.5,4))
            obj=_broken_mass('V4_'+world_id+f'_INLAND_ROCK_RIDGE_{j}_{k}',position,rng.uniform(3,5.1),rng.uniform(2.1,3.5),rng.uniform(4.1,7.0),angle+rng.uniform(-.3,.3),rng,materials,collection,road_distance,protected_half_width,3 if k%3==0 else 0)
            objects.append(obj)
    # Broad jagged undercroft masses replace the hanging thin spear forest.
    roots=26 if not cloud else 18
    cap_minx,cap_maxx=min(p[0]for p in cap_outline),max(p[0]for p in cap_outline)
    cap_miny,cap_maxy=min(p[1]for p in cap_outline),max(p[1]for p in cap_outline)
    for j in range(roots):
        for attempt in range(100):
            center=(rng.uniform(cap_minx+6,cap_maxx-6),rng.uniform(cap_miny+6,cap_maxy-6))
            if _inside_poly(center,cap_outline):break
        else:continue
        objects.append(_broken_mass('V4_'+world_id+f'_BROKEN_UNDERCROFT_{j:02d}',(center[0],center[1],-rng.uniform(32,39)),rng.uniform(6,11),rng.uniform(5,9),rng.uniform(9,16),rng.uniform(0,TAU),rng,materials,collection))
    bbox=[tuple(v.co)for obj in objects for v in obj.data.vertices]
    return{'objects':objects,'anchors':anchors,'interior_ridge_centers':[list(p)for p in selected],
           'bounds':{'min':[min(p[i]for p in bbox)for i in range(3)],'max':[max(p[i]for p in bbox)for i in range(3)]},
           'description':'Staggered oblique fracture masses, shear blades, inland rock ridges and broken broad undercroft'}
