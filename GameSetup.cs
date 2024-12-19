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

enum SetupType
{
  NewGame, LoadGame, Quit, Tutorial
}

class GameLoader(UserInterface ui)
{
  UserInterface UI { get; set; } = ui;

  string LoadGameScreen()
  {
    UI.SqsOnScreen = new Sqr[UserInterface.ScreenHeight, UserInterface.ScreenWidth];
    UI.ClearSqsOnScreen();

    bool noGame = false;
    string s = "Please choose a saved game to load:";
    for (int i = 0; i < s.Length; i++)
    {
      UI.SqsOnScreen[1, 1 + i] = new Sqr(Colours.WHITE, Colours.BLACK, s[i]);
    }

    string savePath = "";
    int selected = 0;
    do
    {
      List<SaveFileInfo> files = Serialize.GetSavedGames();
      if (files.Count == 0)
      {
        s = "Uh-oh, you don't seem to have any saved games!";
        for (int i = 0; i < s.Length; i++)
          UI.SqsOnScreen[3, 1 + i] = new Sqr(Colours.WHITE, Colours.BLACK, s[i]);
        noGame = true;
      }
      else
      {
        int width = files.Select(f => f.CharName.Length).Max() + 1;        
        for (int i = 0; i < files.Count; i++)
        {
          string charName = files[i].CharName.PadRight(width);
          Colour bg = i == selected ? Colours.HILITE : Colours.BLACK;
          for (int j = 0; j < charName.Length; j++)
            UI.SqsOnScreen[3 + i, 1 + j] = new Sqr(Colours.WHITE, bg, charName[j]);
        }
      }

      Thread.Sleep(30);
      char ch = UI.GetKeyInput();
      if (files.Count > 0 && ch == 'j')
      {
        selected = (++selected) % files.Count;
      }
      else if (files.Count > 0 && ch == 'k')
      {
        selected = selected > 0 ? selected - 1 : files.Count - 1;        
      }
      else if (files.Count > 0 && (ch == '\n' || ch == '\r'))
      {
        savePath = files[selected].Path;
        break;
      }
      else if (ch == Constants.ESC || (noGame && ch == ' '))
      {
        throw new GameNotLoadedException();
      }
      
      if (files.Count > 0)
      {
        List<Sqr> preview = Serialize.FetchSavePreview(files[selected].Path);
        int j = 0;
        for (int r = 0; r < 11; r++)
        {
          for (int c = 0; c < 11; c++)
          {
            UI.SqsOnScreen[3 + r, 31 + c] = preview[j++];
          }
        }
      }

      UI.UpdateDisplay(null);
    }
    while (true);

    UI.SqsOnScreen = new Sqr[UserInterface.ViewHeight, UserInterface.ViewWidth];
    UI.ClearSqsOnScreen();

    return savePath;
  }

  public GameState? Load(Options options)
  {
    try
    {
      string path = LoadGameScreen();
      if (path == "")
        throw new GameNotLoadedException();

      GameState? gameState = Serialize.LoadSaveGame(path, options, UI);
      gameState.Player = gameState.ObjDb.FindPlayer() ?? throw new Exception("No player :O");
      gameState.ObjDb.AddToLoc(gameState.Player.Loc, gameState.Player);
      gameState.PrepareFieldOfView();
      gameState.RecentlySeenMonsters.Add(gameState.Player.ID);

      File.Delete(path);
      
      return gameState;
    }
    catch (GameQuitException)
    {
      return null;
    }

    // Need to gracefully handle missing/corrupt/unparseable save game
  }
}

class CampaignCreator(UserInterface ui)
{
  UserInterface UI { get; set; } = ui;

  string QueryPlayerName()
  {
    string playerName;
    do
    {
      playerName = UI.BlockingGetResponse("Who are you?", 30, new PlayerNameInputChecker()).Trim();      
    }
    while (playerName.Length == 0);

    return playerName;    
  }

