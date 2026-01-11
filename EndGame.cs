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

using Yarl2;

class IslandInfo
{
  public int ID { get; set; }
  public HashSet<Dir> Connections = []; 
  public HashSet<Loc> Sqs { get; set; } = [];
}

class EndGameDungeonBuilder(int dungeonId, Loc entrance) : DungeonBuilder
{
  const int BOTTOM_LVL = 4;
  const int HEIGHT = 40;
  const int WIDTH = 70;
  int DungeonId { get; set; } = dungeonId;
  Loc Entrance { get; set; } = entrance;
  public readonly HashSet<Loc> IslandLocs = [];
  public readonly HashSet<Loc> GateHouseLocs = [];
  Loc FirstFloorDoor { get; set; }
  readonly Dictionary<Loc, int> IslandIndices = [];
  readonly HashSet<Loc> CentralLocs = [];
  Loc GaolCentre { get; set; } // Center as in Arioch's location
  Loc BottomLevelArrivalStairs { get; set; }

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

  static string RotateTemplate(string template, Rng rng)
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
        Loc loc = new(DungeonId, BOTTOM_LVL, row * 10 + r + rowOffset, col * 10 + c + colOffSet);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.Sqs.Add(loc);
        IslandIndices[loc] = info.ID;
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
        Loc loc = new(DungeonId, BOTTOM_LVL, row * 10 + r, col * 10 + c);
        CentralLocs.Add(loc);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.Sqs.Add(loc);
        IslandIndices[loc] = info.ID;
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
        Loc loc = new(DungeonId, BOTTOM_LVL, row * 10 + r, col * 10 + c);
        CentralLocs.Add(loc);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.Sqs.Add(loc);
        IslandIndices[loc] = info.ID;
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
        Loc loc = new(DungeonId, BOTTOM_LVL, row * 10 + r, col * 10 + c);
        CentralLocs.Add(loc);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.Sqs.Add(loc);
        IslandIndices[loc] = info.ID;
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
        Loc loc = new(DungeonId, BOTTOM_LVL, row * 10 + r, col * 10 + c);
        CentralLocs.Add(loc);
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        info.Sqs.Add(loc);
        IslandIndices[loc] = info.ID;
      }

      if (endCol < 7 && rng.Next(5) == 0)
        ++endCol;
      else if (endCol > 3 && rng.Next(10) == 0)
        --endCol;
    }  
  }

  int TryToConnect(int id, Map map, Dictionary<int, IslandInfo> islands, List<HashSet<int>> connections, HashSet<int> homeSet, Rng rng)
  {
    List<Dir> dirs = [ Dir.North, Dir.South, Dir.East, Dir.West ];
    dirs.Shuffle(rng);
    IslandInfo island = islands[id];
    int row, col;
    int minRow = island.Sqs.Min(s => s.Row);
    int maxRow = island.Sqs.Max(s => s.Row);
    if (maxRow - minRow > 4)
      row = rng.Next(minRow + rng.Next(1, 3), maxRow - rng.Next(1, 3) + 1);
    else
      row = rng.Next(minRow + 1, maxRow);
    int minCol = island.Sqs.Min(s => s.Col);
    int maxCol = island.Sqs.Max(s => s.Col);
    if (maxCol - minCol > 4)
      col = rng.Next(minCol + rng.Next(1, 3), maxCol - rng.Next(1, 3) + 1);
    else
      col = rng.Next(minCol + 1, maxCol);

    int otherIslandId = -1;

    foreach (Dir dir in dirs)
    {
      if (island.Connections.Contains(dir))
        continue;
      
      var (deltaR, deltaC) = dir switch
      {
        Dir.North => (-1, 0),
        Dir.South => (1, 0),
        Dir.East => (0, 1),
        _ => (0, -1)
      };

      List<Loc> bridges = [];
      Loc loc = new(DungeonId, BOTTOM_LVL, row, col);
      bool failedToJoin = false;
      while (true)
      {
        loc = loc with { Row = loc.Row + deltaR, Col = loc.Col + deltaC };
        if (!map.InBounds(loc.Row, loc.Col))
        {
          failedToJoin = true;
          break;
        }
        if (island.Sqs.Contains(loc)) 
        {
          continue;
        }
        Tile tile = map.TileAt(loc.Row, loc.Col);
        if (tile.Type == TileType.Lava) 
        {
          bridges.Add(loc);
          continue;
        }
        else if (tile.Type == TileType.WoodBridge)
        {
          // No t-joint bridges (for now)
          failedToJoin = true;
          break;
        }
        if (IslandIndices.TryGetValue(loc, out int otherId))
        {
          failedToJoin = homeSet.Contains(otherId);
          if (!failedToJoin)
            otherIslandId = otherId;
          break;
        }
        else
        {
          throw new Exception("I think this should never happen??");
        }
      }

      if (!failedToJoin)
      {
        foreach (Loc bridge in bridges)
        {
          map.SetTile(bridge.Row, bridge.Col, TileFactory.Get(TileType.WoodBridge));
        }
        break;
      }

      island.Connections.Add(dir);
    }

    return otherIslandId;
  }

  void JoinIslands(Map map, Dictionary<int, IslandInfo> islands, List<HashSet<int>> connections, Rng rng)
  {
    connections.Shuffle(rng);

    while (connections.Count > 1)
    {
      HashSet<int> set = connections[0];
      foreach (int id in set)
      {
        if (islands[id].Connections.Count == 4)
          continue;
        int otherIslandId = TryToConnect(id, map, islands, connections, set, rng);
        if (otherIslandId != -1)
        {
          int idx = -1;
          for (int j = 0; j < connections.Count; j++)
          {
            if (connections[j].Contains(otherIslandId))
            {
              idx = j;
              break;
            }
          }
          HashSet<int> unioned = [.. set.Union(connections[idx])];
          connections.RemoveAt(idx);
          connections.RemoveAt(0);
          connections.Add(unioned);
          break;
        }
      }
    }
  }

  static void BottomLevelTweaks(Map map)
  {
    // The sometimes the generator will overwrite lava in 'donut' islands
    // so turn any bridges that don't adjoin any other bridge to 
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r, c).Type == TileType.WoodBridge)
        {
          int adjBridges = 0;
          foreach (var adj in Util.Adj8Sqs(r, c))
          {
            if (map.InBounds(adj) && map.TileAt(adj).Type == TileType.WoodBridge)
              ++adjBridges;
          }
          if (adjBridges == 0)
            map.SetTile(r, c, TileFactory.Get(TileType.Lava));
        }
      }
    }
  }

  Map BottomLevel(GameState gs)
  {
    Map map = new(80, 60, TileType.Lava);
    Dictionary<int, IslandInfo> islands = [];

    // First pick 4 blocks for the island with the final prison
    int row = gs.Rng.Next(0, 5);
    int col = gs.Rng.Next(0, 7);
    int destIslandTL = row * 8 + col;
    int destIslandTR = row * 8 + col + 1;
    int destIslandBL = (row + 1) * 8 + col;
    int destIslandBR = (row + 1) * 8 + col + 1;
    HashSet<int> destIsland = [ destIslandTL, destIslandTR, destIslandBL, destIslandBR ];

    islands[destIslandTL] = new() { ID = destIslandTL };
    DrawDestIslandTL(map, islands[destIslandTL], destIslandTL / 8, destIslandTL % 8, gs.Rng);
    islands[destIslandTR] = new() { ID = destIslandTR };
    DrawDestIslandTR(map, islands[destIslandTR], destIslandTR / 8, destIslandTR % 8, gs.Rng);
    islands[destIslandBL] = new() { ID = destIslandBL };
    DrawDestIslandBL(map, islands[destIslandBL], destIslandBL / 8, destIslandBL % 8, gs.Rng);
    islands[destIslandBR] = new() { ID = destIslandBR };
    DrawDestIslandBR(map, islands[destIslandBR], destIslandBR / 8, destIslandBR % 8, gs.Rng);

    List<HashSet<int>> connections = [ destIsland ];

    List<int> ids = [.. Enumerable.Range(0, 48).Where(id => !destIsland.Contains(id))];
    ids.Shuffle(gs.Rng);
    foreach (int id in ids.Take(gs.Rng.Next(28, 33)))
    {
      IslandInfo info = new() { ID = id };
      DrawIsland(map, info, id / 8, id % 8, gs.Rng);
      connections.Add([id]);
      islands[id] = info;
    }
    
    JoinIslands(map, islands, connections, gs.Rng);
    BottomLevelTweaks(map);

    Map finalMap = new(82, 62, TileType.PermWall) { Features = MapFeatures.UndiggableFloor | MapFeatures.Unmappable };
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        finalMap.SetTile(r + 1, c + 1, map.TileAt(r, c));
      }
    }

    // Place the stairs far from the 'center' ideally
    int minRow = int.MaxValue, maxRow = 0, minCol = int.MaxValue, maxCol = 0;
    foreach (Loc loc in CentralLocs)
    {
      minRow = int.Min(loc.Row, minRow);
      maxRow = int.Max(loc.Row, maxRow);
      minCol = int.Min(loc.Col, minCol);
      maxCol = int.Max(loc.Col, maxCol);
    }
    GaolCentre = new(DungeonId, BOTTOM_LVL, (maxRow + minRow) / 2, (maxCol + minCol) / 2);
    DijkstraMap dij = new(finalMap, [], finalMap.Height, finalMap.Width, false);
    dij.Generate(DijkstraMap.Cost, (GaolCentre.Row, GaolCentre.Col), int.MaxValue);
    List<(Loc, int)> stairCandidates = [];
    for (int r = 0; r < finalMap.Height; r++)
    {
      for (int c = 0; c < finalMap.Width; c++)
      {
        if (dij.Sqrs[r, c] < int.MaxValue)
        {
          stairCandidates.Add((new(DungeonId, BOTTOM_LVL, r, c), dij.Sqrs[r, c]));
        }
      }
    }
    stairCandidates = [.. stairCandidates.OrderByDescending(c => c.Item2).Take(20)];
    BottomLevelArrivalStairs = stairCandidates[gs.Rng.Next(stairCandidates.Count)].Item1;

    finalMap.SetTile(GaolCentre.Row, GaolCentre.Col, TileFactory.Get(TileType.Arioch));
    finalMap.SetTile(GaolCentre.Row - 1, GaolCentre.Col - 1, new Shackle(new ('\\', Colours.DARK_GREY, Colours.DARK_GREY, Colours.BLACK, false)));
    finalMap.SetTile(GaolCentre.Row - 1, GaolCentre.Col + 1, new Shackle(new ('/', Colours.DARK_GREY, Colours.DARK_GREY, Colours.BLACK, false)));
    finalMap.SetTile(GaolCentre.Row + 1, GaolCentre.Col - 1, new Shackle(new ('/', Colours.DARK_GREY, Colours.DARK_GREY, Colours.BLACK, false)));
    finalMap.SetTile(GaolCentre.Row + 1, GaolCentre.Col + 1, new Shackle(new ('\\', Colours.DARK_GREY, Colours.DARK_GREY, Colours.BLACK, false)));

    return finalMap;  
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

  void SetFinalStairs(List<Map> levels, GameState gs)
  {
    Map penultimate = levels[^2];
    Map bottom = levels[^1];
    List<Loc> floors = [.. penultimate.SqsOfType(TileType.DungeonFloor).Select(sq => new Loc(DungeonId, levels.Count - 2, sq.Item1, sq.Item2))];
    Loc downstairsLoc = floors[gs.Rng.Next(floors.Count)];

    Downstairs down = new("") { Destination = BottomLevelArrivalStairs };
    penultimate.SetTile(downstairsLoc.Row, downstairsLoc.Col, down);
    Upstairs up = new("") { Destination = downstairsLoc };
    bottom.SetTile(BottomLevelArrivalStairs.Row, BottomLevelArrivalStairs.Col, up);
  }

  static void AddTreasure(Map map, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    var floors = map.SqsOfType(TileType.DungeonFloor);
    floors.Shuffle(rng);

    for (int j = 0; j < rng.Next(6, 9) && j < floors.Count; j++)
    {
      var quality = rng.Next(10) switch
      {
        0 => TreasureQuality.Common,
        1 => TreasureQuality.Uncommon,
        2 => TreasureQuality.Uncommon,
        3 => TreasureQuality.Uncommon,
        _ => TreasureQuality.Good
      };
      Item item = Treasure.ItemByQuality(quality, objDb, rng);
      Loc loc = new(dungeonId, level, floors[j].Item1, floors[j].Item2);
      objDb.SetToLoc(loc, item);
    }
  }

  static void AddRooms(Map map, int dungeonId, int level, Map[] maps, GameObjectDB objDb, Rng rng)
  {
    List<List<(int, int)>> rooms = map.FindRooms(9);
    List<int> roomIds = [.. Enumerable.Range(0, rooms.Count)];
    roomIds.Shuffle(rng);
    List<int> potentialVaults = []; 

    foreach (int id in roomIds)
    {
      RoomCorners corners = Rooms.IsRectangle(map, rooms[id]);
      if (corners.LowerRow - corners.UpperRow >= 5 && corners.RightCol - corners.LeftCol >= 5)
      {
        var innerSqs = Rooms.RoomInRoom(map, corners, rng);
        rooms[id] = [.. innerSqs];
        break;
      }
    }

    bool mimic = false;
    foreach (int roomId in roomIds)
    {
      // In case the floors squares were modified before we get to considering the room
      List<(int, int)> roomSqs = [.. rooms[roomId].Where(sq => map.TileAt(sq).Type == TileType.DungeonFloor)];

      if (rng.Next(10) == 0)
      {
        switch (rng.Next(4))
        {
          case 0:
            Rooms.ChasmTrapRoom(maps, rng, dungeonId, level, roomSqs, objDb);
            break;
          case 1:
            Rooms.TriggerChasmRoom(maps, rng, dungeonId, level, roomSqs, objDb);
            break;
          case 2:
            Rooms.BasicChasmRoom(maps, rng, dungeonId, level, roomSqs, objDb);
            break;
          default:
            Rooms.ChasmIslandRoom(maps, rng, dungeonId, level, roomSqs, objDb);
            break;
        }
      }

      if (!mimic && rng.Next(10) == 0)
      {        
        Rooms.AddMimicGroup(roomSqs, dungeonId, level, objDb, rng);
        mimic = true;
      }
    }
  }

  public Dungeon Generate(GameState gs)
  {
    Dungeon dungeon = new(DungeonId, "the Gaol", "Sulphur. Heat. Mortals were not meant for this place.", true);
    DungeonMap mapper = new(gs.Rng);
    Map[] levels = new Map[5];

    dungeon.MonsterDecks = DeckBuilder.ReadDeck("gaol", gs.Rng);
    Map firstLevel = FirstLevelMap(gs);
    Map bottom = BottomLevel(gs);
    
    dungeon.ExitLoc = FindArrivalLoc(firstLevel, gs.Rng);
    Upstairs arrival = new("") { Destination = Entrance };
    firstLevel.SetTile(dungeon.ExitLoc.Row, dungeon.ExitLoc.Col, arrival);
    levels[0] = firstLevel;

    for (int levelNum = 1; levelNum <= 3; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(WIDTH, HEIGHT);
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
    List<(int, int)> secondLevelFloors = [.. levels[1].SqsOfType(TileType.DungeonFloor)];
    var (slUpRow, slUpCol) = secondLevelFloors[gs.Rng.Next(secondLevelFloors.Count)];
    Loc secondFloorUpLoc = new(DungeonId, 1, slUpRow, slUpCol);

    Downstairs downstairs = new("") { Destination = secondFloorUpLoc };
    Upstairs upstairs = new("") { Destination = firstFloorDownLoc};
    levels[0].SetTile(firstFloorDownLoc.Row, firstFloorDownLoc.Col, downstairs);        
    levels[1].SetTile(secondFloorUpLoc.Row, secondFloorUpLoc.Col, upstairs);
    
    levels[4] = bottom;
    SetFinalStairs([.. levels], gs);
    levels[^2].Features |= MapFeatures.UndiggableFloor;

    for (int lvl = 0; lvl <= BOTTOM_LVL; lvl++)
    {
      AddTreasure(levels[lvl], dungeonId, lvl, gs.ObjDb, gs.Rng);

      if (lvl > 0 && lvl < BOTTOM_LVL)
        AddRooms(levels[lvl], dungeonId, lvl, levels, gs.ObjDb, gs.Rng);

      dungeon.AddMap(levels[lvl]);
    }
  
    TidyOrphanedDoors(levels);

    CreateStairwayStacked(DungeonId, [.. levels[..^1]], 1, (secondFloorUpLoc.Row, secondFloorUpLoc.Col), true, gs.Rng);

    PopulateDungeon(dungeon, gs.Rng, gs.ObjDb);
    List<Loc> lvlOneIslands = [.. IslandLocs];
    lvlOneIslands.Shuffle(gs.Rng);
    int count = 0;
    foreach (Loc islandLoc in  lvlOneIslands)
    {
      if (levels[0].TileAt(islandLoc.Row, islandLoc.Col).Type == TileType.DungeonFloor && !gs.ObjDb.Occupied(islandLoc))
      {
        Actor spearGuy = MonsterFactory.Get("duergar spearfisher", gs.ObjDb, gs.Rng);
        gs.ObjDb.AddNewActor(spearGuy, islandLoc);
        ++count;
      }

      if (count >= 2)
        break;
    }

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

    gs.FactDb.Add(new SimpleFact() { Name="GaolId", Value = dungeon.ID.ToString() });
  }
}