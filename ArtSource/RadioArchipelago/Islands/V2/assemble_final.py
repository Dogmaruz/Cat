"""Validate/package selected final references; generated images remain untouched."""
import hashlib,json,pathlib,sys,zipfile
ROOT=pathlib.Path(__file__).resolve().parent
REPO=ROOT.parents[3]
sys.path.insert(0,str(REPO/'DancingCatsClone/Library/OriginalChartCalibration/plot-deps'))
from PIL import Image
VIEWS={'front':'Спереди','right':'Справа','left':'Слева','back':'Сзади','top':'Сверху'}
worlds=json.loads((ROOT/'worlds-final.json').read_text(encoding='utf8'))
def hashes(value):
    if isinstance(value,dict):
        for key,digest in value.items():
            if key.lower() in ('sha256','source_sha256','output_sha256','sourcesha256','outputsha256') and isinstance(digest,str):yield digest.lower()
        for item in value.values():yield from hashes(item)
    elif isinstance(value,list):
        for item in value:yield from hashes(item)

missing=[];items=[]
for w in worlds:
    folder=ROOT/'FinalReferences'/w['id'];images=[]
    gp=folder/'generation.json'
    if not gp.exists():missing.append(str(gp.relative_to(ROOT)))
    g=json.loads(gp.read_text(encoding='utf8')) if gp.exists() else {}
    known=set(hashes(g))
    for view in VIEWS:
        p=folder/(view+'.png')
        if not p.exists():missing.append(str(p.relative_to(ROOT)));continue
        with Image.open(p) as im:size=im.size;im.verify()
        assert min(size)>=1000,str(p)+' too small'
        digest=hashlib.sha256(p.read_bytes()).hexdigest()
        if digest not in known:missing.append(str(p.relative_to(ROOT))+' provenance hash pending/mismatch')
        images.append(dict(view=view,path=str(p.relative_to(ROOT)).replace('\\','/'),width=size[0],height=size[1],bytes=p.stat().st_size,sha256=digest))
    rp=ROOT/'RouteGuidesClosed'/w['id']/'route.json'
    if not rp.exists():missing.append(str(rp.relative_to(ROOT)));route={}
    else:
        route=json.loads(rp.read_text(encoding='utf8'))
        r=route.get('route',route)
        assert route.get('referenceOnly') is True,w['id']+' referenceOnly missing'
        assert r.get('closed') is True,w['id']+' route must close'
        assert not r.get('selfCrossings'),w['id']+' route crossing'
    items.append(dict(id=w['id'],title=w['title'],images=images,provenance=g,route=route))
count=sum(len(x['images']) for x in items)
assert len({im['sha256'] for x in items for im in x['images']})==count,'Duplicate selected images'
if '--require-complete' in sys.argv and missing:raise SystemExit('Missing '+json.dumps(missing,ensure_ascii=False))
manifest=dict(schemaVersion=2,selectedViewCount=count,target=50,worldCount=10,missing=missing,tool='Built-in image_gen; no Runway',
              status='complete' if not missing else 'in_progress',note='Art/modeling references, not one reconstructed mesh or production Unity splines.',islands=items)
(ROOT/'manifest-final.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')

