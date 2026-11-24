# Custom songs and audio chart generation

## Current four-island catalog (2 November2026)

LumiLai, TikoTa, NoriNo and approved RadioTrio are public. The other80 combined records remain hidden, not deleted. RadioTrio is bound to AmberWindsPilot; actual PCM85.76s/3,782,016stereo44100Hzframes, measured72.2891566half-time/144.5783133double-time. Base chart317supports,18holds and346 scoring events; actual terrain-conditioned island has97 moving supports. Full native run20261101-234737-711 passed346/346contacts and20finishsparks, fixed camera, no geometry clips, restored profile/input.

The first Wayo-wa was rejected for insufficient rhythmic energy and remains only in source/collection. Accepted RadioTrio cost8credits; two fourth-island full versions cost16total, latest confirmedbalance1894. No further generation was performed during source refinement/integration. Receipts are in `ArtSource/RadioArchipelago/Music/RadioTrio20261102/`. Full generation (~90s), listening approval before binding, original rhyming invented lyrics and clearly accented family dance groove remain the workflow.


## First island: Lumi-Lai (29 October 2026)

Latest playability revision on29October: the opt-in generator groups10physically infeasible rapid ornaments into continuous native supports, preserving34measured attacks as checkpoints. One connected vocal Hold/ornament transition is joined after cached vocal-continuity and full-cap feasibility checks. Current base/derivative have195supports (175Jump,18Hold),228unchanged scoring events and34runtime HoldPoints. Current island uses1.8lateral scale, three centered first landings/two gradual transitions, fixed0.65m ordinary radius and50safe moving rounds. Full native review20261029-195212-088 passed228/228score contacts and34/34HoldPoints. The220-element/9-Hold counts below describe the initial integration baseline, not the current course. All change reports and evidence: `Docs/Design/RadioArchipelago/LumiLaiPlayability20261029/Review`.

The user approved the invented-language Lumi-Lai song and requested replacing the old trial recording on the current island. The untouched approved MP3 is `Resources/CustomAudio/Music/LumiLai.mp3` (SHA-256 `f37309c134b71fe47e37ca615c088e8bb3513a2a4cc05c47c4ad994ab0d55e0f`),85.551020408seconds of Unity-decoded44100Hz stereo PCM. The Runway full generation cost8credits and left1926; these are the last verified generation-time values, not a live balance. Integration uses local analysis and does not generate another song.

`LumiLaiChartAuthoring.ExportPcm/GenerateBase` runs the existing v12 generator with independently measured vocal/instrument evidence and all current musical options enabled. Result:209Jump,9Hold,0Follow,0BigJump,220elements,228scoring events. The pulse detector selects56.0748BPM (the half-time level of approximately112.15); actual PCM timings and native playback rate remain unchanged. No unsupported gestures were manually inserted.

`LumiLaiIslandJobs.Install` binds the new song to the existing `EmeraldAbyss` definition, generates its own `LumiLaiAbyss.timeline.json` and `LumiLaiHoldGrades.json`, and removes `AlwaysRememberMeowCat` from the active custom catalog. Historical trial audio/maps remain as reproducible authoring sources. The island derivative has71moving ordinary platforms. `IslandAbyssJobs` now regenerates from its configured song instead of always rebuilding the old trial. Source/evidence hashes and exact local reproduction commands are retained in `ArtSource/RadioArchipelago/Music/LumiLai/local-analysis.json`.

The following older generation/balance notes are historical.

User preferences recorded on 2026-10-21:

- Work only in Unity Editor for now. Do not start Windows, Android or other player builds unless the user explicitly requests them again. The first custom-song builds had already completed when this instruction arrived.
- Generate a full track directly in Runway. Do not spend a separate generation on a short demo.
- Let the user listen before integrating a new musical direction into the game.
- After every generation report the charge and the returned remaining credit balance. Do not infer the subscription tier from the balance.
- Keep generated full tracks in the Runway collection. Latest preference: aim for complete new songs around90seconds, with a short intro and a resolved ending. The earlier60–75second target concerned playable edits; cutting an existing recording requires no extra generation.
- For Waka Waka, the user requested the original game's meow timbre as an audio reference. A local8second reference was prepared. The connected music tool has no audio-input parameter; after this was explained the user explicitly chose Runway using a text description only. Do not claim the audio was passed to the generator.
- Current instruction: pause music generation until a new explicit user request. The Waka Waka attempt was rejected by provider moderation;8credits were refunded, verified remaining balance1977. The proposed original-song alternative was not authorized and was not generated.

The approved first preview cost 4 credits, the full Always Remember recording cost 8, leaving 1985 credits. These are historical values, not a live balance.

## Original track length evidence

All 58 recovered Android catalog rows: audio median 57.00 s, mean 56.69 s, range 29.75–118.00 s. 49 recordings are shorter than 75 s and 54 shorter than 90 s. The eight Meow Songs rows have median 64.51 s. Mean time to FinishJump is 54.82 s; the final platform/bonus sequence is separate. The first custom recording is 157.153 s and is deliberately still the full version; the user's shorter-level preference was stated for future tracks.

