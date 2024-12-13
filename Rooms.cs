
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

using System.Text;

namespace Yarl2;

enum VaultDoorType { SecretDoor, Trigger, Key }
enum VaultType { Tomb } // I think I'll eventually have more...

class ChasmRoomInfo
{
  public List<(int, int)> ChasmSqs { get; set; } = [];
  public HashSet<(int, int)> Exits { get; set; } = [];
  public List<(int, int)> IslandSqs { get; set; } = [];
}

class Vaults
{
  public static (int, int) FindExit(Map map, HashSet<(int, int)> vault)
  {
    foreach ((int r, int c) sq in vault)
    {
      foreach (var adj in Util.Adj4Sqs(sq.r, sq.c))
      {
        Tile tile = map.TileAt(adj);
        TileType type = tile.Type;
        if (!vault.Contains(adj) && (tile.Passable() || type == TileType.ClosedDoor || type == TileType.LockedDoor))
          return adj;
      }
    }

    return (-1, -1);
  }

  public static void CreateVault(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> vault, Random rng, GameObjectDB objDb, FactDb factDb)
  {
    if (level == 0)
    {
      // A level zero vault has been vandalized or plundered by past
      // adventurers.
      VandalizedVault(map, dungeonID, level, doorRow, doorCol, vault, rng, objDb, factDb);
      return;
    }

    if (level == 1 )
    {
      if (rng.Next(3) == 0)
        HiddenVault(map, dungeonID, level, doorRow, doorCol, vault, rng, objDb, factDb);
      else
        HiddenVault(map, dungeonID, level, doorRow, doorCol, vault, rng, objDb, factDb);
       
      return;
    }

    VaultDoorType doorType = rng.Next(3) switch
    {
      0 => VaultDoorType.Key,
      1 => VaultDoorType.Trigger,
      _ => VaultDoorType.SecretDoor
    };

    switch (doorType)
    {
      case VaultDoorType.Key:
        SetVaultDoorKey(map, dungeonID, level, doorRow, doorCol, vault, rng, objDb);
        break;
      case VaultDoorType.Trigger:
        SetPortcullis(map, dungeonID, level, vault, doorRow, doorCol, objDb, rng);
        break;
      case VaultDoorType.SecretDoor:
        map.SetTile(doorRow, doorCol, TileFactory.Get(TileType.SecretDoor));
        break;
    }
  }

