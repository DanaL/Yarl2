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

class Alchemy
{
  public static bool Compatible(Item item, Item reagent)
  {
    if (reagent.Name == "beetle carapace" || reagent.Name == "mithril ore")
    {
      if (item.HasTrait<ArmourTrait>())
        return true;
    }

    if (reagent.Name == "ogre liver" || reagent.Name == "mithril ore")
    {
      bool upgradableWeapon = false;
      foreach (Trait t in item.Traits)
      {
        if (t is SwordTrait)
          upgradableWeapon = true;
        else if (t is AxeTrait)
          upgradableWeapon = true;
        else if (t is PolearmTrait)
          upgradableWeapon = true;
        else if (t is CudgelTrait)
          upgradableWeapon = true;
        else if (t is FinesseTrait)
          upgradableWeapon = true;
      }

      if (upgradableWeapon && !item.HasTrait<WeaponBonusTrait>())
      {
        item.Traits.Add(new  WeaponBonusTrait() { Bonus = 0, SourceId = item.ID });
      }

      return upgradableWeapon;
    }

    if (reagent.Name == "rune of lashing" && item.Type == ItemType.Weapon && !item.HasTrait<LashTrait>())
    {
      return true;
    }

    if (reagent.Name == "fearful rune" && item.Type == ItemType.Weapon && !item.HasTrait<FrighteningTrait>())
    {
      return true;
    }

    return false;
  }

  public static (bool, string) UpgradeItem(Item item, Item reagent, Actor actor)
  {
    bool success = false;
    string msg = "";
    
    switch (reagent.Name)
    {
      case "beetle carapace":
        if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait armour)
        {
          armour.Bonus += 1;
          success = true;
          msg = $"Your {item.Name} now {Grammar.Conjugate(item, "offer")} more protection!\n\n{reagent.Name.IndefArticle().Capitalize()} was consumed.";
        }
        break;
      case "ogre liver":
        if (item.Traits.OfType<WeaponBonusTrait>().FirstOrDefault() is WeaponBonusTrait wbt)
        {
          wbt.Bonus += 1;
          success = true;
          msg = $"Yuck!\n\nYour {item.Name} is now stronger!\n\n{reagent.Name.IndefArticle().Capitalize()} was consumed.";
        }
        break;
      case "mithril ore":
        if (item.Traits.OfType<ArmourTrait>().FirstOrDefault() is ArmourTrait mithrilArmour)
        {
          mithrilArmour.Bonus += 1;
          success = true;
          msg = $"Enhanced by mithril, your {item.Name} now offers more protection!\n\n{reagent.Name.IndefArticle().Capitalize()} was consumed.";
        }
        else if (item.Traits.OfType<WeaponBonusTrait>().FirstOrDefault() is WeaponBonusTrait mwbt)
        {
          mwbt.Bonus += 1;
          success = true;
          msg = $"Enhanced by mithril, your {item.Name} is now stronger!\n\n{reagent.Name.IndefArticle().Capitalize()} was consumed.";
        }
        break;
      case "scroll of enchanting":
        return Enchant(item, actor);
      case "rune of lashing":
        msg = $"The smith welds the Rune of Lashing to your {item.Name}. Affixed to your weapon, the rune continues to glow with a faint, malevolent light.";
        item.Traits.Add(new LashTrait());
        return (true, msg);
      case "fearful rune":
        msg = $"The smith welds the Fearful Rune to your {item.Name} with a shudder and quickly returns it to you.";
        item.Traits.Add(new FrighteningTrait() { DC = 13 });
        return (true, msg);
    }
    
