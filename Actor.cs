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

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
abstract class Actor : GameObj, IZLevel
{
  static readonly int FLYING_Z = 10;
  static readonly int DEFAULT_Z = 4;

  public Actor()
  {
    Inventory = new EmptyInventory();
    _behaviour = NullBehaviour.Instance();
  }

  public Dictionary<Attribute, Stat> Stats { get; set; } = [];

  public Inventory Inventory { get; set; }

  public double Energy { get; set; } = 0.0;
  public double Recovery { get; set; } = 1.0;
  public virtual string Appearance { get; set; } = "";

  protected IBehaviour _behaviour;
  public IBehaviour Behaviour => _behaviour;

  protected Queue<Action> ActionQ { get; set; } = [];

  public override int Z()
  {
    foreach (var trait in Traits)
    {
      if (trait is FlyingTrait || trait is FloatingTrait)
        return FLYING_Z;
    }

    return DEFAULT_Z;
  }

  public override List<(Colour, Colour, int)> Lights()
  {
    List<(Colour, Colour, int)> lights = base.Lights();

    foreach (Item item in Inventory.Items())
    {
      lights.AddRange(item.Lights());
    }

    return lights;
  }

  public override int TotalLightRadius()
  {
    int radius = base.TotalLightRadius();

    foreach (var item in Inventory.Items())
    {
      foreach (LightSourceTrait light in item.Traits.OfType<LightSourceTrait>())
      {
        if (light.Radius > radius)
          radius = light.Radius;
      }
    }

    return radius;
  }

  public override string FullName
  {
    get 
    { 
      bool named = false;
      DisguiseTrait? disguised = null;

      foreach (Trait t in Traits)
      {
        if (t is NamedTrait)
          named = true;
        else if (t is DisguiseTrait dt && dt.Disguised)
          disguised = dt;
      }

      string txt = disguised is null ? Name : disguised.DisguiseForm;

      return named ? txt.Capitalize() : txt.DefArticle();
     }
  }

  public virtual int TotalMissileAttackModifier(Item weapon) => 0;
  public virtual int TotalSpellAttackModifier() => 0;
  public virtual int AC => 10;
  public virtual List<Damage> MeleeDamage() => [];
  public virtual void HearNoise(int volume, int sourceRow, int sourceColumn, GameState gs) { }
  public virtual int SpellDC => 12;

  public void QueueAction(Action action) => ActionQ.Enqueue(action);

  public bool IsDisguised()
  {
    foreach (Trait t in Traits)
    {
      if (t is DisguiseTrait disguise && disguise.Disguised)
        return true;
    }

    return false;
  }

  public int GetMovementNoise()
  {
    int baseNoise = 9;
    int modifiers = 0;
    foreach (Trait t in Traits)
    {
      if (t is LightStepTrait)
        baseNoise = 5;
      else if (t is QuietTrait) // Sources of Quiet can stack
        modifiers -= 3;
    }

    // If the actor is wearing a shirt that's made of non-mithril metal, it will add
    // to the noisiness. (Only shirts because I feel like a metal helmet wouldn't 
    // be especially loud)
    var armour = Inventory.Items().Where(i => i.Type == ItemType.Armour && i.Equipped);
    foreach (var piece in armour)
    {
      ArmourTrait? armourTrait = piece.Traits.OfType<ArmourTrait>().FirstOrDefault();
      // It would actually be an error for this to be null
      if (armourTrait is not null && (armourTrait.Part == ArmourParts.Shirt))
      {
        var metal = piece.MetalType();
        if (metal != Metals.NotMetal && metal != Metals.Mithril)
        {
          modifiers += 3;
          break;
        }
      }
    }

    return int.Max(1, baseNoise + modifiers);
  }

  public (int, StressLevel) StressPenalty()
  {
    int penalty = 0;
    StressLevel level = StressLevel.None;

    if (Traits.OfType<StressTrait>().FirstOrDefault() is StressTrait stress)
    {
      level = stress.Stress;
      penalty = stress.Stress switch
      {
        StressLevel.Skittish => -1,
        StressLevel.Nervous => -2,
        StressLevel.Anxious => -3,
        StressLevel.Paranoid => -4,
        StressLevel.Hystrical => -5,
        _ => 0
      };
    }

    return (penalty, level);
  }

  // Clear out various Traits that may be pinning the Actor to a location
  // (Usually due to various versions of teleportation)
  public void ClearAnchors(GameState gs)
  {
    List<Trait> toRemove = [.. Traits.Where(t => t is InPitTrait || t is GrappledTrait || t is SwallowedTrait)];
    foreach (Trait t in toRemove)
    {
      if (t is InPitTrait)
      {
        Traits.Remove(t);
      }
      else if (t is GrappledTrait grappled)
      {
        Traits.Remove(t);
      }
    }
  }

  public bool AbleToMove(GameObjectDB objDb)
  {
    bool teflon = false;
    foreach (var t in Traits)
    {
      if (t is ParalyzedTrait)
        return false;
      if (t is GrappledTrait)
        return false;
      if (t is TelepathyTrait)
        teflon = true;
    }

    foreach (Item env in objDb.EnvironmentsAt(Loc))
    {
      if (env.HasTrait<StickyTrait>() && !teflon)
        return false;
    }
 
    return true;
  }

