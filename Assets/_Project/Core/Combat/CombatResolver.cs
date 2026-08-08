namespace Tower.Core.Combat
{
    /// <summary>
    /// 碰撞戰結算：在 DamageFormula 之上跑確定性迴圈。
    /// 無副作用——吃狀態進去、吐結果出來，傷害預覽直接呼叫本方法。
    /// 守關怪走同一條路，沒有專屬程式路徑。
    /// </summary>
    public static class CombatResolver
    {
        public static CollisionOutcome ResolveCollision(in PlayerStats player, MonsterDefinition monster)
        {
            int playerHit = DamageFormula.PlayerHit(player, monster);
            if (playerHit == 0)
                return CollisionOutcome.Unwinnable; // 打不動：不得除零（規格明定分支）

            int damagePerOccasion = DamageFormula.DamagePerOccasion(player, monster);
            int healPerOccasion = DamageFormula.HealPerOccasion(player, monster);

            // 吸血：我方單擊 ≤ 每輪回復 → 淨削減歸零，不可擊殺
            if (healPerOccasion > 0 && playerHit <= healPerOccasion)
                return CollisionOutcome.Unwinnable;

            // 確定性模擬。我方先手；敵方出手發生在「我方一擊未殺」之後；
            // 先攻 = 開戰前額外一次敵方出手（同樣吃連擊倍數與吸血回復）。
            long monsterHp = monster.Hp;
            long loss = 0;
            int rounds = 0;

            if (monster.Traits.HasFlag(TraitSet.FirstStrike))
            {
                loss += damagePerOccasion;
                monsterHp += healPerOccasion;
            }

            while (true)
            {
                rounds++;
                monsterHp -= playerHit;
                if (monsterHp <= 0)
                    break;

                loss += damagePerOccasion;
                monsterHp += healPerOccasion;
            }

            return CollisionOutcome.Win((int)loss, rounds);
        }
    }
}
