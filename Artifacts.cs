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

using System.Text;

using Yarl2;

class Artifacts
{
  static Item BaseItem(GameObjectDB objDb)
  {
    return ItemFactory.Get(ItemNames.LONGSWORD, objDb);
  }

  static string History(FactDb factDb, Rng rng)
  {
    List<string> deities = ["the Moon Daughters", "Huntokar", "the Crimson King"];

    RulerInfo rulerInfo = factDb.Ruler;

    // "Wielded by so-and-so in the battle of such-and-such"
    // "Wielded by so-and-so in the war of such-and-such"
    // "Forged for so-and-so and sacred to Huntokar/the Moon Daughter"
    // "Forged to slay the [monster type] [monster name]"
    // "Gifted to so-and-so on the occasiona of their [life event]"

    StringBuilder sb = new();
    sb.Append("Forged for ");
    sb.Append(rulerInfo.Title.CapitalizeWords());
    sb.Append(' ');
    sb.Append(rulerInfo.Name);
    sb.Append(" and sacred to ");
    sb.Append(deities[rng.Next(deities.Count)]);
    sb.Append('.');

    return sb.ToString();
  }

  static Item ArtifactSword(Item sword, FactDb factDb, Rng rng)
  {
    // add three nice features
    for (int i = 0; i < 3; i++)
    {
      List<int> opts = [3, 3, 3];
      if (sword.Traits.OfType<MetalTrait>().FirstOrDefault() is MetalTrait mt)
      {
        if (!(mt.Type == Metals.Silver || mt.Type == Metals.Mithril))
        {
          opts.Add(0);
          opts.Add(1);
        }
      }
      if (!sword.HasTrait<ViciousTrait>())
        opts.Add(3);

      int roll = opts[rng.Next(opts.Count)];
      switch (roll)
      {
        case 0:
          sword.Traits = [.. sword.Traits.Where(t => t is not MetalTrait)];
          sword.Traits.Add(new MetalTrait() { Type = Metals.Silver });
          break;
        case 1:
          sword.Traits = [.. sword.Traits.Where(t => t is not MetalTrait)];
          sword.Traits.Add(new MetalTrait() { Type = Metals.Mithril });
          break;
        case 2:
          sword.Traits.Add(new ViciousTrait() { Scale = 1.333 });
          Console.WriteLine("Vicious");
          break;
        default:
          if (sword.Traits.OfType<WeaponBonusTrait>().FirstOrDefault() is WeaponBonusTrait wb)
            wb.Bonus += 1;
          else
            sword.Traits.Add(new WeaponBonusTrait() { Bonus = 1 });
          break;
      }
    }

    sword.Traits.Add(new DescriptionTrait(History(factDb, rng)));

    return sword;
  }

  public static Item GenArtifact(GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    Item item = BaseItem(objDb);

    if (item.HasTrait<SwordTrait>())
      item = ArtifactSword(item, factDb, rng);

    return item;
  }

  public static Item GenAncientWeapon(GameObjectDB objDb, FactDb factDb, Rng rng)
  {
    ItemNames type = rng.Next(3) switch
    {
      0 => ItemNames.SPEAR,
      1 => ItemNames.BATTLE_AXE,
      _ => ItemNames.LONGSWORD
    };

    Item artifact = ItemFactory.Get(type, objDb);
    artifact.Traits.Add(new  WeaponBonusTrait() { Bonus = 2, SourceId = artifact.ID });

    DamageType dmgType = factDb.Villain == VillainType.FieryDemon ? DamageType.Holy : DamageType.Fire;
    artifact.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = dmgType });

    bool beloved = factDb.Ruler.Beloved;
    bool successful = false;
    string invader = "";
    
    foreach (Fact fact in factDb.HistoricalEvents)
    {
      if (fact is Invasion invasion)
      {
        invader = invasion.Invader;
        break;
      }
    }

    List<string> names;
    if (beloved)
      names = ["Defender", "Dragontooth", "Lightbringer", "Foehammer"];
    else
      names = ["Reaver", "Dark Tidings", "Reaper", "Tempest"];

    artifact.Name = names[rng.Next(names.Count)];
    artifact.Traits.Add(new NamedTrait());

    string weaponType = type switch
    {
      ItemNames.SPEAR => "spear",
      ItemNames.BATTLE_AXE => "battle axe",
      _ => "longsword"
    };

    StringBuilder sb = new();
    sb.Append(artifact.Name.Capitalize());
    sb.Append($": {weaponType.IndefArticle()} forged long ago to battle ");
    sb.Append(invader);
    sb.Append(" when they assailed these lands. Personally wielded in battle by ");
    sb.Append(factDb.Ruler.Name);
    sb.Append(' ');
    sb.Append(factDb.Ruler.Epithet.CapitalizeWords());
    sb.Append(".\n\n");
    if (successful && beloved)
      sb.Append("Ever after, this weapon was said to be a beacon of hope for the people.");
    else if (successful && !beloved)
      sb.Append("Later, the people would learn to fear the site of this wepaon.");
    else if (!successful && beloved)
    {
      sb.Append("This weapon was burried after ");
      sb.Append(invader);
      sb.Append(" overran these lands.");
    }
    else
    {
      sb.Append("There are many who say this weapon is of ill omen and brings with it a curse.");
    }
    artifact.Traits.Add(new DescriptionTrait(sb.ToString()));

    return artifact;
  }
}