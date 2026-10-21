# Musical chart validation — 2026-10-21

User scope: improve the choice of musical accents and support types. Landing synchronization was already correct. Runtime movement/audio code, forward speed6.5, preroll.6, source recording and58 original songs are unchanged. No player builds or new Runway calls were made.

## Resulting map

AlwaysRememberMeowCat, catalog20001; decoded6930432frames at44100Hz,157.1526530612245seconds. Source MP3 SHA-256:`9e66a28124d6e4c5a261ff1bacc7ba9b5b32324c6b950893db6c92a300e9a509`.

225 musical entries:202Jump,20Hold,3Follow. Native compilation adds15FollowMid,3FollowEnd,Start andFinish:245elements,301events. There are57 Hold checkpoints after their entries. Hold durations are.64–1.5694seconds. Follow spans36.01–38.18,95.08–97.35,145.51–147.49seconds. No oscillating ordinary jumps or periodic fill events are generated.

Independent score review: retained evidence reproduces the score exactly; air intervals.18–1.9419seconds; inter-support lane changes at most1unit. Follow internal intervals are at least.209seconds; maximum lateral speed1.516units/s. Thirty-three air intervals exceed1second, thirteen exceed1.5seconds. No authored musical action begins in the measured near-silent tail.

Two instrumental Holds were explicitly chosen from polyphonic note evidence: closing opening chord12.9993–14.54 and final chord154.1749–155.5. These are not inferred from overall RMS alone. Learned vocal separation, note identities and phrase boundaries remain estimates; confidence is not a calibrated probability. This session could not hear audio directly. Musical acceptance requires listening to the prepared comparison or playing the level.

## Executed checks

- Unity6000.5.4f1, existing interactive Editor: `MeowLevelValidation.GenerateAndVerify` PASS after compilation. Compiler regression fixtures, stronger-onset selection, preserved silence, source/id guards, native Hold/Follow geometry, catalog identity and safe Editor preflight passed.
- Existing original-content checks PASS: native motion/scoring,63 rigs/305 spring bones, progression, tile/theme bindings,618 sprites,58 original maps/catalog rows and previews. Runtime and OriginalData directories have no Git diff.
- Full157.153-second `RunCustomSongInOpenEditor` PASS: actual menu selection/StartBtn input, pause/resume, collider contacts, every Hold checkpoint and every Follow entry/mid/exit, real finish platform, three-star result, best-score persistence and exactly one settlement.
- Interactive review returned to Edit Mode and restored scenes, selection, input and saved progress. GameView cleanup also completed without a restoration failure. Final status:playing=false,compiling=false,sceneDirty=false; original DancingCats scene remains open.
- Python authoring regressions:5/5 PASS, including exact reproduction, real same-pitch reattack/merged fragment, refusal to fabricate silent-tail support, Follow teleport rejection and native finish policy at a changed speed.
- Standalone Mono compiler fixtures PASS, and complete Editor assembly source compilation through bundled Unity Roslyn exit0.
- `git diff --check` PASS.

Two URP warnings about a missing overlay camera were logged around scene/menu setup during the interactive test. There were no gameplay exceptions and the complete contact/finish assertions passed. Camera/runtime configuration was outside this chart change and was not modified.

The comparison package contains old/new30-second cue auditions, diagrams, source/map hashes, Play Mode screenshots and the actual integrated result. Audio markers represent compiled chart events; the source MP3 is unchanged. No claim of musician listening is made.
