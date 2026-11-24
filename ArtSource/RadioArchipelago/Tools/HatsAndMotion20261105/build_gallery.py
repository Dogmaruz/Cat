"""Package unchanged native Unity frames as local H.264 previews and a selector."""
from pathlib import Path
import hashlib, json, subprocess, sys

ROOT = Path(__file__).resolve().parents[4]
OUT = ROOT / 'Docs/Design/RadioArchipelago/HatsAndMotion20261105'
NATIVE = OUT / 'Motion/Native'
FFMPEG = ROOT / 'DancingCatsClone/Library/CourierShowcaseTools/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe'

def run(args):
    p = subprocess.run([str(FFMPEG), '-hide_banner', '-loglevel', 'error', '-y', *args], capture_output=True, text=True)
    if p.returncode:
        raise RuntimeError(p.stderr[-3000:])

def build():
    manifest = json.loads((NATIVE / 'manifest.json').read_text(encoding='utf8'))
    assert manifest['status'] == 'complete'
    videos = OUT / 'Videos'; videos.mkdir(exist_ok=True)
    cache_path = videos/'encode-cache.json'
    cache = json.loads(cache_path.read_text()) if cache_path.exists() else {}
    provenance = {'sourceManifestSha256': hashlib.sha256((NATIVE/'manifest.json').read_bytes()).hexdigest(), 'clips': []}
    for clip in manifest['clips']:
        folder = NATIVE / clip['frameFolder']; n = clip['frames']
        assert all((folder/f'frame-{i:04d}.png').is_file() for i in range(n))
        targets = [videos/f"grid--{clip['id']}.mp4", *[videos/f"{h['assetId']}--{clip['id']}.mp4" for h in manifest['heroes']]]
        first = hashlib.sha256((folder/'frame-0000.png').read_bytes()).hexdigest()
        last = hashlib.sha256((folder/f'frame-{n-1:04d}.png').read_bytes()).hexdigest()
        key = hashlib.sha256(json.dumps([first,last,n,manifest['fps'],manifest['heroes']],sort_keys=True).encode()).hexdigest()
        if cache.get(clip['id']) != key or not all(p.is_file() and p.stat().st_size > 1000 for p in targets):
            labels = ['g'] + [f'h{i}' for i in range(len(manifest['heroes']))]
            filters = ['[0:v]split=' + str(len(labels)) + ''.join(f'[{s}]' for s in labels)]
            for i,h in enumerate(manifest['heroes']):
                filters.append(f"[h{i}]crop={h['cropWidth']}:{h['cropHeight']}:{h['cropX']}:{h['cropY']}[v{i}]")
            args = ['-framerate',str(manifest['fps']),'-i',str(folder/'frame-%04d.png'),'-filter_complex_threads','2','-filter_complex',';'.join(filters)]
            for i,target in enumerate(targets):
                args += ['-map', '[g]' if i==0 else f'[v{i-1}]','-an','-c:v','libx264','-preset','veryfast','-crf','20',
                         '-threads:v','1','-pix_fmt','yuv420p','-frames:v',str(n),'-movflags','+faststart',str(target)]
            run(args)
            cache[clip['id']] = key; cache_path.write_text(json.dumps(cache,indent=2))
        provenance['clips'].append({'id':clip['id'],'frames':n,'firstFrameSha256':first,
            'lastFrameSha256':last,
            'files':{p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in targets}})
        print('ENCODED',clip['id'],n,'frames',flush=True)
    for hero in manifest['heroes']:
        listing = videos/f"{hero['assetId']}-concat.txt"
        listing.write_text('\n'.join("file '"+f"{hero['assetId']}--{c['id']}.mp4"+"'" for c in manifest['clips']),encoding='utf8')
        run(['-f','concat','-safe','0','-i',str(listing),'-c','copy','-movflags','+faststart',str(videos/f"{hero['assetId']}--all.mp4")])
    data = {k:manifest[k] for k in ['heroes','clips','fps']}
    data['hats'] = [{'id':i,'label':s} for i,s in [('AmberBeret','Янтарный берет'),('ExplorerHat','Панама исследователя'),('NavigatorCap','Кепка навигатора'),
        ('AviatorCap','Шлем авиатора'),('ObservatoryCap','Шапочка обсерватории'),('SailorBeret','Морской берет'),('SignalBeanie','Шапка радиста'),('SignalDiadem','Диадема сигнала')]]
    template = Path(__file__).with_name('viewer.html').read_text(encoding='utf8')
    (OUT/'index.html').write_text(template.replace('@@DATA@@',json.dumps(data,ensure_ascii=False).replace('<','\\u003c')),encoding='utf8')
    (OUT/'video-provenance.json').write_text(json.dumps(provenance,indent=2),encoding='utf8')
    print('GALLERY',OUT/'index.html',flush=True)

if __name__ == '__main__': build()