  static void SetItemIDInfo(Random rng)
  {
    List<string> wandMaterials = ["maple", "oak", "birch", "ebony", "tin", "glass", "iron", "silver", "balsa"];

    Item.IDInfo = [];

    int j = rng.Next(wandMaterials.Count);
    Item.IDInfo.Add("wand of magic missiles", new ItemIDInfo(false, $"{wandMaterials[j]} wand"));
    wandMaterials.RemoveAt(j);

    j = rng.Next(wandMaterials.Count);
    Item.IDInfo.Add("wand of swap", new ItemIDInfo(false, $"{wandMaterials[j]} wand"));
    wandMaterials.RemoveAt(j);

    j = rng.Next(wandMaterials.Count);
    Item.IDInfo.Add("wand of heal monster", new ItemIDInfo(false, $"{wandMaterials[j]} wand"));
    wandMaterials.RemoveAt(j);

    j = rng.Next(wandMaterials.Count);
    Item.IDInfo.Add("wand of fireballs", new ItemIDInfo(false, $"{wandMaterials[j]} wand"));
    wandMaterials.RemoveAt(j);

    j = rng.Next(wandMaterials.Count);
    Item.IDInfo.Add("wand of frost", new ItemIDInfo(false, $"{wandMaterials[j]} wand"));

    List<string> ringMaterials = ["silver", "iron", "ruby", "diamond", "gold", "jade", "wood"];
    j = rng.Next(ringMaterials.Count);
    Item.IDInfo.Add("ring of protection", new ItemIDInfo(false, $"{ringMaterials[j]} ring"));
    ringMaterials.RemoveAt(j);

    j = rng.Next(ringMaterials.Count);
    Item.IDInfo.Add("ring of aggression", new ItemIDInfo(false, $"{ringMaterials[j]} ring"));
    ringMaterials.RemoveAt(j);

    j = rng.Next(ringMaterials.Count);
    Item.IDInfo.Add("ring of adornment", new ItemIDInfo(false, $"{ringMaterials[j]} ring"));
    ringMaterials.RemoveAt(j);

    j = rng.Next(ringMaterials.Count);
    Item.IDInfo.Add("ring of fraility", new ItemIDInfo(false, $"{ringMaterials[j]} ring"));
    ringMaterials.RemoveAt(j);

    List<string> talismanDesc = ["jeweled scarab", "bone amulet", "clay fetish", "mummified finger"];
    j = rng.Next(talismanDesc.Count);
    Item.IDInfo.Add("talisman of circumspection", new ItemIDInfo(false, talismanDesc[j]));
    talismanDesc.RemoveAt(j);
  }

  static bool StartSq(Map map, int row, int col)
  {
    return map.TileAt(row, col).Type switch
    {
      TileType.Grass or TileType.Sand or
      TileType.GreenTree or TileType.RedTree or
      TileType.YellowTree or TileType.OrangeTree or
      TileType.Conifer or TileType.Dirt => true,
      _ => false,
    };
  }

  static (int, int) PickStartLoc(Map map, Town town, Random rng)
  {
    List<(int, int)> opts = [];

    for (int r = town.Row - 5; r < town.Row; r++)
    {
      for (int c = town.Col; c < town.Col + town.Width; c++)
      {
        if (map.InBounds(r, c) && StartSq(map, r, c))
          opts.Add((r, c));
      }
    }

    // We'll pick the well as the goal for checking if there's a valid path
    // from the start location
    Loc goal = Loc.Nowhere;
    for (int r = town.Row; r < town.Row + town.Height && goal == Loc.Nowhere; r++)
    {
      for (int c = town.Col; c < town.Col + town.Width; c++)
      {
        if (map.TileAt(r, c).Type == TileType.Well)
        {
          goal = new Loc(0, 0, r, c);
          break;
        }        
      }
    }

    Dictionary<TileType, int> costs = [];
    costs.Add(TileType.Grass, 1);
    costs.Add(TileType.Sand, 1);
    costs.Add(TileType.Dirt, 1);
    costs.Add(TileType.Bridge, 1);
    costs.Add(TileType.GreenTree, 1);
    costs.Add(TileType.RedTree, 1);
    costs.Add(TileType.OrangeTree, 1);
    costs.Add(TileType.YellowTree, 1);
    costs.Add(TileType.Conifer, 1);
    costs.Add(TileType.StoneFloor, 1);
    costs.Add(TileType.WoodFloor, 1);
    costs.Add(TileType.Well, 1);
    
    while (opts.Count > 0)
    {
      int i = rng.Next(opts.Count);
      var (r, c) = opts[i];

      Loc loc = new(0, 0, r, c);
      var path = AStar.FindPath(map, loc, goal, costs);
      if (path.Count > 0)
        return (r, c);

      opts.RemoveAt(i);
    }

    return (-1, -1);
  }

  static bool InTown(int row, int col, Town town) =>
      row >= town.Row && row <= town.Row + town.Height && col >= town.Col && col <= town.Col + town.Width;

