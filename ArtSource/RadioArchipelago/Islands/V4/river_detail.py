"""A finite, modeled forest brook with clear-road slate and moss banks.

plan_river is pure planning (no Blender objects); build_river creates actual
opaque water, foam and geology meshes. All positions are Blender world XY/Z.
The terrain builder owns channel excavation and the flush primary road deck.
"""
import math
import random
import bpy
from mathutils import Vector

TAU=math.tau


def _rgb(value):
    if isinstance(value,str):
        s=value.lstrip('#')
        return tuple(int(s[i:i+2],16)/255 for i in(0,2,4))
    return tuple(value[:3])


def _linear(v):
    return v/12.92 if v<=.04045 else((v+.055)/1.055)**2.4


def _mat(label,color,rough=.65,metal=0):
    color=_rgb(color)
    key='RA4_River_'+label+'_'+''.join(f'{round(v*255):02x}'for v in color)
    existing=bpy.data.materials.get(key)
    if existing:return existing
    mat=bpy.data.materials.new(key);mat.diffuse_color=(*color,1);mat.use_nodes=True
    nodes,links=mat.node_tree.nodes,mat.node_tree.links
    bs=next(n for n in nodes if n.type=='BSDF_PRINCIPLED')
    base=tuple(_linear(v)for v in color)
    bs.inputs['Base Color'].default_value=(*base,1)
    bs.inputs['Roughness'].default_value=rough;bs.inputs['Metallic'].default_value=metal
    tex=nodes.new('ShaderNodeTexNoise');tex.inputs['Scale'].default_value=.42 if label=='water' else 2.4;tex.inputs['Detail'].default_value=3.2
    coords=nodes.new('ShaderNodeTexCoord');links.new(coords.outputs['Object'],tex.inputs['Vector'])
    ramp=nodes.new('ShaderNodeValToRGB')
    ramp.color_ramp.elements[0].color=tuple(v*.72 for v in base)+(1,)
    ramp.color_ramp.elements[1].color=tuple(min(1,v*1.18+.004)for v in base)+(1,)
    links.new(tex.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],bs.inputs['Base Color'])
    bump=nodes.new('ShaderNodeBump');bump.inputs['Strength'].default_value=.18;bump.inputs['Distance'].default_value=.028 if label=='water' else .065
    links.new(tex.outputs['Fac'],bump.inputs['Height']);links.new(bump.outputs['Normal'],bs.inputs['Normal'])
    if label=='water' and 'Coat Weight'in bs.inputs:bs.inputs['Coat Weight'].default_value=.38
    mat['ra_color_space']='linear'
    return mat


def _mesh(name,verts,faces,materials,col,role,location=(0,0,0),smooth=False,bevel=0):
    mesh=bpy.data.meshes.new(name);mesh.from_pydata(verts,[],faces);mesh.update()
    obj=bpy.data.objects.new(name,mesh);col.objects.link(obj);obj.location=location
    for material in materials:mesh.materials.append(material)
    obj['v3_role']=role;obj['ra_river_detail']=True
    for face in mesh.polygons:face.use_smooth=smooth
    if bevel:
        mod=obj.modifiers.new('Slate weathered edges','BEVEL');mod.width=bevel;mod.segments=2
    return obj


def _spline(controls,per_span=20):
    out=[]
    for i in range(len(controls)-1):
        p0=controls[max(0,i-1)];p1=controls[i];p2=controls[i+1];p3=controls[min(len(controls)-1,i+2)]
        for j in range(per_span):
            t=j/per_span;t2=t*t;t3=t2*t
            out.append(tuple(.5*((2*p1[a])+(-p0[a]+p2[a])*t+(2*p0[a]-5*p1[a]+4*p2[a]-p3[a])*t2+(-p0[a]+3*p1[a]-3*p2[a]+p3[a])*t3)for a in(0,1)))
    out.append(tuple(controls[-1]))
    return out


def _resample(points,spacing=.65):
    lengths=[math.dist(points[i],points[i+1])for i in range(len(points)-1)]
    total=sum(lengths);count=max(2,math.ceil(total/spacing));edge=0;start=0;out=[]
    for i in range(count+1):
        target=total*i/count
        while edge<len(lengths)-1 and start+lengths[edge]<target:start+=lengths[edge];edge+=1
        t=(target-start)/max(lengths[edge],1e-9);a,b=points[edge],points[edge+1]
        out.append((a[0]+(b[0]-a[0])*t,a[1]+(b[1]-a[1])*t))
    return out