  public virtual (int, string, int) ReceiveDmg(IEnumerable<(int, DamageType)> damages, int bonusDamage, GameState gs, GameObj? src, double scale)
  {
    string msg = "";

    if (!Stats.TryGetValue(Attribute.HP, out var currHP))
      return (0, "", 0);

    Traits.RemoveAll(t => t is SleepingTrait);
    if (Stats.TryGetValue(Attribute.MobAttitude, out Stat? attitude))
    {
      // Source is the weapon/actual source of damage, not the moral agent
      // responsible for causing the damage. Perhaps I should include a ref
      // to the attacker, because the monster maybe shouldn't become aggressitve
      // if the attack doesn't come from the player?      
      attitude.SetMax(Mob.AGGRESSIVE);
    }

    // If we have allies, let them know we've turned hostile
    if (Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait allies)
    {
      foreach (ulong id in allies.IDs)
      {
        if (gs.ObjDb.GetObj(id) is Mob ally && gs.CanSeeLoc(Loc, 6))
        {
          ally.Traits.RemoveAll(t => t is SleepingTrait);
          ally.Stats[Attribute.MobAttitude] = new Stat(Mob.AGGRESSIVE);
          string name = MsgFactory.CalcName(ally, gs.Player);
          gs.UIRef().AlertPlayer($"{name.Capitalize()} becomes angry.", gs, ally.Loc);
        }
      }
    }

    HashSet<DamageType> resistances = [];
    HashSet<DamageType> immunities = [];
    HashSet<DamageType> vulnerabilities = [];
    foreach (Trait t in Traits)
    {
      if (t is ResistanceTrait res)
        resistances.Add(res.Type);
      else if (t is ImmunityTrait imm)
        immunities.Add(imm.Type);
      else if (t is VulnerableTrait vul)
        vulnerabilities.Add(vul.Type);
    }

    int total = 0;
    bool fireDamage = false;
    bool coldDamage = false;
    foreach (var dmg in damages)
    {
      if (dmg.Item2 == DamageType.Fire)
        fireDamage = true;
      else if (dmg.Item2 == DamageType.Cold)
        coldDamage = true;

      int d = dmg.Item1;

      if (immunities.Contains(dmg.Item2))
      {
        d = 0;
        bonusDamage = 0;
        msg = MsgFactory.CalcResistanceMessage(dmg.Item2, true);
      }
      else if (resistances.Contains(dmg.Item2))
      {
        d /= 2;
        msg = MsgFactory.CalcResistanceMessage(dmg.Item2, false);
      }
      else if (vulnerabilities.Contains(dmg.Item2))
      {
        d *= 2;
        msg = $"{FullName.Capitalize()} {Grammar.Conjugate(this, "is")} in agony!";
      }

      if (d > 0)
        total += d;
    }
    total += bonusDamage;
    total = (int)(total * scale);

    //Console.WriteLine($"{FullName.Capitalize()} took {total} damage.");
    
    if (total > 0 && coldDamage && Traits.OfType<BoolTrait>().Any(t => t.Name == "WaterElemental" && t.Value))
    {
      gs.ObjDb.RemoveActor(this);
      int currHp = Stats[Attribute.HP].Curr;
      Actor iceElemental = MonsterFactory.Get("ice elemental", gs.ObjDb, gs.Rng);
      iceElemental.Stats[Attribute.HP].SetMax(currHp);
      gs.ObjDb.AddNewActor(iceElemental, Loc);
      gs.UIRef().AlertPlayer("The water elemental freezes solid!", gs, Loc);
      return (999, "", 0);
    }

    if (total > 0 && fireDamage && Traits.OfType<BoolTrait>().Any(t => t.Name == "IceElemental" && t.Value))
    {
      gs.ObjDb.RemoveActor(this);
      int currHp = Stats[Attribute.HP].Curr;
      Actor waterElemental = MonsterFactory.Get("water elemental", gs.ObjDb, gs.Rng);
      waterElemental.Stats[Attribute.HP].SetMax(currHp);
      gs.ObjDb.AddNewActor(waterElemental, Loc);
      gs.UIRef().AlertPlayer("The ice elemental melts!", gs, Loc);
      return (999, "", 0);
    }
    
    if (total > 0 && fireDamage && Name == "mud golem" && this is not Player)
    {
      gs.ObjDb.RemoveActor(this);
      Actor clayGolem = MonsterFactory.Get("clay golem", gs.ObjDb, gs.Rng);
      gs.ObjDb.AddNewActor(clayGolem, Loc);
      gs.UIRef().AlertPlayer("The mud golem becomes fully baked!", gs, Loc);
      return (999, "", 0);
    }

    if (total > 0 && IsDisguised())
    {
      DisguiseTrait dt = Traits.OfType<DisguiseTrait>().First();
      gs.UIRef().AlertPlayer($"Wait! That {dt.DisguiseForm} is actually {Name.IndefArticle()}!", gs, Loc);
      Glyph = dt.TrueForm;
      dt.Disguised = false;
    }

    if (HasTrait<SilverAllergyTrait>() && src is Item weapon && weapon.MetalType() == Metals.Silver)
    {
      total += gs.Rng.Next(1, 7) + gs.Rng.Next(1, 7) + gs.Rng.Next(1, 7);
      msg += $" {weapon.FullName.DefArticle().Capitalize()} sears {FullName}!";
    }

    if (total > 0 && fireDamage)
    {
      string s = Inventory.ApplyEffectToInv(DamageType.Fire, gs, Loc);
      if (s != "")
        msg += $" {s}";
    }

    if (total > 0)
    {
      Animation anim;
      if (fireDamage)
      {
        anim = new HitAnimation(gs, this, Colours.BRIGHT_RED, Colours.TORCH_YELLOW, Constants.FIRE_CHAR);
      }
      else if (coldDamage)
      {
        anim = new HitAnimation(gs, this, Colours.WHITE, Colours.ICE_BLUE, '*');
      }
      else
      {
        char ch = VisibleTo(gs.Player) ? Glyph.Ch : ' ';
        anim = new HitAnimation(gs, this, Colours.WHITE, Colours.FX_RED, ch);
      }
      gs.UIRef().RegisterAnimation(anim);
    }
    
    if (Traits.OfType<AuraOfProtectionTrait>().FirstOrDefault() is AuraOfProtectionTrait aura)
    {
      aura.HP -= total;
      if (aura.HP > 0)
      {
        msg += "The shimmering aura cracks.";
        total = 0;
      }
      else
      {
        msg += "The shimmering aura shatters into glitter and sparks!";
        total = -1 * aura.HP;
        Traits.Remove(aura);
      }
    }

    if (Traits.OfType<CrimsonWard>().FirstOrDefault() is CrimsonWard cw && currHP.Curr < currHP.Max)
    {
      bool s = false;
      foreach (Loc adj in Util.Adj8Locs(Loc))
      {
        if (gs.ObjDb.Occupant(adj) is Actor adjActor)
        {
          int dmgDice = 1 + (currHP.Max - currHP.Curr) / 10;

          if (!s)
            gs.UIRef().AlertPlayer("The Ward lashes out!", gs, Loc);

          int wardDmg = 0;
          for (int j = 0; j < dmgDice; j++)
            wardDmg += gs.Rng.Next(6) + 1;
          List<(int, DamageType)> dmg = [(total, DamageType.Force)];
          var (adjHpLeft, _, _) = adjActor.ReceiveDmg(dmg, 0, gs, null, 1.0);
          if (adjHpLeft < 1)
          {
            gs.ActorKilled(adjActor, "a Crimson Ward", null);
          }

          gs.UIRef().RegisterAnimation(new SqAnimation(gs, adj, Colours.YELLOW_ORANGE, Colours.BRIGHT_RED, '*'));

          s = true;
        }
      }
    }

    currHP.Curr -= total;

    if (currHP.Curr < 1 && HasTrait<InvincibleTrait>())
    {
      currHP.SetCurr(1);
    }

    if (HasTrait<DividerTrait>() && currHP.Curr > 2)
    {
      foreach (var dmg in damages)
      {
        switch (dmg.Item2)
        {
          case DamageType.Piercing:
          case DamageType.Slashing:
          case DamageType.Blunt:
            Divide(gs);
            goto done_dividing;
          default:
            continue;
        }
      }
    }
    done_dividing:

    // Is the monster now afraid?
    if (!HasTrait<FrightenedTrait>())
    {
      int maxHP = Stats[Attribute.HP].Max;
      if (this is Mob && !HasTrait<BrainlessTrait>() && currHP.Curr <= maxHP / 2 && currHP.Curr > 0)
      {
        float odds = (float)currHP.Curr / maxHP;
        if (gs.Rng.NextDouble() < odds)
        {
          msg += BecomeFrightened(gs);
        }
      }
    }

    return (Stats[Attribute.HP].Curr, msg.Trim(), total);
  }

