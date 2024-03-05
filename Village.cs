
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

namespace Yarl2;

// Code for populating the town with NPCs and some decorations

class Village
{
    public static void Populate(Map map, Town town, GameObjectDB objDb, Random rng)
    {
        var ng = new NameGenerator(rng, "names.txt");

        var cleric = new Villager()
        {
            Name = ng.GenerateName(rng.Next(5, 9)),
            Status = ActorStatus.Indifferent
        };
        var sqs = town.Shrine.Where(sq => map.TileAt(sq).Type == TileType.StoneFloor || 
                                          map.TileAt(sq).Type == TileType.WoodFloor).ToList();
        var sq = sqs[rng.Next(sqs.Count)];
        cleric.Loc = new Loc(0, 0, sq.Item1, sq.Item2);
        objDb.Add(cleric);
        objDb.SetToLoc(cleric.Loc, cleric);
    }
}