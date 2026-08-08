# 資料欄位設計 — 試算表 / 樓層 JSON / Core POCO（草案，待使用者確認）

數值管線的形狀：**試算表（CSV）→ DataPipeline 匯入 → ScriptableObject → Bootstrap 轉 POCO → Core**。
設計師（你）只碰 CSV 和關卡編輯器，不碰 Inspector。

一條總規則：**格子只放地形，會變的東西全是實體（entity）**——實體才有 id，才能被 `IGameCommand` 記錄與回溯、被封閉經濟（D11）逐一清點。

---

## 1. monsters.csv — 怪物表

| 欄位 | 型別 | 說明 | 範例（門衛雙足獸） |
|---|---|---|---|
| `id` | string | 唯一識別，英文蛇底式 | `gatekeeper_biped` |
| `name_zh` | string | 顯示名 | 門衛雙足獸 |
| `atk` | int | 攻擊 | 30 |
| `def` | int | 防禦 | 24 |
| `hp` | int | 生命 | 300 |
| `traits` | string | 特性組合，`\|` 分隔，空 = 無特性。合法值：`first_strike`（先攻）`multi_hit`（連擊）`pierce`（魔攻）`lifesteal`（吸血）。**支援可選參數** `名稱:值`——`multi_hit` 預設 2，`multi_hit:3` 即三連擊；`lifesteal` 預設 100%。MVP 四特性都用預設值，參數語法是為 11F+ 新特性預留的，**匯入器第一天就支援**，之後不用改資料格式 | `first_strike\|multi_hit` |
| `gold_drop` | int | 擊殺金幣（D11 封閉經濟，總量設計時定死） | 200 |
| `exp_drop` | int | 擊殺經驗 | 250 |
| `is_guardian` | bool | 守關怪旗標——驗證器對它執法兩條合約（預覽必死＋零傷不可達），表現層加短演出 | TRUE |
| `sprite` | string | 素材 id（你找圖後對應） | `boss_gate_01` |
| `bestiary_note` | string | 怪物手冊補充文字（可空） | 「先制的那次也是雙擊」 |

範例列（1F 起步怪）：`slime_green, 綠史萊姆, 12, 6, 40, , 3, 5, FALSE, mon_slime_g, `

**怪物沒有執行期狀態。** 碰撞戰即刻結算，怪物不存在「打到一半」——存活與否全由 `GameState.ConsumedEids` 一個集合表達。這是本 schema 最大的簡化來源：不需要任何 per-monster 的 HP 存檔欄位。

## 2. items.csv — 道具表

稀疏欄位設計：每列只填用得到的欄，其他留空——試算表好讀，匯入器好寫。

| 欄位 | 型別 | 說明 |
|---|---|---|
| `id` | string | 唯一識別 |
| `name_zh` | string | 顯示名 |
| `category` | enum | `key` / `potion` / `gem` / `undo` |
| `key_tier` | enum | `yellow` / `blue` / `red`（僅 key） |
| `heal_hp` | int | 回復量（僅 potion；HP 無上限，純加法） |
| `atk_bonus` / `def_bonus` | int | 永久加成（僅 gem） |
| `undo_steps` | int | 每顆回退步數（僅 undo；**數值待決**，先填 5 佔位） |
| `sprite` | string | 素材 id |

預設八件套：

```
key_yellow  黃鑰匙   key    yellow
key_blue    藍鑰匙   key    blue
key_red     紅鑰匙   key    red
potion_s    小血瓶   potion  heal_hp=150
potion_l    大血瓶   potion  heal_hp=400
gem_atk     攻擊寶石 gem    atk_bonus=2
gem_def     防禦寶石 gem    def_bonus=2
hourglass   沙漏     undo   undo_steps=5(佔位)
```

## 3. floors/*.json — 樓層資料（關卡編輯器產物）

```jsonc
{
  "schema": 1,                          // 格式版本——未來遷移的保命欄位
  "id": "F08",
  "width": 13, "height": 13,           // 已定案：全塔固定 13×13
  "tiles": [                            // 地形用字元列編碼：一列一字串，git diff 直接可讀
    "WWWWWWWWWWWWW",                    //   W=牆  .=可走  ^v<>=單向（箭頭即通行方向）
    "W.....^.....W",
    "W...........W"
    // ... 共 13 列
  ],
  "entities": [
    { "eid": "F08_m01", "type": "monster", "ref": "gatekeeper_biped", "x": 6, "y": 10 },
    { "eid": "F08_d01", "type": "door",    "tier": "red",  "x": 6, "y": 12 },   // 8F 展示紅門
    { "eid": "F08_i01", "type": "item",    "ref": "potion_s", "x": 2, "y": 4 },
    { "eid": "F08_s01", "type": "stairs",  "dir": "up",   "x": 6, "y": 11 },
    { "eid": "F08_sw1", "type": "switch",  "targets": ["F09_d02"], "x": 3, "y": 3 },  // 跨層結構用
    { "eid": "F08_sh1", "type": "shop",    "ref": "shop_f03", "x": 10, "y": 2 },
    { "eid": "F08_a01", "type": "altar",   "ref": "altar_std", "x": 2, "y": 10 },
    { "eid": "F08_n01", "type": "npc",     "dialogue": "dlg_f08_hint", "x": 8, "y": 5 }
  ]
}
```

