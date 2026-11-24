"""Validate final image files and prepare mirrored incoming model folders."""
import hashlib
import json
from pathlib import Path
from PIL import Image

ROOT = Path(__file__).resolve().parent
brief = json.loads((ROOT / "brief.json").read_text(encoding="utf-8-sig"))
groups = {"Characters": brief["characters"], "Backpacks": brief["backpacks"], "Hats": brief["hats"], "Scarves": brief["scarves"]}
views = ("front", "back", "right", "left")
files, assets, errors = [], [], []
for category, items in groups.items():
    for item in items:
        folder = ROOT / category / item["id"]
        (ROOT / "Incoming" / category / item["id"]).mkdir(parents=True, exist_ok=True)
        meta = folder / "generation.json"
        if not meta.is_file():
            errors.append(str(meta.relative_to(ROOT)))
        else:
            json.loads(meta.read_text(encoding="utf-8-sig"))
        assets.append({"category": category, "id": item["id"], "name": item["name"], "views": list(views)})
        for view in views:
            path = folder / (view + ".png")
            if not path.is_file():
                errors.append(str(path.relative_to(ROOT)))
                continue
            with Image.open(path) as image:
                width, height = image.size
                assert image.format == "PNG" and min(width, height) >= 1024, path
                image.verify()
            files.append({"path": path.relative_to(ROOT).as_posix(), "width": width, "height": height,
                          "bytes": path.stat().st_size, "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
expected = sum(len(items) for items in groups.values()) * 4
assert expected == 160
assert len({item["sha256"] for item in files}) == len(files), "Duplicate reference image"
motion = {
    "scope": "Reference designs for future 3D equipment; no new runtime animation has been installed.",
    "nodes": {"BackpackRoot": "Fixed housing and harness", "TopPivot": "Center of visible top swivel", "TopRotor": "Entire independent moving top assembly"},
    "defaultUnitySpinAxis": "local Y (up)",
    "plannedMotion": {"shortJumps": "noticeable spin plus damped spring response", "longJumps": "visible rotations through flight", "lateralInput": "inertial lean opposite movement", "holds": "rotation independent of and counter to actor turn"},
    "constraints": ["single connected top bearing/root", "no stays or cables across rotating joint", "visible space above housing", "validate full revolution clearance after 3D reconstruction"],
    "backpacks": [{"id": item["id"], "heroId": item["heroId"], "variant": item["variant"], "requiresIndependentTopMesh": True,
                   "unitySpinAxis": "local Z (fore-aft horizontal hub)" if item["id"] == "Fira_Beacon" else "local Y (up)",
                   "pivotLocation": "center of raised rotor hub, not case attachment" if item["id"] == "Fira_Beacon" else "center of visible top root swivel",
                   "movingAssembly": "whole fan plus U-yoke; do not spin fan independently through yoke tips" if item["id"] == "Soli_Helio" else "complete independent top rotor"} for item in brief["backpacks"]]
}
(ROOT / "motion-spec.json").write_text(json.dumps(motion, ensure_ascii=False, indent=2), encoding="utf-8")
(ROOT / "Incoming" / "README.ru.md").write_text("Положи модель GLB/FBX и текстуры в папку соответствующего предмета. Тела, рюкзаки, шапки и шарфы создавай отдельно. Корпус рюкзака и верхний подвижный механизм должны оставаться различимыми: после импорта выделим TopRotor и установим TopPivot в шарнире. Подробности и названия — в ../README.ru.md.\n", encoding="utf-8")
manifest = {"complete": not errors and len(files) == expected, "expectedPng": expected, "actualPng": len(files),
            "counts": {key: len(value) for key, value in groups.items()}, "assets": assets, "files": files, "missing": errors,
            "artisticReview": "See QA_NOTES files; this validation verifies files and provenance presence, not mathematical projection equality."}
(ROOT / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2), encoding="utf-8")
print(f"References: {len(files)}/{expected}; missing {len(errors)}; all present PNGs decode and have unique hashes")
if errors:
    print("\n".join(errors))
    raise SystemExit(1)