  static int CostForRoadBuilding(Tile tile) => tile.Type switch
  {
    TileType.HWindow or TileType.VWindow or TileType.StoneWall or TileType.WoodWall => 1,
    _ => tile.Passable() ? 1 : int.MaxValue
  };

  static void DrawOldRoad(Map map, HashSet<(int, int)> region, int overWorldWidth, (int, int) entrance, Town town, Random rng)
  {
    int tcRow = town.Row + town.Height / 2;
    int tcCol = town.Col + town.Width / 2;

    var dmap = new DijkstraMap(map, [], overWorldWidth, overWorldWidth, true);
    var tt = map.TileAt(tcRow, tcCol);

    dmap.Generate(CostForRoadBuilding, (tcRow, tcCol), 257);
    var road = dmap.ShortestPath(entrance.Item1, entrance.Item2);

    double draw = 1.0;
    double delta = 2.0 / road.Count;

    foreach (var sq in road.Skip(1))
    {
      if (InTown(sq.Item1, sq.Item2, town))
        break;

      if (map.TileAt(sq).Type == TileType.Water)
        map.SetTile(sq, TileFactory.Get(TileType.Bridge));
      else if (rng.NextDouble() < draw)
        map.SetTile(sq, TileFactory.Get(TileType.StoneRoad));
      draw -= delta;
      if (draw < 0.03)
        draw = 0.03;
    }
  }

  // I want to pick a square that nestled into the mountains, so we'll pick
  // a square surrounded by at least 3 mountains
  static (int, int) PickDungeonEntrance(Map map, HashSet<(int, int)> region, Town town, Random rng)
  {
    int tcRow = town.Row + town.Height / 2;
    int tcCol = town.Col + town.Width / 2;

    List<(int, int, int)> options = [];
    foreach (var sq in region)
    {
      bool candidate = false;
      switch (map.TileAt(sq).Type)
      {
        case TileType.Grass:
        case TileType.Dirt:
        case TileType.GreenTree:
        case TileType.RedTree:
        case TileType.YellowTree:
        case TileType.OrangeTree:
        case TileType.Sand:
          candidate = true;
          break;
      }
      if (!candidate)
        continue;

      if (Util.CountAdjTileType(map, sq.Item1, sq.Item2, TileType.Mountain) >= 4)
      {
        int d = Util.Distance(sq.Item1, sq.Item2, tcRow, tcCol);
        options.Add((sq.Item1, sq.Item2, d));
      }
    }
    options = [.. options.OrderBy(sq => sq.Item3)];

    if (options.Count == 0)
      throw new CouldNotPlaceDungeonEntranceException();

    var entrance = options[rng.Next(options.Count / 4)];

    return (entrance.Item1, entrance.Item2);
  }

  // This probably belongs in DungeonBuilder
  static void AddGargoyle(Random rng, GameObjectDB objDb, Dungeon dungeon, int level)
  {
    var glyph = new Glyph('&', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK);
    var gargoyle = new Mob()
    {
      Name = "gargoyle",
      Recovery = 1.0,
      MoveStrategy = new SimpleFlightMoveStrategy(),
      Glyph = glyph
    };
    gargoyle.SetBehaviour(new DisguisedMonsterBehaviour());

    gargoyle.Stats.Add(Attribute.HP, new Stat(40));
    gargoyle.Stats.Add(Attribute.AttackBonus, new Stat(4));
    gargoyle.Stats.Add(Attribute.AC, new Stat(15));
    gargoyle.Stats.Add(Attribute.Strength, new Stat(1));
    gargoyle.Stats.Add(Attribute.Dexterity, new Stat(1));

    gargoyle.Actions.Add(new MobMeleeTrait()
    {
      MinRange = 1,
      MaxRange = 1,
      DamageDie = 5,
      DamageDice = 2,
      DamageType = DamageType.Blunt
    });

    gargoyle.Stats[Attribute.InDisguise] = new Stat(1);

    var disguise = new DisguiseTrait()
    {
      Disguise = glyph,
      TrueForm = new Glyph('G', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, Colours.BLACK),
      DisguiseForm = "statue"
    };
    gargoyle.Traits.Add(disguise);
    gargoyle.Traits.Add(new FlyingTrait());
    gargoyle.Traits.Add(new ResistPiercingTrait());
    gargoyle.Traits.Add(new ResistSlashingTrait());

    var sq = dungeon.LevelMaps[level].RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(dungeon.ID, level, sq.Item1, sq.Item2);
    objDb.AddNewActor(gargoyle, loc);

    var adj = Util.Adj4Locs(loc).ToList();
    if (adj.Count > 0)
    {
      var pedestalLoc = adj[rng.Next(adj.Count)];
      var pedetal = new Landmark("A stone pedestal.");
      dungeon.LevelMaps[level].SetTile(pedestalLoc.Row, pedestalLoc.Col, pedetal);
    }
  }

