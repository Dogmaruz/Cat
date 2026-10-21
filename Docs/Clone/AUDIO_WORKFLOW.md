# Custom songs and audio chart generation

User preferences recorded on 2026-10-21:

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

Analysis uses channel energy in 20 ms windows, 10 ms hops, positive RMS changes, autocorrelation over 55–180 BPM and measured onset phase. It selects actual accents for landings and fills long gaps with beat scaffolding. Outgoing jumps last .32–1.0 seconds. Lanes follow a deterministic repeating beat phrase. This is not vocal separation, pitch transcription, or automatic chorus recognition.

Sustained energy with relatively few transients can form a .6–2.1 second hold. This heuristic can also respond to accompaniment; audition and play the result. Long mesh length is duration times forward speed. Hold checkpoints include the start marker, while runtime spawns only the later markers to prevent duplicate entry scoring. Outgoing jump distance begins at the hold exit.

Sparse moving ordinary tiles require at least .6 seconds for both arrival and departure, start after 18 seconds, and are spaced at least 24 seconds apart. Their native movement phase places them in the center at the planned landing. They are not currently selected by semantic chorus detection.

Time mapping: `gameTime = audioTime + delayTime`, `z = gameTime * forwardSpeed`. Default speed is 6.5 and preroll .6 s. The finish launches onto the original platform five units ahead before audio ends. Generated JSON records source MP3 and decoded-PCM hashes, timing and analysis notes for reproduction.

## Verification

`DancingCats.Clone.Editor.MeowLevelValidation.GenerateAndVerify` regenerates the custom map, tests pulse detection/phase, deterministic output, silence/invalid options, safe geometry and catalog collisions, then checks the original content. `IntegratedPlayValidation.RunCustomSong` exercises actual UI input and full-length playback, holds, moving tiles, pause/resume, finish and persistence. Release validation must require the custom catalog; an absent optional catalog remains valid for the base game.
