# Per-person Master Prompts

One prompt per teammate. Each one is self-contained: it holds the whole plan (S0 to S5) for that role.

| Role | Prompt | Levels |
|---|---|---|
| XR & Interaction Developer | [P1-xr-interaction.md](P1-xr-interaction.md) | L0, L1 |
| Core Logic Developer | [P2-core-logic.md](P2-core-logic.md) | L4, L7 |
| Content & AI Coach Developer | [P3-content-ai-coach.md](P3-content-ai-coach.md) | L2, L3 |
| Integration Lead | [P4-integration-lead.md](P4-integration-lead.md) | L5, L6 (stretch) |

## How to use
1. Open your prompt file and paste the **whole file** into your AI coding assistant as the first message.
2. Also give it `CONTRACT.md`, `docs/PRD.md` and `docs/TEAM.md` (or point it at the cloned repo).
3. Type `start`. It works one stage at a time and stops with a **STAGE REPORT** at the end of each stage.
4. Send each STAGE REPORT to the Integration Lead. Type `next stage` only after the team's sync passes.

Everyone works alone against `CONTRACT.md`, the stubs and `fixtures/` until **Sync 1 (end of week 2)**, the first combine.
