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

// Generate dungeon levels! I drew a lot upon Bob Nystrom's blog and Roguelike Basin
class DungeonMap(Random rng)
{
  readonly Random _rng = rng;

  static Tile PickDoor(Random rng) =>
    TileFactory.Get(rng.Next(4) == 0 ? TileType.LockedDoor : TileType.ClosedDoor);
  
  static bool IsDoor(Tile tile) => 
    tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor;

  // Pick a room template to overlay onto the map (currently either 
  // rectangular or circular)
  (List<(int, int)>, RoomShapes) MakeRoomTemplate()
  {
    int height, width;
    RoomShapes shape;
    List<(int, int)> sqs = new();
    var rn = _rng.NextDouble();

    if (rn < 0.8)
    {
      // make a rectangular room
      shape = RoomShapes.Rect;
      height = _rng.Next(5, 10);
      if (height % 2 == 0)
        ++height;
      width = _rng.Next(5, 20);
      if (width % 2 == 0)
        ++width;
      for (int r = 0; r < height; r++)
      {
        for (int c = 0; c < width; c++)
        {
          sqs.Add((r, c));
        }
      }
    }
    else
    {
      // make a circular room       
      shape = RoomShapes.Round;
      var radius = _rng.Next(3, 6);
      height = radius * 2 + 3;
      width = radius * 2 + 3;

      sqs = Util.BresenhamCircle(height / 2, width / 2, radius).ToList();

      // Now turn all the squares inside the circle into floors
      for (int r = 1; r < height - 1; r++)
      {
        for (int c = 1; c < width - 1; c++)
        {
          if (Util.Distance(r, c, height / 2, width / 2) <= radius)
            sqs.Add((r, c));
        }
      }
    }

    return (sqs, shape);
  }

  List<Room> AddRooms(Map map)
  {
    var rooms = new List<Room>();
    var perimeters = new HashSet<(int, int)>();
    int maxTries = 75;

    for (int x = 0; x < maxTries; x++)
    {
      var (sqs, shape) = MakeRoomTemplate();
      int rh = sqs.Select(s => s.Item1).Max();
      int rw = sqs.Select(s => s.Item2).Max();

      var row = _rng.Next(1, map.Height - rh - 1);
      if (row % 2 == 0)
        row += 1;
      var col = _rng.Next(1, map.Width - rw - 1);
      if (col % 2 == 0)
        col += 1;
      sqs = sqs.Select(s => (s.Item1 + row, s.Item2 + col)).ToList();
      bool overlap = false;
      foreach (var sq in sqs)
      {
        if (map.TileAt(sq.Item1, sq.Item2).Type == TileType.DungeonFloor)
        {
          overlap = true;
          break;
        }
        if (perimeters.Contains(sq))
        {

          overlap = true;
          break;
        }
      }
      if (overlap)
        continue;

      var room = new Room(sqs, shape);
      rooms.Add(room);
      perimeters = perimeters.Union(room.Permieter).ToHashSet();

      foreach (var sq in sqs)
      {
        map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    return rooms;
  }

  static List<(int, int)> MazeNeighbours(Map map, int row, int col, TileType type, int d)
  {
    (int, int)[] adj = [(-d, 0), (d, 0), (0, d), (0, -d)];
    return adj.Select(n => (row + n.Item1, col + n.Item2))
                         .Where(n => map.InBounds(n.Item1, n.Item2))
                         .Where(n => map.TileAt(n.Item1, n.Item2).Type == type).ToList();
  }

  static int AdjFloors(Map map, int row, int col)
  {
    (int, int)[] adj = [(-1, -1),
      (-1, 0),
      (-1, 1),
      (0, -1),
      (0, 1),
      (1, -1),
      (1, 0),
      (1, 1)];
    return adj.Select(n => (row + n.Item1, col + n.Item2))
                         .Where(n => map.InBounds(n.Item1, n.Item2))
                         .Where(n => map.TileAt(n.Item1, n.Item2).Type == TileType.DungeonFloor).Count();
  }

  static void ConnectNeighbours(Map map, int r1, int c1, int r2, int c2)
  {
    if (r1 < r2)
      map.SetTile(r1 + 1, c1, TileFactory.Get(TileType.DungeonFloor));
    else if (r1 > r2)
      map.SetTile(r1 - 1, c1, TileFactory.Get(TileType.DungeonFloor));
    else if (c1 < c2)
      map.SetTile(r1, c1 + 1, TileFactory.Get(TileType.DungeonFloor));
    else if (c1 > c2)
      map.SetTile(r1, c1 - 1, TileFactory.Get(TileType.DungeonFloor));
  }

  static (bool, int, int) MazeStart(Map map)
  {
    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        if (map.TileAt(r, c).Type == TileType.DungeonWall && AdjFloors(map, r, c) == 0)
          return (true, r, c);
      }
    }

    return (false, 0, 0);
  }

