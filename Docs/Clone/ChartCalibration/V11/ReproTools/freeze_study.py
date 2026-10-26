"""Manifest-driven freeze checks without opening heldout metrics or feature values.

Usage: python freeze_study.py check-baselines|freeze|verify-freeze|self-test
The freeze operation refuses an existing permit; archive explicitly for a new experiment.
"""
import argparse, copy, datetime, hashlib, json, pathlib, unittest

PROJECT = pathlib.Path(__file__).resolve().parents[2]
REPO = PROJECT.parent
MANIFEST = PROJECT/'Assets/Clone/Editor/ChartStudies/v11.json'
GROUPS = ('algorithm','measurements','baselines','manifests')

def digest(path, normalized=False):
    data=path.read_bytes()
    if normalized: data=data.replace(b'\r\n',b'\n')
    return hashlib.sha256(data).hexdigest()

def is_hash(value):
    return isinstance(value,str) and len(value)==64 and all(c in '0123456789abcdef' for c in value)

def entry(path, normalized=False):
    path=path.resolve()
    assert path.is_relative_to(REPO) and path.is_file(), str(path)
    return dict(path=path.relative_to(REPO).as_posix(),sha256=digest(path,normalized),normalizedLf=normalized)

def baseline_files(m):
    return [PROJECT/m['studyDirectory']/'benchmark'/s['id']/('baseline'+suffix)
            for s in m['sources'] for suffix in ['.json','.typed.json','.generated.timeline.json']]

def manifest_files(m):
    study=PROJECT/m['studyDirectory']
    return [MANIFEST,PROJECT/m['selectionPath'],study/'baseline-source-inputs.json',study/'before/protected-files.json',study/'before/songs.json']

def measurement_files(m):
    return [PROJECT/'Assets/Clone/Editor'/folder/(s['sourceFileSha256']+suffix)
            for s in m['sources'] for folder,suffix in [('AudioEvidence','.voice.json'),('InstrumentEvidence','.notes.json')]]

def expected_coverage(m):
    rel=lambda p:p.relative_to(REPO).as_posix()
    return dict(algorithm=set(m['algorithmPaths']),measurements={rel(p) for p in measurement_files(m)},
                baselines={rel(p) for p in baseline_files(m)},manifests={rel(p) for p in manifest_files(m)})

def validate_coverage(m, frozen):
    assert frozen.get('beforeValidation') is True, 'Missing before-validation freeze permit'
    timestamp=datetime.datetime.fromisoformat(frozen.get('frozenAtUtc','').replace('Z','+00:00'))
    assert timestamp.tzinfo is not None, 'Freeze time must include a UTC offset'
    expected=expected_coverage(m)
    entries=[]
    for group in GROUPS:
        rows=frozen.get(group)
        assert isinstance(rows,list) and rows, 'Missing/empty freeze group '+group
        assert all(isinstance(r,dict) and isinstance(r.get('path'),str) and is_hash(r.get('sha256'))
                   and type(r.get('normalizedLf')) is bool for r in rows), 'Malformed freeze entries '+group
        paths={r['path'] for r in rows}
        assert expected[group]<=paths, 'Incomplete freeze coverage '+group
        if group in ('measurements','baselines'):
            assert len(rows)==len(expected[group]), 'Unexpected freeze count '+group
        if group!='algorithm':
            assert all(not r['normalizedLf'] for r in rows if r['path'] in expected[group]), 'Expected raw byte hashes '+group
        entries.extend(rows)
    assert len({r['path'] for r in entries})==len(entries), 'Duplicate frozen paths'
    return entries

def check_baselines(m, quiet=False):
    # Read only artifact names, complete triple byte hashes and failure markers.
    # Sealed input values and evaluation metrics remain unopened here.
    seals=json.loads((PROJECT/m['studyDirectory']/'baseline-source-inputs.json').read_text(encoding='utf-8-sig'))['selection']
    assert len(seals)==len(m['sources']) and len({s['id'] for s in seals})==len(seals), 'Incomplete baseline seals'
    by_id={s['id']:s for s in seals}
    for source in m['sources']:
        folder=PROJECT/m['studyDirectory']/'benchmark'/source['id']
        assert not (folder/'baseline.failure.json').exists(), 'Baseline failure '+source['id']
        seal=by_id[source['id']]
        for suffix,key in [('.json','reportSha256'),('.typed.json','typedSha256'),('.generated.timeline.json','timelineSha256')]:
            assert digest(folder/('baseline'+suffix))==seal[key], 'Changed baseline bytes '+source['id']+suffix
    if not quiet: print('BASELINES PASS:',len(m['sources']),'immutable triples, sealed byte hashes verified; metrics unopened')

