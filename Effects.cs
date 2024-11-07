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

using System.Text;

namespace Yarl2;

// I suspect I might eventually drop this?
[Flags]
enum EffectFlag
{
  None = 0,
  Wet = 1,
  Rust = 2
}

// I sort of feel like it's maybe 'better' design to have each Item/Trait host
// the code that's specific to an effect inside the class? But for my dumb brain
// I think it makes sense to have the effects code in one place.
class EffectApplier
{
  static string ApplyWet(GameState gs, GameObj receiver, Actor? owner)
  {
    var sb = new StringBuilder();

    // Work on a copy of the traits because apply wet to one trait may
    // cause another to be removed, altering the list collection we are
    // iterating over. (Ie., extinguishing a lit torch will remove the fire
    // damage trait)
    var traits = receiver.Traits.Select(t => t).ToList();
    foreach (var trait in traits) 
    { 
      if (trait is TorchTrait torch && torch.Lit)
      {
        string s = torch.Extinguish(gs, (Item) receiver, receiver.Loc);
        sb.Append(s);
      }
    }

    // Let's say 1 in 3 chance that an item becomes Wet that it might Rust
    if (receiver is Item item && item.CanCorrode() && gs.Rng.Next(3) != 0)
    {
      string s = ApplyRust(gs, item, owner);
      sb.Append(s);
    }

    return sb.ToString();
  }

  static string ApplyRust(GameState gs, GameObj receiver, Actor? owner)
  {
    // At the moment in game, only items can be rusted. Maybe 
    // eventually iron golems or such will exist??
    if (receiver is not Item item)
    {
      return "";
    }

    if (!item.CanCorrode())
      return "";
    Metals metal = item.MetalType();

    RustedTrait? rusted = item.Traits.OfType<RustedTrait>().FirstOrDefault();

    if (rusted == null)
    {
      item.Traits.Add(new AdjectiveTrait("Rusted"));
      item.Traits.Add(new RustedTrait() { Amount = Rust.Rusted });
    }
    else if (rusted.Amount == Rust.Rusted)
    {
      // An already rusted item becomes corroded
      item.Traits = item.Traits.Where(t => !(t is AdjectiveTrait adj && adj.Adj == "Rusted")).ToList();
      item.Traits.Add(new AdjectiveTrait("Corroded"));
      rusted.Amount = Rust.Corroded;
    }
    else
    {
      // Right now we have only two degrees of rust: Rusted and Corroded and hence
      // a max penalty of -2 to item bonuses
      return "";
    }

    // Some items have their bonuses lowered by being rusted/corroded
    var armourTrait = item.Traits.OfType<ArmourTrait>().FirstOrDefault();
    if (armourTrait is not null)
    {
      armourTrait.Bonus -= 1;
    }

    if (item.Type == ItemType.Weapon)
    {
      var wb = item.Traits.OfType<WeaponBonusTrait>().FirstOrDefault();
      if (wb is null)
        item.Traits.Add(new WeaponBonusTrait() { Bonus = -1 });
      else
        wb.Bonus -= 1;
    }

    string s = owner is null ? item.Name.DefArticle().Capitalize() : item.Name.Possessive(owner).Capitalize();
    s += " corrodes!";
    
    return s;
  }

  public static void RemoveRust(GameObj thing)
  {
    if (thing is null)
      return;

    Rust rust;
    if (thing.Traits.OfType<RustedTrait>().FirstOrDefault() is RustedTrait rt)
      rust = rt.Amount;
    else
      rust = Rust.Rusted;

    List<Trait> keepers = [];
    foreach (Trait trait in thing.Traits)
    {
      if (trait is RustedTrait)
        continue;
      if (trait is AdjectiveTrait adj && (adj.Adj == "Rusted" || adj.Adj == "Corroded"))
        continue;
      if (trait is WeaponBonusTrait wbt)
        wbt.Bonus += rust == Rust.Rusted ? 1 : 2;
      if (trait is ArmourTrait at)
        at.Bonus += rust == Rust.Rusted ? 1 : 2;

      keepers.Add(trait);
    }

    thing.Traits = keepers;
  }

  public static string Apply(EffectFlag flag, GameState gs, GameObj receiver, Actor? owner)
  {    
    switch (flag)
    {
      case EffectFlag.Wet:
        return ApplyWet(gs, receiver, owner);
      case EffectFlag.Rust:
        return ApplyRust(gs, receiver, owner);
      default:
        return "";
    }    
  }
}
