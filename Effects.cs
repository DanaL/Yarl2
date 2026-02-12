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

namespace Yarl2;

// I sort of feel like it's maybe 'better' design to have each Item/Trait host
// the code that's specific to an effect inside the class? But for my dumb brain
// I think it makes sense to have the effects code in one place.
class Effects
{
  public static void MoldSpores(GameState gs, Item spores, Loc loc)
  {
    gs.UIRef().AlertPlayer($"{spores.Name.DefArticle().Capitalize()} explodes in a cloud of spores!", gs, loc);
    HashSet<Loc> affected = [.. Util.Adj8Locs(loc).Where(l => gs.TileAt(l).PassableByFlight())];
    affected.Add(loc);
    ExplosionAnimation explosion = new(gs)
    {
      MainColour = Colours.LIME_GREEN,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.DARK_GREEN,
      Centre = loc,
      Sqs = [.. affected.Where(l => gs.LastPlayerFoV.ContainsKey(l))],
      Ch = '*'
    };
    gs.UIRef().PlayAnimation(explosion, gs);

    foreach (Loc affectedLoc in affected)
    {
      if (gs.ObjDb.Occupant(affectedLoc) is Actor actor)
      {                
        DiseasedTrait disease = new() { SourceId = spores.ID };
        var res = disease.Apply(actor, gs);
        if (res.Count > 0) 
        {
          List<string> msgs = [$"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "cough")}!" ];            
          msgs.AddRange(res);
          gs.UIRef().AlertPlayer(string.Join(" ", msgs).Trim(), gs, affectedLoc);
        }
      }
    }

    gs.ObjDb.RemoveItemFromGame(loc, spores);
  }

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

  public static void ApplyLava(Actor actor, GameState gs)
  {
    List<(int, DamageType)> p = [(gs.Rng.Next(50, 76), DamageType.Fire)];
    var (hpLeft, _, _) = actor.ReceiveDmg(p, 0, gs, null, 1.0);
    if (hpLeft < 1)
    {
      string name = MsgFactory.CalcName(actor, gs.Player);
      string msg = $"{name.Capitalize()} {Grammar.Conjugate(actor, "was")} immolated in lava!";
      gs.UIRef().AlertPlayer(msg, gs, actor.Loc);
      gs.ActorKilled(actor, "a lava situation", null);
    } 

    actor.Inventory.ApplyEffectToInv(DamageType.Fire, gs, actor.Loc);
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

    gs.UIRef().AlertPlayer("The altar glows with a holy light and is cleansed! You feel properly pious.", gs, loc);
  }

  public static void CleanseLoc(GameState gs, Loc loc, Item? source)
  {
    // Check for a desecrated altar first, so if a player is standing on one
    // we'll target it before the player. (But likewise if a monster is 
    // standing on items, target the monster before the items)

    foreach (Item item in gs.ObjDb.ItemsAt(loc))
    {
      if (item.Type == ItemType.Altar && item.HasTrait<DesecratedTrait>())
      {
        CleanseDesecratedAltar(gs, item, loc);
        return;
      }
      else if (item.Type == ItemType.Altar && item.HasTrait<MolochAltarTrait>())
      {
        Loc impLoc = Loc.Nowhere;

        if (!gs.ObjDb.Occupied(loc))
        {
          impLoc = loc;
        }
        else
        {
          List<Loc> locs = [.. Util.Adj8Locs(loc).Where(l => gs.TileAt(l).Passable() && !gs.ObjDb.Occupied(l))];
          if (locs.Count > 0)
            impLoc = locs[gs.Rng.Next(locs.Count)];
        }

        if (impLoc == Loc.Nowhere)
        {
          gs.UIRef().AlertPlayer("The holy water sizzles and you hear an angry roar!", gs, loc);
          return;
        }

        Actor imp = MonsterFactory.Get("imp", gs.ObjDb, gs.Rng);
        imp.Stats[Attribute.MobAttitude] = new Stat(Mob.AGGRESSIVE);
        gs.ObjDb.AddNewActor(imp, loc);

        gs.UIRef().SetPopup(new Popup("\n\t\t\t\t[BRIGHTRED HOW DARE YOU!]\n", "An angry voice shouts, the floor shakes", -1, -1));
        gs.UIRef().AlertPlayer("A cloud of sulfurous gas forms over the altar and from it an imp emerges!", gs, loc);

        return;
      }
    }

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
    
    Tile tile = gs.TileAt(loc);
    switch (tile.Type)
    {
      case TileType.Water:
      case TileType.DeepWater:
      case TileType.Underwater:
      case TileType.Pool:
        gs.UIRef().AlertPlayer("The holy water dilutes into the water.", gs, loc);
        break;
      case TileType.StoneFloor:
      case TileType.DungeonFloor:
      case TileType.WoodFloor:
        gs.UIRef().AlertPlayer("You pour the holy water on the floor, to no effect.", gs, loc);
        break;
      default:
        gs.UIRef().AlertPlayer("You pour the holy water on the ground, to no effect.", gs, loc);
        break;
    }  
  }

