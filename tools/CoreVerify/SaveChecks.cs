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

            RoundTrip(catalog, check);
            UndoIsExact(catalog, check);
            SingleTimeline(check);
            FloorEntryClearsStream(check);
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
