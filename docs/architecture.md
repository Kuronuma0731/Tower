# Tower — Unity 架構建議

前提決策見 [`CONTEXT.md`](../CONTEXT.md)。這份文件講**怎麼蓋**，不重述**為什麼**。

---

## 一條總原則

**遊戲邏輯不得依賴 UnityEngine。**

塔的規則——移動、鑰匙門、傷害公式、資源消耗——全部是純 C#，不繼承 `MonoBehaviour`、不碰 `Transform`、不讀 `Time.deltaTime`。Unity 只負責「畫出來」與「收輸入」。

理由不是潔癖，是三件很實際的事：

1. **可達性驗證器**要在沒有 Unity 執行環境的情況下，幾秒內模擬幾千條路徑。邏輯綁在 MonoBehaviour 上就辦不到。D11 封閉經濟下驗證器是生命線，這條理由的權重是三條裡最高的。
2. 數值測試可以用一般 NUnit 跑，不需要 PlayMode（PlayMode 測試慢到你不會想跑）。
3. **傷害預覽**和實戰必須跑同一個函式——邏輯層純粹，預覽才永遠不會騙人。

具體做法：把邏輯放進一個獨立的 asmdef，**不引用 UnityEngine**。編譯器會幫你守住這條線。

**這條線的必然推論：Core 看不見 ScriptableObject**（SO 就是 UnityEngine）。所以邊界上需要一層轉換——每個 SO 定義配一個純 C# 的資料型別，SO 只負責在 Editor 裡被編輯，進入遊戲時由 Bootstrap 轉成 POCO 餵給 Core：

```
MonsterDefinitionSO (UnityEngine, Data/)          // 設計師編輯的資產
    └─ .ToDefinition() → MonsterDefinition (純 C#, Core/)   // Core 唯一認得的形式
```

轉換方向永遠是 SO → POCO、發生在載入時、一次做完。Core 的任何型別出現 `using UnityEngine` 就是走錯了。這層看起來是重複程式碼，實際上是整個架構成立的前提——**可達性驗證器**跑在 Unity 之外，它只吃得下 POCO。

---

## 專案結構

功能導向（feature-based），不是類型導向。不要出現 `Scripts/Managers/`、`Scripts/Utils/` 這種按「它是什麼」分的資料夾——要按「它屬於哪個功能」分。

```
Assets/
  _Project/
    Core/                       # 純 C#，asmdef 不引用 UnityEngine
      Grid/                     # 格子、樓層網格、路徑
      Combat/                   # CombatResolver、傷害公式、戰鬥狀態機
      Progression/              # 屬性、成長、裝備計算
      Save/                     # 存檔資料模型與序列化（POCO）
      Simulation/               # 可達性驗證器 + Boss 壓力測試的無畫面模擬
    Features/                   # MonoBehaviour 層，一個功能一個資料夾
      FloorExploration/         # 方向鍵輸入、樓層渲染、互動
      CollisionBattle/          # 碰撞戰表現（數字跳動、震動回饋）
      Bestiary/                 # 怪物手冊、傷害預覽
      Shop/                     # 商店與祭壇（金幣買道具、經驗買屬性）
      Dialogue/
      FloorMap/                 # 樓層地圖、樓傳介面、安全區點擊傳送
      SaveLoad/                 # 快照、自動存檔、雲端同步
    Data/                       # ScriptableObject 資產與其定義
      Monsters/                 # 數值 + 怪物特性組合
      Items/                    # 含回溯道具
      Floors/
    UI/                         # 共用 UI 元件、字級與安全區處理
    Bootstrap/                  # 組裝根：建立服務、注入相依
  Editor/
    LevelEditor/                # 樓層編輯器
    DataPipeline/               # 試算表 → ScriptableObject 匯入
    SolverRunner/               # 批次跑可達性驗證器
```

`_Project` 前綴讓自家程式在 Project 視窗永遠排在第三方套件之上。

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

**怪物特性**（先攻、連擊、魔攻、吸血…）是 `DamageFormula` 的輸入參數，不是散落在各處的 if——新增一個特性 = 擴充公式的一個結算規則 + 資料表一個欄位。特性一律確定性，禁止機率（D1 衍生規則，機率會毀掉傷害預覽）。

`ResolveCollision` 必須無副作用：吃狀態進去、吐結果出來、不改任何東西。因為**傷害預覽**就是直接呼叫它——預覽和實戰跑的是同一個函式，所以預覽永遠不會騙人。**守關怪**也走同一條路——只是數值大、特性組合兇的怪物，沒有專屬程式路徑。

公式的紙上暫定版（含特性修正規則與完整算例）在 [`boss-test-8f.md`](boss-test-8f.md)——Core 動工時以它為起點鎖定公式，其精算表全部數字必須被 `DamageFormula` 的單元測試重現。

### 2. 狀態變更走指令模式；存檔是快照 + 指令流

D7 要求**步數回溯**，這決定了狀態管理的形狀：所有改變 `GameState` 的操作——移動、開門、碰撞戰、購買——都是一個 `IGameCommand`，帶 `Apply` 與 `Undo`。**這必須從 `Core` 第一天就做，事後補裝等於重寫。**

```
IGameCommand
  ├─ Apply(GameState) → GameState
  └─ Undo(GameState)  → GameState

SaveFile
  ├─ snapshots      : Map<FloorId, GameState>   // 每層入口的快照（外層防軟鎖）
  ├─ currentFloor   : FloorId
  └─ commandsSince  : List<IGameCommand>        // 入口快照之後的指令流（內層防軟鎖）
```

這個結構讓三件事變成同一件事：