table='\n'.join('| '+w['id']+' | '+w['title']+' |' for w in worlds)
readme='''# Радиоархипелаг — разные миры, извилистые маршруты и здания

Финальная редакция v2 учитывает три уточнения: острова различаются палитрой/материалами/силуэтом, дорога петляет через внутренние части острова и возвращается к началу, здания принадлежат технологичному миру героев. Замыкание позволяет повторять проход до окончания песни; простой круг по краю острова больше не является основой композиции.

По 5 отдельных PNG на остров: front, right, left, back, top. Открыть index.html для выбора острова и ракурса. В FinalReferences/<id>/ находятся выбранные изображения; черновые открытые маршруты сохранены отдельно в ReferencePack/, старый набор v1 остался на уровень выше.

| Папка | Мир |
|---|---|
'''+table+'''

## Для Tripo и дальнейшей работы

Каждый ракурс сохранён самостоятельным файлом. Используй набор FinalReferences, а не черновые картинки. Геометрию дороги и размещение основных объектов задаёт итоговый top; техническая схема RouteGuidesClosed помогает проверить замыкание. Фронтальный вид показывает материалы, здания и скальную нижнюю часть. Остальные стороны раскрывают противоположные фасады и проходы.

Это художественные референсы, не калиброванные проекции одной готовой сетки. Высота камеры и мелкие детали скал/растений могут различаться. Точность дороги и проходимость модели после генерации нужно проверить; изображения сами по себе этого не гарантируют. Подробные замечания по каждому выбранному ракурсу записаны в generation.json.

Проходы должны оставаться открытыми ущельями. Допускается короткая каменная арка наподобие небольшого моста, с дневным светом и видимой дорогой с обеих сторон. Длинные закрытые тоннели не нужны. Дома, механизмы и деревья стоят вне дороги. Мосты и склоны без ступеней поперёк движения. По возможности раздели основание, дорогу, крупные здания, растительность и воду на объекты; музыкальные плитки в геометрию не добавляй.

Готовые GLB/FBX с текстурами складывай в ArtSource/RadioArchipelago/Incoming/IslandsV2/<id>/. Оси и масштаб проверим при импорте. В плане север сверху, восток справа. Front смотрит с юга, right с востока, left с запада, back с севера. Размеры островов окончательно выставим в Unity под ширину игровых полос и камеру.

## Маршрут и длительность песни

Физическая дорога образует сложный замкнутый извилистый путь. На каждом повторном проходе появляются новые музыкальные плитки по прогрессу песни, а использованные исчезают. Время карты и типы плиток не зависят от количества кругов. При внедрении нужно хранить полную пройденную дистанцию, а положение на дороге вычислять по остатку от длины пути; будущие проходы не должны заранее включать плитки на текущем.

Технические JSON содержат примеры масштабирования и выборки по длине дуги. Это ориентиры для моделирования, не внедрённые игровые траектории. Реальную проходимость, камеру, уклоны и длительность проверим на 3D-модели. Геометрические проверки схем не равны проверке готовой сцены.

## Происхождение и комплект

- FinalReferences/<id>/ — 5 выбранных PNG, brief.json, prompts.json, edit-prompts.json, generation.json.
- RouteGuidesClosed/<id>/ — итоговые технические PNG/SVG и route.json.
- worlds-final.json — все темы, материалы и архитектурные задания.
- manifest-final.json — размеры, SHA256 и фактически использованные запросы/референсы.
- Source/user-route-sketch.png — пользовательская схема, исходное направление для маршрутов.

Фактически использованные запросы в generation.json могут быть точнее шаблонов prompts.json: отдельные ракурсы исправлялись. Все изображения созданы встроенным image_gen; Runway не использовался. Игровой генератор, музыка, существующие уровни и анимации не изменялись. Сборок игры не создавалось; это комплект референсов, без внедрения островов в Unity.
'''
(ROOT/'README-final.md').write_text(readme,encoding='utf8')
cards=[dict(id=w['id'],title=w['title'],palette=w['palette']) for w in worlds]
page=r'''<!doctype html><html lang="ru"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Радиоархипелаг — разные миры v2</title>
<style>*{box-sizing:border-box}body{margin:0;background:#f4f1eb;color:#20384d;font:16px/1.5 system-ui,sans-serif}main{max-width:1500px;margin:auto;padding:24px}h1{font-size:28px;margin:0}p{color:#576877;margin:6px 0 16px}.strip{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:10px;margin:18px 0}.card{border:1px solid #d5d6d0;background:#fffdf8;color:inherit;border-radius:12px;padding:5px;cursor:pointer;text-align:left;font:inherit}.card img{height:90px;width:100%;object-fit:contain;background:#e8e7e2;border-radius:8px}.card span{display:block;padding:4px 6px;font-size:13px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.card[aria-pressed=true]{outline:2px solid #3f8d86}nav{display:flex;gap:8px;flex-wrap:wrap;margin:18px 0}nav button,a.action{border:1px solid #d2d6d1;border-radius:10px;background:#fffdf8;color:inherit;padding:9px 15px;font:inherit;text-decoration:none;cursor:pointer}nav button[aria-pressed=true]{background:#29475d;color:#fff}.frame{background:#e8e7e2;border-radius:18px;overflow:hidden}.frame img{display:block;width:100%;height:68vh;object-fit:contain;min-height:300px}footer{display:flex;justify-content:space-between;align-items:center;gap:12px;flex-wrap:wrap;margin:16px 0}a{color:#216d69}small{color:#576877}.note{max-width:1050px;font-size:14px}@media(max-width:700px){main{padding:14px}.strip{display:flex;overflow-x:auto;padding:3px 2px 8px;scroll-snap-type:x proximity}.card{flex:0 0 135px;scroll-snap-align:start}.card img{height:80px}.frame img{height:52vh}h1{font-size:23px}}</style>
<main><h1>Радиоархипелаг / разные миры</h1><p>Извилистые замкнутые дороги · здания мира героев · пять отдельных ракурсов</p><section class="strip" id="islands" aria-label="Выбрать остров"></section><h2 id="name"></h2><nav id="views" aria-label="Ракурс"></nav><div class="frame"><img id="preview" alt=""></div><footer><strong id="caption"></strong><a class="action" id="download" download>Скачать отдельный PNG</a></footer><p class="note">Дорога петляет через остров и возвращается к началу. Используем открытые ущелья и короткие каменные арки. Изображения — референсы для моделирования; точную геометрию и проходимость проверим в 3D. Некоторые ракурсы остаются перспективными.</p><small><a href="RadioArchipelago_DiverseWorlds_Final_v2.zip">Скачать комплект из 50 PNG и схем</a> · <a href="README-final.md">Описание и передача моделей</a> · <a href="../index.html">Предыдущий набор v1</a></small></main>
<script>const worlds=__DATA__,views={front:'Спереди',right:'Справа',left:'Слева',back:'Сзади',top:'Сверху',route:'Схема пути'};let island=0,view='front';const strip=document.getElementById('islands'),nav=document.getElementById('views'),preview=document.getElementById('preview');worlds.forEach((w,i)=>{const b=document.createElement('button');b.type='button';b.className='card';b.title=w.title;b.dataset.index=i;const img=document.createElement('img');img.src='FinalReferences/'+w.id+'/front.png';img.alt='';img.loading='lazy';b.append(img);const t=document.createElement('span');t.textContent=String(i+1).padStart(2,'0')+' '+w.title;b.append(t);b.addEventListener('click',()=>{island=i;render()});strip.append(b)});Object.entries(views).forEach(([k,v])=>{const b=document.createElement('button');b.type='button';b.textContent=v;b.dataset.view=k;b.addEventListener('click',()=>{view=k;render()});nav.append(b)});function render(){const w=worlds[island],src=view==='route'?'RouteGuidesClosed/'+w.id+'/route-guide.png':'FinalReferences/'+w.id+'/'+view+'.png';document.getElementById('name').textContent=w.title;preview.src=src;preview.alt=w.title+' — '+views[view];document.getElementById('caption').textContent=preview.alt;const a=document.getElementById('download');a.href=src;a.download=w.id+'_'+view+'.png';strip.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(Number(b.dataset.index)===island)));nav.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.view===view)))}render();</script></html>'''
(ROOT/'index.html').write_text(page.replace('__DATA__',json.dumps(cards,ensure_ascii=False)),encoding='utf8')

