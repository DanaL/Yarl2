// Delve - A roguelike computer RPG
// Written in 2026 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along
// with this software. If not,
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

static class MonsterSpawner
{
  public static Actor? LevelAppropriate(Dungeon dungeon, GameObjectDB objDb, Rng rng, int dungeonId, int level)
  {
    int monsterLevel = level;
    if (monsterLevel > 0)
    {
      double roll = rng.NextDouble();
      if (roll > 0.95)
        monsterLevel += 2;
      else if (roll > 0.8)
        monsterLevel += 1;
      if (monsterLevel > dungeon.LevelMaps.Count)
        monsterLevel = dungeon.LevelMaps.Count;
    }

    monsterLevel = int.Min(monsterLevel, dungeon.MonsterDecks.Count - 1);
    if (monsterLevel == -1 || monsterLevel >= dungeon.MonsterDecks.Count)
      return null;

    MonsterDeck deck = dungeon.MonsterDecks[monsterLevel];
    if (deck.Indexes.Count == 0)
      deck.Reshuffle(rng);
    string m = deck.Monsters[deck.Indexes.Dequeue()];

    return MonsterFactory.Get(m, objDb, rng);
  }

  public static string Random(Campaign campaign, Rng rng, int dungeonId)
  {
    if (dungeonId == 0)
    {
      // I don't yet have a monster deck for the wilderness
      return rng.NextDouble() < 0.5 ? "wolf" : "dire bat";
    }

    Dungeon dungeon = campaign.Dungeons[dungeonId];
    MonsterDeck deck = dungeon.MonsterDecks[rng.Next(dungeon.MonsterDecks.Count)];

    return deck.Monsters[rng.Next(deck.Monsters.Count)];
  }

  public static void Spawn(Dungeon dungeon, GameObjectDB objDb, Rng rng, int dungeonId, int level, Map map, Dictionary<Loc, Glyph> playerFoV)
  {
    if (LevelAppropriate(dungeon, objDb, rng, dungeonId, level) is not Actor monster)
      return;

    List<Loc> openLoc = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        Loc loc = new(dungeonId, level, r, c);

        if (map.TileAt(r, c).Type != TileType.DungeonFloor)
          continue;
        if (objDb.Occupied(loc) || objDb.AreBlockersAtLoc(loc))
          continue;

        // This prevents a monster from being spawned on top of a campfire, lol
        var items = objDb.ItemsAt(loc);
        if (items.Count > 0 && items.Any(i => i.HasTrait<AffixedTrait>()))
          continue;

        openLoc.Add(loc);
      }
    }

    if (openLoc.Count == 0)
      return;

    // Prefer spawning the monster where the player can't see it
    List<Loc> outOfSight = [.. openLoc.Where(l => !playerFoV.ContainsKey(l))];
    if (outOfSight.Count > 0)
      openLoc = outOfSight;

    Loc spawnPoint = openLoc[rng.Next(openLoc.Count)];
    monster.Loc = spawnPoint;
    objDb.AddNewActor(monster, spawnPoint);
  }
}
