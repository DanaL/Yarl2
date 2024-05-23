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

enum DamageType
{
  Slashing,
  Piercing,
  Blunt,
  Fire,
  Cold,
  Poison,
  Acid,
  Necrotic,
  Force
}

record struct Damage(int Die, int NumOfDie, DamageType Type);

class Battle
{
  // We'll average two d20 rolls to make combat rolls a bit less swinging/
  // evenly distributed
  static int AttackRoll(Random rng) => (rng.Next(1, 21) + rng.Next(1, 21)) / 2;

  static (int, DamageType) DamageRoll(Damage dmg, Random rng)
  {
    int total = 0;
    for (int r = 0; r < dmg.NumOfDie; r++)
      total += rng.Next(dmg.Die) + 1;
    return (total, dmg.Type);
  }

  static bool ResolveImpale(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result)
  {
    bool success = false;

    // is there an opponent behind the primary target to impale?
    int diffRow = (attacker.Loc.Row - target.Loc.Row) * 2;
    int diffCol = (attacker.Loc.Col - target.Loc.Col) * 2;
    Loc checkLoc = attacker.Loc with { Row = attacker.Loc.Row - diffRow, Col = attacker.Loc.Col - diffCol };
    Actor? occ = gs.ObjDb.Occupant(checkLoc);
    if (occ is not null && attackRoll >= occ.AC)
    {
      ResolveMeleeHit(attacker, occ, gs, result, Verb.Impale);
      success = true;
    }

    return success;
  }

  static bool ResolveCleave(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result)
  {
    bool success = false;
    // Check for any cleave targets Adj4 to main target and Adj to attacker
    var adjToAtt = new HashSet<(int, int)>(Util.Adj8Sqs(attacker.Loc.Row, attacker.Loc.Col));
    foreach (var sq in Util.Adj4Sqs(target.Loc.Row, target.Loc.Col))
    {
      var loc = target.Loc with { Row = sq.Item1, Col = sq.Item2 };
      var occ = gs.ObjDb.Occupant(loc);
      if (occ is not null && occ.ID != attacker.ID && adjToAtt.Contains((occ.Loc.Row, occ.Loc.Col)))
      {
        if (attackRoll >= occ.AC)
        {
          ResolveMeleeHit(attacker, occ, gs, result, Verb.Cleave);
          success = true;
        }
      }
    }

    return success;
  }

  static void ResolveMissileHit(Actor attacker, Actor target, Item ammo, GameState gs, ActionResult result)
  {
    List<(int, DamageType)> dmg = [];
    foreach (var trait in ammo.Traits)
    {
      if (trait is DamageTrait dt)
      {
        var d = new Damage(dt.DamageDie, dt.NumOfDie, dt.DamageType);
        dmg.Add(DamageRoll(d, gs.Rng));
      }
    }

    int bonusDamage = 0;
    // I don't know if I actually want to add Dex to missile dmg. 5e does 
    // course but archery is fairly OP in 5e. I don't want archery to be
    // blatantly the best play still in my game
    // if (attacker.Stats.TryGetValue(Attribute.Dexterity, out var dex))
    //   bonusDamage += dex.Curr;
    if (attacker.Stats.TryGetValue(Attribute.MissileDmgBonus, out var mdb))
      bonusDamage += mdb.Curr;

    string txt = $"{ammo.FullName.DefArticle().Capitalize()} hits {target.FullName}!";
    result.Messages.Add(new Message(txt, target.Loc));
    var (hpLeft, dmgMsg) = target.ReceiveDmg(dmg, bonusDamage, gs);
    ResolveHit(attacker, target, hpLeft, result, gs);
    if (dmgMsg != "")
      result.Messages.Add(new Message(dmgMsg, target.Loc));
    
    foreach (var trait in ammo.Traits)
    {
      if (trait is PoisonerTrait poison)
      {
        ApplyPoison(poison, target, gs, result);
      }
    }
  }

  static void ApplyPoison(PoisonerTrait source, Actor victim, GameState gs, ActionResult result)
  {
    // We won't apply multiple poison statuses to one victim. Although maybe I
    // should replace the weaker poison with the stronger one?
    if (victim.HasTrait<PoisonedTrait>())
      return;

    bool conCheck = victim.AbilityCheck(Attribute.Constitution, source.DC, gs.Rng);
    if (!conCheck)
    {
      var poisoned = new PoisonedTrait()
      {
        DC = source.DC,
        Strength = source.Strength,
        VictimID = victim.ID
      };
      victim.Traits.Add(poisoned);
      gs.RegisterForEvent(GameEventType.EndOfRound, poisoned);

      var msg = new Message($"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Etre)} poisoned!", victim.Loc);
      result.Messages.Add(msg);
    }
  }