  public string BecomeFrightened(GameState gs)
  {
    FrightenedTrait frightened = new()
    {
      OwnerID = ID,
      ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(10, 21),
      DC = int.MaxValue // We want to guarantee it takes effect
    };
    frightened.Apply(this, gs);

    if (HasTrait<FullBellyTrait>())
      EmptyBelly(gs);

    if (Traits.OfType<GrapplingTrait>().FirstOrDefault() is GrapplingTrait gt)
      ClearGrapple(gt, gs);

    string msg = VisibleTo(gs.Player) ? $" {FullName.Capitalize()}" : "A monster";
    msg += " turns to flee!";

    return msg;
  }

  void ClearGrapple(GrapplingTrait gt, GameState gs)
  {
    if (gs.ObjDb.GetObj(gt.VictimId) is Actor victim)
    {
      if (victim.Traits.OfType<GrappledTrait>().FirstOrDefault() is GrappledTrait grappled)
      {
        grappled.Remove(gs);

        string victimName = MsgFactory.CalcName(victim, gs.Player).Capitalize();
        string grappler = MsgFactory.CalcName(this, gs.Player);
        string s = $"{victimName} {Grammar.Conjugate(victim, "is")} released by {grappler}!";
        gs.UIRef().AlertPlayer(s, gs, victim.Loc);
      }
    }
  }

