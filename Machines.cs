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

// Code for more complicated level features. Borrowing the term 'Machine' from
// brogue, who iirc used it for the more complex mechanisms on a level

class LightPuzzleSetup
{
  const int MAX_CORNER_COUNT = 8;

  public static void Test()
  {
    string[] mapText = [ 
       "####################",
       "#####.....##########",
       "#####.....##########",       
       "#######.############",
       "#######.############",
       "#######.############",
       "#..##.....##########",
       "#..##.....####....##",
       "#..##.....####....##",
       "#.................##",
       "#..##.....##########",
       "####################"
    ];

    Map map = new(mapText[0].Length, mapText.Length);
    for (int r = 0; r < mapText.Length; r++)
    {
      for (int c = 0; c < mapText[r].Length; c++)
      {
        TileType type = mapText[r][c] == '#' ? TileType.DungeonWall : TileType.DungeonFloor;
        map.SetTile(r, c, TileFactory.Get(type));
      }
    }

    FindPotential(map);
  }

  public static void Create(Map map, List<PathInfo> paths, GameObjectDB objDb, int dungeonId, int level, Rng rng)
  {
    PathInfo path = paths[rng.Next(paths.Count)];
    
    Dir dir = rng.Next(4) switch
    {
      0 => Dir.North,
      1 => Dir.South,
      2 => Dir.East,
      _ => Dir.West
    };
    Item lamp = ItemFactory.Lamp(objDb, dir);

    (int, int) lampSq;
    if (path.Corners.Count > 0)
    {
      (int, int) firstCorner = path.Corners[0];
      List<(int, int)> lampOpts = [];
      foreach ((int, int) sq in path.StartRoom)
      {
        if (map.TileAt(sq).Type != TileType.DungeonFloor)
          continue;
        if (sq.Item1 == path.Start.Item1 && sq.Item1 == firstCorner.Item1)
          lampOpts.Add(sq);
        else if (sq.Item2 == path.Start.Item2 && sq.Item2 == firstCorner.Item2)
          lampOpts.Add(sq);
      }      
      lampSq = lampOpts[rng.Next(lampOpts.Count)];                                                
    }
    else
    {
      lampSq = path.Start;
    }
    Loc lampLoc = new(dungeonId, level, lampSq.Item1, lampSq.Item2);
    objDb.SetToLoc(lampLoc, lamp);

    Item target = ItemFactory.BeamTarget(objDb);
    (int, int) targetSq;
    if (path.Corners.Count > 0)
    {
      (int, int) lastCorner = path.Corners.Last();
      List<(int, int)> targetOpts = [];
      foreach ((int, int) sq in path.EndRoom)
      {
        if (map.TileAt(sq).Type != TileType.DungeonFloor)
          continue;
        if (sq.Item1 == path.End.Item1 && sq.Item1 == lastCorner.Item1)
          targetOpts.Add(sq);
        else if (sq.Item2 == path.End.Item2 && sq.Item2 == lastCorner.Item2)
          targetOpts.Add(sq);
      }      
      targetSq = targetOpts[rng.Next(targetOpts.Count)];                                                
    }
    else
    {
      targetSq = path.Start;
    }
    
    Loc targetLoc = new(dungeonId, level, targetSq.Item1, targetSq.Item2);
    objDb.SetToLoc(targetLoc, target);

    List<Loc> clearLocs = map.ClearFloors(dungeonId, level, objDb);
    for (int j = 0; j < path.Corners.Count; j++)
    {
      int mId = rng.Next(clearLocs.Count);
      Loc loc = clearLocs[mId];
      Item mirror = ItemFactory.Mirror(objDb, rng.Next(2) == 0);
      objDb.SetToLoc(loc, mirror);
      clearLocs.RemoveAt(mId);
    }
  }

  public static List<PathInfo> FindPotential(Map map)
  {    
    List<HashSet<(int, int)>> roomsTiles = [.. map.FindRooms()
                                                 .Select(r => new HashSet<(int, int)>(r))];
    List<RoomInfo> rooms = [];
    foreach (HashSet<(int, int)> room in roomsTiles)
    {
      int lowR = int.MaxValue, highR = 0, lowC = int.MaxValue, highC = 0;
      HashSet<(int, int, Dir)> exits = [];

      foreach (var (r, c) in room)
      {
        if (r < lowR) lowR = r;
        if (r > highR) highR = r;
        if (c < lowC) lowC = c;
        if (c > highC) highC = c;
      }

      foreach (var (r, c) in room)
      {
        if (r == lowR && IsExit(map.TileAt(r - 1, c)))
          exits.Add((r - 1, c, Dir.North));
        else if (r == highR && IsExit(map.TileAt(r + 1, c)))
          exits.Add((r + 1, c, Dir.South));
        else if (c == lowC && IsExit(map.TileAt(r, c - 1)))
          exits.Add((r, c - 1, Dir.West));
        else if (c == highC && IsExit(map.TileAt(r, c + 1)))
          exits.Add((r, c + 1, Dir.East));        
      }

      rooms.Add(new() { Sqs = room, Exits = exits });     
    }

    List<PathInfo> paths = [];
    foreach (RoomInfo room in rooms)
    {
      var p = FindRoutesFromRoom(room, map, rooms);
      paths.AddRange(p);
    }

    return paths;
  }
  
