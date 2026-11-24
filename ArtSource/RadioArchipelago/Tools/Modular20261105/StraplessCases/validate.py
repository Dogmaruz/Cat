from pathlib import Path
import bpy,bmesh,json,numpy as np,hashlib,sys
P=Path(__file__).parent

def digest(ob):
 m=ob.data
 h=hashlib.sha256()
 for v in m.vertices:h.update(np.asarray(v.co,dtype='<f4').tobytes())
 for f in m.polygons:h.update(np.asarray(f.vertices,dtype='<i4').tobytes())
 for l in m.uv_layers.active.data:h.update(np.asarray(l.uv,dtype='<f4').tobytes())
 h.update(np.asarray(ob.matrix_world,dtype='<f4').tobytes())
 return h.hexdigest()

args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
payload=P/(args.pop(0) if args else 'Loft')
rows=[]
for d in sorted(payload.iterdir()):
 if args and d.name not in args:continue
 bpy.ops.wm.open_mainfile(filepath=str(P/'Before'/d.name/'editable.blend'),load_ui=False)
 original=digest(bpy.data.objects['TopRotor']);pivot=list(bpy.data.objects['TopPivot'].location)
 before=json.loads((P/'Before'/d.name/'Meta.json').read_text())
 bpy.ops.wm.open_mainfile(filepath=str(d/'editable.blend'),load_ui=False)
 case=bpy.data.objects['Case'];bm=bmesh.new();bm.from_mesh(case.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=1e-6)
 bad=[o.name for o in bpy.context.scene.objects if o.type=='MESH' and o.name not in ('Case','TopRotor')]
 meta=json.loads((d/'Meta.json').read_text());a=np.array([list(v.co) for v in case.data.vertices])
 row={'id':d.name,'meshes':[o.name for o in bpy.context.scene.objects if o.type=='MESH'],'extraMeshes':bad,
      'sourceRotorSha256':original,'derivativeRotorSha256':digest(bpy.data.objects['TopRotor']),
      'rotorUnchanged':original==digest(bpy.data.objects['TopRotor']),
      'pivotUnchanged':pivot==list(bpy.data.objects['TopPivot'].location),
      'caseBodyBoundsUnchanged':before['caseBoundsMin']==meta['caseBoundsMin'] and before['caseBoundsMax']==meta['caseBoundsMax'],
      'caseHeightUnchanged':abs((before['caseBoundsMax'][2]-before['caseBoundsMin'][2])-(meta['caseBoundsMax'][2]-meta['caseBoundsMin'][2]))<1e-6,
      'caseWidthUnchanged':abs((before['caseBoundsMax'][0]-before['caseBoundsMin'][0])-(meta['caseBoundsMax'][0]-meta['caseBoundsMin'][0]))<1e-6,
      'vertices':len(case.data.vertices),'triangles':sum(len(f.vertices)-2 for f in case.data.polygons),
      'boundaryEdges':sum(e.is_boundary for e in bm.edges),'nonManifoldEdges':sum(not e.is_manifold for e in bm.edges),'looseVertices':sum(not v.link_faces for v in bm.verts),
      'degenerateFaces':sum(f.calc_area()<1e-10 for f in bm.faces),'finiteVertices':bool(np.isfinite(a).all()),
      'harnessNames':meta.get('harnessNames',[]),'frame':meta['frame'],
      'sourceArchiveUnchanged':hashlib.sha256((Path(r'C:/Users/Derek/Games_develop/Cat/ArtSource/RadioArchipelago/ModularCast20261102/Incoming/Backpacks')/d.name/(d.name+'.zip')).read_bytes()).hexdigest()==before['sourceArchiveSha256']}
 bm.free();rows.append(row)
passed=len(rows)==(len(args) if args else 20) and all(r['rotorUnchanged'] and r['pivotUnchanged'] and r['caseHeightUnchanged'] and r['caseWidthUnchanged'] and not r['extraMeshes'] and not r['harnessNames'] and r['finiteVertices'] and r['sourceArchiveUnchanged'] for r in rows)
(P/('validation-'+('-'.join(args) if args else 'all')+'.json')).write_text(json.dumps({'passed':passed,'entries':rows},indent=2))
print('STRAPLESS_VALIDATION',passed,len(rows),flush=True)