  static IEnumerable<(int, int)> NextNeighbours(Map map, int r, int c)
  {
    return MazeNeighbours(map, r, c, TileType.DungeonWall, 2)
                .Where(s => AdjFloors(map, s.Item1, s.Item2) == 0);
  }

  // Random floodfill maze passages. (We have to do this a few times)
  bool CarveMaze(Map map)
  {
    var (success, startRow, startCol) = MazeStart(map);

    if (success)
    {
      map.SetTile(startRow, startCol, TileFactory.Get(TileType.DungeonFloor));

      // find neighbours (2 sq away) that they are fully enclosed
      var neighbours = NextNeighbours(map, startRow, startCol)
                          .Select(n => (n, (startRow, startCol))).ToList();
      while (neighbours.Count > 0)
      {
        var i = _rng.Next(neighbours.Count);
        var (next, prev) = neighbours[i];
        neighbours.RemoveAt(i);

        if (map.TileAt(next.Item1, next.Item2).Type == TileType.DungeonFloor)
          continue;

        map.SetTile(next.Item1, next.Item2, TileFactory.Get(TileType.DungeonFloor));
        ConnectNeighbours(map, prev.Item1, prev.Item2, next.Item1, next.Item2);

        neighbours.AddRange(NextNeighbours(map, next.Item1, next.Item2)
                                .Select(n => (n, (next.Item1, next.Item2))));
      }
    }

    return success;
  }

  static bool ValidDoor(Map map, int r, int c)
  {
    if (!map.InBounds(r, c))
      return false;

    if (map.InBounds(r - 1, c) && map.InBounds(r + 1, c)
            && map.TileAt(r - 1, c).Type == TileType.DungeonFloor
            && map.TileAt(r + 1, c).Type == TileType.DungeonFloor)
      return true;

    if (map.InBounds(r, c - 1) && map.InBounds(r, c + 1)
            && map.TileAt(r, c - 1).Type == TileType.DungeonFloor
            && map.TileAt(r, c + 1).Type == TileType.DungeonFloor)
      return true;

    return false;
  }

  static int RegionForSq(Dictionary<int, HashSet<(int, int)>> regions, int row, int col)
  {
    foreach (var k in regions.Keys)
    {
      if (regions[k].Contains((row, col)))
        return k;
    }

    return -1;
  }

  static HashSet<int> AdjoiningRegions(Dictionary<int, HashSet<(int, int)>> regions, (int, int) con)
  {
    var adjoining = new HashSet<int>();
    int above = RegionForSq(regions, con.Item1 - 1, con.Item2);
    int below = RegionForSq(regions, con.Item1 + 1, con.Item2);
    int left = RegionForSq(regions, con.Item1, con.Item2 - 1);
    int right = RegionForSq(regions, con.Item1, con.Item2 + 1);

    if (above != -1 && below != -1)
    {
      adjoining.Add(above);
      adjoining.Add(below);
    }
    if (left != -1 && right != -1)
    {
      adjoining.Add(left);
      adjoining.Add(right);
    }

    return adjoining;
  }

  // Pick a wall along the perimeter of the room that will join the room to a hallway.
  // If a room is surround by walls that are 2 squares deep (or more likely a room in
  // the corner of the map that is surrounded on 2 sides with walls that are 2 sqs deep)
  // just reject it. This happens so rarely in map generation that I didn't think it was
  // worth coding around it by drawing a short tunnel or such. I generated many hundred
  // maps before the situation came up.
  void AddDoorToRectRoom(Map map, Room room)
  {
    var candidates = room.CalcDoorCandidates()
                         .Where(d => ValidDoor(map, d.Item1, d.Item2))
                         .ToList();

    if (candidates.Count == 0)
    {
      throw new InvalidRoomException();
    }

    var door = candidates[_rng.Next(candidates.Count)];
    map.SetTile(door, PickDoor(_rng));    
  }