  void EmptyBelly(GameState gs)
  {
    var full = Traits.OfType<FullBellyTrait>().First();
    if (gs.ObjDb.GetObj(full.VictimID) is Actor victim)
    {
      var swallowed = victim.Traits.OfType<SwallowedTrait>().FirstOrDefault();
      swallowed?.Remove(gs);
    }
  }

  // Candidate spots will be spots adjacent to the contiguous group of the 
  // same monster. (I'm just basing this on name since I don't really have
  // a monster-type field) Yet another floodfill of sorts...
  void Divide(GameState gs)
  {
    var map = gs.Campaign.Dungeons[Loc.DungeonID].LevelMaps[Loc.Level];
    List<Loc> candidateSqs = [];
    Queue<Loc> q = [];
    q.Enqueue(Loc);
    HashSet<Loc> visited = [];

    while (q.Count > 0)
    {
      var curr = q.Dequeue();

      if (visited.Contains(curr))
        continue;

      visited.Add(curr);

      foreach (var adj in Util.Adj8Locs(curr))
      {
        var tile = map.TileAt(adj.Row, adj.Col);
        var occ = gs.ObjDb.Occupant(adj);
        if (occ is null && tile.Passable())
        {
          candidateSqs.Add(adj);
        }
        if (!visited.Contains(adj) && occ is not null && occ.Name == Name && occ is not Player)
        {
          q.Enqueue(adj);
        }
      }
    }

    if (candidateSqs.Count > 0)
    {
      var spot = candidateSqs[gs.Rng.Next(candidateSqs.Count)];
      var other = MonsterFactory.Get(Name, gs.ObjDb, gs.Rng);
      var hp = Stats[Attribute.HP].Curr / 2;
      var half = Stats[Attribute.HP].Curr - hp;
      Stats[Attribute.HP].Curr = hp;
      other.Stats[Attribute.HP].SetMax(half);

      string msg = $"{Name.DefArticle().Capitalize()} divides in two!!";
      gs.UIRef().AlertPlayer(msg, gs, Loc);
      gs.ObjDb.AddNewActor(other, spot);
    }
  }

  public virtual void SetBehaviour(IBehaviour behaviour) => _behaviour = behaviour;

  public abstract Actor PickTarget(GameState gs);
  public abstract Loc PickTargetLoc(GameState gamestate, int range);
  public abstract Loc PickRangedTargetLoc(GameState gamestate, int range);
  public abstract void TakeTurn(GameState gs);
  public abstract void CalcHP();

  public int AbilityRoll(Attribute attr, Rng rng)
  {
    int statMod = Stats.TryGetValue(attr, out var stat) ? stat.Curr : 0;
    return rng.Next(1, 21) + statMod;
  }

  public bool AbilityCheck(Attribute attr, int dc, Rng rng)
  {
    int statMod = Stats.TryGetValue(attr, out var stat) ? stat.Curr : 0;
    int roll = rng.Next(20) + 1 + statMod;

    foreach (Trait t in Traits)
    {
      if (attr == Attribute.Strength && t is RageTrait rage && rage.Active)
        roll += rng.Next(1, 7);
      else if (t is CurseTrait)
        roll -= 3;
    }

    if (attr == Attribute.Strength && HasActiveTrait<RageTrait>())
      roll += rng.Next(1, 7);


    return roll >= dc;
  }

  public virtual char AddToInventory(Item item, GameState? gs)
  {
    char slot = Inventory.Add(item, ID);
    if (gs is not null && slot == '\0')
    {
      gs.ItemDropped(item, Loc);
      gs.UIRef().AlertPlayer($"{item.FullName.DefArticle().Capitalize()} falls to the ground.", gs, Loc);
    }

    return slot;
  }

  // The default is that a monster/NPC will get angry if the player picks up 
  // something which belongs to them
  public virtual string PossessionPickedUp(ulong itemID, Actor other, GameState gameState)
  {
    if (gameState.CanSeeLoc(other.Loc, 6))
    {
      Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);

      return $"{FullName.Capitalize()} gets angry!";
    }

    return "";
  }

  // Which glyph to display, from the perspective of 'this' looking at other
  public Glyph? GlyphSeen(Actor other, bool playerTelepathic, bool playerSeeInvisible)
  {
    if (ID == other.ID)
      return Glyph;

    Glyph glyph = other.Glyph;
    bool invisible = false;
    foreach (Trait t in other.Traits)
    {
      if (playerTelepathic && t is DisguiseTrait disguise)
        glyph = disguise.TrueForm;
      else if (t is InvisibleTrait)
        invisible = true;
    }

    if (invisible && !(playerSeeInvisible || playerTelepathic))
      return null;

    return glyph;
  }

  public bool VisibleTo(Actor other)
  {
    if (ID == other.ID)
      return true;

    bool seeInvisible = false;
    bool telepathy = false;
    bool blinded = false;
    foreach (Trait t in other.Traits)
    {
      if (t is SeeInvisibleTrait)
        seeInvisible = true;
      else if (t is TelepathyTrait) 
        telepathy = true;
      else if (t is BlindTrait)
        blinded = true;
    }

    if (HasTrait<InvisibleTrait>() && !(seeInvisible || telepathy))
      return false;
    if (blinded && !telepathy)
      return false;

    return true;
  }

  protected double CalcEnergyUsed(double baseCost)
  {
    // Maybe I should come up with a formal/better way to differentiate 
    // between real in-game actions and things like opening inventory or
    // looking athelp, etc?
    if (baseCost == 0)
      return 0;

    // Note also there are some actions like Chatting, etc that
    // shouldn't be made faster or slower by alacrity, but I'll
    // worry about that later

    foreach (var t in Traits.OfType<AlacrityTrait>())
    {
      baseCost -= t.Amt;
    }

    // I think boosts to speed should get you only so far
    return Math.Max(0.35, baseCost);
  }
}

