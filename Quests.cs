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

class SorceressQuest
{
  static void SetupSorceress(Map map, int level, int dungeonId, GameObjectDB objDb, Rng rng)
  {
    Mob sorceress = new()
    {
      Name = "the Sorceress",
      Appearance = "Faint vision of a stern-looking mage ",
      Glyph = new Glyph('@', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false)
    };
    sorceress.Stats[Attribute.HP] = new Stat(50);
    sorceress.Traits.Add(new VillagerTrait());
    sorceress.Traits.Add(new NamedTrait());
    sorceress.Traits.Add(new IntelligentTrait());
    sorceress.Traits.Add(new DialogueScriptTrait() { ScriptFile = "sorceress.txt" });
    sorceress.SetBehaviour(new NPCBehaviour());
    sorceress.Traits.Add(new BehaviourTreeTrait() { Plan = "BasicWander" });
    sorceress.Traits.Add(new LightSourceTrait() { Radius = 1, OwnerID = sorceress.ID, FgColour = Colours.ICE_BLUE, BgColour = Colours.BLUE_AURA });

    List<(int, int)> sqsOfType = map.SqsOfType(TileType.DungeonFloor);
    (int, int) sq = sqsOfType[rng.Next(sqsOfType.Count)];
    Loc loc = new(dungeonId, level, sq.Item1, sq.Item2);
    objDb.AddNewActor(sorceress, loc);
  }

  static bool IsValidSpotForTower(int row, int col, Map wilderness, Town town)
  {
    if (!OpenSq(row, col))
      return false;

    foreach (var adj in Util.Adj8Sqs(row, col))
    {
      if (!OpenSq(adj.Item1, adj.Item2))
        return false;
    }

    return true;

    bool OpenSq(int row, int col)
    {
      if (row >= town.Row && row <= town.Row + town.Height && col >= town.Col && col <= town.Col + town.Width)
        return false;

      Tile tile = wilderness.TileAt(row, col);
      return tile.Type switch
      {
        TileType.DeepWater or TileType.Dirt or TileType.StoneRoad
          or TileType.Bridge or TileType.Portal or TileType.WoodWall
          or TileType.HFence or TileType.VFence or TileType.CornerFence or TileType.Crops
          or TileType.Mountain or TileType.SnowPeak => false,
        _ => true,
      };
    }
  }

