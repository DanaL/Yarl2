// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

// Tower/mansion style map I'll use Binary Space Partitioning to build
class Tower(int height, int width, int minLength)
{
  const int VERTICAL = 0;
  const int HORIZONTAL = 1;

  int Height { get; set; } = height;
  int Width { get; set; } = width;
  int MinLength { get; set; } = minLength;

  void Partition(bool[,] map, int tr, int lc, int br, int rc, Rng rng)
  {
    List<int> options = [];
    if (br - tr > MinLength)
      options.Add(HORIZONTAL);
    if (rc - lc > MinLength)
      options.Add(VERTICAL);

    // We're done recursing
    if (options.Count == 0)
      return;

    int choice = options[rng.Next(options.Count)];

    if (choice == VERTICAL)
    {
      int a = int.Min(lc + MinLength, Width - 1), b = int.Max(0, rc - MinLength);
      int col;
      if (a == b)
        col = a;
      else
        col = rng.Next(int.Min(a, b), int.Max(a, b));

      for (int r = tr; r <= br; r++)
      {
        map[r, col] = true;
      }

      Partition(map, tr, lc, br, col, rng);
      Partition(map, tr, col + 1, br, rc, rng);
    }
    else
    {
      int a = int.Min(tr + MinLength, Height - 1), b = int.Max(0, br - MinLength);
      int row;
      if (a == b)
        row = a;
      else
        row = rng.Next(int.Min(a, b), int.Max(a, b));

      for (int c = lc; c <= rc; c++)
      {
        map[row, c] = true;
      }

      Partition(map, tr, lc, row, rc, rng);
      Partition(map, row + 1, lc, br, rc, rng);
    }
  }

  static List<Room> FindRooms(Map map)
  {
    RegionFinder rf = new(new DungeonPassable());
    Dictionary<int, HashSet<(int, int)>> regions = rf.Find(map, false, 0, TileType.DungeonFloor);

    // Convert the hashset of floor tiles to Room objects
    List<Room> rooms = [];
    foreach (var room in regions.Values)
    {
      Room r = new() { Sqs = room };

      foreach ((int row, int col) in room)
      {
        foreach (var sq in Util.Adj8Sqs(row, col))
        {
          if (map.TileAt(sq).Type == TileType.DungeonWall)
            r.Perimeter.Add(sq);
        }
      }

      rooms.Add(r);
    }

    return rooms;
  }

  void Dump(bool[,] map)
  {
    for (int r = 0; r < Height; r++)
    {
      for (int c = 0; c < Width; c++)
      {
        char ch = map[r, c] ? '#' : '.';
        Console.Write(ch);
      }
      Console.WriteLine();
    }
  }

  static void MergeAdjacentRooms(Map map, Room room, List<Room> rooms, Rng rng)
  {
    List<List<(int, int)>> adjWalls = [];
    foreach (Room r in rooms)
    {
      if (r == room)
        continue;

      List<(int, int)> walls = [];
      foreach ((int row, int col) in room.Perimeter.Intersect(r.Perimeter))
      {
        // We want to look for shared walls where there are floor sqs either
        // north and south or east and west.
        if (DoorCandidate(map, row, col))
        {
          walls.Add((row, col));
        }
      }

      if (walls.Count > 0)
      {
        adjWalls.Add(walls);
      }
    }

    if (adjWalls.Count == 0)
      return;

    int i = rng.Next(adjWalls.Count);
    foreach ((int row, int col) in adjWalls[i])
    {
      map.SetTile(row, col, TileFactory.Get(TileType.DungeonFloor));
    }
  }

  static bool DoorCandidate(Map map, int row, int col)
  {
    if (map.TileAt(row - 1, col).Type == TileType.DungeonFloor && map.TileAt(row + 1, col).Type == TileType.DungeonFloor)
      return true;
    if (map.TileAt(row, col - 1).Type == TileType.DungeonFloor && map.TileAt(row, col + 1).Type == TileType.DungeonFloor)
      return true;

    return false;
  }

  static void EraseExteriorRoom(Map map, Room room, List<Room> rooms)
  {
    List<Room> otherRooms = [.. rooms.Where(r => r != room)];
    foreach ((int r, int c) in room.Sqs)
    {
      map.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
    }
    foreach ((int r, int c) in room.Perimeter)
    {
      if (!SharedWall(r, c, otherRooms))
        map.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
    }

    rooms.Remove(room);

    static bool SharedWall(int r, int c, List<Room> others)
    {
      foreach (Room room in others)
      {
        if (room.Perimeter.Contains((r, c)))
          return true;
      }

      return false;
    }
  }

