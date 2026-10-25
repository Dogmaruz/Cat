"""Compose labelled proof sheets from unchanged actual Unity joint captures."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from PIL import Image, ImageDraw
from compose_surface_review import review_font


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    before = args.input / 'baseline-straps'
    after = args.input / 'candidate'
    known = {folder: set(json.loads((folder / 'manifest.json').read_text())['frames'])
             for folder in (before, after)}
    title_font, label_font = review_font(27), review_font(22)

    def frame(folder, filename):
        if filename not in known[folder]:
            raise ValueError(f'Unrecorded Unity frame: {folder / filename}')
        with Image.open(folder / filename) as source:
            return source.convert('RGB').resize((550, 550), Image.Resampling.LANCZOS)

    def sheet(title, rows, filename):
        result = Image.new('RGB', (1100, 100 + 585 * len(rows)), '#f5f2eb')
        draw = ImageDraw.Draw(result)
        draw.text((15, 8), title, font=title_font, fill='#17283e')
        draw.text((15, 47), 'BEFORE: 50caaf5', font=label_font, fill='#17283e')
        draw.text((565, 47), 'AFTER: actual Unity import', font=label_font, fill='#17283e')
        for i, (label, file) in enumerate(rows):
            top = 85 + i * 585
            draw.text((15, top), label, font=label_font, fill='#17283e')
            result.paste(frame(before, file), (0, top + 30))
            result.paste(frame(after, file), (550, top + 30))
        result.save(args.output / filename)

    sheet('Lumen: shoulders inside the straps',
          [(side, f'sail-arms--10-{side}-beauty.png') for side in ('left', 'right')],
          'Lumen-shoulder-placement.png')
    sheet('Tiko: repaired inner case contact',
          [(side, f'signal-case-60-{side}-beauty.png') for side in ('left', 'right')],
          'Tiko-case-contact.png')
    for material in ('beauty', 'clay'):
        sheet('Tiko: complete coupler on its bearing / ' + material,
              [(f'yaw {yaw}, lean 10 degrees', f'signal-hinge-{yaw}-lean-10-side-{material}.png')
               for yaw in (90, 180, 270)], f'Tiko-antenna-{material}.png')

    selected = Image.new('RGB', (1100, 600), '#f5f2eb')
    draw = ImageDraw.Draw(selected)
    draw.text((15, 9), 'Lumen: final left / right views in Unity', font=title_font, fill='#17283e')
    for i, side in enumerate(('left', 'right')):
        selected.paste(frame(after, f'sail-arms--10-{side}-beauty.png'), (i * 550, 45))
    selected.save(args.output / 'Lumen-current-sides.png')
    print('Saved five sheets from recorded Unity frames; source captures were not modified.')


if __name__ == '__main__':
    main()
