from pathlib import Path
import json,hashlib,shutil
root=Path.cwd(); stage=Path(__file__).with_name('UnityStaging'); baseline=Path(__file__).with_name('Before')/'preserved-existing.json'
rows=json.loads(baseline.read_text(encoding='utf-8-sig')); old={str(Path(x['Path'])):x['Hash'] for x in rows}
files={
'IslandPilotPlayReview.cs':'Assets/RadioArchipelago/Editor/IslandPilotPlayReview.cs',
'RadioIslandMenuPolicy.cs':'Assets/RadioArchipelago/Runtime/UI/RadioIslandMenuPolicy.cs',
'RadioIslandMenuCatalog.cs':'Assets/RadioArchipelago/Runtime/UI/RadioIslandMenuCatalog.cs',
'RadioMenuPresenter.cs':'Assets/RadioArchipelago/Runtime/UI/RadioMenuPresenter.cs',
'RadioIslandMenuJobs.cs':'Assets/RadioArchipelago/Editor/RadioIslandMenuJobs.cs',
'RadioIslandMenuLiveChecks.cs':'Assets/RadioArchipelago/Editor/RadioIslandMenuLiveChecks.cs'}
for name,rel in files.items():
 target=root/'DancingCatsClone'/rel
 assert hashlib.sha256(target.read_bytes()).hexdigest().upper()==old[str(target)], 'Unexpected concurrent edit: '+rel
for name,rel in files.items():
 shutil.copyfile(stage/name,root/'DancingCatsClone'/rel)
bridge=root/'DancingCatsClone/Assets/Clone/Editor/MusicalChartEditorSession.cs'
s=bridge.read_text(encoding='utf8'); needle='                    case "amber-scenery-water-live":'
new='''                    case "radio-trio-publish-approved":
                        RadioArchipelago.Editor.RadioTrioChartAuthoring.PublishApproved();
                        break;
                    case "amber-create":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.Prepare();
                        break;
                    case "amber-checks":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.RunChecks();
                        break;
                    case "amber-open":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.Open();
                        break;
                    case "amber-capture":
                        RadioArchipelago.Editor.AmberWindsIslandJobs.CaptureViews();
                        break;
                    case "amber-playtest":
                        IslandPilotPlayReview.BeginAmberWinds();
                        break;
'''
assert needle in s and 'case "radio-trio-publish-approved":' not in s
bridge.write_text(s.replace(needle,new+needle),encoding='utf8')
print('Installed6 approved integration replacements and explicit publish/full-level queue cases. No publication invoked yet.')
