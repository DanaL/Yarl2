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

enum TreasureQuality { Common, Uncommon, Good, Rare }

class Treasure
{  
  static readonly List<ItemNames> CommonItems = 
    [ ItemNames.TORCH, ItemNames.TORCH, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE, ItemNames.SCROLL_BLINK,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.DAGGER, ItemNames.FLASK_OF_BOOZE ];

  static readonly List<ItemNames> UncommonItems = [
      ItemNames.TORCH, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE, ItemNames.POTION_HEALING,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.ANTIDOTE, ItemNames.DAGGER, ItemNames.POTION_CLARITY,
      ItemNames.SHORTSHORD, ItemNames.SHIELD, ItemNames.SCROLL_BLINK, ItemNames.SCROLL_BLINK, ItemNames.POTION_DRAGON_BREATH,
      ItemNames.SCROLL_BLINK, ItemNames.SCROLL_MAGIC_MAP, ItemNames.SCROLL_MAGIC_MAP, ItemNames.HAND_AXE,
      ItemNames.POTION_COLD_RES, ItemNames.POTION_FIRE_RES, ItemNames.POTION_MIND_READING, ItemNames.WAND_MAGIC_MISSILES,
      ItemNames.WAND_HEAL_MONSTER, ItemNames.HELMET, ItemNames.SHIELD, ItemNames.ZORKMIDS_MEDIOCRE, ItemNames.SCROLL_KNOCK,
      ItemNames.SCROLL_KNOCK, ItemNames.LOCK_PICK, ItemNames.VIAL_OF_POISON, ItemNames.SCROLL_PROTECTION,
      ItemNames.SCROLL_DISARM, ItemNames.BEETLE_CARAPACE, ItemNames.LEATHER_GLOVES, ItemNames.MITHRIL_ORE,
      ItemNames.SCROLL_TREASURE_DETECTION, ItemNames.SCROLL_TRAP_DETECTION, ItemNames.SKELETON_KEY, ItemNames.PICKAXE,
      ItemNames.POTION_HEROISM, ItemNames.POTION_DESCENT, ItemNames.SCROLL_STAINLESS,
      ItemNames.POTION_FORGETFULNESS, ItemNames.EMERGENCY_DOOR
  ]; 

  static readonly List<ItemNames> GoodItems = [ 
      ItemNames.ZORKMIDS_MEDIOCRE, ItemNames.ZORKMIDS_GOOD, ItemNames.ZORKMIDS_GOOD,
      ItemNames.POTION_HEALING, ItemNames.POTION_MIND_READING, ItemNames.POTION_COLD_RES,
      ItemNames.POTION_FIRE_RES, ItemNames.SCROLL_BLINK, ItemNames.SCROLL_BLINK, ItemNames.SCROLL_MAGIC_MAP,
      ItemNames.SCROLL_MAGIC_MAP, ItemNames.SCROLL_ESCAPE, ItemNames.GUISARME, ItemNames.LONGSWORD, ItemNames.SHORTSHORD,
      ItemNames.SHIELD, ItemNames.HELMET, ItemNames.STUDDED_LEATHER_ARMOUR, ItemNames.CHAINMAIL, ItemNames.TALISMAN_OF_CIRCUMSPECTION,
      ItemNames.SPEAR, ItemNames.WAND_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER, ItemNames.LEATHER_GLOVES,
      ItemNames.WAND_FROST, ItemNames.WAND_SWAP, ItemNames.RING_OF_PROTECTION, ItemNames.WAND_SLOW_MONSTER,
      ItemNames.POTION_OF_LEVITATION, ItemNames.SCROLL_KNOCK, ItemNames.LOCK_PICK, ItemNames.WAND_FIREBALLS,
      ItemNames.VIAL_OF_POISON, ItemNames.SCROLL_PROTECTION, ItemNames.LEMBAS, ItemNames.SCROLL_ENCHANTING,
      ItemNames.GUIDE_AXES, ItemNames.GUIDE_STABBY, ItemNames.GUIDE_SWORDS, ItemNames.BEETLE_CARAPACE,
      ItemNames.HILL_GIANT_ESSENCE, ItemNames.FROST_GIANT_ESSENCE, ItemNames.FIRE_GIANT_ESSENCE,
      ItemNames.SCROLL_DISARM, ItemNames.GUIDE_BOWS, ItemNames.TROLL_BROOCH, ItemNames.SMOULDERING_CHARM,
      ItemNames.CLOAK_OF_PROTECTION, ItemNames.GAUNTLETS_OF_POWER, ItemNames.SCROLL_TREASURE_DETECTION,
      ItemNames.SCROLL_TRAP_DETECTION, ItemNames.SCROLL_SCATTERING, ItemNames.POTION_OBSCURITY, ItemNames.POTION_DRAGON_BREATH,
      ItemNames.FEATHERFALL_BOOTS, ItemNames.WIND_FAN, ItemNames.SKELETON_KEY,
      ItemNames.TINCTURE_CELERITY, ItemNames.POTION_HEROISM, ItemNames.POTION_DESCENT, ItemNames.SCROLL_STAINLESS,
      ItemNames.HOLY_WATER, ItemNames.SNEAKERS, ItemNames.MOON_MANTLE
  ];

