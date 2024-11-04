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

using Yarl2;
class Artifacts 
{
  static Item BaseItem(GameObjectDB objDb)
  {
    return ItemFactory.Get(ItemNames.LONGSWORD, objDb);
  }

  static Item ArtifactSword(Item sword, History history, Random rng)
  {
    // add three nice features
    for (int i = 0; i < 3; i++)
    {
      int lo = 0;
      if (sword.Traits.OfType<MetalTrait>().FirstOrDefault() is MetalTrait mt)
      {
        if (mt.Type == Metals.Silver || mt.Type == Metals.Mithril)
          lo = 2;
      } 
        
      int roll = rng.Next(lo, 5);
      switch (roll)
      {
        case 0:
          sword.Traits = sword.Traits.Where(t => t is not MetalTrait).ToList();
          sword.Traits.Add(new MetalTrait() { Type = Metals.Silver });
          break;
        case 1:
          sword.Traits = sword.Traits.Where(t => t is not MetalTrait).ToList();
          sword.Traits.Add(new MetalTrait() { Type = Metals.Mithril });
          break;
        default:
          if (sword.Traits.OfType<WeaponBonusTrait>().FirstOrDefault() is WeaponBonusTrait wb)
            wb.Bonus += 1;
          else
            sword.Traits.Add(new WeaponBonusTrait() { Bonus = 1 });
          break;
      }
    }

    foreach (HistoricalFigure hf in history.Facts.OfType<HistoricalFigure>())
    {
      Console.WriteLine($"{hf.Name}, {hf.Title}");
    }

    return sword;
  }

  public static Item GenArtifact(GameObjectDB objDb, History history, Random rng)
  {
    Item item = BaseItem(objDb);

    if (item.HasTrait<SwordTrait>())
      item = ArtifactSword(item, history, rng);
      
    return item;
  }
}