  static void SetLevel5MiniBoss(Dungeon dungeon, GameObjectDB objDb, FactDb factDb, string earlyDenizen, Random rng)
  {
    int bossLevelNum = 4;
    Map bossLevel = dungeon.LevelMaps[bossLevelNum];

    if (earlyDenizen == "kobold")
    {
      Actor ks = MonsterFactory.Get("kobold supervisor", objDb, rng);
      ks.Name = "the Kobold Regional Manager";
      ks.Traits.Add(new NamedTrait());
      var sq = bossLevel.RandomTile(TileType.DungeonFloor, rng);
      var loc = new Loc(dungeon.ID, bossLevelNum, sq.Item1, sq.Item2);
      objDb.AddNewActor(ks, loc);
      factDb.Add(new SimpleFact() { Name = "Level 5 Boss", Value = "the Kobold Regional Manager" });

      List<Loc> options = [];
      // Where shall we put the pet dragon?
      for (int r = -2; r <= 2; r++)
      {
        for (int  c = -2; c <= 2; c++)
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
      factDb.Add(new SimpleFact() { Name = "Level 5 Boss", Value = "the Great Goblin" });
    }   
  }

  static void PrinceOfRats(Dungeon dungeon, GameObjectDB objDb, Random rng)
  {
    var prince = BossFactory.Get("Prince of Rats", rng);
    var sq = dungeon.LevelMaps[4].RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(dungeon.ID, 4, sq.Item1, sq.Item2);
    objDb.AddNewActor(prince, loc);

    // Drop a hint somewhere about the Prince of Rat's immunity
    var immunity = prince.Traits.OfType<ImmunityTrait>().First();
    string desc;
    if (immunity.Type == DamageType.Slashing)
      desc = "axe";
    else if (immunity.Type == DamageType.Blunt)
      desc = "mace";
    else
      desc = "spear";

    var landmark = new Landmark($"Scrawled in blood: the Prince of Rats is a devil! My {desc} was useless against him!");
    var level = rng.Next(0, 4);
    sq = dungeon.LevelMaps[level].RandomTile(TileType.DungeonFloor, rng);
    Console.WriteLine($"{level} {sq}");
    dungeon.LevelMaps[level].SetTile(sq, landmark);
  }

  // This is very temporary/early code since eventually dungeons will need to
  // know how to populate themselves (or receive a populator class of some 
  // sort) because monsters will spawn as the player explores
  private static void PopulateDungeon(Random rng, GameObjectDB objDb, FactDb factDb, Dungeon dungeon, int maxDepth, List<MonsterDeck> monsterDecks)
  {
    // Temp: generate monster decks and populate the first two levels of the dungeon.
    // I'll actually want to save the decks for reuse as random monsters are added
    // in, but I'm not sure where they should live. I guess maybe in the Map structure,
    // which has really come to represent a dungeon level
    for (int lvl = 0; lvl < maxDepth; lvl++)
    {
      for (int j = 0; j < rng.Next(8, 13); j++)
      {
        int monsterLvl = lvl;
        if (lvl > 0 && rng.NextDouble() > 0.8)
        {
          monsterLvl = rng.Next(lvl);
        }

        var deck = monsterDecks[monsterLvl];
        var sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
        var loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);
        if (deck.Indexes.Count == 0)
          deck.Reshuffle(rng);
        string m = deck.Monsters[deck.Indexes.Dequeue()];

        // Some monsters are a bit special and take a bit of extra work
        Actor monster = MonsterFactory.Get(m, objDb, rng);
        monster.Loc = loc;
        if (rng.NextDouble() < 0.8)
          monster.Traits.Add(new SleepingTrait());
        objDb.Add(monster);
        objDb.AddToLoc(loc, monster);
      }
    }
  }