    return (success, msg);
  }

  static (bool, string) EnchantGrantsTrait(Item item, GrantsTrait grantsTrait, Actor actor)
  {
    List<string> traitsGranted = [.. grantsTrait.TraitsGranted];
    bool enchanted = false;
    string msg = "";
    for (int j = 0; j < traitsGranted.Count; j++)
    {
      if (traitsGranted[j].StartsWith("ACMod#"))
      {
        ACModTrait acmod = (ACModTrait)TraitFactory.FromText(traitsGranted[j], item);
        acmod.ArmourMod += 1;

        traitsGranted.RemoveAt(j);
        traitsGranted.Insert(j, acmod.AsText());
        enchanted = true;
        msg = $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!";

        foreach (Trait t in actor.Traits)
        {
          if (t is ACModTrait active && active.SourceId == item.ID)
            active.ArmourMod = acmod.ArmourMod;
        }
      }
      else if (traitsGranted[j].StartsWith("StatBuff"))
      {
        StatBuffTrait buff = (StatBuffTrait)TraitFactory.FromText(traitsGranted[j], item);
        int amt = buff.Attr == Attribute.HP ? 5 : 1;
        buff.Amt += amt;

        traitsGranted.RemoveAt(j);
        traitsGranted.Insert(j, buff.AsText());
        enchanted = true;
        msg = $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!";

        foreach (Trait t in actor.Traits)
        {
          if (t is StatBuffTrait active && active.SourceId == item.ID)
          {
            active.Amt = buff.Amt;
            actor.Stats[active.Attr] = new Stat(actor.Stats[active.Attr].Curr + amt);
            actor.CalcHP();
            break;
          }
        }
      }
      else if (traitsGranted[j].StartsWith("Regeneration"))
      {
        RegenerationTrait regen = (RegenerationTrait)TraitFactory.FromText(traitsGranted[j], item);
        regen.SourceId = item.ID;
        if (regen.Rate < 3) 
        {
          regen.Rate += 1;
          traitsGranted.RemoveAt(j);
          traitsGranted.Insert(j, regen.AsText());
          enchanted = true;
          msg = $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!";
        
          foreach (Trait t in actor.Traits)
          {
            if (t is RegenerationTrait active && active.SourceId == item.ID)
            {            
              active.Rate = regen.Rate;
              break;
            }
          }
        }
      }
      else if (traitsGranted[j].StartsWith("Dodge"))
      {
        DodgeTrait dodge = (DodgeTrait)TraitFactory.FromText(traitsGranted[j], item);
        dodge.SourceId = item.ID;
        if (dodge.Rate < 51) 
        {
          dodge.Rate += 3;
          traitsGranted.RemoveAt(j);
          traitsGranted.Insert(j, dodge.AsText());
          enchanted = true;
          msg = $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!";
        
          foreach (Trait t in actor.Traits)
          {
            if (t is DodgeTrait active && active.SourceId == item.ID)
            {            
              active.Rate = dodge.Rate;
              break;
            }
          }
        }
      }
    }
    
    if (enchanted)
    {
      grantsTrait.TraitsGranted = [.. traitsGranted];
      return (true, msg);
    }

    return (false, "The item glows briefly, but otherwise you discern no effect.");
  }

  static (bool, string) Enchant(Item item, Actor actor)
  {    
    if ((item.Type == ItemType.Weapon || item.Type == ItemType.Bow) && !item.HasTrait<WeaponBonusTrait>())
      item.Traits.Add(new WeaponBonusTrait() { Bonus = 0, SourceId = item.ID });

    foreach (Trait trait in item.Traits)
    {
      if (trait is WeaponBonusTrait wbt)
      {
        string s = $"{item.FullName.DefArticle().Capitalize()} shines faintly and becomes more effective!";
        wbt.Bonus += 1;
        return (true, s);
      }
      else if (trait is ArmourTrait at)
      {
        string s = $"{item.FullName.DefArticle().Capitalize()} shines faintly and becomes stronger!";
        at.Bonus += 1;
        return (true, s);
      }
      else if (trait is ACModTrait acMod)
      {
        string s = $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!";
        acMod.ArmourMod += 1;
        return (true, s);
      }
      else if (trait is GrantsTrait grantsTrait)
      {
        return EnchantGrantsTrait(item, grantsTrait, actor);
      }      
    }

    return (false, "The item glows briefly, but otherwise you discern no effect.");
  }

  public static int CountUpgrades(Item item)
  {
    int total = 0;

    foreach (Trait trait in item.Traits)
    {
      if (trait is WeaponBonusTrait wbt && wbt.Bonus > 0)
        total += wbt.Bonus;
      if (trait is ViciousTrait)
        ++total;
      if (trait is ArmourTrait at && at.Bonus > 0)
        total += at.Bonus;
      if (trait is ACModTrait acm && acm.ArmourMod > 0)
        total += acm.ArmourMod;
    }

    return total;
  }
}