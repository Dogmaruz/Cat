"""Original Amber Winds world. Executed in the independent source-builder namespace."""
RNG=random.Random(421102)
HEIGHT_KEYS=[(0,4),(55,4),(145,9),(230,19),(290,23),(375,23),(455,14),(550,8),(640,4),(LENGTH,4)]
HOPPING_STATIONS=[485,522,561]
ISLANDS=[('Arrival amber meadow',(-26,5),(78,36),65),('Eastern autumn oasis',(65,45),(65,57),72),
 ('High windmill gardens',(43,147),(61,62),83),('Western gold grove',(-24,153),(38,45),67),
 ('Lower return meadow',(-119,13),(64,37),72),('Old copper windmill crag',(-164,128),(23,23),53),
 ('Distant redwood rock',(128,155),(18,25),60),('Small golden rock',(22,-43),(20,15),49),
 ('Miller terrace',(117,81),(21,25),50),
 ('Amber hopping garden',tuple(route(485)[0][:2]),(5.5,5.0),31),
 ('Copper hopping garden',tuple(route(522)[0][:2]),(6.0,5.5),35),
 ('Autumn hopping garden',tuple(route(561)[0][:2]),(5.0,5.5),30)]
ROCKS=[(.36,.37,.36),(.49,.48,.43),(.58,.55,.47),(.44,.45,.43),(.66,.61,.51),(.52,.50,.43),(.68,.65,.57),(.40,.42,.40)]
BUILD_SITES=[('forest_house',(-20,25),1.35,-.1),('cloud_house',(38,121),1.35,.3),('forest_house',(-22,155),1.12,.6)]
MILL_SITES=[(122,82,1.0,-.8),(96,160,1.40,-1.4),(-164,128,.90,.2)]
PADS=[(x,y,8 if i!=1 else 10,play(nearest(x,y)[1])-2.65)for i,(x,y,_,_)in enumerate(MILL_SITES)]+[(x,y,7,play(nearest(x,y)[1])-2.65)for _,(x,y),_,_ in BUILD_SITES]
POOLS=[(78,40,27,22,1.35),(17,151,16,12,15.1)]
UPPER_FALL_FORWARD=Vector((11,-14,0)).normalized()
UPPER_SPILL_RADIUS=3.0
UPPER_FALL_END=Vector((65,118,0))+UPPER_FALL_FORWARD*UPPER_SPILL_RADIUS
STREAMS=[([(97,40),(108,40),(120,43),(132,47)],7.8,1.35), ([(30,150),(43,144),(54,132),(65,118),tuple(UPPER_FALL_END[:2])],5.6,15.1)]
ROTOR_MANIFEST=[]

def lagoon_r(x,y):
    cx,cy,rx,ry,z=POOLS[0];return math.hypot((x-cx)/rx,(y-cy)/ry)

def stream_distance(x,y,points):
    best=(1e9,0,0)
    travel=0
    for a,b in zip(points,points[1:]):
        dx,dy=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dy);t=clamp(((x-a[0])*dx+(y-a[1])*dy)/(length*length))
        px,py=a[0]+t*dx,a[1]+t*dy;dist=math.hypot(x-px,y-py)
        if dist<best[0]:best=(dist,travel+t*length,t)
        travel+=length
    return best

def pool_radius(a,index=0):
    return 1+.055*math.sin(a*3+index)+.040*math.cos(a*7+.7)+.025*math.sin(a*11)

def terrain_height(x,y):
    d,s=nearest(x,y);height=play(s)-2.65;in_basin=False
    height+=(1-smooth((11-d)/5))*(.58*math.sin(x*.105+y*.12)+.35*math.cos(y*.19-x*.055))
    for index,(cx,cy,rx,ry,z)in enumerate(POOLS):
        a=math.atan2((y-cy)/ry,(x-cx)/rx);r=math.hypot((x-cx)/rx,(y-cy)/ry)/pool_radius(a,index)
        in_basin|=r<1.04
        if r<1.5:
            target=z-.65+3.2*smooth((r-.77)/.45);height=min(height,target+(height-target)*smooth((r-1.05)/.45))
    for points,width,z in STREAMS:
        dist,_,_=stream_distance(x,y,points)
        if dist<width*.5+3:
            target=z-.40+3.5*smooth((dist-width*.44)/3.0);f=1-smooth((dist-width*.5)/3.0)
            # The creek may cut through a basin, but its upstream end cap must
            # never build a crescent embankment in the existing open water.
            height=min(height,target)if in_basin else height*(1-f)+target*f
    for cx,cy,r,z in PADS:
        dist=math.hypot(x-cx,y-cy)
        if dist<r+4:
            f=1-smooth((dist-r)/4);height=height*(1-f)+z*f
    # Carve the tiny surviving corners of the bed under the curved spill.
    # This is local to the mouth: the meadow and all raw flight heights stay.
    delta=Vector((x-65,y-118,0));along=delta.dot(UPPER_FALL_FORWARD);across=abs(delta.x*UPPER_FALL_FORWARD.y-delta.y*UPPER_FALL_FORWARD.x)
    if 0<along<UPPER_SPILL_RADIUS and across<3.15:
        water=15.135-UPPER_SPILL_RADIUS*(1-math.sqrt(max(0,1-(along/UPPER_SPILL_RADIUS)**2)))
        cut=1-smooth((across-2.6)/.55);height=min(height,height*(1-cut)+(water-.3)*cut)
    if d<9.5:height=min(height,play(s)-2.65)
    return height

