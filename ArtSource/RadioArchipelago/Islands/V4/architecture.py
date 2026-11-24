"""Context-free, genuinely three dimensional Radio Archipelago architecture.

Public API: build_building(kind, location=(0,0,0), yaw=0, scale=1,
                         palette=None, seed=0, collection=None).
Yaw is in radians; the main door faces local -Y. All building parts share one
transform, so camera changes reveal the same modeled construction. A supplied
collection may be a bpy collection or its name. No operators are used.

Kinds may be generic (house, workshop, windmill, tower, observatory, relay) or
qualified, e.g. forest_house, adobe_tower, autumn_windmill, cloud_observatory.
Palette accepts RGB/RGBA tuples or hex colors for wall, roof, wood, stone,
metal, accent, trim, glass; style/world/id optionally select a material family.
"""

import math
import random
import bpy
from mathutils import Matrix, Vector

TAU = math.tau

_STYLES = {
    "forest": dict(wall="#987E56", roof="#28584B", wood="#55392A", stone="#647076", metal="#BA8653", accent="#AFC4AA", trim="#403D2D", glass="#243F48"),
    "adobe": dict(wall="#CE7464", roof="#A64C43", wood="#67433B", stone="#B7785A", metal="#C79B57", accent="#3F918C", trim="#E4A084", glass="#254E59"),
    "crystal": dict(wall="#55486F", roof="#8F80B5", wood="#4C3B5C", stone="#49425F", metal="#ADB6CC", accent="#A1BCD3", trim="#625780", glass="#37325C"),
    "autumn": dict(wall="#A45537", roof="#88402E", wood="#543328", stone="#8E7760", metal="#B68A4B", accent="#D7A766", trim="#6B3E2C", glass="#334755"),
    "ice": dict(wall="#A7CEDD", roof="#547FA7", wood="#526379", stone="#82B4CD", metal="#C4CEDE", accent="#CD9B57", trim="#446586", glass="#2E567A"),
    "tropical": dict(wall="#BD9855", roof="#847545", wood="#64422D", stone="#4E6662", metal="#B99150", accent="#D18748", trim="#775431", glass="#305D61"),
    "storm": dict(wall="#687A80", roof="#263D56", wood="#554D43", stone="#404A55", metal="#9D7950", accent="#A4533D", trim="#354651", glass="#233B49"),
    "farm": dict(wall="#AC4838", roof="#623C37", wood="#673C2B", stone="#AA9370", metal="#BD9456", accent="#DABF80", trim="#E0BD87", glass="#425B61"),
    "volcanic": dict(wall="#4F4C50", roof="#8D4138", wood="#4F3731", stone="#3B3D44", metal="#B3754C", accent="#CA713F", trim="#653E39", glass="#322E3B"),
    "cloud": dict(wall="#EDDFC5", roof="#A995D3", wood="#A47F77", stone="#CCBBC5", metal="#C8A56A", accent="#C3BBDD", trim="#D8BC87", glass="#6674A7"),
}
_STYLE_WORDS = {
    "forest": ("forest", "emerald", "ravine", "timber"),
    "adobe": ("adobe", "coral", "oasis", "desert"),
    "crystal": ("crystal", "amethyst", "grotto"),
    "autumn": ("autumn", "russet"),
    "ice": ("ice", "frost", "moon", "snow"),
    "tropical": ("tropical", "cascade", "bamboo", "hut"),
    "storm": ("storm", "coast", "navy"),
    "farm": ("farm", "barn", "sunflower", "terraces"),
    "volcanic": ("volcanic", "basalt", "ember"),
    "cloud": ("cloud", "observatory", "pavilion", "celestial"),
}


def _rgb(value):
    if isinstance(value, str):
        value = value.lstrip("#")
        if len(value) == 3:
            value = "".join(c * 2 for c in value)
        return tuple(int(value[i:i + 2], 16) / 255 for i in (0, 2, 4)) + (1,)
    return tuple(value[:3]) + (value[3] if len(value) > 3 else 1,)


def _linear(c):
    return c / 12.92 if c <= 0.04045 else ((c + .055) / 1.055) ** 2.4


def _material(label, color, metallic=0, roughness=.6):
    rgba = _rgb(color)
    key = "RA4_" + label + "_" + "".join(f"{round(c*255):02x}" for c in rgba[:3])
    mat = bpy.data.materials.get(key)
    if mat:
        return mat
    mat = bpy.data.materials.new(key)
    mat.diffuse_color = rgba
    mat.use_nodes = True
    node = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if node:
        base=tuple(_linear(c) for c in rgba[:3]) + (rgba[3],)
        node.inputs["Base Color"].default_value = base
        node.inputs["Metallic"].default_value = metallic
        node.inputs["Roughness"].default_value = roughness
        nodes,links=mat.node_tree.nodes,mat.node_tree.links
        coords=nodes.new('ShaderNodeTexCoord')
        noise=nodes.new('ShaderNodeTexNoise')
        noise.inputs['Scale'].default_value=4.5 if label in ('wood','trim') else 3.4
        noise.inputs['Detail'].default_value=3.0
        noise.inputs['Roughness'].default_value=.72
        if label in ('wood','trim'):
            stretch=nodes.new('ShaderNodeVectorMath');stretch.operation='MULTIPLY'
            stretch.inputs[1].default_value=(1.4,9.0,.45)
            links.new(coords.outputs['Object'],stretch.inputs[0]);links.new(stretch.outputs['Vector'],noise.inputs['Vector'])
        else:links.new(coords.outputs['Object'],noise.inputs['Vector'])
        ramp=nodes.new('ShaderNodeValToRGB')
        ramp.color_ramp.elements[0].position=.16
        ramp.color_ramp.elements[0].color=tuple(v*.68 for v in base[:3])+(1,)
        ramp.color_ramp.elements[1].position=.84
        ramp.color_ramp.elements[1].color=tuple(min(1,v*1.16+.003)for v in base[:3])+(1,)
        if metallic>.4:
            patina=ramp.color_ramp.elements.new(.25);patina.color=(.075,.13,.095,1)
        links.new(noise.outputs['Fac'],ramp.inputs[0]);links.new(ramp.outputs['Color'],node.inputs['Base Color'])
        relief=nodes.new('ShaderNodeBump');relief.inputs['Strength'].default_value=.22
        relief.inputs['Distance'].default_value=.025 if metallic>.4 else (.045 if label=='stone' else .022)
        links.new(noise.outputs['Fac'],relief.inputs['Height']);links.new(relief.outputs['Normal'],node.inputs['Normal'])
        rough=nodes.new('ShaderNodeMapRange');rough.inputs['To Min'].default_value=max(.14,roughness-.12);rough.inputs['To Max'].default_value=min(.94,roughness+.15)
        links.new(noise.outputs['Fac'],rough.inputs['Value']);links.new(rough.outputs[0],node.inputs['Roughness'])
        if label=='warm_glass':
            if 'Emission Color'in node.inputs:node.inputs['Emission Color'].default_value=base
            if 'Emission Strength'in node.inputs:node.inputs['Emission Strength'].default_value=.18
            rough.inputs['To Min'].default_value=.09;rough.inputs['To Max'].default_value=.23
            relief.inputs['Distance'].default_value=.002
            if 'Coat Weight'in node.inputs:node.inputs['Coat Weight'].default_value=.45
    return mat


