# Tower — Godot 架構

引擎：**Godot 4.7（.NET / C#）**（D16）。發行目標：Android + iOS 買斷制 App（D2）＋ **Steam 桌面版**；**不做網頁版**。

---

## 第一條規則，也是唯一不能妥協的一條

**遊戲邏輯不得依賴遊戲引擎。**

塔的規則——移動、鑰匙門、傷害公式、資源消耗——全部是純 C#，不繼承 `Node`、不碰 `Transform2D`、不讀 `delta`。Godot 只負責「畫出來」與「收輸入」。

三個理由：

1. **可達性驗證器**要在沒有引擎執行環境的情況下，幾秒內模擬幾千條路徑。邏輯綁在 `Node` 上就辦不到。D11 封閉經濟下驗證器是生命線，這條理由的權重是三條裡最高的。
2. 規則能用一般的 dotnet 測試工具驗，不必開編輯器。
3. 引擎會換，規則不會。

第 3 點在 2026-08-09 兌現了：專案由 Unity 換到 Godot，`Tower.Core` 的 **32 個檔一行未改**直接搬過來，45 條驗收全綠。當初這條規則靠 Unity asmdef 的 `noEngineReferences` 強制，現在由「`Tower.Core.csproj` 不引用 `GodotSharp`」繼續強制——編譯器仍然幫你守著這條線。

**這條線的必然推論**：Core 看不見任何 Godot 型別，包括 `Resource`。所以資料一律走 CSV → POCO，由表現層在載入時讀進來餵給 Core（見「資料管線」）。Core 的任何型別出現 `using Godot` 就是走錯了。

---

## 專案結構

```
project.godot                   # Godot 專案設定（橫向、Nearest filter）
Tower.csproj                    # 表現層；只編 game/，引用 Tower.Core
game/                           # Godot 端：只有表現與輸入
  Main.tscn                     # 唯一的 gameplay 場景
  GameRoot.cs                   # 協調者：載資料、組樓層、輸入、演出
  HudView.cs                    # 三欄 HUD（CanvasLayer）
  ViewFactory.cs                # 材質快取、Sprite2D/Label/底板
  SpriteMap.cs                  # 概念 id → 檔名的唯一對照表
  TextBank.cs                   # ui-strings / dialogues 的唯一來源
src/Tower.Core/                 # 純 C#，不引用 GodotSharp
  Grid/                         # 格子、樓層網格、路徑
  Combat/                       # CombatResolver、DamageFormula、怪物特性結算
  Commands/                     # IGameCommand、GameState
  Data/                         # Catalog、Csv（POCO 與載入）
  Floors/                       # 樓層定義（只認 id，不存數值）
  Simulation/                   # 可達性驗證器（含守關怪合約檢查）
data/                           # CSV：數值的唯一真相，res:// 直接讀
assets/                         # sprites / audio / fonts（不進版控）
tools/CoreVerify/               # 引擎外驗收（dotnet 幾秒跑完）
tools/*.ps1                     # 素材切片、爆閃產生器
```

**沒有 `StreamingAssets` 那層副本了。** Unity 時期 `data/` 必須複製一份給執行期讀，那份副本漂移過——序章對話加在 `data/` 卻沒同步，遊戲一句話都不顯示。Godot 的 `res://data/` 直接指向專案內的同一份，這個病從根上消失。

---

## 三個關鍵設計

### 1. DamageFormula 是唯一數值真相

D1 之後只有一種戰鬥，但分層仍然保留——公式與結算分開，**怪物特性**才有地方掛：

```
DamageFormula                                    // 最底層，唯一的公式所在地
  └─ ComputeDamage(attackerStats, defenderStats, traits) → int

CombatResolver                                   // 碰撞戰：在公式上跑確定性迴圈
  └─ ResolveCollision(attacker, defender) → CollisionOutcome   // 一次算完
```

