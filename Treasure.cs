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
      ItemNames.SHORTSHORD, ItemNames.SHIELD, ItemNames.SCROLL_BLINK, 
      ItemNames.SCROLL_MAGIC_MAP, ItemNames.POTION_COLD_RES, ItemNames.POTION_FIRE_RES,
      ItemNames.POTION_MIND_READING, ItemNames.WAND_OF_MAGIC_MISSILES, ItemNames.WAND_HEAL_MONSTER,
      ItemNames.HELMET, ItemNames.SHIELD, ItemNames.ZORKMIDS_MEDIOCRE ]; 

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

  static void AddToLevel(Item item, GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    var sq = level.RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(dungeonID, levelNum, sq.Item1, sq.Item2);
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
        AddToLevel(item, objDb, level, dungeonID, levelNum, rng);
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
        AddToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
    else if (levelNum == 2 || levelNum == 3)
    {
      int numItems = rng.Next(1, 5);
      for (int j = 0; j < numItems; j++)
      {
        double roll = rng.NextDouble();
        var name = roll <= 0.5 ? CommonItems[rng.Next(CommonItems.Count)] : UncommonItems[rng.Next(UncommonItems.Count)];
        var item = GenerateItem(name, objDb, rng);
        AddToLevel(item, objDb, level, dungeonID, levelNum, rng);
      }
    }
  }
}
