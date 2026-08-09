using System;
using System.Linq;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Data;
using Tower.Core.Floors;
using Tower.Core.Grid;
using Tower.Core.Save;

namespace Tower.Verify
{
    /// <summary>
    /// 存檔驗收（D7）。存檔的價值全在「還原得精確」——差一點就是玩家的進度壞掉，
    /// 而且是那種**要玩很久才會發現**的壞法，所以這裡驗得比別處嚴。
    /// </summary>
    internal static class SaveChecks
    {
        public static void Run(Catalog catalog, Action<string, bool> check)
        {
            Console.WriteLine("== 存檔（D7）==");

            BestiaryIsKnowledgeNotResource(catalog, check);
            ShopAndAltarRoundTrip(catalog, check);
            RoundTrip(catalog, check);
            UndoIsExact(catalog, check);
            SingleTimeline(check);
            FloorEntryClearsStream(check);
        }

        /// <summary>
        /// 怪物手冊是**知識不是資源**——D7「所有狀態變更都是指令」的唯一例外。
        /// 回溯一步不該讓玩家「忘記」看過的怪；退回樓層入口也不該。
        /// 這條刻意的例外必須被證明，否則日後有人會「順手修正」成走指令模式。
        /// </summary>
        private static void BestiaryIsKnowledgeNotResource(Catalog catalog, Action<string, bool> check)
        {
            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000 });
            save.EnterFloor("F00");
            save.State.SeenMonsters.Add("slime_green");

            var m = catalog.Monsters["slime_green"];
            save.Apply(new CollisionBattleCommand("F00_m01",
                CombatResolver.ResolveCollision(save.State.CombatStats, m), m));

            save.UndoOne();
            check("回溯不會抹掉手冊（知識只增不減）", save.State.SeenMonsters.Contains("slime_green"));

            save.EnterFloor("F01");
            save.State.SeenMonsters.Add("bat_cave");
            save.RevertToFloor("F00");
            check("退回樓層也不抹掉手冊",
                save.State.SeenMonsters.Contains("slime_green"));

            var loaded = SaveGame.FromData(SaveData.FromJson(save.ToData().ToJson()));
            check($"手冊進得了存檔（{loaded.State.SeenMonsters.Count} 筆）",
                loaded.State.SeenMonsters.SetEquals(save.State.SeenMonsters));

