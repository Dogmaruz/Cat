"""Validate and package the island references. Never modifies generated PNGs."""
import hashlib, html, json, math, pathlib, sys, zipfile

ROOT = pathlib.Path(__file__).resolve().parent
REPO = ROOT.parents[2]
sys.path.insert(0, str(REPO/'DancingCatsClone/Library/OriginalChartCalibration/plot-deps'))
from PIL import Image

VIEWS = {'front':'Спереди', 'right':'Справа', 'left':'Слева', 'back':'Сзади', 'top':'Сверху'}
def recorded_hashes(value):
    if isinstance(value,dict):
        if isinstance(value.get('sha256'),str):
            yield value['sha256'].lower()
        for child in value.values():
            yield from recorded_hashes(child)
    elif isinstance(value,list):
        for child in value:
            yield from recorded_hashes(child)

specs = json.loads((ROOT/'pack-specs.json').read_text(encoding='utf8'))
manifest = []
missing = []
for spec in specs:
    folder = ROOT/'ReferencePack_v1'/spec['id']
    route = json.loads((folder/'route.json').read_text(encoding='utf8'))
    loop = route['physicalLoopPoints']
    assert loop[0] == loop[-1], spec['id']+' loop must close'
    assert route['physicalLoopClosed'] and route['examplePasses'] == 2.5
    assert route['roadWidthNormalized'] == .12
    assert len(loop) == 601 and len(route['samplePoints']) == 1501
    assert math.isclose(route['physicalLoopNormalizedLength']*route['examplePasses']*route['uniformXZScaleExample'],
                        route['durationTargetSeconds']*route['forwardSpeedExample'], rel_tol=1e-10)
    assert route['minRadiusAtExampleScale'] > 20
    images = []
    for view in VIEWS:
        path = folder/(view+'.png')
        if not path.exists():
            missing.append(str(path.relative_to(ROOT)))
            continue
        with Image.open(path) as image:
            size, mode = image.size, image.mode
            image.verify()
        assert min(size) >= 1000, str(path)+' unexpectedly small'
        images.append(dict(view=view, path=str(path.relative_to(ROOT)).replace('\\','/'), width=size[0], height=size[1],
                           mode=mode, bytes=path.stat().st_size, sha256=hashlib.sha256(path.read_bytes()).hexdigest()))
    generation_path = folder/'generation.json'
    if not generation_path.exists():
        missing.append(str(generation_path.relative_to(ROOT)))
    generation = json.loads(generation_path.read_text(encoding='utf8')) if generation_path.exists() else {}
    if generation:
        known_hashes=set(recorded_hashes(generation))
        for selected in images:
            assert selected['sha256'] in known_hashes, spec['id']+'/'+selected['view']+' provenance hash mismatch'
    manifest.append(dict(id=spec['id'], title=spec['title'], images=images,
                         exampleSeconds=route['durationTargetSeconds'], examplePasses=route['examplePasses'],
                         exampleScale=route['uniformXZScaleExample'], exampleRoadWidth=route['roadWidthNormalized']*route['uniformXZScaleExample'],
                         exampleMinRadius=route['minRadiusAtExampleScale'], generation=generation))

count = sum(len(item['images']) for item in manifest)
assert len({image['sha256'] for island in manifest for image in island['images']}) == count, 'Duplicate final PNGs'
if '--require-complete' in sys.argv and missing:
    raise SystemExit('Not complete: '+json.dumps(missing, ensure_ascii=False))
report = dict(schemaVersion=1, finalViewCount=count, targetViewCount=50, islandCount=10, missing=missing, provenanceHashesVerified=not missing,
              tool='Built-in image_gen; no Runway', imageReview='Concept references, not calibrated orthographic blueprints. See README caveats.',
              routeValidation='10 closed periodic route references, 2.5 passes/75s numerical example; not tested game splines.', islands=manifest)
(ROOT/'manifest.json').write_text(json.dumps(report, ensure_ascii=False, indent=2)+'\n', encoding='utf8')