  // Circular rooms end up with wall at least two squares away from
  // any hallways, so they are slightly more complicated to join up
  void ConnectCircularRoom(Map map, Room room)
  {
    int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
    foreach (var sq in room.Sqs)
    {
      if (sq.Item1 < minRow) minRow = sq.Item1;
      if (sq.Item2 < minCol) minCol = sq.Item2;
      if (sq.Item1 > maxRow) maxRow = sq.Item1;
      if (sq.Item2 > maxCol) maxCol = sq.Item2;
    }

    bool done = false;
    do
    {
      (int, int) delta;
      int row;
      int col;

      // Pick direction to try drawing a line in
      switch (_rng.Next(4))
      {
        case 0: // northward
          row = minRow;
          col = _rng.Next(minCol + 1, maxCol);
          delta = (-1, 0);
          break;
        case 1: // southward
          row = maxRow;
          col = _rng.Next(minCol + 1, maxCol);
          delta = (1, 0);
          break;
        case 2: // westward
          row = _rng.Next(minRow + 1, maxRow);
          col = minCol;
          delta = (0, -1);
          break;
        default: // eastward
          row = _rng.Next(minRow + 1, maxRow);
          col = maxCol;
          delta = (0, 1);
          break;
      }

      // Because of the irregular shape, sometimes we try
      // to start the tunnel on a wall square, which we don't want
      if (map.TileAt(row, col).Type == TileType.DungeonWall)
        continue;

      bool success = false;
      var tunnel = new List<(int, int)>();
      int tunnelR = row + delta.Item1;
      int tunnelC = col + delta.Item2;
      while (map.InBounds(tunnelR, tunnelC) && map.TileAt(tunnelR, tunnelC).Type == TileType.DungeonWall)
      {
        tunnel.Add((tunnelR, tunnelC));
        tunnelR += delta.Item1;
        tunnelC += delta.Item2;

        if (map.InBounds(tunnelR, tunnelC) && map.TileAt(tunnelR, tunnelC).Type == TileType.DungeonFloor)
        {
          success = true;
          break;
        }
        else if (tunnel.Count > 4)
        {
          // We don't want an absurdly long tunnel; there should be a better candidate
          // (theoretical infinite loop risk? I've never seen a circular room generate that
          // is surrounded on all sides by walls 4 deep)
          success = false;
          break;
        }
      }

      if (success)
      {
        foreach (var sq in tunnel)
          map.SetTile(sq, TileFactory.Get(TileType.DungeonFloor));

        // We can also make some of tunnel a door.
        if (tunnel.Count == 2 && _rng.NextDouble() < 0.75)
        {
          map.SetTile(tunnel[0], PickDoor(_rng));
        }
        else if (tunnel.Count > 2)
        {
          map.SetTile(tunnel[0], PickDoor(_rng));
          map.SetTile(tunnel.Last(), PickDoor(_rng));
        }

        done = true;
      }
    }
    while (!done);
  }

  void RepairIsolatedRegion(Map map, Dictionary<int, HashSet<(int, int)>> regions)
  {
    // first find the smallest region
    int smallest = -1;
    int count = int.MaxValue;

    foreach (var k in regions.Keys)
    {
      if (regions[k].Count < count)
      {
        count = regions[k].Count;
        smallest = k;
      }
    }

    bool done = false;
    do
    {
      bool success = false;
      var sq = regions[smallest].ElementAt(_rng.Next(count));
      var startRegion = RegionForSq(regions, sq.Item1, sq.Item2);
      var dir = Util.Adj4[_rng.Next(4)];
      var tunnel = new List<(int, int)>();
      while (map.InBounds(sq))
      {
        tunnel.Add(sq);
        if (map.TileAt(sq).Type == TileType.DungeonFloor && RegionForSq(regions, sq.Item1, sq.Item2) != startRegion)
        {
          success = true;
          break;
        }
        sq = (sq.Item1 + dir.Item1, sq.Item2 + dir.Item2);
      }

      if (success)
      {
        foreach (var t in tunnel)
          map.SetTile(t, TileFactory.Get(TileType.DungeonFloor));
        done = true;
      }
    }
    while (!done);
  }

