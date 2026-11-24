from pathlib import Path
import hashlib, json, shutil

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT / 'DancingCatsClone'
OUT = ROOT / 'Docs/Design/RadioArchipelago/WardrobePolish20261105'

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    target = OUT / 'Before'
    if (OUT / 'protected-before.json').exists():
        raise SystemExit('Baseline already exists; refusing to replace it.')
    old = ROOT / 'Docs/Design/RadioArchipelago/HatsAndMotion20261105/protected-before.json'
    protected_names = set(json.loads(old.read_text(encoding='utf8')))
    allowed_runtime = {'Assets/RadioArchipelago/Runtime/UI/CourierMenuPreview.cs',
                       'Assets/RadioArchipelago/Runtime/UI/RadioMenuPresenter.cs'}
    protected_names -= allowed_runtime
    for p in (PROJECT / 'Assets/RadioArchipelago/Content/Modular/Icons').glob('*'):
        if p.is_file() and not p.name.startswith('Backpacks_'):
            protected_names.add(p.relative_to(PROJECT).as_posix())
    protected = {n: hashlib.sha256((PROJECT / n).read_bytes()).hexdigest()
                 for n in sorted(protected_names) if (PROJECT / n).is_file()}
    (OUT / 'protected-before.json').write_text(json.dumps(protected, indent=2), encoding='utf8')
    copied=[]
    for folder in ['Assets/RadioArchipelago/Content/Modular/Characters',
                   'Assets/RadioArchipelago/Content/GeneratedHatFits',
                   'Assets/RadioArchipelago/Content/Modular/Icons']:
        for p in (PROJECT / folder).rglob('*'):
            if p.is_file():
                dest=target / p.relative_to(PROJECT); dest.parent.mkdir(parents=True,exist_ok=True)
                shutil.copy2(p,dest); copied.append(p.relative_to(PROJECT).as_posix())
    p=PROJECT/'Assets/RadioArchipelago/Content/CourierCatalog.asset'
    dest=target/p.relative_to(PROJECT); dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,dest)
    (OUT/'snapshot.json').write_text(json.dumps({'protected':len(protected),'backedUp':copied},indent=2),encoding='utf8')
    print('Baseline protected',len(protected),'files; backed up',len(copied),'wardrobe files.')

if __name__=='__main__': main()