def _distance(x,y,points,closed=False):
    best=math.inf
    count=len(points)if closed else len(points)-1
    for i in range(count):
        a,b=points[i],points[(i+1)%len(points)]
        dx,dy=b[0]-a[0],b[1]-a[1]
        t=max(0,min(1,((x-a[0])*dx+(y-a[1])*dy)/max(1e-9,dx*dx+dy*dy)))
        best=min(best,(x-a[0]-t*dx)**2+(y-a[1]-t*dy)**2)
    return math.sqrt(best)


def plan_river(idx,route_data,palette):
    """Return the world-space native brook plan for emerald world 0 only."""
    if idx!=0:return None
    controls=[[-12.,-10.],[-4.,-7.],[3.,-4.],[10.,-4.5],[17.,-6.3],[24.,-11.],[31.,-17.],[40.,-19.]]
    pads=route_data.get('building_pads_v4',[])
    # Protect planned house pads before carving: all values here are world units.
    for unused in range(3):
        for i,point in enumerate(controls):
            if i in(3,4):continue  # The established road crossing is immutable.
            for pad in pads:
                center=pad.get('center',pad.get('center_xy',pad.get('position',[0,0])))if isinstance(pad,dict)else pad
                radius=float(pad.get('blend_radius',pad.get('flat_radius',12)))if isinstance(pad,dict)else 12.
                dx,dy=point[0]-center[0],point[1]-center[1];d=math.hypot(dx,dy);required=radius+7.2
                if d<required:
                    dx,dy=(dx/max(d,.01),dy/max(d,.01))if d>.01 else(-1,0)
                    point[0]=center[0]+dx*required;point[1]=center[1]+dy*required
    points=_resample(_spline(controls),.65)
    protected_pads=[]
    for pad in pads:
        if isinstance(pad,dict):
            center=pad.get('center',pad.get('center_xy',pad.get('position',[0,0])))
            protected_pads.append(([float(center[0]),float(center[1])],float(pad.get('flat_radius',12))+.6))
    return {'points':[[round(x,5),round(y,5)]for x,y in points],
            'width':6.0,'water_z':-.65,'bed_z':-1.8,'bank_width':3.0,
            'controls':controls,'length':sum(math.dist(a,b)for a,b in zip(points[:-1],points[1:])),
            'seed':7101,'name':'Emerald winding slate brook','protected_pads':protected_pads}


def _tube_accumulate(vertices,faces,points,radius,sides=7):
    start=len(vertices);points=[Vector(p)for p in points]
    for i,p in enumerate(points):
        axis=(points[min(i+1,len(points)-1)]-points[max(0,i-1)]).normalized()
        u=axis.cross(Vector((0,0,1))).normalized();v=axis.cross(u).normalized()
        for j in range(sides):vertices.append(tuple(p+radius*(u*math.cos(j*TAU/sides)+v*math.sin(j*TAU/sides))))
    faces.append(tuple(start+j for j in reversed(range(sides))))
    for i in range(len(points)-1):
        for j in range(sides):faces.append((start+i*sides+j,start+i*sides+(j+1)%sides,start+(i+1)*sides+(j+1)%sides,start+(i+1)*sides+j))
    faces.append(tuple(start+(len(points)-1)*sides+j for j in range(sides)))


def _slate_geometry(sx,sy,sz,angle,rng):
    n=8;verts=[];faces=[]
    rough=[rng.uniform(.86,1.13)for i in range(n)]
    c,s=math.cos(angle),math.sin(angle)
    for ring,(radius,z)in enumerate(((.85,-.42),(1.,.08),(.60,.53))):
        for i in range(n):
            a=i*TAU/n;x=sx*radius*rough[i]*math.cos(a);y=sy*radius*rough[i]*math.sin(a)
            verts.append((c*x-s*y,s*x+c*y,z*sz+(rng.uniform(-.06,.06)*sz if ring==2 else 0)))
    faces.append(tuple(reversed(range(n))));faces.append(tuple(2*n+i for i in range(n)))
    for ring in range(2):
        for i in range(n):faces.append((ring*n+i,ring*n+(i+1)%n,(ring+1)*n+(i+1)%n,(ring+1)*n+i))
    return verts,faces


def _moss_geometry(sx,sy,sz,rng):
    n=12;rows=5;verts=[(0,0,sz)];faces=[]
    for row in range(1,rows):
        phi=row/rows*math.pi
        for j in range(n):
            a=j*TAU/n;f=1+.20*math.sin(a*3+row*.8)+.08*math.sin(a*5-row*.4)
            verts.append((sx*math.sin(phi)*math.cos(a)*f,sy*math.sin(phi)*math.sin(a)*f,sz*math.cos(phi)))
    bottom=len(verts);verts.append((0,0,-sz))
    for j in range(n):faces.append((0,1+j,1+(j+1)%n))
    for row in range(rows-2):
        for j in range(n):a=1+row*n+j;b=1+row*n+(j+1)%n;faces.append((a,a+n,b+n,b))
    last=1+(rows-2)*n
    for j in range(n):faces.append((last+j,bottom,last+(j+1)%n))
    return verts,faces


