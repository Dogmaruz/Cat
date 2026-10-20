# Dancing Cat offline recreation

## Current request

The user replaced the initial repair request: leave the old game as an example and create a new playable implementation inside the same project, recreating Dancing Cats gameplay and menus. They explicitly selected all gameplay systems offline, without advertisements or real purchases. Do not resume repairing the old MovementController or RhythmGenerator.

New code/data/scenes live under `The Cat/Assets/DancingCat/`. The main scene is `Scenes/DancingCat_Preview.unity`; five independent song assets are under `Songs/`. Existing project songs/models are reused. No original game code, map content, music or artwork is incorporated.

## Reference evidence

Downloaded standard Dancing Cats 0.5.8, package `com.rhythmdance.taptile.dancingcats`, build113. XAPK SHA256 `f304e12ed6881b84d3d83daa6be3db0e6cc7c478c97730ee126ff91240fbdd83`. Base/split APK signatures verified with Android SDK; certificate matches public APKPure/Uptodown listings. REA archive inventory and static Unity serialized-object inspection were used; the Android application has not been run.

Confirmed: five bundled authored MapData assets reference SonicBloom Koreography data; explicit tile positions/types, per-song speed and delay; Jump/BigJump/Hold/Follow/path points/finish; musical event positions in audio samples; integer payload commands; damped SpringBone tails; separate Idle/Jump/Hold/End states; normal/endless/difficulty, local economy, roles/trails, daily/challenge rewards, dice and four-room house panels.

For inspected events: `z = (startSample / sampleRate + delayTime) * forwardSpeed`. A representative event at sample2425, frequency44100, delay0.4450113475, speed7.5 gives z3.75. Holds group several events. The shipped map is a serialized Unity object inside an AssetBundle; analysis JSON is an export, not evidence that the original source format was JSON.

Koreographer is public: <https://www.koreographer.com/koreographer>. Pro advertises MIDI conversion and audio analysis: <https://www.koreographer.com/buy>. The exact Cobby map converter/editor, and whether Cobby authored MIDI, clicked events by hand or used automated analysis, remain unproven. No public original generator was found in the bounded search. Do not turn this into a claim that no private generator exists.

## Implemented design

- CatSong holds the audio clip, beat offset, time-based notes, sustained checkpoints and follow path points. Analyzer suggestions are deterministic and saved before gameplay. They require listening/manual musical review and are not the recovered original charting algorithm.
- CatRunState consumes a monotonic song clock, evaluates crossed landings and support/path checkpoints, scores each once and handles failure/revival/finish. Rendering and physics do not own these decisions.
- CatGameController schedules audio using DSP time; input is actual mouse/touch drag or keyboard arrows/A-D. It owns menu/play/pause/failure/result transitions, wallet, song/cat unlocks, local normal/endless difficulty and latency options.
- CatVisualMotion uses explicit rig animation states and bounded damped spring secondary motion. Tail motion never changes the gameplay position.
- CatGameUI/CatExtraUI expose songs/cats/daily rewards, settings, HUD, retry/revive/results, challenge, house upgrades, trails, dice and play options. Advertisements/network transactions are replaced by explicit local coin actions.

Original formulas for stars/combo, exact camera/turn curves, reward prices and every live-service feature remain unknown. This implementation uses documented local rules; it is not a claim of pixel-identical or source-identical reconstruction. Five models and five local songs form the test content, rather than the original catalog of58 songs/63 roles.

## Validation

Editor entry `DancingCat.Editor.CatPreviewBuilder.ValidateAndPrepare` runs simulation, synthetic audio/chart, revived reward, five actual rig/presentation and local progression checks, then regenerates preview content. `CatPlayValidation.Run` invokes actual UI button callbacks and steers using Input System events in the actual scene; the callbacks bypass pointer/raycast routing. It never corrects gameplay Y/Z/state. A separate development-player probe validates pointer routing through the EventSystem. Reports include landing identities, scoring, clock drift, tail transforms, errors and saved-preference restoration. Unity compilation is not gameplay proof.

Run the new scene for human playtests. Do not open/save all legacy scenes or upgrade packages. No push, publish or store integration is authorized by this prototype task.