  static List<PathInfo> FindRoutesFromRoom(RoomInfo room, Map map, List<RoomInfo> rooms)
  {
    List<PathInfo> allPaths = [];
    
    foreach ((int, int, Dir) exit in room.Exits)
    {
      HashSet<(int, int)> visited = [];
      Queue<PathSearchNode> q = [];
      q.Enqueue(new PathSearchNode(new PathInfo((exit.Item1, exit.Item2)), exit.Item1, exit.Item2, exit.Item3));
      
      while (q.Count > 0)
      {
        PathSearchNode curr = q.Dequeue();
        if (curr.Path.Corners.Count > MAX_CORNER_COUNT)
        {
          continue;
        }

        HashSet<(int, int)> onPath = [];
        Dir dir = curr.Dir;
        int r = curr.Row;
        int c = curr.Col;

        while (true)
        {         
          var (nr, nc) = Move(r, c, dir);

          if (visited.Contains((nr, nc)))
            break;
            
          onPath.Add((nr, nc));

          Tile tile = map.TileAt(nr, nc);

          var (roomId, terminus) = SqrInRoom(nr, nc, rooms);
          if (terminus || (roomId > -1 && curr.Path.Corners.Count >= 2))
          {
            curr.Path.End = (nr, nc);
            curr.Path.StartRoom = [.. room.Sqs];
            curr.Path.EndRoom = [.. rooms[roomId].Sqs];
            allPaths.Add(curr.Path);
            visited.UnionWith(onPath);
            visited.UnionWith(rooms[roomId].Sqs);
            break;
          }

          if (tile.Type == TileType.PermWall || tile.Type == TileType.DungeonWall)
          {
            visited.UnionWith(onPath);
            break;
          }

          List<Dir> sidePassages = SidePassages(nr, nc, dir, map);
          foreach (Dir nd in sidePassages)
          {
            visited.UnionWith(onPath);
            PathInfo nextPath = PathInfo.Copy(curr.Path);            
            nextPath.Corners.Add((nr, nc));
            q.Enqueue(new PathSearchNode(nextPath, nr, nc, nd));
          }

          (r, c) = (nr, nc);
        }
      }
    }

    return allPaths;
  }

  static (int, int) Move(int r, int c, Dir dir) => dir switch
  {    
    Dir.North => (r - 1, c),
    Dir.South => (r + 1, c),
    Dir.East => (r, c + 1),
    Dir.West => (r, c - 1),
    _ => (r, c)
  };

  static List<Dir> SidePassages(int r, int c, Dir dir, Map map)
  {
    List<Dir> passages = [];

    switch (dir)
    {
      case Dir.North:
      case Dir.South:
        if (Passable(r, c - 1, map))
          passages.Add(Dir.West);
        if (Passable(r, c + 1, map))
          passages.Add(Dir.East);
        break;
      case Dir.East:
      case Dir.West:
        if (Passable(r - 1, c, map))
          passages.Add(Dir.North);
        if (Passable(r + 1, c, map))
          passages.Add(Dir.South);
        break;
    }

    return passages;
  }

  static bool Passable(int r, int c, Map map)
  {
    Tile tile = map.TileAt(r, c);
    if (tile.PassableByFlight())
      return true;

    return tile.Type switch
    {
      TileType.ClosedDoor or TileType.LockedDoor or 
      TileType.SecretDoor or TileType.VaultDoor or 
      TileType.Portcullis => true,
      _ => false,
    };
  }

  static (int, bool) SqrInRoom(int r, int c, List<RoomInfo> rooms)
  {
    (int, int) sq = (r, c);

    for (int j = 0; j < rooms.Count; j++)
    {
      if (rooms[j].Sqs.Contains(sq))
        return (j, rooms[j].Exits.Count < 2);
    }
 
    return (-1, false);
  }

  static bool IsExit(Tile tile) => tile.Type switch
  {
    TileType.ClosedDoor => true,
    TileType.LockedDoor => true,
    TileType.OpenDoor => true,
    TileType.SecretDoor => true,
    TileType.VaultDoor => true,
    TileType.Portcullis => true,
    TileType.OpenPortcullis => true,
    TileType.DungeonFloor => true,
    _ => false
  };
}

record PathSearchNode(PathInfo Path, int Row, int Col, Dir Dir);

record PathInfo((int, int) Start)
{
  public HashSet<(int, int)> StartRoom { get; set; } = [];
  public HashSet<(int, int)> EndRoom { get; set; } = [];
  public List<(int, int)> Corners { get; set; } = [];
  public (int, int) End;

  public static PathInfo Copy(PathInfo other)
  {
    PathInfo copy = new(other.Start)
    {
      StartRoom = [.. other.StartRoom],
      EndRoom = [.. other.EndRoom],
      Corners = [.. other.Corners],
      End = other.End
    };

    return copy;
  }
}

class RoomInfo
{
  public HashSet<(int, int)> Sqs { get; set; } = [];
  public HashSet<(int, int, Dir)> Exits { get; set; } = [];
}