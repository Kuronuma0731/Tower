# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**"Tower" — a Godot mobile + Steam game, in early implementation.** A single-player Tower Strategy RPG / dungeon crawler in the Magic Tower (魔塔) lineage: grid movement, keys/doors, floor teleportation, puzzle solving, and resource-management combat. The core fun is the resource puzzle, not combat spectacle. Ships to **Android/iOS (premium app) and Steam desktop — never web** (D16).

Current state: design docs complete and audited; pixel art in place; pipeline CSVs in `data/`; **Godot 4.7 (.NET)** project with a working `Tower.Core` and floors F00–F02 playable. The build order in `docs/architecture.md` tracks what comes next.

**The code**: game logic lives in `src/Tower.Core/` and **must never reference `GodotSharp`** — that line is what let the whole engine switch cost only the view layer (32 files moved from Unity unchanged). Stats come from `data/*.csv` via `Core/Data/Catalog`; floors declare only ids, never numbers, so the two can't drift. The Godot layer in `game/` is split by job: `GameRoot` (rules + input + presentation), `HudView` (three-column HUD), `ViewFactory` (textures/labels/panels), `TextBank`, `SpriteMap`.

**Verifying** — run this after any Core change; it takes seconds and needs no editor:

```
dotnet run --project tools/CoreVerify
```

It runs 45 checks plus, with `-- --play`, a headless playthrough that walks the floors under the real rules. That playthrough is the only thing that has ever caught "solvable but the design intent failed" — a solver goes green when a guard can be walked around, because bypassing it *is* a solution.

**Godot side**: `dotnet build Tower.csproj` compiles the view layer. Godot's SDK globs every `.cs` under the project root by default, which pulls in `src/` and `tools/` and collides — `Tower.csproj` therefore sets `EnableDefaultCompileItems=false` and lists `game/**` explicitly. To run: `Godot_v4.7.1-stable_mono_win64.exe --path . --resolution 1280x720`; add `--headless --import` first after adding assets.

Art, audio and fonts are **not** tracked (see `.gitignore`); rebuild them from the pack per `docs/art-assets.md`, and the impact burst via `tools/make-fx.ps1`. The CJK font currently in `assets/fonts/` is Microsoft JhengHei, **development only — it cannot ship**; an OFL font is required before release.

Design docs are written in Traditional Chinese; the user works in Traditional Chinese. Player-visible strings never appear in code — they live in `data/ui-strings.csv` and `data/dialogues.csv`.

## The documents, and how to use them

- **`CONTEXT.md`** — the shared vocabulary (碰撞戰, 怪物特性, 守關怪, 軟鎖, 樓層快照/回溯, 祭壇, 可達性驗證器, …) and the locked decisions D1–D17, each with rationale and accepted costs. **Read this before any design or implementation discussion**, and use its vocabulary — the terms carry the design. Don't re-litigate a locked decision without concrete evidence it breaks something; if a decision changes, update `CONTEXT.md` first (including its 變更紀錄), then everything downstream. Key decisions: pure collision combat (no command battle, no active skills), premium, closed economy, undo as a consumable resource with no free misclick remedy anywhere (menus included), lethal collisions are walls — the game has no death system, solo dev + AI.
- **`docs/architecture.md`** — the Godot architecture. Load-bearing rules: game logic lives in `src/Tower.Core` and never references `GodotSharp` (data enters as CSV text → POCOs, so Core needs no engine file APIs); `DamageFormula` is the single source of numeric truth and monster traits are its inputs (deterministic only, never probabilistic); all state mutations are `IGameCommand`s from day one (undo cannot be retrofitted); battles never load scenes. The build order ends at a review gate after the MVP floors — ship scale and art plan get decided there.
- **`docs/mechanics.md`** — the mechanism vocabulary for floors 1F–10F (MVP): every mechanic with the trade-off it creates, the per-floor introduction schedule (max one new combat trait per floor, ≤2 mechanics total), and the solver contract. Content design starts here; numbers live in the data pipeline, not in this file.
- **`docs/boss-test-8f.md`** — the 8F guardian paper test: the provisional `DamageFormula` (with trait rules), a worked decision table proving the "calculable boss" claim, and pass criteria. Its numbers double as acceptance test vectors for the `DamageFormula` implementation — keep them in sync or update both.
- **`docs/data-schema.md`** — the data pipeline: CSV columns (real files live in `data/` at repo root), floor JSON format (char-row terrain, entities with tower-unique eids), Core POCO shapes, and the importer validation gate. Iron rule: no player-visible string may appear in code — everything goes through `ui-strings.csv` / `dialogues.csv`.
- **`docs/reference-classic-mt.md`** — field study of the original game (stat trajectory 1F–7F, its full monster table, trait vocabulary comparison, UI anatomy). Reference only, never spec.
- **`docs/floor-authoring.md`** — how floors get made: five-step pipeline (theme → budget sheet → level editor → solver → human playtest), the editor's six-feature MVP spec, stairs coordinate-alignment convention, and density guidelines. Content waits for the editor (build step 6) by explicit decision.
- **`docs/art-assets.md`** — the sprite mapping table and processing pipeline (`tools/slice-sheets.ps1` cuts the pack sheets; `tools/make-fx.ps1` generates the impact burst). Game-ready sprites live in `assets/sprites/` (untracked).
- **`.claude/commands/grill-tower.md`** — the `/grill-tower` command. Sets a deliberately hostile Principal Game Architect persona for design critique: challenge everything, never say "this is good", output scores plus critical/major/minor problem lists. Use it to stress-test a design before building it.
- **`.claude/commands/review-tower.md`** — the `/review-tower` command. Consistency audit across all design docs: seven axes (decision-downstream sync, vocabulary drift, contradictions, unwritten rules, self-violations, solver impact, staleness), output as 🔴/🟠/🟡 findings with file:line and suggested fixes. Report-only — it never edits. The division of labor: `/grill-tower` judges whether the design is good; `/review-tower` judges whether the documents agree with each other. The user runs it after each writing session.

