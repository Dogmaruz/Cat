"""Paired known-recording report; annotations are used only for evaluation."""
import hashlib,json,pathlib

work=pathlib.Path(__file__).resolve().parent
project=work.parents[1]
repo=project.parent
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
m=read(project/'Assets/Clone/Editor/ChartStudies/v12.json')
seals={s['id']:s for s in read(work/'baseline-source-inputs.json')['selection']}
assert len(seals)==len(m['sources'])==15
protected=read(work/'before/protected-hashes.json')
assert all(sha(repo/p)==h for p,h in protected.items()),'An initial protected file changed'
rows=[]
for source in m['sources']:
    folder=work/'benchmark'/source['id']
    seal=seals[source['id']]
    for suffix,key in [('.json','reportSha256'),('.typed.json','typedSha256'),('.generated.timeline.json','timelineSha256')]:
        assert sha(folder/('baseline'+suffix))==seal[key],source['id']+' changed baseline'
    row=dict(id=source['id'],role=source['role'])
    for stage in ['baseline','candidate']:
        assert not (folder/(stage+'.failure.json')).exists(),source['id']+' failed '+stage
        report=read(folder/(stage+'.json'))
        typed=read(folder/(stage+'.typed.json'))
        assert sha(folder/(stage+'.generated.timeline.json'))==typed['generatedTimelineSha256']
        assert report['geometryValid'],source['id']+' invalid geometry'
        row[stage]=dict(generator=report['generator'],geometryValid=True,eventF1_80ms=report['all']['within80ms']['f1'],
            logicalF1_80ms=report['logicalSupports']['within80ms']['f1'],
            types={kind:{field:typed[kind][field] for field in ['referenceCount','generatedCount','truePositive','falsePositive','falseNegative','precision','recall','f1']}
                   for kind in ['shortHolds','allHolds','follows']})
    rows.append(row)
    diff=[]
    for kind in ['shortHolds','allHolds','follows']:
        a=row['baseline']['types'][kind];b=row['candidate']['types'][kind]
        if a!=b: diff.append(f'{kind}: TP {a["truePositive"]}->{b["truePositive"]}, FP {a["falsePositive"]}->{b["falsePositive"]}, FN {a["falseNegative"]}->{b["falseNegative"]}')
    print(source['id']+': '+('; '.join(diff) if diff else 'unchanged typed metrics'))
(work/'summary.json').write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
totals={stage:{kind:{field:sum(row[stage]['types'][kind][field] for row in rows)
    for field in ['referenceCount','generatedCount','truePositive','falsePositive','falseNegative']}
    for kind in ['shortHolds','allHolds','follows']} for stage in ['baseline','candidate']}
(work/'aggregate.json').write_text(json.dumps(totals,indent=2)+'\n',encoding='utf8')
print('All fifteen immutable paired inputs, native-map hashes and geometry verified. No independent unseen-song claim.')
print(json.dumps(totals))
