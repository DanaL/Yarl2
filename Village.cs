
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

namespace Yarl2;

// Code for populating the town with NPCs and some decorations

class Village
{
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
            Town = town
        };
        var sqs = town.Shrine.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor ||
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        cleric.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        cleric.SetBehaviour(new PriestBehaviour());

        return cleric;
    }

    static Villager GenerateSmith(Map map, Town town, NameGenerator ng, GameObjectDB objDb, Random rng)
    {
        var smith = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent,
            Appearance = VillagerAppearance(rng),
            Town = town,
            Markup = 1.5 + rng.NextDouble() / 2
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

    public static void Populate(Map map, Town town, GameObjectDB objDb, Random rng)
    {
        var ng = new NameGenerator(rng, "names.txt");

        var cleric = GeneratePriest(map, town, ng, rng);
        objDb.Add(cleric);
        objDb.SetToLoc(cleric.Loc, cleric);

        var smith = GenerateSmith(map, town, ng, objDb, rng);
        objDb.Add(smith);
        objDb.SetToLoc(smith.Loc, smith);
    }
}