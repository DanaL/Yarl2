// Delve - A roguelike computer RPG
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

class DebugCommand(GameState gs)
{
  readonly GameState _gs = gs;
  static readonly Dictionary<string, ItemNames> ItemMap = new(StringComparer.OrdinalIgnoreCase)
  {
    {"dagger", ItemNames.DAGGER},
    {"silver dagger", ItemNames.SILVER_DAGGER},
    {"hand axe", ItemNames.HAND_AXE},
    {"battle axe", ItemNames.BATTLE_AXE},
    {"longsword", ItemNames.LONGSWORD},
    {"silver longsword", ItemNames.SILVER_LONGSWORD},
    {"shortsword", ItemNames.SHORTSHORD},
    {"greatsword", ItemNames.GREATSWORD},
    {"claymore", ItemNames.CLAYMORE},
    {"spear", ItemNames.SPEAR},
    {"guisarme", ItemNames.GUISARME},
    {"mace", ItemNames.MACE},
    {"rapier", ItemNames.RAPIER},
    {"longbow", ItemNames.LONGBOW},
    {"arrow", ItemNames.ARROW},
    {"dart", ItemNames.DART},
    {"leather armour", ItemNames.LEATHER_ARMOUR},
    {"studded leather", ItemNames.STUDDED_LEATHER_ARMOUR},
    {"chainmail", ItemNames.CHAINMAIL},
    {"ringmail", ItemNames.RINGMAIL},
    {"helmet", ItemNames.HELMET},
    {"shield", ItemNames.SHIELD},
    {"lock pick", ItemNames.LOCK_PICK},
    {"torch", ItemNames.TORCH},
    {"potion of healing", ItemNames.POTION_HEALING},
    {"potion of hardiness", ItemNames.POTION_HARDINESS},
    {"potion of mind reading", ItemNames.POTION_MIND_READING},
    {"potion of fire resistance", ItemNames.POTION_FIRE_RES},
    {"potion of cold resistance", ItemNames.POTION_COLD_RES},
    {"potion of levitation", ItemNames.POTION_OF_LEVITATION},
    {"antidote", ItemNames.ANTIDOTE},
    {"scroll of blink", ItemNames.SCROLL_BLINK},
    {"scroll of knock", ItemNames.SCROLL_KNOCK},
    {"scroll of magic mapping", ItemNames.SCROLL_MAGIC_MAP},
    {"scroll of protection", ItemNames.SCROLL_PROTECTION},
    {"scroll of escape", ItemNames.SCROLL_ESCAPE},
    {"scroll of scattering", ItemNames.SCROLL_SCATTERING},
    {"scroll of treasure finding", ItemNames.SCROLL_TREASURE_DETECTION},
    {"scroll of trap detection", ItemNames.SCROLL_TRAP_DETECTION},
    {"wand of magic missiles", ItemNames.WAND_MAGIC_MISSILES},
    {"wand of swap", ItemNames.WAND_SWAP},
    {"wand of heal monster", ItemNames.WAND_HEAL_MONSTER},
    {"wand of fireballs", ItemNames.WAND_FIREBALLS},
    {"wand of frost", ItemNames.WAND_FROST},
    {"ring of protection", ItemNames.RING_OF_PROTECTION},
    {"ring of adornment", ItemNames.RING_OF_ADORNMENT},
    {"vial of poison", ItemNames.VIAL_OF_POISON},
    {"ghostcap mushroom", ItemNames.GHOSTCAP_MUSHROOM},
    {"talisman of circumspection", ItemNames.TALISMAN_OF_CIRCUMSPECTION},
    {"blindfold", ItemNames.BLINDFOLD},
    {"potion of blindness", ItemNames.POTION_BLINDNESS},
    {"beetle carapace", ItemNames.BEETLE_CARAPACE},
    {"ogre liver", ItemNames.OGRE_LIVER},
    {"pickaxe", ItemNames.PICKAXE},
    {"apple", ItemNames.APPLE},
    {"rubble", ItemNames.RUBBLE},
    {"hill giant essence", ItemNames.HILL_GIANT_ESSENCE},
    {"fire giant essence", ItemNames.FIRE_GIANT_ESSENCE},
    {"frost giant essence", ItemNames.FROST_GIANT_ESSENCE},
    {"stabby guide", ItemNames.GUIDE_STABBY},
    {"sword guide", ItemNames.GUIDE_SWORDS },
    {"axe guide", ItemNames.GUIDE_AXES },
    {"gaston badge", ItemNames.GASTON_BADGE},
    {"lesser burly charm", ItemNames.LESSER_BURLY_CHARM},
    {"lesser health charm", ItemNames.LESSER_HEALTH_CHARM},
    {"lesser grace charm", ItemNames.LESSER_GRACE_CHARM},
    {"anti-snail sandals", ItemNames.ANTISNAIL_SANDALS},
    {"heavy boots", ItemNames.HEAVY_BOOTS },
    {"boots of water walking", ItemNames.BOOTS_OF_WATER_WALKING },
    {"golden apple", ItemNames.GOLDEN_APPLE },
    {"scroll disarm", ItemNames.SCROLL_DISARM },
    {"bow guide", ItemNames.GUIDE_BOWS },
    {"booze", ItemNames.FLASK_OF_BOOZE },
    {"troll brooch", ItemNames.TROLL_BROOCH },
    {"smouldering charm", ItemNames.SMOULDERING_CHARM },
    {"mithril ore", ItemNames.MITHRIL_ORE },
    {"cutpurse crest", ItemNames.CUTPURSE_CREST },
    {"wand of slow monster", ItemNames.WAND_SLOW_MONSTER },
    {"leather gloves", ItemNames.LEATHER_GLOVES },
    {"cloak of protection", ItemNames.CLOAK_OF_PROTECTION },
    {"gauntlets of power", ItemNames.GAUNTLETS_OF_POWER },
    {"potion of heroism", ItemNames.POTION_HEROISM },
    {"wand of summoning", ItemNames.WAND_SUMMONING },
    {"quarterstaff", ItemNames.QUARTERSTAFF },
    {"meditation crystal", ItemNames.MEDITATION_CRYSTAL },
    {"seeweed", ItemNames.SEEWEED },
    {"croesus charm", ItemNames.CROESUS_CHARM },
    {"potion of obscurity", ItemNames.POTION_OBSCURITY},
    {"wand of digging", ItemNames.WAND_DIGGING },
    {"boots of feather falling", ItemNames.FEATHERFALL_BOOTS },
    {"campfire", ItemNames.CAMPFIRE },
    {"wind fan", ItemNames.WIND_FAN },
    {"skeleton key", ItemNames.SKELETON_KEY },
    {"red crystal", ItemNames.RED_CRYSTAL },
    {"blue crystal", ItemNames.BLUE_CRYSTAL },
    {"holy water", ItemNames.HOLY_WATER },
    {"bone", ItemNames.BONE },
    {"skull", ItemNames.SKULL },
    {"axes book", ItemNames.GUIDE_AXES },
    {"crimson king ward", ItemNames.CRIMSON_KING_WARD },
    {"tincture of celerity", ItemNames.TINCTURE_CELERITY },
    {"potion of descent", ItemNames.POTION_DESCENT },
    {"scroll of stainlessness", ItemNames.SCROLL_STAINLESS },
    {"alchemical compound", ItemNames.ALCHEMICAL_COMPOUND },
    {"bomb", ItemNames.BOMB },
    {"ring of water breathing", ItemNames.RING_OF_WATER_BREATHING },
    {"lembas", ItemNames.LEMBAS },
    {"scroll of enchanting", ItemNames.SCROLL_ENCHANTING },
    {"rune of lashing", ItemNames.RUNE_OF_LASHING }
  };

