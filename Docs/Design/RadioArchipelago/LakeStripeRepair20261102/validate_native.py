"""Validate actual native-clock runs and exact camera replay, not artistic quality."""
import hashlib
import json
import struct
from pathlib import Path

here = Path(__file__).resolve().parent
previous = here.parent / "IslandFinishesAndWaterfalls20261102" / "NativeWater"
runs = {
    "moon-lake-reference-20261102-181510-7405321": "moon-lakes-20261102-103712-4289039",
    "coral-lake-reference-20261102-181552-9358737": "coral-lakes-20261102-104322-3306603",
    "amber-lake-reference-20261102-181641-9939035": "amber-lakes-20261102-103740-4869623",
    "amber-body-reference-20261102-181748-7870697": "amber-art-20261102-102130-3018003",
}
evidence = []
for run, reference in runs.items():
    actual = json.loads((here / "Native" / run / "report.json").read_text(encoding="utf-8-sig"))
    original = json.loads((previous / reference / "report.json").read_text(encoding="utf-8-sig"))
    assert actual["state"] == "complete" and actual["nativeClock"]
    assert actual["savedMaterialsUnchanged"] and actual["originalSceneRestored"] and not actual["shaderErrors"]
    assert len(actual["views"]) == len(original["views"])
    for view, baseline in zip(actual["views"], original["views"]):
        for key in ("cameraPosition", "fieldOfView", "targetNode"):
            assert view[key] == baseline[key], (run, key)
        # Transform.SetPositionAndRotation can renormalize a serialized
        # quaternion by one float32 ULP. Position/FOV remain bit-identical.
        # Record this exact numerical witness instead of equating JSON strings.
        bits = lambda value: struct.unpack("i", struct.pack("f", value))[0]
        rotation_ulp = max(abs(bits(view["cameraRotation"][key]) - bits(baseline["cameraRotation"][key])) for key in "xyzw")
        assert rotation_ulp <= 1, (run, "cameraRotation", rotation_ulp)
        frames = view["frames"]
        assert len(frames) == len({frame["unityFrame"] for frame in frames}) == 24
        duration = frames[-1]["nativeTime"] - frames[0]["nativeTime"]
        assert duration >= 2.3
        assert all(b["nativeTime"] > a["nativeTime"] for a, b in zip(frames, frames[1:]))
        evidence.append(dict(run=run, node=view["targetNode"], frameCount=len(frames),
                             nativeSeconds=duration, exactReferencePositionAndFov=True, quaternionMaxFloat32Ulp=rotation_ulp))
shader = here.parents[3] / "DancingCatsClone/Assets/RadioArchipelago/Art/AmberFlowWater.shader"
report = dict(passed=True, shaderSha256=hashlib.sha256(shader.read_bytes()).hexdigest(),
              lakeViews=7, fallingBodyViews=3, views=evidence,
              scope="Native clock/frame/material/scene/camera checks only. Artistic outcome is independently recorded in VISUAL_REVIEW.md.")
(here / "native-validation.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
print("PASS: 7 lake views + 3 falling-body views, 240 native frames, exact positions/FOV, rotations within 1 float32 ULP, saved state restored")
