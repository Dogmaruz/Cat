"""Plot independent PCM charts alongside an unchanged reference map."""
import argparse
import json
from pathlib import Path

import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt
from matplotlib.patches import Rectangle, Patch

PLAYABLE = {"Jump", "BigJump", "Theme", "Hold", "Follow"}
COLORS = {"Jump": "#208d94", "BigJump": "#8165be", "Theme": "#208d94",
          "Hold": "#ef9261", "Follow": "#496fc4"}


def load(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def plot(reference, baseline, candidate, output):
    fig, axes = plt.subplots(3, 1, figsize=(15, 6.5), sharex=True)
    fig.patch.set_facecolor("#fbf8f0")
    for ax, path, label in zip(axes, [reference, baseline, candidate],
                               ["Оригинал Unstoppable", "Прежний генератор", "Новый генератор"]):
        data = load(path)
        segments = [s for s in data["segments"] if s["kind"] in PLAYABLE]
        ax.set_facecolor("#fbf8f0")
        for segment in segments:
            kind = segment["kind"]
            start = segment["audioTime"]
            duration = segment.get("duration", 0)
            level = {"Hold": .65, "Follow": 1.0}.get(kind, .3)
            if kind in {"Hold", "Follow"}:
                ax.add_patch(Rectangle((start, level - .065), max(.02, duration), .13,
                                       facecolor=COLORS[kind], alpha=.85, linewidth=0))
            ax.plot(start, level, "o" if kind not in {"BigJump"} else "^",
                    color=COLORS[kind], markersize=3, markeredgewidth=0)
        ax.set_title(f"{label}  ·  {len(segments)} основных акцентов", loc="left", fontsize=11, pad=9)
        ax.set_ylim(.05, 1.22)
        ax.set_yticks([.3, .65, 1.0], ["Прыжок", "Удержание", "Прокат"], fontsize=8)
        ax.grid(axis="x", color="#d9dedb", linewidth=.6)
        for spine in ax.spines.values():
            spine.set_visible(False)
    axes[-1].set_xlim(0, 67.1)
    axes[-1].set_xlabel("Время в музыке, секунды")
    fig.suptitle("Unstoppable: расположение и длительность плиток", x=.075, ha="left", fontsize=15)
    fig.text(.075, .925, "Самостоятельный анализ аудио. Исходная карта и музыка сохранены без изменений.",
             fontsize=9, color="#607579")
    labels = {"Jump": "Обычная плитка", "BigJump": "Большая", "Hold": "Удержание", "Follow": "Прокат"}
    fig.legend(handles=[Patch(color=COLORS[k], label=labels[k]) for k in ["Jump", "BigJump", "Hold", "Follow"]],
               loc="lower center", ncol=4, frameon=False, fontsize=9)
    fig.subplots_adjust(top=.86, bottom=.14, left=.075, right=.985, hspace=.65)
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    fig.savefig(output.with_suffix(".png"), dpi=140, facecolor=fig.get_facecolor())
    fig.savefig(output.with_suffix(".svg"), facecolor=fig.get_facecolor())
    plt.close(fig)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ["reference", "baseline", "candidate", "output"]:
        parser.add_argument(name)
    args = parser.parse_args()
    plot(args.reference, args.baseline, args.candidate, args.output)
