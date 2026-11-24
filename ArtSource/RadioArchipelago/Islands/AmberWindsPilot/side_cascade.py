"""Raised side waterfall: a visible landmark passed alongside the native route."""
_BASE_AMBER_TERRAIN_HEIGHT=terrain_height

def terrain_height(x,y):
    height=_BASE_AMBER_TERRAIN_HEIGHT(x,y)
    radius=math.hypot((x-45)/9,(y-94)/6.8)
    if radius<1.65:
        bed=11.35+3.5*smooth((radius-.80)/.72)
        height=min(height,bed+(height-bed)*smooth((radius-1.12)/.53))
    return height

def side_feature_reserved(x,y):
    return math.hypot((x-36)/14,(y-99)/15)<1 or math.hypot((x-45)/9,(y-94)/8.2)<1

def clean_old_side_feature_decor():
    removed=0;keep=[];group_objects=set()
    for entry in GROUND_DECOR:
        objects,x,y,label,nominal=entry
        if side_feature_reserved(x,y)and not any(word in label for word in('cottage','windmill')):
            for ob in objects:
                if ob.name in bpy.data.objects:group_objects.add(ob.name);bpy.data.objects.remove(ob,do_unlink=True);removed+=1
        else:keep.append(entry)
    GROUND_DECOR[:]=keep
    for ob in list(bpy.context.scene.objects):
        if ob.type!='MESH'or ob.get('amber_bucket')not in('Grass','Foliage'):continue
        if not ob.name.startswith('Amber_')or not ob.data.vertices:continue
        centre=sum((ob.matrix_world@v.co for v in ob.data.vertices),Vector())/len(ob.data.vertices)
        if side_feature_reserved(centre.x,centre.y):bpy.data.objects.remove(ob,do_unlink=True);removed+=1
    return removed

def feature_water_node(name,vv,ff,category,colors,uv,soft=True):
    ob=mesh('RA_Amber_SideCascade_'+name,vv,ff,category,colors,uv,soft);ob['amber_feature_node']=True
    return ob

def side_pool(name,cx,cy,rx,ry,height):
    n=96;rings=10;vv=[(cx,cy,height)];ff=[];uv=[(.5,.5)];cc=[col((.11,.47,.50))]
    for row in range(1,rings+1):
        for j in range(n):
            a=j*math.tau/n;r=row/rings*(1+.045*math.sin(a*5)+.022*math.cos(a*9));vv.append((cx+rx*r*math.cos(a),cy+ry*r*math.sin(a),height));uv.append((.5+.5*r*math.cos(a),.5+.5*r*math.sin(a)));cc.append(col((.12+.04*row/rings,.49+.08*row/rings,.53+.08*row/rings)))
    for j in range(n):ff.append((0,1+j,1+(j+1)%n))
    for row in range(rings-1):
        for j in range(n):a=1+row*n+j;b=1+row*n+(j+1)%n;c=b+n;d=a+n;ff.append((a,d,c,b))
    return feature_water_node(name,vv,ff,'Water',cc,uv)