  static (int, int) PickVaultTriggerLoc(Map map, int row, int col, int height, int width, HashSet<(int, int)> vault, Random rng)
  {
    int startRow = int.Max(row - 10, 1);
    int endRow = int.Min(row + 10, height);
    int startCol = int.Max(col - 10, 1);
    int endCol = int.Min(col + 10, width);
    int triggerRow = -1, triggerCol = -1;

    List<(int, int)> candidates = [];
    for (int r = startRow; r < endRow; r++)
    {
      for (int c = startCol; c < endCol; c++)
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

  static void VandalizedVault(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> vault, Random rng, GameObjectDB objDb, FactDb factDb)
  {
    map.SetTile(doorRow, doorCol, TileFactory.Get(TileType.BrokenPortcullis));
    RulerInfo rulerInfo = factDb.Ruler;
        
    char ch;
    string statueDesc;
    string skullType;

    double roll = rng.NextDouble();
    if (roll < 0.5)
    {
      statueDesc = "Broken remains of a statue.";
      ch = '&';
      skullType = "";
    }
    else
    {
      switch (rulerInfo.Type)
      {
        case OGRulerType.ElfLord:
          statueDesc = "A graffitied, defaced statue of an elf.";
          ch = '@';
          skullType = "elf";
          break;
        default:
          statueDesc = "A graffitied, defaced statue of a dwarf.";
          ch = 'h';
          skullType = "dwarf";
          break;
      }
    }

    List<(int, int)> sqs = vault.Where(sq => map.TileAt(sq).Type == TileType.DungeonFloor).ToList();
    if (sqs.Count == 0)
      return; // I can't imagine this actually ever happening
        
    int i = rng.Next(sqs.Count);
    (int, int) loc = sqs[i];
    sqs.RemoveAt(i);
    Loc statueLoc = new(dungeonID, level, loc.Item1, loc.Item2);

    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Traits.Add(new DescriptionTrait(statueDesc));
    statue.Glyph = statue.Glyph with { Ch = ch };
    objDb.SetToLoc(statueLoc, statue);

    List<(int, int)> adj = Util.Adj4Sqs(loc.Item1, loc.Item2)
                               .Where(sq => map.TileAt(sq).Type == TileType.DungeonFloor)
                               .ToList();
    if (adj.Count == 0) 
      return; // I also can't imagine this actually happing
    (int, int) landmarkSq = adj[rng.Next(adj.Count)];
    
    if (rng.NextDouble() < 0.15)
    {
      Item skull = ItemFactory.Get(ItemNames.SKULL, objDb);
      if (skullType != "")
        skull.Traits.Add(new AdjectiveTrait(skullType));
      (int skullRow, int skullCol) = adj[rng.Next(adj.Count)];
      Loc skullLoc = new(dungeonID, level, skullRow, skullCol);
      objDb.SetToLoc(skullLoc, skull);
    }
  }

  static Landmark GetTombDecoration(Random rng, FactDb factDb)
  {
    RulerInfo rulerInfo = factDb.Ruler;
    string s = rulerInfo.Type switch
    {
      OGRulerType.ElfLord => rng.Next(4) switch
      {
        0 => "A bas relief carving of a mighty forest.",
        1 => "A fading fresco of elves dancing under a crescent moon.",
        2 => "A bas relief carving depicting a funeral procession.",
        _ => "A fading fresco of a great hunt."
      },
      _ => rng.Next(3) switch
      {
        0 => "A stone craving of a noble dwarf laying in state.",
        1 => "A stone carving of subterranean city.",
        _ => "A stone carving of a dwarven smith at work."
      },
    };
    Landmark landmark = new(s);

    return landmark;
  }

  static Landmark GetTombMarker(NameGenerator ng, Random rng, FactDb factDb)
  {
    RulerInfo rulerInfo = factDb.Ruler;
    string rulerName = $"{rulerInfo.Title} {rulerInfo.Name}";
    string name = ng.GenerateName(rng.Next(6, 12));
    HistoricalFigure hf = new(name);
    
    string relation = rng.Next(10) switch
    {
      0 => "Consort",
      1 => "Grandchild",
      2 => "Uncle",
      3 => "Aunt",
      4 => "Child",
      5 => "Mother",
      7 => "Father",
      _ => "Cousin"
    };
    string causeOfDeath = rng.Next(11) switch
    {
      0 => "died from plague",
      1 => "died by illness",
      2 => "died from a riding injury",
      3 => "was felled in battle",
      4 => "died under mysterious circumstances",
      5 => "perished from misadventure",
      6 => "died from natural causes",
      7 => "disappeared",
      8 => "died from the family curse",
      9 => "murdered by rebels",
      _ => "we don't talk about"
    };

    var sb = new StringBuilder();
    sb.Append("The tomb of ");
    sb.Append(name.Capitalize());
    sb.Append(", ");
    sb.Append(relation);
    sb.Append(" of ");
    sb.Append(rulerName);
    sb.Append(", who ");
    sb.Append(causeOfDeath);
    sb.Append('.');

    hf.Title = $"{relation.Capitalize()} of {rulerName}";
    factDb.Add(hf);
    
    return new Landmark(sb.ToString());
  } 

  static void HiddenVault(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> vault, Random rng, GameObjectDB objDb, FactDb factDb)
  {
    map.SetTile(doorRow, doorCol, TileFactory.Get(TileType.SecretDoor));
    List<Loc> locs = vault.Where(sq => map.TileAt(sq).Type == TileType.DungeonFloor)
                          .Select(sq => new Loc(dungeonID, level, sq.Item1, sq.Item2)).ToList();

    void SetItem(TreasureQuality quality)
    {
      if (locs.Count == 0)
        return;

      int i = rng.Next(locs.Count);
      Loc loc = locs[i];
      locs.RemoveAt(i);
      Item item = Treasure.ItemByQuality(quality, objDb, rng);
      objDb.SetToLoc(loc, item);
    }

    NameGenerator ng = new(rng, "data/names.txt");
    if (level <= 2)
    {
      SetItem(TreasureQuality.Uncommon);
      SetItem(TreasureQuality.Uncommon);
      SetItem(TreasureQuality.Good);
    }
    else
    {
      for (int i = 0; i < rng.Next(2, 3); i++)
        SetItem(TreasureQuality.Good);
    }

    // place the marker on the other side of the door
    var adj = Util.Adj4Sqs(doorRow, doorCol).Where(sq => vault.Contains(sq)).ToList();
    if (adj.Count > 0)
    {
      Tile marker = GetTombMarker(ng, rng, factDb);
      map.SetTile(adj[0], marker);
    }

    List<Loc> decorationLocs = [];
    foreach (Loc loc in locs)
    {
      var adjWall = Util.Adj4Sqs(loc.Row, loc.Col)
                        .Where(sq => map.TileAt(sq).Type == TileType.DungeonWall)
                        .Any();
      if (adjWall)
        decorationLocs.Add(loc);
    }
    if (decorationLocs.Count > 0)
    {
      Landmark landmark = GetTombDecoration(rng, factDb);
      Loc loc = decorationLocs[rng.Next(decorationLocs.Count)];
      map.SetTile(loc.Row, loc.Col, landmark);
    }

    bool haunted = rng.Next(5) == 0;
    if (haunted)
    {
      Actor spirit = MonsterFactory.Get("shadow", objDb, rng);
      objDb.AddNewActor(spirit, locs[rng.Next(locs.Count)]);
    }
  }

  static void SetVaultDoorKey(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> room, Random rng, GameObjectDB objDb)
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

    var (fg, bg) = Util.MetallicColour(material);
    Item key = new() { 
      Name = "key", Type = ItemType.Tool, Value = 1,
      Glyph = new Glyph(';', fg, bg, Colours.BLACK, Colours.BLACK)
    };
    key.Traits.Add(new MetalTrait() { Type = material });
    key.Traits.Add(new VaultKeyTrait(new Loc(dungeonID, level, doorRow, doorCol)));

    // Find a random spot for key. Eventually we'll place keys potentially on other
    // levels. I'll also need to check for a key that's potentially impossible to get
    // to. (Imagine a level with two vaults and each other's key is locked inside the
    // other vault)
    List<(int, int)> sqs = [];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {
        if (map.TileAt(r, c).Type == TileType.DungeonFloor && !room.Contains((r, c)))
          sqs.Add((r, c));
      }
    }
    var (keyRow, keyCol) = sqs[rng.Next(sqs.Count)];
    Loc keyLoc = new(dungeonID, level, keyRow, keyCol);
    objDb.Add(key);
    objDb.SetToLoc(keyLoc, key);
  }

