# Audio credits

All 14 cues in this folder are **self-made by the ASTRA Academy team (P1)**. They were synthesised from sine, square and noise waveforms by [`Source~/make_audio.py`](Source~/make_audio.py) (Python standard library only), with no samples, recordings or third-party packs. They are released as **CC0 1.0** (public domain dedication).

| File | Use | Length |
|---|---|---|
| hover.wav | gaze/mouse hover tick | 0.04 s |
| select.wav | target selected | 0.12 s |
| toggle.wav | switch flipped | 0.03 s |
| button.wav | button pressed | 0.07 s |
| dial_tick.wav | dial detent | 0.01 s |
| grab.wav | tool picked up | 0.11 s |
| place.wav | tool placed in a bay | 0.15 s |
| correct.wav | correct step | 0.33 s |
| wrong.wav | wrong control or step | 0.25 s |
| phase_change.wav | module phase change | 0.48 s |
| alarm.wav | caution alarm (two tones at 1 Hz, never strobing) | 1.0 s |
| success.wav | qualification passed | 1.1 s |
| fail.wav | qualification not passed | 0.65 s |
| ambient_hum.wav | cabin hum (seamless 4 s loop, quiet) | 4.0 s |

**Loudness:** each cue is normalised to −14 dBFS RMS, an approximation of −14 LUFS for short clips. Peaks are limited to −1 dBFS; the shortest clicks peak-limit first and end up 2–5 dB quieter. The hum is −30 dBFS RMS so it stays in the background.

To regenerate: `python3 "Assets/ASTRA/Audio/Source~/make_audio.py" Assets/ASTRA/Audio`. Existing `.meta` files are kept, so GUIDs don't change. Then run **ASTRA → P1 → Create AudioCues asset**.

Add these lines to the repository's root `CREDITS.md`, which P4 owns: "ASTRA Academy UI audio cues: self-made (synthesised), CC0 1.0."