def meadow_color(x,y):
    shade=clamp(.52+.22*math.sin(x*.085+y*.12)+.15*math.sin(x*.61-y*.45))
    rgb=tuple(a*(1-shade)+b*shade for a,b in zip((.49,.42,.17),(.78,.65,.30)))
    green=clamp((math.sin(x*.043-y*.047)-.70)*1.8)
    return tuple(a*(1-green)+b*green for a,b in zip(rgb,(.37,.46,.23)))

def add_trees():
    count=0;colors=[(.95,.62,.045),(1,.72,.10),(.87,.35,.045),(.72,.12,.035),(.80,.43,.045),(.38,.43,.17)]
    for attempt in range(6500):
        x=RNG.uniform(-181,129);y=RNG.uniform(-25,204);d,s=nearest(x,y);scale=RNG.uniform(1.45,2.10)
        if not footprint(x,y,5)or d<9.0+2.8*scale:continue
        if any(math.hypot(x-cx,y-cy)<r+6 for cx,cy,r,z in PADS):continue
        if any(math.hypot((x-cx)/rx,(y-cy)/ry)<1.15 for cx,cy,rx,ry,z in POOLS):continue
        if any(stream_distance(x,y,p)[0]<w*.5+3 for p,w,z in STREAMS):continue
        z=actual_ground(x,y)
        if z is None:continue
        color=colors[0 if count%10<3 else 1 if count%10<5 else 2 if count%10<7 else 3 if count%10<9 else 5]
        kind='pine'if count%13==0 else'broadleaf'
        if kind=='pine':color=(.22,.32,.17)
        objs=scenery.make_tree((x,y,z-.04),scale=scale,seed=8100+attempt,kind=kind,palette={'leaf':color,'leaf_light':tuple(min(1,c*1.18)for c in color),'bark':(.34,.23,.13)})
        track_decor(objs,x,y,'autumn '+kind,z-.04);count+=1
        if count>=60:break
    return count

def grass_patch(x,y,scale,seed):
    rng=random.Random(seed);vs=[];fs=[];cc=[]
    for i in range(22):
        a=i*2.39996;rr=math.sqrt(rng.random())*.85*scale;xx=x+math.cos(a)*rr;yy=y+math.sin(a)*rr;z=actual_ground(xx,yy)
        if z is None:continue
        h=rng.uniform(.25,.75)*scale;w=rng.uniform(.035,.07)*scale;dx,dy=math.cos(a),math.sin(a);b=len(vs)
        vs.extend([(xx-dy*w,yy+dx*w,z),(xx+dy*w,yy-dx*w,z),(xx+dx*.12+dy*w*.5,yy+dy*.12-dx*w*.5,z+h*.5),(xx+dx*.12-dy*w*.5,yy+dy*.12+dx*w*.5,z+h*.5),(xx+dx*.28,yy+dy*.28,z+h)])
        fs.extend([(b,b+1,b+2,b+3),(b+3,b+2,b+4)]);c=(.50,.47,.20)if i%4 else(.34,.43,.21);cc.extend([col(c)]*4+[col((.73,.62,.29))])
    return mesh('Amber_VolumeMeadowGrass',vs,fs,'Grass',cc)

def flower_patch(x,y,scale,seed):
    rng=random.Random(seed);vs=[];fs=[];cc=[]
    for i in range(11):
        xx=x+rng.uniform(-1,1)*scale;yy=y+rng.uniform(-1,1)*scale;z=actual_ground(xx,yy)
        if z is None:continue
        h=rng.uniform(.25,.60)*scale;b=len(vs);vs.extend([(xx-.018,yy,z),(xx+.018,yy,z),(xx+.018,yy,z+h),(xx-.018,yy,z+h)]);fs.append((b,b+1,b+2,b+3));cc.extend([col((.32,.42,.18))]*4)
        petal=[(1,.72,.09),(.88,.25,.055),(.99,.94,.72)][seed%3]
        for j in range(5):
            a=j*math.tau/5;r=.14*scale;dx,dy=math.cos(a),math.sin(a);b=len(vs)
            vs.extend([(xx,yy,z+h),(xx+dx*r-dy*r*.5,yy+dy*r+dx*r*.5,z+h+.02),(xx+dx*r*1.8,yy+dy*r*1.8,z+h-.02),(xx+dx*r+dy*r*.5,yy+dy*r-dx*r*.5,z+h+.02)]);fs.append((b,b+3,b+2,b+1));cc.extend([col(petal)]*4)
    return mesh('Amber_AutumnWildflowers',vs,fs,'Foliage',cc)

def add_gardens():
    count=0
    for attempt in range(9000):
        x=RNG.uniform(-180,132);y=RNG.uniform(-28,209);d,s=nearest(x,y)
        if not footprint(x,y,1.7)or d<5.8:continue
        if any(math.hypot((x-cx)/rx,(y-cy)/ry)<1.02 for cx,cy,rx,ry,z in POOLS):continue
        if any(stream_distance(x,y,p)[0]<w*.5+.5 for p,w,z in STREAMS):continue
        if any(math.hypot(x-cx,y-cy)<r for cx,cy,r,z in PADS):continue
        if actual_ground(x,y)is None:continue
        scale=RNG.uniform(.8,1.6);grass_patch(x,y,scale,attempt)
        if count%2==0:flower_patch(x,y,scale,attempt)
        count+=1
        if count>=380:break
    return count

