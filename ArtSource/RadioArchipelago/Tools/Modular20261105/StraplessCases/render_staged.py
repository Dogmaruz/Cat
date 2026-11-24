from pathlib import Path
import bpy,sys
P=Path(__file__).parent;sys.path.insert(0,str(P));import clean_cases as c
names=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else sorted(x.name for x in (P/'Loft').iterdir())
for name in names:
 bpy.ops.wm.open_mainfile(filepath=str(P/'Loft'/name/'editable.blend'),load_ui=False)
 c.render(name)
