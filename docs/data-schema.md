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
| `traits` | string | 特性組合，`\|` 分隔，空 = 無特性。合法值：`first_strike`（先攻）`multi_hit`（連擊）`pierce`（魔攻）`lifesteal`（吸血） | `first_strike\|multi_hit` |
| `gold_drop` | int | 擊殺金幣（D11 封閉經濟，總量設計時定死） | 200 |
| `exp_drop` | int | 擊殺經驗 | 250 |
| `is_guardian` | bool | 守關怪旗標——驗證器對它執法兩條合約（預覽必死＋零傷不可達），表現層加短演出 | TRUE |
| `sprite` | string | 素材 id（你找圖後對應） | `boss_gate_01` |
| `bestiary_note` | string | 怪物手冊補充文字（可空） | 「先制的那次也是雙擊」 |

範例列（1F 起步怪）：`slime_green, 綠史萊姆, 12, 6, 40, , 3, 5, FALSE, mon_slime_g, `

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
  "id": "F08",
  "width": 13, "height": 13,          // 直向螢幕，建議寬 ≤ 13
  "tiles": [ /* 地形二維陣列：floor | wall | oneway_n/s/e/w */ ],
  "entities": [
    { "eid": "F08_m01", "type": "monster", "ref": "gatekeeper_biped", "x": 6, "y": 10 },
    { "eid": "F08_d01", "type": "door",    "tier": "red",  "x": 6, "y": 12 },   // 8F 展示紅門
    { "eid": "F08_i01", "type": "item",    "ref": "potion_s", "x": 2, "y": 4 },
    { "eid": "F08_s01", "type": "stairs",  "dir": "up",   "x": 6, "y": 11 },
    { "eid": "F08_sw1", "type": "switch",  "targets": ["F09_d02"] },            // 跨層結構用
    { "eid": "F08_sh1", "type": "shop",    "ref": "shop_f03" },
    { "eid": "F08_a01", "type": "altar",   "ref": "altar_std" },
    { "eid": "F08_n01", "type": "npc",     "dialogue": "dlg_f08_hint" }
  ]
}
```

- `eid` 全塔唯一——`GameState` 用它記錄「已擊殺/已拾取/已開門/已觸發」，回溯與封閉經濟清點都靠它
- 門是實體不是地形：開門是一個 `IGameCommand`
- `tiles` 只有三種地形：可走、牆、單向（含方向）——其他一切是實體

## 4. shops.csv / altars.csv — 商店與祭壇

```
shops.csv:  shop_id, item_id, base_price, price_step     // 遞增價：第 n 次購買 = base + (n−1)×step
            shop_f03, potion_s, 80, 20
            shop_f03, key_yellow, 50, 25

altars.csv: altar_id, stat, exp_cost, gain               // 祭壇曲線待決：先用固定價，欄位預留 cost_step
            altar_std, atk, 20, 1
            altar_std, def, 20, 1
            altar_std, hp,  20, 50
```

## 5. balance.csv — 全域參數

```
player_start_atk, 10
player_start_def, 10
player_start_hp,  400
player_start_gold, 0
player_start_exp,  0
snapshot_on_enter, TRUE
```

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

## 開放給你確認的點

1. **樓層尺寸 13×13** 是直向螢幕下的建議上限，經典魔塔是 11×11——要哪個？
2. **大血瓶（potion_l）** 是我加的第二階，經典魔塔紅/藍瓶兩階——要不要？
3. **玩家初始值** `10/10/400` 是佔位，1F 怪物數值會跟著它定
4. 沙漏 `undo_steps=5` 佔位，待決事項照舊
