
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

// A structure to store info about a dungeon
class Dungeon(int ID, string arrivalMessage)
{
  public int ID { get; init; } = ID;
  public Dictionary<(int, int, int), Sqr> RememberedSqs = [];
  public Dictionary<int, Map> LevelMaps = [];
  public string ArrivalMessage { get; } = arrivalMessage;

  public void AddMap(Map map)
  {
    int id = LevelMaps.Count == 0 ? 0 : LevelMaps.Keys.Max() + 1;
    LevelMaps.Add(id, map);
  }
}

// A data structure to store all the info about 
// the 'story' of the game. All the dungeon levels, etc
class Campaign
{
  public Town? Town { get; set; }
  public History? History { get; set; }
  public Dictionary<int, Dungeon> Dungeons = [];

  public void AddDungeon(Dungeon dungeon)
  {
    int id = Dungeons.Count == 0 ? 0 : Dungeons.Keys.Max() + 1;
    Dungeons.Add(id, dungeon);
  }
}

class PreGameHandler(UserInterface ui)
{
  UserInterface _ui { get; set; } = ui;

  static bool StartSq(Map map, int row, int col)
  {
    switch (map.TileAt(row, col).Type)
    {
      case TileType.Grass:
      case TileType.Sand:
      case TileType.Tree:
      case TileType.Dirt:
        return true;
      default:
        return false;
    }
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

    return opts[rng.Next(opts.Count)];
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
        case TileType.Tree:
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

    var entrance = options[rng.Next(options.Count / 4)];

    return (entrance.Item1, entrance.Item2);
  }

  static bool InTown(int row, int col, Town town) =>
      row >= town.Row && row <= town.Row + town.Height && col >= town.Col && col <= town.Col + town.Width;

  static void DrawOldRoad(Map map, HashSet<(int, int)> region, (int, int) entrance, Town town, Random rng)
  {
    int loRow = 257, loCol = 257, hiRow = 0, hiCol = 0;
    foreach (var sq in region)
    {
      if (sq.Item1 < loRow)
        loRow = sq.Item1;
      if (sq.Item1 > hiRow)
        hiRow = sq.Item1;
      if (sq.Item2 < loCol)
        loCol = sq.Item2;
      if (sq.Item2 > hiCol)
        hiCol = sq.Item2;
    }

    Dictionary<TileType, int> passable = [];
    passable.Add(TileType.Grass, 1);
    passable.Add(TileType.Sand, 1);
    passable.Add(TileType.Tree, 2);
    passable.Add(TileType.Dirt, 1);
    passable.Add(TileType.Bridge, 1);
    passable.Add(TileType.Water, 1);
    passable.Add(TileType.WoodFloor, 1);

    // These aren't passable in a game sense, but we're only drawing the 
    // ancient road to the outskits of town (but I still need to calculate
    // the path all the way to the centre square, which may end up inside
    // a building)        
    passable.Add(TileType.HWindow, 1);
    passable.Add(TileType.VWindow, 1);
    passable.Add(TileType.ClosedDoor, 1);
    passable.Add(TileType.StoneFloor, 1);
    passable.Add(TileType.StoneWall, 1);
    passable.Add(TileType.WoodWall, 1);

    int tcRow = town.Row + town.Height / 2;
    int tcCol = town.Col + town.Width / 2;

    var dmap = new DjikstraMap(map, loRow, hiRow, loCol, hiCol);
    dmap.Generate(passable, (tcRow, tcCol), 257);
    var road = dmap.ShortestPath(entrance.Item1, entrance.Item2, 0, 0);

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

  static (Campaign, int, int) BeginNewCampaign(Random rng, GameObjectDB objDb)
  {
    var campaign = new Campaign();
    var wilderness = new Dungeon(0, "You draw a deep breath of fresh air.");
    var wildernessGenerator = new Wilderness(rng);
    var wildernessMap = wildernessGenerator.DrawLevel(257);

    var tb = new TownBuilder();
    wildernessMap = tb.DrawnTown(wildernessMap, rng);

    Town town = tb.Town;
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
    DrawOldRoad(wildernessMap, mainRegion, entrance, town, rng);

    var history = new History(rng);
    history.CalcDungeonHistory();
    history.GenerateVillain();
    campaign.History = history;

    var dBuilder = new MainDungeonBuilder();
    var mainDungeon = dBuilder.Generate(1, "Musty smells. A distant clang. Danger.", 30, 70, 5, entrance, history, objDb, rng);
    PopulateDungeon(rng, objDb, history, mainDungeon);
    //var dBuilder = new ArenaBuilder();
    //var mainDungeon = dBuilder.Generate(1, entrance, objDb, rng);

    campaign.AddDungeon(mainDungeon);

    var portal = new Portal("You stand before a looming portal.")
    {
      Destination = new Loc(1, 0, dBuilder.ExitLoc.Item1, dBuilder.ExitLoc.Item2)
    };
    wildernessMap.SetTile(entrance, portal);
    history.Facts.Add(new LocationFact() 
    {
      Loc = new Loc(0, 0, entrance.Item1, entrance.Item2),
      Desc = "Dungeon Entrance"
    });

;    Village.Populate(wildernessMap, town, objDb, history, rng);
    campaign.Town = town;


    //PopulateArena(rng, objDb, mainDungeon);

    var (startR, startC) = PickStartLoc(wildernessMap, town, rng);

    return (campaign, startR, startC);
    //return (campaign, entrance.Item1, entrance.Item2);
  }

  private static void PopulateArena(Random rng, GameObjectDB objDb, Dungeon dungeon)
  {
    int lvl = 0;

    var sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);

    //Actor trickster = MonsterFactory.Get("kobold trickster", rng);
    //trickster.Loc = loc;
    //objDb.Add(trickster);
    //objDb.AddToLoc(loc, trickster);

    // for (int k = 0; k < 3; k++)
    // {
    //     sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
    //     loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);
    //     Actor k1 = MonsterFactory.Get("kobold", rng);
    //     k1.Loc = loc;
    //     k1.Status = ActorStatus.Active;
    //     objDb.Add(k1);
    //     objDb.AddToLoc(loc, k1);
    // }

    sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
    loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);
    Actor mob = MonsterFactory.Get("jelly", rng);
    objDb.AddNewActor(mob, loc);

    // sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
    // loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);
    // Actor bat = MonsterFactory.Get("dire bat", rng);
    // bat.Loc = loc;
    // objDb.Add(bat);
    // objDb.AddToLoc(loc, bat);
  }

  // This is very temporary/early code since eventually dungeons will need to
  // know how to populate themselves (or receive a populator class of some 
  // sort) because monsters will spawn as the player explores
  private static void PopulateDungeon(Random rng, GameObjectDB objDb, History history, Dungeon dungeon)
  {
    int maxDepth = 3;
    var decks = DeckBulder.MakeDecks(1, maxDepth, history.Villain, rng);
    history.Facts.Add(new SimpleFact() { Name = "EarlyDenizen", Value = DeckBulder.EarlyMainOccupant });

    // Temp: generate monster decks and populate the first two levels of the dungeon.
    // I'll actually want to save the decks for reuse as random monsters are added
    // in, but I'm not sure where they should live. I guess maybe in the Map structure,
    // which has really come to represent a dungeon level
    for (int lvl = 0; lvl < maxDepth; lvl++)
    {
      for (int j = 0; j < rng.Next(8, 13); j++)
      {
        var deck = decks[lvl];
        var sq = dungeon.LevelMaps[lvl].RandomTile(TileType.DungeonFloor, rng);
        var loc = new Loc(dungeon.ID, lvl, sq.Item1, sq.Item2);
        if (deck.Indexes.Count == 0)
          deck.Reshuffle(rng);
        string m = deck.Monsters[deck.Indexes.Dequeue()];
        Actor monster = MonsterFactory.Get(m, rng);
        monster.Loc = loc;
        objDb.Add(monster);
        objDb.AddToLoc(loc, monster);
      }
    }
  }

  public GameState? StartUp(Options options)
  {
    try
    {
      string playerName = _ui.BlockingGetResponse("Who are you?");
      return SetupGame(playerName, options, _ui);
    }
    catch (GameQuitException)
    {
      return null;
    }
  }

  private GameState SetupGame(string playerName, Options options, UserInterface ui)
  {
    GameState gameState;
    Player player;

    if (Serialize.SaveFileExists(playerName))
    {
      gameState = Serialize.LoadSaveGame(playerName, options, ui);
      gameState.Player = gameState.ObjDb.FindPlayer() ?? throw new Exception("No player :O");
      player = gameState.Player;
    }
    else
    {
      int seed = DateTime.Now.GetHashCode();
      // -758465673 this seed doesn't have a valid entrance loc?

      Console.WriteLine($"Seed: {seed}");
      var rng = new Random(seed);
      var objDb = new GameObjectDB();

      player = PlayerCreator.NewPlayer(playerName, objDb, 0, 0, _ui, rng);
      _ui.ClearLongMessage();
      Campaign campaign;
      int startRow, startCol;
      (campaign, startRow, startCol) = BeginNewCampaign(rng, objDb);
      player.Loc = new Loc(0, 0, startRow, startCol);
      gameState = new GameState(player, campaign, options, _ui, rng, seed)
      {
        ObjDb = objDb,
        Turn = 1
      };
    }

    gameState.ObjDb.AddToLoc(player.Loc, player);
    gameState.ToggleEffect(player, player.Loc, TerrainFlag.Lit, true);

    //seed = 601907053;
    //seed = 1956722118;
    //seed = 1003709949;
    //seed = -1407912410;
    //seed = 937420670;
    //seed = -1514513425;
    //seed = 1760989144;
    //seed = 1067714652;
    //seed = 562054470;
    //seed = -1750061855;
    //seed = 475720358;
    //seed = -910280873;
    //seed = -225039841;

    return gameState;
  }
}

