# Musical chart revision

The user clarified that landing synchronization is already correct. Change musical decisions in the chart and its authoring tools; preserve runtime movement, audio clocks, speed and preroll. Work in the existing Unity Editor only. No player builds or new Runway generation.

The old PCM generator selected the first eligible transient, inserted a fixed gap pattern, and inferred long platforms from overall energy. Replace those rules with strength-ranked attacks and an explicit, source-hashed musical score. The score records selected attacks, stable notes, connected melodic contours, phrase evidence and deliberate rests. Holds use measured beat checkpoints. Follow uses the recovered moving support and stationary path markers, without periodic oscillation.

1. Establish the old chart's failure mechanisms and preserve a comparison copy outside the repository.
2. Measure pulse and repetition, transcribe note hypotheses, and compare a locally separated vocal estimate with the full recording. Source separation and transcription are estimates; this session cannot hear audio directly.
3. Author a complete playable score. Keep real note reattacks separate. Choose a few connected contours for Follow, stable sustained notes for Hold, and actual instrumental attacks where the lead is absent. Do not fill every beat or force syncopation onto a grid.
4. Compile the score into the existing timeline schema, validate native support/exit geometry and source identity, and preserve the 58 original songs.
5. Validate compilation and a complete input-driven playthrough in the open Editor. Preserve the user's scenes, selection and progress. Prepare a short annotated audio comparison for the user to judge musical placement.

Acceptance evidence: regression cases for stronger attacks, rests, measured Hold checkpoints, native Follow, source identity and Editor preflight; actual custom-map validation; full-song Play Mode result with all sustained checkpoints and Follow markers; repeatable source/score hashes. Autoplay confirms mechanics, not musical taste. Listening acceptance remains with the user.
