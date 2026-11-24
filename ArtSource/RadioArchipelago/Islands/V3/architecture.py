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
    "forest": dict(wall="#65786C", roof="#224C3A", wood="#55392A", stone="#566570", metal="#B27E4E", accent="#AFC4AA", trim="#363E34", glass="#243F48"),
    "adobe": dict(wall="#CE7464", roof="#A64C43", wood="#67433B", stone="#B7785A", metal="#C79B57", accent="#3F918C", trim="#E4A084", glass="#254E59"),
    "crystal": dict(wall="#55486F", roof="#8F80B5", wood="#4C3B5C", stone="#49425F", metal="#ADB6CC", accent="#A1BCD3", trim="#625780", glass="#37325C"),
    "autumn": dict(wall="#A45537", roof="#88402E", wood="#543328", stone="#8E7760", metal="#B68A4B", accent="#D7A766", trim="#6B3E2C", glass="#334755"),
    "ice": dict(wall="#A7CEDD", roof="#547FA7", wood="#526379", stone="#82B4CD", metal="#C4CEDE", accent="#CD9B57", trim="#446586", glass="#2E567A"),
    "tropical": dict(wall="#BD9855", roof="#847545", wood="#64422D", stone="#4E6662", metal="#B99150", accent="#D18748", trim="#775431", glass="#305D61"),
    "storm": dict(wall="#687A80", roof="#263D56", wood="#554D43", stone="#404A55", metal="#9D7950", accent="#A4533D", trim="#354651", glass="#233B49"),
    "farm": dict(wall="#AC4838", roof="#623C37", wood="#673C2B", stone="#AA9370", metal="#BD9456", accent="#DABF80", trim="#E0BD87", glass="#425B61"),
    "volcanic": dict(wall="#4F4C50", roof="#8D4138", wood="#4F3731", stone="#3B3D44", metal="#B3754C", accent="#CA713F", trim="#653E39", glass="#322E3B"),
    "cloud": dict(wall="#DCADA5", roof="#9784B2", wood="#9D7A82", stone="#BBADB9", metal="#CDB57A", accent="#B8BBDE", trim="#BA92AA", glass="#62688F"),
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
    key = "RA3_" + label + "_" + "".join(f"{round(c*255):02x}" for c in rgba[:3])
    mat = bpy.data.materials.get(key)
    if mat:
        return mat
    mat = bpy.data.materials.new(key)
    mat.diffuse_color = rgba
    mat.use_nodes = True
    node = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
    if node:
        node.inputs["Base Color"].default_value = tuple(_linear(c) for c in rgba[:3]) + (rgba[3],)
        node.inputs["Metallic"].default_value = metallic
        node.inputs["Roughness"].default_value = roughness
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
        self.objects = []
        self.serial = 0
        self.prefix = "RA3_" + self.kind + f"_{seed}_"
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

    def gable_roof(self, width, depth, eave, ridge, mat="roof", timber=True):
        w,d = width/2,depth/2
        self.prism("solid overhanging pitched roof",[(-w,eave),(0,ridge),(w,eave),(w,eave-.22),(0,ridge-.22),(-w,eave-.22)],-d,d,mat,.07)
        self.cylinder("rounded roof ridge",(0,-d-.02,ridge),(0,d+.02,ridge),.16,"trim",n=12)
        # Raised overlapping tile courses and readable panel seams on both slopes.
        for side in (-1,1):
            for row in range(1,5):
                t = row/5
                x = side*w*t
                z = ridge+(eave-ridge)*t+.045
                self.beam("roof overlapping tile course",(x,-d,z),(x,d,z),.045,"trim")
            for j in range(6):
                yy=-d+(j+.5)*depth/6
                self.beam("roof raised panel seam",(0,yy,ridge+.055),(side*w,yy,eave+.055),.032,"trim")
        for yy in (-d,d):
            self.beam("front gable fascia",(-w,yy,eave),(0,yy,ridge),.115,"wood" if timber else "metal")
            self.beam("front gable fascia",(0,yy,ridge),(w,yy,eave),.115,"wood" if timber else "metal")
        for xx in (-w,w):
            self.box("thick curved eave",(xx,0,eave-.12),(.19,depth+.1,.24),"wood" if timber else "trim",.07)

    def house(self, workshop=False):
        style=self.style
        flat=style=="adobe"
        w,d,h=(7.1,5.0,3.35) if workshop else (5.9,4.7,3.75)
        if style=="ice": h-=.45
        y=-d/2
        doorx=-.68 if workshop else -.9
        doorw,doorh=1.37,2.55
        self.box("broad dressed foundation",(0,0,.22),(w+.48,d+.42,.44),"stone",.15)
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
        self.speaker(1.38 if workshop else 1.24,y-.035,2.45,.66)
        self.speaker(w/2+.035,.55,2.1,.57,side=True)
        # Corner joinery and horizontal belts give the silhouette readable carpentry.
        if style in ("forest","autumn","tropical","farm","storm"):
            for xx in (-w/2+.1,w/2-.1):
                for yy in (-d/2+.1,d/2-.1):
                    self.box("heavy corner timber",(xx,yy,h/2+.37),(.23,.23,h+.12),"wood",.06)
            for zz in (1.2,2.0,2.8):
                self.box("side timber siding",(w/2+.018,0,zz),(.065,d-.1,.065),"trim",.024)
                self.box("back timber siding",(0,d/2+.018,zz),(w-.1,.065,.065),"trim",.024)
        if flat:
            self.box("stepped adobe roof slab",(0,0,h+.51),(w+.3,d+.3,.37),"roof",.16)
            for xx in (-w/2,w/2):
                self.box("rounded low parapet",(xx,0,h+.86),(.28,d+.28,.55),"wall",.13)
            self.box("rear roof parapet",(0,d/2,h+.86),(w,.28,.55),"wall",.13)
            self.box("second adobe roof volume",(-1.55,.6,h+.97),(2.3,2.25,.6),"wall",.18)
            self.box("second red roof cap",(-1.55,.6,h+1.33),(2.55,2.5,.21),"roof",.1)
            top=h+.75
        else:
            ridge=h+(1.65 if workshop else 2.0)
            if style=="tropical": ridge+=.55
            # Front/rear gables are real masonry volumes under the pitched roof.
            for yy in (-d/2,d/2-.18):
                self.prism("modeled gable infill",[(-w/2,h+.3),(0,ridge-.15),(w/2,h+.3)],yy,yy+.18,"wood" if style in ("autumn","farm","tropical") else "wall",.04)
            self.gable_roof(w+.7,d+.65,h+.35,ridge,"roof",style not in ("ice","crystal","cloud"))
            self.speaker(0,-d/2-.08,h+.84,.38)
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
        self.antenna(-1.15,.52,top-.05,.8)
        self.pipe(w/2-.65,d/2-.75,h+.45,.88 if workshop else .75)
        # External elbow/valve package belongs to the building footprint.
        self.cylinder("copper side service pipe",(w/2+.19,.92,.8),(w/2+.19,.92,1.65),.11,"metal")
        self.cylinder("copper service elbow",(w/2+.18,.92,.8),(w/2+.18,-.35,.8),.11,"metal")
        self.torus("service valve wheel",(w/2+.32,.65,1.31),.22,.044,"metal",(1,0,0),n=24)
        for i in range(4):
            a=i*math.pi/2
            self.beam("valve spoke",(w/2+.32,.65,1.31),(w/2+.32,.65+.2*math.cos(a),1.31+.2*math.sin(a)),.025,"metal")
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