  static readonly List<ItemNames> RareItems = [
      ItemNames.GUISARME, ItemNames.LONGSWORD, ItemNames.SHORTSHORD, ItemNames.SPEAR,
      ItemNames.SHIELD, ItemNames.HELMET, ItemNames.STUDDED_LEATHER_ARMOUR, ItemNames.CHAINMAIL,
      ItemNames.LEATHER_GLOVES, ItemNames.CLOAK_OF_PROTECTION, ItemNames.GAUNTLETS_OF_POWER,
      ItemNames.RING_OF_PROTECTION, ItemNames.WAND_FIREBALLS, ItemNames.SCROLL_ENCHANTING, 
      ItemNames.HILL_GIANT_ESSENCE, ItemNames.FROST_GIANT_ESSENCE, ItemNames.FIRE_GIANT_ESSENCE, 
      ItemNames.GUIDE_BOWS, ItemNames.GUIDE_AXES, ItemNames.GUIDE_STABBY, ItemNames.GUIDE_SWORDS,
      ItemNames.CRIMSON_KING_WARD, ItemNames.CROESUS_CHARM, ItemNames.CUTPURSE_CREST,
      ItemNames.GASTON_BADGE, ItemNames.LESSER_BURLY_CHARM, ItemNames.LESSER_GRACE_CHARM, 
      ItemNames.LESSER_HEALTH_CHARM, ItemNames.SMOULDERING_CHARM, ItemNames.TALISMAN_OF_CIRCUMSPECTION, 
      ItemNames.TROLL_BROOCH, ItemNames.RUNE_OF_LASHING, ItemNames.FEARFUL_RUNE, ItemNames.RUNE_OF_PARRYING, 
      ItemNames.MOON_MANTLE, ItemNames.MOON_LYRE
  ];

  public static readonly List<ItemNames> Consumables = [
    ItemNames.POTION_COLD_RES,
    ItemNames.POTION_FIRE_RES,
    ItemNames.POTION_HEALING,
    ItemNames.POTION_MIND_READING,
    ItemNames.POTION_OBSCURITY,
    ItemNames.POTION_OF_LEVITATION,
    ItemNames.POTION_HEROISM,
    ItemNames.SCROLL_BLINK,
    ItemNames.SCROLL_DISARM,
    ItemNames.SCROLL_KNOCK,
    ItemNames.SCROLL_MAGIC_MAP,
    ItemNames.SCROLL_PROTECTION,
    ItemNames.SCROLL_ESCAPE,
    ItemNames.SCROLL_SCATTERING,
    ItemNames.SCROLL_TRAP_DETECTION,
    ItemNames.SCROLL_TREASURE_DETECTION,
    ItemNames.VIAL_OF_POISON,
    ItemNames.WIND_FAN,
    ItemNames.TINCTURE_CELERITY,
    ItemNames.POTION_DESCENT,
    ItemNames.SCROLL_STAINLESS,
    ItemNames.SCROLL_ENCHANTING,
    ItemNames.POTION_CLARITY,
    ItemNames.POTION_FORGETFULNESS,
    ItemNames.EMERGENCY_DOOR
  ];

  public static Item GetTalisman(Rng rng, GameObjectDB objDb)
  {
    List<ItemNames> names = [ ItemNames.CRIMSON_KING_WARD, ItemNames.CROESUS_CHARM, ItemNames.CUTPURSE_CREST,
      ItemNames.GASTON_BADGE, ItemNames.LESSER_BURLY_CHARM, ItemNames.LESSER_GRACE_CHARM, ItemNames.LESSER_HEALTH_CHARM,
      ItemNames.SMOULDERING_CHARM, ItemNames.TALISMAN_OF_CIRCUMSPECTION, ItemNames.TROLL_BROOCH ];

    ItemNames name = names[rng.Next(names.Count)];
    return ItemFactory.Get(name, objDb);
  }

