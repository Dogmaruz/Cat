import hashlib,json,pathlib,shutil,subprocess

work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
repo=project.parent
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p,normalized=False):
    data=p.read_bytes()
    return hashlib.sha256(data.replace(b'\r\n',b'\n') if normalized else data).hexdigest()

before=work/'before'
assert not (before/'protected-hashes.json').exists(),'Never overwrite an initial snapshot'
old=read(project/'Library/ChartGenerationV11/before/protected-hashes.json')
assert all(sha(repo/p)==h for p,h in old.items()),'V11 protected file changed before V12'
paths={repo/p for p in old}|set((project/'Assets/Resources/CustomData/Maps').rglob('*'))
protected={p.relative_to(repo).as_posix():sha(p) for p in sorted(paths) if p.is_file()}
write(before/'protected-hashes.json',protected)
write(before/'protected-files.json',{'files':[dict(path=p,sha256=h,normalizedLf=False) for p,h in protected.items()]})
shutil.copyfile(project/'Assets/Resources/CustomData/songs.json',before/'songs.json')
m=read(project/'Assets/Clone/Editor/ChartStudies/v11.json')
m.update(studyId='ChartGenerationV12',studyDirectory='Library/ChartGenerationV12',selectionPath='Library/ChartGenerationV12/selection.json',
         baselineGenerator='pcm-vocal-phrase-chart-v11+short-test',candidateGenerator='pcm-vocal-phrase-chart-v12+short-test',
         testSuffix='GeneratorTestV12',catalogIdStart=30031,protectedCustomMapFileCount=len(list((project/'Assets/Resources/CustomData/Maps').rglob('*.*'))),
         protectedCatalogRecordCount=len(read(before/'songs.json')),protectedSnapshotSha256=sha(before/'protected-files.json'),protectedCatalogSha256=sha(before/'songs.json'))
for source in m['sources']: source['role']='known'
source_paths=set(m['algorithmPaths'])
source_paths.update(p.relative_to(repo).as_posix() for p in (project/'Assets/Clone/Editor').glob('*.cs'))
source_paths.update(p.relative_to(repo).as_posix() for p in (project/'Assets/Clone/Runtime').glob('*.cs'))
m['algorithmPaths']=sorted(source_paths)
hashes={p:sha(repo/p,True) for p in m['algorithmPaths']}
for p in hashes:
    dest=before/'algorithm'/p
    dest.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(repo/p,dest)
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
write(work/'baseline-code-inputs.json',dict(baselineCommit=head,sourceHashesNormalizedLf=hashes))
write(work/'selection.json',dict(schemaVersion=1,baselineCommit=head,selectionPolicy='All fifteen V11 sources are known calibration/regression data. No independent unseen-song claim.',selection=m['sources']))
write(project/'Assets/Clone/Editor/ChartStudies/v12.json',m)
print(f'Protected {len(protected)} files and {m["protectedCatalogRecordCount"]} prior raw catalog objects; archived {len(hashes)} source files before edits; selected 15 known recordings.')