## Related directories (outside this repo)

- `G:/Claude/MagicTower` — a **different, unrelated project**. Do not read it for reference or treat it as part of Tower.
- Matt Pocock's skills repo formerly vendored here now lives at `G:/Claude/skills-main` — unrelated to this project.


## Engine Decision

The project has migrated from Unity to **Godot 4**.

Godot is now the source of truth for all game implementation decisions.

Do NOT introduce Unity-specific architecture, APIs, packages,
components, scenes, prefabs, or workflows unless explicitly requested.

## Project Direction

Target game: - 2D dungeon / 魔塔 RPG - Multiple dungeon floors (1F--10F
and expandable) - Grid-based or tile-based movement - Player, enemies,
NPCs, items, doors, keys, treasure - Battle/stat systems - Dialogue and
events - Save/load - Custom UI - Custom character art

Current art naming examples: - `hero_d0_f0` (4 directions x 4 walk frames) - `mon_slime_g_f0` - `npc_guard_old`

## Godot Architecture Rules

Prefer Godot-native architecture:

-   `CharacterBody2D` for movable characters when appropriate
-   `Area2D` for trigger/interact zones
-   `StaticBody2D` / collision nodes for solid obstacles
-   `TileMap` / current Godot tilemap workflow for dungeon maps
-   `AnimatedSprite2D` or `AnimationPlayer` for animation
-   `CanvasLayer` for HUD / screen-space UI
-   Resources (`.tres`) for reusable data definitions
-   Signals for decoupled events
-   Autoloads only for genuinely global systems
-   Scenes (`.tscn`) as reusable composition units

**Language: C#, and this is the justified exception to "GDScript by default".**
The reason is `src/Tower.Core` — 32 files of engine-free C# holding
`DamageFormula`, `CombatResolver`, the reachability solver and every floor,
covered by 45 checks that run in seconds on plain dotnet with no editor. That
body of code moved from Unity to Godot **unchanged**, which is the whole payoff
of the engine-free rule. Rewriting it in GDScript would throw that away and,
worse, kill `tools/CoreVerify` — and under D11's closed economy the solver is a
lifeline, not a convenience. Godot-side presentation is also C# simply so the
two halves share one toolchain.

Avoid unnecessary singleton/global state.

## AI Implementation Rule

Before adding a new system: 1. Inspect the existing Godot project
structure. 2. Reuse existing scenes, scripts, resources, signals and
utilities. 3. Keep gameplay logic separate from presentation. 4. Prefer
small composable systems over large manager scripts. 5. Do not duplicate
systems that already exist. 6. Keep changes compatible with the current
Godot version used by the project.

## Migration Rule

When converting old Unity plans/code: - Translate concepts, not APIs. -
Unity `GameObject` concepts should become Godot nodes/scenes. - Unity
Prefabs should normally become reusable `.tscn` scenes. - Unity
ScriptableObjects should normally become Godot `Resource` types /
`.tres`. - Unity Animator workflows should normally become
`AnimatedSprite2D` or `AnimationPlayer`. - Unity Events should normally
become Godot signals. - Unity physics/collision assumptions must be
re-evaluated using Godot's physics nodes.

Never perform a literal API-by-API translation when a native Godot
design is cleaner.

## Definition of Done

A feature is not considered complete merely because the game runs.

It must: - follow the existing architecture; - avoid duplicated logic; -
preserve save/load compatibility where applicable; - remain testable; -
avoid unnecessary global state; - have clear scene/node ownership; - use
Godot-native patterns; - pass the project's review checklist.
