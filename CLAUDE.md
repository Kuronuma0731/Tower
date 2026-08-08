# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**"Tower" — a Unity mobile game in the design phase; no Unity project or source code exists yet.** A single-player, mobile-first (Android/iOS) Tower Strategy RPG / dungeon crawler in the Magic Tower (魔塔) lineage: grid movement, keys/doors, floor teleportation, puzzle solving, and resource-management combat. The core fun is the resource puzzle, not combat spectacle.

Design docs are written in Traditional Chinese; the user works in Traditional Chinese.

## The documents, and how to use them

- **`CONTEXT.md`** — the shared vocabulary (碰撞戰, 怪物特性, 軟鎖, 樓層快照/回溯, 祭壇, 可達性驗證器, …) and the locked decisions D1–D12, each with rationale and accepted costs. **Read this before any design or implementation discussion**, and use its vocabulary — the terms carry the design. Don't re-litigate a locked decision without concrete evidence it breaks something; if a decision changes, update `CONTEXT.md` first (including its 變更紀錄), then everything downstream. Key decisions: pure collision combat (no command battle, no active skills), premium, closed economy, undo as a consumable resource, solo dev + AI.
- **`docs/architecture.md`** — the Unity architecture. Load-bearing rules: game logic lives in a `Core` asmdef that never references `UnityEngine` (ScriptableObjects convert to POCOs at the Bootstrap boundary); `DamageFormula` is the single source of numeric truth and monster traits are its inputs (deterministic only, never probabilistic); all state mutations are `IGameCommand`s from day one (undo cannot be retrofitted); battles never load scenes. The build order ends at a review gate after the MVP floors — ship scale and art plan get decided there.
- **`docs/mechanics.md`** — the mechanism vocabulary for floors 1F–10F (MVP): every mechanic with the trade-off it creates, the per-floor introduction schedule (max one new mechanic per floor), and the solver contract. Content design starts here; numbers live in the data pipeline, not in this file.
- **`.claude/commands/grill-tower.md`** — the `/grill-tower` command. Sets a deliberately hostile Principal Game Architect persona for design critique: challenge everything, never say "this is good", output scores plus critical/major/minor problem lists. Use it to stress-test a design before building it.
- **`.claude/commands/review-tower.md`** — the `/review-tower` command. Consistency audit across all design docs: seven axes (decision-downstream sync, vocabulary drift, contradictions, unwritten rules, self-violations, solver impact, staleness), output as 🔴/🟠/🟡 findings with file:line and suggested fixes. Report-only — it never edits. The division of labor: `/grill-tower` judges whether the design is good; `/review-tower` judges whether the documents agree with each other. The user runs it after each writing session.

## Related directories (outside this repo)

- `G:/Claude/MagicTower` — a **different, unrelated project**. Do not read it for reference or treat it as part of Tower.
- Matt Pocock's skills repo formerly vendored here now lives at `G:/Claude/skills-main` — unrelated to this project.
