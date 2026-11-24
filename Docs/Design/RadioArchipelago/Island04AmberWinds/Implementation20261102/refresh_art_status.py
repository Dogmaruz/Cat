from pathlib import Path
import hashlib,json,shutil
root=Path.cwd(); folder=root/'Docs/Design/RadioArchipelago/Island04AmberWinds/Implementation20261102'
p=folder/'INTEGRATION_HANDOFF.md'; s=p.read_text(encoding='utf8')
s=s.replace('Native finale takeoff at route673.028m, landing679.028m, end713.828m with start8m/scale1.2. Authored final straight begins around657m; final grade source flat from640m.','Native candidate finale takeoff at route675.028m, landing681.028m, end715.828m with start10m/scale1.2. Exact authored final straight begins662m; final grade source is flat from640m.')
s=s.replace('Amber settings: start8m','Amber settings: start10m')
s=s.replace('Wait for source artist\'s frozen FBX/route/heights/rooftop/cloud export hash; do not import provisional source geometry. Final play-height key230m is19m.','DONE: frozen source FBX SHA f50a115a8db22ff5bfa9aa57c071394f48a335ee32c27c904a98ef4f7c6cf714 plus all five sidecars imported. Final play-height key230m is19m.')
s=s.replace('Import only frozen new Amber files into Imported/Islands/AmberWinds, including cloud_steps.json. Keep the three old FBXs/scenes/maps/definitions/music unchanged.','DONE: frozen six-file import, separate new prefab and13 materials prepared. The three old FBXs/scenes/maps/definitions/music remain unchanged. No saved Amber definition or gameplay scene has been created.')
s += '''
## Native scenery-only acceptance (completed after initial handoff)

`PrepareScenery` / `CaptureScenery` now work without any published song, chart, menu entry, saved definition or gameplay scene. They use a transient unsaved definition with empty SongId, Chart=null, actual imported raw route/height profile and source-authored cloud ranges. The3 new Amber Editor files are now installed; existing staged4-menu/presenter/play-review replacements remain outside Assets.

- Source prefab:271 renderers,13 materials,3,000,023 triangles.
- Actual native source terrain:52,700 full-width floor samples,45,697 surface hits, zero terrain violations;33,915 solid body-volume rays, zero structural violations.739,126 terrain/cliff/rock triangles indexed; persistent source FBX/route/prefab GUID anchors verified.
-4 full-size clouds at multiplier1.6: minimum actual drift-reserved gap25.130m, minimum contact clearance.760m, visible volume ratio7.700.
-3 native rotor nodes turned65/50/82 degrees during one second of the shared helper clock; zero-delta pause and rest-pose disposal verified.
-18 native scenery captures in `Library/RadioArchipelago/AmberWinds/SceneryOnly`, including actual fixed-rig portrait views at172/185/200/210/244/272/464/506/542/580/675m. No actor/chart in these images.
- Synchronous offscreen render+readback:17.109ms minimum,74.274ms mean,699.972ms cold maximum across mixed resolutions. ExcludesPNG/file IO. This is an Editor art-preview observation, not gameplay/phone FPS.
- Native main scene remains MoonResonancePilot, clean Edit Mode. Candidate music remains unpublished. Temporal water proof is still separate; still images alone do not establish flow motion.

Visual review: waterfall appears clearly at185/210m and windmill fills the frame at244m; cloud crossings show large isolated volumes. A narrow straight terrain-colored lip at the waterfall drop in portrait210 needs a source-art decision. No source geometry edited during this integration.
'''
p.write_text(s,encoding='utf8')
(folder/'staged-code-hashes.json').write_text(json.dumps({p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in (folder/'UnityStaging').glob('*.cs')},indent=2)+'\n',encoding='utf8')
proof=folder/'NativeScenery'; proof.mkdir(exist_ok=True)
source=root/'DancingCatsClone/Library/RadioArchipelago/AmberWinds'
for name in ('scenery-only-checks.json','scenery-terrain-clearance.json','rooftop-motion-preview.json'):
 shutil.copy2(source/name,proof/name)
for name in ('overview.png','portrait-route-185.png','portrait-route-210.png','portrait-route-244.png','portrait-route-506.png','preview-timings.json'):
 shutil.copy2(source/'SceneryOnly'/name,proof/name)
print('Updated handoff to frozen imported source + StartDistance10 and copied native art proof.')
