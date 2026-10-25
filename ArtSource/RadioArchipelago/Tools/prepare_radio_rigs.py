"""Blender background authoring pipeline for the supplied Radioarchipelago models.

Original ZIPs are read-only. Extraction, inspections and review renders go to the
Unity project's ignored Library. Editable/processed assets go to ArtSource.
Run with Blender --background --factory-startup --python this_file -- --stage inspect
"""
from __future__ import annotations

import argparse
import json
import math
import pathlib
import sys
import zipfile
import csv
import shutil
import heapq

import bpy
from mathutils import Vector

sys.path.insert(0,str(pathlib.Path(__file__).resolve().parent))
from radio_root_caps import close_root_caps

ROOT = pathlib.Path(__file__).resolve().parents[3]
LIBRARY = ROOT / "DancingCatsClone/Library/RadioRigging"
REFERENCES = ROOT / "Docs/Design/RadioArchipelago/ModelReferences"
PROCESSED = ROOT / "ArtSource/RadioArchipelago/Processed"
CHARACTERS = ("Tiko", "Lumen", "Nori")

# Positions are in the normalized source inspection frame (before foot centering).
ANATOMY = {
    "Tiko": {
        "eyes": [(-.282,-.700,1.335),(.122,-.705,1.327)], "eye_inner":(.080,.145), "eye_outer":(.165,.255), "eye_front":-.55,
        "antenna": [(-.82,.48,1.52),(-.96,.56,1.90),(-.70,.38,2.30),(-.25,.06,2.52),(.12,-.25,2.45),(.217,-.30,2.314)],
        "orb": ((.217,-.30,2.314),.155),
        "crest": [(-.40,.10,1.82),(-.59,.19,1.78),(-.72,.28,1.91)],
        "scarves": [[(.29,.38,1.07),(.63,.55,1.01),(1.05,.76,1.02)], [(.28,-.45,.99),(.06,-.68,.81),(-.22,-.92,.64)]],
        "shoulders": [(-.59228885,-.06007691,.99),(.35771115,-.06007691,.98)], "hands":[(-.83,-.11,.56),(.65,-.11,.56)],
        "arm_start":.38, "arm_full":.65, "arm_y":.30,
        "arm_planes":(-.56,.63), # Final foot-centered X coordinates, unlike landmark points.
        "continuous_skin": True,
        "arm_capsule_radius": .19,
        "arm_back":.28,
        "head_bottom":1.04, "head_full":1.47,
    },
    "Lumen": {
        "eyes": [(-.018,-.429,1.220),(.192,-.433,1.240)], "eye_inner":(.052,.125), "eye_outer":(.112,.340), "eye_front":-.33,
        "antenna": [(-.20,.16,1.37),(-.615,.52,1.45),(-.49,.40,1.94),(-.20,.11,2.31),(.21,-.25,2.53)],
        "orb": ((.4454,-.4826,1.7560),.095),
        "crest": [], "scarves": [],
        "shoulders": [(-.24188314,-.03604754,.88),(.40811686,-.04604754,.87)], "hands":[(-.45988314,.03995246,.54),(.58011686,.03995246,.54)],
        "continuous_skin": True, "arm_refine_cuts":1,
        "arm_start":.27, "arm_full":.44, "arm_y":.30,
        "arm_planes":(-.39,.36),
        "arm_back":1.0,
        "head_bottom":1.10, "head_full":1.48,
    },
    "Nori": {
        "eyes": [(.030,-.898,1.196),(.435,-.899,1.164)], "eye_inner":(.080,.140), "eye_outer":(.170,.250), "eye_front":-.77,
        "antenna": [(-.62,.38,1.74),(-.65,.40,1.98),(-.39,.14,2.30),(.04,-.29,2.48),(.41,-.67,2.42),(.715,-.930,2.197)],
        "orb": ((.715,-.930,2.197),.202),
        "crest": [],
        "scarves": [[(-.60,.48,.89),(-.85,.75,.98),(-1.22,1.14,.94)], [(-.60,.48,.85),(-.85,.71,.72),(-1.10,.96,.56)]],
        "shoulders": [(-.45977635,-.05412963,.88),(.89022365,-.04412963,.89)], "hands":[(-.66977635,.01987037,.49),(1.04022365,.01987037,.49)],
        "continuous_skin": True, "arm_refine_cuts":1,
        "arm_start":.48, "arm_full":.73, "arm_y":.63,
        "arm_planes":(-.66,.72),
        "arm_back":.35,
        "head_bottom":.96, "head_full":1.35,
    },
}


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def extract_source(name):
    target = (LIBRARY / "Source" / name).resolve()
    target.mkdir(parents=True, exist_ok=True)
    archive_path = REFERENCES / name / (name + ".zip")
    with zipfile.ZipFile(archive_path) as archive:
        for member in archive.infolist():
            destination = (target / member.filename).resolve()
            if not destination.is_relative_to(target):
                raise RuntimeError(f"Unsafe ZIP member {member.filename}")
            if member.is_dir():
                destination.mkdir(parents=True, exist_ok=True)
            elif not destination.exists() or destination.stat().st_size != member.file_size:
                destination.parent.mkdir(parents=True, exist_ok=True)
                with archive.open(member) as source, destination.open("wb") as output:
                    output.write(source.read())
    return target


def source_material(name, directory):
    material = bpy.data.materials.new(name + "_SourceSurface")
    material.use_nodes = True
    nodes, links = material.node_tree.nodes, material.node_tree.links
    nodes.clear()
    shader = nodes.new("ShaderNodeBsdfPrincipled")
    output = nodes.new("ShaderNodeOutputMaterial")
    links.new(shader.outputs["BSDF"], output.inputs["Surface"])
    shader.inputs["Roughness"].default_value = .6
    for index, (suffix, socket) in enumerate((("basecolor", "Base Color"), ("metallic", "Metallic"), ("roughness", "Roughness"), ("normal", "Normal"))):
        image_path = next(directory.rglob(f"{name}_{suffix}.PNG"))
        image = bpy.data.images.load(str(image_path), check_existing=True)
        image.colorspace_settings.name = "sRGB" if suffix == "basecolor" else "Non-Color"
        texture = nodes.new("ShaderNodeTexImage")
        texture.image = image
        texture.label = suffix
        texture.location = (-600, index * -260)
        if suffix == "normal":
            normal = nodes.new("ShaderNodeNormalMap")
            normal.inputs["Strength"].default_value = .7
            links.new(texture.outputs["Color"], normal.inputs["Color"])
            links.new(normal.outputs["Normal"], shader.inputs["Normal"])
        else:
            links.new(texture.outputs["Color"], shader.inputs[socket])
    return material


