"""Make evidence-only montages from matched Unity capture sheets; no pose interpolation."""
import hashlib
import json
import math
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT.parent / 'OriginalChartCalibration' / 'plot-deps'))
from PIL import Image, ImageDraw, ImageFont

BASE = ROOT / 'Baseline'
CAND = ROOT / 'Candidate'
COLS, LABEL = 4, 22
FONT_PATH = Path('C:/Windows/Fonts/segoeui.ttf')
FONT = ImageFont.truetype(str(FONT_PATH), 16) if FONT_PATH.exists() else ImageFont.load_default()
SMALL = ImageFont.truetype(str(FONT_PATH), 13) if FONT_PATH.exists() else ImageFont.load_default()
BG, FG = (24, 29, 38), (235, 240, 247)


def read_capture(folder):
    data = json.loads((folder / 'metrics.json').read_text(encoding='utf-8'))
    assert len(data['frames']) == 117, (folder, len(data['frames']))
    assert all(v['fits'] for f in data['frames'] for v in f['views']), 'Clipped frame in ' + str(folder)
    assert len(data['sheets']) == 18
    assert all((folder / s).exists() for s in data['sheets'])
    return data


def select(data, courier, sequence):
    return [f for f in data['frames'] if f['courier'] == courier and f['sequence'] == sequence]


