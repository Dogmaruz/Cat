"""Create a disposable, single-song Unity project; optionally build its Android APK.

The source project is never edited. Destination must not exist, and all pruning is
restricted to the newly created copy. Run with --help for the exact interface.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

SONG = "AlwaysRememberMeowCat"
SCENE = "Assets/RadioArchipelago/Scenes/EmeraldIslandPilot.unity"
TREES = ("Assets", "Packages", "ProjectSettings")
MARKER = "island-android-staging.json"


def digest(path: Path) -> str:
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def snapshot(source: Path) -> dict[str, str]:
    result = {}
    for tree in TREES:
        for path in sorted((source / tree).rglob("*")):
            if path.is_symlink():
                raise RuntimeError(f"Source symlinks require manual review: {path}")
            if path.is_file():
                result[path.relative_to(source).as_posix()] = digest(path)
    return result


def inside(path: Path, root: Path) -> Path:
    resolved = path.resolve()
    if resolved == root or root not in resolved.parents:
        raise RuntimeError(f"Refusing operation outside staging project: {resolved}")
    return resolved


def stage(source: Path, destination: Path, output: Path) -> dict:
    source, destination, output = source.resolve(), destination.resolve(), output.resolve()
    if destination.exists():
        raise FileExistsError(f"Destination already exists; choose a fresh directory: {destination}")
    if source == destination or source in destination.parents or destination in source.parents:
        raise ValueError("Source and staging project must be independent directories")
    if output.suffix.lower() != ".apk" or source in output.parents:
        raise ValueError("Output must be an .apk outside the source project")
    for tree in TREES:
        if not (source / tree).is_dir():
            raise FileNotFoundError(source / tree)
    if not (source / SCENE).is_file():
        raise FileNotFoundError(source / SCENE)
    records = json.loads((source / "Assets/Resources/CustomData/songs.json").read_text(encoding="utf-8-sig"))
    approved = [record for record in records if record.get("id") == SONG]
    if len(approved) != 1 or approved[0].get("audioResource") != f"CustomAudio/Music/{SONG}":
        raise ValueError("The approved song is absent, duplicated, or has an unexpected audio resource")
    original_records = json.loads((source / "Assets/Resources/OriginalData/songs.json").read_text(encoding="utf-8-sig"))
    excluded_names = set()
    for record in original_records + records:
        if record.get("id") == SONG:
            continue
        excluded_names.add(record["id"])
        for key in ("audioResource", "previewAudioResource"):
            if record.get(key):
                excluded_names.add(record[key].rsplit("/", 1)[-1].split("__", 1)[0])
    before = snapshot(source)
    destination.mkdir(parents=True, exist_ok=False)
    for tree in TREES:
        shutil.copytree(source / tree, destination / tree, copy_function=shutil.copy2)
    removed = []

    def remove(relative: str):
        path = inside(destination / relative, destination)
        if not path.exists():
            return
        if path.is_dir():
            removed.extend(p.relative_to(destination).as_posix() for p in path.rglob("*") if p.is_file())
            shutil.rmtree(path)
        else:
            removed.append(relative)
            path.unlink()

    for relative in ("Assets/Resources/OriginalAudio/Music", "Assets/Resources/OriginalAudio/Previews",
                     "Assets/Resources/OriginalData/Maps"):
        remove(relative)
        remove(relative + ".meta")
    # A few recovered prefab clips duplicate full songs outside Resources.
    recovered_audio = destination / "Assets/Recovered/AudioClip"
    if recovered_audio.is_dir():
        for path in tuple(recovered_audio.iterdir()):
            if path.suffix.lower() != ".meta" and path.stem in excluded_names:
                relative = path.relative_to(destination).as_posix()
                remove(relative)
                remove(relative + ".meta")
    maps = destination / "Assets/Resources/CustomData/Maps"
    keep = {SONG + ".timeline.json", SONG + ".timeline.json.meta"}
    for path in tuple(maps.iterdir()):
        if path.name not in keep:
            remove(path.relative_to(destination).as_posix())
    # Original catalog must be nonempty; the optional catalog must not duplicate it.
    original = destination / "Assets/Resources/OriginalData/songs.json"
    custom = destination / "Assets/Resources/CustomData/songs.json"
    original.write_text(json.dumps(approved, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    custom.write_text("[]\n", encoding="utf-8")
    if before != snapshot(source):
        raise RuntimeError("Source files changed while staging; the copy is not a consistent snapshot. Create a new stage after source freezes.")
    for relative, expected in before.items():
        path = destination / relative
        if relative in removed or relative in ("Assets/Resources/OriginalData/songs.json", "Assets/Resources/CustomData/songs.json"):
            continue
        if not path.is_file() or digest(path) != expected:
            raise RuntimeError(f"Copied asset differs from source: {relative}")
    version_text = (destination / "ProjectSettings/ProjectVersion.txt").read_text(encoding="utf-8")
    version = next(line.split(":", 1)[1].strip() for line in version_text.splitlines() if line.startswith("m_EditorVersion:"))
    manifest = {"schema": "radio_archipelago_android_pilot/1", "sourceProject": str(source),
                "stagingProject": str(destination), "createdUnixTime": int(time.time()),
                "songId": SONG, "scene": SCENE, "outputApk": str(output), "unityVersion": version,
                "sourceUnchanged": True, "copiedAssetsVerified": True, "removedFromCopy": sorted(removed),
                "excludedMusicNames": sorted(excluded_names),
                "sourceHashes": before, "rewrittenCatalogs": [str(original.relative_to(destination)), str(custom.relative_to(destination))]}
    (destination / MARKER).write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({key: value for key, value in manifest.items() if key != "sourceHashes" and key != "removedFromCopy"}, ensure_ascii=False))
    print(f"STAGING_READY: {len(before)} source hashes verified; {len(removed)} copied files excluded", flush=True)
    return manifest


def main():
    # Windows redirected consoles may default to cp1251; catalog names and paths
    # can contain characters outside that codepage even after staging succeeds.
    sys.stdout.reconfigure(encoding="utf-8", errors="backslashreplace")
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", required=True, type=Path)
    parser.add_argument("--destination", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--build", action="store_true", help="After staging, launch Unity CLI Android build")
    args = parser.parse_args()
    manifest = stage(args.source, args.destination, args.output)
    if args.build:
        cli = shutil.which("unity")
        if not cli:
            raise FileNotFoundError("Unity CLI is not on PATH")
        env = os.environ.copy()
        env["RADIO_ISLAND_APK_PATH"] = manifest["outputApk"]
        cmd = [cli, "build", manifest["stagingProject"], "--editor-version", manifest["unityVersion"],
               "--target", "Android", "--execute-method", "DancingCats.Clone.Editor.EditorAndroidPilotBuild.Build",
               "--log-file", str(Path(manifest["stagingProject"]) / "android-build.log"),
               "--timeout", "7200", "--non-interactive", "--format", "json"]
        print("BUILD_COMMAND: " + subprocess.list2cmdline(cmd), flush=True)
        subprocess.run(cmd, env=env, check=True)


if __name__ == "__main__":
    main()