def import_source(name):
    directory = extract_source(name)
    fbx = next(directory.glob("*.fbx"))
    bpy.ops.import_scene.fbx(filepath=str(fbx), use_image_search=False, use_anim=False)
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    if len(meshes) != 1:
        raise RuntimeError(f"Expected one original mesh for {name}, got {len(meshes)}")
    mesh = meshes[0]
    mesh.name = name + "_OriginalSurface"
    bpy.context.view_layer.objects.active = mesh
    mesh.select_set(True)
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bounds = [mesh.matrix_world @ Vector(c) for c in mesh.bound_box]
    minimum = Vector([min(p[i] for p in bounds) for i in range(3)])
    maximum = Vector([max(p[i] for p in bounds) for i in range(3)])
    scale = 2.6 / (maximum.z - minimum.z)
    center = Vector(((minimum.x + maximum.x) / 2, (minimum.y + maximum.y) / 2, minimum.z))
    for v in mesh.data.vertices:
        v.co = (v.co - center) * scale
    feet = [v.co for v in mesh.data.vertices if v.co.z < .16]
    offset = Vector(((min(v.x for v in feet)+max(v.x for v in feet))/2,
                     (min(v.y for v in feet)+max(v.y for v in feet))/2, 0))
    for v in mesh.data.vertices:
        v.co -= offset
    mesh["source_foot_offset"] = list(offset)
    mesh.location = (0, 0, 0)
    mesh.data.materials.clear()
    mesh.data.materials.append(source_material(name, directory))
    mesh.data.update()
    return mesh


def smoothstep(a,b,x):
    t = max(0., min(1., (x-a)/(b-a)))
    return t*t*(3-2*t)


def feature_kind(name, point, color=None):
    """Anatomical boundaries inspected in all four supplied-model views."""
    x,y,z = point
    r,g,b=color if color is not None else (0,0,0)
    coral=r>.32 and r>g*1.25 and r>b*1.4
    sky=g>.25 and b>g*1.08 and b>r*1.20 and r>.10
    if name == "Tiko":
        if z>2.025 or (x<-.72 and z>1.48 and y>.29): return "Antenna"
        if z>1.72 and x<-.45 and .04<y<.37: return "Crest"
        if .72<z<1.14 and x>.58 and coral: return "Scarf.L"
        if .55<z<.89 and y<-.65 and x<.20 and coral: return "Scarf.R"
    elif name == "Lumen":
        a,b=Vector((.21,-.23,1.95)),Vector((.445,-.483,1.756))
        delta=b-a;t=max(0,min(1,(Vector(point)-a).dot(delta)/delta.length_squared))
        light=(Vector(point)-Vector(ANATOMY[name]["orb"][0])).length<.115 or (Vector(point)-(a+delta*t)).length<.055
        a,b=Vector((-.615,.52,1.45)),Vector((.21,-.25,1.95))
        delta=b-a;t=max(0,min(1,(Vector(point)-a).dot(delta)/delta.length_squared))
        lower_rail=(Vector(point)-(a+delta*t)).length<.060
        r,g,blue=color if color is not None else (0,0,0)
        sail=(z>1.43 and x<.25 and blue>g*.72 and r>.28 and abs(y+x+.04)<.22)
        if z>1.82 or (z>1.34 and x<-.27 and y>.22) or light or lower_rail or sail: return "Antenna"
    else:
        navy=b>r*1.10 and b>g*1.02 and r<.4
        if z>1.83 or (x<-.55 and z>1.76 and y>.22 and navy): return "Antenna"
        if x<-.60 and .50<z<1.15 and y>.43 and y-x>1.05 and sky:
            return "Scarf.L" if z>.86 else "Scarf.R"
    return None


def refine_surface(name, obj):
    """Refine only eyes and spherical beacon. UVs and corner normals interpolate."""
    import bmesh
    offset = Vector(obj["source_foot_offset"])
    cfg = ANATOMY[name]
    old = obj.data
    old_normals = [n.vector.copy() for n in old.corner_normals]
    bm = bmesh.new()
    bm.from_mesh(old)
    bm.faces.ensure_lookup_table()
    normal_layer = bm.loops.layers.float_vector.new("SourceCornerNormal")
    for face, polygon in zip(bm.faces, old.polygons):
        for loop, index in zip(face.loops, polygon.loop_indices):
            loop[normal_layer] = old_normals[index]
    flecks=[face for face in bm.faces if len(face.verts)==3 and all(len(edge.link_faces)==1 for edge in face.edges)]
    if flecks:
        bmesh.ops.delete(bm,geom=flecks,context="FACES")
        obj["removed_source_flecks"]=len(flecks)
    planes=[]if cfg.get("continuous_skin")else[(Vector((plane,0,0)),Vector((1,0,0)))for plane in cfg["arm_planes"]]
    if cfg.get("arm_attachment")=="shoulder":
        planes=[(Vector(shoulder)-offset,(Vector(hand)-Vector(shoulder)).normalized())for shoulder,hand in zip(cfg["shoulders"],cfg["hands"])]
    for center,normal in planes:
        bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=1e-7,
                              plane_co=center,plane_no=normal,
                              clear_inner=False,clear_outer=False)
    selected = set()
    arm_edges=set()
    orb_center, orb_radius = cfg["orb"]
    orb_center = Vector(orb_center)
    for face in bm.faces:
        point = face.calc_center_median() + offset
        eye_area = point.y < cfg["eye_front"] and any(abs(point.x-e[0])<cfg["eye_outer"][0]*1.1 and abs(point.z-e[2])<cfg["eye_outer"][1]*1.1 for e in cfg["eyes"])
        orb_area = (point-orb_center).length < orb_radius*1.13
        if eye_area or (orb_area and not cfg.get("continuous_skin")):
            selected.update(face.edges)
        for shoulder,hand in zip(cfg["shoulders"],cfg["hands"]):
            a,b=Vector(shoulder),Vector(hand)
            direction=b-a
            t=max(0.,min(1.,(point-a).dot(direction)/direction.length_squared))
            if (point-(a+direction*t)).length<(.28 if cfg.get("continuous_skin")else .24) and .3<point.z<1.05:
                arm_edges.update(face.edges)
    if arm_edges:
        bmesh.ops.subdivide_edges(bm,edges=list(arm_edges),cuts=cfg.get("arm_refine_cuts",2)if cfg.get("continuous_skin")else 1,use_grid_fill=True)
    bmesh.ops.subdivide_edges(bm, edges=list(selected), cuts=2, use_grid_fill=True)
    # Round the already spherical source beacon, leaving its attachment intact.
    for vertex in bm.verts:
        point = vertex.co + offset
        delta = point-orb_center
        if not cfg.get("continuous_skin") and .78*orb_radius < delta.length < 1.17*orb_radius:
            factor = smoothstep(.78*orb_radius,.95*orb_radius,delta.length)
            vertex.co = (point.lerp(orb_center+delta.normalized()*orb_radius,factor))-offset
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.to_mesh(old)
    bm.free()
    old.update()
    normals = []
    attribute = old.attributes.get("SourceCornerNormal")
    for loop in old.loops:
        normal = Vector(attribute.data[loop.index].vector) if attribute else loop.normal.copy()
        point = old.vertices[loop.vertex_index].co+offset
        delta = point-orb_center
        if not cfg.get("continuous_skin") and .82*orb_radius < delta.length < 1.18*orb_radius:
            normal = delta.normalized()
        normals.append(normal.normalized() if normal.length>.01 else Vector((0,0,1)))
    old.normals_split_custom_set(normals)
    if attribute:
        old.attributes.remove(attribute)
    if len(old.polygons)>25000:
        raise RuntimeError(f"Unexpected mesh refinement budget: {len(old.polygons)}")


