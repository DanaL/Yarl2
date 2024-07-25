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

record TreasureOdds(double Lo, double Hi, ItemNames Name, int Min, int Max);

class Treasure
{
  static Dictionary<(int, int), List<TreasureOdds>> TreasureTable = [];

  public static List<Item> PoorTreasure(int numOfItems, Random rng, GameObjectDB objDb)
  {
    List<Item> loot = [];

    for (int j = 0; j < numOfItems; j++)
    {
      double roll = rng.NextDouble();
      if (roll > 0.95)
      {
        loot.Add(ItemFactory.Get(ItemNames.POTION_HEALING, objDb));
      }
      else if (roll > 0.80)
      {
        loot.Add(ItemFactory.Get(ItemNames.TORCH, objDb));        
      }
      else if (roll > 0.5)
      {
        var cash = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
        cash.Value = rng.Next(3, 11);        
      }
    }
    
    return loot;
  }

  // Eventually I need to add file format checking...
  static void LoadTreasureTables()
  {
    var lines = File.ReadAllLines("data/treasure_tables.txt");

    int dungeonID = -1, level = -1;

    foreach (string line in lines)
    {
      if (line.StartsWith("Dungeon"))
      {
        var parts = line.Split(' ');
        dungeonID = int.Parse(parts[1]);
        level = int.Parse(parts[3]);
      }
      else
      {
        // 0.0,0.33,TORCH,1,1
        var parts = line.Split(',');
        double lo = double.Parse(parts[0]);
        double hi = double.Parse(parts[1]);
        Enum.TryParse(parts[2], out ItemNames name);
        int min = int.Parse(parts[3]);
        int max = int.Parse(parts[4]);

        var key = (dungeonID, level);
        if (!TreasureTable.ContainsKey(key))
        {
          TreasureTable.Add(key, []);
        }

        TreasureTable[key].Add(new TreasureOdds(lo, hi, name, min, max));
      }
    }
  }

  static void AddToLevel(Item item, GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    var sq = level.RandomTile(TileType.DungeonFloor, rng);
    var loc = new Loc(dungeonID, levelNum, sq.Item1, sq.Item2);
    objDb.Add(item);
    objDb.SetToLoc(loc, item);
  }

  public static void AddTreasureToDungeonLevel(GameObjectDB objDb, Map level, int dungeonID, int levelNum, Random rng)
  {
    if (TreasureTable.Count == 0)
    {
      LoadTreasureTables();
    }

    var foo = rng.NextDouble();
    foo = rng.NextDouble();
    foo = rng.NextDouble();

    var key = (dungeonID, levelNum);
    if (TreasureTable.TryGetValue(key, out List<TreasureOdds>? value))
    {
      var treasureTable = value;

      int numItems = rng.Next(1, 5);
      for (int j = 0; j < numItems; j++)
      {
        double roll = rng.NextDouble();
        foreach (var ti in treasureTable)
        {
          if (roll >= ti.Lo && roll < ti.Hi)
          {
            int amt = ti.Max > 1 ? rng.Next(ti.Min, ti.Max + 1) : 1;

            // Zorkminds are odd ducks...
            if (ti.Name == ItemNames.ZORKMIDS)
            {
              var zorkmids = ItemFactory.Get(ti.Name, objDb);
              zorkmids.Value = amt;
              AddToLevel(zorkmids, objDb, level, dungeonID, levelNum, rng);
            }
            else
            {
              for (int k = 0; k < amt; k++)
              {
                var item = ItemFactory.Get(ti.Name, objDb);
                AddToLevel(item, objDb, level, dungeonID, levelNum, rng);
              }
            }
            break;
          }
        }
      }
    }       
  }
}
