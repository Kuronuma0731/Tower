# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this project is

**"Tower" — a Unity mobile game in the design phase; no Unity project or source code exists yet.** A single-player, mobile-first (Android/iOS) Tower Strategy RPG / dungeon crawler in the Magic Tower (魔塔) lineage: grid movement, keys/doors, floor teleportation, puzzle solving, and resource-management combat. The core fun is the resource puzzle, not combat spectacle.

Design docs are written in Traditional Chinese; the user works in Traditional Chinese.

## The documents, and how to use them

- **`CONTEXT.md`** — the shared vocabulary (碰撞戰/指令戰, 軟鎖, 樓層快照/步數回溯, 可達性驗證器, …) and the locked decisions D1–D7, each with rationale and accepted costs. **Read this before any design or implementation discussion**, and use its vocabulary — the terms carry the design. Don't re-litigate a locked decision without concrete evidence it breaks something; if a decision changes, update `CONTEXT.md` first, then everything downstream.
- **`docs/architecture.md`** — the Unity architecture. Load-bearing rules: game logic lives in a `Core` asmdef that never references `UnityEngine` (ScriptableObjects convert to POCOs at the Bootstrap boundary); `DamageFormula` is the single source of numeric truth shared by both battle modes; all state mutations are `IGameCommand`s from day one (undo cannot be retrofitted); battles never load scenes. The build order at the bottom is deliberate — command battle comes last and is explicitly cancellable.
- **`.claude/commands/grill-tower.md`** — the `/grill-tower` command. Sets a deliberately hostile Principal Game Architect persona for design critique: challenge everything, never say "this is good", output scores plus critical/major/minor problem lists. Use it to stress-test a design before building it.

## Related directories (outside this repo)

- `G:/Claude/MagicTower` — a sibling directory, possibly reference material for the genre; not part of this project.
- Matt Pocock's skills repo formerly vendored here now lives at `G:/Claude/skills-main` — unrelated to this project.
