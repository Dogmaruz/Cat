"""Pin the thirteen explicitly scoped visual outputs; never rewrite historical receipts."""
import hashlib
import json
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[4]
REPORT = Path(__file__).resolve().parent
PREFIX = "DancingCatsClone/Assets/RadioArchipelago/"
ALLOWED = {
    PREFIX + "Content/Islands/AmberWinds/AmberWinds.asset",
    PREFIX + "Content/Islands/AmberWinds/AmberWinds.prefab",
    PREFIX + "Content/Islands/AmberWinds/RA_Amber_Waterfall.mat",
    PREFIX + "Imported/Islands/AmberWinds/amber_winds_island.fbx",
    PREFIX + "Art/AmberFlowWater.shader",
    *{PREFIX + f"Content/Islands/{island}/{name}" for island, names in (
        ("CoralOasis", ("CoralOasis.asset", "CoralOasis.prefab", "RA_Coral_Waterfall.mat")),
        ("MoonResonance", ("MoonResonance.asset", "MoonResonance.prefab", "RA_Moon_Waterfall.mat")),
    ) for name in names},
    PREFIX + "Imported/Islands/CoralOasis/coral_oasis_island.fbx",
    PREFIX + "Imported/Islands/MoonResonance/moon_resonance_island.fbx",
}
baseline = json.loads((REPORT / "baseline.json").read_text(encoding="utf-8-sig"))
changes = []
for entry in baseline:
    path = ROOT / entry["path"]
    after = hashlib.sha256(path.read_bytes()).hexdigest() if path.is_file() else "MISSING"
    if after != entry["sha256"]:
        assert entry["path"] in ALLOWED, f"Unexpected protected change: {entry['path']}"
        changes.append(dict(path=entry["path"], before=entry["sha256"], after=after))
assert {entry["path"] for entry in changes} == ALLOWED, "Expected exactly the thirteen reviewed visual asset outputs"
source = ROOT / "DancingCatsClone/Assets/RadioArchipelago/Editor/IslandPreservationRevisions.cs"
text = source.read_text(encoding="utf-8")
marker = "        };\n\n        public static string ExpectedHash"
lines = []
edges = {}
for path,before,after in re.findall(r'new Revision\("([^"]+)",\s*"([0-9A-Fa-f]{64})",\s*"([0-9A-Fa-f]{64})"\)',text):
    edge=(path.casefold(),before.upper())
    assert edge not in edges or edges[edge]==after.upper(), f"Ambiguous revision: {edge}"
    edges[edge]=after.upper()
for entry in changes:
    path = entry["path"].removeprefix("DancingCatsClone/")
    before=entry["before"].upper()
    visited=set()
    while (path.casefold(),before) in edges:
        assert before not in visited, f"Revision cycle: {path}"
        visited.add(before)
        before=edges[(path.casefold(),before)]
    after=entry["after"].upper()
    if before!=after:
        lines.append(f'            new Revision("{path}",\n                "{before}",\n                "{after}")')
if lines:
    assert text.count(marker) == 1
    text = text.replace("\n" + marker, ",\n" + ",\n".join(lines) + "\n" + marker)
    source.write_text(text, encoding="utf-8", newline="\n")
report = dict(passed=True, baselineFiles=len(baseline), byteIdentical=len(baseline)-len(changes),
              authorizedChanges=changes, unexpected=[],
              scope="Three finish theme definitions, three unified lake/fall FBX/prefab/material sets and opt-in shared lake shader; exact immutable receipt transitions")
(REPORT / "preservation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print(f"PINNED: {len(changes)} exact outputs; {report['byteIdentical']}/{len(baseline)} protected files unchanged")
