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

// I sort of feel like it's maybe 'better' design to have each Item/Trait host
// the code that's specific to an effect inside the class? But for my dumb brain
// I think it makes sense to have the effects code in one place.
class EffectApplier
{
  public static string ExtinguishTorch(GameState gs, GameObj receiver)
  {
    StringBuilder sb = new();
    // Work on a copy of the traits because apply wet to one trait may
    // cause another to be removed, altering the list collection we are
    // iterating over. (Ie., extinguishing a lit torch will remove the fire
    // damage trait)
    List<Trait> traits = [..receiver.Traits.Select(t => t)];
    foreach (Trait trait in traits) 
    { 
      if (trait is TorchTrait torch && torch.Lit)
      {
        string s = torch.Extinguish(gs, (Item) receiver, receiver.Loc);
        sb.Append(s);
      }
    }

    return sb.ToString();
  }

  static string ApplyWet(GameState gs, GameObj receiver, Actor? owner)
  {
    StringBuilder sb = new();

    string s = ExtinguishTorch(gs, receiver);
    if (s != "")
      sb.Append(s);

    // Let's say 1 in 3 chance that an item becomes Wet that it might Rust
    if (receiver is Item item && item.CanCorrode() && gs.Rng.Next(3) != 0)
    {
      s = ApplyRust(gs, item, owner);
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
      item.Traits = [.. item.Traits.Where(t => !(t is AdjectiveTrait adj && adj.Adj == "Rusted"))];
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

    if (item.Type == ItemType.Weapon || (item.Type == ItemType.Tool && item.Name == "pickaxe"))
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

  static (string, bool) ApplyFire(GameState gs, GameObj receiver, Actor? owner)
  {
    int chance = 50;
    if (owner is not null)
    {
      foreach (Trait t in owner.Traits)
      {
        if (t is ImmunityTrait it && it.Type == DamageType.Fire)
          return ("", false);
        if (t is ResistanceTrait rt && rt.Type == DamageType.Fire) 
        {
          chance = 10;
          break;
        }
      }
    }

    if (receiver.HasTrait<FlammableTrait>() && gs.Rng.Next(100) < chance)
    {
      string s = $"{receiver.FullName.IndefArticle().Capitalize()} burns up!";
      return (s, true);
    }

    return ("", false);
  }

  public static (string, bool) Apply(DamageType damageType, GameState gs, GameObj receiver, Actor? owner)
  {
    return damageType switch
    {
      DamageType.Wet => (ApplyWet(gs, receiver, owner), false),
      DamageType.Rust => (ApplyRust(gs, receiver, owner), false),
      DamageType.Fire => ApplyFire(gs, receiver, owner),
      _ => ("", false),
    };
  }

  static void CleansePlayer(GameState gs)
  {
    gs.UIRef().AlertPlayer("You douse yourself in holy water and feel slightly cleaner.");
  }

  static void CleanseUndead(GameState gs, Actor victim, Item? source)
  {
    int dmg = gs.Rng.Next(8) + gs.Rng.Next(8) + 1;
    List<(int, DamageType)> holy = [(dmg, DamageType.Holy)];

    string s = $"{MsgFactory.CalcName(victim, gs.Player).Capitalize()} is burned";
    s += source is not null ? $" by {source.FullName.DefArticle()}!" : "!";

    gs.UIRef().AlertPlayer(s, gs, victim.Loc);

    var (hpLeft, _, _) = victim.ReceiveDmg(holy, 0, gs, null, 1.0);
    if (hpLeft < 1)
    {
      gs.ActorKilled(victim, "cleansing", null);
    }
  }

  public static void CleanseDesecratedAltar(GameState gs, Item target, Loc loc)
  {
    foreach (Trait t in target.Traits)
    {
      if (t is AdjectiveTrait adj && adj.Adj == "desecrated")
      {
        adj.Adj = "holy";
        break;
      }
    }

    target.Traits = [.. target.Traits.Where(t => t is not DesecratedTrait && t is not DescriptionTrait)];
    target.Traits.Add(new DescriptionTrait("An altar consecrated to Hunktokar."));
    target.Traits.Add(new LightSourceTrait() { Radius = 1, FgColour = Colours.YELLOW, BgColour = Colours.HOLY_AURA, ExpiresOn = ulong.MaxValue, OwnerID = target.ID });
    target.Traits.Add(new HolyTrait());
    target.Glyph = target.Glyph with { Lit = Colours.WHITE, Unlit = Colours.DARK_GREY };

    gs.Player.Stats[Attribute.Piety].ChangeMax(1);

    gs.UIRef().AlertPlayer("The altar glows with a holy light and is cleansed!", gs, loc);
  }

  public static void CleanseLoc(GameState gs, Loc loc, Item? source)
  {
    if (gs.ObjDb.Occupant(loc) is Actor actor)
    {
      if (actor is Player player)
      {
        CleansePlayer(gs);
        return;
      }
      else if (actor.HasTrait<UndeadTrait>())
      {
        CleanseUndead(gs, actor, source);
        return;
      }
    }

    foreach (Item item in gs.ObjDb.ItemsAt(loc))
    {
      if (item.Type == ItemType.Altar && item.HasTrait<DesecratedTrait>())
      {
        CleanseDesecratedAltar(gs, item, loc);
        return;
      }
    }
  }
}
