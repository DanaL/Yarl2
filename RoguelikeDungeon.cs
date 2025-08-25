// Yarl2 - A roguelike computer RPG
// Written in 2025 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

namespace Yarl2;

record RLRoom(int Row, int Col, int Height, int Width, int Cell);

class RLLevelMaker
{
  const int HEIGHT = 30;
  const int WIDTH = 70;
  const int CELL_WIDTH = 17;
  const int CELL_HEIGHT = 9;

  public static Map MakeLevel(Rng rng)
  {
    // I'm initializing the map with sand and peppering some walls here and 
    // there throughout hoping that when it comes time to use pathfinding to
    // draw the halls, the alg will route around walls making for a bit more
    // twisty passages.
    Map map = new(WIDTH, HEIGHT, TileType.Sand);

    for (int c = 0; c < WIDTH; c++)
    {
      map.SetTile(0, c, TileFactory.Get(TileType.PermWall));
      map.SetTile(HEIGHT - 1, c, TileFactory.Get(TileType.PermWall));
    }

    for (int r = 1; r < HEIGHT - 1; r++)
    {
      map.SetTile(r, 0, TileFactory.Get(TileType.PermWall));
      map.SetTile(r, WIDTH - 1, TileFactory.Get(TileType.PermWall));
    }

    for (int i = 0; i < 125; i++)
    {
      int r = rng.Next(1, HEIGHT - 1);
      int c = rng.Next(1, WIDTH - 1);      
      map.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));      
    }

    int numOfRooms = rng.Next(8, 11);
    Dictionary<int, RLRoom> rooms = [];

    HashSet<int> usedCells = [];
    for (int i = 0; i < numOfRooms; i++)
    {
      RLRoom room = PlaceRoom(map, usedCells, rng);
      rooms.Add(room.Cell, room);
    }
   
    JoinRooms(rooms, map, usedCells, rng);

    for (int r = 0; r < HEIGHT; r++)
    {
      for (int c = 0; c < WIDTH; c++)
      {
        if (map.TileAt(r, c).Type == TileType.Sand)
          map.SetTile(r, c, TileFactory.Get(TileType.DungeonWall));
      }
    }

    return map;
  }

  static List<int> AdjRooms(int cell, HashSet<int> usedCells)
  {
    List<int> adj = [];

    int row = cell / 4;
    int col = cell % 4;

    foreach ((int dr, int dc) in Util.Adj8)
    {
      int adjR = row + dr;
      int adjC = col + dc;

      if (adjR < 0 || adjC < 0 || adjR > 2 || adjC > 3)
        continue;
      int i = adjR * 4 + adjC;
      if (usedCells.Contains(i))
        adj.Add(i);
    }

    return adj;
  }

  static readonly Dictionary<TileType, int> hallwayCosts = new() {
    { TileType.DungeonFloor, 1},
    { TileType.ClosedDoor, 0},
    { TileType.Sand, 1},
    { TileType.DungeonWall, 3}
  };
  static void DrawHallway(Dictionary<int, RLRoom> rooms, Map map, int startId, int endId)
  {
    RLRoom start = rooms[startId];
    RLRoom end = rooms[endId];
    int startR = start.Row + start.Height / 2, startC = start.Col + start.Width / 2;
    int endR = end.Row + end.Height / 2, endC = end.Col + end.Width / 2;

    Stack<Loc> path = AStar.FindPath(new GameObjectDB(), map, new(0, 0, startR, startC),
        new(0, 0, endR, endC), hallwayCosts, false);

    TileType prev = TileType.DungeonFloor;
    while (path.Count > 0)
    {
      Loc loc = path.Pop();
      TileType curr = map.TileAt(loc.Row, loc.Col).Type;

      // We've reached a door so we can stop, and we don't want to 
      // erase the door
      if (curr == TileType.ClosedDoor)
      {
        prev = curr;
        continue;
      }

      Tile toDraw;
      if (curr == TileType.DungeonWall && prev == TileType.Sand)
        toDraw = new Door(TileType.ClosedDoor, false);
      else if (curr == TileType.DungeonWall && prev == TileType.DungeonFloor)
        toDraw = new Door(TileType.ClosedDoor, false);
      else
        toDraw = TileFactory.Get(TileType.DungeonFloor);
      prev = curr;

      map.SetTile(loc.Row, loc.Col, toDraw);
    }
  }

  static void JoinRooms(Dictionary<int, RLRoom> rooms, Map map, HashSet<int> usedCells, Rng rng)
  {
    List<HashSet<int>> joinedRooms = [];

    JoinAdjacentRooms(rooms, map, joinedRooms, usedCells, rng);

    if (joinedRooms.Count > 1)
    {
      JoinDistantRooms(rooms, map, joinedRooms, usedCells, rng);
    }

    // Finally, try to join a few more rooms to hopefully add a looping path
    // or two in the map
    CreateLoops(rooms, map, usedCells, rng);
  }

  static void CreateLoops(Dictionary<int, RLRoom> rooms, Map map, HashSet<int> usedCells, Rng rng)
  {
    List<int> roomIds = [.. usedCells];
    roomIds.Shuffle(rng);
    int connected = 0;
    while (connected < 3)
    {
      int roomA = roomIds[0];
      roomIds.Remove(roomA);
      int roomB = roomIds[0];
      roomIds.Remove(roomB);

      DrawHallway(rooms, map, roomA, roomB);
      ++connected;

      if (rng.NextDouble() < 0.4)
        break;
    }
  }

  static void JoinAdjacentRooms(Dictionary<int, RLRoom> rooms, Map map, List<HashSet<int>> joinedRooms, HashSet<int> usedCells, Rng rng)
  {
    List<int> roomIds = [.. usedCells];
    foreach (int cell in roomIds)
    {
      joinedRooms.Add([cell]);
    }

    roomIds.Shuffle(rng);

    while (joinedRooms.Count > 1 && roomIds.Count > 0)
    {
      int roomId = roomIds[0];
      roomIds.RemoveAt(0);

      List<int> adjRooms = [.. AdjRooms(roomId, usedCells).Where(r => !AlreadyJoined(roomId, r, joinedRooms))];
      if (adjRooms.Count > 0)
      {
        int adjId = adjRooms[rng.Next(adjRooms.Count)];

        DrawHallway(rooms, map, roomId, adjId);

        // Merge the sets
        var roomSet = joinedRooms.Where(s => s.Contains(roomId)).First();
        var adjSet = joinedRooms.Where(s => s.Contains(adjId)).First();
        joinedRooms.Remove(roomSet);
        joinedRooms.Remove(adjSet);
        HashSet<int> unioned = [.. roomSet.Union(adjSet)];
        joinedRooms.Add(unioned);
      }
    }
  }

  static void JoinDistantRooms(Dictionary<int, RLRoom> rooms, Map map, List<HashSet<int>> joinedRooms, HashSet<int> usedCells, Rng rng)
  {
    List<int> roomIds = [.. usedCells];
    roomIds.Shuffle(rng);

    while (joinedRooms.Count > 1 && roomIds.Count > 0)
    {
      int roomId = roomIds[0];
      roomIds.RemoveAt(0);

      int r = roomId / 4;
      int c = roomId % 4;
      bool joined = false;
      foreach ((int dr, int dc) in DirsToSearch(roomId))
      {
        int sr = r + dr;
        int sc = c + dc;
        while (sr >= 0 && sr < 4 && sc >= 0 && sc < 4)
        {
          int otherId = sr * 4 + sc;

          // Stop looking if we hit an adjacent room
          if (AlreadyJoined(roomId, otherId, joinedRooms))
            break;

          if (usedCells.Contains(otherId))
          {
            DrawHallway(rooms, map, roomId, otherId);

            // Merge the sets
            var roomSet = joinedRooms.Where(s => s.Contains(roomId)).First();
            var adjSet = joinedRooms.Where(s => s.Contains(otherId)).First();
            joinedRooms.Remove(roomSet);
            joinedRooms.Remove(adjSet);
            HashSet<int> unioned = [.. roomSet.Union(adjSet)];
            joinedRooms.Add(unioned);

            joined = true;

            break;
          }

          if (joined)
            break;

          sr += dr;
          sc += dc;
        }
      }
    }

    static List<(int, int)> DirsToSearch(int roomId) => roomId switch
    {
      0 => [(1, 0), (0, 1)],
      1 or 2 => [(1, 0), (0, -1), (0, 1)],
      3 => [(1, 0), (0, -1)],
      4 => [(1, 0), (-1, 0), (0, 1)],
      5 or 6 => [(1, 0), (-1, 0), (0, 1), (0, -1)],
      7 => [(1, 0), (-1, 0), (0, -1)],
      8 => [(-1, 0), (0, 1)],
      9 or 10 => [(-1, 0), (0, -1), (0, 1)],
      _ => [(-1, 0), (0, -1)]
    };
  }

  static bool AlreadyJoined(int room, int other, List<HashSet<int>> joinedRooms)
  {
    foreach (var set in joinedRooms)
    {
      if (set.Contains(room) && set.Contains(other))
        return true;
    }

    return false;
  }

  static readonly int[] roomWidths = [3, 4, 5, 6, 7, 7, 8, 8, 8, 9, 9, 9, 10, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 14, 15];
  static (bool, RLRoom) TryPlacingInCell(int cell, Map map, HashSet<int> usedCells, Rng rng)
  {
    int h = rng.Next(3, 7);
    int w = roomWidths[rng.Next(roomWidths.Length)];

    int rr = rng.Next(0, CELL_HEIGHT - h - 1);
    int rc = rng.Next(0, CELL_WIDTH - w - 1);

    (int or, int oc) = OffSet(cell);
    int row = or + rr + 1, col = oc + rc + 1; // + 1 because outer walls are permanent

    if (CanPlace(row, col, h, w))
    {
      for (int c = col - 1; c <= col + w; c++)
      {
        if (map.TileAt(row - 1, c).Type == TileType.Sand)
          map.SetTile(row - 1, c, TileFactory.Get(TileType.DungeonWall));
        if (map.TileAt(row + h, c).Type == TileType.Sand)
          map.SetTile(row + h, c, TileFactory.Get(TileType.DungeonWall));
      }
      for (int r = row - 1; r <= row + h; r++)
      {
        if (map.TileAt(r, col - 1).Type == TileType.Sand)
          map.SetTile(r, col - 1, TileFactory.Get(TileType.DungeonWall));
        if (map.TileAt(r, col + w).Type == TileType.Sand)
          map.SetTile(r, col + w, TileFactory.Get(TileType.DungeonWall));
      }

      for (int r = row; r < row + h; r++)
      {
        for (int c = col; c < col + w; c++)
        {
          map.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
        }
      }

      usedCells.Add(cell);

      return (true, new(row, col, h, w, cell));
    }
    
    return (false, new(-1, -1, -1, -1, cell));

    bool CanPlace(int row, int col, int h, int w)
    {
      for (int r = row; r < row + h; r++)
      {
        for (int c = col; c < col + w; c++)
        {
          if (map.TileAt(r, c).Type == TileType.DungeonFloor)
            return false;
        }
      }

      return true;
    }

    // kinda dumb but clear...
    (int, int) OffSet(int cell) => cell switch
    {
      0 => (0, 0),
      1 => (0, 18),
      2 => (0, 36),
      3 => (0, 52),
      4 => (11, 0),
      5 => (11, 18),
      6 => (11, 36),
      7 => (11, 52),
      8 => (21, 0),
      9 => (21, 18),
      10 => (21, 36),
      _ => (21, 52)
    };
  }
  
  static RLRoom PlaceRoom(Map map, HashSet<int> usedCells, Rng rng)
  {
    List<int> cells = [.. Enumerable.Range(0, 12).Where(c => !usedCells.Contains(c))];
    cells.Shuffle(rng);

    foreach (int cell in cells)
    {
      (bool placed, RLRoom room) = TryPlacingInCell(cell, map, usedCells, rng);
      if (placed)
        return room;
    }

    return new(0, 0, 0, 0, -1);
  }
}

