"""Verify all existing island/music/chart/scene/settings assets remain byte-identical."""
import hashlib
import json
from pathlib import Path

folder = Path(__file__).resolve().parent
records = json.loads((folder / "Before/preserved-assets.json").read_text(encoding="utf-8-sig"))
errors = []
for record in records:
    target = Path(record["Path"])
    if not target.is_file():
        errors.append({"path": str(target), "reason": "missing"})
    elif hashlib.sha256(target.read_bytes()).hexdigest().upper() != record["Hash"]:
        errors.append({"path": str(target), "reason": "changed"})
print(json.dumps({"passed": not errors, "protectedFiles": len(records), "errors": errors}, ensure_ascii=False, indent=2))
raise SystemExit(bool(errors))
