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

class PocketDimension
{
  // I am flat out making the assumuption that I am going to create so many many
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
    Dungeon belly = new(dungeonId, "a Pocket Dimension", $"You've been swallowed by {monster.Name.IndefArticle()}!", true);
    belly.AddMap(map);


    return (new Loc(dungeonId, 0, 1, 1), belly);
  }
}