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
  public string Appearance { get; set; } = "";

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

  public override int LightRadius()
  {
    int radius = base.LightRadius();

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

  public override string FullName => HasTrait<NamedTrait>() ? Name.Capitalize() : Name.DefArticle();

  public virtual int TotalMissileAttackModifier(Item weapon) => 0;
  public virtual int TotalSpellAttackModifier() => 0;
  public virtual int AC => 10;
  public virtual List<Damage> MeleeDamage() => [];
  public virtual void HearNoise(int volume, int sourceRow, int sourceColumn, GameState gs) { }
  public virtual int SpellDC => 12;

  public void QueueAction(Action action) => ActionQ.Enqueue(action);

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
    var toRemove = Traits.Where(t => t is InPitTrait || t is GrappledTrait || t is SwallowedTrait).ToList();
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
  
  public bool AbleToMove() 
  {
    foreach (var t in Traits)
    {
      if (t is ParalyzedTrait)
        return false;
      if (t is GrappledTrait)
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
      // Soource is the weapon/actual source of damage, not the moral agent
      // responsible for causing the damage. Perhaps I should include a ref
      // to the attacker, because the monster maybe shouldn't become aggressitve
      // if the attack doesn't come from the player?
      if (attitude.Curr != Mob.AFRAID)
        attitude.SetMax(Mob.AGGRESSIVE);
    }

    // If we have allies, let them know we've turned hostile
    if (Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait allies)
    {
      foreach (ulong id in allies.IDs)
      {
        if (gs.ObjDb.GetObj(id) is Mob ally && gs.CanSeeLoc(ally, Loc, 6))
        {
          ally.Traits.RemoveAll(t => t is SleepingTrait);
        }
      }
    }

    // If I pile up a bunch of resistances, I'll probably want something less brain-dead here
    int total = 0;
    bool fireDamage = false;
    foreach (var dmg in damages)
    {
      if (dmg.Item2 == DamageType.Fire)
        fireDamage = true;

      int d = dmg.Item1;
      if (dmg.Item2 == DamageType.Blunt && HasActiveTrait<ResistBluntTrait>())
        d /= 2;
      else if (dmg.Item2 == DamageType.Piercing && HasActiveTrait<ResistPiercingTrait>())
        d /= 2;
      else if (dmg.Item2 == DamageType.Slashing && HasActiveTrait<ResistSlashingTrait>())
        d /= 2;

      foreach (var trait in Traits)
      {
        if (trait is ImmunityTrait immunity && immunity.Type == dmg.Item2) 
        {
          d = 0;
          bonusDamage = 0;
          msg = "It is seems ineffectual!";
        }
        else if (trait is ResistanceTrait resist && resist.Type == dmg.Item2)
        {
          d /= 2;
          msg = "It seems less effective!";          
        }
      }
      
      if (d > 0)
        total += d;
    }    
    total += bonusDamage;
    total = (int)(total * scale);

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

    AuraOfProtectionTrait? aura = Traits.OfType<AuraOfProtectionTrait>().FirstOrDefault();
    if (aura is not null)
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
    if (Stats.TryGetValue(Attribute.MobAttitude, out attitude) && attitude.Curr != Mob.AFRAID)
    {
      int maxHP = Stats[Attribute.HP].Max;
      if (this is Mob && !HasTrait<BrainlessTrait>() && currHP.Curr <= maxHP / 2 && currHP.Curr > 0)
      {
        float odds = (float)currHP.Curr / maxHP;
        if (gs.Rng.NextDouble() < odds)
        {
          Stats[Attribute.MobAttitude].SetMax(Mob.AFRAID);          
          msg += VisibleTo(gs.Player) ? $" {FullName.Capitalize()}" : "A monster";
          msg += " turns to flee!";
          
          if (HasTrait<FullBellyTrait>())
            EmptyBelly(gs);

          if (Traits.OfType<GrapplingTrait>().FirstOrDefault() is GrapplingTrait gt)
            ClearGrapple(gt, gs);
        }
      }
    }
    
    return (Stats[Attribute.HP].Curr, msg.Trim(), total);
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
      
      gs.UIRef().AlertPlayer($"{Name.DefArticle().Capitalize()} divides into two!!");
      gs.ObjDb.AddNewActor(other, spot);      
    }
  }

  public virtual void SetBehaviour(IBehaviour behaviour) => _behaviour = behaviour;

  public abstract Actor PickTarget(GameState gs);
  public abstract Loc PickTargetLoc(GameState gamestate);
  public abstract Loc PickRangedTargetLoc(GameState gamestate);
  public abstract void TakeTurn(GameState gs);

  public bool AbilityCheck(Attribute attr, int dc, Random rng)
  {
    int statMod = Stats.TryGetValue(attr, out var stat) ? stat.Curr : 0;
    int roll = rng.Next(20) + 1 + statMod;

    if (attr == Attribute.Strength && HasActiveTrait<RageTrait>())
      roll += rng.Next(1, 7);

    return roll >= dc;
  }

  public char AddToInventory(Item item, GameState gs)
  {
    char slot = Inventory.Add(item, ID);
    if (slot == '\0')
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
    if (gameState.CanSeeLoc(this, other.Loc, 6))
    {
      Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
      
      return $"{FullName.Capitalize()} gets angry!";
    }

    return "";
  }

  public bool VisibleTo(Actor other)
  {
    if (ID == other.ID)
      return true;

    bool seeInvisible = false;
    foreach (Trait t in other.Traits)
    {
      if (t is SeeInvisibleTrait || t is TelepathyTrait)
      {
        seeInvisible = true;
        break;  
      }
    }
    if (HasTrait<InvisibleTrait>() && !seeInvisible)
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

class Mob : Actor
{
  public List<Power> Powers { get; set; } = []; // this will supersede the Actions list
  public Dictionary<string, ulong> LastPowerUse = []; // I'll probably want to eventually serialize these

  BehaviourNode? CurrPlan { get; set; } = null;
  public void ClearPlan() => CurrPlan = null;

  public const int  INACTIVE = 0;
  public const int INDIFFERENT = 1;
  public const int AGGRESSIVE = 2;
  public const int AFRAID = 4;

  public Mob() => _behaviour = new MonsterBehaviour();

  public Damage? Dmg { get; set; }
  public override List<Damage> MeleeDamage()
  {
    List<Damage> dmgs = [Dmg ?? new Damage(4, 1, DamageType.Blunt)];

    return dmgs;
  }

  public override void HearNoise(int volume, int sourceRow, int sourceColumn, GameState gs)
  {
    int threshold = volume - Util.Distance(sourceRow, sourceColumn, Loc.Row, Loc.Col);
    bool heard = gs.Rng.Next(11) <= threshold;

    if (Stats.TryGetValue(Attribute.MobAttitude, out var attitude) 
          && !(attitude.Curr == AFRAID || attitude.Curr == AGGRESSIVE))
    {
        Stats[Attribute.MobAttitude].SetMax(AGGRESSIVE);        
    }
        
    if (heard && HasTrait<SleepingTrait>())
    {
      if (gs.LastPlayerFoV.Contains(Loc))
        gs.UIRef().AlertPlayer($"{FullName.Capitalize()} wakes up.");
      Traits.RemoveAll(t => t is SleepingTrait);
    }
  }

  public override int TotalMissileAttackModifier(Item weapon)
  {
    return Stats.TryGetValue(Attribute.AttackBonus, out var ab) ? ab.Curr : 0;
  }

  public override int TotalSpellAttackModifier()
  {
    return Stats.TryGetValue(Attribute.AttackBonus, out var ab) ? ab.Curr : 0;
  }

  public override int AC => Stats.TryGetValue(Attribute.AC, out var ac) ? ac.Curr : base.AC;

  public void ExecuteAction(Action action)
  {
    double result = action.Execute();
    Energy -= CalcEnergyUsed(result);
  }

  public string GetBark(GameState gs) => _behaviour.GetBark(this, gs);

  public override void TakeTurn(GameState gs)
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

  // At the moment, monsters will pick the player, but I'm working toward
  // changing thawt
  public override Actor PickTarget(GameState gs) 
  {
    if (gs.Player.HasTrait<NondescriptTrait>())
      return NoOne.Instance();

    return gs.Player;
  }

  public override Loc PickTargetLoc(GameState gameState)
  {
    if (gameState.Player.HasTrait<NondescriptTrait>())
      return Loc.Nowhere;

    return HasTrait<ConfusedTrait>() ? Util.RandomAdjLoc(Loc, gameState) : gameState.Player.Loc;
  }

  // I suspect eventually these will diverge
  public override Loc PickRangedTargetLoc(GameState gameState) => PickTargetLoc(gameState);
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

  public static Actor Get(string name, GameObjectDB objDb, Random rng)
  {
    if (_catalog.Count == 0)
      LoadCatalog();

    if (!_catalog.TryGetValue(name, out string? template))
      throw new UnknownMonsterException(name);

    var fields = template.Split('|').Select(f => f.Trim()).ToArray();

    char ch = fields[0].Length == 0 ?  ' ' : fields[0][0];
    var glyph = new Glyph(ch, Colours.TextToColour(fields[1]),
                                Colours.TextToColour(fields[2]), Colours.BLACK, Colours.BLACK);

    var m = new Mob()
    {
      Name = name,
      Glyph = glyph,
      Recovery = Util.ToDouble(fields[6])
    };
    m.Traits.Add(new BehaviourTreeTrait() { Plan = "MonsterPlan" });

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
      foreach (var powerTxt in fields[9].Split(','))
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
      foreach (var traitTxt in fields[10].Split(','))
      {
        var trait = TraitFactory.FromText(traitTxt, m);
        m.Traits.Add(trait);

        if (trait is IGameEventListener listener)
        {
          objDb.EndOfRoundListeners.Add(listener);                     
        }
      }
    }

    // Yes, I will write code just to insert a joke/Simpsons reference
    // into the game
    if (name == "zombie" && rng.Next(100) == 0)
      m.Traits.Add(new DeathMessageTrait() { Message = "Is this the end of Zombie Shakespeare?" });
    
    return m;
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
      case "MeleeNecrotic":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Necrotic);
        return new MeleeAttackAction(gs, mob, loc);
      case "MissilePiercing":
        mob.Dmg = new Damage(DmgDie, NumOfDice, DamageType.Necrotic);        
        var arrow = ItemFactory.Get(ItemNames.ARROW, gs.ObjDb);
        return new MissileAttackAction(gs, mob, loc, arrow);
      case "RumBreath":
        return new RumBreathAction(gs, mob, loc, MaxRange);
      case "Nudity":
        return new InduceNudityAction(gs, mob);
      case "FogCloud":
        return new FogCloudAction(gs, mob);
      case "Blink":
        return new BlinkAction(gs, mob);
      case "SummonKobold":
        return new SummonAction(mob.Loc, "kobold", 1)
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

        return new SummonAction(mob.Loc, summons, 1) { GameState = gs, Actor = mob };
      case "Web":
        return new WebAction(gs, loc);
      case "FireBolt":
        return new FireboltAction(gs, mob, loc);
      case "MirrorImage":
        return new MirrorImageAction(gs, mob, loc);
      case "ConfusingScream":
        txt = $"{mob.FullName.Capitalize()} screams!";
        return new AoEAction(gs, mob, mob.Loc, $"Confused#0#{DC}#0", DmgDie, txt);
      case "DrainTorch":
        return new DrainTorchAction(gs, mob, loc);
      case "Entangle":
        return new EntangleAction(gs, mob);
      case "FireBreath":
        return new FireBreathAction(gs, mob, loc, MaxRange, DmgDie, NumOfDice);
      case "FearsomeBellow":
        txt = $"{mob.FullName.Capitalize()} bellows fearsomely!";
        return new AoEAction(gs, mob, mob.Loc, $"Frightened#0#{DC}#0", MaxRange, txt);
      case "Shriek":
        return new ShriekAction(gs, mob, MaxRange);
      case "Gulp":
        return new GulpAction(gs, mob, DC, DmgDie, NumOfDice);      
      default:
        return new PassAction();
    }    
  }
}

class NoOne : Actor
{
    private static NoOne? _instance;
    public static NoOne Instance() => _instance ??= new NoOne();

    private NoOne()
    {
        Name = "No One";
        Glyph = new Glyph(' ', Colours.BLACK, Colours.BLACK, Colours.BLACK, Colours.BLACK);
    }

    public override Actor PickTarget(GameState gs) => this;
    public override Loc PickTargetLoc(GameState gamestate) => Loc;
    public override Loc PickRangedTargetLoc(GameState gamestate) => Loc;
    public override void TakeTurn(GameState gs) { }
}