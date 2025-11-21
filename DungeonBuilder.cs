// Delve - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;

namespace Yarl2;

abstract class DungeonBuilder
{
  public (int, int) ExitLoc { get; set; }

  static bool GoodClosetSpot(Map map, int r, int c)
  {
    if (map.TileAt(r, c).Type != TileType.DungeonFloor)
      return false;

    return Util.Adj8Sqs(r, c)
               .Where(t => map.InBounds(t.Item1, t.Item2))
               .Count(t => map.TileAt(t).Type == TileType.DungeonFloor) == 5;
  }

  public static List<(int, int, int, int, int, int)> PotentialClosets(Map map)
  {
    var closets = new List<(int, int, int, int, int, int)>();

    // Check each tile in the map
    for (int r = 2; r < map.Height - 2; r++)
    {
      for (int c = 2; c < map.Width - 2; c++)
      {
        if (map.TileAt(r, c).Type != TileType.DungeonWall)
          continue;

        bool surroundedByWalls = true;
        foreach (var sq in Util.Adj8Sqs(r, c))
        {
          if (map.TileAt(sq).Type != TileType.DungeonWall)
          {
            surroundedByWalls = false;
            break;
          }
        }
        if (!surroundedByWalls)
          continue;

        if (GoodClosetSpot(map, r - 2, c))
          closets.Add((r, c, r - 2, c, r - 1, c));
        else if (GoodClosetSpot(map, r + 2, c))
          closets.Add((r, c, r + 2, c, r + 1, c));
        else if (GoodClosetSpot(map, r, c - 2))
          closets.Add((r, c, r, c - 2, r, c - 1));
        else if (GoodClosetSpot(map, r, c + 2))
          closets.Add((r, c, r, c + 2, r, c + 1));
      }
    }

    return closets;
  }

  protected static void DecorateRiver(Map map, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    // if there's a river, sometimes add seeweed nearby
    if (rng.Next(3) == 0)
    {
      HashSet<(int, int)> candidates = [];
      for (int r = 0; r < map.Height; r++)
      {
        for (int c = 0; c < map.Width; c++)
        {
          if (map.TileAt(r, c).Type == TileType.DeepWater)
          {
            foreach (var sq in map.TilesNearSq(TileType.DungeonFloor, r, c, 2))
              candidates.Add(sq);
          }
        }
      }

      List<(int, int)> sqs = [.. candidates];
      sqs.Shuffle(rng);
      int numOfWeeds = rng.Next(1, 4);
      foreach (var sq in sqs.Take(numOfWeeds))
      {
        Item weed = ItemFactory.Get(ItemNames.SEEWEED, objDb);
        objDb.SetToLoc(new(dungeonId, level, sq.Item1, sq.Item2), weed);
      }
    }
  }