def verify_entries(entries):
    for row in entries:
        target=(REPO/row['path']).resolve()
        assert target.is_relative_to(REPO) and target.is_file(), 'Missing/outside protected path '+row['path']
        assert digest(target,row['normalizedLf'])==row['sha256'], 'Changed protected bytes '+row['path']

def check_protection(m):
    study=PROJECT/m['studyDirectory']
    snapshot=study/'before/protected-files.json'
    catalog=study/'before/songs.json'
    assert digest(snapshot)==m['protectedSnapshotSha256'], 'Changed initial protected-file inventory'
    assert digest(catalog)==m['protectedCatalogSha256'], 'Changed initial raw catalog snapshot'
    rows=json.loads(snapshot.read_text(encoding='utf-8-sig'))['files']
    assert rows and all(r.get('normalizedLf') is False and is_hash(r.get('sha256')) for r in rows), 'Malformed initial file snapshot'
    assert len({r['path'] for r in rows})==len(rows), 'Duplicate initial protected paths'
    verify_entries(rows)

def make_freeze(m):
    return dict(beforeValidation=True,frozenAtUtc=datetime.datetime.now(datetime.timezone.utc).isoformat(),
        scope='Manifest-pinned PCM/measurement/options paired study; heldout candidates begin only after this freeze.',
        algorithm=[entry(REPO/p,True) for p in m['algorithmPaths']],measurements=[entry(p) for p in measurement_files(m)],
        baselines=[entry(p) for p in baseline_files(m)],manifests=[entry(p) for p in manifest_files(m)])

def self_test(m):
    coverage=expected_coverage(m)
    valid=dict(beforeValidation=True,frozenAtUtc='2026-10-26T00:00:00+00:00',
               **{g:[dict(path=p,sha256='a'*64,normalizedLf=(g=='algorithm')) for p in sorted(coverage[g])] for g in GROUPS})
    class CoverageTests(unittest.TestCase):
        def test_complete(self): validate_coverage(m,valid)
        def test_empty_and_missing_groups(self):
            for group in GROUPS:
                for empty in ([],None):
                    with self.subTest(group=group,empty=empty):
                        bad=copy.deepcopy(valid);bad[group]=empty
                        with self.assertRaises(AssertionError): validate_coverage(m,bad)
        def test_incomplete_coverage(self):
            for group in GROUPS:
                with self.subTest(group=group):
                    bad=copy.deepcopy(valid);bad[group]=bad[group][1:]
                    with self.assertRaises(AssertionError): validate_coverage(m,bad)
        def test_unbound_initial_snapshots(self):
            bad=copy.deepcopy(valid);bad['manifests']=[r for r in bad['manifests'] if '/before/' not in r['path']]
            with self.assertRaises(AssertionError): validate_coverage(m,bad)
        def test_normalized_measurement_and_duplicate(self):
            bad=copy.deepcopy(valid);bad['measurements'][0]['normalizedLf']=True
            with self.assertRaises(AssertionError): validate_coverage(m,bad)
            bad=copy.deepcopy(valid);bad['algorithm'].append(copy.deepcopy(bad['algorithm'][0]))
            with self.assertRaises(AssertionError): validate_coverage(m,bad)
        def test_bad_permit_and_hash(self):
            bad=copy.deepcopy(valid);bad['beforeValidation']=False
            with self.assertRaises(AssertionError): validate_coverage(m,bad)
            bad=copy.deepcopy(valid);bad['baselines'][0]['sha256']='invalid'
            with self.assertRaises(AssertionError): validate_coverage(m,bad)
    result=unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(CoverageTests))
    assert result.wasSuccessful(), 'Freeze coverage negative fixtures failed'

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['check-baselines','freeze','verify-freeze','self-test'])
    args=parser.parse_args()
    m=json.loads(MANIFEST.read_text(encoding='utf-8-sig'))
    study=PROJECT/m['studyDirectory']
    assert study.resolve().is_relative_to(PROJECT/'Library')
    if args.action=='self-test': self_test(m);return
    check_baselines(m)
    if args.action=='check-baselines': return
    check_protection(m)
    target=study/'freeze.json'
    if args.action=='verify-freeze':
        frozen=json.loads(target.read_text())
        entries=validate_coverage(m,frozen);verify_entries(entries)
        print('FREEZE PASS:',len(entries),'required file coverage and actual hashes verified')
        return
    assert not target.exists(), 'Never replace a freeze permit; archive explicitly first'
    frozen=make_freeze(m)
    entries=validate_coverage(m,frozen)
    target.write_text(json.dumps(frozen,indent=2)+'\n',encoding='utf-8')
    print('FREEZE CREATED:',target,'with',len(entries),'source-bound files')

if __name__=='__main__': main()
