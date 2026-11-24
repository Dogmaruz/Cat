from pathlib import Path
p=Path('DancingCatsClone/Assets/RadioArchipelago/Editor/ModularSourceHarnessFitter.cs')
s=p.read_text(encoding='utf-8-sig');Path('ArtSource/RadioArchipelago/Tools/Modular20261104/HarnessRepair/ModularSourceHarnessFitter-v5.cs.txt').write_text(s,encoding='utf-8')
