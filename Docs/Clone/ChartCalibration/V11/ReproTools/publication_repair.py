"""Explicit publication utility repair; never replace the original assessment freeze or outputs.

Usage: python publication_repair.py create|verify|revise|self-test
Only the declared publisher/validation source deltas are permitted; heldout evaluation remains strict.
"""
import argparse, copy, datetime, json, os, pathlib, unittest
import freeze_study as fs

PROJECT, REPO, MANIFEST = fs.PROJECT, fs.REPO, fs.MANIFEST
ORIGINAL_FREEZE_SHA256 = '6e0ea0eb8a70959649027b8d48da9d0200157de2b6faa401a99e8f894d971981'
PURPOSE = 'Publication-only double JSON round-trip comparison repair; original assessment freeze and candidate outputs preserved.'
GUARD = 'DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.PublicationRepair.cs'
ALLOWED = ['DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.Publication.cs',
           'DancingCatsClone/Assets/Clone/Editor/ChartGeneratorStudy.Validation.cs']
REVISION_REASON = 'Restore generic manifest publication: the exact recorded v11 freeze requires its repair guard; every other freeze requires strict freeze validation. Assessed candidate outputs and the repair guard source remain unchanged.'

def candidate_files(m):
    return [PROJECT/m['studyDirectory']/'benchmark'/s['id']/('candidate'+suffix)
            for s in m['sources'] for suffix in ['.json','.typed.json','.generated.timeline.json']]

def original_freeze(m):
    path=PROJECT/m['studyDirectory']/'freeze.json'
    assert fs.digest(path)==ORIGINAL_FREEZE_SHA256, 'The original assessment freeze must remain byte-for-byte unchanged'
    frozen=json.loads(path.read_text())
    fs.validate_coverage(m,frozen)
    return frozen

def validate_structure(m, frozen, proof):
    assert proof['schemaVersion']==1 and proof['purpose']==PURPOSE
    assert proof['originalFreezeSha256']==ORIGINAL_FREEZE_SHA256
    assert proof['originalFrozenAtUtc']==frozen['frozenAtUtc'] and proof['originalFreezeScope']==frozen['scope'], 'Changed assessment metadata'
    assert datetime.datetime.fromisoformat(proof['createdAtUtc']).tzinfo is not None
    assert proof['doubleMaxUlps']==2 and proof['finiteDoubles'] is True and proof['sameSignDoubles'] is True
    assert proof['absoluteEpsilon']==0 and proof['floatComparison']=='exact' and proof['discreteComparison']=='exact', 'Widened publication repair'
    changes=proof['changedSources']
    assert len(changes)==len(ALLOWED) and {r['path'] for r in changes}==set(ALLOWED), 'Only two declared publication utilities may change'
    before={r['path']:r for r in frozen['algorithm']}
    for row in changes:
        assert row['normalizedLf'] is True and before[row['path']]['normalizedLf'] is True
        assert row['beforeSha256']==before[row['path']]['sha256'] and fs.is_hash(row['afterSha256'])
        assert row['beforeSha256']!=row['afterSha256'], 'An actual declared utility delta is required'
    guard=proof['guardSource']
    assert guard['path']==GUARD and guard['normalizedLf'] is True and fs.is_hash(guard['sha256']), 'Pin the new guard source'
    candidates=proof['candidates']
    expected={p.relative_to(REPO).as_posix() for p in candidate_files(m)}
    assert len(candidates)==len(expected) and {r['path'] for r in candidates}==expected, 'Pin every assessed candidate artifact'
    assert all(r['normalizedLf'] is False and fs.is_hash(r['sha256']) for r in candidates), 'Candidate hashes must retain raw bytes'

def verify_repair(m, frozen, proof, quiet=False):
    validate_structure(m,frozen,proof)
    verify_revision_history(m,frozen,proof)
    fs.check_baselines(m,quiet=True)
    fs.check_protection(m)
    changes={r['path']:r for r in proof['changedSources']}
    entries=fs.validate_coverage(m,frozen)
    for row in entries:
        replacement=changes.get(row['path'])
        if replacement:
            row=dict(path=row['path'],sha256=replacement['afterSha256'],normalizedLf=True)
        fs.verify_entries([row])
    fs.verify_entries([proof['guardSource']]+proof['candidates'])
    for source in m['sources']:
        assert not (PROJECT/m['studyDirectory']/'benchmark'/source['id']/'candidate.failure.json').exists(), 'Candidate failure marker'
    if not quiet: print('PUBLICATION REPAIR PASS:',len(entries),'original frozen inputs checked; exactly2utility deltas; guard pinned;',len(proof['candidates']),'assessed candidate artifacts unchanged')