descriptions = [
 'Радиодомик, два кипариса и круговая дорожка среди золотистых цветов.',
 'Маяк с мятным фонарём, домиком смотрителя и скальной аркой.',
 'Три светлые арки, бронзовый колокол и сад пряных трав.',
 'Мастерская с тканевыми парусами и медным баком.',
 'Обсерватория с мятным куполом и бронзовой армиллярной сферой.',
 'Мятная лагуна, кристаллы, мост и два водопада.',
 'Небольшой воздушный порт с парусными навесами и ангаром.',
 'Стеклянная оранжерея с медными рёбрами и солнечными растениями.',
 'Расщелина, широкий каменный мост и бронзовые резонаторы.',
 'Большая радиобашня с кольцом сигнала и плавными террасами.',
]
gallery = [dict(id=s['id'], title=s['title'], description=descriptions[i]) for i,s in enumerate(specs)]
template = r'''<!doctype html>
<html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>Радиоархипелаг — острова</title>
<style>
*{box-sizing:border-box}body{margin:0;background:#f5f1e8;color:#192d48;font:16px/1.5 system-ui,sans-serif}
main{max-width:1480px;margin:auto;padding:28px}header{display:flex;align-items:end;gap:24px;justify-content:space-between;flex-wrap:wrap}
h1{font-size:30px;margin:0}p{margin:8px 0 20px;color:#526479}label{font-size:14px;font-weight:600}select,button,a.action{font:inherit;border:1px solid #d4d7d2;border-radius:12px;background:#fffdf7;color:#192d48;padding:10px 16px}
select{max-width:100%;margin-top:7px}nav{display:flex;gap:8px;flex-wrap:wrap;margin:20px 0}button{cursor:pointer}button[aria-pressed=true]{background:#233e56;color:white;border-color:#233e56}
.frame{background:#eae7df;border-radius:20px;overflow:hidden;min-height:280px;display:flex;align-items:center;justify-content:center}img{display:block;width:100%;height:70vh;min-height:280px;object-fit:contain}
footer{display:flex;gap:16px;justify-content:space-between;align-items:center;flex-wrap:wrap;margin:18px 0}.action{text-decoration:none;display:inline-block}small{color:#526479}a{color:#1d6d67}.note{max-width:900px;font-size:14px}
@media(max-width:600px){main{padding:16px}h1{font-size:23px}img{height:52vh}button{padding:8px 12px}}
</style>
<main><header><div><h1>Радиоархипелаг / острова</h1><p id="description"></p></div><label>Остров<br><select id="island" aria-label="Выбрать остров"></select></label></header>
<nav id="views" aria-label="Ракурс"></nav><div class="frame"><img id="preview" alt=""></div>
<footer><strong id="caption"></strong><a id="download" class="action" download>Скачать отдельный PNG</a></footer>
<p class="note">Пять отдельных изображений на остров. «Схема пути» — отдельный технический ориентир: одна плавная петля, повторные проходы идут по той же дороге. Пример: 2½ круга за 75 секунд. Это художественные референсы; точную геометрию и ширину пути закрепим в 3D. Часть верхних ракурсов имеет перспективу.</p>
<small><a href="README.md">Описание и передача в 3D</a> · <a href="RadioArchipelago_Islands_References_v1.zip">Все 50 PNG и схемы, ZIP</a></small>
</main><script>
const islands=__DATA__,views={front:'Спереди',right:'Справа',left:'Слева',back:'Сзади',top:'Сверху',route:'Схема пути'};
const select=document.getElementById('island'),nav=document.getElementById('views'),preview=document.getElementById('preview');let view='front';
islands.forEach((item,i)=>{const option=document.createElement('option');option.value=i;option.textContent=String(i+1).padStart(2,'0')+' — '+item.title;select.append(option)});
Object.entries(views).forEach(([key,label])=>{const b=document.createElement('button');b.type='button';b.textContent=label;b.dataset.view=key;b.addEventListener('click',()=>{view=key;render()});nav.append(b)});
function render(){const item=islands[Number(select.value)];const src=view==='route'?'RouteGuides_v1/'+item.id+'/route-guide.png':'ReferencePack_v1/'+item.id+'/'+view+'.png';preview.src=src;preview.alt=item.title+' — '+views[view];document.getElementById('description').textContent=item.description;document.getElementById('caption').textContent=preview.alt;const a=document.getElementById('download');a.href=src;a.download=item.id+'_'+view+'.png';nav.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===view)))}
select.addEventListener('change',render);render();
</script></html>'''
(ROOT/'index.html').write_text(template.replace('__DATA__',json.dumps(gallery,ensure_ascii=False)),encoding='utf8')