  static Room MergeRooms(Room a, Room b)
  {
    Room r = new()
    {
      Sqs = [.. a.Sqs.Union(b.Sqs)]
    };

    foreach ((int row, int col) in a.Perimeter.Union(b.Perimeter))
    {
      if (OutsideWall(row, col))
        r.Perimeter.Add((row, col));
      else
        r.Sqs.Add((row, col));
    }

    return r;

    bool OutsideWall(int row, int col)
    {
      foreach (var sq in Util.Adj8Sqs(row, col))
      {
        if (!(a.Sqs.Contains(sq) || b.Sqs.Contains(sq) || a.Perimeter.Contains(sq) || b.Perimeter.Contains(sq)))
          return true;
      }

      return false;
    }
  }

  static void SetDoors(Map map, Rng rng)
  {
    // Just rebuilding the set of rooms here. It seemed simpler than trying to
    // merge Room objects when we are merged interior rooms and I can't imagine
    // the inefficiency will be even noticable.
    Dictionary<int, Room> rooms = [];
    int i = 0;
    foreach (Room r in FindRooms(map))
    {
      rooms[i++] = r;
    }

    List<int> roomIds = [.. Enumerable.Range(0, rooms.Count)];
    roomIds.Shuffle(rng);

    while (roomIds.Count > 1)
    {
      int j = roomIds[0];
      Room room = rooms[j];

      // Find the adjacent rooms
      List<int> adjRooms = [];
      foreach (int otherId in roomIds)
      {
        if (otherId == j)
          continue;

        foreach ((int r, int c) in room.Perimeter.Intersect(rooms[otherId].Perimeter))
        {
          if (DoorCandidate(map, r, c))
          {
            adjRooms.Add(otherId);
            break;
          }
        }
      }

      if (adjRooms.Count == 0)
      {
        // if there were no adjacent rooms, the room is a dud like:
        //
        //       ##########
        //       #........#
        //       #........#
        //   ##############
        //   #...#
        //   #...#
        //   #####
        //
        // I'll just fill them in with walls?

        rooms.Remove(j);
        roomIds.Remove(j);

        continue;
      }

      adjRooms.Shuffle(rng);

      int toMerge = rng.Next(1, adjRooms.Count + 1);
      for (int k = 0; k < toMerge; k++)
      {
        int otherId = adjRooms[k];
        Room other = rooms[otherId];

        // place the door
        List<(int r, int c)> doorable = [];
        List<(int r, int c)> shared = [.. room.Perimeter.Intersect(other.Perimeter)];
        foreach ((int r, int c) in shared)
        {
          if (DoorCandidate(map, r, c))
            doorable.Add((r, c));
        }

        (int dr, int dc) = doorable[rng.Next(doorable.Count)];
        TileType tile = rng.NextDouble() <= 0.25 ? TileType.ClosedDoor : TileType.LockedDoor;

        map.SetTile(dr, dc, TileFactory.Get(tile));

        room = MergeRooms(room, other);
        rooms.Remove(otherId);
        roomIds.Remove(otherId);
      }

      rooms[j] = room;
    }
  }

  static void TweakExterior(Map map, List<Room> rooms, Rng rng)
  {
    List<Room> corners = [];
    List<Room> exterior = [];
    List<Room> interior = [];

    foreach (Room room in rooms)
    {
      bool north = NorthExterior(room.Perimeter);
      bool south = SouthExterior(map, room.Perimeter);
      bool west = WestExterior(room.Perimeter);
      bool east = EastExterior(map, room.Perimeter);

      if (north || south)
      {
        if (east || west)
          corners.Add(room);
        else
          exterior.Add(room);
      }
      else if (west || east)
      {
        exterior.Add(room);
      }
      else
      {
        interior.Add(room);
      }
    }

    foreach (Room room in corners)
    {
      EraseExteriorRoom(map, room, rooms);
    }

    int exteriorToRemove = rng.Next(1, 4);
    List<int> exteriorIndexes = [.. Enumerable.Range(0, exterior.Count)];
    while (exteriorIndexes.Count > 0 && exteriorToRemove > 0)
    {
      int j = rng.Next(exteriorIndexes.Count);

      Room room = exterior[j];
      EraseExteriorRoom(map, room, rooms);

      --exteriorToRemove;
      exteriorIndexes.RemoveAt(j);
    }
  }

  static bool NorthExterior(HashSet<(int, int)> perimeter)
  {
    foreach ((int r, _) in perimeter)
    {
      if (r == 1)
        return true;
    }

    return false;
  }

  static bool SouthExterior(Map map, HashSet<(int, int)> perimeter)
  {
    foreach ((int r, _) in perimeter)
    {
      if (r == map.Height - 2)
        return true;
    }

    return false;
  }

  static bool WestExterior(HashSet<(int, int)> perimeter)
  {
    foreach ((_, int c) in perimeter)
    {
      if (c == 1)
        return true;
    }

    return false;
  }

  static bool EastExterior(Map map, HashSet<(int, int)> perimeter)
  {
    foreach ((_, int c) in perimeter)
    {
      if (c == map.Width - 2)
        return true;
    }

    return false;
  }