def validate_revision_link(m,frozen,proof,previous,previous_sha):
    validate_structure(m,frozen,previous)
    assert previous.get('revision',1)==1, 'This explicit revision retains the original v1 proof'
    assert proof.get('revision')==2 and proof.get('revisionReason')==REVISION_REASON
    assert proof.get('previousProofSha256')==previous_sha and fs.is_hash(previous_sha), 'Bind the exact previous proof raw bytes'
    expected_path=(PROJECT/m['studyDirectory']/'publication-repair.v1.json').relative_to(REPO).as_posix()
    assert proof.get('previousProofPath')==expected_path
    assert proof['guardSource']==previous['guardSource'], 'The approved guard source cannot change in this utility revision'
    assert proof['candidates']==previous['candidates'], 'All assessed candidate byte pins must remain unchanged'
    assert sorted((r['path'],r['beforeSha256']) for r in proof['changedSources'])==sorted((r['path'],r['beforeSha256']) for r in previous['changedSources']), 'Retain the original frozen before-hashes'

def verify_revision_history(m,frozen,proof):
    if proof.get('revision',1)==1:
        assert not any(k in proof for k in ('previousProofSha256','previousProofPath','revisionReason')), 'Partial revision metadata'
        return
    archive=PROJECT/m['studyDirectory']/'publication-repair.v1.json'
    assert fs.digest(archive)==proof.get('previousProofSha256'), 'The archived original proof raw bytes changed'
    validate_revision_link(m,frozen,proof,json.loads(archive.read_bytes()),fs.digest(archive))

def revise_proof(m,frozen,target):
    previous_raw=target.read_bytes();previous=json.loads(previous_raw);previous_sha=fs.digest(target)
    validate_structure(m,frozen,previous)
    assert previous.get('revision',1)==1, 'Only the explicit v1-to-v2 generic publication utility revision is supported'
    fs.check_baselines(m,quiet=True);fs.check_protection(m)
    # Before revising, verify every previous candidate/guard byte pin and all 106 non-utility frozen inputs.
    fs.verify_entries([r for r in fs.validate_coverage(m,frozen) if r['path'] not in ALLOWED])
    fs.verify_entries([previous['guardSource']]+previous['candidates'])
    for source in m['sources']:
        assert not (PROJECT/m['studyDirectory']/'benchmark'/source['id']/'candidate.failure.json').exists()
    archive=target.with_name('publication-repair.v1.json')
    temporary=target.with_name('publication-repair.revision-tmp.json')
    assert not archive.exists() and not temporary.exists(), 'Never overwrite the previous proof archive or an unfinished revision'
    proof=copy.deepcopy(previous)
    proof.update(createdAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),revision=2,
                 previousProofSha256=previous_sha,previousProofPath=archive.relative_to(REPO).as_posix(),revisionReason=REVISION_REASON)
    for row in proof['changedSources']:row['afterSha256']=fs.digest(REPO/row['path'],True)
    validate_structure(m,frozen,proof);validate_revision_link(m,frozen,proof,previous,previous_sha)
    with archive.open('xb') as stream:stream.write(previous_raw)
    verify_repair(m,frozen,proof)
    with temporary.open('x',encoding='utf-8') as stream:stream.write(json.dumps(proof,indent=2)+'\n')
    os.replace(temporary,target)
    print('PUBLICATION REPAIR REVISED: prior raw proof retained at',archive,'; previous SHA',previous_sha)

