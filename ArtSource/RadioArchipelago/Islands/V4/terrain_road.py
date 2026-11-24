"""A single connected floating landscape and a genuinely closed, flush road.

All guide coordinates use X=(guideX-500)*world_scale,
Y=(500-guideY)*world_scale. The road's upper face is exactly Z=0.
No scene reset, operators, cameras, lights, bridges above the road, or exports.
"""
import math
import random
import bpy
import bmesh
from mathutils import Vector
from mathutils.kdtree import KDTree
from mathutils.bvhtree import BVHTree
from mathutils.geometry import delaunay_2d_cdt
import geology_detail

TAU=math.tau


def _rgba(value):
    if isinstance(value,str):
        value=value.lstrip('#')
        if len(value)==3:value=''.join(c*2 for c in value)
        return tuple(int(value[i:i+2],16)/255 for i in (0,2,4))+(1,)
    return tuple(value[:3])+(1,)


def _lin(c):
    return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4


def _mat(name,color,roughness=.88,noise_scale=2.8,bump=.1):
    rgba=_rgba(color)
    key='RA4_Ground_'+name+'_'+''.join(f'{round(c*255):02x}' for c in rgba[:3])
    mat=bpy.data.materials.get(key)
    if mat:return mat
    mat=bpy.data.materials.new(key)
    mat.diffuse_color=rgba
    mat.use_nodes=True
    nodes=mat.node_tree.nodes
    links=mat.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    color=tuple(_lin(c) for c in rgba[:3])+(1,)
    bs.inputs['Base Color'].default_value=color
    bs.inputs['Roughness'].default_value=roughness
    coord=nodes.new('ShaderNodeTexCoord')
    tex=nodes.new('ShaderNodeTexNoise')
    tex.inputs['Scale'].default_value=noise_scale
    tex.inputs['Detail'].default_value=3.2
    tex.inputs['Roughness'].default_value=.72
    links.new(coord.outputs['Object'],tex.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].position=.17
    ramp.color_ramp.elements[0].color=tuple(c*.72 for c in color[:3])+(1,)
    ramp.color_ramp.elements[1].position=.83
    ramp.color_ramp.elements[1].color=tuple(min(1,c*1.13+.008) for c in color[:3])+(1,)
    links.new(tex.outputs['Fac'],ramp.inputs[0])
    links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
    if bump:
        bn=nodes.new('ShaderNodeBump')
        bn.inputs['Strength'].default_value=.25
        bn.inputs['Distance'].default_value=bump
        links.new(tex.outputs['Fac'],bn.inputs['Height'])
        links.new(bn.outputs['Normal'],bs.inputs['Normal'])
    return mat


def _mesh(name,verts,faces,materials,collection,role):
    mesh=bpy.data.meshes.new(name)
    mesh.from_pydata(verts,[],faces)
    mesh.update()
    obj=bpy.data.objects.new(name,mesh)
    collection.objects.link(obj)
    for material in materials:mesh.materials.append(material)
    obj['v3_role']=role
    obj['radio_archipelago_role']=role
    return obj


