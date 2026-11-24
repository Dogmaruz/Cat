from pathlib import Path
import hashlib,json
root=Path.cwd()
folder=root/'Docs/Design/RadioArchipelago/Island04AmberWinds/Implementation20261102'
stage=folder/'UnityStaging'
files={p.name:hashlib.sha256(p.read_bytes()).hexdigest() for p in stage.glob('*.cs')}
(folder/'staged-code-hashes.json').write_text(json.dumps(files,indent=2)+'\n',encoding='utf8')
text='''# Fourth-island integration handoff — 2026-11-02

Status: RadioTrio audio has been previewed to the user by the root agent; acceptance is still pending as of this handoff. Candidate-only preparation is authorized. No source catalog row, published base chart, menu entry, scene or song binding has been created by this task yet.

## Candidate source and native evidence

- Source: `ArtSource/RadioArchipelago/Music/RadioTrio20261102/RadioTrio-full.mp3`.
- Source SHA-256: `b3a7249d3f28984bf190d6498a2cadee7335d3bf61f6c22679e69e911d8759f6`.
- Native Unity PCM SHA-256: `e238c81e97f83bd2e7c1889eb1f377769161dcb1f57fa96b04f1b8e08715c50e`.
- Native duration85.76s, 3,782,016 frames,44,100Hz stereo.
- Local voice and mixed-note provenance verified in `DancingCatsClone/Library/RadioTrioChart/Audio/analysis.json`; no audio upload or paid generation during this analysis.
- Candidate-only native generation PASS in `Library/RadioTrioChart/generation.json`, chart `candidate.json`, SHA `dc7c29d728796b86cf212826c190c7f3dc6d371e6c10c2f7ee99eeba1b45d34c`.
-317 supports,18 holds(16 short),1 big jump,346 scoring events; all event samples retained during grouping. Pulse estimate72.2891566 BPM corresponds to144.5783133 double-time; requested144 is informational, never written over measured timestamps.
- Native finale takeoff at route673.028m, landing679.028m, end713.828m with start8m/scale1.2. Authored final straight begins around657m; final grade source flat from640m.

## Already imported

Only `RadioTrioChartAuthoring.cs`, new `RadioTrio.mp3`, new hash-addressed voice/notes evidence, and two minimal allowlisted queue cases (`radio-trio-pcm`, `radio-trio-candidate`). `GenerateBase` writes Library candidate/report only. It never publishes the base chart or edits the catalog. `PublishApproved` is a separate explicit action and has no queue case yet.

The412-file baseline in `Before/preserved-existing.json` still matched byte-for-byte after candidate generation. `songs.json` contains no RadioTrio row and the published RadioTrio chart path does not exist.

## Staged Unity integration

Ten C# files in `UnityStaging`. New AmberWindsIslandJobs/AmberWindsRouteChecks/AmberWindsTerrainChecks go to RadioArchipelago/Editor. RadioTrioChartAuthoring is already installed. Existing IslandPilotPlayReview goes to Clone/Editor; RadioIslandMenuJobs and RadioIslandMenuLiveChecks go to RadioArchipelago/Editor; RadioIslandMenuPolicy, RadioIslandMenuCatalog, RadioMenuPresenter go to RadioArchipelago/Runtime/UI.

The only presenter change is count-aware Atlas card spacing, preserving the three-card original layout and fitting four cards above the footer. The live menu proof checks actual card anchors for overlap in addition to real callbacks/all four stage bindings/hidden catalog retention/preferences restoration.

Amber settings: start8m, distance1.2, lateral1.8, uniform full-size supports, unified long tiles, close fixed-distance camera, jump1.65, no glow, ground finish, source-authored cloud windows and CloudBankScaleMultiplier1.6. New Amber native checks reuse strict transformed cloud-triangle separation/slope/volume proof; actual geometry must pass rather than shrinking clouds. Windmills use existing scene-owned IslandRooftopMotion.

Amber water materials explicitly use shared flowing-water shader: Stream UseFlowUV1/current4.2/FlowSpeed.65; pools UseFlowUV0/current2.1/FlowSpeed.55; waterfalls Fall1/FlowSpeed1.3/transparent alpha. No old material rewrites.

## Remaining root-coordinated sequence

1. Obtain user acceptance before PublishApproved, menu integration or scene/song binding. Rejected WayoWa remains unused.
2. Wait for source artist's frozen FBX/route/heights/rooftop/cloud export hash; do not import provisional source geometry. Final play-height key230m is19m.
3. Import only frozen new Amber files into Imported/Islands/AmberWinds, including cloud_steps.json. Keep the three old FBXs/scenes/maps/definitions/music unchanged.
4. Apply the ten staged code files and add explicit native actions for publish/create/check/open/capture/play. Root owns two old Coral/Moon catalog guards to permit exactly one RadioTrio addition.
5. Explicitly PublishApproved, then AmberWindsIslandJobs.Prepare. Prepare builds the actual terrain-conditioned chart and new prefab/materials/definition/scene, prepares fourth menu, runs native checks and captures views.
6. Fix any actual native geometry/hold/terrain/cloud failures at source or in new-level data. Never loosen old guards or shrink supports to conceal defects.
7. Native four-entry menu checks and full unsimplified BeginAmberWinds play-review. Inspect fresh terrain/structure/finale/cloud reports and actual gameplay images/water/sail motion.
8. Verify baseline preservation, no unexpected settings changes, correct MP3/source hashes, clean Editor state. No Player builds, commits or pushes.
'''
(folder/'INTEGRATION_HANDOFF.md').write_text(text,encoding='utf8')
print('Handoff written; staged C# files',len(files))