def low_autumn_patch(x,y,seed):
    """Baked leaves and bent grass blades keep the close foreground tactile."""
    rng=random.Random(seed);vs=[];fs=[];cc=[];leaves=[];lf=[];lc=[];accepted=0
    tones=[(.96,.64,.13),(.86,.36,.055),(.67,.16,.045),(.74,.51,.16),(.46,.48,.20)]
    for i in range(24):
        a=rng.random()*math.tau;r=math.sqrt(rng.random())*1.35;xx=x+math.cos(a)*r;yy=y+math.sin(a)*r;z=actual_ground(xx,yy)
        if z is None:continue
        d,s=nearest(xx,yy);height=rng.uniform(.30,.56)
        if z+height>play(s)-1.6:continue
        if any(math.hypot((xx-cx)/rx,(yy-cy)/ry)<1.02 and z<level+.2 for cx,cy,rx,ry,level in POOLS):continue
        if any(stream_distance(xx,yy,p)[0]<w*.51 and z<level+.15 for p,w,level in STREAMS):continue
        accepted+=1;dx,dy=math.cos(a),math.sin(a);width=rng.uniform(.022,.055);bend=rng.uniform(.10,.25);base=len(vs)
        vs.extend([(xx-dy*width,yy+dx*width,z),(xx+dy*width,yy-dx*width,z),
          (xx+dx*bend+dy*width*.35,yy+dy*bend-dx*width*.35,z+height*.65),
          (xx+dx*bend-dy*width*.35,yy+dy*bend+dx*width*.35,z+height*.65),
          (xx+dx*bend*1.5,yy+dy*bend*1.5,z+height)])
        fs.extend([(base,base+1,base+2,base+3),(base+3,base+2,base+4)])
        tone=tones[i%5];cc.extend([col(tuple(c*.70 for c in tone))]*2+[col(tone)]*3)
        if i%2:continue
        length=rng.uniform(.24,.52);wide=length*rng.uniform(.34,.53);base=len(leaves);right=Vector((-dy,dx,0));direction=Vector((dx,dy,0));p=Vector((xx,yy,z+.035))
        shape=[p-direction*length*.5,p-direction*length*.16+right*wide*.5,p+direction*length*.5,
               p+direction*length*.12-right*wide*.5,p+Vector((0,0,.022))]
        leaves.extend(map(tuple,shape));lf.extend([(base+4,base+(j+1)%4,base+j)for j in range(4)]);lc.extend([col(tone)]*5)
    if vs:mesh('Amber_CloseGoldenMeadowBlades',vs,fs,'Grass',cc)
    if leaves:mesh('Amber_ScatteredAutumnLeafLitter',leaves,lf,'Foliage',lc)
    return accepted

def add_route_autumn_dressing():
    report=dict(low_patches=0,low_blades=0,shrubs=0,approach_canopies=0,max_low_cover_height=.56)
    rng=random.Random(461102)
    for station in np.arange(5,LENGTH-5,3.0):
        p,n=route(station)
        for side in(-1,1):
            for offset in(2.2,5.1,8.0):
                pp=p+n*(side*(offset+rng.uniform(-.55,.55)));seed=36000+report['low_patches']
                count=low_autumn_patch(pp.x,pp.y,seed)
                if count:
                    report['low_patches']+=1;report['low_blades']+=count
                    if report['low_patches']%10==0:flower_patch(pp.x,pp.y,.75,seed)
    palette=[(1,.69,.06),(.92,.40,.025),(.74,.17,.035),(.90,.57,.08)]
    for index,station in enumerate(np.arange(18,LENGTH-18,13.0)):
        p,n=route(station)
        for sign in(-1,1):
            pp=p+n*(sign*rng.uniform(11.4,14.5));d,s=nearest(pp.x,pp.y);z=actual_ground(pp.x,pp.y)
            if z is None or d<11.4:continue
            if any(math.hypot(pp.x-cx,pp.y-cy)<r+2 for cx,cy,r,zz in PADS):continue
            if any(math.hypot((pp.x-cx)/rx,(pp.y-cy)/ry)<1.12 for cx,cy,rx,ry,zz in POOLS):continue
            if any(stream_distance(pp.x,pp.y,p)[0]<w*.5+2 for p,w,zz in STREAMS):continue
            color=palette[(index+(1 if sign>0 else 0))%4];scale=rng.uniform(.65,.90)
            objs=scenery.make_tree((pp.x,pp.y,z-.03),scale=scale,seed=39000+index*2+sign,kind='shrub',palette={'leaf':color,'leaf_light':tuple(min(1,c*1.15)for c in color),'bark':(.38,.22,.10)})
            track_decor(objs,pp.x,pp.y,'grouped low autumn shrub',z-.03);report['shrubs']+=1
    for index,station in enumerate((55,95,142,158,225,244,280,320,350,395,425,607,625,664,704,739)):
        p,n=route(station);pp=p+n*((-1 if index%2 else 1)*17.7);d,s=nearest(pp.x,pp.y);z=actual_ground(pp.x,pp.y)
        if z is None or d<16.0:continue
        if any(math.hypot(pp.x-cx,pp.y-cy)<r+6 for cx,cy,r,zz in PADS):continue
        if any(math.hypot((pp.x-cx)/rx,(pp.y-cy)/ry)<1.14 for cx,cy,rx,ry,zz in POOLS):continue
        if any(stream_distance(pp.x,pp.y,p)[0]<w*.5+4 for p,w,zz in STREAMS):continue
        color=palette[index%4];objs=scenery.make_tree((pp.x,pp.y,z-.04),scale=1.55,seed=41000+index,kind='broadleaf',palette={'leaf':color,'leaf_light':tuple(min(1,c*1.15)for c in color),'bark':(.34,.22,.12)})
        track_decor(objs,pp.x,pp.y,'gold and copper approach canopy',z-.04);report['approach_canopies']+=1
    return report

