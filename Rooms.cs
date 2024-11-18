
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

class Rooms
{
  public static void ChasmRoom(Map[] levels, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb)
  {
    HashSet<(int, int)> exits = [];
    HashSet<(int, int)> chasmSqs = [];
    Map map = levels[level];
    Map mapBelow = levels[level + 1];

    foreach (var (r, c) in room)
    {    
      List<(int, int)> adjSqs = Util.Adj4Sqs(r, c).ToList();
      bool isPerimeter = adjSqs.Any(sq => !room.Contains(sq));
      if (isPerimeter)
      {
        chasmSqs.Add((r, c));
        foreach (var adj in adjSqs)
        {
          if (room.Contains(adj))
            chasmSqs.Add(adj);
          else if (map.TileAt(adj).Type != TileType.DungeonWall)
            exits.Add(adj);
        }
      }
    }

    foreach (var (r, c) in chasmSqs)
    {
      if (map.TileAt(r, c).Type == TileType.DungeonFloor)
      {
        map.SetTile(r, c, TileFactory.Get(TileType.Chasm));
        if (mapBelow.TileAt(r, c).Type == TileType.DungeonWall)
          mapBelow.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    List<(int, int)> islandSqs = room.Where(sq => map.TileAt(sq).Type != TileType.Chasm)
                                     .ToList();
    Dictionary<TileType, int> passable = new()
    {
      [TileType.DungeonFloor] = 1,
      [TileType.Chasm] = 1
    };

    // Find the bridges (I suppose I don't need to have every exit have a bridge)
    HashSet<Loc> bridges = [];
    foreach (var (r, c) in exits)
    {
      Loc startLoc = new(dungeonID, level, r, c);
      (int, int) goalSq = islandSqs[rng.Next(islandSqs.Count)];
      Loc goalLoc = new(dungeonID, level, goalSq.Item1, goalSq.Item2);
      Stack<Loc> path = AStar.FindPath(map, startLoc, goalLoc, passable, false);
      if (path.Count > 0)
      {
        while (path.Count > 0)
        {
          Loc loc = path.Pop();
          if (map.TileAt(loc.Row, loc.Col).Type == TileType.Chasm)
            bridges.Add(loc);
        }
      }
    }

    List<(int, int)> floors = room.Where(sq => map.TileAt(sq).Type == TileType.DungeonFloor)
                                  .ToList();
    (int, int) triggerSq = floors[rng.Next(floors.Count)];
    BridgeTrigger trigger = new()
    {
      BridgeTiles = bridges
    };
    map.SetTile(triggerSq.Item1, triggerSq.Item2, trigger);
    objDb.LocListeners.Add(new Loc(dungeonID, level, triggerSq.Item1, triggerSq.Item2));
  }

  public static void MarkGraves(Map map, string epitaph, Random rng, int dungeonID, int level, List<List<(int, int)>> rooms, GameObjectDB objDb)
  {
    NameGenerator ng = new(rng, "data/names.txt");
    int roomNum = rng.Next(rooms.Count);
    List<(int r, int c)> room = rooms[roomNum];
    rooms.RemoveAt(roomNum);

    int numOfGraves = room.Count / 4;
    for (int j = 0; j < numOfGraves; j++)
    {
      var (r, c) = room[rng.Next(room.Count)];
      int roll = rng.Next(10);
      string message;
      if (roll == 0)
        message = $"{ng.GenerateName(rng.Next(6, 11)).Capitalize()}, claimed by {epitaph}.";
      else if (roll == 1)
        message = $"Here lies {ng.GenerateName(rng.Next(6, 11)).Capitalize()}, missed except not by that troll.";
      else if (roll == 2)
        message = $"{ng.GenerateName(rng.Next(6, 11)).Capitalize()}, mourned by few.";
      else if (roll == 3)
        message = $"{ng.GenerateName(rng.Next(6, 11)).Capitalize()}, beloved and betrayed.";
      else if (roll == 4)
        message = $"{ng.GenerateName(rng.Next(6, 11)).Capitalize()}: My love for you shall live forever. You, however, did not.";
      else
        message = "A grave too worn to be read.";

      map.SetTile(r, c, new Gravestone(message));
    }

    var (cr, cc) = room[rng.Next(room.Count)];
    Loc cryptLoc = new(dungeonID, level, cr, cc);
    Actor crypt = MonsterFactory.Get("haunted crypt", objDb, rng);
    objDb.AddNewActor(crypt, cryptLoc);
    
    map.Alerts.Add("A shiver runs up your spine.");
  }
}