sealed class Mob : Actor
{
  public List<Power> Powers { get; set; } = []; // this will supersede the Actions list
  public Dictionary<string, ulong> LastPowerUse = []; // I'll probably want to eventually serialize these

  BehaviourNode? CurrPlan { get; set; } = null;
  public void ClearPlan() => CurrPlan = null;

  public const int INACTIVE = 0;
  public const int INDIFFERENT = 1;
  public const int AGGRESSIVE = 2;

  public Mob() => _behaviour = new MonsterBehaviour();

  public override string Appearance
  {
    get
    {
      if (base.Appearance == "")
      {
        var cyclopedia = Util.LoadCyclopedia();
        if (cyclopedia.TryGetValue(Name, out var entry))
          return entry.Text;
      }

      return base.Appearance;
    }
  }

  public Damage? Dmg { get; set; }
  public override List<Damage> MeleeDamage()
  {
    List<Damage> dmgs = [Dmg ?? new Damage(4, 1, DamageType.Blunt)];

    return dmgs;
  }

  public void SetAttitude(int attitude)
  {
    if (attitude == AGGRESSIVE && HasTrait<FriendlyMonsterTrait>())
      return;

    Stats[Attribute.MobAttitude] = new Stat(attitude);
  }

  public override void HearNoise(int volume, int sourceRow, int sourceColumn, GameState gs)
  {
    int threshold = volume - Util.Distance(sourceRow, sourceColumn, Loc.Row, Loc.Col);
    bool heard = gs.Rng.Next(11) <= threshold;
    if (!heard)
      return;

    if (Stats.TryGetValue(Attribute.MobAttitude, out var attitude) && !HasTrait<WorshiperTrait>() && !HasTrait<VillagerTrait>())
    {
      Stats[Attribute.MobAttitude].SetMax(AGGRESSIVE);
    }

    if (HasTrait<SleepingTrait>())
    {
      if (gs.LastPlayerFoV.ContainsKey(Loc))
        gs.UIRef().AlertPlayer($"{FullName.Capitalize()} wakes up.");
      Traits.RemoveAll(t => t is SleepingTrait);
    }
  }

  public override void CalcHP() { }

  public override int TotalMissileAttackModifier(Item weapon)
  {
    return Stats.TryGetValue(Attribute.AttackBonus, out var ab) ? ab.Curr : 0;
  }

  public override int TotalSpellAttackModifier()
  {
    return Stats.TryGetValue(Attribute.AttackBonus, out var ab) ? ab.Curr : 0;
  }

  //public override int AC => Stats.TryGetValue(Attribute.AC, out var ac) ? ac.Curr : base.AC;

  public override int AC
  {
    get
    {
      int ac = Stats.TryGetValue(Attribute.AC, out var baseAC) ? baseAC.Curr : base.AC;

      int armour = 0;
      foreach (char slot in Inventory.UsedSlots())
      {
        var (item, _) = Inventory.ItemAt(slot);
        if (item is not null && item.Equipped)
        {
          armour += item.Traits.OfType<ArmourTrait>()
                               .Select(t => t.ArmourMod + t.Bonus)
                               .Sum();
          armour += item.Traits.OfType<ACModTrait>()
                               .Select(t => t.ArmourMod)
                               .Sum();
        }
      }

      foreach (Trait t in Traits)
      {
        if (t is ACModTrait acMod)
          ac += acMod.ArmourMod;
        if (t is MageArmourTrait ma)
          ac += 3;
      }

      return ac + armour;
    }
  }

  public void ExecuteAction(Action action)
  {
    double result = action.Execute();
    Energy -= CalcEnergyUsed(result);
  }

  public string GetBark(GameState gs) => _behaviour.GetBark(this, gs);

  public void ResetPlan() => CurrPlan = null;

  public override void TakeTurn(GameState gs)
  {
    if (ActionQ.Count > 0)
    {
      ExecuteAction(ActionQ.Dequeue());
    }
    else
    {
      if (CurrPlan is null)
      {
        string planName = Traits.OfType<BehaviourTreeTrait>().First().Plan;
        CurrPlan = Planner.GetPlan(planName, this, gs);
      }

      if (CurrPlan.Execute(this, gs) == PlanStatus.Failure)
      {
        CurrPlan = null;
        ExecuteAction(new PassAction());
      }
    }
    
    gs.PrepareFieldOfView();
  }