def add_stones():
    count=0
    for attempt in range(4000):
        x=RNG.uniform(-181,133);y=RNG.uniform(-25,207);d,s=nearest(x,y)
        if not footprint(x,y,3)or d<9.8:continue
        if any(math.hypot(x-cx,y-cy)<r+2 for cx,cy,r,z in PADS):continue
        z=actual_ground(x,y)
        if z is None:continue
        scale=RNG.uniform(.6,1.9);objs=scenery.make_rock((x,y,z-.14),scale=scale,seed=15000+attempt,kind='boulder',palette={'rock':(.51,.49,.42)})
        for ob in objs:ob['amber_bucket']='Rocks'
        track_decor(objs,x,y,'weathered meadow boulder',z-.14);count+=1
        if count>=115:break
    return count

def windmill(x,y,scale,index,yaw):
    z=actual_ground(x,y);palette=dict(style='forest',wall='#F3D79E',roof='#3E8883',wood='#73502B',stone='#857D65',metal='#D8A64D',accent='#EBD9AE',trim='#F8E9BD',glass='#37797D')
    b=architecture._Builder('forest_windmill',palette,1420+index,None)
    b.cylinder('windmill full stone footing',(0,0,0),(0,0,.65),3.8,'stone',r2=3.65,n=48)
    b.cylinder('warm plaster tapered mill tower',(0,0,.50),(0,0,10.8),3.35,'wall',r2=2.55,n=48)
    for h in(1.0,3.7,7.0,10.6):b.torus('tower copper courses',(0,0,h),3.32-.065*h,.07,'metal',n=64)
    b.cylinder('patinated teal conical mill roof',(0,0,10.7),(0,0,14.0),3.5,'roof',r2=.08,n=48)
    b.torus('broad roof copper rim',(0,0,10.9),3.5,.12,'metal',n=64)
    b.window(0,-3.34,1.1,1.5,2.6,shutters=False);b.window(0,-2.98,6.1,1.25,1.6,shutters=True)
    b.speaker(2.4,-2.5,4.1,.65)
    for j in range(5):b.box('entrance worn stone step',(0,-3.5-j*.42,.18*(5-j)),(2.45,.55,.22),'stone',.07)
    objs=b.finish((x,y,z-.05),yaw,scale);track_decor(objs,x,y,'full radio windmill',z-.05)
    axis=Vector((math.sin(yaw),-math.cos(yaw),0));right=Vector((math.cos(yaw),math.sin(yaw),0));up=Vector((0,0,1));pivot=Vector((x,y,z))+axis*(3.65*scale)+up*(9.3*scale)
    rotor=[];radius=6.6*scale
    rotor.append(tube('Amber_MillBrassHub',[pivot-axis*.18*scale,pivot+axis*.32*scale],.48*scale,'Metal',(.84,.60,.22),32))
    for j in range(6):
        a=j*math.tau/6+.10;direction=right*math.cos(a)+up*math.sin(a);tangent=-right*math.sin(a)+up*math.cos(a)
        rotor.append(tube('Amber_MillRadialCopperBeam',[pivot+direction*.5*scale,pivot+direction*radius],.075*scale,'Metal',(.77,.47,.16),8))
        vv=[];ff=[];cc=[]
        for row in range(7):
            t=row/6;reach=radius*(.28+.69*t);half=scale*(.28+.65*t)
            for colIndex in range(4):
                u=colIndex/3;point=pivot+direction*reach+tangent*((u-.5)*2*half)+axis*(math.sin(u*math.pi)*.17*scale)
                vv.append(tuple(point));cc.append(col((.94,.84,.62)if j%2 else(1,.93,.73)))
        for row in range(6):
            for k in range(3):q=row*4+k;ff.append((q,q+1,q+5,q+4))
        rotor.append(mesh('Amber_CreamWindSail',vv,ff,'Fabric',cc,soft=False))
        for frac in(.40,.60,.80,.96):
            reach=radius*frac;half=scale*(.28+.65*(frac-.28)/.69)
            rotor.append(tube('Amber_SailStitchedCopperRib',[pivot+direction*reach-tangent*half,pivot+direction*reach+tangent*half],.025*scale,'Metal',(.72,.48,.22),6))
    bpy.ops.object.select_all(action='DESELECT')
    for ob in rotor:ob.select_set(True)
    bpy.context.view_layer.objects.active=rotor[0];bpy.ops.object.join();ob=rotor[0]
    world=[ob.matrix_world@v.co for v in ob.data.vertices]
    ob.matrix_world.identity();ob.location=pivot
    for v,p in zip(ob.data.vertices,world):v.co=p-pivot
    ob.name='RA_Rooftop_Amber_%02d_WindmillSails'%(index+1);ob['amber_rotor']=True
    # Merge identical category material slots for native shared-material binding.
    unique=[];mapping={}
    for i,m in enumerate(ob.data.materials):
        if m not in unique:unique.append(m)
        mapping[i]=unique.index(m)
    indices=[mapping[p.material_index]for p in ob.data.polygons];ob.data.materials.clear()
    for m in unique:ob.data.materials.append(m)
    for p,i in zip(ob.data.polygons,indices):p.material_index=i
    speed=[65,-50,82][index]
    ROTOR_MANIFEST.append(dict(nodeName=ob.name,kind='windmill_sails',pivot=dict(x=pivot.x,y=pivot.z,z=-pivot.y),axis=dict(x=axis.x,y=axis.z,z=-axis.y),degreesPerSecond=speed))
    clearance=min(nearest(*(pivot+right*math.cos(a)*radius+up*math.sin(a)*radius)[:2])[0]for a in np.linspace(0,math.tau,361))
    if clearance<8.5:raise RuntimeError('Windmill sweep invades flight corridor '+str(clearance))
    return dict(position=[x,y,z],scale=scale,sailRadius=radius,speed=speed,sweepRouteClearance=clearance)