  static void ResolveMeleeHit(Actor attacker, Actor target, GameState gs, ActionResult result, Verb attackVerb)
  {    
    // Need to handle the case where the player isn't currently wielding a weapon...
    List<(int, DamageType)> dmg = [];
    foreach (var d in attacker.MeleeDamage())
    {
      var dr = DamageRoll(d, gs.Rng);
      dmg.Add(dr);
    }

    // If melee attacking an Idle target, deal double damage.
    // Note: I need to make sure immobile monsters like vines 
    // aren't always getting double dmg
    if (target.Status == MobAttitude.Idle)
    {
      string txt = $"{attacker.FullName.Capitalize()} {Grammar.Conjugate(attacker, "strike")} {target.FullName} at unawares.";
      result.Messages.Add(new Message(txt, target.Loc));

      foreach (var d in attacker.MeleeDamage())
      {
        var dr = DamageRoll(d, gs.Rng);
        dmg.Add(dr);
      }
    }

    int bonusDamage = 0; // this is separate from the damage types because, say,
                         // a flaming sword that does 1d8 slashing, 1d6 fire has
                         // two damage types but we only want to add the player's
                         // strength modifier once
    if (attacker.Stats.TryGetValue(Attribute.Strength, out var str))
      bonusDamage += str.Curr;
    if (attacker.Stats.TryGetValue(Attribute.MeleeDmgBonus, out var mdb))
      bonusDamage += mdb.Curr;
    if (attacker.HasActiveTrait<RageTrait>())
      bonusDamage += gs.Rng.Next(1, 7) + gs.Rng.Next(1, 7);

    Message msg = MsgFactory.Phrase(attacker.ID, attackVerb, target.ID, 0, true, target.Loc, gs);
    result.Messages.Add(msg);
    var (hpLeft, dmgMsg) = target.ReceiveDmg(dmg, bonusDamage, gs);
    ResolveHit(attacker, target, hpLeft, result, gs);
    if (dmgMsg != "")
      result.Messages.Add(new Message(dmgMsg, target.Loc));

    if (attacker.Traits.Count > 0)
    {
      if (attacker.HasTrait<PoisonerTrait>())
      {
        var poison = attacker.Traits.OfType<PoisonerTrait>().First();
        ApplyPoison(poison, target, gs, result);
      }

      if (attacker.Traits.OfType<WeakenTrait>().FirstOrDefault() is WeakenTrait weaken)
      {       
        var debuff = new StatBuffTrait()
        {
          DC = weaken.DC,
          VictimID = target.ID,
          Attr = Attribute.Strength,
          Amt = -weaken.Amt,
          ExpiresOn = gs.Turn + 100
        };

        if (debuff.IsAffected(target, gs))
        {
          string txt = debuff.Apply(target, gs);
          result.Messages.Add(new Message(txt, target.Loc));
        }
      }
    }
  }

  static void ResolveHit(Actor attacker, Actor target, int hpLeft, ActionResult result, GameState gs)
  {
    static void HitAnim(Actor target, GameState gs)
    {
      var hitAnim = new HitAnimation(target.ID, gs, Colours.FX_RED);
      gs.UIRef().RegisterAnimation(hitAnim);
    }

    if (hpLeft < 1)
      gs.ActorKilled(target, attacker.Name.IndefArticle(), result);
    
    HitAnim(target, gs);

    if (target.HasTrait<AcidSplashTrait>())
    {
      foreach (var adj in Util.Adj8Locs(target.Loc))
      {
        if (gs.ObjDb.Occupant(adj) is Actor victim)
        {
          string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} splashed by acid!";
          result.Messages.Add(new Message(txt, victim.Loc));
          int roll = gs.Rng.Next(4) + 1;
          var (hpLeftAfterAcid, acidMsg) = victim.ReceiveDmg([(roll, DamageType.Acid)], 0, gs);   
          
          HitAnim(victim, gs);
          
          if (hpLeftAfterAcid < 1)
            gs.ActorKilled(victim, "acid", result);
          if (acidMsg != "")
            result.Messages.Add(new Message(acidMsg, victim.Loc));
        }
      }      
    }