  static void TweakInterior(Map map, List<Room> rooms, Rng rng)
  {
    List<Room> interior = [];

    foreach (Room room in rooms)
    {
      bool north = NorthExterior(room.Perimeter);
      bool south = SouthExterior(map, room.Perimeter);
      bool west = WestExterior(room.Perimeter);
      bool east = EastExterior(map, room.Perimeter);

      if (north || south || west || east)
        continue;

      interior.Add(room);
    }

    int toMerge = int.Min(rng.Next(8, 12), interior.Count);
    List<int> indexes = [.. Enumerable.Range(0, interior.Count)];
    indexes.Shuffle(rng);
    for (int j = 0; j < toMerge; j++)
    {
      int m = indexes[j];
      MergeAdjacentRooms(map, rooms[m], rooms, rng);
    }

    SetDoors(map, rng);
  }

  static void DrawWallFromCorner(Map map, int row, int col, Rng rng)
  {
    List<(int, int)> floors = [];
    foreach (var sq in Util.Adj4Sqs(row, col))
    {
      if (map.TileAt(sq).Type == TileType.DungeonFloor)
        floors.Add((sq.Item1, sq.Item2));
    }

    if (floors.Count > 1 && rng.NextDouble() < 0.5)
    {
      floors.RemoveAt(rng.Next(0, 2));
    }

    foreach (var sq in floors)
    {
      var floor = (sq.Item1, sq.Item2);
      List<(int, int)> wall = [];
      (int, int) delta = (sq.Item1 - row, sq.Item2 - col);

      bool validWall = true;
      while (map.TileAt(floor).Type == TileType.DungeonFloor)
      {
        if (!ValidWallPlacement(floor.Item1, floor.Item2, delta.Item1, delta.Item2))
        {
          validWall = false;
          break;
        }

        wall.Add(floor);
        floor = (floor.Item1 + delta.Item1, floor.Item2 + delta.Item2);
      }

      if (validWall)
      {
        foreach (var wallTile in wall)
        {
          map.SetTile(wallTile, TileFactory.Get(TileType.DungeonWall));
        }
      }
    }

    bool ValidWallPlacement(int row, int col, int deltaR, int deltaC)
    {
      if (deltaR != 0)
      {
        if (map.TileAt(row, col - 1).Type != TileType.DungeonFloor)
          return false;
        if (map.TileAt(row, col + 1).Type != TileType.DungeonFloor)
          return false;
      }
      else
      {
        if (map.TileAt(row - 1, col).Type != TileType.DungeonFloor)
          return false;
        if (map.TileAt(row + 1, col).Type != TileType.DungeonFloor)
          return false;
      }

      return true;
    }
  }

  Map RedrawInterior(Map outline, Rng rng)
  {
    // First, we want to find the interior corners.
    // I think all the interior corners will be squares that have two
    // adjacent floors?
    List<(int, int)> corners = [];
    for (int r = 0; r < outline.Height; r++)
    {
      for (int c = 0; c < outline.Width; c++)
      {
        if (IsCorner(r, c))
          corners.Add((r, c));
      }
    }

    Map map = (Map) outline.Clone();
    foreach ((int row, int col) in corners)
    {
      DrawWallFromCorner(map, row, col, rng);
    }

    RegionFinder rf = new(new DungeonPassable());
    Dictionary<int, HashSet<(int, int)>> rooms =  rf.Find(map, false, 0, TileType.DungeonFloor);

    foreach (int roomId in rooms.Keys)
    {
      HashSet<(int, int)> room = rooms[roomId];

      int tr = map.Height, lc = map.Width, br = 0, rc = 0;
      foreach ((int row, int col) in room)
      {
        if (row < tr)
          tr = row;
        if (row > br)
          br = row;
        if (col < lc)
          lc = col;
        if (col > rc)
          rc = col;
      }

      int height = br - tr + 1;
      int width = rc - lc + 1;
      bool[,] roomMap = new bool[height + 2, width + 2];
      for (int r = 0; r < height + 2; r++)
      {
        roomMap[r, 0] = true;
        roomMap[r, width + 1] = true;
      }
      for (int c = 0; c < width + 2; c++)
      {
        roomMap[0, c] = true;
        roomMap[height + 1, c] = true;
      }

      Partition(roomMap, 0, 0, height, width, rng);

      // Copy the partitioned room back into the map
      for (int r = 0; r < height + 2; r++)
      {
        for (int c = 0; c < width + 2; c++)
        {
          if (roomMap[r, c])
            map.SetTile(r + tr - 1, c + lc - 1, TileFactory.Get(TileType.DungeonWall));
        }
      }
    }

    List<Room> rms = FindRooms(map);
    TweakInterior(map, rms, rng);

    return map;

    bool IsCorner(int row, int col)
    {
      if (outline.TileAt(row, col).Type != TileType.DungeonWall)
        return false;

      int i = 0;
      foreach (var sq in Util.Adj4Sqs(row, col))
      {
        if (outline.TileAt(sq).Type == TileType.DungeonFloor)
          ++i;
      }

      return i == 2;
    }
  }

