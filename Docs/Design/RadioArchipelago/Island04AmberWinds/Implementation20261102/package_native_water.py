from pathlib import Path
from PIL import Image
import json,shutil,hashlib
root=Path.cwd(); folder=Path(__file__).parent; src=root/'DancingCatsClone/Library/RadioArchipelago/AmberWinds'
out=folder/'NativeScenery'; out.mkdir(exist_ok=True)
for name in ('scenery-only-checks.json','scenery-terrain-clearance.json','rooftop-motion-preview.json'):
 shutil.copy2(src/name,out/name)
for name in ('overview.png','portrait-route-185.png','portrait-route-200.png','portrait-route-210.png','portrait-route-244.png','portrait-route-506.png','preview-timings.json'):
 shutil.copy2(src/'SceneryOnly'/name,out/name)
for name in ('live-clock.json','stream-clock.json','waterfall-clock.json'):
 shutil.copy2(src/'WaterNative'/name,out/('water-'+name))
for feature in ('stream','waterfall'):
 images=[Image.open(p).convert('RGB') for p in sorted((src/'WaterNative').glob(feature+'-flow-*.png'))]
 palette=images[0].quantize(colors=128)
 frames=[im.quantize(palette=palette,dither=Image.Dither.NONE) for im in images]
 frames[0].save(out/(feature+'-native-flow.gif'),save_all=True,append_images=frames[1:],duration=100,loop=0,optimize=False)
print('Native water GIFs/reports and corrected source art snapshots packaged:',out)
