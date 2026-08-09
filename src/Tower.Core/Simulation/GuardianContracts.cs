using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Commands;
using Tower.Core.Floors;

namespace Tower.Core.Simulation
{
    public readonly struct GuardianCheckResult
    {
        public readonly string Eid;

        /// <summary>合約①：以「抵達該層、可兌換資源未兌換」的入場狀態直接碰撞，預覽必須是死亡（或無法戰勝）。</summary>
        public readonly bool PreviewDeathHolds;

        /// <summary>合約②：按 DamageFormula 含特性結算，「敵方單擊 = 0」在當層資源上界內不可達。</summary>
        public readonly bool ZeroDamageUnreachable;

        public bool Passed => PreviewDeathHolds && ZeroDamageUnreachable;

        public GuardianCheckResult(string eid, bool previewDeath, bool zeroDamageUnreachable)
        {
            Eid = eid;
            PreviewDeathHolds = previewDeath;
            ZeroDamageUnreachable = zeroDamageUnreachable;
        }
    }

    /// <summary>
    /// 守關怪合約檢查（CONTEXT.md 守關怪詞條的兩條，驗證器執法）。
    /// 上界估算採過度近似（over-approximation）：連上界都歸不了零，實際遊玩更不可能——安全方向。
    /// </summary>
    public static class GuardianContracts
    {
        public static List<GuardianCheckResult> Check(
            FloorDefinition floor,
            IReadOnlyDictionary<string, MonsterDefinition> monsters,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, AltarDefinition> altars,
            GameState entry)
        {
            var results = new List<GuardianCheckResult>();

            // 當層資源上界：入場經驗 + 全部怪物經驗掉落；入場防禦 + 全部防寶石
            int expCeiling = entry.Exp;
            int defFromGems = 0;
            bool floorHasAltar = false;

            foreach (var e in floor.Entities)
            {
                if (e.Type == EntityType.Monster && monsters.TryGetValue(e.Ref, out var m))
                    expCeiling += m.ExpDrop;
                else if (e.Type == EntityType.Item && items.TryGetValue(e.Ref, out var it) && it.Category == ItemCategory.Gem)
                    defFromGems += it.DefBonus;
                else if (e.Type == EntityType.Altar)
                    floorHasAltar = true;
            }

            foreach (var e in floor.Entities)
            {
                if (e.Type != EntityType.Monster) continue;
                var m = monsters[e.Ref];
                if (!m.IsGuardian) continue;

                // 合約①：入場狀態直接碰撞必死（無法戰勝亦符合「預覽不顯示可行數字」）
                var outcome = CombatResolver.ResolveCollision(entry.CombatStats, m);
                bool previewDeath = !outcome.Winnable || outcome.ExpectedLoss >= entry.Hp;

                // 合約②：防禦上界 = 入場防 + 全部防寶石 + 祭壇可買到的最大防（遞增價，用經驗上界推）
                int maxDef = entry.Def + defFromGems;
                if (floorHasAltar)
                    maxDef += MaxAltarDefPoints(floor, altars, entry, expCeiling);

                bool zeroUnreachable =
                    DamageFormula.MonsterHit(new PlayerStats(entry.Atk, maxDef), m) > 0;

                results.Add(new GuardianCheckResult(e.Eid, previewDeath, zeroUnreachable));
            }

            return results;
        }

        private static int MaxAltarDefPoints(
            FloorDefinition floor,
            IReadOnlyDictionary<string, AltarDefinition> altars,
            GameState entry,
            int expCeiling)
        {
            // 找到當層任一祭壇的防兌換項，按遞增價從入場計數開始一路買到經驗上界耗盡
            foreach (var e in floor.Entities)
            {
                if (e.Type != EntityType.Altar) continue;
                if (!altars.TryGetValue(e.Ref, out var altar)) continue;

                foreach (var offer in altar.Offers)
                {
                    if (offer.Stat != AltarStat.Def) continue;
                    string key = altar.Id + ":" + AltarStat.Def;
                    int count = entry.PurchaseCounts.TryGetValue(key, out var n) ? n : 0;
                    int exp = expCeiling;
                    int points = 0;
                    while (true)
                    {
                        int cost = offer.CostAt(count + points);
                        if (exp < cost) break;
                        exp -= cost;
                        points += 1;
                    }
                    return points * offer.Gain;
                }
            }
            return 0;
        }
    }
}
