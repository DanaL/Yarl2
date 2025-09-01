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

class UnderwaterCave
{
  static void SetCandleOfBinding(int dungeonId, List<(int, int)> floorSqs, GameObjectDB objDb, Rng rng)
  {
    Item candle = new()
    {
      Name = "Candle of Binding",
      Type = ItemType.Tool,
      Glyph = new Glyph('(', Colours.WHITE, Colours.GREY, Colours.BLACK, false)
    };
    candle.Traits.Add(new DescriptionTrait("An ornate candle carved with symbols of the Moon Daughter."));
    candle.Traits.Add(new ArtifactTrait());

    (int r, int c) = floorSqs[rng.Next(floorSqs.Count)];
    Loc candleLoc = new(dungeonId, 0, r, c);
    objDb.Add(candle);
    objDb.SetToLoc(candleLoc, candle);
  }

  public static void SetupUnderwaterCave(Campaign campaign, int entranceRow, int entranceCol, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    UnderwaterCaveDungeon caveBuilder = new(campaign.Dungeons.Count, 30, 70);
    Dungeon cave = caveBuilder.Generate(entranceRow, entranceCol, objDb, rng);
    campaign.AddDungeon(cave);

    Loc caveEntrance = new(cave.ID, 0, caveBuilder.ExitLoc.Item1, caveBuilder.ExitLoc.Item2);
    factDb.Add(new LocationFact() { Desc = "UnderwaterCaveEntrance", Loc = caveEntrance });

    Dungeon temple = new(campaign.Dungeons.Count, "", true);
    campaign.AddDungeon(temple);
    (Map templeMap, List<(int, int)> floors) = RLLevelMaker.MakeLevel(rng);
    temple.AddMap(templeMap);
    (int, int) sq = floors[rng.Next(floors.Count)];
    Loc templeEntrance = new(temple.ID, 0, sq.Item1, sq.Item2);

    Upstairs upstairs = new("") { Destination = templeEntrance };

    Map bottomCave = cave.LevelMaps[cave.LevelMaps.Count - 1];
    floors = bottomCave.SqsOfType(TileType.DungeonFloor);
    sq = floors[rng.Next(floors.Count)];
    Loc caveExit = new(cave.ID, cave.LevelMaps.Count - 1, sq.Item1, sq.Item2);
    floors.Remove(sq);
    Downstairs downstairs = new("") { Destination = caveExit };

    templeMap.SetTile(templeEntrance.Row, templeEntrance.Col, downstairs);
    bottomCave.SetTile(caveExit.Row, caveExit.Col, upstairs);

    SetCandleOfBinding(temple.ID, floors, objDb, rng);
  }
}

class UnderwaterCaveDungeon(int dungeonId, int height, int width) : DungeonBuilder
{
  int Height { get; set; } = height + 2;
  int Width { get; set; } = width + 2;
  int DungeonId { get; set; } = dungeonId;

  Map MidLevel(GameObjectDB objDb, Rng rng)
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

    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.Underwater);
    CACave.JoinCaves(map, rng, objDb, passable, TileType.Underwater, TileType.DungeonWall, TileType.DungeonWall);

    map.Dump();

    return map;
  }

  Map BottomLevel(GameObjectDB objDb, Rng rng)
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

    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.DungeonFloor);
    CACave.JoinCaves(map, rng, objDb, passable, TileType.DungeonFloor, TileType.DungeonWall, TileType.DungeonWall);

    List<(int, int)> floorSqs = map.SqsOfType(TileType.DungeonFloor);
    floorSqs.Shuffle(rng);
    for (int j = 0; j < floorSqs.Count / 10; j++)
    {
      map.SetTile(floorSqs[0], TileFactory.Get(TileType.Kelp));
      floorSqs.RemoveAt(0);
    }

    map.Dump();

    return map;
  }

  Map TopLevel(int entranceRow, int entranceCol, GameObjectDB objDb, Rng rng)
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

    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.Lake);
    CACave.JoinCaves(topLevel, rng, objDb, passable, TileType.Lake, TileType.DungeonWall, TileType.DungeonWall);

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

    topLevel.Dump();

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

  public Dungeon Generate(int entranceRow, int entranceCol, GameObjectDB objDb, Rng rng)
  {
    Dungeon cave = new(DungeonId, "A moist, clammy cave. From the distance comes the sound of dripping water.", true);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    cave.MonsterDecks.Add(deck);

    cave.AddMap(TopLevel(entranceRow, entranceCol, objDb, rng));
    cave.AddMap(MidLevel(objDb, rng));
    cave.AddMap(BottomLevel(objDb, rng));

    return cave;


  }
}