def crop_cell(folder, courier, sequence, view, index, spec, count, box=None):
    path = folder / f'{courier}-{sequence}-{view}.png'
    with Image.open(path) as sheet:
        width, height = spec['width'], spec['height']
        rows = math.ceil(count / COLS)
        assert sheet.size == (COLS * width, rows * (height + LABEL)), (path, sheet.size)
        # PNG scan lines start at the top; Unity wrote row-major cells with the label below each image.
        x = (index % COLS) * width
        y = (index // COLS) * (height + LABEL)
        image = sheet.crop((x, y, x + width, y + height)).convert('RGB')
        return image.crop(box) if box else image


def durations(frames):
    # GIF timing is quantized to 10 ms. Keep every captured pose and the real sample intervals.
    # Last pose holds 400 ms solely to make the loop boundary readable.
    return [max(10, round((b['time'] - a['time']) * 100) * 10) for a, b in zip(frames, frames[1:])] + [400]


def phase(frame):
    if frame['label'].startswith('landing'):
        return frame['label']
    return f"Hop {frame['hop']}  p={frame['progress']:.2f}"


def make_pair_gif(courier, sequence, spec, base, candidate):
    view = spec['id']
    frames = []
    for i, (bf, cf) in enumerate(zip(base, candidate)):
        left = crop_cell(BASE, courier, sequence, view, i, spec, len(base))
        right = crop_cell(CAND, courier, sequence, view, i, spec, len(base))
        image = Image.new('RGB', (left.width * 2 + 4, left.height + 76), BG)
        draw = ImageDraw.Draw(image)
        draw.text((12, 7), f'{courier.upper()}   BASELINE', font=FONT, fill=FG)
        draw.text((left.width + 16, 7), 'CANDIDATE', font=FONT, fill=FG)
        image.paste(left, (0, 32))
        image.paste(right, (left.width + 4, 32))
        text = f"t={cf['time']:.3f}s   {phase(cf)}   |   {view}; actual captured poses"
        draw.text((12, left.height + 40), text, font=SMALL, fill=FG)
        frames.append(image)
    name = f'{courier}-comparison.gif' if sequence == 'short-pair' else f'{courier}-long-comparison.gif'
    frames[0].save(ROOT / name, save_all=True, append_images=frames[1:], loop=0,
                   duration=durations(candidate), optimize=False, disposal=2)
    return name


def shared_gameplay_crop(data, spec):
    frames = [f for capture in data for f in capture['frames'] if f['sequence'] == 'short-pair']
    bounds = [next(v for v in f['views'] if v['view'] == 'gameplay') for f in frames]
    x0 = max(0, math.floor(min(b['min']['x'] for b in bounds) * spec['width']) - 12)
    x1 = min(spec['width'], math.ceil(max(b['max']['x'] for b in bounds) * spec['width']) + 12)
    y0 = max(0, math.floor((1 - max(b['max']['y'] for b in bounds)) * spec['height']) - 12)
    y1 = min(spec['height'], math.ceil((1 - min(b['min']['y'] for b in bounds)) * spec['height']) + 12)
    return (x0, y0, x1, y1)


def make_gameplay_gif(ids, spec, baseline, candidate, box):
    count = len(select(candidate, ids[0], 'short-pair'))
    width, height = box[2] - box[0], box[3] - box[1]
    column = width * 2 + 10
    frames = []
    for i in range(count):
        image = Image.new('RGB', (column * len(ids), height + 96), BG)
        draw = ImageDraw.Draw(image)
        for n, courier in enumerate(ids):
            x = n * column
            draw.text((x + 10, 5), courier.upper(), font=FONT, fill=FG)
            draw.text((x + 10, 28), 'Baseline', font=SMALL, fill=FG)
            draw.text((x + width + 14, 28), 'Candidate', font=SMALL, fill=FG)
            image.paste(crop_cell(BASE, courier, 'short-pair', 'gameplay', i, spec, count, box), (x, 49))
            image.paste(crop_cell(CAND, courier, 'short-pair', 'gameplay', i, spec, count, box), (x + width + 4, 49))
        frame = select(candidate, ids[0], 'short-pair')[i]
        draw.text((10, height + 57), f"Gameplay camera | t={frame['time']:.3f}s | {phase(frame)} | fixed shared crop; native pixels", font=SMALL, fill=FG)
        frames.append(image)
    frames[0].save(ROOT / 'gameplay-comparison.gif', save_all=True, append_images=frames[1:], loop=0,
                   duration=durations(select(candidate, ids[0], 'short-pair')), optimize=False, disposal=2)
    return 'gameplay-comparison.gif'


def range_summary(frames):
    roll = [f['bodyRollDegrees'] for f in frames]
    scale = [f['visualScaleRatio']['y'] for f in frames]
    body_height = [f['bodyBoundsMax']['y'] - f['bodyBoundsMin']['y'] for f in frames]
    foot = [f['leftMinusRightFootWorldY'] for f in frames]
    return {'rollDegrees': [min(roll), max(roll)], 'scaleYRatio': [min(scale), max(scale)],
            'actualBodyAabbHeight': [min(body_height), max(body_height)], 'actualFootCentroidYDifference': [min(foot), max(foot)]}


def hop_midpoint(frames, hop):
    subset = [f for f in frames if f['label'] == f'hop-{hop}']
    frame = min(subset, key=lambda f: abs(f['progress'] - .5))
    return {'time': frame['time'], 'progress': frame['progress'], 'rollDegrees': frame['bodyRollDegrees'],
            'scaleYRatio': frame['visualScaleRatio']['y'], 'leftMinusRightActualFootCentroidY': frame['leftMinusRightFootWorldY'],
            'leftMinusRightFootControlLocalY': frame['leftMinusRightFootLocalY']}


def keyframes(ids, rear, gameplay, candidate, box):
    # Two mid-flight captures and the +35ms final landing. Keep all native image pixels.
    chosen = [4, 12, 17]
    w, h = rear['width'], rear['height']
    image = Image.new('RGB', (w * 2 * len(chosen), (h + 46) * len(ids)), BG)
    draw = ImageDraw.Draw(image)
    for row, courier in enumerate(ids):
        records = select(candidate, courier, 'short-pair')
        for col, index in enumerate(chosen):
            x, y = col * w * 2, row * (h + 46)
            frame = records[index]
            draw.text((x + 10, y + 4), f"{courier.upper()} {phase(frame)} {frame['time']:.3f}s | baseline / candidate", font=SMALL, fill=FG)
            for n, folder in enumerate((BASE, CAND)):
                image.paste(crop_cell(folder, courier, 'short-pair', rear['id'], index, rear, len(records)), (x + n * w, y + 28))
    image.save(ROOT / 'rear-keyframes.png')
    # A small native-pixel gameplay crop for a readable static check of both actual feet.
    w, h = box[2] - box[0], box[3] - box[1]
    image = Image.new('RGB', (w * 2 * len(chosen), (h + 46) * len(ids)), BG)
    draw = ImageDraw.Draw(image)
    for row, courier in enumerate(ids):
        records = select(candidate, courier, 'short-pair')
        for col, index in enumerate(chosen):
            x, y = col * w * 2, row * (h + 46)
            frame = records[index]
            draw.text((x + 5, y + 3), f"{courier} {frame['time']:.3f}s: before / after", font=SMALL, fill=FG)
            for n, folder in enumerate((BASE, CAND)):
                image.paste(crop_cell(folder, courier, 'short-pair', 'gameplay', index, gameplay, len(records), box), (x + n * w, y + 25))
    image.save(ROOT / 'gameplay-keyframes.png')


def project_gameplay(point, frame, spec):
    # OriginalPlayController camera translation/20-degree pitch, captured square-pixel portrait projection.
    px = point['x'] - spec['position']['x']
    py = point['y'] - spec['position']['y']
    pz = point['z'] - (spec['position']['z'] + frame['rootPosition']['z'])
    angle = math.radians(spec['euler']['x'])
    y = math.cos(angle) * py + math.sin(angle) * pz
    z = -math.sin(angle) * py + math.cos(angle) * pz
    tangent = math.tan(math.radians(spec['fieldOfView']) * .5)
    vx = .5 + px / (2 * z * tangent * spec['width'] / spec['height'])
    vy = .5 + y / (2 * z * tangent)
    return vx * spec['width'], (1 - vy) * spec['height']


def feet_detail(ids, spec, baseline, candidate):
    # Pixel magnification only. No markers, retouching or per-side repositioning.
    output = Image.new('RGB', (960, 230 * len(ids)), BG)
    draw = ImageDraw.Draw(output)
    crops = []
    for row, courier in enumerate(ids):
        b = select(baseline, courier, 'short-pair')
        c = select(candidate, courier, 'short-pair')
        for col, index in enumerate((4, 12)):
            points = [project_gameplay(f[key], f, spec) for f in (b[index], c[index])
                      for key in ('leftFootCentroid', 'rightFootCentroid')]
            x0 = math.floor(min(p[0] for p in points)) - 13
            x1 = math.ceil(max(p[0] for p in points)) + 13
            y0 = math.floor(min(p[1] for p in points)) - 18
            y1 = math.ceil(max(p[1] for p in points)) + 10
            box = (x0, y0, x1, y1)
            crops.append({'courier': courier, 'cell': index + 1, 'box': box, 'scale': 4, 'resampling': 'nearest'})
            x, y = col * 480, row * 230
            draw.text((x + 8, y + 4), f"{courier.upper()} hop {col + 1} t={c[index]['time']:.3f}s", font=FONT, fill=FG)
            draw.text((x + 8, y + 27), 'Gameplay actual boot pixels x4: baseline / candidate', font=SMALL, fill=FG)
            for n, folder in enumerate((BASE, CAND)):
                crop = crop_cell(folder, courier, 'short-pair', 'gameplay', index, spec, len(c), box)
                crop = crop.resize((crop.width * 4, crop.height * 4), Image.Resampling.NEAREST)
                output.paste(crop, (x + n * 240, y + 51))
    output.save(ROOT / 'gameplay-feet-detail.png')
    return crops


def main():
    baseline, candidate = read_capture(BASE), read_capture(CAND)
    assert baseline['framing'] == candidate['framing'], 'Cameras or scale differ'
    assert baseline['lightingDependencyHash'] == candidate['lightingDependencyHash'], 'Lighting changed'
    ids = [entry['courier'] for entry in candidate['inputs']]
    assert ids == [entry['courier'] for entry in baseline['inputs']]
    for b, c in zip(baseline['inputs'], candidate['inputs']):
        assert b['prefab'] == c['prefab'] and b['instanceScale'] == c['instanceScale']
    assert baseline['motionSourceSha256'] != candidate['motionSourceSha256'], 'Motion sources unexpectedly identical'
    views = {v['id']: v for v in candidate['framing']['views']}
    outputs, comparison = [], {}
    for courier in ids:
        comparison[courier] = {}
        for sequence in ('short-pair', 'long-flight'):
            b, c = select(baseline, courier, sequence), select(candidate, courier, sequence)
            assert len(b) == len(c)
            assert all((x['time'], x['label'], x['cell'], x['duration']) == (y['time'], y['label'], y['cell'], y['duration']) for x, y in zip(b, c))
            outputs.append(make_pair_gif(courier, sequence, views['rear-threequarter'], b, c))
            comparison[courier][sequence] = {'baseline': range_summary(b), 'candidate': range_summary(c),
                'midHop1': {'baseline': hop_midpoint(b, 1), 'candidate': hop_midpoint(c, 1)}}
            if sequence == 'short-pair':
                comparison[courier][sequence]['midHop2'] = {'baseline': hop_midpoint(b, 2), 'candidate': hop_midpoint(c, 2)}
    box = shared_gameplay_crop((baseline, candidate), views['gameplay'])
    outputs.append(make_gameplay_gif(ids, views['gameplay'], baseline, candidate, box))
    keyframes(ids, views['rear-threequarter'], views['gameplay'], candidate, box)
    feet_crops = feet_detail(ids, views['gameplay'], baseline, candidate)
    report = {'source': 'Matched actual Unity render sheets; no invented or interpolated poses.',
        'baselineFrames': len(baseline['frames']), 'candidateFrames': len(candidate['frames']), 'allViewsFit': True,
        'sameFraming': True, 'sameLighting': True, 'samePrefabPathsAndScales': True,
        'motionSourceSha256': {'baseline': baseline['motionSourceSha256'], 'candidate': candidate['motionSourceSha256']},
        'diagnosticFramingMargin': '60% over common rest silhouette (framing values match exactly).',
        'gifTiming': 'Actual sample intervals rounded to nearest 10ms; every captured pose preserved; final captured pose held400ms for loop readability.',
        'gameplayCrop': {'rectangleLeftTopRightBottom': box, 'sharedAcrossBothCapturesAndAllCouriers': True, 'resampled': False},
        'feetInspectionCrops': feet_crops,
        'comparisons': comparison, 'outputs': outputs + ['rear-keyframes.png', 'gameplay-keyframes.png', 'gameplay-feet-detail.png']}
    (ROOT / 'summary.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(json.dumps({'outputs': report['outputs'], 'frames': [len(baseline['frames']), len(candidate['frames'])],
                      'gameplayCrop': box, 'sameFraming': True, 'allViewsFit': True, 'comparisons': comparison}, indent=2))


if __name__ == '__main__':
    main()
