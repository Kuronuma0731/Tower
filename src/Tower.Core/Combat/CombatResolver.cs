using System;

namespace Tower.Core.Combat
{
    /// <summary>
    /// 碰撞戰結算：在 DamageFormula 之上跑確定性迴圈。
    /// 無副作用——吃狀態進去、吐結果出來，傷害預覽直接呼叫本方法。
    /// 守關怪走同一條路，沒有專屬程式路徑。
    /// </summary>
    public static class CombatResolver
    {
        /// <summary>安全閥：超過此出手數視為打不完（吸血/迴避的極端組合）。</summary>
        private const int AttackCap = 100_000;

        public static CollisionOutcome ResolveCollision(in PlayerStats player, MonsterDefinition monster)
        {
            // 特殊戰鬥：代價寫死，與雙方數值無關，且必定可勝（劇情性關卡用）
            if (monster.Traits.HasFlag(TraitSet.FixedLoss))
                return CollisionOutcome.Win(Math.Max(0, monster.TraitValue), 1, 0);

            int weakenPerHit = DamageFormula.WeakenPerHit(monster);
            int playerHit = DamageFormula.PlayerHit(player, monster);
            if (playerHit == 0)
                return CollisionOutcome.Unwinnable; // 打不動：不得除零（規格明定分支）

            int damagePerOccasion = DamageFormula.DamagePerOccasion(player, monster);
            int healPerOccasion = DamageFormula.HealPerOccasion(player, monster);
            int agility = Math.Clamp(monster.Agility, 0, 90);

            // 每次出手的期望削減（計入迴避）；不足以抵銷吸血回復 → 不可擊殺
            long netPerAttackTimes100 = (long)playerHit * (100 - agility) - (long)healPerOccasion * 100;
            if (healPerOccasion > 0 && netPerAttackTimes100 <= 0)
                return CollisionOutcome.Unwinnable;

            // 確定性模擬。我方先手；敵方出手發生在「我方一擊未殺」之後；
            // 先攻 = 開戰前額外一次敵方出手（同樣吃連擊倍數與吸血回復）。
            long monsterHp = monster.Hp;
            long loss = 0;
            int attacks = 0;
            int misses = 0;
            int atkPenalty = 0;   // 衰弱累積量（本場戰鬥內）

            // D15 迴避：每次出手累加敏捷，滿 100 即落空一次——落空比例恰為敏捷%，
            // 且**次數算死**。哪幾下落空由表現層隨機挑，總帳不受影響。
            long evasionAccumulator = 0;

            if (monster.Traits.HasFlag(TraitSet.FirstStrike))
            {
                loss += damagePerOccasion;
                monsterHp += healPerOccasion;
            }

            while (true)
            {
                attacks++;
                if (attacks > AttackCap)
                    return CollisionOutcome.Unwinnable;

                evasionAccumulator += agility;
                bool missed = evasionAccumulator >= 100;
                if (missed)
                {
                    evasionAccumulator -= 100;
                    misses++;
                }
                else
                {
                    monsterHp -= playerHit;
                    if (monsterHp <= 0) break;
                }

                loss += damagePerOccasion;   // 落空與否，怪都會回擊
                monsterHp += healPerOccasion;

                // 衰弱：每挨一次，我方的刀就鈍一分（本場戰鬥內累積）。
                // 削減歸零即不可擊殺——AttackCap 也擋得住，但這裡提早收斂，
                // 免得白跑上萬次迴圈才回報同一個結論。
                if (weakenPerHit > 0)
                {
                    atkPenalty += weakenPerHit;
                    playerHit = DamageFormula.PlayerHit(player, monster, atkPenalty);
                    if (playerHit <= 0) return CollisionOutcome.Unwinnable;
                }
            }

            return CollisionOutcome.Win((int)loss, attacks, misses);
        }
    }
}
