using System;
using System.Collections.Generic;
using Tower.Core.Commands;
using Tower.Core.Floors;
using Tower.Core.Grid;

namespace Tower.Core.Save
{
    /// <summary>
    /// 指令的可攜形式——存檔就是存這個。
    ///
    /// 之所以做得到，是因為每個 <see cref="IGameCommand"/> 存的都是**差值**而不是定義的參照
    /// （`PickupItemCommand` 存 +1 黃鑰匙，不是存 `ItemDefinition`）。所以還原時不需要 Catalog，
    /// 存檔也不會因為日後調整道具數值而錯亂——舊存檔重放的是當時發生的事，不是現在的數值表。
    /// 這是指令模式從第一天就做（D7）順帶換來的紅利。
    /// </summary>
    public readonly struct CommandRecord
    {
        public readonly string Kind;
        public readonly string Eid;
        public readonly int[] Values;

        public CommandRecord(string kind, string eid, params int[] values)
        {
            Kind = kind;
            Eid = eid ?? "";
            Values = values ?? Array.Empty<int>();
        }
    }

    /// <summary>指令 ↔ 記錄的雙向轉換。新增指令型別時**兩邊都要加**，否則存檔會靜默漏掉一步。</summary>
    public static class CommandCodec
    {
        public static CommandRecord ToRecord(IGameCommand cmd) => cmd switch
        {
            MoveCommand m => new CommandRecord("move", "", m.From.X, m.From.Y, m.To.X, m.To.Y),
            OpenDoorCommand d => new CommandRecord("door", d.Eid, (int)d.Tier),
            PickupItemCommand p => new CommandRecord("pickup", p.Eid,
                p.DKeyY, p.DKeyB, p.DKeyR, p.DHp, p.DAtk, p.DDef, p.DHourglass),
            CollisionBattleCommand b => new CommandRecord("battle", b.Eid, b.HpLoss, b.GoldDrop, b.ExpDrop),
            _ => throw new ArgumentException($"未知的指令型別 {cmd.GetType().Name}——存檔會漏掉這一步，必須在 CommandCodec 補上"),
        };

        public static IGameCommand FromRecord(in CommandRecord r) => r.Kind switch
        {
            "move" => new MoveCommand(new GridPos(V(r, 0), V(r, 1)), new GridPos(V(r, 2), V(r, 3))),
            "door" => new OpenDoorCommand(r.Eid, (KeyTier)V(r, 0)),
            "pickup" => PickupItemCommand.FromDeltas(r.Eid,
                V(r, 0), V(r, 1), V(r, 2), V(r, 3), V(r, 4), V(r, 5), V(r, 6)),
            "battle" => CollisionBattleCommand.FromDeltas(r.Eid, V(r, 0), V(r, 1), V(r, 2)),
            _ => throw new ArgumentException($"存檔含未知的指令種類 '{r.Kind}'"),
        };

        private static int V(in CommandRecord r, int i)
            => i < r.Values.Length ? r.Values[i] : throw new ArgumentException($"'{r.Kind}' 記錄缺少第 {i} 個欄位");
    }
}
