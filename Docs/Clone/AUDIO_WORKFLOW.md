# Custom songs and audio chart generation

User preferences recorded on 2026-10-21:

- Work only in Unity Editor for now. Do not start Windows, Android or other player builds unless the user explicitly requests them again. The first custom-song builds had already completed when this instruction arrived.
- Generate a full track directly in Runway. Do not spend a separate generation on a short demo.
- Let the user listen before integrating a new musical direction into the game.
- After every generation report the charge and the returned remaining credit balance. Do not infer the subscription tier from the balance.
- Keep generated full tracks in the Runway collection. Aim for 60–75 seconds in future game edits; cutting an existing recording requires no extra generation.

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
