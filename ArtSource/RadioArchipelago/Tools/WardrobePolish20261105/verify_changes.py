from pathlib import Path
import hashlib, json, re

ROOT = Path(__file__).resolve().parents[4]
PROJECT = ROOT/'DancingCatsClone'
OUT = ROOT/'Docs/Design/RadioArchipelago/WardrobePolish20261105'
SELECTED = {'backpack_Boro','backpack_Miro','backpack_Miro_Garden','backpack_Piko_Voyager','backpack_Vela_Cloud'}

def original_before_xyz(name, text):
    if name.endswith('/CourierEquipmentDefinition.cs'):
        return text.replace('        // Independent hat-axis multipliers; the uniform scale remains the authored base size.\n        public Vector3 axisScale = Vector3.one;\n', '')
    if name.endswith('/CourierWardrobeRig.cs'):
        text=text.replace('            if (kind == CourierEquipmentKind.Hat && fit != null)\n                instance.transform.localScale = Vector3.Scale(instance.transform.localScale, HatAxisScale(fit.axisScale));\n', '')
        return re.sub(r'\n        private static Vector3 HatAxisScale\(Vector3 value\)\n        \{.*?\n        \}\n', '', text, flags=re.S)
    return text

def legacy_fit(text):
    # Only newly serialized identity axes are irrelevant to legacy fit comparison.
    return re.sub(r'(?m)^      axisScale: \{x: 1, y: 1, z: 1\}\n', '', text)

def fit_parts(text):
    parts = re.split(r'(?m)^    - equipmentId: ', text)
    return parts[0], {p.splitlines()[0]:p for p in parts[1:]}

def verify():
    protected = json.loads((OUT/'protected-before.json').read_text(encoding='utf8'))
    unexpected=[]; expected_xyz=[]
    for name, digest in protected.items():
        p=PROJECT/name
        if not p.is_file(): unexpected.append(name); continue
        if hashlib.sha256(p.read_bytes()).hexdigest()!=digest:
            restored=original_before_xyz(name,p.read_text(encoding='utf8'))
            if hashlib.sha256(restored.encode('utf8')).hexdigest()==digest:
                expected_xyz.append(name)
            else: unexpected.append(name)
    before_root=OUT/'Before'
    changed_hats=[]; changed_packs=[]; preserved_fits=[]; outside_fit_changes=[]
    heroes=PROJECT/'Assets/RadioArchipelago/Content/Modular/Characters'
    for p in heroes.glob('*.prefab'):
        old=legacy_fit((before_root/p.relative_to(PROJECT)).read_text(encoding='utf8'))
        new=legacy_fit(p.read_text(encoding='utf8'))
        old_header, old_fits=fit_parts(old); new_header,new_fits=fit_parts(new)
        if old_header!=new_header or old_fits.keys()!=new_fits.keys(): outside_fit_changes.append(p.stem)
        for name, previous in old_fits.items():
            now=new_fits.get(name)
            allowed_hat=name.startswith('hat_') and name!='hat_SignalDiadem'
            if previous!=now:
                if allowed_hat:changed_hats.append(p.stem+'/'+name)
                elif name in SELECTED:changed_packs.append(p.stem+'/'+name)
                else:outside_fit_changes.append(p.stem+'/'+name)
            else:preserved_fits.append(p.stem+'/'+name)
    catalog_path=Path('Assets/RadioArchipelago/Content/CourierCatalog.asset')
    old=(before_root/catalog_path).read_text(encoding='utf8')
    new=(PROJECT/catalog_path).read_text(encoding='utf8')
    removed=re.sub(r'(?ms)^  - id: hat_SignalDiadem\n.*?(?=^  - id: |\Z)','',old)
    catalog_only_diadem_removed = removed==new and '  - id: hat_SignalDiadem\n' not in new
    icon_dir=PROJECT/'Assets/RadioArchipelago/Content/Modular/Icons'
    updated_icons=[]; icon_failures=[]
    for p in icon_dir.glob('*'):
        if not p.is_file():continue
        previous=before_root/p.relative_to(PROJECT)
        if not previous.is_file():icon_failures.append(p.name);continue
        same=hashlib.sha256(previous.read_bytes()).digest()==hashlib.sha256(p.read_bytes()).digest()
        if p.name.startswith('Backpacks_') and p.suffix=='.png':
            if same:icon_failures.append(p.name+' did not update')
            else:updated_icons.append(p.name)
        elif not same:icon_failures.append(p.name+' changed unexpectedly')
    report={'protectedFiles':len(protected),'unexpectedProtectedChanges':unexpected,
            'verifiedMinimalHatXYZSourceChanges':expected_xyz,
            'changedHatFits':len(changed_hats),'changedBackpackFits':len(changed_packs),
            'gameHatPosesRetained':not changed_hats,
            'preservedEquipmentFits':len(preserved_fits),'outsidePermittedFitChanges':outside_fit_changes,
            'catalogOnlyDiademRemoved':catalog_only_diadem_removed,
            'updatedBackpackIcons':len(updated_icons),'iconFailures':icon_failures}
    proof_path=OUT/'Workshop/AfterAll/report.json'
    if proof_path.exists():
        proof=json.loads(proof_path.read_text(encoding='utf8'))
        report['nativeWorkshop']={k:proof[k] for k in ['passed','mouseDragPassed','touchDragPassed','arrowsRemoved','preferencesRestored']}
        report['nativeWorkshop']['images']=len(proof['images'])
        report['nativeWorkshop']['scope']='Functional pointer/selection checks only; provisional hat art is not accepted.'
    axis_path=OUT/'hat-axis-runtime-checks.json'
    report['nativeHatXYZ']=json.loads(axis_path.read_text(encoding='utf8')) if axis_path.exists() else {'passed':False}
    workbench=ROOT/'Docs/Design/RadioArchipelago/HatFittingWorkbench/Validation'
    report['manualWorkbench']={}
    for key,path in [('roundTrip',workbench/'RoundTrip.json'),('inventory',workbench/'Inventory.json'),('reopen',OUT/'hat-fitting-scene-reopen.json')]:
        report['manualWorkbench'][key]=json.loads(path.read_text(encoding='utf8')) if path.exists() else {'passed':False}
    scarves=ROOT/'ArtSource/RadioArchipelago/TripoReferences/Scarves_20261105'
    scarf_check=json.loads((scarves/'verification.json').read_text(encoding='utf8'))
    report['scarfReferences']={'concepts':scarf_check['concepts'],'views':scarf_check['separateViews'],
        'passed':scarf_check['passed'] and all((scarves/p['concept']/(p['view']+'.png')).is_file() and
            hashlib.sha256((scarves/p['concept']/(p['view']+'.png')).read_bytes()).hexdigest()==p['sha256'] for p in scarf_check['images'])}
    report['passed']=not unexpected and not outside_fit_changes and catalog_only_diadem_removed and not icon_failures and \
        len(updated_icons)==20 and len(changed_packs)==50 and not changed_hats and len(expected_xyz)==2 and report['nativeHatXYZ']['passed'] and \
        all(r['passed'] for r in report['manualWorkbench'].values()) and \
        report.get('nativeWorkshop',{}).get('passed',False) and report['nativeWorkshop']['images']==383 and \
        all(report['nativeWorkshop'][k] for k in ['mouseDragPassed','touchDragPassed','arrowsRemoved','preferencesRestored']) and report['scarfReferences']['passed']
    (OUT/'change-verification.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report,indent=2))
    raise SystemExit(0 if report['passed'] else 1)

if __name__=='__main__':verify()
