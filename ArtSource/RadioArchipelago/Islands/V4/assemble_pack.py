"""Audit and package the ten canonical V4 scenes; preserve incomplete outputs.

Run with the bundled Python (Pillow required), after the Blender render job::
    python assemble_pack.py

Produces index.html, package-audit.json, and (only after a complete passing
audit) RadioArchipelago_Islands_V4_True3D.zip. No source outputs are edited.
Use --audit-only to skip ZIP creation. A failed audit exits 2 after writing the
partial gallery/report. Existing ZIPs are preserved, but never advertised as
current after a failed audit. V1 and V2 are read-only and are not packaged.
"""

import argparse
import hashlib
import json
import math
import os
from pathlib import Path
import struct
import sys
import zipfile

VIEWS = ("front", "right", "left", "back", "top", "isometric")
ORTHO_VIEWS = VIEWS[:5]
ARCHIVE = "RadioArchipelago_Islands_V4_True3D.zip"
FALLBACK_WORLDS = (
    ("01_emerald_ravine", "Изумрудное ущелье"),
    ("02_coral_oasis", "Коралловый оазис"),
    ("03_amethyst_grotto", "Аметистовый грот"),
    ("04_autumn_windmills", "Осенние ветряные холмы"),
    ("05_frost_moon", "Лунный ледник"),
    ("06_tropical_cascade", "Тропические каскады"),
    ("07_storm_coast", "Грозовой берег"),
    ("08_sunflower_terraces", "Солнечные поля"),
    ("09_volcanic_garden", "Вулканический сад"),
    ("10_cloud_observatory", "Облачная обсерватория"),
)


def read_json(path):
    with open(path, encoding="utf-8-sig") as handle:
        return json.load(handle)


