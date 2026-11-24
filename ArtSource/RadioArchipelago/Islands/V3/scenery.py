"""Deterministic, exportable landscape meshes for Radio Archipelago V3.

Importing this module never creates scene content. All public builders return
the mesh objects they create. Locations are XYZ with Z up; scale is uniform.
Geometry is finite and opaque (including water) so every camera sees one model.
"""
import math
import random
import bpy
from mathutils import Vector

TAU = math.tau
_MAT_CACHE = {}


def _color(value, default):
    if value is None:
        return tuple(default[:3])
    if isinstance(value, str):
        value = value.lstrip('#')
        if len(value) in (6, 8):
            return tuple(int(value[i:i+2], 16) / 255.0 for i in (0, 2, 4))
        return tuple(default[:3])
    return tuple(float(c) for c in value[:3])


def _c(palette, keys, default):
    palette = palette or {}
    for key in keys.split('/'):
        if key in palette:
            return _color(palette[key], default)
    return tuple(default)


def _tint(c, factor):
    return tuple(min(1.0, max(0.0, x * factor)) for x in c[:3])


def _mat(label, color, roughness=0.72, metallic=0.0, emission=0.0):
    color = tuple(round(float(v), 5) for v in color[:3])
    key = (label, color, round(roughness, 3), round(metallic, 3), round(emission, 3))
    old = _MAT_CACHE.get(key)
    try:
        if old is not None and bpy.data.materials.get(old.name) == old:
            return old
    except ReferenceError:
        pass  # The caller may have cleared a previous island's material data.
    mat = bpy.data.materials.new('RA_' + label)
    mat.diffuse_color = (*color, 1.0)
    mat.roughness = roughness
    mat.metallic = metallic
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED')
    bsdf.inputs['Base Color'].default_value = (*color, 1.0)
    bsdf.inputs['Roughness'].default_value = roughness
    bsdf.inputs['Metallic'].default_value = metallic
    if emission:
        socket = bsdf.inputs.get('Emission Color') or bsdf.inputs.get('Emission')
        if socket is not None:
            socket.default_value = (*color, 1.0)
        strength = bsdf.inputs.get('Emission Strength')
        if strength is not None:
            strength.default_value = emission
    _MAT_CACHE[key] = mat
    return mat


def _collection(collection):
    if isinstance(collection, str):
        old = bpy.data.collections.get(collection)
        if old is None:
            old = bpy.data.collections.new(collection)
            bpy.context.scene.collection.children.link(old)
        return old
    return collection or bpy.context.collection or bpy.context.scene.collection