def add_architecture():
    palette=dict(style='forest',wall='#EFDCA8',roof='#3C8982',wood='#77542F',stone='#8F8469',metal='#CFA04E',accent='#DC8C42',trim='#F5E8C6',glass='#417B7B')
    report=[]
    for i,(kind,(x,y),scale,yaw)in enumerate(BUILD_SITES):
        z=actual_ground(x,y)
        if z is None:raise RuntimeError('Radio cottage unsupported')
        objs=architecture.build_building(kind,location=(x,y,z-.03),yaw=yaw,scale=scale,palette=palette,seed=9210+i);track_decor(objs,x,y,'full autumn radio cottage',z-.03)
        report.append(dict(kind=kind,position=[x,y,z],scale=scale))
    for i,(x,y,scale,yaw)in enumerate(MILL_SITES):report.append(dict(kind='animated windmill',**windmill(x,y,scale,i,yaw)))
    # Static cream weather pennants share the sail fabric material.
    for x,y,_,_ in MILL_SITES:
        z=actual_ground(x,y)
        tube('Amber_CopperPennantPole',[(x+5,y,z),(x+5,y,z+4.8)],.065,'Metal',(.79,.56,.25),9)
        mesh('Amber_CreamWeatherPennant',[(x+5,y,z+4.7),(x+7.6,y+.3,z+4.4),(x+7.2,y-.1,z+3.9),(x+5,y,z+4.0)],[(0,1,2,3)],'Fabric',col((.96,.85,.63)))
    return report

def add_hopping_gardens():
    report=[]
    for index,s in enumerate(HOPPING_STATIONS):
        p,n=route(s);site=ISLANDS[9+index];rx,ry=site[2]
        for j in range(12):
            a=j*2.39996;r=.35+.035*(j%5);x=p.x+rx*r*math.cos(a);y=p.y+ry*r*math.sin(a)
            if actual_ground(x,y)is not None:flower_patch(x,y,.7+(j%3)*.2,18000+j+index*100);grass_patch(x,y,.8,j)
        report.append(dict(name=site[0],station=s,source_center=[p.x,p.y,actual_ground(p.x,p.y)],radii=list(site[2]),rock_depth=site[3]))
    return report

def add_resonators():return []

def shore_rock(x,y,z,index,scale):
    objs=scenery.make_rock((x,y,z-.15*scale),scale=scale,seed=24000+index*11,kind='boulder',palette={'rock':ROCKS[index%len(ROCKS)]})
    yaw=(index*2.39996)%math.tau
    for ob in objs:ob['amber_bucket']='Rocks';ob.rotation_euler.z=yaw;ob.scale.x*=.72+(index%5)*.17;ob.scale.y*=.8+(index%7)*.09
    return objs

def reeds(x,y,z,seed):
    rng=random.Random(seed)
    for i in range(8):
        xx=x+rng.uniform(-.5,.5);yy=y+rng.uniform(-.5,.5);h=rng.uniform(.8,1.65)
        tube('Amber_ShoreReedStem',[(xx,yy,z),(xx+.12,yy,z+h)],.024,'Grass',(.32,.41,.15),5)
        tube('Amber_ReedVelvetHead',[(xx+.11,yy,z+h-.32),(xx+.12,yy,z+h)],.065,'Foliage',(.39,.23,.09),7)