incoming=REPO/'ArtSource/RadioArchipelago/Incoming/IslandsV2'
for w in worlds:
    folder=incoming/w['id'];folder.mkdir(parents=True,exist_ok=True)
    (folder/'README.md').write_text('# '+w['title']+'\n\nСюда GLB/FBX с текстурами по итоговым референсам Islands/V2/FinalReferences/'+w['id']+'/. Дорога извилистая и замкнутая. Открытые ущелья/короткие арки, без закрытых тоннелей и ступеней поперёк пути. По возможности основание, дорога, здания, деревья и вода отдельными объектами. Масштаб и оси проверим при импорте в Unity.\n',encoding='utf8')
if not missing:
    archive=ROOT/'RadioArchipelago_DiverseWorlds_Final_v2.zip'
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for w in worlds:
            for name in [x+'.png' for x in VIEWS]+['brief.json','prompts.json','edit-prompts.json','generation.json']:
                p=ROOT/'FinalReferences'/w['id']/name;z.write(p,p.relative_to(ROOT))
            for name in ['route-guide.png','route-guide.svg','route.json']:
                p=ROOT/'RouteGuidesClosed'/w['id']/name;z.write(p,p.relative_to(ROOT))
        for name in ['worlds-final.json','manifest-final.json','README-final.md','index.html','Source/user-route-sketch.png']:
            z.write(ROOT/name,name)
    with zipfile.ZipFile(archive) as z:
        assert z.testzip() is None
        assert len([p for p in z.namelist() if p.startswith('FinalReferences/') and p.endswith('.png')])==50
    print('PASS:50 unique final PNGs, provenance hashes,10 closed route references,ZIP CRC. Bytes:',archive.stat().st_size)
else:print('Partial gallery:',count,'/50 selected final views; missing',len(missing),'items.')
