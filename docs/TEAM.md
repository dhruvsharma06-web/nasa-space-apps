# Team: 4 people, 8 levels

Each person owns **one system** (shared code everyone uses) and **two levels** (built from those systems).
Everyone works independently against `CONTRACT.md`, the stubs and `fixtures/` until **Sync 1 (end of week 2)**, the first combine.
Each person's full instructions are in `docs/prompts/`.

| | XR & Interaction Developer | Core Logic Developer | Content & AI Coach Developer | Integration Lead |
|---|---|---|---|---|
| **System** | Interactables, XR rig, hand/controller input, all UI, Hub | Phase FSM, mission runner, scorer, power simulation, progress save | Content style guide, hints/subtitles, offline coach, HTTP coach client, coach proxy | Unity project + packages, headset builds, LevelController, CI, PlayMode smoke tests, demo |
| **Levels** | L0 Safety, L1 Orientation | L4 Emergency, L7 Finale | L2 Equipment, L3 Cabin | L5 EVA, L6 Robotics (stretch) |
| **Prompt** | `docs/prompts/P1-xr-interaction.md` | `docs/prompts/P2-core-logic.md` | `docs/prompts/P3-content-ai-coach.md` | `docs/prompts/P4-integration-lead.md` |
| **Branch prefix** | `feat/xr-` | `feat/core-` | `feat/content-` | `feat/int-` |

## Folder ownership (edit only your own)
| Owner | Folders / files |
|---|---|
| XR & Interaction | `Assets/ASTRA/XR/**`, `Assets/ASTRA/UI/**`, `Assets/ASTRA/Scenes/Hub.unity`, `Assets/ASTRA/Levels/L0_Safety/**`, `Assets/ASTRA/Levels/L1_Orientation/**`, missions `l0-safety-v1.json`, `l1-orientation-v1.json` |
| Core Logic | `Assets/ASTRA/Core/**`, `Assets/ASTRA/Tests/EditMode/**`, `Assets/ASTRA/Levels/L4_Emergency/**`, `Assets/ASTRA/Levels/L7_Finale/**`, missions `l4-emergency-v1.json`, `l7-restore-power-v1.json` |
| Content & AI Coach | `Assets/ASTRA/Integrations/Coach/**` (assembly `ASTRA.Coach` + its tests), `services/coach-proxy/**`, `docs/content-style-guide.md`, `Assets/ASTRA/Levels/L2_Equipment/**`, `Assets/ASTRA/Levels/L3_Cabin/**`, missions `l2-equipment-v1.json`, `l3-cabin-v1.json` |
| Integration Lead | `Packages/**`, `ProjectSettings/**`, `Assets/ASTRA/App/**`, `Assets/ASTRA/Levels/_Template/**`, `Assets/Settings/**` (URP assets), `Assets/ASTRA/Tests/PlayMode/**`, `Assets/ASTRA/Levels/L5_EVA/**`, `Assets/ASTRA/Levels/L6_Robotics/**`, missions `l5-eva-v1.json`, `l6-robotics-v1.json`, `CONTRACT.md`, `.github/**`, `scripts/**`, `fixtures/**`, `docs/**` (except the style guide and your own prompt), `.gitignore`, `.gitattributes`, `.env.example`, `README.md` |

Shared art (`Assets/ASTRA/Art/**`): add new files freely; don't edit other people's files without asking.

## Who waits on whom (and what you use meanwhile)
| You need | From | Until it arrives, use |
|---|---|---|
| Unity project | Integration Lead, day 1–2 | Non-Unity work: Core C# + tests, JSON, coach proxy |
| Interactable prefabs (`ControlTarget`, `GrabbableTool`, `PlacementSocket`, `AstraXRRig`) | XR & Interaction, end of week 1 | Plain cubes named by `targetId` + the XR Interaction Toolkit starter XR Origin |
| `MissionRunner`, `MissionScorer` | Core Logic, end of week 2 | The LevelController's mock runner; `fixtures/session-result.*.json` |
| `LevelController` + level template scene | Integration Lead, end of week 1 | `ConsoleInteractionLogger` (XR & Interaction) to see your events |
| UI components | XR & Interaction | The stubs in `Assets/ASTRA/UI` (they log to the console) |
| Coach client | Content & AI Coach | Integration Lead shows the scripted offline text from step hints |

## Rules
1. Edit only your folders. Unity scenes and prefabs cannot be merged by hand, so never edit someone else's scene.
2. Don't change `CONTRACT.md`: ask the Integration Lead with a `CONTRACT CHANGE REQUEST` issue.
3. Work on short branches and merge through PRs. CI must be green. Commit `.meta` files together with their assets.
4. "Done" means it works on the headset. The Editor simulator alone does not count.
