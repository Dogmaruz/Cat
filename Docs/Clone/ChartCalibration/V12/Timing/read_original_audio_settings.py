"""Read shipped serialized AudioManager, without editing/extracting the original Unity bundle."""
import hashlib
import json
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
RESEARCH = Path(r"C:\Users\Derek\Documents\Codex\2026-10-20\dancing-cat\work\clone-research")
sys.path.insert(0, str(RESEARCH.parent / "reference/python-libs"))
import UnityPy

UnityPy.config.FALLBACK_UNITY_VERSION = "2022.3.62f2"
source = RESEARCH / "input/reference-full/assets/bin/Data/data.unity3d"
environment = UnityPy.load(str(source))
records = []
for obj in environment.objects:
    if obj.type.name == "AudioManager":
        records.append(dict(serialized_file=obj.assets_file.name, path_id=obj.path_id, type_id=obj.type.value,
                            serialized_fields=obj.read_typetree()))
result = dict(source=str(source), sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
              unitypy_version=UnityPy.__version__, audio_managers=records)
assert len(records) == 1, "Expected exactly one shipped AudioManager"
(HERE / "original-shipped-audio-manager.json").write_text(json.dumps(result, indent=2), encoding="utf-8")
print(json.dumps(result, indent=2))
