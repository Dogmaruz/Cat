"""Reproduce an editable musical score from retained note/onset evidence (stdlib only).

This is an authoring policy, not the recovered game's generator. Learned source
separation/transcription remain estimates; final musical acceptance needs listening.
The JSON retains evidence and explicit Follow choices so editing does not require
another AI music generation or model inference.
"""
import argparse
import copy
import hashlib
import json
import math
from pathlib import Path

MIN_AIR = .18
MAX_AIR = 1.95


def lane(midi):
    return round(max(-.9, min(.9, (midi - 68) * .12)), 4)


def supported_end(cue):
    return cue['time'] if cue['kind'] == 'Jump' else cue['endTime']


def timing(score):
    speed = float(score['authoring']['forwardSpeed'])
    delay = float(score['authoring']['delayTime'])
    if not math.isfinite(speed) or speed <= 0 or not MIN_AIR <= delay <= MAX_AIR:
        raise ValueError('Invalid authoring speed/preroll')
    return delay, round(score['audioDuration'] - max(1.1, 5 / speed + .25), 6)


def phrase_id(score, time):
    for i, phrase in enumerate(score.get('phrases', [])):
        if phrase['start'] <= time < phrase['end']:
            return 'phrase-' + str(i + 1)
    return ''


def jump(time, x, confidence, reason, score):
    return dict(time=round(time, 6), endTime=round(time, 6), kind='Jump',
                lane=x, points=[], confidence=confidence, reason=reason,
                phraseId=phrase_id(score, time))


def author(score):
    score = copy.deepcopy(score)
    policy = score['authoring']
    delay, finish = timing(score)
    notes = [n for n in score['notes'] if n['confidence'] >= .64
             and n['strength'] >= .4 and 60 <= n['midi'] <= 84
             and n['end'] - n['start'] >= .095 and n['start'] < finish - MIN_AIR]
    # Strong notes win close conflicts. Do not snap syncopated attacks to a grid.
    ranked = sorted(notes, key=lambda n: (-n['confidence'] - .012 * min(n.get('onsetStrength', 0), 8), n['start']))
    selected = []
    for note in ranked:
        if all(abs(note['start'] - old['start']) >= .20 - 1e-6 for old in selected):
            selected.append(note)
    selected.sort(key=lambda n: n['start'])
    cues = []
    for i, note in enumerate(selected):
        cue = jump(note['start'], lane(note['midi']), note['confidence'],
                   'Predicted meow-note attack; same-pitch reattacks preserved when supported by spectral onset evidence.', score)
        end = min(note['end'], selected[i + 1]['start'] - MIN_AIR if i + 1 < len(selected) else finish - MIN_AIR)
        spread = note.get('pitchSpreadSemitones')
        if end - note['start'] >= .6 and note['confidence'] >= .70 and note.get('pyinAgreement', 0) >= .60 and spread is not None and spread <= .9:
            cue.update(kind='Hold', endTime=round(end, 6),
                       reason='Stable separated-note estimate, supported by independent pitch tracking. End follows note release; later reattacks remain separate.')
        cues.append(cue)

    # In the instrumental opening, select actual salient attacks, not all beats.
    for onset in sorted(policy['supportOnsets'], key=lambda o: (-o['relativeStrength'], o['time'])):
        t = onset['time']
        if not any(a <= t < b for a, b in policy['instrumentalSpans']):
            continue
        if onset['relativeStrength'] < .19 or onset['localRmsDbfs'] < -42:
            continue
        if all(abs(t - c['time']) >= .32 for c in cues):
            cues.append(jump(t, 0, .65, 'Salient measured instrumental attack in a passage without a predicted lead; no metrical filler.', score))
    cues.sort(key=lambda c: c['time'])

    for annotation in policy.get('manualCues', []):
        cue = copy.deepcopy(annotation)
        a, b = cue['time'], supported_end(cue)
        cues = [c for c in cues if not a - MIN_AIR < c['time'] < b + MIN_AIR]
        cue.setdefault('points', [])
        cue.setdefault('phraseId', phrase_id(score, a))
        cues.append(cue)
    cues.sort(key=lambda c: c['time'])

    # These spans are editorial choices. A sequence of pitches alone does not
    # establish legato, so they are retained explicitly for listening revisions.
    for span in policy['followSpans']:
        a, b = span['start'], span['end']
        material = [n for n in notes if a - 1e-5 <= n['start'] < b - .12]
        if not material:
            raise ValueError('Follow has no note evidence: ' + str(a))
        points = [dict(time=a, lane=lane(material[0]['midi']))]
        for note in material[1:]:
            dt = note['start'] - points[-1]['time']
            x = lane(note['midi'])
            if dt >= .12 and abs(x - points[-1]['lane']) <= 2.2 * dt:
                points.append(dict(time=note['start'], lane=x))
        if b - points[-1]['time'] < .12:
            points.pop()
        points.append(dict(time=b, lane=points[-1]['lane']))
        if max(p['lane'] for p in points) - min(p['lane'] for p in points) < .2:
            raise ValueError('Follow has no meaningful contour')
        cues = [c for c in cues if not a - MIN_AIR < c['time'] < b + MIN_AIR]
        for cue in cues:
            if cue['time'] < a and supported_end(cue) > a - MIN_AIR:
                cue['endTime'] = round(a - MIN_AIR, 6)
                if cue['endTime'] - cue['time'] < .6:
                    cue.update(kind='Jump', endTime=cue['time'])
        cues.append(dict(time=a, endTime=b, kind='Follow', lane=points[0]['lane'], points=points,
                         confidence=round(sum(n['confidence'] for n in material) / len(material), 3),
                         reason=span['reason'], phraseId=phrase_id(score, a)))
        cues.sort(key=lambda c: c['time'])

    # Bridge unsafe unsupported travel ONLY through measured audible events.
    # Dynamic programming minimizes extra actions while preferring salient events.
    # If the recording gives no support, refuse it for manual musical authoring.
    supports = policy['supportOnsets']
    if not cues:
        raise ValueError('No supported musical cues; provide note or instrumental evidence.')
    pending = [(-delay, cues[0]['time'])] + [(supported_end(a), b['time']) for a, b in zip(cues, cues[1:])] + [(supported_end(cues[-1]), finish)]
    for a, b in pending:
        if b - a <= MAX_AIR + 1e-6:
            continue
        candidates = [o for o in supports if a + MIN_AIR <= o['time'] <= b - MIN_AIR
                      and o['localRmsDbfs'] > -48 and o['relativeStrength'] >= .075]
        times = [a] + [o['time'] for o in candidates] + [b]
        costs = [0] + [math.inf] * (len(times) - 1)
        back = [-1] * len(times)
        for j in range(1, len(times)):
            for i in range(j - 1, -1, -1):
                gap = times[j] - times[i]
                if gap > MAX_AIR + 1e-6:
                    break
                if gap < MIN_AIR - 1e-6:
                    continue
                salience = 1 if j == len(times) - 1 else candidates[j - 1]['relativeStrength']
                cost = costs[i] + (0 if j == len(times) - 1 else 1.4 - min(salience, 1)) + .025 * gap * gap
                if cost < costs[j]:
                    costs[j], back[j] = cost, i
        if not math.isfinite(costs[-1]):
            raise ValueError('No evidenced support through gap %.3f–%.3f; annotate manually.' % (a, b))
        j = back[-1]
        while j > 0:
            onset = candidates[j - 1]
            cues.append(jump(onset['time'], 0, .6, 'Measured accompaniment accent during a lead gap; selected for playable travel, never a fabricated beat.', score))
            j = back[j]
    cues.sort(key=lambda c: c['time'])
    # Keep inter-support movement inside this chart's easier input policy.
    previous_x = 0
    for cue in cues:
        entry = max(previous_x - 1, min(previous_x + 1, cue['lane']))
        if cue['kind'] == 'Follow':
            offset = entry - cue['lane']
            for point in cue['points']:
                point['lane'] = round(max(-.9, min(.9, point['lane'] + offset)), 4)
            cue['lane'] = cue['points'][0]['lane']
            previous_x = cue['points'][-1]['lane']
        else:
            cue['lane'] = round(entry, 4)
            previous_x = cue['lane']
    tail = policy['nearSilenceStart']
    if tail < finish:
        cues.append(dict(time=tail, endTime=finish, kind='Rest', lane=0, points=[], confidence=.95,
                         reason='Measured recording tail; no scored note added during near-silence. Technical finish is separate.', phraseId=phrase_id(score, tail)))
    score['cues'] = cues
    validate(score)
    return score


