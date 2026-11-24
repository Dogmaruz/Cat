"""Verify selected PNGs against the three workers' recorded native provenance schemas."""
import hashlib
import json
from collections import Counter
from html.parser import HTMLParser
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
brief = json.loads((ROOT / "brief.json").read_text(encoding="utf-8-sig"))
groups = {"Characters": brief["characters"], "Backpacks": brief["backpacks"], "Hats": brief["hats"], "Scarves": brief["scarves"]}
checked = 0
for category, items in groups.items():
    for item in items:
        folder = ROOT / category / item["id"]
        meta = json.loads((folder / "generation.json").read_text(encoding="utf-8-sig"))
        records = meta.get("views", meta.get("assets", meta))
        for view in ("front", "back", "right", "left"):
            entry = records[view]
            # Body/hat records select a direct source. Backpack records retain
            # an explicit attempts list and mark the chosen native attempt.
            candidates = [entry] if "nativeSource" in entry else entry["attempts"]
            candidates = [x for x in candidates if x.get("status") != "rejected"]
            actual = hashlib.sha256((folder / (view + ".png")).read_bytes()).digest()
            matches = []
            for candidate in candidates:
                source = Path(candidate["nativeSource"])
                assert source.is_file(), source
                if hashlib.sha256(source.read_bytes()).digest() == actual:
                    matches.append(str(source))
            assert matches, (folder, view, "no matching selected native source")
            checked += 1

class Gallery(HTMLParser):
    def __init__(self):
        super().__init__()
        self.counts, self.missing, self.images = Counter(), [], 0
    def handle_starttag(self, tag, attrs):
        attrs = dict(attrs)
        if tag == "article":
            self.counts[attrs["data-category"]] += 1
        if tag == "img":
            self.images += 1
            if not (ROOT / attrs["src"]).is_file():
                self.missing.append(attrs["src"])

page = Gallery()
page.feed((ROOT / "index.html").read_text(encoding="utf-8"))
assert page.counts == {"Characters": 10, "Backpacks": 20, "Hats": 8, "Scarves": 2}, page.counts
assert page.images == 200 and not page.missing, page.missing
report = {"passed": True, "nativeByteMatches": checked, "galleryCards": dict(page.counts), "galleryImageLinks": page.images, "missingLinks": page.missing}
(ROOT / "_Generation/final-checks.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
print(f"PASS: {checked} native PNG byte matches; 40 gallery cards / 200 existing image links")
