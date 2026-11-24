"""Independent, read-only project audit. Writes only its adjacent JSON receipt."""
import datetime
import hashlib
import json
from pathlib import Path
import re

PROJECT = Path(r"C:\Users\Derek\Games_develop\Cat\DancingCatsClone")
REVIEW = Path(__file__).resolve().parent
BEFORE = REVIEW.parent / "Before"
CATALOG = PROJECT / "Assets/Resources/CustomData/songs.json"


def digest(data):
    return hashlib.sha256(data).hexdigest().upper()


def read_json(path):
    return json.loads(path.read_bytes().decode("utf-8-sig"))


def hash_receipt(name, expected_count, excluded=None):
    source = BEFORE / name
    records = read_json(source)
    result = []
    skipped = []
    for record in records:
        path = Path(record["Path"])
        if excluded is not None and path.resolve() == excluded.resolve():
            skipped.append(record)
            continue
        actual = digest(path.read_bytes()) if path.is_file() else None
        result.append({"path": str(path), "expectedSha256": record["Hash"].upper(),
                       "actualSha256": actual, "unchanged": actual == record["Hash"].upper()})
    return {"baselineReceipt": str(source), "baselineReceiptSha256": digest(source.read_bytes()),
            "expectedFileCount": expected_count, "checkedFileCount": len(result),
            "passed": len(result) == expected_count and all(row["unchanged"] for row in result),
            "excluded": skipped, "files": result}


def raw_rows(path):
    # Preserve every character inside each object, including CR/LF and unknown fields.
    text = path.read_bytes().decode("utf-8-sig")
    decoder = json.JSONDecoder()
    position = 0
    while position < len(text) and text[position].isspace():
        position += 1
    if text[position] != "[":
        raise ValueError("Expected a JSON array: " + str(path))
    position += 1
    rows = []
    while True:
        while position < len(text) and text[position].isspace():
            position += 1
        if text[position] == "]":
            position += 1
            break
        start = position
        value, position = decoder.raw_decode(text, position)
        rows.append((value, text[start:position]))
        while position < len(text) and text[position].isspace():
            position += 1
        if text[position] == ",":
            position += 1
        elif text[position] != "]":
            raise ValueError("Invalid array delimiter")
    if text[position:].strip():
        raise ValueError("Unexpected data after catalog")
    return rows


first = hash_receipt("preserved-first-island-hashes.json", 55, CATALOG)
scenery = hash_receipt("preserved-coral-scenery-hashes.json", 36)
legacy = raw_rows(BEFORE / "songs.json")
current = raw_rows(CATALOG)
old_by_id = {value["id"]: (value, raw) for value, raw in legacy}
new_by_id = {value["id"]: (value, raw) for value, raw in current}
row_evidence = []
for value, raw in legacy:
    now = new_by_id.get(value["id"])
    row_evidence.append({"id": value["id"], "catalogId": value["catalogId"],
                         "baselineRawObjectSha256": digest(raw.encode("utf-8")),
                         "currentRawObjectSha256": digest(now[1].encode("utf-8")) if now else None,
                         "rawTextUnchanged": now is not None and now[1] == raw,
                         "allParsedFieldsUnchanged": now is not None and now[0] == value})
added = [value["id"] for value, _ in current if value["id"] not in old_by_id]
new_song = new_by_id.get("TikoTa", ({}, ""))[0]
metadata = new_song.get("catalog", {})
catalog_anchor = first["excluded"]
baseline_anchored = len(catalog_anchor) == 1 and digest((BEFORE / "songs.json").read_bytes()) == catalog_anchor[0]["Hash"].upper()
all_songs = read_json(PROJECT / "Assets/Resources/OriginalData/songs.json") + [value for value, _ in current]
identifiers_unique = len({value["id"].casefold() for value in all_songs}) == len(all_songs)
catalog_ids_unique = len({value["catalogId"] for value in all_songs}) == len(all_songs)
tiko_valid = (new_song.get("catalogId") == 20003 and metadata.get("id") == 20003
              and metadata.get("musicUnlockType") == 0 and metadata.get("gold") == 0
              and metadata.get("builtin") is True and metadata.get("disable") is False)
catalog = {"baselineFile": str(BEFORE / "songs.json"), "currentFile": str(CATALOG),
           "baselineMatchesOriginalReceiptHash": baseline_anchored,
           "baselineRowCount": len(legacy), "currentRowCount": len(current),
           "allLegacyRawObjectsUnchanged": all(row["rawTextUnchanged"] for row in row_evidence),
           "allLegacyUnknownFieldsPreserved": all(row["allParsedFieldsUnchanged"] for row in row_evidence),
           "legacyRelativeOrderUnchanged": [value["id"] for value, _ in current if value["id"] in old_by_id] == [value["id"] for value, _ in legacy],
           "addedIds": added, "uniqueSongIdsAcrossMergedCatalog": identifiers_unique,
           "uniqueCatalogIdsAcrossMergedCatalog": catalog_ids_unique,
           "tikoTaIsFreeWithCatalogId20003": tiko_valid, "legacyRows": row_evidence}
catalog["passed"] = (baseline_anchored and len(legacy) == len(old_by_id) == 23
                     and len(current) == len(new_by_id) == 24 and added == ["TikoTa"]
                     and catalog["allLegacyRawObjectsUnchanged"] and catalog["allLegacyUnknownFieldsPreserved"]
                     and catalog["legacyRelativeOrderUnchanged"] and identifiers_unique and catalog_ids_unique and tiko_valid)

