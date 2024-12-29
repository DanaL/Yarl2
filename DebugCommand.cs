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
    {"potion of mind reading", ItemNames.POTION_MIND_READING},
    {"potion of fire resistance", ItemNames.POTION_FIRE_RES},
    {"potion of cold resistance", ItemNames.POTION_COLD_RES},
    {"potion of levitation", ItemNames.POTION_OF_LEVITATION},
    {"antidote", ItemNames.ANTIDOTE},
    {"scroll of identify", ItemNames.SCROLL_IDENTIFY},
    {"scroll of blink", ItemNames.SCROLL_BLINK},
    {"scroll of knock", ItemNames.SCROLL_KNOCK},
    {"scroll of magic mapping", ItemNames.SCROLL_MAGIC_MAP},
    {"scroll of protection", ItemNames.SCROLL_PROTECTION},
    {"scroll of recall", ItemNames.SCROLL_RECALL},
    {"wand of magic missiles", ItemNames.WAND_MAGIC_MISSILES},
    {"wand of swap", ItemNames.WAND_SWAP},
    {"wand of heal monster", ItemNames.WAND_HEAL_MONSTER},
    {"wand of fireballs", ItemNames.WAND_FIREBALLS},
    {"wand of frost", ItemNames.WAND_FROST},
    {"ring of protection", ItemNames.RING_OF_PROTECTION},
    {"ring of aggression", ItemNames.RING_OF_AGGRESSION},
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
    {"ring of fraility", ItemNames.RING_OF_FRAILITY},
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
    {"wand of summoning", ItemNames.WAND_SUMMONING }
  };

  public string DoCommand(string txt)
  {
    if (txt == "loc")
    {
      _gs.UIRef().AlertPlayer($"Loc: {_gs.Player.Loc}");
      return "";
    }
    else if (txt == "heal")
    {
      _gs.Player.Stats[Attribute.HP].Reset();
      return "";
    }
    else if (txt == "dmap")
    {
      var start = DateTime.Now;
      for (int i = 0; i < 1000; i++)
      {
        _gs.SetDMaps(_gs.Player.Loc);
      }
      var elapsed = DateTime.Now - start;
      _gs.UIRef().AlertPlayer($"{elapsed.TotalMilliseconds}ms");
      return "";
    }

    var parts = txt.Split(' ', 2);
    if (parts.Length < 2)
      return "Debug commands are formated: add/give/drop <obj name>";

    switch (parts[0].ToLower())
    {
      case "add":
        return AddMonster(parts[1]);
      case "give":
      case "drop":
        return AddItem(parts[0].ToLower(), parts[1]);
      case "zorkmids":
      case "$":
        return GiveZorkminds(parts[1]);
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

  private string GiveZorkminds(string amount)
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

  private string AddItem(string action, string name)
  {
    bool illusion = false;
    if (name.EndsWith("illusion"))
    {
      illusion = true;
      name = name.Substring(0, name.LastIndexOf(' '));
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
    return Util.Adj8Locs(loc)
        .Where(loc => !_gs.ObjDb.Occupied(loc) 
                          && _gs.CurrentMap.TileAt(loc.Row, loc.Col).Passable()
                          && !_gs.ObjDb.ItemsAt(loc).Where(item => item.HasTrait<BlockTrait>()) .Any())
        .ToList();
  }

  private string AddMonster(string monsterName)
  {
    var adjSpots = AdjSpots(_gs.Player.Loc);

    if (adjSpots.Count == 0)
      return "No open spot to add monster";

    try
    {
      var spawnLoc = adjSpots[_gs.Rng.Next(adjSpots.Count)];
      var monster = MonsterFactory.Get(monsterName, _gs.ObjDb, _gs.Rng);

      _gs.ObjDb.AddNewActor(monster, spawnLoc);
      _gs.AddPerformer(monster);

      return "";
    }
    catch (UnknownMonsterException)
    {
      return $"Unknown monster: {monsterName}";
    }
  }
}