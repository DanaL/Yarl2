// Delve - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

class PocketDimension
{
  // I am flat out making the assumuption that I am not going to create so many
  // temporary levels that I underflow into the 'real' dungeon IDs.
  //
  // Real dungeonIds start at 0 (the wilderness) and work upwards. I think when
  // I have all the content I want in Delve, it'll be a half dozen dungeons at 
  // most? Maybe a dozen.
  //
  // Anyhow, for temporary levels (like a toad's belly), I'm going to start at 
  // MaxInt and work downward until you find an unused ID.
  static int TempDungeonId(GameState gs)
  {
    int dungeonId = int.MaxValue;

    while (gs.Campaign.Dungeons.ContainsKey(dungeonId))
      --dungeonId;

    return dungeonId;
  }

  public static (Loc, Dungeon) MonsterBelly(Actor monster, GameState gs)
  {
    Glyph mg = monster.Glyph;
    Map map = new(3, 3);
    map.SetTile(0, 0, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(0, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(0, 2, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(1, 0, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(1, 1, TileFactory.Get(TileType.DungeonFloor));
    map.SetTile(1, 2, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(2, 0, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(2, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));
    map.SetTile(2, 2, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, mg.Illuminate), monster.ID));

    int dungeonId = TempDungeonId(gs);
    Dungeon belly = new(dungeonId, "a monster's belly", $"You've been swallowed by {monster.Name.IndefArticle()}!", true);
    belly.AddMap(map);

    return (new Loc(dungeonId, 0, 1, 1), belly);
  }

  public static (Loc, Dungeon) WhaleBelly(Actor monster, GameState gs)
  {
    Glyph mg = monster.Glyph;

    Map map = new(19, 5);

    map.SetTile(0, 0, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 2, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 3, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 4, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 5, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 6, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 7, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 8, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 9, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 10, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 11, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(0, 12, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 13, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 14, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 15, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 16, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 17, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(0, 18, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));

    map.SetTile(1, 0, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 1, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 2, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 3, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 4, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 5, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 6, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 7, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 8, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 9, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 10, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 11, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(1, 12, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 13, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 14, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 15, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 16, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 17, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(1, 18, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));

    map.SetTile(2, 0, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(2, 1, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 2, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 3, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 4, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 5, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 6, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 7, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 8, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 9, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 10, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 11, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 12, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 13, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 14, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 15, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 16, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 17, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(2, 18, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));

    map.SetTile(3, 0, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 1, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 2, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 3, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 4, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 5, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 6, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 7, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 8, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 9, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 10, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 11, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(3, 12, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 13, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 14, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 15, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 16, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 17, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(3, 18, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));

    map.SetTile(4, 0, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 2, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 3, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 4, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 5, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 6, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 7, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 8, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 9, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 10, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 11, TileFactory.Get(TileType.BellyFloor));
    map.SetTile(4, 12, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 13, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 14, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 15, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 16, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 17, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));
    map.SetTile(4, 18, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BG, false), monster.ID));

    int dungeonId = TempDungeonId(gs);
    Dungeon belly = new(dungeonId, "a monster's belly", $"You've been swallowed by {monster.Name.IndefArticle()}!", true);
    belly.AddMap(map);
    gs.Campaign.AddDungeon(belly, belly.ID);

    List<Loc> floors = [.. map.SqsOfType(TileType.BellyFloor).Select(sq => new Loc(dungeonId, 0, sq.Item1, sq.Item2))];
    for (int i = 0; i < gs.Rng.Next(1, 4); i++)
    {
      Item item = gs.Rng.Next(3) switch
      {
        0 => Treasure.GoodMagicItem(gs.Rng, gs.ObjDb),
        1 => Treasure.ItemByQuality(TreasureQuality.Good, gs.ObjDb, gs.Rng),
        _ => Treasure.ItemByQuality(TreasureQuality.Uncommon, gs.ObjDb, gs.Rng)
      };
      Loc loc = floors[gs.Rng.Next(floors.Count)];
      gs.ItemDropped(item, loc);
    }

    return (new Loc(dungeonId, 0, 2, 1), belly);
  }
}