
// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;
using SDL2;

namespace Yarl2;

// Code for populating the town with NPCs and some decorations

class Village
{
    static Loc RandomOutdoorLoc(Map map, Town town, Random rng)
    {
        List<(int, int)> sqs = [];
        for (int r = town.Row; r < town.Row + town.Height; r++)
        {
            for (int c = town.Col; c < town.Col + town.Width; c++)
            {
                switch (map.TileAt(r, c).Type)
                {
                    case TileType.Grass:
                    case TileType.Tree:
                    case TileType.Sand:
                    case TileType.Dirt:
                        sqs.Add((r, c));
                        break;
                }
            }
        }

        var sq = sqs[rng.Next(sqs.Count)];

        return new Loc(0, 0, sq.Item1, sq.Item2);
    }

    static string VillagerAppearance(Random rng)
    {
        string[] species = [ "human", "elf", "half-elf", "gnome", "dwarf", "orc", "half-orc"];
        string[] eyes = [ "bright", "deep", "dark", "sad", "distant", "piercing", "clear", "clouded"];
        string[] hair = [ "bald", "long", "short", "frizzy", "stylish", "unkempt", "messy", "perfurmed",
                            "unwashed", "tousled", "curly", "wavy" ];

        int r = rng.Next(3);
        var appearance = new StringBuilder();
        if (r == 0)
            appearance.Append("tall ");
        else if (r == 1)
            appearance.Append("short ");
        appearance.Append(species[rng.Next(species.Length)]);

        string villagerHair = hair[rng.Next(hair.Length)];
        if (villagerHair == "bald")
        {
            appearance.Append(", bald, with ");
        }
        else 
        {
            appearance.Append(" with ");
            appearance.Append(villagerHair);
            appearance.Append(" hair, and ");
        }

        appearance.Append(eyes[rng.Next(eyes.Length)]);
        appearance.Append(" eyes");
        
        return appearance.ToString();
    }

    static Villager GeneratePriest(Map map, Town town, NameGenerator ng, Random rng)
    {        
        var cleric = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent,
            Appearance = VillagerAppearance(rng),
            Town = town,
            Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        var sqs = town.Shrine.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor ||
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        cleric.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        cleric.SetBehaviour(new PriestBehaviour());

        return cleric;
    }

    static Villager GenerateGrocer(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
    {
        var grocer = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent,
            Appearance = VillagerAppearance(rng),
            Town = town,
            Markup = 1.25 + rng.NextDouble() / 2,
            Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        var sqs = town.Market.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor ||
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        grocer.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        grocer.SetBehaviour(new GrocerBehaviour());
        
        grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
        grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
        grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
        grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
        for (int j = 0; j < rng.Next(4); j++)
            grocer.Inventory.Add(ItemFactory.Get("torch", objDb), grocer.ID);
        for (int j = 0; j < rng.Next(1, 4); j ++)
            grocer.Inventory.Add(ItemFactory.Get("potion of healing", objDb), grocer.ID);

        return grocer;
    }

    static Villager GenerateSmith(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
    {
        var smith = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent,
            Appearance = VillagerAppearance(rng),
            Town = town,
            Markup = 1.5 + rng.NextDouble() / 2,
            Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE)
        };
        var sqs = town.Smithy.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor ||
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        smith.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        smith.SetBehaviour(new SmithBehaviour());

        smith.Inventory.Add(ItemFactory.Get("ringmail", objDb), smith.ID);
        smith.Inventory.Add(ItemFactory.Get("helmet", objDb), smith.ID);
        if (rng.NextDouble() < 0.25)
            smith.Inventory.Add(ItemFactory.Get("chainmail", objDb), smith.ID);
        smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
        smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
        smith.Inventory.Add(ItemFactory.Get("dagger", objDb), smith.ID);
        if (rng.NextDouble() < 0.33)
            smith.Inventory.Add(ItemFactory.Get("battle axe", objDb), smith.ID);
        if (rng.NextDouble() < 0.33)
            smith.Inventory.Add(ItemFactory.Get("mace", objDb), smith.ID);
        if (rng.NextDouble() < 0.33)
            smith.Inventory.Add(ItemFactory.Get("longsword", objDb), smith.ID);
        if (rng.NextDouble() < 0.33)
            smith.Inventory.Add(ItemFactory.Get("rapier", objDb), smith.ID);

        return smith;
    }

    static VillageAnimal GeneratePuppy(Map map, Town town, GameObjectDB objDb, Random rng)
    {                
        int roll = rng.Next(4);
        var (colourDesc, colour) = roll switch
        {
            0 => ("grey", Colours.GREY),
            1 => ("brown", Colours.LIGHT_BROWN),
            2 => ("black", Colours.DARK_GREY),
            _ => ("rusty", Colours.DULL_RED)
        };

        roll = rng.Next(5);
        string adj = roll switch
        {
            0 => "scruffy",
            1 => "furry",
            2 => "fluffy",
            3 => "playful",
            _ => "friendly"
        };

        roll = rng.Next(4);
        string dogType = roll switch
        {
            0 => "pup",
            1 => "dog",
            2 => "puppy",
            _ => "hound"
        };

        var pup = new VillageAnimal()
        {
            Name = $"{adj} {dogType}",
            Town = town,
            Status = ActorStatus.Indifferent,
            Appearance = $"{adj} {dogType} with {colourDesc} fur",
            Glyph = new Glyph('d', colour, colour),
            Loc = RandomOutdoorLoc(map, town, rng)
        };

        return pup;
    }

    public static void Populate(Map map, Town town, GameObjectDB objDb, Random rng)
    {
        var ng = new NameGenerator(rng, "names.txt");

        var cleric = GeneratePriest(map, town, ng, rng);
        objDb.Add(cleric);
        objDb.SetToLoc(cleric.Loc, cleric);

        var smith = GenerateSmith(map, town, ng, objDb, rng);
        objDb.Add(smith);
        objDb.SetToLoc(smith.Loc, smith);

        var grocer = GenerateGrocer(map, town, ng, objDb, rng);
        objDb.Add(grocer);
        objDb.SetToLoc(grocer.Loc, grocer);

        var pup = GeneratePuppy(map, town, objDb, rng);
        objDb.Add(pup);
        objDb.SetToLoc(pup.Loc, pup);
    }
}