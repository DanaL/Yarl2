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

class Vaults
{
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
            if (tile.IsTrap())
              open = true;
            else
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

  static int CountAdjTileType(Map map, int row, int col, TileType type)
  {
    int count = 0;

    foreach (var sq in Util.Adj8Sqs(row, col))
    {
      if (map.InBounds(row, col) && map.TileAt(sq).Type == type)
        ++count;
    }

    return count;
  }

  public static void FindPotentialVaults(Map map, int height, int width, Random rng, int dungeonID, int levelNum, GameObjectDB objDb, FactDb factDb)
  {
    Dictionary<(int, int), int> areas = [];
    Dictionary<int, HashSet<(int, int)>> regions = [];

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
          regions.Add(areaID, region);
          foreach (var sq in region)
          {
            areas.Add(sq, areaID);
          }
          areaID++;
        }
      }
    }

    // We want to only consider for vaults areas that have at least 12 sqs that
    // are floors surrounded by other floors, are smaller than 75 sqs
    // (if just feels weird to me to have really giant vaults), and have only 
    // one exit.
    List<HashSet<(int, int)>> rooms = [];
    List<(int, int)> doors = [];
    (int, int) lastDoor = (-1, -1);
    foreach (int roomID in regions.Keys)
    {
      if (regions[roomID].Count > 75)
        continue;

      HashSet<(int, int)> region = regions[roomID];
      HashSet<(int, int)> adjDoors = [];
      int allFloorNeighbours = 0;
      foreach (var sq in region)
      {
        int adjFloorCount = 0;
        foreach (var adj in Util.Adj8Sqs(sq.Item1, sq.Item2))
        {
          if (!map.InBounds(adj))
            continue;
          TileType adjType = map.TileAt(adj.Item1, adj.Item2).Type;
          if (adjType == TileType.DungeonFloor)
            ++adjFloorCount;
          else if (adjType == TileType.ClosedDoor || adjType == TileType.LockedDoor)
          {
            adjDoors.Add(adj);
            lastDoor = adj;
          }
        }

        if (adjFloorCount == 8)
          ++allFloorNeighbours;
      }

      if (allFloorNeighbours >= 12 && adjDoors.Count == 1)
      {
        rooms.Add(region);
        doors.Add(lastDoor);
      }
    }

    int vaultsPlaced = 0;
    for (int j = 0; j < rooms.Count; j++)
    {
      if (rng.NextDouble() < 0.25)
      {
        (int doorRow, int doorCol) = doors[j];
        CreateVault(map, dungeonID, levelNum, doorRow, doorCol, rooms[j], rng, objDb, factDb);
        ++vaultsPlaced;
      }

      if (vaultsPlaced == 2 || (levelNum == 0 && vaultsPlaced > 0))
        break;
    }
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
  }

  static void CreateVault(Map map, int dungeonID, int level, int doorRow, int doorCol, HashSet<(int, int)> vault, Random rng, GameObjectDB objDb, FactDb factDb)
  {
    if (level == 0)
    {
      // A level zero vault has been vandalized or plundered by past
      // adventurers.
      //VandalizedVault(map, dungeonID, level, doorRow, doorCol, vault, rng, objDb, factDb);
      SetPortcullis(map, dungeonID, level, vault, doorRow, doorCol, objDb, rng);
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