def nearest_chain_weights(points, point, prefix):
    choices = []
    for i,(a,b) in enumerate(zip(points,points[1:])):
        delta=b-a
        t=max(0.,min(1.,(point-a).dot(delta)/delta.length_squared))
        choices.append(((point-(a+delta*t)).length,i,t))
    _,index,t=min(choices)
    last=len(points)-2
    result={f"{prefix}.{index+1:02d}":1.} if index==last else {f"{prefix}.{index+1:02d}":1-t,f"{prefix}.{index+2:02d}":t}
    distance=sum((b-a).length for a,b in zip(points[:index],points[1:index+1]))+(points[index+1]-points[index]).length*t
    attachment=smoothstep(.035,.17,distance)
    parent="head" if prefix=="crest" else "body"
    result={key:value*attachment for key,value in result.items()}
    result[parent]=1-attachment
    return result


def vertex_weights(name, point, offset, color, hand_distances):
    cfg=ANATOMY[name]
    original=point+offset
    kind=feature_kind(name,original,color)
    if kind=="Antenna":
        if name=="Lumen":
            # The metal sail frame is an assembly: sway it as one rigid object
            # instead of bending its open triangle across nearest-curve regions.
            root=Vector(cfg["antenna"][0])-offset
            influence=smoothstep(.015,.15,(point-root).length)
            return {"antenna.01":influence,"body":1-influence}
        return nearest_chain_weights([Vector(p)-offset for p in cfg["antenna"]],point,"antenna")
    if kind=="Crest":
        return nearest_chain_weights([Vector(p)-offset for p in cfg["crest"]],point,"crest")
    if kind and kind.startswith("Scarf"):
        index=0 if kind=="Scarf.L" else 1
        return nearest_chain_weights([Vector(p)-offset for p in cfg["scarves"][index]],point,"scarf.L" if index==0 else "scarf.R")
    x,y,z=point
    foot=1-smoothstep(.12,.34,z)
    if foot>0:
        return {"foot.L" if x>0 else "foot.R":foot,"body":1-foot}
    r,g,b=color
    hand_color=(r>.34 and g>.29 and b>.26 and 1.01<r/g<1.4 and b/g>.70) if name=="Tiko" else (b>r*1.12 and b>g*1.02 and r<.4)
    arm=0
    if hand_color:
        side=0 if x<0 else 1
        shoulder=Vector(cfg["shoulders"][side])-offset
        hand=Vector(cfg["hands"][side])-offset
        axis=(hand-shoulder).normalized()
        along=(point-shoulder).dot(axis)
        perpendicular=((point-shoulder)-axis*along).length
        arm=(1-smoothstep(.19,.55,hand_distances[side]))*smoothstep(.025,.19,along)*(1-smoothstep(.13,.19,perpendicular))
    head=smoothstep(cfg["head_bottom"],cfg["head_full"],z)
    # Radio, straps and buckles remain rigid; an aerial starts at its own mount.
    if original.y>.14 and .42<z<1.53:
        head=0
    result={"body":(1-arm)*(1-head),"head":(1-arm)*head}
    if arm>0: result["arm.L" if x>0 else "arm.R"]=arm
    return {k:v for k,v in result.items() if v>1e-6}


