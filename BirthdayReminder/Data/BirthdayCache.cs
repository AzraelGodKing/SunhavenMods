using System;
using System.Collections.Generic;
using System.Linq;

namespace BirthdayReminder.Data
{
    /// <summary>
    /// Static cache of all NPC birthdays and gift preferences
    /// </summary>
    public static class BirthdayCache
    {
        // Universal gifts that work for all NPCs
        public static readonly List<string> UniversalLoved = new List<string>
        {
            "Blue Rose Bouquet", "Red Rose Bouquet", "Black Diamond", "Havenite"
        };

        public static readonly List<string> UniversalLiked = new List<string>
        {
            "BLT", "Caribbean Green Soup", "Cheeseburger", "Cheesecake", "Cinnamon Apple Pie",
            "Diamond", "Pizza", "Pot Pie", "Red Veggie Soup", "Shimmeroot Treat",
            "Spicy Ramen", "Spring Roll", "Tomato Salad"
        };

        private static readonly List<NPCBirthday> _allBirthdays;

        static BirthdayCache()
        {
            _allBirthdays = new List<NPCBirthday>
            {
                // === SPRING ===
                CreateBirthday("Darius", "Spring", 1,
                    new[] { "Blue Rose Bouquet", "Devilfin", "Enchanted Glorite Bar", "Enhanced Glorite Bar", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Ghost Pepper", "Glorite Bar", "Glorite Ore", "Glorite Sword", "Red Rose Bouquet", "Steak", "Sunite Sword", "Sweet and Spicy Shrimp", "Withercake" },
                    new[] { "Adamant Sword", "Armoranth", "Bone Gift", "Cinnaberry", "Copper Sword", "Darkness Essence", "Diamond", "Ghostly Great Sword", "Iron Sword", "Legendary Great Sword", "Legendary Hammer", "Lightning Hammer", "Mithril Sword", "Orchid", "Pepper", "Pepper Great Sword", "Potato Hammer", "Red Velvet Cake", "Red Velvet Cupcake", "Spicy Ramen", "Spiked Salmon" }),

                CreateBirthday("Lynn", "Spring", 10,
                    new[] { "Anvil", "Cream Seltzer", "Creamy Beef Stew", "Creamy Mushroom Soup", "Egg Hash", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Glorite Bar", "Hot Chocolate", "Mana Anvil", "Mithril Bar", "Monster Anvil", "Pot Pie", "Stuffed Casserole", "Sunite Bar", "Withergate Anvil" },
                    new[] { "Adamant Bar", "Apple Juice", "Copper Bar", "Fizzy Seltzer", "Glass of Pure Water", "Iron Bar", "Milk", "Mithril Bar", "Pickled Veggie Salad", "Veggie Kebab" }),

                CreateBirthday("Nathaniel", "Spring", 13,
                    new[] { "Adamant Sword", "City Guard Chest Plate", "City Guard Gloves", "City Guard Helmet", "City Guard Pants", "Copper Sword", "Cream Seltzer", "Creamy Mushroom Soup", "Dragon Mail Boots", "Dragon Mail Cape", "Dragon Mail Chest Plate", "Dragon Mail Gloves", "Dragon Mail Helmet", "Enchanted Light Boots", "Enchanted Light Chest", "Enchanted Light Gloves", "Enchanted Light Helmet", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Glorite Sword", "Hearty Pie", "Iron Sword", "Lasagna", "Legendary Great Sword", "Legendary Hammer", "Mashed Potatoes", "Mithril Sword", "Multiplate Chest", "Multiplate Helmet", "Multiplate Pants", "Pot Pie", "Steak", "Sunite Sword" },
                    new[] { "Adamant Helmet", "Apple Juice", "Armoranth", "Copper Helmet", "Copper Ore", "Ghostly Great Sword", "Iron Helmet", "Iron Ore", "Lightning Hammer", "Milk", "Mithril Helmet", "Pepper Great Sword", "Potato Hammer", "Sunite Helmet", "Veggie Kebab" }),

                CreateBirthday("Wesley", "Spring", 18,
                    new[] { "Blue Roses Honey", "Cooled Lava Honey", "Daisy Honey", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Grapes", "Hibiscus Honey", "Honey", "Honeybrew", "Honeycomb Cake", "Honeyglazed Apple", "Lavender Honey", "Lily Honey", "Lotus Honey", "Orchid Honey", "Red Roses Honey", "Snobfish", "Sun Flower Honey", "Tulip Honey", "Walk Choy", "Walk Choy Pet" },
                    new[] { "Advanced Compost", "Advanced Earth Fertilizer", "Advanced Fire Fertilizer", "Advanced Magic Fertilizer", "Advanced Water Fertilizer", "Bubble Tea", "Compost", "Dragon Tea", "Earth Dragon Tea", "Elven Compost", "Fire Dragon Tea", "Grape Juice", "Honeysuckle", "Indiglow Tea", "Lily", "Mana Fertilizer", "Simple Earth Fertilizer", "Simple Fire Fertilizer", "Simple Magic Fertilizer", "Simple Sand Fertilizer", "Simple Water Fertilizer", "Slime Fertilizer", "Tea", "Water Dragon Tea", "Wind Chime Tea" }),

                CreateBirthday("Claude", "Spring", 24,
                    new[] { "Apple Pie", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Red Veggie Soup", "Tomato", "Tomato Bread", "Tomato Juice", "Tomato Salad", "Tomato Soup", "Vampire Piranha", "Vampire Squid" },
                    new[] { "Apple", "Apple Juice", "Cinnamon Apple Pie", "Claude's Performance Record", "Record Player", "Red Veggie Soup", "Spaghetti" }),

                // === SUMMER ===
                CreateBirthday("Liam", "Summer", 5,
                    new[] { "Chef Hat (Black)", "Chef Hat (Blue)", "Chef Hat (Pink)", "Chef Hat (Red)", "Chef Hat (White)", "Coffee", "Demon Coffee", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Giant Blue Bunny Plushie", "Giant Pink Bunny Plushie", "Giant Pink Teddy Plushie", "Giant Purple Teddy Plushie", "Giant Teddy Plushie", "Hot Chocolate" },
                    new[] { "Almond Croissant", "Barley", "Cinnamon Spice Latte", "Coal", "Cookies", "Fire Crystal", "Flour", "Hearty Pie", "Log", "Scythe", "Sunflower", "Wheat" }),

                CreateBirthday("Shang", "Summer", 9,
                    new[] { "Adamant Key", "Adamant Ring", "Adamant Sword", "Advanced Attack Potion", "Advanced Defense Potion", "Blunted Swordfish", "Earth Dragon Tea", "Fire Dragon Tea", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Glorite Sword", "Hearty Armor Pie", "Hearty Pie", "Incredible Attack Potion", "Incredible Defense Potion", "Indiglow Tea", "Mithril Sword", "Razor Stalk Tea", "Sunite Sword", "Swordfish Sashimi", "Tea", "Water Dragon Tea", "Wind Chime Tea" },
                    new[] { "Adamant Key", "Armoranth", "Attack Potion", "Carrot Juice", "Copper Key", "Copper Ring", "Copper Sword", "Defense Potion", "Dock Worker's Bandana", "Energy Smoothie", "Festive Oranges", "Glass of Pure Water", "Glorite Key", "Health Potion", "Iron Key", "Iron Ring", "Iron Sword", "Kale Juice", "Mithril Key", "Moonfish", "Rusty Key", "Sunite Key", "Tea Leaves", "Wind Chime", "Yucky Green Juice" }),

                CreateBirthday("Elyssia", "Summer", 11,
                    new[] { "Apple Pie", "Cookies", "Cuckoo Wall Clock", "Flan", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Fried Rice", "Fruit Tart", "Grandfather Clock", "Grilled Cheese", "Omelet", "Potato Salad", "Spellberry Juice", "Spellberry Tartlets", "Sugar Apple Herbal Soup", "Sugar Apple Ice Cream", "Sugar Apple Juice", "Triceratops Plushie", "Turtle Plushie", "Wind Chime Tea" },
                    new[] { "Antler", "Crystal Fruit", "Dragon Scale", "Earth Crystal", "Earth Rune", "Hexfruit", "Mantel Clock", "Refined Glass", "Refined Metal", "Round Wall Clock", "Sand Dollar", "Scrambled Eggs", "Small Mana Tome", "Spellberry", "Sugar Apple", "Tea", "Tea Leaves" }),

                CreateBirthday("Karish", "Summer", 17,
                    new[] { "Adamant Chest Plate", "Adamant Sword", "Crab Roll", "Enchanted Fishing Rod", "Fishing Skill Tome", "Fishing Totem", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Fried Carp", "Grilled Carp", "Grilled Crab", "Large Fishing Net", "Legendary Fish Bait", "Poke Bowl", "Roasted Tuna", "Tuna Nigiri", "Tuna Sashimi", "Very Good Fishing Rod" },
                    new[] { "Basic Fishing Rod", "Blunted Swordfish", "Carp", "Copper Chest Plate", "Copper Sword", "Ghostly Great Sword", "Handmade Bobber", "Iron Chest Plate", "Iron Sword", "Ironhead Sturgeon", "Legendary Great Sword", "Legendary Hammer", "Lightning Hammer", "Pepper Great Sword", "Potato Hammer", "Salmon", "Sea Bass", "Silver Carp", "Small Fishing Net", "Sweet Fish Bait", "Tuna", "Worm" }),

                CreateBirthday("Lucia", "Summer", 20,
                    new[] { "Cream Seltzer", "Fire Crystal", "Fizzy Seltzer", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Large Mana Tome", "Small Mana Tome", "Sunite Ore", "Tikka Masala" },
                    new[] { "Blazeel", "Energy Smoothie", "Flame Ray", "Greenspice", "Hearth Angler", "Heat Fruit", "Hot Sauce", "Inferno Guppy", "Magma Star", "Molten Slug", "Pepper", "Pyrelus", "Scorching Squid", "Searback", "Spicy Catsup", "Spicy Ramen", "Spicy Shrimp Ramen", "Strawberry Juice", "Sweet and Spicy Shrimp", "Young Mage Hat", "Young Mage Robe", "Young Mage Skirt" }),

                CreateBirthday("Iris", "Summer", 22,
                    new[] { "Amazing Elixir of Farming", "Bitter Seltzer", "Blue Rose Bouquet", "Blueberry Smoothie", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Grape Juice", "Lotus", "Pricklepop Pear", "Prickletot Pear", "Red Rose Bouquet", "Small Mana Tome", "Spicy Ramen" },
                    new[] { "Advanced Earth Fertilizer", "Advanced Fire Fertilizer", "Advanced Magic Fertilizer", "Amethyst", "Diamond", "Honeysuckle", "Lily", "Mana Fertilizer", "Oak Tree Seeds", "Ruby", "Sapphire", "Simple Earth Fertilizer", "Simple Fire Fertilizer", "Simple Magic Fertilizer", "Simple Sand Fertilizer" }),

                CreateBirthday("Catherine", "Summer", 25,
                    new[] { "Berry Pie", "Blue Moon Fruit", "Blueberry Salad", "Carrot Cake", "Carrot Juice", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Glorite Watering Can", "Lavender", "Mushroom Pie", "Pumpkin", "Shimmeroot", "Sunite Watering Can" },
                    new[] { "Adamant Watering Can", "Beet", "Candy Cane", "Caribbean Green Soup", "Carrot", "Cooking Pot", "Copper Watering Can", "Earth Crystal", "Flan", "Iron Watering Can", "Mithril Watering Can", "Red Veggie Soup", "Shimmeroot", "Small Mana Tome", "Tadpole" }),

                // === FALL ===
                CreateBirthday("Wornhardt", "Fall", 4,
                    new[] { "Caribbean Green Soup", "Citrus Salad", "Cobb Salad", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Honeyglazed Apple", "Kale Juice", "Wooden Windmill", "Yucky Green Juice" },
                    new[] { "BLT", "Beginner's Apple Pie", "Blueberry Muffin", "Cheeseburger", "Coffee", "Lava Brew", "Roasted Turnip", "Trail Mix" }),

                CreateBirthday("Zaria", "Fall", 8,
                    new[] { "Adamant Key", "Adamant Ring", "Elemental Cocktail", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Golden Water Rune", "Lemon Cake", "Lemon Meringue", "Lemonade", "Mithril Key", "Mithril Ring", "Sour Plum Jam", "Sour Plum Soda", "Sour Plum Sorbet", "Sour Plum Tea", "Water Dragon Tea", "Water Fruit", "Watermelon" },
                    new[] { "Coconut Water", "Copper Key", "Copper Ring", "Fairy Cherry Water", "Ghostly Great Sword", "Glass of Pure Water", "Iron Key", "Iron Ring", "Kiwi Berry", "Legendary Great Sword", "Lemon", "Magical Water", "Mana Water", "Pepper Great Sword", "Sour Plum", "Water Crystal", "Water Rune", "Zaria's Empty Bucket" }),

                CreateBirthday("Xyla", "Fall", 11,
                    new[] { "Crown", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Globfish", "Heavy Crossbow", "Purrmaid", "Red Velvet Cake", "Snappy Plant", "Withercake" },
                    new[] { "Chess Board", "Fancy Hot Dog", "Loaded Hot Dog", "Plain Hot Dog", "Red Velvet Cupcake", "Refined Concrete", "Refined Glass", "Refined Plastic" }),

                CreateBirthday("Donovan", "Fall", 17,
                    new[] { "Bark Fish", "Blue Rose Bouquet", "Bone Gift", "Bonemouth Bass", "Fancy Hot Dog", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Hardwood", "Hardwood Plank", "Ice Cream", "Meat", "Nachos", "Paper Bag Disguise", "Pizza", "Red Rose Bouquet", "Wood Plank" },
                    new[] { "Apple Core", "BLT", "Cheeseburger", "Hot Sauce", "Loaded Hot Dog", "Log", "Old Boot", "Steak", "Tin Can", "Vivi's Bone Dagger", "Waffles" }),

                CreateBirthday("Lucius", "Fall", 20,
                    new[] { "Berry Cake", "Berry Pie", "Blue Moon Fruit", "Blueberry Cake", "Blueberry Pie", "Blueberry Smoothie", "Candelabra", "Enchanted Iron Bar", "Firefly Jar", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Magically Delicious Drink", "Magma Star", "Moon Cake", "Moon Cream", "Moon Cream Pie", "Moon Mailbox", "Moonfish", "Moonplant Cola", "Shadow Tuna", "Sky Ray", "Star Fruit", "Starfish" },
                    new[] { "Berry", "Blueberry", "Elven Steel Bar", "Elven Steel Ore", "Glow Ring", "Iron Bar", "Iron Ore", "Lantern", "Lightning in a Bottle", "Mana Bloom Vase", "Moonplant", "Night Small Painting", "Noodles", "Orchid", "Star Fruit Seeds", "Swirly Night Painting" }),

                CreateBirthday("Vivi", "Fall", 23,
                    new[] { "Acorn", "Acorn Anchovy", "Acorn Milk", "Barrel Of Swords", "Berry Pie", "Carrot Sword", "Cinnamon Apple Pie", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Fruit Snacks", "Glorite Sword", "Gold Bar", "Health Woven Scarf", "Raspberry Pie", "Rel'Tar's Mark (Crossbow)", "Seared Acorn Anchovy", "Sunite Sword", "Trail Mix" },
                    new[] { "Adamant Sword", "Advanced Attack Potion", "Attack Potion", "Berry", "Chocolate Mousse Cake", "Copper Sword", "Creamy Mushroom Soup", "Crossbow", "Egg Crossbow", "Elven Crossbow", "Ghostly Great Sword", "Gold Ore", "Heavy Crossbow", "Incredible Attack Potion", "Iron Sword", "Legendary Great Sword", "Mithril Crossbow", "Mithril Sword", "Mushroom", "Mushroom Pie", "Mushroom Risotto", "Mushroom Stroganoff", "Pepper Great Sword", "Small Money Bag" }),

                CreateBirthday("Thorian", "Fall", 26,
                    new[] { "Avocado Toast", "Blue Rose Cake", "Bubble Tea", "Caramel Creme Brulee", "Coffee", "Conjurmelon Bruschetta", "Eclair", "Fancy Grape Juice", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Indiglow Tea", "Mana Gem Jam", "Seared Lobster", "Souffle", "Wind Chime Tea" },
                    new[] { "Avocado", "Bitter Seltzer", "Black Cat Painting", "Blue Rose", "Caramel", "Cream Seltzer", "Dragon Tea", "Dreamy Painting", "Elegant Vanity Painting", "Fizzy Seltzer", "Lavender", "Lily", "Lotus", "Meatloaf Painting", "Moody Flower Painting", "Night Small Painting", "Orchid", "Organic Painting", "Peaceful Waterfall Painting", "Razor Stalk Tea", "Red Rose", "Spiced Cocoa", "Spooky Painting", "Sunflower Field Painting", "Sunny Day Small Painting", "Sunset Small Painting", "Swirly Night Painting", "Tea", "Tree Oval Painting", "Wild Mushrooms Painting", "Wildflowers Painting" }),

                // === WINTER ===
                CreateBirthday("Vaan", "Winter", 3,
                    new[] { "Flan", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Large Mana Tome", "Leaf Sole", "Red Veggie Soup", "Sky Ray", "Small Mana Tome", "Two Peas and a Pod", "Water Crystal" },
                    new[] { "Adventurer Cape", "Black Cape", "Cape of Combat", "Cape of Exploration", "Cape of Farming", "Cape of Mining", "Caribbean Green Soup", "Grapes", "Gray Cape", "Green Cape", "Honey", "Lightning in a Bottle", "Majestic Cape", "Moonlight Cape", "Orange Cape", "Peas", "Red Cape", "Roasted Turnip", "Sunset Cape", "Zeppelin Pilot's Hat" }),

                CreateBirthday("Miyeon", "Winter", 6,
                    new[] { "Angelfin", "Berry Cake", "Blue Rose Cake", "Blueberry Cake", "Bubble Tea", "Flower Flounder", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Honeycomb Cake", "Honeysuckle", "Hydrangea Cupcake", "Indiglow Tea", "Lemon Cake", "Orange Cream Cake", "Small Potted Bush", "Snappy Juice", "Strawberry Cake", "Strawberry Shortcake", "Sugar Cake", "Tea", "Wind Chime Tea" },
                    new[] { "Acorn Milk", "Angel Fish", "Angelfin", "Bear-y Cute Burger", "Cake Pop", "Cookies", "Earth Crystal", "Earth Dragon Tea", "Fire Dragon Tea", "Flan", "Fruit Cupcake", "Glass of Pure Water", "Hand Stitched Soccer Ball", "Large Mana Potion", "Large Mana Tome", "Magical Water", "Mana Water", "Small Mana Potion", "Small Mana Tome", "Strawberry Milk", "Tea Leaves", "Water Dragon Tea" }),

                CreateBirthday("Jun", "Winter", 11,
                    new[] { "Berry Fruit Salad", "Blueberry Salad", "Chicken Noodle Soup", "Citrus Salad", "Cobb Salad", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Hydrangea Cupcake", "Lotus", "Painted Egg", "Pickled Veggie Salad", "Potato Salad", "Sesame Rice Ball", "Spring Roll", "Tea", "Tomato Salad" },
                    new[] { "Flower Flounder", "Fried Rice", "Lavender", "Mochi", "Painter's Easel", "Sand Dollar", "Spaghetti", "Sunflower", "Tea Leaves", "Tulip" }),

                CreateBirthday("Kai", "Winter", 17,
                    new[] { "Egg Tarts", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Golden Fishing Rod", "Grilled Crab", "Kelp Eel", "Large Fishing Net", "Lemonade", "Lobster Bisque", "Lobster Roll", "Lobster Sushi", "Mithril Ring", "Neapolitan Ice Cream", "Poke Bowl", "Ribbon Eel", "Seared Lobster", "Seaweed Ice Cream", "Tropical Punch", "Very Good Fishing Rod" },
                    new[] { "Adamant Ring", "Advanced Fish Bait", "Basic Fish Bait", "Basic Fishing Rod", "Blazeel", "Copper Ring", "Crab", "Eel", "Egg", "Electric Eel", "Glass of Pure Water", "Iron Ring", "Lobster", "Sand Dollar", "Seaweed", "Small Fishing Net" }),

                CreateBirthday("Anne", "Winter", 21,
                    new[] { "Cheesecake", "Diamond", "Fancy Grape Juice", "Fancy Hot Dog", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Persian Love Cake", "Shimmeroot", "Small Money Bag" },
                    new[] { "Amethyst", "Black Forest Cake", "Eclair", "Gold Bar", "Gold Ore", "Raspberry", "Red Velvet Cake", "Red Velvet Cupcake", "Ruby", "Sapphire", "Souffle", "Wheat" }),

                CreateBirthday("Kitty", "Winter", 23,
                    new[] { "California Roll", "Cod Sashimi", "Crab Roll", "Duorado Double Roll", "Eel Roll", "Electric Eel Sushi", "Fish Taco", "Fish Tempura", "Fish and Chips", "Forgotten Bouquet of Hyacinth", "Forgotten Bouquet of Orchids", "Forgotten Bouquet of Peonies", "Forgotten Bouquet of Roses", "Forgotten Bouquet of Tulips", "Ghosthead Tuna Sashimi", "Ghoul Fish Roll", "Jack'o'fin Sashimi", "Lobster Roll", "Lobster Sushi", "Pufferfish Sashimi", "Rainbow Roll", "Red Snapper Sashimi", "Redfinned Pincher Sushi Roll", "Salmon Roll", "Sandstone Sashimi", "Spiked Salmon Sashimi", "Spring Roll", "Swordfish Sashimi", "Tiger Roll", "Tuna Sashimi", "Viperfish Sashimi" },
                    new[] { "Amethyst", "Animal Food", "Apple Pie", "Cinnamon Apple Pie", "Diamond", "Gear Maiden Wig", "Mochi", "Mochi Covered Strawberry", "Ruby", "Sandstone Fish", "Sapphire", "Wool", "XBK One Chance_ Romance Record" }),
            };
        }

        private static NPCBirthday CreateBirthday(string name, string season, int day, string[] loved, string[] liked)
        {
            var birthday = new NPCBirthday(name, season, day);
            birthday.LovedGifts.AddRange(loved);
            birthday.LikedGifts.AddRange(liked);
            return birthday;
        }

        /// <summary>
        /// Get all birthdays
        /// </summary>
        public static IReadOnlyList<NPCBirthday> AllBirthdays => _allBirthdays;

        /// <summary>
        /// Get birthdays for a specific date
        /// </summary>
        public static List<NPCBirthday> GetBirthdaysForDate(string season, int day)
        {
            return _allBirthdays
                .Where(b => b.IsBirthday(season, day))
                .ToList();
        }

        /// <summary>
        /// Get a specific NPC's birthday
        /// </summary>
        public static NPCBirthday GetBirthday(string npcName)
        {
            return _allBirthdays
                .FirstOrDefault(b => string.Equals(b.NPCName, npcName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Get random gift suggestions for an NPC (3 items)
        /// </summary>
        public static List<string> GetRandomGiftSuggestions(string npcName, int count = 3)
        {
            var birthday = GetBirthday(npcName);
            var suggestions = new List<string>();
            var random = new Random();

            if (birthday != null)
            {
                // Prefer loved gifts, then liked, then universal
                var available = new List<string>();

                if (birthday.LovedGifts.Count > 0)
                    available.AddRange(birthday.LovedGifts);
                else if (birthday.LikedGifts.Count > 0)
                    available.AddRange(birthday.LikedGifts);
                else
                    available.AddRange(UniversalLoved);

                // Shuffle and take requested count
                var shuffled = available.OrderBy(x => random.Next()).ToList();
                suggestions.AddRange(shuffled.Take(count));
            }

            // Fill with universal if needed
            while (suggestions.Count < count)
            {
                var universal = UniversalLoved.Concat(UniversalLiked).ToList();
                var remaining = universal.Where(u => !suggestions.Contains(u)).OrderBy(x => random.Next()).FirstOrDefault();
                if (remaining != null)
                    suggestions.Add(remaining);
                else
                    break;
            }

            return suggestions;
        }
    }
}