**怪物特性**（先攻、連擊、魔攻、吸血…）是 `DamageFormula` 的輸入參數，不是散落在各處的 if——新增一個特性 = 擴充公式的一個結算規則 + 資料表一個欄位。特性一律確定性，禁止機率（D1 衍生規則）；迴避是唯一的例外形式，隨機性被限制在表現層（D15）。

`ResolveCollision` 必須無副作用：吃狀態進去、吐結果出來、不改任何東西。因為**傷害預覽**就是直接呼叫它——預覽和實戰跑的是同一個函式，所以預覽永遠不會騙人。**守關怪**也走同一條路——只是數值大、特性組合兇的怪物，沒有專屬程式路徑。

公式的紙上暫定版在 [`boss-test-8f.md`](boss-test-8f.md)，其精算表全部數字由 `tools/CoreVerify` 重現。

### 2. 狀態變更走指令模式；存檔是快照 + 指令流

D7 要求**回溯**，這決定了狀態管理的形狀：所有改變 `GameState` 的操作——移動、開門、碰撞戰、購買——都是一個 `IGameCommand`，帶 `Apply` 與 `Undo`。**這必須從 `Core` 第一天就做，事後補裝等於重寫。**

```
IGameCommand
  ├─ Apply(GameState)   // 就地變更；Apply 後 Undo 必須讓狀態與原先完全相等
  └─ Undo(GameState)

GameState.Clone()       // 快照 = 深拷貝（樓層入口自動存檔用它）

SaveFile
  ├─ snapshots      : Map<FloorId, GameState>   // 每層入口的快照（外層防軟鎖）
  ├─ currentFloor   : FloorId
  └─ commandsSince  : List<IGameCommand>        // 入口快照之後的指令流（內層防軟鎖）
```

這個結構讓三件事變成同一件事：

- **回溯** = 從指令流尾端 pop command 執行 `Undo`——但入口在遊戲層：先檢查並消耗一顆**回溯道具**（D7），Core 只提供機制，不管收費
- **當前狀態** = 入口快照 + 重放指令流
- **退回樓層入口** = 丟掉指令流（免費，D7 外層）

**快照完整性規則**：退回 N 層入口時，**所有晚於該快照的快照一併作廢**（時間軸只有一條）。否則玩家可以退回 5F 重新配置資源，再「跳回」9F 的舊快照，兩個時間線的資源憑空疊加——這是套利漏洞，不是防軟鎖。實作上快照帶單調遞增的序號，回退即截斷。

一場碰撞戰 = **一個** command，所以回溯一步就是回溯一整場戰鬥，語義乾淨。

檔案大小可控：`GameState` 是純數值與 flag，一層幾 KB；指令流在寫入新樓層快照時清空。**不要**把樓層地圖本身存進去——那是靜態資料。

### 3. 戰鬥不載入場景

**整個遊戲只有一個 gameplay 場景（`Main.tscn`）。** 碰撞戰在 HUD 的 VS 面板上逐回合演出（爆閃、紅色傷害數字、體力逐格掉，照原版），守關戰頂多加一段短演出，仍在同一場景。

理由：場景載入在中低階 Android 上是 0.5–2 秒。魔塔類型一場遊戲會發生數百次戰鬥，載入一次就毀掉節奏。

**演出不改變規則**：`ResolveCollision` 一次算完，逐回合只是把算好的結果攤開來播。回合數壓在 12 次以內、超過就縮短間隔，否則數十回合的硬仗會拖垮節奏。

---

## 資料管線

**數值在 CSV 調，不在編輯器 Inspector。** `data/*.csv` 是唯一真相：

```
data/monsters.csv ─┐
data/items.csv   ─┴→ Core/Data/Catalog.Load(csvText) → POCO 字典 → CombatResolver
data/ui-strings.csv ┐
data/dialogues.csv ─┴→ game/TextBank → 所有玩家可見文字
```

