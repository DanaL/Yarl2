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

  Map MidLevel(Rng rng)
  {
    Map map = new(Width, Height, TileType.PermWall) { Submerged = true };
    bool[,] floors = CACave.GetCave(Height - 2, Width - 2, rng);
    for (int r = 0; r < Height - 2; r++)
    {
      for (int c = 0; c < Width - 2; c++)
      {
        TileType tile = floors[r, c] ? TileType.Underwater : TileType.DungeonWall;
        map.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    return map;
  }

  Map BottomLevel(Rng rng)
  {
    Map map = new(Width, Height, TileType.PermWall) { Submerged = true };
    bool[,] floors = CACave.GetCave(Height - 2, Width - 2, rng);
    for (int r = 0; r < Height - 2; r++)
    {
      for (int c = 0; c < Width - 2; c++)
      {
        TileType tile = floors[r, c] ? TileType.DungeonFloor : TileType.DungeonWall;
        map.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    List<(int, int)> floorSqs = map.SqsOfType(TileType.DungeonFloor);
    floorSqs.Shuffle(rng);
    for (int j = 0; j < floorSqs.Count / 10; j++)
    {
      map.SetTile(floorSqs[0], TileFactory.Get(TileType.Kelp));
      floorSqs.RemoveAt(0);
    }

    return map;
  }

  Map TopLevel(int entranceRow, int entranceCol, Rng rng)
  {
    Map topLevel = new(Width, Height, TileType.PermWall);
    bool[,] floors = CACave.GetCave(Height - 2, Width - 2, rng);
    for (int r = 0; r < Height - 2; r++)
    {
      for (int c = 0; c < Width - 2; c++)
      {
        TileType tile = floors[r, c] ? TileType.Lake : TileType.DungeonWall;
        topLevel.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    // Make an island somewhere in the cave and turn the rest of the squares
    // to water tiles
    bool[,] island =
    {
      { false, false, false, false, false },
      { false, true,  true,  true,  false },
      { false, true,  true,  true,  false },
      { false, true,  true,  true,  false },
      { false, false, false, false, false }
    };
    for (int c = 0; c < 5; c++)
    {
      if (rng.NextDouble() < 0.2)
        island[0, c] = true;
      if (rng.NextDouble() < 0.2)
        island[4, c] = true;
    }
    for (int r = 1; r < 4; r++)
    {
      if (rng.NextDouble() < 0.2)
        island[r, 0] = true;
      if (rng.NextDouble() < 0.2)
        island[r, 4] = true;
    }

    List<Loc> floorLocs = [];
    foreach ((int Row, int Col) sq in topLevel.SqsOfType(TileType.Lake))
    {
      if (IslandFits(sq.Row, sq.Col, island))
      {
        for (int r = -2; r < 3; r++)
        {
          for (int c = -2; c < 3; c++)
          {
            if (island[r + 2, c + 2])
            {
              topLevel.SetTile(sq.Row + r, sq.Col + c, TileFactory.Get(TileType.DungeonFloor));
              floorLocs.Add(new(DungeonId, 0, sq.Row + r, sq.Col + c));
            }
          }
        }
        break;
      }
    }

    Loc exit = floorLocs[rng.Next(floorLocs.Count)];
    ExitLoc = (exit.Row, exit.Col);

    Upstairs stairs = new("") { Destination = new(0, 0, entranceRow, entranceCol) };
    topLevel.SetTile(exit.Row, exit.Col, stairs);

    return topLevel;

    bool IslandFits(int cr, int cc, bool[,] island)
    {
      for (int r = -2; r < 3; r++)
      {
        for (int c = -2; c < 3; c++)
        {
          if (!topLevel.InBounds(cr + r, cc + c))
            return false;
          if (island[r + 2, c + 2] && topLevel.TileAt(cr + r, cc + c).Type != TileType.Lake)
            return false;
        }
      }

      return true;
    }
  }

  public Dungeon Generate(int entranceRow, int entranceCol, Rng rng)
  {
    Dungeon cave = new(DungeonId, "A moist, clammy cave. From the distance comes the sound of dripping water.", true);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    cave.MonsterDecks.Add(deck);
       
    cave.AddMap(TopLevel(entranceRow, entranceCol, rng));
    cave.AddMap(MidLevel(rng));
    cave.AddMap(BottomLevel(rng));

    return cave;

    
  }
}
