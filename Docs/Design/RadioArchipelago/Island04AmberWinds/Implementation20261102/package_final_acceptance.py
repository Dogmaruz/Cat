from pathlib import Path
import json,hashlib,shutil
from PIL import Image
root=Path.cwd(); folder=Path(__file__).parent
native=root/'DancingCatsClone/Library/RadioArchipelago/AmberWinds'; out=folder/'FinalAcceptance'; out.mkdir(exist_ok=True)
for name in ('island-checks.json','terrain-clearance.json','chart-authoring.json','rooftop-motion-preview.json'):
 shutil.copy2(native/name,out/name)
for src,name in [(native/'SeamDiagnosis/comparison.json','imported-seam-continuity.json'),(native/'PlayReview/play-review.json','full-native-play-review.json'),(root/'DancingCatsClone/Library/RadioArchipelago/PublicIslandMenu/menu-checks.json','menu-checks.json'),(root/'DancingCatsClone/Library/RadioArchipelago/PublicIslandMenu/menu-review-state.json','menu-live-state.json'),(native/'WaterNative/live-clock.json','native-water-clock.json'),(native/'WaterNative/side-cascade-visibility.json','side-cascade-visibility.json')]: shutil.copy2(src,out/name)
for name in ('overview.png','portrait-route-155.png','portrait-route-170.png','portrait-route-185.png','portrait-route-210.png','portrait-route-244.png','portrait-route-506.png'):
 shutil.copy2(native/'Scenery'/name,out/name)
play=json.loads((native/'PlayReview/play-review.json').read_text(encoding='utf8')); run=play['runId']
for suffix in ('terrain-review-clock-19.5','hold-riding','cloud-crossing','signal-spark-ground-line','finish-departure-leap','result'):
 src=native/'PlayReview'/('play-'+run+'-'+suffix+'.png')
 if src.exists(): shutil.copy2(src,out/('gameplay-'+suffix+'.png'))
water=native/'WaterNative'
for tag in ('stream','roll210','side-roll','waterfall'):
 d=json.loads((water/(tag+'-clock.json')).read_text(encoding='utf8'))
 images=[Image.open(water/(tag+f'-flow-{i:02}.png')).convert('RGB') for i in range(14)]
 palette=images[0].quantize(colors=128); frames=[image.quantize(palette=palette,dither=Image.Dither.NONE) for image in images]
 times=d['frameTimes']; durations=[max(20,round((b-a)*1000)) for a,b in zip(times,times[1:])]+[100]
 frames[0].save(out/(tag+'-native-flow.gif'),save_all=True,append_images=frames[1:],duration=durations,loop=0,optimize=False)
 for suffix in ('clock.json','direction.json'):
  src=water/(tag+'-'+suffix)
  if src.exists(): shutil.copy2(src,out/src.name)
source=root/'DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/AmberWinds'
hashes={str(p.relative_to(root)):hashlib.sha256(p.read_bytes()).hexdigest() for p in source.iterdir() if p.suffix!='.meta'}
hashes[str(Path('DancingCatsClone/Assets/RadioArchipelago/Art/CoralWater.shader'))]=hashlib.sha256((root/'DancingCatsClone/Assets/RadioArchipelago/Art/CoralWater.shader').read_bytes()).hexdigest()
hashes[str(Path('DancingCatsClone/Assets/Resources/CustomAudio/Music/RadioTrio.mp3'))]=hashlib.sha256((root/'DancingCatsClone/Assets/Resources/CustomAudio/Music/RadioTrio.mp3').read_bytes()).hexdigest()
(out/'source-hashes.json').write_text(json.dumps(hashes,indent=2)+'\n',encoding='utf8')
(folder/'staged-code-hashes.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (folder/'UnityStaging').glob('*.cs')},indent=2)+'\n',encoding='utf8')
print('Final actual-native artifact package:',out)