  public static void Setup(Map wilderness, Town town, GameObjectDB objDb, FactDb factDb, Campaign campaign, Rng rng)
  {    
    // First, pick a spot in the wilderness for the tower and draw it
    List<(int, int)> options = [];
    for (int r = 3; r < wilderness.Height - 13; r++)
    {
      for (int c = 3; c < wilderness.Width - 13; c++)
      {
        if (IsValidSpotForTower(r, c, wilderness, town))
        {
          options.Add((r, c));
        }
      }
    }

    (int row, int col) = options[rng.Next(options.Count)];
    foreach (var sq in Util.Adj8Sqs(row, col))
    {
      wilderness.SetTile(sq, TileFactory.Get(TileType.PermWall));
    }

    List<(int, int)> doorCandidates = [];
    if (wilderness.TileAt(row - 2, col).Passable()) doorCandidates.Add((row - 1, col));
    if (wilderness.TileAt(row + 2, col).Passable()) doorCandidates.Add((row + 1, col));
    if (wilderness.TileAt(row, col - 2).Passable()) doorCandidates.Add((row, col - 1));
    if (wilderness.TileAt(row, col + 2).Passable()) doorCandidates.Add((row, col + 1));
    if (doorCandidates.Count == 0)
      throw new CampaignCreationException("Unable to place sorceress tower");

    (int doorRow, int doorCol) = doorCandidates[rng.Next(doorCandidates.Count)];
    
    Portcullis p = new(false);
    wilderness.SetTile(doorRow, doorCol, p);
    LocationFact lf = new()
    {
      Loc = new Loc(0, 0, doorRow, doorCol),
      Desc = "Tower Gate"
    };
    campaign.FactDb!.Add(lf);

    (int dr, int dc) = (doorRow - row, doorCol - col);
    Loc msgLoc = new(0, 0, doorRow + dr, doorCol + dc);
    MessageAtLoc pal = new(msgLoc, "A portcullis scored with glowing, arcane runes bars the entrance to this tower.");
    objDb.ConditionalEvents.Add(pal);

    int dungeonId = campaign.Dungeons.Keys.Max() + 1;
    SorceressDungeonBuilder sdb = new(dungeonId, 21, 36);
    (Dungeon sorceressTower, Loc towerExit) = sdb.Generate(row, col, objDb, rng);
    sorceressTower.ExitLoc = new(0, 0, row, col);
    campaign.AddDungeon(sorceressTower);

    // Set the decoy mirrors.
    (Dungeon wumpus, Loc wumpusLoc) = SorceressDungeonBuilder.WumpusDungeon(sdb.DecoyMirror1, dungeonId + 1, objDb, rng);
    Map mirrorLevel = sorceressTower.LevelMaps[sdb.DecoyMirror1.Level];
    if (mirrorLevel.TileAt(sdb.DecoyMirror1.Row, sdb.DecoyMirror1.Col) is MysteriousMirror mirror)
    {
      mirror.Destination = wumpusLoc;

      List<Loc> adjToMirror = [.. Util.Adj8Locs(sdb.DecoyMirror1)
                                    .Where(l => mirrorLevel.TileAt(l.Row, l.Col).Type == TileType.DungeonFloor)];
      wumpus.ExitLoc = adjToMirror[rng.Next(adjToMirror.Count)];
    }
    campaign.Dungeons.Add(wumpus.ID, wumpus);

    (Dungeon vampyArea, Loc vampyLoc) = SorceressDungeonBuilder.VampyDungeon(sdb.DecoyMirror2, dungeonId + 2, objDb, rng);
    mirrorLevel = sorceressTower.LevelMaps[sdb.DecoyMirror2.Level];
    if (mirrorLevel.TileAt(sdb.DecoyMirror2.Row, sdb.DecoyMirror2.Col) is MysteriousMirror mirror2)
    {
      mirror2.Destination = vampyLoc;

      List<Loc> adjToMirror = [.. Util.Adj8Locs(sdb.DecoyMirror2)
                                    .Where(l => mirrorLevel.TileAt(l.Row, l.Col).Type == TileType.DungeonFloor)];
      vampyArea.ExitLoc = adjToMirror[rng.Next(adjToMirror.Count)];
    }
    campaign.Dungeons.Add(vampyArea.ID, vampyArea);

    Upstairs entrance = new("")
    {
      Destination = towerExit
    };
    wilderness.SetTile(row, col, entrance);

    int tl = sorceressTower.LevelMaps.Count - 1;
    Map topLevel = sorceressTower.LevelMaps[tl];
    SetupSorceress(topLevel, tl, dungeonId, objDb, rng);

    Item diary1 = new()
    {
      Name = "exerpt of a memoir",
      Type = ItemType.Document,
      Glyph = new Glyph('♪', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false)
    };
    string magicWord = History.MagicWord(rng);
    factDb.Add(new SimpleFact() { Name = "MDTemplePassword", Value = magicWord });
    string txt = $"...and so I borrowed -- well, borrowed without permission -- the Candle from the Moon Daughter's hidden temple. I did return it when I was finished! It wasn't even that hard to find. You just stand in the centre of the Moonstone Ring on a clear night and speak: {magicWord}.";
    diary1.Traits.Add(new ReadableTrait(txt) { OwnerID = diary1.ID });
    diary1.Traits.Add(new DescriptionTrait("A hastily scrawled document with many passages crossed out and rewritten."));
    List<(int, int)> floorSqs = sorceressTower.LevelMaps[tl - 1].SqsOfType(TileType.DungeonFloor);
    (int drow, int dcol) = floorSqs[rng.Next(floorSqs.Count)];
    Loc diaryLoc = new(dungeonId, tl - 1, drow, dcol);
    objDb.Add(diary1);
    objDb.SetToLoc(diaryLoc, diary1);

    Landmark landmark = new("Written 100 times on a chalkboard: I will not borrow my mistress' favourite Bell and lose it in an ancient dungeon in a hidden valley.");
    floorSqs = sorceressTower.LevelMaps[tl - 2].SqsOfType(TileType.DungeonFloor);
    (int lmRow, int lmCol) = floorSqs[rng.Next(floorSqs.Count)];
    sorceressTower.LevelMaps[tl - 2].SetTile(lmRow, lmCol, landmark);
  }
}

