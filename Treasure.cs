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

enum TreasureQuality { Common, Uncommon, Good }

class Treasure
{  
  static readonly List<ItemNames> CommonItems = 
    [ ItemNames.TORCH, ItemNames.TORCH, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.DAGGER, ItemNames.FLASK_OF_BOOZE ];
  static readonly List<ItemNames> UncommonItems = [
      ItemNames.TORCH, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE, ItemNames.POTION_HEALING,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.ANTIDOTE, ItemNames.DAGGER,
      ItemNames.SHORTSHORD, ItemNames.SHIELD, ItemNames.SCROLL_BLINK, ItemNames.HAND_AXE,
      ItemNames.SCROLL_MAGIC_MAP, ItemNames.POTION_COLD_RES, ItemNames.POTION_FIRE_RES,
      ItemNames.POTION_MIND_READING, ItemNames.WAND_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER,
      ItemNames.HELMET, ItemNames.SHIELD, ItemNames.ZORKMIDS_MEDIOCRE, ItemNames.SCROLL_KNOCK,
      ItemNames.LOCK_PICK, ItemNames.SCROLL_IDENTIFY, ItemNames.VIAL_OF_POISON, ItemNames.SCROLL_PROTECTION,
      ItemNames.SCROLL_DISARM, ItemNames.BEETLE_CARAPACE, ItemNames.LEATHER_GLOVES,
      ItemNames.SCROLL_TREASURE_DETECTION, ItemNames.SCROLL_TRAP_DETECTION
  ]; 
  static readonly List<ItemNames> GoodItems = [ 
      ItemNames.ZORKMIDS_MEDIOCRE, ItemNames.ZORKMIDS_GOOD, ItemNames.ZORKMIDS_GOOD,
      ItemNames.POTION_HEALING, ItemNames.POTION_MIND_READING, ItemNames.POTION_COLD_RES,
      ItemNames.POTION_FIRE_RES, ItemNames.SCROLL_BLINK, ItemNames.SCROLL_MAGIC_MAP, ItemNames.SCROLL_RECALL, 
      ItemNames.GUISARME, ItemNames.LONGSWORD, ItemNames.SHORTSHORD, ItemNames.SHIELD, ItemNames.HELMET, 
      ItemNames.STUDDED_LEATHER_ARMOUR, ItemNames.CHAINMAIL, ItemNames.TALISMAN_OF_CIRCUMSPECTION,
      ItemNames.SPEAR, ItemNames.WAND_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER, ItemNames.LEATHER_GLOVES,
      ItemNames.WAND_FROST, ItemNames.WAND_SWAP, ItemNames.RING_OF_PROTECTION, ItemNames.WAND_SLOW_MONSTER,
      ItemNames.POTION_OF_LEVITATION, ItemNames.SCROLL_KNOCK, ItemNames.LOCK_PICK, ItemNames.WAND_FIREBALLS,
      ItemNames.SCROLL_IDENTIFY, ItemNames.VIAL_OF_POISON, ItemNames.SCROLL_PROTECTION,
      ItemNames.GUIDE_AXES, ItemNames.GUIDE_STABBY, ItemNames.GUIDE_SWORDS, ItemNames.BEETLE_CARAPACE,
      ItemNames.HILL_GIANT_ESSENCE, ItemNames.FROST_GIANT_ESSENCE, ItemNames.FIRE_GIANT_ESSENCE,
      ItemNames.SCROLL_DISARM, ItemNames.GUIDE_BOWS, ItemNames.TROLL_BROOCH, ItemNames.SMOULDERING_CHARM,
      ItemNames.CLOAK_OF_PROTECTION, ItemNames.GAUNTLETS_OF_POWER, ItemNames.SCROLL_TREASURE_DETECTION,
      ItemNames.SCROLL_TRAP_DETECTION, ItemNames.SCROLL_SCATTERING, ItemNames.POTION_OBSCURITY,
      ItemNames.FEATHERFALL_BOOTS, ItemNames.WIND_FAN
  ];
  static readonly List<ItemNames> Consumables = [
    ItemNames.POTION_COLD_RES,
    ItemNames.POTION_FIRE_RES,
    ItemNames.POTION_HEALING,
    ItemNames.POTION_MIND_READING,
    ItemNames.POTION_OBSCURITY,
    ItemNames.POTION_OF_LEVITATION,
    ItemNames.SCROLL_BLINK,
    ItemNames.SCROLL_DISARM,
    ItemNames.SCROLL_IDENTIFY,
    ItemNames.SCROLL_KNOCK,
    ItemNames.SCROLL_MAGIC_MAP,
    ItemNames.SCROLL_PROTECTION,
    ItemNames.SCROLL_RECALL,
    ItemNames.SCROLL_SCATTERING,
    ItemNames.SCROLL_TRAP_DETECTION,
    ItemNames.SCROLL_TREASURE_DETECTION,
    ItemNames.VIAL_OF_POISON,
    ItemNames.WIND_FAN
  ];
  public static Item? LootFromTrait(LootTrait trait, Random rng, GameObjectDB objDb)
  {
    if (trait is PoorLootTrait)
    {
      var loot = PoorTreasure(1, rng, objDb);
      return loot[0];
    }
    else if (trait is CarriesTrait carries)
    {
      if (rng.Next(100) < carries.Chance && Enum.TryParse(carries.ItemName.ToUpper(), out ItemNames itemName))
      {
        return ItemFactory.Get(itemName, objDb);
      }
    }
    else if (trait is GoodMagicLootTrait)
    {
      return GoodMagicItem(rng, objDb);
    }
    else if (trait is CoinsLootTrait coins)
    {
      var zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      zorkmids.Value = rng.Next(coins.Min, coins.Max + 1);
      return zorkmids;
    }

    return null;
  }

