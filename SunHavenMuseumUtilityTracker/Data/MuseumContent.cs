using System.Collections.Generic;
using System.Linq;

namespace SunHavenMuseumUtilityTracker.Data
{
    /// <summary>
    /// Defines all museum sections, bundles, and items.
    /// Item IDs match actual Sun Haven game item IDs for icon loading.
    /// </summary>
    public static class MuseumContent
    {
        private static List<MuseumSection> _sections;

        public static List<MuseumSection> GetAllSections()
        {
            if (_sections == null)
            {
                _sections = BuildMuseumContent();
            }
            return _sections;
        }

        private static List<MuseumSection> BuildMuseumContent()
        {
            var sections = new List<MuseumSection>();

            // ==================== HALL OF GEMS ====================
            var hallOfGems = new MuseumSection("hall_of_gems", "The Hall of Gems", "A dazzling collection of precious gems and minerals.");

            // Bundle IDs from game: Wish.MuseumCurator.miningMuseumProgress (sync uses these progress keys)
            // Mana Bundle (1 item, requires 20)
            var manaBundle = new MuseumBundle("ManaBundle", "Mana Bundle", "hall_of_gems", "Collect 20 Mana Drops.");
            manaBundle.Items.AddRange(new[]
            {
                new MuseumItem("mana_drop", "Mana Drop (x20)", "ManaBundle", 60234, ItemRarity.Rare, "A shimmering drop of pure mana. Donate 20."),
            });
            hallOfGems.Bundles.Add(manaBundle);

            // Money Bundle
            var moneyBundle = new MuseumBundle("MoneyBundle", "Money Bundle", "hall_of_gems", "Wealth and currency collection.");
            moneyBundle.Items.AddRange(new[]
            {
                new MuseumItem("coins", "Coins (x25,000)", "MoneyBundle", 60000, ItemRarity.Common, "Gold coins. Donate 25,000."),
                new MuseumItem("mana_orbs", "Mana Orbs (x1,000)", "MoneyBundle", 60001, ItemRarity.Uncommon, "Magical mana orbs. Donate 1,000."),
                new MuseumItem("tickets", "Tickets (x1,000)", "MoneyBundle", 60002, ItemRarity.Uncommon, "Event tickets. Donate 1,000."),
            });
            hallOfGems.Bundles.Add(moneyBundle);

            // Golden Bundle
            var goldenBundle = new MuseumBundle("GoldenBundle", "Golden Bundle", "hall_of_gems", "Rare golden items collection.");
            goldenBundle.Items.AddRange(new[]
            {
                new MuseumItem("golden_milk", "Golden Milk", "GoldenBundle", 2920, ItemRarity.Rare, "Milk with a golden hue."),
                new MuseumItem("golden_egg", "Golden Egg", "GoldenBundle", 3052, ItemRarity.Rare, "A shimmering golden egg."),
                new MuseumItem("golden_wool", "Golden Wool", "GoldenBundle", 2113, ItemRarity.Rare, "Luxurious golden wool."),
                new MuseumItem("golden_pomegranate", "Golden Pomegranate", "GoldenBundle", 3053, ItemRarity.Rare, "A golden pomegranate."),
                new MuseumItem("golden_log", "Golden Log", "GoldenBundle", 2114, ItemRarity.Rare, "A log of golden wood."),
                new MuseumItem("golden_feather", "Golden Feather", "GoldenBundle", 2115, ItemRarity.Rare, "A brilliant golden feather."),
                new MuseumItem("golden_silk", "Golden Silk", "GoldenBundle", 2116, ItemRarity.Rare, "Fine golden silk."),
                new MuseumItem("golden_apple", "Golden Apple", "GoldenBundle", 3054, ItemRarity.Rare, "A golden apple."),
                new MuseumItem("golden_orange", "Golden Orange", "GoldenBundle", 3055, ItemRarity.Rare, "A golden orange."),
                new MuseumItem("golden_strawberry", "Golden Strawberry", "GoldenBundle", 3057, ItemRarity.Rare, "A golden strawberry."),
                new MuseumItem("golden_blueberry", "Golden Blueberry", "GoldenBundle", 3056, ItemRarity.Rare, "A golden blueberry."),
                new MuseumItem("golden_peach", "Golden Peach", "GoldenBundle", 3058, ItemRarity.Rare, "A golden peach."),
                new MuseumItem("golden_raspberry", "Golden Raspberry", "GoldenBundle", 3059, ItemRarity.Rare, "A golden raspberry."),
            });
            hallOfGems.Bundles.Add(goldenBundle);

            // Bars Bundle
            var barsBundle = new MuseumBundle("BarsBundle", "Bars Bundle", "hall_of_gems", "Metal bars collection.");
            barsBundle.Items.AddRange(new[]
            {
                new MuseumItem("copper_bar", "Copper Bar", "BarsBundle", 1200, ItemRarity.Common, "A bar of copper."),
                new MuseumItem("iron_bar", "Iron Bar", "BarsBundle", 1201, ItemRarity.Common, "A bar of iron."),
                new MuseumItem("adamant_bar", "Adamant Bar", "BarsBundle", 1202, ItemRarity.Uncommon, "A bar of adamant."),
                new MuseumItem("mithril_bar", "Mithril Bar", "BarsBundle", 1203, ItemRarity.Rare, "A bar of mithril."),
                new MuseumItem("sunite_bar", "Sunite Bar", "BarsBundle", 1204, ItemRarity.Epic, "A bar of sunite."),
                new MuseumItem("gold_bar", "Gold Bar", "BarsBundle", 1205, ItemRarity.Rare, "A bar of gold."),
                new MuseumItem("glorite_bar", "Glorite Bar", "BarsBundle", 1206, ItemRarity.Legendary, "A bar of glorite."),
                new MuseumItem("elven_steel_bar", "Elven Steel Bar", "BarsBundle", 1207, ItemRarity.Epic, "A bar of elven steel."),
            });
            hallOfGems.Bundles.Add(barsBundle);

            // Gem Bundle (game uses "GemBundle" not "GemsBundle")
            var gemsBundle = new MuseumBundle("GemBundle", "Gems Bundle", "hall_of_gems", "Precious gems collection.");
            gemsBundle.Items.AddRange(new[]
            {
                new MuseumItem("sapphire", "Sapphire", "GemBundle", 1000, ItemRarity.Rare, "A brilliant blue sapphire."),
                new MuseumItem("ruby", "Ruby", "GemBundle", 1001, ItemRarity.Rare, "A deep red ruby."),
                new MuseumItem("amethyst", "Amethyst", "GemBundle", 1002, ItemRarity.Uncommon, "A purple amethyst."),
                new MuseumItem("diamond", "Diamond", "GemBundle", 1003, ItemRarity.Epic, "A sparkling diamond."),
                new MuseumItem("havenite", "Havenite", "GemBundle", 1004, ItemRarity.Legendary, "A rare havenite gem."),
                new MuseumItem("black_diamond", "Black Diamond", "GemBundle", 1005, ItemRarity.Legendary, "A mysterious black diamond."),
                new MuseumItem("dizzite", "Dizzite", "GemBundle", 10620, ItemRarity.Rare, "A shimmering dizzite gem."),
            });
            hallOfGems.Bundles.Add(gemsBundle);

            // Nel'Vari Mines Bundle
            var nelvariMinesBundle = new MuseumBundle("NelvariMinesBundle", "Nel'Vari Mines Bundle", "hall_of_gems", "Treasures from the Nel'Vari Mines.");
            nelvariMinesBundle.Items.AddRange(new[]
            {
                new MuseumItem("mana_shard", "Mana Shard (x5)", "NelvariMinesBundle", 18015, ItemRarity.Rare, "A shard of crystallized mana. Donate 5."),
                new MuseumItem("sparkling_dragon_scale", "Sparkling Dragon Scale (x5)", "NelvariMinesBundle", 1115, ItemRarity.Epic, "A sparkling dragon scale. Donate 5."),
                new MuseumItem("sharp_dragon_scale", "Sharp Dragon Scale (x5)", "NelvariMinesBundle", 1116, ItemRarity.Epic, "A sharp dragon scale. Donate 5."),
                new MuseumItem("tough_dragon_scale", "Tough Dragon Scale (x5)", "NelvariMinesBundle", 1114, ItemRarity.Epic, "A tough dragon scale. Donate 5."),
            });
            hallOfGems.Bundles.Add(nelvariMinesBundle);

            // Withergate Mines Bundle
            var withergateMinesBundle = new MuseumBundle("WithergateMinesBundle", "Withergate Mines Bundle", "hall_of_gems", "Sweet treasures from the Withergate Mines.");
            withergateMinesBundle.Items.AddRange(new[]
            {
                new MuseumItem("candy_corn_pieces", "Candy Corn Pieces (x5)", "WithergateMinesBundle", 18016, ItemRarity.Rare, "Candy corn pieces. Donate 5."),
                new MuseumItem("rock_candy_gem", "Rock Candy Gem (x5)", "WithergateMinesBundle", 3759, ItemRarity.Rare, "A rock candy gem. Donate 5."),
                new MuseumItem("jawbreaker_gem", "Jawbreaker Gem (x5)", "WithergateMinesBundle", 3761, ItemRarity.Rare, "A jawbreaker gem. Donate 5."),
                new MuseumItem("hard_butterscotch_gem", "Hard Butterscotch Gem (x5)", "WithergateMinesBundle", 3760, ItemRarity.Rare, "A hard butterscotch gem. Donate 5."),
            });
            hallOfGems.Bundles.Add(withergateMinesBundle);

            sections.Add(hallOfGems);

            // ==================== HALL OF CULTURE ====================
            // Bundle IDs: Wish.MuseumCurator.culturalMuseumProgress. Item IDs: Wish.ItemID (decompiled SunHaven.Core); sets aligned with wiki museum pages.
            var hallOfCulture = new MuseumSection("hall_of_culture", "The Hall of Culture", "Crops, flowers, foraging, combat, alchemy, exploration, and farming from all regions.");

            var winterCrops = new MuseumBundle("WinterCropsBundle", "Winter Crops", "hall_of_culture", "Crops harvested in winter.");
            winterCrops.Items.AddRange(new[]
            {
                new MuseumItem("wc_tea_leaves", "Tea Leaves", "WinterCropsBundle", 11048, ItemRarity.Common, ""),
                new MuseumItem("wc_turnip", "Turnip", "WinterCropsBundle", 11040, ItemRarity.Common, ""),
                new MuseumItem("wc_purple_eggplant", "Purple Eggplant", "WinterCropsBundle", 12081, ItemRarity.Common, ""),
                new MuseumItem("wc_heat_fruit", "Heat Fruit", "WinterCropsBundle", 11041, ItemRarity.Common, ""),
                new MuseumItem("wc_marshmallow_bean", "Marshmallow Bean", "WinterCropsBundle", 11042, ItemRarity.Common, ""),
                new MuseumItem("wc_brr_nana", "Brr-Nana", "WinterCropsBundle", 12082, ItemRarity.Common, ""),
                new MuseumItem("wc_star_fruit", "Star Fruit", "WinterCropsBundle", 11066, ItemRarity.Common, ""),
                new MuseumItem("wc_hexagon_berry", "Hexagon Berry", "WinterCropsBundle", 11065, ItemRarity.Common, ""),
                new MuseumItem("wc_snow_pea", "Snow Pea", "WinterCropsBundle", 11078, ItemRarity.Common, ""),
                new MuseumItem("wc_snow_ball_crop", "Snow Ball (Crop)", "WinterCropsBundle", 11067, ItemRarity.Common, ""),
                new MuseumItem("wc_balloon_fruit", "Balloon Fruit", "WinterCropsBundle", 11063, ItemRarity.Common, ""),
                new MuseumItem("wc_blizzard_berry", "Blizzard Berry", "WinterCropsBundle", 11068, ItemRarity.Common, ""),
                new MuseumItem("wc_pythagorean_berry", "Pythagorean Berry", "WinterCropsBundle", 11069, ItemRarity.Common, ""),
                new MuseumItem("wc_blue_moon_fruit", "Blue Moon Fruit", "WinterCropsBundle", 11064, ItemRarity.Common, ""),
                new MuseumItem("wc_candy_cane", "Candy Cane", "WinterCropsBundle", 11077, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(winterCrops);

            var flowersBundle = new MuseumBundle("FlowersBundle", "Flowers", "hall_of_culture", "Flowers grown year-round.");
            flowersBundle.Items.AddRange(new[]
            {
                new MuseumItem("fl_red_rose", "Red Rose", "FlowersBundle", 11107, ItemRarity.Common, ""),
                new MuseumItem("fl_blue_rose", "Blue Rose", "FlowersBundle", 11108, ItemRarity.Common, ""),
                new MuseumItem("fl_daisy", "Daisy", "FlowersBundle", 11130, ItemRarity.Common, ""),
                new MuseumItem("fl_orchid", "Orchid", "FlowersBundle", 11105, ItemRarity.Common, ""),
                new MuseumItem("fl_tulip", "Tulip", "FlowersBundle", 11109, ItemRarity.Common, ""),
                new MuseumItem("fl_hibiscus", "Hibiscus", "FlowersBundle", 11103, ItemRarity.Common, ""),
                new MuseumItem("fl_lavender", "Lavender", "FlowersBundle", 11102, ItemRarity.Common, ""),
                new MuseumItem("fl_sunflower", "Sunflower", "FlowersBundle", 11106, ItemRarity.Common, ""),
                new MuseumItem("fl_lily", "Lily", "FlowersBundle", 11104, ItemRarity.Common, ""),
                new MuseumItem("fl_lotus", "Lotus", "FlowersBundle", 11110, ItemRarity.Common, ""),
                new MuseumItem("fl_honey_flower", "Honey Flower", "FlowersBundle", 11101, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(flowersBundle);

            var springCrops = new MuseumBundle("SpringCropsBundle", "Spring Crops", "hall_of_culture", "Crops harvested in spring.");
            springCrops.Items.AddRange(new[]
            {
                new MuseumItem("sp_grapes", "Grapes", "SpringCropsBundle", 11022, ItemRarity.Common, ""),
                new MuseumItem("sp_wheat", "Wheat", "SpringCropsBundle", 11000, ItemRarity.Common, ""),
                new MuseumItem("sp_tomato", "Tomato", "SpringCropsBundle", 11003, ItemRarity.Common, ""),
                new MuseumItem("sp_corn", "Corn", "SpringCropsBundle", 11001, ItemRarity.Common, ""),
                new MuseumItem("sp_onion", "Onion", "SpringCropsBundle", 11009, ItemRarity.Common, ""),
                new MuseumItem("sp_potato", "Potato", "SpringCropsBundle", 11002, ItemRarity.Common, ""),
                new MuseumItem("sp_greenroot", "Greenroot", "SpringCropsBundle", 11010, ItemRarity.Common, ""),
                new MuseumItem("sp_carrot", "Carrot", "SpringCropsBundle", 11006, ItemRarity.Common, ""),
                new MuseumItem("sp_kale", "Kale", "SpringCropsBundle", 11050, ItemRarity.Common, ""),
                new MuseumItem("sp_lettuce", "Lettuce", "SpringCropsBundle", 11013, ItemRarity.Common, ""),
                new MuseumItem("sp_cinnaberry", "Cinnaberry", "SpringCropsBundle", 11012, ItemRarity.Common, ""),
                new MuseumItem("sp_pepper", "Pepper", "SpringCropsBundle", 11008, ItemRarity.Common, ""),
                new MuseumItem("sp_shimmeroot", "Shimmeroot", "SpringCropsBundle", 11007, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(springCrops);

            var fallCrops = new MuseumBundle("FallCropsBundle", "Fall Crops", "hall_of_culture", "Crops harvested in fall.");
            fallCrops.Items.AddRange(new[]
            {
                new MuseumItem("fa_garlic", "Garlic", "FallCropsBundle", 11060, ItemRarity.Common, ""),
                new MuseumItem("fa_yam", "Yam", "FallCropsBundle", 11047, ItemRarity.Common, ""),
                new MuseumItem("fa_soda_pop_crop", "Soda Pop Crop", "FallCropsBundle", 11038, ItemRarity.Common, ""),
                new MuseumItem("fa_fizzy_fruit", "Fizzy Fruit", "FallCropsBundle", 11039, ItemRarity.Common, ""),
                new MuseumItem("fa_cranberry", "Cranberry", "FallCropsBundle", 11070, ItemRarity.Common, ""),
                new MuseumItem("fa_barley", "Barley", "FallCropsBundle", 11045, ItemRarity.Common, ""),
                new MuseumItem("fa_pumpkin", "Pumpkin", "FallCropsBundle", 11036, ItemRarity.Common, ""),
                new MuseumItem("fa_ghost_pepper", "Ghost Pepper", "FallCropsBundle", 11049, ItemRarity.Common, ""),
                new MuseumItem("fa_butternut", "Butternut", "FallCropsBundle", 11044, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(fallCrops);

            var nelvariBooks = new MuseumBundle("NelvariTempleBooks", "Nel'Vari Temple Books", "hall_of_culture", "Mine book series from Sun Haven, Nel'Vari, and Withergate.");
            nelvariBooks.Items.AddRange(new[]
            {
                new MuseumItem("book_nivara_i", "Origins of the Grand Tree and Nivara, Book I", "NelvariTempleBooks", 6500, ItemRarity.Rare, ""),
                new MuseumItem("book_nivara_ii", "Origins of the Grand Tree and Nivara, Book II", "NelvariTempleBooks", 6501, ItemRarity.Rare, ""),
                new MuseumItem("book_nivara_iii", "Origins of the Grand Tree and Nivara, Book III", "NelvariTempleBooks", 6502, ItemRarity.Rare, ""),
                new MuseumItem("book_nivara_iv", "Origins of the Grand Tree and Nivara, Book IV", "NelvariTempleBooks", 6503, ItemRarity.Rare, ""),
                new MuseumItem("book_nivara_v", "Origins of the Grand Tree and Nivara, Book V", "NelvariTempleBooks", 6504, ItemRarity.Rare, ""),
                new MuseumItem("book_elios_i", "Origins of Sun Haven and Elios, Book I", "NelvariTempleBooks", 6505, ItemRarity.Rare, ""),
                new MuseumItem("book_elios_ii", "Origins of Sun Haven and Elios, Book II", "NelvariTempleBooks", 6506, ItemRarity.Rare, ""),
                new MuseumItem("book_elios_iii", "Origins of Sun Haven and Elios, Book III", "NelvariTempleBooks", 6507, ItemRarity.Rare, ""),
                new MuseumItem("book_elios_iv", "Origins of Sun Haven and Elios, Book IV", "NelvariTempleBooks", 6508, ItemRarity.Rare, ""),
                new MuseumItem("book_elios_v", "Origins of Sun Haven and Elios, Book V", "NelvariTempleBooks", 6509, ItemRarity.Rare, ""),
                new MuseumItem("book_dynus_i", "Origins of Dynus and Shadows, Book I", "NelvariTempleBooks", 6510, ItemRarity.Rare, ""),
                new MuseumItem("book_dynus_ii", "Origins of Dynus and Shadows, Book II", "NelvariTempleBooks", 6511, ItemRarity.Rare, ""),
                new MuseumItem("book_dynus_iii", "Origins of Dynus and Shadows, Book III", "NelvariTempleBooks", 6512, ItemRarity.Rare, ""),
                new MuseumItem("book_dynus_iv", "Origins of Dynus and Shadows, Book IV", "NelvariTempleBooks", 6513, ItemRarity.Rare, ""),
                new MuseumItem("book_dynus_v", "Origins of Dynus and Shadows, Book V", "NelvariTempleBooks", 6514, ItemRarity.Rare, ""),
            });
            hallOfCulture.Bundles.Add(nelvariBooks);

            var summerCrops = new MuseumBundle("SummerCropsBundle", "Summer Crops", "hall_of_culture", "Crops harvested in summer.");
            summerCrops.Items.AddRange(new[]
            {
                new MuseumItem("su_armoranth", "Armoranth", "SummerCropsBundle", 11053, ItemRarity.Common, ""),
                new MuseumItem("su_guava_berry", "Guava Berry", "SummerCropsBundle", 11035, ItemRarity.Common, ""),
                new MuseumItem("su_beet", "Beet", "SummerCropsBundle", 12080, ItemRarity.Common, ""),
                new MuseumItem("su_lemon", "Lemon", "SummerCropsBundle", 11052, ItemRarity.Common, ""),
                new MuseumItem("su_chocoberry", "Chocoberry", "SummerCropsBundle", 11054, ItemRarity.Common, ""),
                new MuseumItem("su_pineapple", "Pineapple", "SummerCropsBundle", 11056, ItemRarity.Common, ""),
                new MuseumItem("su_pepper", "Pepper", "SummerCropsBundle", 11008, ItemRarity.Common, ""),
                new MuseumItem("su_melon", "Melon", "SummerCropsBundle", 11057, ItemRarity.Common, ""),
                new MuseumItem("su_stormelon", "Stormelon", "SummerCropsBundle", 11051, ItemRarity.Common, ""),
                new MuseumItem("su_durian", "Durian", "SummerCropsBundle", 11062, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(summerCrops);

            var foragingBundle = new MuseumBundle("ForagingBundle", "Foraging", "hall_of_culture", "Foraged goods from trees and the beach.");
            foragingBundle.Items.AddRange(new[]
            {
                new MuseumItem("fo_apple", "Apple", "ForagingBundle", 3044, ItemRarity.Common, ""),
                new MuseumItem("fo_berry", "Berry", "ForagingBundle", 16500, ItemRarity.Common, ""),
                new MuseumItem("fo_blueberry", "Blueberry", "ForagingBundle", 3046, ItemRarity.Common, ""),
                new MuseumItem("fo_mushroom", "Mushroom", "ForagingBundle", 3001, ItemRarity.Common, ""),
                new MuseumItem("fo_orange", "Orange", "ForagingBundle", 3045, ItemRarity.Common, ""),
                new MuseumItem("fo_peach", "Peach", "ForagingBundle", 3048, ItemRarity.Common, ""),
                new MuseumItem("fo_raspberry", "Raspberry", "ForagingBundle", 3049, ItemRarity.Common, ""),
                new MuseumItem("fo_sand_dollar", "Sand Dollar", "ForagingBundle", 2102, ItemRarity.Common, ""),
                new MuseumItem("fo_seaweed", "Seaweed", "ForagingBundle", 3002, ItemRarity.Common, ""),
                new MuseumItem("fo_starfish", "Starfish", "ForagingBundle", 2103, ItemRarity.Common, ""),
                new MuseumItem("fo_strawberry", "Strawberry", "ForagingBundle", 3047, ItemRarity.Common, ""),
                new MuseumItem("fo_log", "Log", "ForagingBundle", 2002, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(foragingBundle);

            var combatBundle = new MuseumBundle("CombatBundle", "Combat", "hall_of_culture", "Monster trinkets and ancient swords.");
            combatBundle.Items.AddRange(new[]
            {
                new MuseumItem("co_leafie_trinket", "Leafie Trinket", "CombatBundle", 20103, ItemRarity.Uncommon, ""),
                new MuseumItem("co_elite_leafie_trinket", "Elite Leafie Trinket", "CombatBundle", 20104, ItemRarity.Uncommon, ""),
                new MuseumItem("co_centipillar_trinket", "Centipillar Trinket", "CombatBundle", 20105, ItemRarity.Uncommon, ""),
                new MuseumItem("co_peppinch_green_trinket", "Peppinch - Green Trinket", "CombatBundle", 20106, ItemRarity.Uncommon, ""),
                new MuseumItem("co_scorpepper_trinket", "Scorpepper Trinket", "CombatBundle", 20107, ItemRarity.Uncommon, ""),
                new MuseumItem("co_elite_scorpepper_trinket", "Elite Scorpepper Trinket", "CombatBundle", 20108, ItemRarity.Uncommon, ""),
                new MuseumItem("co_hat_crab_trinket", "Hat Crab Trinket", "CombatBundle", 20109, ItemRarity.Uncommon, ""),
                new MuseumItem("co_floaty_crab_trinket", "Floaty Crab Trinket", "CombatBundle", 20110, ItemRarity.Uncommon, ""),
                new MuseumItem("co_bucket_crab_trinket", "Bucket Crab Trinket", "CombatBundle", 20111, ItemRarity.Uncommon, ""),
                new MuseumItem("co_umbrella_crab_trinket", "Umbrella Crab Trinket", "CombatBundle", 20112, ItemRarity.Uncommon, ""),
                new MuseumItem("co_chimchuck_trinket", "Chimchuck Trinket", "CombatBundle", 20113, ItemRarity.Uncommon, ""),
                new MuseumItem("co_ancient_sun_haven_sword", "Ancient Sun Haven Sword", "CombatBundle", 20100, ItemRarity.Rare, ""),
                new MuseumItem("co_ancient_nelvarian_sword", "Ancient Nel'Varian Sword", "CombatBundle", 20101, ItemRarity.Rare, ""),
                new MuseumItem("co_ancient_withergate_sword", "Ancient Withergate Sword", "CombatBundle", 20102, ItemRarity.Rare, ""),
            });
            hallOfCulture.Bundles.Add(combatBundle);

            var alchemyBundle = new MuseumBundle("AlchemyBundle", "Alchemy", "hall_of_culture", "Combat potions from alchemy.");
            alchemyBundle.Items.AddRange(new[]
            {
                new MuseumItem("al_mana_potion", "Mana Potion", "AlchemyBundle", 3080, ItemRarity.Common, ""),
                new MuseumItem("al_health_potion", "Health Potion", "AlchemyBundle", 3081, ItemRarity.Common, ""),
                new MuseumItem("al_attack_potion", "Attack Potion", "AlchemyBundle", 3082, ItemRarity.Common, ""),
                new MuseumItem("al_speed_potion", "Speed Potion", "AlchemyBundle", 3083, ItemRarity.Common, ""),
                new MuseumItem("al_defense_potion", "Defense Potion", "AlchemyBundle", 3084, ItemRarity.Common, ""),
                new MuseumItem("al_advanced_attack", "Advanced Attack Potion", "AlchemyBundle", 3085, ItemRarity.Uncommon, ""),
                new MuseumItem("al_advanced_defense", "Advanced Defense Potion", "AlchemyBundle", 3086, ItemRarity.Uncommon, ""),
                new MuseumItem("al_advanced_spell_damage", "Advanced Spell Damage Potion", "AlchemyBundle", 3087, ItemRarity.Uncommon, ""),
                new MuseumItem("al_incredible_attack", "Incredible Attack Potion", "AlchemyBundle", 3767, ItemRarity.Rare, ""),
                new MuseumItem("al_incredible_defense", "Incredible Defense Potion", "AlchemyBundle", 3768, ItemRarity.Rare, ""),
                new MuseumItem("al_incredible_spell_damage", "Incredible Spell Damage Potion", "AlchemyBundle", 3766, ItemRarity.Rare, ""),
            });
            hallOfCulture.Bundles.Add(alchemyBundle);

            var explorationBundle = new MuseumBundle("ExplorationBundle", "Exploration", "hall_of_culture", "Rare drops from trees and exploration.");
            explorationBundle.Items.AddRange(new[]
            {
                new MuseumItem("ex_petrified_log", "Petrified Log", "ExplorationBundle", 20200, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_phoenix_feather", "Phoenix Feather", "ExplorationBundle", 20201, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_fairy_wings", "Fairy Wings", "ExplorationBundle", 20202, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_griffon_egg", "Griffon Egg", "ExplorationBundle", 20203, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_mana_sap", "Mana Sap", "ExplorationBundle", 20204, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_pumice_stone", "Pumice Stone", "ExplorationBundle", 20205, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_mysterious_antler", "Mysterious Antler", "ExplorationBundle", 20206, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_dragon_fang", "Dragon Fang", "ExplorationBundle", 20207, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_monster_candy", "Monster Candy", "ExplorationBundle", 20208, ItemRarity.Uncommon, ""),
                new MuseumItem("ex_unicorn_hair_tuft", "Unicorn Hair Tuft", "ExplorationBundle", 20209, ItemRarity.Uncommon, ""),
            });
            hallOfCulture.Bundles.Add(explorationBundle);

            var withergateFarming = new MuseumBundle("WithergateFarmingBundle", "Withergate Farming", "hall_of_culture", "Crops from Withergate.");
            withergateFarming.Items.AddRange(new[]
            {
                new MuseumItem("wg_kraken_kale", "Kraken Kale", "WithergateFarmingBundle", 11016, ItemRarity.Common, ""),
                new MuseumItem("wg_tombmelon", "Tombmelon", "WithergateFarmingBundle", 11019, ItemRarity.Common, ""),
                new MuseumItem("wg_suckerstem", "Suckerstem", "WithergateFarmingBundle", 11018, ItemRarity.Common, ""),
                new MuseumItem("wg_razorstalk", "Razorstalk", "WithergateFarmingBundle", 11020, ItemRarity.Common, ""),
                new MuseumItem("wg_snappy_plant", "Snappy Plant", "WithergateFarmingBundle", 11005, ItemRarity.Common, ""),
                new MuseumItem("wg_moonplant", "Moonplant", "WithergateFarmingBundle", 11017, ItemRarity.Common, ""),
                new MuseumItem("wg_eggplant", "Eggplant", "WithergateFarmingBundle", 11015, ItemRarity.Common, ""),
                new MuseumItem("wg_demon_orb", "Demon Orb", "WithergateFarmingBundle", 11004, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(withergateFarming);

            var nelvariFarming = new MuseumBundle("NelvariFarmingBundle", "Nel'Vari Farming", "hall_of_culture", "Crops from Nel'Vari.");
            nelvariFarming.Items.AddRange(new[]
            {
                new MuseumItem("nv_acorn", "Acorn", "NelvariFarmingBundle", 11031, ItemRarity.Common, ""),
                new MuseumItem("nv_rock_fruit", "Rock Fruit", "NelvariFarmingBundle", 11025, ItemRarity.Common, ""),
                new MuseumItem("nv_water_fruit", "Water Fruit", "NelvariFarmingBundle", 11023, ItemRarity.Common, ""),
                new MuseumItem("nv_fire_fruit", "Fire Fruit", "NelvariFarmingBundle", 11024, ItemRarity.Common, ""),
                new MuseumItem("nv_walk_choy", "Walk Choy", "NelvariFarmingBundle", 11028, ItemRarity.Common, ""),
                new MuseumItem("nv_wind_chime", "Wind Chime", "NelvariFarmingBundle", 11026, ItemRarity.Common, ""),
                new MuseumItem("nv_shiiwalki_mushroom", "Shiiwalki Mushroom", "NelvariFarmingBundle", 11029, ItemRarity.Common, ""),
                new MuseumItem("nv_dragon_fruit", "Dragon Fruit", "NelvariFarmingBundle", 11027, ItemRarity.Common, ""),
                new MuseumItem("nv_mana_gem", "Mana Gem", "NelvariFarmingBundle", 11033, ItemRarity.Common, ""),
                new MuseumItem("nv_cat_tail", "Cat Tail", "NelvariFarmingBundle", 11032, ItemRarity.Common, ""),
                new MuseumItem("nv_indiglow", "Indiglow", "NelvariFarmingBundle", 11030, ItemRarity.Common, ""),
            });
            hallOfCulture.Bundles.Add(nelvariFarming);

            sections.Add(hallOfCulture);

            // ==================== AQUARIUM ====================
            // Bundle IDs: Wish.MuseumCurator.aquaticMuseumProgress. Fishing relics: Wish.FishingRod.fishingMuseumItems. Fish IDs: Wish.ItemID; tank fish sets from wiki museum pages.
            var aquarium = new MuseumSection("aquarium", "Aquarium", "Fish and aquatic life from all waters and seasons.");

            var fishingBundle = new MuseumBundle("FishingBundle", "Fishing (Relics)", "aquarium", "Rare fishing relics and treasures.");
            fishingBundle.Items.AddRange(new[]
            {
                new MuseumItem("fishing_relic_20150", "Handmade Bobber", "FishingBundle", 20150, ItemRarity.Rare, "A handmade fishing bobber."),
                new MuseumItem("fishing_relic_20151", "Ancient Magic Staff", "FishingBundle", 20151, ItemRarity.Rare, "An ancient magic staff."),
                new MuseumItem("fishing_relic_20152", "Bronze Dragon Relic", "FishingBundle", 20152, ItemRarity.Rare, "A bronze dragon relic."),
                new MuseumItem("fishing_relic_20153", "Old Sword Hilt", "FishingBundle", 20153, ItemRarity.Rare, "An old sword hilt."),
                new MuseumItem("fishing_relic_20154", "Ancient Almari Totem", "FishingBundle", 20154, ItemRarity.Rare, "An ancient Almari totem."),
                new MuseumItem("fishing_relic_20155", "Ancient Angel Quill", "FishingBundle", 20155, ItemRarity.Rare, "An ancient angel quill."),
                new MuseumItem("fishing_relic_20156", "Ancient Elven Headdress", "FishingBundle", 20156, ItemRarity.Rare, "An ancient elven headdress."),
                new MuseumItem("fishing_relic_20157", "Ancient Naga Crook", "FishingBundle", 20157, ItemRarity.Rare, "An ancient Naga crook."),
                new MuseumItem("fishing_relic_20158", "Nel'Varian Runestone", "FishingBundle", 20158, ItemRarity.Rare, "A Nel'Varian runestone."),
                new MuseumItem("fishing_relic_20159", "Old Mayoral Painting", "FishingBundle", 20159, ItemRarity.Rare, "An old mayoral painting."),
                new MuseumItem("fishing_relic_20160", "Tentacle Monster Emblem", "FishingBundle", 20160, ItemRarity.Rare, "A tentacle monster emblem."),
            });
            aquarium.Bundles.Add(fishingBundle);

            var bigTank = new MuseumBundle("MuseumAquariumBigTank", "Large Tank (Sun Haven)", "aquarium", "Year-round Sun Haven fish.");
            bigTank.Items.AddRange(new[]
            {
                new MuseumItem("bt_pygmy_tuna", "Pygmy Tuna", "MuseumAquariumBigTank", 15023, ItemRarity.Common, ""),
                new MuseumItem("bt_catfish", "Catfish", "MuseumAquariumBigTank", 15018, ItemRarity.Common, ""),
                new MuseumItem("bt_gold_fish", "Gold Fish", "MuseumAquariumBigTank", 15008, ItemRarity.Common, ""),
                new MuseumItem("bt_streamline_cod", "Streamline Cod", "MuseumAquariumBigTank", 15014, ItemRarity.Common, ""),
                new MuseumItem("bt_salmon", "Salmon", "MuseumAquariumBigTank", 15085, ItemRarity.Common, ""),
                new MuseumItem("bt_clown_fish", "Clown Fish", "MuseumAquariumBigTank", 15083, ItemRarity.Common, ""),
                new MuseumItem("bt_black_bass", "Black Bass", "MuseumAquariumBigTank", 15084, ItemRarity.Common, ""),
                new MuseumItem("bt_rainbow_trout", "Rainbow Trout", "MuseumAquariumBigTank", 15004, ItemRarity.Common, ""),
                new MuseumItem("bt_popeye_goldfish", "Popeye Goldfish", "MuseumAquariumBigTank", 15082, ItemRarity.Common, ""),
                new MuseumItem("bt_pufferfish", "Pufferfish", "MuseumAquariumBigTank", 15007, ItemRarity.Common, ""),
                new MuseumItem("bt_ironhead_sturgeon", "Ironhead Sturgeon", "MuseumAquariumBigTank", 15024, ItemRarity.Common, ""),
                new MuseumItem("bt_cuddlefish", "Cuddlefish", "MuseumAquariumBigTank", 15022, ItemRarity.Common, ""),
                new MuseumItem("bt_lobster", "Lobster", "MuseumAquariumBigTank", 15088, ItemRarity.Common, ""),
                new MuseumItem("bt_silver_carp", "Silver Carp", "MuseumAquariumBigTank", 15012, ItemRarity.Common, ""),
                new MuseumItem("bt_tuna", "Tuna", "MuseumAquariumBigTank", 15087, ItemRarity.Common, ""),
                new MuseumItem("bt_blunted_swordfish", "Blunted Swordfish", "MuseumAquariumBigTank", 15017, ItemRarity.Common, ""),
                new MuseumItem("bt_ribbon_eel", "Ribbon Eel", "MuseumAquariumBigTank", 15089, ItemRarity.Common, ""),
                new MuseumItem("bt_tiger_trout", "Tiger Trout", "MuseumAquariumBigTank", 15086, ItemRarity.Common, ""),
                new MuseumItem("bt_eel", "Eel", "MuseumAquariumBigTank", 15002, ItemRarity.Common, ""),
                new MuseumItem("bt_red_snapper", "Red Snapper", "MuseumAquariumBigTank", 15011, ItemRarity.Common, ""),
                new MuseumItem("bt_carp", "Carp", "MuseumAquariumBigTank", 15010, ItemRarity.Common, ""),
                new MuseumItem("bt_redeye_piranha", "Redeye Piranha", "MuseumAquariumBigTank", 15016, ItemRarity.Common, ""),
                new MuseumItem("bt_angel_fish", "Angel Fish", "MuseumAquariumBigTank", 15005, ItemRarity.Common, ""),
                new MuseumItem("bt_whitebelly_shark", "Whitebelly Shark", "MuseumAquariumBigTank", 15013, ItemRarity.Common, ""),
                new MuseumItem("bt_koi_fish", "Koi Fish", "MuseumAquariumBigTank", 15090, ItemRarity.Common, ""),
                new MuseumItem("bt_sandstone_fish", "Sandstone Fish", "MuseumAquariumBigTank", 2118, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(bigTank);

            var springTank = new MuseumBundle("MuseumAquariumSpring", "Spring Tank", "aquarium", "Spring season fish in Sun Haven.");
            springTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_sp_butterflyfish", "Butterflyfish", "MuseumAquariumSpring", 15117, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_sunfish", "Sunfish", "MuseumAquariumSpring", 15116, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_flower_flounder", "Flower Flounder", "MuseumAquariumSpring", 15114, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_raincloud_ray", "Raincloud Ray", "MuseumAquariumSpring", 15118, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_floral_trout", "Floral Trout", "MuseumAquariumSpring", 15119, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_neon_tetra", "Neon Tetra", "MuseumAquariumSpring", 15121, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_seahorse", "Seahorse", "MuseumAquariumSpring", 15122, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_painted_egg", "Painted Egg", "MuseumAquariumSpring", 15123, ItemRarity.Common, ""),
                new MuseumItem("aq_sp_tadpole", "Tadpole", "MuseumAquariumSpring", 15124, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(springTank);

            var summerTank = new MuseumBundle("MuseumAquariumSummer", "Summer Tank", "aquarium", "Summer season fish in Sun Haven.");
            summerTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_su_blazeel", "Blazeel", "MuseumAquariumSummer", 15104, ItemRarity.Common, ""),
                new MuseumItem("aq_su_hearth_angler", "Hearth Angler", "MuseumAquariumSummer", 15106, ItemRarity.Common, ""),
                new MuseumItem("aq_su_scorching_squid", "Scorching Squid", "MuseumAquariumSummer", 15107, ItemRarity.Common, ""),
                new MuseumItem("aq_su_magma_star", "Magma Star", "MuseumAquariumSummer", 15108, ItemRarity.Common, ""),
                new MuseumItem("aq_su_tinder_turtle", "Tinder Turtle", "MuseumAquariumSummer", 15109, ItemRarity.Common, ""),
                new MuseumItem("aq_su_pyrelus", "Pyrelus", "MuseumAquariumSummer", 15110, ItemRarity.Common, ""),
                new MuseumItem("aq_su_flame_ray", "Flame Ray", "MuseumAquariumSummer", 15111, ItemRarity.Common, ""),
                new MuseumItem("aq_su_molten_slug", "Molten Slug", "MuseumAquariumSummer", 15112, ItemRarity.Common, ""),
                new MuseumItem("aq_su_searback", "Searback", "MuseumAquariumSummer", 15113, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(summerTank);

            var fallTank = new MuseumBundle("MuseumAquariumFall", "Fall Tank", "aquarium", "Fall season fish in Sun Haven.");
            fallTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_fa_coducopia", "Coducopia", "MuseumAquariumFall", 15125, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_king_salmon", "King Salmon", "MuseumAquariumFall", 15126, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_hayfish", "Hayfish", "MuseumAquariumFall", 15127, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_acorn_anchovy", "Acorn Anchovy", "MuseumAquariumFall", 15128, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_vampire_piranha", "Vampire Piranha", "MuseumAquariumFall", 15131, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_ghostfish", "Ghostfish", "MuseumAquariumFall", 15132, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_pumpkin_jelly", "Pumpkin Jelly", "MuseumAquariumFall", 15133, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_pirate_perch", "Pirate Perch", "MuseumAquariumFall", 15134, ItemRarity.Common, ""),
                new MuseumItem("aq_fa_autumn_leaf_sole", "Autumn Leaf Sole", "MuseumAquariumFall", 15135, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(fallTank);

            var winterTank = new MuseumBundle("MuseumAquariumWinter", "Winter Tank", "aquarium", "Winter season fish in Sun Haven.");
            winterTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_wi_frostfin", "Frostfin", "MuseumAquariumWinter", 15094, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_christmas_lightfish", "Christmas Lightfish", "MuseumAquariumWinter", 15095, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_holly_carp", "Holly Carp", "MuseumAquariumWinter", 15096, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_jingle_bass", "Jingle Bass", "MuseumAquariumWinter", 15097, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_frozen_tuna", "Frozen Tuna", "MuseumAquariumWinter", 15098, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_scarffish", "Scarffish", "MuseumAquariumWinter", 15099, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_heatfin", "Heatfin", "MuseumAquariumWinter", 15100, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_icicle_carp", "Icicle Carp", "MuseumAquariumWinter", 15101, ItemRarity.Common, ""),
                new MuseumItem("aq_wi_blazing_herring", "Blazing Herring", "MuseumAquariumWinter", 15102, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(winterTank);

            var nelvariTank = new MuseumBundle("MuseumAquariumNelvari", "Nel'Vari Tank", "aquarium", "Fish from Nel'Vari waters.");
            nelvariTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_nv_robed_parrotfish", "Robed Parrotfish", "MuseumAquariumNelvari", 15041, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_axolotl", "Axolotl", "MuseumAquariumNelvari", 15055, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_frilled_betta", "Frilled Betta", "MuseumAquariumNelvari", 15054, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_horsefish", "Horsefish", "MuseumAquariumNelvari", 15053, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_flamefish", "Flamefish", "MuseumAquariumNelvari", 15036, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_dragon_gulper", "Dragon Gulper", "MuseumAquariumNelvari", 15040, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_neapolitan_fish", "Neapolitan Fish", "MuseumAquariumNelvari", 15056, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_snobfish", "Snobfish", "MuseumAquariumNelvari", 15042, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_kelp_eel", "Kelp Eel", "MuseumAquariumNelvari", 15045, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_princely_frog", "Princely Frog", "MuseumAquariumNelvari", 15058, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_angelfin", "Angelfin", "MuseumAquariumNelvari", 15059, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_bubblefish", "Bubblefish", "MuseumAquariumNelvari", 15060, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_crystal_tetra", "Crystal Tetra", "MuseumAquariumNelvari", 15044, ItemRarity.Common, ""),
                new MuseumItem("aq_nv_sky_ray", "Sky Ray", "MuseumAquariumNelvari", 15046, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(nelvariTank);

            var withergateTank = new MuseumBundle("MuseumAquariumWithergate", "Withergate Tank", "aquarium", "Fish from Withergate waters.");
            withergateTank.Items.AddRange(new[]
            {
                new MuseumItem("aq_wg_kraken", "Kraken", "MuseumAquariumWithergate", 15070, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_water_bear", "Water Bear", "MuseumAquariumWithergate", 15065, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_bonemouth_bass", "Bonemouth Bass", "MuseumAquariumWithergate", 15028, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_mummy_trout", "Mummy Trout", "MuseumAquariumWithergate", 15069, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_deadeye_shrimp", "Deadeye Shrimp", "MuseumAquariumWithergate", 15033, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_electric_eel", "Electric Eel", "MuseumAquariumWithergate", 15066, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_brain_jelly", "Brain Jelly", "MuseumAquariumWithergate", 15068, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_redfinned_pincher", "Redfinned Pincher", "MuseumAquariumWithergate", 15067, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_sea_bat", "Sea Bat", "MuseumAquariumWithergate", 15071, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_ghosthead_tuna", "Ghosthead Tuna", "MuseumAquariumWithergate", 15073, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_globfish", "Globfish", "MuseumAquariumWithergate", 15072, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_living_jelly", "Living Jelly", "MuseumAquariumWithergate", 15031, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_purrmaid", "Purrmaid", "MuseumAquariumWithergate", 15037, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_slime_leech", "Slime Leech", "MuseumAquariumWithergate", 15035, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_goblin_shark", "Goblin Shark", "MuseumAquariumWithergate", 15074, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_moonfish", "Moonfish", "MuseumAquariumWithergate", 15076, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_toothy_angler", "Toothy Angler", "MuseumAquariumWithergate", 15030, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_vampire_squid", "Vampire Squid", "MuseumAquariumWithergate", 15075, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_viperfish", "Viperfish", "MuseumAquariumWithergate", 15077, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_albino_squid", "Albino Squid", "MuseumAquariumWithergate", 15079, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_devilfin", "Devilfin", "MuseumAquariumWithergate", 15080, ItemRarity.Common, ""),
                new MuseumItem("aq_wg_shadow_tuna", "Shadow Tuna", "MuseumAquariumWithergate", 15029, ItemRarity.Common, ""),
            });
            aquarium.Bundles.Add(withergateTank);

            sections.Add(aquarium);

            return sections;
        }

        /// <summary>
        /// Adds a bundle with placeholder slots so counts match the game. Item names are not in code (they live in Unity assets);
        /// Sync uses the game's progress key (bundle Id) and will mark all slots when the bundle is complete.
        /// </summary>
        private static void AddPlaceholderBundle(MuseumSection section, string bundleId, string displayName, int slotCount)
        {
            var bundle = new MuseumBundle(bundleId, displayName, section.Id, $"Donate all {slotCount} items to complete this bundle.");
            for (int i = 1; i <= slotCount; i++)
            {
                bundle.Items.Add(new MuseumItem(
                    bundleId + "_slot_" + i,
                    "Item " + i,
                    bundleId,
                    -1, // Placeholder: real item IDs live in Unity assets; -1 avoids matching in FindByGameItemId
                    ItemRarity.Common,
                    "Slot " + i + " (see in-game museum for exact item)."));
            }
            section.Bundles.Add(bundle);
        }

        /// <summary>
        /// Gets a flattened list of all museum items.
        /// </summary>
        public static List<MuseumItem> GetAllItems()
        {
            var items = new List<MuseumItem>();
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    items.AddRange(bundle.Items);
                }
            }
            return items;
        }

        /// <summary>
        /// Gets all bundle IDs across all sections.
        /// </summary>
        public static List<string> GetAllBundleIds()
        {
            var ids = new List<string>();
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    ids.Add(bundle.Id);
                }
            }
            return ids;
        }

        /// <summary>
        /// Gets all items within a specific bundle.
        /// </summary>
        public static List<MuseumItem> GetItemsInBundle(string bundleId)
        {
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    if (bundle.Id == bundleId)
                        return bundle.Items;
                }
            }
            return new List<MuseumItem>();
        }

        /// <summary>
        /// Finds a bundle by its game progress key.
        /// </summary>
        public static MuseumBundle FindBundleByProgressKey(string progressKey)
        {
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    if (bundle.Id == progressKey)
                        return bundle;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the first museum row with this game item ID (section/bundle order).
        /// When the same ID appears in multiple bundles (e.g. Pepper in Spring and Summer), use <see cref="FindByGameItemIdInBundle"/>.
        /// </summary>
        public static MuseumItem FindByGameItemId(int gameItemId)
        {
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    foreach (var item in bundle.Items)
                    {
                        if (item.GameItemId == gameItemId)
                            return item;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a museum item by game item ID within a specific bundle (progress key / bundle id).
        /// </summary>
        public static MuseumItem FindByGameItemIdInBundle(int gameItemId, string bundleId)
        {
            if (string.IsNullOrEmpty(bundleId)) return null;
            foreach (var item in GetItemsInBundle(bundleId))
            {
                if (item.GameItemId == gameItemId)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Lists fish (game id + display name) for an aquarium bundle from static museum content.
        /// </summary>
        public static List<(int GameItemId, string Name)> GetResolvedAquariumItems(string bundleId)
        {
            var rows = GetItemsInBundle(bundleId);
            if (rows == null || rows.Count == 0) return null;

            var list = new List<(int, string)>();
            foreach (var i in rows)
            {
                if (i.GameItemId > 0)
                    list.Add((i.GameItemId, i.Name));
            }
            return list.Count > 0 ? list : null;
        }

        /// <summary>
        /// Finds an item by its ID.
        /// </summary>
        public static MuseumItem FindById(string itemId)
        {
            foreach (var section in GetAllSections())
            {
                foreach (var bundle in section.Bundles)
                {
                    foreach (var item in bundle.Items)
                    {
                        if (item.Id == itemId)
                            return item;
                    }
                }
            }
            return null;
        }
    }
}