  // If a river/chasm cuts the up stairs off from the down stairs, drop
  // a potion of levitation on the level so the player isn't trapped.
  protected static void RiverQoLCheck(Map map, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    List<(int, int)> upStairs = [];
    List<(int, int)> downStairs = [];

    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        if (map.TileAt(r, c).Type == TileType.Upstairs)
          upStairs.Add((r, c));
        if (map.TileAt(r, c).Type == TileType.Downstairs)
          downStairs.Add((r, c));
      }
    }

    foreach (var (ur, uc) in upStairs)
    {
      Loc start = new(0, 0, ur, uc);
      foreach (var (dr, dc) in downStairs)
      {
        Loc goal = new(0, 0, dr, dc);
        Stack<Loc> path = AStar.FindPath(objDb, map, start, goal, Costs);
        if (path.Count == 0)
        {
          AddRiverCrossing(map, ur, uc, dungeonId, level, objDb, rng);
          return;
        }
      }
    }

    static int Costs(Tile tile) => tile.Type switch
    { 
      TileType.DungeonFloor => 1,
      TileType.ClosedDoor => 1,
      TileType.LockedDoor => 1,
      TileType.Upstairs => 1,
      TileType.Downstairs => 1,
      TileType.WoodBridge => 1,
      TileType.SecretDoor => 1,
      _ => int.MaxValue
    };
  }

  // At the moment, I am just adding a potion of levitation on the stairs up side,
  // but I can imagine other solutions to the level being split by a river (adding
  // another set of stairs, etc)
  static void AddRiverCrossing(Map map, int r, int c, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    HashSet<(int, int)> contiguous = [];
    Queue<(int, int)> q = new();
    q.Enqueue((r, c));
    contiguous.Add((r, c));

    while (q.Count > 0)
    {
      var (row, col) = q.Dequeue();
      foreach (var sq in Util.Adj4Sqs(row, col))
      {
        if (contiguous.Contains(sq))
          continue;

        TileType type = map.TileAt(sq).Type;
        if (type == TileType.DungeonFloor || type == TileType.ClosedDoor)
        {
          contiguous.Add(sq);
          q.Enqueue(sq);
        }
      }
    }

    List<Loc> opts = [.. contiguous.Select(s => new Loc(dungeonId, level, s.Item1, s.Item2))];
    Loc loc = opts[rng.Next(opts.Count)];
    Item potion = ItemFactory.Get(ItemNames.POTION_OF_LEVITATION, objDb);
    objDb.SetToLoc(loc, potion);
  }

  // Tidy up useless doors. Sometimes chasm generate will result in orphaned doors like:
  //
  //  #....
  //  #..+.
  //  ###..
  protected static void TidyOrphanedDoors(int dungeonId, Map[] levels, GameObjectDB objDb, Rng rng)
  {
    for (int levelNum = 0; levelNum < levels.Length; levelNum++)
    {
      Map map = levels[levelNum];

      for (int r = 0; r < levels[levelNum].Height; r++)
      {
        for (int c = 0; c < levels[levelNum].Width; c++)
        {
          TileType ttype = map.TileAt(r, c).Type;
          if ((ttype == TileType.ClosedDoor || ttype == TileType.LockedDoor) && !GoodDoor(map, r, c))
          {
            map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
          }
        }
      }
    }

    static bool GoodDoor(Map map, int row, int col)
    {
      TileType n = map.TileAt(row - 1, col).Type;
      TileType s = map.TileAt(row + 1, col).Type;
      TileType e = map.TileAt(row, col + 1).Type;
      TileType w = map.TileAt(row, col - 1).Type;
      return DoorNeighbour(n) && DoorNeighbour(s) || DoorNeighbour(e) && DoorNeighbour(w);
    }

    static bool DoorNeighbour(TileType tt) => tt switch
    {
      TileType.DungeonWall => true,
      TileType.ClosedDoor => true,
      TileType.LockedDoor => true,
      _ => false
    };
  }

  protected static void AddGoodItemToLevel(Map map, int dungeonId, int level, Rng rng, GameObjectDB objDb)
  {
    List<Loc> opts = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r, c).Passable())
          opts.Add(new Loc(dungeonId, level, r, c));
      }
    }

    if (opts.Count > 0)
    {
      Loc loc = opts[rng.Next(opts.Count)];
      Item item = Treasure.GetTalisman(rng, objDb);
      objDb.SetToLoc(loc, item);
    }
  }

  protected static void PopulateDungeon(Dungeon dungeon, Rng rng, GameObjectDB objDb)
  {
    for (int lvl = 0; lvl < dungeon.LevelMaps.Count; lvl++)
    {
      for (int j = 0; j < rng.Next(dungeon.PopulationLow, dungeon.PopulationHigh + 1); j++)
      {
        int monsterLvl = lvl;
        if (lvl > 0 && rng.NextDouble() > 0.8)
        {
          monsterLvl = rng.Next(lvl);
        }

        MonsterDeck deck = dungeon.MonsterDecks[monsterLvl];
        (int, int) sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
        Loc loc = new(dungeon.ID, lvl, sq.Item1, sq.Item2);
        if (deck.Indexes.Count == 0)
          deck.Reshuffle(rng);
        string m = deck.Monsters[deck.Indexes.Dequeue()];

        // Some monsters are a bit special and take a bit of extra work
        //Actor monster = MonsterFactory.Get(m, objDb, rng);
        Actor monster = MonsterFactory.Get(m, objDb, rng);
        monster.Loc = loc;
        if (rng.NextDouble() < 0.8)
          monster.Traits.Add(new SleepingTrait());
        objDb.AddNewActor(monster, loc);
      }
    }
  }

  protected static void PlaceMistyPortal(Map map, Rng rng)
  {
    HashSet<(int, int)> candidates = [];
    foreach (var sq in map.SqsOfType(TileType.DungeonWall))
    {
      if (PortalCandidate(map, sq.Item1, sq.Item2))
        candidates.Add(sq);
    }

    if (candidates.Count == 0)
      return;

    List<(int, int)> v = [.. candidates];
    var mp = v[rng.Next(v.Count)];
    map.SetTile(mp, TileFactory.Get(TileType.MistyPortal));

    static bool PortalCandidate(Map map, int row, int col)
    {
      int adjWalls = 0;
      int adjFloors = 0;
      foreach (var sq in Util.Adj8Sqs(row, col))
      {
        if (map.TileAt(sq).Type == TileType.DungeonFloor)
          ++adjFloors;
        if (map.TileAt(sq).Type == TileType.DungeonWall)
          ++adjWalls;
      }

      return adjFloors == 3 && adjWalls == 5;
    }
  }

  // Turn some doors into secret doors and add perhaps add soom secret  
  // door in hallways
  protected static void AddSecretDoors(Map map, Rng rng)
  {
    List<(int, int)> candidates = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        TileType tt = map.TileAt(r, c).Type;
        if ((tt == TileType.ClosedDoor || tt == TileType.LockedDoor) && rng.Next(10) == 0)
        {
          map.SetTile(r, c, TileFactory.Get(TileType.SecretDoor));
        }
        else if (tt == TileType.DungeonFloor && SecretDoorSpot(r, c))
        {          
          
          candidates.Add((r, c));
        }
      }
    }

    if (rng.Next(2) == 0 && candidates.Count > 0)
    {
      int numToAdd = rng.Next(1, 4);
      for (int j = 0; j < numToAdd; j++)
      {
        (int, int) sq = candidates[rng.Next(candidates.Count)];
        map.SetTile(sq, TileFactory.Get(TileType.SecretDoor));
      }
    }

    bool SecretDoorSpot(int row, int col)
    {
      int adjWalls = Util.Adj8Sqs(row, col)
                          .Select(map.TileAt)
                          .Where(t => t.Type == TileType.DungeonWall).Count();
      if (adjWalls != 6)
        return false;

      TileType floor = TileType.DungeonFloor;

      if (map.TileAt(row - 1, col).Type == floor && map.TileAt(row + 1, col).Type == floor)
        return true;

      if (map.TileAt(row, col - 1).Type == floor && map.TileAt(row, col + 1).Type == floor)
        return true;

      return false;
    }

  }

  protected void SetStairs(int dungeonId, Map[] levels, (int, int) entrance, bool desc, Rng rng)
  {
    List<(int, int)> floors = levels[0].SqsOfType(TileType.DungeonFloor);

    // so first set the exit stairs on the first floor
    // (Exit is outwards, ie., stairs that lead oout of the dungeon)
    ExitLoc = floors[rng.Next(floors.Count)];
    Tile exitStairs = desc ? new Upstairs("") { Destination = new Loc(0, 0, entrance.Item1, entrance.Item2) }
                           : new Downstairs("") { Destination = new Loc(0, 0, entrance.Item1, entrance.Item2) };

    levels[0].SetTile(ExitLoc, exitStairs);

    CreateStairwayStacked(dungeonId, levels, 0, ExitLoc, desc, rng);
  }

  public void SetRoguelikeStairs(int dungeonId, Map[] levelMaps, Loc prevDest, Rng rng)
  {
    Downstairs? exitStairs = null;
    
    for (int lvl = 0; lvl < levelMaps.Length; lvl++)
    {      
      List<(int, int)> currFloorSqs = [];
      foreach (var room in levelMaps[lvl].FindRooms(5))
      {
        currFloorSqs.AddRange(room.Where(sq => levelMaps[lvl].TileAt(sq).Type == TileType.DungeonFloor));
      }
      
      Upstairs arrivalStairs = new("") { Destination = prevDest };
      (int upstairsR, int upstairsC) = currFloorSqs[rng.Next(currFloorSqs.Count)];
      levelMaps[lvl].SetTile(upstairsR, upstairsC, arrivalStairs);

      if (exitStairs is not null)
        exitStairs.Destination = new(dungeonId, lvl, upstairsR, upstairsC);

      if (lvl == 0)
        ExitLoc = (upstairsR, upstairsC);

      if (lvl == levelMaps.Length - 1)
        break;

      DijkstraMap dijkstra = new(levelMaps[lvl], [], levelMaps[lvl].Height, levelMaps[lvl].Width, false);
      dijkstra.Generate(StairsPathsCosts, (upstairsR, upstairsC), int.MaxValue);

      HashSet<(int, int)> floorsSet = [.. currFloorSqs];
      List<(int, int, int)> exitSqs = [];
      int sumOfCosts = 0;
      for (int r = 1; r < levelMaps[lvl].Height - 1; r++)
      {
        for (int c = 1; c < levelMaps[lvl].Width - 1; c++)
        {
          Tile tile = levelMaps[lvl].TileAt(r, c);
          if (tile.Type == TileType.DungeonFloor && floorsSet.Contains((r, c)))
          {
            int cost = dijkstra.Sqrs[r, c] * 2;
            if (cost < int.MaxValue)
            {
              exitSqs.Add((r, c, cost));
              sumOfCosts += cost;
            }
          }
        }        
      }

      exitSqs = [.. exitSqs.OrderByDescending(i => i.Item3)];
      int n = rng.Next(sumOfCosts);
      int j = exitSqs[0].Item3, i = 0;
      while (j < n)
      {
        j += exitSqs[i++].Item3;
      }

      (int exitR, int exitC) = (exitSqs[i].Item1, exitSqs[i].Item2);
      exitStairs = new Downstairs("");
      levelMaps[lvl].SetTile(exitR, exitC, exitStairs);

      prevDest = new Loc(dungeonId, lvl, exitR, exitC);
    }   
  }

  // Create stairways where dungeon levels are nearly stacked on top of one
  // another. Ie., if you go down stairs at (13, 20) on one level, you'll 
  // arrive at (13, 20) on the next level. Contrasted with classic rogue 
  // dungeons.
  static void CreateStairwayStacked(int dungeonId, Map[] levelMaps, int levelNum, (int, int) arrivalStairs, bool desc, Rng rng)
  {
    Map currLvl = levelMaps[levelNum];
    Map nextLvl = levelMaps[levelNum + 1];
    DijkstraMap dijkstra = new(currLvl, [], currLvl.Height, currLvl.Width, false);
    dijkstra.Generate(StairsPathsCosts, arrivalStairs, int.MaxValue);

    // We want to avoid placing stairs in a vault the player has potentially
    // no way to open. I can probably skip this check if I'm adding a second
    // staircase to a level?
    HashSet<(int, int)> lockedVaultSqs = [];    
    foreach (var room in nextLvl.FindRooms(9))
    {
      if (Rooms.IsLockedVault(nextLvl, room))
      {
        lockedVaultSqs = [.. lockedVaultSqs.Union(room)];
      }
    }
    
    List<(int, int, int)> floors = [];
    int sumOfCosts = 0;
    for (int r = 1; r < currLvl.Height - 1; r++)
    {
      for (int c = 1; c < currLvl.Width - 1; c++)
      {
        Tile tileBelow = nextLvl.TileAt(r, c);
        Tile tile = currLvl.TileAt(r, c);
        if (tile.Type == TileType.DungeonFloor && tileBelow.Type == TileType.DungeonFloor && !lockedVaultSqs.Contains((r, c)))
        {
          int cost = dijkstra.Sqrs[r, c] * 2;
          if (cost < int.MaxValue)
          {
            floors.Add((r, c, cost));
            sumOfCosts += cost;
          }
        }
      }
    }

    floors = [.. floors.OrderByDescending(i => i.Item3)];

    if (sumOfCosts < 0)
    {
      currLvl.Dump();
    }
    int n = rng.Next(sumOfCosts);
    int j = floors[0].Item3, i = 0;
    while (j < n)
    {
      j += floors[i++].Item3;
    }

    (int stairsR, int stairsC) = (floors[i].Item1, floors[i].Item2);
    Loc exitDest = new(dungeonId, levelNum + 1, stairsR, stairsC);
    Loc nextArrivalDest = new(dungeonId, levelNum, stairsR, stairsC);

    if (desc)
    {
      Downstairs exitStairs = new("") { Destination = exitDest };
      Upstairs nextArrivalStairs = new("") {  Destination = nextArrivalDest };
      currLvl.SetTile(stairsR, stairsC, exitStairs);
      nextLvl.SetTile(stairsR, stairsC, nextArrivalStairs);
    }
    else
    {
      Upstairs exitStairs = new("") { Destination = exitDest };
      Downstairs nextArrivalStairs = new("") { Destination = nextArrivalDest };
      currLvl.SetTile(stairsR, stairsC, exitStairs);
      nextLvl.SetTile(stairsR, stairsC, nextArrivalStairs);
    }

    if (levelNum < levelMaps.Length - 2)
    {
      CreateStairwayStacked(dungeonId, levelMaps, levelNum + 1, (stairsR, stairsC) , desc, rng);
    }
  }

  static int StairsPathsCosts(Tile tile)
  {
    if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor || tile.Type == TileType.SecretDoor)
      return 1;

    // This will allow instances where the arrival stairs are in a chasm room,
    // which I think I am going to allow for now. I'll revisit this later if
    // it seems annoying
    if (tile.Type == TileType.Chasm)
      return 2;

    if (!tile.Passable())
      return int.MaxValue;

    return 1;
  }

  static bool IsWall(TileType type) => type == TileType.DungeonWall || type == TileType.PermWall;

  static bool IsNWCorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row, col - 1).Type))
      return false;
    if (map.TileAt(row, col + 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (map.TileAt(row + 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;

    return true;
  }

  static bool IsNECorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row, col - 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row, col + 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (map.TileAt(row + 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;

    return true;
  }

  static bool IsSWCorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row - 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row, col - 1).Type))
      return false;
    if (map.TileAt(row, col + 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;

    return true;
  }

  static bool IsSECorner(Map map, int row, int col)
  {
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row - 1, col).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row - 1, col - 1).Type))
      return false;
    if (map.TileAt(row, col - 1).Type != TileType.DungeonFloor)
      return false;
    if (!IsWall(map.TileAt(row, col + 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col - 1).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col).Type))
      return false;
    if (!IsWall(map.TileAt(row + 1, col + 1).Type))
      return false;

    return true;
  }

  static List<(Loc, string)> FindCorners(Map map, int dungeonID, int level)
  {
    List<(Loc, string)> corners = [];

    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        TileType tile = map.TileAt(r, c).Type;

        if (tile != TileType.DungeonFloor)
          continue;

        if (IsNWCorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "nw"));
        else if (IsNECorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "ne"));
        else if (IsSWCorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "sw"));
        else if (IsSECorner(map, r, c))
          corners.Add((new Loc(dungeonID, level, r, c), "se"));
      }
    }

    return corners;
  }

  protected static void SetTraps(Map map, int dungeonID, int level, int dungeonDepth, Rng rng)
  {
    int[] trapOpts;
    if (level == 0)
      trapOpts = [3, 6, 7];
    else if (level == dungeonDepth - 1)
      trapOpts = [0, 1, 2, 3, 4, 6, 7]; // no trap doors on bottom level
    else
      trapOpts = [0, 1, 2, 3, 4, 5, 6, 7];

    (int, int) sq;
    int numOfTraps = rng.Next(1, 6);
    for (int j = 0; j < numOfTraps; j++)
    {
      int trap = trapOpts[rng.Next(trapOpts.Length)];
      switch (trap)
      {
        case 0:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenTeleportTrap));
          break;
        case 1:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenDartTrap));
          break;
        case 2:
          List<(Loc, string)> corners = FindCorners(map, dungeonID, level);
          (Loc corner, string dir) = corners[rng.Next(corners.Count)];
          FireJetTrap(map, corner, dir, rng);
          break;
        case 3:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenPit));
          break;
        case 4:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenWaterTrap));
          break;
        case 5:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenTrapDoor));
          break;
        case 6:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenMagicMouth));
          break;
        case 7:
          sq = map.RandomTile(TileType.DungeonFloor, rng);
          map.SetTile(sq, TileFactory.Get(TileType.HiddenSummonsTrap));
          break;
      }
    }
  }

  static bool CanPlaceJetTrigger(Map map, (int, int) corner, (int, int) delta)
  {
    (int, int) loc = corner;
    int count = 0;

    while (map.InBounds(loc) && map.TileAt(loc).Type == TileType.DungeonFloor && count < 4)
    {
      ++count;
      loc = (loc.Item1 + delta.Item1, loc.Item2 + delta.Item2);
    }

    return count == 4;
  }

  static void FireJetTrap(Map map, Loc cornerLoc, string dir, Rng rng)
  {
    (int, int) deltaH, deltaV;
    Dir horizontalDir, verticalDir;
    switch (dir)
    {
      case "nw":
        deltaH = (0, 1);
        deltaV = (1, 0);
        horizontalDir = Dir.East;
        verticalDir = Dir.South;
        break;
      case "ne":
        deltaH = (0, -1);
        deltaV = (0, 1);
        horizontalDir = Dir.West;
        verticalDir = Dir.South;
        break;
      case "sw":
        deltaH = (0, 1);
        deltaV = (-1, 0);
        horizontalDir = Dir.East;
        verticalDir = Dir.North;
        break;
      default:
        deltaH = (0, -1);
        deltaV = (-1, 0);
        horizontalDir = Dir.West;
        verticalDir = Dir.North;
        break;
    }

    bool horizontalValid = CanPlaceJetTrigger(map, (cornerLoc.Row, cornerLoc.Col), deltaH);
    bool verticalValid = CanPlaceJetTrigger(map, (cornerLoc.Row, cornerLoc.Col), deltaV);

    if (!horizontalValid && !verticalValid)
      return;

    Loc jetLoc;
    Loc triggerLoc;
    Dir jetDir;
    if (horizontalValid && verticalValid)
    {
      if (rng.NextDouble() < 0.5)
      {
        // horizontal
        jetDir = horizontalDir;
        jetLoc = cornerLoc with { Col = cornerLoc.Col - deltaH.Item2 };
        triggerLoc = cornerLoc with { Col = cornerLoc.Col + deltaH.Item2 * rng.Next(1, 4) };
      }
      else
      {
        // vertical
        jetDir = verticalDir;
        jetLoc = cornerLoc with { Row = cornerLoc.Row - deltaV.Item1 };
        triggerLoc = cornerLoc with { Row = cornerLoc.Row + deltaV.Item1 * rng.Next(1, 4) };
      }
    }
    else if (horizontalValid)
    {
      jetDir = horizontalDir;
      jetLoc = cornerLoc with { Col = cornerLoc.Col - deltaH.Item2 };
      triggerLoc = cornerLoc with { Col = cornerLoc.Col + deltaH.Item2 * rng.Next(1, 4) };
    }
    else
    {
      jetDir = verticalDir;
      jetLoc = cornerLoc with { Row = cornerLoc.Row - deltaV.Item1 };
      triggerLoc = cornerLoc with { Row = cornerLoc.Row + deltaV.Item1 * rng.Next(1, 4) };
    }

    Tile fireJet = new FireJetTrap(false, jetDir);
    map.SetTile(jetLoc.Row, jetLoc.Col, fireJet);
    Tile trigger = new JetTrigger(jetLoc, false);
    map.SetTile(triggerLoc.Row, triggerLoc.Col, trigger);
  }

  protected static void AddBaitIllusion(Map map, int dungeonId, int levelNum, GameObjectDB objDb, Rng rng)
  {
    var sqs = map.SqsOfType(TileType.DungeonFloor).Select(sq => new Loc(dungeonId, levelNum, sq.Item1, sq.Item2));
    List<Loc> openFloors = [.. sqs.Where(l => !objDb.AreBlockersAtLoc(l))];
    if (openFloors.Count == 0)
      return;
    Loc loc = openFloors[rng.Next(openFloors.Count)];
    Tile trap = rng.Next(3) switch
    {
      0 => TileFactory.Get(TileType.HiddenDartTrap),
      1 => TileFactory.Get(TileType.HiddenTrapDoor),
      _ => TileFactory.Get(TileType.HiddenWaterTrap)
    };
    map.SetTile(loc.Row, loc.Col, trap);

    ItemNames itemName = rng.Next(7) switch
    {
      0 => ItemNames.ZORKMIDS,
      1 => ItemNames.POTION_HEALING,
      2 => ItemNames.SCROLL_BLINK,
      3 => ItemNames.LONGSWORD,
      4 => ItemNames.FLASK_OF_BOOZE,
      5 => ItemNames.WAND_MAGIC_MISSILES,
      _ => ItemNames.SCROLL_PROTECTION
    };
    Item bait = ItemFactory.Illusion(itemName, objDb);
    objDb.SetToLoc(loc, bait);
  }

  protected static void AddRiverToLevel(TileType riverTile, Map map, Map? mapBelow, int levelNum, int height, int width, int dungeonId, GameObjectDB objDb, Rng rng)
  {
    DungeonMap.CreateRiver(map, width + 1, height + 1, riverTile, dungeonId, levelNum, objDb, rng);

    // When making a chasm, we want to turn any walls below chasms on the 
    // floor below into floors. 
    if (riverTile == TileType.Chasm && mapBelow is not null)
    {
      for (int r = 1; r < height; r++)
      {
        for (int c = 1; c < width; c++)
        {
          var pt = (r, c);
          if (ReplaceChasm(map, pt) && mapBelow.IsTile(pt, TileType.DungeonWall))
          {
            mapBelow.SetTile(pt, TileFactory.Get(TileType.DungeonFloor));
          }
        }
      }
    }

    static bool ReplaceChasm(Map map, (int, int) pt) => map.TileAt(pt).Type switch
    {
      TileType.Chasm or TileType.Bridge or TileType.WoodBridge => true,
      _ => false,
    };
  }

  protected static void AddMoldPatch(Map map, List<Loc> floors, GameObjectDB objDb, Rng rng)
  {
    List<Loc> openFloors = [.. floors.Where(l => !objDb.AreBlockersAtLoc(l))];
    if (openFloors.Count == 0)
      return;

    Loc loc = openFloors[rng.Next(openFloors.Count)];
    Item mold = ItemFactory.YellowMold();
    objDb.Add(mold);
    objDb.SetToLoc(loc, mold);

    List<Loc> moldySpots = [loc];
    foreach (Loc adj in Util.Adj8Locs(loc))
    {
      if (map.TileAt(adj.Row, adj.Col).Passable() && rng.Next(5) == 0)
      {
        mold = ItemFactory.YellowMold();
        objDb.Add(mold);
        objDb.SetToLoc(adj, mold);
      }
    }
  }
}

