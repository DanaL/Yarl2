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


class Vaults
{
  static (int, int) FindVaultTrigger(Map map, int row, int col, int height, int width, HashSet<(int, int)> vault, Random rng)
  {
    int startRow = int.Max(row - 10, 1);
    int endRow = int.Min(row + 10, height);
    int startCol = int.Max(col - 10, 1);
    int endCol = int.Min(col + 10, width);
    int triggerRow = -1, triggerCol = -1;

    List<(int, int)> candidates = [];
    for (int r = startRow; r <= endRow; r++)
    {
      for (int c = startCol; c <= endCol; c++)
      {
        var sq = (r, c);
        if (!vault.Contains(sq) && map.TileAt(sq).Type == TileType.DungeonFloor)
        {
          candidates.Add(sq);
        }
      }
    }

    if (candidates.Count > 0)
    {
      (triggerRow, triggerCol) = candidates[rng.Next(candidates.Count)];
    }

    return (triggerRow, triggerCol);
  }

  // How many times can I implement flood fill in one project?
  static HashSet<(int, int)> MarkRegion(Map map, int startRow, int startCol)
  {
    HashSet<(int, int)> region = [(startRow, startCol)];
    Queue<(int, int)> q = [];
    q.Enqueue((startRow, startCol));

    while (q.Count > 0)
    {
      var (currRow, currCol) = q.Dequeue();
      region.Add((currRow, currCol));

      foreach (var adj in Util.Adj8Sqs(currRow, currCol))
      {
        if (!map.InBounds(adj))
          continue;

        Tile tile = map.TileAt(adj);
        bool open;
        switch (tile.Type)
        {
          case TileType.DungeonFloor:
          case TileType.DeepWater:
          case TileType.WoodBridge:
          case TileType.Landmark:
          case TileType.Upstairs:
          case TileType.Downstairs:
          case TileType.Chasm:
            open = true;
            break;
          default:
            open = false;
            break;
        }

        if (open && !region.Contains(adj))
        {
          region.Add(adj);
          q.Enqueue(adj);
        }
      }
    }

    return region;
  }

  public static void FindPotentialVaults(Map map, int height, int width, Random rng, int dungeonID, int levelNum)
  {
    Dictionary<(int, int), int> areas = [];
    Dictionary<int, HashSet<(int, int)>> rooms = [];

    int areaID = 0;
    for (int r = 1; r < height - 1; r++)
    {
      for (int c = 1; c < width - 1; c++)
      {
        if (!map.InBounds(r, c))
          continue;
        if (map.TileAt(r, c).Type == TileType.DungeonFloor && !areas.ContainsKey((r, c)))
        {
          var region = MarkRegion(map, r, c);
          rooms.Add(areaID, region);
          foreach (var sq in region)
          {
            areas.Add(sq, areaID);
          }
          areaID++;
        }
      }
    }

    foreach (int roomID in rooms.Keys)
    {
      if (rooms[roomID].Count > 75)
        continue;

      // A potential vault will have only one door adj to its squares
      int doorCount = 0;
      int doorRow = -1, doorCol = -1;
      foreach (var sq in rooms[roomID])
      {
        foreach (var adj in Util.Adj4Sqs(sq.Item1, sq.Item2))
        {
          if (!map.InBounds(adj))
            continue;

          TileType type = map.TileAt(adj).Type;

          if (type == TileType.ClosedDoor || type == TileType.LockedDoor)
          {
            (doorRow, doorCol) = adj;
            ++doorCount;
          }

          // Reject rooms containing the upstairs. (We don't want the player to
          // arrive in a locked vault where they can't access the method of 
          // opening it
          if (type == TileType.Upstairs)
          {
            doorCount = int.MaxValue;
            break;
          }
        }
        if (doorCount > 1)
          break;
      }

      if (doorCount == 1) // && rng.NextDouble() < 0.25)
      {
        SetVaultDoor(map, dungeonID, levelNum, doorRow, doorCol, rooms[roomID], rng);
        //SetPortcullisVault(map, height, width, dungeonID, levelNum, rooms[roomID], doorRow, doorCol, rng);
      }
    }

    MapUtils.Dump(map, areas);
  }

  static void SetVaultDoor(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> room, Random rng)
  {
    Metals material;
    int roll = rng.Next(10);
    if (roll < 5)
      material = Metals.Iron;
    else if (roll < 9)
      material = Metals.Bronze;
    else
      material = Metals.Mithril;
    VaultDoor door = new(false, material);
    map.SetTile(doorRow, doorCol, door);
  }

  static void SetPortcullisVault(Map map, int h, int w, int dungeonID, int level, HashSet<(int, int)> room, int doorRow, int doorCol, Random rng)
  {
    int triggerRow, triggerCol;
    (triggerRow, triggerCol) = FindVaultTrigger(map, doorRow, doorCol, h, w, room, rng);
    if (triggerRow != -1 && triggerCol != -1)
    {
      Console.WriteLine($"Vault!!");
      map.SetTile(doorRow, doorCol, new Portcullis(false));
      map.SetTile(triggerRow, triggerCol, new GateTrigger(new Loc(dungeonID, level, doorRow, doorCol)));

      // need to actually put stuff in the vaults
    }
  }

  
}