## Generator

`Dancing Cats > Generate Always Remember Meow Chart` regenerates the first custom chart. To author another imported AudioClip, select an audio asset inside a Resources directory and use `Assets > Dancing Cats > Generate Chart From Selected Audio`. The menu reuses that custom id's catalog number or selects the next available custom catalog number. Original song identities are protected.

Audio must use Decompress On Load with Load In Background disabled. The editor reads actual decoded PCM; runtime requires neither the generator nor an online service. Recovered original resources remain intact. Custom catalog rows are in `Resources/CustomData/songs.json`, maps in `Resources/CustomData/Maps/<id>.timeline.json`. The first song's audio is `Resources/CustomAudio/Music/AlwaysRememberMeowCat.mp3`.

Generation first looks for `Assets/Clone/Editor/MusicalScores/<id>.score.json`. This editable annotation retains measured beat times, estimated notes, phrase evidence and explicit Jump/Hold/Follow/Rest decisions. The source MP3 SHA-256, exact song identity, decoded duration and sample rate must match before compilation. The original author's generator was not recovered; this is our authoring pipeline.

The revised Always Remember score uses a locally computed UVR vocal-source estimate, BasicPitch ONNX notes and independent pYIN pitch evidence. Weak same-pitch transcription fragments merge; supported repeated syllables stay separate. Source separation can leak accompaniment or omit synthetic meows, so these are estimates. This Codex session cannot receive audio directly and did not perform human listening. The three Follow passages are explicit musical/gameplay choices, not automatic proof of legato or glissando. See `Tools/Audio/README.md` for reproduction and retained evidence.

Holds follow stable note intervals rather than periodic overall-energy windows. Two instrumental chord holds are explicitly annotated from polyphonic note evidence in the opening and closing passages. Mesh length is duration times forward speed. Hold checkpoints follow measured beat times, including entry and exit; runtime spawns only later markers to prevent duplicate entry scoring. Outgoing jump distance begins at the hold exit.

Follow compiles into the native moving support (id4/updateType2), stationary scored path markers (id5), and outgoing exit (id6). Pitch contours determine the authored lanes. The player must still drag to the markers; the path does not move the cat automatically. No timer-driven oscillating ordinary jumps are generated. Current Follow passages:36.01–38.18,95.08–97.35 and145.51–147.49 audio seconds. The first two repeat the descending71→69→67→64 pitch pattern.

Without an annotation, the conservative PCM fallback still measures channel energy, transient strength, autocorrelation pulse and phase. Strong peaks win nearby conflicts. It adds no periodic gap filler, energy-only holds or moving jumps. Unsafe unsupported travel is rejected before saving and needs musical annotation. The current score uses .18–1.95-second air intervals and actual measured accompaniment attacks when lead gaps need support; it does not score every beat. Rests add no scoring events.

Time mapping: `gameTime = audioTime + delayTime`, `z = gameTime * forwardSpeed`. Default speed is 6.5 and preroll .6 s. The finish launches onto the original platform five units ahead before audio ends. Generated JSON records source MP3 and decoded-PCM hashes, timing and analysis notes for reproduction.

## Verification

`DancingCats.Clone.Editor.MeowLevelValidation.GenerateAndVerify` regenerates the custom map, tests pulse detection/phase, strong-peak selection, preserved rests, measured Hold checkpoints, native Follow geometry, source identity, catalog collisions and Editor-review preflight, then checks original content. `IntegratedPlayValidation.RunCustomSongInOpenEditor` exercises actual UI input and full-length playback, every Hold checkpoint and Follow marker, pause/resume, finish and persistence. It refuses unsaved scenes, restores prior scenes/selection/progress/input/GameView after returning to Edit Mode, and leaves the user's Editor open. `RunCustomSong` retains batch-child behavior. Release validation must require the custom catalog; an absent optional catalog remains valid for the base game.
# Current music preference — 2 November 2026

For every new song, first choose a recognizable family-oriented viral TikTok listening reference and describe its rhythmic energy/arrangement for an original composition. The user wants a strong clearly accented dance groove, not merely a gentle melodic track. Invented rhyming syllables may combine Lumi-Lai, Tiko-Ta and Nori-No, with new short answering sounds. Do not copy the reference melody or lyrics or claim its audio was supplied when the connected generator accepts text only.

Generate one full song around90seconds, preview before integration, keep rejected full versions in the Runway collection, and report actual credit expenditure/balance for every generation. The first Amber Wayo-wa attempt was rejected for insufficient rhythmic energy (8credits,1902remaining); the user's explicit feedback was a more energetic rhythm with clearer accents. The user-directed full Radio Trio revision was accepted on2November2026 for fourth-island integration (8more credits, confirmed1894remaining;16total). Native duration85.759999seconds and measured72.2891566BPM half-time/144.5783133double-time are independently confirmed; requested144BPM is not written over actual timings. The verified Library candidate may now be published and bound to the fourth island; the old three recordings/maps remain unchanged. Native duration/BPM and integration approval are recorded separately from requested values.

