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

class UnderwaterCave
{
  static void SetCandleOfBinding(Map map, int dungeonId, List<(int, int)> floorSqs, GameObjectDB objDb, Rng rng)
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
    cave.ExitLoc = new(0, 0, entranceRow, entranceCol);

    campaign.AddDungeon(cave);

    Loc caveEntrance = new(cave.ID, 0, caveBuilder.ExitLoc.Item1, caveBuilder.ExitLoc.Item2);
    factDb.Add(new LocationFact() { Desc = "UnderwaterCaveEntrance", Loc = caveEntrance });

    Dungeon temple = new(campaign.Dungeons.Count, "a Dusty Temple", "", true)
    {
      ExitLoc = new(0, 0, entranceRow, entranceCol)
    };
    campaign.AddDungeon(temple);
    (Map templeMap, List<(int, int)> templeFloors) = RLLevelMaker.MakeLevel(rng);
    
    temple.AddMap(templeMap);
    (int, int) sq = templeFloors[rng.Next(templeFloors.Count)];
    Loc templeEntrance = new(temple.ID, 0, sq.Item1, sq.Item2);

    Upstairs upstairs = new("") { Destination = templeEntrance };

    Map bottomCave = cave.LevelMaps[cave.LevelMaps.Count - 1];
    List<(int, int)> caveFloors = bottomCave.SqsOfType(TileType.DungeonFloor);
    sq = caveFloors[rng.Next(caveFloors.Count)];
    Loc caveExit = new(cave.ID, cave.LevelMaps.Count - 1, sq.Item1, sq.Item2);
    Downstairs downstairs = new("") { Destination = caveExit };

    templeMap.SetTile(templeEntrance.Row, templeEntrance.Col, downstairs);
    bottomCave.SetTile(caveExit.Row, caveExit.Col, upstairs);

    SetCandleOfBinding(templeMap, temple.ID, templeFloors, objDb, rng);
  }
}

class UnderwaterCaveDungeon(int dungeonId, int height, int width) : DungeonBuilder
{
  int Height { get; set; } = height + 2;
  int Width { get; set; } = width + 2;
  int DungeonId { get; set; } = dungeonId;
  HashSet<Loc> ShelfLocs { get; set; } = [];

  protected override bool IsValidMonsterPlacementTile(Tile tile) => tile.Type == TileType.DungeonFloor || tile.IsWater();

  Map MidLevel(GameObjectDB objDb, Rng rng)
  {
    Map map = new(Width, Height, TileType.PermWall) { Features = MapFeatures.Submerged };
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

    return map;
  }

  Map BottomLevel(GameObjectDB objDb, Rng rng)
  {
    Map map = new(Width, Height, TileType.PermWall) { Features = MapFeatures.Submerged };
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

    return map;
  }

  void AddShelf(Map map, HashSet<(int, int)> floorSqs, Rng rng)
  {
    (int r, int c, int dr, int dc) = rng.Next(4) switch
    {
      0 => (rng.Next(3, Height - 3), 0, 0, 1), // Start at west wall
      1 => (rng.Next(3, Height - 3), Width - 1, 0, -1), // east wall
      2 => (0, rng.Next(3, Width - 3), 1, 0), // north wall
      _ => (Height - 1, rng.Next(3, Width - 3), -1, 0) // south wall
    };

    while (map.InBounds(r, c) && map.TileAt(r, c).Type != TileType.Lake)
    {
      r += dr;
      c += dc;
    }

    if (map.InBounds(r, c))
    {
      Stack<(int, int)> stack = [];
      stack.Push((r, c));

      int count = 0;
      int shelfSize = rng.Next(5, 10);
      while (stack.Count > 0 && count < shelfSize)
      {
        ++count;
        var sq = stack.Pop(); 
        map.SetTile(sq, TileFactory.Get(TileType.DungeonFloor));
        ShelfLocs.Add(new(DungeonId, 0, sq.Item1, sq.Item2));

        floorSqs.Add(sq);

        List<(int, int)> adjTiles = [.. Util.Adj8Sqs(r, c)];
        adjTiles.Shuffle(rng);
        foreach (var adj in adjTiles)
        {
          if (map.TileAt(adj).Type == TileType.Lake)
          {
            map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
            ShelfLocs.Add(new(DungeonId, 0, r, c));
            stack.Push(adj);
          }
        } 
      }      
    }
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
    List<Loc> lakes = [];
    foreach ((int Row, int Col) sq in topLevel.SqsOfType(TileType.Lake))
    {
      lakes.Add(new(DungeonId, 0, sq.Row, sq.Col));
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

    HashSet<(int, int)> floorSqs = [];
    for (int j = 0; j < rng.Next(3, 7); j++) 
    {
      AddShelf(topLevel, floorSqs, rng);
    }

    if (rng.Next(4) == 0)
    {
      Item seeweed = ItemFactory.Get(ItemNames.SEEWEED, objDb);
      Loc loc = lakes[rng.Next(lakes.Count)];
      objDb.SetToLoc(loc, seeweed);
    }

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

  static void AddWhale(Map map, int dungeonID, int level, GameObjectDB objDb, Rng rng)
  {
    int tries = 0;
    do
    {
      var sq = map.RandomTile(t => t.Passable(), rng);
      Loc loc = new(dungeonID, level, sq.Item1, sq.Item2);
      if (!objDb.Occupied(loc))
      {
        Actor whale = MonsterFactory.Get("umbral whale", objDb, rng);
        objDb.AddNewActor(whale, loc);
        return;
      }
      
      ++tries;
    }
    while (tries < 100);
  }

  void SeedTreasure(Dungeon cave, GameObjectDB objDb, Rng rng)
  {
    for (int level = 0; level < cave.LevelMaps.Count; level++)
    {
      Map map = cave.LevelMaps[level];

      List<Loc> floors = level switch
      {
        0 => floors = [.. ShelfLocs],
        1 => [.. map.SqsOfType(TileType.Underwater).Select(sq => new Loc(cave.ID, level, sq.Item1, sq.Item2))],
        _ => [.. map.SqsOfTypes([TileType.DungeonFloor, TileType.Kelp]).Select(sq => new Loc(cave.ID, level, sq.Item1, sq.Item2))]
      };
      floors.Shuffle(rng);

      int numOfItems = rng.Next(2, 4);
      for (int j = 0; j < numOfItems; j++)
      {
        Item item = Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng);
        Loc loc = floors[j];
        objDb.SetToLoc(loc, item);
      }
    }
  }

  public Dungeon Generate(int entranceRow, int entranceCol, GameObjectDB objDb, Rng rng)
  {    
    Dungeon cave = new(DungeonId, "a Flooded Cavern", "A moist, clammy cave. From the distance comes the sound of dripping water.", true)
    {
      PopulationLow = 3,
      PopulationHigh = 5,
      MonsterDecks = DeckBuilder.ReadDeck("flooded_cave", rng)
    };

    cave.AddMap(TopLevel(entranceRow, entranceCol, objDb, rng));
    cave.AddMap(MidLevel(objDb, rng));
    cave.AddMap(BottomLevel(objDb, rng));

    PopulateDungeon(cave, rng, objDb);
    SeedTreasure(cave, objDb, rng);

    if (rng.Next(3) == 0)
    {
      AddWhale(cave.LevelMaps[2], DungeonId, 2, objDb, rng);
    }

    return cave;
  }
}