class WitchQuest
{
  public static Loc QuestEntrance(GameState gs)
  {
    static bool ProbablyOpen(Map map, int r, int c)
    {
      int blocked = 0;
      foreach (var adj in Util.Adj8Sqs(r, c))
      {
        TileType tile = map.TileAt(adj.Item1, adj.Item2).Type;
        switch (tile)
        {
          case TileType.Mountain:
          case TileType.SnowPeak:
          case TileType.DeepWater:
          case TileType.Water:
            blocked++;
            break;
        }
      }

      return blocked <= 5;
    }

    Map wilderness = gs.Campaign.Dungeons[0].LevelMaps[0];

    List<Loc> witchSqs = [];
    foreach (Loc loc in gs.Town.WitchesCottage)
    {
      Tile tile = wilderness.TileAt(loc.Row, loc.Col);
      if (tile.Type == TileType.Grass || tile.IsTree())
        witchSqs.Add(loc);
    }
    Loc witches = witchSqs[gs.Rng.Next(witchSqs.Count)];

    // For the entrance to the cave where the witches' quest goal, pick a 
    // location that's not too far from their hut, with a preference for a
    // mountain tile.


    int northRow = int.Max(2, witches.Row - 50);
    int southRow = int.Min(wilderness.Height, witches.Row + 50);
    int westCol = int.Max(2, witches.Col - 50);
    int eastCol = int.Min(wilderness.Width, witches.Col + 50);
    List<Loc> mountains = [];
    List<Loc> others = [];
    for (int r = northRow; r < southRow - 2; r++)
    {
      for (int c = westCol; c < eastCol - 2; c++)
      {
        // We don't want to be too close either
        if (Util.Distance(r, c, witches.Row, witches.Col) < 25)
          continue;

        if (Util.PtInSqr(r, c, gs.Town.Row, gs.Town.Col, gs.Town.Height, gs.Town.Width))
          continue;

        Tile tile = wilderness.TileAt(r, c);
        if (tile.Type == TileType.Mountain && ProbablyOpen(wilderness, r, c))
          mountains.Add(new Loc(0, 0, r, c));
        else if (tile.Type == TileType.Grass || tile.IsTree())
          others.Add(new Loc(0, 0, r, c));
      }
    }

    while (mountains.Count > 0)
    {
      int i = gs.Rng.Next(mountains.Count);
      Loc loc = mountains[i];
      mountains.RemoveAt(i);

      // Start from the proposed entrance, otherwise pathfinding will fail
      // because mountains aren't open
      var path = AStar.FindPath(gs.ObjDb, wilderness, loc, witches, Costs, true);
      if (path.Count > 0)
        return loc;
    }

    if (others.Count > 0)
    {
      return others[gs.Rng.Next(others.Count)];
    }

    static int Costs(Tile tile) => tile.Type switch
    {
      TileType.Grass or TileType.Sand or TileType.Dirt
        or TileType.Bridge or TileType.GreenTree or TileType.RedTree
        or TileType.OrangeTree or TileType.YellowTree or TileType.Conifer
        or TileType.Water or TileType.Well or TileType.WoodWall 
        or TileType.StoneWall or TileType.WoodFloor => 1,
      _ => int.MaxValue
    };

    // I'm not sure what to do if there are no valid locations? Is it
    // even possible?
    throw new Exception("I couldn't find a spot for the Witch Quest!");
  }

