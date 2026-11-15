# Space Apps 2026 project form: ready-to-paste answers

Challenge: **Build a Junior Astronaut Mission Trainer**. Team: Dhruv Sharma, Rayyan Shaikh, Vyom Vyas, Arnav Tawar.

---

## Project Name

ASTRA Academy: Hands-On Astronaut Procedure Training in XR

## Summary (≈170 words)

ASTRA Academy is a hands-on astronaut procedure trainer in XR. In a low-cost (~₹1,000) VR headset, or in passthrough AR with hand-pinch pointing, learners sit inside a fictional space-station cabin, with NASA Blue Marble, Black Marble and Deep Star Maps imagery outside the window, and complete eight short missions by looking and tapping (or pinching): from safety and calibration to stowing tools, configuring the cabin, clearing a power-bus fault, EVA tethering, capturing cargo with a robotic arm, and a final mission that restores station power.

Every level follows the loop crews train with: Briefing, Demonstration, Practice, Qualification and Debrief. A deterministic scorer gives a 0–100 score with Gold/Silver/Bronze and explains every deduction. An AI flight instructor (Gemini, via a rate-limited proxy) answers questions during Practice but can never change a score, and an offline coach takes over without a network.

Most space-learning apps are passive. XR lets students practise doing the task, and ASTRA tells them exactly why they passed or failed, without expensive hardware. New missions are just two JSON files.

## Project Demonstration

Upload `ASTRA-Academy-SpaceApps-2026-Final.pptx` (7 slides) to Google Drive, set sharing to **Anyone with the link → Viewer**, and paste that link. Check it in a private window.

## Project

https://github.com/dhruvsharma06-web/nasa-space-apps

