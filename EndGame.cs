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

using System.Security.Cryptography;
using Yarl2;

class IslandInfo
{
  public int ID { get; set; }
  public bool FullyConnection { get; set; } = false;
  public HashSet<Loc> IslandSqs { get; set; } = [];
}

class EndGameDungeonBuilder(int dungeonId, Loc entrance) : DungeonBuilder
{
  const int HEIGHT = 40;
  const int WIDTH = 70;
  int DungeonId { get; set; } = dungeonId;
  Loc Entrance { get; set; } = entrance;
  public readonly HashSet<Loc> IslandLocs = [];
  public readonly HashSet<Loc> GateHouseLocs = [];
  Loc FirstFloorDoor { get; set; }

  static readonly List<string> IslandTemplates =
  [
    "~~~~~~~~~~~.....~~~......~~~.....~~~~.....~~~~~.....~~~~~.....~~~~...~~~~~~~~~~~~",
    "~~~~~~~~~~~~.....~~~.....~~~...~~~~~~..~~~~~~~..~~~~~~~...~~~~~~~....~~~~~~~~~~~~",
    "~~~~~~~~~~~.....~~~......~~~......~~~......~~~.....~~~~~...~~~~~~..~~~~~~~~~~~~~~",
    "~~~~~~~~~~~.....~~~......~~~...~..~~~......~~~..~..~~~~~...~~~~~~..~~~~~~~~~~~~~~",
    "~~~~~~~~~~~~..~~~~~~....~~~~......~~~......~~~.....~~~~~...~~~~~~~.~~~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~.......~~.......~~.......~~~.....~~~~~...~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~.......~~.......~~.......~~~.....~~~~~...~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~...~...~~...~...~~...~...~~~.....~~~~~...~~~~~~~~~~~~",
    "~~~~~~~~~~~~.....~~~......~~~~~..~~~~~~..~~~~~~~...~~~~......~~~.....~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~.......~~.......~~......~~~~....~~~~~~..~~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~.......~~.......~~......~~~~....~~~~~~..~~~~~~~~~~~~~",
    "~~~~~~~~~~~~...~~~~~.....~~~.......~~.......~~......~~~~....~~~~~~..~~~~~~~~~~~~~"
  ];

  string RotateTemplate(string template, Rng rng)
  {
    char[] rotated = new char[81];
    int rotation = rng.Next(4);
    for (int r = 0; r < 9; r++)
    {
      for (int c = 0; c < 9; c++)
      {
        var (rotatedR, rotatedC) = rotation switch
        {
          0 => (r, c),
          1 => (c, 8 - r),
          2 => (8 - r, 8 - c),
          _ => (8 - c, r)
        };
        int idx = rotatedR * 9 + rotatedC;
        rotated[idx] = template[r * 9 + c];
        
      }
    }

    return string.Join("", rotated);
  }
  void DrawIsland(Map map, IslandInfo info, int row, int col, Rng rng)
  {
    string template = RotateTemplate(IslandTemplates[rng.Next(IslandTemplates.Count)], rng);

    int rowOffset = rng.Next(5) switch
    {
      0 => -1,
      1 => 1,
      _ => 0
    };
    int colOffSet = rng.Next(5) switch
    {
      0 => -1,
      1 => 1,
      _ => 0
    };

    for (int r = 0; r < 9; r++)
    {
      for (int c = 0; c < 9; c++)
      {
        if (template[r * 9 + c] == '~')
          continue;
        Loc loc = new(DungeonId, 4, row * 10 + r + rowOffset, col * 10 + c + colOffSet);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.IslandSqs.Add(loc);
      }
    }
  }

  void DrawDestIslandTL(Map map, IslandInfo info, int row, int col, Rng rng)
  {
    int startRow = rng.Next(1, 3);
    int startCol = rng.Next(5, 8);
    for (int r = startRow; r < 11; r++)
    {
      for (int c = startCol; c < 11; c++)
      {
        Loc loc = new(DungeonId, 4, row * 10 + r, col * 10 + c);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.IslandSqs.Add(loc);
      }

      if (startCol > 3 && rng.Next(5) == 0)
        --startCol;
      else if (startCol < 7 && rng.Next(10) == 0)
        ++startCol;
    }  
  }

