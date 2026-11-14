# Proposal: Google Cardboard XR Plugin for the Irusu phone build (TASKS S1 step 1a)

**From:** P1 XR & Interaction · **To:** the team lead, for approval · **Status:** proposed. No changes to `Packages/` or `ProjectSettings/` have been made.

## Recommendation
Approve the plugin install on a branch and test it on the Irusu phone. Until then, and if the test fails, ship the **gyro "magic window" fallback**, which is already implemented in `Assets/ASTRA/XR/Phone` and needs no package change. Gaze, tap, dwell and recenter work the same on both routes, so levels don't change.

## What the plugin needs (from its README and quickstart, read 2026-11-14)
| Item | Requirement | Our project (main) |
|---|---|---|
| Unity | Quickstart: "Unity 6000.0.23f1 or later"; README: 6000.0.58f2 or later | 6000.3.28f1: newer than both, but 6.3 is **not tested** by Google |
| Install | Package Manager → Add from git URL `https://github.com/googlevr/cardboard-xr-plugin.git`; `git` must be on PATH | adds a `Packages/manifest.json` entry; pin a release tag, not `main` |
| XR | XR Plug-in Management → Android → **Cardboard XR Plugin** | `com.unity.xr.management` 4.6.1 is already present |
| Android min / target API | 26 (Android 8.0) / 35 | to set in Player settings |
| Scripting / arch | IL2CPP; ARMv7 and/or ARM64 | IL2CPP + ARM64 |
| Graphics API | OpenGLES3 and/or Vulkan (GLES2 is gone in 2023.1+). With Vulkan: clear "Apply display rotation during rendering" and use ETC2 textures | start with **GLES3 only** (fewest surprises), then try Vulkan |
| Orientation | Landscape Left (or Right) | Landscape Left |
| Frame pacing | Optimized Frame Pacing **off** | to set |
| Input | Active Input Handling = Input System | already Input System (`activeInputHandler: 1`) |
| Gradle | Custom Main Gradle Template (dependencies: `androidx.appcompat:appcompat:1.6.1`, `com.google.android.gms:play-services-vision:20.1.3`, `com.google.android.material:material:1.12.0`, `com.google.protobuf:protobuf-javalite:3.19.4`) and Custom Gradle Properties Template (`android.useAndroidX=true`, `android.enableJetifier=true`) | new files under `Assets/Plugins/Android/` |
| Internet access | "Require" (QR code scan for viewer parameters) | to set |
| Entry point | Activity (clear GameActivity) | to set |
| URP | Not mentioned in the quickstart | **Risk:** verify eye rendering and post-processing with `Mobile_RPAsset`; turn off post-processing on the phone if needed |

## Risks and fallback trigger
- **6.3 compatibility is unverified.** If the plugin fails to import, build or render on 6000.3.28f1 by the 02:00 cut-off, stop and ship the fallback.
- **URP + Cardboard distortion pass:** if the image is black, or shows as one eye, try GLES3 only, MSAA off and post-processing off, then fall back.
- The **Irusu viewer profile** needs a Cardboard QR code. Without one, the plugin uses a default profile, and the lens distortion may be slightly off.

## Fallback (already built, no package change)
- `PhoneHeadPose`: Input System `AttitudeSensor` rotation. The correction is serialized, so landscape-left can be tuned on the phone.
- `PhoneStereo`: split-screen eyes, 63 mm IPD, no distortion correction.
- Same `GazeInteractor`, reticle, tap, dwell and recenter.
- Fallback Player settings: Android, IL2CPP, ARM64, Landscape Left, GLES3, Input System. No Gradle templates are needed.

## Verification still required (not done; no phone or Android build support on this machine)
1. An APK installs on the phone.
2. L3 renders in stereo with head tracking.
3. A full L3 run reaches the Debrief using only gaze and tap.
4. Record fps (target ≥ 50) and comfort notes in the stage report.
