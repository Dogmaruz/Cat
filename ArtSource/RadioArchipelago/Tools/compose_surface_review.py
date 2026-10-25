"""Compose before/after proof from actual Unity captures, without altering them."""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def review_font(size: int):
    for path in ['C:/Windows/Fonts/segoeui.ttf', 'DejaVuSans.ttf']:
        try:
            return ImageFont.truetype(path, size)
        except OSError:
            pass
    return ImageFont.load_default()


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', required=True, type=Path)
    parser.add_argument('--output', required=True, type=Path)
    args = parser.parse_args()
    args.output.mkdir(parents=True, exist_ok=True)
    title_font, label_font = review_font(28), review_font(22)
    for character, courier in [('Tiko', 'signal'), ('Nori', 'orbit'), ('Lumen', 'sail')]:
        for surface in ['beauty', 'clay']:
            views = ['front', 'right', 'left', 'under-right']
            sheet = Image.new('RGB', (1200, 2580), '#f5f2eb')
            draw = ImageDraw.Draw(sheet)
            draw.text((20, 8), f'{character} / {surface} / shoulder angle 60 degrees', font=title_font, fill='#17283e')
            draw.text((20, 48), 'BEFORE: 87bc426', font=label_font, fill='#17283e')
            draw.text((620, 48), 'AFTER: current Unity import', font=label_font, fill='#17283e')
            for row, view in enumerate(views):
                for column, stage in enumerate(['baseline', 'candidate']):
                    filename = f'{courier}-60-{view}-{surface}.png'
                    with Image.open(args.input / stage / filename) as source:
                        image = source.convert('RGB').resize((600, 600), Image.Resampling.LANCZOS)
                        sheet.paste(image, (column * 600, 120 + row * 620))
                draw.text((20, 93 + row * 620), view, font=label_font, fill='#17283e')
            sheet.save(args.output / f'{character}-{surface}-before-after.png')


if __name__ == '__main__':
    main()