def side_rock_mass(name,x,y,base,rx,ry,height,seed):
    rng=random.Random(seed);n=14;vv=[];ff=[];cc=[];phase=rng.random()*math.tau
    for row,(z,f)in enumerate([(0,.94),(.18,1),(.55,.84),(.88,.56),(.97,.29)]):
        for j in range(n):
            a=j*math.tau/n;rad=f*(1+.09*math.sin(j*2.1+phase)+.06*math.cos(j*.7+row));xx=x+rx*rad*math.cos(a)+row*.10;yy=y+ry*rad*math.sin(a)-row*.08
            vv.append((xx,yy,base+height*z+.12*math.sin(j*1.3+row)));cc.append(col(ROCKS[(seed+j//3+row)%len(ROCKS)]))
    for row in range(4):
        for j in range(n):a=row*n+j;b=row*n+(j+1)%n;c=b+n;d=a+n;ff.extend([(a,b,c),(a,c,d)])
    ff.append(tuple(reversed(range(n))));top=len(vv);vv.append((x+.3,y-.3,base+height));cc.append(col((.55,.54,.47)))
    for j in range(n):ff.append((top,4*n+j,4*n+(j+1)%n))
    mesh('Amber_SideCascade_'+name,vv,ff,'Rocks',cc)

def side_headwall():
    n=96;vv=[];ff=[];cc=[];cx,cy=36,99;rx,ry=12.6,13.1
    levels=[(13.2,1.08),(14.8,1.12),(16.4,1.05),(18.6,1.02),(19.3,1.06),(21.7,.98),(22.6,1.01),(24.9,.93),(25.7,.96),(27.5,.90)]
    for row,(height,factor)in enumerate(levels):
        for j in range(n):
            a=j*math.tau/n;r=factor*(1+.045*math.sin(a*5+.4)+.026*math.cos(a*11));x=cx+rx*r*math.cos(a);y=cy+ry*r*math.sin(a);z=height+.30*math.sin(a*4+row*.4)+.18*math.cos(a*9)
            # The flowing face stands in front of the mountain all the way to
            # its basin; broad lower flanks must never bury the lower curtain.
            front=46.4+.23*math.sin(a*5)+(.65 if row==len(levels)-1 else 0)
            x=min(x,front)
            if row==0:z=min(z,actual_ground(x,y)-.55 if actual_ground(x,y)is not None else z)
            vv.append((x,y,z));tone=ROCKS[(row//2+j//9)%len(ROCKS)];cc.append(col(tuple(v*(.91+.08*math.sin(j*.8+row))for v in tone)))
    for row in range(len(levels)-1):
        for j in range(n):a=row*n+j;b=row*n+(j+1)%n;c=b+n;d=a+n;ff.extend([(a,b,c),(a,c,d)])
    # Sculpted top bowl/stream bed, closed into the upper cliff ring.
    top=len(vv);vv.append((cx,cy,27.6));cc.append(col((.49,.49,.37)))
    for j in range(n):a=(len(levels)-1)*n+j;b=(len(levels)-1)*n+(j+1)%n;ff.append((top,a,b))
    bottom=len(vv);vv.append((cx,cy,12.5));cc.append(col((.40,.42,.38)))
    for j in range(n):ff.append((bottom,(j+1)%n,j))
    mesh('Amber_SideCascade_LayeredHeadwall',vv,ff,'Cliffs',cc,soft=False)
    # Broad interlocking weathered masses form the summit and asymmetric ledges.
    for j,(x,y,z,scale)in enumerate([(30,103,26.5,3.1),(35,107,26.4,2.5),(28,97,25.8,2.8),(39,109,25.7,2.0),(43,103,24.0,1.8),(43,89,17.0,1.8)]):
        objs=scenery.make_rock((x,y,z),scale=scale,seed=47000+j,kind='boulder',palette={'rock':(.51,.49,.43),'leaf_light':(.43,.47,.20)})
        for ob in objs:ob['amber_bucket']='Rocks'
    for j,(x,y,z,rx,ry,h)in enumerate([(31,99,26.8,5.6,4.8,8.4),(38,105,27,4.2,3.6,6.6),(27,105,26.8,4.3,3.9,7.6)]):side_rock_mass('BroadSummit'+str(j),x,y,z,rx,ry,h,53000+j)
    for j,yy in enumerate((85.7,89.2,92.1,99.9,103.0,107.0,110.4)):
        side_rock_mass('FrontRockButtress'+str(j),46.25,yy,13.3,1.15,1.35+(j%3)*.25,9.0+(j%4)*1.5,54000+j)
    for j,(x,y)in enumerate(((35,91),(39,103))):
        objs=scenery.make_tree((x,y,27.4),scale=.4,seed=55000+j,kind='shrub',palette={'leaf':(.74,.42,.095)if j==0 else(.41,.45,.21),'bark':(.34,.24,.14)})
        for ob in objs:ob['amber_bucket']='Foliage'if'leaves'in ob.name else'Wood'

def side_stream_and_fall():
    # Raised spring flows east, rolls smoothly and falls beside the player.
    points=[];distances=[]
    for value in np.linspace(0,8,33):points.append(Vector((39+value,96,28)));distances.append(float(value))
    radius=1.5
    for theta in np.linspace(0,math.pi*.5,17)[1:]:points.append(Vector((47+radius*math.sin(theta),96,28-radius*(1-math.cos(theta)))));distances.append(8+radius*theta)
    width=4.2;vv=[];ff=[];uv=[];cc=[];cols=16
    for row,(p,travel)in enumerate(zip(points,distances)):
        for j in range(cols+1):
            u=j/cols;vv.append(tuple(p+Vector((0,(u-.5)*width,0))));uv.append((u,travel));cc.append(col((.15,.55,.57)))
        if row:
            for j in range(cols):a=(row-1)*(cols+1)+j;ff.append((a,a+cols+1,a+cols+2,a+1))
    feature_water_node('RaisedStreamAndRoll',vv,ff,'Stream',cc,uv)
    end=points[-1];drop=14.5;last_distance=distances[-1]
    centres=[];flow_metres=[];travel=last_distance
    for row in range(41):
        t=row/40;point=end+Vector((.55*smooth(t*3),0,-drop*t))
        if centres:travel+=(point-centres[-1]).length
        centres.append(point);flow_metres.append(travel)
    for layer in range(3):
        vv=[];ff=[];uv=[];cc=[];rows=40;cols=24
        for row in range(rows+1):
            t=row/rows;blend=smooth(t*7);centre=end+Vector((.55*smooth(t*3)+layer*.08*blend,0,-drop*t));w=width*(1-layer*.18)*(1+.04*math.sin(t*18+layer)*blend)
            for j in range(cols+1):
                u=j/cols;pp=centre+Vector((math.sin(u*math.pi)*.16*blend,(u-.5)*w,0));vv.append(tuple(pp));uv.append((u,flow_metres[row]));cc.append(col((.22+layer*.03,.63+layer*.03,.68+layer*.03)))
            if row:
                for j in range(cols):a=(row-1)*(cols+1)+j;ff.append((a,a+cols+1,a+cols+2,a+1))
        feature_water_node('RaisedFall_Layer'+str(layer),vv,ff,'Waterfall',cc,uv)
    return dict(top=[47,96,28],curtainTop=list(end),mid=[49,96,20],base=[49.05,96,12],width=width,drop=16,flowUv='U across0..1,V continuous downstream metres through roll into curtain',rollRadius=radius)

def add_side_cascade():
    removed=clean_old_side_feature_decor();before=set(bpy.context.scene.objects);side_headwall()
    # Small hydraulic overlap avoids coplanar pool/stream flicker at its source.
    side_pool('RaisedSpringPool',40,96,3.5,2.65,27.965)
    water=side_stream_and_fall();side_pool('ReceivingBasin',45,94,7,6.2,12)
    stones=0
    for j in range(48):
        a=j*2.39996;r=1+.035*math.sin(j*1.7);x=45+7*r*math.cos(a);y=94+6.2*r*math.sin(a);h=actual_ground(x,y)
        if h is None:continue
        scale=[.45,.7,1.0,.58,1.25,.8][j%6]
        if x>48.5:scale=min(scale,.45) # Keep the camera-facing water/base visible.
        shore_rock(x,y,max(h,11.7),48000+j,scale);stones+=1
        if j%9==0:reeds(x,y,max(h,12),49000+j)
        if j%12==0:
            objs=scenery.make_tree((x,y,max(h,12)),scale=.5,seed=50000+j,kind='shrub',palette={'leaf':(.82,.37,.055),'bark':(.32,.22,.13)})
            for ob in objs:ob['amber_bucket']='Foliage'if'leaves'in ob.name else'Wood'
    # Solid wet stones define the raised spring banks and the real spill mouth.
    for j in range(18):
        angle=j*math.tau/18;x=40+3.5*math.cos(angle);y=96+2.6*math.sin(angle)
        if x>42 and abs(y-96)<2.0:continue
        shore_rock(x,y,27.45,51000+j,.35+.11*(j%4))
    for sign in(-1,1):
        for j in range(3):shore_rock(46.3-j*.8,96+sign*(2.35+.15*j),27.45,52000+j+sign,.45+.12*j)
    bpy.context.view_layer.update()
    minimum=1e9;minimum_sample=None;bounds_min=Vector((1e9,1e9,1e9));bounds_max=-bounds_min
    for ob in set(bpy.context.scene.objects)-before:
        if ob.type!='MESH':continue
        ob['amber_side_feature']=True
        for v in ob.data.vertices:
            p=ob.matrix_world@v.co;distance,station=nearest(p.x,p.y)
            if distance<minimum:minimum=distance;minimum_sample=dict(node=ob.name,point=list(p),station=station)
            for k in range(3):bounds_min[k]=min(bounds_min[k],p[k]);bounds_max[k]=max(bounds_max[k],p[k])
    assert minimum>=8.5,('Raised cascade corridor',minimum,minimum_sample)
    report=dict(id='raised_side_cascade',explicit_water_node_prefix='RA_Amber_SideCascade_',source=water,unity=dict(top=[47,28,-96],mid=[49,20,-96],base=[49.05,12,-96]),native_camera_stations=[155,170,185],basin_stones=stones,removed_local_decor_objects=removed,minimum_all_feature_vertex_route_distance=minimum,bounds_source=[list(bounds_min),list(bounds_max)],preserve_route=True)
    (HERE/'side_cascade_handoff.json').write_text(json.dumps(report,indent=2));return report
