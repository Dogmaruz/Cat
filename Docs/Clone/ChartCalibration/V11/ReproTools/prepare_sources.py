from concurrent.futures import ThreadPoolExecutor
import hashlib,json,pathlib,subprocess,sys,shutil
work=pathlib.Path(__file__).resolve().parent;root=work.parents[2];project=root/'DancingCatsClone'
items=json.loads((work/'selection.json').read_text(encoding='utf-8-sig'))['selection']
def run(item):
 if item['role']=='known':return
 export=project/'Library/VocalAnalysis'/item['sourceFileSha256'];folder=work/'benchmark'/item['id'];folder.mkdir(parents=True,exist_ok=True)
 common=['--input',str(project/item['sourceFile']),'--decoded-pcm',str(export/'source.f32'),'--decoded-metadata',str(export/'source.json'),
 '--deps',str(project/'Library/OriginalChartCalibration/v4/ml-deps'),'--model-dir',str(project/'Library/OriginalChartCalibration/v4/stem-model')]
 for script,filename,assetfolder,suffix in [('analyze_vocal_holds.py','voice.json','AudioEvidence','.voice.json'),('analyze_instrumental_notes.py','notes.json','InstrumentEvidence','.notes.json')]:
  cmd=[sys.executable,str(root/'Tools/RhythmAnalysis'/script),*common,'--output',str(folder/filename),'--cache-dir',str(work/('Cache'+assetfolder))]
  with (folder/(filename+'.analysis-unopened.txt')).open('w')as log:subprocess.run(cmd,cwd=root,stdout=log,stderr=subprocess.STDOUT,check=True)
  dest=project/'Assets/Clone/Editor'/assetfolder/(item['sourceFileSha256']+suffix)
  assert not dest.exists(),'Do not overwrite earlier source evidence';shutil.copyfile(folder/filename,dest)
 print('NEW_SOURCE_INPUTS_READY '+item['id'],flush=True)
with ThreadPoolExecutor(max_workers=2)as pool:list(pool.map(run,items))