            check("怪物都有手冊註記（bestiary_note 有讀進 POCO）",
                catalog.Monsters.Values.Count(x => !string.IsNullOrEmpty(x.BestiaryNote)) >= 14);
        }

        /// <summary>
        /// 商店與祭壇：遞增價、回溯、存讀往返。
        ///
        /// 新增指令型別時最危險的漏洞是**忘了加進 CommandCodec**——存檔會靜默漏掉那一步，
        /// 玩家讀檔後發現買的東西不見了。這條檢查就是為了讓那件事變成紅燈。
        /// </summary>
        private static void ShopAndAltarRoundTrip(Catalog catalog, Action<string, bool> check)
        {
            check($"shops.csv {catalog.Shops.Count} 家、altars.csv {catalog.Altars.Count} 座",
                catalog.Shops.Count >= 1 && catalog.Altars.Count >= 1);

            var shop = catalog.Shops["shop_f03"];
            var offer = shop.Offers.First(o => o.ItemId == "key_yellow");
            check($"遞增價：第 1 次 {offer.PriceAt(0)}、第 2 次 {offer.PriceAt(1)}、第 3 次 {offer.PriceAt(2)}",
                offer.PriceAt(0) == 50 && offer.PriceAt(1) == 75 && offer.PriceAt(2) == 100);

            var altar = catalog.Altars["altar_std"];
            var atk = altar.Offers.First(o => o.Stat == AltarStat.Atk);
            check($"祭壇遞增價、各屬性獨立計數：攻第 1 次 {atk.CostAt(0)}、第 2 次 {atk.CostAt(1)}",
                atk.CostAt(0) == 20 && atk.CostAt(1) == 25);

            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000, Gold = 300, Exp = 100 });
            save.EnterFloor("DEV_SETTINGS");
            var before = save.State.Clone();

            save.Apply(new PurchaseCommand(shop.Id, catalog.Items[offer.ItemId], offer.PriceAt(0)));
            save.Apply(new PurchaseCommand(shop.Id, catalog.Items[offer.ItemId], offer.PriceAt(1)));
            save.Apply(new AltarExchangeCommand(altar.Id, atk, atk.CostAt(0)));

            check($"買兩把鑰匙＋兌一次攻：金 {save.State.Gold}、鑰匙 {save.State.KeysYellow}、攻 {save.State.Atk}、經驗 {save.State.Exp}",
                save.State.Gold == 300 - 50 - 75 && save.State.KeysYellow == 2
                && save.State.Atk == 11 && save.State.Exp == 80);

            var loaded = SaveGame.FromData(SaveData.FromJson(save.ToData().ToJson()));
            check("商店/祭壇指令進得了存檔（沒漏進 CommandCodec）",
                loaded.UndoDepth == 3 && loaded.State.Gold == save.State.Gold
                && loaded.State.Atk == save.State.Atk
                && loaded.State.PurchaseCounts.OrderBy(k => k.Key).SequenceEqual(
                       save.State.PurchaseCounts.OrderBy(k => k.Key)));

            while (save.UndoOne()) { }
            check("回溯到底＝買賣前（含遞增計價的計數歸零）",
                save.State.Gold == before.Gold && save.State.Atk == before.Atk
                && save.State.Exp == before.Exp && save.State.KeysYellow == before.KeysYellow
                && save.State.PurchaseCounts.Count == 0);
        }

        /// <summary>存 → 讀 → 完全相等。含 eid 帳本與遞增計價的計數。</summary>
        private static void RoundTrip(Catalog catalog, Action<string, bool> check)
        {
            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000, Hourglasses = 2 });
            save.EnterFloor("F00");
            save.Apply(new MoveCommand(new GridPos(6, 1), new GridPos(6, 2)));
            save.Apply(new PickupItemCommand("F00_i01", catalog.Items["potion_s"]));
            var m = catalog.Monsters["slime_green"];
            save.Apply(new CollisionBattleCommand("F00_m01",
                CombatResolver.ResolveCollision(new PlayerStats(10, 10), m), m));
            save.Apply(new OpenDoorCommand("F00_d01", KeyTier.Yellow));
            save.State.PurchaseCounts["altar_std:atk"] = 3;

            string json = save.ToData().ToJson();
            var loaded = SaveGame.FromData(SaveData.FromJson(json));

            var a = save.State;
            var b = loaded.State;
            bool same = a.Hp == b.Hp && a.Atk == b.Atk && a.Def == b.Def
                        && a.Gold == b.Gold && a.Exp == b.Exp
                        && a.KeysYellow == b.KeysYellow && a.Hourglasses == b.Hourglasses
                        && a.Position == b.Position && a.CurrentFloor == b.CurrentFloor
                        && a.ConsumedEids.SetEquals(b.ConsumedEids)
                        && a.PurchaseCounts.OrderBy(k => k.Key).SequenceEqual(b.PurchaseCounts.OrderBy(k => k.Key));
            check($"存讀往返完全相等（{json.Length} 字元、{save.UndoDepth} 步指令流）", same);
            check("往返後指令流深度不變（回溯步數不會憑空消失）", loaded.UndoDepth == save.UndoDepth);
        }

        /// <summary>
        /// 回溯必須把狀態還原到**位元級相同**。這是 D7 的核心承諾——
        /// 玩家花了一顆沙漏，換到的不能是「差不多的過去」。
        /// </summary>
        private static void UndoIsExact(Catalog catalog, Action<string, bool> check)
        {
            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000 });
            save.EnterFloor("F01");
            var before = save.State.Clone();

            var m = catalog.Monsters["bat_cave"];
            save.Apply(new CollisionBattleCommand("F01_m03",
                CombatResolver.ResolveCollision(save.State.CombatStats, m), m));
            save.Apply(new PickupItemCommand("F01_i03", catalog.Items["potion_s"]));

            bool changed = save.State.Hp != before.Hp;
            while (save.UndoOne()) { }

            var after = save.State;
            check($"回溯到底＝入口狀態（中途 Hp 曾變動={changed}）",
                changed && after.Hp == before.Hp && after.Gold == before.Gold
                && after.Exp == before.Exp && after.ConsumedEids.Count == 0);
            check("回溯到底後再回溯回傳 false（不會退過頭）", !save.UndoOne());
        }

        /// <summary>
        /// 單一時間軸：退回 F01 之後，F02 的快照必須作廢。
        /// 否則玩家可以退回低層重配資源，再跳回高層的舊快照，兩條時間線的資源憑空疊加。
        /// </summary>
        private static void SingleTimeline(Action<string, bool> check)
        {
            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000 });
            save.EnterFloor("F00");
            save.EnterFloor("F01");
            save.State.Gold = 50;
            save.EnterFloor("F02");

            check("三層都有快照", save.VisitedFloors.OrderBy(f => f).SequenceEqual(new[] { "F00", "F01", "F02" }));

            save.RevertToFloor("F01");
            check("退回 F01 後 F02 快照作廢（套利漏洞已封）",
                save.VisitedFloors.OrderBy(f => f).SequenceEqual(new[] { "F00", "F01" }));
            check("退回後狀態＝該層入口", save.State.CurrentFloor == "F01" && save.State.Gold == 0);

            bool threw = false;
            try { save.RevertToFloor("F02"); } catch (ArgumentException) { threw = true; }
            check("退不回已作廢的快照", threw);
        }

        /// <summary>進入新樓層要清空指令流——否則存檔會無限長，且回溯會跨層。</summary>
        private static void FloorEntryClearsStream(Action<string, bool> check)
        {
            var save = new SaveGame(new GameState { Atk = 10, Def = 10, Hp = 1000 });
            save.EnterFloor("F00");
            save.Apply(new MoveCommand(new GridPos(1, 1), new GridPos(1, 2)));
            save.Apply(new MoveCommand(new GridPos(1, 2), new GridPos(1, 3)));
            bool had = save.UndoDepth == 2;

            save.EnterFloor("F01");
            check($"進入新樓層清空指令流（原有 {(had ? 2 : -1)} 步 → 現在 {save.UndoDepth} 步）",
                had && save.UndoDepth == 0);
        }
    }
}
