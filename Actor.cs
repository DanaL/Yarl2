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

// Interface for anything that will get a turn in the game. I'm not sure this
// needs to exist outside of Actor. Originally I was going to have Items and
// Traits take turns to handle things like torch fuel counting down but now
// that's handled by EndOfTurn events
interface IPerformer
{
  double Energy { get; set; }
  double Recovery { get; set; }
  
  Action TakeTurn(GameState gameState);
}

// Actor should really be an abstract class but abstract classes seemed
// to be problematic when I was trying to use the JSON serialization
// libraries
abstract class Actor : GameObj, IPerformer, IZLevel
{
  static readonly int FLYING_Z = 10;
  static readonly int DEFAULT_Z = 4;

  public Dictionary<Attribute, Stat> Stats { get; set; } = [];

  public Inventory Inventory { get; set; }

  public double Energy { get; set; } = 0.0;
  public double Recovery { get; set; } = 1.0;
  public bool RemoveFromQueue { get; set; }
  public string Appearance { get; set; } = "";

  protected IBehaviour _behaviour;
  public IBehaviour Behaviour => _behaviour;

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

  // I'm sure eventually there will be more factors that do into determining
  // how noisy an Actor's walking is. Wearing metal armour for instance
  public int GetMovementNoise()
  {
    int baseNoise = HasTrait<LightStepTrait>() ? 3 : 9;

    // If the actor is wearing a shirt that's made of non-mithril metal, it will add
    // to the noisiness. (Only shirts because I feel like a metal helmet wouldn't 
    // be especially loud)
    var armour = Inventory.Items().Where(i => i.Type == ItemType.Armour && i.Equiped);
    foreach (var piece in armour)
    {      
      ArmourTrait? armourTrait = piece.Traits.OfType<ArmourTrait>().FirstOrDefault();
      // It would actually be an error for this to be null
      if (armourTrait is not null && (armourTrait.Part == ArmourParts.Shirt))
      {
        var metal = piece.MetalType();
        if (metal != Metals.NotMetal && metal != Metals.Mithril)
        {
          baseNoise += 3;
          break;
        }
      }
    }

    return baseNoise;
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

  public Actor()
  {
    Inventory = new EmptyInventory(ID);
  }

  public virtual (int, string) ReceiveDmg(IEnumerable<(int, DamageType)> damages, int bonusDamage, GameState gs, GameObj? src, double scale)
  {
    string msg = "";

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
    foreach (var dmg in damages)
    {
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
          msg = "The attack seems ineffectual!";
        }
        else if (trait is ResistanceTrait resist && resist.Type == dmg.Item2)
        {
          d /= 2;
          msg = "The attack seems less effective!";          
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

    Stats[Attribute.HP].Curr -= total;

    if (HasTrait<DividerTrait>() && Stats[Attribute.HP].Curr > 2)
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
      int currHP = Stats[Attribute.HP].Curr;
      int maxHP = Stats[Attribute.HP].Max;
      if (this is Mob && !HasTrait<BrainlessTrait>() && currHP <= maxHP / 2)
      {
        float odds = (float)currHP / maxHP;
        if (gs.Rng.NextDouble() < odds)
        {
          Stats[Attribute.MobAttitude].SetMax(Mob.AFRAID);
          msg += $" {FullName.Capitalize()} turns to flee!";
        }
      }

      if (this is not Player)
        Stats[Attribute.MobAttitude].SetMax(Mob.AFRAID);
    }
    
    return (Stats[Attribute.HP].Curr, msg.Trim());
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
      
      gs.UIRef().AlertPlayer($"{Name.DefArticle()} divides into two!!");
      gs.ObjDb.AddNewActor(other, spot);
      gs.AddPerformer(other);
    }
  }

  public virtual void SetBehaviour(IBehaviour behaviour) => _behaviour = behaviour;

  public abstract Action TakeTurn(GameState gameState);