  static void SetPortcullis(Map map, int dungeonID, int level, HashSet<(int, int)> room, int doorRow, int doorCol, GameObjectDB objDb, Random rng)
  {
    int triggerRow, triggerCol;
    (triggerRow, triggerCol) = PickVaultTriggerLoc(map, doorRow, doorCol, map.Height, map.Width, room, rng);
    if (triggerRow != -1 && triggerCol != -1)
    {
      Console.WriteLine($"Vault!! {level}");
      
      map.SetTile(doorRow, doorCol, new Portcullis(false));
      objDb.LocListeners.Add(new(dungeonID, level, triggerRow, triggerCol));
      map.SetTile(triggerRow, triggerCol, new GateTrigger(new Loc(dungeonID, level, doorRow, doorCol)));
    }
  }
}

class Rooms
{
  public static bool PotentialVault(Map map, List<(int, int)> room)
  {
    if (room.Count > 75)
      return false;

    // Vaults only have one exit
    HashSet<(int, int)> exits = [];
    foreach (var (r, c) in room)
    {
      // Don't allow a room with the upstairs to be a vault because it's
      // likely the key will be on the other side of its door, preventing
      // the player from progress. (Maybe when the dungeon is deeper and the
      // PC presumably more powerful this will be okay)
      if (map.TileAt(r, c).Type == TileType.Upstairs)
        return false;

      foreach (var adj in Util.Adj8Sqs(r, c))
      {
        Tile adjTile = map.TileAt(adj);
        TileType adjType = adjTile.Type;  
        if (!room.Contains(adj) && (adjTile.Passable() || adjType == TileType.ClosedDoor || adjType == TileType.LockedDoor))
          exits.Add(adj);
      }
    }

    return exits.Count == 1;
  }

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
          else
          {
            switch (map.TileAt(adj).Type)
            {
              case TileType.ClosedDoor:
              case TileType.LockedDoor:
              case TileType.DungeonFloor:
                exits.Add(adj);
                break;
            }
          }
        }
      }
    }

    List<(int, int)> islandSqs 
      = room.Where(sq => !chasmSqs.Contains(sq) 
            && map.TileAt(sq.Item1, sq.Item2).Type != TileType.Upstairs
            && map.TileAt(sq.Item1, sq.Item2).Type != TileType.Downstairs)
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
      Tile tile = map.TileAt(r, c);
      if (tile.Type == TileType.DungeonFloor)
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

    if (info.IslandSqs.Count == 0)
      return [];

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
    if (info.ChasmSqs.Count == 0 || info.IslandSqs.Count == 0)
      return;

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
    if (info.ChasmSqs.Count == 0 || info.IslandSqs.Count == 0)
      return;

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
    if (info.ChasmSqs.Count == 0 || info.IslandSqs.Count == 0)
      return;
      
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
      boss = MonsterFactory.Get("hobgoblin", objDb, rng);
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

    // This stops a tree from being underneath the statue, which would
    // seemweird to me
    map.SetTile(statueR, statueC, TileFactory.Get(TileType.Dirt));

    Loc statueLoc = new(dungeonId, level, statueR, statueC);
    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Traits.Add(new DescriptionTrait("An elf holding their hands up to the sky."));
    AppleProducerTrait pt = new() { OwnerID = statue.ID };
    statue.Traits.Add(pt);
    objDb.EndOfRoundListeners.Add(pt);

    int rowRange = maxRow - minRow;
    int colRange = maxCol - minCol;
    statue.Traits.Add(new LightSourceTrait()
    {
      ExpiresOn = ulong.MaxValue, OwnerID = statue.ID, 
      Radius = rowRange > colRange ? rowRange : colRange
    });
    objDb.SetToLoc(statueLoc, statue);

    if (factDb.FactCheck("OrchardExists") is not SimpleFact orchardExists)
    {
      factDb.Add(new SimpleFact() { Name = "OrchardExists", Value = "true" });
    }
  }

  public static void MarkGraves(Map map, string epitaph, Random rng, int dungeonID, int level, List<(int, int)> room, GameObjectDB objDb, FactDb factDb)
  {
    NameGenerator ng = new(rng, "data/names.txt");

    int numOfGraves = room.Count / 4;
    for (int j = 0; j < numOfGraves; j++)
    {
      var (r, c) = room[rng.Next(room.Count)];
      int roll = rng.Next(10);
      string message;
      string name;

      if (factDb.FactCheck("KylieGrave") is not SimpleFact && rng.Next(20) == 0)
      {
        name = "Kylie";
        factDb.Add(new SimpleFact() { Name = "KylieGrave", Value = "KylieGrave" });
      }
      else
      {
        name = ng.GenerateName(rng.Next(6, 11)).Capitalize();
      }

      if (roll == 0)
        message = $"{name}, claimed by {epitaph}.";
      else if (roll == 1)
        message = $"Here lies {name}, missed except not by that troll.";
      else if (roll == 2)
        message = $"{name}, mourned by few.";
      else if (roll == 3)
        message = $"{name}, beloved and betrayed.";
      else if (roll == 4)
        message = $"{name}: My love for you shall live forever. You, however, did not.";
      else
        message = "A grave too worn to be read.";

      map.SetTile(r, c, new Gravestone(message));
    }

    // We won't generate every graveyard with a haunted crypt
    if (rng.NextDouble() <= 0.33)
    {
      var (cr, cc) = room[rng.Next(room.Count)];
      Loc cryptLoc = new(dungeonID, level, cr, cc);
      Actor crypt = MonsterFactory.Get("haunted crypt", objDb, rng);
      objDb.AddNewActor(crypt, cryptLoc);
      map.Alerts.Add("A shiver runs up your spine.");
    }    
  }
}