def add_water():
    shores=[];falls=[];poolCount=0
    for index,(cx,cy,rx,ry,z)in enumerate(POOLS):
        n=192;nr=20;vv=[(cx,cy,z)];cc=[col((.10,.45,.48))];uv=[(.5,.5)];ff=[]
        for row in range(1,nr+1):
            for j in range(n):
                a=j*math.tau/n;r=row/nr*pool_radius(a,index);vv.append((cx+rx*r*math.cos(a),cy+ry*r*math.sin(a),z));cc.append(col(tuple(a0*(1-row/nr)+b*row/nr for a0,b in zip((.08,.38,.43),(.16,.59,.60)))));uv.append((math.cos(a)*r,math.sin(a)*r))
        for j in range(n):ff.append((0,1+j,1+(j+1)%n))
        for row in range(nr-1):
            for j in range(n):q=(j+1)%n;a=1+row*n+j;b=1+row*n+q;c=1+(row+1)*n+q;d=1+(row+1)*n+j;ff.append((a,d,c,b))
        mesh('Amber_OrganicSpringPool_'+str(index),vv,ff,'Water',cc,uv,True);poolCount+=1
        for j in range(110 if index==0 else 78):
            a=j*2.39996;var=.98+.04*math.sin(j*1.71);r=pool_radius(a,index)*var;x=cx+rx*r*math.cos(a);y=cy+ry*r*math.sin(a);h=actual_ground(x,y)
            if h is None or nearest(x,y)[0]<8.8:continue
            scale=[.55,.85,1.2,1.6,.7,1.0,1.85][j%7];shore_rock(x,y,max(h,z-.12),j+index*200,scale);shores.append(dict(x=x,y=y,z=h,scale=scale,yaw=(j*2.39996)%math.tau,shape=j%7))
            if j%8==0:reeds(x+.8,y-.3,max(h,z),j)
            if j%13==0:
                objs=scenery.make_tree((x+.9,y+.5,max(h,z)),scale=.45,seed=28000+j+index*100,kind='shrub',palette={'leaf':(.66,.33,.065),'bark':(.32,.22,.12)})
                for ob in objs:ob['amber_bucket']='Foliage'if'leaf'in ob.name or'crown'in ob.name else'Wood'
    for index,(points,width,z)in enumerate(STREAMS):
        # Fine centreline sampling gives true downstream-metre V coordinates.
        dense=[];distance=[];travel=0
        for segment,(a,b)in enumerate(zip(points,points[1:])):
            delta=Vector((b[0]-a[0],b[1]-a[1],0));length=delta.length
            if index==1 and segment==len(points)-2:
                # The last short reach rolls over the natural ledge with a
                # horizontal entry and vertical exit, never a hanging board.
                arc=UPPER_SPILL_RADIUS*math.pi*.5
                for t in np.linspace(0,1,17,endpoint=False):
                    theta=t*math.pi*.5;p=Vector((a[0],a[1],z))+UPPER_FALL_FORWARD*(UPPER_SPILL_RADIUS*math.sin(theta))+Vector((0,0,-UPPER_SPILL_RADIUS*(1-math.cos(theta))))
                    dense.append(p);distance.append(travel+UPPER_SPILL_RADIUS*theta)
                travel+=arc
            else:
                for t in np.linspace(0,1,max(2,math.ceil(length/.6)),endpoint=False):dense.append(Vector((a[0],a[1],z))+delta*t);distance.append(travel+t*length)
                travel+=length
        dense.append(Vector((*points[-1],z-(UPPER_SPILL_RADIUS if index==1 else 0))));distance.append(travel)
        vv=[];ff=[];uv=[];cc=[];across=12
        for row,p in enumerate(dense):
            tangent=(dense[min(row+1,len(dense)-1)]-dense[max(row-1,0)]).normalized();side=Vector((-tangent.y,tangent.x,0)).normalized();w=width*(1+.09*math.sin(distance[row]*.37))
            for j in range(across+1):
                u=j/across;pp=p+side*((u-.5)*w)
                # A tiny hydraulic overlap above the basin prevents coplanar
                # self-intersection at the mouth, retaining continuous flow UVs.
                pp.z+=.035
                vv.append(tuple(pp));uv.append((u,distance[row]));cc.append(col((.13,.53,.54)))
            if row:
                for j in range(across):q=(row-1)*(across+1)+j;ff.append((q,q+across+1,q+across+2,q+1))
            if row%3==0:
                for sign in(-1,1):
                    pp=p+side*(w*.54*sign);h=actual_ground(pp.x,pp.y)
                    if h is not None and nearest(pp.x,pp.y)[0]>8.8:shore_rock(pp.x,pp.y,max(h,z-.18),1200+row*2+sign+index*200,.65+.22*((row+index)%5))
        stream_ob=mesh('Amber_DownstreamCreek_'+str(index),vv,ff,'Stream',cc,uv,True);stream_ob['amber_feature_node']=True
        # Three convex, offset ribbons give the fall real cross-sectional volume.
        end=dense[-1];direction=dense[-1]-dense[-2];fwd=Vector((direction.x,direction.y,0)).normalized();side=Vector((-fwd.y,fwd.x,0));drop=52+index*9
        # Low wet outcrops cover the square sheet corners without spanning the
        # channel or entering the airborne support envelope above the lip.
        for sign in(-1,1):
            for j in range(3):
                bank=Vector((65,118,z))if index==1 else end
                pp=bank+side*(sign*width*(.48+.10*j))-fwd*(.3+j*.65);h=actual_ground(pp.x,pp.y);scale=.46+.14*j
                if h is not None and h+2.35*scale<play(nearest(pp.x,pp.y)[1])-1.6:shore_rock(pp.x,pp.y,min(h,z-.1),1900+index*30+j*2+sign,scale)
        lipWidth=width*(1+.09*math.sin(distance[-1]*.37))
        flow_centres=[];flow_metres=[];flow_distance=distance[-1]
        for row in range(math.ceil(drop/1.2)+1):
            t=row/math.ceil(drop/1.2);point=end+fwd*(1.6*smooth(t*3))+side*(math.sin(t*11)*.15)+Vector((0,0,.035-drop*t))
            if flow_centres:flow_distance+=(point-flow_centres[-1]).length
            flow_centres.append(point);flow_metres.append(flow_distance)
        for layer in range(3):
            vv=[];ff=[];uv=[];cc=[];rows=math.ceil(drop/1.2);cols=24;layerWidth=width*(1-layer*.19)
            for row in range(rows+1):
                t=row/rows;center=flow_centres[row].copy()
                # Start precisely on the creek's final cross-section: the old
                # .6m offset exposed the channel bed as a gold cross-strip.
                center+=side*((math.sin(t*11+layer)-math.sin(layer)-math.sin(t*11))*.15)+fwd*(layer*.16*smooth(t*6))
                for j in range(cols+1):
                    u=j/cols;edge=(u-.5)*layerWidth*(1+.055*math.sin(row*.71+j*.42));bulge=math.sin(u*math.pi)*.28+.08*math.sin(t*35+u*9)
                    blend=smooth(t*8);mouthWidth=lipWidth*(1-layer*.19);edge=(u-.5)*(mouthWidth*(1-blend)+layerWidth*blend)*(1+.055*math.sin(row*.71+j*.42)*blend);bulge*=blend
                    pp=center+side*edge+fwd*bulge
                    vv.append(tuple(pp));uv.append((u,flow_metres[row]));cc.append(col((.20+layer*.055,.62+layer*.025,.68+layer*.035)))
                if row:
                    for j in range(cols):q=(row-1)*(cols+1)+j;ff.append((q,q+cols+1,q+cols+2,q+1))
            fall_ob=mesh('Amber_VoluminousFallingWater_%d_%d'%(index,layer),vv,ff,'Waterfall',cc,uv,True);fall_ob['amber_feature_node']=True
        falls.append(dict(lip=list(end),width=width,drop=drop,layers=3,rowStep=drop/rows,spillRollRadius=UPPER_SPILL_RADIUS if index==1 else 0,rollArcMetres=UPPER_SPILL_RADIUS*math.pi*.5 if index==1 else 0))
    (HERE/'shoreline-design.json').write_text(json.dumps(dict(poolCount=poolCount,shoreRocks=shores,shapeVariants=7,stoneScaleRange=[.55,1.85],waterfalls=falls,streams=[dict(points=p,width=w,height=z,uv='U0..1 across,V downstream metres')for p,w,z in STREAMS]),indent=2),encoding='utf8')
    return len(falls),len(shores)

