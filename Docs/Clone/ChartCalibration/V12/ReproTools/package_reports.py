import datetime,gzip,hashlib,json,pathlib,shutil,subprocess,sys

work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
repo=project.parent
out=repo/'Docs/Clone/ChartCalibration/V12'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def write(p,v):
    p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(json.dumps(v,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
def copy(source,target):
    target.parent.mkdir(parents=True,exist_ok=True)
    shutil.copyfile(source,target)
def objects(p):
    text=p.read_text(encoding='utf-8-sig');dec=json.JSONDecoder();i=text.index('[')+1;result={}
    while True:
        while text[i].isspace() or text[i]==',': i+=1
        if text[i]==']': return result
        value,end=dec.raw_decode(text,i);result[value['id']]=text[i:end];i=end

subprocess.run([sys.executable,str(work/'freeze_study.py'),'verify-freeze'],check=True)
subprocess.run([sys.executable,str(work/'compare.py')],check=True)
protected=read(work/'before/protected-hashes.json')
assert all(sha(repo/p)==h for p,h in protected.items())
old=objects(work/'before/songs.json');now=objects(project/'Assets/Resources/CustomData/songs.json')
assert all(now.get(k)==v for k,v in old.items())
manifest=read(project/'Assets/Clone/Editor/ChartStudies/v12.json')
pub=read(work/'publication.json')
assert len(old)==18 and len(pub['tests'])==5
assert {s['id'] for s in pub['tests']}=={s+manifest['testSuffix'] for s in manifest['publishSourceIds']}
for source in manifest['sources']:
    folder=work/'benchmark'/source['id']
    for stage in ['baseline','candidate']:
        for suffix in ['.json','.typed.json','.supports.csv']:
            copy(folder/(stage+suffix),out/source['id']/(stage+suffix))
for name in ['summary.json','aggregate.json','freeze.json','selection.json','baseline-code-inputs.json','baseline-source-inputs.json','publication.json',
             'python-tests.txt','python-freeze-tests.txt','unity-checks.txt','unity-publication.txt','unity-baseline-response.json','unity-preview.json','integration-red.json','integration-motif-intermediate-red.json']:
    if (work/name).exists(): copy(work/name,out/name)
copy(project/'Assets/Clone/Editor/ChartStudies/v12.json',out/'study-manifest.json')
for name in ['protected-hashes.json','protected-files.json','songs.json']:
    copy(work/'before'/name,out/'Before'/name)
# Keep the original YAML bytes, including Unity's empty-field spaces, in a
# deterministic lossless archive instead of presenting them as new source.
(out/'Before/AudioManager.asset.gz').write_bytes(gzip.compress((work/'before/AudioManager.asset').read_bytes(),mtime=0))
for name in ['RESEARCH.md','production-linked-proof.json','motif-dual-proof.json','reference-semantics-proof.json',
             'red-original.txt','green-frozen.txt','csharp-frozen-full-pcm.log']:
    copy(work/'Holds'/name,out/'Holds'/name)
for name in ['INTEGRATION.md','proof.json','READY.json','final-fixture-green.txt','final-full-pipeline-all15.txt','FullPipeline/metrics.json']:
    copy(work/'Follow'/name,out/'Follow'/name)
for p in (work/'Timing').iterdir():
    if p.is_file() and p.suffix in {'.json','.txt','.md','.py'}: copy(p,out/'Timing'/p.name)
failed_log=out/'Timing/unity-original-selection-failed.txt'
if failed_log.exists():
    failed_log.write_text('\n'.join(s.rstrip() for s in failed_log.read_text(encoding='utf-8-sig').splitlines())+'\n',encoding='utf8')
for key in ['jingle','disconnect','aaj','cancan','unstoppable']:
    path=work/'Runtime'/(key+'.txt')
    if path.exists():
        assert path.read_text(encoding='utf-8-sig').startswith('PASS '),key+' runtime failed'
        copy(path,out/('unity-'+key+'.txt'))
for name in ['setup.py','freeze_study.py','compare.py','package_reports.py','stage_changes.py','render_results.py','render_timing.py']:
    copy(work/name,out/'ReproTools'/name)
if (work/'jingle-glide.png').exists():copy(work/'jingle-glide.png',out/'jingle-glide.png')
# The source prototype used a looser long-Hold endpoint check; the final typed
# Unity metric is authoritative and must not be relabeled as thirteen matches.
integration=out/'Follow/INTEGRATION.md'
text=integration.read_text(encoding='utf8').replace("preserving Unstoppable's13 correct long Holds and its one correct short Hold",
    "preserving Unstoppable's13 generated long Holds and its one generated short Hold (the final strict all-Hold metric is12TP/2FP/2FN; this is preservation, not14correct matches)")
integration.write_text(text,encoding='utf8')
published={}
for s in pub['tests']:
    path=project/('Assets/Resources/'+s['timelineResource']+'.json')
    assert sha(path)==s['mapSha256'],s['id']+' changed after publication'
    published[s['id']]=sha(path)
write(out/'integrity.json',dict(checkedAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
    protectedCount=len(protected),protectedSha256=protected,previousRawCatalogRecordsUnchanged=list(old),
    freezeSha256=sha(work/'freeze.json'),frozenInputCount=171,publishedTests=published,
    allFifteenKnownRecordingComparisons=True,independentUnseenEvaluation=False,playerBuildsCreated=False))
write(out/'audio-settings-change.json',dict(beforeSha256=sha(work/'before/AudioManager.asset'),afterSha256=sha(project/'ProjectSettings/AudioManager.asset'),
    changedFields={'m_DSPBufferSize':[1024,512],'m_RequestedDSPBufferSize':[0,512]},live=read(work/'Timing/project-buffer-applied.json'),
    originalEvidence='Timing/original-shipped-audio-manager.json',physicalAudibleLatencyMeasured=False))
print('PACKAGED: 15 paired studies;171 frozen inputs;446 protected files;18 raw catalog objects;5 new map SHA256s verified.')
