
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

class ChasmRoomInfo
{
  public List<(int, int)> ChasmSqs { get; set; } = [];
  public HashSet<(int, int)> Exits { get; set; } = [];
  public List<(int, int)> IslandSqs { get; set; } = [];
}

class Rooms
{
  static ChasmRoomInfo ChasmRoomInfo(Map map, List<(int, int)> room)
  {
    HashSet<(int, int)> exits = [];
    List<(int, int)> chasmSqs = [];
    
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
          else if (map.TileAt(adj).Type == TileType.ClosedDoor || map.TileAt(adj).Type == TileType.LockedDoor)
            exits.Add(adj);
        }
      }
    }

    List<(int, int)> islandSqs = room.Where(sq => !chasmSqs.Contains(sq))
                                     .ToList();

    return new ChasmRoomInfo()
    {
      ChasmSqs = chasmSqs,
      Exits = exits,
      IslandSqs = islandSqs
    };
  }

  static void MakeChasm(Map map, Map mapBelow, List<(int, int)> chasmSqs)
  {
  foreach (var (r, c) in chasmSqs)
    {
      if (map.TileAt(r, c).Type == TileType.DungeonFloor)
      {
        map.SetTile(r, c, TileFactory.Get(TileType.Chasm));
        if (mapBelow.TileAt(r, c).Type == TileType.DungeonWall)
          mapBelow.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }
  }

  static HashSet<Loc> DetermineBridges(Map map, int dungeonID, int level, ChasmRoomInfo info, Random rng)
  {
    HashSet<Loc> bridges = [];
    (int, int) goalSq = info.IslandSqs[rng.Next(info.IslandSqs.Count)];

    Dictionary<TileType, int> passable = new()
    {
      [TileType.DungeonFloor] = 1,
      [TileType.Chasm] = 1
    };

    foreach (var (r, c) in info.Exits)
    {
      Loc startLoc = new(dungeonID, level, r, c);      
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

    return bridges;
  }

  public static void ChasmTrapRoom(Map[] levels, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb)
  {
    Map map = levels[level];
    Map mapBelow = levels[level + 1];

    ChasmRoomInfo info = ChasmRoomInfo(map, room);
    MakeChasm(map, mapBelow, info.ChasmSqs);
    HashSet<Loc> bridges = DetermineBridges(map, dungeonID, level, info, rng);
    foreach (Loc bridge in bridges)
    {
      map.SetTile(bridge.Row, bridge.Col, TileFactory.Get(TileType.WoodBridge));
    }

    (int, int) trapSq = info.IslandSqs[rng.Next(info.IslandSqs.Count)];
    Loc triggerLoc = new Loc(dungeonID, level, trapSq.Item1, trapSq.Item2);
    BridgeCollapseTrap trap = new()
    {
      BridgeTiles = bridges
    };
    map.SetTile(trapSq.Item1, trapSq.Item2, trap);
    objDb.LocListeners.Add(triggerLoc);

    Item bait = Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng);
    objDb.SetToLoc(triggerLoc, bait);
    if (bait.Type != ItemType.Zorkmid)
    {
      bait = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      bait.Value = rng.Next(20, 51);
      objDb.SetToLoc(triggerLoc, bait);
    }
  }

  public static void TriggerChasmRoom(Map[] levels, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb)
  {
    Map map = levels[level];
    Map mapBelow = levels[level + 1];

    ChasmRoomInfo info = ChasmRoomInfo(map, room);
    MakeChasm(map, mapBelow, info.ChasmSqs);
    HashSet<Loc> bridges = DetermineBridges(map, dungeonID, level, info, rng);

    (int, int) triggerSq = info.IslandSqs[rng.Next(info.IslandSqs.Count)];
    BridgeTrigger trigger = new()
    {
      BridgeTiles = bridges
    };
    map.SetTile(triggerSq.Item1, triggerSq.Item2, trigger);
    objDb.LocListeners.Add(new Loc(dungeonID, level, triggerSq.Item1, triggerSq.Item2));

    (int, int) treasureSq = info.IslandSqs[rng.Next(info.IslandSqs.Count)];
    Loc treasureLoc = new(dungeonID, level, treasureSq.Item1, treasureSq.Item2);
    TreasureQuality quality = level < 2 ? TreasureQuality.Uncommon : TreasureQuality.Good;
    Item treasure = Treasure.ItemByQuality(quality, objDb, rng);
    objDb.SetToLoc(treasureLoc, treasure);
  }

  public static void BasicChasmRoom(Map[] levels, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb)
  {
    Map map = levels[level];
    Map mapBelow = levels[level + 1];

    ChasmRoomInfo info = ChasmRoomInfo(map, room);
    MakeChasm(map, mapBelow, info.ChasmSqs);
    HashSet<Loc> bridges = DetermineBridges(map, dungeonID, level, info, rng);
    foreach (Loc bridge in bridges)
    {
      map.SetTile(bridge.Row, bridge.Col, TileFactory.Get(TileType.WoodBridge));
    }

    if (rng.NextDouble() < 0.5)
    {
      (int, int) treasureSq = info.IslandSqs[rng.Next(info.IslandSqs.Count)];
      Loc treasureLoc = new(dungeonID, level, treasureSq.Item1, treasureSq.Item2);
      TreasureQuality quality = rng.NextDouble() < 05 ? TreasureQuality.Uncommon : TreasureQuality.Good;
      Item treasure = Treasure.ItemByQuality(quality, objDb, rng);
      objDb.SetToLoc(treasureLoc, treasure);
    }
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