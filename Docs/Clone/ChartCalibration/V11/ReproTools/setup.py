import hashlib,json,pathlib,shutil,subprocess
work=pathlib.Path(__file__).resolve().parent;root=work.parents[2];project=root/'DancingCatsClone'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.parent.mkdir(parents=True,exist_ok=True);p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def sha(p,normalized=False):return hashlib.sha256(p.read_bytes().replace(b'\r\n',b'\n') if normalized else p.read_bytes()).hexdigest()
before=work/'before';before.mkdir(exist_ok=True)
old=read(project/'Library/ChartGenerationV10/before/protected-hashes.json');assert all(sha(root/p)==h for p,h in old.items())
paths={root/p for p in old}|set((project/'Assets/Resources/CustomData/Maps').rglob('*'))
protected={p.relative_to(root).as_posix():sha(p)for p in sorted(paths)if p.is_file()}
assert not(before/'protected-hashes.json').exists(),'Never overwrite the initial protected snapshot'
write(before/'protected-hashes.json',protected);shutil.copyfile(project/'Assets/Resources/CustomData/songs.json',before/'songs.json')
oldfreeze=read(project/'Library/ChartGenerationV10/freeze.json')
assert all(sha(root/e['path'],e['normalizedLf'])==e['sha256']for e in oldfreeze['algorithm']),'Capture before classifier edits'
sources={e['path']:sha(root/e['path'],True)for e in oldfreeze['algorithm']}
for p in sources:
 dest=before/'algorithm'/p;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copyfile(root/p,dest)
write(work/'baseline-code-inputs.json',dict(baselineCommit=subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),sourceHashesNormalizedLf=sources))
m=read(project/'Assets/Clone/Editor/ChartStudies/v11.json');assert len(m['sources'])==15 and sum(s['role'].startswith('heldout')for s in m['sources'])==2
write(work/'selection.json',dict(schemaVersion=1,baselineCommit='fd39ed4',selectionPolicy=read(work/'selection-proposal.json')['selectionPolicy'],selection=m['sources']))
print('Protected',len(protected),'files; archived22baseline sources; selected13known/calibration +2unopenedheldout.')
