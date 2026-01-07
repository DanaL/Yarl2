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
    if (item.Type == ItemType.Weapon && !item.HasTrait<WeaponBonusTrait>())
      item.Traits.Add(new WeaponBonusTrait() { Bonus = 0, SourceId = item.ID });

    foreach (Trait trait in item.Traits)
    {
      if (trait is WeaponBonusTrait wbt)
      {
        wbt.Bonus += 1;
        return (true, $"{item.FullName.DefArticle().Capitalize()} shines faintly and becomes more effective!");
      }
      else if (trait is ArmourTrait at)
      {
        at.Bonus += 1;
        return (true, $"{item.FullName.DefArticle().Capitalize()} shines faintly and becomes stronger!");
      }
      else if (trait is ACModTrait acMod)
      {
        acMod.ArmourMod += 1;
        return (true, $"{item.FullName.DefArticle().Capitalize()} shines faintly and hums with new magic!");
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