  void ConnectRegions(Map map, List<Room> rooms)
  {    
    // For rectangular rooms, each perimeter sqpare should be next to a hallway
    // (rounded rooms are more complicated)
    // So start by picking a random perimeter sq to turn into a door from each room
    foreach (var room in rooms)
    {
      if (room.Shape == RoomShapes.Rect)
        AddDoorToRectRoom(map, room);
      else
        ConnectCircularRoom(map, room);
    }

    var regionFinder = new RegionFinder(new DungeonPassable());
    var regions = regionFinder.Find(map, true, TileType.DungeonWall);

    if (regions.Count > 1)
    {
      List<(int, int)> connectors;
      var perimeters = new HashSet<(int, int)>();
      foreach (var room in rooms)
        perimeters = perimeters.Union(room.Permieter).ToHashSet();

      bool done = false;
      do
      {
        connectors = new List<(int, int)>();
        // Find the walls that are adjacent to different regions
        for (int r = 1; r < map.Height - 1; r++)
        {
          for (int c = 1; c < map.Width - 1; c++)
          {
            if (map.TileAt(r, c).Type == TileType.DungeonWall && AdjoiningRegions(regions, (r, c)).Count >= 2)
            {
              connectors.Add((r, c));
            }
          }
        }

        // Okay, sometimes a region is generated whose walls are all at least two
        // thick. Almost always this is two adjacent round rooms (Round rooms! Why
        // did I decide to implement round rooms??) who got connected which makes my 
        // code to find connectors between regions fail. So, shotgun approach: I'm going
        // to pick a square in the smaller region and erase walls until I reach a floor in
        // another region. 
        if (connectors.Count == 0)
        {
          RepairIsolatedRegion(map, regions);
          regions = regionFinder.Find(map, true, TileType.DungeonWall);
          if (regions.Count == 1)
            return;
        }
        else
        {
          done = true;
        }
      }
      while (!done);

      done = false;
      do
      {
        var con = connectors[_rng.Next(connectors.Count)];

        // I can check to see if the connector is on the perimeter of a room
        // and make it a door instead
        if (perimeters.Contains(con) && _rng.NextDouble() < 0.8)
          map.SetTile(con, PickDoor(_rng));
        else
          map.SetTile(con, TileFactory.Get(TileType.DungeonFloor));

        var adjoiningRegions = AdjoiningRegions(regions, con);
        var remaining = new List<(int, int)>();
        // Remove the connectors we don't need
        foreach (var other in connectors)
        {
          var otherAdjoining = AdjoiningRegions(regions, other);
          if (adjoiningRegions.Intersect(otherAdjoining).Count() > 1)
          {
            // small chance of removing other connector to create the occasional loop
            // in the map
            if (_rng.NextDouble() < 0.20)
              map.SetTile(other, TileFactory.Get(TileType.DungeonFloor));
          }
          else
          {
            remaining.Add(other);
          }
        }

        if (remaining.Count == 0)
          done = true;
        else
          connectors = remaining;
      }
      while (!done);
    }
  }

  // Clean up artifacts that my generator sometimes to creates.
  // So far the one I see is useless doors, like doors sitting in
  // open space. (I blame the round rooms which have proven very finicky)
  // We'll just turn them into open floors
  static void TidyUp(Map map)
  {
    static bool Blocked(TileType t) => t switch 
    {
      TileType.DungeonWall => true,
      TileType.PermWall => true,
      TileType.ClosedDoor => true,
      TileType.LockedDoor => true,
      _ => false
    };

    for (int r = 1; r < map.Height - 1; r++)
    {
      for (int c = 1; c < map.Width - 1; c++)
      {
        if (IsDoor(map.TileAt(r, c)))
        {
          var north = map.TileAt(r - 1, c).Type;
          var south = map.TileAt(r + 1, c).Type;
          var east = map.TileAt(r, c + 1).Type;
          var west = map.TileAt(r, c - 1).Type;

          // I'm too dumb/tired to figure out the much-cleaner inverse logic of this right now
          if (Blocked(north) && Blocked(south))
            continue;
          else if (Blocked(east) && Blocked(west))
            continue;
          else
            map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
        }
      }
    }
  }