  static (Campaign, int, int) BeginNewCampaign(Random rng, GameObjectDB objDb)
  {
    Campaign campaign;
    Dungeon wilderness;
    Map wildernessMap;
    Town town;
    int startR, startC;
    int wildernessWidth = 129;

    do
    {
      try
      {
        campaign = new Campaign();
        wilderness = new Dungeon(0, "You draw a deep breath of fresh air.");
        var wildernessGenerator = new Wilderness(rng, wildernessWidth);
        wildernessMap = wildernessGenerator.DrawLevel();

        // Redraw map if there aren't enough mountains
        int mountains = 0;
        for (int r = 0; r < wildernessMap.Height; r++)
        {
          for (int c = 0; c < wildernessMap.Width; c++)
          {
            if (wildernessMap.TileAt(r, c).Type == TileType.Mountain)
              ++mountains;
          }
        }
        if (mountains < 20)
          continue;

        var tb = new TownBuilder();
        wildernessMap = tb.DrawnTown(wildernessMap, rng);
        town = tb.Town;
        town.Name = NameGenerator.TownName(rng);
        Console.WriteLine(town.Name);

        wilderness.AddMap(wildernessMap);
        campaign.AddDungeon(wilderness);

        // find the 'hidden valleys' that may be among the mountains
        var regionFinder = new RegionFinder(new WildernessPassable());
        var regions = regionFinder.Find(wildernessMap, false, TileType.Unknown);

        // I'm assuming the largest area is the one we want to place the dungeon entrance in
        int largest = 0;
        HashSet<(int, int)> mainRegion = [];
        foreach (var k in regions.Keys)
        {
          if (regions[k].Count > largest)
          {
            mainRegion = regions[k];
            largest = regions[k].Count;
          }
        }
        var entrance = PickDungeonEntrance(wildernessMap, mainRegion, town, rng);

        DrawOldRoad(wildernessMap, mainRegion, wildernessWidth, entrance, town, rng);

        // Add a dash of devestation around the dungeon entrance
        foreach (var loc in Util.LocsInRadius(new(0, 0, entrance.Item1, entrance.Item2), 5, wildernessWidth, wildernessWidth))
        {
          if (!wildernessMap.InBounds(loc.Row, loc.Col))
            continue;
          Tile tile = wildernessMap.TileAt(loc.Row, loc.Col);
          if (tile.Type == TileType.Grass) 
          {
            Tile replacement = TileFactory.Get(rng.NextDouble() < 0.5 ? TileType.Dirt : TileType.CharredGrass);
            wildernessMap.SetTile(loc.Row, loc.Col, replacement);
          }
          else if (tile.IsTree()) 
          {
            Tile replacement = TileFactory.Get(rng.NextDouble() < 0.3 ? TileType.Dirt : TileType.CharredStump);
            wildernessMap.SetTile(loc.Row, loc.Col, replacement);
          }
        }
        foreach (var (r, c) in Util.BresenhamCircle(entrance.Item1, entrance.Item2, 6))
        {
          if (!wildernessMap.InBounds(r, c))
            continue;
          Tile tile = wildernessMap.TileAt(r, c);
          if (tile.Type == TileType.Grass) 
          {
            Tile replacement = TileFactory.Get(rng.NextDouble() < 0.5 ? TileType.Dirt : TileType.CharredGrass);
            wildernessMap.SetTile(r, c, replacement);
          }
          else if (tile.IsTree()) 
          {
            Tile replacement = TileFactory.Get(rng.NextDouble() < 0.3 ? TileType.Dirt : TileType.CharredStump);
            wildernessMap.SetTile(r, c, replacement);
          }
        }

        var history = new History(rng);
        FactDb factDb = history.GenerateHistory(rng);
        campaign.FactDb = factDb;

        int maxDepth = 5;
        var monsterDecks = DeckBuilder.MakeDecks(1, maxDepth, factDb.Villain, rng);
        factDb.Add(new SimpleFact() { Name = "EarlyDenizen", Value = DeckBuilder.EarlyMainOccupant });
        var dBuilder = new MainDungeonBuilder();
        var mainDungeon = dBuilder.Generate(1, "Musty smells. A distant clang. Danger.", 30, 70, 5,
          entrance, factDb, objDb, rng, monsterDecks, wildernessMap);
        PopulateDungeon(rng, objDb, factDb, mainDungeon, 5, monsterDecks);

        SetLevel5MiniBoss(mainDungeon, objDb, factDb, DeckBuilder.EarlyMainOccupant, rng);

        campaign.MonsterDecks = monsterDecks;
        campaign.AddDungeon(mainDungeon);

        var portal = new Portal("You stand before a looming portal.")
        {
          Destination = new Loc(1, 0, dBuilder.ExitLoc.Item1, dBuilder.ExitLoc.Item2)
        };
        Loc entranceLoc = new Loc(0, 0, entrance.Item1, entrance.Item2);
        wildernessMap.SetTile(entrance, portal);
        factDb.Add(new LocationFact()
        {
          Loc = entranceLoc,
          Desc = "Dungeon Entrance"
        });

        PlayerAtLoc pal = new(entranceLoc, "A portal looms before you! Danger lurks within -- when you feel prepared you can enter via the [LIGHTBLUE >] command!");
        objDb.ConditionalEvents.Add(pal);

        Village.Populate(wildernessMap, town, objDb, factDb, rng);
        campaign.Town = town;

        (startR, startC) = PickStartLoc(wildernessMap, town, rng);
        if (startR == -1 || startC == -1)
        {
          // If there was somehow no valid path for any start location, 
          // start over and generate a new wilderness map
          continue;
        }
        //(startR, startC) = entrance;

        break;
      }
      catch (InvalidTownException)
      {
        Console.WriteLine("Oh no not enough cottages");
        // Should I just bail out after too many tries? I can't imagine it 
        // will take more than 1 or 2 more tries
      }
      catch (PlacingBuldingException)
      {
        Console.WriteLine("Failed to place a building");
      }
      catch (CouldNotPlaceDungeonEntranceException)
      {
        Console.WriteLine("Failed to find spot for Main Dungeon");
      }
    }
    while (true);

    return (campaign, startR, startC);
  }

