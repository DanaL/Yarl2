// Yarl2 - A roguelike computer RPG
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

class SorceressDungeonBuilder(int dungeonId, int height, int width) : DungeonBuilder
{
  int Height { get; set; } = height;
  int Width { get; set; } = width;
  int DungeonId { get; set; } = dungeonId;

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, Rng rng)
  {
    Dungeon towerDungeon = new(DungeonId, "Ancient halls that smell of dust and magic.", false);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    towerDungeon.MonsterDecks.Add(deck);

    Tower towerBuilder = new(Height, Width, 5);
    Map[] floors = [..towerBuilder.BuildLevels(5, rng)];

    SetStairs(DungeonId, floors, Height, Width, 5, (entranceRow, entranceCol), false, rng);

    foreach (Map floor in floors)
    {
      towerDungeon.AddMap(floor);
    }

    Loc entrance = Loc.Nowhere;
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        if (floors[0].TileAt(r, c).Type == TileType.Downstairs)
        {
          entrance = new(DungeonId, 0, r, c);
          break;
        }
      }
    }
    return (towerDungeon, entrance);
  }
}