  // Get rid of all (most) of the tunnels that go nowhere
  static void FillInDeadEnds(Map map)
  {
    bool done;

    do
    {
      done = true;
      for (int r = 0; r < map.Height; r++)
      {
        for (int c = 0; c < map.Width; c++)
        {
          if (map.TileAt(r, c).Type == TileType.DungeonWall)
            continue;

          // If there's only one exit, it's a dead end
          int exits = 0;
          foreach (var adj in Util.Adj4)
          {
            int nr = r + adj.Item1;
            int nc = c + adj.Item2;
            if (map.InBounds(nr, nc) && map.TileAt(nr, nc).Type != TileType.DungeonWall)
              ++exits;
          }

          if (exits == 1)
          {
            map.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));
            done = false;
          }
        }
      }
    }
    while (!done);
  }

  static (int, int) DeltaFromDir(Dir dir, Random rng) => dir switch
  {
    Dir.South => (rng.Next(3, 6), rng.Next(-2, 3)),
    Dir.North => (rng.Next(-5, -4), rng.Next(-2, 3)),
    Dir.East => (rng.Next(-2, 3), rng.Next(3, 6)),
    _ => (rng.Next(-2, 3), rng.Next(-5, -4))
  };

  static Dir ChangeRiverDir(Dir dir, Dir origDir, Random rng)
  {
    List<Dir> dirs = dir switch
    {
      Dir.North => [Dir.North, Dir.North, Dir.North, Dir.East, Dir.West],
      Dir.South => [Dir.South, Dir.South, Dir.South, Dir.East, Dir.West],
      Dir.West => [Dir.West, Dir.West, Dir.West, Dir.North, Dir.South],
      _ => [Dir.East, Dir.East, Dir.East, Dir.North, Dir.South]
    };

    dirs = dirs.Where(d => d != origDir).ToList();

    return dirs[rng.Next(dirs.Count)];
  }

  // Draw a river on the map. River being a flow of some sort: water, lava,
  // or a chasm
  public static void AddRiver(Map map, int width, int height, TileType riverTile, Random rng)
  {
    // pick starting wall
    int roll = rng.Next(4);
    List<(int, int, Dir)> pts = [];

    Dir dir, origDir;
    int row, col;
    roll = 2;
    if (roll == 0)
    {
      // start on north wall
      row = 1;
      col = rng.Next(width / 3, width / 3 * 2);
      dir = Dir.South;
      origDir = Dir.North;
      pts.Add((row, col, Dir.South));
    }
    else if (roll == 1)
    {
      // start on west wall
      col = 1;
      row = rng.Next(height / 3, height / 3 * 2);
      dir = Dir.East;
      origDir = Dir.West;
      pts.Add((row, col, Dir.East));
    }
    else if (roll == 2)
    {
      // start on east wall
      col = width - 2;
      row = rng.Next(height / 3, height / 3 * 2);
      dir = Dir.West;
      origDir = Dir.East;
      pts.Add((row, col, Dir.West));
    }
    else
    {
      // start on south wall
      row = height - 2;
      col = rng.Next(width / 3, width / 3 * 2);
      dir = Dir.North;
      origDir = Dir.South;
      pts.Add((row, col, Dir.North));
    }

    bool done = false;
    while (!done)
    {
      var nd = DeltaFromDir(dir, rng);
      row += nd.Item1;
      col += nd.Item2;
      if (row < 1)
      {
        row = 1;
        done = true;
      }
      if (row >= height - 1)
      {
        row = height - 2;
        done = true;
      }
      if (col < 1)
      {
        col = 1;
        done = true;
      }
      if (col >= width - 1)
      {
        col = width - 2;
        done = true;
      }
      pts.Add((row, col, dir));
      dir = ChangeRiverDir(dir, origDir, rng);
    }

    DrawRiver(map, pts, riverTile, rng);

    AddBridges(map, height, width, riverTile, rng);
  }

  static void AddBridges(Map map, int height, int width, TileType riverTile, Random rng)
  {
    var regionFinder = new RegionFinder(new DungeonPassable());
    var regions = regionFinder.Find(map, false, TileType.Unknown);
    int largest = 0;
    int count = 0;
    foreach (var k in regions.Keys)
    {
      if (regions[k].Count > count)
      {
        largest = k;
        count = regions[k].Count;
      }
    }

    var djmap = new DjikstraMap(map, height, width);
    var passable = new Dictionary<TileType, int>
        {
            { TileType.DungeonFloor, 1 },
            { TileType.ClosedDoor, 1 },
            { TileType.LockedDoor, 1 },
            { TileType.OpenDoor, 1 },
            { TileType.WoodBridge, 1 },
            { riverTile, 1 }
        };

    foreach (var k in regions.Keys)
    {
      if (k != largest && regions[k].Count >= 5)
      {
        // find the closest points between this region and the main/largest region
        var nearby = ClosestPts(regions[largest], regions[k]);
        var pair = nearby[rng.Next(nearby.Count)];
        djmap.Generate(passable, pair.Item2, 70);

        var start = pair.Item1;
        var path = djmap.ShortestPath(start.Item1, start.Item2);
        foreach (var pt in path)
        {
          if (map.IsTile(pt, riverTile))
            map.SetTile(pt, TileFactory.Get(TileType.WoodBridge));
        }
      }
    }
  }

  static List<((int, int), (int, int))> ClosestPts(HashSet<(int, int)> lg, HashSet<(int, int)> sm)
  {
    List<((int, int), (int, int), int)> distances = [];
    foreach (var pt in sm)
    {
      var lsm = new Loc(0, 0, pt.Item1, pt.Item2);
      foreach (var pt2 in lg)
      {
        var llg = new Loc(0, 0, pt2.Item1, pt2.Item2);
        int d = Util.Distance(lsm, llg);
        if (distances.Count < 5)
          distances.Add((pt, pt2, d));
        else
        {
          for (int j = 0; j < 5; j++)
          {
            if (d < distances[j].Item3)
            {
              distances[j] = (pt, pt2, d);
              break;
            }
          }
        }
      }
    }

    return distances.Select(d => (d.Item1, d.Item2)).ToList();
  }

  static void WidenRiver(Map map, int row, int col, TileType riverTile, Random rng)
  {
    var above = (row - 1, col);
    var below = (row + 1, col);
    var left = (row, col - 1);
    var right = (row, col + 1);

    if (!map.IsTile(above, riverTile) && !map.IsTile(below, riverTile))
    {
      if (map.InBounds(above))
      {
        map.SetTile(above, TileFactory.Get(riverTile));
        above = (above.Item1 - 1, above.Item2);
        if (map.InBounds(above) && rng.NextDouble() < 0.33)
          map.SetTile(above, TileFactory.Get(riverTile));
      }
      if (map.InBounds(below))
      {
        map.SetTile(below, TileFactory.Get(riverTile));
        below = (below.Item1 + 1, below.Item2);
        if (map.InBounds(below) && rng.NextDouble() < 0.33)
          map.SetTile(below, TileFactory.Get(riverTile));
      }
    }
    if (!map.IsTile(left, riverTile) && !map.IsTile(right, riverTile))
    {
      if (map.InBounds(left))
      {
        map.SetTile(left, TileFactory.Get(riverTile));
        left = (left.Item1, left.Item2 - 1);
        if (map.InBounds(left) && rng.NextDouble() < 0.33)
          map.SetTile(left, TileFactory.Get(riverTile));
      }
      if (map.InBounds(right))
      {
        map.SetTile(right, TileFactory.Get(riverTile));
        right = (right.Item1, right.Item2 + 1);
        if (map.InBounds(right) && rng.NextDouble() < 0.33)
          map.SetTile(right, TileFactory.Get(riverTile));
      }
    }
  }

  static void DrawRiver(Map map, List<(int, int, Dir)> pts, TileType riverTile, Random rng)
  {
    for (int j = 0; j < pts.Count - 1; j++)
    {
      var river = Util.Bresenham(pts[j].Item1, pts[j].Item2, pts[j + 1].Item1, pts[j + 1].Item2);
      foreach (var w in river)
      {
        map.SetTile(w, TileFactory.Get(riverTile));
        WidenRiver(map, w.Item1, w.Item2, riverTile, rng);
      }
    }
  }

  // If there are rooms who share a border, connect some of them to hopefully
  // make the dungeon map a little more loopy
  static void ConnectRooms(Map map, List<Room> rooms, Random rng)
  {
    static bool ConnectionCandidate(Map map, int row, int col)
    {
      int nr = row - 1, sr = row + 1, ec = col + 1, wc = col - 1;
      if (!map.InBounds(nr, col) || !map.InBounds(sr, col) || !map.InBounds(row, ec) || !map.InBounds(row, wc))
        return false;

      TileType north = map.TileAt(nr, col).Type;
      TileType south = map.TileAt(sr, col).Type;
      TileType east = map.TileAt(row, ec).Type;
      TileType west = map.TileAt(row, wc).Type;

      if (north == TileType.DungeonFloor && south == TileType.DungeonFloor && east == TileType.DungeonWall && west == TileType.DungeonWall)
        return true;
      if (north == TileType.DungeonWall && south == TileType.DungeonWall && east == TileType.DungeonFloor && west == TileType.DungeonFloor)
        return true;

      return false;
    }

    HashSet<(int, int)> shared = [];

    for (int j = 0; j < rooms.Count; j++)
    {
      for (int k = j + 1; k < rooms.Count; k++)
      {
        if (j == k)
          continue;
        var sqs = rooms[j].Permieter.Intersect(rooms[k].Permieter).ToHashSet();

        if (sqs.Count == 0)
          continue;

        // If there's already connection between rooms, skip this wall
        bool reject = false;
        foreach (var sq in sqs)
        {
          if (!map.InBounds(sq))
            continue;
          if (map.TileAt(sq).Type == TileType.DungeonFloor || IsDoor(map.TileAt(sq)))
          {
            reject = true;
            break;
          }            
        }

        if (reject)
          continue;

        var candidates = sqs.Where(s => ConnectionCandidate(map, s.Item1, s.Item2)).ToList();
        if (candidates.Count > 0)
        {
          var passage = candidates[rng.Next(candidates.Count)];
          Tile tile = rng.Next(5) == 0 ? TileFactory.Get(TileType.DungeonFloor) : PickDoor(rng);        
          map.SetTile(passage, tile);
        }        
      }
    }
  }

  // Okay, so we still have two separate regions after other tweaks so I'm just 
  // going to draw a straight tunnel between the two regions
  static void BruteForceJoinRegions(Map map, HashSet<(int, int)> a, HashSet<(int, int)> b, Random rng)
  {    
    List<(int, int)> smaller, larger;
    if (a.Count > b.Count)
    {
      larger = a.Where(s => map.TileAt(s).Type == TileType.DungeonFloor).ToList();
      smaller = b.Where(s => map.TileAt(s).Type == TileType.DungeonFloor).ToList();
    }
    else
    {
      larger = b.Where(s => map.TileAt(s).Type == TileType.DungeonFloor).ToList();
      smaller = a.Where(s => map.TileAt(s).Type == TileType.DungeonFloor).ToList();
    }

    bool done = false;
    while (!done)
    {
      int i = rng.Next(smaller.Count);
      var (startR, startC) = smaller[i];
      smaller.RemoveAt(i);

      List<(int, int)> endSqs = [];
      foreach (var sq in larger)
      {
        if (Util.Distance(sq.Item1, sq.Item2, startR, startC) < 6 && (sq.Item1 == startR || sq.Item2 == startC))
          endSqs.Add(sq);
      }

      if (endSqs.Count > 0)
      {
        var (endR, endC) = endSqs[rng.Next(endSqs.Count)];
        int deltaR;
        if (startR == endR)
          deltaR = 0;
        else if (startR < endR)
          deltaR = 1;
        else
          deltaR = -1;
        int deltaC;
        if (startC == endC)
          deltaC = 0;
        else if (startC < endC)
          deltaC = 1;
        else
          deltaC = -1;

        int row = startR, col = startC;
        while (row != endR || col != endC)
        {
          map.SetTile(row, col, TileFactory.Get(TileType.DungeonFloor));
          row += deltaR;
          col += deltaC;
        }
        
        return;
      }
      else
      {
        done = false;
      }
    }

    throw new InvalidRoomException();
  }

  public Map DrawLevel(int width, int height)
  {
    var map = new Map(width, height);
    List<Room> rooms;

    while (true)
    {
      for (int j = 0; j < width * height; j++)
        map.Tiles[j] = TileFactory.Get(TileType.DungeonWall);

      try
      {
        rooms = AddRooms(map);
        // Draw in the room perimeters
        foreach (var room in rooms)
        {
          foreach (var sq in room.Permieter)
          {
            if (map.InBounds(sq.Item1, sq.Item2))
              map.SetTile(sq.Item1, sq.Item2, TileFactory.Get(TileType.DungeonWall));
          }
        }

        bool mazing = true;
        while (mazing)
        {
          mazing = CarveMaze(map);
        }

        ConnectRegions(map, rooms);
        FillInDeadEnds(map);
        ConnectRooms(map, rooms, _rng);

        var regionFinder = new RegionFinder(new DungeonPassable());
        var regions = regionFinder.Find(map, true, TileType.DungeonWall);
        if (regions.Count == 2)
        {
          BruteForceJoinRegions(map, regions[0], regions[1], rng);
        }
        else if (regions.Count > 2)
        {
          // If there are still 3 or more disjoint regions, assume the map
          // is hopeless and start over
          throw new InvalidRoomException();
        }
      }
      catch (InvalidRoomException)
      {
        map = new Map(width, height);
        continue;  
      }

      break;
    }
    
    // We want to surround the level with permanent walls
    var finalMap = new Map(width + 2, height + 2, TileType.PermWall);
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        finalMap.SetTile(r + 1, c + 1, map.TileAt(r, c));
      }
    }

    TidyUp(finalMap);

    return finalMap;
  }
}