  public static (Dungeon, Loc) GenerateDungeon(GameState gs, Loc entrance)
  {
    int id = gs.Campaign.Dungeons.Keys.Max() + 1;
    Dungeon dungeon = new(id, "a Mysterious Cave", "You shudder. Not from cold, but from sensing something unnatural within this cave.", true);
    dungeon.ExitLoc = entrance;
    MonsterDeck deck = new();
    deck.Monsters.AddRange(["skeleton", "skeleton", "zombie", "zombie", "dire bat"]);
    dungeon.MonsterDecks.Add(deck);

    int caveHeight = 25;
    int caveWidth = 40;
    bool[,] cave = CACave.GetCave(caveHeight, caveWidth, gs.Rng);
    Map map = new(caveWidth + 2, caveHeight + 2, TileType.PermWall);

    List<(int, int)> floors = [];
    for (int r = 0; r < caveHeight; r++)
    {
      for (int c = 0; c < caveWidth; c++)
      {
        TileType tile = TileType.DungeonWall;
        if (cave[r, c])
        {
          tile = TileType.DungeonFloor;
          floors.Add((r + 1, c + 1));
        }
        map.SetTile(r + 1, c + 1, TileFactory.Get(tile));
      }
    }

    CACave.JoinCaves(map, gs.Rng, gs.ObjDb, new DungeonPassable(), TileType.DungeonFloor, TileType.DungeonWall, TileType.DungeonWall);

    int i = gs.Rng.Next(floors.Count);
    var exitSq = floors[i];
    floors.RemoveAt(i);
    var exitStairs = new Upstairs("")
    {
      Destination = entrance
    };
    map.SetTile(exitSq, exitStairs);

    dungeon.AddMap(map);

    // Place some remains
    i = gs.Rng.Next(floors.Count);
    var sq = floors[i];
    floors.RemoveAt(i);
    Loc loc = new(id, 0, sq.Item1, sq.Item2);
    Item skull = ItemFactory.Get(ItemNames.SKULL, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, skull);
    ItemNames itemName = gs.Rng.NextDouble() < 0.8 ? ItemNames.DAGGER : ItemNames.SILVER_DAGGER;
    Item dagger = ItemFactory.Get(itemName, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, dagger);
    itemName = gs.Rng.NextDouble() < 0.5 ? ItemNames.QUARTERSTAFF : ItemNames.GENERIC_WAND;
    Item focus = ItemFactory.Get(itemName, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, focus);

    sq = floors[gs.Rng.Next(floors.Count)];
    loc = new Loc(id, 0, sq.Item1, sq.Item2);
    Item crystal = ItemFactory.Get(ItemNames.MEDITATION_CRYSTAL, gs.ObjDb);
    gs.ObjDb.SetToLoc(loc, crystal);

    // Add a few monsters to the cave
    int numOfMonsters = gs.Rng.Next(3, 6);
    for (int j = 0; j < numOfMonsters; j++)
    {
      string name = gs.Rng.Next(3) switch
      {
        0 => "skeleton",
        1 => "zombie",
        _ => "dire bat"
      };
      Actor m = MonsterFactory.Get(name, gs.ObjDb, gs.Rng);

      sq = floors[gs.Rng.Next(floors.Count)];
      loc = new Loc(id, 0, sq.Item1, sq.Item2);

      gs.ObjDb.AddNewActor(m, loc);
    }

    // Add in a 'boss' monster
    string boss = gs.Rng.Next(3) switch
    {
      0 => "ghoul",
      1 => "shadow",
      _ => "phantom"
    };
    Actor b = MonsterFactory.Get(boss, gs.ObjDb, gs.Rng);
    gs.ObjDb.AddNewActor(b, loc);

    return (dungeon, new Loc(id, 0, exitSq.Item1, exitSq.Item2));
  }
}

