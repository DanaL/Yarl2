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
  static char TileToChar(Tile tile) => tile.Type switch
  {
    TileType.PermWall => '#',
    TileType.DungeonWall => '#',
    TileType.DungeonFloor or TileType.Sand => '.',
    TileType.ClosedDoor or TileType.LockedDoor => '+',
    TileType.SecretDoor => '+',
    TileType.VaultDoor => '|',
    TileType.OpenPortcullis => '|',
    TileType.Portcullis => '|',
    TileType.Mountain or TileType.SnowPeak => '^',
    TileType.Grass => ',',
    TileType.GreenTree => 'T',
    TileType.RedTree => 'T',
    TileType.OrangeTree => 'T',
    TileType.YellowTree => 'T',
    TileType.DeepWater => '~',
    TileType.WoodBridge => '=',
    TileType.Upstairs => '<',
    TileType.Downstairs => '>',
    _ => ' '
  };

  public static void FindPotential(Map map)
  {
    map.Dump();
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

      for (int r = lowR - 1; r <= highR + 1; r++)
      {
        for (int c = lowC - 1; c <= highC + 1; c++)
        {          
          Console.Write(TileToChar(map.TileAt(r, c)));
        }
        Console.WriteLine();
      }
      foreach (var e in exits)
      {
        Console.Write(e);
      }
      Console.WriteLine();
      Console.WriteLine();
    }

    foreach (RoomInfo room in rooms)
    {
      FindRoutesFromRoom(room, map, rooms);
    }
  }

  static void FollowPathFromExit(int r, int c, Dir dir, Map map, List<(int, int)> path, List<RoomInfo> rooms)
  {
    while (true)
    {
      var (nr, nc) = Move(r, c, dir);
      Tile tile = map.TileAt(nr, nc);
      if (tile.Type == TileType.PermWall || tile.Type == TileType.DungeonWall)
      {
        List<Dir> turns = FindTurns(r, c, dir, map);
        foreach (Dir nd in turns)
          FollowPathFromExit(r, c, nd, map, [.. path], rooms);
        return;
      }
      else if (SqrInRoom(nr, nc, rooms))
      {
        Console.WriteLine("End of route!");
        return;
      }
      else
      {
        path.Add((nr, nc));
      }

      (r, c) = (nr, nc);
    }

    Console.WriteLine(path);

    static (int, int) Move(int r, int c, Dir dir) => dir switch
    {    
      Dir.North => (r - 1, c),
      Dir.South => (r + 1, c),
      Dir.East => (r, c + 1),
      Dir.West => (r, c - 1),
      _ => (r, c)
    };

    static List<Dir> FindTurns(int r, int c, Dir dir, Map map)
    {
      List<Dir> turns = [];

      switch (dir)
      {
        case Dir.North:
        case Dir.South:
          if (Passable(r, c - 1, map))
            turns.Add(Dir.West);
          else if (Passable(r, c + 1, map))
            turns.Add(Dir.East);
          break;
        case Dir.East:
        case Dir.West:
          if (Passable(r - 1, c, map))
            turns.Add(Dir.North);
          else if (Passable(r + 1, c, map))
            turns.Add(Dir.South);
          break;
      }

      return turns;
    }

    static bool Passable(int r, int c, Map map)
    {
      Tile tile = map.TileAt(r, c);
      if (tile.PassableByFlight())
        return true;

      switch (tile.Type)
      {
        case TileType.ClosedDoor:
        case TileType.LockedDoor:
        case TileType.SecretDoor:
        case TileType.VaultDoor:
        case TileType.Portcullis:
          return true;
      }

      return false;
    }

    static bool SqrInRoom(int r, int c, List<RoomInfo> rooms)
    {
      (int, int) sq = (r, c);

      foreach (RoomInfo room in rooms)
      {
        if (room.Sqs.Contains(sq))
          return true;
      }

      return false;
    }
  }
  
  static void FindRoutesFromRoom(RoomInfo room, Map map, List<RoomInfo> rooms)
  {
    foreach ((int, int, Dir) exit in room.Exits)
    {
      FollowPathFromExit(exit.Item1, exit.Item2, exit.Item3, map, [], rooms);
    }
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

class RoomInfo
{
  public HashSet<(int, int)> Sqs { get; set; } = [];
  public HashSet<(int, int, Dir)> Exits { get; set; } = [];
}