  void DrawDestIslandTR(Map map, IslandInfo info, int row, int col, Rng rng)
  {
    int startRow = rng.Next(1, 3);
    int endCol = rng.Next(4, 6);
    for (int r = startRow; r < 11; r++)
    {
      for (int c = 0; c < endCol; c++)
      {
        Loc loc = new(DungeonId, 4, row * 10 + r, col * 10 + c);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.IslandSqs.Add(loc);
      }

      if (endCol < 7 && rng.Next(5) == 0)
        ++endCol;
      else if (endCol > 3 && rng.Next(10) == 0)
        --endCol;
    }  
  }

  void DrawDestIslandBL(Map map, IslandInfo info, int row, int col, Rng rng)
  {
    int endRow = rng.Next(5, 9);
    int startCol = rng.Next(5, 8);
    for (int r = 0; r < endRow; r++)
    {
      for (int c = startCol; c < 11; c++)
      {
        Loc loc = new(DungeonId, 4, row * 10 + r, col * 10 + c);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.IslandSqs.Add(loc);
      }

      if (startCol > 3 && rng.Next(5) == 0)
        --startCol;
      else if (startCol < 7 && rng.Next(10) == 0)
        ++startCol;
    }  
  }

  void DrawDestIslandBR(Map map, IslandInfo info, int row, int col, Rng rng)
  {
    int endRow = rng.Next(5, 9);
    int endCol = rng.Next(4, 6);
    for (int r = 0; r < endRow; r++)
    {
      for (int c = 0; c < endCol; c++)
      {
        Loc loc = new(DungeonId, 4, row * 10 + r, col * 10 + c);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.IslandSqs.Add(loc);
      }

      if (endCol < 7 && rng.Next(5) == 0)
        ++endCol;
      else if (endCol > 3 && rng.Next(10) == 0)
        --endCol;
    }  
  }

  Map BottomLevel(GameState gs)
  {
    Map map = new(80, 60, TileType.Lake);

    // First pick 4 blocks for the island with the final prison
    int row = gs.Rng.Next(0, 5);
    int col = gs.Rng.Next(0, 7);
    int destIslandTL = row * 8 + col;
    int destIslandTR = row * 8 + col + 1;
    int destIslandBL = (row + 1) * 8 + col;
    int destIslandBR = (row + 1) * 8 + col + 1;
    HashSet<int> destIsland = [ destIslandTL, destIslandTR, destIslandBL, destIslandBR ];

    IslandInfo info = new() { ID = destIslandTL };
    DrawDestIslandTL(map, info, destIslandTL / 8, destIslandTL % 8, gs.Rng);
    info = new() { ID = destIslandTR };
    DrawDestIslandTR(map, info, destIslandTR / 8, destIslandTR % 8, gs.Rng);
    info = new() { ID = destIslandBL };
    DrawDestIslandBL(map, info, destIslandBL / 8, destIslandBL % 8, gs.Rng);
    info = new() { ID = destIslandBR };
    DrawDestIslandBR(map, info, destIslandBR / 8, destIslandBR % 8, gs.Rng);

    List<int> ids = [.. Enumerable.Range(0, 48).Where(id => !destIsland.Contains(id))];
    ids.Shuffle(gs.Rng);
    foreach (int id in ids.Take(gs.Rng.Next(28, 33)))
    {
      info = new() { ID = id };
      DrawIsland(map, info, id / 8, id % 8, gs.Rng);
    }

    List<HashSet<int>> connections = [ destIsland ];

    map.Dump();

    return map;  
  }

