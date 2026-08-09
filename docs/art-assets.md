# 美術素材 — 像素風（D14）

**素材不進版控**（第三方授權品，`.gitignore` 已排除 `art/`）。本文件記錄**來源、切法、對應表**，任何人照著就能重建。

## 來源與重建

本機來源：`G:\圖片放置\魔塔`（策展層 86 檔＋`素材整包/` 完整 RPG Maker 素材庫＋`bgm/` 41 個音效）

```
powershell -File tools/slice-sheets.ps1 -SourceDir "G:\圖片放置\魔塔" -OutputDir art\pixel\raw
```

策展層檔案一律 **128×128 = 4×4 的 32px 格**（列＝變體、行＝動畫幀）。切片產出 1454 格，命名 `<表名>_r<列>_c<行>.png`。

再依下方對照表複製到 `assets/sprites/`（Godot 以 `res://assets/sprites/` 讀取；該資料夾不進版控）。命中爆閃另由 `tools/make-fx.ps1` 產生 8 幀。

## 換素材的唯一接觸點

`game/SpriteMap.cs` 是**全遊戲唯一知道檔名的地方**。遊戲邏輯只講概念 id；換整套素材＝改那一個檔 + 換 `sprites/` 內容，邏輯零改動。這是 D14 授權風險的緩解設計。

## 對照表

### 地形（懷舊配色 A — 複製原版的紫框灰地）

| sprite id | 切片 | 說明 |
|---|---|---|
| `tile_floor` | `Wall_r0_c3` | 灰石磚地板 |
| `tile_wall` | `地形_r0_c3` | 紫藍石牆——原版外框的顏色 |

其他配色候選（渲染比對過）：紫框淡紫地 `Wall_r0_c1`／灰地深灰牆 `IronFloor_r2_c0`／淡紫地棕牆 `地形_r0_c4`＋`地形_r0_c0`。

### 門與樓梯

| sprite id | 切片 |
|---|---|
| `ent_door_y` / `ent_door_b` / `ent_door_r` | `Door_r0_c0` / `Door_r0_c1` / `Door_r1_c0` |
| `ent_stairs_up` / `ent_stairs_down` | `Statir_r0_c0` / `Statir_r0_c3` |

### 道具

| sprite id | 切片 |
|---|---|
| `item_key_y` / `item_key_b` / `item_key_r` | `Key_r0_c0` / `Key_r1_c1` / `Key_r0_c2` |
| `item_potion_s` / `item_potion_l` | `Potion_r0_c0` / `Potion_r2_c0` |
| `item_gem_atk` / `item_gem_def` | `MagicGems_r0_c0` / `MagicGems_r0_c1` |
| `item_hourglass` | `Constant _r1_c0` |

### 怪物（15 種，皆取 `c0` 靜態幀；`c1`–`c3` 為待用動畫幀）

| sprite id | 切片 | | sprite id | 切片 |
|---|---|---|---|---|
| `mon_slime_g` | `Slime_r0_c0` | | `mon_duelist_twin` | `Swordsman_r0_c0` |
| `mon_slime_r` | `Slime_r1_c0` | | `mon_mage_void` | `Majician_r0_c0` |
| `mon_slime_b` | `Slime_r2_c0` | | `mon_ghost_pale` | `Mask_r0_c0` |
| `mon_bat_cave` | `Bat_r0_c0` | | `mon_vampbat_king` | `Bat_r2_c0` |
| `mon_wasp_striker` | `Bat_r1_c0` | | `boss_gate_01` | `King_r0_c0` |
| `mon_skel_gray` | `skeleton_r0_c0` | | `boss_warden_10` | `Guard_r0_c0` |
| `mon_skel_soldier` | `skeleton_r1_c0` | | | |
| `mon_rat_giant` | `Zombie_r0_c0` | | | |
| `mon_bandit` | `Kinght_r0_c0` | | | |

### 互動點與主角

| sprite id | 切片 |
|---|---|
| `ent_shop` / `ent_altar` / `ent_switch` | `Merchat_r0_c0` / `MagicGems_r3_c1` / `Constant _r0_c0` |
| `npc_guard_old` | `Guard_r1_c0` |
| `hero_d{0-3}_f{0-3}` | `hero_r{0-3}_c{0-3}` |

**主角列序**（RPG Maker 慣例）：0 下／1 左／2 右／3 上，各 4 幀行走動畫。

## 渲染要求（不可省）

像素素材必須用 **Nearest filter**，否則整套糊掉。專案層級已設於 `project.godot`（`default_texture_filter=0`），節點層級另由 `ViewFactory` 對每個 Sprite2D/Label 指定。

## 未做

- 音效：9 個事件音已接入（`assets/audio/`，對照表在 `game/AudioBank.cs`）。來源 `bgm/` 內的**新新魔塔**系列音效——鐵劍平A／暴擊／金幣／系統音，正是本類型的聲音。BGM 尚未接入
- 怪物待機動畫：每隻的 `c1`–`c3` 幀已切好，尚未接
- 舊的手繪厚塗素材（38 張）：D14 前的路線，已停用

## 授權

**上架前必須確認商用授權範圍**（見 `CONTEXT.md` 待決事項）。若不可商用，換素材成本已被 `SpriteMap` 壓到最低。
