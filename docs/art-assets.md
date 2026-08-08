# 美術素材清單 — MVP 1F–10F（生圖規格）

給素材蒐集/AI 生圖用的完整清單。**檔名 = 表裡的 sprite id**（`.png`），跟 `data-schema.md` 的 `sprite` 欄位一一對應——圖生好、照檔名放進來，管線直接吃。

## 全域規格（每一張都適用）

| 項目 | 規格 |
|---|---|
| 尺寸 | **1024×1024 源檔**（遊戲內縮放；13 欄直向下每格實際約 80px，源檔留高解析度給圖鑑放大用） |
| 格式 | PNG，**透明背景**（生成器做不到透明就用純白底，之後批次去背） |
| 構圖 | 單一主體、置中、佔畫面約 80%，**正面朝向鏡頭**（俯視格子遊戲的標準朝向） |
| 風格一致性 | **所有圖共用同一段風格前綴詞**——先寫定一句（例：`pixel art, 32-bit, clean outline, vibrant colors` 或 `hand-painted fantasy, soft shading`），之後每張 prompt 都以它開頭。風格選哪種是你的美術決定，**一致性比風格本身重要十倍** |
| 禁止 | 圖內不要有文字、浮水印、外框、陰影落在透明區之外 |

建議的 prompt 骨架：`〔風格前綴〕, 〔主體描述〕, single character, front facing, centered, plain background, no text`

---

## 1. 怪物（14 張）— 同時定案 1F–10F 怪物名單

史萊姆三色是同型換色（分開生三張即可，prompt 只改顏色）。守關怪兩張畫大隻一點、細節多一點——牠們要撐 Boss 演出的鏡頭推近。

| sprite id | 名稱 | 樓層 | 特性 | 生成描述（接在風格前綴後） |
|---|---|---|---|---|
| `mon_slime_g` | 綠史萊姆 | 1F | — | small green slime blob, cute but menacing, glossy |
| `mon_bat_cave` | 洞穴蝙蝠 | 1F | — | small purple cave bat, spread wings, fangs |
| `mon_skel_gray` | 小骷髏 | 1F | — | small gray skeleton warrior, rusty short sword |
| `mon_slime_r` | 紅史萊姆 | 2F | — | small red slime blob, angry expression, glossy |
| `mon_rat_giant` | 大老鼠 | 2F | — | giant brown rat, hunched, long tail, sharp teeth |
| `mon_slime_b` | 藍史萊姆 | 3F | — | small blue slime blob, calm expression, glossy |
| `mon_bandit` | 強盜 | 3F | — | masked human bandit, leather armor, dagger, coin pouch |
| `mon_skel_soldier` | 骷髏兵 | 4F | — | armored skeleton soldier, shield and sword, battle stance |
| `mon_ghost_pale` | 蒼白幽魂 | 4F | — | pale floating ghost, translucent, wispy trails |
| `mon_wasp_striker` | 刺蜂 | 5F | 先攻 | giant hornet, needle stinger forward, aggressive dive pose |
| `mon_duelist_twin` | 雙刀鬥士 | 6F | 連擊 | lean humanoid duelist with two curved blades, crossed |
| `mon_mage_void` | 虛空法師 | 7F | 魔攻 | hooded mage, dark purple void energy between hands |
| `boss_gate_01` | 門衛雙足獸 | 8F | 先攻+連擊（守關） | massive bipedal beast guarding a gate, heavy claws, battle scars, imposing |
| `mon_vampbat_king` | 吸血蝠王 | 9F | 吸血 | large crimson vampire bat, blood-red eyes, regal crown-like ears |
| `boss_warden_10` | 塔層守衛 | 10F | 佔位（守關） | towering stone-and-metal construct warden, glowing core, ancient runes |

> 名單即怪物 roster 定案：**15 種**（含兩隻守關怪）。每層 2–3 種可用（含前層續用），數值之後在 monsters.csv 錨定初始值 10/10/550 來調。

## 2. 道具（8 張）

| sprite id | 名稱 | 生成描述 |
|---|---|---|
| `item_key_y` | 黃鑰匙 | simple brass yellow key |
| `item_key_b` | 藍鑰匙 | ornate blue crystal key |
| `item_key_r` | 紅鑰匙 | elaborate red ruby key, regal |
| `item_potion_s` | 小血瓶 | small round red potion bottle |
| `item_potion_l` | 大血瓶 | large ornate red potion flask |
| `item_gem_atk` | 攻擊寶石 | red sword-shaped gemstone, glowing |
| `item_gem_def` | 防禦寶石 | blue shield-shaped gemstone, glowing |
| `item_hourglass` | 沙漏 | magical golden hourglass, swirling time sand |

## 3. 地形與結構（9 張）

| sprite id | 名稱 | 生成描述 |
|---|---|---|
| `tile_floor` | 地板 | stone dungeon floor tile, seamless, top-down |
| `tile_wall` | 牆 | stone brick wall block, top-down dungeon |
| `tile_oneway` | 單向箭頭 | glowing floor arrow marker（**一張即可**，遊戲內旋轉出四方向） |
| `ent_door_y` | 黃門 | closed yellow wooden dungeon door with lock |
| `ent_door_b` | 藍門 | closed blue reinforced dungeon door with lock |
| `ent_door_r` | 紅門 | closed grand red door, ornate ruby lock |
| `ent_stairs_up` | 上樓梯 | stone stairs going up, top-down view |
| `ent_stairs_down` | 下樓梯 | stone stairs going down, top-down view |
| `ent_switch` | 開關 | floor lever switch, metal base |

地板與牆是**鋪滿格子的方塊**（不留透明邊），其他照全域規格置中留透明。

## 4. 互動點與人物（6 張）

| sprite id | 名稱 | 生成描述 |
|---|---|---|
| `ent_shop` | 商人 | hooded merchant behind small stall, coins and wares |
| `ent_altar` | 祭壇 | glowing stone altar, floating runes |
| `npc_guard_old` | 老守衛 | elderly tower guard NPC, lantern, weathered armor（8F 對話用） |
| `hero_down` | 主角（面向下） | young adventurer, light armor, facing viewer |
| `hero_up` | 主角（背面） | same adventurer, seen from behind |
| `hero_side` | 主角（側面） | same adventurer, side profile（**一張即可**，遊戲內鏡像出左右） |

主角三張務必**同一次生成流程/同一 seed 風格**，不然走路轉向會像換了人。

---

## 合計與優先序

**37 張**。生成順序建議：

1. **先生 1 張測試**（`mon_slime_g`）→ 確定風格前綴詞 → 這句話就是全塔的美術憲法
2. 主角 3 張 + 1F 三隻怪 + 地板/牆/黃門/黃鑰匙/樓梯 → **湊齊第 3 步「最小可玩版」的全部素材**
3. 其餘照樓層順序補

生好的圖先集中放一個資料夾（檔名照表），Unity 專案建好後放 `Assets/_Project/Art/`。

## 待補（現在不用生）

戰鬥數字跳動、UI 按鈕/框、樓層地圖圖標、App icon、商店頁截圖——等可玩版出來再說。
