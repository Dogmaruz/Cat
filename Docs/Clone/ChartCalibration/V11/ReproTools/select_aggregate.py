"""Select fresh study sources using aggregate gesture counts and source hashes only."""
import collections, hashlib, json, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
OUT = ROOT / 'Library/ChartGenerationV11'
def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
known = json.loads((ROOT/'Library/ChartGenerationV10/selection.json').read_text(encoding='utf-8'))['selection']
excluded = {s['sourceFileSha256'] for s in known}
# File names and cache directories identify every recording already measured. No feature bodies are read.
for base in [ROOT/'Library/VocalAnalysis', ROOT/'Assets/Clone/Editor/InstrumentEvidence', ROOT/'Assets/Clone/Editor/AudioEvidence']:
    if base.exists():
        for path in base.iterdir():
            token = path.name.split('.')[0]
            if len(token)==64 and all(c in '0123456789abcdef' for c in token): excluded.add(token)
# Reproduction uses the exclusion inventory captured before the newly selected audio was measured.
# Otherwise creating this study's caches would silently alter the original source choice.
proposal_path=OUT/'selection-proposal.json'
if proposal_path.exists():
    prior=json.loads(proposal_path.read_text(encoding='utf-8'))
    assert prior['sourceCatalogSha256']==sha(ROOT/'Assets/Resources/OriginalData/songs.json'), 'Original catalog changed'
    excluded=set(prior['excludedSourceSha256'])
rows=[]
catalog=json.loads((ROOT/'Assets/Resources/OriginalData/songs.json').read_text(encoding='utf-8'))
for song in catalog:
    map_path=ROOT/'Assets/Resources/OriginalData/Maps'/f"{song['id']}.timeline.json"
    stem=pathlib.Path(song['audioFile']).stem
    matches=[p for p in (ROOT/'Assets/Resources/OriginalAudio').rglob(stem+'.*') if p.suffix!='.meta']
    assert len(matches)==1,(song['id'],matches)
    source=matches[0]
    source_hash=sha(source)
    if source_hash in excluded: continue
    data=json.loads(map_path.read_text(encoding='utf-8'))
    counts=collections.Counter(s['kind'] for s in data['segments'])
    holds=[s['duration'] for s in data['segments'] if s['kind']=='Hold']
    follows=[s['duration'] for s in data['segments'] if s['kind']=='Follow']
    rows.append(dict(id=song['id'],title=song['title'],artist=song['artist'],duration=song['audioLengthSeconds'],
        sourceFile=source.relative_to(ROOT).as_posix(),sourceFileSha256=source_hash,referenceMapSha256=sha(map_path),
        typeCounts=dict(sorted(counts.items())),shortHoldCount=sum(d<=.45+1e-6 for d in holds),followCount=len(follows),
        followDurationBins=dict(collections.Counter('<=.5' if d<=.5 else '.5-1' if d<=1 else '1-2' if d<=2 else '>2' for d in follows))))
rows.sort(key=lambda r:r['id'])
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'aggregate-candidates.json').write_text(json.dumps(dict(excludedSourceSha256=sorted(excluded),candidates=rows),indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def pick(pool, key): return sorted(pool,key=lambda r:(-key(r),r['id']))[0]
fresh=[]
def select(row,role,criterion):
    chosen=dict(row,role=role,criterion=criterion)
    fresh.append(chosen)
    rows[:] = [r for r in rows if r['sourceFileSha256']!=row['sourceFileSha256']]
for _ in range(2):
    select(pick([r for r in rows if r['artist']=='Piano Songs' and r['shortHoldCount']>0],lambda r:r['shortHoldCount']/r['duration']),
           'calibration','Top remaining Piano Songs shortHold density, positive shortHold count; ordinal ID tie-break.')
select(pick([r for r in rows if r['artist']=='Cover Songs' and r['followCount']>=2],lambda r:r['followCount']/r['duration']),
       'calibration','Top remaining Cover Songs Follow density with multiple Follow; ordinal ID tie-break; varies musical source category.')
select(pick(rows,lambda r:r['shortHoldCount']/r['duration']),'heldout-short-hold','Maximum remaining shortHold density; ordinal ID tie-break; source-audio distinct.')
select(pick(rows,lambda r:r['followCount']/r['duration']),'heldout-follow','Maximum remaining Follow density; ordinal ID tie-break; source-audio distinct.')
proposal=dict(schemaVersion=1,baselineCommit='fd39ed4',sourceCatalogCount=len(catalog),sourceCatalogSha256=sha(ROOT/'Assets/Resources/OriginalData/songs.json'),
    selectionPolicy='Aggregate type/duration counts and source hashes only. No new heldout event times, paths, audio features or candidate errors opened. Excludes all ten v10 sources and all previously measured cache source hashes.',
    excludedSourceSha256=sorted(excluded),selection=fresh)
(OUT/'selection-proposal.json').write_text(json.dumps(proposal,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print(json.dumps(fresh,indent=2,ensure_ascii=True))