- **回溯** = 從指令流尾端 pop command 執行 `Undo`——但入口在遊戲層：先檢查並消耗一顆**回溯道具**（D7），Core 只提供機制，不管收費
- **當前狀態** = 入口快照 + 重放指令流（存檔裡甚至不用存 currentState）
- **退回樓層入口** = 丟掉指令流（免費，D7 外層）

**快照完整性規則**：退回 N 層入口時，**所有晚於該快照的快照一併作廢**（時間軸只有一條）。否則玩家可以退回 5F 重新配置資源，再「跳回」9F 的舊快照，兩個時間線的資源憑空疊加——這是套利漏洞，不是防軟鎖。實作上快照帶單調遞增的序號，回退即截斷。

一場碰撞戰 = **一個** command（`ResolveCollision` 的結果打包進指令流），所以回溯一步就是回溯一整場戰鬥，語義乾淨。

檔案大小可控：`GameState` 是純數值與 flag，一層幾 KB；指令流在寫入新樓層快照時清空，不會無限長。**不要**把樓層地圖本身存進去——那是靜態資料，從 ScriptableObject 讀。

### 3. 戰鬥不載入場景

**整個遊戲只有一個 gameplay 場景。** 碰撞戰直接在地圖上播表現（數字跳動 + 震動回饋），Boss 戰頂多加一段短演出（鏡頭推近、特性圖示展示），仍在同一場景。

理由：場景載入在中低階 Android 上是 0.5–2 秒。魔塔類型一場遊戲會發生數百次戰鬥，載入一次就毀掉節奏。D1 砍掉指令戰後，這條規則沒有任何例外了。

---

## ScriptableObject 的使用界線

**要用**：怪物、道具、技能、樓層佈局——所有**設計師調整、執行期唯讀**的資料。

**不要用**：執行期會變的狀態。SO 在 Editor 裡的修改會被持久化，用它存玩家 HP 會讓你在測試時莫名其妙地「繼承」上一輪的數值——這是 Unity 專案最常見的除錯地獄之一。執行期狀態一律放純 C# 物件。

**匯入管線**：數值在試算表調，不在 Inspector。`Editor/DataPipeline/` 讀 CSV/TSV 產生 SO 資產。這在你有 200 隻怪之後會救你一命——沒有它，平衡調整就是 200 次手動點擊。

---

## 相依注入

**需要，但不需要框架。**

Zenject / VContainer 對這個規模的專案是過度工程。你只需要一個 `Bootstrap` 場景，在裡面手動 new 出所有服務、串好、丟給需要的人：

```csharp
// SO → POCO 的轉換就發生在這裡，Core 從頭到尾看不見 UnityEngine
var balance   = balanceConfigSO.ToConfig();          // POCO
var monsters  = monsterDatabaseSO.ToDefinitions();   // POCO
var resolver  = new CombatResolver(new DamageFormula(balance));
var saveService = new SaveService(Application.persistentDataPath);
var game = new GameSession(resolver, saveService, monsters);
```

好處是相依關係**看得見**——你打開 Bootstrap 就知道整個系統長怎樣。框架會把這件事藏進 attribute 裡。等專案真的複雜到手動組裝很痛的時候再換，那時候你也才知道自己需要什麼。

**絕對不要用 singleton / static manager。** 它會讓**可達性驗證器**沒辦法平行跑多個模擬。

---

## 效能

這類型的效能問題跟一般手遊不同——你的瓶頸**不在 500 隻怪同時動**（它們是回合制的，不會同時動）。真正的風險：

1. **每格一個 GameObject** — 一層 20×20 = 400 個 GameObject，切樓層時全部銷毀重建。用 Tilemap 或物件池，不要每次 Instantiate。
2. **UI 重建** — 怪物手冊、背包這類長列表要用虛擬化列表，不要一次生成 200 個 item。
3. **存檔序列化** — 每次進樓層都寫快照。用 JSON 就好，但**寫在背景執行緒**，不要卡住樓層轉場。
4. **Addressables** — MVP 階段不需要。10–15 層的資產全部打進 build 就好。等內容量真的撐不下再導入，過早導入只會讓你在除錯載入問題上浪費時間。

---

## 先做什麼

順序不是隨便排的——每一步都是為了讓下一步能被驗證：

1. **`Core/Grid` + `Core/Combat`（含 DamageFormula、怪物特性）+ `IGameCommand`（純 C#，含測試）** — 沒有畫面，但規則對了。指令模式必須在這一步就進來（D7），不能事後補
2. **`Simulation` + 可達性驗證器** — 現在你能自動驗證樓層可不可解。D1 純碰撞戰 + D11 封閉經濟下，驗證器覆蓋全塔含 Boss。先做**每層獨立驗證**（入口預算 → 出口預算），全塔只做資源總量守恆檢查——不要一開始就挑戰全塔搜索，那是狀態空間爆炸的地方
3. **`FloorExploration` 最小可玩版** — 一層地圖、方向鍵移動、碰撞戰、鑰匙門
4. **`Bestiary` + 傷害預覽 + 回溯道具** — 到這裡遊戲才真的「可玩」。回溯的 UI 很小，但它背後的指令流在第 1 步已經就緒
5. **存檔：樓層快照 + 指令流序列化 + 祭壇**
6. **關卡編輯器** — 在手工做超過 5 層之前一定要有。一人開發（D12）下，它的產能直接決定 D6 的 25–30 層是否成立
7. **10–15 層內容 + 難度弧線**（MVP＝D3；出貨版 25–30 層＝D6，在編輯器成熟後才擴產）

第 7 步完成後有一個**複審點**：碰撞戰魔塔本身夠不夠好玩、編輯器產能撐不撐得起 25–30 層。D6 的規模與美術方案（CONTEXT.md 待決）都在這裡拍板。