(Play in browser: https://dhruvsharma06-web.github.io/nasa-space-apps/; put it in the details below too.)

---

## Project Details

**Play now (no install):** https://dhruvsharma06-web.github.io/nasa-space-apps/
**Source code:** https://github.com/dhruvsharma06-web/nasa-space-apps

**What it does**
ASTRA Academy puts a trainee in a fictional space-station cabin, on an Earth-facing orbit drawn from NASA imagery, and teaches spacecraft procedures by doing them. There are eight levels of 2–5 minutes each:
- L0 Safety & Calibration: seat posture, recenter, pause.
- L1 Cadet Orientation: look, aim, select; finding the panels.
- L2 Equipment Handling: stow each tool in its bay.
- L3 Cabin Familiarization: set up lights, air and comms.
- L4 Emergency Response: clear a power-bus fault safely.
- L5 EVA Preparation: tether and tool check.
- L6 Robotics: capture a free-floating cargo module with the arm.
- L7 Restore Station Power: an integrated final mission using every skill.

**How it works**
Every level runs the same five phases crews train with:
1. Briefing: the mission goal.
2. Demonstration: the controls light up in order.
3. Practice: the next control glows, hints appear after mistakes, and the trainee can ask the AI flight instructor about the current step (typed or by voice in the browser).
4. Qualification: scored, with no help.
5. Debrief: score, medal and an explanation of every deduction.

Scoring is deterministic and explainable: start at 100; −25 per missed critical step, −10 per out-of-order step, −8 per wrong control, −1 per 5 s over par (max −15), +5 for a fast run with no mistakes. Gold ≥ 90, Silver ≥ 75, Bronze for any pass. You only pass if every critical step is done. The same actions always give the same score.

The AI instructor (Google Gemini via our own FastAPI proxy hosted on Render) is advisory only. It is grounded in the mission file, rate-limited, refuses to reveal Qualification answers and never changes a score or the simulation. With no network, an offline rule-based coach answers instead. The API key never reaches the device.

Each attempt writes a local session log. An offline instructor dashboard reads those logs and shows where trainees struggle.

**Built to scale:** a new procedure is just two JSON files (a mission file with steps and hints, and a layout file with panel and control positions). Our Unity level factory generates the 3D cabin, controls and UI from them, so instructors can add training without game-engine work.

**XR experience**
- VR: a low-cost (~₹1,000) Cardboard-style headset with 3DoF head tracking, a gaze reticle and tap; seated, with no artificial movement (tested on real hardware at 30 fps).
- Passthrough AR and VR on an iPhone headset (Vision Aura build): head tracking and hand-pinch pointing; the virtual cabin can be hidden so controls appear in your real room.
- A web demo (WebGL, mouse and click) lets anyone try every level with no headset or install.

**Benefits and intended impact**
- Access: hands-on XR procedure practice for schools, outreach programmes and remote learners, using a ~₹1,000 headset instead of simulators or headsets costing lakhs of rupees.
- Active learning: learners prove they can do the task, not just watch it.
- Fair feedback: every point lost is explained, and AI never decides the grade.
- Next step for the challenge: adapt language and controls for ages 7–12, and add Moon/Mars outpost trade-offs (power, life support, food, delayed comms) driven by NASA data.

**Tools and technologies**
Unity 6.3 LTS (C#), Universal Render Pipeline, Input System, XR Interaction Toolkit, XR Plug-in Management; Python with FastAPI, Uvicorn, Pydantic and pytest for the coach proxy; Google Gemini API (gemini-3.5-flash-lite); JavaScript for the scoring mirror, level simulator and instructor dashboard; GitHub Actions (CI) and GitHub Pages (web build); Render (proxy hosting). Hardware: Cardboard-style VR headset; iPhone XR headset (VR and passthrough AR) via a Mac host.

**Quality**
The core scoring code is plain C# with no engine references, mirrored in JavaScript and cross-checked against shared fixtures. 300+ Unity EditMode and PlayMode tests, including a first-time-user playthrough of all 8 levels. On every pull request, CI runs the content validator, 29 scoring tests, 13 dashboard tests, the coach-proxy tests and a secret scan.

**Safety and honesty**
The ASTRA Station and every procedure are fictional. This is not astronaut certification or a real flight procedure. No accounts, names, camera or voice data are stored; session logs stay on the device. ASTRA Academy is not affiliated with or endorsed by NASA and uses no NASA logos.

## Use of Super Intelligence (SI)

Select: **Code**, **Text** (and **Other / AI feature in the product** if that option exists). Do not select images, video or audio unless a teammate used SI to make them.

Suggested wording if a description box appears:
> We used AI coding assistants to help write and review code, tests and documentation; every change was reviewed and tested by the team. The in-app AI flight instructor uses Google Gemini at runtime through our proxy; its answers are labelled "AI coach (advisory)" and it never affects scores. No SI-generated images, video or audio are used; the cloud layer is generated by our own procedural code.

## NASA Data

| Link Text | Link Url |
|---|---|
| NASA Earth Observatory – Blue Marble: Next Generation (Topography & Bathymetry, July 2004) | https://science.nasa.gov/earth/earth-observatory/blue-marble-next-generation/base-topography-bathymetry/ |
| NASA Earth Observatory – Black Marble 2016 (Earth at Night maps) | https://science.nasa.gov/earth/earth-observatory/earth-at-night/maps/ |
| NASA/Goddard Scientific Visualization Studio – Deep Star Maps 2020 | https://svs.gsfc.nasa.gov/4851 |
| NASA Images and Media Usage Guidelines | https://www.nasa.gov/nasa-brand-center/images-and-media/ |

## Space Agency Partner & Other Data

| Link Text | Link Url |
|---|---|
| ESA Gaia DR2 (star catalog used in Deep Star Maps) | https://www.cosmos.esa.int/web/gaia/dr2 |
| Unity 6.3 LTS (engine) | https://unity.com |
| Unity Universal Render Pipeline | https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.3/manual/index.html |
| Unity XR Interaction Toolkit | https://docs.unity3d.com/Packages/com.unity.xr.interaction.toolkit@3.3/manual/index.html |
| Unity Input System | https://docs.unity3d.com/Packages/com.unity.inputsystem@1.20/manual/index.html |
| Google Gemini API | https://ai.google.dev/gemini-api |
| Google Gen AI SDK for Python | https://github.com/googleapis/python-genai |
| FastAPI | https://github.com/fastapi/fastapi |
| Uvicorn | https://github.com/encode/uvicorn |
| Pydantic | https://github.com/pydantic/pydantic |
| HTTPX | https://github.com/encode/httpx |
| pytest | https://github.com/pytest-dev/pytest |
| Liberation Sans font (SIL OFL 1.1) | https://github.com/liberationfonts/liberation-fonts |
| Barlow font (SIL OFL 1.1) | https://github.com/jpt/barlow |
| IBM Plex Mono font (SIL OFL 1.1) | https://github.com/IBM/plex |
| EmojiOne sprite sheet bundled with TextMesh Pro (CC BY 4.0) | https://www.joypixels.com |
| GitHub Pages (web hosting) | https://pages.github.com |
| GitHub Actions (CI) | https://github.com/features/actions |
| Render (coach proxy hosting) | https://render.com |