def sha256(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def valid_hash(value):
    return isinstance(value, str) and len(value) == 64 and all(c in "0123456789abcdefABCDEF" for c in value)


def status(record):
    if not isinstance(record, dict):
        return None
    return record.get("pass", record.get("passed"))


def camera_records(manifest):
    records = manifest.get("camera_views", manifest.get("cameras", []))
    if isinstance(records, dict):
        return [dict(value, view_id=key) for key, value in records.items() if isinstance(value, dict)]
    return records if isinstance(records, list) else []


def view_id(record):
    value = record.get("view_id", record.get("view", record.get("id", record.get("name", ""))))
    return str(value).lower().removesuffix(".png")


def camera_vectors(record):
    matrix = record.get("matrix_world", record.get("matrix"))
    if isinstance(matrix, list) and len(matrix) == 16 and all(isinstance(v, (int, float)) for v in matrix):
        matrix = [matrix[i:i + 4] for i in range(0, 16, 4)]
    if isinstance(matrix, list) and len(matrix) == 4 and all(isinstance(row, list) and len(row) == 4 for row in matrix):
        position = [matrix[i][3] for i in range(3)]
        forward = [-matrix[i][2] for i in range(3)]
        up = [matrix[i][1] for i in range(3)]
    else:
        position, forward, up = record.get("position", record.get("location")), record.get("forward"), record.get("up")
    for vector in (position, forward, up):
        if not isinstance(vector, (list, tuple)) or len(vector) != 3 or not all(isinstance(c, (int, float)) and math.isfinite(c) for c in vector):
            raise ValueError("camera needs finite matrix_world or position/forward/up")
    return position, forward, up


def angle_degrees(a, b):
    den = math.sqrt(sum(c * c for c in a) * sum(c * c for c in b))
    return math.degrees(math.acos(max(-1.0, min(1.0, sum(a[i] * b[i] for i in range(3)) / den)))) if den > 1e-12 else 180.0


def audit_png(path):
    from PIL import Image
    with Image.open(path) as image:
        if image.format != "PNG":
            raise ValueError("file is not a PNG")
        image.verify()
    with Image.open(path) as image:
        image.load()
        if image.width < 64 or image.height < 64:
            raise ValueError("image is smaller than 64 x 64")
        rgba = image.convert("RGBA")
        if rgba.getextrema()[3][1] == 0:
            raise ValueError("image is entirely transparent")
        rgb = rgba.convert("RGB")
        if all(lo == hi for lo, hi in rgb.getextrema()):
            raise ValueError("image is a uniform blank color")
        return {"width": image.width, "height": image.height, "mode": image.mode,
                "bytes": path.stat().st_size, "sha256": sha256(path)}


def audit_blend(path):
    with open(path, "rb") as handle:
        header = handle.read(8)
    if not (header.startswith(b"BLENDER") or header.startswith(b"\x1f\x8b") or header.startswith(b"\x28\xb5\x2f\xfd")):
        raise ValueError("missing Blender or compressed Blender file signature")
    if path.stat().st_size < 128:
        raise ValueError("Blender file is implausibly small")
    return {"bytes": path.stat().st_size, "sha256": sha256(path)}


def audit_glb(path):
    with open(path, "rb") as handle:
        header = handle.read(12)
        if len(header) != 12:
            raise ValueError("incomplete GLB header")
        magic, version, length = struct.unpack("<4sII", header)
        if magic != b"glTF" or version != 2 or length != path.stat().st_size:
            raise ValueError("GLB magic/version/declared size does not match file")
        chunk_header = handle.read(8)
        if len(chunk_header) != 8:
            raise ValueError("missing GLB JSON chunk")
        size, kind = struct.unpack("<II", chunk_header)
        if kind != 0x4E4F534A or size > length - 20:
            raise ValueError("invalid GLB JSON chunk")
        doc = json.loads(handle.read(size).decode("utf-8").rstrip("\0 \t\r\n"))
    if not doc.get("meshes") or not doc.get("nodes"):
        raise ValueError("GLB contains no model meshes/nodes")
    factors = [tuple(material.get("pbrMetallicRoughness", {}).get("baseColorFactor", [1, 1, 1, 1])) for material in doc.get("materials", [])]
    nonwhite = {color for color in factors if max(abs(v - 1) for v in color[:3]) > .001}
    textured = any(material.get("pbrMetallicRoughness", {}).get("baseColorTexture") for material in doc.get("materials", []))
    if len(nonwhite) < 3 and not textured:
        raise ValueError("GLB lost its material colors (procedural-only Blender shaders exported white)")
    return {"bytes": length, "meshes": len(doc["meshes"]), "nodes": len(doc["nodes"]), "materials": len(factors), "distinct_nonwhite_base_colors": len(nonwhite), "sha256": sha256(path)}


def audit_world(base, world):
    folder = base / "Output" / world["id"]
    result = {"id": world["id"], "title": world.get("title", world["id"]),
              "pass": False, "errors": [], "images": {}, "files": {},
              "geometry_sha256": None, "camera_proofs": []}
    for view in VIEWS:
        path = folder / (view + ".png")
        try:
            result["images"][view] = audit_png(path)
        except Exception as exc:
            result["errors"].append(f"{view}.png: {exc}")
    for filename, function in (("island.blend", audit_blend), ("island.glb", audit_glb)):
        try:
            result["files"][filename] = function(folder / filename)
        except Exception as exc:
            result["errors"].append(f"{filename}: {exc}")
    try:
        manifest = read_json(folder / "manifest.json")
        if manifest.get("id", world["id"]) != world["id"]:
            raise ValueError("manifest ID differs from output folder")
        result["title"] = manifest.get("title", result["title"])
        result["palette"] = manifest.get("palette")
        geometry_hash = manifest.get("geometry_sha256", manifest.get("geometry", {}).get("sha256") if isinstance(manifest.get("geometry"), dict) else None)
        if not valid_hash(geometry_hash):
            raise ValueError("manifest needs a 64-hex geometry_sha256")
        result["geometry_sha256"] = geometry_hash.lower()
        cameras = camera_records(manifest)
        indexed = {view_id(record): record for record in cameras if isinstance(record, dict)}
        directions = {"top": (0, 0, -1), "front": (0, 1, 0), "back": (0, -1, 0), "left": (1, 0, 0), "right": (-1, 0, 0)}
        for view in VIEWS:
            camera = indexed.get(view)
            if camera is None:
                result["errors"].append(f"manifest.camera_views: missing {view}")
                continue
            try:
                position, forward, up = camera_vectors(camera)
                kind = camera.get("type", camera.get("projection", ""))
                if str(kind).upper() not in {"ORTHO", "ORTHOGRAPHIC"}:
                    raise ValueError("camera must be ORTHO")
                scale = camera.get("ortho_scale", camera.get("orthographic_scale"))
                if not isinstance(scale, (int, float)) or not math.isfinite(scale) or scale <= 0:
                    raise ValueError("positive ortho_scale is required")
                direction_error = angle_degrees(forward, directions[view]) if view in directions else None
                if direction_error is not None and direction_error > 0.5:
                    raise ValueError(f"axis direction error {direction_error:.3f} degrees")
                up_error = angle_degrees(up, (0, 1, 0) if view == "top" else (0, 0, 1)) if view != "isometric" else None
                if up_error is not None and up_error > 0.5:
                    raise ValueError(f"up axis error {up_error:.3f} degrees")
                camera_hash = camera.get("geometry_sha256", camera.get("geometry_hash"))
                if camera_hash is not None and str(camera_hash).lower() != geometry_hash.lower():
                    raise ValueError("per-view geometry hash differs from frozen scene")
                result["camera_proofs"].append({"view": view, "type": kind, "position": list(position),
                    "forward": list(forward), "up": list(up), "ortho_scale": scale,
                    "direction_error_degrees": direction_error, "up_error_degrees": up_error,
                    "per_view_geometry_sha256": camera_hash})
            except Exception as exc:
                result["errors"].append(f"camera {view}: {exc}")
        result["files"]["manifest.json"] = {"bytes": (folder / "manifest.json").stat().st_size, "sha256": sha256(folder / "manifest.json")}
    except Exception as exc:
        result["errors"].append(f"manifest.json: {exc}")
    try:
        report = read_json(folder / "validation.json")
        if status(report) is not True:
            raise ValueError("overall pass/passed is not true")
        tests = report.get("checks", report.get("tests", []))
        if isinstance(tests, dict):
            tests = [dict(record, name=name) if isinstance(record, dict) else {"name": name, "pass": record} for name, record in tests.items()]
        if not tests or any(status(test) is not True for test in tests):
            raise ValueError("one or more checks are absent or did not pass")
        geometry_check = next((test for test in tests if test.get("name") == "same_geometry_across_renders"), None)
        if not geometry_check or not geometry_check.get("before_and_after_present"):
            raise ValueError("before/after geometry evidence is absent")
        hashes = geometry_check.get("hashes", [])
        if len(hashes) < 2 or not all(valid_hash(h) for h in hashes) or len({h.lower() for h in hashes}) != 1:
            raise ValueError("before/after hashes are absent, invalid or unequal")
        if result["geometry_sha256"] != hashes[0].lower():
            raise ValueError("validation geometry differs from manifest")
        camera_check = next((test for test in tests if test.get("name") == "five_orthographic_camera_views"), None)
        if not camera_check or camera_check.get("recorded_count", 0) < 5 or camera_check.get("missing_views"):
            raise ValueError("five-view camera evidence is absent")
        proven_cameras = {view_id(camera): camera for camera in camera_check.get("cameras", [])}
        for proof in result["camera_proofs"]:
            if proof["view"] in proven_cameras:
                proven_vectors = camera_vectors(proven_cameras[proof["view"]])
                for label, vector in zip(("position", "forward", "up"), proven_vectors):
                    if any(abs(proof[label][i] - vector[i]) > 1e-5 for i in range(3)):
                        raise ValueError("manifest camera differs from validated camera: " + proof["view"] + "/" + label)
        result["validation_check_count"] = len(tests)
        result["render_hash_snapshot_count"] = len(hashes)
        result["unity_playtest_performed"] = report.get("unity_playtest_performed", False)
        result["files"]["validation.json"] = {"bytes": (folder / "validation.json").stat().st_size, "sha256": sha256(folder / "validation.json")}
    except Exception as exc:
        result["errors"].append(f"validation.json: {exc}")
    result["pass"] = not result["errors"]
    return result


HTML = r'''<!doctype html><html lang="ru"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Радиоархипелаг · V4</title><style>
:root{color-scheme:dark;--bg:#111718;--panel:#192123;--text:#edf2ed;--muted:#a6b5b2;--line:#344343;--accent:#aed6bb}*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.5 system-ui,sans-serif}main{max-width:1440px;margin:auto;padding:32px 28px 44px}header{display:flex;align-items:start;justify-content:space-between;gap:24px}h1{font-size:clamp(24px,3vw,36px);letter-spacing:-.04em;font-weight:600;margin:2px 0 6px}p{margin:0;color:var(--muted)}.eyebrow{font-size:12px;letter-spacing:.13em;text-transform:uppercase;color:var(--accent)}a{color:var(--accent);text-decoration:none}a:hover{text-decoration:underline}.download{border:1px solid var(--line);padding:11px 16px;border-radius:8px;white-space:nowrap}.worldbar{display:flex;flex-wrap:nowrap;gap:8px;overflow-x:auto;margin:24px 0 12px;padding:3px 1px 12px;scrollbar-width:thin}.worldbar button{flex:0 0 auto;max-width:260px;white-space:nowrap}.controls{display:flex;justify-content:space-between;align-items:center;gap:16px;flex-wrap:wrap}button,select{font:inherit;color:var(--text);background:var(--panel);border:1px solid var(--line);border-radius:7px;padding:9px 13px;cursor:pointer}button:hover{border-color:var(--accent)}button.active{background:#344b40;border-color:var(--accent)}.views{display:flex;gap:6px;flex-wrap:nowrap;overflow:auto}select{min-width:225px;max-width:100%}.stage{margin-top:16px;display:grid;grid-template-columns:minmax(0,1fr) 270px;border:1px solid var(--line);border-radius:12px;overflow:hidden;background:var(--panel)}.imagearea{position:relative;min-width:0;background:#d8dfda;display:flex;align-items:center;justify-content:center;min-height:420px}#image{display:block;max-width:100%;width:100%;height:min(71vh,850px);object-fit:contain}#missing{padding:60px;color:#273836;text-align:center}aside{padding:24px;display:flex;flex-direction:column;gap:20px}h2{font-size:20px;letter-spacing:-.025em;line-height:1.25;margin:0 0 9px}h3{font-size:12px;letter-spacing:.07em;text-transform:uppercase;color:var(--muted);margin:0 0 7px}.badge{font-size:12px;color:var(--accent)}.badge.warn{color:#e3ba80}.small{font-size:13px}.links{display:flex;flex-direction:column;gap:7px}.hash{font:11px/1.55 ui-monospace,monospace;overflow-wrap:anywhere;color:var(--muted)}.caption{position:absolute;left:16px;bottom:13px;background:#15201eda;color:#edf2ed;padding:5px 10px;border-radius:5px;font-size:12px}.note{margin-top:15px;font-size:13px;max-width:960px}.statusline{margin-top:10px;font-size:12px;color:var(--muted)}[hidden]{display:none!important}footer{display:flex;gap:24px;margin-top:18px;font-size:13px}.stage{height:min(58vh,760px)}.imagearea{min-height:0}#image{height:100%;min-height:0}aside{overflow:auto}@media(max-width:780px){.stage{height:auto}main{padding:22px 14px 30px}header{display:block}.download{display:inline-block;margin-top:14px}.stage{grid-template-columns:1fr}.imagearea{min-height:250px}#image{height:53vh;min-height:270px}aside{display:grid;grid-template-columns:1fr 1fr;padding:18px;gap:18px}.proof{grid-column:1/-1}.views{width:100%}.views button{flex:1 0 auto;padding:9px 11px}.controls select{width:100%}.worldbar{margin-top:18px}footer{gap:16px;flex-wrap:wrap}}
</style></head><body><main><header><div><div class="eyebrow">Radio Archipelago / Canonical 3D</div><h1>Радиоархипелаг · острова V4</h1><p>Десять островов. Пять точных проекций и один объёмный ракурс.</p></div><a id="zip" class="download" href="RadioArchipelago_Islands_V4_True3D.zip">Скачать полный пакет ZIP</a></header><nav id="worldbar" class="worldbar" aria-label="Острова"></nav><div class="controls"><select id="worldselect" aria-label="Выбрать остров"></select><nav id="views" class="views" aria-label="Выбрать проекцию"></nav></div><section class="stage"><div class="imagearea"><img id="image" alt=""><div id="missing" hidden>Этот ракурс ещё не готов.</div><span id="caption" class="caption"></span></div><aside><div><h2 id="title"></h2><div id="badge" class="badge"></div><p id="dimensions" class="small"></p></div><div><h3>Файлы острова</h3><div id="links" class="links small"></div></div><div class="proof"><h3>Одна геометрия во всех видах</h3><p id="proof" class="small"></p><p id="hash" class="hash"></p></div><div class="proof"><h3>Свободный маршрут</h3><p class="small">Дорога 6 м, обочины по 1 м. Ровная замкнутая петля; над дорогой открытое небо.</p></div></aside></section><p class="note">Во фронтальном и боковых видах части дороги могут быть закрыты реальными объектами. Геометрия сохраняется: маршрут удобно проверять сверху и в объёмном ракурсе. Пять проекций сняты строго по осям ортографической камерой.</p><div id="statusline" class="statusline"></div><footer><a href="QA/all-islands-overview.jpg">Обзор десяти островов</a><a href="QA/before-after_01_10.jpg">Сравнение V3 → V4</a><a href="README.md">Описание и использование</a><a href="package-audit.json">Проверка полного пакета JSON</a></footer></main><script>
const PACK=__PACK_DATA__;
const names={front:'Спереди',right:'Справа',left:'Слева',back:'Сзади',top:'Сверху',isometric:'Объёмный'};
let index=0,view='isometric';const el=id=>document.getElementById(id);
PACK.worlds.forEach((w,i)=>{let b=document.createElement('button');b.textContent=String(i+1).padStart(2,'0')+'  '+w.title;b.onclick=()=>choose(i);el('worldbar').append(b);let o=document.createElement('option');o.value=i;o.textContent=w.title;el('worldselect').append(o)});
Object.entries(names).forEach(([id,label])=>{let b=document.createElement('button');b.textContent=label;b.dataset.view=id;b.onclick=()=>{view=id;show()};el('views').append(b)});
el('worldselect').onchange=e=>choose(Number(e.target.value));
function choose(i){index=i;show();el('worldbar').children[i].scrollIntoView({behavior:'smooth',block:'nearest',inline:'nearest'});history.replaceState(null,'','#'+PACK.worlds[i].id+'/'+view)}
function addlink(label,file){let a=document.createElement('a');a.textContent=label;a.href='Output/'+PACK.worlds[index].id+'/'+file;if(file.endsWith('.blend')||file.endsWith('.glb')||file.endsWith('.png'))a.download='';el('links').append(a)}
function show(){let w=PACK.worlds[index],img=w.images[view];el('title').textContent=w.title;el('worldselect').value=index;Array.from(el('worldbar').children).forEach((b,i)=>b.classList.toggle('active',i===index));Array.from(el('views').children).forEach(b=>{b.classList.toggle('active',b.dataset.view===view);b.setAttribute('aria-pressed',String(b.dataset.view===view))});el('image').hidden=!img;el('missing').hidden=!!img;if(img){el('image').src='Output/'+w.id+'/'+view+'.png';el('image').alt=w.title+' — '+names[view];el('dimensions').textContent=img.width+' × '+img.height+' · PNG'}else el('dimensions').textContent='Ожидание готового PNG';el('caption').textContent=names[view]+(view==='isometric'?' · та же 3D-сцена':' · ортографическая проекция');el('badge').textContent=w.pass?'Геометрические проверки пройдены':'Пакет острова ещё не завершён';el('badge').classList.toggle('warn',!w.pass);el('links').replaceChildren();if(img)addlink('PNG · '+names[view],view+'.png');[['Blender · исходная сцена','island.blend'],['GLB · готовая 3D-геометрия','island.glb'],['Проверки · JSON','validation.json'],['Манифест · JSON','manifest.json']].forEach(([label,file])=>{if(w.files[file])addlink(label,file)});el('proof').textContent=w.pass?'Совпали хеши геометрии до и после рендеров; проверены положения и направления всех шести камер.':'Полное подтверждение появится после завершения рендеров и проверок.';el('hash').textContent=w.geometry_sha256?'SHA-256 '+w.geometry_sha256:'';history.replaceState(null,'','#'+w.id+'/'+view)}
el('zip').hidden=!PACK.archive_ready;el('statusline').textContent=PACK.pass?'Пакет проверен: 60 PNG, 10 BLEND, 10 GLB. Проверена геометрия Blender; игровой тест Unity не выполнялся.':'Подготовка: '+PACK.passed_world_count+'/10 островов и '+PACK.png_count+'/60 PNG прошли аудит. ZIP появится только после полной проверки.';
const match=location.hash.slice(1).split('/');const found=PACK.worlds.findIndex(w=>w.id===match[0]);if(found>=0)index=found;if(names[match[1]])view=match[1];show();
</script></body></html>'''


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base", type=Path, default=Path(__file__).resolve().parent)
    parser.add_argument("--audit-only", action="store_true")
    args = parser.parse_args(argv)
    base = args.base.resolve()
    source = base / "Source" / "worlds.json"
    if not source.is_file(): source = base.parent / "V2" / "worlds-final.json"
    worlds = read_json(source) if source.is_file() else [{"id": i, "title": title} for i, title in FALLBACK_WORLDS]
    expected = [i for i, _ in FALLBACK_WORLDS]
    if [w.get("id") for w in worlds] != expected:
        raise ValueError("World IDs/order must match the ten established V2 worlds")
    results = [audit_world(base, world) for world in worlds]
    errors = [] if (base / "README.md").is_file() else ["README.md is absent"]
    complete = all(world["pass"] for world in results) and not errors
    audit = {"schema": "radio_archipelago_v4_package_audit/1", "pass": complete,
             "expected_world_count": 10, "passed_world_count": sum(w["pass"] for w in results),
             "expected_png_count": 60, "png_count": sum(len(w["images"]) for w in results),
             "archive_ready": complete and not args.audit_only,
             "unity_gameplay_tested_by_packager": False, "errors": errors, "worlds": results}
    gallery = HTML.replace("__PACK_DATA__", json.dumps(audit, ensure_ascii=False).replace("<", "\\u003c"))
    (base / "index.html").write_text(gallery, encoding="utf-8")
    report_path = base / "package-audit.json"
    report_path.write_text(json.dumps(audit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    if complete and not args.audit_only:
        archive = base / ARCHIVE
        temporary = base / (ARCHIVE + ".tmp")
        files = [base / "README.md", base / "index.html", report_path,
                 base / "assemble_pack.py", base / "validate_scene.py"]
        files += [base / name for name in ("build_worlds.py", "terrain_road.py", "architecture.py", "scenery.py", "building-layouts.json", "plan_buildings.py", "refresh_exports.py", "export-materials.json", "world_finish.py", "nature_detail.py", "geology_detail.py", "river_detail.py", "forest_floor.py")]
        files += sorted((base / "Source").rglob("*.json"))
        files += sorted((base / "Textures").glob("*"))
        files += [base / "QA/glb-roundtrip.json", base / "TerrainQA/all-worlds.json", base / "TerrainQA/current-road-width-checks.json", base / "TerrainQA/validator-optimization-regressions.json", base / "QA/all-islands-overview.jpg", base / "QA/before-after_01_10.jpg"]
        files += [base / "TerrainQA" / (world["id"] + ".json") for world in worlds]
        for world in worlds:
            folder = base / "Output" / world["id"]
            files += [folder / (view + ".png") for view in VIEWS]
            files += [folder / filename for filename in ("island.blend", "island.glb", "validation.json", "manifest.json")]
        try:
            with zipfile.ZipFile(temporary, "w", compression=zipfile.ZIP_DEFLATED, compresslevel=6, allowZip64=True) as package:
                for path in files:
                    if path.is_file():
                        package.write(path, path.relative_to(base).as_posix())
            with zipfile.ZipFile(temporary) as package:
                corrupt = package.testzip()
                if corrupt:
                    raise ValueError("Archive CRC failed for " + corrupt)
                if sum(name.startswith("Output/") and name.endswith(".png") for name in package.namelist()) != 60:
                    raise ValueError("Archive must contain exactly 60 projection PNGs in Output/")
            os.replace(temporary, archive)
        except Exception:
            # Preserve an existing finished archive if the replacement fails.
            if temporary.exists():
                temporary.unlink()
            audit["archive_ready"] = False
            audit["errors"].append("ZIP creation failed; an existing archive may be stale")
            report_path.write_text(json.dumps(audit, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
            (base / "index.html").write_text(HTML.replace("__PACK_DATA__", json.dumps(audit, ensure_ascii=False).replace("<", "\\u003c")), encoding="utf-8")
            raise
    print(json.dumps({"pass": complete, "worlds_passed": audit["passed_world_count"],
                      "pngs_verified": audit["png_count"], "archive_created": audit["archive_ready"],
                      "audit": str(report_path), "gallery": str(base / "index.html")}, ensure_ascii=False))
    if not complete:
        for world in results:
            for error in world["errors"]:
                print(world["id"] + ": " + error, file=sys.stderr)
    return 0 if complete else 2


if __name__ == "__main__":
    raise SystemExit(main())