class _Builder:
    def __init__(self, kind, palette, seed, collection):
        self.kind = kind.lower()
        self.rng = random.Random(seed)
        palette = palette or {}
        words = self.kind + " " + " ".join(str(palette.get(k, "")) for k in ("style", "world", "id"))
        self.style = next((k for k, aliases in _STYLE_WORDS.items() if any(a in words for a in aliases)), "forest")
        colors = dict(_STYLES[self.style])
        colors.update({k: v for k, v in palette.items() if k in colors})
        self.materials = {k: _material(k, v, .68 if k == "metal" else (.18 if k == "roof" else 0), .32 if k == "metal" else .61) for k, v in colors.items()}
        self.materials["dark"] = _material("recess", "#202932", .05, .74)
        self.materials["warm"] = _material("warm_glass", "#CDA76B", .1, .38)
        self.materials["cloth"] = _material("sail_cloth", colors["accent"], 0, .86)
        roofcolor=_rgb(colors['roof'])
        self.materials['rooflight']=_material('rooflight',tuple(min(1,c*1.16+.025)for c in roofcolor[:3]),.14,.64)
        self.materials['roofdark']=_material('roofdark',tuple(c*.82 for c in roofcolor[:3]),.14,.66)
        self.materials['stonewarm']=_material('stonewarm',tuple(min(1,c*1.12+.025)for c in _rgb(colors['stone'])[:3]),0,.83)
        self.objects = []
        self.serial = 0
        self.prefix = "RA4_" + self.kind + f"_{seed}_"
        if isinstance(collection, str):
            collection = bpy.data.collections.get(collection) or bpy.data.collections.new(collection)
            if not collection.users:
                bpy.context.scene.collection.children.link(collection)
        self.collection = collection or bpy.context.scene.collection

    def mesh(self, role, verts, faces, mat="wall", bevel=0, smooth=False):
        self.serial += 1
        name = self.prefix + f"{self.serial:03d}_" + role
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.update()
        obj = bpy.data.objects.new(name, mesh)
        self.collection.objects.link(obj)
        mesh.materials.append(self.materials[mat])
        if smooth:
            for p in mesh.polygons:
                p.use_smooth = True
        if bevel:
            mod = obj.modifiers.new("Soft handcrafted edges", "BEVEL")
            mod.width = bevel
            mod.segments = 3
        self.objects.append(obj)
        return obj

    def box(self, role, center, size, mat="wall", bevel=.08, rotation=0):
        x, y, z = center
        w, d, h = [v / 2 for v in size]
        c, s = math.cos(rotation), math.sin(rotation)
        verts = [(x + c*a-s*b, y+s*a+c*b, z+e) for a, b, e in ((-w,-d,-h),(w,-d,-h),(w,d,-h),(-w,d,-h),(-w,-d,h),(w,-d,h),(w,d,h),(-w,d,h))]
        return self.mesh(role, verts, [(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)], mat, bevel)

    def cylinder(self, role, a, b, radius, mat="metal", r2=None, n=20, bevel=.035):
        a, b = Vector(a), Vector(b)
        axis = (b - a).normalized()
        ref = Vector((0,0,1)) if abs(axis.z) < .9 else Vector((0,1,0))
        u = axis.cross(ref).normalized()
        v = axis.cross(u).normalized()
        r2 = radius if r2 is None else r2
        verts = [tuple(p + r*(math.cos(i*TAU/n)*u + math.sin(i*TAU/n)*v)) for p,r in ((a,radius),(b,r2)) for i in range(n)]
        faces = [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))] + [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        ob = self.mesh(role, verts, faces, mat, bevel)
        for face in ob.data.polygons[2:]:
            face.use_smooth = True
        return ob

    def beam(self, role, a, b, width=.16, mat="wood"):
        return self.cylinder(role, a, b, width, mat, n=8, bevel=.025)

    def torus(self, role, center, radius, tube=.1, mat="metal", normal=(0,0,1), n=32, m=8):
        c, axis = Vector(center), Vector(normal).normalized()
        ref = Vector((0,0,1)) if abs(axis.z) < .9 else Vector((0,1,0))
        u = axis.cross(ref).normalized()
        v = axis.cross(u).normalized()
        verts = []
        for i in range(n):
            radial = math.cos(i*TAU/n)*u + math.sin(i*TAU/n)*v
            for j in range(m):
                verts.append(tuple(c + (radius+tube*math.cos(j*TAU/m))*radial + tube*math.sin(j*TAU/m)*axis))
        faces = [(i*m+j,((i+1)%n)*m+j,((i+1)%n)*m+(j+1)%m,i*m+(j+1)%m) for i in range(n) for j in range(m)]
        return self.mesh(role, verts, faces, mat, smooth=True)

    def prism(self, role, outline, y0, y1, mat="wall", bevel=.045):
        verts = [(x,y,z) for y in (y0,y1) for x,z in outline]
        n = len(outline)
        faces = [tuple(range(n-1,-1,-1)),tuple(range(n,2*n))] + [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
        return self.mesh(role, verts, faces, mat, bevel)

    def arch(self, role, x, y, z, width, height, depth, mat="wood", rim=0):
        # U-shaped trim has a real opening; back panels use a solid arch prism.
        r = width/2
        spring = z+height-r
        points = [(x-r,z),(x-r,spring)] + [(x+r*math.cos(math.pi-i*math.pi/16),spring+r*math.sin(math.pi-i*math.pi/16)) for i in range(1,17)] + [(x+r,z)]
        if not rim:
            return self.prism(role, points, y-depth/2,y+depth/2,mat,.035)
        ro = r+rim
        outer = [(x-ro,z),(x-ro,spring)] + [(x+ro*math.cos(math.pi-i*math.pi/16),spring+ro*math.sin(math.pi-i*math.pi/16)) for i in range(1,17)] + [(x+ro,z)]
        n = len(points)
        verts = [(a,yy,b) for yy in (y-depth/2,y+depth/2) for ring in (points,outer) for a,b in ring]
        faces = []
        for k in range(n-1):
            for base in (0,2*n):
                faces.append((base+k,base+k+1,base+n+k+1,base+n+k))
            faces.append((k,k+1,2*n+k+1,2*n+k))
            faces.append((n+k,n+k+1,3*n+k+1,3*n+k))
        faces.extend([(0,n,3*n,2*n),(n-1,2*n-1,4*n-1,3*n-1)])
        return self.mesh(role,verts,faces,mat,.035)

    def speaker(self, x, y, z, radius=.65, side=False):
        normal = (1,0,0) if side else (0,-1,0)
        center = Vector((x,y,z))
        axis = Vector(normal)
        self.cylinder("speaker recessed cone", center-axis*.1,center+axis*.02,radius*.87,"dark",r2=radius*.75,n=24)
        self.torus("speaker cast lip",center+axis*.08,radius,.105,"metal",normal)
        self.torus("speaker inner ring",center+axis*.12,radius*.73,.038,"trim",normal)
        for i in range(-2,3):
            offset = i*radius*.25
            half = math.sqrt(max(0,(radius*.78)**2-offset**2))
            a,b = ((x+.13,y-half,z+offset),(x+.13,y+half,z+offset)) if side else ((x-half,y-.13,z+offset),(x+half,y-.13,z+offset))
            self.beam("speaker grille bar",a,b,.038,"metal")
        self.cylinder("speaker dust cap",center+axis*.09,center+axis*.15,radius*.2,"metal",n=16)

    def antenna(self, x, y, z, size=1):
        self.cylinder("aerial ceramic base",(x,y,z),(x,y,z+.35*size),.3*size,"stone")
        self.cylinder("aerial thick mast",(x,y,z+.2*size),(x,y,z+1.55*size),.09*size,"metal")
        for sign in (-1,1):
            self.beam("aerial fork",(x,y,z+1.12*size),(x+sign*.57*size,y,z+1.75*size),.075*size,"metal")
            self.cylinder("aerial terminal",(x+sign*.57*size,y,z+1.67*size),(x+sign*.57*size,y,z+1.87*size),.13*size,"accent")

    def pipe(self, x, y, z, size=1):
        self.cylinder("copper exhaust riser",(x,y,z),(x,y,z+1.4*size),.22*size,"metal")
        self.cylinder("exhaust broad collar",(x,y,z+.75*size),(x,y,z+1.0*size),.32*size,"metal")
        self.cylinder("exhaust flared throat",(x,y,z+1.35*size),(x,y,z+1.55*size),.23*size,"metal",r2=.38*size)
        self.cylinder("exhaust dark opening",(x,y,z+1.555*size),(x,y,z+1.57*size),.285*size,"dark",bevel=0)
        self.torus("exhaust rolled lip",(x,y,z+1.57*size),.31*size,.065*size,"metal")

    @staticmethod
    def _roof_z(x,width,eave,ridge):
        t=min(1,abs(x)/(width/2))
        return ridge-(ridge-eave)*t**.78+.18*t**8

    def gable_roof(self, width, depth, eave, ridge, mat="roof", timber=True):
        w,d=width/2,depth/2
        profile=[(-w+width*j/32,self._roof_z(-w+width*j/32,width,eave,ridge))for j in range(33)]
        self.prism('sculpted sweeping roof shell',profile+[(x,z-.17)for x,z in reversed(profile)],-d,d,mat,.035)
        verts=[];faces=[];tilemats=[]
        rows=9;cols=max(8,round(depth/.48))
        for side in (-1,1):
            for row in range(rows):
                t0=row/rows;t1=min(1,(row+1.13)/rows)
                x0=side*w*t0;x1=side*w*t1
                stagger=(row%2)*.5
                for col in range(-1,cols+1):
                    y0=max(-d,-d+(col+stagger)*depth/cols+.018)
                    y1=min(d,-d+(col+1+stagger)*depth/cols-.018)
                    if y1-y0<.12:continue
                    lift=self.rng.uniform(.035,.07)
                    z0=self._roof_z(x0,width,eave,ridge)+lift
                    z1=self._roof_z(x1,width,eave,ridge)+lift+.06
                    base=len(verts)
                    verts.extend(((x0,y0,z0),(x1,y0,z1),(x1,y1,z1),(x0,y1,z0),(x0,y0,z0-.07),(x1,y0,z1-.07),(x1,y1,z1-.07),(x0,y1,z0-.07)))
                    tilefaces=((0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0))
                    faces.extend(tuple(base+i for i in (f if side>0 else reversed(f)))for f in tilefaces)
                    tilemats.extend([self.rng.choices((0,1,2),(.7,.18,.12))[0]]*6)
        ob=self.mesh('individually overlapped handmade roof tiles',verts,faces,mat,.025)
        ob.data.materials.append(self.materials['rooflight']);ob.data.materials.append(self.materials['roofdark'])
        for p,mi in zip(ob.data.polygons,tilemats):p.material_index=mi
        self.cylinder('copper capped ridge roll',(0,-d-.05,ridge+.07),(0,d+.05,ridge+.07),.14,'metal',n=16)
        for yy in (-d,d):
            for a,b in zip(profile[:-1],profile[1:]):self.beam('swept carved fascia',(a[0],yy,a[1]-.035),(b[0],yy,b[1]-.035),.10,'wood'if timber else'metal')
        for sign in (-1,1):
            zz=self._roof_z(sign*w,width,eave,ridge)
            self.beam('rounded deep roof eave',(sign*w,-d-.05,zz-.13),(sign*w,d+.05,zz-.13),.15,'wood'if timber else'trim')
            for yy in (-d,d):
                self.torus('carved eave copper curl',(sign*(w-.12),yy,zz-.25),.18,.048,'metal',(0,1,0),n=20)

    def window(self,x,y,z,width=1.45,height=1.72,side=False,shutters=True):
        start=len(self.objects)
        self.arch('deep arched window cavity',0,-.02,z,width,height,.12,'dark')
        self.arch('warm recessed window glazing',0,-.11,z+.09,width-.21,height-.19,.055,'warm')
        self.arch('carved arched window frame',0,-.17,z,width,height,.15,'wood',.095)
        self.arch('window fine brass bead',0,-.267,z+.06,width-.09,height-.12,.04,'metal',.025)
        for dx in (-width*.23,0,width*.23):
            self.box('window vertical timber mullion',(dx,-.22,z+height*.44),(.045,.07,height*.74),'wood',.018)
        self.box('window horizontal timber mullion',(0,-.23,z+height*.4),(width-.1,.075,.055),'wood',.017)
        self.box('projecting carved window sill',(0,-.22,z-.065),(width+.34,.44,.16),'wood',.055)
        if shutters:
            for sign in (-1,1):
                for j in range(3):
                    xx=sign*(width/2+.12+j*.105)
                    self.box('hand cut hinged shutter plank',(xx,-.10,z+height*.39),(.098,.12,height*.78),'trim',.024)
                for zz in (z+.17,z+height*.66):
                    self.box('shutter metal hinge strap',(sign*(width/2+.22),-.18,zz),(.36,.065,.055),'metal',.018)
        transform=Matrix.Translation((x,y,0))@Matrix.Rotation(math.pi/2 if side else 0,4,'Z')
        for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world

    def masonry(self,w,d):
        # Real staggered blocks provide the broad dressed-stone base silhouette.
        for course in range(3):
            zz=.12+course*.19
            for yy in (-d/2-.08,d/2+.08):
                count=9
                for i in range(count):
                    xx=-w/2+(i+.5+(course%2)*.28)*w/count
                    if xx>w/2-.06:continue
                    self.box('staggered dressed stone plinth',(xx,yy,zz),(w/count-.035,.32,.19),'stonewarm'if(i+course)%4==0 else'stone',.045)
            for xx in (-w/2-.08,w/2+.08):
                for i in range(7):
                    yy=-d/2+(i+.5)*d/7
                    self.box('corner returning stone plinth',(xx,yy,zz),(.32,d/7-.035,.19),'stone',.045)

    def copper_chamber(self,x,y,z,size=1):
        r=.48*size
        self.cylinder('ornate copper pressure chamber',(x,y,z),(x,y,z+1.75*size),r,'metal',n=24)
        self.cylinder('rounded chamber crown',(x,y,z+1.73*size),(x,y,z+2.06*size),r,'metal',r2=.24*size,n=24)
        for dz in (.17,.76,1.50):self.torus('chamber heavy brass compression ring',(x,y,z+dz*size),r+.035*size,.065*size,'metal')
        for i in range(6):
            a=i*TAU/6
            self.beam('chamber curved rib',(x+(r+.035)*math.cos(a),y+(r+.035)*math.sin(a),z+.15*size),(x+(r+.035)*math.cos(a),y+(r+.035)*math.sin(a),z+1.55*size),.045*size,'trim')
        self.cylinder('pressure gauge housing',(x,y-r,z+1.08*size),(x,y-r-.12*size,z+1.08*size),.19*size,'metal')
        self.cylinder('ivory gauge face',(x,y-r-.125*size,z+1.08*size),(x,y-r-.14*size,z+1.08*size),.15*size,'stonewarm')
        self.beam('gauge needle',(x,y-r-.16*size,z+1.08*size),(x+.08*size,y-r-.16*size,z+1.16*size),.016*size,'dark')

    def dome(self,rx,ry,z,height):
        n=64;rows=12;verts=[];faces=[]
        for row in range(rows+1):
            phi=row/rows*math.pi/2
            for j in range(n):
                a=j*TAU/n
                verts.append((rx*math.cos(phi)*math.cos(a),ry*math.cos(phi)*math.sin(a),z+height*math.sin(phi)))
        for row in range(rows):
            for j in range(n):faces.append((row*n+j,row*n+(j+1)%n,(row+1)*n+(j+1)%n,(row+1)*n+j))
        self.mesh('continuous sculpted lilac dome shell',verts,faces,'roof',smooth=True)
        verts=[];faces=[];choices=[]
        for row in range(6):
            p0=row/6*math.pi/2+.007;p1=(row+1)/6*math.pi/2-.009
            count=max(12,round(34*math.cos((p0+p1)/2)))
            for j in range(count):
                a0=(j+(row%2)*.5)*TAU/count+.008;a1=(j+1+(row%2)*.5)*TAU/count-.008
                patch=[]
                for radd,zadd in ((.035,.025),(-.025,-.025)):
                    for a,p in ((a0,p0),(a1,p0),(a1,p1),(a0,p1)):
                        patch.append(((rx+radd)*math.cos(p)*math.cos(a),(ry+radd)*math.cos(p)*math.sin(a),z+height*math.sin(p)+zadd))
                base=len(verts);verts.extend(patch)
                faces.extend(tuple(base+i for i in f)for f in ((0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)))
                choices.extend([self.rng.choices((0,1,2),(.65,.25,.1))[0]]*6)
        ob=self.mesh('large individually seamed lilac dome panels',verts,faces,'roof',.018)
        ob.data.materials.append(self.materials['rooflight']);ob.data.materials.append(self.materials['roofdark'])
        for p,mi in zip(ob.data.polygons,choices):p.material_index=mi
        for j in range(48):
            a=j*TAU/48;b=(j+1)*TAU/48
            self.beam('gold dome rolled cornice',(rx*math.cos(a),ry*math.sin(a),z),(rx*math.cos(b),ry*math.sin(b),z),.10,'metal')

    def cloud_house(self,workshop=False):
        rx,ry=(3.18,2.5)if workshop else(2.72,2.62)
        h=3.85
        # The cylindrical plaster wall is open through its south doorway.
        n=64;verts=[];faces=[]
        for z in(.55,3.22,h):
            verts.extend((rx*math.cos(j*TAU/n),ry*math.sin(j*TAU/n),z)for j in range(n))
        for j in range(n):
            a=(j+.5)*TAU/n
            front=abs(((a+math.pi/2+math.pi)%TAU)-math.pi)<.29
            if not front:faces.append((j,(j+1)%n,n+(j+1)%n,n+j))
            faces.append((n+j,n+(j+1)%n,2*n+(j+1)%n,2*n+j))
        self.mesh('rounded cream plaster walls with real entry opening',verts,faces,'wall',.065,smooth=True)
        for z,rad,height,material in ((.10,1.10,.20,'stone'),(.30,1.08,.20,'stonewarm'),(.50,1.04,.20,'trim')):
            obj=self.cylinder('round stepped carved foundation',(0,0,z-height/2),(0,0,z+height/2),rx*rad,material,n=64,bevel=.065)
            obj.scale.y=ry/rx
        # Individual large radial masonry blocks around the plinth.
        for j in range(24):
            a=j*TAU/24;b=(j+1)*TAU/24-.025
            outer=[((rx+.21)*math.cos(t),(ry+.21)*math.sin(t),z)for z in(.18,.43)for t in(a,b)]
            inner=[((rx-.08)*math.cos(t),(ry-.08)*math.sin(t),z)for z in(.18,.43)for t in(a,b)]
            self.mesh('radial dressed cloudstone foundation block',outer+inner,[(0,1,3,2),(4,6,7,5),(0,4,5,1),(2,3,7,6),(0,2,6,4),(1,5,7,3)],'stonewarm',.04)
        self.arch('cloud cottage inset arched oak door',0,-ry-.045,.55,1.45,2.48,.1,'wood')
        self.arch('cloud cottage sculpted door arch',0,-ry-.14,.52,1.53,2.54,.21,'metal',.15)
        self.arch('cloud door inner ivory bead',0,-ry-.27,.58,1.30,2.37,.055,'trim',.035)
        for j in range(6):self.box('cloud arched door vertical plank',((j-2.5)*.18,-ry-.12,1.50),(.014,.025,1.55),'trim',.005)
        self.torus('cloud door brass pull',(.39,-ry-.24,1.65),.11,.035,'metal',(0,-1,0))
        # A genuine curved arch masonry lintel closes the wall above the doorway.
        r=.78;spring=2.30
        arc=[(r*math.cos(math.pi-j*math.pi/20),spring+r*math.sin(math.pi-j*math.pi/20))for j in range(21)]
        self.prism('plaster arched entry lintel',[(-r,3.28),(r,3.28)]+list(reversed(arc)),-ry-.04,-ry+.2,'wall',.055)
        for sign in(-1,1):
            angle=-math.pi/2+sign*.84
            x,y=rx*math.cos(angle),ry*math.sin(angle)
            start=len(self.objects);self.window(0,0,1.26,1.26,1.76,shutters=False)
            transform=Matrix.Translation((x,y,0))@Matrix.Rotation(sign*.84,4,'Z')
            for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world
        for sign in(-1,1):
            angle=math.pi/2+sign*.72
            x,y=rx*math.cos(angle),ry*math.sin(angle)
            start=len(self.objects);self.window(0,0,1.26,1.19,1.72,shutters=False)
            transform=Matrix.Translation((x,y,0))@Matrix.Rotation(angle+math.pi/2,4,'Z')
            for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world
        # Rounded ceramic cornices below a real segmented dome.
        for zz in(3.55,3.87):
            ob=self.cylinder('rounded plaster dome cornice',(0,0,zz),(0,0,zz+.17),rx+.13,'wall',n=64,bevel=.08);ob.scale.y=ry/rx
        self.dome(rx+.22,ry+.22,4.02,1.77)
        self.cylinder('dome copper finial cup',(0,0,5.73),(0,0,6.10),.24,'metal',r2=.11,n=24)
        for j in range(3):self.torus('dome finial calibration rings',(0,0,5.83+j*.12),.22-j*.04,.032,'metal')
        for sign in(-1,1):
            self.copper_chamber(sign*(rx+.5),.28,.55,.94)
            self.cylinder('cloud mechanism rising service pipe',(sign*(rx+.5),.28,2.42),(sign*(rx+.5),.28,3.22),.12,'metal')
            self.beam('cloud curved service bridge',(sign*(rx+.5),.28,3.22),(sign*(rx-.25),.28,3.75),.12,'metal')
        self.dish(1.05,.87,5.22,.68)
        self.antenna(-1.37,1.10,5.09,1.08)
        for zz in(5.65,6.07,6.52):self.torus('celestial aerial induction rings',(-1.37,1.10,zz),.34,.055,'metal')
        # Large brass gear on the return mechanism, with explicit modeled teeth.
        gc=(rx+.42,-.32,2.98)
        self.torus('celestial drive gear hoop',gc,.49,.12,'metal',(0,-1,0))
        for j in range(14):
            a=j*TAU/14
            self.box('celestial gear tooth',(gc[0]+.55*math.cos(a),gc[1],gc[2]+.55*math.sin(a)),(.16,.20,.17),'metal',.025)
        self.cylinder('celestial gear axle',(gc[0],gc[1]-.14,gc[2]),(gc[0],gc[1]+.17,gc[2]),.15,'metal')
        for j in range(3):self.box('cloud cottage broad entrance step',(0,-ry-.38-j*.35,.36-j*.12),(1.95,.58,.24),'stonewarm',.065)
        return rx*2,ry*2,7.25

    def house(self, workshop=False):
        style=self.style
        if style=='cloud':
            return self.cloud_house(workshop)
        flat=style=="adobe"
        w,d,h=(7.1,5.0,3.35) if workshop else (5.9,4.7,3.75)
        if style=="ice": h-=.45
        y=-d/2
        doorx=-.68 if workshop else -.9
        doorw,doorh=1.37,2.55
        self.box("broad dressed foundation",(0,0,.22),(w+.48,d+.42,.44),"stone",.15)
        self.masonry(w+.15,d+.15)
        self.box("lower wall plinth",(0,0,.57),(w+.12,d+.12,.36),"trim",.09)
        self.box("back rounded wall",(0,d/2-.16,h/2+.35),(w,.33,h),"wall",.17)
        self.box("left rounded wall",(-w/2+.16,0,h/2+.35),(.33,d,h),"wall",.16)
        self.box("right rounded wall",(w/2-.16,0,h/2+.35),(.33,d,h),"wall",.16)
        # Front masonry built around an arched void instead of painting a door on a block.
        xl,xr=doorx-doorw/2,doorx+doorw/2
        spring=.48+doorh-doorw/2
        self.box("front left arch pier",((-w/2+xl)/2,y+.16,h/2+.35),(xl+w/2,.33,h),"wall",.11)
        self.box("front right arch pier",((w/2+xr)/2,y+.16,h/2+.35),(w/2-xr,.33,h),"wall",.11)
        archline=[(doorx+doorw/2*math.cos(math.pi-i*math.pi/20),spring+doorw/2*math.sin(math.pi-i*math.pi/20)) for i in range(21)]
        self.prism("curved arch masonry lintel",[(xl,h+.35),(xr,h+.35)]+list(reversed(archline)),y,y+.33,"wall",.035)
        self.arch("shadow in open doorway",doorx,y+.2,.46,doorw,doorh,.08,"dark")
        self.arch("deep arched timber door",doorx,y+.14,.49,doorw-.18,doorh-.12,.075,"wood")
        self.arch("continuous carved door surround",doorx,y-.10,.44,doorw+.03,doorh+.02,.19,"metal" if style in ("ice","cloud","crystal") else "trim",.14)
        for j in range(5):
            xx=doorx+(j-2)*.21
            self.box("door plank groove",(xx,y+.092,1.24),(.022,.026,1.45),"trim",.009)
        self.torus("door brass ring handle",(doorx+.37,y+.033,1.45),.11,.035,"metal",(0,-1,0),n=20)
        self.box("single doorstep",(doorx,y-.24,.43),(1.9,.75,.2),"stone",.08)
        self.window(1.32 if workshop else 1.22,y-.045,1.28,1.48,1.94)
        self.speaker(w/2+.035,.55,2.1,.57,side=True)
        self.window(w/2+.04,-1.03,1.3,1.15,1.72,side=True,shutters=False)
        # Full reverse elevations are designed with the same care as the front.
        # These inset frames remain inside the existing roof overhang footprint.
        start=len(self.objects)
        self.window(0,0,1.23,1.62,1.94,shutters=True)
        transform=Matrix.Translation((-w/2+.025,-.05,0))@Matrix.Rotation(-math.pi/2,4,'Z')
        for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world
        for xx in(-1.34,1.18):
            start=len(self.objects);self.window(0,0,1.25,1.14,1.79,shutters=True)
            transform=Matrix.Translation((xx,d/2-.025,0))@Matrix.Rotation(math.pi,4,'Z')
            for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world
        # Corner joinery and horizontal belts give the silhouette readable carpentry.
        if style in ("forest","autumn","tropical","farm","storm"):
            for xx in (-w/2+.1,w/2-.1):
                for yy in (-d/2+.1,d/2-.1):
                    self.box("heavy corner timber",(xx,yy,h/2+.37),(.23,.23,h+.12),"wood",.06)
            for zz in (1.2,2.0,2.8):
                self.box("side timber siding",(w/2+.018,0,zz),(.065,d-.1,.065),"trim",.024)
                self.box("back timber siding",(0,d/2+.018,zz),(w-.1,.065,.065),"trim",.024)
            # Deep joinery and many long wall planks read at island scale.
            for row in range(11):
                zz=.78+row*.275
                for a,b in ((-w/2+.2,xl-.04),(xr+.04,w/2-.2)):
                    if b>a:self.box('long timber facade weatherboard',((a+b)/2,y-.018,zz),(b-a,.115,.25),'wall',.035)
                self.box('long side weatherboard',(w/2+.018,0,zz),(.105,d-.3,.25),'wall',.035)
                self.box('long reverse weatherboard',(0,d/2+.018,zz),(w-.3,.105,.25),'wall',.035)
                self.box('long left weatherboard',(-w/2-.018,0,zz),(.105,d-.3,.25),'wall',.035)
            for sign in (-1,1):
                self.beam('front diagonal timber brace',(sign*(w/2-.16),y-.12,3.5),(sign*(w/2-.95),y-.12,2.75),.095,'wood')
                self.beam('side diagonal timber brace',(w/2+.12,sign*(d/2-.2),3.5),(w/2+.12,sign*(d/2-1.0),2.75),.095,'wood')
                self.beam('reverse diagonal timber brace',(sign*(w/2-.16),d/2+.12,3.5),(sign*(w/2-.95),d/2+.12,2.75),.095,'wood')
                self.beam('left diagonal timber brace',(-w/2-.12,sign*(d/2-.2),3.5),(-w/2-.12,sign*(d/2-1.0),2.75),.095,'wood')
            # A substantial little cordwood stack gives the rear service wall
            # an inhabited silhouette without extending past the main eave.
            for row in range(2):
                for j in range(3-row):
                    xx=1.30+j*.29+row*.145
                    zz=.72+row*.22
                    yy=d/2+.23
                    self.cylinder('rear stacked split firewood',(xx-.32,yy,zz),(xx+.32,yy,zz),.115,'wood',n=10,bevel=.015)
                    self.torus('visible firewood end grain',(xx+.326,yy,zz),.067,.012,'wall',(1,0,0),n=16,m=6)
        if flat:
            self.box("stepped adobe roof slab",(0,0,h+.51),(w+.3,d+.3,.37),"roof",.16)
            for xx in (-w/2,w/2):
                self.box("rounded low parapet",(xx,0,h+.86),(.28,d+.28,.55),"wall",.13)
            self.box("rear roof parapet",(0,d/2,h+.86),(w,.28,.55),"wall",.13)
            self.box("second adobe roof volume",(-1.55,.6,h+.97),(2.3,2.25,.6),"wall",.18)
            self.box("second red roof cap",(-1.55,.6,h+1.33),(2.55,2.5,.21),"roof",.1)
            top=h+.75
        else:
            ridge=h+(2.25 if workshop else 2.65)
            if style=="tropical": ridge+=.55
            # Front/rear gables are real masonry volumes under the pitched roof.
            for yy in (-d/2,d/2-.18):
                profile=[(-w/2+w*j/20,self._roof_z(-w/2+w*j/20,w+.7,h+.35,ridge)-.12)for j in range(21)]
                self.prism("modeled curved gable infill",profile+[(w/2,h+.29),(-w/2,h+.29)],yy,yy+.18,"wood" if style in ("forest","autumn","farm","tropical") else "wall",.04)
            self.gable_roof(w+.7,d+.65,h+.35,ridge,"roof",style not in ("ice","crystal","cloud"))
            self.window(0,-d/2-.12,h+.43,.92,1.12,shutters=False)
            start=len(self.objects);self.window(0,0,h+.43,.92,1.12,shutters=False)
            transform=Matrix.Translation((0,d/2-.025,0))@Matrix.Rotation(math.pi,4,'Z')
            for obj in self.objects[start:]:obj.matrix_world=transform@obj.matrix_world
            self.beam('kingpost timber gable brace',(0,-d/2-.21,h+.23),(0,-d/2-.21,ridge-.22),.08,'wood')
            for sign in(-1,1):self.beam('gable radiating timber brace',(0,-d/2-.2,h+.27),(sign*w*.32,-d/2-.2,h+.81),.075,'wood')
            for sign in(-1,1):self.beam('rear gable radiating timber brace',(0,d/2+.13,h+.27),(sign*w*.32,d/2+.13,h+.81),.075,'wood')
            if style=="tropical":
                # Bundled thatch ribs follow the slope, with a thick scalloped fringe.
                for j in range(18):
                    yy=-d/2-.28+j*(d+.56)/17
                    for sign in (-1,1):
                        self.beam("broad thatch bundle",(sign*.1,yy,ridge+.08),(sign*(w/2+.42),yy,h+.2),.07,"accent")
            if style=="ice":
                for sign in (-1,1):
                    self.beam("rounded snow eave",(sign*(w/2+.24),-d/2-.28,h+.4),(sign*(w/2+.24),d/2+.28,h+.4),.17,"wall")
            top=ridge
        aerialx=0.0 if not flat else -1.15
        aerialy=.92 if not flat else .52
        aerialz=(top-.08)if not flat else(h+1.45)
        self.antenna(aerialx,aerialy,aerialz,.8)
        self.pipe(w/2-.65,d/2-.75,h+.45,.88 if workshop else .75)
        # External elbow/valve package belongs to the building footprint.
        self.cylinder("copper side service pipe",(w/2+.19,.92,.8),(w/2+.19,.92,1.65),.11,"metal")
        self.cylinder("copper service elbow",(w/2+.18,.92,.8),(w/2+.18,-.35,.8),.11,"metal")
        self.torus("service valve wheel",(w/2+.32,.65,1.31),.22,.044,"metal",(1,0,0),n=24)
        for i in range(4):
            a=i*math.pi/2
            self.beam("valve spoke",(w/2+.32,.65,1.31),(w/2+.32,.65+.2*math.cos(a),1.31+.2*math.sin(a)),.025,"metal")
        # A genuine braced entrance porch and broad stone steps ground the home.
        self.box('timber porch deck',(doorx,y-.87,.48),(2.25,1.65,.22),'wood',.065)
        for j in range(8):self.box('individual porch floor plank',(doorx-.98+j*.28,y-.87,.61),(.25,1.62,.07),'wood',.022)
        for j in range(3):self.box('broad dressed entrance step',(doorx,y-1.76-j*.28,.36-j*.12),(1.84,.47,.24),'stonewarm',.055)
        for sign in(-1,1):
            xx=doorx+sign*.98
            self.box('porch heavy timber post',(xx,y-1.45,1.78),(.17,.17,2.34),'wood',.04)
            self.beam('porch ornamental knee brace',(xx,y-1.45,2.38),(xx-sign*.36,y-1.45,2.96),.085,'wood')
        start=len(self.objects)
        self.gable_roof(2.55,1.82,3.0,3.70,'roof',True)
        for obj in self.objects[start:]:obj.matrix_world=Matrix.Translation((doorx,y-.83,0))@obj.matrix_world
        # Large, readable copper mechanisms, not little facade cubes.
        chamberx=w/2+.54
        self.copper_chamber(chamberx,1.0,.62,.86)
        self.cylinder('rising copper service trunk',(chamberx,1.0,2.26),(chamberx,1.0,3.88),.14,'metal')
        self.beam('copper trunk return',(chamberx,1.0,3.88),(w/2-.45,1.0,3.88),.14,'metal')
        self.pipe(w/2-.62,1.0,h+.54,1.25)
        for zz in (aerialz+.45,aerialz+.84):self.torus('aerial induction coil',(aerialx,aerialy,zz),.27,.055,'metal')
        if not flat:
            # A roof dormer has its own walls, arched warm window and tile roof.
            dx=1.65;dy=-.96;dz=h+1.06
            self.box('dormer real timber cheeks',(dx,dy,dz+.28),(1.45,1.55,.85),'wood',.09)
            self.window(dx,dy-.8,dz-.06,.91,.92,shutters=False)
            start=len(self.objects);self.gable_roof(1.85,1.85,dz+.65,dz+1.31,'roof',True)
            for obj in self.objects[start:]:obj.matrix_world=Matrix.Translation((dx,dy,0))@obj.matrix_world
        if style in('forest','crystal','storm','adobe'):
            dishz=(h+.68)if flat else self._roof_z(-1.85,w+.7,h+.35,top)-.08
            self.dish(-1.85,1.12,dishz,.55)
        return w,d,top+1.5

    def windmill(self):
        """Tapered eight-sided mill, with four modeled open-backed cloth sails."""
        self.cylinder("octagonal stone mill footing",(0,0,.02),(0,0,.55),2.5,"stone",n=8,bevel=.1)
        self.cylinder("timber mill sill",(0,0,.53),(0,0,.83),2.27,"wood",n=8,bevel=.08)
        outline=[(1.38,-2.12),(2.12,-1.38),(2.12,1.38),(1.38,2.12),(-1.38,2.12),(-2.12,1.38),(-2.12,-1.38),(-1.38,-2.12)]
        verts=[(x*f,y*f,z) for f,z in ((1,.7),(.73,6.65)) for x,y in outline]
        faces=[tuple(range(7,-1,-1)),tuple(range(8,16))]+[(i,(i+1)%8,(i+1)%8+8,i+8) for i in range(7)]
        # Leave the front wall open; build its curved entry jambs separately.
        self.mesh("tapered octagonal mill shell",verts,faces,"wall",.055)
        r=.65
        spring=2.0
        arc=[(r*math.cos(math.pi-i*math.pi/20),spring+r*math.sin(math.pi-i*math.pi/20)) for i in range(21)]
        def front_y(z): return -2.12*(1-.27*(z-.7)/5.95)
        # Thick sloping front piers and the arch-to-eave region share the shell plane.
        for sign in (-1,1):
            poly=[(sign*r,.7),(sign*1.38,.7),(sign*1.38*.73,6.65),(sign*r,6.65)]
            vs=[(x,front_y(z)+dy,z) for dy in (0,.18) for x,z in poly]
            self.mesh("mill doorway side pier",vs,[(0,1,2,3),(4,7,6,5),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)],"wall",.025)
        poly=[(-r,6.65),(r,6.65)]+list(reversed(arc))
        n=len(poly)
        vs=[(x,front_y(z)+dy,z) for dy in (0,.18) for x,z in poly]
        self.mesh("mill curved doorway lintel",vs,[tuple(range(n-1,-1,-1)),tuple(range(n,2*n))]+[(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)],"wall",.025)
        self.arch("mill recessed arched entrance",0,-1.94,.7,1.24,1.94,.09,"wood")
        self.arch("mill arched copper jamb",0,-2.075,.7,1.3,1.95,.12,"trim",.13)
        self.torus("mill door handle",(.35,-2.06,1.53),.11,.032,"metal",(0,-1,0),n=20)
        self.box("mill threshold",(0,-2.13,.64),(1.8,.62,.22),"stone",.07)
        for i in range(8):
            a,b=outline[i],outline[(i+1)%8]
            self.beam("mill structural upright",(a[0],a[1],.8),(a[0]*.73,a[1]*.73,6.63),.11,"wood")
            for z in (1.1,2.95,4.35,5.75):
                f=1-.27*(z-.7)/5.95
                self.beam("mill horizontal weatherboard",(a[0]*f,a[1]*f,z),(b[0]*f,b[1]*f,z),.065,"wood")
        self.cylinder("mill broad roof eave",(0,0,6.52),(0,0,6.77),1.95,"trim",n=8,bevel=.08)
        self.cylinder("mill tapered roof cap",(0,0,6.74),(0,0,8.02),1.96,"roof",r2=.18,n=8,bevel=.04)
        self.cylinder("mill roof finial",(0,0,7.94),(0,0,8.37),.18,"metal",r2=.08,n=16)
        self.speaker(1.58,-.30,4.6,.51,side=True)
        # The rotor plane is -Y; every sail includes real thickness and cross-ribs.
        hubz=5.95
        huby=-2.02
        self.cylinder("mill heavy horizontal shaft",(0,-1.35,hubz),(0,huby-.46,hubz),.31,"metal",n=24)
        self.torus("mill hub bearing",(0,huby-.12,hubz),.48,.12,"metal",(0,-1,0))
        rotor=.23
        for arm in range(4):
            angle=rotor+arm*math.pi/2
            radial=Vector((math.sin(angle),0,math.cos(angle)))
            lateral=Vector((math.cos(angle),0,-math.sin(angle)))
            origin=Vector((0,huby-.25,hubz))
            self.beam("mill thick sail spar",origin,origin+radial*5.0,.13,"wood")
            a=origin+radial*1.35-lateral*.12
            b=origin+radial*4.85-lateral*.12
            c=origin+radial*4.85+lateral*1.03
            d=origin+radial*1.35+lateral*.67
            vv=[tuple(p+Vector((0,yy,0))) for yy in (-.055,.055) for p in (a,b,c,d)]
            self.mesh("thick canvas sail panel",vv,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],"cloth",.045)
            self.beam("mill sail outer rail",d,c,.075,"wood")
            for j in range(6):
                t=j/5
                self.beam("mill sail ladder batten",a.lerp(b,t),d.lerp(c,t),.057,"wood")
        self.cylinder("mill brass hub cap",(0,huby-.42,hubz),(0,huby-.62,hubz),.47,"metal",r2=.34,n=24)
        self.pipe(1.0,.6,6.58,.65)
        self.window(0,-1.82,3.30,.82,1.10,shutters=True)
        # Solid tapered weatherboards, built in one mesh around all eight faces.
        boards=[];boardfaces=[]
        for face in range(8):
            aa,bb=outline[face],outline[(face+1)%8]
            mid=Vector(((aa[0]+bb[0])/2,(aa[1]+bb[1])/2,0)).normalized()
            for row in range(13):
                z0=.88+row*.425;z1=z0+.39
                if face==7 and z0<2.8:continue
                patch=[]
                for extrusion in(.065,.0):
                    for a,z in((aa,z0),(bb,z0),(bb,z1),(aa,z1)):
                        f=1-.27*(z-.7)/5.95
                        patch.append((a[0]*f+mid.x*extrusion,a[1]*f+mid.y*extrusion,z))
                base=len(boards);boards.extend(patch)
                boardfaces.extend(tuple(base+i for i in f)for f in((0,1,2,3),(7,6,5,4),(0,4,5,1),(1,5,6,2),(2,6,7,3),(3,7,4,0)))
        self.mesh('tapered windmill individual timber weatherboards',boards,boardfaces,'wall',.023)
        self.copper_chamber(1.72,1.15,.61,.68)

    def dish(self,x,y,z,size=1):
        self.cylinder("dish geared pedestal",(x,y,z),(x,y,z+.55*size),.47*size,"stone",n=16)
        self.cylinder("dish gimbal neck",(x,y,z+.45*size),(x,y,z+1.18*size),.18*size,"metal",n=16)
        # Concave reflector points toward the south and visibly curves in every view.
        center=Vector((x,y,z+1.57*size))
        axis=Vector((0,-.72,.69)).normalized()
        u=Vector((1,0,0))
        v=axis.cross(u).normalized()
        n=40
        verts=[tuple(center-axis*.22*size)]
        rings=6
        for j in range(1,rings+1):
            r=j/rings*1.15*size
            depth=(r/(1.15*size))**2*.42*size-.22*size
            for i in range(n):
                verts.append(tuple(center+axis*depth+r*(math.cos(i*TAU/n)*u+math.sin(i*TAU/n)*v)))
        faces=[(0,1+i,1+(i+1)%n) for i in range(n)]
        for j in range(rings-1):
            a=1+j*n
            b=a+n
            faces.extend([(a+i,b+i,b+(i+1)%n,a+(i+1)%n) for i in range(n)])
        dish=self.mesh("concave solid copper receiver",verts,faces,"metal",smooth=True)
        solid=dish.modifiers.new("Thick reflector casting","SOLIDIFY")
        solid.thickness=.09*size
        self.torus("receiver rolled rim",center+axis*.2*size,1.15*size,.07*size,"metal",tuple(axis),n=40)
        for i in range(3):
            a=i*TAU/3
            p=center+axis*.2*size+.95*size*(math.cos(a)*u+math.sin(a)*v)
            self.beam("dish feed support strut",p,center+axis*.94*size,.048*size,"metal")
        self.cylinder("dish feed horn",center+axis*.8*size,center+axis*1.04*size,.16*size,"accent",r2=.24*size,n=16)

    def tower(self,relay=False):
        self.cylinder("relay broad footing",(0,0,.02),(0,0,.46),2.08,"stone",n=12,bevel=.1)
        if relay:
            self.cylinder("relay squat ceramic body",(0,0,.45),(0,0,1.85),1.5,"wall",r2=1.36,n=12,bevel=.1)
            self.cylinder("relay metal shoulder cap",(0,0,1.82),(0,0,2.04),1.63,"roof",n=12)
            self.speaker(0,-1.46,1.13,.53)
            self.dish(0,0,2.05,1.1)
            for i in range(8):
                a=i*TAU/8
                self.cylinder("relay shoulder bolt",(1.42*math.cos(a),1.42*math.sin(a),2.03),(1.42*math.cos(a),1.42*math.sin(a),2.16),.09,"metal",n=10)
            return
        self.cylinder("squat signal tower body",(0,0,.46),(0,0,5.5),1.7,"wall",r2=1.5,n=10,bevel=.1)
        for z in (1.0,3.15,5.36):
            self.cylinder("tower heavy masonry belt",(0,0,z),(0,0,z+.21),1.75-(z-.8)*.035,"trim",n=10,bevel=.04)
        self.arch("tower inset arched oak door",0,-1.64,.51,1.14,2.2,.12,"wood")
        self.arch("tower heavy arched surround",0,-1.75,.5,1.2,2.24,.19,"metal",.12)
        self.speaker(0,-1.58,3.9,.58)
        self.cylinder("tower broad roof rim",(0,0,5.55),(0,0,5.85),1.98,"roof",n=10)
        self.cylinder("tower low red roof",(0,0,5.84),(0,0,6.66),1.97,"roof",r2=.28,n=10)
        self.cylinder("tower receiver stalk",(0,0,6.45),(0,0,7.6),.17,"metal")
        self.torus("thick ring receiver",(0,0,8.12),.83,.15,"metal",(0,-1,0),n=36)
        for sign in (-1,1):
            self.beam("receiver cradle fork",(0,0,7.35),(sign*.64,0,7.57),.11,"metal")
        if self.style=="storm":
            for sign in (-1,1):
                self.beam("semaphore heavy arm",(0,-.12,7.1),(sign*2,-.12,7.7 if sign>0 else 6.5),.17,"wood")
                self.box("semaphore broad paddle",(sign*1.9,-.12,7.65 if sign>0 else 6.55),(.8,.25,1.05),"accent",.11,rotation=-sign*.35)

    def observatory(self):
        """Open low pavilion: no dome or wall can hide the telescope silhouette."""
        self.cylinder("pavilion broad low plinth",(0,0,.01),(0,0,.28),4.05,"stone",n=48,bevel=.12)
        self.cylinder("pavilion ceramic platform",(0,0,.27),(0,0,.48),3.78,"wall",n=48,bevel=.10)
        self.torus("pavilion floor gold inlay",(0,0,.495),3.22,.045,"metal",n=64)
        self.torus("pavilion inner floor inlay",(0,0,.5),2.18,.038,"accent",n=56)
        for i in range(6):
            a=(i+.5)*TAU/6
            x,y=3.25*math.cos(a),3.25*math.sin(a)
            self.cylinder("pavilion column rounded foot",(x,y,.47),(x,y,.8),.39,"metal",n=16)
            self.cylinder("pavilion thick gold column",(x,y,.76),(x*.96,y*.96,3.4),.16,"metal",r2=.14,n=16)
            self.cylinder("pavilion column capital",(x*.96,y*.96,3.21),(x*.96,y*.96,3.51),.29,"metal",n=16)
            # Each thick roof rib is an explicit curved arch, with large daylight gaps.
            old=None
            for j in range(13):
                t=j/12
                r=3.25*(1-t)
                z=3.43+1.24*math.sin(t*math.pi/2)
                p=(r*math.cos(a),r*math.sin(a),z)
                if old:
                    self.beam("open pavilion sculpted roof rib",old,p,.12,"metal")
                old=p
        self.torus("pavilion open eave ring",(0,0,3.45),3.25,.135,"metal",n=64)
        self.torus("pavilion upper rib hoop",(0,0,4.31),1.56,.085,"metal",n=48)
        self.cylinder("pavilion small celestial finial",(0,0,4.57),(0,0,4.97),.18,"metal",r2=.065,n=16)
        self.cylinder("telescope geared base",(0,0,.5),(0,0,1.07),.7,"stone",n=24,bevel=.07)
        self.cylinder("telescope mounting pier",(0,0,1.0),(0,0,2.18),.25,"metal",n=20)
        self.torus("telescope gimbal wheel",(.4,0,2.15),.48,.1,"metal",(1,0,0))
        a=Vector((0,.75,1.92))
        b=Vector((0,-1.35,3.21))
        axis=(b-a).normalized()
        self.cylinder("large telescope enamel barrel",a,b,.39,"roof",r2=.56,n=24,bevel=.06)
        self.cylinder("telescope brass front collar",b-axis*.18,b+axis*.14,.6,"metal",n=24,bevel=.04)
        self.cylinder("telescope deep blue lens",b+axis*.146,b+axis*.154,.48,"glass",n=32,bevel=0)
        self.torus("telescope rolled objective lip",b+axis*.17,.52,.055,"metal",tuple(axis),n=32)
        self.cylinder("telescope eyepiece",a-axis*.4,a,.15,"metal",n=16)
        # A compact control cabinet remains wholly within the platform.
        self.box("pavilion radio control console",(1.6,.8,1.1),(.85,.58,1.2),"wall",.12)
        self.speaker(1.6,.49,1.2,.27)

    def finish(self,location,yaw,scale):
        transform=Matrix.Translation(Vector(location)) @ Matrix.Rotation(yaw,4,"Z") @ Matrix.Scale(scale,4)
        for obj in self.objects:
            obj.matrix_world=transform @ obj.matrix_world
            obj["radio_archipelago_role"]="architecture"
            obj["radio_archipelago_kind"]=self.kind
            obj["radio_archipelago_style"]=self.style
        return self.objects


def build_building(kind, location=(0,0,0), yaw=0, scale=1, palette=None, seed=0, collection=None):
    """Create independent mesh parts and return them; creates no cameras or lights."""
    builder=_Builder(kind,palette,seed,collection)
    lowered=kind.lower()
    if "windmill" in lowered or lowered=="mill":
        builder.windmill()
    elif any(w in lowered for w in ("observatory","pavilion")):
        builder.observatory()
    elif any(w in lowered for w in ("relay","turbine")):
        builder.tower(relay=True)
    elif any(w in lowered for w in ("tower","signal","watch")):
        builder.tower()
    else:
        builder.house(workshop=any(w in lowered for w in ("workshop","depot","barn")))
    return builder.finish(location,yaw,scale)