  static Loc PickInvisibleTarget(Loc loc, GameState gs, int range)
  {
    List<Loc> randomLoc = [loc];
    foreach (Loc adj in Util.Adj4Locs(gs.Player.Loc))
    {
      if (Util.Distance(adj, loc) > range)
        continue;
      if (!gs.ObjDb.Occupied(adj) && gs.TileAt(adj).PassableByFlight())
        randomLoc.Add(adj);
    }

    return randomLoc.Count > 0 ? randomLoc[gs.Rng.Next(randomLoc.Count)] : Loc.Nowhere;
  }

  // At the moment, monsters will pick the player, but I'm working toward
  // changing that
  public override Actor PickTarget(GameState gs)
  {
    if (gs.Player.HasTrait<NondescriptTrait>())
      return NoOne.Instance();

    if (gs.Player.VisibleTo(this))
      return gs.Player;

    return NoOne.Instance();
  }

  public override Loc PickTargetLoc(GameState gs, int range)
  {
    if (gs.Player.HasTrait<NondescriptTrait>())
      return Loc.Nowhere;

    if (HasTrait<ConfusedTrait>())
      Util.RandomAdjLoc(Loc, gs);

    if (!gs.Player.VisibleTo(this))
      return PickInvisibleTarget(gs.Player.Loc, gs, range);

    return gs.Player.Loc;
  }

  // I suspect eventually these will diverge
  public override Loc PickRangedTargetLoc(GameState gameState, int range) => PickTargetLoc(gameState, range);
}

class MonsterFactory
{
  static Dictionary<string, string> _catalog = [];

  static void LoadCatalog()
  {
    string path = ResourcePath.GetDataFilePath("monsters.txt");
    foreach (var line in File.ReadAllLines(path))
    {
      int i = line.IndexOf('|');
      string name = line[..i].Trim();
      string val = line[(i + 1)..];
      _catalog.Add(name, val);
    }
  }

  public static Actor Get(string name, GameObjectDB objDb, Rng rng)
  {
    if (_catalog.Count == 0)
      LoadCatalog();

    if (!_catalog.TryGetValue(name, out string? template))
      throw new UnknownMonsterException(name);

    var fields = template.Split('|').Select(f => f.Trim()).ToArray();

    char ch = fields[0].Length == 0 ? ' ' : fields[0][0];
    Glyph glyph = new(ch, Colours.TextToColour(fields[1]),
                           Colours.TextToColour(fields[2]), Colours.BLACK, false);

    Mob m = new()
    {
      Name = name,
      Glyph = glyph,
      Recovery = Util.ToDouble(fields[6])
    };

    int ac = int.Parse(fields[3]);
    m.Stats.Add(Attribute.AC, new Stat(ac));
    int hp = int.Parse(fields[4]);
    m.Stats.Add(Attribute.HP, new Stat(hp));
    int attBonus = int.Parse(fields[5]);
    m.Stats.Add(Attribute.AttackBonus, new Stat(attBonus));
    int str = Util.StatRollToMod(int.Parse(fields[7]));
    m.Stats.Add(Attribute.Strength, new Stat(str));
    int dex = Util.StatRollToMod(int.Parse(fields[8]));
    m.Stats.Add(Attribute.Dexterity, new Stat(dex));
    int attitude = rng.NextDouble() <= 0.8 ? Mob.INDIFFERENT : Mob.AGGRESSIVE;
    m.Stats.Add(Attribute.MobAttitude, new Stat(attitude));

    if (fields[9] != "")
    {
      foreach (string powerTxt in fields[9].Split(','))
      {
        try
        {
          m.Powers.Add(Power.FromText(powerTxt));
        }
        catch (Exception) { }
      }
    }

    if (!string.IsNullOrEmpty(fields[10]))
    {
      foreach (string traitTxt in fields[10].Split(','))
      {
        Trait trait = TraitFactory.FromText(traitTxt, m);
        m.Traits.Add(trait);

        if (trait is IGameEventListener listener)
        {
          objDb.EndOfRoundListeners.Add(listener);
        }
      }
    }

    m.Inventory = new Inventory(m.ID, objDb);

    if (!string.IsNullOrEmpty(fields[11]))
    {
      foreach (string itemTemplate in fields[11].Split(','))
      {
        if (itemTemplate == "PoorLoot")
        {
          Item item = Treasure.PoorTreasure(1, rng, objDb)[0];
          m.AddToInventory(item, null);
        }
        else if (itemTemplate == "GoodMagic")
        {
          Item item = Treasure.GoodMagicItem(rng, objDb);
          m.AddToInventory(item, null);
        }
        else if (itemTemplate.StartsWith("Coins"))
        {
          string[] pieces = itemTemplate.Split('#');
          int zorkmids = rng.Next(int.Parse(pieces[1]), int.Parse(pieces[2]) + 1);
          m.Inventory.Zorkmids = zorkmids;
        }
        else
        {
          string[] pieces = itemTemplate.Split('#');
          Enum.TryParse(pieces[0], out ItemNames itemName);
          int itemCount = int.Parse(pieces[1]);
          int odds = int.Parse(pieces[2]);
          bool equiped = bool.Parse(pieces[3]);

          if (rng.Next(100) < odds)
          {
            int total = rng.Next(itemCount) + 1;
            for (int j = 0; j < total; j++)
            {
              Item item = ItemFactory.Get(itemName, objDb);
              char slot = m.AddToInventory(item, null);
              if (equiped && slot != '$')
                m.Inventory.ToggleEquipStatus(slot);
            }
          }
        }
      }
    }

    // Yes, I will write code just to insert a joke/Simpsons reference
    // into the game
    if (name == "zombie" && rng.Next(100) == 0)
      m.Traits.Add(new DeathMessageTrait() { Message = "Is this the end of Zombie Shakespeare?" });

    if (!m.HasTrait<BehaviourTreeTrait>())
      m.Traits.Add(new BehaviourTreeTrait() { Plan = "MonsterPlan" });

    return m;
  }

