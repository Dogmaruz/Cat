"""Organic meadow caps and closed native-rock piles, isolated from the mainland."""
import bpy,bmesh,math,pathlib,sys,json,random,collections,numpy as np
from mathutils import Vector,Matrix,Euler
from mathutils.geometry import delaunay_2d_cdt
from mathutils.bvhtree import BVHTree
HERE=pathlib.Path(__file__).resolve().parent;sys.path.insert(0,str(HERE.parent));import build_abyss as a
import scenery
SPECS=json.loads((HERE.parent/'abyss_report.json').read_text())['islets']
def inside(p,poly):
 x,y=p;flag=False
 for aa,bb in zip(poly,poly[1:]+poly[:1]):
  if (aa[1]>y)!=(bb[1]>y) and x<(bb[0]-aa[0])*(y-aa[1])/(bb[1]-aa[1])+aa[0]:flag=not flag
 return flag
def frame(spec):
 p,n=a.road.sample(spec['distance']);return Vector(spec['center']),Vector((n.y,-n.x,0)),n
def world(spec,u,v):
 center,t,n=frame(spec);return center+t*u+n*v
def grade(spec,u,v):
 q=world(spec,u,v);return a.play(a.r.nearest_s(q.x,q.y))
def meadow(spec,u,v):
 s=spec['distance'];phase=s*.07;h=-1.65+.33*math.sin(.68*u+phase)*math.cos(.79*v)+.22*math.sin(.52*u+1.1*v+phase)
 h+=.24*math.exp(-((u+spec['radius_along']*.27)**2+(v-spec['radius_across']*.3)**2)/5)
 h=min(-1.0,h)
 # Ground the two inherited trees and tiny radio cottage on local natural pads.
 if s==420:
  for u0 in(-4.5,4.6):
   w=math.exp(-((u-u0)**2+(v-3.4)**2)/.8);h=h*(1-w)-1.01*w
 if s==478:
  w=math.exp(-((u-.3)**2+(v-3.2)**2)/1.0);h=h*(1-w)-.94*w
 z=grade(spec,u,v)+h
 if s==420:
  for u0 in(-4.5,4.6):
   w=math.exp(-((u-u0)**2+(v-3.4)**2)/.65);z=z*(1-w)+(grade(spec,u0,3.4)-1.01)*w
 if s==478 and abs(u-.3)<1.1 and abs(v-3.2)<.85:z=grade(spec,.3,3.2)-.875
 return z
def new_core(spec):
 rng=random.Random(77263+int(spec['distance']));along=spec['radius_along'];across=spec['radius_across'];phase=rng.random()*math.tau;N=40;outline=[]
 for i in range(N):
  theta=math.tau*i/N;r0=1+.12*math.sin(3*theta+phase)+.10*math.cos(5*theta-phase*.6)+.045*math.sin(9*theta)
  if spec['distance']==420:
   for u0 in(-4.5,4.6):
    direction=math.atan2(3.4/across,u0/along);delta=(theta-direction+math.pi)%math.tau-math.pi
    if abs(delta)<.32:r0=max(r0,1.10)
  outline.append((math.cos(theta)*along*r0,math.sin(theta)*across*r0))
 inputs=list(outline)
 anchors=[(u,3.4) for u in(-4.5,4.6)] if spec['distance']==420 else [(.3,3.2),(-.7,2.4),(1.3,2.4),(-.7,4),(1.3,4)] if spec['distance']==478 else []
 inputs+=anchors
 for u in np.arange(-along*1.15,along*1.15,.70):
  for v in np.arange(-across*1.15,across*1.15,.70):
   q=(float(u+rng.uniform(-.12,.12)),float(v+rng.uniform(-.12,.12)))
   if inside(q,outline) and min(math.dist(q,p) for p in outline)>.20:inputs.append(q)
 vv,ee,ff,_,_,_=delaunay_2d_cdt([Vector(p) for p in inputs],[(i,(i+1)%N) for i in range(N)],[list(range(N))],1,1e-6,False)
 verts=[];faces=[tuple(f) for f in ff];colors=[];mids=[]
 for uv in vv:q=world(spec,uv.x,uv.y);verts.append((q.x,q.y,meadow(spec,uv.x,uv.y)))
 # Recover the CDT boundary order, so the meadow and body share exact indices.
 edge_counts=collections.Counter(tuple(sorted((f[i],f[(i+1)%len(f)]))) for f in faces for i in range(len(f)));adj=collections.defaultdict(list)
 for (i,j),count in edge_counts.items():
  if count==1:adj[i].append(j);adj[j].append(i)
 start=next(iter(adj));boundary=[start];prev=None;current=start
 while True:
  nxt=next(v for v in adj[current] if v!=prev)
  if nxt==start:break
  boundary.append(nxt);prev,current=current,nxt
 # Preserve organic soil/stone margins rather than a continuous green sliced rim.
 for f in faces:
  u=sum(vv[i].x for i in f)/len(f);v=sum(vv[i].y for i in f)/len(f);rho=math.hypot(u/along,v/across);edge=rho>.80+.08*math.sin(u*2+v)
  color=(.33,.36,.24) if edge else(.28+.035*math.sin(u*.7),.42+.025*math.cos(v*.9),.20+.025*math.sin(u*.5+v))
  colors.append(color);mids.append(1 if edge and rho>.96 else 0)
 center,t,n=frame(spec);last=boundary;L=len(boundary);depth=spec['depth'];core_depth=depth-4.5;rings=[(-1.1,.96),(-3.4,.55),(-8,.32),(-14,.18),(-core_depth,.025)]
 for ri,(drop,factor) in enumerate(rings):
  ring=[]
  for j,bi in enumerate(boundary):
   u,v=vv[bi];angle=math.atan2(v,u);lean=ri*.42;uu=u*factor+lean*math.sin(phase);vvv=v*factor+lean*math.cos(phase)
   q=world(spec,uu,vvv);z=meadow(spec,uu,vvv)+drop+.23*math.sin(j*1.4+ri);ring.append(len(verts));verts.append((q.x,q.y,z))
  for j in range(L):faces.append((last[j],last[(j+1)%L],ring[(j+1)%L],ring[j]));colors.append((.32,.38,.39));mids.append(1)
  last=ring
 tip=len(verts);tipu=(len(rings)-1)*.42*math.sin(phase);tipv=(len(rings)-1)*.42*math.cos(phase);q=world(spec,tipu,tipv);verts.append((q.x,q.y,meadow(spec,tipu,tipv)-core_depth-.5))
 for j in range(L):faces.append((tip,last[j],last[(j+1)%L]));colors.append((.29,.35,.37));mids.append(1)
 materials=[a.r.add_material('RA_Emerald_Terrain','Terrain'),a.r.add_material('RA_Emerald_Cliffs','Cliffs')]
 obj=a.objmesh('RA_Polished_'+str(int(spec['distance'])),verts,faces,materials,mids);obj['unity_material_bucket']='Structural';obj['structural_closed_shell']=True;obj['polished_islet']=True
 m=obj.data;attr=m.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');rgba=[]
 for p,c in zip(m.polygons,colors):rgba.extend([tuple(a.r.cob.linear(x) for x in c)+(1,)]*len(p.loop_indices))
 attr.data.foreach_set('color',np.asarray(rgba,np.float32).reshape(-1));stats=a.topology(m);assert stats['boundary_edges']==0 and stats['nonmanifold_edges']==0,stats
 return obj
