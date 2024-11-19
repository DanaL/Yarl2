
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

  public static void CampRoom(List<(int, int)> room, int dungeonID, int level, FactDb factDb, GameObjectDB objDb, Random rng)
  {
    if (factDb.FactCheck("EarlyDenizen") is not SimpleFact ed)
      // This is an error condition but I don't think worth calling an expection over
      // because by this point in the process something else would have thrown an
      // exception if no early denizen had been set
      return;

    NameGenerator ng = new(rng, "data/names.txt");

    (int, int) fireSq = room[rng.Next(room.Count)];
    Loc fireLoc = new(dungeonID, level, fireSq.Item1, fireSq.Item2);
    Item fire = ItemFactory.Get(ItemNames.CAMPFIRE, objDb);
    objDb.SetToLoc(fireLoc, fire);

    List<Loc> spotsNearFire = room.Where(sq => Util.Distance(sq.Item1, sq.Item2, fireSq.Item1, fireSq.Item2) <= 3)
                                  .Select(sq => new Loc(dungeonID, level, sq.Item1, sq.Item2))
                                .Where(loc => loc != fireLoc && !objDb.Occupied(loc))
                                .ToList();

    for (int j = 0; j < rng.Next(2, 5); j++)
    {
      TreasureQuality quality = rng.Next(4) switch
      {
        0 => TreasureQuality.Common,
        1 or 2 => TreasureQuality.Uncommon,
        _ => TreasureQuality.Good
      };

      Item item = Treasure.ItemByQuality(quality, objDb, rng);
      Loc itemSq = spotsNearFire[rng.Next(spotsNearFire.Count)];
      objDb.SetToLoc(itemSq, item);
    }

    Actor boss;
    if (ed.Value == "kobold")
    {
      boss = MonsterFactory.Get("kobold foreman", objDb, rng);
      boss.Name = ng.BossName();
      boss.Traits.Add(new NamedTrait());      
    }
    else // goblins
    {      
      boss = MonsterFactory.Get("goblin boss", objDb, rng);
      boss.Name = ng.BossName();
      boss.Traits.Add(new NamedTrait());
    }

    int i = rng.Next(spotsNearFire.Count);
    boss.Traits.Add(new HomebodyTrait() { Loc = fireLoc, Range = 3 });
    objDb.AddNewActor(boss, spotsNearFire[i]);
    spotsNearFire.RemoveAt(i);

    for (int j = 0; j < rng.Next(2, 5); j++)
    {
      Actor minion = MonsterFactory.Get(ed.Value, objDb, rng);
      minion.Traits.Add(new HomebodyTrait() { Loc = fireLoc, Range = 3 });
      i = rng.Next(spotsNearFire.Count);
      objDb.AddNewActor(minion, spotsNearFire[i]);
      spotsNearFire.RemoveAt(i);
    }
  }

  public static void Orchard(Map map, List<(int, int)> room, int dungeonId, int level, FactDb factDb, GameObjectDB objDb, Random rng)
  {
    int minRow = int.MaxValue, maxRow = 0;
    int minCol = int.MaxValue, maxCol = 0;

    foreach ((int r, int c) in room)
    {
      TileType type = rng.NextDouble() < 0.35 ? TileType.Dirt : TileType.GreenTree;
      if (map.TileAt(r, c).Type == TileType.DungeonFloor)
        map.SetTile(r, c, TileFactory.Get(type));

      // find the rough centre
      if (r < minRow)
        minRow = r;
      if (r > maxRow)
        maxRow = r;
      if (c < minCol)
        minCol = c;
      if (c > maxCol)
        maxCol = c;
    }

    int statueR = (minRow + maxRow) / 2;
    int statueC = (minCol + maxCol) / 2;
    Loc statueLoc = new(dungeonId, level, statueR, statueC);
    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Traits.Add(new DescriptionTrait("An elf holding their hands up to the sky."));
    int rowRange = maxRow - minRow;
    int colRange = maxCol - minCol;
    statue.Traits.Add(new LightSourceTrait()
    {
      ExpiresOn = ulong.MaxValue, OwnerID = statue.ID, 
      Radius = rowRange > colRange ? rowRange : colRange
    });
    objDb.SetToLoc(statueLoc, statue);
  }

  public static void MarkGraves(Map map, string epitaph, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb)
  {
    NameGenerator ng = new(rng, "data/names.txt");
   
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