class SmithQuest
{
  public static void Setup(Dungeon dungeon, string denizen, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    if (factDb.FactCheck("SmithId") is not SimpleFact smithFact)
      throw new CampaignCreationException("Village smith info was not found!");

    ulong smithId = ulong.Parse(smithFact.Value);
    if (objDb.GetObj(smithId) is not Actor smith)
      throw new CampaignCreationException("Village smith info was not found!");

    string name = "hammer".Possessive(smith);
    Item hammer = new()
    {
      Name = name,
      Type = ItemType.Trinket,
      Value = 0,
      Glyph = new Glyph('(', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, false)
    };
    hammer.Traits.Add(new MetalTrait { Type = Metals.Iron });
    hammer.Traits.Add(new NamedTrait());
    hammer.Traits.Add(new DescriptionTrait("A well-used and well-cared for smith's hammer. Someone is probably looking for this."));
    objDb.Add(hammer);

    factDb.Add(new SimpleFact() { Name = "SmithHammerId", Value = hammer.ID.ToString() });

    NameGenerator ng = new(rng, Util.NamesFile);
    string thiefName = ng.BossName();
    string template = denizen == "kobold" ? "kobold bully" : "penny pincher";
    
    Actor thief = MonsterFactory.NamedActor(thiefName, template, objDb, rng);
    thief.Glyph = thief.Glyph with { Lit = Colours.LIGHT_PURPLE, Unlit = Colours.PURPLE };
    thief.Inventory.Add(hammer, thief.ID);

    Map map = dungeon.LevelMaps[1];
    List<Loc> floors = map.ClearFloors(dungeon.ID, 1, objDb);
    if (floors.Count == 0)
      throw new CampaignCreationException("Could not place the smith's hammer thief!");

    Loc loc = floors[rng.Next(floors.Count)];
    objDb.AddNewActor(thief, loc);
  }
}

class CKShrine
{
  // Copying FloodFill() code from Util.cs mostly, but instead here I am 
  // looking mountains around the 'perimeter' of the start wilderness area
  static List<Loc> FindMountains(Loc start, Map wilderness)
  {
    HashSet<Loc> mountains = [];
    HashSet<Loc> locs = [];
    Queue<Loc> q = [];
    q.Enqueue(start);

    while (q.Count > 0)
    {
      Loc curr = q.Dequeue();
      if (locs.Contains(curr))
        continue;
      locs.Add(curr);

      foreach (Loc adj in Util.Adj8Locs(curr))
      {
        if (locs.Contains(adj))
          continue;
        Tile tile = wilderness.TileAt(adj.Row, adj.Col);
        if (tile.Type == TileType.Mountain && GoodSpot(adj))
          mountains.Add(adj);

        if (tile.Passable())
          q.Enqueue(adj);
      }
    }

    bool GoodSpot(Loc loc)
    {
      int count = 0;

      foreach (Loc adj in Util.Adj8Locs(loc))
      {
        switch (wilderness.TileAt(adj.Row, adj.Col).Type)
        {
          case TileType.Sand:
          case TileType.Dirt:
          case TileType.Grass:
          case TileType.GreenTree:
          case TileType.YellowTree:
          case TileType.OrangeTree:
          case TileType.RedTree:
          case TileType.Conifer:
          case TileType.Bridge:
            ++count;
            break;
        }
      }

      return count > 2;
    }

    return [.. mountains];
  }