  public static Item GoodMagicItem(Rng rng, GameObjectDB objDb)
  {
    int roll = rng.Next(28);
    return roll switch
    {
      0 => ItemFactory.Get(ItemNames.WAND_FIREBALLS, objDb),
      1 => ItemFactory.Get(ItemNames.WAND_FROST, objDb),
      2 => ItemFactory.Get(ItemNames.WAND_SWAP, objDb),
      3 => ItemFactory.Get(ItemNames.WAND_MAGIC_MISSILES, objDb),
      4 => ItemFactory.Get(ItemNames.SMOULDERING_CHARM, objDb),
      5 => ItemFactory.Get(ItemNames.TROLL_BROOCH, objDb),
      6 => ItemFactory.Get(ItemNames.HILL_GIANT_ESSENCE, objDb),
      7 => ItemFactory.Get(ItemNames.FROST_GIANT_ESSENCE, objDb),
      8 => ItemFactory.Get(ItemNames.FIRE_GIANT_ESSENCE, objDb),
      9 => ItemFactory.Get(ItemNames.TALISMAN_OF_CIRCUMSPECTION, objDb),
      10 => ItemFactory.Get(ItemNames.ANTISNAIL_SANDALS, objDb),
      11 => ItemFactory.Get(ItemNames.BOOTS_OF_WATER_WALKING, objDb),
      12 => ItemFactory.Get(ItemNames.GASTON_BADGE, objDb),
      13 => ItemFactory.Get(ItemNames.LESSER_BURLY_CHARM, objDb),
      14 => ItemFactory.Get(ItemNames.LESSER_GRACE_CHARM, objDb),
      15 => ItemFactory.Get(ItemNames.LESSER_HEALTH_CHARM, objDb),
      16 => ItemFactory.Get(ItemNames.RING_OF_PROTECTION, objDb),
      18 => ItemFactory.Get(ItemNames.RING_OF_ADORNMENT, objDb),
      19 => ItemFactory.Get(ItemNames.WAND_SLOW_MONSTER, objDb),
      20 => ItemFactory.Get(ItemNames.FEATHERFALL_BOOTS, objDb),
      21 => ItemFactory.Get(ItemNames.WIND_FAN, objDb),
      22 => ItemFactory.Get(ItemNames.GAUNTLETS_OF_POWER, objDb),
      23 => ItemFactory.Get(ItemNames.CLOAK_OF_PROTECTION, objDb),
      24 => ItemFactory.Get(ItemNames.POTION_OBSCURITY, objDb),
      25 => ItemFactory.Get(ItemNames.CRIMSON_KING_WARD, objDb),
      26 => ItemFactory.Get(ItemNames.SNEAKERS, objDb),
      27 => ItemFactory.Get(ItemNames.MOON_MANTLE, objDb),
      _ => ItemFactory.Get(ItemNames.SILVER_LONGSWORD, objDb),
    };
  }

  public static List<Item> PoorTreasure(int numOfItems, Rng rng, GameObjectDB objDb)
  {
    List<Item> loot = [];

    for (int j = 0; j < numOfItems; j++)
    {
      var name = CommonItems[rng.Next(CommonItems.Count)];
      loot.Add(GenerateItem(name, objDb, rng));
    }
    
    return loot;
  }

  static public void AddObjectToLevel(Item item, GameObjectDB objDb, Map level, int dungeonID, int levelNum, Rng rng)
  {
    // I'm generating this list for every item placed, which is pretty dumb
    List<Loc> candidates = [];
    for (int r = 0; r < level.Height; r++)
    {
      for (int c = 0; c < level.Width; c++)
      {
        switch (level.TileAt(r, c).Type)
        {
          case TileType.DungeonFloor:
          case TileType.HiddenTrapDoor:
          case TileType.TrapDoor:
          case TileType.TeleportTrap:
          case TileType.HiddenTeleportTrap:
          case TileType.WoodBridge:
            Loc floor = new(dungeonID, levelNum, r, c);
            if (Util.GoodFloorSpace(objDb, floor))
             candidates.Add(floor);
            break;
        }
      }
    }

    Loc loc = candidates[rng.Next(candidates.Count)];
    objDb.SetToLoc(loc, item);
  }