class MainDungeonBuilder : DungeonBuilder
{
  int _dungeonID;

  string DeepOneShrineDesc(Rng rng)
  {
    var sb = new StringBuilder();
    sb.Append("A shrine depicting ");

    string adj = rng.Next(4) switch
    {
      0 => "a grotesque ",
      1 => "a misshapen ",
      2 => "a rough-hewn ",
      _ => "a crudely carved "
    };
    sb.Append(adj);

    string feature;
    switch (rng.Next(4))
    {
      case 0:
        sb.Append("humanoid with ");
        feature = rng.Next(3) switch
        {
          0 => "eyestalks and lobster claws.",
          1 => "the head of a carp.",
          _ => "a crab's body."
        };
        sb.Append(feature);
        break;        
      case 1:
        sb.Append("shark with ");
        feature = rng.Next(2) == 0 ? "the arms of a human." : "eyestalks.";
        sb.Append(feature);
        break;
      case 2:
        sb.Append("turtle with ");
        feature = rng.Next(2) == 0 ? "a human face." : "a shark's head.";
        sb.Append(feature);
        break;
      default:
        sb.Append("lobster with ");
        feature = rng.Next(2) == 0 ? "a human face." : "a shark's head.";
        sb.Append(feature);
        break;
    }

    string decoration = rng.Next(4) switch
    {
      0 => " It is strewn with shells and glass bleads.",
      1 => " It is streaked with blood.",
      2 => " It is adorned with teeth and driftwood.",
      _ => " It is decorated with rotting meat and worthless baubles."
    };
    sb.Append(decoration);

    return sb.ToString();
  }