  // I didn't put mimics in the monster data file because they're going they
  // need to be placed and configured specifcally anyhow.
  public static Actor Mimic(bool random, Rng rng)
  {
    Glyph glyph;
    string name;
    if (random)
    {
      (name, glyph) = ItemFactory.MimicDetails(rng);
    }
    else
    {
      glyph = new('+', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false);
      name = "closed door";
    }
             
    Mob mimic = new() { Name = "mimic", Recovery = 1.0, Glyph = glyph };
    mimic.Traits.Add(new BehaviourTreeTrait() { Plan = "MimicPlan" });

    mimic.Stats.Add(Attribute.HP, new Stat(40));
    mimic.Stats.Add(Attribute.AttackBonus, new Stat(3));
    mimic.Stats.Add(Attribute.AC, new Stat(15));
    mimic.Stats.Add(Attribute.Strength, new Stat(1));
    mimic.Stats.Add(Attribute.Dexterity, new Stat(0));
    mimic.Stats.Add(Attribute.MobAttitude, new Stat(Mob.INDIFFERENT));

    mimic.Powers.Add(Power.FromText("MeleeBlunt#1#1#6#2#0#0#Attack"));
    mimic.Traits.Add(new GrapplerTrait() { DC = 18 });

    mimic.Traits.Add(new ImmobileTrait());

    DisguiseTrait disguise = new()
    {
      Disguise = glyph,
      TrueForm = new Glyph('m', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, false),
      DisguiseForm = name,
      Disguised = true
    };
    mimic.Traits.Add(disguise);

    return mimic;
  }
}

// Class for traicking powers/abilities monsters have access to
enum PowerType { Attack, Passive, Movement }
class Power
{
  public string Name { get; set; } = "";
  public int MinRange { get; set; } = 1;
  public int MaxRange { get; set; } = 1;
  public int DmgDie { get; set; }
  public int NumOfDice { get; set; }
  public int DC { get; set; }
  public ulong Cooldown { get; set; }
  public PowerType Type { get; set; }
  public string Quip { get; set; } = "";

  public static Power FromText(string txt)
  {
    string[] pieces = txt.Split('#');

    Enum.TryParse(pieces[7], out PowerType type);
    string quip = pieces.Length > 8 ? pieces[8] : "";

    return new Power()
    {
      Name = pieces[0],
      MinRange = int.Parse(pieces[1]),
      MaxRange = int.Parse(pieces[2]),
      DmgDie = int.Parse(pieces[3]),
      NumOfDice = int.Parse(pieces[4]),
      DC = int.Parse(pieces[5]),
      Cooldown = ulong.Parse(pieces[6]),
      Type = type,
      Quip = quip
    };
  }

  public override string ToString() => $"{Name}#{MinRange}#{MaxRange}#{DmgDie}#{NumOfDice}#{DC}#{Cooldown}#{Type}#{Quip}";

