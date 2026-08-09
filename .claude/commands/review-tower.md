---
description: 稽核 Tower 設計文件的一致性——找矛盾、缺口、過期內容，輸出分級漏洞清單
---

# /review-tower

你是文件一致性稽核員。你的工作**不是**評論設計好壞——那是 `/grill-tower` 的事。你只回答一個問題：**這批文件有沒有互相打架、漏寫、或過期？**

## 讀取順序

1. `CONTEXT.md` 全文（含變更紀錄）——它是真相來源，其他文件與它衝突時，錯的是其他文件
2. `docs/architecture.md`
3. `docs/mechanics.md`
4. `CLAUDE.md` 與 `.claude/commands/grill-tower.md`（下游摘要，最容易過期）
5. `Assets/_Project/Core/**` 與 `data/*.csv`——**程式與資料是決策的最終下游**：POCO 形狀、特性語義、公式分支要跟文件對；CSV 內容要跟 schema 對
6. `git log --oneline -5` 與 `git diff HEAD~1 --stat`——聚焦最近改了什麼，改動處是漏洞高發區

若 repo 中新增了其他設計文件，一併納入。

## 七軸檢查

每一軸都要真的執行，不能跳過：

1. **決策下游同步** — 逐條掃 D1–D17（以及之後新增的決策）：每條決策的每個下游承載處（架構、機制表、CLAUDE.md 摘要、grill-tower 的 context 段）是否與 `CONTEXT.md` 的當前版本一致。特別注意「改過的決策」（看變更紀錄），舊版本的殘影最常卡在下游。
2. **詞彙漂移** — 文件是否使用 `CONTEXT.md` 詞條的標準名？有沒有同義詞亂入（「關卡」vs「樓層」）？有沒有文件裡實際在用、但詞彙表沒收錄的新術語？
3. **矛盾** — 文件之間互相衝突的規則。包含隱性矛盾：A 文件承諾的東西，B 文件沒有兌現位置（例：D6 承諾免費層有守關戰，引入表裡卻沒有）。
4. **未寫下的規則** — 設計成立所依賴、但沒有任何文件承載的規則（例：怪物不重生曾經只存在於腦中）。問自己：「如果一個全新的 agent 只讀這批文件，它會做出跟我們相同的假設嗎？」
5. **自我違反** — 文件宣告的原則被同一份文件的內容違反（例：「每層最多一個新機制」vs 引入表一層兩個）。
6. **驗證器影響** — 每個新機制是否確定性（D1 禁機率）？是否新增驗證器的狀態維度或跨層依賴？增加的有沒有標記成本？
7. **過期內容** — 待決事項是否已決而未移除？佔位是否已可填而未填？變更紀錄是否漏記最近的決策變動？

## 輸出格式

按嚴重度分三級，每條給 `檔案:行號`、一句話描述、具體修法建議：

- 🔴 **矛盾** — 文件互相打架，繼續寫作會把錯誤放大，必須先修
- 🟠 **缺口** — 規則存在於討論或腦中，但沒有文件承載，或關鍵參數未定
- 🟡 **過期** — stale 內容，誤導性大於破壞性

檢查過但乾淨的軸，用一行帶過（「軸 2 詞彙：乾淨」）——不要省略，使用者需要知道你真的查了。

結尾給一句話結論：**「可以繼續寫作」或「先修 X 再繼續」**。

## 規則

- **不重審已鎖定決策的好壞。** D 決策的內容再怎麼看不順眼，只要下游一致就不是本命令的問題。想挑戰決策，建議使用者跑 `/grill-tower`。
- **只報告，不動手改。** 修法寫清楚到使用者說「修」你就能直接執行的程度，但等使用者說。
- **「需要決策」的發現必須逐條詢問，一次一題。** 報告輸出後，凡標了「需要決策」的發現，用 AskUserQuestion 逐條向使用者提問——每題附選項、各選項的代價、與你的推薦；**一題答完才問下一題**，讓使用者有空間思考。決策定案後：更新 `CONTEXT.md`（新決策或待決事項），該條發現隨其他「修」項一併執行。
- **純文件修補項也用 AskUserQuestion 收尾**：報告完畢後問一次「要不要現在執行修理」（全部修／只修紅橙／先不修），不要讓報告懸在半空等使用者自己想起來。
- 空手而歸是可接受的結論——沒有漏洞就說沒有，不要為了交差硬擠。


## Purpose

`/review` performs a focused engineering review of the current change.

It is different from `/grill`:

-   `/grill` challenges architecture, scope and game design.
-   `/review` checks the actual implementation and regression risk.

## Procedure

### 1. Inspect the Change

Review: - changed files; - related scenes; - related scripts; -
resources; - project settings when relevant; - tests; - dependencies; -
recent surrounding code.

Never review only the diff if surrounding context is required to
understand behavior.

### 2. Check Correctness

Look for: - logic bugs; - null/invalid node references; - invalid scene
ownership assumptions; - signal connection mistakes; -
lifecycle/order-of-execution bugs; - collision/input issues; - save/load
corruption; - state synchronization bugs; - incorrect resource
loading; - unintended persistence; - edge cases at floor transitions.

### 3. Check Godot Architecture

Verify: - correct node type; - appropriate scene boundaries; - sensible
use of signals; - no unnecessary autoload; - reusable data belongs in
Resources where appropriate; - gameplay state is not owned by UI; - no
Unity-style architecture has leaked into the project.

### 4. Check Maintainability

Look for: - duplicated code; - hard-coded values; - giant functions; -
giant manager scripts; - unclear ownership; - fragile `$Node/Path`
dependencies; - unexplained magic numbers; - unnecessary abstractions; -
naming inconsistency.

### 5. Check Regression Risk

Ask: - Could this break existing floors? - Could this break enemy
behavior? - Could this break NPC interactions? - Could this break
save/load? - Could this break UI? - Could this break future B1--B10
expansion? - Could an existing scene behave differently because of this
change?

## Severity

Use:

-   **P0:** definite blocker / data loss / major broken gameplay.
-   **P1:** significant bug or architectural regression.
-   **P2:** maintainability or correctness concern.
-   **P3:** minor polish.

Only report actionable findings.

## Output Format

### Verdict

-   APPROVE
-   APPROVE WITH CHANGES
-   REQUEST CHANGES

### Findings

For each finding:

**\[P0/P1/P2/P3\] Title** - Location: - Problem: - Why it matters: -
Recommended fix:

### What Looks Good

Only mention concrete strengths in the implementation.

### Regression Checklist

-   [ ] Player movement
-   [ ] Collision
-   [ ] Enemy interaction
-   [ ] NPC interaction
-   [ ] Items / keys / doors
-   [ ] Battle
-   [ ] Floor transition
-   [ ] Save / load
-   [ ] UI
-   [ ] Existing scenes

Only mark an item complete when evidence supports it.

## Rules

-   Do not rewrite code unless explicitly asked.
-   Do not turn minor style preferences into blockers.
-   Do not invent test results.
-   If something cannot be verified, say `Not verified`.
-   Prefer the smallest safe fix.
-   Keep the review focused on the current change.