  public static Item GetTalisam(Random rng, GameObjectDB objDb)
  {
    int roll = rng.Next(7);
    return roll switch
    {
      0 => ItemFactory.Get(ItemNames.SMOULDERING_CHARM, objDb),
      1 => ItemFactory.Get(ItemNames.TROLL_BROOCH, objDb),
      2 => ItemFactory.Get(ItemNames.TALISMAN_OF_CIRCUMSPECTION, objDb),
      3 => ItemFactory.Get(ItemNames.GASTON_BADGE, objDb),
      4 => ItemFactory.Get(ItemNames.LESSER_BURLY_CHARM, objDb),
      5 => ItemFactory.Get(ItemNames.LESSER_GRACE_CHARM, objDb),
      _ => ItemFactory.Get(ItemNames.LESSER_HEALTH_CHARM, objDb),
    };
  }

  public static Item GoodMagicItem(Random rng, GameObjectDB objDb)
  {
    int roll = rng.Next(25);
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
      _ => ItemFactory.Get(ItemNames.SILVER_LONGSWORD, objDb),
    };
  }

  public static List<Item> PoorTreasure(int numOfItems, Random rng, GameObjectDB objDb)
  {
    List<Item> loot = [];

    for (int j = 0; j < numOfItems; j++)
    {
      var name = CommonItems[rng.Next(CommonItems.Count)];
      loot.Add(GenerateItem(name, objDb, rng));
    }
    
    return loot;
  }

  static void AddObjectToLevel(Item item, GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
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

  public static Item ItemByQuality(TreasureQuality quality, GameObjectDB objDb, Random rng)
  {
    var name = quality switch
    {
      TreasureQuality.Uncommon => UncommonItems[rng.Next(UncommonItems.Count)],
      TreasureQuality.Good => GoodItems[rng.Next(GoodItems.Count)],
      _ => CommonItems[rng.Next(CommonItems.Count)],
    };
    
    return GenerateItem(name, objDb, rng);
  }

  public static Item MinorGift(GameObjectDB objDb, Random rng) => rng.Next(8) switch
  {
    0 => ItemFactory.Get(ItemNames.POTION_HEALING, objDb),
    1 => ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb),
    2 => ItemFactory.Get(ItemNames.ANTIDOTE, objDb),
    3 => ItemFactory.Get(ItemNames.SCROLL_MAGIC_MAP, objDb),
    4 => ItemFactory.Get(ItemNames.SCROLL_PROTECTION, objDb),
    5 => ItemFactory.Get(ItemNames.SCROLL_DISARM, objDb),
    6 => ItemFactory.Get(ItemNames.POTION_OBSCURITY, objDb),
    _ => ItemFactory.Get(ItemNames.POTION_MIND_READING, objDb)
  };

  static Item GenerateItem(ItemNames name, GameObjectDB objDb, Random rng)
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

  public static List<Item> GraveContents(GameState gs, int level, Random rng) 
  {
    GameObjectDB objDb = gs.ObjDb;
    List<Item> items = [];

    Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
    zorkmids.Value = rng.Next(25, 51);
    items.Add(zorkmids);

    switch (rng.Next(30))
    {
      case 0:
        items.Add(ItemFactory.Get(ItemNames.SCROLL_RECALL, objDb));
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
        EffectApplier.Apply(DamageType.Rust, gs, helm, null);
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
        EffectApplier.Apply(DamageType.Rust, gs, claymore, null);
        items.Add(claymore);
        break;
      default:
        break;
    }

    if (rng.NextDouble() < 0.5)
      items.Add(ItemFactory.Get(ItemNames.SKULL, objDb));

    return items;
  }

  static void AddConsumables(GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    int n = rng.Next(2, 5);
    while (n > 0)
    {
      ItemNames name = Consumables[rng.Next(Consumables.Count)];
      Item item = ItemFactory.Get(name, objDb);
      AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      --n;
    }
  }

  public static void AddTreasureToDungeonLevel(GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    if (levelNum == 0) 
    {
      int numItems = rng.Next(1, 4);
      for (int j = 0; j < numItems; j++) 
      {
        Item item = ItemByQuality(TreasureQuality.Common, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
      Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
      zorkmids.Value = rng.Next(10,21);
      AddObjectToLevel(zorkmids, objDb, level, dungeonID, levelNum, rng);
    }
    else if (levelNum == 1)
    {
      int numItems = rng.Next(3, 8);
      for (int j = 0; j < numItems; j++)
      {
        double roll = rng.NextDouble();
        TreasureQuality quality = roll <= 0.9 ? TreasureQuality.Common : TreasureQuality.Uncommon;
        Item item = ItemByQuality(quality, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }

      Item goodItem = ItemByQuality(TreasureQuality.Good, objDb, rng);
      AddObjectToLevel(goodItem, objDb, level, dungeonID, levelNum, rng);

      for (int j = 0; j < rng.Next(1, 3); j++)
      {
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(10,21);
        AddObjectToLevel(zorkmids, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 2 || levelNum == 3)
    {
      int numItems = rng.Next(3, 8);
      for (int j = 0; j < numItems; j++)
      {
        TreasureQuality quality;
        double roll = rng.NextDouble();
        if (roll <= 0.4)
          quality = TreasureQuality.Common;
        else if (roll <= 0.9)
          quality = TreasureQuality.Uncommon;
        else
          quality = TreasureQuality.Good;
        Item item = ItemByQuality(quality, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);      
      }

      for (int j = 0; j < rng.Next(1, 4); j++)
      {
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(15,36);
        AddObjectToLevel(zorkmids, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 4)
    {
      int numItems = rng.Next(3, 8);
      for (int j = 0; j < numItems; j++)
      {
        TreasureQuality quality;
        double roll = rng.NextDouble();
        if (roll <= 0.2)
          quality = TreasureQuality.Common;
        else if (roll <= 0.7)
          quality = TreasureQuality.Uncommon;          
        else
          quality = TreasureQuality.Good;          
        Item item = ItemByQuality(quality, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }

      for (int j = 0; j < rng.Next(1, 4); j++)
      {
        Item zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(15,36);
        AddObjectToLevel(zorkmids, objDb, level, dungeonID, levelNum, rng);
      }
    }

    AddConsumables(objDb, level, dungeonID, levelNum, rng);
  }
}
