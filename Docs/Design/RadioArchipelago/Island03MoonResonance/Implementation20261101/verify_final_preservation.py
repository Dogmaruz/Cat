"""Read-only final preservation audit; run with Python 3 from any directory.

The only earlier-island exception is two appended serialized fields on the
Coral definition. All other protected files and all 24 original catalog object
strings must match the captured Before baseline exactly. Prints JSON and exits
nonzero on a violation; never writes project assets or receipts.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path


AUDIT_FOLDER = Path(__file__).resolve().parent
REPOSITORY = AUDIT_FOLDER.parents[4]
PROJECT = REPOSITORY / "DancingCatsClone"
BEFORE = AUDIT_FOLDER / "Before"
CORAL = "Assets/RadioArchipelago/Content/Islands/CoralOasis/CoralOasis.asset"
CATALOG = "Assets/Resources/CustomData/songs.json"
CORAL_ADDITION = b"  SparseCloudSteps: 1\n  CloudStepRanges: []\n"


def sha256(data: bytes) -> str:
    return hashlib.sha256(data).hexdigest().upper()


def project_relative(saved_path: str) -> str:
    normalized = saved_path.replace("\\", "/")
    marker = "/DancingCatsClone/"
    if marker not in normalized:
        raise ValueError(f"Receipt contains an unexpected external path: {saved_path}")
    return normalized.split(marker, 1)[1]


def raw_catalog_objects(data: bytes) -> list[tuple[dict, str]]:
    text = data.decode("utf-8-sig")
    parsed = json.loads(text)
    if not isinstance(parsed, list) or not all(isinstance(row, dict) for row in parsed):
        raise ValueError("The catalog must be a JSON array of objects")
    decoder = json.JSONDecoder()
    position = text.index("[") + 1
    objects = []
    for _ in parsed:
        while text[position].isspace() or text[position] == ",":
            position += 1
        value, end = decoder.raw_decode(text, position)
        objects.append((value, text[position:end]))
        position = end
    return objects


def verify() -> dict:
    errors = []
    receipt = json.loads((BEFORE / "preserved-islands-hashes.json").read_text(encoding="utf-8-sig"))
    paths = [project_relative(row["Path"]) for row in receipt]
    if len(paths) != 108 or len(set(paths)) != 108:
        errors.append("The frozen receipt must contain exactly 108 unique protected files")
    if paths.count(CORAL) != 1 or paths.count(CATALOG) != 1:
        errors.append("The frozen receipt must contain exactly one Coral definition and one catalog")

    strict_checked = 0
    coral_exception_passed = False
    coral_baseline = (BEFORE / "CoralOasis.asset").read_bytes()
    for row, relative in zip(receipt, paths):
        target = PROJECT / relative
        if relative == CATALOG:
            continue
        if not target.is_file():
            errors.append(f"Missing protected file: {relative}")
            continue
        actual = target.read_bytes()
        if relative == CORAL:
            if sha256(coral_baseline) != row["Hash"].upper():
                errors.append("Before/CoralOasis.asset does not match its frozen receipt hash")
            coral_exception_passed = actual == coral_baseline + CORAL_ADDITION
            if not coral_exception_passed:
                errors.append("Coral definition differs beyond the two exact authorized appended fields")
        else:
            strict_checked += 1
            if sha256(actual) != row["Hash"].upper():
                errors.append(f"Protected file hash changed: {relative}")

    old_catalog = (BEFORE / "songs.json").read_bytes()
    catalog_record = next((row for row, path in zip(receipt, paths) if path == CATALOG), None)
    if catalog_record is None or sha256(old_catalog) != catalog_record["Hash"].upper():
        errors.append("Before/songs.json does not match its frozen receipt hash")
    before_rows = raw_catalog_objects(old_catalog)
    after_rows = raw_catalog_objects((PROJECT / CATALOG).read_bytes())
    old_ids = [value.get("id") for value, _ in before_rows]
    after_ids = [value.get("id") for value, _ in after_rows]
    raw_old_rows_unchanged = [raw for _, raw in before_rows] == [raw for _, raw in after_rows[:-1]]
    only_nori_appended = (
        len(before_rows) == 24
        and len(after_rows) == 25
        and len(set(old_ids)) == 24
        and "NoriNo" not in old_ids
        and after_ids == old_ids + ["NoriNo"]
        and after_rows[-1][0].get("catalogId") == 20004
    )
    if not raw_old_rows_unchanged:
        errors.append("An original catalog object string changed or moved")
    if not only_nori_appended:
        errors.append("The catalog must retain its 24 original rows and append only NoriNo, catalogId 20004")
    return {
        "passed": not errors,
        "strictProtectedFilesChecked": strict_checked,
        "coralOnlyAuthorizedFieldsAppended": coral_exception_passed,
        "previousCatalogRecords": len(before_rows),
        "currentCatalogRecords": len(after_rows),
        "rawOldCatalogRecordsUnchanged": raw_old_rows_unchanged,
        "onlyNoriNoAppended": only_nori_appended,
        "errors": errors,
    }


if __name__ == "__main__":
    try:
        result = verify()
    except (OSError, ValueError, KeyError, TypeError, IndexError) as error:
        result = {"passed": False, "errors": [f"Audit could not complete: {error}"]}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    raise SystemExit(0 if result["passed"] else 1)
