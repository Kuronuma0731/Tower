---
description: 以 Principal Game Architect 視角嚴厲拷問 Tower 專案的設計決策，產出評分與問題清單
---

# /grill-tower

## Purpose

`/grill` is a hostile-but-constructive architecture and product review.

Its job is to find problems before implementation becomes expensive.

Do not praise the project by default. Challenge assumptions.

## Review Procedure

### 1. Understand Before Criticizing

Inspect: - current Godot project structure; - scenes; - scripts; -
resources; - autoloads; - assets; - existing tests; - recent changes; -
relevant documentation.

Do not invent files or architecture that were not inspected.

### 2. Attack the Design

Ask:

-   Is this feature actually needed?
-   Is the proposed architecture over-engineered?
-   Is there duplicated state?
-   Is a global manager being introduced unnecessarily?
-   Does this belong in a scene, component, resource, or system?
-   Is gameplay logic coupled to UI?
-   Will adding B1--B10 create copy-pasted floor logic?
-   Will future enemies/items/NPCs require modifying a giant manager?
-   Is the save system resilient to future data changes?
-   Are signals being used appropriately?
-   Are scene dependencies clear?
-   Is the implementation using Godot-native patterns?

### 3. Attack the Game Design

Check: - player goal; - difficulty curve; - combat/stat balance; -
keys/doors/resources; - exploration; - pacing; - dead ends; -
progression; - player feedback; - save/load behavior; - whether the
feature makes the game better rather than merely bigger.

### 4. Attack the Implementation

Look for: - giant scripts; - excessive autoloads; - hard-coded floor
numbers; - hard-coded enemy/item IDs; - duplicated scene logic; -
fragile node paths; - hidden dependencies; - magic numbers; -
unnecessary polling; - UI controlling game state directly; - game state
duplicated across multiple systems.

### 5. Force Decisions

For every important problem, classify it:

-   **P0 --- Blocker:** must fix before continuing.
-   **P1 --- Important:** should fix before the next major feature.
-   **P2 --- Improvement:** useful but can wait.
-   **P3 --- Optional:** do not spend meaningful time on it now.

## Output Format

### Verdict

One of: - PASS - PASS WITH CHANGES - BLOCKED

### P0 --- Must Fix

Concrete problems only.

### P1 --- Should Fix

Important structural or gameplay risks.

### P2 --- Later

Useful improvements that should not interrupt current progress.

### Biggest Risk

State the single most dangerous issue.

### Simplest Better Design

Give the smallest architecture that solves the problem.

### Next 3 Actions

Give exactly three concrete next steps.

## Rules

-   Do not recommend Unity solutions.
-   Prefer Godot-native solutions.
-   Do not redesign working systems without evidence.
-   Do not create abstractions just because they look elegant.
-   Do not expand scope during review.
-   If the project is already good, say so and identify what should NOT
    be changed.
