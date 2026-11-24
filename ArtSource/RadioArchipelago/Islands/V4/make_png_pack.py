"""A smaller reference-only download; never modifies canonical images."""
from pathlib import Path
import json,zipfile,os
ROOT=Path(__file__).resolve().parent
audit=json.loads((ROOT/'package-audit.json').read_text(encoding='utf8'))
assert audit['pass'] and audit['png_count']==60 and audit['passed_world_count']==10
target=ROOT/'RadioArchipelago_V4_Tripo_PNG.zip'
temp=target.with_suffix('.zip.tmp')
views=('front','right','left','back','top','isometric')
with zipfile.ZipFile(temp,'w',zipfile.ZIP_DEFLATED,compresslevel=6)as z:
    z.writestr('README.txt','Радиоархипелаг V4: 10 островов, 60 отдельных PNG 2048x2048.\n'
        'front — спереди; right — справа; left — слева; back — сзади; top — сверху.\n'
        'isometric — дополнительный объёмный ракурс той же модели.\n'
        'Пять основных видов сняты ортографическими камерами одной 3D-сцены.\n'
        'Исходные BLEND/GLB и скрипты входят в отдельный полный пакет.\n')
    for world in audit['worlds']:
        for view in views:
            p=ROOT/'Output'/world['id']/(view+'.png')
            z.write(p,(Path(world['id'])/p.name).as_posix())
    for name in('all-islands-overview.jpg','before-after_01_10.jpg'):
        z.write(ROOT/'QA'/name,name)
with zipfile.ZipFile(temp)as z:
    assert len([n for n in z.namelist()if n.endswith('.png')])==60
    assert z.testzip()is None
os.replace(temp,target)
print(json.dumps({'pass':True,'pngs':60,'archive':str(target),'mb':round(target.stat().st_size/1024/1024,1)}))
