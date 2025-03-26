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
    List<HashSet<(int, int)>> rooms = [.. map.FindRooms()
                                             .Select(r => new HashSet<(int, int)>(r))];

    foreach (var room in rooms)
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
