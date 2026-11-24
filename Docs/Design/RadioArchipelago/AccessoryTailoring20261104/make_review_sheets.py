"""Arrange native Unity captures for visual inspection; does not synthesize game imagery."""
from pathlib import Path
import argparse
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parent
FONT = Path('C:/Windows/Fonts/segoeui.ttf')


def sheet(folder, groups, name, columns=4, detail=True, hat=False):
    tile_w, tile_h = (360, 400) if detail else (320, 450)
    rows = (len(groups) + columns - 1) // columns
    canvas = Image.new('RGB', (columns * tile_w, rows * tile_h + 55), '#efede4')
    d = ImageDraw.Draw(canvas)
    font = ImageFont.truetype(str(FONT), 16)
    title = ImageFont.truetype(str(FONT), 24)
    d.text((18, 12), name.replace('-', ' '), font=title, fill='#203140')
    for i, (file, label) in enumerate(groups):
        x, y = (i % columns) * tile_w, (i // columns) * tile_h + 55
        image = Image.open(folder / file).convert('RGB')
        if hat:
            image = image.crop((65, 210, 575, 650))
        elif detail:
            # Identical crop in every panel, centered on torso/straps/case.
            image = image.crop((65, 300, 575, 760))
        image.thumbnail((tile_w - 12, tile_h - 48), Image.Resampling.LANCZOS)
        canvas.paste(image, (x + (tile_w - image.width) // 2, y))
        d.text((x + 12, y + tile_h - 42), label[:40], font=font, fill='#203140')
    output = folder / (name + '.png')
    canvas.save(output)
    return str(output)


def main():
    p = argparse.ArgumentParser()
    p.add_argument('--phase', choices=['Before', 'After', 'OriginalFit'], default='After')
    args = p.parse_args()
    folder = ROOT / args.phase
    report = json.loads((folder / 'report.json').read_text(encoding='utf-8'))
    pairs = report['pairs']
    outputs = []
    for hero in sorted({r['hero'] for r in pairs}):
        packs = [r for r in pairs if r['hero'] == hero and r['kind'] == 'Backpack']
        if len(packs) > 3:
            for side in ('side', 'back'):
                outputs.append(sheet(folder, [
                    (next(i for i in r['images'] if i.endswith('__' + side + '.png')), r['item'].removeprefix('backpack_'))
                    for r in packs], hero + '-20-packs-' + side, 5))
        hats = [r for r in pairs if r['hero'] == hero and r['kind'] == 'Hat']
        if hats:
            outputs.append(sheet(folder, [
                (next(i for i in r['images'] if i.endswith('__' + side + '.png')), r['item'].removeprefix('hat_') + ' ' + side)
                for r in hats for side in ('front', 'side')], hero + '-hats-front-side', 4, False))
            for side in ('front', 'side'):
                outputs.append(sheet(folder, [
                    (next(i for i in r['images'] if i.endswith('__' + side + '.png')), r['item'].removeprefix('hat_'))
                    for r in hats], hero + '-hats-' + side, 4, True, True))
    classics = [r for r in pairs if r['kind'] == 'Backpack' and r['hero'] in ('Tiko', 'Nori', 'Lumen') and r['item'] == 'backpack_' + r['hero']]
    if classics:
        outputs.append(sheet(folder, [
            (next(i for i in r['images'] if i.endswith('__' + side + '.png')), r['hero'] + ' ' + side)
            for side in ('back', 'side') for r in sorted(classics, key=lambda r: ['Tiko', 'Nori', 'Lumen'].index(r['hero']))],
            'classic-trio-back-side', 3))
    if args.phase == 'OriginalFit':
        owners = [r for r in pairs if r['kind'] == 'Backpack']
        outputs.append(sheet(folder, [
            (next(i for i in r['images'] if i.endswith('__' + side + '.png')), r['hero'] + ' / ' + r['item'].removeprefix('backpack_') + ' ' + side)
            for side in ('side', 'back') for r in owners], 'original-straps-owners-side-back', len(owners)))
        outputs.append(sheet(folder, [
            (next(i for i in r['images'] if i.endswith('__front.png')), r['hero'] + ' / ' + r['item'].removeprefix('backpack_'))
            for r in owners], 'original-straps-owners-front', len(owners)))
    (folder / 'sheets.json').write_text(json.dumps({'nativeSource': True, 'fixedCrop': True, 'images': outputs}, indent=2), encoding='utf-8')
    deformations = [dict(hero=r['hero'], item=r['item'], **m)
                    for r in pairs if r['kind'] == 'Backpack'
                    for m in r.get('strapDeformation', [])]
    if deformations:
        deformation_summary = {
            'straps': len(deformations),
            'newlyCollapsedTriangles': sum(m['newlyCollapsedTriangles'] for m in deformations),
            'sourceDegenerateTriangles': sum(m['sourceDegenerateTriangles'] for m in deformations),
            'largestEdgeP95': sorted(deformations, key=lambda m: m['edgeLengthRatio']['p95'], reverse=True)[:10],
            'largestAreaP95': sorted(deformations, key=lambda m: m['triangleAreaRatio']['p95'], reverse=True)[:10],
            'mostCollapsedFaces': sorted(deformations, key=lambda m: m['newlyCollapsedTriangles'], reverse=True)[:10],
            'note': 'Relative deformation metrics guide inspection; exact source UV/topology preservation alone does not guarantee a good fit.'
        }
        (folder / 'deformation-summary.json').write_text(json.dumps(deformation_summary, indent=2), encoding='utf-8')
    print(json.dumps({'sheets': len(outputs), 'folder': str(folder), 'deformationStraps': len(deformations)}))


if __name__ == '__main__':
    main()
