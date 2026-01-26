// Delve - A roguelike computer RPG
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

class InitialDungeonBuilder(int dungeonId, (int, int) entrance, string mainOccupant) : DungeonBuilder
{
  const int HEIGHT = 30;
  const int WIDTH = 70;
  int DungeonId { get; set; } = dungeonId;
  (int, int) Entrance { get; set; } = entrance;
  string MainOccupant { get; set; } = mainOccupant;

  public Dungeon Generate(string arrivalMessage, FactDb factDb, GameObjectDB objDb, Rng rng)
  {
    int numOfLevels = rng.Next(5, 8);

    Dungeon dungeon = new(DungeonId, "the Old Ruins", arrivalMessage, true);
    DungeonMap mapper = new(rng);
    Map[] levels = new Map[numOfLevels];

    dungeon.MonsterDecks = DeckBuilder.ReadDeck(MainOccupant, rng);

    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(WIDTH, HEIGHT);
      dungeon.AddMap(levels[levelNum]);

      AddSecretDoors(levels[levelNum], rng);
    }

    AddRooms(levels, objDb, factDb, rng);
      
    dungeon.LevelMaps[numOfLevels - 1].Features |= MapFeatures.UndiggableFloor;

    List<(int, TileType)> riverLevels = [];
    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      // Maybe add a river/chasm to the level?
      if (rng.Next(4) == 0)
      {
        Map? nextLevel = null;
        RiverConfig riverConfig;
        TileType riverType = TileType.DeepWater;
        if (levelNum < numOfLevels - 1 && rng.Next(3) == 0)
        {
          riverConfig = new(TileType.Chasm, false, false);
          nextLevel = levels[levelNum + 1];
        }
        else
        {
          riverConfig = new(TileType.DeepWater, false, false);
        }

        AddRiverToLevel(riverConfig, levels[levelNum], nextLevel, levelNum, HEIGHT, WIDTH, DungeonId, objDb, rng);
        riverLevels.Add((levelNum, riverType));
      }
    }

    TidyOrphanedDoors(levels);

    SetStairs(DungeonId, levels, Entrance, dungeon.Descending, rng);

    foreach ((int levelNum, TileType riverType) in riverLevels)
    {
      RiverQoLCheck(levels[levelNum], DungeonId, levelNum, objDb, rng);
      if (riverType == TileType.DeepWater)
        DecorateRiver(levels[levelNum], DungeonId, levelNum, objDb, rng);
    }

    for (int levelNum = 0; levelNum < levels.Length; levelNum++)
    {
      Map map = levels[levelNum];

      SetTraps(map, DungeonId, levelNum, numOfLevels, rng);

      List<Loc> floors = [];
      for (int r = 0; r < map.Height; r++)
      {
        for (int c = 0; c < map.Width; c++)
        {
          switch (map.TileAt(r, c).Type)
          {
            case TileType.DungeonFloor:
            case TileType.HiddenTrapDoor:
            case TileType.TrapDoor:
            case TileType.TeleportTrap:
            case TileType.HiddenTeleportTrap:
            case TileType.WoodBridge:
              Loc floor = new(DungeonId, levelNum, r, c);
              if (Util.GoodFloorSpace(objDb, floor))
                floors.Add(floor);
              break;
          }
        }
      }
      
      AddTreasure(objDb, floors, levelNum, rng);
      
      // Maybe add an illusion/trap
      if (levelNum < numOfLevels - 1 && rng.Next(10) == 0)
      {
        AddBaitIllusion(map, DungeonId, levelNum, objDb, rng);
      }

      if (rng.Next(4) == 0)
      {
        TunnelCarver.MakeCollapsedTunnel(DungeonId, levelNum, map, objDb, rng);
      }

      if (rng.Next(6) == 0)
      {
        AddMoldPatch(map, floors, objDb, rng);
      }
    }

    // 1 in 3 dungeons have a captive
    if (rng.Next(3) == 0)
    {
      int captiveLevel = rng.Next(1, numOfLevels);
      CaptiveFeature.Create(DungeonId, captiveLevel, levels[captiveLevel], objDb, factDb, rng);
    }

    AddDecorations(levels, objDb, factDb, rng);

    PopulateDungeon(dungeon, rng, objDb);

    // Add a couple of guaranteed good items to dungeon
    AddTalismanToLevel(levels[1], DungeonId, 1, rng, objDb);
    AddTalismanToLevel(levels[3], DungeonId, 3, rng, objDb);

    int fallenAdventurer = rng.Next(1, numOfLevels);
    AddWidowerBeau(objDb, levels[fallenAdventurer], fallenAdventurer, factDb, rng);

    if (factDb.FactCheck("EarlyDenizen") is SimpleFact earlyOcc)
    {
      SetBoss(dungeon, objDb, factDb, earlyOcc.Value, rng);
    }

    // The idol 'puzzle' gives a guaranteed Potion of Hardiness, but I think 
    // I'd like to have a few different mini-puzzles for variety
    int altarLevel = rng.Next(0, numOfLevels);
    IdolAltarMaker.MakeAltar(DungeonId, levels, objDb, factDb, rng, altarLevel);

    SetPuzzle(dungeon, objDb, factDb, rng);

    if (rng.Next(3) == 0)
      GnomeMerchant(levels, DungeonId, rng, objDb);

    // Update the main quest state when the player steps on the up stairs of 
    // the 3rd level. (This will mess up if the player somehow never actually 
    // steps on the upstairs...)
    foreach (var sq in levels[2].SqsOfType(TileType.Upstairs))
    {
      Loc loc = new(DungeonId, 2, sq.Item1, sq.Item2);
      SetQuestStateAtLoc ce = new(loc, 1);
      objDb.ConditionalEvents.Add(ce);
    }
    
    return dungeon;
  }

  void AddRooms(Map[] levelMaps, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    bool graveyard = false;
    double chanceOfDesecratedAltar = 0.25;
    bool artifactVault = false, ckRoom = false;
    HashSet<TileType> vaultExists = [ TileType.ClosedDoor, TileType.LockedDoor ];
    for (int level = 0; level < levelMaps.Length; level++)
    {
      Map map = levelMaps[level];
      List<List<(int, int)>> rooms = map.FindRooms(9);
      List<int> roomIds = [.. Enumerable.Range(0, rooms.Count)];
      roomIds.Shuffle(rng);
      List<int> potentialVaults = [];

      foreach (int id in roomIds)
      {
        RoomCorners corners = Rooms.IsRectangle(map, rooms[id]);
        if (corners.LowerRow - corners.UpperRow >= 5 && corners.RightCol - corners.LeftCol >= 5)
        {
          var innerSqs = Rooms.RoomInRoom(map, corners, rng);
          rooms[id] = [.. innerSqs];
          break;
        }
      }

      foreach (int id in roomIds)
      {
        if (Rooms.PotentialVault(map, rooms[id], vaultExists))
          potentialVaults.Add(id);
      }
      
      if (potentialVaults.Count > 0 && rng.NextDouble() < 0.33)
      {
        int vaultId = potentialVaults[rng.Next(potentialVaults.Count)];
        HashSet<(int, int)> vault = [.. rooms[vaultId]];
        var (doorR, doorC) = Vaults.FindExit(map, vault);
        roomIds.Remove(vaultId);

        // We could have found a false vault. Likely a spot separate from
        // the rest of the dungeon by a river or chasm
        if (doorR >= 0 && doorC >= 0)
        {
          bool artifact = false;
          if (level >= 3 && !artifactVault && rng.NextDouble() < 0.5)
          {
            artifact = true;
            artifactVault = true;
          }

          Vaults.CreateVault(map, DungeonId, level, doorR, doorC, vault, artifact, rng, objDb, factDb);
        }
      }

      if (level < levelMaps.Length - 1 && rng.NextDouble() < 0.2)
      {
        switch (rng.Next(4))
        {
          case 0:
            Rooms.ChasmTrapRoom(levelMaps, rng, DungeonId, level, rooms[roomIds[0]], objDb);
            break;
          case 1:
            Rooms.TriggerChasmRoom(levelMaps, rng, DungeonId, level, rooms[roomIds[0]], objDb);
            break;
          case 2:
            Rooms.BasicChasmRoom(levelMaps, rng, DungeonId, level, rooms[roomIds[0]], objDb);
            break;
          default:
            Rooms.ChasmIslandRoom(levelMaps, rng, DungeonId, level, rooms[roomIds[0]], objDb);
            break;
        }

        roomIds.RemoveAt(0);
      }

      if (level >= 4 && rng.NextDouble() < 0.10)
      {
        // For mimic groups, we don't take their rooms out of rotation
        int roomId = roomIds[rng.Next(roomIds.Count)];
        Rooms.AddMimicGroup(rooms[roomId], DungeonId, level, objDb, rng);
      }

      // These aren't rooms really, but decorations/features
      if (level > 0 && rng.NextDouble() < 0.25)
      {
        Rooms.CampRoom(rooms[roomIds[0]], DungeonId, level, factDb, objDb, rng);
        roomIds.RemoveAt(0);
      }

      if (factDb.Ruler.Type == OGRulerType.ElfLord && rng.NextDouble() < 0.15)
      {
        Rooms.Orchard(levelMaps[level], rooms[roomIds[0]], DungeonId, level, factDb, objDb, rng);
        roomIds.RemoveAt(0);
      }

      if (!graveyard && level > 0 && rng.Next(10) == 0)
      {
        Rooms.MarkGraves(levelMaps[level], rng, DungeonId, level, rooms[roomIds[0]], objDb, factDb);
        roomIds.RemoveAt(0);
        graveyard = true;
      }

      if (level >= 2 && factDb.Ruler.Type == OGRulerType.DwarfLord && rng.NextDouble() < 0.15)
      {
        Rooms.MakeMinedChamber(levelMaps[level], rooms[roomIds[0]], DungeonId, level, factDb, objDb, rng);
        roomIds.RemoveAt(0);
      }

      if (level >= 4 && !ckRoom && rng.NextDouble() < 0.1)
      {
        CrimsonKingRoom(levelMaps[level], rooms[roomIds[0]], DungeonId, level, objDb, rng);
        roomIds.RemoveAt(0);
        ckRoom = true;  
      }

      if (level == 0)
      {
       MoonDaughterSpot(levelMaps[level], rooms[roomIds[0]], DungeonId, level, objDb, rng);
       roomIds.RemoveAt(0); 
      }

      if (rng.NextDouble() < chanceOfDesecratedAltar)
      {
        List<(int, int)> room = rooms[roomIds[0]];
        roomIds.RemoveAt(0);
        (int, int) altarSq = room[rng.Next(room.Count)];
        Loc altarLoc = new(DungeonId, level, altarSq.Item1, altarSq.Item2);
        Item altar = ItemFactory.Get(ItemNames.STONE_ALTAR, objDb);
        altar.Traits.Add(new AdjectiveTrait("desecrated"));
        altar.Traits.Add(new DesecratedTrait());
        string fluid = rng.NextDouble() < 0.5 ? "blood" : "excrement";
        altar.Traits.Add(new DescriptionTrait($"This altar, once holy, has been desecrated by vile symbols drawn in {fluid}."));
        objDb.SetToLoc(altarLoc, altar);
        chanceOfDesecratedAltar = 0.25;
      }
      else
      {
        chanceOfDesecratedAltar += 0.1;
      }

      if (level >= 2 && rng.Next(5) == 0)
      {
        PlaceMistyPortal(levelMaps[level], rng);
      }
    }    
  }

  static void MoonDaughterSpot(Map map, List<(int, int)> room, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    var sq = room[rng.Next(room.Count)];
    Loc loc = new(dungeonId, level, sq.Item1, sq.Item2);

    Item mdt = ItemFactory.MoonDaughterTile();
    objDb.Add(mdt);
    objDb.SetToLoc(loc, mdt);

    foreach (Loc adj in Util.Adj8Locs(loc))
    {
      if (map.TileAt(adj.Row, adj.Col).Passable())
      {
        Item dark = ItemFactory.Darkness();
        objDb.Add(dark);
        objDb.SetToLoc(adj, dark);
      }
    }
  }

  static void CrimsonKingRoom(Map map, List<(int, int)> room, int dungeonId, int level, GameObjectDB objDb, Rng rng)
  {
    int loR = int.MaxValue, hiR = 0, loC = int.MaxValue, hiC = 0;
    foreach (var sq in room)
    {
      if (sq.Item1 < loR) loR = sq.Item1;
      if (sq.Item1 > hiR) hiR = sq.Item1;
      if (sq.Item2 < loC) loC = sq.Item2;
      if (sq.Item2 > hiC) hiC = sq.Item2;
    }

    int statueR = (loR + hiR) / 2;
    int statueC = (loC + hiC) / 2;
    Loc statueLoc = new(dungeonId, level, statueR, statueC);
    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Glyph = statue.Glyph with { Lit = Colours.BRIGHT_RED, Unlit = Colours.DULL_RED };
    statue.Traits.Add(new DescriptionTrait("a figure in plate armour, gesturing with a broadsword."));
    objDb.SetToLoc(statueLoc, statue);

    room.Remove((statueR, statueC));
    Item item = Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng);
    var (itemR, itemC) = room[rng.Next(room.Count)];
    Loc itemLoc = new(dungeonId, level, itemR, itemC);
    objDb.SetToLoc(itemLoc, item);

    Actor sword = MonsterFactory.Get("dancing sword", objDb, rng);
    sword.Traits.Add(new RegenerationTrait() { Rate = 1, ExpiresOn = ulong.MaxValue, OwnerID = sword.ID, SourceId = sword.ID });
    sword.Traits.Add(new HomebodyTrait() { Loc = statueLoc, Range = 4});
    var (swordR, swordC) = room[rng.Next(room.Count)];
    Loc swordLoc = new(dungeonId, level, swordR, swordC);
    objDb.AddNewActor(sword, swordLoc);
  }

  void AddDecorations(Map[] levelMaps, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    List<Decoration> decorations = Decorations.GenDecorations(factDb, rng);
    HashSet<int> lvlsWithDocs = [];
    
    foreach (var decoration in decorations)
    {
      // We won't use every last generated decoration
      if (rng.NextDouble() < 0.1)
        continue;

      int level = rng.Next(levelMaps.Length);
      Map map = levelMaps[level];

      List<Loc> floors = [];
      for (int r = 0; r < map.Height; r++)
      {
        for (int c = 0; c < map.Width; c++)
        {
          switch (map.TileAt(r, c).Type)
          {
            case TileType.DungeonFloor:
            case TileType.HiddenTrapDoor:
            case TileType.TrapDoor:
            case TileType.TeleportTrap:
            case TileType.HiddenTeleportTrap:
            case TileType.WoodBridge:
            case TileType.Grass:
            case TileType.Dirt:
            case TileType.GreenTree:
              Loc floor = new(DungeonId, level, r, c);
              if (Util.GoodFloorSpace(objDb, floor))
                floors.Add(floor);
              break;
          }
        }
      }

      if (floors.Count == 0)
        continue; // unlikely...

      if (decoration.Type == DecorationType.Statue)
      {
        List<Loc> candidates = [.. floors.Where(l => ValidStatueSq(map, l.Row, l.Col))];

        // Prevent a statue from blocking a hallway
        if (candidates.Count == 0)
          continue;

        Loc loc = candidates[rng.Next(candidates.Count)];
        Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
        statue.Traits.Add(new DescriptionTrait(decoration.Desc.Capitalize()));
        objDb.SetToLoc(loc, statue);
      }
      else if (decoration.Type == DecorationType.Mosaic)
      {
        if (floors.Count == 0)
          continue;

        Loc loc = floors[rng.Next(floors.Count)];
        Landmark mosaic = new(decoration.Desc.Capitalize());
        map.SetTile(loc.Row, loc.Col, mosaic);
      }
      else if (decoration.Type == DecorationType.Fresco)
      {
        PlaceFresco(map, floors, decoration.Desc, rng);
      }
      else if (decoration.Type == DecorationType.ScholarJournal && !lvlsWithDocs.Contains(level))
      {
        PlaceDocument(floors, decoration.Desc, objDb, rng);        
        lvlsWithDocs.Add(level);
      }
    }

    static bool ValidStatueSq(Map map, int r, int c)
    {
      int adjFloorCount = 0;
      foreach (var t in Util.Adj8Sqs(r, c))
      {
        if (map.TileAt(t).Type == TileType.DungeonFloor)
          adjFloorCount++;
      }

      return adjFloorCount > 4;
    }
  }

  static void PlaceFresco(Map map, List<Loc> floors, string frescoText, Rng rng)
  {
    // We want a floor tile that's next to a wall
    List<Loc> candidates = [.. floors.Where(l => WallAdj(l))];

    if (candidates.Count == 0)
      return;

    Loc loc = candidates[rng.Next(candidates.Count)];
    Landmark tile = new(frescoText.Capitalize());
    map.SetTile(loc.Row, loc.Col, tile);

    bool WallAdj(Loc loc)
    {
      foreach (Loc adj in Util.Adj4Locs(loc))
      {
        if (map.TileAt(adj.Row, adj.Col).Type == TileType.DungeonWall)
          return true;
      }

      return false;
    }
  }

  static void PlaceDocument(List<Loc> floors, string documentText, GameObjectDB objDb, Rng rng)
  {    
    string adjective;
    string desc;
    var roll = rng.NextDouble();
    if (roll < 0.5)
    {
      desc = "scroll";
      adjective = "tattered";
    }
    else
    {
      desc = "page";
      adjective = "torn";
    }

    Item doc = new()
    {
      Name = desc, Type = ItemType.Document,
      Glyph = new Glyph('?', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, false)
    };
    doc.Traits.Add(new FlammableTrait());
    doc.Traits.Add(new ScrollTrait());
    doc.Traits.Add(new AdjectiveTrait(adjective));
    doc.Traits.Add(new ReadableTrait(documentText) { OwnerID = doc.ID });

    Loc loc = floors[rng.Next(floors.Count)];
    objDb.Add(doc);
    objDb.SetToLoc(loc, doc);
  }

  static int AdjWalls(Map map, int r, int c)
  {
    int walls = 0;
    foreach (var sq in Util.Adj8Sqs(r, c))
    {
      Tile tile = map.TileAt(sq);
      if (tile.Type == TileType.DungeonWall || tile.Type == TileType.PermWall || tile.Type == TileType.WorldBorder)
        ++walls;
    }

    return walls;
  }

  // This is probably overly complicated...
  static void AddTreasure(GameObjectDB objDb, List<Loc> floors, int levelNum, Rng rng)
  {
    int zorkmidPiles = 3;

    if (levelNum == 0)
    {
      int numItems = rng.Next(1, 4);
      for (int j = 0; j < numItems; j++)
      {
        PlaceItem(Treasure.ItemByQuality(TreasureQuality.Common, objDb, rng));        
      }      
    }
    else if (levelNum == 1)
    {
      int numItems = rng.Next(2, 5);
      for (int j = 0; j < numItems; j++)
      {
        double roll = rng.NextDouble();
        TreasureQuality quality = roll <= 0.9 ? TreasureQuality.Common : TreasureQuality.Uncommon;
        PlaceItem(Treasure.ItemByQuality(quality, objDb, rng));
      }

      PlaceItem(Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng));

      zorkmidPiles = 2;
    }
    else if (levelNum == 2 || levelNum == 3)
    {
      int numItems = rng.Next(3, 6);
      for (int j = 0; j < numItems; j++)
      {
        TreasureQuality quality;
        double roll = rng.NextDouble();
        if (roll <= 0.4)
          quality = TreasureQuality.Common;
        else if (roll <= 0.9)
          quality = TreasureQuality.Uncommon;
        else
          quality = TreasureQuality.Good;
        PlaceItem(Treasure.ItemByQuality(quality, objDb, rng));
      }
    }
    else
    {
      int numItems = rng.Next(3, 5);
      for (int j = 0; j < numItems; j++)
      {
        TreasureQuality quality;
        double roll = rng.NextDouble();
        if (roll <= 0.2)
          quality = TreasureQuality.Common;
        else if (roll <= 0.7)
          quality = TreasureQuality.Uncommon;
        else
          quality = TreasureQuality.Good;
        PlaceItem(Treasure.ItemByQuality(quality, objDb, rng));
      }
    }
    
    for (int j = 0; j < rng.Next(1, zorkmidPiles + 1); j++)
    {
      int minZ = levelNum > 0 ? 10 : 1;
      int maxZ = levelNum > 0 ? 36 : 15;
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      zorkmids.Value = rng.Next(minZ, maxZ);
      PlaceItem(zorkmids);
    }

    if (levelNum > 0)
    {
      for (int i = 0; i < rng.Next(1, 4); i++)
      {
        ItemNames name = Treasure.Consumables[rng.Next(Treasure.Consumables.Count)];
        PlaceItem(ItemFactory.Get(name, objDb));
      }
    }

    void PlaceItem(Item item)
    {
      Loc loc = floors[rng.Next(floors.Count)];
      objDb.SetToLoc(loc, item);
    }
  }

  static void SetPuzzle(Dungeon dungeon, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    int puzzleLevel = dungeon.LevelMaps.Count - 1;
    Map map = dungeon.LevelMaps[puzzleLevel];
    List<PathInfo> paths = LightPuzzleSetup.FindPotential(map);

    // If there are no valid paths I'll probably want to redraw the bottom level

    if (paths.Count != 0)
    {
      Loc targetLoc = LightPuzzleSetup.Create(map, paths, objDb, dungeon.ID, puzzleLevel, rng);
      factDb.Add(new SimpleFact() { Name = "QuestPuzzle1", Value = puzzleLevel.ToString() });

      CreateCellar(targetLoc, dungeon, objDb, rng);
    }
  }

  static void CreateCellar(Loc stairsLoc, Dungeon dungeon, GameObjectDB objDb, Rng rng)
  {
    int cellarHeight = HEIGHT + 2;
    int cellarWidth = WIDTH + 2;
    int levelNum = stairsLoc.Level + 1;

    Map cellar = new(cellarWidth, cellarHeight) 
    { 
      Features = MapFeatures.UndiggableFloor | MapFeatures.NoRandomEncounters 
    };

    for (int r = 0; r < cellarHeight; r++)
    {
      for (int c = 0; c < cellarWidth; c++)
      {
        cellar.SetTile(r, c, TileFactory.Get(TileType.PermWall));
      }
    }

    List<(int, int)> roomOpts = [];
    List<(int, int)> centers = [(-8, -8), (-8, 8), (8, -8), (8, 8)];
    foreach ((int r, int c) in centers)
    {
      int dr = stairsLoc.Row + r;
      int dc = stairsLoc.Col + c;

      if (dr - 3 < 0 || dr + 3 >= cellarHeight || dc - 3 < 0 || dc + 3 >= cellarWidth)
        continue;
      roomOpts.Add((dr, dc));
    }

    // I don't think this can actually happen??
    if (roomOpts.Count == 0)
      throw new Exception("Unable to build cellar room in Initial dungeon");

    List<Loc> floors = [];
    (int roomCenterRow, int roomCenterCol) = roomOpts[rng.Next(roomOpts.Count)];
    for (int r = roomCenterRow - 2; r <= roomCenterRow + 2; r++)
    {
      for (int c = roomCenterCol - 2; c <= roomCenterCol + 2; c++)
      {
        cellar.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
        floors.Add(new Loc(dungeon.ID, levelNum, r, c));
      }
    }
    
    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Traits.Add(new DescriptionTrait("a statue of a greater demon covered in cracks, from which red light streams."));
    statue.Traits.Add(new LightSourceTrait() { FgColour = Colours.BRIGHT_RED, BgColour = Colours.RED_AURA, Radius = 1, OwnerID = statue.ID });
    statue.Traits.Add(new DemonVisageTrait());

    Loc loc = new(dungeon.ID, levelNum, roomCenterRow, roomCenterCol);
    objDb.SetToLoc(loc, statue);

    floors.Remove(loc);
    floors.Shuffle(rng);
    Loc hellHoundLoc = floors[0];
    Actor hh = MonsterFactory.Get("hellhound pup", objDb, rng);
    objDb.AddNewActor(hh, hellHoundLoc);
    
    Item tablet = History.SealingTablet1(objDb);
    List<Loc> locs = [.. Util.Adj8Locs(loc)];
    Loc tabetLoc = locs[rng.Next(locs.Count)];
    objDb.SetToLoc(tabetLoc, tablet);

    int startRow = int.Min(stairsLoc.Row, roomCenterRow);
    for (int r = startRow; r < startRow + 8; r++)    
      cellar.SetTile(r, roomCenterCol, TileFactory.Get(TileType.DungeonFloor));
    if (roomCenterRow <  stairsLoc.Row)
    {
      cellar.SetTile(stairsLoc.Row - 2, roomCenterCol - 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row - 2, roomCenterCol - 1);
      cellar.SetTile(stairsLoc.Row - 2, roomCenterCol + 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row - 2, roomCenterCol + 1);      
      cellar.SetTile(stairsLoc.Row - 4, roomCenterCol - 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row - 4, roomCenterCol - 1);
      cellar.SetTile(stairsLoc.Row - 4, roomCenterCol + 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row - 4, roomCenterCol + 1);
    }
    else
    {
      cellar.SetTile(stairsLoc.Row + 2, roomCenterCol - 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row + 2, roomCenterCol - 1);
      cellar.SetTile(stairsLoc.Row + 2, roomCenterCol + 1, TileFactory.Get(TileType.DungeonFloor));      
      SetStatue(stairsLoc.Row + 2, roomCenterCol + 1);
      cellar.SetTile(stairsLoc.Row + 4, roomCenterCol - 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row + 4, roomCenterCol - 1);
      cellar.SetTile(stairsLoc.Row + 4, roomCenterCol + 1, TileFactory.Get(TileType.DungeonFloor));
      SetStatue(stairsLoc.Row + 4, roomCenterCol + 1);
    }

    int startCol = int.Min(stairsLoc.Col, roomCenterCol);
    for (int c = startCol; c < startCol + 8; c++)
      cellar.SetTile(stairsLoc.Row, c, TileFactory.Get(TileType.DungeonFloor));

    Upstairs upStairs = new("") { Destination = stairsLoc };
    cellar.SetTile(stairsLoc.Row, stairsLoc.Col, upStairs);

    dungeon.AddMap(cellar);
    
    void SetStatue(int row, int col)
    {
      Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
      statue.Traits.Add(new DescriptionTrait("a statue depicting a demonic form."));
      int levelNum = stairsLoc.Level + 1;
      Loc loc = new(dungeon.ID, levelNum, row, col);
      objDb.SetToLoc(loc, statue);
    }
  }

  static void SetBoss(Dungeon dungeon, GameObjectDB objDb, FactDb factDb, string earlyDenizen, Rng rng)
  {
    int bossLevelNum = dungeon.LevelMaps.Count - 1;
    Map bossLevel = dungeon.LevelMaps[bossLevelNum];

    if (earlyDenizen == "kobold")
    {
      Actor ks = MonsterFactory.Get("kobold supervisor", objDb, rng);
      ks.Name = "the Kobold Regional Manager";
      ks.Traits.Add(new NamedTrait());
      ks.Traits.Add(new ImmunityTrait() { ExpiresOn = ulong.MaxValue, SourceId = ks.ID, Type = DamageType.Fire });
      ks.Stats[Attribute.AttackBonus] = new Stat(6);
      ks.Stats[Attribute.HP] = new Stat(35);

      var sq = bossLevel.RandomTile(FindFloor, rng);
      Loc loc = new(dungeon.ID, bossLevelNum, sq.Item1, sq.Item2);
      objDb.AddNewActor(ks, loc);
      factDb.Add(new SimpleFact() { Name = "First Boss", Value = "the Kobold Regional Manager" });

      List<Loc> options = [];
      // Where shall we put the pet dragon?
      for (int r = -2; r <= 2; r++)
      {
        for (int c = -2; c <= 2; c++)
        {
          if (!bossLevel.InBounds(loc.Row + r, loc.Col + c))
            continue;
          Loc opt = loc with { Row = loc.Row + r, Col = loc.Col + c };
          if (bossLevel.TileAt(opt.Row, opt.Col).Passable() && !objDb.Occupied(opt))
            options.Add(opt);
        }
      }
      if (options.Count > 0)
      {
        // I guess if there's no spot for the dragon then the supervisor doesn't have a pet :O
        Loc wyrmLoc = options[rng.Next(options.Count)];
        Actor wyrm = MonsterFactory.Get("wyrmling", objDb, rng);
        objDb.AddNewActor(wyrm, wyrmLoc);
      }
    }
    else if (earlyDenizen == "goblin")
    {
      Actor gg = MonsterFactory.Get("the Great Goblin", objDb, rng);
      var sq = bossLevel.RandomTile(FindFloor, rng);
      Loc loc = new(dungeon.ID, bossLevelNum, sq.Item1, sq.Item2);
      objDb.AddNewActor(gg, loc);
      factDb.Add(new SimpleFact() { Name = "First Boss", Value = "the Great Goblin" });
    }

    static bool FindFloor(Tile t) => t.Type == TileType.DungeonFloor;
  }

  void AddWidowerBeau(GameObjectDB objDb, Map level, int levelNum, FactDb factDb, Rng rng)
  {
    (int, int) sq = level.RandomTile(t => t.Type == TileType.DungeonFloor, rng);
    Loc loc = new(DungeonId, levelNum, sq.Item1, sq.Item2);

    for (int j = 0; j < 3; j++)
    {
      Item torch = ItemFactory.Get(ItemNames.TORCH, objDb);
      objDb.SetToLoc(loc, torch);
    }
    if (rng.NextDouble() < 0.25)
    {
      Item poh = ItemFactory.Get(ItemNames.POTION_HEALING, objDb);
      objDb.SetToLoc(loc, poh);
    }
    if (rng.NextDouble() < 0.25)
    {
      Item antidote = ItemFactory.Get(ItemNames.ANTIDOTE, objDb);
      objDb.SetToLoc(loc, antidote);
    }
    if (rng.NextDouble() < 0.25)
    {
      Item blink = ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb);
      objDb.SetToLoc(loc, blink);
    }

    // add trinket
    Item trinket = new()
    {
      Name = "tin locket", Type = ItemType.Trinket,
      Value = 1, Glyph = new Glyph('"', Colours.GREY, Colours.LIGHT_GREY, Colours.BLACK, false)
    };
    trinket.Traits.Add(new EquipableTrait());

    GrantsTrait buff = rng.Next(3) switch
    {
      0 => new GrantsTrait() { TraitsGranted = ["StatBuff#0#max#Strength#1#item"] },
      1 => new GrantsTrait() { TraitsGranted = ["StatBuff#0#max#Dexterity#1#item"] },
      _ => new GrantsTrait() { TraitsGranted = ["StatBuff#0#max#Constitution#1#item"] },
    };
    trinket.Traits.Add(buff);

    objDb.Add(trinket);
    objDb.SetToLoc(loc, trinket);

    string text = "Scratched into the stone: if only I'd managed to level up.";
    Landmark tile = new(text);
    level.SetTile(sq, tile);

    // Generate an actor for the fallen adventurer so I can store their 
    // name and such in the objDb. Maybe sometimes they'll be an actual
    // ghost?
    NameGenerator ng = new(rng, Util.NamesFile);
    string adventurerName = ng.GenerateName(rng.Next(5, 12)).Capitalize();
    factDb.Add(new SimpleFact() { Name = "WidowerBeau", Value = adventurerName });
    factDb.Add(new SimpleFact() { Name = "TrinketId", Value = trinket.ID.ToString() });
  }

  static void GnomeMerchant(Map[] levels, int dungeonId, Rng rng, GameObjectDB objDb)
  {
    int level = -1;
    for (int j = 2; j < levels.Length; j++)
    {
      if (rng.NextDouble() <= 1.20)
      {
        level = j;
        break;
      }
    }

    if (level == -1)
      return;

    Mob flinFlon = new()
    {
      Name = "Flin Flon",
      Appearance = "A mildly dishevelled gnome with a sparse beard. He's carrying a heavy satchel.",
      Glyph = new Glyph('@', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false)
    };
    flinFlon.Stats[Attribute.HP] = new Stat(50);
    flinFlon.Stats[Attribute.ShopInvoice] = new Stat(0);
    flinFlon.Traits.Add(new VillagerTrait());
    flinFlon.Traits.Add(new NamedTrait());
    flinFlon.Traits.Add(new IntelligentTrait());
    flinFlon.Traits.Add(new DialogueScriptTrait() { ScriptFile = "gnome_merchant.txt" });
    flinFlon.SetBehaviour(new GnomeMerchantBehaviour());
    flinFlon.Traits.Add(new BehaviourTreeTrait() { Plan = "SimpleRandomPlan" });
    flinFlon.Traits.Add(new NumberListTrait() { Name = "ShopSelections", Items = [] });
    LeaveDungeonTrait ldt = new() { SourceId = flinFlon.ID };
    objDb.EndOfRoundListeners.Add(ldt);
    flinFlon.Traits.Add(ldt);

    flinFlon.Inventory = new Inventory(flinFlon.ID, objDb);
    int numItems = rng.Next(3, 6);
    while (numItems > 0)
    {
      Item item = Treasure.ItemByQuality(TreasureQuality.Good, objDb, rng);
      if (item.Type == ItemType.Zorkmid)
        continue;
      flinFlon.Inventory.Add(item, flinFlon.ID);

      --numItems;
    }

    List<Loc> floors = levels[level].ClearFloors(dungeonId, level, objDb);
    Loc startLoc = floors[rng.Next(floors.Count)];
    objDb.AddNewActor(flinFlon, startLoc);

    for (int j = 0; j < 3; j++)
    {
      int flyerLevel = rng.Next(0, level + 1);
      floors = levels[flyerLevel].ClearFloors(dungeonId, flyerLevel, objDb);
    
      Item flyer = new()
      {
        Name = "neatly printed flyer",
        Type = ItemType.Document,
        Glyph = new Glyph('?', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, false)
      };
      flyer.Traits.Add(new FlammableTrait());
      flyer.Traits.Add(new ScrollTrait());
      objDb.Add(flyer);

      string txt = "Flin Flon's Supplies and Mercantile!\n\nCome find us for a selection of discounted adventuring gear you may literally not survive without!!\n\nSale! Sale! Sale!";
      ReadableTrait rt = new(txt) { OwnerID = flyer.ID };
      flyer.Traits.Add(rt);

      Loc loc = floors[rng.Next(floors.Count)];      
      objDb.SetToLoc(loc, flyer);
    }
  }
}
