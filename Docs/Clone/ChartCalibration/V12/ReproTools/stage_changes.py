import hashlib,json,pathlib,subprocess,sys
work=pathlib.Path(__file__).resolve().parent
repo=work.parents[2]
def git(*args):return subprocess.check_output(['git','-c','core.safecrlf=false',*args],cwd=repo)
subprocess.run([sys.executable,str(work/'freeze_study.py'),'verify-freeze'],check=True)
protected=json.loads((work/'before/protected-hashes.json').read_text(encoding='utf-8-sig'))
assert all(hashlib.sha256((repo/p).read_bytes()).hexdigest()==h for p,h in protected.items())
preserve={'DancingCatsClone/Assets/Settings/Mobile_RPAsset.asset','Docs/Clone/AUDIO_WORKFLOW.md',
          'The Cat/Assets/Resources/DOTweenSettings.asset','The Cat/Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset'}
prefixes=('DancingCatsClone/Assets/Clone/Editor/','DancingCatsClone/Assets/Resources/CustomData/','Docs/Clone/ChartCalibration/V12/')
exact={'DancingCatsClone/Assets/Clone/Runtime/OriginalPlayController.cs','DancingCatsClone/Assets/Clone/Runtime/OriginalPlayController.EditorTiming.cs',
       'DancingCatsClone/Assets/Clone/Runtime/OriginalPlayController.EditorTiming.cs.meta',
       'DancingCatsClone/Assets/RadioArchipelago/Editor/RadioEditorValidation.cs','DancingCatsClone/Assets/RadioArchipelago/Runtime/CourierMotion.cs',
       'DancingCatsClone/ProjectSettings/AudioManager.asset','Docs/Clone/2026-10-26-chart-v12-plan.md','Docs/Clone/ChartCalibration/README.md'}
paths={p.decode('utf8') for data in [git('diff','HEAD','--name-only','-z'),git('ls-files','--others','--exclude-standard','-z')] for p in data.split(b'\0') if p}
ours=sorted(paths-preserve)
assert all(p in exact or p.startswith(prefixes) for p in ours),[p for p in ours if p not in exact and not p.startswith(prefixes)]
assert not set(ours)&set(protected),'Never stage a protected original/prior file'
for p in ours:
    if p.endswith('.meta') and not git('ls-files','--',p).strip():
        f=repo/p;f.write_text('\n'.join(s.rstrip() for s in f.read_text(encoding='utf8').splitlines())+'\n',encoding='utf8')
subprocess.run(['git','-c','core.safecrlf=false','add','--',*ours],cwd=repo,check=True)
staged={p.decode('utf8') for p in git('diff','--cached','--name-only','-z').split(b'\0') if p}
assert staged==set(ours) and not staged&preserve
# Two frozen C# sources retain a harmless terminal blank line. Never modify a
# frozen input to appease an EOF style check; other whitespace checks stay on.
subprocess.run(['git','-c','core.safecrlf=false','-c','core.whitespace=-blank-at-eof','diff','--cached','--check'],cwd=repo,check=True)
print(f'STAGED {len(ours)} scoped files;446 protected files preserved;4 pre-existing user changes excluded.')