rows='\n'.join('| '+s['id']+' | '+s['title']+' | '+descriptions[i]+' |' for i,s in enumerate(specs))
readme='''# Радиоархипелаг — референсы островов v1

10 островов, по 5 самостоятельных PNG: `front.png` (спереди), `right.png` (справа), `left.png` (слева), `back.png` (сзади), `top.png` (сверху). Открыть `index.html` для просмотра и скачивания каждого изображения. Архив содержит только выбранные финальные ракурсы, схемы и метаданные; промежуточные варианты в него не входят.

| Папка | Остров | Основные элементы |
|---|---|---|
'''+rows+'''

## Идея маршрута

Один остров соответствует одной песне. Между островами возможны отдельные воздушные уровни без островной поверхности. На самом острове одна широкая плавная дорога огибает декорации, а музыкальные плитки появляются и исчезают независимо от поверхности. Одну и ту же дорогу можно проходить повторно. В этом наборе показана простая замкнутая петля; общие проходы и ответвления можно добавить после проверки камеры на первой модели.

Пример в `route.json`: 75 секунд, 2.5 прохода по одному физическому кругу, примерно 30 секунд на круг. При условной скорости 6.5 единиц/сек суммарный путь равен 487.5 единицы, а длина самого круга — 195 единиц. Это расчётный ориентир, не измеренная длительность маршрута на готовой модели. Финальную ширину, масштаб, перепады высот и скорость выбираем в Unity. Без резких поворотов: изменение направления на 180° занимает большую часть половины круга.

Схема рисует одну дорогу; повторные проходы не означают несколько вложенных дорог. В первом прототипе все проходы лучше направить в одну сторону, чтобы камера не разворачивалась резко. Плитки следующего круга должны включаться по времени песни/прогрессу. Удобно хранить пройденную дистанцию без сброса, а позицию на замкнутой дороге вычислять по остатку от длины круга. Плитки идентифицируются событием карты, а не только местом в пространстве. Так повторные круги не накладывают игровые события друг на друга.

Для внедрения позже: музыкальная карта задаёт времена и типы плиток; отдельный путь задаёт положение и направление; смещение вправо/влево идёт поперёк касательной пути; камера плавно следует за касательной с упреждением. Дистанцию следует переводить в параметр кривой через таблицу длины дуги: равномерные значения параметра не означают равномерную скорость. Эти изменения сейчас НЕ внесены в игровой код.

## Передача в моделирование

Используй фронтальный ракурс для художественного облика, остальные — как ориентиры сторон. Это сгенерированные концепты, не калиброванные чертежи одной 3D-сетки: часть верхних изображений остаётся перспективной, а мелкие скалы, флаги и декор могут отличаться между сторонами. Не нужно пытаться совместить пиксели разных видов в точную фотограмметрию. При расхождениях единую геометрию закрепляем в модели; непрерывность игровой дороги важнее декоративных ступеней на рисунке. Техническая схема пути приоритетна для замыкания и плавности дороги.

Сохрани раздельными объектами основание острова, дорогу, строения, деревья и крупные украшения. Дорога без ступеней, бордюров поперёк движения и высоких препятствий; модели мостов/рамп с непрерывным профилем. Мелкие обломки из концептов необязательны. Декор не должен перекрывать камеру и место для трёх полос. Коллайдеры, LOD и окончательный бюджет геометрии зададим после проверки первого острова на устройстве.

Модель можно положить в `ArtSource/RadioArchipelago/Incoming/Islands/<имя_папки>/`: предпочтительно GLB или FBX с текстурами. В Blender: Z вверх, перед к -Y; при импорте в Unity: Y вверх, перед к -Z, восток к +X. На схеме X — восток, Z — север. `front` смотрит с юга, `back` с севера, `right` с востока, `left` с запада. Точное начало координат и масштаб закрепим при импорте; дорогу не запекай в фон.

## Состав и происхождение

- `ReferencePack_v1/<id>/` — пять выбранных PNG, brief.json, prompts.json, generation.json и route.json.
- `RouteGuides_v1/<id>/` — отдельные технические PNG/SVG плавного пути.
- `Source/` — исходный дизайн и первый остров как ориентир материалов.
- `manifest.json` — размеры, SHA256 и метаданные выбранных файлов.
- `generation.json` в каждой папке — фактически использованные запросы, референсы, исходные пути и замечания проверки. Они точнее шаблонов prompts.json, потому что ракурсы корректировались.

Использован встроенный image_gen. Runway не использовался, его кредиты на эту работу не расходовались. Музыка, генератор карт, существующие уровни и анимации не менялись. Сборки игры не создавались. Это набор художественных референсов и схем; 3D-моделей островов и внедрения в игру в этом пакете пока нет.
'''
(ROOT/'README.md').write_text(readme,encoding='utf8')

if not missing:
    archive=ROOT/'RadioArchipelago_Islands_References_v1.zip'
    with zipfile.ZipFile(archive,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for p in (ROOT/'Source').glob('*.png'):
            z.write(p,p.relative_to(ROOT))
        for item in manifest:
            folder=ROOT/'ReferencePack_v1'/item['id']
            for name in [view+'.png' for view in VIEWS]+['brief.json','prompts.json','generation.json','route.json']:
                p=folder/name;z.write(p,p.relative_to(ROOT))
            for name in ['route-guide.png','route-guide.svg']:
                p=ROOT/'RouteGuides_v1'/item['id']/name;z.write(p,p.relative_to(ROOT))
        for name in ['README.md','index.html','manifest.json','pack-specs.json']:
            z.write(ROOT/name,name)
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert len([n for n in z.namelist() if n.startswith('ReferencePack_v1/') and n.endswith('.png')])==50
    print('Validated 50 PNGs, 10 route references, provenance JSON and ZIP CRC. Archive bytes:',archive.stat().st_size)
else:
    print('Prepared partial gallery. Final views:',count,'/50. Missing:',len(missing))