  // Add a deep one shrine near the river that was generated on the map, if
  // possible
  void DeepOneShrine(Map map, int dungeonID, int level, GameObjectDB objDb, Rng rng)
  {
    static string CalcChant(Rng rng)
    {
      int roll = rng.Next(4);

      char[] subs = ['w', 'v', 'u', 'm', 'n', '\'', ' '];
      var sb = new StringBuilder("Ooooooo");     
      for (int i = 0; i < 5; i++)
      {
        int c = rng.Next(1, sb.Length - 1);
        sb[c] = subs[rng.Next(subs.Length)];
      }
      sb.Append('!');
      
      return sb.ToString();
    }

    HashSet<(int, int)> candidates = [];


    if (candidates.Count == 0)
      // can't place the shrine
      return;

    List<(int, int)> floors = [.. candidates];
    var loc = floors[rng.Next(floors.Count)];

    Tile shrine = new Landmark(DeepOneShrineDesc(rng));
    map.SetTile(loc, shrine);
    Loc shrineLoc = new(dungeonID, level, loc.Item1, loc.Item2);

    List<Loc> deepOneLocs = [.. floors.Select(sq => new Loc(dungeonID, level, sq.Item1, sq.Item2)).Where(l => Util.Distance(shrineLoc, l) <= 3)];
    
    int numOfDeepOnes = int.Min(rng.Next(3) + 2, deepOneLocs.Count);
    List<Actor> deepOnes = [];
    for (int j = 0; j < numOfDeepOnes; j++)
    {
      if (deepOneLocs.Count == 0)
        break;

      Actor d = MonsterFactory.Get("deep one", objDb, rng);
      d.Traits.Add(new WorshiperTrait() 
      { 
        AltarLoc = shrineLoc,
        AltarId = 0,
        Chant = CalcChant(rng)
      });

      int x = rng.Next(deepOneLocs.Count);
      Loc pickedLoc = deepOneLocs[x];
      deepOneLocs.RemoveAt(x);

      objDb.AddNewActor(d, pickedLoc);
      deepOnes.Add(d);
    }

    Actor shaman = MonsterFactory.Get("deep one shaman", objDb, rng);
    shaman.Traits.Add(new WorshiperTrait() 
    { 
      AltarLoc = shrineLoc,
      AltarId = 0,
      Chant = CalcChant(rng)
    });
    shaman.Stats[Attribute.MobAttitude].SetMax(Mob.INDIFFERENT);
    
    if (deepOneLocs.Count > 0)
    {
      Loc shamanLoc = deepOneLocs[rng.Next(deepOneLocs.Count)];
      objDb.AddNewActor(shaman, shamanLoc);
      deepOnes.Add(shaman);
    }

    foreach (Actor deepOne in deepOnes)
    {
      List<ulong> allies = [.. deepOnes.Select(k => k.ID).Where(id => id != deepOne.ID)];
      deepOne.Traits.Add(new AlliesTrait() { IDs = allies });
      deepOne.Stats[Attribute.MobAttitude].SetMax(Mob.INDIFFERENT);
    }

    // Add a few items nearby
    List<Loc> nearbyLocs = [];
    for (int r = -2; r < 3; r++)
    {
      for (int c = -2;  c < 3; c++)
      {
        Loc l = shrineLoc with { Row = shrineLoc.Row + r, Col = shrineLoc.Col + c };
        if (map.InBounds(l.Row, l.Col) && map.TileAt(l.Row, l.Col).Type == TileType.DungeonFloor)
        {
          nearbyLocs.Add(l);
        }
      }
    }

    if (nearbyLocs.Count > 0)
    {
      foreach (Item loot in Treasure.PoorTreasure(4, rng, objDb))
      {
        loot.Traits.Add(new OwnedTrait() { 
          OwnerIDs = [.. deepOnes.Select(d => d.ID)]
        });
        Loc itemLoc = nearbyLocs[rng.Next(nearbyLocs.Count)];
        objDb.SetToLoc(itemLoc, loot);
      }
    }
  }

