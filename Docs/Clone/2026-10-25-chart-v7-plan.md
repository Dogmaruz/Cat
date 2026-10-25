# Audio chart v7: large flights and glide choreography

User contract: improve the independent audio generator against Unstoppable,
write only its separate test map, then regenerate Always Remember Meow from
the same music. Preserve originals and audio; validate in the running Editor,
without player builds.

Approach: extend the existing offline cue pipeline. Large flights require
phrase-boundary evidence rather than classifying every long gap. Follow timing
continues to use measured recurrent phrases; spatial choreography is an explicit
game-design policy, since the same musical phrase has different original routes.
Copying reference cues into inference and changing runtime movement are excluded.

- [x] Preserve hashes of original maps/audio and pre-existing user changes; save
  both prior custom maps outside Assets. Capture v6 metrics in the Editor.
- [x] Add counterexample tests for large flights and smooth Follow paths before
  implementing their rules. Keep reference data in evaluation only.
- [x] Integrate rules, retaining all fourteen Hold and both Follow intervals.
  Compare BigJump precision/recall and both Follow shape metrics with v6.
- [x] Export actual Unity PCM for the custom song, prepare local vocal evidence,
  and regenerate through an explicit audio route which bypasses but preserves
  the existing authored score. Do not generate or replace music.
- [x] Run Editor compile/regressions and full input-driven playthroughs of both
  generated maps. Restore scene/profile state, leave the custom song ready to
  inspect. Record measured improvements and remaining differences honestly.

Additional user steering: every long support now completes an alternating
turn; actual world-space accessory inertia and continual squash/stretch were
implemented and checked on all three imported couriers. No model assets changed.
Full 67.004/157.153-second runs and 9 menu cases passed; see ChartCalibration
and Design/RadioArchipelago/RigReview/MotionV2 for actual Editor evidence.

Review focus: a long gap alone must not create a large jump; dense reattacks and
long supports must remain intact; Follow marker spacing/speed must stay playable;
missing/uncertain voice data must fail conservatively; score priority must not
silently bypass the requested new generation.