def validate(score):
    playable = [c for c in score['cues'] if c['kind'] != 'Rest']
    delay, finish = timing(score)
    previous_end, previous_x = -delay, 0
    for cue in playable:
        gap = cue['time'] - previous_end
        if not MIN_AIR - 1e-6 <= gap <= MAX_AIR + 1e-6:
            raise ValueError('Unsafe unsupported interval %s before %s' % (gap, cue['time']))
        if abs(cue['lane'] - previous_x) > 1.00001:
            raise ValueError('Unreachable lateral entry')
        if cue['kind'] == 'Follow':
            for a, b in zip(cue['points'], cue['points'][1:]):
                dt = b['time'] - a['time']
                if dt < .12 - 1e-6 or abs(b['lane'] - a['lane']) > dt * 2.2 + 1e-6:
                    raise ValueError('Follow exceeds the chosen .12s /2.2 units/s authoring policy')
            previous_x = cue['points'][-1]['lane']
        else:
            previous_x = cue['lane']
        previous_end = supported_end(cue)
    if not MIN_AIR - 1e-6 <= finish - previous_end <= MAX_AIR + 1e-6:
        raise ValueError('Unsafe finish travel')
    return playable


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, required=True, help='Score JSON retaining notes and authoring policy/onsets')
    parser.add_argument('--audio', type=Path, required=True, help='Exact imported source, checked by SHA-256')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    evidence = json.loads(args.evidence.read_text(encoding='utf-8-sig'))
    if hashlib.sha256(args.audio.read_bytes()).hexdigest() != evidence['sourceSha256']:
        raise ValueError('Source audio SHA-256 changed; analyze the new recording first.')
    result = author(evidence)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print({kind: sum(c['kind'] == kind for c in result['cues']) for kind in ['Jump', 'Hold', 'Follow', 'Rest']})


if __name__ == '__main__':
    main()
