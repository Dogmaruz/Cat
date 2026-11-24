"""Closed curved leaves and smoothly tapered, visibly branching botanical trees."""
import math,random,bpy,numpy as np
from mathutils import Vector
TAU=math.tau
def lin(c):return c/12.92 if c<=.04045 else ((c+.055)/1.055)**2.4
class Geometry:
 def __init__(self):self.v=[];self.f=[];self.c=[];self.uv=[]
 def face(self,ids,color,uv):self.f.append(tuple(ids));self.c.extend(color if isinstance(color,list) else [color]*len(ids));self.uv.extend(uv)
 def tube(self,points,radii,color,sides=8):
  points=[Vector(q) for q in points];start=len(self.v)
  for j,(q,r) in enumerate(zip(points,radii)):
   axis=(points[min(j+1,len(points)-1)]-points[max(0,j-1)]).normalized();side=axis.cross(Vector((0,1,0)))
   if side.length<.01:side=axis.cross(Vector((1,0,0)))
   side.normalize();up=axis.cross(side).normalized()
   for k in range(sides):
    theta=k*TAU/sides;rr=r*(1+.045*math.sin(3*theta+j*.6));self.v.append(tuple(q+(side*math.cos(theta)+up*math.sin(theta))*rr))
  self.face(tuple(start+i for i in reversed(range(sides))),color,[(i/sides,0) for i in reversed(range(sides))])
  for j in range(len(points)-1):
   for k in range(sides):
    kk=(k+1)%sides;ids=(start+j*sides+k,start+j*sides+kk,start+(j+1)*sides+kk,start+(j+1)*sides+k);tone=.84+.16*(.5+.5*math.sin(k*1.5))
    self.face(ids,tuple(x*tone for x in color[:3])+(0,),[(k/sides,j/(len(points)-1)),((k+1)/sides,j/(len(points)-1)),((k+1)/sides,(j+1)/(len(points)-1)),(k/sides,(j+1)/(len(points)-1))])
  st=start+(len(points)-1)*sides;self.face(tuple(st+i for i in range(sides)),color,[(i/sides,1) for i in range(sides)])
 def leaf(self,base,direction,length,width,color,roll,bend):
  base=Vector(base);axis=Vector(direction).normalized();side=axis.cross(Vector((0,0,1)))
  if side.length<.01:side=axis.cross(Vector((1,0,0)))
  side.normalize();normal=side.cross(axis).normalized();side,normal=side*math.cos(roll)+normal*math.sin(roll),normal*math.cos(roll)-side*math.sin(roll)
  contour=((0,0),(.32,-.91),(.72,-.74),(1,0),(.72,.74),(.32,.91));start=len(self.v);coords=[]
  for t,w in contour:self.v.append(tuple(base+axis*(length*t)+side*(width*w)+normal*(bend*math.sin(t*math.pi))));coords.append((.5+w*.5,t))
  center=base+axis*(length*.51)+normal*bend;self.v.append(tuple(center+normal*width*.18));coords.append((.5,.51))
  for j in range(6):
   n=(j+1)%6
   for ids in ((n,j,6),):
    colors=[tuple(x*(.86+.14*coords[i][1]) for x in color[:3])+(coords[i][1],) for i in ids];self.face(tuple(start+i for i in ids),colors,[coords[i] for i in ids])
 def object(self,name,material):
  mesh=bpy.data.meshes.new(name);mesh.from_pydata(self.v,[],self.f);mesh.update();mesh.materials.append(material)
  attr=mesh.color_attributes.new(name='Color',type='FLOAT_COLOR',domain='CORNER');attr.data.foreach_set('color',np.asarray(self.c,np.float32).reshape(-1));uv=mesh.uv_layers.new(name='UVMap');uv.data.foreach_set('uv',np.asarray(self.uv,np.float32).reshape(-1))
  for p in mesh.polygons:p.use_smooth=True
  ob=bpy.data.objects.new(name,mesh);bpy.context.scene.collection.objects.link(ob);return ob
