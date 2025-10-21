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

enum DamageType
{
  Slashing,
  Piercing,
  Blunt,
  Fire,
  Cold,
  Electricity,
  Poison,
  Acid,
  Necrotic,
  Force,
  Confusion,
  Fear,
  Wet,
  Rust,
  Holy,
  Mud,
  Grease
}

record struct Damage(int Die, int NumOfDie, DamageType Type);

class Battle
{
  // We'll average two d20 rolls to make combat rolls a bit less swinging/
  // evenly distributed. Also, if the first roll is a 20 then the attack hits 
  // regardless. (So even if you have a very high AC lowly monsters still have
  // a small chance of hitting you -- I'm simulating this by returning 100, 
  // which should beat any AC in the game)
  static int AttackRoll(Rng rng) 
  {
    int a = rng.Next(1, 21);
    int b = rng.Next(1, 21);
    if (a == 20)
      return 100;

    return (a + b) / 2;
  } 

  static (int, DamageType) DamageRoll(Damage dmg, Rng rng)
  {
    int total = 0;
    for (int r = 0; r < dmg.NumOfDie; r++)
      total += rng.Next(dmg.Die) + 1;
    return (total, dmg.Type);
  }

  static bool ResolveImpale(Actor attacker, Actor target, int attackRoll, GameState gs, int weaponBonus)
  {
    bool success = false;

    // is there an opponent behind the primary target to impale?
    int diffRow = (attacker.Loc.Row - target.Loc.Row) * 2;
    int diffCol = (attacker.Loc.Col - target.Loc.Col) * 2;
    Loc checkLoc = attacker.Loc with { Row = attacker.Loc.Row - diffRow, Col = attacker.Loc.Col - diffCol };
    Actor? occ = gs.ObjDb.Occupant(checkLoc);
    if (occ is not null && attackRoll >= occ.AC)
    {
      ResolveMeleeHit(attacker, occ, gs, "impale", weaponBonus);
      success = true;
    }

    return success;
  }

