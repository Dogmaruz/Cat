"""Detailed opaque plant and fractured-stone geometry for the V4 art scenes.

Installed into scenery.py after its common mesh utilities are defined. Every
leaf is a closed, convex six-vertex volume, never an alpha card or a sphere.
Builders use a private seed and merge components into a handful of mesh objects.
"""
import math
import random
from mathutils import Vector
from mathutils.bvhtree import BVHTree

TAU = math.tau


def _leaflet(mesh, base, direction, length, width, material, roll=0, bend=.035, rounded=False):
    """A closed lenticular leaf with pointed ends and a raised midrib."""
    base, axis = Vector(base), Vector(direction).normalized()
    side = axis.cross(Vector((0,0,1)))
    if side.length < .01:
        side = axis.cross(Vector((1,0,0)))
    side.normalize()
    normal = side.cross(axis).normalized()
    side, normal = (side*math.cos(roll)+normal*math.sin(roll),
                    normal*math.cos(roll)-side*math.sin(roll))
    mid = base+axis*(length*.48)+normal*bend
    tip = base+axis*length+normal*bend*.3
    st = len(mesh.verts)
    if rounded:
        # Eight-point ovate outline gives broad leaves soft curved silhouettes;
        # central upper/lower poles retain an actual closed lenticular volume.
        contour=((0,0),(.20,-.68),(.48,-1),(.79,-.64),(1,0),
                 (.79,.64),(.48,1),(.20,.68))
        for t,w in contour:
            mesh.verts.append(tuple(base+axis*(length*t)+side*(width*w)
                                    +normal*(bend*math.sin(t*math.pi))))
        mesh.verts.extend((tuple(mid+normal*max(.008,width*.23)),
                           tuple(mid-normal*max(.006,width*.07))))
        for i in range(8):
            n=(i+1)%8
            mesh.face((st+n,st+i,st+8),material)
            mesh.face((st+i,st+n,st+9),material)
        return
    mesh.verts.extend(map(tuple,(base,tip,mid-side*width,mid+side*width,
                                mid+normal*max(.008,width*.23),
                                mid-normal*max(.006,width*.07))))
    for face in ((0,2,4),(2,1,4),(1,3,4),(3,0,4),
                 (2,0,5),(1,2,5),(3,1,5),(0,3,5)):
        mesh.face(tuple(st+i for i in reversed(face)),material)


def _bound(mesh, radius, height=None, bottom=-.16):
    """Hard safety envelope, independent of seed or palette."""
    for i,(x,y,z) in enumerate(mesh.verts):
        r=math.hypot(x,y)
        if r>radius:
            x,y=x*radius/r,y*radius/r
        mesh.verts[i]=(x,y,max(bottom,min(height,z)) if height is not None else max(bottom,z))


def _mix(a,b,t):
    return tuple(a[i]*(1-t)+b[i]*t for i in range(3))


def _foliage_materials(p,kind):
    defaults={'autumn':(.72,.29,.045),'mauve':(.57,.28,.65),
              'bonsai':(.57,.10,.075),'pine':(.13,.35,.22),
              'palm':(.16,.48,.27),'winter':(.45,.65,.65)}
    base=_c(p,'leaf/foliage/green',defaults.get(kind,(.23,.43,.14)))
    light=_c(p,'leaf_light',_mix(base,(.72,.79,.24),.35))
    if kind in ('pine','winter'):
        base=_mix(base,(.075,.255,.19),.38)
        light=_mix(light,_mix(base,(.28,.48,.32),.35),.60)
    # Warm/dark and sunlit tips are spatially grouped, not pure random noise.
    colors=[_tint(base,.58),_tint(base,.76),base,
            _mix(base,light,.42),light,_tint(light,1.13)]
    return [_mat('Foliage_'+kind,c,.55+i*.025) for i,c in enumerate(colors)]


