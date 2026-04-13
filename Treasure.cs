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
  static readonly List<(ItemNames, int)> CommonItems =[ 
      (ItemNames.TORCH, 1), (ItemNames.TORCH, 2), (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.ZORKMIDS_PITTANCE, 1),
      (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.SCROLL_BLINK, 1),
      (ItemNames.POTION_HEALING, 1), (ItemNames.ANTIDOTE, 1), (ItemNames.DAGGER, 1), (ItemNames.FLASK_OF_BOOZE, 1) 
    ];

  static readonly List<(ItemNames, int)> UncommonItems = [
      (ItemNames.TORCH, 2), (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.ZORKMIDS_PITTANCE, 1),
      (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.ZORKMIDS_PITTANCE, 1), (ItemNames.POTION_HEALING, 1),
      (ItemNames.POTION_HEALING, 1), (ItemNames.ANTIDOTE, 1), (ItemNames.ANTIDOTE, 1), (ItemNames.DAGGER, 1), (ItemNames.POTION_CLARITY, 1),
      (ItemNames.SHORTSHORD, 1), (ItemNames.SHIELD, 1), (ItemNames.SCROLL_BLINK, 1), (ItemNames.SCROLL_BLINK, 2), (ItemNames.POTION_DRAGON_BREATH, 1),
      (ItemNames.SCROLL_BLINK, 1), (ItemNames.SCROLL_MAGIC_MAP, 1), (ItemNames.SCROLL_MAGIC_MAP, 1), (ItemNames.HAND_AXE, 1),
      (ItemNames.POTION_COLD_RES, 1), (ItemNames.POTION_FIRE_RES, 1), (ItemNames.POTION_MIND_READING, 1), (ItemNames.WAND_MAGIC_MISSILES, 1),
      (ItemNames.WAND_HEAL_MONSTER, 1), (ItemNames.HELMET, 1), (ItemNames.SHIELD, 1), (ItemNames.ZORKMIDS_MEDIOCRE, 1), 
      (ItemNames.SCROLL_KNOCK, 2), (ItemNames.LOCK_PICK, 1), (ItemNames.VIAL_OF_POISON, 1), (ItemNames.SCROLL_PROTECTION, 1),
      (ItemNames.SCROLL_DISARM, 1), (ItemNames.BEETLE_CARAPACE, 1), (ItemNames.LEATHER_GLOVES, 1), (ItemNames.MITHRIL_ORE, 1),
      (ItemNames.SCROLL_TREASURE_DETECTION, 1), (ItemNames.SCROLL_TRAP_DETECTION, 1), (ItemNames.SKELETON_KEY, 1), (ItemNames.PICKAXE, 1),
      (ItemNames.POTION_HEROISM, 1), (ItemNames.POTION_DESCENT, 1), (ItemNames.SCROLL_STAINLESS, 1),
      (ItemNames.POTION_FORGETFULNESS, 1), (ItemNames.EMERGENCY_DOOR, 1), (ItemNames.KNOCKBACK_ARROW, 3), (ItemNames.LONGBOW, 1),
      (ItemNames.ARROW_BANISHMENT, 3), (ItemNames.SPIDER_ARROW, 3), (ItemNames.SILVER_ARROW, 3), (ItemNames.YENDORIAN_SODA, 1),
      (ItemNames.WAND_SUMMONING, 1), (ItemNames.BLINDFOLD, 1), (ItemNames.WOOL_CLOAK, 1)
  ];

  static readonly List<(ItemNames, int)> GoodItems = [
      (ItemNames.ZORKMIDS_MEDIOCRE, 1), (ItemNames.ZORKMIDS_GOOD, 1), (ItemNames.ZORKMIDS_GOOD, 1),
      (ItemNames.POTION_HEALING, 1), (ItemNames.POTION_MIND_READING, 1), (ItemNames.POTION_COLD_RES, 1),
      (ItemNames.POTION_FIRE_RES, 1), (ItemNames.SCROLL_BLINK, 2), (ItemNames.SCROLL_BLINK, 1), (ItemNames.SCROLL_MAGIC_MAP, 1),
      (ItemNames.SCROLL_MAGIC_MAP, 1), (ItemNames.SCROLL_ESCAPE, 1), (ItemNames.GUISARME, 1), (ItemNames.LONGSWORD, 1), (ItemNames.SHORTSHORD, 1),
      (ItemNames.SHIELD, 1), (ItemNames.HELMET, 1), (ItemNames.STUDDED_LEATHER_ARMOUR, 1), (ItemNames.CHAINMAIL, 1), (ItemNames.TALISMAN_OF_CIRCUMSPECTION, 1),
      (ItemNames.SPEAR, 1), (ItemNames.WAND_MAGIC_MISSILES, 1), (ItemNames.WAND_HEAL_MONSTER, 1), (ItemNames.LEATHER_GLOVES, 1),
      (ItemNames.WAND_FROST, 1), (ItemNames.WAND_SWAP, 1), (ItemNames.RING_OF_PROTECTION, 1), (ItemNames.WAND_SLOW_MONSTER, 1),
      (ItemNames.POTION_OF_LEVITATION, 1), (ItemNames.SCROLL_KNOCK, 2), (ItemNames.LOCK_PICK, 1), (ItemNames.WAND_FIREBALLS, 1),
      (ItemNames.VIAL_OF_POISON, 1), (ItemNames.SCROLL_PROTECTION, 1), (ItemNames.LEMBAS, 1), (ItemNames.SCROLL_ENCHANTING, 1),
      (ItemNames.GUIDE_AXES, 1), (ItemNames.GUIDE_STABBY, 1), (ItemNames.GUIDE_SWORDS, 1), (ItemNames.BEETLE_CARAPACE, 1),
      (ItemNames.HILL_GIANT_ESSENCE, 1), (ItemNames.FROST_GIANT_ESSENCE, 1), (ItemNames.FIRE_GIANT_ESSENCE, 1),
      (ItemNames.SCROLL_DISARM, 2), (ItemNames.GUIDE_BOWS, 1), (ItemNames.TROLL_BROOCH, 1), (ItemNames.SMOULDERING_CHARM, 1),
      (ItemNames.CLOAK_OF_PROTECTION, 1), (ItemNames.GAUNTLETS_OF_POWER, 1), (ItemNames.SCROLL_TREASURE_DETECTION, 1),
      (ItemNames.SCROLL_TRAP_DETECTION, 1), (ItemNames.SCROLL_SCATTERING, 2), (ItemNames.POTION_OBSCURITY, 1), (ItemNames.POTION_DRAGON_BREATH, 2),
      (ItemNames.FEATHERFALL_BOOTS, 1), (ItemNames.WIND_FAN, 1), (ItemNames.SKELETON_KEY, 1),
      (ItemNames.TINCTURE_CELERITY, 1), (ItemNames.POTION_HEROISM, 1), (ItemNames.POTION_DESCENT, 1), (ItemNames.SCROLL_STAINLESS, 1),
      (ItemNames.HOLY_WATER, 1), (ItemNames.SNEAKERS, 1), (ItemNames.MOON_MANTLE, 1), (ItemNames.CLAYMORE, 1), (ItemNames.KNOCKBACK_ARROW, 3),
      (ItemNames.SNOWBURST_ARROW, 3), (ItemNames.FIREBURST_ARROW,3), (ItemNames.LONGBOW, 1), (ItemNames.ARROW_BANISHMENT, 3),
      (ItemNames.GLOVES_OF_ARCHERY, 1), (ItemNames.SPIDER_ARROW, 3), (ItemNames.SILVER_ARROW, 3), (ItemNames.PHALANX_PHOLIO_2, 1),
      (ItemNames.HALFLING_CUPCAKE, 1), (ItemNames.YENDORIAN_SODA, 1), (ItemNames.GINGERBREAD_MAN, 1)
  ];

  static readonly List<(ItemNames, int)> RareItems = [
      (ItemNames.GUISARME, 1), (ItemNames.LONGSWORD, 1), (ItemNames.SHORTSHORD, 1), (ItemNames.SHIELD, 1), (ItemNames.HELMET, 1), 
      (ItemNames.STUDDED_LEATHER_ARMOUR, 1), (ItemNames.CHAINMAIL, 1),
      (ItemNames.LEATHER_GLOVES, 1), (ItemNames.CLOAK_OF_PROTECTION, 1), (ItemNames.GAUNTLETS_OF_POWER, 1),
      (ItemNames.RING_OF_PROTECTION, 1), (ItemNames.WAND_FIREBALLS, 1), (ItemNames.SCROLL_ENCHANTING, 1),
      (ItemNames.HILL_GIANT_ESSENCE, 1), (ItemNames.FROST_GIANT_ESSENCE, 1), (ItemNames.FIRE_GIANT_ESSENCE, 1),
      (ItemNames.GUIDE_BOWS, 1), (ItemNames.GUIDE_AXES, 1), (ItemNames.GUIDE_STABBY, 1), (ItemNames.GUIDE_SWORDS, 1),
      (ItemNames.CRIMSON_KING_WARD, 1), (ItemNames.CROESUS_CHARM, 1), (ItemNames.CUTPURSE_CREST, 1),
      (ItemNames.GASTON_BADGE, 1), (ItemNames.LESSER_BURLY_CHARM, 1), (ItemNames.LESSER_GRACE_CHARM, 1),
      (ItemNames.LESSER_HEALTH_CHARM, 1), (ItemNames.SMOULDERING_CHARM, 1), (ItemNames.TALISMAN_OF_CIRCUMSPECTION, 1),
      (ItemNames.TROLL_BROOCH, 1), (ItemNames.RUNE_OF_LASHING, 1), (ItemNames.FEARFUL_RUNE, 1), (ItemNames.RUNE_OF_PARRYING, 1),
      (ItemNames.MOON_MANTLE, 1), (ItemNames.MOON_LYRE, 1), (ItemNames.PHALANX_PHOLIO_2, 1), (ItemNames.HALFLING_CUPCAKE, 1),
      (ItemNames.VIAL_SPRITE_BLOOD, 1)
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
    ItemNames.EMERGENCY_DOOR,
    ItemNames.YENDORIAN_SODA,
    ItemNames.GINGERBREAD_MAN
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
    int roll = rng.Next(29);
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
      28 => ItemFactory.Get(ItemNames.GLOVES_OF_ARCHERY, objDb),
      _ => ItemFactory.Get(ItemNames.SILVER_LONGSWORD, objDb),
    };
  }

  public static List<Item> PoorTreasure(int numOfItems, Rng rng, GameObjectDB objDb)
  {
    List<Item> loot = [];

    for (int j = 0; j < numOfItems; j++)
    {
      var (name, _) = CommonItems[rng.Next(CommonItems.Count)];
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

  public static List<Item> TreasureByQuality(TreasureQuality quality, GameObjectDB objDb, Rng rng)
  {
    var (name, maxCount) = quality switch
    {
      TreasureQuality.Uncommon => UncommonItems[rng.Next(UncommonItems.Count)],
      TreasureQuality.Good => GoodItems[rng.Next(GoodItems.Count)],
      TreasureQuality.Rare => RareItems[rng.Next(RareItems.Count)],
      _ => CommonItems[rng.Next(CommonItems.Count)],
    };

    int count = maxCount > 1 ? rng.Next(maxCount ) + 1 : 1;
    List<Item> items = [];
    for (int i = 0; i < count; i++)
    {
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
      items.Add(item);
    }

    return items;
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