    // Paralyzing gaze only happens in melee range
    if (Util.Distance(attacker.Loc, target.Loc) < 2)
    {
      if (target.Traits.OfType<ParalyzingGazeTrait>().FirstOrDefault() is ParalyzingGazeTrait gaze)
      {
        var paralyzed = new ParalyzedTrait()
        {
          VictimID = attacker.ID,
          DC = gaze.DC
        };

        if (paralyzed.IsAffected(attacker, gs))
        {
          string txt = paralyzed.Apply(attacker, gs);          
          result.Messages.Add(new Message(txt, attacker.Loc));
        }       
      }
    }
  }

  static Message ResolveKnockBack(Actor attacker, Actor target, GameState gs)
  {
    static bool CanPass(Loc loc, GameState gs)
    {
      var t = gs.TileAt(loc);
      return t.Type != TileType.Unknown && !gs.ObjDb.Occupied(loc) && t.PassableByFlight();
    }

    int deltaRow = attacker.Loc.Row - target.Loc.Row;
    int deltaCol = attacker.Loc.Col - target.Loc.Col;

    Loc first = target.Loc with { Row = target.Loc.Row - deltaRow, Col = target.Loc.Col - deltaCol };
    Loc second = target.Loc with { Row = target.Loc.Row - 2 * deltaRow, Col = target.Loc.Col - 2 * deltaCol };

    if (CanPass(first, gs) && CanPass(second, gs))
    {
      gs.ResolveActorMove(target, target.Loc, second);
      target.Loc = second;
      var txt = $"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Etre)} knocked backward!";
      return new Message(txt, second);
    }
    else if (CanPass(first, gs))
    {
      gs.ResolveActorMove(target, target.Loc, first);
      target.Loc = first;
      var txt = $"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Stumble)} backward!";
      return new Message(txt, first);
    }

    return new Message("", Loc.Nowhere);
  }

  static Message ResolveGrapple(Actor actor, Actor target, GameState gs)
  {
    // You can only be grappled by one thing at a time
    if (target.HasTrait<GrappledTrait>())
      return new Message("", Loc.Nowhere);

    var grapple = actor.Traits
                       .OfType<GrapplerTrait>()
                       .First();
    if (target.AbilityCheck(Attribute.Strength, grapple.DC, gs.Rng))
      return new Message("", Loc.Nowhere);

    var grappled = new GrappledTrait()
    {
      VictimID = target.ID,
      GrapplerID = actor.ID,
      DC = grapple.DC
    };
    gs.RegisterForEvent(GameEventType.Death, grappled, actor.ID);
    
    target.Traits.Add(grappled);
    var msg = $"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Etre)} grappled by "; 
    msg += actor.FullName + "!";
    return new Message(msg, target.Loc);
  }

  public static ActionResult MeleeAttack(Actor attacker, Actor target, GameState gs)
  {
    static bool CanCleave(Actor attacker)
    {
      if (!attacker.HasTrait<CleaveTrait>())
        return false;

      if (attacker.Inventory.ReadiedWeapon() is Item weapon)
      {
        if (!(weapon.HasTrait<SwordTrait>() || weapon.HasTrait<AxeTrait>()))
          return false;

        return true;
      }

      return false;
    }

    static bool CanImpale(Actor attacker, Actor target)
    {
      if (!attacker.HasTrait<ImpaleTrait>())
        return false;

      if (attacker.Inventory.ReadiedWeapon() is Item weapon)
      {
        if (weapon.HasTrait<PolearmTrait>() || weapon.HasTrait<StabbyTrait>())
        {
          // We check the distance because if the player has a weapon with Reach,
          // we don't want them to attack monsters 3 squares away. Ie.,
          //      ...@.gh...
          // When attacking g with reach, the player shouldn't be able to hit
          // the h via Reach
          if (Util.Distance(attacker.Loc, target.Loc) == 1)
            return true;
        }
      }

      return false;
    }

    var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };

    int roll = AttackRoll(gs.Rng) + attacker.TotalMeleeAttackModifier();
    if (roll >= target.AC)
    {
      if (target.HasTrait<DodgeTrait>() && target.AbleToMove())
      {
        int dodgeChance = target.Traits.OfType<DodgeTrait>().First().Rate;
        int dodgeRoll = gs.Rng.Next(100);
        if (dodgeRoll < dodgeChance && HandleDodge(attacker, target, gs))
        {
          string txt = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "dodge")} out of the way!";
          var msg = new Message(txt, target.Loc);
          result.Messages.Add(msg);
          return result;
        }        
      }

      ResolveMeleeHit(attacker, target, gs, result, Verb.Hit);

      if (CanCleave(attacker))
        ResolveCleave(attacker, target, roll, gs, result);
     
      if (CanImpale(attacker, target))
        ResolveImpale(attacker, target, roll, gs, result);
      
      if (attacker.HasActiveTrait<KnockBackTrait>())
      {
        var msg = ResolveKnockBack(attacker, target, gs);
        if (msg.Loc != Loc.Nowhere)
          result.Messages.Add(msg);
      }
      
      if (attacker.HasActiveTrait<GrapplerTrait>())
      {
        var msg = ResolveGrapple(attacker, target, gs);
        if (msg.Loc != Loc.Nowhere)
          result.Messages.Add(msg);
      }
    }
    else
    {
      Message msg = MsgFactory.Phrase(attacker.ID, Verb.Miss, target.ID, 0, true, target.Loc, gs);
      result.Messages.Add(msg);
    }

    if (attacker is Player player && player.Inventory.ReadiedWeapon() is Item weapon)
    {
      foreach (Trait t in  weapon.Traits) 
      {
        switch (t)
        {
          case PolearmTrait:
            player.ExerciseStat(Attribute.PolearmsUse);
            break;
          case SwordTrait:
            player.ExerciseStat(Attribute.SwordUse);
            break;
          case AxeTrait:
            player.ExerciseStat(Attribute.AxeUse); 
            break;
          case FinesseTrait:
            player.ExerciseStat(Attribute.FinesseUse);
            break;
        }
      }
    }

    return result;
  }

  public static bool HandleDodge(Actor attacker, Actor target, GameState gs)
  {
    // Find square to dodge to
    HashSet<Loc> options = [];

    foreach (var adj in Util.Adj8Locs(attacker.Loc).Intersect(Util.Adj8Locs(target.Loc)))
    {
      var tile = gs.TileAt(adj);
      if (!gs.ObjDb.Occupied(adj) && tile.Passable())
        options.Add(adj);
    }

    if (options.Count > 0)
    {
      var sq = options.ToList()[gs.Rng.Next(options.Count)];
      gs.ResolveActorMove(target, target.Loc, sq);
      target.Loc = sq;

      return true;
    }

    return false;
  }

  // attackBonus is because at this point I don't know what weapon shot the ammunition so pass
  // bonsuses related to that here
  public static ActionResult MissileAttack(Actor attacker, Actor target, GameState gs, Item ammo, int attackBonus, Animation? anim)
  {
    var result = new ActionResult() { Complete = false, EnergyCost = 1.0 };

    int roll = AttackRoll(gs.Rng) + attacker.TotalMissileAttackModifier(ammo) + attackBonus;
    if (roll >= target.AC)
    {      
      if (anim is not null)
        gs.UIRef().PlayAnimation(anim, gs);
      ResolveMissileHit(attacker, target, ammo, gs, result);

      result.Complete = true;
    }
    else
    {
      Message msg = MsgFactory.Phrase(ammo.ID, Verb.Miss, target.ID, 0, true, target.Loc, gs);
      result.Messages.Add(msg);
    }

    // Firebolts, ice, should apply their effects to the square they hit
    foreach (var dmg in ammo.Traits.OfType<DamageTrait>())
    {
      gs.ApplyDamageEffectToLoc(target.Loc, dmg.DamageType);
    }

    return result;
  }

  // This is identical to MissileAttack, save for which ability is used for the attack roll. Not
  // going to merge them just yet in case they diverge as I develop more spells
  public static ActionResult MagicAttack(Actor attacker, Actor target, GameState gs, Item spell, int attackBonus, Animation? anim)
  {
    var result = new ActionResult() { Complete = false, EnergyCost = 1.0 };

    int roll = AttackRoll(gs.Rng) + attacker.TotalSpellAttackModifier() + attackBonus;
    if (roll >= target.AC)
    {
      if (anim is not null)
        gs.UIRef().PlayAnimation(anim, gs);
      ResolveMissileHit(attacker, target, spell, gs, result);
      result.Complete = true;
    }
    else
    {
      string txt = $"{spell.FullName.DefArticle().Capitalize()} misses {target.FullName}.";
      result.Messages.Add(new Message(txt, target.Loc));
    }

    // Firebolts, ice, should apply their effects to the square they hit
    foreach (var dmg in spell.Traits.OfType<DamageTrait>())
    {
      gs.ApplyDamageEffectToLoc(target.Loc, dmg.DamageType);
    }

    return result;
  }
}