  public void SavedGameExists(string playerName, string existingSavePath)
  {
    string s = $"Hmm! A saved game for [LIGHTBLUE {playerName}] already exists!";
    s += "\n\na) go back";
    s += "\nb) [BRIGHTRED ERASE] existing save and continue";
    UI.SetPopup(new Popup(s, "", -1, -1));
    UI.UpdateDisplay(null);

    char c;    
    do
    {
      Thread.Sleep(30);
      c = UI.GetKeyInput();

      if (c == Constants.ESC || c == 'a')
      {
        throw new GameNotLoadedException();
      }
      else if (c == 'b')
      {
        try
        {
          File.Delete(existingSavePath);
          UI.ClosePopup();
          return;
        }
        catch (Exception)
        {
          throw new GameNotLoadedException();
        }
      }      
    }
    while (true);
  }

  public GameState? Create(Options options)
  {
    try
    {
      string playerName = QueryPlayerName();

      foreach (var existingSave in Serialize.GetSavedGames())
      {
        if (existingSave.CharName.Equals(playerName, StringComparison.InvariantCultureIgnoreCase))
        {
          SavedGameExists(playerName, existingSave.Path);
          break;
        }
      }
      
      int seed = DateTime.Now.GetHashCode();
      
      Console.WriteLine($"Seed: {seed}");
      var rng = new Random(seed);
      var objDb = new GameObjectDB();
      SetItemIDInfo(rng);

      Player player = PlayerCreator.NewPlayer(playerName, objDb, 0, 0, UI, rng);
      UI.ClearLongMessage();
      Campaign campaign;
      int startRow, startCol;
      (campaign, startRow, startCol) = BeginNewCampaign(rng, objDb);
      player.Loc = new Loc(0, 0, startRow, startCol);
      GameState gameState = new(player, campaign, options, UI, rng, seed)
      {
        ObjDb = objDb,
        Turn = 1
      };

      string welcomeText = "An adventure begins!\n\nHaving recently graduated from one of the top fourteen Adventurer Colleges in ";
      welcomeText += $"Yendor, you've ventured to the remote town of {gameState.Town.Name}, ";
      welcomeText += "having heard that a growing darkness imperils its people. What better venue for a new adventurer to earn fame, glory, and gold!";
      welcomeText += "\n\n";
      welcomeText += "You might wish to speak with the townsfolk before your first delve into the nearby dungeon (marked with a [LIGHTBLUE Ո] symbol). They may have advice for you, and supplies to help you survive.";
      welcomeText += "\n\n";
      welcomeText += "Press [ICEBLUE ? for help], and [ICEBLUE x] will allow you to examine interesting features on screen.";
      welcomeText += "\n\n";
      welcomeText += "Press [ICEBLUE /] to toggle between recent messages and command/movement key cheat sheets.";
      UI.SetPopup(new Popup(welcomeText, "", -2, -1));
    
      gameState.ObjDb.AddToLoc(player.Loc, player);
      gameState.PrepareFieldOfView();
      gameState.RecentlySeenMonsters.Add(gameState.Player.ID);

      return gameState;
    }
    catch (GameQuitException)
    {
      return null;
    }
  }
}