internal class RoguelikeDungeonBuilder(int dungeonId) : DungeonBuilder
{
  int DungeonId { get; set; } = dungeonId;

  public (Dungeon, Loc) Generate(int entranceRow, int entranceCol, Rng rng)
  {
    Dungeon dungeon = new(DungeonId, "", true);

    MonsterDeck deck = new();
    deck.Monsters.AddRange(["creeping coins"]);
    dungeon.MonsterDecks.Add(deck);

    // FOr rogue-esque levels, the stairs can go anywhere. Ie., the dungeon
    // levels don't stack neatly like in my other dungeon types. So I'm not
    // going to use the DungeonBuilder class's SetStairs method()
    Loc entranceStairs = Loc.Nowhere;
    Loc prevLoc = new(0, 0, entranceRow, entranceCol);
    Portal? prevDownstairs = null;
    int maxLevels = rng.Next(5, 8);
    for (int lvl = 0; lvl < maxLevels; lvl++)
    {
      Map map = RLLevelMaker.MakeLevel(rng);

      List<(int, int)> floors = map.SqsOfType(TileType.DungeonFloor);
      Upstairs upstairs = new("") { Destination = prevLoc };
      int i = rng.Next(floors.Count);
      (int ur, int uc) = floors[i];
      map.SetTile(ur, uc, upstairs);
      floors.RemoveAt(i);

      if (prevDownstairs is not null)
      {
        prevDownstairs.Destination = new(DungeonId, lvl, ur, uc);
      }

      if (lvl == 0)
      {
        entranceStairs = new(DungeonId, 0, ur, uc);
      }

      if (lvl < maxLevels - 1)
      {
        Downstairs downstairs = new("");
        (int dr, int dc) = floors[rng.Next(floors.Count)];
        map.SetTile(dr, dc, downstairs);
        prevLoc = new(DungeonId, lvl, dr, dc);
        prevDownstairs = downstairs;
      }

      dungeon.AddMap(map);
    }

    return (dungeon, entranceStairs);
  }
}