def build_river(plan,land,palette,col):
    """Return watertight meshes; every raised bank fragment clears the road."""
    if not plan:return []
    if isinstance(col,str):
        col=bpy.data.collections.get(col)or bpy.data.collections.new(col)
        if not col.users:bpy.context.scene.collection.children.link(col)
    col=col or bpy.context.scene.collection
    rng=random.Random(plan.get('seed',7101));palette=palette or {}
    water=_mat('water',palette.get('water','#3C9CA9'),.22,.10)
    foam=_mat('foam','#D9F0E7',.40)
    current=_mat('deep current','#337785',.28,.08)
    rockcolors=[palette.get('rock','#687476'),'#72858A','#52656D','#81928B']
    slate=[_mat('slate'+str(i),c,.83)for i,c in enumerate(rockcolors)]
    moss=_mat('moss','#4F7538',.91)
    points=[tuple(p[:2])for p in plan['points']];n=len(points);width=plan['width'];z=plan['water_z']
    normals=[];tangents=[]
    for i,p in enumerate(points):
        a,b=points[max(0,i-1)],points[min(n-1,i+1)];dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy)
        tangents.append((dx/length,dy/length));normals.append((-dy/length,dx/length))
    # One closed, finite water volume; side edges softly vary like real banks.
    across=8;verts=[];faces=[]
    for i,(p,v)in enumerate(zip(points,normals)):
        end=min(1,min(i,n-1-i)/7);taper=.62+.38*(end*end*(3-2*end))
        half=width/2*taper*(1+.06*math.sin(i*.24)+.035*math.sin(i*.71))
        for j in range(across+1):
            lateral=-half+2*half*j/across
            verts.append((p[0]+v[0]*lateral,p[1]+v[1]*lateral,z+.012*math.sin(i*.53+j*.8)))
    upper=len(verts);verts.extend((x,y,zz-.18)for x,y,zz in list(verts))
    for i in range(n-1):
        for j in range(across):
            a=i*(across+1)+j;b=(i+1)*(across+1)+j
            faces.extend(((a,b,b+1,a+1),(a+upper,a+1+upper,b+1+upper,b+upper)))
        for j in(0,across):
            a=i*(across+1)+j;b=(i+1)*(across+1)+j
            faces.append((a,a+upper,b+upper,b)if j==0 else(a,b,b+upper,a+upper))
    # Rounded shallow terminal pools close into the excavated endpoint banks;
    # no visible ruler-straight water cuts or degenerate collapsed ribbon tips.
    for row,sign in((0,-1),(n-1,1)):
        p,tangent=points[row],tangents[row]
        inner=[row*(across+1)+j for j in range(across+1)]
        half=math.dist(verts[inner[0]][:2],verts[inner[-1]][:2])/2
        outer_top=[];outer_bottom=[]
        for j,a in enumerate(inner):
            if j in(0,across):outer_top.append(a);outer_bottom.append(a+upper);continue
            q=2*j/across-1;extension=half*.85*math.sqrt(max(0,1-q*q))
            x,y,zz=verts[a]
            outer_top.append(len(verts));verts.append((x+sign*tangent[0]*extension,y+sign*tangent[1]*extension,zz))
            outer_bottom.append(len(verts));verts.append((x+sign*tangent[0]*extension,y+sign*tangent[1]*extension,zz-.18))
        for j in range(across):
            top=[inner[j],inner[j+1],outer_top[j+1],outer_top[j]]
            bottom=[inner[j]+upper,inner[j+1]+upper,outer_bottom[j+1],outer_bottom[j]]
            top=list(dict.fromkeys(top));bottom=list(dict.fromkeys(bottom))
            faces.append(tuple(top if sign<0 else reversed(top)))
            faces.append(tuple(reversed(bottom))if sign<0 else tuple(bottom))
            a,b=outer_top[j],outer_top[j+1];c,d=outer_bottom[j],outer_bottom[j+1]
            faces.append((a,b,d,c)if sign<0 else(b,a,c,d))
    ribbon=_mesh('River01_closed_winding_water_volume',verts,faces,[water],col,'water',smooth=True)
    ribbon['river_length_m']=plan.get('length',0);ribbon['river_width_m']=width
    objects=[ribbon]
    road=[tuple(p[:2])for p in land.get('route_points',[])]
    protected=land.get('road_width',8)/2+land.get('shoulder',2)
    min_bank_margin=math.inf;rock_count=0
    # Irregular slate pieces hug the excavated bank, never the road shoulder.
    stations=[];station=2
    while station<n-2:
        stations.append(station);station+=rng.randint(2,6)
    for i in stations:
        p,v,tangent=points[i],normals[i],tangents[i]
        for side in(-1,1):
            if rng.random()<.25:continue
            offset=width/2+rng.uniform(.40,2.1);jitter=rng.uniform(-.75,.75)
            x,y=p[0]+side*v[0]*offset+tangent[0]*jitter,p[1]+side*v[1]*offset+tangent[1]*jitter
            mass=rng.choices((.72,1.,1.55),(.18,.55,.27))[0]
            sx,sy,sz=rng.uniform(1.05,2.05)*mass,rng.uniform(.82,1.42)*mass,rng.uniform(1.3,2.35)*mass
            rv,rf=_slate_geometry(sx,sy,sz,math.atan2(tangent[1],tangent[0])+rng.uniform(-.5,.5),rng)
            bound=max(math.hypot(a,b)for a,b,c in rv)
            road_distance=_distance(x,y,road,True)if road else math.inf
            if road_distance<=protected+bound+.14:continue
            if any(math.hypot(x-pad[0],y-pad[1])<rad+bound for pad,rad in plan.get('protected_pads',[])):continue
            ground=land['height_at'](x,y);cz=ground+.08*sz
            rock=_mesh(f'River01_slate_bank_{rock_count:03d}',rv,rf,slate,col,'obstacle',(x,y,cz),bevel=.065)
            for face in rock.data.polygons:face.material_index=rng.randrange(len(slate))if face.index%5==0 else rock_count%len(slate)
            rock['road_distance_m']=road_distance;rock['horizontal_bound_m']=bound;rock['road_clearance_margin_m']=road_distance-protected-bound
            min_bank_margin=min(min_bank_margin,road_distance-protected-bound)
            objects.append(rock);rock_count+=1
            if rng.random()<.47:
                mv,mf=_moss_geometry(sx*rng.uniform(.40,.68),sy*rng.uniform(.38,.66),.10,rng)
                objects.append(_mesh(f'River01_moss_cap_{rock_count:03d}',mv,mf,[moss],col,'obstacle',(x,y,cz+sz*.51+.05),smooth=True))
    # Modeled white current arcs and darker flow filaments survive GLB export.
    fv=[];ff=[];dv=[];df=[]
    for i in range(4,n-4,4):
        if rng.random()<.16:continue
        p,v,tangent=points[i],normals[i],tangents[i]
        lateral=rng.uniform(-width*.30,width*.30);length=rng.uniform(.70,2.35);drift=rng.uniform(-.6,.6)
        arc=[]
        for j in range(7):
            t=j/6-.5
            arc.append((p[0]+v[0]*(lateral+t*length)+tangent[0]*(drift+.32*math.cos(t*math.pi)),p[1]+v[1]*(lateral+t*length)+tangent[1]*(drift+.32*math.cos(t*math.pi)),z+.045))
        _tube_accumulate(fv,ff,arc,rng.uniform(.025,.050),7)
        for speck in range(rng.randint(2,4)):
            sv,sf=_moss_geometry(rng.uniform(.08,.18),rng.uniform(.05,.11),.018,rng)
            a=arc[rng.randrange(len(arc))];dx,dy=rng.uniform(-.25,.25),rng.uniform(-.25,.25);base=len(fv)
            fv.extend((x+a[0]+dx,y+a[1]+dy,zz+z+.042)for x,y,zz in sv)
            ff.extend(tuple(base+j for j in face)for face in sf)
        if i%8==0:
            for side in(-1,1):
                streak=[(p[0]+side*v[0]*(1.45+.14*math.sin(t*2))+t*tangent[0],p[1]+side*v[1]*(1.45+.14*math.sin(t*2))+t*tangent[1],z+.009)for t in(-1.3,-.7,0,.7,1.3)]
                _tube_accumulate(dv,df,streak,.025,7)
    if fv:objects.append(_mesh('River01_modeled_white_current_foam',fv,ff,[foam],col,'water',smooth=True))
    if dv:objects.append(_mesh('River01_modeled_deep_flow_filaments',dv,df,[current],col,'water',smooth=True))
    ribbon['bank_rock_count']=rock_count;ribbon['minimum_bank_clearance_margin_m']=min_bank_margin if math.isfinite(min_bank_margin)else 0
    return objects

