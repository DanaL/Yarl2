
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

    public static void Populate(Map map, Town town, GameObjectDB objDb, Random rng)
    {
        var ng = new NameGenerator(rng, "names.txt");

        var cleric = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent,
            Appearance = VillagerAppearance(rng)
        };
        var sqs = town.Shrine.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor || 
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        cleric.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        cleric.VillagerType = VillagerType.Priest;
        cleric.SetBehaviour(new PriestBehaviour());

        Console.WriteLine(cleric.FullName);
        Console.WriteLine(cleric.Appearance.IndefArticle().Capitalize());
        objDb.Add(cleric);
        objDb.SetToLoc(cleric.Loc, cleric);
    }
}