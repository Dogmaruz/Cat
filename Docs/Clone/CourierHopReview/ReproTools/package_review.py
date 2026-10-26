import datetime,hashlib,json,pathlib,shutil

work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
repo=project.parent
out=repo/'Docs/Clone/CourierHopReview'
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source,target)
before=read(work/'before.json')
assert all(sha(repo/p)==h for p,h in before['protected'].items()),'A protected source/asset changed'
summary=read(work/'summary.json')
assert all(summary[k] for k in ['allViewsFit','sameFraming','sameLighting','samePrefabPathsAndScales'])
assert sha(project/'Assets/RadioArchipelago/Runtime/CourierMotion.cs')==summary['motionSourceSha256']['candidate']
assert sha(work/'CourierMotion.before.cs.txt')==summary['motionSourceSha256']['baseline']
for stage in ['Baseline','Candidate']:
    report=read(work/stage/'metrics.json')
    assert len(report['frames'])==117
    assert all(v['fits'] for f in report['frames'] for v in f['views'])
    for p in (work/stage).iterdir():
        if p.is_file() and p.suffix in {'.json','.png','.txt'}:copy(p,out/stage/p.name)
assert read(work/'Baseline/metrics.json')['framing']==read(work/'Candidate/metrics.json')['framing']
runtime={}
for name in ['unity-jingle.txt','unity-our-song.txt']:
    text=(work/name).read_text(encoding='utf-8-sig')
    assert text.startswith('PASS ') and 'actual tile contacts' in text and 'result and retry' in text
    runtime[name]=sha(work/name)
for name in ['before.json','framing.json','reference.md','summary.json','findings.txt','checks-red.json','checks-green.txt','compatibility-checks.txt',
             'unity-jingle.txt','unity-our-song.txt','unity-preview.json','gameplay-comparison.gif','gameplay-keyframes.png','rear-keyframes.png','gameplay-feet-detail.png',
             'orbit-comparison.gif','sail-comparison.gif','signal-comparison.gif','orbit-long-comparison.gif','sail-long-comparison.gif','signal-long-comparison.gif','gameplay-actual.png']:
    p=work/name
    if p.exists():copy(p,out/name)
copy(work/'CourierMotion.before.cs.txt',out/'Source/CourierMotion.before.cs.txt')
copy(work/'NarrowDiagnostic/Candidate/metrics.json',out/'Notes/narrow-framing-candidate.json')
for name in ['make_comparisons.py','package_review.py']:
    copy(work/name,out/'ReproTools'/name)
integrity=dict(checkedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),baselineCommit=before['commit'],
    protectedCount=len(before['protected']),protectedSha256=before['protected'],runtimeLogsSha256=runtime,
    actualSourceSha256=summary['motionSourceSha256'],matchingSkinSamplesPerStage=117,viewChecksPerStage=351,allViewChecksPass=True,
    originalAndCustomChartsUnchanged=True,sourceAudioUnchanged=True,modelsAndMaterialsUnchanged=True,
    gameplayControllerAndAudioSettingsUnchanged=True,playerBuildsCreated=False)
(out/'integrity.json').write_text(json.dumps(integrity,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(f'PACKAGED: {len(before["protected"])} protected hashes unchanged; 234 actual-skin samples/702 view checks; both complete runtime logs PASS.')