def make_variant(index):
 rng=random.Random(70421+index*283);bark=Geometry();leaves=Geometry();conifer=index>=4;h=(6.2,5.4,6.0,5.0,6.6,6.1)[index];spread=(1.95,2.05,1.75,1.9,1.8,2.1)[index]
 palette=((.27,.42,.15),(.33,.47,.18),(.34,.49,.21),(.24,.40,.15),(.18,.34,.21),(.22,.39,.20))[index];barkrgb=(.43,.36,.27) if index==2 else(.29,.20,.13);bc=tuple(lin(x) for x in barkrgb)+(0,)
 sway=Vector((rng.uniform(-.45,.45),rng.uniform(-.38,.38),0));trunk=[sway*(t*t)+Vector((.05*math.sin(t*8),.03*math.sin(t*11),h*t*(1 if conifer else .73))) for t in (0,.12,.25,.42,.59,.75,.89,1)]
 bark.tube(trunk,[.23*(1-i/7)**1.1+.015 for i in range(8)],bc,11)
 for i in range(5):
  theta=i*2.399+index;tip=Vector((.43*math.cos(theta),.43*math.sin(theta),-.025));bark.tube([tip,tip*.5+Vector((0,0,.13)),Vector((0,0,.40))],[.025,.07,.11],bc,6)
 branches=12 if conifer else 9
 for j in range(branches):
  theta=j*2.399963+index*.7+rng.uniform(-.25,.25)
  if conifer:t=.30+.62*j/(branches-1);reach=spread*((1-t)*1.3+.2);rise=rng.uniform(.06,.4) if index==4 else rng.uniform(.35,.85)
  else:
   crownz=.66+.34*((j*7)%9)/8;t=.35+rng.random()*.22;reach=spread*math.sqrt(max(.15,1-((crownz-.76)/.36)**2))*rng.uniform(.81,1.08);rise=h*(crownz-t)
  direction=Vector((math.cos(theta),math.sin(theta),0));side=Vector((-direction.y,direction.x,0));root=sway*t*t+Vector((0,0,h*t));tip=root+direction*reach+Vector((0,0,rise));mid=root.lerp(tip,.55)+side*rng.uniform(-.17,.17)+Vector((0,0,-.05 if conifer else .15))
  bark.tube([root,root.lerp(mid,.5),mid,mid.lerp(tip,.55),tip],[.105*(1-t*.6),.076,.049,.026,.009],bc,8)
  sprays=5 if conifer else 6
  for k in range(sprays):
   frac=.40+k*.115 if not conifer else .20+k*.18;origin=mid.lerp(tip,(frac-.5)*2) if frac>.5 else root.lerp(mid,frac*2);wing=-1 if k%2 else 1;out=(direction*rng.uniform(-.40,.65)+side*wing*rng.uniform(.45,1.05)+Vector((0,0,rng.uniform(-.40,.85) if not conifer else rng.uniform(-.12,.30)))).normalized();length=rng.uniform(.75,1.20) if not conifer else rng.uniform(.5,.95);end=origin+out*length
   bark.tube([origin,origin.lerp(end,.45)+Vector((0,0,.09)),end],[.028,.016,.0035],bc,4)
   for m in range(14 if conifer else 20):
    u=.04+.95*m/(13 if conifer else 19);base=origin.lerp(end,u)+Vector((0,0,.09*math.sin(u*math.pi)));sign=-1 if m%2 else 1;leafdir=(out*.35+Vector((-out.y,out.x,0))*sign*.9+Vector((0,0,rng.uniform(-.30,.65)))).normalized()
    lengthleaf=rng.uniform(.25,.39) if not conifer else rng.uniform(.24,.37);width=lengthleaf*(rng.uniform(.30,.40) if not conifer else .16);tone=rng.uniform(.80,1.13)*(1+.10*(base.z/h));color=tuple(lin(min(.9,x*tone)) for x in palette)+(1,)
    leaves.leaf(base,leafdir,lengthleaf,width,color,rng.uniform(-.65,.65),lengthleaf*.09)
 # An irregular small leader cluster rounds the upper silhouette without a lid.
 for j in range(5):
  theta=j*2.399+index;root=trunk[-2];tip=trunk[-1]+Vector((math.cos(theta)*.36,math.sin(theta)*.36,-rng.uniform(.08,.45)));bark.tube([root,root.lerp(tip,.5),tip],[.04,.018,.004],bc,4)
  for k in range(20):
   u=.05+k*.047;base=root.lerp(tip,u);di=Vector((math.cos(theta+k*2.4),math.sin(theta+k*2.4),rng.uniform(.1,.6)));ll=rng.uniform(.23,.36);color=tuple(lin(x*rng.uniform(.96,1.13)) for x in palette)+(1,);leaves.leaf(base,di,ll,ll*(.28 if not conifer else .12),color,rng.uniform(-.7,.7),ll*.1)
 return bark,leaves,dict(name=('field_ash','broad_oak','light_birch','young_elm','branch_spruce','open_scots_pine')[index],height=h,spread=spread,leaves=len(leaves.v)//7,leaf_shell_triangles=6)

