"""Build a local, dependency-free catalog; never edits the reference images."""
import html
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parent
brief = json.loads((ROOT / "brief.json").read_text(encoding="utf-8-sig"))
labels = {"front": "Спереди", "back": "Сзади", "right": "Правый бок", "left": "Левый бок"}
groups = [
    ("Characters", "Персонажи", brief["characters"], "Тело в T-позе"),
    ("Backpacks", "Рюкзаки", brief.get("backpacks", brief["characters"]), "Рюкзак, ремни и антенна"),
    ("Hats", "Головные уборы", brief["hats"], "Отдельный головной убор"),
    ("Scarves", "Шарфы", brief["scarves"], "Отдельный тканевый элемент"),
]
cards = []
complete = 0
for category, title, items, subtitle in groups:
    for item in items:
        asset = ROOT / category / item["id"]
        primary = "front"
        if category == "Backpacks":
            primary = "right" if item["id"] in ("Tiko", "Nori", "Lumen") else "back"
        present = sum((asset / (view + ".png")).is_file() for view in labels)
        complete += present
        thumbs = "".join(
            '<a class="view" href="' + category + "/" + item["id"] + "/" + view + '.png" target="_blank">'
            '<img loading="lazy" src="' + category + "/" + item["id"] + "/" + view + '.png" alt="' + label + '">'
            '<span>' + label + '</span></a>' for view, label in labels.items()
        )
        main = category + "/" + item["id"] + "/" + primary + ".png"
        cards.append('<article class="card" data-category="' + category + '"><a class="cover" href="' + main +
            '" target="_blank"><img src="' + main + '" loading="lazy" alt="' + html.escape(item["name"]) +
            '"></a><div class="body"><p class="eyebrow">' + html.escape(subtitle) + '</p><h2>' +
            html.escape(item["name"]) + '</h2><p class="id">' + category + "/" + item["id"] +
            '</p><div class="views">' + thumbs + '</div><span class="status">' + str(present) +
            ' / 4 вида</span></div></article>')
style = """
*{box-sizing:border-box}body{margin:0;background:#f4f1e8;color:#193447;font:16px/1.5 system-ui,sans-serif}
header{padding:48px max(24px,calc((100% - 1480px)/2)) 28px;background:#fffdf7;border-bottom:1px solid #dedfd4}
.eyebrow{color:#58877e;font-size:12px;letter-spacing:.12em;text-transform:uppercase;margin:0 0 8px}
h1{font-size:clamp(30px,4vw,54px);letter-spacing:-.04em;line-height:1.05;margin:0 0 16px}
.intro{max-width:860px;color:#607580;margin:0}.chips{display:flex;flex-wrap:wrap;gap:10px;margin-top:22px}
.chip{border:1px solid #d8dfd5;border-radius:30px;padding:7px 14px;background:#f3f8ef;font-size:14px}
nav{position:sticky;top:0;z-index:2;background:#fffdf7ee;backdrop-filter:blur(12px);padding:14px 24px;display:flex;justify-content:center;flex-wrap:wrap;gap:8px;border-bottom:1px solid #dedfd4}
button{font:inherit;border:0;border-radius:24px;padding:9px 18px;background:transparent;color:#506a75;cursor:pointer}
button.active{background:#234d50;color:white}main{max-width:1528px;margin:28px auto;padding:0 24px;display:grid;grid-template-columns:repeat(auto-fill,minmax(300px,1fr));gap:24px}
.card{border:1px solid #d9ddd4;border-radius:20px;overflow:hidden;background:#fffdf8;box-shadow:0 4px 20px #19344707}
.cover{display:block;aspect-ratio:1;background:#e8e9e9}.cover img{width:100%;height:100%;object-fit:contain;display:block}
.body{padding:20px}h2{font-size:25px;line-height:1.15;margin:0 0 7px}.id{font-size:12px;color:#78909b;margin:0 0 16px}
.views{display:grid;grid-template-columns:repeat(4,1fr);gap:7px}.view{color:#365d68;text-decoration:none;font-size:11px;text-align:center}
.view img{display:block;width:100%;aspect-ratio:1;object-fit:contain;border-radius:8px;background:#e8e9e9;margin-bottom:5px}
.status{display:block;font-size:11px;color:#8a9a99;margin-top:14px}.card[hidden]{display:none}
footer{max-width:1480px;margin:32px auto 64px;padding:0 24px;color:#66818a;font-size:13px}
"""
nav = '<button class="active" data-filter="all">Всё</button>' + "".join(
    '<button data-filter="' + category + '">' + title + '</button>' for category, title, _, _ in groups)
page = '<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Радиоархипелаг · Модульная коллекция</title><style>' + style + '</style><header><p class="eyebrow">Радиоархипелаг / Референсы для Tripo</p><h1>Герои и их снаряжение</h1><p class="intro">Десять самостоятельных персонажей и отдельные сменные предметы. Нажми на изображение, чтобы открыть полный PNG. Все четыре проекции каждого предмета лежат в его папке.</p><div class="chips"><span class="chip">10 героев</span><span class="chip">20 рюкзаков</span><span class="chip">8 головных уборов</span><span class="chip">2 шарфа</span><span class="chip">' + str(complete) + ' / 160 PNG</span></div></header><nav>' + nav + '</nav><main>' + "".join(cards) + '</main><footer>Тела созданы без экипировки. Шарфы и головные уборы генерируй отдельными моделями. Для рюкзака спереди показана сторона к спине героя, сзади — внешняя сторона. Готовые модели складывай в соответствующие папки Incoming; инструкция — README.ru.md.</footer><script>document.querySelectorAll("button[data-filter]").forEach(b=>b.onclick=()=>{document.querySelectorAll("button[data-filter]").forEach(x=>x.classList.toggle("active",x===b));document.querySelectorAll(".card").forEach(c=>c.hidden=b.dataset.filter!=="all"&&c.dataset.category!==b.dataset.filter)});</script></html>'
(ROOT / "index.html").write_text(page, encoding="utf-8")
print("Gallery:", complete, "/ 160 PNG;", ROOT / "index.html")