  Map FirstLevelMap(GameState gs)
  {
    Map map = new(82, 42, TileType.PermWall);

    bool[,] open = CACave.GetCave(40, 80, gs.Rng);
    for (int r = 0; r < 40; r++)
    {
      for (int c = 0; c < 80; c++)
      {
        TileType tt = open[r, c] ? TileType.DungeonFloor : TileType.DungeonWall;
        map.SetTile(r + 1 , c + 1, TileFactory.Get(tt));
      }
    }

    ConfigurablePassable passable = new();
    passable.Passable.Add(TileType.DungeonFloor);
    CACave.JoinCaves(map, gs.Rng, gs.ObjDb, passable, TileType.DungeonFloor, TileType.DungeonWall, TileType.DungeonWall);

    // Draw the lava lake
    for (int r = 16; r <= 26; r++)
    {
      int start_c = 31 - gs.Rng.Next(1, 4);
      int end_c = 51 + gs.Rng.Next(1, 4);
      for (int c = start_c; c <= end_c; c++)
      {
        if ((r > 16 && r < 26) || gs.Rng.Next(3) < 2)
         map.SetTile(r, c, TileFactory.Get(TileType.Lava));
      }
    }

    // Draw island in the centre
    for (int r = 19; r <= 23; r++)
    {
      int start_c = r > 19 && r < 23 ? 37 - gs.Rng.Next(1, 4) : 38;
      int end_c = r > 19 && r < 23 ? 45 + gs.Rng.Next(1, 4) : 44;
      for (int c = start_c; c <= end_c; c++)
      {
        if ((r > 16 && r < 26) || gs.Rng.Next(3) < 2) 
        {
          map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
          IslandLocs.Add(new(DungeonId, 0, r, c));
        }
      }
    }

    for (int c = 35; c <= 47; c++)
    {
      map.SetTile(11, c, TileFactory.Get(TileType.DungeonFloor));
    }
    map.SetTile(12, 36, TileFactory.Get(TileType.DungeonFloor));
    map.SetTile(12, 46, TileFactory.Get(TileType.DungeonFloor));

    // Draw the gatehouse
    for (int c = 37; c <= 45; c++) 
    {
      map.SetTile(12, c, TileFactory.Get(TileType.StoneWall));
      GateHouseLocs.Add(new (DungeonId, 0, 11, c));
      // Ensure these tiles are lava for aesthetics
      map.SetTile(16, c, TileFactory.Get(TileType.Lava));
    }
    for (int r = 13; r <= 15; r++)
    {      
      map.SetTile(r, 37, TileFactory.Get(TileType.StoneWall));
      map.SetTile(r, 45, TileFactory.Get(TileType.StoneWall));
      
      map.SetTile(r, 35, TileFactory.Get(TileType.DungeonFloor));
      map.SetTile(r, 36, TileFactory.Get(TileType.DungeonFloor));
      map.SetTile(r, 46, TileFactory.Get(TileType.DungeonFloor));
      map.SetTile(r, 47, TileFactory.Get(TileType.DungeonFloor));
      
      GateHouseLocs.Add(new (DungeonId, 0, r, 36));
      GateHouseLocs.Add(new (DungeonId, 0, r, 46));

      // Erase any dungeon walls that are inside the gate house
      for (int c = 38; c <= 44; c++) 
      {
        map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
        GateHouseLocs.Add(new (DungeonId, 0, r, c));
      }
    }
    int doorCol = gs.Rng.Next(39, 44);
    map.SetTile(12, doorCol, TileFactory.Get(TileType.LockedDoor));
    FirstFloorDoor = new(DungeonId, 0, 12, doorCol);

    int leverRow = gs.Rng.Next(13, 16);
    int leverCol = gs.Rng.Next(38, 45);
    Loc bridgeStart = new(DungeonId, 0, 16, gs.Rng.Next(38, 45));
    map.SetTile(leverRow, leverCol, new Lever(TileType.BridgeLever, false, bridgeStart));
    
    return map;
  }

