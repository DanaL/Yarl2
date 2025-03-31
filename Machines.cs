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
  }

  static void FollowPathFromExit(int r, int c, Dir dir, Map map, List<RoomInfo> rooms)
  {
    List<(int, int)> path = [];

    while (true)
    {
      (r, c) = Move(r, c, dir);
      Tile tile = map.TileAt(r, c);
      if (tile.Type == TileType.PermWall || tile.Type == TileType.DungeonWall)
        break;
      path.Add((r, c));
    }

    static (int, int) Move(int r, int c, Dir dir) => dir switch
    {    
      Dir.North => (r - 1, c),
      Dir.South => (r + 1, c),
      Dir.East => (r, c + 1),
      Dir.West => (r, c - 1),
      _ => (r, c)
    };
  }
  
  static void FindRoutesFromRoom(RoomInfo room, Map map, List<RoomInfo> rooms)
  {
    foreach ((int, int, Dir) exit in room.Exits)
    {
      
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