enum ScanDirs { RightDown, DownRight, LeftDown, DownLeft, UpRight, RightUp, LeftUp, UpLeft };
enum RoomShapes { Rect, Round }

class Room
{
  public RoomShapes Shape { get; set; }
  public HashSet<(int, int)> Sqs { get; set; }
  public HashSet<(int, int)> Permieter { get; set; }

  public Room(IEnumerable<(int, int)> sqs, RoomShapes shape)
  {
    Shape = shape;
    Sqs = new HashSet<(int, int)>(sqs);

    int minRow = int.MaxValue, maxRow = 0;
    int minCol = int.MaxValue, maxCol = 0;
    foreach (var sq in sqs)
    {
      if (sq.Item1 < minRow)
        minRow = sq.Item1;
      if (sq.Item1 > maxRow)
        maxRow = sq.Item1;
      if (sq.Item2 > maxCol)
        maxCol = sq.Item2;
      if (sq.Item2 < minCol)
        minCol = sq.Item2;
    }

    minRow = minRow == 0 ? minRow : (minRow - 1);
    maxRow += 1;
    minCol = minCol == 0 ? minCol : (minCol - 1);
    maxCol += 1;

    Permieter = [];
    for (int r = minRow; r <= maxRow; r++)
    {
      for (int c = minCol; c <= maxCol; c++)
      {
        if (!Sqs.Contains((r, c)))
          Permieter.Add((r, c));
      }
    }
  }

  public bool Overlaps(Room other)
  {
    return Permieter.Intersect(other.Permieter).Any() ||
        Permieter.Intersect(other.Sqs).Any();
  }

  public List<(int, int)> CalcDoorCandidates()
  {
    List<(int, int)> candidates = [];

    foreach (var (dr, dc) in Permieter)
    {
      foreach (var n in Util.Adj4)
      {
        int nr = dr + n.Item1;
        int nc = dc + n.Item2;
        if (nr >= 0 && nc >- 0 && Sqs.Contains((nr, nc)))
        {
          candidates.Add((dr, dc));
        }
      }
    }

    return candidates;
  }
}
