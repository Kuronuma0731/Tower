# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**"Tower" — a Unity mobile game, now in early implementation.** A single-player, mobile-first (Android/iOS) Tower Strategy RPG / dungeon crawler in the Magic Tower (魔塔) lineage: grid movement, keys/doors, floor teleportation, puzzle solving, and resource-management combat. The core fun is the resource puzzle, not combat spectacle.

Current state: design docs complete and audited; art complete (38/38 sprites in `art/`); pipeline CSVs live in `data/`; Unity project scaffolded for **6000.0.37f1** with a working `Tower.Core` (all 13 acceptance vectors green). The build order in `docs/architecture.md` tracks what comes next.

**The code**: game logic lives in `Assets/_Project/Core/` under the `Tower.Core` asmdef with `noEngineReferences: true` — it must never reference `UnityEngine`. Stats come from `data/*.csv` via `Core/Data/Catalog`; floors declare only ids, never numbers, so the two can't drift. The Unity layer in `Assets/_Project/Game/` is split by job: `GamePreviewBootstrap` (rules + input), `ViewFactory` (sprites/text/plates), `HudView` (the three-column HUD), `TextBank`, `AudioBank`, `SpriteMap`.

**Verifying** — run this after any Core change, it takes seconds and needs no Unity:

```
dotnet run --project tools/CoreVerify
```

For the Unity side, `-batchmode -quit` **exits before compilation finishes** and its log will look clean even when the build is broken — that false negative has bitten twice. Use `-batchmode -executeMethod Tower.EditorDev.DevAutomation.CompileOnly` instead: Unity finishes compiling before running the method, so a log with no `error CS` is then trustworthy.

Once Unity has opened the project, `*.meta` files MUST be committed — never gitignore them. Art and audio are **not** tracked (see `.gitignore`); rebuild them from the pack per `docs/art-assets.md`.

Design docs are written in Traditional Chinese; the user works in Traditional Chinese. Player-visible strings never appear in code — they live in `data/ui-strings.csv` and `data/dialogues.csv`.

## The documents, and how to use them

- **`CONTEXT.md`** — the shared vocabulary (碰撞戰, 怪物特性, 守關怪, 軟鎖, 樓層快照/回溯, 祭壇, 可達性驗證器, …) and the locked decisions D1–D15, each with rationale and accepted costs. **Read this before any design or implementation discussion**, and use its vocabulary — the terms carry the design. Don't re-litigate a locked decision without concrete evidence it breaks something; if a decision changes, update `CONTEXT.md` first (including its 變更紀錄), then everything downstream. Key decisions: pure collision combat (no command battle, no active skills), premium, closed economy, undo as a consumable resource with no free misclick remedy anywhere (menus included), lethal collisions are walls — the game has no death system, solo dev + AI.
- **`docs/architecture.md`** — the Unity architecture. Load-bearing rules: game logic lives in a `Core` asmdef that never references `UnityEngine` (ScriptableObjects convert to POCOs at the Bootstrap boundary); `DamageFormula` is the single source of numeric truth and monster traits are its inputs (deterministic only, never probabilistic); all state mutations are `IGameCommand`s from day one (undo cannot be retrofitted); battles never load scenes. The build order ends at a review gate after the MVP floors — ship scale and art plan get decided there.
- **`docs/mechanics.md`** — the mechanism vocabulary for floors 1F–10F (MVP): every mechanic with the trade-off it creates, the per-floor introduction schedule (max one new combat trait per floor, ≤2 mechanics total), and the solver contract. Content design starts here; numbers live in the data pipeline, not in this file.
- **`docs/boss-test-8f.md`** — the 8F guardian paper test: the provisional `DamageFormula` (with trait rules), a worked decision table proving the "calculable boss" claim, and pass criteria. Its numbers double as acceptance test vectors for the `DamageFormula` implementation — keep them in sync or update both.
- **`docs/data-schema.md`** — the data pipeline: CSV columns (real files live in `data/` at repo root), floor JSON format (char-row terrain, entities with tower-unique eids), Core POCO shapes, and the importer validation gate. Iron rule: no player-visible string may appear in code — everything goes through `ui-strings.csv` / `dialogues.csv`.
- **`docs/reference-classic-mt.md`** — field study of the original game (stat trajectory 1F–7F, its full monster table, trait vocabulary comparison, UI anatomy). Reference only, never spec.
- **`docs/floor-authoring.md`** — how floors get made: five-step pipeline (theme → budget sheet → Unity level editor → solver → human playtest), the editor's six-feature MVP spec, stairs coordinate-alignment convention, and density guidelines. Content waits for the editor (build step 6) by explicit decision.
- **`docs/art-assets.md`** — the 38-sprite asset list (complete), generation workflow, and the processing pipeline (`tools/center-sprites.ps1`, `tools/dewhite.ps1`). Sources in `art/source/`, game-ready sprites in `art/sprites/`.
- **`.claude/commands/grill-tower.md`** — the `/grill-tower` command. Sets a deliberately hostile Principal Game Architect persona for design critique: challenge everything, never say "this is good", output scores plus critical/major/minor problem lists. Use it to stress-test a design before building it.
- **`.claude/commands/review-tower.md`** — the `/review-tower` command. Consistency audit across all design docs: seven axes (decision-downstream sync, vocabulary drift, contradictions, unwritten rules, self-violations, solver impact, staleness), output as 🔴/🟠/🟡 findings with file:line and suggested fixes. Report-only — it never edits. The division of labor: `/grill-tower` judges whether the design is good; `/review-tower` judges whether the documents agree with each other. The user runs it after each writing session.

## Related directories (outside this repo)

- `G:/Claude/MagicTower` — a **different, unrelated project**. Do not read it for reference or treat it as part of Tower.
- Matt Pocock's skills repo formerly vendored here now lives at `G:/Claude/skills-main` — unrelated to this project.