  public static void Setup(Campaign campaign, Loc start, Map wilderness, GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    Dungeon dungeon = new(campaign.Dungeons.Count, "the Red Shrine", "A musty shrine. There is a metallic tang in the air.", true);
    Map map = new(15, 17, TileType.PermWall) 
    { 
      Features = MapFeatures.UndiggableFloor | MapFeatures.NoRandomEncounters | MapFeatures.NoExplore
    };
    
    for (int c = 5; c < 10; c++) map.SetTile(3, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 4; c < 11; c++) map.SetTile(4, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 3; c < 12; c++) map.SetTile(5, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 2; c < 13; c++) map.SetTile(6, c, TileFactory.Get(TileType.DungeonFloor));

    for (int c = 1; c < 14; c++) map.SetTile(7, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 1; c < 14; c++) map.SetTile(8, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 1; c < 14; c++) map.SetTile(9, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 1; c < 14; c++) map.SetTile(10, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 1; c < 14; c++) map.SetTile(11, c, TileFactory.Get(TileType.DungeonFloor));

    for (int c = 2; c < 13; c++) map.SetTile(12, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 3; c < 12; c++) map.SetTile(13, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 4; c < 11; c++) map.SetTile(14, c, TileFactory.Get(TileType.DungeonFloor));
    for (int c = 5; c < 10; c++) map.SetTile(15, c, TileFactory.Get(TileType.DungeonFloor));

    List<Loc> mountains = FindMountains(start, wilderness);
    Loc shrineLoc = mountains[rng.Next(mountains.Count)];
    Portal entrance = new("", TileType.CKShrineEntrance) { Destination = new(dungeon.ID, 0, 1, 7) };
    wilderness.SetTile(shrineLoc.Row, shrineLoc.Col, entrance);

    List<Loc> nearBy = [];
    for (int r = shrineLoc.Row - 3; r <= shrineLoc.Row + 3; r++)
    {
      for (int c = shrineLoc.Col - 3; c <= shrineLoc.Col + 3; c++)
      {
        if (!wilderness.InBounds(r, c))
          continue;
        Tile nb = wilderness.TileAt(r, c);
        if (nb.Type == TileType.Grass || nb.Type == TileType.Dirt || nb.IsTree())
          nearBy.Add(new (0, 0, r, c));
      }
    }
    nearBy.Shuffle(rng);
    for (int s = 0; s < int.Min(nearBy.Count, rng.Next(1, 3)); s++)
    {
      RedLandmark marker = new("A shattered statue, carved from red stone.");
      wilderness.SetTile(nearBy[s].Row, nearBy[s].Col, marker);
    }

    Portal exit = new("", TileType.CKShrineExit) { Destination = shrineLoc };
    map.SetTile(1, 7, exit);
    map.SetTile(2, 7, TileFactory.Get(TileType.CKShrineFoyer));

    Statue(8, 6);
    Statue(8, 8);
    Statue(10, 6);
    Statue(10, 8);

    Item blade = ItemFactory.Get(ItemNames.GREATSWORD, objDb);
    blade.Glyph = blade.Glyph with { Lit = Colours.DULL_RED, Unlit = Colours.DULL_RED };
    blade.Name = "Crimson King's Blade";
    blade.Traits.Add(new NamedTrait());
    blade.Traits.Add(new ArtifactTrait());
    blade.Traits.Add(new RustProofTrait());
    blade.Traits.Add(new GrantsTrait() { TraitsGranted = [ "ReaverBlessing#0#0" ]});
    blade.Traits.Add(new WeaponBonusTrait() { Bonus = 1 });
    blade.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Blunt });
    blade.Traits.Add(new DescriptionTrait("This weapon is the vessel of the Crimson King. When you hold it, your mind is filled visions of battles and war. Be forewarned: this blade's power and fury is fueled by your very [BRIGHTRED life force]."));
    objDb.SetToLoc(new (dungeon.ID, 0, 9, 7), blade);
    factDb.Add(new SimpleFact() { Name = "CrimsonKingBladeId", Value = blade.ID.ToString() });
    factDb.Add(new LocationFact() { Desc = "CrimsonKingAltar", Loc = new (dungeon.ID, 0, 9, 7) });

    dungeon.AddMap(map);
    campaign.AddDungeon(dungeon);

    void Statue(int row, int col)
    {
      Item statue = ItemFactory.Get(ItemNames.STATUE, objDb);
      statue.Glyph = statue.Glyph with { Lit = Colours.BRIGHT_RED, Unlit = Colours.DULL_RED };
      statue.Traits.Add(new DescriptionTrait("a statue of a fur-clad warrior, leaning on a massive sword."));
      objDb.SetToLoc(new(dungeon.ID, 0, row, col), statue);
    }
  }
}