def _paving_material(name,color,wood=False):
    """Flat metric-UV paving: shader relief only, never raised geometry."""
    material=_mat(name+'_metric_paving_v4',color,roughness=.84,noise_scale=2.4,bump=0)
    nodes=material.node_tree.nodes
    if any(n.label=='Metric flat paving' for n in nodes):return material
    links=material.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    uv=nodes.new('ShaderNodeTexCoord')
    brick=nodes.new('ShaderNodeTexBrick')
    brick.label='Metric flat paving'
    brick.inputs['Scale'].default_value=1
    brick.inputs['Mortar Size'].default_value=.026 if wood else .065
    brick.inputs['Mortar Smooth'].default_value=.006
    brick.inputs['Brick Width'].default_value=.27 if wood else .79
    brick.inputs['Row Height'].default_value=8.0 if wood else .56
    brick.offset=0.0 if wood else .5
    brick.offset_frequency=2
    rgba=_rgba(color)
    base=[_lin(c) for c in rgba[:3]]
    brick.inputs['Color1'].default_value=tuple(c*.65 for c in base)+(1,)
    brick.inputs['Color2'].default_value=tuple(min(1,c*1.23+.012) for c in base)+(1,)
    brick.inputs['Mortar'].default_value=tuple(c*.31 for c in base)+(1,)
    links.new(uv.outputs['UV'],brick.inputs['Vector'])
    links.new(brick.outputs['Color'],bs.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump')
    bump.inputs['Strength'].default_value=.36
    bump.inputs['Distance'].default_value=.04
    bump.invert=True
    links.new(brick.outputs['Fac'],bump.inputs['Height'])
    links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    return material


def _resample(points,spacing):
    points=[tuple(p[:2]) for p in points]
    if len(points)>1 and math.dist(points[0],points[-1])<1e-5:points.pop()
    lengths=[math.dist(points[i],points[(i+1)%len(points)]) for i in range(len(points))]
    total=sum(lengths)
    count=max(8,math.ceil(total/spacing))
    out=[]
    edge=0
    start=0
    for i in range(count):
        s=i*total/count
        while edge<len(lengths)-1 and start+lengths[edge]<s:
            start+=lengths[edge]
            edge+=1
        t=(s-start)/max(lengths[edge],1e-10)
        a,b=points[edge],points[(edge+1)%len(points)]
        out.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
    return out


def _inside(x,y,poly):
    result=False
    j=len(poly)-1
    for i in range(len(poly)):
        a,b=poly[i],poly[j]
        if (a[1]>y)!=(b[1]>y) and x<(b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]:result=not result
        j=i
    return result


def _smooth(t):
    t=max(0,min(1,t))
    return t*t*(3-2*t)


def build_landscape(route_data,palette,collection=None,seed=0,world_scale=.18,road_width=8.,shoulder=2.,world_id='01_emerald_ravine'):
    """Return terrain/road geometry and exact raycast-based terrain height lookup."""
    rng=random.Random(seed)
    if isinstance(collection,str):
        collection=bpy.data.collections.get(collection) or bpy.data.collections.new(collection)
        if not collection.users:bpy.context.scene.collection.children.link(collection)
    collection=collection or bpy.context.scene.collection
    palette=palette or {}
    default=dict(land='#65864C',rock='#46545B',road='#8B9698',water='#4D92A8',accent='#9AA67A')
    default.update({k:v for k,v in palette.items() if k in default})
    land_rgba,rock_rgba=_rgba(default['land']),_rgba(default['rock'])
    earth=tuple((land_rgba[i]*.58+rock_rgba[i]*.42)*.82 for i in range(3))
    mats=[_mat('land',default['land'],noise_scale=.35,bump=.11),
          _mat('rock',default['rock'],noise_scale=.65,bump=.2),
          _mat('rock_strata',default['rock'],noise_scale=1.8,bump=.13),
          _mat('natural_earth_patches',earth,noise_scale=.43,bump=.12)]
    # Broad colour variation blends continuously in world space, rather than
    # painting polygon-shaped dark spots or a road-following shoulder stripe.
    land_nodes=mats[0].node_tree.nodes
    land_links=mats[0].node_tree.links
    if not any(node.label=='Natural macro land variation'for node in land_nodes):
        macro=land_nodes.new('ShaderNodeTexNoise');macro.label='Natural macro land variation'
        macro.inputs['Scale'].default_value=.055;macro.inputs['Detail'].default_value=2.5
        coord=next(node for node in land_nodes if node.type=='TEX_COORD')
        ramp=next(node for node in land_nodes if node.type=='VALTORGB')
        land_links.new(coord.outputs['Object'],macro.inputs['Vector'])
        land_links.new(macro.outputs['Fac'],ramp.inputs[0])
    roadmat=_paving_material('road',default['road'])
    roadalt=_paving_material('road_worn',default['road'])
    woodmat=_paving_material('bridge_timber',palette.get('wood','#75543D'),wood=True)
    watermat=_mat('water',default['water'],roughness=.31,noise_scale=.13,bump=.055)
    prefix='RA4_'+world_id+'_'
    def xy(p):return ((float(p[0])-500)*world_scale,(500-float(p[1]))*world_scale)
    route=route_data['route']
    source=route.get('samples',[])
    points=[xy((p['x'],p['y'])) if isinstance(p,dict) else xy(p) for p in source]
    if not points:points=[xy(p) for p in route['controlPoints']]
    points=_resample(points,.8)
    n=len(points)
    outline=_resample([xy(p) for p in route_data['island']['outlineSamples']],2.2)
    area=sum(outline[i][0]*outline[(i+1)%len(outline)][1]-outline[(i+1)%len(outline)][0]*outline[i][1] for i in range(len(outline)))
    if area<0:outline.reverse()
    tree=KDTree(n)
    for i,p in enumerate(points):tree.insert((p[0],p[1],0),i)
    tree.balance()
    def distance(x,y):
        _,idx,_=tree.find((x,y,0))
        best=1e30
        for j in range(idx-2,idx+3):
            a,b=points[j%n],points[(j+1)%n]
            dx,dy=b[0]-a[0],b[1]-a[1]
            t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/max(dx*dx+dy*dy,1e-12)))
            best=min(best,(x-a[0]-t*dx)**2+(y-a[1]-t*dy)**2)
        return math.sqrt(best)
    normals=[]
    for i,p in enumerate(points):
        a,b=points[(i-1)%n],points[(i+1)%n]
        dx,dy=b[0]-a[0],b[1]-a[1]
        length=max(math.hypot(dx,dy),1e-10)
        normals.append((-dy/length,dx/length))
    landmarks=[]
    for landmark in route_data.get('landmarks',[]):
        if 'center' not in landmark:continue
        c=xy(landmark['center'])
        radii=landmark.get('radii',[35,25])
        landmarks.append(dict(landmark,xy=c,rx=float(radii[0])*world_scale,ry=float(radii[1])*world_scale))
    water_kinds=('pond','pool','lake','oasis','inlet','ravine','water','stream','brook','crater','fissure')
    watery=[l for l in landmarks if any(w in (l.get('kind','')+' '+l.get('name','')).lower() for w in water_kinds)]
    bridges=[l for l in landmarks if 'bridge' in (l.get('kind','')+' '+l.get('name','')).lower()]
    if 'autumn' in world_id:
        bridges.extend(l for l in watery if l.get('kind')=='stream')
    river=route_data.get('river_v4')
    river_points=[tuple(float(v)for v in p[:2])for p in river.get('points',[])]if river else[]
    if river and len(river_points)<2:raise ValueError('river_v4 needs at least two world-XY points')
    river_width=float(river.get('width',6))if river else 0
    river_half=river_width*.5
    river_carve_half=river_half+.40 if river else 0
    river_bank=float(river.get('bank_width',3))if river else 0
    river_water=float(river.get('water_z',-.65))if river else-.65
    river_bed=float(river.get('bed_z',-1.8))if river else-1.8
    if river and not(river_width>0 and river_bank>0 and river_bed<river_water<-.08):
        raise ValueError('river_v4 needs width/bank_width>0 and bed_z<water_z<-.08')
    if river and world_id.startswith('01_'):
        watery=[];bridges=[]
    river_segments=[]
    for a,b in zip(river_points,river_points[1:]):
        dx,dy=b[0]-a[0],b[1]-a[1]
        river_segments.append((a[0],a[1],dx,dy,max(dx*dx+dy*dy,1e-12)))
    def river_distance(x,y):
        best=1e30
        for ax,ay,dx,dy,den in river_segments:
            t=max(0,min(1,((x-ax)*dx+(y-ay)*dy)/den))
            best=min(best,(x-ax-t*dx)**2+(y-ay-t*dy)**2)
        return math.sqrt(best)
    def carve_river(x,y,value):
        if not river:return value
        d=river_distance(x,y)
        if d>=river_carve_half+river_bank:return value
        bed_edge=river_water-.32
        if d<=river_carve_half:
            target=river_bed+(bed_edge-river_bed)*_smooth(d/max(river_carve_half,1e-6))
        else:
            blend=_smooth((d-river_carve_half)/river_bank)
            target=bed_edge*(1-blend)+value*blend
        return min(value,target)
    half=road_width/2
    edge=half+shoulder
    clear=edge+.25
    def ellipse(l,x,y):
        a=-math.radians(l.get('angle',0))
        c,s=math.cos(a),math.sin(a)
        dx,dy=x-l['xy'][0],y-l['xy'][1]
        return math.hypot((c*dx+s*dy)/max(l['rx'],.1),(-s*dx+c*dy)/max(l['ry'],.1))
    # Local elongated ridges, with large quiet glades between them. They are
    # placed independently of the road curvature, rather than manufacturing a
    # constant-height berm around every bend of the route.
    ridge_rng=random.Random(seed*1049+901)
    center_x=sum(p[0]for p in outline)/len(outline)
    center_y=sum(p[1]for p in outline)/len(outline)
    ridges=[]
    for i in range(0,len(outline),max(1,len(outline)//11)):
        ox,oy=outline[i]
        dx,dy=center_x-ox,center_y-oy
        length=max(math.hypot(dx,dy),1)
        inset=ridge_rng.uniform(7,15)
        rx,ry=ox+dx/length*inset,oy+dy/length*inset
        if distance(rx,ry)<14:continue
        ridges.append((rx,ry,math.atan2(dy,dx)+ridge_rng.uniform(-.6,.6),
                       ridge_rng.uniform(16,25),ridge_rng.uniform(6,10),ridge_rng.uniform(4.5,10)))
    def natural_height(x,y):
        dr=distance(x,y)
        # All road shoulders start with an unobstructed, perfectly flat corridor.
        corridor_floor=-.13
        for l in watery:
            e=ellipse(l,x,y)
            if e<1.22:
                wet_blend=1-_smooth((e-.63)/.59)
                corridor_floor=min(corridor_floor,-.13-2.05*wet_blend)
        if dr<=clear:return carve_river(x,y,corridor_floor)
        # Natural low land grades into isolated ridges. The previous uniform
        # six-metre rise made the whole road read as a golf-course trench.
        phase=seed*.73
        broad=.75+.48*math.sin(x*.038+phase)*math.cos(y*.041-phase)+.35*math.sin((x+y)*.027+1.7)
        fine=.16*math.sin(x*.28+y*.17)+.11*math.cos(x*.21-y*.25)
        value=broad+fine
        for rx,ry,angle,long_radius,short_radius,height in ridges:
            dx,dy=x-rx,y-ry
            u=dx*math.cos(angle)+dy*math.sin(angle)
            v=-dx*math.sin(angle)+dy*math.cos(angle)
            value+=height*math.exp(-2.0*((u/long_radius)**2+(v/short_radius)**2))
        if 'frost'in world_id:value*=.83
        if 'cloud'in world_id:value=value*.72+.3
        if 'sunflower'in world_id:value*=.53
        for l in watery:
            e=ellipse(l,x,y)
            if e<1.25:
                blend=1-_smooth((e-.65)/.6)
                value=value*(1-blend)+(-1.5-.4*math.cos(e*math.pi))*blend
        # Grade to the shoulder continuously; terrain cannot poke into the road.
        blend=_smooth((dr-clear)/(9.0+1.8*math.sin(x*.025+y*.033+phase)))
        return carve_river(x,y,corridor_floor+(value-corridor_floor)*blend)
    # Optional authored house clearings flatten terrain itself under the real
    # terrace footprint, eliminating tall monolithic foundation blocks. Raw
    # heights are sampled before any pad influence; the road always wins.
    building_pads=[]
    for pad in route_data.get('building_pads_v4',[]):
        center=tuple(float(v)for v in pad['center'][:2])
        flat=float(pad.get('flat_radius',12.0));blend=float(pad.get('blend_radius',13.8))
        if flat<0 or blend<=flat:raise ValueError('Building pad needs 0 <= flat_radius < blend_radius')
        building_pads.append((center,flat,blend,natural_height(*center)))
    def analytic_height(x,y):
        raw=natural_height(x,y)
        if not building_pads or distance(x,y)<=clear:return raw
        influences=[(math.hypot(x-center[0],y-center[1]),center,flat,blend,z)
                    for center,flat,blend,z in building_pads
                    if math.hypot(x-center[0],y-center[1])<blend]
        if not influences:return raw
        d,center,flat,blend,z=min(influences,key=lambda item:item[0])
        weight=1-_smooth((d-flat)/(blend-flat))
        return carve_river(x,y,raw*(1-weight)+z*weight)
    # Constrained triangulation: outline and both flat corridor boundaries are
    # real mesh edges, preventing long triangles from rising through the road.
    verts2=[Vector(p) for p in outline]
    outline_n=len(verts2)
    constraints=[(i,(i+1)%outline_n) for i in range(outline_n)]
    for radius in (half+.45,clear,edge+3.6):
        for sign in (-1,1):
            base=len(verts2)
            verts2.extend(Vector((p[0]+sign*radius*v[0],p[1]+sign*radius*v[1])) for p,v in zip(points,normals))
            constraints.extend((base+i,base+(i+1)%n) for i in range(n))
    if river:
        river_normals=[]
        for i,p in enumerate(river_points):
            a,b=river_points[max(0,i-1)],river_points[min(len(river_points)-1,i+1)]
            dx,dy=b[0]-a[0],b[1]-a[1];length=max(math.hypot(dx,dy),1e-9)
            river_normals.append((-dy/length,dx/length))
        # Real channel edges/bed center are CDT constraints, so long shoreline
        # triangles cannot fill in the winding channel beneath water or bridge.
        for radius in(-river_carve_half,0,river_carve_half):
            base=len(verts2)
            verts2.extend(Vector((p[0]+normal[0]*radius,p[1]+normal[1]*radius))for p,normal in zip(river_points,river_normals))
            constraints.extend((base+i,base+i+1)for i in range(len(river_points)-1))
    # Staggered, gently perturbed grid keeps the surface naturally triangulated.
    minx,maxx=min(p[0] for p in outline),max(p[0] for p in outline)
    miny,maxy=min(p[1] for p in outline),max(p[1] for p in outline)
    step=2.15
    row=0
    y=miny+step*.5
    while y<maxy:
        x=minx+step*.5+(row%2)*step*.5
        while x<maxx:
            xx=x+rng.uniform(-.23,.23)
            yy=y+rng.uniform(-.23,.23)
            if _inside(xx,yy,outline):verts2.append(Vector((xx,yy)))
            x+=step
        y+=step*.87
        row+=1
    vv,ee,ff,_,_,_=delaunay_2d_cdt(verts2,constraints,[list(range(outline_n))],1,1e-5,False)
    topverts=[(p.x,p.y,analytic_height(p.x,p.y)) for p in vv]
    topfaces=[tuple(f) for f in ff if len(f)>=3 and _inside(sum(vv[i].x for i in f)/len(f),sum(vv[i].y for i in f)/len(f),outline)]
    used=sorted({i for f in topfaces for i in f})
    remap={old:new for new,old in enumerate(used)}
    topverts=[topverts[i] for i in used]
    topfaces=[tuple(remap[i] for i in f) for f in topfaces]
    # Determine the exact triangulation boundary so the slab is fully connected.
    edgecounts={}
    edgedirections={}
    for face in topfaces:
        for i,a in enumerate(face):
            b=face[(i+1)%len(face)]
            key=tuple(sorted((a,b)))
            edgecounts[key]=edgecounts.get(key,0)+1
            edgedirections[key]=(a,b)
    boundary=[edgedirections[e] for e,count in edgecounts.items() if count==1]
    boundary_indices=sorted({i for e in boundary for i in e})
    cx=sum(p[0] for p in outline)/len(outline)
    cy=sum(p[1] for p in outline)/len(outline)
    verts=list(topverts)
    faces=list(topfaces)
    # Patchy earth and exposed stone follow the natural relief; there is no
    # continuous pale shoulder material band tracing the road.
    face_mats=[]
    for f in topfaces:
        x=sum(topverts[i][0]for i in f)/len(f)
        y=sum(topverts[i][1]for i in f)/len(f)
        z=sum(topverts[i][2]for i in f)/len(f)
        face_mats.append(0 if river and river_distance(x,y)<river_carve_half+river_bank+.8 else(3 if z<-.45 else 0))
    prior={i:i for i in boundary_indices}
    # Broad broken buttresses replace concentric uniform skirts. Each angular
    # sector has its own early taper, layer height, and geological lobe. The
    # underside remains one manifold mesh joined to the unchanged top rim.
    geological_rng=random.Random(seed*71+903)
    sectors=22 if 'frost' not in world_id else 16
    radial_noise=[geological_rng.uniform(-1,1) for _ in range(sectors)]
    depth_noise=[geological_rng.uniform(-1,1) for _ in range(sectors)]
    def geological_profile(angle,values):
        t=(angle/TAU%1)*sectors
        j=int(t)
        f=t-j
        return values[j%sectors]*(1-f)+values[(j+1)%sectors]*f
    coral=any(k in world_id for k in ('coral','sunflower'))
    ice='frost' in world_id
    storm=any(k in world_id for k in ('storm','volcanic'))
    layers=((.985,3.0),(.915,11.0),(.88,17.0),(.79,24.0),(.74,30.0))
    if coral:layers=((1.,3.),(.95,7.),(.95,11.),(.86,15.),(.86,21.),(.75,27.),(.72,32.))
    if ice:layers=((.96,5.),(.86,14.),(.81,21.),(.72,29.))
    if storm:layers=((.97,5.),(.90,13.),(.82,21.),(.76,31.))
    for layer,(shrink,depth) in enumerate(layers):
        current={}
        for i in boundary_indices:
            x,y,z=topverts[i]
            theta=math.atan2(y-cy,x-cx)
            buttress=geological_profile(theta,radial_noise)
            fracture=geological_profile(theta,depth_noise)
            # The same broad buttress persists through several heights, giving
            # large faceted rock masses instead of a necklace of small rocks.
            strength=(.045+.016*layer)*(1.35 if storm else 1)
            local_shrink=shrink+strength*buttress
            if ice:local_shrink+=.028*math.sin(theta*8+layer*.9)
            dx,dy=x-cx,y-cy
            shearx=(layer+1)*.7*math.sin(seed*.37+theta*2)
            sheary=(layer+1)*.55*math.cos(seed*.23+theta*3)
            relief=(2.2+layer*1.35)*fracture+(1.5 if coral else 2.6)*math.sin(theta*3+seed*.3)
            zz=-depth-relief
            # First ledges always remain below terrain and below the road.
            zz=min(-.5,zz)
            current[i]=len(verts)
            verts.append((cx+dx*local_shrink+shearx,cy+dy*local_shrink+sheary,zz))
        for a,b in boundary:
            # Triangular splits expose major crystalline/fractured planes.
            quad=(prior[b],prior[a],current[a],current[b])
            if not coral and (ice or storm or (a+layer)%3==0):
                faces.extend(((quad[0],quad[1],quad[2]),(quad[0],quad[2],quad[3])))
                face_mats.extend((1,2 if (a+layer)%4==0 else 1))
            else:
                faces.append(quad)
                face_mats.append(2 if coral and layer%2 else 1)
        prior=current
    # A real triangulated lower surface supports multiple hanging geological
    # roots. Moving the major root toward the front outer mass makes its depth
    # visible from the canonical isometric view rather than hiding centrally.
    following={a:b for a,b in boundary}
    start=boundary[0][0]
    ordered=[start]
    cursor=following[start]
    while cursor!=start and len(ordered)<=len(boundary_indices):
        ordered.append(cursor)
        cursor=following[cursor]
    cap_outline=[(verts[prior[i]][0],verts[prior[i]][1]) for i in ordered]
    cap_inputs=[Vector(p) for p in cap_outline]
    cap_n=len(cap_inputs)
    width=maxx-minx
    depth_span=maxy-miny
    # The sealed main undercroft stays shallow and irregular. Many narrow,
    # independently fractured hanging roots are added below, instead of three
    # enormous triangular pyramids in the central cap.
    roots=[]
    cap_step=6.8 if not ice else 9.0
    yy=min(p[1] for p in cap_outline)+cap_step*.5
    while yy<max(p[1] for p in cap_outline):
        xx=min(p[0] for p in cap_outline)+cap_step*.5
        while xx<max(p[0] for p in cap_outline):
            px=xx+geological_rng.uniform(-1.6,1.6)
            py=yy+geological_rng.uniform(-1.6,1.6)
            if _inside(px,py,cap_outline):cap_inputs.append(Vector((px,py)))
            xx+=cap_step
        yy+=cap_step
    for rx,ry,rz,spread in roots:
        if _inside(rx,ry,cap_outline):cap_inputs.append(Vector((rx,ry)))
    cap_v,cap_e,cap_f,_,_,_=delaunay_2d_cdt(cap_inputs,[(i,(i+1)%cap_n) for i in range(cap_n)],[list(range(cap_n))],1,1e-5,False)
    cap_tree=KDTree(cap_n)
    for j,p in enumerate(cap_outline):cap_tree.insert((p[0],p[1],0),j)
    cap_tree.balance()
    cap_map={}
    for j,p in enumerate(cap_v):
        _,nearest,dist=cap_tree.find((p.x,p.y,0))
        if dist<1e-4:
            cap_map[j]=prior[ordered[nearest]]
            continue
        # Smooth broad root cones remain faceted by the intentionally coarse
        # lower triangulation. Boundary blending seals them into the skirt.
        zz=-32.0-2.4*math.sin(p.x*.08+p.y*.035+seed)-1.2*math.cos(p.x*.14-p.y*.09)
        for rx,ry,rz,spread in roots:
            radial=math.hypot((p.x-rx),(p.y-ry)*1.1)
            influence=max(0,1-radial/spread)
            zz=min(zz,-30-(rz-30)*influence)
        boundary_z=verts[prior[ordered[nearest]]][2]
        blend=_smooth(dist/7.0)
        zz=boundary_z+(zz-boundary_z)*blend
        cap_map[j]=len(verts)
        verts.append((p.x,p.y,zz))
    for f in cap_f:
        faces.append(tuple(cap_map[i] for i in reversed(f)))
        face_mats.append(2 if sum(f)%7==0 else 1)
    terrain=_mesh(prefix+'CONNECTED_FLOATING_TERRAIN',verts,faces,mats,collection,'terrain')
    for poly,idx in zip(terrain.data.polygons,face_mats):
        poly.material_index=idx
        if poly.index<len(topfaces):poly.use_smooth=True
    # CDT intersections occasionally create sub-millimetre shoreline slivers.
    # Weld only those numerical remnants before export, retaining a sealed
    # manifold and leaving the separate exact road mesh untouched.
    cleanup=bmesh.new();cleanup.from_mesh(terrain.data)
    bmesh.ops.remove_doubles(cleanup,verts=list(cleanup.verts),dist=.0001)
    bmesh.ops.dissolve_degenerate(cleanup,edges=list(cleanup.edges),dist=.0001)
    cleanup.normal_update();cleanup.to_mesh(terrain.data);cleanup.free()
    terrain.data.update()
    if river:
        # Smooth vertex-weighted bank weathering replaces centroid-classified
        # triangle colours, which produced a visibly sawtoothed bank ribbon.
        weights=terrain.data.attributes.new('river_bank_mix','FLOAT','POINT')
        for vertex in terrain.data.vertices:
            d=river_distance(vertex.co.x,vertex.co.y)
            weights.data[vertex.index].value=1-_smooth((d-river_carve_half-.40)/max(river_bank*.78,.1))
        nodes=mats[0].node_tree.nodes;links=mats[0].node_tree.links
        bs=next(node for node in nodes if node.type=='BSDF_PRINCIPLED')
        existing=bs.inputs['Base Color'].links[0].from_socket
        attribute=nodes.new('ShaderNodeAttribute');attribute.attribute_name='river_bank_mix'
        mix=nodes.new('ShaderNodeMixRGB');mix.blend_type='MIX'
        slate=_rgba(default['rock']);mix.inputs[2].default_value=tuple(_lin(c)*.96 for c in slate[:3])+(1,)
        links.new(attribute.outputs['Fac'],mix.inputs[0]);links.new(existing,mix.inputs[1]);links.new(mix.outputs[0],bs.inputs['Base Color'])
        coord=next(node for node in nodes if node.type=='TEX_COORD')
        fine=nodes.new('ShaderNodeTexNoise');fine.inputs['Scale'].default_value=3.6;fine.inputs['Detail'].default_value=3.0
        links.new(coord.outputs['Object'],fine.inputs['Vector'])
        strength=nodes.new('ShaderNodeMath');strength.operation='MULTIPLY';strength.inputs[1].default_value=.28
        links.new(attribute.outputs['Fac'],strength.inputs[0])
        bump=nodes.new('ShaderNodeBump');bump.inputs['Distance'].default_value=.07
        if bs.inputs['Normal'].is_linked:links.new(bs.inputs['Normal'].links[0].from_socket,bump.inputs['Normal'])
        links.new(strength.outputs[0],bump.inputs['Strength']);links.new(fine.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    terrain['road_clearance_half_width']=edge
    terrain['top_vertex_count']=len(topverts)
    # Build the primary road as one solid closed strip. Face assignments form
    # subtle flagstones or bridge planks without one raised object or seam.
    roadverts=[]
    across=6
    for p,normal in zip(points,normals):
        for j in range(across+1):
            t=-half+road_width*j/across
            roadverts.append((p[0]+normal[0]*t,p[1]+normal[1]*t,0.0))
    upper=len(roadverts)
    roadverts.extend((x,y,-.3) for x,y,z in list(roadverts))
    roadfaces=[]
    roadmats=[]
    bridge_mask=[]
    for p in points:bridge_mask.append((river and river_distance(*p)<river_half+1.5)or any(ellipse(l,*p)<1.28 for l in bridges))
    for i in range(n):
        nxt=(i+1)%n
        for j in range(across):
            a=i*(across+1)+j
            b=nxt*(across+1)+j
            roadfaces.append((a,b,b+1,a+1))
            roadmats.append(2 if bridge_mask[i] else (1 if (i//3+j*7)%5==0 else 0))
            roadfaces.append((a+upper,a+1+upper,b+1+upper,b+upper))
            roadmats.append(2 if bridge_mask[i] else 0)
        for j in (0,across):
            a=i*(across+1)+j
            b=nxt*(across+1)+j
            roadfaces.append((a,a+upper,b+upper,b) if j==0 else (a,b,b+upper,a+upper))
            roadmats.append(2 if bridge_mask[i] else 0)
    # All bridge carpentry is fused into this same primary road object and
    # terminates strictly below the continuous upper deck.
    def support_box(cx,cy,cz,sx,sy,sz):
        base=len(roadverts)
        roadverts.extend((cx+dx*sx/2,cy+dy*sy/2,cz+dz*sz/2) for dx,dy,dz in ((-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)))
        for f in ((0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)):
            roadfaces.append(tuple(base+i for i in f))
            roadmats.append(2)
    for i in range(0,n,7):
        if not bridge_mask[i]:continue
        p,normal=points[i],normals[i]
        for sign in (-1,1):
            support_box(p[0]+sign*(half-.65)*normal[0],p[1]+sign*(half-.65)*normal[1],-1.64,.47,.47,2.64)
    road_obj=_mesh(prefix+'SINGLE_CLOSED_FLUSH_ROAD',roadverts,roadfaces,[roadmat,roadalt,woodmat],collection,'road')
    for poly,idx in zip(road_obj.data.polygons,roadmats):poly.material_index=idx
    # Per-loop unwrap preserves a continuous last-to-first span at the closed
    # lap seam while expressing both axes in actual world meters.
    arc=[0.]
    for i in range(1,n):arc.append(arc[-1]+math.dist(points[i-1],points[i]))
    lap=arc[-1]+math.dist(points[-1],points[0])
    uv=road_obj.data.uv_layers.new(name='RoadMeters')
    for poly in road_obj.data.polygons:
        rows=[(vid%upper)//(across+1) for vid in poly.vertices if vid<2*upper]
        seam=bool(rows and min(rows)==0 and max(rows)==n-1)
        for loopindex in poly.loop_indices:
            vid=road_obj.data.loops[loopindex].vertex_index
            if vid<2*upper:
                row,col=divmod(vid%upper,across+1)
                u=lap if seam and row==0 else arc[row]
                uv.data[loopindex].uv=(u,col*road_width/across)
            else:
                pos=road_obj.data.vertices[vid].co
                uv.data[loopindex].uv=(pos.x,pos.y)
    road_obj['top_z']=0.0
    road_obj['width']=road_width
    road_obj['closed']=True
    road_obj['uv_lap_length_meters']=lap
    geology=geology_detail.build_geology(outline,cap_outline,analytic_height,distance,default,
        _mat,collection,seed=seed,world_id=world_id,protected_half_width=edge,
        reserved_centers=[pad[0]for pad in building_pads]or None,base_mesh=terrain)
    objects=[terrain,road_obj]+geology['objects']
    # Opaque finite inset water surfaces. Roads crossing them remain the same
    # continuous z=0 solid mesh; all supporting piers terminate below its deck.
    for j,l in enumerate(watery):
        center=l['xy']
        waterverts=[(center[0],center[1],-.9)]
        angle=-math.radians(l.get('angle',0))
        ca,sa=math.cos(angle),math.sin(angle)
        sides=64
        for k in range(sides):
            a=k*TAU/sides
            r=.79+.035*math.sin(a*5+seed)+.025*math.cos(a*3)
            dx,dy=l['rx']*r*math.cos(a),l['ry']*r*math.sin(a)
            waterverts.append((center[0]+ca*dx-sa*dy,center[1]+sa*dx+ca*dy,-.9))
        waterfaces=[(0,k+1,(k+1)%sides+1) for k in range(sides)]
        water_upper=len(waterverts)
        waterverts.extend((x,y,z-.18)for x,y,z in list(waterverts))
        for k in range(sides):
            a,b=k+1,(k+1)%sides+1
            waterfaces.extend(((a,a+water_upper,b+water_upper,b),
                               (water_upper,b+water_upper,a+water_upper)))
        water=_mesh(prefix+f'INSET_WATER_{j:02d}',waterverts,waterfaces,[watermat],collection,'water')
        objects.append(water)
    intervals=[]
    for i,value in enumerate(bridge_mask):
        if value and not bridge_mask[(i-1)%n]:
            end=i
            while bridge_mask[end%n] and end<i+n:end+=1
            intervals.append((i,end%n))
    # BVH raycasting returns the actual triangulated surface, not just the
    # analytic sampling function, so large buildings can be grounded exactly.
    terrain.data.calc_loop_triangles()
    top_bvh=BVHTree.FromPolygons([v.co.copy()for v in terrain.data.vertices],
        [tuple(t.vertices)for t in terrain.data.loop_triangles],all_triangles=True)
    def height_at(x,y):
        hit,normal,index,dist=top_bvh.ray_cast(Vector((x,y,80)),Vector((0,0,-1)),200)
        return hit.z if hit is not None else analytic_height(x,y)
    river_result=dict(river,points=river_points,width=river_width,water_z=river_water,bed_z=river_bed,
        bank_width=river_bank,carved_bed_half_width=river_carve_half,bridge_intervals=intervals,water_geometry_created=False)if river else None
    return dict(objects=objects,route_points=[(x,y,0.) for x,y in points],
                road_left=[(p[0]+half*v[0],p[1]+half*v[1],0.) for p,v in zip(points,normals)],
                road_right=[(p[0]-half*v[0],p[1]-half*v[1],0.) for p,v in zip(points,normals)],
                road_width=road_width,shoulder=shoulder,road_z=0.,height_at=height_at,outline=outline,bridge_intervals=intervals,
                geology_bounds=geology['bounds'],geology_anchors=geology['anchors'],localized_ridges=ridges,
                inland_geology_centers=geology['interior_ridge_centers'],building_pad_count=len(building_pads),
                river_metadata=river_result,river_v4=river_result)

