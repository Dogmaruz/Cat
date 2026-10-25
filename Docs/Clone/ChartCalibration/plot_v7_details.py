"""Read-only comparison of large launches and Follow choreography."""
import argparse
import json
from pathlib import Path

import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt


def load(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def follows(data):
    return [s for s in data['segments'] if s['kind'] == 'Follow']


def route(segment):
    points = [(0, segment['x'])]
    for point in segment['path']:
        phase = (point['audioTime'] - segment['audioTime']) / segment['duration']
        if phase <= points[-1][0] + 1e-7:
            continue
        # The original release can reset its X within a millisecond. That reset
        # is an outgoing launch, rather than a traversable section of the ride.
        if point['kind'] == 'FollowEnd' and (phase - points[-1][0]) * segment['duration'] < .02 and abs(point['x'] - points[-1][1]) > .2:
            continue
        points.append((phase, point['x']))
    return list(zip(*points))


def plot(reference, before, candidate, output):
    maps = [load(path) for path in (reference, before, candidate)]
    fig = plt.figure(figsize=(13, 7), facecolor='#fbf8f0')
    grid = fig.add_gridspec(2, 2, height_ratios=[.65, 1], hspace=.7, wspace=.25)
    top = fig.add_subplot(grid[0, :])
    for index, data in enumerate(maps):
        times = [s['audioTime'] for s in data['segments'] if s['kind'] == 'BigJump']
        top.scatter(times, [2-index]*len(times), marker='^', s=55, color=['#35465a', '#cf9e77', '#168f91'][index])
    top.set_yticks([2, 1, 0], ['Оригинал: 7', 'v6: 0', 'v7: 4'])
    top.set_xlim(0, 67.1)
    top.set_ylim(-.5, 2.5)
    top.set_xlabel('Время в музыке, с')
    top.set_title('Большие прыжки: 4 совпадения, 0 лишних; ещё 3 пропущены', loc='left', fontsize=12)
    for number in range(2):
        ax = fig.add_subplot(grid[1, number])
        for data, label, color, style in zip(maps, ['Оригинал', 'v6', 'v7'], ['#35465a', '#cf9e77', '#168f91'], ['-', '--', '-']):
            x, y = route(follows(data)[number])
            ax.plot(x, y, style, color=color, label=label, linewidth=2)
        ax.set_title(f'Прокат {number+1}: направление '+ ('37% → 95%' if number == 0 else '100% → 100%'), loc='left', fontsize=12)
        ax.set_xlabel('Доля музыкальной фразы')
        ax.set_ylabel('Координата полосы')
        ax.set_xlim(0, 1)
        ax.set_ylim(-1.15, 1.15)
        ax.legend(frameon=False, fontsize=9)
    for ax in fig.axes:
        ax.set_facecolor('#fbf8f0')
        ax.grid(color='#d9dedb', linewidth=.6)
        for spine in ax.spines.values():
            spine.set_visible(False)
    fig.suptitle('Unstoppable · большие прыжки и форма прокатов', x=.07, ha='left', fontsize=16)
    fig.text(.07, .025, 'Повторная калибровка одной записи. Исходная карта сохранена. Пространственный путь — игровая хореография.', fontsize=9, color='#607579')
    fig.subplots_adjust(left=.09, right=.97, top=.86, bottom=.13)
    fig.savefig(output, dpi=140, facecolor=fig.get_facecolor())
    plt.close(fig)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('reference', 'before', 'candidate', 'output'):
        parser.add_argument(name)
    plot(**vars(parser.parse_args()))
