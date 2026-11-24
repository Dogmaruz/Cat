import bpy,numpy as np,pathlib,json
for n in ['Boro','Nori','Lumen']:
 r=pathlib.Path('C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ProcessedModular/Characters')/n;m=json.loads((r/'Meta.json').read_text());bpy.ops.wm.open_mainfile(filepath=str(r/(n+'_Rigged.blend')));o=next(o for o in bpy.data.objects if o.type=='MESH');p=np.array([v.co[:] for v in o.data.vertices]);z=m['armHeight']-.095; half=m['bodyHalfWidth']-.02;x=half*.76
 b=p[(abs(p[:,0]-x)<.04)&(abs(p[:,2]-z)<.035)];surface=-float(b[:,1].min());front=m['bodyDepth']*.5;ellipse=front*np.sqrt(1-.76**2)+.035
 print(n,'yHeight',z,'x',x,'actualSurfaceFrontZ',surface,'ellipsePlusClearance',ellipse,'penetration',surface-ellipse,flush=True)