  public string DoCommand(string txt)
  {
    if (txt == "loc")
    {
      _gs.UIRef().AlertPlayer($"Loc: {_gs.Player.Loc}");
      return "";
    }    
    else if (txt == "turn")
    {
      _gs.UIRef().AlertPlayer($"Turn {_gs.Turn}");
      return "";
    }    
    else if (txt == "genocide")
    {
      List<Actor> toRremove = [.. _gs.ObjDb.AllActors().Where(a => a is not Player && a.Loc.DungeonID == _gs.CurrDungeonID && a.Loc.Level == _gs.CurrLevel)];
      foreach (Actor a in toRremove)
      {
        _gs.ObjDb.RemoveActor(a);
      }
      _gs.FlushPerformers();

      return "";
    }
    else if (txt == "heal")
    {
      _gs.Player.Stats[Attribute.HP].Reset();
      return "";
    }
    else if (txt == "nobless")
    {
      List<Trait> traits = [.. _gs.Player.Traits.Where(t => t is BlessingTrait)];
      foreach (BlessingTrait t in traits.Cast<BlessingTrait>())
      {
        t.ExpiresOn = 1;
      }

      return "";
    }
    else if (txt == "pit")
    {
      List<Loc> adj = AdjSpots(_gs.Player.Loc);
      Loc loc = adj[_gs.Rng.Next(adj.Count)];
      _gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pit));
      _gs.PrepareFieldOfView();
      return "";
    }
    else if (txt == "quest1")
    {
      Item tablet1 = History.SealingTablet1(_gs.ObjDb);
      _gs.Player.Inventory.Add(tablet1, _gs.Player.ID);
      _gs.Player.Stats[Attribute.MainQuestState] = new Stat(2);
      return "";
    }
    else if (txt == "quest2")
    {
      _gs.Player.Stats[Attribute.MainQuestState] = new Stat(3);

      Loc towerGate = Loc.Nowhere;
      if (_gs.FactDb.FactCheck("Tower Gate") is LocationFact lf)
      {
        towerGate = lf.Loc;
      }

      Map wilderness = _gs.Campaign.Dungeons[0].LevelMaps[0];
      wilderness.SetTile(towerGate.Row, towerGate.Col, TileFactory.Get(TileType.StoneFloor));
      return "";
    }
    else if (txt == "set end game")
    {
      EndGame.Setup(_gs);
      return "";
    }
    else if (txt == "candle")
    {
      Item candle = History.CandleOfBinding(_gs.ObjDb);
      _gs.Player.AddToInventory(candle, _gs);
      return "";
    }
    else if (txt == "bell")
    {
      Item bell = History.AbjurationBell(_gs.ObjDb);
      _gs.Player.AddToInventory(bell, _gs);
      return "";
    }
    else if (txt == "tome")
    {
      Item tome = History.SorceressTome(_gs.ObjDb);
      _gs.Player.AddToInventory(tome, _gs);
      return "";
    }
    else if (txt == "unlock")
    {
      for (int r = 0; r < _gs.CurrentMap.Height; r++)
      {
        for (int c = 0; c < _gs.CurrentMap.Width; c++)
        {
          if (_gs.CurrentMap.TileAt(r, c).Type == TileType.LockedDoor)
          {
            Door door = new(TileType.ClosedDoor, false);
            _gs.CurrentMap.SetTile(r, c, door);
          }
        }
      }

      _gs.UIRef().AlertPlayer("Click.");

      return "";
    }
    else if (txt == "mold")
    {
      List<Loc> opt = [.. Util.Adj8Locs(_gs.Player.Loc).Where(loc => _gs.CurrentMap.TileAt(loc.Row, loc.Col).Passable())];

      if (opt.Count > 0)
      {
        Loc loc = opt[_gs.Rng.Next(opt.Count)];
        Item mold = ItemFactory.YellowMold();
        _gs.ObjDb.Add(mold);
        _gs.ObjDb.SetToLoc(loc, mold);
      }

      return "";
    }
    
    var parts = txt.Split(' ', 2);
    if (parts.Length < 2)
      return "Debug commands are formated: add/give/drop <obj name>";

    switch (parts[0].ToLower())
    {
      case "add":
        return AddMonster(parts[1]);
      case "clearfact":
        if (_gs.FactDb.FactCheck(parts[1]) is Fact fact)
          _gs.FactDb.ClearFact(fact);
        return "";
      case "give":
      case "drop":
        return AddItem(parts[0].ToLower(), parts[1]);
      case "lamp":
        MakeLamp(_gs, parts[1].Capitalize());
        return "";
      case "mirror":
        MakeMirror(_gs, parts[1]);
        return "";
      case "zorkmids":
      case "$":
        return GiveZorkminds(parts[1]);
      case "timeskip":
        _gs.Turn += ulong.Parse(parts[1]);
        return "";
      case "stress":
        if (uint.TryParse(parts[1], out uint stress))
        {
          _gs.Player.Stats[Attribute.Nerve].SetCurr((int)stress);
          _gs.Player.CalcStress();
          return "";
        }
        else
          return "Need integer stress level";
      default:
        return "Unknown debug command";
    }
  }

  void MakeMirror(GameState gs, string dirStr)
  {
    bool left = dirStr == "left";
    Item mirror = ItemFactory.Mirror(gs.ObjDb, left);
    _gs.ObjDb.SetToLoc(_gs.Player.Loc, mirror);
  }

  void MakeLamp(GameState gs, string dirStr)
  {
    Enum.TryParse(dirStr, out Dir dir);
    Item lamp = ItemFactory.Lamp(gs.ObjDb, dir);
    _gs.ObjDb.SetToLoc(_gs.Player.Loc, lamp);
  }

  string GiveZorkminds(string amount)
  {
    if (uint.TryParse(amount, out uint total))
    {
      
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, _gs.ObjDb);
      zorkmids.Value = (int)total;
      _gs.Player.Inventory.Add(zorkmids, _gs.Player.ID);

      return "";
    }

    return $"Need a valid amount!";
  }

  string AddItem(string action, string name)
  {
    bool illusion = false;
    if (name.EndsWith("illusion"))
    {
      illusion = true;
      name = name[..name.LastIndexOf(' ')];
    }

    if (ItemMap.TryGetValue(name, out var itemEnum))
    {
      Loc loc = _gs.Player.Loc;

      Item item;
      if (illusion)
      {
        var adjSpots = AdjSpots(_gs.Player.Loc);
        loc = adjSpots[_gs.Rng.Next(adjSpots.Count)];
        item = ItemFactory.Illusion(itemEnum, _gs.ObjDb);
      }
      else
      {
        item = ItemFactory.Get(itemEnum, _gs.ObjDb);
      }

      if (name == "bone")
      {
        item.Traits.Add(new AdjectiveTrait("old"));
      }
      
      if (name == "rubble" || name == "boulder")
      {
        var adjSpots = AdjSpots(_gs.Player.Loc);
        if (adjSpots.Count == 0)
          return "No open spot to add item";

        loc = adjSpots[_gs.Rng.Next(adjSpots.Count)];
        _gs.ObjDb.SetToLoc(loc, item);
      }
      else if (action == "give" && !illusion)
        _gs.Player.Inventory.Add(item, _gs.Player.ID);
      else
        _gs.ObjDb.SetToLoc(loc, item);

      _gs.PrepareFieldOfView();

      return "";   
    }

    return $"Unknown item: {name}";
  }

  List<Loc> AdjSpots(Loc loc)
  {
    return [..Util.Adj8Locs(loc)
        .Where(loc => !_gs.ObjDb.Occupied(loc) 
                          && _gs.CurrentMap.TileAt(loc.Row, loc.Col).Passable()
                          && !_gs.ObjDb.ItemsAt(loc).Where(item => item.HasTrait<BlockTrait>()).Any())];
  }

  List<Loc> AdjWater(Loc loc)
  {
    static bool IsWater(TileType t) => t switch
    {
      TileType.DeepWater or TileType.Underwater or TileType.Lake => true,
      _ => false
    };

    return [..Util.Adj8Locs(loc)
        .Where(loc => !_gs.ObjDb.Occupied(loc) 
                          && IsWater(_gs.CurrentMap.TileAt(loc.Row, loc.Col).Type)
                          && !_gs.ObjDb.ItemsAt(loc).Where(item => item.HasTrait<BlockTrait>()).Any())];
  }
  
  string AddMonster(string monsterName)
  {
    try
    {      
      Actor monster;

      if (monsterName == "mimic")
        monster = MonsterFactory.Mimic(true, _gs.Rng);
      else
        monster = MonsterFactory.Get(monsterName, _gs.ObjDb, _gs.Rng);
      
      List<Loc> adjSpots = AdjSpots(_gs.Player.Loc);
      if (monster.HasTrait<AmphibiousTrait>())
        adjSpots.AddRange(AdjWater(_gs.Player.Loc));
      else if (monster.HasTrait<SwimmerTrait>())
        adjSpots = AdjWater(_gs.Player.Loc);
        
      if (adjSpots.Count == 0)
        return "No open spot to add monster";

      var spawnLoc = adjSpots[_gs.Rng.Next(adjSpots.Count)];

      _gs.ObjDb.AddNewActor(monster, spawnLoc);
      _gs.PrepareFieldOfView();

      return "";
    }
    catch (UnknownMonsterException)
    {
      return $"Unknown monster: {monsterName}";
    }
  }
}