def build_proof(m, frozen):
    before={r['path']:r for r in frozen['algorithm']}
    return dict(schemaVersion=1,purpose=PURPOSE,createdAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        originalFreezeSha256=ORIGINAL_FREEZE_SHA256,originalFrozenAtUtc=frozen['frozenAtUtc'],originalFreezeScope=frozen['scope'],
        doubleMaxUlps=2,finiteDoubles=True,sameSignDoubles=True,absoluteEpsilon=0,floatComparison='exact',discreteComparison='exact',
        changedSources=[dict(path=p,beforeSha256=before[p]['sha256'],afterSha256=fs.digest(REPO/p,True),normalizedLf=True) for p in ALLOWED],
        guardSource=fs.entry(REPO/GUARD,True),candidates=[fs.entry(p) for p in candidate_files(m)])

def self_test(m, frozen):
    valid=build_proof(m,frozen)
    class RepairTests(unittest.TestCase):
        def test_actual_original_inputs_and_candidate_bytes(self): verify_repair(m,frozen,valid,quiet=True)
        def reject(self,change):
            bad=copy.deepcopy(valid);change(bad)
            with self.assertRaises(AssertionError): verify_repair(m,frozen,bad,quiet=True)
        def test_changed_assessment_identity_or_scope(self):
            self.reject(lambda p:p.update(originalFreezeSha256='0'*64))
            self.reject(lambda p:p.update(originalFreezeScope='changed'))
        def test_inference_or_measurement_repair_not_allowed(self):
            for path in ['DancingCatsClone/Assets/Clone/Editor/AudioChartGenerator.cs',frozen['measurements'][0]['path']]:
                self.reject(lambda p:p['changedSources'][0].update(path=path))
        def test_wrong_before_or_after_source_hash(self):
            self.reject(lambda p:p['changedSources'][0].update(beforeSha256='0'*64))
            self.reject(lambda p:p['changedSources'][0].update(afterSha256='0'*64))
        def test_guard_hash_and_candidate_hash_changes(self):
            self.reject(lambda p:p['guardSource'].update(sha256='0'*64))
            self.reject(lambda p:p['candidates'][0].update(sha256='0'*64))
        def test_candidate_missing_or_normalized(self):
            self.reject(lambda p:p.update(candidates=p['candidates'][:-1]))
            self.reject(lambda p:p['candidates'][0].update(normalizedLf=True))
        def test_repair_cannot_widen_numeric_or_discrete_rules(self):
            self.reject(lambda p:p.update(doubleMaxUlps=3))
            self.reject(lambda p:p.update(absoluteEpsilon=1e-8))
            self.reject(lambda p:p.update(floatComparison='approximate'))
            self.reject(lambda p:p.update(discreteComparison='approximate'))
        def test_revision_cannot_change_candidate_guard_or_before_pins(self):
            previous=copy.deepcopy(valid);revised=copy.deepcopy(valid)
            revised.update(revision=2,previousProofSha256='a'*64,revisionReason=REVISION_REASON,
                           previousProofPath=(PROJECT/m['studyDirectory']/'publication-repair.v1.json').relative_to(REPO).as_posix())
            validate_revision_link(m,frozen,revised,previous,'a'*64)
            for field in ['candidates','guardSource','changedSources']:
                bad=copy.deepcopy(revised)
                if field=='candidates':bad[field][0]['sha256']='0'*64
                elif field=='guardSource':bad[field]['sha256']='0'*64
                else:bad[field][0]['beforeSha256']='0'*64
                with self.subTest(field=field):
                    with self.assertRaises(AssertionError):validate_revision_link(m,frozen,bad,previous,'a'*64)
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(RepairTests))
    assert result.wasSuccessful(), 'Publication repair negative fixtures failed'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['create','verify','revise','self-test'])
    args=parser.parse_args()
    m=json.loads(MANIFEST.read_text());frozen=original_freeze(m)
    target=PROJECT/m['studyDirectory']/'publication-repair.json'
    if args.action=='self-test':self_test(m,frozen);return
    if args.action=='verify':verify_repair(m,frozen,json.loads(target.read_text()));return
    if args.action=='revise':revise_proof(m,frozen,target);return
    assert not target.exists(), 'Never replace a publication repair proof; original freeze/assessment outputs remain untouched'
    proof=build_proof(m,frozen);verify_repair(m,frozen,proof)
    with target.open('x',encoding='utf-8') as stream:stream.write(json.dumps(proof,indent=2)+'\n')
    print('PUBLICATION REPAIR PROOF CREATED:',target)

if __name__=='__main__':main()
