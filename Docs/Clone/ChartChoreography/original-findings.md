# Recovered horizontal placement, 58 maps

Read-only study against `Assets/Resources/OriginalData/Maps/*.timeline.json`.
Run `study.py` with the bundled Python runtime; it writes `study.json` only
under this ignored Library folder and verifies all 58 source-map hashes.

7315 logical playable supports produce 7257 chronological transitions.
2259 (31.1%) preserve their landing/exit lane. Median absolute movement is .5
world units, the 75th percentile is 1.0. The original is not an always-alternate
left/right sequence. Of 5863 stationary ordinary/large/held supports, 93.4% use
the five main positions 0,+/-.5,+/-1; 99.6% are on the .25-unit grid.

| Free travel interval after prior support | Pairs | Same lane | Median abs dx | 95th abs dx |
| --- | ---: | ---: | ---: | ---: |
| under .18 s | 1247 | 531 | .25 | .5 |
| .18-.30 s | 2857 | 1182 | .5 | 1 |
| .30-.50 s | 2131 | 410 | 1 | 2 |
| .50-.80 s | 798 | 98 | 1 | 2 |
| at least .80 s | 224 | 38 | 1 | 2 |

Unstoppable054 is more conservative: all 35 transitions between .18-.30 s
and all six under .18 s preserve X. Its .30-.50 s median movement is .5;
its .50-.80 s median is 1. Twinkle instead uses short .25/.5-unit staircase
steps in dense subdivisions. LABUBU preserves X on 36/46 of its under-.18 s
transitions. Thus both repeated-lane groups and short staircase patterns are
supported, with larger turns after enough travel time.

48/58 maps have true moving ordinary/large tiles. All elements together have
1926 updateType3 entries: 995 Jump, 253 BigJump, 608 FollowMids, 68 Theme,
2 FinishJump. All 820 Holds are stationary. Follow entries use updateType2
(rider-follow); they are distinct from moving Jump/BigJump targets.
There are 458 chronological contiguous runs of moving playable supports:
median length2, 75th percentile3, 90th6, maximum21. 362 runs keep one msLeft
flag; 96 mix flags. All moving elements: msLeft1=821, msLeft0=1105.
Unstoppable054 and Unity054 have no true moving tiles; later UnityPop has9.

Runtime contract: `OriginalTile.Tick` delegates updateType3 to
`NativeRhythmRules.MoveCycleX`:

`x = sin(2*pi*(rawX/5 + (msLeft != 0 ? -ahead : ahead)/30))`

`ahead = element.pos.z - playerZ`. Thus rawX is a phase parameter for this
mode, not the desired landing lane. At contact, x=sin(2*pi*rawX/5).
For target x in [-1,1], a principal phase is rawX=asin(x)*5/(2*pi).
The original mostly uses raw0,+/-.5,+/-1. Raw+/-1 lands at about+/-.951.

Name/sign trap: at rawX0, as playerZ advances, msLeft0 moves toward world
left, while msLeft1 moves toward world right. Sample two increasing clock
times rather than inferring the visible direction from the field name.
The cycle amplitude is1 and its world-distance period30; its time period
is30/forwardSpeed.

Existing custom compiler produces all ordinary/large cues at lane0 and never
sets updateType3. Its current geometry validator also compares raw x with a
constant <=1 transition bound, which is not adequate to judge phase-encoded
moving targets or original wide crosses. A separate route compositor can
preserve acoustic times, cue types, support duration and accepted Follow paths,
then apply small repeated/staircase motifs, larger timed turns, and coherent
short runs of moving Jump/BigJump targets. Test actual landing and exit lanes,
and use a conservative time-aware reach envelope. No per-song coordinate list
or original event times need enter inference.

Source points: `Assets/Clone/Editor/AudioChartGenerator.cs` ordinary/Big cue
construction; `MusicalChartCompiler.cs` maps cue.lane directly to elements;
`Assets/Clone/Runtime/OriginalTile.cs` and `NativeRhythmRules.cs` define playback.
`AudioChartGeneratorValidation.cs` currently has the fixed raw-x delta check.
