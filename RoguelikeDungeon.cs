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

record RLRoom(int Row, int Col, int Height, int Width, int Cell);

class RLLevelMaker
{
  const int HEIGHT = 30;
  const int WIDTH = 70;
  const int CELL_WIDTH = 17;
  const int CELL_HEIGHT = 9;

  public Map MakeLevel(Rng rng)
  {
    // I'm initializing the map with sand and peppering some walls here and 
    // there throughout hoping that when it comes time to use pathfinding to
    // draw the halls, the alg will route around walls making for a bit more
    // twisty passages.
    Map map = new(WIDTH, HEIGHT, TileType.Sand);

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

    for (int i = 0; i < 125; i++)
    {
      int r = rng.Next(1, HEIGHT - 1);
      int c = rng.Next(1, WIDTH - 1);      
      map.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));      
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
      RLRoom room = PlaceRoom(map, usedCells, rng);
      rooms.Add(room.Cell, room);
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
      int rr = rng.Next(0, CELL_HEIGHT - h - 1);
      int rc = rng.Next(0, CELL_WIDTH - w - 1);
      
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

        return new(row, col, h, w, cell);
      }
    }

    return new(0, 0, 0, 0, -1);

    bool CanPlace(int row, int col, int h, int w)
    {
      for (int r = row; r < row + h; r++)
      {
        for (int c = col; c < col + w; c++)
        {
          if (map.TileAt(r, c).Type == TileType.DungeonFloor)
            return false;
        }
      }

      return true;
    }

    // kinda dumb but clear...
    (int, int) OffSet(int cell)  => cell switch
    {
      0 => (0, 0),
      1 => (0, 18),
      2 => (0, 36),
      3 => (0, 52),
      4 => (11, 0),
      5 => (11, 18),
      6 => (11, 36),
      7 => (11, 52),
      8 => (21, 0),
      9 => (21, 18),
      10 => (21, 36),
      _ => (21, 52)
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