  static bool ResolveCleave(Actor attacker, Actor target, int attackRoll, GameState gs, int weaponBonus)
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
          ResolveMeleeHit(attacker, occ, gs, "cleave", weaponBonus);
          success = true;
        }
      }
    }

    return success;
  }

  public static void ResolveMissileHit(GameObj attacker, Actor target, Item ammo, GameState gs)
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
    gs.UIRef().AlertPlayer(txt, gs, target.Loc);    
    var (hpLeft, dmgMsg, _) = target.ReceiveDmg(dmg, bonusDamage, gs, ammo, 1.0);
    if (dmgMsg != "")
      gs.UIRef().AlertPlayer(dmgMsg);    
    ResolveHit(attacker, target, hpLeft, ammo, gs);
    
    bool poisoner = false;
    foreach (var trait in ammo.Traits)
    {
      if (trait is PoisonerTrait poison)
      {
        ApplyPoison(poison, target, gs);
        poisoner = true;
      }
    }

    if (poisoner)
      CheckCoatedPoison(ammo, gs.Rng);
  }

  static void CheckForInfection(int infectionDC, ulong sourceId, Actor victim, GameState gs)
  {    
    if (victim.AbilityCheck(Attribute.Constitution, infectionDC, gs.Rng))
      return;

    DiseasedTrait disease = new() { SourceId = sourceId };

    foreach (string s in disease.Apply(victim, gs))
      gs.UIRef().AlertPlayer(s, gs, victim.Loc);
  }

  static void ApplyPoison(PoisonerTrait source, Actor victim, GameState gs)
  {
    int duration = source.Duration + gs.Rng.Next(-5, 6);
    if (duration < 0)
      duration = 1;
    PoisonedTrait poison = new()
    {
      DC = source.DC,
      Strength = source.Strength,
      Duration = duration
    };

    foreach (string s in poison.Apply(victim, gs))
      gs.UIRef().AlertPlayer(s, gs, victim.Loc);    
  }

  static void CheckAttackTraits(Actor target, GameState gs, GameObj obj, int dmgDone)
  {
    bool poisoner = false;
    foreach (Trait trait in obj.Traits)
    {
      if (trait is PoisonerTrait poison)
      {
        ApplyPoison(poison, target, gs);
        poisoner = true;
      }

      if (trait is InfectiousTrait infect)
      {
        CheckForInfection(infect.DC, obj.ID, target, gs);
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

        foreach (string s in debuff.Apply(target, gs))
          gs.UIRef().AlertPlayer(s);
      }

      if (dmgDone > 0 && obj is Actor actor && trait is MosquitoTrait && gs.Rng.NextDouble() < 0.6)
      {
        Spawn(actor, gs);
      }

      if (trait is CorrosiveTrait)
      {
        
        List<Item> metalItems = [.. target.Inventory
                                      .Items()
                                      .Where(i => i.CanCorrode() && i.Equipped)];

        if (metalItems.Count > 0)
        {
          var damagedItem = metalItems[gs.Rng.Next(metalItems.Count)];
          var (s, _) = EffectApplier.Apply(DamageType.Rust, gs, damagedItem, target);
          gs.UIRef().AlertPlayer(s);
        }
      }
    }

    if (poisoner)
      CheckCoatedPoison(obj, gs.Rng);
  }

  static void Spawn(Actor actor, GameState gs)
  {
    List<Loc> options = [.. Util.Adj8Locs(actor.Loc).Where(loc => gs.TileAt(loc).Passable() && !gs.ObjDb.Occupied(loc))];
    if (options.Count > 0)
    {
      Loc loc = options[gs.Rng.Next(options.Count)];
      Actor spawnling = MonsterFactory.Get(actor.Name, gs.ObjDb, gs.Rng);
      spawnling.Stats[Attribute.HP].SetCurr(actor.Stats[Attribute.HP].Curr);
      gs.ObjDb.AddNewActor(spawnling, loc);
      
      if (gs.LastPlayerFoV.Contains(loc))
        gs.UIRef().AlertPlayer($"{actor.FullName.Capitalize()} spawns!");
    }
  }

  static void ResolveMeleeHit(Actor attacker, Actor target, GameState gs, string attackVerb, int weaponBonus)
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
      gs.UIRef().AlertPlayer(txt);
      
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

    foreach (Trait t in attacker.Traits)
    {
      if (t is RageTrait rt && rt.Active)
      {
        bonusDamage += gs.Rng.Next(1, 7) + gs.Rng.Next(1, 7);
      }
      else if (t is MeleeDamageModTrait dmt)
      {
        bonusDamage += dmt.Amt;
      }      
    }
    
    Item? weapon = attacker.Inventory.ReadiedWeapon();

    string msg = MsgFactory.HitMessage(attacker, target, attackVerb, gs);
    gs.UIRef().AlertPlayer(msg);
    
    double dmgScale = 1.0;
    if (weapon is not null && weapon.Traits.OfType<ViciousTrait>().FirstOrDefault() is ViciousTrait vt)
      dmgScale = vt.Scale;

    var (hpLeft, dmgMsg, dmgDone) = target.ReceiveDmg(dmg, bonusDamage, gs, weapon, dmgScale);    
    if (dmgMsg != "")
      gs.UIRef().AlertPlayer(dmgMsg);
    
    ResolveHit(attacker, target, hpLeft, weapon, gs);
    CheckAttackTraits(target, gs, attacker, dmgDone);

    if (weapon is not null) 
    { 
      CheckAttackTraits(target, gs, weapon, dmgDone);
    }      
  }

  static void ResolveHit(GameObj attacker, Actor target, int hpLeft, Item? weapon, GameState gs)
  {
    if (hpLeft < 1)
    {
      gs.ActorKilled(target, MsgFactory.KillerName(attacker, gs.Player), attacker);
    }

    // This looks convoluted but the target's collection of traits can be modified
    // if the attacker is killed by the acidSplash or fireRebuke (for example, if
    // the attacker had been grappling the target)
    AcidSplashTrait? acidSplash = null;
    FireRebukeTrait? fireRebuke = null;
    foreach (Trait t in target.Traits)
    {
      if (t is AcidSplashTrait acid)
        acidSplash = acid;
      else if (t is FireRebukeTrait rebuke)
        fireRebuke = rebuke;
    }
    acidSplash?.HandleSplash(target, gs);
    if (fireRebuke is not null && attacker is Actor att)
      fireRebuke.Rebuke(target, att, gs);

    Actor? actor = attacker as Actor ?? null;

    if (target.HasTrait<CorrosiveTrait>() && weapon is not null)
    {
      var (s, _) = EffectApplier.Apply(DamageType.Rust, gs, weapon, actor);
      if (attacker is Player)
      {
        gs.UIRef().AlertPlayer(s);          
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
      gs.ResolveActorMove(target, target.Loc, second);
      target.Loc = second;
      
      return $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} knocked backward!";
    }
    else if (CanPass(first, gs))
    {
      gs.ResolveActorMove(target, target.Loc, first);
      target.Loc = first;
      
      return $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} knocked backward!";
    }

    return "";
  }

  static string ResolveGrapple(Actor actor, Actor target, GameState gs, GrapplerTrait grapple)
  {
    // You can only be grappled by one thing at a time
    if (target.HasTrait<GrappledTrait>())
      return "";

    if (target.AbilityCheck(Attribute.Strength, grapple.DC, gs.Rng))
      return "";

    GrappledTrait grappled = new()
    {
      VictimID = target.ID,
      GrapplerID = actor.ID,
      DC = grapple.DC
    };
    gs.RegisterForEvent(GameEventType.Death, grappled, actor.ID);
    target.Traits.Add(grappled);

    GrapplingTrait grappling = new() { VictimId = target.ID };
    actor.Traits.Add(grappling);

    string msg = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} grappled by "; 
    msg += MsgFactory.CalcName(actor, gs.Player) + "!";

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

    foreach (Trait t in attacker.Traits)
    {
      if (t is NauseaTrait)
        totalMod -= 3;
      else if (t is AttackModTrait amt)
        totalMod += amt.Amt;
      else if (t is CurseTrait)
        totalMod -= 3;
    }

    return totalMod;
  }

  public static ActionResult MeleeAttack(Actor attacker, Actor target, GameState gs)
  {    
    var result = new ActionResult() { EnergyCost = 1.0 };
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

    ClearObscured(attacker, gs);

    if (roll >= target.AC)
    {
      if (target.HasTrait<DodgeTrait>() && target.AbleToMove(gs.ObjDb) && !target.HasTrait<InPitTrait>())
      {
        int dodgeChance = target.Traits.OfType<DodgeTrait>().First().Rate;
        int dodgeRoll = gs.Rng.Next(100);
        if (dodgeRoll < dodgeChance && HandleDodge(attacker, target, gs))
        {
          string txt = $"{MsgFactory.CalcName(attacker, gs.Player).Capitalize()} {Grammar.Conjugate(attacker, "attack")}";
          txt += $" but {MsgFactory.CalcName(target, gs.Player)} {Grammar.Conjugate(target, "dodge")} out of the way!";
          gs.UIRef().AlertPlayer(txt);
          
          return result;
        }        
      }

      if (target.HasTrait<DisplacementTrait>() && target.AbleToMove(gs.ObjDb))
      {        
        int displaceRoll = gs.Rng.Next(100);
        if (displaceRoll <= 33 && HandleDisplacement(attacker, target, gs))
        {
          string txt = $"{attacker.FullName.Capitalize()} {Grammar.Conjugate(attacker, "attack")}";
          txt += $" but {target.FullName} {Grammar.Conjugate(target, "shimmer")} away before reappearing!";
          gs.UIRef().AlertPlayer(txt);
          
          return result;
        }        
      }

      bool swallowed = attacker.HasTrait<SwallowedTrait>();

      List<string> messages = [];
      GrapplerTrait? grappler = null;
      bool thief = false;
      foreach (Trait t in attacker.Traits)
      {
        if (t is KnockBackTrait)
        {
          string msg = ResolveKnockBack(attacker, target, gs);
          messages.Add(msg);
        }

        if (t is NumbsTrait)
        {
          NumbedTrait numbed = new() { SourceId = attacker.ID };
          var msgs = numbed.Apply(target, gs);
          messages.AddRange(msgs);          
        }

        if (t is GrapplerTrait gt)
          grappler = gt;

        if (t is CutpurseTrait && !swallowed)
        {
          HandleCutpurse(attacker, target, gs);
        }

        // Thief trait is different from cutpurse because cutpurse just 
        // randomly generates coins. Thief actually takes money out of target's 
        // invetory and puts it in attacker's
        if (t is ThiefTrait && !swallowed)
        {
          thief = true;
        }

        if (t is FrighteningTrait ft && !swallowed)
        {
          FrightenedTrait frightened = new() { DC = ft.DC, ExpiresOn = gs.Turn + 25 };
          List<string> msgs = frightened.Apply(target, gs);
          messages.AddRange(msgs);
        }
      }

      if (grappler is not null)
        messages.Add(ResolveGrapple(attacker, target, gs, grappler));

      string verb = "hit";
      if (attacker.Traits.OfType<AttackVerbTrait>().FirstOrDefault() is AttackVerbTrait avt)
        verb = avt.Verb;
      ResolveMeleeHit(attacker, target, gs, verb, weaponBonus);

      if (messages.Count > 0)
      {        
          gs.UIRef().AlertPlayer(string.Join(' ', messages).Trim(), gs, target.Loc);      
      }

      if (thief)
      {
        HandleThief(attacker, target, gs);
      }

      if (weapon is not null && weapon.HasTrait<CleaveTrait>() && !swallowed)
      {
        // A versatile weapon only cleaves if it is being wielded with two hands
        // (ie., the attacker doesn't have a shield equipped)
        bool versatile = weapon.HasTrait<VersatileTrait>();
        if (!(versatile && attacker.Inventory.ShieldEquipped()))
        {
          ResolveCleave(attacker, target, roll, gs, weaponBonus);
        }
      }

      if (weapon is not null && weapon.HasTrait<ImpaleTrait>() && !swallowed)
        ResolveImpale(attacker, target, roll, gs, weaponBonus);

    }
    else
    {
      // The attacker missed!      
      gs.UIRef().AlertPlayer(MsgFactory.MissMessage(attacker, target, gs));
      
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
    HashSet<Loc> options = [..Util.Adj8Locs(attacker.Loc)
                               .Where(sq => !gs.ObjDb.Occupied(sq) && gs.TileAt(sq).Passable())];
    if (options.Count > 0)
    {
      Loc sq = options.ToList()[gs.Rng.Next(options.Count)];
      gs.ResolveActorMove(target, target.Loc, sq);      
      return true;
    }

    return false;
  }

  static void HandleThief(Actor attacker, Actor target, GameState gs)
  {
    if (target.Inventory.Zorkmids == 0 || gs.Rng.NextDouble() > 0.25)
      return;

    int zorkmids = int.Min(target.Inventory.Zorkmids, gs.Rng.Next(5, 15));
    target.Inventory.Zorkmids -= zorkmids;
    attacker.Inventory.Zorkmids += zorkmids;

    string targetName = MsgFactory.CalcName(target, gs.Player);
    string thiefName = MsgFactory.CalcName(attacker, gs.Player);
    string s = $"{thiefName.Capitalize()} {Grammar.Conjugate(attacker, "lift")} some coins from {targetName}!";

    // Not exactly *frightened* but this will cause the thief to run away
    // for a while when after they successfully steal some zorkmids
    s += attacker.BecomeFrightened(gs);

    gs.UIRef().AlertPlayer(s, gs, target.Loc);
  }

  static void HandleCutpurse(Actor attacker, Actor target, GameState gs)
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

    gs.UIRef().AlertPlayer($"You lift {ItemDesc(loot)} from {target.FullName}!");
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
      Loc sq = options.ToList()[gs.Rng.Next(options.Count)];
      gs.ResolveActorMove(target, target.Loc, sq);
      target.Loc = sq;

      return true;
    }

    return false;
  }

  // attackBonus is because at this point I don't know what weapon shot the ammunition so pass
  // bonsuses related to that here
  public static bool MissileAttack(Actor attacker, Actor target, GameState gs, Item ammo, int attackBonus, Animation? anim)
  {
    bool success = false;
    int roll = AttackRoll(gs.Rng) + attacker.TotalMissileAttackModifier(ammo) + attackBonus;
    if (attacker.HasTrait<TipsyTrait>())
      roll -= gs.Rng.Next(1, 6);
  
    var (stressPenalty, _) = attacker.StressPenalty();
    roll -= stressPenalty;

    if (roll >= target.AC)
    {      
      if (anim is not null)
        gs.UIRef().PlayAnimation(anim, gs);
      ResolveMissileHit(attacker, target, ammo, gs);

      success = true;
    }
    else
    {
      string s = $"{ammo.Name.DefArticle().Capitalize()} misses {MsgFactory.CalcName(target, gs.Player)}!";
      gs.UIRef().AlertPlayer(s, gs, target.Loc);      
    }

    // Firebolts, ice, should apply their effects to the square they hit
    foreach (var dmg in ammo.Traits.OfType<DamageTrait>())
    {
      gs.ApplyDamageEffectToLoc(target.Loc, dmg.DamageType);
    }

    ClearObscured(attacker, gs);

    return success;
  }

  // This is identical to MissileAttack, save for which ability is used for the attack roll. Not
  // going to merge them just yet in case they diverge as I develop more spells
  public static bool MagicAttack(Actor attacker, Actor target, GameState gs, Item spell, int attackBonus, Animation? anim)
  {
    bool success = false;
    int roll = AttackRoll(gs.Rng) + attacker.TotalSpellAttackModifier() + attackBonus;
    if (roll >= target.AC)
    {
      if (anim is not null)
        gs.UIRef().PlayAnimation(anim, gs);
      ResolveMissileHit(attacker, target, spell, gs);
      success = true;
    }
    else
    {
      string txt = $"{spell.FullName.DefArticle().Capitalize()} misses {target.FullName}.";
      gs.UIRef().AlertPlayer(txt);
    }

    // Firebolts, ice, should apply their effects to the square they hit
    foreach (var dmg in spell.Traits.OfType<DamageTrait>())
    {
      gs.ApplyDamageEffectToLoc(target.Loc, dmg.DamageType);
    }

    ClearObscured(attacker, gs);

    return success;
  }

  static void ClearObscured(Actor attacker, GameState gs)
  {
    // hmm maybe I should just prevent acquiring more than one source of
    // obscurity?
    List<NondescriptTrait> toRemove = [.. attacker.Traits.OfType<NondescriptTrait>()];
    foreach (var t in toRemove)
      t.Remove(gs);
  }

  // A poison source that is just coated in poison (like a poison dart) has a 
  // chance of the poison wearing out during an attack so check for that here.
  static void CheckCoatedPoison(GameObj obj, Rng rng)
  {
    if (obj.HasTrait<PoisonCoatedTrait>() && rng.NextDouble() < 0.2)
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
    TipsyTrait? tipsy = imbiber.Traits.OfType<TipsyTrait>()
                                     .FirstOrDefault();

    // Imbiding always reduces stress, even if you pass your saving throw
    if (imbiber.Stats.TryGetValue(Attribute.Nerve, out var nerve))
    {
      nerve.Change(tipsy == null ? 100 : 25);
    }
    
    int dc = tipsy is null ? 15 : 12;
    if (imbiber.AbilityCheck(Attribute.Constitution, dc, gs.Rng))
      return messages;

    if (tipsy is not null)
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
  public static bool PlayerWillAttack(Actor target)
  {
    foreach (Trait t in target.Traits)
    {
      if (t is VillagerTrait)
        return false;
      else if (t is FriendlyMonsterTrait)
        return false;
    }

    return true;
  }
}
