# Island phone feedback implementation plan

**Goal:** Correct the overlap, Hold flashes, note readability and touch handling shown in the user's 20-second recording; make the pilot route more energetic while keeping the original audio and timing.

**Scope:** Cat/DancingCatsClone, EmeraldIslandPilot only for route/art changes. Original song/map/scene remain preserved. InkSun-Unity and Druid-C64 are unrelated and must not be edited. Existing APK is a baseline, not a new verified device run.

**Evidence:** `Docs/Design/RadioArchipelago/IslandPilotReview/PhoneFeedback/frame-*.png` and contact sheets from the supplied recording. Current full-song baseline is review `20261027-175025-418`.

- [ ] Touch: verify 24% screen full travel, tap anchoring and immediate reversal at either edge; keep desktop input and elapsed-time smoothing. Scalar tests plus a virtual Touchscreen probe of the real input method.
- [ ] Route: quantify visual overlaps, use native-sized round bodies with separation for rapid subdivisions, preserve logical/music timing, increase world travel to 1.18 and add reachable lateral phrasing. Show native collectible markers without inventing rewards.
- [ ] Hold/VFX: isolate the transverse white flash using actual native mesh/particle data, then correct only its island presentation. Enlarge native note particles selectively; preserve Great/Perfect/streak and collectible emission rules.
- [ ] Integration: compile in a separate Cat Editor, author/check pilot assets, run the full recording with the real input bot, capture Hold/notes/markers and check both route seams, expiry, coins, preferences/input restoration.
- [ ] Delivery: report the actual behavior and limitations. If rebuilding the authorized phone pilot, use a marked Android staging copy and verify package, ABI, signature, one song/scene and required shaders.

The faster route is a visual/world-distance change; the song's BPM and DSP accents are unchanged. All runtime and source changes remain reviewable in the existing working branch.
