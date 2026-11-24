"""Compare this shader correction with the exact completed prior water pass."""
import hashlib
import json
from pathlib import Path

here = Path(__file__).resolve().parent
root = here.parents[3]
previous = here.parent / "IslandFinishesAndWaterfalls20261102"
baseline = json.loads((previous / "baseline.json").read_text(encoding="utf-8-sig"))
prior = json.loads((here / "prior-preservation.json").read_text(encoding="utf-8-sig"))
expected = {row["path"]: row["sha256"] for row in baseline}
expected.update({row["path"]: row["after"] for row in prior["authorizedChanges"]})
changed = []
for relative, before in expected.items():
    file = root / relative
    after = hashlib.sha256(file.read_bytes()).hexdigest() if file.is_file() else "MISSING"
    if before != after:
        changed.append(dict(path=relative, before=before, after=after))
assert [row["path"] for row in changed] == ["DancingCatsClone/Assets/RadioArchipelago/Art/AmberFlowWater.shader"], changed
report = dict(passed=True, comparedFiles=len(expected), byteIdentical=len(expected)-len(changed),
              authorizedChanges=changed, unexpected=[],
              scope="Only the shared lake shader changes relative to completed prior pass. Models, materials, prefabs, scenes, first island, music, charts and settings remain byte-identical.")
(here / "preservation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PASS: {len(expected)-1}/{len(expected)} protected files unchanged; only reviewed shader changed")
