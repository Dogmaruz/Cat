# Timing investigation, V12

This investigation keeps chart timestamps, melody selection, landing geometry,
animation phase, software sound submission and audible output latency separate.
No physical sound latency or original-app audio-perceptual equality is measured.

## Confirmed reconstruction difference

The shipped original's `data.unity3d` has exactly one AudioManager, in
`globalgamemanagers`, class ID11, path ID4. Both `m_DSPBufferSize` and
`m_RequestedDSPBufferSize` are512. The clone has1024/requested0 and actually
used1024 frames x4 at48kHz before the comparison. The original bundle SHA256 is
`c9ea2dbcb15904cdb9ab4cca67ac5b8f62c84cf7adf079c5be041608c4bedb60`.
`read_original_audio_settings.py` reads the packaged data with the already
installed UnityPy1.25.4; `original-shipped-audio-manager.json` preserves the
literal serialized fields. The untouched AssetRipper export agrees.

## What the runtime pair establishes

Full real-pointer playthroughs of recovered Jingle and generated JingleV11
passed, including pause/resume, actual colliders, finish, retry and cleanup.
The first aborted original-selection run is retained separately and contributes
no timing evidence. Successful traces have287 and132 scored contacts.

Ordinary Jump contact occurs median5.74ms after its nominal chart position on
the recovered original map, and6.08ms on V11; maxima10.63/10.58ms. Thus these
runs do not show a new generated-map global landing delay. Callback timing must
be compared by kind: Follow midpoints/end collide about69–74ms early in the
recovered map, and Hold checkpoints about20.5ms early. Averaging these together
would fabricate an apparent original advantage.

The active settings have rhythmLineHeight0 and calibrationOffsetSeconds0.
The new UI edits rhythmLineHeight correctly. At rate1 and zero user offset,
`gameTime=audioTime+delayTime`; music starts at `dspStart+delayTime`. The preroll
therefore cancels. Moving the0.6s preroll or shifting all timestamps earlier is
not supported by this evidence. Across the original trace, music timeSamples
track the DSP-derived audio coordinate within0.03ms; the comparison has no
observed accumulating clock drift.

The V11 real `Resources.Load` call for the hit effect costs median0.0084ms,
max0.2844ms in this run. The hit WAV is1s mono44.1kHz; its first -40dB amplitude
crossing is5.986ms and its peak12.676ms, so it has no large leading silent gap.
These waveform crossings are not a measurement of perceived onset.

## Controlled512 experiment

Changing only live AudioConfiguration.dspBufferSize from1024 to512 was accepted
and512x4 at48kHz was independently verified during Play Mode. Same V11 map,
same132 actual contacts, same ordinary contact median6.08ms. Ordinary callback
submission median improved11.86→8.65ms; the smallest observed advancing DSP
clock step changed21.333→10.667ms. Render frame medians remained16.67ms. The
exact paired132 contacts include43 callbacks10.667ms earlier,69 unchanged,
and20 callbacks10.667ms later; paired mean is1.86ms earlier and paired median0.
Thus the distribution-median change is not a uniform per-hit latency gain.
processing block itself is halved, which is a confirmed configuration and
software quantization improvement. It does not establish a physical audible
latency reduction of a particular number of milliseconds.

The first comparison report omitted its nested AudioConfiguration fields;
its software contact measurements remain valid. A concern that the separate
SessionState snapshot had also become`{}` was disproved by the real Unity
serialization check: direct root serialization contains all five fields, and
the normal exact restoration path succeeded without invoking legacy recovery.
The explicit-field DTO repairs nested reports, and the Serializable pose
repairs the missing pose payload. The initial512 trace has no pose fields and
must not be cited as a visual measurement. A separate clean comparison capture
and direct Unity serialization checks verify the corrected diagnostic output.

## Visual question

The landing impulse is applied directly after bodyStretch smoothing. Its
mathematical kernel peaks near35ms of internal landing age, but LateUpdate
advances that age by the entire current frame immediately. That internal age
cannot be called a35ms wall-clock delay, and body filtering cannot simply be
added to produce an invented60–100ms visual lag. The clean comparison records
rendered scaleYRatio, actual landing age, impulse, bodyStretch, feet and bob;
its analysis uses render wall time relative to the true scored contact.

The recovered cat Jump clip begins scale1, reaches y1.1 at83.33ms after launch,
and y0.9 at183.33ms. It resets at launch; this clip is not proof of a separate
contact-triggered impulse. Native observations are from saved decompilation,
not a live original-app rendering capture.

The corrected capture contains132 real scored contacts,132 effect submits and
127 eligible landing windows with serialized poses. The rendered minimum body
scale occurs median2.09ms after real contact (p90 18.41ms, maximum26.86ms);
the impulse maximum occurs median18.27ms (p95 19.08ms, maximum26.86ms).
This does not support a60–100ms late visual squash in the measured run. It does
not measure pixel-level sole contact or original-app perception. Ordinary
collision remains median6.08ms after nominal position. The second512 run's
ordinary callback median10.05ms, versus11.86ms at1024, further demonstrates
frame-phase variation rather than a uniform gain.

## Scope and remaining verification

No source audio, original map, generated chart time, calibration preference,
model or build was modified by this timing subtask. The parent owns any eventual
AudioManager change and Unity queue. A smaller serialized buffer matching the
original is justified independently; chart-wide advance, extra compensation and
animation retiming require additional evidence. Real output-device latency and
user listening acceptance remain unmeasured.
