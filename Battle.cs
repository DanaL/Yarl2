﻿// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Security.Cryptography;

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
  Force,
  Confusion,
  Fear,
  Wet,
  Rust
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

  static bool ResolveImpale(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result, int weaponBonus)
  {
    bool success = false;

    // is there an opponent behind the primary target to impale?
    int diffRow = (attacker.Loc.Row - target.Loc.Row) * 2;
    int diffCol = (attacker.Loc.Col - target.Loc.Col) * 2;
    Loc checkLoc = attacker.Loc with { Row = attacker.Loc.Row - diffRow, Col = attacker.Loc.Col - diffCol };
    Actor? occ = gs.ObjDb.Occupant(checkLoc);
    if (occ is not null && attackRoll >= occ.AC)
    {
      ResolveMeleeHit(attacker, occ, gs, result, Verb.Impale, weaponBonus);
      success = true;
    }

    return success;
  }

  static bool ResolveCleave(Actor attacker, Actor target, int attackRoll, GameState gs, ActionResult result, int weaponBonus)
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
          ResolveMeleeHit(attacker, occ, gs, result, Verb.Cleave, weaponBonus);
          success = true;
        }
      }
    }

    return success;
  }

  public static void ResolveMissileHit(GameObj attacker, Actor target, Item ammo, GameState gs, ActionResult result)
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
    if (attacker is Actor actor && actor.Stats.TryGetValue(Attribute.MissileDmgBonus, out var mdb))
      bonusDamage += mdb.Curr;

    string txt = $"{ammo.FullName.DefArticle().Capitalize()} hits {target.FullName}!";
    result.Messages.Add(txt);
    var (hpLeft, dmgMsg, _) = target.ReceiveDmg(dmg, bonusDamage, gs, ammo, 1.0);
    if (dmgMsg != "")
      result.Messages.Add(dmgMsg);
    ResolveHit(attacker, target, hpLeft, result, gs);
    
    bool poisoner = false;
    foreach (var trait in ammo.Traits)
    {
      if (trait is PoisonerTrait poison)
      {
        ApplyPoison(poison, target, gs, result);
        poisoner = true;
      }
    }

    if (poisoner)
      CheckCoatedPoison(ammo, gs.Rng);
  }

  static void ApplyPoison(PoisonerTrait source, Actor victim, GameState gs, ActionResult result)
  {
    int duration = source.Duration + gs.Rng.Next(-5, 6);
    if (duration < 0)
      duration = 1;
    var poison = new PoisonedTrait()
    {
      DC = source.DC,
      Strength = source.Strength,
      Duration = duration
    };
    result.Messages.AddRange(poison.Apply(victim, gs));
  }

  static void CheckAttackTraits(Actor target, GameState gs, ActionResult result, GameObj obj, int dmgDone)
  {
    bool poisoner = false;
    foreach (Trait trait in obj.Traits)
    {
      if (trait is PoisonerTrait poison)
      {
        ApplyPoison(poison, target, gs, result);
        poisoner = true;
      }

      if (trait is WeakenTrait weaken)
      {
        var debuff = new StatDebuffTrait()
        {
          DC = weaken.DC,
          OwnerID = target.ID,
          Attr = Attribute.Strength,
          Amt = -weaken.Amt,
          ExpiresOn = gs.Turn + 100
        };

        result.Messages.AddRange(debuff.Apply(target, gs));
      }

      if (dmgDone > 0 && obj is Actor actor && trait is MosquitoTrait && gs.Rng.NextDouble() < 0.6)
      {
        Spawn(actor, gs, result);
      }

      if (trait is CorrosiveTrait)
      {
        
        List<Item> metalItems = target.Inventory
                                      .Items()
                                      .Where(i => i.CanCorrode() && i.Equipped).ToList();

        if (metalItems.Count > 0)
        {
          var damagedItem = metalItems[gs.Rng.Next(metalItems.Count)];
          var (s, _) = EffectApplier.Apply(DamageType.Rust, gs, damagedItem, target);
          if (s != "")
          {
            result.Messages.Add(s);
          }
        }
      }
    }

    if (poisoner)
      CheckCoatedPoison(obj, gs.Rng);
  }

  static void Spawn(Actor actor, GameState gs, ActionResult result)
  {
    List<Loc> options = Util.Adj8Locs(actor.Loc)
                            .Where(loc => gs.TileAt(loc).Passable() && !gs.ObjDb.Occupied(loc))
                            .ToList();
    if (options.Count > 0)
    {
      Loc loc = options[gs.Rng.Next(options.Count)];
      Actor spawnling = MonsterFactory.Get(actor.Name, gs.ObjDb, gs.Rng);
      spawnling.Stats[Attribute.HP].SetCurr(actor.Stats[Attribute.HP].Curr);
      gs.ObjDb.AddNewActor(spawnling, loc);
      gs.AddPerformer(spawnling);
      if (gs.LastPlayerFoV.Contains(loc))
        result.Messages.Add($"{actor.FullName.Capitalize()} spawns!");
    }
  }

  static void ResolveMeleeHit(Actor attacker, Actor target, GameState gs, ActionResult result, Verb attackVerb, int weaponBonus)
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
    if (target.HasTrait<SleepingTrait>())
    {
      string txt = $"{attacker.FullName.Capitalize()} {Grammar.Conjugate(attacker, "strike")} {target.FullName} at unawares.";
      result.Messages.Add(txt);

      foreach (var d in attacker.MeleeDamage())
      {
        var dr = DamageRoll(d, gs.Rng);
        dmg.Add(dr);
      }
    }

    int bonusDamage = weaponBonus; // this is separate from the damage types because, say,
                                   // a flaming sword that does 1d8 slashing, 1d6 fire has
                                   // two damage types but we only want to add the player's
                                   // strength modifier once
    if (attacker.Stats.TryGetValue(Attribute.Strength, out var str))
      bonusDamage += str.Curr;
    if (attacker.Stats.TryGetValue(Attribute.MeleeDmgBonus, out var mdb))
      bonusDamage += mdb.Curr;
    if (attacker.HasActiveTrait<RageTrait>())
      bonusDamage += gs.Rng.Next(1, 7) + gs.Rng.Next(1, 7);

    Item? weapon = attacker.Inventory.ReadiedWeapon();

    string msg = MsgFactory.HitMessage(attacker, target, attackVerb, gs);
    result.Messages.Add(msg);

    double dmgScale = 1.0;
    if (weapon is not null && weapon.Traits.OfType<ViciousTrait>().FirstOrDefault() is ViciousTrait vt)
      dmgScale = vt.Scale;

    var (hpLeft, dmgMsg, dmgDone) = target.ReceiveDmg(dmg, bonusDamage, gs, weapon, dmgScale);    
    if (dmgMsg != "")
      result.Messages.Add(dmgMsg);
    ResolveHit(attacker, target, hpLeft, result, gs);

    CheckAttackTraits(target, gs, result, attacker, dmgDone);

    if (weapon is not null) 
    { 
      CheckAttackTraits(target, gs, result, weapon, dmgDone);
    }      
  }

  static void ResolveHit(GameObj attacker, Actor target, int hpLeft, ActionResult result, GameState gs)
  {
    static void HitAnim(Actor target, GameState gs)
    {
      var hitAnim = new HitAnimation(target.ID, gs, Colours.FX_RED);
      gs.UIRef().RegisterAnimation(hitAnim);
    }

    if (hpLeft < 1)
      gs.ActorKilled(target, attacker.Name.IndefArticle(), result, attacker);
    
    HitAnim(target, gs);

    if (target.HasTrait<AcidSplashTrait>())
    {
      foreach (var adj in Util.Adj8Locs(target.Loc))
      {
        if (gs.ObjDb.Occupant(adj) is Actor victim)
        {
          string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} splashed by acid!";
          result.Messages.Add(txt);
          int roll = gs.Rng.Next(4) + 1;
          var (hpLeftAfterAcid, acidMsg, _) = victim.ReceiveDmg([(roll, DamageType.Acid)], 0, gs, null, 1.0);   
          
          HitAnim(victim, gs);
          
          if (hpLeftAfterAcid < 1)
            gs.ActorKilled(victim, "acid", result, null);
          if (acidMsg != "")
            result.Messages.Add(acidMsg);
        }
      }      
    }

    Actor?  actor = attacker as Actor ?? null;

    if (actor is not null && target.HasTrait<CorrosiveTrait>())
    {
      Item? weapon = actor.Inventory.ReadiedWeapon();
      if (weapon is not null)
      {
        var (s, _) = EffectApplier.Apply(DamageType.Rust, gs, weapon, actor);
        if (s != "" && attacker is Player)
        {
          result.Messages.Add(s);
        }        
      }
    }

    // Paralyzing gaze only happens in melee range
    if (actor is not null && Util.Distance(actor.Loc, target.Loc) < 2)
    {
      if (target.Traits.OfType<ParalyzingGazeTrait>().FirstOrDefault() is ParalyzingGazeTrait gaze)
      {
        if (!actor.HasTrait<BlindTrait>())
        {
          var paralyzed = new ParalyzedTrait() { DC = gaze.DC };
          paralyzed.Apply(actor, gs);
        }        
      }
    }
  }

  static string ResolveKnockBack(Actor attacker, Actor target, GameState gs)
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
      string moveMsg = gs.ResolveActorMove(target, target.Loc, second);
      target.Loc = second;
      var txt = $"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Etre)} knocked backward!";
      if (moveMsg != "")
        txt += " " + moveMsg;

      return txt;
    }
    else if (CanPass(first, gs))
    {
      string moveMsg = gs.ResolveActorMove(target, target.Loc, first);
      target.Loc = first;
      var txt = $"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Stagger)} backward!";
      if (moveMsg != "")
        txt += " " + moveMsg;

      return txt;
    }

    return "";
  }

  static string ResolveGrapple(Actor actor, Actor target, GameState gs)
  {
    // You can only be grappled by one thing at a time
    if (target.HasTrait<GrappledTrait>())
      return "";

    var grapple = actor.Traits
                       .OfType<GrapplerTrait>()
                       .First();
    if (target.AbilityCheck(Attribute.Strength, grapple.DC, gs.Rng))
      return "";

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
    return msg;
  }

  static int CalcAttackMod(Actor attacker, Item? weapon)
  {
    int totalMod = 0;
    if (attacker.Stats.TryGetValue(Attribute.AttackBonus, out Stat? ab))
      totalMod += ab.Curr;

    attacker.Stats.TryGetValue(Attribute.Strength, out Stat? strMod);    
    attacker.Stats.TryGetValue(Attribute.Dexterity, out Stat? dexMod);

    // If the attacker is wielding a weapon, add the weapon category modifier 
    // and appropriate stat modifier. We'll default to strength if no weapon
    // is wielded. 
    if (weapon is not null)
    {
      bool sword = false, axe = false, polearm = false, cudgel = false, bow = false, finesse = false;
      foreach (Trait trait in weapon.Traits)
      {
        if (trait is WeaponBonusTrait wb)
          totalMod += wb.Bonus;
        else if (trait is SwordTrait)
          sword = true;
        else if (trait is AxeTrait)
          axe = true;
        else if (trait is PolearmTrait)
          polearm = true;
        else if (trait is CudgelTrait)
          cudgel = true;
        else if (trait is BowTrait)
          bow = true;
        else if (trait is FinesseTrait)
          finesse = true;
      }

      if (sword && attacker.Stats.TryGetValue(Attribute.SwordUse, out Stat? swordUse))
      {
        totalMod += swordUse.Curr / Constants.PRACTICE_RATIO;
        if (strMod is not null) 
          totalMod += strMod.Curr;
      }
      else if (axe && attacker.Stats.TryGetValue(Attribute.AxeUse, out Stat? axeUse))
      {
        totalMod += axeUse.Curr / Constants.PRACTICE_RATIO;
        if (strMod is not null)
          totalMod += strMod.Curr;
      }
      else if (polearm && attacker.Stats.TryGetValue(Attribute.PolearmsUse, out Stat? polearmUse))
      {
        totalMod += polearmUse.Curr / Constants.PRACTICE_RATIO;
        if (strMod is not null)
          totalMod += strMod.Curr;
      }
      else if (cudgel && attacker.Stats.TryGetValue(Attribute.CudgelUse, out Stat? cudgelUse))
      {
        totalMod += cudgelUse.Curr / Constants.PRACTICE_RATIO;
        if (strMod is not null)
          totalMod += strMod.Curr;
      }
      else if (bow && attacker.Stats.TryGetValue(Attribute.BowUse, out Stat? bowUse))
      {
        totalMod += bowUse.Curr / Constants.PRACTICE_RATIO;
        if (dexMod is not null)
          totalMod += dexMod.Curr;
      }
      else if (finesse && attacker.Stats.TryGetValue(Attribute.FinesseUse, out Stat? finesseUse))
      {
        totalMod += finesseUse.Curr / Constants.PRACTICE_RATIO;
        int dex = dexMod is null ? 0 : dexMod.Curr;
        int str = strMod is null ? 0 : strMod.Curr;
        totalMod += int.Max(dex, str);
      }
    }
    else if (attacker.Stats.TryGetValue(Attribute.Strength, out Stat? str))
    {
      totalMod += str.Curr;
    }

    if (attacker.HasTrait<NauseaTrait>())
    {
      totalMod -= 3;
    }

    return totalMod;
  }

  public static ActionResult MeleeAttack(Actor attacker, Actor target, GameState gs)
  {
    var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
    Item? weapon = attacker.Inventory.ReadiedWeapon();
    int weaponBonus = 0;
    if (weapon is not null)
    {
      foreach (Trait trait in weapon.Traits)
      {
        if (trait is WeaponBonusTrait wb)
          weaponBonus += wb.Bonus;
        if (trait is WeaponSpeedTrait qw)
          result.EnergyCost = qw.Cost;
      }
    }
    
    int roll = AttackRoll(gs.Rng) + CalcAttackMod(attacker, weapon);
    
    var (stressPenalty, _) = attacker.StressPenalty();
    roll -= stressPenalty;

    if (roll >= target.AC)
    {
      if (target.HasTrait<DodgeTrait>() && target.AbleToMove())
      {
        int dodgeChance = target.Traits.OfType<DodgeTrait>().First().Rate;
        int dodgeRoll = gs.Rng.Next(100);
        if (dodgeRoll < dodgeChance && HandleDodge(attacker, target, gs))
        {
          string txt = $"{attacker.FullName.Capitalize()} {Grammar.Conjugate(attacker, "attack")}";
          txt += $" but {target.FullName} {Grammar.Conjugate(target, "dodge")} out of the way!";
          result.Messages.Add(txt);

          return result;
        }        
      }

      if (target.HasTrait<DisplacementTrait>() && target.AbleToMove())
      {        
        int displaceRoll = gs.Rng.Next(100);
        if (displaceRoll <= 33 && HandleDisplacement(attacker, target, gs))
        {
          string txt = $"{attacker.FullName.Capitalize()} {Grammar.Conjugate(attacker, "attack")}";
          txt += $" but {target.FullName} {Grammar.Conjugate(target, "shimmer")} away before reappearing!";
          result.Messages.Add(txt);
          return result;
        }        
      }

      Verb verb = Verb.Hit;
      if (attacker.Traits.OfType<AttackVerbTrait>().FirstOrDefault() is AttackVerbTrait avt)
        verb = avt.Verb;
      ResolveMeleeHit(attacker, target, gs, result, verb, weaponBonus);

      if (weapon is not null && weapon.HasTrait<CleaveTrait>())
      {
        // A versatile weapon only cleaves if it is being wielded with two hands
        // (ie., the attacker doesn't have a shield equipped)
        bool versatile = weapon.HasTrait<VersatileTrait>();
        if (!(versatile && attacker.Inventory.ShieldEquipped()))
        {
          ResolveCleave(attacker, target, roll, gs, result, weaponBonus);
        }        
      }
     
      if (weapon is not null && weapon.HasTrait<ImpaleTrait>())
        ResolveImpale(attacker, target, roll, gs, result, weaponBonus);
      
      if (attacker.HasActiveTrait<KnockBackTrait>())
      {
        string msg = ResolveKnockBack(attacker, target, gs);
        if (msg != "")
          result.Messages.Add(msg);
      }
      
      if (attacker.HasActiveTrait<GrapplerTrait>())
      {
        string msg = ResolveGrapple(attacker, target, gs);
        if (msg != "")
          result.Messages.Add(msg);
      }

      if (attacker.HasTrait<CutpurseTrait>())
      {
        HandleCutpurse(attacker, target, gs, result);
      }
    }
    else
    {
      // The attacker missed!
      result.Messages.Add(MsgFactory.MissMessage(attacker, target, gs));

      // if it is the player, exercise their weapon on a miss
      if (attacker is Player player && weapon is not null)
      {
        foreach (Trait t in weapon.Traits)
        {
          switch (t)
          {            
            case AxeTrait:
              player.ExerciseStat(Attribute.AxeUse);
              break;
            case CudgelTrait:
              player.ExerciseStat(Attribute.CudgelUse);
              break;
            case FinesseTrait:
              player.ExerciseStat(Attribute.FinesseUse);
              break;
            case PolearmTrait:
              player.ExerciseStat(Attribute.PolearmsUse);
              break;
            case SwordTrait:
              player.ExerciseStat(Attribute.SwordUse);
              break;
          }
        }
      }
    }

    return result;
  }

  public static bool HandleDisplacement(Actor attacker, Actor target, GameState gs)
  {
    HashSet<Loc> options = Util.Adj8Locs(attacker.Loc)
                               .Where(sq => !gs.ObjDb.Occupied(sq) && gs.TileAt(sq).Passable())
                               .ToHashSet();
    if (options.Count > 0)
    {
      Loc sq = options.ToList()[gs.Rng.Next(options.Count)];
      string moveMsg = gs.ResolveActorMove(target, target.Loc, sq);
      gs.UIRef().AlertPlayer(moveMsg);
      return true;
    }

    return false;
  }

  static void HandleCutpurse(Actor attacker, Actor target, GameState gs, ActionResult result)
  {
    // If you are attacking with reach, like with a polearm, you don't get 
    // to be a cutpurse
    if (Util.Distance(attacker.Loc, target.Loc) > 1)
      return;

    if (gs.Rng.NextDouble() > 0.2)
      return;

    Item? loot;
    bool intelligent = false;
    foreach (Trait t in target.Traits)
    {
      if (t is RobbedTrait)
        return;
      else if (t is IntelligentTrait)
        intelligent = true;
      else if (t is LootTrait lt)
      {
        loot = Treasure.LootFromTrait(lt, gs.Rng, gs.ObjDb);
        if (loot is not null)
        {
          result.Messages.Add($"You lift {ItemDesc(loot)} from {target.FullName}!");
          target.Traits.Add(new RobbedTrait());
          attacker.Inventory.Add(loot, attacker.ID);
          return;
        }
      }
    }

    if (!intelligent)
      return;

    if (gs.Rng.NextDouble() < 0.5)
    {
      loot = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
      loot.Value = gs.Rng.Next(10, 21);
    }
    else
    {
      loot = Treasure.PoorTreasure(1, gs.Rng, gs.ObjDb)[0];
    }
    result.Messages.Add($"You lift {ItemDesc(loot)} from {target.FullName}!");
    target.Traits.Add(new RobbedTrait());
    attacker.Inventory.Add(loot, attacker.ID);

    static string ItemDesc(Item item)
    {
      if (item.Type != ItemType.Zorkmid)
        return item.FullName.IndefArticle();
      else if (item.Value == 1)
        return "a zorkmid";
      else
        return $"{item.Value} zorkmids";
    }
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
      string moveMsg = gs.ResolveActorMove(target, target.Loc, sq);
      gs.UIRef().AlertPlayer(moveMsg);
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
    if (attacker.HasTrait<TipsyTrait>())
      roll -= gs.Rng.Next(1, 6);
  
    var (stressPenalty, _) = attacker.StressPenalty();
    roll -= stressPenalty;

    if (roll >= target.AC)
    {      
      if (anim is not null)
        gs.UIRef().PlayAnimation(anim, gs);
      ResolveMissileHit(attacker, target, ammo, gs, result);

      result.Complete = true;
    }
    else
    {
      result.Messages.Add(MsgFactory.Phrase(ammo.ID, Verb.Miss, target.ID, 0, true, gs));
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
      result.Messages.Add(txt);
    }

    // Firebolts, ice, should apply their effects to the square they hit
    foreach (var dmg in spell.Traits.OfType<DamageTrait>())
    {
      gs.ApplyDamageEffectToLoc(target.Loc, dmg.DamageType);
    }

    return result;
  }

  // A poison source that is just coated in poison (like a poison dart) has a 
  // chance of the poison wearing out during an attack so check for that here.
  static void CheckCoatedPoison(GameObj obj, Random rng)
  {
    if (obj.HasTrait<PoisonCoatedTrait>() && rng.NextDouble() < 0.5)
    {
      List<Trait> traits = [];
      foreach (Trait t in obj.Traits) 
      {
        if (t is PoisonCoatedTrait || t is PoisonerTrait)
          continue;
        if (t is AdjectiveTrait adj && adj.Adj == "poisoned")
          continue;
        traits.Add(t);
      }
      obj.Traits = traits;
    }
  }

  public static List<string> HandleTipsy(Actor imbiber, GameState gs)
  {
    List<string> messages = [];

    if (imbiber.Stats.TryGetValue(Attribute.Nerve, out var nerve))
    {
      nerve.Change(100);
    }

    bool alreadyTipsy = imbiber.HasTrait<TipsyTrait>();
    int dc = alreadyTipsy ? 15 : 12;
    if (imbiber.AbilityCheck(Attribute.Constitution, dc, gs.Rng))
      return messages;

    if (imbiber.Traits.OfType<TipsyTrait>().FirstOrDefault() is TipsyTrait tipsy)
    {
      tipsy.ExpiresOn += (ulong) gs.Rng.Next(50, 76);
      if (gs!.LastPlayerFoV.Contains(imbiber!.Loc))
        messages.Add($"{imbiber.FullName.Capitalize()} {Grammar.Conjugate(imbiber, "get")} tipsier.");
    }
    else
    {
      tipsy = new TipsyTrait()
      {
        ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(50, 76),
        OwnerID = imbiber.ID
      };
      imbiber.Traits.Add(tipsy);
      gs.RegisterForEvent(GameEventType.EndOfRound, tipsy, imbiber.ID);

      messages.Add($"{imbiber.FullName.Capitalize()} {Grammar.Conjugate(imbiber, "become")} tipsy!");
    }

    if (imbiber.Traits.OfType<FrightenedTrait>().FirstOrDefault() is FrightenedTrait frightened)
    {
      frightened.Remove(imbiber, gs);
    }

    return messages;
  }

  // At the moment I won't have the player attack villagers because I don't 
  // want to make decisions about consequences, etc at the moment.
  public static bool PlayerWillAttack(Actor target) => !target.HasTrait<VillagerTrait>();
}