def make_armature(name, offset):
    cfg=ANATOMY[name]
    data=bpy.data.armatures.new(name+"_Skeleton")
    rig=bpy.data.objects.new(name+"_Rig",data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active=rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    def bone(key,head,tail,parent=None):
        b=data.edit_bones.new(key)
        b.head=head; b.tail=tail
        if parent: b.parent=data.edit_bones[parent]
        b.use_deform=key!="root"
        return b
    bone("root",(0,0,0),(0,0,.25))
    bone("body",(0,0,.43),(0,0,1.10),"root")
    bone("head",Vector((0,-.12,1.09)),Vector((0,-.12,1.75)),"body")
    for shoulder,hand in zip(cfg["shoulders"],cfg["hands"]):
        shoulder,hand=Vector(shoulder)-offset,Vector(hand)-offset
        bone("arm.L" if shoulder.x>0 else "arm.R",shoulder,hand,"body")
    for side,x in (("L",.27),("R",-.27)):
        bone("foot."+side,(x,0,.20),(x,-.13,.045),"body")
    for prefix,points,parent in (("antenna",cfg["antenna"],"body"),("crest",cfg["crest"],"head")):
        for i,(a,b) in enumerate(zip(points,points[1:])):
            key=f"{prefix}.{i+1:02d}"
            bone(key,Vector(a)-offset,Vector(b)-offset,parent if i==0 else f"{prefix}.{i:02d}")
    for side,points in zip(("L","R"),cfg["scarves"]):
        for i,(a,b) in enumerate(zip(points,points[1:])):
            key=f"scarf.{side}.{i+1:02d}"
            bone(key,Vector(a)-offset,Vector(b)-offset,"body" if i==0 else f"scarf.{side}.{i:02d}")
    bpy.ops.object.mode_set(mode="OBJECT")
    rig.show_in_front=True
    rig.select_set(False)
    return rig


def logical_part(name, point, offset, color, hand_distances):
    original=point+offset
    kind=feature_kind(name,original,color)
    if kind:return kind
    if point.z<.23:return "Foot.L" if point.x>0 else "Foot.R"
    cfg=ANATOMY[name]
    r,g,b=color
    skin=(r>.025 and g>.02 and b>.015 and .99<r/max(g,.001)<1.5 and b/max(g,.001)>.68) if name=="Tiko" else (b>r*1.10 and b>g*1.01 and r<.42)
    side=0 if point.x<0 else 1
    shoulder,hand=Vector(cfg["shoulders"][side])-offset,Vector(cfg["hands"][side])-offset
    axis=(hand-shoulder).normalized();along=(point-shoulder).dot(axis)
    perpendicular=((point-shoulder)-axis*along).length
    outside=point.x<cfg["arm_planes"][0]-.000001 if side==0 else point.x>cfg["arm_planes"][1]+.000001
    fabric_guard=(r>g*1.25 and r>b*1.4 and r>.32)if name=="Tiko"else(g>.25 and b>g*1.08 and b>r*1.2 and r>.1)if name=="Nori"else False
    rear_skin=skin if original.y>.10 or name=="Lumen" else True
    if cfg.get("arm_attachment")=="shoulder":
        # A complete source flipper starts at its upper anatomical shoulder.
        # Its lower contact with the generated torso is an internal shell seam,
        # not a long pinned hinge running down the palm.
        outward=(point.x-shoulder.x)*(-1 if side==0 else 1)
        radius=.075+(cfg["arm_capsule_radius"]-.075)*smoothstep(0,.20,along)
        if skin and not fabric_guard and hand_distances[side]<.70 and 0<along<.70 and perpendicular<radius and outward>-.035 and .28<point.z<1.06 and original.y<cfg["arm_back"]:
            return "Arm.L"if side==1 else"Arm.R"
        outside=False
    if outside and rear_skin and not fabric_guard and hand_distances[side]<.80 and .30<point.z<1.01 and original.y<cfg["arm_back"]:
        return "Arm.L" if point.x>0 else "Arm.R"
    if original.y>.14 and .42<point.z<1.53:return "RadioBackpack"
    if original.y<cfg["eye_front"]+.18 and point.z>min(e[2]for e in cfg["eyes"])-cfg["eye_outer"][1]-.05:return "Head"
    if point.z>(cfg["head_bottom"]+cfg["head_full"])/2:return "Head"
    return "Body"


def split_and_skin(name, source, rig):
    offset=Vector(source["source_foot_offset"])
    mesh=source.data
    import numpy as np
    original=next(n.image for n in source.active_material.node_tree.nodes if n.type=="TEX_IMAGE" and n.label=="basecolor")
    temporary=original.copy();temporary.scale(512,512)
    pixels=np.empty(512*512*4,dtype=np.float32);temporary.pixels.foreach_get(pixels);pixels=pixels.reshape((512,512,4))
    vertex_colors=[Vector()for _ in mesh.vertices];vertex_counts=[0 for _ in mesh.vertices]
    face_colors=[]
    for polygon in mesh.polygons:
        colors=[]
        for index in polygon.loop_indices:
            uv=mesh.uv_layers.active.data[index].uv
            color=Vector(pixels[min(511,max(0,int(uv.y*512))),min(511,max(0,int(uv.x*512))),:3])
            colors.append(color);vertex_colors[mesh.loops[index].vertex_index]+=color;vertex_counts[mesh.loops[index].vertex_index]+=1
        face_colors.append(sum(colors,Vector())/len(colors))
    vertex_colors=[v/max(1,c)for v,c in zip(vertex_colors,vertex_counts)]
    bpy.data.images.remove(temporary)
    eye_specs=[]
    cfg=ANATOMY[name]
    for eye in cfg["eyes"]:
        samples=[]
        for vertex,color in zip(mesh.vertices,vertex_colors):
            p=vertex.co+offset;r,g,b=color
            if p.y<cfg["eye_front"] and abs(p.x-eye[0])<cfg["eye_outer"][0] and abs(p.z-eye[2])<.22 and r<.35 and g<.40 and b>r*1.10 and g>r*1.02:
                samples.append(p)
        if samples:
            lo_x,hi_x=min(p.x for p in samples),max(p.x for p in samples)
            lo_z,hi_z=min(p.z for p in samples),max(p.z for p in samples)
            inner_x=min(cfg["eye_outer"][0]*.82,(hi_x-lo_x)/2+.018)
            inner_z=min(cfg["eye_outer"][1]*.67,(hi_z-lo_z)/2+.018)
            eye_specs.append({"center":[(lo_x+hi_x)/2,eye[1],(lo_z+hi_z)/2],"inner":[inner_x,inner_z],"samples":len(samples)})
        else:eye_specs.append({"center":list(eye),"inner":list(cfg["eye_inner"]),"samples":0})
    rig["blink_eye_specs"]=json.dumps(eye_specs)
    graph=[[]for _ in mesh.vertices]
    for edge in mesh.edges:
        a,b=edge.vertices;length=(mesh.vertices[a].co-mesh.vertices[b].co).length
        graph[a].append((b,length));graph[b].append((a,length))
    distances=[]
    for hand in ANATOMY[name]["hands"]:
        target=Vector(hand)-offset
        start=min(range(len(mesh.vertices)),key=lambda i:(mesh.vertices[i].co-target).length_squared)
        distance=[float("inf")]*len(mesh.vertices);distance[start]=0;queue=[(0,start)]
        while queue:
            d,i=heapq.heappop(queue)
            if d!=distance[i]:continue
            for other,length in graph[i]:
                new=d+length
                if new<distance[other]:distance[other]=new;heapq.heappush(queue,(new,other))
        distances.append(distance)
    if cfg.get("continuous_skin"):
        return skin_continuous_source(name,source,rig,offset,vertex_colors,face_colors,distances,graph)
    groups={}
    for polygon in mesh.polygons:
        point=sum((mesh.vertices[i].co for i in polygon.vertices),Vector())/len(polygon.vertices)
        hand_distances=[sum(d[i]for i in polygon.vertices)/len(polygon.vertices)for d in distances]
        groups.setdefault(logical_part(name,point,offset,face_colors[polygon.index],hand_distances),[]).append(polygon)
    face_neighbors=[set()for _ in mesh.polygons];edge_faces={}
    for polygon in mesh.polygons:
        for edge in polygon.edge_keys:edge_faces.setdefault(tuple(sorted(edge)),[]).append(polygon.index)
    for adjacent in edge_faces.values():
        for i in adjacent:face_neighbors[i].update(j for j in adjacent if j!=i)
    for part in ("Arm.L","Arm.R"):
        candidates={p.index for p in groups.get(part,[])};components=[]
        while candidates:
            start=candidates.pop();component={start};queue=[start]
            while queue:
                index=queue.pop()
                for other in face_neighbors[index]&candidates:
                    candidates.remove(other);component.add(other);queue.append(other)
            components.append(component)
        if not components:raise RuntimeError(f"Missing source flipper {name} {part}")
        keep=max(components,key=len)
        removed=[mesh.polygons[i]for component in components if component is not keep for i in component]
        groups[part]=[mesh.polygons[i]for i in sorted(keep)]
        for polygon in removed:
            center=sum((mesh.vertices[i].co for i in polygon.vertices),Vector())/len(polygon.vertices)+offset
            groups.setdefault("RadioBackpack"if center.y>.14 else"Body",[]).append(polygon)
    normals=[n.vector.copy() for n in mesh.corner_normals]
    all_weights=[vertex_weights(name,v.co,offset,vertex_colors[v.index],[d[v.index]for d in distances]) for v in mesh.vertices]
    # These supplied arms are fused against the radio harness. They articulate
    # as capped source-surface flippers, with a hinge hidden inside the torso.
    # Moving the harness/body with an automatic skin gradient would stretch it.
    for i,weights in enumerate(all_weights):
        weights={key:value for key,value in weights.items()if not key.startswith("arm.")}
        total=sum(weights.values())
        all_weights[i]={key:value/total for key,value in weights.items()}if total>1e-8 else {"body":1.}
    for prefix in ("scarf.L.","scarf.R."):
        keys=[b.name for b in rig.data.bones if b.name.startswith(prefix)]
        if not keys:continue
        allowed=[]
        for color,vertex in zip(vertex_colors,mesh.vertices):
            r,g,b=color
            fabric=(r>.32 and r>g*1.25 and r>b*1.4) if name=="Tiko" else (g>.25 and b>g*1.08 and b>r*1.20 and r>.10)
            allowed.append(fabric and vertex.co.z<1.30)
        fields={key:[w.get(key,0.)for w in all_weights]for key in keys}
        for _ in range(12):
            updated={key:values.copy()for key,values in fields.items()}
            for i,neighbors in enumerate(graph):
                if not allowed[i]:
                    for key in keys:updated[key][i]=0
                    continue
                denominator=sum(1/max(length,.005)for _,length in neighbors)
                if not denominator:continue
                for key in keys:
                    average=sum(fields[key][j]/max(length,.005)for j,length in neighbors)/denominator
                    updated[key][i]=fields[key][i]*.35+average*.65
            fields=updated
        for i,old in enumerate(all_weights):
            for key in keys:old.pop(key,None)
            total=sum(old.values())
            if total<1e-8:old={"body":1.};total=1.
            chain_total=sum(fields[key][i]for key in keys)
            chain_total=min(1.,chain_total)
            all_weights[i]={key:value/total*(1-chain_total)for key,value in old.items()}
            for key in keys:
                if fields[key][i]>1e-6:all_weights[i][key]=fields[key][i]
    for i,weights in enumerate(all_weights):
        kept=sorted(weights.items(),key=lambda item:item[1],reverse=True)[:4]
        total=sum(value for _,value in kept)
        all_weights[i]={key:value/total for key,value in kept if value>1e-7}
    objects=[]
    arm_boundaries={}
    for part,polygons in groups.items():
        if not part.startswith("Arm."):continue
        edges={}
        for polygon in polygons:
            for a,b in zip(polygon.vertices,(*polygon.vertices[1:],polygon.vertices[0])):
                key=tuple(sorted((a,b)))
                if key in edges:edges.pop(key)
                else:edges[key]=(a,b)
        remaining=list(edges.values());clusters=[]
        while remaining:
            cluster=[remaining.pop()];vertices=set(cluster[0]);changed=True
            while changed:
                changed=False
                for edge in remaining[:]:
                    if vertices.intersection(edge):
                        cluster.append(edge);vertices.update(edge);remaining.remove(edge);changed=True
            clusters.append(cluster)
        arm_boundaries[part]=clusters
    for part,polygons in groups.items():
        indices=sorted({i for p in polygons for i in p.vertices})
        remap={old:new for new,old in enumerate(indices)}
        data=bpy.data.meshes.new(name+"_"+part+"_Surface")
        vertices=[mesh.vertices[i].co.copy()for i in indices]
        faces=[[remap[i]for i in p.vertices]for p in polygons]
        data.from_pydata(vertices,[],faces)
        data.materials.append(source.active_material)
        uv=data.uv_layers.new(name=mesh.uv_layers.active.name)
        new_normals=[]
        for new_polygon,old_polygon in zip(data.polygons,polygons):
            new_polygon.use_smooth=old_polygon.use_smooth
            for new_loop,old_loop in zip(new_polygon.loop_indices,old_polygon.loop_indices):
                uv.data[new_loop].uv=mesh.uv_layers.active.data[old_loop].uv
                new_normals.append(normals[old_loop])
        candidates=[]
        for polygon,color in zip(mesh.polygons,face_colors):
            r,g,b=color
            match=(r>.50 and g>.40 and b>.32 and 1.01<r/g<1.35 and b/g>.76)if name=="Tiko"else(b>r*1.12 and b>g*1.04 and r<.35)
            if match:
                point=sum((mesh.vertices[i].co for i in polygon.vertices),Vector())/len(polygon.vertices)
                target=Vector(ANATOMY[name]["hands"][0 if part.endswith("R")else 1])-offset
                candidates.append(((point-target).length_squared,polygon))
        cap_sample=min(candidates,key=lambda item:item[0])[1]if candidates else polygons[0]
        cap_uv=sum((mesh.uv_layers.active.data[i].uv for i in cap_sample.loop_indices),Vector((0,0)))/len(cap_sample.loop_indices)
        if part.startswith("Arm."):
            import bmesh
            side=0 if part.endswith("R")else 1
            axis=(Vector(ANATOMY[name]["hands"][side])-Vector(ANATOMY[name]["shoulders"][side])).normalized()
            bm=bmesh.new();bm.from_mesh(data)
            normal_layer=bm.loops.layers.float_vector.new("SavedCornerNormal")
            cap_layer=bm.faces.layers.int.new("SocketCap")
            uv_layer=bm.loops.layers.uv.active
            for face,polygon in zip(bm.faces,data.polygons):
                for loop,index in zip(face.loops,polygon.loop_indices):loop[normal_layer]=new_normals[index]
            boundary=[edge for edge in bm.edges if edge.is_boundary]
            close_root_caps(bm,boundary,part[-1],cap_layer,uv_layer,normal_layer,cap_uv)
            bm.verts.index_update()
            unique={};duplicates=[]
            for face in sorted(bm.faces,key=lambda f:f[cap_layer]):
                key=tuple(sorted(v.index for v in face.verts))
                if key in unique:duplicates.append(face)
                else:unique[key]=face
            if duplicates:bmesh.ops.delete(bm,geom=duplicates,context="FACES_ONLY")
            if any(len(edge.link_faces)!=2 for edge in bm.edges):
                raise RuntimeError(f"Arm root is not edge-closed: {name} {part}")
            if any(face.calc_area()<1e-12 for face in bm.faces):
                raise RuntimeError(f"Degenerate arm root: {name} {part}")
            bm.to_mesh(data);bm.free();data.update()
            saved=data.attributes.get("SavedCornerNormal")
            new_normals=[Vector(v.vector).normalized()for v in saved.data]
            data.normals_split_custom_set(new_normals);data.attributes.remove(saved)
        else:data.normals_split_custom_set(new_normals)
        obj=bpy.data.objects.new(name+"_"+part,data)
        bpy.context.scene.collection.objects.link(obj)
        obj.parent=rig
        for bone in rig.data.bones:
            if bone.use_deform:obj.vertex_groups.new(name=bone.name)
        for new_index,old_index in enumerate(indices):
            weights={"arm."+part[-1]:1.}if part.startswith("Arm.")else all_weights[old_index]
            for key,value in weights.items():
                obj.vertex_groups[key].add([new_index],value,"REPLACE")
        if part.startswith("Arm."):
            # Root stays on the torso in every pose. Only this detached source
            # hand surface blends into arm motion, never the radio or harness.
            for group in obj.vertex_groups:
                group.remove(list(range(len(data.vertices))))
            cap_flag=data.attributes.get("SocketCap")
            pinned={i for polygon in data.polygons if cap_flag.data[polygon.index].value for i in polygon.vertices}
            plane=ANATOMY[name]["arm_planes"][0 if part.endswith("R")else 1]
            shoulder_mode=ANATOMY[name].get("arm_attachment")=="shoulder"
            shoulder=Vector(ANATOMY[name]["shoulders"][0 if part.endswith("R")else 1])-offset
            direction=(Vector(ANATOMY[name]["hands"][0 if part.endswith("R")else 1])-offset-shoulder).normalized()
            for vertex in data.vertices:
                distance=abs(vertex.co.x-plane)
                influence=0. if vertex.index in pinned else smoothstep(.02,.16,distance)
                if shoulder_mode:
                    influence=smoothstep(-.010,.060,(vertex.co-shoulder).dot(direction))
                if influence<1:obj.vertex_groups["body"].add([vertex.index],1-influence,"REPLACE")
                if influence>0:obj.vertex_groups["arm."+part[-1]].add([vertex.index],influence,"REPLACE")
            obj["arm_attachment_mode"]="shoulder"if shoulder_mode else"pinned-side"
        modifier=obj.modifiers.new("CourierSkin","ARMATURE")
        modifier.object=rig
        modifier.use_deform_preserve_volume=False # Match Unity linear skinning.
        obj["mesh_role"]=part
        obj["source_vertex_indices"]=indices+[-1]*(len(data.vertices)-len(indices))
        objects.append(obj)
    hinges={}
    for arm in [obj for obj in objects if obj["mesh_role"].startswith("Arm.")]:
        part=arm["mesh_role"];side=0 if part.endswith("R")else 1
        flag=arm.data.attributes.get("SocketCap")
        polygons=[p for p in arm.data.polygons if flag.data[p.index].value]
        if not polygons:
            edge_use={}
            for polygon in arm.data.polygons:
                for edge in polygon.edge_keys:edge_use[edge]=edge_use.get(edge,0)+1
            if any(count==1 for count in edge_use.values()):
                folder=LIBRARY/"Review"/name;folder.mkdir(parents=True,exist_ok=True)
                bpy.ops.wm.save_as_mainfile(filepath=str(folder/"rig-debug.blend"))
                raise RuntimeError(f"Open source flipper without root cap: {arm.name}; diagnostic rig-debug.blend saved")
            # Some generated flippers are already closed shells touching the
            # body at a vertex. They need a hinge, but no invented socket face.
            points=[v.co for v in arm.data.vertices]
            outer=max(p.x for p in points)if side==0 else min(p.x for p in points)
            near=[p for p in points if abs(p.x-outer)<.08]
            center=sum(near,Vector())/len(near)
            center.x+=(.055 if side==0 else-.055)
            center.z=min((Vector(ANATOMY[name]["shoulders"][side])-offset).z,center.z+.10)
            hinges["arm.R"if side==0 else"arm.L"]=center
            continue
        indices=sorted({i for p in polygons for i in p.vertices});remap={old:new for new,old in enumerate(indices)}
        vertices=[arm.data.vertices[i].co.copy()for i in indices]
        center=sum(vertices,Vector())/len(vertices)
        inside=.055
        center.x+=inside if side==0 else -inside
        center.z=min((Vector(ANATOMY[name]["shoulders"][side])-offset).z,center.z+.10)
        if ANATOMY[name].get("arm_attachment")=="shoulder":
            center=Vector(ANATOMY[name]["shoulders"][side])-offset
        hinges["arm.R"if side==0 else"arm.L"]=center
        faces=[[remap[i]for i in reversed(p.vertices)]for p in polygons]
        data=bpy.data.meshes.new(name+"_ShoulderSocket."+part[-1]+"_Surface")
        data.from_pydata(vertices,[],faces);data.materials.append(source.active_material)
        uv=data.uv_layers.new(name=mesh.uv_layers.active.name)
        cap_normals=[]
        arm_normals=[n.vector.copy()for n in arm.data.corner_normals]
        torso_sample=Vector((0,-.30 if name!="Lumen"else-.12,.65))
        body_polygons=groups.get("Body",[])
        body_sample=min(body_polygons,key=lambda p:((sum((mesh.vertices[i].co for i in p.vertices),Vector())/len(p.vertices))-torso_sample).length_squared)
        body_uv=sum((mesh.uv_layers.active.data[i].uv for i in body_sample.loop_indices),Vector((0,0)))/len(body_sample.loop_indices)
        for polygon,original_polygon in zip(data.polygons,polygons):
            polygon.use_smooth=True
            for corner,(index,old_loop) in enumerate(zip(polygon.loop_indices,reversed(original_polygon.loop_indices))):
                uv.data[index].uv=body_uv+Vector((.00002 if corner==1 else 0,.00002 if corner==2 else 0))
                arm_vertex=arm.data.loops[old_loop].vertex_index
                source_index=arm["source_vertex_indices"][arm_vertex]
                if source_index>=0:
                    cap_normals.append(mesh.vertices[source_index].normal.copy())
                else:cap_normals.append(-arm_normals[old_loop])
        data.normals_split_custom_set(cap_normals)
        obj=bpy.data.objects.new(name+"_ShoulderSocket."+part[-1],data);bpy.context.scene.collection.objects.link(obj);obj.parent=rig
        group=obj.vertex_groups.new(name="body");group.add(list(range(len(vertices))),1.,"REPLACE")
        modifier=obj.modifiers.new("CourierSkin","ARMATURE");modifier.object=rig
        obj["mesh_role"]="ShoulderSocket."+part[-1];obj["source_vertex_indices"]=[arm["source_vertex_indices"][i]for i in indices]
        objects.append(obj)
    bpy.context.view_layer.objects.active=rig;rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for key,center in hinges.items():
        bone=rig.data.edit_bones[key]
        bone.head=center
        bone.tail=center+Vector((-.22 if key.endswith("R")else .22,0,-.10))
    bpy.ops.object.mode_set(mode="OBJECT");rig.select_set(False)
    bpy.data.objects.remove(source,do_unlink=True)
    return objects


def skin_continuous_source(name, source, rig, offset, colors, face_colors, distances, graph):
    """Keep the supplied connected surface intact; articulate small shoulder waves."""
    import numpy as np
    cfg=ANATOMY[name]
    mesh=source.data
    weights=[vertex_weights(name,v.co,offset,colors[v.index],[d[v.index]for d in distances])for v in mesh.vertices]
    arm_fields=[[0.]*len(mesh.vertices)for _ in range(2)]
    anchors=[set(),set()]
    allowed=[False]*len(mesh.vertices)
    protected=set()
    for polygon,color in zip(mesh.polygons,face_colors):
        r,g,b=color
        hardware=(g>r*1.12 or g>b*1.45 or(r>g*1.45 and r>b*1.5))if name=="Tiko"else(r>b*1.05 and g>b*.95)or(name=="Nori"and g>.26 and b>.42)
        if hardware:protected.update(polygon.vertices)
    hardware_distance=[float("inf")]*len(mesh.vertices)
    queue=[]
    for index in protected:
        hardware_distance[index]=0.;heapq.heappush(queue,(0.,index))
    while queue:
        distance,index=heapq.heappop(queue)
        if distance!=hardware_distance[index]or distance>.20:continue
        for neighbor,length in graph[index]:
            candidate=distance+length
            if candidate<hardware_distance[neighbor]:
                hardware_distance[neighbor]=candidate;heapq.heappush(queue,(candidate,neighbor))
    for vertex,color in zip(mesh.vertices,colors):
        point=vertex.co;original=point+offset;r,g,b=color
        skin=(r>.025 and g>.02 and b>.015 and .86<r/max(g,.001)<1.55 and b/max(g,.001)>.63)if name=="Tiko"else(r<.50 and b>r*1.03 and b>g*.95)
        feature=feature_kind(name,original,color)
        if not skin or feature or vertex.index in protected or not .30<point.z<1.05:continue
        side=0 if point.x<0 else 1
        shoulder=Vector(cfg["shoulders"][side])-offset
        hand=Vector(cfg["hands"][side])-offset
        axis=(hand-shoulder).normalized()
        along=(point-shoulder).dot(axis)
        radial=((point-shoulder)-axis*along).length
        outward=(point.x-shoulder.x)*(-1 if side==0 else 1)
        if not -.025<along<.70 or radial>.30 or outward<-.04:continue
        allowed[vertex.index]=True
        arm_fields[side][vertex.index]=(smoothstep(.005,.115,along)*(1-smoothstep(.18,.28,radial))*smoothstep(-.015,.075,outward)*(1-smoothstep(.57,.75,distances[side][vertex.index])))
        if along>.18 and radial<.16 and outward>.13 and distances[side][vertex.index]<.25 and hardware_distance[vertex.index]>.14:
            anchors[side].add(vertex.index)
            arm_fields[side][vertex.index]=1.
    # Mixed UV corners and the finite local influence domain also create zero
    # boundaries. They need an anchor-free transition, just like the hardware.
    zero_distance=[float("inf")]*len(mesh.vertices);queue=[]
    for i,value in enumerate(allowed):
        if not value:zero_distance[i]=0.;heapq.heappush(queue,(0.,i))
    while queue:
        distance,index=heapq.heappop(queue)
        if distance!=zero_distance[index]or distance>.09:continue
        for neighbor,length in graph[index]:
            candidate=distance+length
            if candidate<zero_distance[neighbor]:
                zero_distance[neighbor]=candidate;heapq.heappush(queue,(candidate,neighbor))
    anchors=[{i for i in field if zero_distance[i]>.08}for field in anchors]
    if not all(anchors):raise RuntimeError(f"No free distal shoulder anchors for {name}")
    # Short local smoothing softens the existing fused shoulder, with fabric,
    # buckles and the radio held on the body. No new cap or cut is created.
    targets=np.array([i for i,neighbors in enumerate(graph)for _,_ in neighbors],dtype=np.int32)
    neighbors=np.array([j for links in graph for j,_ in links],dtype=np.int32)
    conductance=np.array([1/max(length,.005)for links in graph for _,length in links])
    denominator=np.maximum(np.bincount(targets,weights=conductance,minlength=len(graph)),1e-12)
    fixed_zero=~np.asarray(allowed,dtype=bool)
    for side in range(2):
        values=np.asarray(arm_fields[side],dtype=np.float64)
        fixed_one=np.asarray(sorted(anchors[side]),dtype=np.int32)
        for _ in range(1500):
            average=np.bincount(targets,weights=values[neighbors]*conductance,minlength=len(graph))/denominator
            updated=values*.25+average*.75
            updated[fixed_zero]=0.;updated[fixed_one]=1.
            maximum_change=float(np.max(np.abs(updated-values)))
            values=updated
            if maximum_change<1e-5:break
        arm_fields[side]=values.tolist()
    for i,base in enumerate(weights):
        base={key:value for key,value in base.items()if not key.startswith("arm.")}
        total=sum(base.values())
        base={key:value/total for key,value in base.items()}if total>1e-8 else{"body":1.}
        side=0 if mesh.vertices[i].co.x<0 else 1
        influence=arm_fields[side][i]
        result={key:value*(1-influence)for key,value in base.items()}
        if influence>1e-7:result["arm.R"if side==0 else"arm.L"]=influence
        kept=sorted(result.items(),key=lambda item:item[1],reverse=True)[:4]
        total=sum(value for _,value in kept)
        weights[i]={key:value/total for key,value in kept if value>1e-7}
    source.name=name+"_Character"
    source["mesh_role"]="Character"
    source["source_vertex_indices"]=list(range(len(mesh.vertices)))
    source["arm_attachment_mode"]="continuous-source"
    source["arm_anchor_counts"]=[len(field)for field in anchors]
    source.parent=rig
    for bone in rig.data.bones:
        if bone.use_deform:source.vertex_groups.new(name=bone.name)
    for i,result in enumerate(weights):
        for key,value in result.items():source.vertex_groups[key].add([i],value,"REPLACE")
    modifier=source.modifiers.new("CourierSkin","ARMATURE")
    modifier.object=rig;modifier.use_deform_preserve_volume=False
    return[source]


def add_blink(name, objects, offset):
    from mathutils.bvhtree import BVHTree
    cfg=ANATOMY[name]
    affected=[]
    for obj in objects:
        specs=json.loads(obj.parent["blink_eye_specs"])
        points=[v.co+offset for v in obj.data.vertices]
        if not any(p.y<cfg["eye_front"]+.10 and any(abs(p.x-e["center"][0])<cfg["eye_outer"][0] and abs(p.z-e["center"][2])<cfg["eye_outer"][1] for e in specs) for p in points):continue
        obj.shape_key_add(name="Basis")
        key=obj.shape_key_add(name="Blink")
        surface=BVHTree.FromPolygons([v.co for v in obj.data.vertices],[list(p.vertices)for p in obj.data.polygons],all_triangles=True)
        front=min(v.co.y for v in obj.data.vertices)-1
        for index,p in enumerate(points):
            if p.y>=cfg["eye_front"]+.10:continue
            spec=min(specs,key=lambda e:abs(p.x-e["center"][0]));eye=spec["center"]
            dx,dz=p.x-eye[0],p.z-eye[2]
            inner_x,inner_z=spec["inner"];outer_x,outer_z=cfg["eye_outer"]
            if abs(dx)>=outer_x or abs(dz)>=outer_z:continue
            horizontal=(1-smoothstep(inner_x,outer_x,abs(dx)))*(1-smoothstep(cfg["eye_front"]-.05,cfg["eye_front"]+.10,p.y))
            target=abs(dz)*.055 if abs(dz)<=inner_z else inner_z*.055+(abs(dz)-inner_z)*(outer_z-inner_z*.055)/(outer_z-inner_z)
            key.data[index].co.z += (math.copysign(target,dz)-dz)*horizontal
            destination=key.data[index].co
            location,_,_,_=surface.ray_cast(Vector((destination.x,front,destination.z)),Vector((0,1,0)),10)
            if location is not None:destination.y=location.y
        affected.append(obj.name)
    if not affected:raise RuntimeError(f"No localized blink mesh for {name}")
    return affected


def apply_review_pose(rig, objects, pose):
    for bone in rig.pose.bones:
        bone.rotation_mode="XYZ";bone.rotation_euler=(0,0,0)
    for obj in objects:
        if obj.data.shape_keys:
            obj.data.shape_keys.key_blocks["Blink"].value=1 if pose=="blink" else 0
    if pose=="arms-raised"or pose.startswith("arms-"):
        # Rotate around the source front axis; opposite sides raise away from hips.
        from mathutils import Quaternion
        amplitude=105 if pose=="arms-raised"else float(pose.split("-")[1])
        for side,angle in (("L",-amplitude),("R",amplitude)):
            bone=rig.pose.bones["arm."+side]
            axis=bone.bone.matrix_local.to_quaternion().inverted()@Vector((0,1,0))
            bone.rotation_mode="QUATERNION"
            bone.rotation_quaternion=Quaternion(axis,math.radians(angle))
    elif pose=="flutter":
        from mathutils import Quaternion
        for bone in rig.pose.bones:
            if bone.name.startswith(("antenna.","crest.","scarf.")):
                axis=bone.bone.matrix_local.to_quaternion().inverted()@Vector((1,0,0))
                bone.rotation_mode="QUATERNION"
                bone.rotation_quaternion=Quaternion(axis,math.radians(9 if bone.name.startswith("scarf") else 4))
    bpy.context.view_layer.update()


def rig_character(name):
    reset()
    source=import_source(name)
    offset=Vector(source["source_foot_offset"])
    refine_surface(name,source)
    rig=make_armature(name,offset)
    objects=split_and_skin(name,source,rig)
    blink_meshes=add_blink(name,objects,offset)
    folder=LIBRARY/"Review"/name
    folder.mkdir(parents=True,exist_ok=True)
    camera=render_setup()
    bounds=[Vector(c) for obj in objects for c in obj.bound_box]
    min_x,max_x=min(p.x for p in bounds),max(p.x for p in bounds)
    camera.data.ortho_scale=max(3.15,(max_x-min_x)/(720/960)*1.14)
    camera.location=((min_x+max_x)/2,-6,1.3)
    aim(camera,((min_x+max_x)/2,0,1.3))
    poses=("rest","blink","arms-15","arms-25","arms-35","flutter")if ANATOMY[name].get("continuous_skin")else("rest","blink","arms-30","arms-60","arms-90","arms-raised","flutter")if ANATOMY[name].get("arm_attachment")=="shoulder"else("rest","blink","arms-raised","flutter")
    for pose in poses:
        apply_review_pose(rig,objects,pose)
        bpy.context.scene.render.filepath=str(folder/f"rig-{pose}.png")
        bpy.ops.render.render(write_still=True)
        print(f"RADIO_RIG_REVIEW {name} {pose}",flush=True)
        if pose=="arms-raised"or pose.startswith("arms-"):
            views=(("three-quarter",(4,-6,1.3)),("back",(0,6,1.3)))if pose=="arms-raised"else(("three-quarter",(4,-6,1.3)),)
            for side,location in views:
                camera.location=location;aim(camera,((min_x+max_x)/2,0,1.3))
                bpy.context.scene.render.filepath=str(folder/f"rig-{pose}-{side}.png")
                bpy.ops.render.render(write_still=True)
            camera.location=((min_x+max_x)/2,-6,1.3);aim(camera,((min_x+max_x)/2,0,1.3))
    apply_review_pose(rig,objects,"rest")
    bpy.ops.wm.save_as_mainfile(filepath=str(folder/"rig-review.blend"))
    manifest={"character":name,"coordinate_system":"Blender Z up, facing -Y, feet at root origin; FBX export forward -Z/up Y","height":2.6,"source_foot_offset":list(offset),"triangles":sum(len(o.data.polygons)for o in objects),"meshes":[{"name":o.name,"role":o["mesh_role"],"vertices":len(o.data.vertices),"triangles":len(o.data.polygons)}for o in objects],"bones":[{"name":b.name,"parent":b.parent.name if b.parent else None,"head":list(b.head_local),"tail":list(b.tail_local)}for b in rig.data.bones],"blink_meshes":blink_meshes,"blink_shape":"Blink","has_crest":bool(ANATOMY[name]["crest"]),"has_scarf":bool(ANATOMY[name]["scarves"])}
    (folder/"rig-manifest.json").write_text(json.dumps(manifest,indent=2),encoding="utf-8")
    print(f"RADIO_RIG_COMPLETE {name} tris={manifest['triangles']} meshes={len(objects)} blink={blink_meshes}",flush=True)


def aim(obj, point):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_setup():
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 24
    scene.cycles.use_denoising = True
    scene.cycles.max_bounces = 5
    scene.render.resolution_x = 720
    scene.render.resolution_y = 960
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGBA"
    scene.render.film_transparent = True
    scene.world = bpy.data.worlds.new("NeutralStudio")
    scene.world.use_nodes = True
    scene.world.node_tree.nodes["Background"].inputs[0].default_value = (.8, .84, .9, 1)
    scene.world.node_tree.nodes["Background"].inputs[1].default_value = .35
    scene.view_settings.view_transform = "AgX"
    for name, location, energy, size, color in (
        ("Key", (3,-4,5), 450, 4, (1,.9,.77)),
        ("Fill", (-4,-2,2.5), 230, 4, (.77,.88,1)),
        ("Rim", (1,3,4), 550, 3, (1,.98,.9)),
    ):
        data = bpy.data.lights.new(name, "AREA")
        data.energy, data.shape, data.size, data.color = energy, "DISK", size, color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        aim(obj, (0,0,1.2))
    camera_data = bpy.data.cameras.new("ReviewCamera")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 3.1
    camera = bpy.data.objects.new("ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def render_views(name, prefix="source"):
    camera = render_setup()
    folder = LIBRARY / "Review" / name
    folder.mkdir(parents=True, exist_ok=True)
    for side, location in (("front", (0,-6,1.3)), ("right", (-6,0,1.3)), ("back", (0,6,1.3)), ("left", (6,0,1.3))):
        meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
        bounds = [obj.matrix_world @ Vector(c) for obj in meshes for c in obj.bound_box]
        axis = 0 if side in ("front", "back") else 1
        width = max(p[axis] for p in bounds) - min(p[axis] for p in bounds)
        camera.data.ortho_scale = max(3.1, width / (720 / 960) * 1.12)
        camera.location = location
        aim(camera, (0,0,1.3))
        bpy.context.scene.render.filepath = str(folder / f"{prefix}-{side}.png")
        bpy.ops.render.render(write_still=True)
        print(f"RADIO_RENDER {name} {side} {bpy.context.scene.render.filepath}", flush=True)


def inspect(name):
    reset()
    mesh = import_source(name)
    folder = LIBRARY / "Review" / name
    folder.mkdir(parents=True, exist_ok=True)
    metadata = {"name":name, "vertices":len(mesh.data.vertices), "polygons":len(mesh.data.polygons), "bounds":[list(c) for c in mesh.bound_box], "materials":[m.name for m in mesh.data.materials], "normal_attributes":[a.name for a in mesh.data.attributes], "uv_layers":[uv.name for uv in mesh.data.uv_layers]}
    (folder / "source-mesh.json").write_text(json.dumps(metadata, indent=2), encoding="utf-8")
    render_views(name)
    bpy.ops.wm.save_as_mainfile(filepath=str(folder / "source-inspection.blend"))


def analyze(name):
    import numpy as np
    reset()
    mesh = import_source(name)
    original = next(n.image for n in mesh.active_material.node_tree.nodes if n.type == "TEX_IMAGE" and n.label == "basecolor")
    temporary = original.copy()
    temporary.scale(512,512)
    pixels = np.empty(512 * 512 * 4, dtype=np.float32)
    temporary.pixels.foreach_get(pixels)
    pixels = pixels.reshape((512,512,4))
    folder = LIBRARY / "Review" / name
    folder.mkdir(parents=True, exist_ok=True)
    with (folder / "source-faces.csv").open("w", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(("face", "x", "y", "z", "r", "g", "b"))
        for polygon in mesh.data.polygons:
            center = sum((mesh.data.vertices[i].co for i in polygon.vertices), Vector()) / len(polygon.vertices)
            uv = sum((mesh.data.uv_layers.active.data[i].uv for i in polygon.loop_indices), Vector((0,0))) / len(polygon.loop_indices)
            color = pixels[min(511,int(uv.y * 512)),min(511,int(uv.x * 512))]
            writer.writerow((polygon.index,*center,*color[:3]))
    bpy.data.images.remove(temporary)
    print("RADIO_ANALYZED",name,flush=True)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=("inspect", "analyze", "rig", "accessories", "paws", "rounded"), default="inspect")
    parser.add_argument("--character", choices=CHARACTERS)
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    for name in ((args.character,) if args.character else CHARACTERS):
        if args.stage == "rounded":
            from rebuild_smooth_couriers import process
            process(name, do_render=False, publish=True, repair_torso=True)
        elif args.stage == "paws":
            from prepare_radio_arm_clearance import process
            process(name, False, True)
        elif args.stage == "accessories":
            from prepare_radio_accessories import process
            process(name, False)
        else:
            {"inspect":inspect,"analyze":analyze,"rig":rig_character}[args.stage](name)


if __name__ == "__main__":
    main()