allowed = {"SongId", "DistanceScale", "Chart", "ElevationJson"}
definition_path = PROJECT / "Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset"
before_bytes = (BEFORE / "CoralOasis.asset").read_bytes()
after_bytes = definition_path.read_bytes()
field_pattern = re.compile(rb"^  ([A-Za-z][A-Za-z0-9_]*): ([^\r\n]*)(\r?\n|$)", re.M)
old_fields = {match[1].decode(): match[2].decode() for match in field_pattern.finditer(before_bytes)}
new_fields = {match[1].decode(): match[2].decode() for match in field_pattern.finditer(after_bytes)}
changed = {key: {"before": old_fields.get(key), "after": new_fields.get(key)}
           for key in sorted(old_fields.keys() | new_fields.keys()) if old_fields.get(key) != new_fields.get(key)}
def mask_allowed(data):
    return field_pattern.sub(lambda match: b"  " + match[1] + b": <allowed>" + match[3]
                             if match[1].decode() in allowed else match[0], data)
definition = {"path": str(definition_path), "changedFields": changed,
              "allOtherBytesUnchanged": mask_allowed(before_bytes) == mask_allowed(after_bytes),
              "songId": new_fields.get("SongId"), "distanceScale": new_fields.get("DistanceScale")}
definition["passed"] = (set(changed) == allowed and definition["allOtherBytesUnchanged"]
                         and definition["songId"] == "TikoTa" and float(definition["distanceScale"]) == 1.2)

def guid(path):
    return re.search(r"^guid: ([0-9a-f]+)$", path.read_text(encoding="utf-8-sig"), re.M)[1]

first_definition = PROJECT / "Assets/RadioArchipelago/Content/Islands/EmeraldAbyss/EmeraldAbyss.asset"
first_scene = PROJECT / "Assets/RadioArchipelago/Scenes/EmeraldAbyssPilot.unity"
first_text = first_definition.read_text(encoding="utf-8-sig")
scene_text = first_scene.read_text(encoding="utf-8-sig")
stage_guid = guid(PROJECT / "Assets/RadioArchipelago/Runtime/World/IslandStagePresentation.cs.meta")
stage_blocks = [block for block in re.split(r"(?=^--- !u!)", scene_text, flags=re.M) if "guid: " + stage_guid in block]
stage_id = re.search(r"^--- !u!114 &(\d+)", stage_blocks[0], re.M)[1] if len(stage_blocks) == 1 else None
expected_definition_guid = guid(Path(str(first_definition) + ".meta"))
first_chart = first_definition.parent / "LumiLaiAbyss.timeline.json"
first_profile = first_definition.parent / "LumiLaiHoldGrades.json"
first_refs = {"scene": str(first_scene), "definition": str(first_definition),
              "songId": re.search(r"^  SongId: (.+)$", first_text, re.M)[1],
              "definitionGuid": expected_definition_guid,
              "stageFileId": stage_id,
              "sceneReferencesExpectedDefinition": len(stage_blocks) == 1 and "definition: {fileID: 11400000, guid: " + expected_definition_guid in stage_blocks[0],
              "controllerReferencesSceneStage": stage_id is not None and "islandStage: {fileID: " + stage_id + "}" in scene_text,
              "chartGuid": guid(Path(str(first_chart) + ".meta")),
              "profileGuid": guid(Path(str(first_profile) + ".meta")),
              "chartMapId": read_json(first_chart)["id"]}
first_refs["chartReferencesPreservedLumiAsset"] = "Chart: {fileID: 4900000, guid: " + first_refs["chartGuid"] in first_text
first_refs["profileReferencesPreservedLumiAsset"] = "ElevationJson: {fileID: 4900000, guid: " + first_refs["profileGuid"] in first_text
first_refs["passed"] = (first_refs["songId"] == first_refs["chartMapId"] == "LumiLai"
                        and all(first_refs[key] for key in ("sceneReferencesExpectedDefinition", "controllerReferencesSceneStage",
                                                           "chartReferencesPreservedLumiAsset", "profileReferencesPreservedLumiAsset")))
report = {"schemaVersion": 1, "checkedUtc": datetime.datetime.now(datetime.timezone.utc).isoformat(),
          "scope": "Independent on-disk byte/SHA-256 audit, exact legacy JSON object text including unknown fields, Coral definition byte-mask comparison, and preserved first-scene GUID binding verification. No Unity commands, queue operations, builds or Assets mutations. Native playback is outside this receipt.",
          "firstIslandProtectedFiles": first, "customSongCatalog": catalog, "coralSceneryProtectedFiles": scenery,
          "coralDefinitionChanges": definition, "firstIslandBindings": first_refs}
report["passed"] = all(section["passed"] for section in (first, catalog, scenery, definition, first_refs))
output = REVIEW / "first-island-preservation.json"
output.write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(json.dumps({"passed": report["passed"], "firstIslandFiles": first["checkedFileCount"],
                  "legacyRowsRawTextUnchanged": sum(row["rawTextUnchanged"] for row in row_evidence),
                  "catalogRows": len(current), "addedIds": added, "sceneryFiles": scenery["checkedFileCount"],
                  "definitionChangedFields": list(changed), "firstIslandBindingsPassed": first_refs["passed"],
                  "output": str(output)}, indent=2))
