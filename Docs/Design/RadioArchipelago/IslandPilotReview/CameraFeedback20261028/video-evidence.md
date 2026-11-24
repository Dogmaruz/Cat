# Video evidence — camera feedback, 2026-10-28

Read-only review of `C:/Users/Derek/OneDrive/Desktop/video_2026-10-28_11-10-47.mp4`. No Unity runtime, editor code, settings, assets, or bridge were changed. Timestamps below are video playback time from decoded presentation timestamps, not in-game song time.

## Capture

- 375.06 seconds, 590 × 1280 portrait, H.264; 9,309 decoded video frames.
- Container average is approximately 24.85 fps. The reported 120 tbr is a stream timing value, not evidence that gameplay runs at 120 fps.
- `contact-01.jpg` through `contact-05.jpg` give approximately five-second overview samples; their labels use actual source frame timestamps (`sample-times.json`).
- `detail-01.jpg` through `detail-08.jpg` give approximately half-second samples around affected turns; labels include source playback time and sample number (`detail-times.json`).

## Confirmed framing failures

| Video time | Observation | Evidence |
| --- | --- | --- |
| 01:14.42 | Tiko is clipped at the left edge while route pads extend diagonally across the frame. | `key-01m14s-left-clipping.jpg` |
| 01:17.53 | Tiko and the active curved hold strip intersect the left boundary. The courier cannot be read in full during the hold. | `key-01m17s-left-clipping.jpg`; `detail-04.jpg` |
| 02:34.70–02:36.27 | Finish approach swings toward the right boundary. At 02:36.27 the courier and hold strip are cropped on the right. | `key-02m36s-right-clipping.jpg`; `detail-05.jpg` |
| 04:00.67 | Nori and the near route pads are cropped by the right boundary on a turn. | `key-04m00s-right-clipping.jpg`; `detail-06.jpg` |
| 04:40.65 | Almost the whole Nori courier is beyond the right edge; the antenna tip and a small dark silhouette edge remain. Nearby pads are also cropped. The framing starts recovering by 04:41.15. | `key-04m40s-offscreen.jpg`; `detail-07.jpg` |

The evidence shows large lateral excursions of the playable subject. On some turns the curve and portrait viewport consume the horizontal margin before camera framing recovers. This is an observed behavior, not a confirmed camera-code root cause. The recording shows failures on both sides and with two couriers; it is not isolated to one model or one terminal event. Whole character visibility and visibility of several near upcoming pads should be evaluated together rather than framing only the road center.

## Recorded pacing evidence

`capture-timing-summary.json`, `source-frame-timestamps.json`, and `decoded-frame-hashes.txt` contain the measurements.

- Across the recording, the median interval between decoded presentation timestamps is 33.644 ms; the 95th percentile is 66.889 ms and the 99th percentile is 90.977 ms.
- There are 32 timestamp intervals of at least 100 ms, including four of at least 200 ms (two occur in menus).
- During first-run gameplay, one recorded frame persists from 00:46.185 to 00:46.400 (215.145 ms), with additional 125–175 ms gaps concentrated around 00:44–00:47.
- During second-run gameplay, there is a 266.055 ms gap from 05:30.419 to 05:30.685 near the finish, plus a 134.467 ms gap at 05:31.076.
- No two adjacent full-resolution decoded video frames have identical MD5 hashes. That excludes exact repeated decoded images in this file; it does not exclude near-identical frames, irregular motion, or variable-rate capture stalls.

These measurements confirm uneven timing in the delivered recording. They cannot separate Unity frame time, device performance, screen recording load, capture encoding, or later video processing. Engine FPS or a performance root cause requires on-device profiling or runtime frame timing.