  static void MoonDaughterCleric(Map[] levels, int dungeonId, Rng rng, GameObjectDB objDb)
  {
    int level = -1;
    for (int j = 2; j < levels.Length; j++)
    {
      if (rng.NextDouble() <= 0.2)
      {
        level = j;
        break;        
      }
    }

    if (level == -1)
      return;

    NameGenerator ng = new(rng, Util.NamesFile);
    Mob cleric = new()
    {
      Name = ng.GenerateName(8),
      Appearance = "A cleric whose face is concealed by a deep hood. They are suffused with a faint silver glow.",
      Glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREEN, Colours.BLACK, false)
    };
    cleric.Stats[Attribute.HP] = new Stat(50);
    cleric.Traits.Add(new VillagerTrait());
    cleric.Traits.Add(new NamedTrait());
    cleric.Traits.Add(new IntelligentTrait());
    cleric.Traits.Add(new DialogueScriptTrait() { ScriptFile = "moon_daughter_cleric.txt" });
    cleric.SetBehaviour(new MoonDaughtersClericBehaviour());
    cleric.Traits.Add(new BehaviourTreeTrait() { Plan = "MoonClericPlan" });
    cleric.Traits.Add(new LightSourceTrait() { Radius = 1, OwnerID = cleric.ID, FgColour = Colours.ICE_BLUE, BgColour = Colours.MYSTIC_AURA });

    List<Loc> floors = levels[level].ClearFloors(dungeonId, level, objDb);

    Loc startLoc = floors[rng.Next(floors.Count)];
    objDb.AddNewActor(cleric, startLoc);
  }

  public Dungeon Generate(int id, string name, string arrivalMessage, int h, int w, int numOfLevels, (int, int) entrance, 
        FactDb factDb, GameObjectDB objDb, Rng rng, List<MonsterDeck> monsterDecks, Map wildernessMap)
  {    
    _dungeonID = id;
    var dungeon = new Dungeon(id, name, arrivalMessage, true);
    var mapper = new DungeonMap(rng);
    Map[] levels = new Map[numOfLevels];

    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(w, h);
      dungeon.AddMap(levels[levelNum]);      
    }

    MoonDaughterCleric(levels, id, rng, objDb);

    return dungeon;
  }
}