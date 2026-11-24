"""Prepare the playable altitude derivative; the sculpted ground stays unchanged."""
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / "DancingCatsClone/Assets/RadioArchipelago/Imported/Islands/EmeraldRelief"


def smooth(t):
    t = max(0.0, min(1.0, t))
    return t * t * t * (10.0 + t * (-15.0 + 6.0 * t))


def sky_height(distance):
    if distance < 392.0 or distance > 503.0:
        return 0.0
    if distance < 437.0:
        return 12.0 * smooth((distance - 392.0) / 45.0)
    if distance <= 453.0:
        return 12.0
    return 12.0 * (1.0 - smooth((distance - 453.0) / 50.0))


def main():
    ground = json.loads((FOLDER / "ground_elevation.json").read_text(encoding="utf-8-sig"))
    playable = {
        "length": ground["length"], "closed": True,
        "keys": [{"distance": key["distance"], "height": key["height"] + sky_height(key["distance"])}
                 for key in ground["keys"]],
        "description": "Ground bridge and hill, then a smooth 12m canopy passage; original musical distance preserved."
    }
    (FOLDER / "play_elevation.json").write_text(json.dumps(playable, indent=2), encoding="utf-8")
    print(json.dumps({"keys": len(playable["keys"]), "maxHeight": max(k["height"] for k in playable["keys"]),
                      "skyStart": 392, "skyTop": [437, 453], "skyEnd": 503}))


if __name__ == "__main__":
    main()