def make_tree(location,scale=1,palette=None,seed=0,kind='broadleaf',collection=None):
    p=palette or {};rng=random.Random(seed);kind=kind.lower()
    if kind in ('fir','spruce'):kind='pine'
    if kind in ('tropical',):kind='palm'
    shrub=kind in ('shrub','mauve_shrub','bush')
    if kind=='mauve_shrub':kind='mauve';shrub=True
    bark=_c(p,'bark/wood/trunk',(.24,.12,.055))
    barkmats=[_mat('Bark',bark,.92),_mat('Bark_ridge',_tint(bark,1.38),.88),
              _mat('Bark_crevice',_tint(bark,.56),.97)]
    leafmats=_foliage_materials(p,kind)
    wood,foliage=_Mesh(),_Mesh();soft_bough_faces=[]
    if kind in ('pine','winter'):
        h=5.7+rng.uniform(-.2,.35)
        _tube(wood,[(0,0,-.06),(.04,.01,h*.48),(.05,-.03,h)],
              [.22,.13,.022],10,0,seed*.1,.11)
        for tier in range(9):
            t=tier/9;z=.72+t*(h-.85);reach=1.72*(1-t*.88)
            arms=7 if tier<5 else 6
            for arm in range(arms):
                a=TAU*arm/arms+tier*2.39996+rng.uniform(-.14,.14)
                direction=Vector((math.cos(a),math.sin(a),0))
                base=Vector((.03,0,z+.22))
                end=base+direction*reach+Vector((0,0,-.19+.18*t))
                _tube(wood,[base,(base+end)*.5+Vector((0,0,.08)),end],
                      [.065*(1-t*.7),.034,.009],6,(tier+arm)%2)
                # Substantial sculpted bough cushions fill the inner silhouette.
                # Individual needles remain exposed at each irregular pad edge.
                # These are separate elongated, overlapping branch volumes,
                # rather than a solid cone or another ring of sphere canopies.
                for lobe,u in enumerate((.36,.61,.80)):
                    center=base.lerp(end,u)+Vector((0,0,-.025+rng.uniform(-.025,.035)))
                    length=reach*(.31 if lobe<2 else .23)
                    width=max(.065,reach*(.20 if lobe<2 else .13))
                    depth=.12+.09*(1-t)
                    first=len(foliage.verts)
                    first_face=len(foliage.faces)
                    _blob(foliage,(0,0,0),(length,width,depth),seed*997+tier*47+arm*3+lobe,
                          2+(tier+arm+lobe)%3,12,7,.20)
                    soft_bough_faces.append((first_face,len(foliage.faces)))
                    ca,sa=math.cos(a),math.sin(a)
                    for vertex in range(first,len(foliage.verts)):
                        x,y,zv=foliage.verts[vertex]
                        foliage.verts[vertex]=(center.x+ca*x-sa*y,center.y+sa*x+ca*y,center.z+zv)
                # Five side twigs per branch; needle sprays form irregular pads.
                side=Vector((-direction.y,direction.x,0))
                for spray in range(5):
                    u=.24+spray*.16;pos=base.lerp(end,u)
                    for sign in (-1,1):
                        v=(direction*.45+side*sign*.85+Vector((0,0,.15))).normalized()
                        twiglen=reach*(.25*(1-u)+.08)
                        tip=pos+v*twiglen
                        _tube(wood,[pos,tip],[.018,.004],5,1)
                        for needle in range(4):
                            f=.12+needle*.21;root=pos.lerp(tip,f)
                            for wing in (-1,1):
                                d=(v*.45+direction*wing*.7+Vector((0,0,.27+rng.random()*.32))).normalized()
                                _leaflet(foliage,root,d,rng.uniform(.16,.29),.032,
                                         1+(spray+tier+needle)%5,roll=rng.uniform(-.3,.3),bend=.012)
                for k in range(5):
                    root=end-direction*(k*.055)
                    _leaflet(foliage,root,(*direction[:2],.8),.24,.033,3+k%3,bend=.01)
        radius,height=2.15,6.25
    elif kind=='palm':
        h=4.6+rng.uniform(-.22,.25);bend=Vector((rng.uniform(-.25,.25),rng.uniform(-.25,.25),0))
        trunk=[bend*t*t+Vector((0,0,h*t)) for t in (0,.16,.32,.5,.7,.85,1)]
        _tube(wood,trunk,[.25,.235,.21,.19,.17,.14,.105],12,0,ridges=.07)
        for j in range(20):
            t=(j+.6)/21;pos=bend*t*t+Vector((0,0,h*t))
            _tube(wood,[pos-Vector((0,0,.018)),pos+Vector((0,0,.025))],
                  [.25-.14*t,.252-.14*t],12,1)
        top=trunk[-1]
        for frond in range(12):
            a=frond*TAU/12+rng.uniform(-.08,.08);direction=Vector((math.cos(a),math.sin(a),0));side=Vector((-direction.y,direction.x,0))
            length=rng.uniform(2.0,2.55)
            def point(t):return top+direction*(length*t)+Vector((0,0,.65*math.sin(t*math.pi)-.78*t*t))
            spine=[point(i/9) for i in range(10)]
            _tube(wood,spine,[.045*(1-i/10)+.006 for i in range(10)],6,1)
            for j in range(16):
                t=.10+j*.053;root=point(t)
                leaflen=(.18+.55*math.sin(t*math.pi))*(1-.12*rng.random())
                for sign in (-1,1):
                    d=(direction*.5+side*sign*.9+Vector((0,0,-.24-.2*t))).normalized()
                    _leaflet(foliage,root,d,leaflen,.055,2+(j+frond)%4,
                             roll=sign*.25,bend=.06)
        for j in range(5):
            a=j*TAU/5
            _blob(wood,top+Vector((math.cos(a)*.17,math.sin(a)*.17,-.19)),(.16,.15,.21),seed+j,2,10,6,.04)
        radius,height=2.95,5.8
    else:
        bonsai=kind in ('bonsai','red','redleaf')
        h=(1.0 if shrub else 3.4 if bonsai else 4.25)+rng.uniform(-.1,.16)
        sway=Vector((rng.uniform(-.16,.16),rng.uniform(-.16,.16),0))
        trunk=[(0,0,-.07),(.045,.02,h*.26),tuple(sway+Vector((0,0,h*.64))),tuple(sway*1.3+Vector((0,0,h*.98)))]
        _tube(wood,trunk,[.11 if shrub else .29,.09 if shrub else .23,.06 if shrub else .15,.028],11,0,seed*.2,.14)
        for i in range(6 if not shrub else 3):
            a=i*2.39996+seed*.01
            _tube(wood,[(0,0,.3),(.30*math.cos(a),.30*math.sin(a),.035),(.51*math.cos(a),.51*math.sin(a),-.035)],
                  [.12 if not shrub else .06,.07,.018],6,0)
        clusters=10 if not shrub else 7
        for i in range(clusters):
            a=i*2.39996+seed*.13
            reach=(.66+rng.random()*.48)*(.68 if shrub else 1)
            center=Vector((math.cos(a)*reach,math.sin(a)*reach,h*(.73+rng.random()*.24)))+sway
            if i==0:center=Vector((sway.x,sway.y,h*1.04))
            if i in (8,9) and not shrub:
                center.z=h*(.52+.12*(i-8))
            if shrub:center.z=h*(.55+rng.random()*.4)
            anchor=Vector((sway.x*.6,sway.y*.6,h*(.40+(i%3)*.11)))
            _tube(wood,[anchor,anchor.lerp(center,.56)+Vector((0,0,.12)),center],
                  [.07 if shrub else .105,.045,.012],7,i%2)
            sprays=7 if not shrub else 6
            for j in range(sprays):
                aa=j*2.39996+a;rad=rng.uniform(.22,.58)*(.65 if shrub else 1)
                zspread=rng.uniform(-.31,.30)*(.65 if shrub else .68 if bonsai else 1)
                tip=center+Vector((math.cos(aa)*rad,math.sin(aa)*rad,zspread))
                _tube(wood,[center,center.lerp(tip,.55)+Vector((0,0,.055)),tip],
                      [.028 if not shrub else .018,.016,.004],5,j%2)
                # Small thick leaves on both sides of real secondary shoots.
                for k in range(18 if not shrub else 13):
                    theta=k*2.39996+aa
                    r=math.sqrt(rng.random())*(.40 if not shrub else .25)
                    pos=tip+Vector((math.cos(theta)*r,math.sin(theta)*r,rng.uniform(-.22,.23)*(.6 if shrub else 1)))
                    d=Vector((math.cos(theta),math.sin(theta),rng.uniform(-.18,.7)))
                    length=rng.uniform(.18,.31)*(.72 if shrub else 1)
                    mat=min(5,max(0,(i+j)%4+(1 if pos.z>h*.9 else 0)))
                    _leaflet(foliage,pos,d,length,length*rng.uniform(.30,.42),mat,
                             roll=rng.uniform(-.8,.8),bend=.025,rounded=True)
        radius,height=(1.4,1.55) if shrub else (2.15,5.35)
    _bound(wood,radius,height);_bound(foliage,radius,height)
    result=[wood.object('V4_Tree_'+kind+'_branchwork',location,scale,barkmats,collection,True),
            foliage.object('V4_Tree_'+kind+'_individual_leaves',location,scale,leafmats,collection,False)]
    for first,last in soft_bough_faces:
        for face in result[1].data.polygons[first:last]:face.use_smooth=True
    if kind in ('apple','orchard'):
        fruit=_Mesh()
        for j in range(18):
            a=j*2.39996;r=rng.uniform(.8,1.55);z=rng.uniform(3.4,4.5)
            _blob(fruit,(math.cos(a)*r,math.sin(a)*r,z),(.095,.095,.11),seed+j,0,10,6,.03)
        _bound(fruit,2.15,5.35)
        result.append(fruit.object('V4_Orchard_apples',location,scale,[_mat('Apple_red',(.72,.12,.035),.42)],collection,True))
    return [obj for obj in result if obj]