def transform_rock(objects,position,scale,angles):
 rotation=Euler(angles,'XYZ').to_matrix().to_4x4();matrix=Matrix.Translation(position)@rotation@Matrix.Diagonal((*scale,1))
 # Native rock bases are recentered before nonuniform transforms.
 base=next(o for o in objects if 'fracture_planes' in o.name);center=sum((Vector(c) for c in base.bound_box),Vector())/8
 for o in objects:o.matrix_world=matrix@Matrix.Translation(-center);o['new_islet_decoration']=True
def add_rocks(spec):
 rng=random.Random(8133+int(spec['distance']));center,t,n=frame(spec);objects=[]
 for j in range(35):
  band=j//7;theta=j*2.39996+rng.uniform(-.3,.3);factor=(.78,.65,.48,.32,.14)[band]
  u=math.cos(theta)*spec['radius_along']*factor+band*.5;v=math.sin(theta)*spec['radius_across']*factor-band*.25
  lo,hi=((2,5),(5,9),(10,15),(16,22),(23,max(25,spec['depth']+2)))[band];depth=rng.uniform(lo,hi)
  if j in(0,1):u=(-1 if j==0 else 1)*spec['radius_along']*.84;v=0;depth=3.0
  q=world(spec,u,v);q.z=grade(spec,u,v)-depth
  made=scenery.make_rock(location=(0,0,0),scale=1,kind='slate' if j%5==0 else 'boulder',palette={'rock':(.37,.42,.43),'style':'forest'},seed=int(spec['distance'])*100+j)
  keep=[]
  for o in made:
   if 'fracture_planes' in o.name:o['abyss_bucket']='Cliffs';keep.append(o)
   elif band<2 and j%3==0 and 'surface_moss' in o.name:o['abyss_bucket']='Foliage';keep.append(o)
   else:bpy.data.objects.remove(o,do_unlink=True)
  scale=(rng.uniform(1.7,2.7)*(.84**band),rng.uniform(1.4,2.2)*(.84**band),rng.uniform(3.3,5.3)*(.94**band))
  transform_rock(keep,q,scale,(rng.uniform(-1.0,1.0),rng.uniform(-1.0,1.0),rng.uniform(0,math.tau)))
  # Keep boulder tops below the meadow in the clear flight corridor.
  lower=0
  for o in keep:
   if o.get('abyss_bucket')!='Cliffs':continue
   matrix=o.matrix_world
   for vert in o.data.vertices:
    w=matrix@vert.co;s=a.r.nearest_s(w.x,w.y);p,normal=a.road.sample(s);cross=(w-p).dot(normal)
    if abs(cross)<4.2:lower=max(lower,w.z-(a.play(s)-1.4))
  if lower>0:
   for o in keep:o.location.z-=lower
  objects+=keep
 return objects
