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

  public Dungeon Generate(string arrivalMessage, FactDb factDb, GameObjectDB objDb, Rng rng, Map wildernessMap)
  {
    int numOfLevels = rng.Next(5, 8);

    Dungeon dungeon = new(DungeonId, arrivalMessage);
    DungeonMap mapper = new(rng);
    Map[] levels = new Map[numOfLevels];

    dungeon.MonsterDecks = DeckBuilder.ReadDeck(MainOccupant, rng);

    for (int levelNum = 0; levelNum < numOfLevels; levelNum++)
    {
      levels[levelNum] = mapper.DrawLevel(WIDTH, HEIGHT);
      dungeon.AddMap(levels[levelNum]);

      // Sometimes add a secret door or two in hallways
      if (rng.Next(2) == 0)
        PutSecretDoorsInHallways(levels[levelNum], rng);
    }

    AddRivers(levels, HEIGHT, WIDTH, DungeonId, objDb, rng);

    SetStairs(DungeonId, levels, HEIGHT, WIDTH, numOfLevels, Entrance, rng);

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
    }

    PopulateDungeon(dungeon, rng, objDb);

    // Add a couple of guaranteed good items to dungeon
    AddGoodItemToLevel(levels[1], DungeonId, 1, rng, objDb);
    AddGoodItemToLevel(levels[3], DungeonId, 3, rng, objDb);

    int fallenAdventurer = rng.Next(1, numOfLevels);
    AddFallenAdventurer(objDb, levels[fallenAdventurer], fallenAdventurer, rng);

    if (factDb.FactCheck("EarlyDenizen") is SimpleFact earlyOcc)
    {
      SetFirstBoss(dungeon, objDb, factDb, earlyOcc.Value, rng);
    }

    return dungeon;
  }

  static void SetFirstBoss(Dungeon dungeon, GameObjectDB objDb, FactDb factDb, string earlyDenizen, Rng rng)
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
}