def _stone_mass(mesh,center,radii,h,rng,mat):
    sides=rng.choice((6,7,8));st=len(mesh.verts);angle=rng.uniform(0,TAU)
    radial=[rng.uniform(.82,1.15) for _ in range(sides)]
    for row,(factor,z) in enumerate(((.86,-.06),(1,.15),(.93,h*.5),(.66,h*.91),(.37,h))):
        for i in range(sides):
            a=angle+i*TAU/sides
            mesh.verts.append((center[0]+math.cos(a)*radii[0]*factor*radial[i]+row*.025,
                               center[1]+math.sin(a)*radii[1]*factor*radial[i]-row*.018,
                               center[2]+z+rng.uniform(-.045,.045)))
    mesh.face([st+i for i in reversed(range(sides))],mat)
    for row in range(4):
        for i in range(sides):
            n=(i+1)%sides
            mesh.face((st+row*sides+i,st+row*sides+n,st+(row+1)*sides+n,st+(row+1)*sides+i),mat+(i+row)%3)
    mesh.face([st+4*sides+i for i in range(sides)],mat+3)


def make_rock(location,scale=1,palette=None,seed=0,kind='boulder',collection=None):
    p=palette or {};rng=random.Random(seed);kind=kind.lower()
    ice=kind in ('ice','glacier','frost');desert=kind in ('sandstone','coral')
    basalt=kind in ('basalt','columnar','columns');slate=kind in ('slate','slab')
    color=_c(p,'rock/stone/cliff',(.29,.35,.39) if not desert else (.65,.32,.20))
    if ice:color=_c(p,'rock',(.32,.64,.77))
    mats=[_mat('Fractured_'+kind,_tint(color,f),.41 if ice else .86) for f in (.61,.79,.98,1.18)]
    core=_Mesh()
    if basalt:
        for j in range(11):
            a=j*2.39996;r=0 if j==0 else rng.uniform(.42,1.08)
            x,y=math.cos(a)*r,math.sin(a)*r;h=rng.uniform(.7,2.12)
            _tube(core,[(x,y,-.08),(x,y,h*.40),(x+.018,y-.015,h*.76),(x+.015,y-.01,h)],
                  [.24,.245,.235,.22],6,j%4,phase=j*.3,ridges=.015)
    else:
        _stone_mass(core,(0,0,0),(1.0,.8),1.6 if ice else .72 if slate else 1.25,rng,0)
        for j in range(6):
            a=j*2.39996+seed;r=rng.uniform(.54,1.08)
            x,y=math.cos(a)*r,math.sin(a)*r
            _stone_mass(core,(x,y,-.01),(rng.uniform(.32,.61),rng.uniform(.27,.48)),
                        rng.uniform(.45,.95)*(1.5 if ice else .65 if slate else 1),rng,0)
    _bound(core,1.91,2.35)
    stone=core.object('V4_Rock_'+kind+'_fracture_planes',location,scale,mats,collection,False)
    result=[stone]
    # Real surface-conforming fragments and micro-foliage, not color-only noise.
    surface=BVHTree.FromPolygons([Vector(p) for p in core.verts],core.faces,all_triangles=False)
    detail=_Mesh();veins=_Mesh()
    biome=str(p.get('style','forest'))
    moss=not ice and not desert and biome not in ('storm','volcanic')
    mosscolor=_c(p,'leaf_light',(.36,.48,.15))
    detailmats=[_mat('Moss_deep',_tint(mosscolor,.7),.96),_mat('Moss_leaf',mosscolor,.91),
                _mat('Lichen',(.61,.66,.43) if moss else (.75,.60,.40),.96),
                _mat('Snow',(.9,.95,.98),.83),_mat('Vein',_mix(color,(.82,.84,.79),.55),.82)]
    for j in range(58 if moss else 32):
        x,y=rng.uniform(-1.65,1.65),rng.uniform(-1.5,1.5)
        hit,normal,_,_=surface.ray_cast(Vector((x,y,5)),Vector((0,0,-1)),10)
        if hit is None or normal.z<.32:continue
        if ice:
            for k in range(4):
                a=k*2.39996;d=Vector((math.cos(a),math.sin(a),.05))
                _leaflet(detail,hit+normal*.014,d,.19,.095,3,roll=0,bend=.006)
        elif moss:
            for k in range(7):
                a=k*2.39996+j
                pos=hit+normal*.012+Vector((rng.uniform(-.05,.05),rng.uniform(-.05,.05),.012))
                _leaflet(detail,pos,(math.cos(a),math.sin(a),rng.uniform(.0,.3)),rng.uniform(.075,.15),.035,(j+k)%2,bend=.014)
        else:
            a=rng.uniform(0,TAU)
            _leaflet(detail,hit+normal*.009,(math.cos(a),math.sin(a),.03),rng.uniform(.05,.11),.028,2,bend=.002)
        if j%7==0:
            # Tiny pale mineral splinters lie on exposed upper fracture facets.
            _leaflet(veins,hit+normal*.018,(normal.y+.3,-normal.x+.2,.02),.20,.014,4,bend=.003)
    for mesh,name in ((detail,'surface_moss_lichen_snow'),(veins,'mineral_splinters')):
        if mesh.verts:
            _bound(mesh,1.95,2.42)
            result.append(mesh.object('V4_Rock_'+kind+'_'+name,location,scale,detailmats,collection,False))
    return result


