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
    {"wand of magic missiles", ItemNames.WAND_OF_MAGIC_MISSILES},
    {"wand of swap", ItemNames.WAND_SWAP},
    {"wand of heal monster", ItemNames.WAND_HEAL_MONSTER},
    {"wand of fireballs", ItemNames.WAND_FIREBALLS},
    {"wand of frost", ItemNames.WAND_FROST},
    {"ring of protection", ItemNames.RING_OF_PROTECTION},
    {"ring of aggression", ItemNames.RING_OF_AGGRESSION},
    {"vial of poison", ItemNames.VIAL_OF_POISON},
    {"ghostcap mushroom", ItemNames.GHOSTCAP_MUSHROOM},
    {"talisman of circumspection", ItemNames.TALISMAN_OF_CIRCUMSPECTION},
    {"blindfold", ItemNames.BLINDFOLD}
  };    

  public string DoCommand(string txt)
  {
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
      default:
        return "Unknown debug command";
    }
  }

  private string AddItem(string action, string name)
  {
    if (ItemMap.TryGetValue(name, out var itemEnum))
    {
      Item item = ItemFactory.Get(itemEnum, _gs.ObjDb);    
      if (action == "give")
        _gs.Player.Inventory.Add(item, _gs.Player.ID);
      else       
        _gs.ObjDb.SetToLoc(_gs.Player.Loc, item);   
      return "";   
    }

    return $"Unknown item: {name}";
  }

  private string AddMonster(string monsterName)
  {
    var adjSpots = Util.Adj8Locs(_gs.Player.Loc)
        .Where(loc => !_gs.ObjDb.Occupied(loc) && _gs.CurrentMap.TileAt(loc.Row, loc.Col).Passable())
        .ToList();

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