- `eid` 全塔唯一——`GameState` 用它記錄「已擊殺/已拾取/已開門/已觸發」，回溯與封閉經濟清點都靠它
- **`eid` 由關卡編輯器自動生成**（格式 `<樓層>_<類型><流水號>`），人手永不編輯——手打 eid 是撞號與 dangling reference 的頭號來源
- 門是實體不是地形：開門是一個 `IGameCommand`
- `tiles` 只有三種地形：可走、牆、單向——其他一切是實體。字元列編碼一格一字元，13×13 = 13 個字串，人眼可直接在 JSON 裡看出樓層形狀

## 4. shops.csv / altars.csv — 商店與祭壇

```
shops.csv:  shop_id, item_id, base_price, price_step     // 遞增價：第 n 次購買 = base + (n−1)×step
            shop_f03, potion_s, 80, 20
            shop_f03, key_yellow, 50, 25

altars.csv: altar_id, stat, exp_cost, gain, cost_step    // 祭壇曲線待決：cost_step 先填 0（固定價），定案後改數字即可，不動格式
            altar_std, atk, 20, 1, 0
            altar_std, def, 20, 1, 0
            altar_std, hp,  20, 50, 0
```

遞增計數的鍵格式：`PurchaseCounts["<shop_id>:<item_id>"]`（祭壇同款：`"<altar_id>:<stat>"`）——每間店、每個品項獨立計數，兩間商店賣同款血瓶不互相抬價。

## 5. balance.csv — 全域參數

```
player_start_atk,   10
player_start_def,   10
player_start_hp,    550        // 已定案（原 400 佔位）
player_start_gold,  0
player_start_exp,   0
player_start_keys_yellow, 0
player_start_keys_blue,   0
player_start_keys_red,    0
```

（原草案的 `snapshot_on_enter` 已移除——進樓層必快照是 D7 規則，不是可調參數，放進平衡表只會誘惑人關掉它。）

## 5b. dialogues.csv — 對話文本

```
dialogues.csv: id, speaker_zh, text_zh
               dlg_f08_hint, 老守衛, 那扇紅門後面的東西，值得你留到最後一把鑰匙。
```

MVP 對話是單句/短序列（同 id 多列 = 依序播放），不做分支樹——NPC 在本作是提示與敘事，不是任務系統（任務門是 11F+ 的事）。所有玩家可見文字集中在 `*_zh` 欄，日後本地化時整欄翻譯即可。

## 6. Core POCO（欄位對應，實作時的形狀）

```csharp
record MonsterDefinition(string Id, int Atk, int Def, int Hp,
                         TraitSet Traits, int GoldDrop, int ExpDrop, bool IsGuardian);

[Flags] enum TraitSet { None=0, FirstStrike=1, MultiHit=2, Pierce=4, Lifesteal=8 }

record CollisionOutcome(bool Winnable, int ExpectedLoss, int Rounds);
// Winnable=false ⇔「無法戰勝」——打不動或吸血淨削減 ≤ 0，UI 不顯示數字

class GameState {
  PlayerStats Player;            // atk, def, hp
  int Gold, Exp;
  int KeysYellow, KeysBlue, KeysRed, Hourglasses;
  FloorId CurrentFloor; GridPos Position;
  HashSet<string> ConsumedEids;  // 已擊殺/已拾取/已開門/已觸發（eid）
  Dictionary<string,int> PurchaseCounts;  // 商店遞增價的計數（祭壇曲線若定遞增，同款欄位）
}
```

---

## 匯入器驗證（DataPipeline 的守門規則）

匯入時任何一條不過 = 匯入失敗、指出行號，**不產生半套資料**：

1. **參照完整性**：floor JSON 的 `ref` 必須存在於對應 CSV；`switch.targets` 的 eid 必須存在（跨層 dangling reference 是跨層結構的頭號事故）
2. **eid 唯一性**：全塔掃描，撞號即失敗
3. **特性合法性**：`traits` 只接受詞彙表列出的名稱與參數格式（擋 typo——`first_stirke` 這種錯在匯入期抓，不是在玩家手機上）
4. **地形字元合法性**：tiles 只接受 `W . ^ v < >`，每層恰好 13×13
5. **結構最低要求**：每層至少一個樓梯；`is_guardian` 怪物所在樓層記入守關清單，供驗證器跑兩條合約

## 已確認紀錄（2026-08-08）

- 樓層尺寸 **13×13 定案**（全塔固定，編輯器與驗證器都按此寫死）
- 大血瓶保留（150/400 兩階）
- 玩家初始 HP **550**（攻/防 10/10 維持佔位，1F 怪物數值錨定於此）
- 沙漏 `undo_steps` 續留待決，資料先填 5 佔位
