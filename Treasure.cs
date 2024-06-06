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
  public static List<Item> PoorTreasure(int numOfItems, Random rng, GameObjectDB objDb)
  {
    List<Item> loot = [];

    for (int j = 0; j < numOfItems; j++)
    {
      double roll = rng.NextDouble();
      if (roll > 0.95)
      {
        loot.Add(ItemFactory.Get("potion of healing", objDb));
      }
      else if (roll > 0.80)
      {
        loot.Add(ItemFactory.Get("torch", objDb));        
      }
      else if (roll > 0.5)
      {
        var cash = ItemFactory.Get("zorkmids", objDb);
        cash.Value = rng.Next(3, 11);        
      }
    }
    
    return loot;
  }
}