  static Map GenerateOutline(Map map)
  {
    Map outline = new(map.Width, map.Height);

    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r, c).Type == TileType.WorldBorder)
        {
          outline.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
          continue;
        }

        bool isEdge = false;
        foreach (var sq in Util.Adj8Sqs(r, c))
        {
          if (map.TileAt(sq).Type == TileType.WorldBorder)
          {
            isEdge = true;
            break;
          }
        }

        TileType type = isEdge ? TileType.DungeonWall : TileType.DungeonFloor;
        outline.SetTile(r, c, TileFactory.Get(type));
      }
    }

    return outline;
  }

  Map Build(Rng rng)
  {
    // False == floor, true == wall
    var map = new bool[Height, Width];
    for (int r = 0; r < Height; r++)
    {
      map[r, 0] = true;
      map[r, Width - 1] = true;
    }
    for (int c = 0; c < Width; c++)
    {
      map[0, c] = true;
      map[Height - 1, c] = true;
    }

    Partition(map, 1, 1, Height - 2, Width - 2, rng);

    Map tower = new(Width + 2, Height + 2);
    for (int r = 0; r < Height + 2; r++)
    {
      for (int c = 0; c < Width + 2; c++)
      {
        if (r == 0 || c == 0)
          tower.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
        else if (r == Height + 1 || c == Width + 1)
          tower.SetTile(r, c, TileFactory.Get(TileType.WorldBorder));
        else if (map[r - 1, c - 1])
          tower.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));
        else
          tower.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    List<Room> rooms = FindRooms(tower);
    TweakExterior(tower, rooms, rng);
    TweakInterior(tower, rooms, rng);
    tower.Dump();

    Map outline = GenerateOutline(tower);
    outline.Dump();

    var nextFloor = RedrawInterior(outline, rng);
    nextFloor.Dump();

    var nnextFloor = RedrawInterior(outline, rng);
    nnextFloor.Dump();

    return tower;
  }

  static bool ValidSpotForTower(int row, int col, Map tower, Map wilderness, Town town)
  {
    if (!OpenSq(row, col))
      return false;
      
    foreach (var adj in Util.Adj8Sqs(row, col))
    {
      if (!OpenSq(adj.Item1, adj.Item2))
        return false;
    }

    return true;

    bool OpenSq(int row, int col)
    {
      if (row >= town.Row && row <= town.Row + town.Height && col >= town.Col && col <= town.Col + town.Width)
          return false;

      Tile tile = wilderness.TileAt(row, col);
      return tile.Type switch
      {
        TileType.DeepWater or TileType.Dirt or TileType.StoneRoad
          or TileType.Bridge or TileType.Portal 
          or TileType.Mountain or TileType.SnowPeak => false,
        _ => true,
      };
    }
  }

  public void BuildTower(Map wilderness, Town town, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    Map firstFloor = Build(rng);

    // Find a place for the tower
    List<(int, int)> options = [];
    for (int r = 3; r < wilderness.Height - Height - 3; r++)
    {
      for (int c = 3; c < wilderness.Width - Width - 3; c++)
      {
        if (ValidSpotForTower(r, c, firstFloor, wilderness, town))
        {
          options.Add((r, c));
        }
      }
    }

    (int row, int col) = options[rng.Next(options.Count)];
    foreach (var sq in Util.Adj8Sqs(row, col))
    {
      wilderness.SetTile(sq, TileFactory.Get(TileType.PermWall));
    }
    Upstairs entrance = new("")
    {
      Destination = new Loc(0, 0, 0, 0)
    };
    wilderness.SetTile(row, col, entrance);

    (int doorRow, int doorCol) = rng.Next(4) switch
    {
      0 => (row - 1, col),
      1 => (row + 1, col),
      2 => (row, col + 1),
      _ => (row, col - 1)
    };
    // This will eventually be a fancy, magically locked door
    Portcullis p = new(false);
    wilderness.SetTile(doorRow, doorCol, p);
    LocationFact lf = new()
    {
      Loc = new Loc(0, 0, doorRow, doorCol),
      Desc = "Tower Gate"
    };
    factDb.Add(lf);
    
    (int dr, int dc) = (doorRow - row, doorCol - col);
    Loc msgLoc = new(0, 0, doorRow + dr, doorCol + dc);
    MessageAtLoc pal = new(msgLoc, "A portcullis scored with glowing, arcane runes bars the entrance to this tower.");
    objDb.ConditionalEvents.Add(pal);
  }
}