  public Action Action(Mob mob, GameState gs, Loc loc)
  {
    string txt;

    switch (Name)
    {
      case "MeleeSlashing":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Slashing);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleePiercing":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Piercing);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeBlunt":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Blunt);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeAcid":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Acid);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeCold":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Cold);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeFire":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Fire);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeElectric":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Electricity);
        return new MeleeAttackAction(gs, mob, loc);
      case "MeleeNecrotic":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Necrotic);
        return new MeleeAttackAction(gs, mob, loc);
      case "MissilePiercing":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Necrotic);
        var arrow = ItemFactory.Get(ItemNames.ARROW, gs.ObjDb);
        return new MissileAttackAction(gs, mob, loc, arrow);
      case "CastCurse":
        return new CastCurse(loc, DC)
        {
          GameState = gs,
          Actor = mob
        };
      case "CastTeleportAway":
        return new CastTeleportAway(loc) { GameState = gs, Actor = mob };
      case "RumBreath":
        return new RumBreathAction(gs, mob, loc, MaxRange);
      case "Nudity":
        return new InduceNudityAction(gs, mob,MaxRange);
      case "FogCloud":
        return new FogCloudAction(gs, mob, MaxRange);
      case "InkCloud":
        return new InkCloudAction(gs, mob);
      case "Blink":
        return new BlinkAction(gs, mob);
      case "SummonKobold":
        List<string> kobolds = ["kobold", "kobold", "kobold"];

        if (mob.Loc.Level > 2)
        {
          kobolds.Add("kobold bully");
          kobolds.Add("kobold knight");
        }

        if (mob.Loc.Level > 4)
          kobolds.Add("kobold artillerist");

        return new SummonAction(mob.Loc, kobolds[gs.Rng.Next(kobolds.Count)], 1)
        {
          GameState = gs,
          Actor = mob,
          Quip = Quip
        };
      case "SummonCaveLizard":
        return new SummonAction(mob.Loc, "cave lizard", 1)
        {
          GameState = gs,
          Actor = mob,
          Quip = Quip
        };
      case "SummonCentipede":
        return new SummonAction(mob.Loc, "centipede", 2)
        {
          GameState = gs,
          Actor = mob,
          Quip = Quip
        };
      case "SummonUndead":
        List<string> undead = ["skeleton", "zombie"];

        if (mob.Loc.Level >= 2)
        {
          undead.Add("ghoul");
          undead.Add("phantom");
        }

        if (mob.Loc.Level >= 1)
          undead.Add("shadow");

        if (mob.Loc.Level == 1)
        {
          undead.Add("skeleton");
          undead.Add("skeleton");
          undead.Add("zombie");
          undead.Add("zombie");
        }

        string summons = undead[gs.Rng.Next(undead.Count)];

        return new SummonAction(mob.Loc, summons, 1) { GameState = gs, Actor = mob, Quip = Quip };
      case "SummonBats":
        int batCount = gs.Rng.Next(1, 4);
        return new SummonAction(gs.Player.Loc, "dire bat", batCount) { GameState = gs, Actor = mob, Quip = Quip };
      case "MinorSummon":
        return new MinorSummonAction(gs, mob);
      case "Web":
        return new WebAction(gs, loc);
      case "FireBolt":
        return new FireboltAction(gs, mob, loc);
      case "MagicMissile":
        return new MagicMissleAction(gs, mob, null) { DamageDie = DmgDie, NumOfDie = NumOfDice, Target = gs.Player.Loc };
      case "MirrorImage":
        return new MirrorImageAction(gs, mob, loc);
      case "ConfusingScream":
        txt = $"{mob.FullName.Capitalize()} screams!";
        return new AoEAction(gs, mob, mob.Loc, $"Confused#0#{DC}#0", DmgDie, txt);
      case "DrainTorch":
        return new DrainTorchAction(gs, mob, loc);
      case "FireBreath":
        return new BreathWeaponAction(gs, mob, DamageType.Fire, "a gout of flame", DmgDie, NumOfDice, MaxRange, new(Colours.BRIGHT_RED, Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.WHITE, Constants.FIRE_CHAR)) { Loc = loc };
      case "FrostyBreath":
        return new BreathWeaponAction(gs, mob, DamageType.Cold, "a blast of cold", DmgDie, NumOfDice, MaxRange, new(Colours.ICE_BLUE, Colours.BLUE, Colours.MYSTIC_AURA, Colours.WHITE, Constants.WIND_CHAR)) { Loc = loc };
      case "FearsomeBellow":
        txt = $"{mob.FullName.Capitalize()} bellows fearsomely!";
        return new AoEAction(gs, mob, mob.Loc, $"Frightened#0#{DC}#0", MaxRange, txt);
      case "Shriek":
        return new ShriekAction(gs, mob, MaxRange);
      case "Gulp":
        return new GulpAction(gs, mob, DC, DmgDie, NumOfDice);
      case "FlareFire":
        return new FlareAction(gs, mob, DmgDie, NumOfDice, DamageType.Fire);
      case "GetOverHere":
        return new GetOverHereAction(gs, mob, loc, DmgDie, NumOfDice);
      case "BloodDrain":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Piercing);
        return new MeleeAttackAction(gs, mob, loc) { AttackEffect = new BloodDrainTrait() };
      case "ThrowBomb":
        return new ThrowBombAction(gs, mob, loc);
      case "Whirlpool":
        return new WhirlpoolAction(gs, mob);
      case "CastConfusion":
        txt = $"{mob.FullName.Capitalize()} inflicts confusion!";
        return new ApplyAffectAction(gs, mob, loc, $"Confused#0#{DC}#0", txt);
      case "CastPoison":
        txt = $"{mob.FullName.Capitalize()} inflicts poison!";
        string poisoned = $"Poisoned#{DC}#2#0#0#10";
        return new ApplyAffectAction(gs, mob, loc, poisoned, txt);
      default:
        return new PassAction();
    }
  }
}

class NoOne : Actor
{
  static NoOne? _instance;
  public static NoOne Instance() => _instance ??= new NoOne();

  NoOne()
  {
    Name = "No One";
    Glyph = new Glyph(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, false);
  }

  public override Actor PickTarget(GameState gs) => this;
  public override Loc PickTargetLoc(GameState gamestate, int range) => Loc;
  public override Loc PickRangedTargetLoc(GameState gamestate, int range) => Loc;
  public override void TakeTurn(GameState gs) { }
  public override void CalcHP() { }
}