def add_mist():return 0

def render_proof():
    scene=bpy.context.scene;scene.world=bpy.data.worlds.new('Amber gold afternoon');scene.world.use_nodes=True
    bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.49,.67,.71,1);bg.inputs['Strength'].default_value=.65
    ld=bpy.data.lights.new('Amber afternoon sun','SUN');ld.energy=3.2;ld.angle=.14;ob=bpy.data.objects.new(ld.name,ld);scene.collection.objects.link(ob);ob.rotation_euler=(.45,-.4,-.6)
    scene.render.engine='CYCLES';scene.cycles.samples=12;scene.cycles.use_denoising=True
    try:
        prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
        for d in prefs.devices:d.use=d.type=='CUDA'
        scene.cycles.device='GPU'
    except:pass
    scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.view_settings.view_transform='AgX'
    add_tile_guide()
    for label,target,offset,scale in([]if'--spill-proof-only'in sys.argv else [('overview',(-25,87,-7),(245,-310,270),375),('windmill',(96,160,32),(47,-66,32),90),('shore',(78,40,0),(57,-64,56),112),('top',(-25,90,0),(0,0,400),355)]):
        data=bpy.data.cameras.new('Amber '+label);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=Vector(target)+Vector(offset);cam.rotation_euler=(Vector(target)-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(PROOF/(label+'.png'));bpy.ops.render.render(write_still=True);print('AMBER_PROOF',label,flush=True)
    # Same fixed orbit and vertical FOV as the native close gameplay camera.
    # These are geometry composition checks; Unity owns the real-clock proof.
    scene.render.resolution_x=720;scene.render.resolution_y=1280;scene.cycles.samples=8
    for station in((155,170,185,210)if'--spill-proof-only'in sys.argv else(58,155,170,172,185,200,210,244,272)):
        p,n=route(station);p.z=play(station);lead,normal=route(station+3.2);forward=Vector((normal.y,-normal.x,0))
        target=p+forward*3.8+Vector((0,0,3.6));origin=p-forward*9.5+Vector((0,0,8))
        data=bpy.data.cameras.new('Amber gameplay composition '+str(station));data.type='PERSP';data.sensor_fit='VERTICAL';data.sensor_height=32;data.lens=16/math.tan(math.radians(82/2))
        cam=bpy.data.objects.new(data.name,data);scene.collection.objects.link(cam);cam.location=origin;cam.rotation_euler=(target-origin).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(PROOF/('gameplay-'+str(station)+'.png'));bpy.ops.render.render(write_still=True);print('AMBER_GAMEPLAY_COMPOSITION',station,flush=True)

def write_air_windows():
    samples=[]
    for s in np.arange(430,624,.25):
        p,n=route(s);h=actual_ground(p.x,p.y);samples.append((float(s),h is None))
    gaps=[];start=None
    for s,empty in samples+[(624,False)]:
        if empty and start is None:start=s
        elif not empty and start is not None:
            if s-start>8:gaps.append(dict(x=start+.50,y=s-.50))
            start=None
    (OUT/'cloud_steps.json').write_text(json.dumps(dict(ranges=gaps),indent=2),encoding='utf8')
    return gaps

def build():
    started=time.monotonic();OUT.mkdir(parents=True,exist_ok=True);PROOF.mkdir(parents=True,exist_ok=True);write_route()
    frozen={str(p):sha(p)for p in [REPO/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands'/folder/(stem+'_island.fbx')for folder,stem in[('EmeraldAbyss','emerald_abyss'),('CoralOasis','coral_oasis'),('MoonResonance','moon_resonance')]]}
    for ob in list(bpy.data.objects):bpy.data.objects.remove(ob,do_unlink=True)
    for i,(name,center,radii,depth)in enumerate(ISLANDS):landmass(name,center,radii,depth,1130+i)
    canyon_cleanup=clip_legacy_canyon_shells();add_canyon();unify_terrain();prepare_cap_index();print('AMBER_TERRAIN',flush=True)
    gaps=write_air_windows();rocks=add_stones();buildings=add_architecture();trees=add_trees();print('AMBER_TREES',trees,flush=True)
    flowers=add_gardens();hopping=add_hopping_gardens();falls,shore=add_water();autumn_dressing=add_route_autumn_dressing();side_cascade=add_side_cascade();add_roots();decor=audit_ground_decor();density=runtime_density()
    import water_seam_shading
    water_seam_shading.apply()
    originals=list(bpy.context.scene.objects);audit=source_audits(originals)
    assert not audit['upward_errors'],audit
    assert not audit['above_flight_terrain'],audit
    export=export_scene(originals);bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'amber_winds_preview.blend'))
    report=dict(id='04_amber_winds',frozen_inputs=frozen,frozen_inputs_unchanged=all(sha(p)==h for p,h in frozen.items()),route_length=LENGTH,height_keys=HEIGHT_KEYS,protected_air_corridor_half_width=8.5,architecture=buildings,trees=trees,rock_clusters=rocks,flower_clusters=flowers,hopping_islets=hopping,cloud_ranges=gaps,waterfalls=falls+1,shoreline_stones=shore+side_cascade['basin_stones'],autumn_dressing=autumn_dressing,canyon_cleanup=canyon_cleanup,side_cascade=side_cascade,decor_support=decor,runtime_density=density,source_geometry_audit=audit,export=export,elapsed_seconds=time.monotonic()-started)
    assert report['frozen_inputs_unchanged'];(HERE/'amber_winds_report.json').write_text(json.dumps(report,indent=2),encoding='utf8');print('AMBER_EXPORT_COMPLETE',json.dumps(report),flush=True)
    render_proof()

def canyon_r(x,y):return math.hypot((x-81)/26,(y-102)/19)

def clip_legacy_canyon_shells():
    """Subtract only the canyon interior from the old overlapping undersides.

    Convex polygon subtraction retains all outside triangle fragments. The
    matching continuous inward rock wall below seals the exposed cut contour.
    """
    factor=1.13;n=144
    border=[Vector((81+26*factor*math.cos(j*math.tau/n),102+19*factor*math.sin(j*math.tau/n),0))for j in range(n)]
    planes=[(a,b-a)for a,b in zip(border,border[1:]+border[:1])]
    result=dict(cut_half_axes=[26*factor,19*factor],input_faces=0,fully_removed_faces=0,intersected_faces=0,output_triangles=0,shells=0)
    def clipped(poly,a,edge,inside):
        if not poly:return []
        output=[];prev=poly[-1];sp=edge.x*(prev[0].y-a.y)-edge.y*(prev[0].x-a.x);prev_keep=sp>=-1e-8 if inside else sp<=1e-8
        for cur in poly:
            sc=edge.x*(cur[0].y-a.y)-edge.y*(cur[0].x-a.x);keep=sc>=-1e-8 if inside else sc<=1e-8
            if keep!=prev_keep:
                t=sp/(sp-sc);point=prev[0].lerp(cur[0],t);color=tuple(v+(w-v)*t for v,w in zip(prev[1],cur[1]));output.append((point,color))
            if keep:output.append(cur)
            prev,sp,prev_keep=cur,sc,keep
        return output
    for ob in list(bpy.context.scene.objects):
        if ob.type!='MESH'or ob.get('amber_bucket')!='Cliffs':continue
        data=ob.data;attr=data.color_attributes.get('Color');vv=[];ff=[];cc=[];changed=False
        def emit(poly):
            if len(poly)<3:return
            for j in range(1,len(poly)-1):
                tri=[poly[0],poly[j],poly[j+1]]
                if (tri[1][0]-tri[0][0]).cross(tri[2][0]-tri[0][0]).length<1e-7:continue
                start=len(vv);vv.extend(tuple(p)for p,c in tri);cc.extend(c for p,c in tri);ff.append((start,start+1,start+2))
        for face in data.polygons:
            result['input_faces']+=1
            poly=[(ob.matrix_world@data.vertices[data.loops[i].vertex_index].co,tuple(attr.data[i].color))for i in face.loop_indices]
            xs=[p.x for p,c in poly];ys=[p.y for p,c in poly]
            if max(xs)<81-26*factor or min(xs)>81+26*factor or max(ys)<102-19*factor or min(ys)>102+19*factor:emit(poly);continue
            if all(canyon_r(p.x,p.y)<factor-.0001 for p,c in poly):changed=True;result['fully_removed_faces']+=1;continue
            remain=poly;outside=[]
            for a,edge in planes:
                piece=clipped(remain,a,edge,False)
                if piece:outside.append(piece)
                remain=clipped(remain,a,edge,True)
                if not remain:break
            if remain:changed=True;result['intersected_faces']+=1
            for piece in outside:emit(piece)
        if changed:
            name=ob.name;bpy.data.objects.remove(ob,do_unlink=True);mesh(name+'_CanyonCut',vv,ff,'Cliffs',cc);result['shells']+=1
        result['output_triangles']+=len(ff)
    return result

def add_canyon():
    n=144;levels=[0,-.8,-2.0,-5,-11,-19,-30,-44,-56];vs=[];cc=[];fs=[]
    for row,drop in enumerate(levels):
        # Lower rings match the cylinder clipped out of the old island shells;
        # this is a continuous rock side, never a flat replacement band.
        f=1 if row==0 else 1.13
        for j in range(n):
            a=j*math.tau/n;x=81+26*math.cos(a)*f;y=102+19*math.sin(a)*f
            h=terrain_height(81+26*math.cos(a),102+19*math.sin(a))
            vs.append((x,y,h+drop+.13*math.sin(j*1.7+row)));cc.append(col(ROCKS[(j//7+row)%len(ROCKS)]))
    for row in range(len(levels)-1):
        for j in range(n):q=row*n+j;qn=row*n+(j+1)%n;fs.extend([(q,qn,qn+n),(q,qn+n,q+n)])
    mesh('Amber_OpenCascadeCanyonWalls',vs,fs,'Cliffs',cc)
    for j in range(18):
        a=j*2.39996;x=81+26*math.cos(a)*1.02;y=102+19*math.sin(a)*1.02;h=terrain_height(x,y)
        objs=scenery.make_rock((x,y,h-9-(j%4)*7),scale=2.2+(j%3)*.6,seed=31000+j,kind='basalt',palette={'rock':(.47,.46,.40)})
        for ob in objs:ob['amber_bucket']='Rocks'

exec(compile((HERE/'side_cascade.py').read_text(encoding='utf8'),str(HERE/'side_cascade.py'),'exec'))

if __name__=='__main__':
    if '--route-only'in sys.argv:write_route()
    elif '--render-only'in sys.argv:bpy.ops.wm.open_mainfile(filepath=str(HERE/'amber_winds_preview.blend'));render_proof()
    else:build()