class _Mesh:
    def __init__(self):
        self.verts, self.faces, self.materials = [], [], []

    def face(self, indices, material=0):
        self.faces.append(tuple(indices))
        self.materials.append(material)

    def object(self, name, location, scale, mats, collection, smooth=False, bevel=0):
        if not self.verts:
            return None
        mesh = bpy.data.meshes.new(name + '_Mesh')
        mesh.from_pydata(self.verts, [], self.faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        _collection(collection).objects.link(obj)
        obj.location = location
        obj.scale = (scale, scale, scale) if isinstance(scale, (int, float)) else scale
        for m in mats:
            mesh.materials.append(m)
        for poly, index in zip(mesh.polygons, self.materials):
            poly.material_index = min(index, len(mats)-1)
            poly.use_smooth = smooth
        if bevel:
            mod = obj.modifiers.new('Soft hewn edges', 'BEVEL')
            mod.width, mod.segments = bevel, 2
        return obj


def _tube(mesh, points, radii, segments=9, material=0, phase=0, ridges=0.04):
    points = [Vector(p) for p in points]
    start = len(mesh.verts)
    for j, (p, r) in enumerate(zip(points, radii)):
        tangent = points[min(j+1, len(points)-1)] - points[max(0, j-1)]
        tangent.normalize()
        u = tangent.cross(Vector((0, 1, 0)))
        if u.length < 0.01:
            u = tangent.cross(Vector((1, 0, 0)))
        u.normalize()
        v = tangent.cross(u).normalized()
        for i in range(segments):
            a = phase + i * TAU/segments
            radius = r * (1 + ridges * math.sin(a * 3 + j*0.7))
            mesh.verts.append(tuple(p + (u*math.cos(a)+v*math.sin(a))*radius))
    mesh.face([start+i for i in reversed(range(segments))], material)
    for j in range(len(points)-1):
        for i in range(segments):
            ni = (i+1) % segments
            mesh.face((start+j*segments+i, start+j*segments+ni,
                       start+(j+1)*segments+ni, start+(j+1)*segments+i), material)
    mesh.face([start+(len(points)-1)*segments+i for i in range(segments)], material)


def _blob(mesh, center, radii, seed, material=0, segments=14, rings=9, lump=0.12):
    """One sculpted closed crown mass, with broad irregular lobes."""
    rng = random.Random(seed)
    phase = rng.uniform(0, TAU)
    center = Vector(center)
    start = len(mesh.verts)
    mesh.verts.append(tuple(center + Vector((0, 0, -radii[2]))))
    for j in range(1, rings):
        p = -math.pi/2 + math.pi*j/rings
        for i in range(segments):
            a = i*TAU/segments
            bulge = 1 + lump*(0.58*math.sin(3*a+phase)*math.cos(p)**2
                              + 0.33*math.cos(5*a-2*p+phase) + 0.24*math.sin(7*p+phase))
            mesh.verts.append(tuple(center + Vector((radii[0]*math.cos(p)*math.cos(a)*bulge,
                radii[1]*math.cos(p)*math.sin(a)*bulge, radii[2]*math.sin(p)*bulge))))
    top = len(mesh.verts)
    mesh.verts.append(tuple(center + Vector((0, 0, radii[2]))))
    for i in range(segments):
        mesh.face((start, start+1+(i+1)%segments, start+1+i), material)
    for j in range(rings-2):
        row = start+1+j*segments
        for i in range(segments):
            ni=(i+1)%segments
            mesh.face((row+i,row+ni,row+segments+ni,row+segments+i), material)
    row=start+1+(rings-2)*segments
    for i in range(segments):
        mesh.face((row+i,row+(i+1)%segments,top), material)


def _leaf(mesh, start, direction, length, width, seed=0, material=0, droop=0.4):
    """Thick palm/fern blade with scalloped sides and a raised central rib."""
    rng=random.Random(seed)
    side=Vector((-direction[1],direction[0],0)).normalized()
    direction=Vector(direction).normalized()
    origin=Vector(start)
    first=len(mesh.verts)
    count=10
    for j in range(count+1):
        t=j/count
        center=origin+direction*(length*t)+Vector((0,0,length*(0.33*math.sin(t*math.pi)-droop*t*t)))
        w=width*(math.sin(math.pi*t)**0.65 if 0<t<1 else 0.014)
        w*=1 if j%2 else 0.78
        for k in (-1,0,1):
            mesh.verts.append(tuple(center+side*(w*k)+Vector((0,0,0.07*length*math.sin(math.pi*t)*(1-abs(k))))))
    for j in range(count):
        for k in range(2):
            a=first+j*3+k
            mesh.face((a,a+3,a+4,a+1),material)
    # Export-friendly solid blade without transparency or thin alpha cards.
    bottom=len(mesh.verts)
    for p in mesh.verts[first:bottom]:
        mesh.verts.append((p[0],p[1],p[2]-0.025))
    for j in range(count):
        for k in range(2):
            a=first+j*3+k
            b=bottom+j*3+k
            mesh.face((b,b+1,b+4,b+3),material)
        for k in (0,2):
            a=first+j*3+k
            b=bottom+j*3+k
            mesh.face((a,b,b+3,a+3) if k==0 else (a,a+3,b+3,b),material)
    for j in (0,count):
        for k in range(2):
            a=first+j*3+k
            b=bottom+j*3+k
            mesh.face((a,a+1,b+1,b) if j==0 else (a,b,b+1,a+1),material)


def make_tree(location, scale=1, palette=None, seed=0, kind='broadleaf', collection=None):
    """Build a tree. broadleaf/autumn/mauve/apple/bonsai/pine/palm/shrub."""
    p=palette or {}
    rng=random.Random(seed)
    kind=kind.lower()
    defaults={'autumn':(0.76,0.19,0.035),'mauve':(0.43,0.21,0.49),
              'mauve_shrub':(0.43,0.21,0.49),
              'bonsai':(0.54,0.055,0.035),'pine':(0.025,0.19,0.11),
              'palm':(0.045,0.35,0.22),'winter':(0.27,0.45,0.52)}
    foliage=_c(p,'leaf/foliage/green',defaults.get(kind,(0.105,0.34,0.045)))
    bark=_c(p,'bark/wood/trunk',(0.19,0.075,0.031))
    leafmats=[_mat('Foliage',_tint(foliage,f)) for f in (0.64,0.86,1.08,1.32)]
    barkmats=[_mat('Bark',bark),_mat('Bark_light',_tint(bark,1.3))]
    trunk, crown = _Mesh(),_Mesh()
    if kind in ('palm','tropical'):
        height=4.7+rng.uniform(-0.25,0.4)
        bend=Vector((rng.uniform(-0.45,0.45),rng.uniform(-0.45,0.45),0))
        points=[tuple(bend*(t*t)+Vector((0,0,height*t))) for t in (0,.2,.4,.6,.8,1)]
        _tube(trunk,points,[.27,.24,.21,.18,.15,.12],11)
        for j in range(13):
            z=height*(j+0.7)/14
            t=z/height
            pos=bend*t*t+Vector((0,0,z))
            _tube(trunk,[pos-Vector((0,0,.035)),pos+Vector((0,0,.035))],[.25-.11*t,.25-.11*t],11,1)
        top=Vector(points[-1])
        for i in range(11):
            a=i*TAU/11+rng.uniform(-.12,.12)
            direction=(math.cos(a),math.sin(a),rng.uniform(-.01,.12))
            _leaf(crown,top,direction,rng.uniform(2.0,2.8),.37,seed+i,i%4,.36+rng.random()*.12)
        _blob(crown,top+Vector((0,0,.12)),(.28,.28,.4),seed,2,10,6)
        _blob(trunk,top-Vector((0,0,.22)),(.23,.21,.27),seed+66,1,10,6)
    elif kind in ('pine','fir','spruce','winter'):
        height=5.8+rng.uniform(-.35,.35)
        _tube(trunk,[(0,0,0),(.08,-.05,height*.55),(.06,0,height)],[.24,.14,.025],9)
        tiers=7
        for j in range(tiers):
            t=j/(tiers-1)
            z=.9+t*(height-1.4)
            radius=1.5*(1-t*.83)
            topz=min(height+.1,z+1.65-t*.55)
            n=18
            st=len(crown.verts)
            phase=j*.73
            for ring,(rr,zz) in enumerate(((.1,topz),(.55,z+.55),(.8,z+.22),(1,z),(.55,z-.12))):
                for i in range(n):
                    a=i*TAU/n+phase
                    scallop=1+.14*math.cos(a*9+phase)
                    crown.verts.append((math.cos(a)*radius*rr*scallop,
                                        math.sin(a)*radius*rr*scallop,
                                        zz+.09*math.sin(a*9+phase)))
            for ring in range(4):
                for i in range(n):
                    crown.face((st+ring*n+i,st+(ring+1)*n+i,
                                st+(ring+1)*n+(i+1)%n,st+ring*n+(i+1)%n), (j+i//3)%4)
            crown.face([st+i for i in range(n)],2)
            crown.face([st+4*n+i for i in reversed(range(n))],0)
            if j<4:
                for i in range(5):
                    a=i*TAU/5+phase
                    _tube(trunk,[(.03,0,z+.4),(math.cos(a)*radius*.88,math.sin(a)*radius*.88,z-.04)],[.075,.025],6)
    else:
        shrub=kind in ('shrub','mauve_shrub','bush')
        bonsai=kind in ('bonsai','red','redleaf')
        h=(1.0 if shrub else 3.0 if bonsai else 4.0)+rng.uniform(-.15,.25)
        sway=Vector((rng.uniform(-.25,.25),rng.uniform(-.25,.25),0))
        _tube(trunk,[(0,0,-.06),(.06,.02,h*.25),tuple(sway+Vector((0,0,h*.64))),tuple(sway*1.4+Vector((0,0,h*.91)))],
              [.29 if not shrub else .13,.22,.15,.055],10,0,seed*.2,.10)
        # Buttress roots grow out from the trunk instead of a floating cylinder.
        for i in range(5 if not shrub else 3):
            a=i*TAU/5+seed
            _tube(trunk,[(0,0,.4),(math.cos(a)*.5,math.sin(a)*.5,.04)],[.14,.035],7,0)
        clusters=7 if not shrub else 5
        for i in range(clusters):
            a=i*2.399963+seed*.13
            reach=(.7+rng.random()*.45)*(1.2 if bonsai else 1)
            center=Vector((math.cos(a)*reach,math.sin(a)*reach,h*(.75+rng.random()*.22)))+sway
            if i==0:
                center=Vector((sway.x,sway.y,h*1.08))
            if shrub:
                center*=.65
                center.z+=.28
            anchor=Vector((sway.x*.7,sway.y*.7,h*(.42+(i%3)*.12)))
            _tube(trunk,[anchor,(anchor+center)*.5+Vector((0,0,.1)),center],[.12,.085,.025],7,i%2)
            radii=(1.0+rng.random()*.2,.85+rng.random()*.23,.74+rng.random()*.18)
            if bonsai:
                radii=(radii[0]*1.18,radii[1]*1.1,radii[2]*.62)
            if shrub:
                radii=tuple(x*.63 for x in radii)
            _blob(crown,center,radii,seed*31+i,i%4,16,10,.15)
            # Attached bud masses make a leafy edge while retaining a cohesive crown.
            for j in range(3):
                aa=a+j*2.1
                pos=center+Vector((math.cos(aa)*radii[0]*.65,math.sin(aa)*radii[1]*.65,.15))
                _blob(crown,pos,(radii[0]*.45,radii[1]*.44,radii[2]*.58),seed*61+i*7+j,(i+j+1)%4,10,6,.13)
        if kind in ('apple','orchard'):
            fruit=_Mesh()
            for i in range(14):
                a=i*2.4
                r=1.0+rng.random()*.5
                _blob(fruit,(math.cos(a)*r,math.sin(a)*r,h*(.76+rng.random()*.3)),(.13,.13,.15),seed+i,0,8,5,.08)
            fruitobj=fruit.object('Apple_fruit',location,scale,[_mat('Apple_red',(0.65,.055,.018))],collection,True)
        else:
            fruitobj=None
    result=[trunk.object('Tree_'+kind+'_wood',location,scale,barkmats,collection,True),
            crown.object('Tree_'+kind+'_crown',location,scale,leafmats,collection,True)]
    if 'fruitobj' in locals() and fruitobj:
        result.append(fruitobj)
    return [o for o in result if o]


def make_rock(location, scale=1, palette=None, seed=0, kind='boulder', collection=None):
    """Build a faceted boulder, hewn slab, columnar basalt, or glacier crag."""
    rng=random.Random(seed)
    kind=kind.lower()
    default=(.17,.20,.23) if kind not in ('sandstone','coral') else (.54,.22,.13)
    if kind in ('ice','glacier','frost'):
        default=(.23,.55,.69)
    rock=_c(palette,'rock/stone/cliff',default)
    mats=[_mat('Rock_'+kind,_tint(rock,f),.6 if kind in ('ice','glacier','frost') else .82) for f in (.66,.83,1,1.16)]
    if kind in ('ice','glacier','frost'):
        mats[3]=_mat('Glacier_snow',_c(palette,'snow',(.76,.89,.94)),.66)
    mesh=_Mesh()
    if kind in ('basalt','columnar','columns'):
        for j in range(7):
            a=j*2.39996
            r=.0 if j==0 else .65
            x,y=math.cos(a)*r,math.sin(a)*r
            h=rng.uniform(.85,1.85)
            _tube(mesh,[(x,y,-.08),(x+.035,y-.04,h*.78),(x+.025,y-.025,h)],
                  [.35+rng.random()*.08,.33,.29],6,j%4,phase=rng.uniform(0,.4),ridges=.01)
    else:
        n=rng.choice((7,8,9))
        angle=rng.uniform(0,TAU)
        sx,sy=rng.uniform(.85,1.2),rng.uniform(.66,.95)
        height=rng.uniform(.95,1.4)
        if kind in ('ice','glacier','frost'):
            height*=1.5
        if kind in ('slab','slate'):
            height*=.55
            sx*=1.3
        radial=[rng.uniform(.83,1.15) for i in range(n)]
        rings=((.77,-.04),(1,.16),(.93,height*.52),(.61,height*.90),(.27,height))
        for j,(radius,z) in enumerate(rings):
            for i in range(n):
                a=i*TAU/n+angle
                wobble=rng.uniform(-.08,.08)
                mesh.verts.append((math.cos(a)*radius*radial[i]*sx+.10*j/4,
                                   math.sin(a)*radius*radial[i]*sy-.08*j/4,z+wobble))
        mesh.face(list(reversed(range(n))),0)
        for j in range(len(rings)-1):
            for i in range(n):
                ni=(i+1)%n
                if (i+j+seed)%3==0:
                    mesh.face((j*n+i,j*n+ni,(j+1)*n+ni),(i+j)%4)
                    mesh.face((j*n+i,(j+1)*n+ni,(j+1)*n+i),(i+j+1)%4)
                else:
                    mesh.face((j*n+i,j*n+ni,(j+1)*n+ni,(j+1)*n+i),(i+j)%4)
        mesh.face([(len(rings)-1)*n+i for i in range(n)],3)
    obj=mesh.object('Rock_'+kind,location,scale,mats,collection,False,.025)
    return [obj]


def make_crystal(location, scale=1, palette=None, seed=0, kind='amethyst', collection=None):
    """A single mesh of large six-sided prisms with pointed terminations."""
    rng=random.Random(seed)
    color=_c(palette,'crystal/accent',(0.43,.18,.72) if kind!='ice' else (.24,.7,.87))
    mats=[_mat('Crystal_'+kind,_tint(color,f),.28,.12,.17) for f in (.55,.78,1,1.35)]
    mesh=_Mesh()
    for j in range(8):
        a=j*2.39996+seed*.4
        r=0 if j==0 else rng.uniform(.3,.8)
        base=Vector((math.cos(a)*r,math.sin(a)*r,-.025))
        h=rng.uniform(.75,1.75)*(1.5 if j==0 else 1)
        radius=rng.uniform(.17,.31)*(1.3 if j==0 else 1)
        axis=Vector((math.cos(a)*r*.32,math.sin(a)*r*.32,1)).normalized()
        u=axis.cross(Vector((0,1,0))).normalized()
        v=axis.cross(u).normalized()
        st=len(mesh.verts)
        for z,rr in ((0,radius),(.72*h,radius*.88)):
            for i in range(6):
                aa=i*TAU/6+seed*.13
                mesh.verts.append(tuple(base+axis*z+(u*math.cos(aa)+v*math.sin(aa))*rr))
        tip=len(mesh.verts)
        mesh.verts.append(tuple(base+axis*h))
        mesh.face([st+i for i in reversed(range(6))],0)
        for i in range(6):
            ni=(i+1)%6
            mesh.face((st+i,st+ni,st+6+ni,st+6+i),(i+j)%4)
            mesh.face((st+6+i,st+6+ni,tip),(i+j+1)%4)
    return [mesh.object('Crystal_cluster_'+kind,location,scale,mats,collection,False,.008)]


def _disk(mesh, center, radius, depth, material=0, n=32, irregular=0, phase=0):
    st=len(mesh.verts)
    for z in (center[2]-depth,center[2]):
        for i in range(n):
            a=i*TAU/n
            r=radius*(1+irregular*(.6*math.sin(3*a+phase)+.4*math.sin(5*a-phase)))
            mesh.verts.append((center[0]+r*math.cos(a),center[1]+r*math.sin(a),z))
    mesh.face([st+i for i in reversed(range(n))],material)
    mesh.face([st+n+i for i in range(n)],material)
    for i in range(n):
        ni=(i+1)%n
        mesh.face((st+i,st+ni,st+n+ni,st+n+i),material)


def _ring(mesh, center, outer, inner, height, material=0, n=32):
    st=len(mesh.verts)
    for radius,z in ((outer,center[2]),(outer,center[2]+height),(inner,center[2]+height),(inner,center[2])):
        for i in range(n):
            a=i*TAU/n
            mesh.verts.append((center[0]+radius*math.cos(a),center[1]+radius*math.sin(a),z))
    for j in range(4):
        for i in range(n):
            ni=(i+1)%n
            mesh.face((st+j*n+i,st+j*n+ni,st+((j+1)%4)*n+ni,st+((j+1)%4)*n+i),material)


def make_prop(kind, location, scale=1, palette=None, seed=0, collection=None):
    """Build fern, flowers, sunflower, wheat, pumpkin, mushroom, pond,
    fountain, waterfall, basalt, glacier, crystal, or a shrub cluster.
    All props fit a roughly 2m footprint at scale 1 except waterfall (2m drop).
    """
    kind=kind.lower().replace('-','_')
    p=palette or {}
    rng=random.Random(seed)
    if kind in ('basalt','columnar','glacier','ice','frost','boulder','sandstone'):
        return make_rock(location,scale,p,seed,kind,collection)
    if kind in ('crystal','amethyst'):
        return make_crystal(location,scale,p,seed,'amethyst',collection)
    if kind in ('shrub','mauve','bush','pine','palm','autumn','apple','bonsai'):
        return make_tree(location,scale,p,seed,'mauve_shrub' if kind=='mauve' else kind,collection)
    green=_c(p,'leaf/foliage/green',(.10,.32,.055))
    accent=_c(p,'flower/accent',(.94,.53,.025))
    stone=_c(p,'rock/stone',(.36,.39,.42))
    water=_c(p,'water',(.035,.42,.52))
    mats=[_mat('Prop_leaf',green),_mat('Prop_leaf_light',_tint(green,1.3)),
          _mat('Prop_accent',accent),_mat('Prop_dark',(.17,.075,.022)),
          _mat('Prop_stone',stone),_mat('Prop_water',water,.2,.08),
          _mat('Prop_foam',(.64,.9,.88),.38),_mat('Prop_gold',(.52,.29,.075),.32,.6)]
    mesh=_Mesh()
    if kind in ('fern','ferns','grass','reeds'):
        for j in range(3 if kind=='ferns' else 1):
            base=Vector((rng.uniform(-.35,.35),rng.uniform(-.35,.35),.03))
            for i in range(9):
                a=i*TAU/9+j*.8
                _leaf(mesh,base,(math.cos(a),math.sin(a),.42),rng.uniform(.65,1.1),.18,seed+i,i%2,.6)
    elif kind in ('flowers','flower','flower_cluster','hibiscus'):
        for j in range(7):
            a=j*2.4
            x,y=math.cos(a)*.45,math.sin(a)*.45
            h=rng.uniform(.35,.7)
            _tube(mesh,[(x,y,0),(x+.03,y,h)],[.027,.019],5,0)
            for i in range(5):
                aa=i*TAU/5
                _blob(mesh,(x+math.cos(aa)*.11,y+math.sin(aa)*.11,h),(.11,.11,.045),seed+j*8+i,2,8,5,.03)
            _blob(mesh,(x,y,h+.025),(.065,.065,.05),seed+j,3,8,5,.03)
    elif kind in ('sunflower','sunflowers','sunflower_cluster'):
        for j in range(5):
            a=j*2.4
            x,y=math.cos(a)*.52,math.sin(a)*.52
            h=rng.uniform(.85,1.25)
            _tube(mesh,[(x,y,0),(x+.03,y,h)],[.045,.027],7,0)
            for sign in (-1,1):
                _leaf(mesh,(x,y,h*.45),(sign,.1,.45),.42,.16,seed+j,1,.4)
            # Flower faces upward and slightly south for readability in every view.
            normal=Vector((0,-.44,.9)).normalized()
            side=Vector((1,0,0)); up=normal.cross(side)
            center=Vector((x,y,h))
            for i in range(12):
                aa=i*TAU/12
                radial=side*math.cos(aa)+up*math.sin(aa)
                c=center+radial*.22
                _blob(mesh,c,(.10,.085,.06),seed+j*13+i,2,8,5,.03)
            _blob(mesh,center+normal*.035,(.15,.14,.085),seed+j,3,12,7,.04)
    elif kind in ('wheat','crop','wheat_cluster'):
        for j in range(18):
            x,y=rng.uniform(-.7,.7),rng.uniform(-.55,.55)
            h=rng.uniform(.55,.9)
            lean=Vector((rng.uniform(.03,.1),0,0))
            _tube(mesh,[(x,y,0),tuple(Vector((x,y,h))+lean)],[.018,.012],5,2)
            for i in range(5):
                z=h-.17+i*.065
                sign=-1 if i%2 else 1
                _blob(mesh,(x+lean.x+sign*.045,y,z),(.055,.032,.077),seed+j*5+i,2,7,5,.05)
    elif kind in ('pumpkin','pumpkins','pumpkin_patch'):
        for j in range(5):
            a=j*2.4
            x,y=math.cos(a)*.55,math.sin(a)*.55
            r=rng.uniform(.2,.32)
            _blob(mesh,(x,y,r*.75),(r,r,r*.78),seed+j,2,16,8,.12)
            _tube(mesh,[(x,y,r*1.45),(x+.06,y,r*1.8)],[.045,.027],6,3)
            _leaf(mesh,(x,y,.08),(math.cos(a+.7),math.sin(a+.7),.1),.48,.21,seed+j,0,.1)
    elif kind in ('mushroom','mushrooms','fungi'):
        # Broad domed caps with a solid pale under-rim and visible thick stems.
        for j in range(5):
            a=j*2.4
            x,y=math.cos(a)*.45,math.sin(a)*.45
            h=rng.uniform(.38,.7)
            _tube(mesh,[(x,y,0),(x+.025,y,h)],[.075,.05],9,6)
            _blob(mesh,(x,y,h),(.27,.25,.13),seed+j,5,14,8,.06)
            _disk(mesh,(x,y,h-.03),.235,.035,6,18)
    elif kind in ('pond','pool','mineral_pool','frozen_lake','ember_pool'):
        # Broad shallow basin: finite solid water and flattened shore pebbles.
        _disk(mesh,(0,0,.045),1.18,.13,4,48,.13,seed)
        _disk(mesh,(0,0,.075),1.02,.08,5,48,.13,seed)
        for j in range(11):
            a=j*TAU/11
            r=1.13*(1+.07*math.sin(a*3+seed))
            _blob(mesh,(math.cos(a)*r,math.sin(a)*r,.09),(.19,.16,.10),seed+j,4,9,6,.1)
        if kind=='frozen_lake':
            for j in range(3):
                a=j*1.5
                _tube(mesh,[(math.cos(a)*.1,math.sin(a)*.1,.09),(math.cos(a)*.76,math.sin(a)*.76,.09)],[.014,.009],4,6)
    elif kind in ('fountain','cloudwell','cloudwell_fountain'):
        _disk(mesh,(0,0,.06),1.2,.11,4,40)
        _ring(mesh,(0,0,.06),1.10,.90,.22,4,40)
        _disk(mesh,(0,0,.22),.91,.08,5,40)
        _tube(mesh,[(0,0,.18),(0,0,.32),(0,0,.63),(0,0,.79)],[.35,.32,.13,.19],20,4)
        _ring(mesh,(0,0,.72),.48,.40,.13,4,32)
        _disk(mesh,(0,0,.81),.40,.04,5,32)
        _tube(mesh,[(0,0,.81),(0,0,1.02),(0,0,1.15)],[.09,.065,.12],14,7)
        for i in range(4):
            a=i*TAU/4
            _tube(mesh,[(0,0,1.06),(.20*math.cos(a),.20*math.sin(a),1.14),(.38*math.cos(a),.38*math.sin(a),.85)],
                  [.035,.03,.025],7,5)
    elif kind in ('waterfall','waterfall_ribbon','cascade'):
        # Origin is the lower splash pool. The finite waterfall drops two units.
        _disk(mesh,(0,.15,.045),1.0,.10,5,36,.10,seed)
        st=len(mesh.verts)
        rows=13
        for j in range(rows+1):
            t=j/rows
            z=2.2*(1-t)
            y=.65-.85*t+.16*math.sin(t*math.pi)
            width=.68*(.88+.15*math.sin(t*math.pi))
            for side in (-1,0,1):
                mesh.verts.append((side*width,y+(.04 if side else -.02),z))
        for j in range(rows):
            for i in range(2):
                a=st+j*3+i
                mesh.face((a,a+3,a+4,a+1),5)
        # Thickness side and back make the ribbon readable from rear projections.
        back=len(mesh.verts)
        for v in mesh.verts[st:back]:
            mesh.verts.append((v[0],v[1]+.12,v[2]))
        for j in range(rows):
            for i in range(2):
                a=back+j*3+i
                mesh.face((a,a+1,a+4,a+3),5)
            for i in (0,2):
                a=st+j*3+i;b=back+j*3+i
                mesh.face((a,b,b+3,a+3) if i==0 else (a,a+3,b+3,b),6)
        for j in (0,rows):
            for i in range(2):
                a=st+j*3+i;b=back+j*3+i
                mesh.face((a,a+1,b+1,b) if j==0 else (a,b,b+1,a+1),5)
        # Narrow relief foam threads follow the surface; the full ribbon stays blue.
        for lane in (-.43,-.12,.25,.50):
            points=[]
            for j in range(rows+1):
                t=j/rows
                points.append((lane+.018*math.sin(t*9+lane),
                    .65-.85*t+.16*math.sin(t*math.pi)-.045,2.2*(1-t)))
            _tube(mesh,points,[.016 if lane<0 else .023]*(rows+1),5,6)
        for j in range(6):
            a=j*TAU/6
            _blob(mesh,(math.cos(a)*.45,math.sin(a)*.3-.18,.12),(.28,.17,.09),seed+j,6,10,6,.04)
    else:
        raise ValueError('Unknown Radio Archipelago prop kind: '+kind)
    return [mesh.object('Prop_'+kind,location,scale,mats,collection,True,
                        .018 if kind in ('fountain','cloudwell','cloudwell_fountain') else 0)]


# Conservative placement cylinders in LOCAL units: (horizontal radius, height).
# Every generated vertex is inside radius, with -0.18 <= z <= height. Multiply
# both bounds by uniform scale. With XYZ scale use max(abs(sx),abs(sy)) for the
# radius and abs(sz) for height; place with positive sz to preserve a base at Z0.
LANDMARK_BOUNDS = {
    'forest_moss_mound': (4.5, 4.5),
    'desert_fins': (4.5, 6.0),
    'ice_spires': (4.5, 7.0),
    'storm_columns': (4.5, 6.0),
    'volcanic_vent': (4.5, 6.0),
    'tropical_cistern': (4.5, 5.0),
    'cloud_orrery': (4.5, 7.5),
}


def _mass(mesh, center, radii, height, seed, material=0, top_material=None,
          angle=0, layers=6, strata=False):
    """Layered closed hewn rock, from broad embedded foot to irregular summit."""
    rng=random.Random(seed)
    n=9
    st=len(mesh.verts)
    radial=[rng.uniform(.86,1.12) for i in range(n)]
    for j in range(layers):
        t=j/(layers-1)
        radius=(.92+.10*math.sin(t*math.pi))*(1-.56*t**1.9)
        if strata:
            radius*=1+(.06 if j%2 else -.035)
        for i in range(n):
            a=i*TAU/n
            x=math.cos(a)*radii[0]*radius*radial[i]
            y=math.sin(a)*radii[1]*radius*radial[i]
            z=height*t+(rng.uniform(-.07,.07) if j else -.08)
            mesh.verts.append((center[0]+math.cos(angle)*x-math.sin(angle)*y+.15*t,
                               center[1]+math.sin(angle)*x+math.cos(angle)*y-.1*t,
                               center[2]+z))
    mesh.face([st+i for i in reversed(range(n))],material)
    for j in range(layers-1):
        for i in range(n):
            ni=(i+1)%n
            mesh.face((st+j*n+i,st+j*n+ni,st+(j+1)*n+ni,st+(j+1)*n+i),
                      material+(j%3 if strata else i%3))
    mesh.face([st+(layers-1)*n+i for i in range(n)],
              top_material if top_material is not None else material+2)


def _prism(mesh, base, axis, height, radius, seed, material=0, sides=6):
    axis=Vector(axis).normalized();base=Vector(base)
    u=axis.cross(Vector((0,1,0))).normalized();v=axis.cross(u).normalized()
    st=len(mesh.verts)
    for z,r in ((0,radius),(.75*height,radius*.88)):
        for i in range(sides):
            a=i*TAU/sides+seed*.17
            mesh.verts.append(tuple(base+axis*z+(u*math.cos(a)+v*math.sin(a))*r))
    tip=len(mesh.verts);mesh.verts.append(tuple(base+axis*height))
    mesh.face([st+i for i in reversed(range(sides))],material)
    for i in range(sides):
        ni=(i+1)%sides
        mesh.face((st+i,st+ni,st+sides+ni,st+sides+i),material+i%3)
        mesh.face((st+sides+i,st+sides+ni,tip),material+(i+1)%3)


def _orbit(mesh, center, radius, thickness, normal, material=0, segments=64, cross=9):
    """True closed torus in an arbitrary plane, not an emissive curve/card."""
    center=Vector(center);normal=Vector(normal).normalized()
    u=normal.cross(Vector((0,1,0)))
    if u.length<.01:
        u=normal.cross(Vector((1,0,0)))
    u.normalize();v=normal.cross(u).normalized()
    st=len(mesh.verts)
    for i in range(segments):
        a=i*TAU/segments
        radial=u*math.cos(a)+v*math.sin(a)
        for j in range(cross):
            b=j*TAU/cross
            mesh.verts.append(tuple(center+radial*(radius+thickness*math.cos(b))+
                                    normal*thickness*math.sin(b)))
    for i in range(segments):
        for j in range(cross):
            ni=(i+1)%segments;nj=(j+1)%cross
            mesh.face((st+i*cross+j,st+ni*cross+j,st+ni*cross+nj,st+i*cross+nj),material)


def _landmark_finish(mesh, kind, location, scale, mats, collection, smooth=False):
    radius,height=LANDMARK_BOUNDS[kind]
    # A final conservative fit guarantees bounds for every seed without depending
    # on the caller's choice of mesh modifiers or a scene evaluation update.
    actual_r=max(math.hypot(v[0],v[1]) for v in mesh.verts)
    actual_h=max(v[2] for v in mesh.verts)
    fit=min(1.,(radius-.05)/max(actual_r,.01),(height-.05)/max(actual_h,.01))
    mesh.verts=[(x*fit,y*fit,max(-.18,z*fit)) for x,y,z in mesh.verts]
    obj=mesh.object('Landmark_'+kind,location,scale,mats,collection,smooth)
    obj['ra_landmark_kind']=kind
    obj['ra_bounds_radius']=radius
    obj['ra_bounds_height']=height
    obj['ra_actual_radius']=max(math.hypot(v[0],v[1]) for v in mesh.verts)
    obj['ra_actual_height']=max(v[2] for v in mesh.verts)
    return [obj]


def make_landmark(kind, location, scale=1, palette=None, seed=0, collection=None):
    """High-impact landscape landmark, with no road or overhead crossing.

    Canonical kinds and guaranteed LOCAL placement cylinders (radius, height):
      forest_moss_mound (4.5,4.5), desert_fins (4.5,6), ice_spires (4.5,7),
      storm_columns (4.5,6), volcanic_vent (4.5,6), tropical_cistern (4.5,5),
      cloud_orrery (4.5,7.5). All start at Z >= -0.18; multiply by scale.
    Returns a list with one closed opaque mesh object. Disconnected components
    intentionally overlap as sculpted rock/foliage/metal assemblies. Opaque
    steam is modeled geometry: no particles, transparent cards or volumes.
    """
    aliases={'forest':'forest_moss_mound','moss_mound':'forest_moss_mound',
             'desert':'desert_fins','sandstone_fins':'desert_fins',
             'ice':'ice_spires','glacier':'ice_spires',
             'storm':'storm_columns','basalt_columns':'storm_columns',
             'volcano':'volcanic_vent','smoking_vent':'volcanic_vent',
             'coral':'tropical_cistern','tropical':'tropical_cistern',
             'cistern':'tropical_cistern','cascade':'tropical_cistern',
             'cloud':'cloud_orrery','orrery':'cloud_orrery'}
    kind=kind.lower().replace('-','_');kind=aliases.get(kind,kind)
    if kind not in LANDMARK_BOUNDS:
        raise ValueError('Unknown Radio Archipelago landmark: '+kind)
    rng=random.Random(seed);p=palette or {};mesh=_Mesh()
    fallback={'forest_moss_mound':(.16,.21,.22),'desert_fins':(.61,.24,.12),
              'ice_spires':(.17,.45,.64),'storm_columns':(.075,.095,.13),
              'volcanic_vent':(.085,.055,.06),'tropical_cistern':(.28,.19,.12),
              'cloud_orrery':(.58,.34,.46)}
    stone=_c(p,'rock/stone/cliff',fallback[kind])
    mats=[_mat('Landmark_'+kind,_tint(stone,f),.72) for f in (.66,.88,1.16)]
    if kind=='forest_moss_mound':
        green=_c(p,'leaf/foliage',(.12,.32,.045))
        mats += [_mat('Deep_moss',_tint(green,.63)),_mat('Moss_lit',green),
                 _mat('Fern_lit',_tint(green,1.42))]
        masses=[(-1.5,.25,2.3,1.45,2.1,.12),(.55,.75,2.0,1.65,3.45,-.2),
                (1.9,-.9,1.2,1.45,1.6,.4),(-1.25,-1.45,1.4,1.15,1.15,.6)]
        for j,(x,y,rx,ry,h,a) in enumerate(masses):
            _mass(mesh,(x,y,0),(rx,ry),h,seed+j,0,2,a,6)
            _blob(mesh,(x+.13,y-.08,h*.96),(rx*.52,ry*.52,.20),seed+j*7,3+j%2,18,8,.12)
        for j in range(12):
            a=j*2.39996
            r=2.3+rng.random()*.75
            base=(math.cos(a)*r,math.sin(a)*r,.12+rng.random()*.2)
            for i in range(5):
                aa=a+i*TAU/5
                _leaf(mesh,base,(math.cos(aa),math.sin(aa),.4),.55+rng.random()*.35,.14,seed+j*9+i,4+i%2,.45)
        # Long stepped slate seam tells a clearer silhouette than round boulders.
        _mass(mesh,(-2.5,.8,0),(.6,1.45),2.3,seed+70,0,2,-.4,7)
    elif kind=='desert_fins':
        mats += [_mat('Sandstone_light_band',_tint(stone,1.5)),
                 _mat('Sandstone_dark_band',_tint(stone,.59))]
        # Three narrow upright fins with long eroded profiles and real strata.
        for j,(x,y,h,rx,ry) in enumerate(((-1.7,.3,3.9,.62,2.1),
                (0,.45,5.65,.72,2.65),(1.65,-.2,3.1,.62,2.0))):
            _mass(mesh,(x,y,0),(rx,ry),h,seed+j,0,3,-.24,13,True)
            # Attached projecting ledges break the vertical outline at each band.
            for k in range(1,5):
                z=h*k/6
                _blob(mesh,(x+.025*k,y-.025*k,z),(rx*(1-.10*k),ry*(1-.08*k),.08),
                      seed+j*11+k,3 if k%2 else 4,18,5,.025)
        _mass(mesh,(-.5,-2.45,0),(1.5,.65),.8,seed+81,0,2,.3,5,True)
    elif kind=='ice_spires':
        snow=_c(p,'snow',(.77,.91,.98))
        mats += [_mat('Landmark_snow',snow,.68),_mat('Ice_cut_light',(.35,.72,.85),.25,.08)]
        _mass(mesh,(0,0,0),(3.4,2.35),1.05,seed,0,3,.3,5)
        for j in range(7):
            a=j*2.4
            r=.0 if j==0 else 1.15+rng.random()*.7
            h=6.25 if j==0 else rng.uniform(2.6,4.8)
            _prism(mesh,(math.cos(a)*r,math.sin(a)*r,.4),
                   (math.cos(a)*.16,math.sin(a)*.16,1),h,.58+rng.random()*.24,seed+j,0,5)
        for j in range(6):
            a=j*TAU/6
            _blob(mesh,(math.cos(a)*2.15,math.sin(a)*1.55,.60),(.85,.65,.24),seed+j,3,14,7,.07)
    elif kind=='storm_columns':
        mats += [_mat('Wet_basalt_caps',_tint(stone,1.42),.38),
                 _mat('Rust_signal_metal',(.39,.11,.045),.44,.45),
                 _mat('Storm_brass',(.4,.25,.08),.4,.65)]
        # Unequal tightly clustered hexagonal stacks make a broken coastal ridge.
        for j in range(15):
            a=j*2.4
            r=.0 if j==0 else .85+((j-1)//5)*.82
            x,y=math.cos(a)*r,math.sin(a)*r*.77
            h=4.8 if j==0 else rng.uniform(1.25,4.1)*(1-.08*((j-1)//5))
            _tube(mesh,[(x,y,-.08),(x+.025,y,h*.84),(x+.025,y,h)],
                  [.60,.57,.55],6,j%3,phase=.18,ridges=.015)
            _disk(mesh,(x+.025,y,h+.015),.53,.055,3,6)
        # A compact sea signal receiver makes the outcrop part of the radio world.
        _tube(mesh,[(.0,.0,4.77),(.0,.0,5.65)],[.15,.12],10,4)
        _tube(mesh,[(-.55,0,5.38),(.55,0,5.38)],[.105,.105],9,5)
        for x in (-.55,.55):
            _tube(mesh,[(x,0,5.38),(x,0,5.88)],[.105,.08],9,5)
    elif kind=='volcanic_vent':
        mats += [_mat('Ember_sealed_pool',_c(p,'ember/accent',(.85,.14,.013)),.44,0,.45),
                 _mat('Opaque_steam',(.61,.65,.65),.9),
                 _mat('Copper_vent_band',(.37,.16,.055),.4,.5)]
        n=18;st=len(mesh.verts)
        rings=((3.2,-.1),(3.0,.32),(2.45,1.35),(1.85,2.32),
               (1.35,2.23),(1.0,1.48))
        for j,(r,z) in enumerate(rings):
            for i in range(n):
                a=i*TAU/n
                rr=r*(1+.065*math.sin(5*a+seed))
                mesh.verts.append((rr*math.cos(a),rr*math.sin(a),z+.09*math.sin(3*a+seed)))
        mesh.face([st+i for i in reversed(range(n))],0)
        for j in range(len(rings)-1):
            for i in range(n):
                ni=(i+1)%n
                mesh.face((st+j*n+i,st+j*n+ni,st+(j+1)*n+ni,st+(j+1)*n+i),j%3)
        mesh.face([st+(len(rings)-1)*n+i for i in range(n)],3)
        # Sealed opaque ember surface stays inside the bowl, below the rim.
        _disk(mesh,(0,0,1.63),1.08,.10,3,28,.03,seed)
        for j in range(3):
            a=j*TAU/3+.2
            _mass(mesh,(math.cos(a)*2.8,math.sin(a)*2.8,-.04),(.6,.75),.85,seed+j,0,1,a,5)
        # Two finite sculpted steam ribbons, with no alpha, particles or volume.
        _tube(mesh,[(-.22,.14,1.65),(-.4,.13,2.35),(-.1,.12,3.0),
                    (.35,.2,3.55),(.45,.25,4.15),(.1,.3,4.8),(.3,.32,5.5),(.48,.33,5.8)],
              [.20,.30,.43,.50,.62,.56,.40,.10],16,4,phase=.15,ridges=.08)
        _tube(mesh,[(.55,-.1,1.65),(.75,-.1,2.15),(1.0,-.1,2.7),(.85,-.12,3.2),(1.15,-.1,3.7)],
              [.16,.24,.37,.32,.08],14,4,ridges=.08)
    elif kind=='tropical_cistern':
        mats += [_mat('Cistern_turquoise',_c(p,'water',(.035,.49,.56)),.23,.1),
                 _mat('Water_foam',(.67,.9,.83),.4),
                 _mat('Pump_brass',_c(p,'metal',(.48,.27,.075)),.35,.65),
                 _mat('Broad_tropical_leaf',_c(p,'leaf/foliage',(.045,.32,.15)),.72)]
        _mass(mesh,(0,.55,0),(2.8,2.15),1.0,seed,0,2,0,6)
        _disk(mesh,(0,-1.25,.12),2.1,.16,3,42,.1,seed)
        _ring(mesh,(0,.6,1.03),2.0,1.68,.52,0,40)
        _disk(mesh,(0,.6,1.42),1.69,.18,3,40)
        # Carved cistern has one broad, finite spillway descending to its pool.
        pts=[(-.8,-.74,1.39),(-.8,-1.05,1.34),(-.8,-1.33,.98),
             (-.8,-1.48,.55),(-.8,-1.70,.18)]
        for x in (-.52,-.26,0,.26,.52):
            _tube(mesh,[(x+q[0],q[1],q[2]) for q in pts],[.16]*len(pts),8,3)
        for x in (-1.25,-.55):
            _tube(mesh,[(x,-.75,1.49),(x,-1.05,1.42),(x,-1.40,.6),(x,-1.65,.20)],
                  [.025]*4,6,4)
        # Real thick pump pipe and flywheel identify the water mechanism.
        _tube(mesh,[(1.3,.5,1.48),(1.3,.5,2.65),(1.1,.5,2.95),(.40,.5,2.95),
                    (.20,.5,2.7),(.20,.5,2.3)],[.14]*6,12,5)
        _orbit(mesh,(1.3,.24,2.05),.48,.085,(0,1,0),5,32,8)
        for a in (0,TAU/3,2*TAU/3):
            _tube(mesh,[(1.3,.24,2.05),(1.3+.45*math.cos(a),.24,2.05+.45*math.sin(a))],
                  [.045,.045],7,5)
        for j in range(6):
            a=j*TAU/6
            base=(math.cos(a)*2.65,math.sin(a)*2.0+.6,.12)
            _leaf(mesh,base,(math.cos(a),math.sin(a),.55),.95,.25,seed+j,6,.35)
    elif kind=='cloud_orrery':
        mats += [_mat('Celestial_gold',_c(p,'metal/gold',(.63,.38,.10)),.28,.72),
                 _mat('Pearl_orbs',(.78,.65,.75),.30,.2),
                 _mat('Periwinkle_inlay',(.26,.31,.58),.4,.2)]
        _disk(mesh,(0,0,.06),2.4,.15,0,40)
        _disk(mesh,(0,0,.30),2.1,.24,2,40)
        _ring(mesh,(0,0,.30),1.72,1.56,.11,3,48)
        _tube(mesh,[(0,0,.31),(0,0,.7),(0,0,1.75),(0,0,2.3)],
              [.85,.76,.30,.44],18,0)
        center=(0,0,4.25)
        _tube(mesh,[(0,0,2.0),(0,0,4.25)],[.13,.1],12,3)
        _orbit(mesh,center,2.66,.14,(0,.92,.38),3,64,10)
        _orbit(mesh,center,2.15,.115,(.79,.25,.56),3,64,9)
        _orbit(mesh,center,1.55,.10,(.2,.3,1),5,56,8)
        _blob(mesh,center,(.58,.58,.58),seed,4,24,14,.015)
        for i,(x,y,z,r) in enumerate(((2.5,0,4.8,.29),(-1.5,.8,5.1,.37),
                                      (.7,-1.2,4.6,.25),(-.2,.6,2.25,.22))):
            _blob(mesh,(x,y,z),(r,r,r),seed+i,4,16,10,.02)
        # Low forked receivers frame the base without an overhead road opening.
        for x in (-1.5,1.5):
            _tube(mesh,[(x,0,.30),(x,0,1.4)],[.10,.08],10,3)
            _blob(mesh,(x,0,1.5),(.16,.16,.18),seed,4,10,7,.02)
    return _landmark_finish(mesh,kind,location,scale,mats,collection,
                            kind in ('tropical_cistern','cloud_orrery'))


__all__=['make_tree','make_rock','make_crystal','make_prop','make_landmark','LANDMARK_BOUNDS']