def add_meadow_props(spec,core):
 rng=random.Random(9147+int(spec['distance']));result=[];center,t,n=frame(spec)
 # Props are only on the side away from the seven-metre flight corridor.
 sign=1 if (center-a.road.sample(spec['distance'])[0]).dot(n)>0 else -1
 bpy.context.view_layer.update();bvh=BVHTree.FromObject(core,bpy.context.evaluated_depsgraph_get());placed=0
 for i in range(16):
  if placed>=7:break
  u=rng.uniform(-spec['radius_along']*.63,spec['radius_along']*.63);v=sign*rng.uniform(spec['radius_across']*.45,spec['radius_across']*.69);q=world(spec,u,v)
  s=a.r.nearest_s(q.x,q.y);p,normal=a.road.sample(s)
  if abs((q-p).dot(normal))<4.65:continue
  q.z=meadow(spec,u,v);hit,normal,_,_=bvh.ray_cast(q+Vector((0,0,4)),Vector((0,0,-1)),8)
  if hit is None or normal.z<.35 or abs(hit.z-q.z)>.35:continue
  q.z=hit.z+.015
  if placed<3:
   objects=scenery.make_rock(location=q,scale=rng.uniform(.23,.40),kind='slate' if placed==0 else 'boulder',seed=int(spec['distance'])*50+i,palette={'rock':(.40,.44,.43),'style':'forest'})
   for o in objects:o['abyss_bucket']='Cliffs' if 'fracture_planes' in o.name else 'Foliage'
  elif placed==6:
   objects=scenery.make_tree(location=q,scale=.25,kind='shrub',seed=int(spec['distance'])*70+i)
   for o in objects:o['abyss_bucket']='Foliage' if 'leaves' in o.name else 'Wood'
  else:
   objects=scenery.make_prop('wildflowers' if i%2 else 'ferns',location=q,scale=.55,seed=int(spec['distance'])*60+i,palette={'flower':(.86,.70,.36),'leaf':(.29,.44,.18)})
   for o in objects:o['abyss_bucket']='Foliage'
  for o in objects:o['new_islet_decoration']=True
  result+=objects
  placed+=1
 return result
def build(specs):
 result=[]
 for spec in specs:
  core=new_core(spec);result.append(core);result+=add_rocks(spec);result+=add_meadow_props(spec,core)
 return result
def preview(objects,spec):
 scene=bpy.context.scene
 for m in bpy.data.materials:
  if m.name.startswith('RA_Emerald'):
   bs=a.ex.node_bsdf(m);vc=m.node_tree.nodes.new('ShaderNodeVertexColor');vc.layer_name='Color';m.node_tree.links.new(vc.outputs['Color'],bs.inputs['Base Color'])
 scene.world=bpy.data.worlds.new('Meadow daylight');scene.world.use_nodes=True;bg=scene.world.node_tree.nodes.get('Background');bg.inputs['Color'].default_value=(.63,.77,.91,1);bg.inputs['Strength'].default_value=.75
 ld=bpy.data.lights.new('Meadow sun','SUN');ld.energy=2.7;ld.angle=.12;lo=bpy.data.objects.new('Meadow sun',ld);scene.collection.objects.link(lo);lo.rotation_euler=(.55,-.4,-.8)
 scene.render.engine='CYCLES';scene.cycles.samples=20;scene.cycles.use_denoising=True
 try:
  prefs=bpy.context.preferences.addons['cycles'].preferences;prefs.compute_device_type='CUDA';prefs.get_devices()
  for device in prefs.devices:device.use=device.type=='CUDA'
  scene.cycles.device='GPU'
 except:pass
 scene.render.resolution_x=1500;scene.render.resolution_y=1100;scene.render.resolution_percentage=100;scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
 center,t,n=frame(spec);target=center.copy();target.z=spec['cap_height']-8
 for name,offset,scale in [('first_islet_side',n*28+t*18+Vector((0,0,9)),58),('first_islet_top',n*5+t*3+Vector((0,0,40)),23)]:
  data=bpy.data.cameras.new(name);data.type='ORTHO';data.ortho_scale=scale;cam=bpy.data.objects.new(name,data);scene.collection.objects.link(cam)
  target2=target if name.endswith('side') else center+Vector((0,0,spec['cap_height']));cam.location=target2+offset;cam.rotation_euler=(target2-cam.location).to_track_quat('-Z','Y').to_euler();scene.camera=cam;scene.render.filepath=str(HERE/(name+'.png'));bpy.ops.render.render(write_still=True);print('FIRST_POLISH_PROOF',name,flush=True)
if __name__=='__main__':
 bpy.ops.wm.read_factory_settings(use_empty=True);objects=build([SPECS[2]]);bpy.ops.wm.save_as_mainfile(filepath=str(HERE/'first_polished_islet.blend'));preview(objects,SPECS[2])