  public static void ApplyDamageEffectToLoc(Loc loc, DamageType damageType, GameState gs)
  {
    List<Item> items = [];
    items.AddRange(gs.ObjDb.ItemsAt(loc));
    items.AddRange(gs.ObjDb.EnvironmentsAt(loc));
    Tile tile = gs.TileAt(loc);
    bool fireStarted = false;
    Map map = gs.MapForLoc(loc);

    switch (damageType)
    {
      case DamageType.Fire:
        // Wooden bridges always burn for comedy reasons
        if (TileBurns(tile))
          fireStarted = true;

        if (tile.Type == TileType.FrozenWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Water));
          gs.UIRef().AlertPlayer("The ice melts!", gs, loc);
        }
        else if (tile.Type == TileType.FrozenDeepWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
          gs.UIRef().AlertPlayer("The ice melts!", gs, loc);
          gs.BridgeDestroyedOverWater(loc);
        }
        else if (tile.Type == TileType.FrozenPool)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pool));
          gs.UIRef().AlertPlayer("The ice melts!", gs, loc);
        }
        else if (tile.Type == TileType.FrozenLake)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Lake));
          gs.UIRef().AlertPlayer("The ice melts!", gs, loc);
        }

        foreach (var item in items)
        {
          if (item.HasTrait<FlammableTrait>())
          {
            gs.UIRef().AlertPlayer($"{item.FullName.DefArticle().Capitalize()} burns up!", gs, loc);
            gs.ItemDestroyed(item, loc);
            fireStarted = true;
          }

          if (item.Name == "mud")
          {
            gs.UIRef().AlertPlayer("The mud dries up.", gs, loc);
            gs.ItemDestroyed(item, loc);
          }
        }
        break;
      case DamageType.Cold:
        // Perhaps Cold can destroy poitions on the ground and such?

        if (tile.Type == TileType.Water)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenWater));
          gs.UIRef().AlertPlayer("The water freezes!");
        }
        else if (tile.Type == TileType.DeepWater)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenDeepWater));
          gs.UIRef().AlertPlayer("The water freezes!");
        }
        else if (tile.Type == TileType.Pool)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenPool));
          gs.UIRef().AlertPlayer("The pool freezes!");
        }
        else if (tile.Type == TileType.Lake)
        {
          map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.FrozenLake));
          gs.UIRef().AlertPlayer("The water freezes!");
        }
        break;
      default:
        break;
    }

    if (fireStarted)
    {
      var fire = ItemFactory.Fire(gs);
      gs.ObjDb.SetToLoc(loc, fire);

      if (tile.Type == TileType.Grass)
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredGrass));
      }
      else if (tile.IsTree())
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.CharredStump));
      }
      else if (tile.Type == TileType.WoodBridge)
      {
        if (gs.LastPlayerFoV.ContainsKey(gs.Player.Loc))
          gs.UIRef().AlertPlayer("The bridge burns up and collapses!");

        gs.BridgeDestroyed(loc);
      }
      else if (tile is Door)
      {
        if (gs.LastPlayerFoV.ContainsKey(gs.Player.Loc))
          gs.UIRef().AlertPlayer("The door is destroyed!");
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
      }
    }

    bool TileBurns(Tile tile)
    {
      if (!tile.Flammable())
        return false;
      
      if (tile.Type == TileType.WoodBridge || tile is Door)
        return true;

      return gs.Rng.NextDouble() < 0.15;
    }
  }

  public static void HandleTipsy(Actor imbiber, GameState gs)
  {
    TipsyTrait? tipsy = null;
    bool immuneToBooze = false;
    foreach (Trait t in imbiber.Traits)
    {
      if (t is TipsyTrait tt)
        tipsy = tt;
      else if (t is UndeadTrait || t is ConstructTrait || t is PlantTrait || t is BrainlessTrait)
        immuneToBooze = true;
    }

    if (immuneToBooze)
      return;

    // Imbiding always reduces stress, even if you pass your saving throw
    if (imbiber.Stats.TryGetValue(Attribute.Nerve, out var nerve))
    {
      nerve.Change(tipsy == null ? 100 : 25);
    }

    int dc = tipsy is null ? 15 : 12;
    if (imbiber.AbilityCheck(Attribute.Constitution, dc, gs.Rng))
      return;

    if (tipsy is not null)
    {
      tipsy.ExpiresOn += (ulong)gs.Rng.Next(50, 76);
      if (gs!.LastPlayerFoV.ContainsKey(imbiber!.Loc))
        gs.UIRef().AlertPlayer($"{imbiber.FullName.Capitalize()} {Grammar.Conjugate(imbiber, "get")} tipsier.", gs, imbiber.Loc);
    }
    else
    {
      tipsy = new TipsyTrait()
      {
        ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(50, 76),
        OwnerID = imbiber.ID
      };
      imbiber.Traits.Add(tipsy);

      gs.RegisterForEvent(GameEventType.EndOfRound, tipsy, imbiber.ID);
      gs.UIRef().AlertPlayer($"{imbiber.FullName.Capitalize()} {Grammar.Conjugate(imbiber, "become")} tipsy!", gs, imbiber.Loc);
    }

    if (imbiber.Traits.OfType<FrightenedTrait>().FirstOrDefault() is FrightenedTrait frightened)
    {
      frightened.Remove(imbiber, gs);
    }
  }
}