`Catalog.Load` 吃的是 **CSV 文字**而不是路徑——這樣 Core 不需要知道 `res://` 或檔案系統，驗證器與遊戲可以用同一個函式從不同來源餵資料。未知的特性名或分類在載入期就擲例外，錯字不會流到玩家手機。

**樓層只認 id，不存數值**（`F01.MonsterRefs = { "slime_green", ... }`）。這條規則是被實際的 bug 逼出來的：曾經樓層、CSV、展示層各存一份數值，三份漂移到互相矛盾。

**鐵則**：任何玩家可見字串都不得出現在程式碼，一律走 `ui-strings.csv` / `dialogues.csv`。

---

## 相依組裝

**需要，但不需要框架。** `GameRoot._Ready()` 就是組裝根：手動 new 出所有服務、串好。

```csharp
_text = TextBank.Load();                                   // res://data/*.csv
_catalog = Catalog.Load(TextBank.ReadCsv("monsters.csv"),
                        TextBank.ReadCsv("items.csv"));    // CSV → POCO
_view = new ViewFactory();
_hud  = new HudView(_view, _text, this);
```

相依關係**看得見**——打開 `GameRoot` 就知道整個系統長怎樣。

**絕對不要用 singleton / static manager。** 它會讓**可達性驗證器**沒辦法平行跑多個模擬。

---

## 效能

這類型的瓶頸**不在 500 隻怪同時動**（回合制，不會同時動）。真正的風險：

1. **每格一個節點** — 一層 13×13 = 169 個 `Sprite2D`，切樓層時全部銷毀重建。目前規模無虞；擴到更大樓層時改用 `TileMapLayer`。
2. **UI 重建** — 怪物手冊這類長列表要虛擬化，不要一次生成 200 個 item。
3. **存檔序列化** — 每次進樓層都寫快照。JSON 即可，但**寫在背景執行緒**。
4. **匯出設定** — 行動端要確認材質壓縮與圖集打包；像素風必須維持 Nearest filter 與整數縮放，否則整套糊掉。

---

## 先做什麼

1. ~~**Core：Grid + Combat + IGameCommand**~~ ✅ 完成（跨引擎搬遷後仍全綠）
2. ~~**Simulation + 可達性驗證器**~~ ✅ 完成（含守關怪合約檢查、守衛有效性檢查）
3. ~~**最小可玩版**~~ ✅ 完成（F00–F02、方向鍵、碰撞戰演出、三欄 HUD）
4. ~~**怪物手冊 + 回溯道具**~~ ✅ 完成（`game/BestiaryView.cs`，B 鍵；回溯 Z 鍵消耗沙漏）
5. ~~**存檔：樓層快照 + 指令流序列化 + 祭壇**~~ ✅ 完成（商店與祭壇一併落地，見 `game/ShopView.cs`）
6. ~~**關卡編輯器**~~ ✅ 完成（`game/EditorMode.cs`，F2 開關）。六項規格全數落地；做成**遊戲內模式**而非 `EditorPlugin`——直接重用 ViewFactory/SpriteMap/FloorSolver，所見即遊戲實際長相。原本的理由仍然成立：在手工做超過 5 層之前一定要有。一人開發（D12）下，它的產能直接決定 D6 的 25–30 層是否成立。**Godot 的編輯器外掛（`EditorPlugin`）是這一步的載體**，Unity 時期寫的六功能規格（`floor-authoring.md`）仍然適用
7. **10–15 層內容 + 難度弧線**（MVP＝D3；出貨版 25–30 層＝D6）

第 7 步完成後有一個**複審點**：碰撞戰魔塔本身夠不夠好玩、編輯器產能撐不撐得起 25–30 層。D6 的規模與美術方案都在這裡拍板。

**新增於 D16 的驗證項**：Android / iOS / Steam 三個匯出目標各自跑通一次（尤其 iOS，Godot 的流程與 Unity 差異最大），不要等到內容做完才發現匯出有坑。