  public static Item ItemByQuality(TreasureQuality quality, GameObjectDB objDb, Rng rng)
  {
    var name = quality switch
    {
      TreasureQuality.Uncommon => UncommonItems[rng.Next(UncommonItems.Count)],
      TreasureQuality.Good => GoodItems[rng.Next(GoodItems.Count)],
      TreasureQuality.Rare => RareItems[rng.Next(RareItems.Count)],
      _ => CommonItems[rng.Next(CommonItems.Count)],
    };
    
    Item item = GenerateItem(name, objDb, rng);

    if (quality == TreasureQuality.Rare)
    {
      switch (item.Type)
      {
        case ItemType.Weapon:
        case ItemType.Bow:
          int bonus = rng.Next(5) == 0 ? 2 : 1;
          item.Traits.Add(new WeaponBonusTrait() { Bonus = bonus, SourceId = item.ID });
          break;
        case ItemType.Armour:
          if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait at)
          {
            at.Bonus += rng.Next(5) == 0 ? 2 : 1;
          }
          break;
      }
    }

    return GenerateItem(name, objDb, rng);
  }

  public static Item MinorGift(GameObjectDB objDb, Rng rng) => rng.Next(10) switch
  {
    0 => ItemFactory.Get(ItemNames.POTION_HEALING, objDb),
    1 => ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb),
    2 => ItemFactory.Get(ItemNames.ANTIDOTE, objDb),
    3 => ItemFactory.Get(ItemNames.SCROLL_MAGIC_MAP, objDb),
    4 => ItemFactory.Get(ItemNames.SCROLL_PROTECTION, objDb),
    5 => ItemFactory.Get(ItemNames.SCROLL_DISARM, objDb),
    6 => ItemFactory.Get(ItemNames.POTION_OBSCURITY, objDb),
    7 => ItemFactory.Get(ItemNames.TINCTURE_CELERITY, objDb),
    8 => ItemFactory.Get(ItemNames.LEMBAS, objDb),
    _ => ItemFactory.Get(ItemNames.POTION_MIND_READING, objDb)
  };

  static Item GenerateItem(ItemNames name, GameObjectDB objDb, Rng rng)
  {
    Item zorkmids;
    switch (name) 
    {
      case ItemNames.ZORKMIDS_PITTANCE:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(1, 21);
        return zorkmids;
      case ItemNames.ZORKMIDS_MEDIOCRE:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(10, 31);
        return zorkmids;
      case ItemNames.ZORKMIDS_GOOD:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(15, 51);
        return zorkmids;
      default:
        return ItemFactory.Get(name, objDb);
    } 
  }

  public static List<Item> GraveContents(GameState gs, int level, Rng rng) 
  {
    GameObjectDB objDb = gs.ObjDb;
    List<Item> items = [];

    Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
    zorkmids.Value = rng.Next(25, 51);
    items.Add(zorkmids);

    switch (rng.Next(30))
    {
      case 0:
        items.Add(ItemFactory.Get(ItemNames.SCROLL_ESCAPE, objDb));
        break;
      case 1:
        Item axe = ItemFactory.Get(ItemNames.HAND_AXE, objDb);
        if (rng.NextDouble() < 0.5)
          axe.Traits.Add(new WeaponBonusTrait() { Bonus = 1 });
        else 
        {
          axe.Traits.OfType<MetalTrait>().First().Type = Metals.Silver;
          axe.Traits.Add(new AdjectiveTrait("silver"));
        }
        items.Add(axe);
        break;
      case 2:
        items.Add(ItemFactory.Get(ItemNames.LONGBOW, objDb));
        break;
      case 3:
        Item helm = ItemFactory.Get(ItemNames.HELMET, objDb);
        Effects.Apply(DamageType.Rust, gs, helm, null);
        items.Add(helm);
        break;
      case 4:
       Item sword = ItemFactory.Get(ItemNames.LONGSWORD, objDb);
        if (rng.NextDouble() < 0.5)
          sword.Traits.Add(new WeaponBonusTrait() { Bonus = 1 });
        else 
        {
          sword.Traits.OfType<MetalTrait>().First().Type = Metals.Silver;
          sword.Traits.Add(new AdjectiveTrait("silver"));
        }
        items.Add(sword);
        break;
      case 5:
        items.Add(ItemFactory.Get(ItemNames.RING_OF_PROTECTION, objDb));
        break;
      case 6:
        items.Add(ItemFactory.Get(ItemNames.GHOSTCAP_MUSHROOM, objDb));
        break;
      case 7:
        Item claymore = ItemFactory.Get(ItemNames.CLAYMORE, objDb);
        Effects.Apply(DamageType.Rust, gs, claymore, null);
        items.Add(claymore);
        break;
      default:
        break;
    }

    if (rng.NextDouble() < 0.5)
      items.Add(ItemFactory.Get(ItemNames.SKULL, objDb));

    return items;
  }  
}