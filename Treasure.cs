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

class Treasure
{  
  static readonly List<ItemNames> CommonItems = 
    [ ItemNames.TORCH, ItemNames.TORCH, ItemNames.TORCH, ItemNames.TORCH,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.DAGGER, ];
  static readonly List<ItemNames> UncommonItems = [
      ItemNames.TORCH, ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE,
      ItemNames.ZORKMIDS_PITTANCE, ItemNames.ZORKMIDS_PITTANCE, ItemNames.POTION_HEALING,
      ItemNames.POTION_HEALING, ItemNames.ANTIDOTE, ItemNames.ANTIDOTE, ItemNames.DAGGER,
      ItemNames.SHORTSHORD, ItemNames.SHIELD, ItemNames.SCROLL_BLINK, ItemNames.HAND_AXE,
      ItemNames.SCROLL_MAGIC_MAP, ItemNames.POTION_COLD_RES, ItemNames.POTION_FIRE_RES,
      ItemNames.POTION_MIND_READING, ItemNames.WAND_OF_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER,
      ItemNames.HELMET, ItemNames.SHIELD, ItemNames.ZORKMIDS_MEDIOCRE ]; 
  static readonly List<ItemNames> GoodItems = [ 
      ItemNames.ZORKMIDS_MEDIOCRE, ItemNames.ZORKMIDS_GOOD, ItemNames.ZORKMIDS_GOOD,
      ItemNames.POTION_HEALING, ItemNames.POTION_MIND_READING, ItemNames.POTION_COLD_RES,
      ItemNames.POTION_FIRE_RES, ItemNames.SCROLL_BLINK, ItemNames.SCROLL_BLINK,
      ItemNames.SCROLL_MAGIC_MAP, ItemNames.SCROLL_MAGIC_MAP, ItemNames.SCROLL_RECALL,
      ItemNames.GUISARME, ItemNames.LONGSWORD, ItemNames.SHORTSHORD, ItemNames.SHIELD,
      ItemNames.HELMET, ItemNames.STUDDED_LEATHER_ARMOUR, ItemNames.CHAINMAIL,
      ItemNames.SPEAR, ItemNames.WAND_OF_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER,
      ItemNames.WAND_FROST, ItemNames.WAND_SWAP, ItemNames.RING_OF_PROTECTION,
      ItemNames.POTION_OF_LEVITATION ];

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
    List<Loc> candidates = [];
    for (int r = 0; r < level.Height; r++)
    {
      for (int c = 0; c < level.Width; c++)
      {
        switch (level.TileAt(r, c).Type)
        {
          case TileType.DungeonFloor:
          case TileType.Pit:
          case TileType.OpenPit:
          case TileType.TeleportTrap:
          case TileType.HiddenTeleportTrap:
            candidates.Add(new Loc(dungeonID, levelNum, r, c));
            break;
        }
      }
    }

    Loc loc = candidates[rng.Next(candidates.Count)];
    objDb.Add(item);
    objDb.SetToLoc(loc, item);
  }

  static Item GenerateItem(ItemNames name, GameObjectDB objDb, Random rng)
  {
    Item zorkmids;
    switch (name) 
    {
      case ItemNames.ZORKMIDS_PITTANCE:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(1, 11);
        return zorkmids;
      case ItemNames.ZORKMIDS_MEDIOCRE:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(1, 11);
        return zorkmids;
      case ItemNames.ZORKMIDS_GOOD:
        zorkmids = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        zorkmids.Value = rng.Next(15, 31);
        return zorkmids;
      default:
        return ItemFactory.Get(name, objDb);
    } 
  }

  public static void AddTreasureToDungeonLevel(GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    if (levelNum == 0) 
    {
      int numItems = rng.Next(1, 4);
      for (int j = 0; j < numItems; j++) 
      {
        var name = CommonItems[rng.Next(CommonItems.Count)];
        var item = GenerateItem(name, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 1)
    {
      int numItems = rng.Next(1, 5);
      for (int j = 0; j < numItems; j++)
      {
        double roll = rng.NextDouble();
        var name = roll <= 0.9 ? CommonItems[rng.Next(CommonItems.Count)] : UncommonItems[rng.Next(UncommonItems.Count)];
        var item = GenerateItem(name, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 2 || levelNum == 3)
    {
      int numItems = rng.Next(1, 5);
      for (int j = 0; j < numItems; j++)
      {
        ItemNames name;
        double roll = rng.NextDouble();
        if (roll <= 0.4)
          name = CommonItems[rng.Next(CommonItems.Count)];
        else if (roll <= 0.9)
          name = UncommonItems[rng.Next(UncommonItems.Count)];
        else
          name = GoodItems[rng.Next(GoodItems.Count)];
        var item = GenerateItem(name, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 5)
    {
      int numItems = rng.Next(1, 5);
      for (int j = 0; j < numItems; j++)
      {
        ItemNames name;
        double roll = rng.NextDouble();
        if (roll <= 0.2)
          name = CommonItems[rng.Next(CommonItems.Count)];
        else if (roll <= 0.7)
          name = UncommonItems[rng.Next(UncommonItems.Count)];
        else
          name = GoodItems[rng.Next(GoodItems.Count)];
        var item = GenerateItem(name, objDb, rng);
        AddObjectToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
  }
}
