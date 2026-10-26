import hashlib,json,pathlib
work=pathlib.Path(__file__).resolve().parent;root=work.parents[2];project=root/'DancingCatsClone'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
protected=read(work/'before/protected-hashes.json');assert len(protected)==434 and all(sha(root/p)==h for p,h in protected.items())
index=work/'before/protected-files.json'
payload=json.dumps(dict(files=[dict(path=p,sha256=h,normalizedLf=False)for p,h in sorted(protected.items())]),indent=2)+'\n'
if index.exists():assert index.read_text()==payload,'Never replace the initial snapshot'
else:index.write_text(payload,encoding='utf8')
manifest=project/'Assets/Clone/Editor/ChartStudies/v11.json';m=read(manifest)
m.update(protectedSnapshotSha256=sha(index),protectedCatalogSha256=sha(work/'before/songs.json'))
manifest.write_text(json.dumps(m,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print('Pinned initial434file snapshot and12rawcatalogrecords; current resources still match.')
