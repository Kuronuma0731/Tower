using System;
using System.Collections.Generic;
using Tower.Core.Combat;
using Tower.Core.Floors;

namespace Tower.Core.Data
{
    /// <summary>
    /// 從 CSV 文字建出的怪物／道具定義表——**全專案唯一的數值真相**。
    ///
    /// 先前 F01、GalleryFloor、monsters.csv 各存一份數值，已經漂移（展示層顯示的
    /// 數字全是錯的）。現在樓層只認 id，數值一律來自這裡。
    ///
    /// 刻意收 CSV **文字**而非路徑：Core 不假設檔案系統，Unity 與引擎外測試各自餵字串。
    /// </summary>
    public sealed class Catalog
    {
        public IReadOnlyDictionary<string, MonsterDefinition> Monsters { get; }
        public IReadOnlyDictionary<string, ItemDefinition> Items { get; }
        public IReadOnlyDictionary<string, ShopDefinition> Shops { get; }
        public IReadOnlyDictionary<string, AltarDefinition> Altars { get; }

        private Catalog(
            Dictionary<string, MonsterDefinition> monsters,
            Dictionary<string, ItemDefinition> items,
            Dictionary<string, ShopDefinition> shops,
            Dictionary<string, AltarDefinition> altars)
        {
            Monsters = monsters;
            Items = items;
            Shops = shops;
            Altars = altars;
        }

        /// <summary>shops/altars 可省略——只驗戰鬥公式的測試不需要它們。</summary>
        public static Catalog Load(string monstersCsv, string itemsCsv,
                                   string shopsCsv = null, string altarsCsv = null)
            => new Catalog(ParseMonsters(monstersCsv), ParseItems(itemsCsv),
                           ParseShops(shopsCsv), ParseAltars(altarsCsv));

        // id,name_zh,atk,def,hp,agility,traits,gold_drop,exp_drop,is_guardian,sprite,bestiary_note
        private const int MonsterColumns = 12;

        public static Dictionary<string, MonsterDefinition> ParseMonsters(string csv)
        {
            var result = new Dictionary<string, MonsterDefinition>();
            foreach (var c in Csv.Rows(csv, MonsterColumns))
            {
                string id = c[0].Trim();
                if (id.Length == 0) continue;
                if (c[2].Trim().Length == 0) continue; // 數值未填（佔位列）→ 跳過

                result[id] = new MonsterDefinition(
                    id,
                    atk: Csv.Int(c[2]),
                    def: Csv.Int(c[3]),
                    hp: Csv.Int(c[4]),
                    traits: ParseTraits(c[6]),
                    goldDrop: Csv.Int(c[7]),
                    expDrop: Csv.Int(c[8]),
                    isGuardian: Csv.Bool(c[9]),
                    nameZh: c[1].Trim(),
                    agility: Csv.Int(c[5]));
            }
            return result;
        }

        /// <summary>解析 `first_strike|multi_hit` 形式；未知名稱擲例外，錯字在匯入期就抓到。</summary>
        public static TraitSet ParseTraits(string field)
        {
            var traits = TraitSet.None;
            if (string.IsNullOrWhiteSpace(field)) return traits;

            foreach (var raw in field.Split('|'))
            {
                var name = raw.Trim();
                if (name.Length == 0) continue;
                int colon = name.IndexOf(':'); // `multi_hit:3` 的參數語法，數值部分暫未使用
                if (colon >= 0) name = name.Substring(0, colon);

                traits |= name switch
                {
                    "first_strike" => TraitSet.FirstStrike,
                    "multi_hit" => TraitSet.MultiHit,
                    "pierce" => TraitSet.Pierce,
                    "lifesteal" => TraitSet.Lifesteal,
                    _ => throw new ArgumentException($"未知的怪物特性：'{name}'"),
                };
            }
            return traits;
        }

        // id,name_zh,category,key_tier,heal_hp,atk_bonus,def_bonus,undo_steps,sprite,desc_zh
        private const int ItemColumns = 10;

        public static Dictionary<string, ItemDefinition> ParseItems(string csv)
        {
            var result = new Dictionary<string, ItemDefinition>();
            foreach (var c in Csv.Rows(csv, ItemColumns))
            {
                string id = c[0].Trim();
                if (id.Length == 0) continue;

                result[id] = new ItemDefinition(
                    id,
                    category: ParseCategory(c[2]),
                    keyTier: ParseKeyTier(c[3]),
                    healHp: Csv.Int(c[4]),
                    atkBonus: Csv.Int(c[5]),
                    defBonus: Csv.Int(c[6]),
                    undoSteps: Csv.Int(c[7]),
                    nameZh: c[1].Trim());
            }
            return result;
        }

        // shop_id,item_id,base_price,price_step
        public static Dictionary<string, ShopDefinition> ParseShops(string csv)
        {
            var byId = new Dictionary<string, List<ShopOffer>>();
            foreach (var c in Csv.Rows(csv ?? "", 4))
            {
                string id = c[0].Trim();
                if (id.Length == 0) continue;
                if (!byId.TryGetValue(id, out var list)) byId[id] = list = new List<ShopOffer>();
                list.Add(new ShopOffer(c[1].Trim(), Csv.Int(c[2]), Csv.Int(c[3])));
            }
            var result = new Dictionary<string, ShopDefinition>();
            foreach (var kv in byId) result[kv.Key] = new ShopDefinition(kv.Key, kv.Value);
            return result;
        }

        // altar_id,stat,exp_cost,gain,cost_step
        public static Dictionary<string, AltarDefinition> ParseAltars(string csv)
        {
            var byId = new Dictionary<string, List<AltarOffer>>();
            foreach (var c in Csv.Rows(csv ?? "", 5))
            {
                string id = c[0].Trim();
                if (id.Length == 0) continue;
                if (!byId.TryGetValue(id, out var list)) byId[id] = list = new List<AltarOffer>();
                list.Add(new AltarOffer(ParseStat(c[1]), Csv.Int(c[2]), Csv.Int(c[3]), Csv.Int(c[4])));
            }
            var result = new Dictionary<string, AltarDefinition>();
            foreach (var kv in byId) result[kv.Key] = new AltarDefinition(kv.Key, kv.Value);
            return result;
        }

        private static AltarStat ParseStat(string s) => s.Trim().ToLowerInvariant() switch
        {
            "atk" => AltarStat.Atk,
            "def" => AltarStat.Def,
            "hp" => AltarStat.Hp,
            _ => throw new ArgumentException($"未知的祭壇屬性 '{s}'"),
        };

        private static ItemCategory ParseCategory(string s) => s.Trim() switch
        {
            "key" => ItemCategory.Key,
            "potion" => ItemCategory.Potion,
            "gem" => ItemCategory.Gem,
            "undo" => ItemCategory.Undo,
            var other => throw new ArgumentException($"未知的道具類別：'{other}'"),
        };

        private static KeyTier ParseKeyTier(string s) => s.Trim() switch
        {
            "blue" => KeyTier.Blue,
            "red" => KeyTier.Red,
            _ => KeyTier.Yellow,
        };
    }
}