def _fern(mesh,center,rng,material=0,scale=1):
    base=Vector(center)
    for frond in range(8):
        a=frond*2.39996+rng.uniform(-.15,.15);direction=Vector((math.cos(a),math.sin(a),0));side=Vector((-direction.y,direction.x,0))
        length=rng.uniform(.52,.80)*scale
        def point(t):return base+direction*(length*t)+Vector((0,0,(.38*math.sin(t*math.pi*.85)+.05)*scale))
        spine=[point(i/6) for i in range(7)]
        _tube(mesh,spine,[.012*(1-i/8)*scale for i in range(7)],5,material)
        for j in range(7):
            t=.14+j*.11;root=point(t);leaflen=(.05+.13*math.sin(t*math.pi))*scale
            for sign in (-1,1):
                axis=(direction*.5+side*sign*.9+Vector((0,0,.08))).normalized()
                _leaflet(mesh,root,axis,leaflen,leaflen*.22,material+(j%2),roll=sign*.1,bend=.01)


def _groundcover(kind,location,scale,p,seed,collection):
    rng=random.Random(seed);mesh=_Mesh()
    leaf=_c(p,'leaf/foliage/green',(.28,.48,.11));light=_c(p,'leaf_light',_mix(leaf,(.68,.72,.2),.35))
    accent=_c(p,'flower/accent',(.98,.73,.22))
    mats=[_mat('Ground_leaf',leaf,.79),_mat('Ground_leaf_light',light,.70),
          _mat('Ground_petal',accent,.61),_mat('Ground_petal_pale',_mix(accent,(.99,.99,.88),.75),.66),
          _mat('Ground_flower_heart',(.49,.29,.06),.82),_mat('Leaf_litter',(.45,.24,.075),.94)]
    if kind in ('fern','ferns'):
        for j in range(3 if kind=='ferns' else 1):
            _fern(mesh,(rng.uniform(-.35,.35),rng.uniform(-.35,.35),0),rng,0,rng.uniform(.75,1))
    else:
        # Loose irregular patch of separately rooted narrow blades, 168 in total.
        for tuft in range(14):
            a=tuft*2.39996;r=math.sqrt(rng.random())*.95;base=Vector((math.cos(a)*r,math.sin(a)*r,.008))
            for blade in range(12):
                aa=a+blade*2.39996;h=rng.uniform(.18,.51)
                d=Vector((math.cos(aa)*rng.uniform(.18,.55),math.sin(aa)*rng.uniform(.18,.55),1)).normalized()
                _leaflet(mesh,base+Vector((rng.uniform(-.045,.045),rng.uniform(-.045,.045),0)),d,h,rng.uniform(.012,.025),blade%2,
                         roll=rng.uniform(-.35,.35),bend=.025)
        if kind not in ('grass','grass_clump','reeds','leaf_litter','moss'):
            for j in range(3 if kind in ('groundcover','forest_floor') else 9):
                a=j*2.39996;r=rng.uniform(.2,.9);x,y=math.cos(a)*r,math.sin(a)*r;h=rng.uniform(.26,.58)
                _tube(mesh,[(x,y,0),(x+.035,y,h)],[.012,.008],5,0)
                base=Vector((x+.035,y,h))
                for petal in range(6):
                    a=petal*TAU/6
                    _leaflet(mesh,base,(math.cos(a),math.sin(a),.12),.105,.045,2+j%2,bend=.017)
                _blob(mesh,base+Vector((0,0,.02)),(.029,.029,.018),seed+j,4,8,5,.02)
        if kind in ('groundcover','forest_floor','moss'):
            _fern(mesh,(rng.uniform(-.4,.4),rng.uniform(-.4,.4),0),rng,0,.65)
        if kind in ('groundcover','forest_floor','leaf_litter'):
            for j in range(18):
                a=j*2.39996;r=rng.uniform(.3,1.18)
                _leaflet(mesh,(math.cos(a)*r,math.sin(a)*r,.014),(math.cos(a+.7),math.sin(a+.7),.04),.13,.045,5,bend=.005)
    _bound(mesh,1.4,.8,bottom=0)
    return [mesh.object('V4_Groundcover_'+kind,location,scale,mats,collection,False)]


def install(namespace):
    global _Mesh,_tube,_blob,_mat,_c,_tint,_legacy_prop
    _Mesh=namespace['_Mesh'];_tube=namespace['_tube'];_blob=namespace['_blob']
    _mat=namespace['_mat'];_c=namespace['_c'];_tint=namespace['_tint']
    _legacy_prop=namespace['make_prop']
    def prop(kind,location,scale=1,palette=None,seed=0,collection=None):
        normalized=kind.lower().replace('-','_')
        if normalized in ('fern','ferns','grass','grass_clump','reeds','groundcover','forest_floor','moss',
                          'leaf_litter','wildflowers','flowers','flower','flower_cluster','hibiscus'):
            return _groundcover(normalized,location,scale,palette or {},seed,collection)
        return _legacy_prop(kind,location,scale,palette,seed,collection)
    namespace['make_tree']=make_tree
    namespace['make_rock']=make_rock
    namespace['make_prop']=prop
    namespace['NATURE_DETAIL_BOUNDS']={'tree_radius':2.15,'palm_radius':2.95,'rock_radius':1.95,
                                      'groundcover_radius':1.4,'groundcover_height':.8}
