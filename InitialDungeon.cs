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

class InitialDungeonBuilder(int dungeonID, (int, int) entrance, string mainOccupant) : DungeonBuilder
{
  const int HEIGHT = 30;
  const int WIDTH = 70;
  int DungeonId { get; set; } = dungeonID;
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
      
    dungeon.LevelMaps[numOfLevels - 1].DiggableFloor = false;

    List<int> riverLevels = [];
    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      // Maybe add a river/chasm to the level?
      if (rng.Next(4) == 0)
      {
        Map? nextLevel = null;
        TileType riverType = TileType.DeepWater;
        if (levelNum < numOfLevels - 1 && rng.Next(3) == 0)
        {
          riverType = TileType.Chasm;
          nextLevel = levels[levelNum + 1];
        }

        AddRiverToLevel(riverType, levels[levelNum], nextLevel, levelNum, HEIGHT, WIDTH, DungeonId, objDb, rng);
        riverLevels.Add(levelNum);
      }
    }

    TidyOrphanedDoors(DungeonId, levels, objDb, rng);

    SetStairs(DungeonId, levels, HEIGHT, WIDTH, numOfLevels, Entrance, dungeon.Descending, rng);

    foreach (int levelNum in riverLevels)
    {
      RiverQoLCheck(levels[levelNum], DungeonId, levelNum, objDb, rng);
    }

    for (int levelNum = 0; levelNum < levels.Length; levelNum++)
    {
      Treasure.AddTreasureToDungeonLevel(objDb, levels[levelNum], DungeonId, levelNum, rng);
      SetTraps(levels[levelNum], DungeonId, levelNum, numOfLevels, rng);

      // Maybe add an illusion/trap
      if (levelNum < numOfLevels - 1)
      {
        // We don't want to make these tooooooo common
        if (rng.NextDouble() > 0.1)
          continue;

        AddBaitIllusion(levels[levelNum], DungeonId, levelNum, objDb, rng);
      }

      if (rng.Next(4) == 0)
        TunnelCarver.MakeCollapsedTunnel(DungeonId, levelNum, levels[levelNum], objDb, rng);
    }

    PopulateDungeon(dungeon, rng, objDb);

    // Add a couple of guaranteed good items to dungeon
    AddGoodItemToLevel(levels[1], DungeonId, 1, rng, objDb);
    AddGoodItemToLevel(levels[3], DungeonId, 3, rng, objDb);

    int fallenAdventurer = rng.Next(1, numOfLevels);
    AddFallenAdventurer(objDb, levels[fallenAdventurer], fallenAdventurer, rng);

    if (factDb.FactCheck("EarlyDenizen") is SimpleFact earlyOcc)
    {
      //SetBoss(dungeon, objDb, factDb, earlyOcc.Value, rng);
    }

    // The idol 'puzzle' gives a guaranteed Potion of Hardiness, but I think 
    // I'd like to have a few different mini-puzzles for variety
    int altarLevel = rng.Next(0, numOfLevels);
    IdolAltarMaker.MakeAltar(DungeonId, levels, objDb, factDb, rng, altarLevel);

    SetPuzzle(dungeon, objDb, factDb, rng);

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
    bool captive = false;
    string denizen = factDb.FactCheck("EarlyDenizen") is SimpleFact denizenFact ? denizenFact.Value : "";
    double chanceOfDesecratedAltar = 0.25;

    // Can we create any rooms-within-rooms?
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
          Rooms.RoomInRoom(map, corners, rng);

          List<(int, int)> nonFloors = [];
          foreach (var sq in rooms[id])
          {
            if (map.TileAt(sq).Type != TileType.DungeonFloor)
              nonFloors.Add(sq);
          }
          foreach (var sq in nonFloors)
          {
            rooms[id].Remove(sq);
          }

          break;
        }

        for (var i = 0; i < rooms.Count; i++)
        {
          if (Rooms.PotentialVault(map, rooms[i]))
            potentialVaults.Add(i);
        }
      }

      if (potentialVaults.Count > 0 && rng.NextDouble() < 0.2)
      {
        int vaultId = potentialVaults[rng.Next(potentialVaults.Count)];
        HashSet<(int, int)> vault = [.. rooms[vaultId]];
        var (doorR, doorC) = Vaults.FindExit(map, vault);        
        roomIds.Remove(vaultId);
        
        // We could have found a false vault. Likely a spot separate from
        // the rest of the dungoen by a river or chasm
        if (doorR >= 0 && doorC >= 0)
        {
          Vaults.CreateVault(map, DungeonId, level, doorR, doorC, vault, rng, objDb, factDb);
        }
      }

      if (level < levelMaps.Length - 1 && rng.NextDouble() < 0.2)
      {
        int roomId = rng.Next(rooms.Count);

        if (level == 0 && IsEntranceHall(levelMaps[level], rooms[roomId]))
        {
          continue;
        }

        switch (rng.Next(4))
        {
          case 0:
            Rooms.ChasmTrapRoom(levelMaps, rng, DungeonId, level, rooms[roomId], objDb);
            break;
          case 1:
            Rooms.TriggerChasmRoom(levelMaps, rng, DungeonId, level, rooms[roomId], objDb);
            break;
          default:
            Rooms.BasicChasmRoom(levelMaps, rng, DungeonId, level, rooms[roomId], objDb);
            break;
        }

        roomIds.Remove(roomId);
      }

      // These aren't rooms but decorations/features
      if (level > 0 && rng.NextDouble() < 0.25)
      {
        int roomId = rng.Next(roomIds.Count);
        Rooms.CampRoom(rooms[roomId], DungeonId, level, factDb, objDb, rng);
        roomIds.Remove(roomId);
      }

      if (factDb.Ruler.Type == OGRulerType.ElfLord && rng.NextDouble() < 0.15)
      {
        int roomId = rng.Next(rooms.Count);
        Rooms.Orchard(levelMaps[level], rooms[roomId], DungeonId, level, factDb, objDb, rng);
        rooms.RemoveAt(roomId);
      }

      // Not technically a room but...
      if (level > 0 && rng.NextDouble() < 0.2 && !captive)
      {
        captive = true;
        CaptiveFeature.Create(DungeonId, level, levelMaps[level], objDb, factDb, rng);
      }
      else if (rng.NextDouble() < 0.15)
      {
        // If there's no prisoner on the level, give a small chance of there
        // being a blood-stained altar. (Mainly because I don't want to bother
        // checking against the possibility of two altars)
        List<(int, int)> floors = [];
        foreach (var (r, c) in levelMaps[level].SqsOfType(TileType.DungeonFloor))
        {
          if (AdjWalls(levelMaps[level], r, c) >= 3)
            continue;
          floors.Add((r, c));
        }

        (int, int) altarSq = floors[rng.Next(floors.Count)];
        Loc altarLoc = new(DungeonId, level, altarSq.Item1, altarSq.Item2);
        Item altar = ItemFactory.Get(ItemNames.STONE_ALTAR, objDb);
        altar.Glyph = new Glyph('∆', Colours.DULL_RED, Colours.BROWN, Colours.BLACK, false);
        altar.Traits.Add(new MolochAltarTrait());
        objDb.SetToLoc(altarLoc, altar);
      }
            
      if (rng.NextDouble() < chanceOfDesecratedAltar)
      {
        int roomId = rng.Next(rooms.Count);
        List<(int, int)> room = rooms[roomId];
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

    // Generate the cellar level
    Map cellar = new(cellarWidth, cellarHeight)
    {
      DiggableFloor = false
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

    (int roomCenterRow, int roomCenterCol) = roomOpts[rng.Next(roomOpts.Count)];
    for (int r = roomCenterRow - 2; r <= roomCenterRow + 2; r++)
    {
      for (int c = roomCenterCol - 2; c <= roomCenterCol + 2; c++)
      {
        cellar.SetTile(r, c, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
    statue.Traits.Add(new DescriptionTrait("A statue of a greater demon covered in cracks, from which red light streams."));
    statue.Traits.Add(new LightSourceTrait() { FgColour = Colours.BRIGHT_RED, BgColour = Colours.RED_AURA, Radius = 1, OwnerID = statue.ID });
    statue.Traits.Add(new DemonVisageTrait());

    int levelNum = stairsLoc.Level + 1;
    Loc loc = new(dungeon.ID, levelNum, roomCenterRow, roomCenterCol);
    objDb.SetToLoc(loc, statue);

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
      SetStatue(stairsLoc.Row + 5, roomCenterCol - 1);
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
      statue.Traits.Add(new DescriptionTrait("A statue depicting a demonic form."));
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
      var sq = bossLevel.RandomTile(TileType.DungeonFloor, rng);
      var loc = new Loc(dungeon.ID, bossLevelNum, sq.Item1, sq.Item2);
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
      var sq = bossLevel.RandomTile(TileType.DungeonFloor, rng);
      var loc = new Loc(dungeon.ID, bossLevelNum, sq.Item1, sq.Item2);
      objDb.AddNewActor(gg, loc);
      factDb.Add(new SimpleFact() { Name = "First Boss", Value = "the Great Goblin" });
    }
  }

  void AddFallenAdventurer(GameObjectDB objDb, Map level, int levelNum, Rng rng)
  {
    (int, int) sq = level.RandomTile(TileType.DungeonFloor, rng);
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
      Name = "tin locket",
      Type = ItemType.Trinket,
      Value = 1,
      Glyph = new Glyph('"', Colours.GREY, Colours.LIGHT_GREY, Colours.BLACK, false)
    };
    objDb.Add(trinket);
    objDb.SetToLoc(loc, trinket);

    string text = "Scratched into the stone: if only I'd managed to level up.";
    Landmark tile = new(text);
    level.SetTile(sq, tile);

    // Generate an actor for the fallen adventurer so I can store their 
    // name and such in the objDb. Maybe sometimes they'll be an actual
    // ghost?
    NameGenerator ng = new(rng, Util.NamesFile);
    Mob adventurer = new()
    {
      Name = ng.GenerateName(rng.Next(5, 12))
    };
    adventurer.Traits.Add(new FallenAdventurerTrait());
    adventurer.Traits.Add(new OwnsItemTrait() { ItemID = trinket.ID });
    objDb.Add(adventurer);
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
