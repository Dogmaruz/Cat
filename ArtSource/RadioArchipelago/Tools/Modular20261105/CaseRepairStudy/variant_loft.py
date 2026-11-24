"""Isolated case-specific derivatives of the validated Miro Garden loft method.

Reads immutable source backpacks and only writes FinalVariants. The facade and
original rotating part remain source geometry; the harness-bearing return is
fully exchanged for a closed, fitted housing and a new stitched contact pad.
"""
from pathlib import Path
import sys,json
HERE=Path(__file__).parent
TEMPLATE=(HERE/'miro_loft.py').read_text()
CONFIG={
 'Lumen_Glider':{'plane':.025,'roof':1.055,'enamel':(.86,.81,.65),'width':.25,'halfHeight':.355},
 'Fira_Beacon':{'plane':.025,'roof':1.0440458059310913,'enamel':(.70,.24,.15),'width':.245,'halfHeight':.335},
}
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else list(CONFIG)
for name in names:
 cfg=CONFIG[name]
 meta=json.loads((HERE.parent/'StraplessCases/Before'/name/'Meta.json').read_text())
 height=meta['caseBoundsMax'][2];pad=meta['contactPlaneY'];pivot=meta['topPivot'];plane=cfg['plane'];roof=cfg['roof']
 contact=pad-.014;delta=contact+.253;back=pad+.012;depth=plane-back
 code=TEMPLATE
 def swap(old,new):
  global code
  if old not in code:raise RuntimeError('Template sentinel missing: '+old)
  code=code.replace(old,new)
 swap("OUT=HERE/'MiroGardenCandidate'/'Miro_Garden'",f"OUT=HERE/'FinalVariants'/{name!r}")
 swap("BASE=HERE.parent/'StraplessCases/Before/Miro_Garden'",f"BASE=HERE.parent/'StraplessCases/Before'/{name!r}")
 swap("plane=-.025",f"plane={plane!r}")
 swap("green=swatch((.34,.53,.38))",f"green=swatch({cfg['enamel']!r})")
 swap("min(.755,float(v.co.z))",f"min({roof!r},float(v.co.z))")
 swap("v.co.z<.825",f"v.co.z<{height!r}")
 swap("(abs(p.x)<.17 and p.z>.70)",f"(abs(p.x-{pivot[0]!r})<.17 and p.z>{height*.89!r})")
 if name=='Fira_Beacon':
  swap(f"(abs(p.x-{pivot[0]!r})<.17 and p.z>{height*.89!r})",
       f"(((p.x-{pivot[0]!r})**2+(p.y-{pivot[1]!r})**2)<.075**2 and p.z>{height+.02!r})")
 rings=[(.003,1),(-depth*.16,1),(-depth*.43,.993),(-depth*.69,.973),(-depth*.88,.935),(-depth,.885)]
 swap("[(+.003,1),(-.028,1),(-.075,.993),(-.12,.973),(-.153,.935),(-.173,.885)]",repr(rings))
 swap("padcenter=.42",f"padcenter={height*.49!r}")
 swap(".225*rho",f"{cfg['width']!r}*rho")
 swap(".280*rho",f"{cfg['halfHeight']!r}*rho")
 swap("t=i*2*math.pi/128;x,z=pad_xy(t,rho)",f"t=i*2*math.pi/128;x,z=pad_xy(t,rho);y+=0")
 swap("yy=y+.008*math.exp",f"yy=y+{delta!r}+.008*math.exp")
 swap("(0,-.255,padcenter)",f"(0,{- .255 + delta!r},padcenter)")
 swap("center=Vector((x,-.237,z))",f"center=Vector((x,{- .237 + delta!r},z))")
 swap("location=(.022,-.080,.760)",f"location=({pivot[0]!r},{pivot[1]!r},{roof+.012!r})")
 if name=='Fira_Beacon':
  swap(f"bpy.ops.mesh.primitive_cube_add(size=1,location=({pivot[0]!r},{pivot[1]!r},{roof+.012!r}))",
       f"bpy.ops.mesh.primitive_cylinder_add(vertices=48,radius=.08,depth=.045,location=({pivot[0]!r},{pivot[1]!r},{height+.008!r}))")
  swap("collar.dimensions=(.29,.19,.065)","collar.dimensions=(.16,.16,.045)")
  swap("modifier.width=.022;modifier.segments=4","modifier.width=.008;modifier.segments=3")
 else:
  swap("for ob in (shell,pad,stitches,collar):ob.select_set(True)","for ob in (shell,pad,stitches):ob.select_set(True)\nbpy.data.objects.remove(collar,do_unlink=True)")
 swap("meta['contactPlaneY']=-.253",f"meta['contactPlaneY']={contact!r}")
 swap("'sourceBodyHeight':.83",f"'sourceBodyHeight':{height!r}")
 swap("c.data.ortho_scale=1.35","c.data.ortho_scale=1.58")
 swap("target=Vector((0,0,.5))",f"target=Vector((0,0,{height*.5!r}))")
 exec(compile(code,str(HERE/'miro_loft.py')+'::'+name,'exec'),{'__file__':str(__file__),'__name__':'isolated_variant'})
