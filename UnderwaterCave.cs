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

class UnderwaterCaveDungeon(int dungeonId, int height, int width) : DungeonBuilder
{
  int Height { get; set; } = height + 2;
  int Width { get; set; } = width + 2;
  int DungeonId { get; set; } = dungeonId;

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, Rng rng)
  {
    Dungeon cave = new(DungeonId, "A mooist, clammy cave. From the distance comes the sound of dripping water.", true);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    cave.MonsterDecks.Add(deck);

    Map topLevel = new(Width, Height, TileType.PermWall);    
    bool[,] floors = CACave.GetCave(Height - 2, Width - 2, rng);
    for (int r = 0; r < Height - 2; r++)
    {
      for (int c = 0; c < Width - 2; c++)
      {
        TileType tile = TileType.DungeonWall;
        if (floors[r, c])
        {
          tile = TileType.DungeonFloor;
        }
        topLevel.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }
    topLevel.Dump();

    // Make an island somewhere in the cave and turn the rest of the squares
    // to water tiles
    bool[,] island = new bool[5, 5];

    cave.AddMap(topLevel);
    return (cave, Loc.Nowhere);
  }
}