  Loc FindArrivalLoc(Map map, Rng rng)
  {
    DijkstraMap dmap = new(map, [], map.Height, map.Width, true);
    dmap.Generate(DijkstraMap.CostWithDoors, (FirstFloorDoor.Row, FirstFloorDoor.Col), 100);

    List<Loc> floors = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        Loc candidate = new (DungeonId, 0, r, c);
        // We don't need to avoid the island because they are as yet unreachable
        // from the gatehouse door
        if (GateHouseLocs.Contains(candidate))
          continue;
        if (dmap.Sqrs[r, c] < int.MaxValue && map.TileAt(r, c).Type == TileType.DungeonFloor)
        {
          floors.Add(candidate);
        }
      }
    }
    
    return floors[rng.Next(floors.Count)];
  }

  public Dungeon Generate(GameState gs)
  {
    Dungeon dungeon = new(DungeonId, "the Gaol", "Sulphur. Heat. Mortals were not meant for this place.", true);
    DungeonMap mapper = new(gs.Rng);
    List<Map> levels = [];

    //dungeon.MonsterDecks = DeckBuilder.ReadDeck(MainOccupant, rng);
    Map firstLevel = FirstLevelMap(gs);
    Map bottom = BottomLevel(gs);

    dungeon.ExitLoc = FindArrivalLoc(firstLevel, gs.Rng);
    Upstairs arrival = new("") { Destination = Entrance };
    firstLevel.SetTile(dungeon.ExitLoc.Row, dungeon.ExitLoc.Col, arrival);
    levels.Add(firstLevel);

    for (int levelNum = 1; levelNum <= 4; levelNum++)
    {
      levels.Add(mapper.DrawLevel(WIDTH, HEIGHT));
      AddSecretDoors(levels[levelNum], gs.Rng);
    }

    // Pick spot on the island for the stairs down from the first level
    List<Loc> options = [];
    foreach (Loc loc in IslandLocs)
    {
      if (levels[0].TileAt(loc.Row, loc.Col).Type == TileType.DungeonFloor)
        options.Add(loc);
    }
    Loc firstFloorDownLoc = options[gs.Rng.Next(options.Count)];
    Downstairs downstairs = new("") { Destination = firstFloorDownLoc with { Level = 1}};
    Upstairs upstairs = new("") { Destination = firstFloorDownLoc};
    levels[0].SetTile(firstFloorDownLoc.Row, firstFloorDownLoc.Col, downstairs);
    levels[1].SetTile(firstFloorDownLoc.Row, firstFloorDownLoc.Col, upstairs);
    //CreateStairwayStacked(DungeonId, [.. levels], 1, (firstFloorDownLoc.Row, firstFloorDownLoc.Col), true, gs.Rng);

    foreach (Map map in levels)
      dungeon.AddMap(map);

    return dungeon;
  }
}

class EndGame
{
  public static int CostNearby(Tile tile)
  {
    switch (tile.Type) 
    {
      case TileType.Mountain:
      case TileType.SnowPeak:
        return 2;
      default:
        if (tile.Passable())
          return 1;
        break;
    }

    return int.MaxValue;
  }

  public static void Setup(GameState gs)
  {
    if (gs.FactDb.FactCheck("Dungeon Entrance") is not LocationFact entranceFact)
      throw new Exception("Missing Dungeon Entrance fact. Cannot build end game.");

    Loc initialDungeon = entranceFact.Loc;

    DijkstraMap dmap = new(gs.Wilderness, [], gs.Wilderness.Height, gs.Wilderness.Width, true);
    dmap.Generate(CostNearby, (initialDungeon.Row, initialDungeon.Col), 10);

    List<Loc> mountains = [];
    List<Loc> passable = [];
    for (int r = initialDungeon.Row - 3; r <= initialDungeon.Row + 3; r++)
    {
      for (int c = initialDungeon.Col -3; c <= initialDungeon.Col + 3; c++)
      {
        Loc loc = initialDungeon with { Row = r, Col = c };
        Tile tile = gs.TileAt(loc);
        if (tile.Type == TileType.Mountain || tile.Type == TileType.SnowPeak)
          mountains.Add(loc);
        else if (tile.Passable())
          passable.Add(loc);
      }
    }

    Loc finalDungeonLoc = Loc.Nowhere;
    if (mountains.Count > 0)
     finalDungeonLoc = mountains[gs.Rng.Next(mountains.Count)];
    else if (passable.Count > 0)
      finalDungeonLoc = passable[gs.Rng.Next(passable.Count)];
    
    if (finalDungeonLoc == Loc.Nowhere)
      throw new Exception("Could not place final dungeon location!");

    var path = dmap.ShortestPath(finalDungeonLoc.Row, finalDungeonLoc.Col);
    foreach (var loc in path)
    {
      if (!gs.Wilderness.TileAt(loc).Passable())
        gs.Wilderness.SetTile(loc, TileFactory.Get(TileType.Dirt));
    }

    EndGameDungeonBuilder db = new (gs.Campaign.Dungeons.Count, finalDungeonLoc);
    Dungeon dungeon = db.Generate(gs);
    gs.Campaign.AddDungeon(dungeon);

    Portal portal = new("A smouldering arch covered in profane sigils.", TileType.ProfanePortal)
    {
      Destination = dungeon.ExitLoc
    };

    gs.Wilderness.SetTile(finalDungeonLoc.Row, finalDungeonLoc.Col, portal);
  }
}