  public bool AbilityCheck(Attribute attr, int dc, Random rng)
  {
    int statMod = Stats.TryGetValue(attr, out var stat) ? stat.Curr : 0;
    int roll = rng.Next(20) + 1 + statMod;

    if (attr == Attribute.Strength && HasActiveTrait<RageTrait>())
      roll += rng.Next(1, 7);

    return roll >= dc;
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

    if (HasTrait<InvisibleTrait>() && !other.HasTrait<SeeInvisibleTrait>())
      return false;

    return true;
  }
}

class Mob : Actor
{
  public MoveStrategy MoveStrategy { get; set; }
  public List<ActionTrait> Actions { get; set; } = [];

  public const int  INACTIVE = 0;
  public const int INDIFFERENT = 1;
  public const int AGGRESSIVE = 2;
  public const int AFRAID = 4;

  public Mob()
  {
    _behaviour = new MonsterBehaviour();
    MoveStrategy = new DumbMoveStrategy();
  }

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

    int attitude = Stats[Attribute.MobAttitude].Curr;
    if (!(attitude == AFRAID || attitude == AGGRESSIVE)) 
    {
      Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
      Console.WriteLine($"{FullName} becomes aggressive");
    }
    
    if (heard && HasTrait<SleepingTrait>())
    {
      Console.WriteLine($"{Name} wakes up");
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

  public override Action TakeTurn(GameState gameState) => _behaviour.CalcAction(this, gameState);
}

class MonsterFactory
{
  static Dictionary<string, string> _catalog = [];

  static void LoadCatalog()
  {
    foreach (var line in File.ReadAllLines("data/monsters.txt"))
    {
      int i = line.IndexOf('|');
      string name = line[..i].Trim();
      string val = line[(i + 1)..];
      _catalog.Add(name, val);
    }
  }

  static MoveStrategy TextToMove(string txt) => txt.ToLower() switch
  {
    "door" => new DoorOpeningMoveStrategy(),
    "flying" or "floating" => new SimpleFlightMoveStrategy(),
    "wall" => new WallMoveStrategy(),
    _ => new DumbMoveStrategy()
  };

  //       0       1    2      3   4   5           6         7    8    9       10        11       
  // name, symbol, lit, unlit, AC, HP, Attack Mod, Recovery, Str, Dex, Movement, Actions, Other Traits 
  // skeleton        |z|white        |darkgrey    |12| 8|2| 1.0|12|10|Dumb|Melee#6#1#Slashing|Immunity#Confusion,Immunity#Poison,ResistPiercing
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

    var mv = TextToMove(fields[9]);
    var m = new Mob()
    {
      Name = name,
      Glyph = glyph,
      Recovery = double.Parse(fields[6]),
      MoveStrategy = mv
    };

    int hp = int.Parse(fields[4]);
    m.Stats.Add(Attribute.HP, new Stat(hp));
    int attBonus = int.Parse(fields[5]);
    m.Stats.Add(Attribute.AttackBonus, new Stat(attBonus));
    int ac = int.Parse(fields[3]);
    m.Stats.Add(Attribute.AC, new Stat(ac));
    int str = Util.StatRollToMod(int.Parse(fields[7]));
    m.Stats.Add(Attribute.Strength, new Stat(str));
    int dex = Util.StatRollToMod(int.Parse(fields[8]));
    m.Stats.Add(Attribute.Dexterity, new Stat(dex));
    int attitude = rng.NextDouble() <= 0.8 ? Mob.INDIFFERENT : Mob.AGGRESSIVE;
    m.Stats.Add(Attribute.MobAttitude, new Stat(attitude));

    if (fields[10] != "")
    {
      foreach (var actionTxt in fields[10].Split(','))
      {
        m.Actions.Add((ActionTrait)TraitFactory.FromText(actionTxt, m));
      }
    }

    if (!string.IsNullOrEmpty(fields[11]))
    {
      foreach (var traitTxt in fields[11].Split(','))
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