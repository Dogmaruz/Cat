"""Encode actual Unity review frames as GIFs without generating or altering poses.

Requires Pillow. Run after Radio Archipelago/Imported couriers/Render animation reel.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from PIL import Image


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    for sequence in ("flight", "ride"):
        files = sorted((args.source / sequence).glob("*.png"))
        if len(files) != 48:
            raise ValueError(f"Expected 48 actual Unity frames for {sequence}, got {len(files)}")
        frames = []
        size = None
        for path in files:
            with Image.open(path) as source:
                if size is None:
                    size = source.size
                if source.size != size:
                    raise ValueError(f"Inconsistent frame dimensions: {path}")
                frames.append(source.convert("RGB").quantize(colors=256))
        target = args.output / f"couriers-{sequence}.gif"
        # GIF frame duration is quantized to 10 ms: 3 seconds / 48 frames.
        durations = [60 if i % 4 else 70 for i in range(48)]
        frames[0].save(target, save_all=True, append_images=frames[1:],
                       duration=durations, loop=0, optimize=False, disposal=2)
        with Image.open(target) as result:
            if result.n_frames != 48 or result.size != size:
                raise ValueError(f"Encoded review lost frames: {target}")
        print(json.dumps({"file": str(target.resolve()), "frames": 48,
                          "dimensions": size, "milliseconds": sum(durations),
                          "bytes": target.stat().st_size}))


if __name__ == "__main__":
    main()
