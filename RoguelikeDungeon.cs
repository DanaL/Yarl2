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

record RLRoom(int Row, int Col, int Height, int Width);

class RLLevelMaker
{
  const int HEIGHT = 30;
  const int WIDTH = 70;
  const int CELL_WIDTH = 17;
  const int CELL_HEIGHT = 9;

  public Map MakeLevel(Rng rng)
  {
    Map map = new(WIDTH, HEIGHT, TileType.DungeonWall);

    for (int c = 0; c < WIDTH; c++)
    {
      map.SetTile(0, c, TileFactory.Get(TileType.PermWall));
      map.SetTile(HEIGHT - 1, c, TileFactory.Get(TileType.PermWall));
    }

    for (int r = 1; r < HEIGHT - 1; r++)
    {
      map.SetTile(r, 0, TileFactory.Get(TileType.PermWall));
      map.SetTile(r, WIDTH - 1, TileFactory.Get(TileType.PermWall));
    }

    // Debug temp
    for (int c = 1; c < WIDTH - 1; c++)
    {
      map.SetTile(10, c, TileFactory.Get(TileType.DeepWater));
      map.SetTile(20, c, TileFactory.Get(TileType.DeepWater));      
    }

    for (int r = 1; r < HEIGHT - 1; r++)
    {
      map.SetTile(r, 18, TileFactory.Get(TileType.DeepWater));
      map.SetTile(r, 36, TileFactory.Get(TileType.DeepWater));
      map.SetTile(r, 52, TileFactory.Get(TileType.DeepWater));
    }
    // Debug temp

    int numOfRooms = rng.Next(8, 11);
    Dictionary<int, RLRoom> rooms = [];

    HashSet<int> usedCells = [];
    for (int i = 0; i < numOfRooms; i++)
    {
      PlaceRoom(map, usedCells, rng);      
    }
    map.Dump();

    return map;
  }

  static readonly int[] roomWidths = [3, 4, 5, 6, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 14, 15];
  static RLRoom PlaceRoom(Map map, HashSet<int> usedCells, Rng rng)
  {
    List<int> cells = [.. Enumerable.Range(0, 12).Where(c => !usedCells.Contains(c))];
    cells.Shuffle(rng);

    int h = rng.Next(3, 7);
    int w = roomWidths[rng.Next(roomWidths.Length)];

    foreach (int cell in cells)
    {
      int rr = rng.Next(0, CELL_HEIGHT - h);
      int rc = rng.Next(0, CELL_WIDTH - w);
      
      (int or, int oc) = OffSet(cell);
      int row = or + rr + 1, col = oc + rc + 1; // + 1 because outer walls are permanent

      if (CanPlace(row, col, h, w))
      {
        for (int r = row; r < row + h; r++)
        {
          for (int c = col; c < col + w; c++)
          {
            map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
          }
        }

        usedCells.Add(cell);

        return new(row, col, h, w); ;
      }
    }

    return new(0, 0, 0, 0);

    bool CanPlace(int row, int col, int h, int w)
    {
      for (int r = row; r < row + h; r++)
      {
        for (int c = col; c < col + w; c++)
        {
          if (map.TileAt(r, c).Type != TileType.DungeonWall)
            return false;
        }
      }

      return true;
    }

    // kinda dumb but clear...
    (int, int) OffSet(int cell)  => cell switch
    {
      0 => (0, 0),
      1 => (0, 17),
      2 => (0, 34),
      3 => (0, 51),
      4 => (10, 0),
      5 => (10, 17),
      6 => (10, 34),
      7 => (10, 51),
      8 => (21, 0),
      9 => (21, 17),
      10 => (21, 34),
      _ => (21, 51)
    };
  }
}

internal class RoguelikeDungeonBuilder(int dungeonId) : DungeonBuilder
{
  int DungeonId { get; set; } = dungeonId;

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, Rng rng)
  {
    Dungeon dungeon = new(DungeonId, "", true);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["creeping coins"]);
    dungeon.MonsterDecks.Add(deck);

    return (dungeon, Loc.Nowhere);
  }
}

