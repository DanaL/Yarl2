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
  public static (Loc, Dungeon) MonsterBelly(Actor monster)
  {
    Glyph mg = monster.Glyph;
    Map map = new(3, 3);
    map.SetTile(0, 0, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(0, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(0, 2, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(1, 0, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(1, 1, TileFactory.Get(TileType.DungeonFloor));
    map.SetTile(1, 2, new MonsterWall(new Glyph('|', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(2, 0, new MonsterWall(new Glyph('\\', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(2, 1, new MonsterWall(new Glyph('-', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));
    map.SetTile(2, 2, new MonsterWall(new Glyph('/', mg.Lit, mg.Unlit, mg.BGLit, mg.BGUnlit), monster.ID));

    Dungeon belly = new(int.MaxValue, $"You've been swallowed by {monster.Name.IndefArticle()}!");
    belly.AddMap(map);

    return (new Loc(int.MaxValue, 0, 1, 1), belly);
  }
}