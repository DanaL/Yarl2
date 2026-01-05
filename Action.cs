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

using System.Runtime.Intrinsics.X86;

namespace Yarl2;

abstract class Action
{
  public Actor? Actor { get; set; }
  public GameState? GameState { get; set; }
  public string Quip { get; set; } = "";
  public int QuipDuration { get; set; } = 2500;

  public Action() { }
  public Action(GameState gs, Actor actor)
  {
    Actor = actor;
    GameState = gs;
  }

  public virtual double Execute()
  {
    if (!string.IsNullOrEmpty(Quip) && Actor is not null && GameState is not null)
    {
      BarkAnimation bark = new(GameState, QuipDuration, Actor, Quip);
      GameState.UIRef().RegisterAnimation(bark);      
    }

    return 0.0;
  }

  public virtual void ReceiveUIResult(UIResult result) { }
}

class MeleeAttackAction(GameState gs, Actor actor, Loc target) : Action(gs, actor)
{
  public Trait? AttackEffect { get; set; } = null;

  Loc Target { get; set; } = target;
  
  public override double Execute()
  {
    base.Execute();

    if (Actor!.IsDisguised())
    {
      var disguise = Actor.Traits.OfType<DisguiseTrait>().First();
      GameState!.UIRef().AlertPlayer($"Wait! That {disguise.DisguiseForm} is actually {Actor.Name.IndefArticle()}!", GameState, Actor.Loc);
      disguise.Disguised = false;
      Actor.Glyph = disguise.TrueForm;
    }

    double result;
    if (GameState!.ObjDb.Occupant(Target) is Actor target)
    {
      result = Battle.MeleeAttack(Actor!, target, GameState, AttackEffect);
    }
    else
    {
      GameState.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "swing")} wildly!", GameState, Actor.Loc, Actor);      
      result = 1.0;
    }

    return result;
  }
}

class GulpAction(GameState gs, Actor actor, int dc, int dmgDie, int numOfDice) : Action(gs, actor)
{
  int DC { get; set; } = dc;
  int AcidDie { get; set; } = dmgDie;
  int AcidDice { get; set; } = numOfDice;

  public override double Execute()
  {
    base.Execute();

    UserInterface ui = GameState!.UIRef();
    Loc targetLoc = Actor!.PickTargetLoc(GameState!, 1);
    if (GameState.ObjDb.Occupant(targetLoc) is not Actor victim)
    {
      GameState.UIRef().AlertPlayer($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "lunges")} at empty air!", GameState, Actor.Loc, Actor);
      return 1.0;
    }

    string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "bite")} {victim.FullName}!";
    ui.AlertPlayer(s);
    
    if (!victim.AbilityCheck(Attribute.Dexterity, DC, GameState.Rng))
    {
      SwallowedTrait st = new()
      {
        VictimID = victim.ID,
        SwallowerID = Actor.ID,
        Origin = victim.Loc
      };
      GameState.RegisterForEvent(GameEventType.Death, st, Actor.ID);
      victim.Traits.Add(st);

      FullBellyTrait fbt = new()
      {
        VictimID = victim.ID,
        AcidDie = AcidDie,
        AcidDice = AcidDice
      };
      Actor.Traits.Add(fbt);
      GameState.RegisterForEvent(GameEventType.EndOfRound, fbt);

      // This is where the player will 'enter' the pocket dimension representing
      // the interior of the monster's belly.
      Loc entry;
      Dungeon belly;
      if (Actor.Traits.OfType<PocketDimensionTrait>().FirstOrDefault() is PocketDimensionTrait dim)
      {
        entry = dim.Entry;
        belly = GameState.Campaign.Dungeons[dim.ID];  
      }
      else 
      {
        string bellyType = Actor.Traits.OfType<StrTrait>().FirstOrDefault(t => t.Name == "Belly")?.Value ?? "Small";

        (entry, belly) = bellyType switch
        {
          "Whale" => (entry, belly) = PocketDimension.WhaleBelly(Actor, GameState),
          _ => (entry, belly) = PocketDimension.MonsterBelly(Actor, GameState)
        };

        Actor.Traits.Add(new PocketDimensionTrait() { ID = belly.ID, Entry = entry });
      }

      Loc start = victim.Loc;
      GameState.ActorEntersLevel(victim, belly.ID, 0);      
      GameState.ResolveActorMove(victim, start, entry);
      victim.Loc = entry;

      GameState.FlushPerformers();
      GameState.PrepareFieldOfView();

      if (belly.ArrivalMessage != "") 
      {
        ui.AlertPlayer(belly.ArrivalMessage);
      }
    }

    return 1.0;
  }
}

class TransFormIntoBatsAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override double Execute()
  {
    base.Execute();

    List<Loc> batLocs = [];
    for (int r = Actor!.Loc.Row - 2; r <= Actor.Loc.Row + 2; r++)
    {
      for (int c = Actor.Loc.Col - 2; c <= Actor.Loc.Col + 2; c++)
      {
        if (!GameState!.CurrentMap.InBounds(r, c))
          continue;
        Loc loc = Actor.Loc with { Row = r, Col = c };
        if (GameState.CurrentMap.TileAt(r, c).PassableByFlight() && !GameState.ObjDb.Occupied(loc))
          batLocs.Add(loc);
      }
    }
    batLocs.Shuffle(GameState!.Rng);

    List<Actor> bats = [];
    int numOfBats = GameState.Rng.Next(4, 5);
    for (int j = 0; j < int.Min(numOfBats, batLocs.Count); j++)
    {
      Actor bat = MonsterFactory.Get("dire bat", GameState.ObjDb, GameState.Rng);
      GameState.ObjDb.AddNewActor(bat, batLocs[j]);
      bats.Add(bat);
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, batLocs[j], Colours.DARK_GREY, Colours.BLACK, '*'));
    }

    string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "transform")} into a swarm of bats!";
    GameState!.UIRef().AlertPlayer(s, GameState, Actor.Loc, Actor);

    GameState.ObjDb.StashActor(Actor);

    ulong expiry = GameState.Turn + (ulong)GameState.Rng.Next(5, 8);
    TransformedTrait transformed = new() 
    { 
      OriginalId = Actor.ID, OwnerID = Actor.ID, ExpiresOn = expiry,
      TransformedIds = [ ..bats.Select(b => b.ID)]
    };
    transformed.Apply(Actor, GameState);
    
    foreach (Actor bat in bats)
    {
      transformed = new() { OriginalId = Actor.ID, OwnerID = bat.ID, TransformedIds = [.. bats.Select(b => b.ID)], ExpiresOn = expiry };
      transformed.Apply(bat, GameState);
    }

    return 1.0;
  }
}

// This is a different class from MissileAttackAction because it will take the result the 
// aim selection. It also handles the animation and following the path of the arrow
class ArrowShotAction(GameState gs, Actor actor, Item? bow, Item ammo, int attackBonus) : TargetedAction(gs, actor)
{
  readonly Item _ammo = ammo;
  readonly int _attackBonus = attackBonus;

  public override double Execute()
  {
    base.Execute();
    var trajectory = Trajectory(Actor!.Loc, false);
    List<Loc> pts = [];
    bool creatureTargeted = false;
    bool targetHit = false;
    for (int j = 0; j < trajectory.Count; j++)
    {
      var pt = trajectory[j];
      Tile tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);
        bool attackSuccessful = Battle.MissileAttack(Actor!, occ, GameState, _ammo, _attackBonus, new ArrowAnimation(GameState!, pts, _ammo.Glyph.Lit));
        creatureTargeted = true;
        
        if (attackSuccessful)
        {
          pts = [];
          break;
        }
      }
      else if (tile.Passable() || tile.PassableByFlight())
      {
        pts.Add(pt);
      }
      else
      {
        break;
      }
    }

    if (pts.Count > 0)
    {
      var anim = new ArrowAnimation(GameState!, pts, _ammo.Glyph.Lit);
      GameState!.UIRef().PlayAnimation(anim, GameState);
    }
   
    if (creatureTargeted && !targetHit && Actor is Player player && bow is Item && bow.HasTrait<BowTrait>())
    {
      player.ExerciseStat(Attribute.BowUse);
    }

    return 1.0;
  }
}

class MissileAttackAction(GameState gs, Actor actor, Loc loc, Item ammo) : Action(gs, actor)
{
  Loc _loc = loc;
  readonly Item _ammo = ammo;
  
  public override double Execute()
  {
    double result = 0.0;
    ArrowAnimation arrowAnim = new(GameState!, Util.Trajectory(Actor!.Loc, _loc), _ammo.Glyph.Lit);
    GameState!.UIRef().RegisterAnimation(arrowAnim);

    if (GameState!.ObjDb.Occupant(_loc) is Actor target)
    {
      if (Actor is not Player)
      {
        if (GameState.LastPlayerFoV.ContainsKey(Actor.Loc)) 
        {
          string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} shoots at ";
          s+= $"{MsgFactory.CalcName(target, GameState.Player)}!";
          GameState.UIRef().AlertPlayer(s);
        }
        else
        {
          GameState.UIRef().AlertPlayer("Twang!");
        }
      }

      result = 1.0;
      Battle.MissileAttack(Actor!, target, GameState, _ammo, 0, null);
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result) => _loc = ((LocUIResult)result).Loc;
}

class GetOverHereAction(GameState gs, Actor actor, Loc loc, int dmgDie, int numOfDie) : Action(gs, actor)
{
  Loc Loc { get; set; } = loc;
  int DmgDie { get; set; } = dmgDie;
  int NumOfDie { get; set; } = numOfDie;

  public override double Execute()
  {
    base.Execute();

    List<Loc> sqs = [.. Util.LerpLine(Actor!.Loc.Row, Actor.Loc.Col, Loc.Row, Loc.Col)                            
                            .Select(l => new Loc(Loc.DungeonID, Loc.Level, l.Item1, l.Item2))];
    sqs = [.. sqs.Skip(1)];
    sqs = [.. sqs.Take(sqs.Count - 1)];

    GameState!.UIRef().RegisterAnimation(new SqAnimation(GameState, sqs[0], Colours.GREY, Colours.BLACK, Util.ArrowChar(sqs[0], Loc)));
    foreach (Loc seg in sqs.Skip(1))
    {
      char ch = '―';
      if (int.Abs(Actor.Loc.Row - Loc.Row) > int.Abs(Actor.Loc.Col - Loc.Col))
        ch = '|';
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState!, seg, Colours.GREY, Colours.BLACK, ch));
    }
    
    if (GameState.ObjDb.Occupant(Loc) is Actor target)
    {
      Loc landingSpot = Loc;
      for (int j = sqs.Count - 1; j >= 0; j--)
      {
        if (GameState.ObjDb.Occupied(sqs[j]))
          break;
        landingSpot = sqs[j];
      }

      GameState.UIRef().AlertPlayer($"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} speared by {Actor.FullName}!");

      if (landingSpot != Loc)
      {
        GameState.UIRef().AlertPlayer($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "drag")} {target.FullName} toward them!");
        GameState.ResolveActorMove(target, target.Loc, landingSpot);

        int dmg = 0;
        for (int j = 0; j < NumOfDie; j++)
          dmg += GameState.Rng.Next(1, DmgDie) + 1;
        List<(int, DamageType)> totalDmg = [(dmg, DamageType.Piercing)];
        var (hpLeft, dmgMsg, _) = GameState.Player.ReceiveDmg(totalDmg, 0, GameState, null, 1.0);
        GameState.UIRef().AlertPlayer(dmgMsg, GameState, landingSpot, Actor);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(target, "being speared", Actor);
        }     
      }
    }

    return 1.0;
  }
}

class AssumeDisguiseAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override double Execute()
  {
    base.Execute();

    string disguiseForm;
    Glyph glyph;
    switch (GameState!.Rng.Next(8))
    {
      case 0:
        disguiseForm = "chainmail";        
        glyph = new Glyph(']', Colours.LIGHT_GREY, Colours.GREY, Colours.BLACK, false);
        break;
      case 1:
        disguiseForm = "torch";
        glyph = new Glyph('(', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false);
        break;
      case 2:
        disguiseForm = "potion of healing";
        glyph = new Glyph('!', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false);
        break;
      case 3:
        disguiseForm = "scroll of blink";
        glyph = new Glyph('?', Colours.WHITE, Colours.GREY, Colours.BLACK, false);
        break;
      case 4:
        disguiseForm = "scroll of magic mapping";
        glyph = new Glyph('?', Colours.WHITE, Colours.GREY, Colours.BLACK, false);
        break;
      case 5:
        disguiseForm = "flask of booze";
        glyph = new Glyph('!', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false);
        break;
      case 6:
        disguiseForm = "claymore";
        glyph = new Glyph(')', Colours.WHITE, Colours.LIGHT_GREY, Colours.BLACK, false);
        break;
      default:
        disguiseForm = "zorkmid";
        glyph = new Glyph('$', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false);
        break;
    }

    DisguiseTrait disguise = Actor!.Traits.OfType<DisguiseTrait>().First();
    disguise.Disguised = true;
    disguise.DisguiseForm = disguiseForm;
    disguise.Disguise = glyph;
    Actor.Glyph = glyph;

    return 1.0;
  }
}

class ApplyTraitAction : Action
{
  List<TemporaryTrait> Traits { get; set; }

  public ApplyTraitAction(GameState gs, Actor actor, List<TemporaryTrait> traits) : base(gs, actor) => Traits = traits;
  public ApplyTraitAction(GameState gs, Actor actor, TemporaryTrait trait) : base(gs, actor) => Traits = [trait];

  public override double Execute()
  {
    UserInterface ui = GameState!.UIRef();

    if (Actor is not null)
    {
      foreach (var t in Traits)
      {
        List<string> msgs = t.Apply(Actor, GameState!);
        foreach (string s in msgs)
          ui.AlertPlayer(s);
      }
    }
    
    return 1.0;
  }
}

class ShriekAction(GameState gs, Actor actor, int radius) : Action(gs, actor)
{
  int Radius { get; set; } = radius;

  public override double Execute()
  {
    base.Execute();

    string msg;
    if (GameState!.LastPlayerFoV.ContainsKey(Actor!.Loc))
      msg = $"{Actor.FullName.Capitalize()} lets out a piercing shriek!";
    else
      msg = "You hear a piercing shriek!";
    GameState.UIRef().AlertPlayer(msg);

    for (int r = Actor.Loc.Row - Radius; r < Actor.Loc.Row + Radius; r++)
    {
      for (int c = Actor.Loc.Col - Radius; c < Actor.Loc.Col + Radius; c++)
      {
        if (!GameState.CurrentMap.InBounds(r, c))
          continue;
        Loc loc = Actor.Loc with { Row = r, Col = c };
        if (GameState.ObjDb.Occupant(loc) is Mob mob && mob.Stats.ContainsKey(Attribute.MobAttitude))
        {
          mob.Traits = [.. mob.Traits.Where(t => t is not SleepingTrait)];
          mob.SetAttitude(Mob.AGGRESSIVE);
        }
      }
    }

    return 1.0;
  }
} 

class AoEAction(GameState gs, Actor actor, Loc target, string effectTemplate, int radius, string txt) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  string EffectTemplate { get; set; } = effectTemplate;
  public int Radius { get; set; } = radius;
  string EffectText { get; set; } = txt;

  public override double Execute()
  {
    base.Execute();
    
    GameState!.UIRef().AlertPlayer(EffectText);
    
    var affected = GameState.Flood(Target, Radius);
    foreach (var loc in affected)
    {
      // Ugh at the moment I can't handle things like a fireball
      // hitting an area and damaging items via this :/
      if (GameState.ObjDb.Occupant(loc) is Actor occ)
      {
        var effect = (TemporaryTrait) TraitFactory.FromText(EffectTemplate, occ);
        List<string> msgs = effect.Apply(occ, GameState);
        string s = string.Join(' ', msgs).Trim();

        GameState.UIRef().AlertPlayer(s, GameState, loc, occ);
      }
    }

    return 1.0;
  }
}

class RumBreathAction(GameState gs, Actor actor, Loc target, int range) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  int Range { get; set; } = range;

  public override double Execute()
  {
    base.Execute();

    if (GameState!.LastPlayerFoV.ContainsKey(Actor!.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "spew")} a gout of alcohol!";
      GameState!.UIRef().AlertPlayer(s);
    }

    // Actor targets a specific loc, but the cone of the breath weapon extends
    // its full range.
    var (fullR, fullC) = Util.ExtendLine(Actor.Loc.Row, Actor.Loc.Col, Target.Row, Target.Col, Range);
    Loc actualTarget = Target with { Row = fullR, Col = fullC };
    List<Loc> affected = ConeCalculator.Affected(Range, Actor.Loc, actualTarget, GameState.CurrentMap, GameState.ObjDb, []);
    affected.Insert(0, Actor.Loc);
    ExplosionAnimation explosion = new(GameState!)
    {
      MainColour = Colours.LIGHT_BROWN,
      AltColour1 = Colours.BROWN,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = Actor.Loc,
      Sqs = [ ..affected ],
      Ch = '#'
    };
    GameState.UIRef().PlayAnimation(explosion, GameState);

    affected.Remove(Actor.Loc);

    UserInterface ui = GameState.UIRef();
    foreach (var pt in affected)
    {
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        foreach (string s in Battle.HandleTipsy(victim, GameState))
          ui.AlertPlayer(s);
      }
    }

    return 1.0;
  }
}

// I'm sure as I add more breath weapons I'll make this more generic, or extract
// a subclass
class FireBreathAction(GameState gs, Actor actor, Loc target, int range, int dmgDie, int dmgDice) : Action(gs, actor)
{
  Loc Target { get; set; } = target;
  int Range { get; set; } = range;
  int DmgDie { get; set; } = dmgDie;
  int DmgDice { get; set; } = dmgDice;

  public override double Execute()
  {
    UserInterface ui = GameState!.UIRef();
    base.Execute();

    if (GameState.LastPlayerFoV.ContainsKey(Actor!.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "breath")} a gout of flame!";
      ui.AlertPlayer(s);
    }

    // Actor targets a specific loc, but the cone of the breath weapon extends
    // its full range.
    var (fullR, fullC) = Util.ExtendLine(Actor.Loc.Row, Actor.Loc.Col, Target.Row, Target.Col, Range);
    Loc actualTarget = Target with { Row = fullR, Col = fullC };
    List<Loc> affected = ConeCalculator.Affected(Range, Actor.Loc, actualTarget, GameState.CurrentMap, GameState.ObjDb, []);
    affected.Insert(0, Actor.Loc);
    var explosion = new ExplosionAnimation(GameState!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = Actor.Loc,
      Sqs = [ ..affected ],
      Ch = '\u22CF'
    };
    ui.PlayAnimation(explosion, GameState);

    affected.Remove(Actor.Loc);

    foreach (var pt in affected)
    {
      Effects.ApplyDamageEffectToLoc(pt, DamageType.Fire, GameState);
    }

    int total = 0;
    for (int j = 0; j < DmgDice; j++)
      total += GameState.Rng.Next(DmgDie) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];    
    foreach (var pt in affected)
    {
      Effects.ApplyDamageEffectToLoc(pt, DamageType.Fire, GameState);
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!";
        ui.AlertPlayer(s, GameState, victim.Loc, victim);

        var (hpLeft, dmgMsg, _) = victim.ReceiveDmg(dmg, 0, GameState, null, 1.0);
        if (dmgMsg != "")
          ui.AlertPlayer(dmgMsg);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "fiery breath", null);
        }        
      }
    }

    return 1.0;
  }
}

class BashAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public Loc Target { get; set; }

  static bool CheckForInjury(TileType type) => type switch
  {
    TileType.ClosedDoor => true,
    TileType.LockedDoor => true,
    TileType.WoodWall => true,
    TileType.PermWall => true,
    TileType.StoneWall => true,
    TileType.DungeonWall => true,
    TileType.GreenTree => true,
    TileType.OrangeTree => true,
    TileType.YellowTree => true,
    TileType.RedTree => true,
    _ => false
  };

  public override double Execute()
  {
    base.Execute();
    var gs = GameState!;
    UserInterface ui = gs.UIRef();

    // I'll probably want to do a knock-back ki nd of thing?
    if (gs.ObjDb.Occupied(Target))
    {
      ui.AlertPlayer("There's someone in your way!");
      return 0.0;
    }

    Tile tile = gs.TileAt(Target);
    if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor)
    {
      ui.AlertPlayer("Bam!");

      int dc = 14 + gs.CurrLevel/4;
      int roll = gs.Rng.Next(1, 21) + Actor!.Stats[Attribute.Strength].Curr;

      if (roll >= dc)
      {
        ui.AlertPlayer("You smash open the door!");
        gs.CurrentMap.SetTile(Target.Row, Target.Col, TileFactory.Get(TileType.BrokenDoor));
      }
      else
      {
        ui.AlertPlayer("The door holds firm!");
      }

      gs.Noise(Target.Row, Target.Col, 5);
    }

    // I should impose a small chance of penalty/injury so that spamming
    // bashing is a little risky
    if (CheckForInjury(tile.Type) && gs.Rng.Next(4) == 0) 
    {
      LameTrait lame = new()
      {
        OwnerID = Actor!.ID,
        ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(100, 151)
      };

      List<string> msgs = lame.Apply(Actor!, gs);
      if (msgs.Count > 0)
      {
        foreach (string s in msgs)
          ui.AlertPlayer(s);
      }        
      else
      {        
        ui.AlertPlayer($"You injure your leg kicking {Tile.TileDesc(tile.Type)}!");
      }
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var dirResult = (DirectionUIResult)result;
    var actorLoc = Actor!.Loc;
    Target = actorLoc with { Row = actorLoc.Row + dirResult.Row, 
                            Col = actorLoc.Col + dirResult.Col };
  }
}

class DisarmAction(GameState gs, Actor actor, Loc loc) : Action(gs, actor)
{
  Loc Origin { get; set; } = loc;

  public override double Execute()
  {
    base.Execute();
    UserInterface ui = GameState!.UIRef();

    Map map = GameState!.CurrentMap;
    int trapCount = 0;
    foreach (Loc loc in Util.LocsInRadius(Origin, 3, map.Height, map.Width))
    {
      Tile tile = GameState.TileAt(loc);
      if (tile.IsTrap())
      {
        map.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
        ++trapCount;
        if (GameState.LastPlayerFoV.ContainsKey(loc))
        {
          SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.FADED_PURPLE, '^');
          ui.RegisterAnimation(anim);
          ui.AlertPlayer("A trap is destroyed!");
        }
        else
        {
          ui.AlertPlayer("You hear crunching and tinkling of machinery being destroyed.");
        }
      }
    }

    if (trapCount == 0)
      ui.AlertPlayer("The spell doesn't seem to do anything at all.");
    
    return 1.0;
  }
}

// Action for when an actor jumps into a river or chasm (and eventually lava?)
class DiveAction(GameState gs, Actor actor, Loc loc, bool voluntary) : Action(gs, actor)
{
  Loc Loc { get; set; } = loc;
  bool Voluntary { get; set; } = voluntary;

  void PlungeIntoWater(Actor actor, GameState gs)
  {
    UserInterface ui = gs.UIRef();
    if (actor is Player && Voluntary)
      ui.AlertPlayer("You plunge into the water!");
    else if (actor is Player)
      ui.AlertPlayer("You stumble and fall into some water!");
    else if (gs.LastPlayerFoV.ContainsKey(Loc))
      ui.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "plunge")} into the water!");
    else
      ui.AlertPlayer("You hear a splash!");
    
    gs.FallIntoWater(actor, Loc);
  }

  void PlungeIntoChasm(Actor actor, GameState gs)
  {
    UserInterface ui = gs.UIRef();
    if (actor is Player && Voluntary)
      ui.AlertPlayer("You leap into the darkness!");
    else if (actor is Player)
      ui.AlertPlayer("There's no floor beneath your feet!");
    else if (gs.LastPlayerFoV.ContainsKey(Loc))
      ui.AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} into the darkness!");
    
    var landingSpot = new Loc(Loc.DungeonID, Loc.Level + 1, Loc.Row, Loc.Col);

    gs.FallToNextLevel(actor, landingSpot);    
  }

  public override double Execute()
  {
    base.Execute();

    var tile = GameState!.TileAt(Loc);
    if (tile.Type == TileType.DeepWater)
    {
      PlungeIntoWater(Actor!, GameState);
    }
    else if (tile.Type == TileType.Chasm)
    {
      PlungeIntoChasm(Actor!, GameState);
    }

    return 1.0;
  }
}

abstract class PortalAction : Action
{  
  public PortalAction(GameState gameState) => GameState = gameState;

  protected void UsePortal(Portal portal)
  {
    Player player = GameState!.Player;
    Loc start = player.Loc;        
    var (dungeon, level, _, _) = portal.Destination;
    
    bool trip = level > start.Level && GameState.Player.HasTrait<TipsyTrait>() && GameState.Rng.NextDouble() < 0.33;

    GameState.ActorEntersLevel(GameState.Player!, dungeon, level);
    GameState.Player!.Loc = portal.Destination;
    GameState.ResolveActorMove(GameState.Player!, start, portal.Destination);
    
    if (trip)
    {
      GameState.UIRef().AlertPlayer("You trip and fall down the stairs!");
      int dmg = GameState.Rng.Next(1, 5);
      List<(int, DamageType)> fallDmg = [(dmg, DamageType.Blunt)];
      var (hpLeft, dmgMsg, _) = GameState.Player.ReceiveDmg(fallDmg, 0, GameState, null, 1.0);
      GameState.UIRef().AlertPlayer(dmgMsg);
      if (hpLeft < 1)
      {
        GameState.ActorKilled(player, "drunken fall", null);
      }
    }

    GameState.FlushPerformers();
    GameState.PrepareFieldOfView();

    if (start.DungeonID != portal.Destination.DungeonID)
      GameState.UIRef().AlertPlayer(GameState.CurrentDungeon.ArrivalMessage);
  }
}

class DownstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override double Execute()
  {
    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Downstairs || t.Type == TileType.Portal || t.Type == TileType.ProfanePortal)
    {
      UsePortal((Portal)t);
    }
    else
    {
      GameState!.UIRef().AlertPlayer("You cannot go down here.");
    }

    // Bit of a kludge: because we change current level in the UsePortal() 
    // call, the list of performers is rebuilt and the actors all get their
    // energy recharged, but this happens 
    return 1.0;
  }
}

class UpstairsAction(GameState gameState) : PortalAction(gameState)
{
  public override double Execute()
  {
    var p = GameState!.Player!;
    var t = GameState.CurrentMap.TileAt(p.Loc.Row, p.Loc.Col);

    if (t.Type == TileType.Upstairs)
    {
      UsePortal((Portal)t);
    }
    else
    {
      GameState.UIRef().AlertPlayer("You cannot go up here.");      
    }

    return 1.0;
  }
}

class UpgradeItemAction : Action
{
  char ItemSlot {  get; set; }
  char ReagentSlot { get; set; }
  readonly Mob _shopkeeper;
  int Total { get; set; }

  public UpgradeItemAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override double Execute()
  {
    base.Execute();
    double result = 1.0;

    var (item, _) = GameState!.Player.Inventory.ItemAt(ItemSlot);
    var (reagent, _) = GameState.Player.Inventory.ItemAt(ReagentSlot);

    if (item is null || reagent is null)
      throw new Exception("Hmm this shouldn't happen when upgrading an item!");

    bool canUpgrade = Alchemy.Compatible(item, reagent);
    if (canUpgrade)
    {
      GameState.Player.Inventory.Zorkmids -= Total;

      var (_, msg) = Alchemy.UpgradeItem(item, reagent);

      GameState.Player.Inventory.RemoveByID(reagent.ID, GameState);

      GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      foreach (string s in msg.Split('\n'))
        GameState.UIRef().AlertPlayer(s.Trim());      
    }
    else
    {
      result = 0.0;
      string txt = $"Hmm I can't figure out a way to enchant your {item!.Name} with {reagent!.Name.IndefArticle()}.";
      GameState.UIRef().SetPopup(new Popup(txt, "", -1, -1));
    }

    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var upgradeResult = (UpgradeItemUIResult)result;
    Total = upgradeResult.Zorkminds;
    ItemSlot = upgradeResult.ItemSlot;
    ReagentSlot = upgradeResult.ReagentSlot;
  }
}

// This is the action for paying an NPC to repair an item
class RepairItemAction : Action
{
  readonly Mob _shopkeeper;
  int Total { get; set; }
  HashSet<ulong> ToRepair { get; set; } = [];

  public RepairItemAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override double Execute()
  {
    base.Execute();

    GameState!.Player.Inventory.Zorkmids -= Total;

    List<Item> items = [];
    foreach (Item item in GameState.Player.Inventory.Items())
    {
      if (ToRepair.Contains(item.ID))
      {
        Effects.RemoveRust(item);
        items.Add(item);

        // Removing it and adding it back in will unstack an item where needed.
        // Ie., you have a stack of rusted daggers but only reapir one.
        if (item.HasTrait<StackableTrait>())
        {
          GameState.Player.Inventory.RemoveByID(item.ID, GameState);
          GameState.Player.Inventory.Add(item, GameState.Player.ID);
        }        
      }
    }
    
    string txt = $"{_shopkeeper.FullName.Capitalize()} gets to work and soon your ";
    if (items.Count > 1)
      txt += "items look almost as good as new!";
    else
      txt += items[0].Name + " looks almost as good as new!";

    GameState.UIRef().SetPopup(new Popup(txt, "", -1, -1));
    GameState.UIRef().AlertPlayer(txt);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var repairResult = (RepairItemUIResult)result;
    Total = repairResult.Zorkminds;
    ToRepair = [..repairResult.ItemIds];
  }
}

class InnkeeperServiceAction : Action
{
  readonly Mob _innkeeper;
  int Invoice { get; set; } = 0;
  string Service { get; set; } = "";

  public InnkeeperServiceAction(GameState gs, Mob innkeeper)
  {
    GameState = gs;
    _innkeeper = innkeeper;
  }

  public override double Execute()
  {
    base.Execute();

    if (Service == "Booze")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      Item booze = ItemFactory.Get(ItemNames.FLASK_OF_BOOZE, GameState.ObjDb);
      GameState.Player.AddToInventory(booze, GameState);
      GameState.UIRef().AlertPlayer($"You purchase a flask of booze from {_innkeeper.FullName}.");
    }
    else if (Service == "Rest")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      GameState.Player.Stats[Attribute.HP].Reset();
      GameState.Player.Stats[Attribute.Nerve].Change(500);

      // Resting at an inn cures poison. It's part of room service.
      List<TemporaryTrait> toRemove = [.. GameState.Player.Traits.OfType<TemporaryTrait>()
                                                  .Where(t => t is PoisonedTrait || t is BlessingTrait)];
      foreach (TemporaryTrait t in toRemove)
      {
        t.Remove(GameState);
      }

      // Leet's be nice an extinguish the player's torch for them if they 
      // forget to do it before they rest.
      foreach (Item item in GameState.Player.Inventory.Items())
      {
        Effects.ExtinguishTorch(GameState, item);
      }

      // Rest for six hours
      RestingTrait rt = new() { ExpiresOn = GameState.Turn + 360 };
      rt.Apply(GameState.Player, GameState);
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult)result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
  }
}

class PriestServiceAction : Action
{
  readonly Mob _priest;
  int Invoice { get; set; } = 0;
  string Service { get; set; } = "";

  public PriestServiceAction(GameState gs, Mob priest)
  {
    GameState = gs;
    _priest = priest;
  }

  public override double Execute()
  {
    base.Execute();

    if (Service == "Absolution")
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;

      string s = $"{_priest.FullName.Capitalize()} accepts your donation, chants a prayer while splashing you with holy water.";
      s += "\n\nYou feel cleansed.";

      GameState.UIRef().AlertPlayer("You feel cleansed.");
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));

      GameState.Player.Traits = [.. GameState.Player.Traits.Where(t => t is not ShunnedTrait)];
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult) result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
  }
}

class WitchServiceAction(GameState gs, Mob witch) : Action(gs, witch)
{
  int Invoice { get; set; }
  string Service { get; set; } = "";

  public override double Execute()
  {
    base.Execute();

    if (Service == "magic101")
    {
      Stat mp = new(GameState!.Rng.NextDouble() < 0.5 ? 1 : 2);
      GameState.Player.Stats.Add(Attribute.MagicPoints, mp);
      GameState.Player.SpellsKnown.Add("arcane spark");

      Item? crystal = null;
      bool hasFocus = false;
      foreach (Item item in GameState.Player.Inventory.Items())
      {
        if (item.Name == "meditation crystal")
          crystal = item;
        if (item.Type == ItemType.Wand || item.Name == "quarterstaff")
          hasFocus = true;
      }

      if (crystal is not null)
      {
        GameState.Player.Inventory.RemoveByID(crystal.ID, GameState);
        GameState.ObjDb.RemoveItemFromGame(Loc.Nowhere, crystal);
      }
      
      if (GameState.FactDb.FactCheck("KylieQuest") is SimpleFact fact)
      {
        fact.Value = "complete";
      }
      
      string s = $"{Actor!.FullName.Capitalize()} gives you a crash course in elementary magic! After some light magic theory and learning to focus through the meditation crystal, you become able to tap into your inner arcane power!";
      if (!hasFocus)
        s += "\n\nOh! You'll also need a magical focus to cast spells through. Any wand will do, or a good quarterstaff!";
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      GameState.UIRef().AlertPlayer("You have mastered Magic 101.");
      GameState.UIRef().AlertPlayer("You have learned to cast arcane spark.");
    }
    else
    {
      GameState!.Player.Inventory.Zorkmids -= Invoice;
      GameState.Player.SpellsKnown.Add(Service);

      string s = $"{Actor!.FullName.Capitalize()} teaches you an arcane formula and you can now cast the [ICEBLUE {Service}] spell!";
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      GameState.UIRef().AlertPlayer("You have learned a new spell.");
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var serviceResult = (ServiceResult)result;
    Invoice = serviceResult.Zorkminds;
    Service = serviceResult.Service;
  }
}

class ShoppingCompletedAction : Action
{
  readonly Mob _shopkeeper;
  int _invoice;
  List<(char, int)> _selections = [];

  public ShoppingCompletedAction(GameState gs, Mob shopkeeper)
  {
    GameState = gs;
    _shopkeeper = shopkeeper;
  }

  public override double Execute()
  {
    GameState!.Player.Inventory.Zorkmids -= _invoice;
    
    string txt = $"You pay {_shopkeeper.FullName} {_invoice} zorkmid";
    if (_invoice > 1)
      txt += "s";
    txt += " and collect your goods.";
    GameState.UIRef().AlertPlayer(txt);

    bool overflow = false;
    foreach (var (slot, count) in _selections)
    {
      List<Item> bought = _shopkeeper.Inventory.Remove(slot, count, GameState);      
      foreach (Item item in bought)
      {
        char inventorySlot = GameState.Player.AddToInventory(item, GameState);
        if (inventorySlot == '\0')
          overflow = true;
      }
    }

    if (overflow)
    {
      GameState.UIRef().AlertPlayer("Uh-oh - you didn't have enough room in your inventory!");      
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var shopResult = (ShoppingUIResult) result;
    _invoice = shopResult.Zorkminds;
    _selections = shopResult.Selections;
  }
}

abstract class DirectionalAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public Loc Loc { get; set; }

  public override void ReceiveUIResult(UIResult result)
  {
    var dirResult = (DirectionUIResult)result;
    Loc = Actor!.Loc with { Row = Actor.Loc.Row + dirResult.Row, Col = Actor.Loc.Col + dirResult.Col };
  }
}

class SelectActionAction(GameState gs, Actor actor) : DirectionalAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();

    Tile tile = GameState!.TileAt(Loc);

    if (GameState.ObjDb.ItemsAt(Loc).Where(i => i.Type == ItemType.Device).Any())
    {
      Actor!.QueueAction(new DeviceInteractionAction(GameState, Actor) { Loc = Loc });
    }
    else if (GameState.ObjDb.Occupied(Loc))
    {
      Actor!.QueueAction(new ChatAction(GameState, Actor) { Loc = Loc });
    }
    else if (tile.Type == TileType.OpenDoor)
    {
      Actor!.QueueAction(new CloseDoorAction(GameState, Actor) { Loc = Loc });
    }
    else if (tile.Type == TileType.ClosedDoor)
    {
      Actor!.QueueAction(new OpenDoorAction(GameState, Actor) { Loc = Loc });
    }
    else if (tile.Type == TileType.BrokenDoor)
    {
      GameState.UIRef().AlertPlayer("That door is broken.");
    }
    else
    {
      GameState.UIRef().AlertPlayer("There's nothing to interact with there.");
    }

    return 0.0;
  }
}

class SayAloudAction(GameState gs, Actor actor) : Action(gs, actor)
{
  string Phrase { get; set; } = "";
  static readonly char[] trimChars = ['.', '!', '?', ',', ';', ':', '-', ')', '(', ' '];

  public override double Execute()
  {
    base.Execute();

    if (Phrase.Length > 0)
    {
      Phrase = Phrase.TrimEnd(trimChars);
    }

    Loc towerGate = Loc.Nowhere;
    if (GameState!.FactDb.FactCheck("Tower Gate") is LocationFact lf)
    {
      towerGate = lf.Loc;
    }

    if (string.Compare(Phrase, "fuck", StringComparison.InvariantCultureIgnoreCase) == 0)
    {
      GameState.UIRef().SetPopup(new Popup("If you are feeling frustrated, you can access Help via the [ICEBLUE ?] command. And you can toggle the commands cheatsheet via [ICEBLUE /].", "", -1, -1));
    }
    else if (OpenTower(towerGate))
    {
      string s = $"As you chant '{Phrase}', the runes on the tower gate flare brightly then fade away. The iron portcullis then shudders, turns to dust, and disappears!";
      GameState!.UIRef().SetPopup(new Popup(s, "", -1, -1));
      GameState.CurrentMap.SetTile(towerGate.Row, towerGate.Col, TileFactory.Get(TileType.StoneFloor));
    }
    else if (TransportToCave(GameState.FactDb))
    {
      Loc caveEntrance = Loc.Nowhere;
      if (GameState.FactDb.FactCheck("UnderwaterCaveEntrance") is LocationFact celf)
        caveEntrance = celf.Loc;

      GameState.ActorEntersLevel(GameState.Player, caveEntrance.DungeonID, caveEntrance.Level);
      Loc start = GameState.Player.Loc;
      GameState.Player.Loc = caveEntrance;
      GameState.ResolveActorMove(GameState.Player, start, caveEntrance);
      GameState.FlushPerformers();
      
      string s = $"You call out '{Phrase}'! Your stomach lurches and you find yourself somewhere else!";
      GameState!.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
    else
    {
      GameState!.UIRef().SetPopup(new Popup($"You say '{Phrase}' out loud, but nothing seems to happen.", "", -1, -1));
    }

    return 1.0;

    bool TransportToCave(FactDb factDb)
    {
      if (factDb.FactCheck("Stone ring centre") is not LocationFact src || GameState.Player.Loc != src.Loc)
      {
        return false;
      }

      if (GameState!.FactDb.FactCheck("MDTemplePassword") is SimpleFact pwd)
      {
        if (string.Compare(Phrase, pwd.Value, StringComparison.InvariantCultureIgnoreCase) != 0 && Phrase != "foo")
          return false;
      }

      return true;
    }

    bool OpenTower(Loc towerGate)
    {
      if (GameState!.FactDb.FactCheck("SorceressPassword") is SimpleFact sf)
      {
        if (string.Compare(Phrase, sf.Value, StringComparison.InvariantCultureIgnoreCase) != 0)
          return false;
      }
      
      foreach (Loc loc in Util.Adj8Locs(GameState.Player.Loc))
      {
        if (loc == towerGate)
          return true;
      }
      
      return false;
    }
  }

  public override void ReceiveUIResult(UIResult result) 
  { 
    if (result is StringUIResult s)
      Phrase = s.Text;
  }
}

class ChatAction(GameState gs, Actor actor) : DirectionalAction(gs, actor)
{  
  public override double Execute()
  {
    base.Execute();

    Actor? other = GameState!.ObjDb.Occupant(Loc);

    if (other is Player)
    {
      TextInputer ti = new(GameState, "What do you say?")
      {
        DeferredAction = new SayAloudAction(GameState, other)
      };
      GameState.UIRef().SetInputController(ti);

      return 0.0;
    }
    else if (other is not null)
    {
      var (chatAction, acc) = other.Behaviour.Chat((Mob)other, GameState);

      if (chatAction is NullAction)
      {        
        return 1.0;
      }

      acc!.DeferredAction = chatAction;
      GameState.UIRef().SetInputController(acc);

      return 0.0;
    }
    else
    {
      GameState.UIRef().AlertPlayer("There's no one there!");
      return 0.0;
    }
  }
}

class CloseDoorAction : DirectionalAction
{
  public CloseDoorAction(GameState gs, Actor actor) : base(gs, actor) => GameState = gs;
  public CloseDoorAction(GameState gs, Actor actor, Loc loc) : base(gs, actor)
  {
    Loc = loc;
    GameState = gs;
  }

  public override double Execute()
  {
    base.Execute();
    double result = 0.0;
    UserInterface ui = GameState!.UIRef();
    Tile tile = GameState!.CurrentMap.TileAt(Loc.Row, Loc.Col);

    if (tile is Door door)
    {
      if (GameState.ObjDb.Occupied(Loc))
      {
        if (Actor is Player)
          ui.AlertPlayer("There is someone in the way.");
      }
      else if (GameState.ObjDb.ItemsAt(Loc).Count > 0)
      {
        if (Actor is Player)
          ui.AlertPlayer("There is something in the way.");
      }
      else if (door.Open)
      {
        door.Open = false;
        result = 1.0;
        ui.AlertPlayer(MsgFactory.DoorMessage(Actor!, Loc, "close", GameState!));
      }
      else if (Actor is Player)
      {
        ui.AlertPlayer("The door is already closed.");
      }
    }
    else if (Actor is Player)
    {
      string s = tile.Type == TileType.BrokenDoor ? "The door is broken!" : "There's no door there!";
      ui.AlertPlayer(s);
    }

    return result;
  }
}

class OpenDoorAction : DirectionalAction
{
  public OpenDoorAction(GameState gs, Actor actor) : base(gs, actor) => GameState = gs;
  public OpenDoorAction(GameState gs, Actor actor, Loc loc) : base(gs, actor)
  {
    Loc = loc;
    GameState = gs;
  }

  public override double Execute()
  {
    base.Execute();
    double result = 0.0;
    Tile door = GameState!.CurrentMap.TileAt(Loc.Row, Loc.Col);
    UserInterface ui = GameState.UIRef();

    if (door is Door d)
    {
      if (d.Type == TileType.LockedDoor)
      {
        result = 1.0;

        ui.AlertPlayer("The door is locked!", GameState, Loc);
      }
      else if (!d.Open)
      {
        d.Open = true;
        result = 1.0;
        ui.AlertPlayer(MsgFactory.DoorMessage(Actor!, Loc, "open", GameState!));        
      }
      else if (Actor is Player)
      {
        ui.AlertPlayer("The door is already open.", GameState, Loc);
      }
    }
    else if (door is VaultDoor vd)
    {
      string msg = vd.Open ? "The doors stand open." : "You'll need a key!";
      ui.AlertPlayer(msg, GameState, Loc);
    }
    else if (Actor is Player)
    {
      ui.AlertPlayer("There's no door there!");
    }

    return result;
  }
}

class DeviceInteractionAction : DirectionalAction
{
  public DeviceInteractionAction(GameState gs, Actor actor) : base(gs, actor) => GameState = gs;
  public DeviceInteractionAction(GameState gs, Actor actor, Loc loc) : base(gs, actor)
  {
    Loc = loc;
    GameState = gs;
  }

  public override double Execute()
  {
    base.Execute();
    
    Item? device = null;
    foreach (Item item in GameState!.ObjDb.ItemsAt(Loc))
    {
      if (item.Type == ItemType.Device)
      {
        device = item;
        break;
      }
    }

    if (device is not null)
    {
      foreach (Trait t in device.Traits)
      {
        if (t is BoolTrait b && b.Name == "Tilt")
        {
          b.Value = !b.Value;
          GameState.UIRef().AlertPlayer("You tilt the mirror.");

          // Assuming a mirror here...
          device.Glyph = device.Glyph with { Ch = b.Value ? '\\' : '/' };

          return 1.0;
        }
        else if (t is DirectionTrait d)
        {
          GameState.UIRef().AlertPlayer("You rotate the lamp.");
          switch (d.Dir)
          {
            case Dir.North:
              d.Dir = Dir.East;
              device.Glyph = device.Glyph with { Ch = '◑' };
              break;
            case Dir.East:            
              d.Dir = Dir.South;
              device.Glyph = device.Glyph with { Ch = '◒' };
              break;
            case Dir.South:
              d.Dir = Dir.West;
              device.Glyph = device.Glyph with { Ch = '◐' };
              break;
            case Dir.West:
              d.Dir = Dir.North;
              device.Glyph = device.Glyph with { Ch = '◓' };
              break;
          }
          
          return 1.0;
        }
      }  
    }

    return 0.0;
  }
}

// Some monsters, when grappling, automatically deal damage to their victim
class CrushAction(GameState gs, Actor actor, ulong victimId, int dmgDie, int dmgDice) : Action(gs, actor)
{
  public int DmgDie { get; set; } = dmgDie;
  public int DmgDice { get; set; } = dmgDice;
  public ulong VictimId { get; set; } = victimId;

  public override double Execute()
  {
    base.Execute();

    if (GameState!.ObjDb.GetObj(VictimId) is Actor victim)
    {
      if (GameState!.LastPlayerFoV.ContainsKey(Actor!.Loc))
      {
        string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "crush")} {victim.FullName}.";
        GameState.UIRef().AlertPlayer(s);
      }

      List<(int, DamageType)> damageRolls = [];
      for (int i = 0; i < DmgDice; i++)
      {
        damageRolls.Add((GameState.Rng.Next(DmgDie) + 1, DamageType.Blunt));        
      }
      var (hpLeft, _, _) = victim.ReceiveDmg(damageRolls, 0, GameState, null, 1.0);
      if (hpLeft < 1)
      {
        GameState.ActorKilled(victim, "being crushed", null);
      }
    }
    
    return 1.0;
  }
}

class PickupItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public List<ulong> ItemIDs { get; set; } = [];
  
  public override double Execute()
  {
    base.Execute();
    double result = 1.0;

    UserInterface ui = GameState!.UIRef();
    ui.ClosePopup();
    
    int webDC = 0;
    string webName = "";
    // First, is there anything preventing the actor from picking items up off
    // the floor? (At the moment it's just webs in the game, but a 
    // Sword-in-the-Stone situation might be neat)
    foreach (Item env in GameState.ObjDb.EnvironmentsAt(Actor!.Loc))
    {
      if (env.Traits.OfType<StickyTrait>().FirstOrDefault() is StickyTrait web)
      {
        webDC = int.Max(webDC, web.DC);
        webName = env.Name.DefArticle();
      }
    }

    Inventory inv = Actor.Inventory;
    bool anythingPickedUp = false;
    foreach (ulong id in ItemIDs)
    {
      Item item = GameState.ObjDb.GetObj(id) as Item ?? throw new Exception($"Item ID {id} does not exist");

      if (item.HasTrait<AffixedTrait>())
      {
        ui.AlertPlayer($"You cannot pick up {item.FullName.DefArticle()}!");
        continue;
      }

      if (webDC > 0)
      {
        bool strCheck = Actor.AbilityCheck(Attribute.Strength, webDC, GameState.Rng);
        if (strCheck)
        {
          ui.AlertPlayer($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "tear")} {item.FullName.DefArticle()} from {webName}.");          
        }
        else
        {
          ui.AlertPlayer($"{item.FullName.DefArticle().Capitalize()} {Grammar.Conjugate(item, "is")} stuck to {webName}!");
          continue;
        }
      }

      GameState.ObjDb.RemoveItemFromLoc(item.Loc, item);

      bool success = Cmd.AddItemToInventory(Actor, item, GameState);
      if (!success)
        continue;

      int count = 1;
      if (item.HasTrait<StackableTrait>())
      {
        foreach (Item stackedItem in GameState.ObjDb.ItemsAt(Actor.Loc).Where(i => i == item))
        {
          Cmd.AddItemToInventory(Actor, stackedItem, GameState);
          GameState.ObjDb.RemoveItemFromLoc(stackedItem.Loc, stackedItem);
          ++count;
        }
      }
      
      string pickupMsg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "pick")} up ";
      if (item.Type == ItemType.Zorkmid && item.Value == 1)
        pickupMsg += "a zorkmid.";
      else if (item.Type == ItemType.Zorkmid)
        pickupMsg += $"{item.Value} zorkmids.";
      else
        pickupMsg += MsgFactory.CalcName(item, GameState.Player, count, Article.Def) + ".";
      
      if (item.Traits.OfType<OwnedTrait>().FirstOrDefault() is OwnedTrait ownedTrait)
      {
        List<string> msgs = GameState.OwnedItemPickedUp(ownedTrait.OwnerIDs, Actor, item.ID);
        foreach (string s in msgs)
          ui.AlertPlayer(s);
      }

      List<Trait> toClear = [];
      foreach (Trait t in item.Traits)
      {
        if (t is InPitTrait)
          toClear.Add(t);
        else if (t is OnPickupTrait opt)
        {
          opt.Apply(Actor, GameState);
          if (opt.Clear)
            toClear.Add(t);
        }
      }
      foreach (Trait t in toClear)
        item.Traits.Remove(t);

      anythingPickedUp = true;
      ui.AlertPlayer(pickupMsg, GameState, Actor.Loc);
    }

    if (!anythingPickedUp)
    {
      result = 0.0;
    }
            
    return result;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    if (result is LongListResult list)
      ItemIDs = list.Values;    
  }
}

class CastCurse(Loc target, int dc) : Action
{
  Loc TargetLoc { get; set; } = target;
  int DC { get; set; } = dc;

  public override double Execute()
  {
    if (GameState!.ObjDb.Occupant(TargetLoc) is not Actor target)
      return 1.0;

    Actor caster = Actor!;

    string casterName = MsgFactory.CalcName(caster, GameState.Player).Capitalize();
    string targetName = MsgFactory.CalcName(target, GameState.Player);
    string s = $"{casterName} {Grammar.Conjugate(caster, "curse")} {targetName}!";
    GameState.UIRef().AlertPlayer(s, GameState, TargetLoc);

    if (!target.AbilityCheck(Attribute.Will, DC, GameState.Rng))
    {
      CurseTrait curse = new();
      List<string> msgs = curse.Apply(target, GameState);
      GameState.UIRef().AlertPlayer(string.Join(" ", msgs), GameState, TargetLoc);
    }
    else
    {
      GameState.UIRef().AlertPlayer("The curse fails to get a grip.", GameState, TargetLoc);
    }

    return 1.0;
  }
}

class MinorSummonAction(GameState gs, Actor actor) : Action(gs, actor)
{
  static string RandomMonster(Rng rng)
  {
    string[] opts = ["screech bat", "rust monster", "ghoul", "flame beetle", "dire goat", "skeleton"];
    return opts[rng.Next(opts.Length)];
  }

  Loc PickAnimSpot(Loc centre)
  {
    List<Loc> opts = [];

    Map map = GameState!.CurrentMap;
    GameObjectDB objDb = GameState.ObjDb;
    for (int c = centre.Col - 1; c <= centre.Col + 3; c++)
    {
      if (CheckLoc(centre.Row - 2, c))
        opts.Add(centre with { Row = centre.Row - 2, Col = c });      
      if (CheckLoc(centre.Row + 2, c))
        opts.Add(centre with { Row = centre.Row + 2, Col = c });
    }

    return opts.Count > 0 ? opts[GameState.Rng.Next(opts.Count)] : Loc.Nowhere;

    bool CheckLoc(int row, int col)
    {
      if (!(map.InBounds(row, col) && map.TileAt(row, col).Passable() && !objDb.Occupied(centre with { Row = row, Col = col })))
        return false;
      if (!(map.InBounds(row, col + 1) && map.TileAt(row, col + 1).Passable() && !objDb.Occupied(centre with { Row = row, Col = col + 1 })))
        return false;
      if (!(map.InBounds(row, col + 2) && map.TileAt(row, col + 2).Passable() && !objDb.Occupied(centre with { Row = row, Col = col + 2 })))
        return false;

      return true;
    }
  }

  void SetBarkAnim(GameState gs, Loc speaker, Loc left, int duration)
  {
    SqAnimation anim;
    if (speaker.Row > left.Row)
    {
      if (speaker.Col < left.Col)
        anim = new(gs, speaker with { Row = speaker.Row - 1, Col = speaker.Col + 1 }, Colours.WHITE, Colours.BLACK, '/', duration);
      else if (speaker.Col > left.Col)
        anim = new(gs, speaker with { Row = speaker.Row - 1, Col = speaker.Col - 1}, Colours.WHITE, Colours.BLACK, '\\', duration);
      else
        anim = new(gs, speaker with { Row = speaker.Row - 1, Col = speaker.Col }, Colours.WHITE, Colours.BLACK, '|', duration);
    }
    else
    {
      if (speaker.Col < left.Col)
        anim = new(gs, speaker with { Row = speaker.Row + 1, Col = speaker.Col + 1 }, Colours.WHITE, Colours.BLACK, '\\', duration);
      else if (speaker.Col > left.Col)
        anim = new(gs, speaker with { Row = speaker.Row + 1, Col = speaker.Col - 1 }, Colours.WHITE, Colours.BLACK, '/', duration);
      else
        anim = new(gs, speaker with { Row = speaker.Row + 1, Col = speaker.Col }, Colours.WHITE, Colours.BLACK, '|', duration);
    }

    gs.UIRef().RegisterAnimation(anim);
  }

  public override double Execute()
  {
    base.Execute();

    GameState!.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} summons some monsters!", GameState, Actor.Loc);

    Actor m1 = MonsterFactory.Get(RandomMonster(GameState.Rng), GameState.ObjDb, GameState.Rng);
    m1.Stats[Attribute.MobAttitude] = new Stat(Mob.AGGRESSIVE);
    Actor m2 = MonsterFactory.Get(RandomMonster(GameState.Rng), GameState.ObjDb, GameState.Rng);
    m2.Stats[Attribute.MobAttitude] = new Stat(Mob.AGGRESSIVE);
    Actor m3 = MonsterFactory.Get(RandomMonster(GameState.Rng), GameState.ObjDb, GameState.Rng);
    m3.Stats[Attribute.MobAttitude] = new Stat(Mob.AGGRESSIVE);

    int animDuration = 750;
    int numToSummon = GameState.Rng.Next(2, 4);
    // First try to find a spot for the silly summon animation
    Loc left = PickAnimSpot(Actor.Loc);
    if (left != Loc.Nowhere)
    {
      AddMonster(m1, left, GameState);

      Loc secondLoc = left with { Col = left.Col + 2 };
      AddMonster(m2, secondLoc, GameState);
      
      Loc thirdLoc = left with { Col = left.Col + 1 };
      if (numToSummon == 3)
      {
        AddMonster(m3, thirdLoc, GameState);
      }
      else
      {
        GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, thirdLoc, Colours.ICE_BLUE, Colours.BLACK, ' ', animDuration));
      }

      SetBarkAnim(GameState, Actor.Loc, left, animDuration);

      return 1.0;
    }

    Map map = GameState.CurrentMap;
    List<Loc> nearby = [];
    // We couldn't do the animation so just try to find three open locations nearby to plunk the monsters into
    for (int r = Actor.Loc.Row - 2; r <= Actor.Loc.Row + 3; r++)
    {
      for (int c = Actor.Loc.Col - 2; c <= Actor.Loc.Col + 3; c++)
      {
        Loc loc = Actor.Loc with { Row = r, Col = c };
        if (map.InBounds(r, c) && map.TileAt(r, c).Passable() && !GameState.ObjDb.Occupied(loc))
          nearby.Add(loc);
      }     
    }
    nearby.Shuffle(GameState.Rng);

    if (nearby.Count == 0)
    {
      GameState!.UIRef().AlertPlayer($"...but the spell fizzles!", GameState, Actor.Loc);
      return 1.0;
    }

    if (nearby.Count > 0)
    {
      Loc loc = nearby[0];
      nearby.RemoveAt(0);
      AddMonster(m1, loc, GameState);
    }

    if (nearby.Count > 0)
    {
      Loc loc = nearby[0];
      nearby.RemoveAt(0);
      AddMonster(m2, loc, GameState);
    }
  
    if (numToSummon == 3 && nearby.Count > 0)
    {
      Loc loc = nearby[0];
      AddMonster(m3, loc, GameState);
    }

    void AddMonster(Actor m, Loc loc, GameState gs)
    {
      gs.ObjDb.AddNewActor(m, loc);
      gs.UIRef().RegisterAnimation(new SqAnimation(gs, loc, Colours.ICE_BLUE, Colours.BLACK, m.Glyph.Ch, animDuration));
    }

    return 1.0;
  }
}

class SummonAction(Loc target, string summons, int count) : Action()
{
  readonly Loc _target = target;
  readonly string _summons = summons;
  readonly int _count = count;

  Loc SpawnPt()
  {
    var gs = GameState!;
    if (gs.TileAt(_target).Passable() && !gs.ObjDb.Occupied(_target))
      return _target;

    List<Loc> locs = [.. Util.Adj8Locs(_target).Where(l => gs.TileAt(l).Passable() && !gs.ObjDb.Occupied(l))];
    if (locs.Count == 0)
      return Loc.Nowhere;
    else
      return locs[gs.Rng.Next(locs.Count)];
  }

  public override double Execute()
  {
    base.Execute();

    // We'll keep the level from getting too over-populated. I don't know what
    // a good number is yet, but let's start with 100
    int levelPop = GameState!.ObjDb.LevelCensus(Actor!.Loc.DungeonID, Actor.Loc.Level);
    if (levelPop > 100)
    {
      if (GameState.LastPlayerFoV.ContainsKey(Actor.Loc))
        GameState.UIRef().AlertPlayer("A spell fizzles");

      return 1.0;
    }

    Actor? summoned = null;
    int summonCount = 0;
    for (int j = 0; j < _count; j++)
    {
      var loc = SpawnPt();
      if (loc != Loc.Nowhere)
      {
        summoned = MonsterFactory.Get(_summons, GameState!.ObjDb, GameState.Rng);
        summoned.Stats[Attribute.MobAttitude].SetMax(Mob.AGGRESSIVE);
        GameState.ObjDb.AddNewActor(summoned, loc);
        ++summonCount;
      }
    }

    List<string> msgs = [];
    if (GameState!.LastPlayerFoV.ContainsKey(Actor!.Loc))
    {
      if (summonCount == 0)
      {
        msgs.Add("A spell fizzles.");
      }
      else if (summoned is not null)
      {
        string txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "summon")} ";
        txt += MsgFactory.CalcName(summoned, GameState.Player, summonCount, Article.InDef) + ".";
        msgs.Add(txt);
      }
    }

    foreach (string s in msgs)
      GameState!.UIRef().AlertPlayer(s);

    return 1.0;
  }
}

class SearchAction(GameState gs, Actor player) : Action(gs, player)
{
  public override double Execute()
  {
    base.Execute();

    GameState gs = GameState!;
    UserInterface ui = gs.UIRef();
    Loc playerLoc = Actor!.Loc;
    List<Loc> sqsToSearch = [..gs.LastPlayerFoV.Keys
                                 .Where(loc => Util.Distance(playerLoc, loc) <= 3)];

    bool rogue = gs.Player.Background == PlayerBackground.Skullduggery;
    int dc;
    foreach (Loc loc in sqsToSearch)
    {
      if (gs.ObjDb.Occupant(loc) is Actor actor && actor.IsDisguised())
      {
        dc = rogue ? 13 : 15;
        if (gs.Rng.Next(1, 21) >+ dc)
        {
          var disguise = actor.Traits.OfType<DisguiseTrait>().First();
          ui.AlertPlayer($"Wait! That {disguise.DisguiseForm} is actually {actor.Name.IndefArticle()}!");
          disguise.Disguised = false;
          actor.Glyph = disguise.TrueForm;
        }
      }
      
      // I'm not going to roll to find secret items. I'm not sure I should
      // even bother for traps/secret doors
      foreach (Item item in gs.ObjDb.ItemsAt(loc))
      {
        if (item.HasTrait<HiddenTrait>())
        {
          item.Traits = [.. item.Traits.Where(t => t is not HiddenTrait)];
          ui.AlertPlayer($"You find {item.FullName.IndefArticle()}!");
        }
      }

      Tile tile = gs.TileAt(loc);
      switch (tile.Type)
      {
        case TileType.SecretDoor:
          dc = 10 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc) 
          {
            ui.AlertPlayer("You spot a secret door!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.ClosedDoor));
          }
          break;
        case TileType.HiddenPit:
        case TileType.HiddenTrapDoor:
        case TileType.HiddenTeleportTrap:
        case TileType.HiddenDartTrap:
          dc = 15 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc)
          {
            TileType replacementTile = tile.Type switch 
            {
              TileType.HiddenPit => TileType.Pit,
              TileType.HiddenTeleportTrap => TileType.TeleportTrap,
              TileType.HiddenDartTrap => TileType.DartTrap,
              _ => TileType.TrapDoor
            };
            ui.AlertPlayer("You spot a trap!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(replacementTile));
          }          
          break;
        case TileType.HiddenMagicMouth:
          dc = 10 + gs.CurrLevel + 1;
          if (rogue)
            dc -= 2;
          dc = int.Min(dc, 20);
          if (gs.Rng.Next(1, 21) <= dc)
          {
            ui.AlertPlayer("You spot a magic mouth!");
            gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.MagicMouth));
          }
          break;
        case TileType.JetTrigger:
          JetTrigger jt = (JetTrigger)tile;
          if (!jt.Visible)
          {
            dc = 15 + gs.CurrLevel + 1;
            if (rogue)
              dc -= 2;
            dc = int.Min(dc, 20);
            if (gs.Rng.Next(1, 21) <= dc)
            {
              jt.Visible = true;
              ui.AlertPlayer("You spot a loose flagstone!");
            }
          }
          break;
        case TileType.GateTrigger:
          GateTrigger gt = (GateTrigger)tile;
          if (!gt.Found)
          {
            dc = 12 + gs.CurrLevel + 1;
            if (rogue)
              dc -= 2;
            dc = int.Min(dc, 20);
            if (gs.Rng.Next(1, 21) <= dc)
            {
              ui.AlertPlayer("You spot a pressure plate!");
              gt.Found = true;
            }
          }
          
          break;
      }      
    }

    MagicMapAnimation anim = new(gs, gs.CurrentDungeon, sqsToSearch, false)
    {
      Colour = Colours.SEARCH_HIGHLIGHT,
      AltColour = Colours.SEARCH_HIGHLIGHT
    };
    gs.UIRef().RegisterAnimation(anim);

    return 1.0;
  }
}

class DetectTrapsAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override double Execute()
  {
    base.Execute();

    if (Actor is Player player)
    {
      Loc playerLoc = GameState!.Player.Loc;
      
      int topScreenRow = playerLoc.Row - UserInterface.ViewHeight / 2;
      int topScreenCol = playerLoc.Col - UserInterface.ViewWidth / 2;
      int trapsFound = 0;
      for (int r = 1; r < GameState!.CurrentMap.Height - 1; r++)
      {
        for (int c = 1; c < GameState.CurrentMap.Width - 1; c++)
        {
          Loc loc = GameState.Player.Loc with { Row = r, Col = c };
          Tile tile = GameState.TileAt(loc);
          if (tile.IsTrap())
          {
            ++trapsFound;
            Glyph g = new('^', Colours.WHITE, Colours.WHITE, Colours.BLACK, true);
            GameState.CurrentDungeon.RememberedLocs[loc] = new(g, 0);
            Traps.RevealTrap(tile, GameState, loc);

            if (Util.PtInSqr(r, c, topScreenRow, topScreenCol, UserInterface.ViewHeight, UserInterface.ViewWidth))
            {
              SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, '^') { IgnoreFoV = true };
              GameState.UIRef().RegisterAnimation(anim);
            }
          }
        }
      }
      
      if (trapsFound > 0)
        GameState.UIRef().AlertPlayer("You have a sense of looming danger!");
      else
        GameState.UIRef().AlertPlayer("You feel a certain relief.");
    }

    return 1.0;
  }
}

class DetectTreasureAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override double Execute()
  {
    base.Execute();

    if (Actor is Player player)
    {
      Loc playerLoc = GameState!.Player.Loc;

      int topScreenRow = playerLoc.Row - UserInterface.ViewHeight / 2;
      int topScreenCol = playerLoc.Col - UserInterface.ViewWidth / 2;
      int itemsFound = 0;
      for (int r = 1; r < GameState!.CurrentMap.Height - 1; r++)
      {
        for (int c = 1; c < GameState.CurrentMap.Width - 1; c++)
        {
          Loc loc = GameState.Player.Loc with { Row = r, Col = c };
          Glyph glyph = GameState.ObjDb.ItemGlyphAt(loc);
          if (glyph != GameObjectDB.EMPTY)
          {
            ++itemsFound;
            GameState.CurrentDungeon.RememberedLocs[loc] = new(glyph, 0);

            if (Util.PtInSqr(r, c, topScreenRow, topScreenCol, UserInterface.ViewHeight, UserInterface.ViewWidth))
            {
              SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, glyph.Ch) { IgnoreFoV = true };
              GameState.UIRef().RegisterAnimation(anim);
            }
          }
        }
      }
      
      if (itemsFound > 0)
        GameState.UIRef().AlertPlayer("Your nose twitches and you feel greedy!");
      else
        GameState.UIRef().AlertPlayer("You feel a sense of disappointment.");
    }

    return 1.0;
  }
}

class MagicMapAction(GameState gs, Actor caster) : Action(gs, caster)
{
  // Essentially we want to flood fill out and mark all reachable squares as 
  // remembered, ignoreable Passable() or not but stopping at walls. This will
  // currently not fully map a level with disjoint spaces but I'm not sure if
  // I think that's a problem or not.
  void FloodFillMap(GameState gs, Loc start)
  {    
    Dungeon dungeon = gs.CurrentDungeon;
    PriorityQueue<Loc, int> locsQ = new();
    HashSet<Loc> visited = [];
    var q = new Queue<Loc>();
    q.Enqueue(start);
    
    while (q.Count > 0) 
    { 
      var curr = q.Dequeue();
      visited.Add(curr);

      foreach (var adj in Util.Adj8Locs(curr)) 
      {        
        if (visited.Contains(adj))
          continue;

        var tile = gs.TileAt(adj);
        locsQ.Enqueue(adj, Util.Distance(Actor!.Loc, adj));

        switch (tile.Type)
        {
          case TileType.Unknown:
          case TileType.DungeonWall:
          case TileType.PermWall:
          case TileType.WorldBorder:            
            break;
          default:
            if (!visited.Contains(adj))
              q.Enqueue(adj);
            break;
        }

        visited.Add(adj);
      }
    }

    List<Loc> locs = [];
    while (locsQ.Count > 0)
      locs.Add(locsQ.Dequeue());

    MagicMapAnimation anim = new(gs, dungeon, locs);
    gs.UIRef().RegisterAnimation(anim);
  }

  public override double Execute()
  {
    base.Execute();

    // It's probably a bug if a monster invokes this action??
    if (Actor is Player player)
    {
      if (GameState!.CurrentMap.HasFeature(MapFeatures.Unmappable))
      {
        GameState.UIRef().AlertPlayer("This region cannot be encompassed by such puny magicks! The spell faiis.");
      }
      else
      {
        GameState.UIRef().AlertPlayer("A vision of your surroundings fills your mind!");
        FloodFillMap(GameState, player.Loc);
      }      
    }

    return 1.0;
  }
}

class MirrorImageAction : Action
{
  readonly Loc _target;

  public MirrorImageAction(GameState gs, Actor caster, Loc target)
  {
    GameState = gs;
    Actor = caster;
    _target = target;
  }

  static Mob MakeDuplciate(GameState gs, Actor src)
  { 
    Glyph glyph = new(src.Glyph.Ch, src.Glyph.Lit, src.Glyph.Unlit, src.Glyph.BG, src.Glyph.Illuminate);
    
    // I originally implemented MirrorImage for cloakers, who can fly but I
    // think it makes sense for all mirror images since they're illusions that
    // may drift over water/lava 
    Mob dup = new()
    {
      Name = src.Name,
      Glyph = glyph,
      Recovery = 1.0
    };

    dup.Stats.Add(Attribute.HP, new Stat(1));
    dup.Stats.Add(Attribute.AC, new Stat(10));
    dup.Stats.Add(Attribute.MobAttitude, new Stat(Mob.AGGRESSIVE));

    dup.Traits.Add(new FlyingTrait());

    var illusion = new IllusionTrait()
    {
      SourceId = src.ID,
      ObjId = dup.ID
    };
    dup.Traits.Add(illusion);
    gs.RegisterForEvent(GameEventType.Death, illusion, src.ID);

    dup.Traits.Add(new BehaviourTreeTrait() { Plan = "MonsterPlan" });

    var msg = new DeathMessageTrait() { Message = $"{dup.FullName.Capitalize()} fades away!" };
    dup.Traits.Add(msg);

    return dup;
  }

  public override double Execute()
  {
    // Mirror image, create 4 duplicates of caster surrounding the target location
    List<Loc> options = [];

    foreach (var loc in Util.Adj8Locs(_target))
    {
      if (GameState!.TileAt(loc).PassableByFlight() && !GameState.ObjDb.Occupied(loc))
        options.Add(loc);
    }

    if (options.Count == 0)
    {
      GameState!.UIRef().AlertPlayer("A spell fizzles...");
      return 1.0;
    }

    List<Mob> images = [];
    int duplicates = int.Min(options.Count, 4);
    while (duplicates > 0)
    {
      int i = GameState!.Rng.Next(options.Count);
      Loc loc = options[i];
      options.RemoveAt(i);

      Mob dup = MakeDuplciate(GameState, Actor!);
      GameState.ObjDb.AddNewActor(dup, loc);
      
      images.Add(dup);

      --duplicates;
    }

    // We've created the duplicates so now the caster swaps locations
    // with one of them
    Mob swap = images[GameState!.Rng.Next(images.Count)];
    GameState.SwapActors(Actor!, swap);

    base.Execute();
    GameState.UIRef().AlertPlayer("How puzzling!");

    return 1.0;
  }
}

class InkCloudAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override double Execute()
  {
    base.Execute();

    GameState gs = GameState!;
    Loc targetLoc = Actor!.Loc;

    if (gs.LastPlayerFoV.ContainsKey(targetLoc))
      gs.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "spray")} a cloud of ink!");

    foreach (Loc loc in Util.LocsInRadius(targetLoc, 2, gs.CurrentMap.Height, gs.CurrentMap.Width))
    {
      TileType tile = gs.TileAt(loc).Type;
      if (!(tile == TileType.Lake || tile == TileType.Underwater || tile == TileType.DeepWater))
        continue;

      Item ink = ItemFactory.Ink(gs);
      var timer = ink.Traits.OfType<CountdownTrait>().First();
      gs.RegisterForEvent(GameEventType.EndOfRound, timer);
      gs.ObjDb.Add(ink);
      gs.ObjDb.SetToLoc(loc, ink);
    }

    // We need to do this here because it changes the player's FOV and we want
    // to update the display appropriately
    gs.PrepareFieldOfView();

    return 1.0;
  }
}

class FogCloudAction(GameState gs, Actor caster, int range) : Action(gs, caster)
{
  public int Range { get; set; } = range;

  public override double Execute()
  {
    base.Execute();

    GameState gs = GameState!;

    Loc targetLoc = Actor!.PickRangedTargetLoc(GameState!, Range);
    if (gs.LastPlayerFoV.ContainsKey(targetLoc))
      gs.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Fog Cloud!");
    
    foreach (Loc loc in Util.LocsInRadius(targetLoc, 2, gs.CurrentMap.Height, gs.CurrentMap.Width))
    {
      Item mist = ItemFactory.Fog(gs);
      var timer = mist.Traits.OfType<CountdownTrait>().First();
      gs.RegisterForEvent(GameEventType.EndOfRound, timer);
      gs.ObjDb.Add(mist);
      gs.ItemDropped(mist, loc);
    }

    // We need to do this here because it changes the player's FOV and we want
    // to update the display appropriately
    gs.PrepareFieldOfView();

    return 1.0;
  }
}

class FlareAction(GameState gs, Actor actor, int dmgDie, int numOfDice, DamageType dmgType) : Action(gs, actor)
{
  int DmgDie { get; set; } = dmgDie;
  int NumOfDice { get; set; } = numOfDice;
  DamageType DamageType { get; set; } = dmgType;

  public override double Execute()
  {
    base.Execute();

    UserInterface ui = GameState!.UIRef();
    if (DamageType == DamageType.Fire)
      ui.AlertPlayer($"Flames flare out from {Actor!.FullName}!", GameState, Actor.Loc, Actor);
    else
      ui.AlertPlayer($"Energy emits from {Actor!.FullName}!", GameState, Actor.Loc, Actor);

    foreach (Loc adj in Util.Adj8Locs(Actor.Loc))
    {
      Effects.ApplyDamageEffectToLoc(adj, DamageType, GameState);

      if (GameState.ObjDb.Occupant(adj) is Actor adjActor)
      {
        int dmg = 0;
        for (int j = 0; j < NumOfDice; j++)
          dmg += GameState.Rng.Next(DmgDie) + 1;
        List<(int, DamageType)> dmgs = [(dmg, DamageType)];
        var (adjHpLeft, _, _) = adjActor.ReceiveDmg(dmgs, 0, GameState, null, 1.0);

        if (DamageType == DamageType.Fire)
          ui.AlertPlayer($"{adjActor.FullName.Capitalize()} {Grammar.Conjugate(adjActor, "is")} burnt!");
        
        if (adjHpLeft < 1)
        {
          GameState.ActorKilled(adjActor, Actor.FullName, null);
        }
      }

      if (DamageType == DamageType.Fire)
      {
        Glyph g = Util.FlameGlyph(GameState.Rng);
        ui.RegisterAnimation(new SqAnimation(GameState, adj, g.Lit, g.BG, Constants.FIRE_CHAR));
      }
      else
      {
        ui.RegisterAnimation(new SqAnimation(GameState, adj, Colours.LIGHT_PURPLE, Colours.FADED_PURPLE, '*'));
      }
    }
        
    return 1.0;
  }
}

class WhirlpoolAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override double Execute()
  {
    base.Execute();

    UserInterface ui = GameState!.UIRef();
    ui.AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "create")} a swirling whirlpool!", GameState, Actor.Loc, Actor);

    int strength = int.Max(30, Actor.Stats[Attribute.Strength].Curr * 30);
    int spiralLength = int.Min(strength, Util.Spiral.Length);

    // Process all locations in the spiral pattern
    Dictionary<ulong, int> movedObjects = [];
    List<Loc> affectedLocs = [ Actor.Loc ];
    
    // Find the path of the spiral, by moving along the path until the end or 
    // we reach a non-water tile
    for (int j = 1; j < spiralLength; j++)
    {
      var (dr, dc) = Util.Spiral[j];
      Loc loc = Actor.Loc with { Row = Actor.Loc.Row + dr, Col = Actor.Loc.Col + dc };

      affectedLocs.Add(loc);

      if (GameState.ObjDb.Occupant(loc) is Actor victim && !victim.HasTrait<HeavyTrait>())
        movedObjects.Add(victim.ID, j);

      foreach (Item item in GameState.ObjDb.ItemsAt(loc))
      {
        if (MoveableItem(item))
          movedObjects.Add(item.ID, j);
      }

      if (GameState.TileAt(loc).IsWater()) 
      {
        int alpha = int.Min(255, 100 + (j * 2)); // Fades from opaque at center to transparent at edge
        Colour bg = Colours.BLUE with { Alpha = alpha };
        Colour fg = Colours.ICE_BLUE with { Alpha = alpha };
        ui.RegisterAnimation(new SqAnimation(GameState, loc, fg, bg, '~'));
      }
    }

    static bool MoveableItem(Item item)
    {
      if (item.Type == ItemType.Environment || item.Type == ItemType.Landscape || item.Type == ItemType.Ink || item.Type == ItemType.Fog)
        return false;

      if (item.HasTrait<AffixedTrait>() || item.HasTrait<HeavyTrait>())
        return false;

      return true;
    }

    foreach (ulong objId in movedObjects.Keys) 
    {
      MoveObject(objId, movedObjects[objId], affectedLocs, GameState);
    }

    return 1.0;
  }

  static void MoveObject(ulong objId, int startIndex, List<Loc> affectedLocs, GameState gs)
  {
    if (gs.ObjDb.GetObj(objId) is not GameObj obj)
      return;

    string blockerName = "";
    bool collision = false;
    int j = startIndex - 1;
    while (j > 0)
    {
      Tile tile = gs.TileAt(affectedLocs[j]);
      if (!tile.IsWater()) 
      {
        collision = (startIndex - j > 1) && !(tile.Passable() || tile.PassableByFlight());
        blockerName = Tile.TileDesc(tile.Type);
        ++j;
        break;
      }

      if (gs.ObjDb.Occupant(affectedLocs[j]) is Actor actor)
      {
        ++j;
        collision = startIndex - j > 1;
        blockerName = MsgFactory.CalcName(actor, gs.Player, Article.Def);
        break;
      }

      List<Item> blockers = [.. gs.ObjDb.BlockersAtLoc(affectedLocs[j])];
      if (blockers.Count > 0)
      {
        ++j;
        collision = startIndex - j > 1;
        blockerName = MsgFactory.CalcName(blockers[0], gs.Player, Article.Def);
        break;
      }

      --j;
    }

    if (j == 0)
      j = 1;
    
    Loc finalLoc = affectedLocs[j];
    string s = $"{obj.FullName.Capitalize()} {Grammar.Conjugate(obj, "is")} caught in the vortex!"; 
    gs.UIRef().AlertPlayer(s, gs, finalLoc);

    if (collision) 
    {
      s = $"{obj.FullName.Capitalize()} {Grammar.Conjugate(obj, "collide")} with {blockerName}!";
      gs.UIRef().AlertPlayer(s, gs, finalLoc);
    }
    
    if (obj is Actor victim)
    {
      gs.ResolveActorMove(victim, victim.Loc, finalLoc);

      if (collision)
      {
        int d = gs.Rng.Next(2, 13);
        var (hpLeft, _, _) = victim.ReceiveDmg([(d, DamageType.Blunt)], 0, gs, null, 1.0);
        if (hpLeft < 1)
        {
          gs.ActorKilled(victim, "a collision", null);
        }
      }
    }
    else if (obj is Item item)
    {
      gs.ObjDb.RemoveItemFromLoc(item.Loc, item);
      gs.ItemDropped(item, finalLoc);
    }
  }
}

class InduceNudityAction(GameState gs, Actor caster, int range) : Action(gs, caster)
{
  int Range { get; set; } = range;

  public override double Execute()
  {
    base.Execute();
    UserInterface ui = GameState!.UIRef();

    if (GameState!.LastPlayerFoV.ContainsKey(GameState!.Player.Loc))
    {
      string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "dance")} a peculiar jig.";
      ui.AlertPlayer(s);

      Loc targetLoc = Actor.PickRangedTargetLoc(GameState, Range);
      if (GameState.ObjDb.Occupant(targetLoc) is Actor victim)
      {
        List<Item> clothes = [.. victim.Inventory.Items().Where(i => i.Type == ItemType.Armour && i.Equipped)];
        if (clothes.Count == 0)
          return 1.0;

        Item item = clothes[GameState.Rng.Next(clothes.Count)];
        s = $"{item.FullName.Possessive(victim).Capitalize()} falls off!";
        if (GameState.LastPlayerFoV.ContainsKey(victim.Loc))
          ui.AlertPlayer(s);

        victim.Inventory.RemoveByID(item.ID, GameState);

        GameState.ItemDropped(item, victim.Loc);

        if (victim is Player)
          GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
      }
    }

    return 1.0;
  }
}

class DrainTorchAction(GameState gs, Actor caster, Loc target) : Action(gs, caster)
{
  readonly Loc _target = target;

  public override double Execute()
  {
    base.Execute();
    UserInterface ui = GameState!.UIRef();

    bool success = false;
    if (GameState!.ObjDb.Occupant(_target) is Actor victim)
    {
      foreach (var item in victim.Inventory.Items())
      {
        if (item.Traits.OfType<TorchTrait>().FirstOrDefault() is TorchTrait torch)
        {
          if (torch.Lit && torch.Fuel > 0)
          {
            int drain = GameState.Rng.Next(350, 751);
            torch.Fuel = int.Max(0, torch.Fuel - drain);
            string s = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "drain")}";
            ui.AlertPlayer(s + $" {item.FullName.Possessive(victim)}!");
            success = true;
          }
        }
      }
    }
    
    if (!success)
    {
      ui.AlertPlayer("The spell fizzles.");
    }

    return 1.0;
  }
}

class EntangleAction(GameState gs, Actor caster, int range) : Action(gs, caster)
{
  int Range { get; set; } = range;

  public override double Execute()
  {
    Loc targetLoc = Actor!.PickRangedTargetLoc(GameState!, Range);
    foreach (var (r, c) in Util.Adj8Sqs(targetLoc.Row, targetLoc.Col))
    {
      var loc = targetLoc with { Row = r, Col = c };
      var tile = GameState!.TileAt(loc);
      if (tile.Type != TileType.Unknown && tile.Passable() && !GameState.ObjDb.Occupied(loc))
      {
        Actor vines = MonsterFactory.Get("vines", GameState.ObjDb, GameState.Rng);
        vines.Loc = loc;
        GameState.ObjDb.Add(vines);
        GameState.ObjDb.SetActorToLoc(loc, vines.ID);
      }
    }

    string txt = $"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "cast")} Entangle!";
    GameState!.UIRef().AlertPlayer(txt);

    return 1.0;
  }
}

class FireboltAction(GameState gs, Actor caster, Loc target) : Action(gs, caster)
{
  readonly Loc _target = target;

  public override double Execute()
  {
    string txt = $"{MsgFactory.CalcName(Actor!, GameState!.Player).Capitalize()} {Grammar.Conjugate(Actor!, "cast")} firebolt!";
    GameState.UIRef().AlertPlayer(txt);

    Item firebolt = ItemFactory.Get(ItemNames.FIREBOLT, GameState!.ObjDb);
    Actor!.QueueAction(new MissileAttackAction(GameState, Actor!, _target, firebolt));

    return 0.0;
  }
}

class WebAction : Action
{
  Loc Target { get; set; }

  public WebAction(GameState gs, Loc target)
  {
    GameState = gs;
    Target = target;
  }

  public override double Execute()
  {
    Item w = ItemFactory.Web();
    GameState!.ObjDb.Add(w);
    GameState.ItemDropped(w, Target);

    foreach (Loc adj in Util.Adj8Locs(Target))
    {
      if (GameState.TileAt(adj).Passable() && GameState.Rng.NextDouble() < 0.666)
      {
        w = ItemFactory.Web();
        GameState.ObjDb.Add(w);
        GameState.ItemDropped(w, Target with { Row = adj.Row, Col = adj.Col });
      }
    }

    if (GameState.ObjDb.Occupant(Target) is Actor victim)
    {
      string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught up in webs!";
      GameState!.UIRef().AlertPlayer(txt, GameState, Target);
    }

    return 1.0;
  }
}

class EscapeDungeonAction(GameState gs) : Action(gs, gs.Player)
{
  public override double Execute()
  {
    base.Execute();

    var player = GameState!.Player;
    if (player.HasTrait<RecallTrait>() || GameState.InWilderness)
    {
      GameState!.UIRef().AlertPlayer("You shudder for a moment.");
      return 1.0;
    }

    ulong happensOn = GameState.Turn + (ulong) GameState.Rng.Next(1, 6);
    var recall = new RecallTrait()
    {
      ExpiresOn = happensOn,
      ObjId = player.ID
    };

    GameState.RegisterForEvent(GameEventType.EndOfRound, recall);
    GameState.Player.Traits.Add(recall);
    GameState.UIRef().AlertPlayer("The air crackles around you.");

    return 1.0;
  }
};

class KnockAction(GameState gs, Actor caster) : Action(gs, caster)
{  
  public override double Execute()
  {
    base.Execute();

    if (Actor is Actor caster)
    {
      GameState!.UIRef().AlertPlayer("You hear a spectral knocking.");

      var sqs = GameState!.Flood(caster.Loc, 4, true);
      foreach (Loc sq in sqs)
      {
        Tile tile = GameState.TileAt(sq);
        if (tile.Type == TileType.LockedDoor || tile.Type == TileType.SecretDoor)
        {
          GameState.UIRef().AlertPlayer("Click!");
          GameState.CurrentMap.SetTile(sq.Row, sq.Col, TileFactory.Get(TileType.ClosedDoor));
        }
      }

      MagicMapAnimation anim = new(GameState, GameState.CurrentDungeon, [.. sqs]);
      GameState.UIRef().RegisterAnimation(anim);
    }
    
    return 1.0;
  }
}

class DescentAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override double Execute()
  {
    Loc loc = Actor!.Loc;
    Map map = GameState!.Campaign.Dungeons[loc.DungeonID].LevelMaps[loc.Level];

    if (GameState!.InWilderness || map.HasFeature(MapFeatures.UndiggableFloor))
    {
      GameState.UIRef().AlertPlayer("A tiny, nearly imperceptible divot forms.", GameState, loc);
    }
    else
    {
      Tile tile = GameState.TileAt(loc);
      string msg = "A chasm forms!";
      bool water = map.HasFeature(MapFeatures.Submerged) || tile.Type == TileType.Lake;
      if (water)
        msg = "A whirlpool forms!";
      GameState.UIRef().AlertPlayer(msg, GameState, loc);
      Item chasm = ItemFactory.Chasm(GameState);
      GameState.ObjDb.SetToLoc(loc, chasm);
      GameState.ChasmCreated(loc, water);
    }

    return 1.0;
  }
}

class BindSpellAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override double Execute()
  {
    Loc loc = GameState!.Player.Loc;
    bool bindingAura = GameState.Player.Inventory.Items()
                            .Any(i => i.Traits.OfType<BindingTrait>().FirstOrDefault() is BindingTrait bt && bt.Lit);

    int shackleCount = 0;
    if (GameState.FactDb.FactCheck("ShacklesCharged") is SimpleFact fact)
    {
      shackleCount = int.Parse(fact.Value);
      GameState.FactDb.ClearFact(fact);
    }

    bool shackleActivated = false;
    string msg = "The Sorceress conveniently left bookmarked a powerful binding incantation, which you read aloud.";
    // Is the player positioned to engage a shackle?
    Tile tile = GameState.CurrentMap.TileAt(loc.Row + 1, loc.Col + 1);
    if (bindingAura && tile is Shackle shackle1 && !shackle1.Activated)
    {
      msg += "\n\nThe chains begin to glow with power!";
      shackle1.Activated = true;
      shackle1.Glyph = shackle1.Glyph with { Lit = Colours.ICE_BLUE, Unlit = Colours.LIGHT_BLUE };
      shackleActivated = true;
      Loc shackleLoc = loc with { Row = loc.Row + 1, Col = loc.Col + 1};
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, shackleLoc, Colours.WHITE, Colours.ICE_BLUE, '\\'));
      ++shackleCount;
    }

    tile = GameState.CurrentMap.TileAt(loc.Row + 1, loc.Col - 1);
    if (bindingAura && tile is Shackle shackle2 && !shackle2.Activated)
    {
      msg += "\n\nThe chains begin to glow with power!";
      shackle2.Activated = true;
      shackle2.Glyph = shackle2.Glyph with { Lit = Colours.ICE_BLUE, Unlit = Colours.LIGHT_BLUE };
      shackleActivated = true;
      Loc shackleLoc = loc with { Row = loc.Row + 1, Col = loc.Col - 1};
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, shackleLoc, Colours.WHITE, Colours.ICE_BLUE, '/'));
      ++shackleCount;
    }

    tile = GameState.CurrentMap.TileAt(loc.Row - 1, loc.Col - 1);
    if (bindingAura && tile is Shackle shackle3 && !shackle3.Activated)
    {
      msg += "\n\nThe chains begin to glow with power!";
      shackle3.Activated = true;
      shackle3.Glyph = shackle3.Glyph with { Lit = Colours.ICE_BLUE, Unlit = Colours.LIGHT_BLUE };
      shackleActivated = true;
      Loc shackleLoc = loc with { Row = loc.Row - 1, Col = loc.Col - 1};
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, shackleLoc, Colours.WHITE, Colours.ICE_BLUE, '\\'));
      ++shackleCount;
    }

    tile = GameState.CurrentMap.TileAt(loc.Row - 1, loc.Col + 1);
    if (bindingAura && tile is Shackle shackle4 && !shackle4.Activated)
    {
      msg += "\n\nThe chains begin to glow!";
      shackle4.Activated = true;
      shackle4.Glyph = shackle4.Glyph with { Lit = Colours.ICE_BLUE, Unlit = Colours.LIGHT_BLUE };
      shackleActivated = true;
      Loc shackleLoc = loc with { Row = loc.Row - 1, Col = loc.Col + 1};
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, shackleLoc, Colours.WHITE, Colours.ICE_BLUE, '/'));
      ++shackleCount;
    }
    
    GameState.FactDb.Add(new SimpleFact() { Name = "ShacklesCharged", Value = shackleCount.ToString()});
    if (!shackleActivated)
      msg += "\n\n...to little effect.";

    if (shackleCount == 4)
    {
      throw new VictoryException();
    }
    
    GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));  
    
    return 1.0;
  }
}

class BlinkAction(GameState gs, Actor caster) : Action(gs, caster)
{
  public override double Execute()
  {    
    // If the caster is currently swallowed, we want to remove the swallwed 
    // trait before resolving the blink so that we're blinking to a loc in the
    // real level. (This might have problems with weird interactions were, like,
    // the player was standing in lava, then got swallowed, then blinked. 
    // Technically the player would land on the lava sq then blink, which we 
    // might not want. Problem for future Dana
    if (Actor!.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swallowed)
    {
      swallowed.Remove(GameState!);
    }

    List<Loc> sqs = [];
    Loc start = Actor!.Loc;
    Map map = GameState!.Campaign.Dungeons[start.DungeonID].LevelMaps[start.Level];
    bool submered = map.HasFeature(MapFeatures.Submerged);
    for (int r = start.Row - 12; r < start.Row + 12; r++)
    {
      for (int c = start.Col - 12; c < start.Col + 12; c++)
      {
        Loc loc = start with { Row = r, Col = c };
        int d = Util.Distance(start, loc);
        if (d < 8 || d > 12 || GameState.ObjDb.Occupied(loc))
          continue;

        Tile tile = GameState!.TileAt(loc);
        if (tile.Passable())
          sqs.Add(loc);
        else if (submered && tile.IsWater())
          sqs.Add(loc);
      }
    }

    if (sqs.Count == 0)
    {
      GameState!.UIRef().AlertPlayer("A spell fizzles...");
      return 1.0;
    }
    else
    {
      Actor.ClearAnchors(GameState!);
      
      Loc landingSpot = sqs[GameState!.Rng.Next(sqs.Count)];
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, start, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));

      base.Execute();
      Actor.QueueAction(new MoveAction(GameState, Actor, landingSpot, false));
      if (GameState.LastPlayerFoV.ContainsKey(Actor.Loc))
        GameState.UIRef().AlertPlayer($"Bamf! {Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "blink")} away!");
      
      return 0.0;
    }
  }
}

class AntidoteAction(GameState gs, Actor target) : Action(gs, target)
{
  public override double Execute()
  {
    if (Actor is Player && !Actor.HasTrait<PoisonedTrait>())
    {
      GameState!.UIRef().AlertPlayer("That tasted not bad.");
      return 1.0;
    }

    foreach (var t in Actor!.Traits.OfType<PoisonedTrait>())
    {
      GameState!.StopListening(GameEventType.EndOfRound, t);
    }
    Actor.Traits = [..Actor.Traits.Where(t => t is not PoisonedTrait)];
    string msg = $"That makes {Actor.FullName} {Grammar.Conjugate(Actor, "feel")} better.";
    GameState!.UIRef().AlertPlayer(msg);

    return 1.0;
  }
}

class CureDisease(GameState gs, Actor target) : Action(gs, target)
{
  public override double Execute()
  {
    bool wasIll = false;
    List<DiseasedTrait> diseases = [.. Actor!.Traits.OfType<DiseasedTrait>()];
    foreach (DiseasedTrait disease in diseases)
    {
      disease.Remove(GameState!);
      Actor.Traits.Remove(disease);
      wasIll = true;
    }

    if (wasIll)
    {
      string msg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "have")} been cured!";
      GameState!.UIRef().AlertPlayer(msg);
    }

    return 1.0;
  }
}

class DrinkBoozeAction(GameState gs, Actor target) : Action(gs, target)
{
  public override double Execute()
  {
    base.Execute();
    UserInterface ui = GameState!.UIRef();

    bool canSeeLoc = GameState!.LastPlayerFoV.ContainsKey(Actor!.Loc);

    if (Actor is Player)
      ui.AlertPlayer("Glug! Glug! Glug!");
    else if (canSeeLoc)
      ui.AlertPlayer($"{Actor.FullName.Capitalize()} drinks some booze!");

    foreach (string s in Battle.HandleTipsy(Actor, GameState))
      ui.AlertPlayer(s);

    return 1.0;
  }
}

class HealAction(GameState gs, Actor target, int healDie, int healDice) : Action(gs, target)
{
  readonly int _healDie = healDie;
  readonly int _healDice = healDice;
  
  public override double Execute()
  {
    base.Execute();

    if (!Actor!.Stats.ContainsKey(Attribute.HP))
    {
      GameState!.UIRef().AlertPlayer("The spell seems to fizzle.", GameState, Actor.Loc);
      return 1.0;
    }

    Stat hpStat = Actor!.Stats[Attribute.HP];
    int hpBefore = hpStat.Curr;

    int hp = 0;

    if (_healDice == -1)
      hp = _healDie;
    else
    {
      for (int j = 0; j < _healDice; j++)
        hp += GameState!.Rng.Next(_healDie) + 1;
    }
    hpStat.Change(hp);
    int delta = hpStat.Curr - hpBefore;

    string txt;
    if (delta > 0 && Actor is Player)
      txt = $"You are healed for {delta} HP.";
    else if (delta > 0)
      txt = $"{Actor.FullName.Capitalize()} is healed.";
    else
      txt = "";
    GameState!.UIRef().AlertPlayer(txt, GameState, Actor.Loc);

    SqAnimation healAnim = new(GameState!, Actor.Loc, Colours.WHITE, Colours.PURPLE, '\u2665');
    GameState!.UIRef().RegisterAnimation(healAnim);

    if (Actor.Traits.OfType<LameTrait>().FirstOrDefault() is LameTrait lame)
    {
      lame.Remove(GameState, Actor);
    }

    return 1.0;
  }
}

class DestressAction(GameState gs, Actor target, int amount) : Action(gs, target)
{
  int Amount { get; init; } = amount;

  public override double Execute()
  {
    base.Execute();
    double cost = 0.0;
    
    if (Actor is not null && Actor.Stats.TryGetValue(Attribute.Nerve, out var nerve))
    {
      nerve.Change(50);

      if (Actor is Player)
        GameState!.UIRef().AlertPlayer("You feel calmer.");
      cost = 1.0;
    }

    return cost;
  }
}

class SootheAction(GameState gs, Actor target, int amount) : Action(gs, target)
{
  int Amount { get; set; } = amount;
  
  public override double Execute()
  {
    base.Execute();
    
    if (Actor!.Stats.TryGetValue(Attribute.Nerve, out Stat? nerve))
    {
      GameState!.UIRef().AlertPlayer($"{Actor!.FullName.Capitalize()} {Grammar.Conjugate(Actor, "feel")} more calm.");
      SqAnimation anim = new(GameState!, Actor.Loc, Colours.WHITE, Colours.SOPHIE_GREEN, '\u2665');
      GameState!.UIRef().RegisterAnimation(anim);

      nerve.Change(Amount);
    }
    
    return 1.0;
  }
}

class DropZorkmidsAction(GameState gs, Actor actor) : Action(gs, actor)
{
  int _amount;

  public override double Execute()
  {
    double energyCost = 1.0;
    string msg;
    
    Inventory inventory = Actor!.Inventory;
    if (_amount > inventory.Zorkmids)
    {
      _amount = inventory.Zorkmids;
    }

    if (_amount == 0)
    {
      energyCost = 0.0; // we won't make the player spend an action if they drop nothing
      GameState!.UIRef().AlertPlayer("You hold onto your zorkmids.");      
    }
    else
    {      
      Item coins = ItemFactory.Get(ItemNames.ZORKMIDS, GameState!.ObjDb);
      coins.Value = _amount;
      
      msg = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
      if (_amount == 1)
        msg += "a single zorkmid.";
      else if (_amount == inventory.Zorkmids)
        msg += "all your money!";
      else
        msg += $"{_amount} zorkmids.";
      GameState.UIRef().AlertPlayer(msg);

      GameState.ItemDropped(coins, Actor.Loc);
      if (Actor is Player && GameState.TileAt(Actor.Loc).Type == TileType.Well && coins.Value == 1)
      {
        GameState.UIRef().AlertPlayer("The coin disappears into the well and you hear a faint plop.");
        GameState.ObjDb.RemoveItemFromGame(Actor.Loc, coins);

        if (GameState.Rng.Next(100) < 5 && !Actor.HasTrait<AuraOfProtectionTrait>())
        {
          GameState.UIRef().AlertPlayer("A warm glow surrounds you!");
          Actor.Traits.Add(new AuraOfProtectionTrait());
        }
      }

      inventory.Zorkmids -= _amount;
    }

    return energyCost;
  }
  
  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
}

class DropStackAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  int _amount;

  public override double Execute()
  {
    GameState!.UIRef().ClosePopup();
    var (item, itemCount) = Actor!.Inventory.ItemAt(_slot);
    if (item is null)
      return 1.0; // This should never happen

    if (_amount == 0 || _amount > itemCount)
      _amount = itemCount;

    var droppedItems = Actor.Inventory.Remove(_slot, _amount, GameState!);
    foreach (var droppedItem in droppedItems)
    {
      GameState.ItemDropped(droppedItem, Actor.Loc);
      droppedItem.Equipped = false;
    }

    string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
    s += MsgFactory.CalcName(item, GameState.Player, _amount) + ".";
    GameState!.UIRef().AlertPlayer(s, GameState, Actor.Loc);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) => _amount = ((NumericUIResult)result).Amount;
}

class ThrowBombAction(GameState gs, Actor actor, Loc target) : Action(gs, actor)
{
  Loc Target { get; set; } = target;

  public override double Execute()
  {
    Item bomb = ItemFactory.Get(ItemNames.BOMB, GameState!.ObjDb);
    Cmd.ThrowItem(Actor!, bomb, Target, GameState!);
   
    return 1.0;
  }
}

class ThrowAction(GameState gs, Actor actor, char slot) : Action(gs, actor)
{
  readonly char _slot = slot;
  Loc Target { get; set; }

  public override double Execute()
  {
    var ammo = Actor!.Inventory.Remove(_slot, 1, GameState!).First();
    if (ammo != null)
    {
      Cmd.ThrowItem(Actor, ammo, Target, GameState!);
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) => Target = ((LocUIResult)result).Loc;
}

class FireSelectedBowAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }

  public override double Execute()
  {
    base.Execute();

    GameState!.ClearMenu();

    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null || item.Type != ItemType.Bow)
    {
      GameState.UIRef().AlertPlayer("That doesn't make any sense!");
    }
    else
    {
      PlayerCommandController.FireReadedBow(item, GameState);
    }

    return 0.0;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ThrowSelectionAction(GameState gs, Player player) : Action(gs, player)
{
  public char Choice { get; set; }
  
  public override double Execute()
  {
    GameState!.ClearMenu();
    var player = Actor as Player;

    var (item, _) = player!.Inventory.ItemAt(Choice);
    if (item is null)
    {
      GameState.UIRef().AlertPlayer("That doesn't make sense!");
      return 0.0;
    }
    else if (item.Type == ItemType.Armour && item.Equipped)
    {
      GameState.UIRef().AlertPlayer("You're wearing that!");
      return 0.0;
    }
    else if ((item.Type == ItemType.Ring || item.Type == ItemType.Talisman) && item.Equipped)
    {
      GameState.UIRef().AlertPlayer("You'll need to un-equip it first!");
      return 0.0;
    }

    ThrowAction action = new(GameState, player, Choice);
    int range = 7 + player.Stats[Attribute.Strength].Curr;
    if (range < 2)
      range = 2;
    Aimer acc = new(GameState, player.Loc, range) { DeferredAction = action };
    GameState.UIRef().SetInputController(acc);

    return 0.0;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class DropItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  public override double Execute()
  {
    GameState!.ClearMenu();
    UserInterface ui = GameState.UIRef();

    if (Choice == '$')
    {
      var inventory = Actor!.Inventory;
      if (inventory.Zorkmids == 0)
      {
        GameState.UIRef().AlertPlayer("You have no money!");
        return 0.0;
      }
      
      if (Actor is Player)
      {
        NumericInputer acc = new(GameState, "How much?")
        {
          DeferredAction = new DropZorkmidsAction(GameState, Actor)
        };
        ui.SetInputController(acc);

        return 0.0;
      }
      else
        // Will monsters ever just decide to drop money?
        return 0.0;
    }

    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      throw new Exception("Hmm this shouldn't happen!");

    if (item.Equipped && item.Type == ItemType.Armour)
    {
      GameState.UIRef().AlertPlayer("You cannot drop something you are wearing.");
      return 0.0;
    }
    if (item.Equipped && item.Type == ItemType.Ring)
    {
      GameState.UIRef().AlertPlayer("You'll need to take it off first.");
      return 0.0;
    }
    else if (item.Equipped && item.Type == ItemType.Talisman)
    {
      GameState.UIRef().AlertPlayer("You'll need to un-equip it first.");
      return 0.0;
    }
    else if (itemCount > 1 && Actor is Player)
    {
      DropStackAction dropStackAction = new(GameState, Actor, Choice);
      string prompt = $"Drop how many {item.FullName.Pluralize()}?\n(enter for all)";
      ui.SetPopup(new Popup(prompt, "", -1, -1));
      ui.SetInputController(new NumericInputer(GameState, prompt) { DeferredAction = dropStackAction });
      return 0.0;

      // When monsters can drop stuff I guess I'll have to handle that here??
    }
    else
    {
      string s = $"{MsgFactory.CalcName(Actor, GameState.Player).Capitalize()} {Grammar.Conjugate(Actor, "drop")} ";
      s += MsgFactory.CalcName(item, GameState.Player) + ".";
      ui.AlertPlayer(s);

      Actor.Inventory.Remove(Choice, 1, GameState);
      GameState.ItemDropped(item, Actor.Loc);
      item.Equipped = false;

      return 1.0;
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ApplyPoisonAction(GameState gs, Actor actor, Item? item) : Action(gs, actor)
{
  public char Choice { get; set; }
  Item? SourceItem { get; set; } = item;

  public override double Execute()
  {
    base.Execute();

    GameState!.ClearMenu();

    var (item, _) = Actor!.Inventory.ItemAt(Choice);

    if (item != null)
    {
      string objName = item.FullName.DefArticle();

      item.Traits.Add(new PoisonCoatedTrait());
      item.Traits.Add(new AdjectiveTrait("poisoned"));
      item.Traits.Add(new PoisonerTrait() { DC = 15, Strength = 2, Duration = 10 });

      string name = Actor.FullName.Capitalize();
      string verb = Grammar.Conjugate(Actor, "smear");      
      string s = $"{name} {verb} some poison on {objName}.";
      GameState.UIRef().AlertPlayer(s);

      if (SourceItem is not null && SourceItem.HasTrait<ConsumableTrait>())
      {
        Actor.Inventory.ConsumeItem(SourceItem, Actor, GameState);
      }
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ConsumeAlchemicalCompound(GameState gs, Actor actor, Item item) : Action(gs, actor)
{
  Item SourceItem { get; set; } = item;

  public override double Execute()
  {
    base.Execute();

    List<string> messages = ["You hold your nose and consume the noisome substance."];
    Actor!.Inventory.ConsumeItem(SourceItem, Actor, GameState!);

    Attribute boosted = GameState!.Rng.Next(3) switch
    {
      0 => Attribute.Strength,
      1 => Attribute.Dexterity,
      _ => Attribute.Constitution
    };
    BoostMaxStatTrait b = new() { Stat = boosted, Amount = 2 };
    messages.AddRange(b.Apply(Actor, GameState));

    int roll = GameState.Rng.Next(10);
    switch (roll)
    {
      case 0:
        PoisonedTrait poison = new()
        {
          DC = 15,
          Strength = 2,
          Duration = GameState.Rng.Next(10, 21)
        };
        messages.AddRange(poison.Apply(Actor, GameState));
        break;
      case 1:
        messages.Add("Oof -- this burns!");
        int acidDmg = GameState.Rng.Next(10, 21);
        var (hpLeft, _, _) = Actor.ReceiveDmg([(acidDmg, DamageType.Acid)], 0, GameState, SourceItem, 1.0);
        if (hpLeft < 1)
        {
          foreach (string s in messages)
            GameState.UIRef().AlertPlayer(s);
          GameState.ActorKilled(Actor, "an alchemical experiment", null);
        }
        break;
      case 2:
        messages.Add("Mmm tastes like molasses.");
        ExhaustedTrait exhausted = new()
        {
          OwnerID = Actor.ID,
          ExpiresOn = GameState.Turn + (ulong)GameState.Rng.Next(50, 100)
        };
        messages.AddRange(exhausted.Apply(Actor, GameState));
        break;
      case 3:
        messages.Add($"{Grammar.Possessive(Actor)} skin turns transparent!");
        InvisibleTrait invis = new() { ExpiresOn = GameState.Turn + (ulong)GameState.Rng.Next(50, 100) };
        messages.AddRange(invis.Apply(Actor, GameState));
        break;
      default:
        messages.Add("You feel dizzy!");
        GameState.UIRef().AlertPlayer("You feel dizzy!");
        ConfusedTrait confused = new() { DC = 15 };
        messages.AddRange(confused.Apply(Actor, GameState));
        break;
    }
    
    foreach (string s in messages)
      GameState.UIRef().AlertPlayer(s);

    return 1.0;
  }
}

class ApplyStainlessnessAction(GameState gs, Actor actor, Item? item) : Action(gs, actor)
{
  public char Choice { get; set; }
  Item? SourceItem { get; set; } = item;

  public override double Execute()
  {
    base.Execute();

    GameState!.ClearMenu();

    var (item, _) = Actor!.Inventory.ItemAt(Choice);

    if (item != null)
    {
      string objName = item.FullName.DefArticle();
      string name = Actor.FullName.Capitalize();
      string verb = Grammar.Conjugate(Actor, "cast");
      string s = $"{name} {verb} a spell on {objName}.";
      GameState.UIRef().AlertPlayer(s);

      s = $"A glow envelops {objName}";
      if (!item.HasTrait<MetalTrait>() || item.HasTrait<RustProofTrait>())
        s += " but it seems to have little effect.";
      else
        s += " and it seems protected!";
      GameState.UIRef().AlertPlayer(s);

      item.Traits.Add(new RustProofTrait());

      // Still need to remove rust from currently rusted items
      if (item.HasTrait<RustedTrait>())
      {
        Effects.RemoveRust(item);
        GameState.UIRef().AlertPlayer($"{objName.Capitalize()} looks as good as new!");
      }

      if (SourceItem is not null && SourceItem.HasTrait<ConsumableTrait>())
      {
        Actor.Inventory.ConsumeItem(SourceItem, Actor, GameState);
      }
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class ToggleEquippedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }

  public override double Execute()
  {
    var (item, _) = Actor!.Inventory.ItemAt(Choice);
    GameState!.ClearMenu();

    if (item is null)
    {
      GameState.UIRef().AlertPlayer("You cannot equip that!");
      return 0.0;
    }

    if (!item.Equipable())
    {
      GameState.UIRef().AlertPlayer("You cannot equip that!");
      return 0.0;
    }

    double energyCost = 0.0;
    var (equipResult, conflict) = ((Player)Actor).Inventory.ToggleEquipStatus(Choice);
    string s;
    switch (equipResult)
    {
      case EquipingResult.Equipped:
        s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "ready")} {item.FullName.DefArticle()}";
        s += item.Type == ItemType.Wand ? " as a casting focus." : ".";
        GameState.UIRef().AlertPlayer(s);
        energyCost = 1.0;
        break;
      case EquipingResult.Unequipped:
        s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "remove")} {item.FullName.DefArticle()}.";
        GameState.UIRef().AlertPlayer(s);
        energyCost = 1.0;
        break;
      case EquipingResult.TwoHandedConflict:
        GameState.UIRef().AlertPlayer("You cannot wear a shield with a two-handed weapon!");
        break;
      case EquipingResult.ShieldConflict:
        GameState.UIRef().AlertPlayer("You cannot use a two-handed weapon with a shield!");
        break;
      case EquipingResult.TooManyRings:
        GameState.UIRef().AlertPlayer("You are already wearing two rings!");
        break;
      case EquipingResult.TooManyTalismans:
        GameState.UIRef().AlertPlayer("You may only use two talismans at a time!");
        break;
      case EquipingResult.NoFreeHand:
        GameState.UIRef().AlertPlayer("You have no free hands!");
        break;
      default:
        string msg = "You are already wearing ";
        msg += conflict switch
        {
          ArmourParts.Hat => "a helmet.",
          ArmourParts.Shield => "a shield.",
          ArmourParts.Boots => "boots.",
          ArmourParts.Cloak => "a cloak.",
          ArmourParts.Gloves => "some gloves",
          _ => "some armour."
        };
        GameState.UIRef().AlertPlayer(msg);
        break;
    }

    if (item.Traits.OfType<GrantsTrait>().FirstOrDefault() is GrantsTrait grants)
    {
      if (Item.IDInfo.TryGetValue(item.Name, out ItemIDInfo? value))
        Item.IDInfo[item.Name] = value with { Known = true };

      if (equipResult == EquipingResult.Equipped)
      {
        foreach (string msg in grants.Grant(Actor, GameState, item))
          GameState.UIRef().AlertPlayer(msg);
      }
      else if (equipResult == EquipingResult.Unequipped)
      {
        grants.Remove(Actor, GameState, item);
      }
    }

    GameState.UIRef().SetInputController(new PlayerCommandController(GameState));

    return energyCost;
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}

class FireballAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;
  
  public override double Execute()
  {
    base.Execute();
    
    // Fireball shoots toward the target and then explodes, but its path 
    // may be interrupted
    List<Loc> pts = [];
    Loc actualLoc = Target;
    foreach (var pt in Trajectory(Actor!.Loc, true))
    {            
      actualLoc = pt;
      pts.Add(pt);

      if (GameState!.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
        break;      
    }

    var ui = GameState!.UIRef();

    var anim = new ArrowAnimation(GameState!, pts, Colours.BRIGHT_RED);
    ui.PlayAnimation(anim, GameState);
    
    var affected = GameState!.Flood(actualLoc, 3);
    affected.Add(actualLoc);

    ExplosionAnimation explosion = new(GameState!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = actualLoc,
      Sqs = affected
    };
    ui.PlayAnimation(explosion, GameState);
    
    int total = 0;
    for (int j = 0; j < 4; j++)
      total += GameState.Rng.Next(6) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Fire)];
    foreach (var pt in affected)
    {
      Effects.ApplyDamageEffectToLoc(pt, DamageType.Fire, GameState);
      if (GameState.ObjDb.Occupant(pt) is Actor victim)
      {
        GameState.UIRef().AlertPlayer($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!");

        var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 0, GameState, null, 1.0);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "a fireball", null);
        }
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of fireballs"] = Item.IDInfo["wand of fireballs"] with { Known = true };
      wand.Used();
    }
    else if (_source is ICharged charged)
    {
      charged.Used();
    }

    return 1.0;
  }
}

class RayOfSlownessAction(GameState gs, Actor actor, Trait src, ulong sourceId) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;
  readonly ulong SourceId = sourceId;

  public override double Execute()
  {
    base.Execute();
    
    Item ray = new()
    {
      Name = "ray of slowness",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.FADED_PURPLE, Colours.FADED_PURPLE, Colours.BLACK, false)
    };
    GameState!.ObjDb.Add(ray);

    List<Loc> pts = [.. Trajectory(Actor!.Loc, true).Skip(1)];
    var anim = new BeamAnimation(GameState!, pts, Colours.FADED_PURPLE, Colours.BLACK);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    // The AlacrityTrait never expires, I don't think. Hmm... Did I create this
    // before I created TemporaryTrait. It's not really a problem right now now
    // because the player should never be hit by a ray of slowness, but 
    // eventually monsters might be able to use wands.
    foreach (Loc loc in pts)
    {
      if (GameState.ObjDb.Occupant(loc) is Actor victim)
      {
        bool alreadyAffected = false;
        foreach (Trait t in victim.Traits)
        {
          // They can only be affected once by the same wand
          if (t is AlacrityTrait at && at.SourceId == SourceId)
          {
            alreadyAffected = true;
            break;
          }
        }

        if (!alreadyAffected)
        {
          string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "begin")} to move slower.";
          GameState.UIRef().AlertPlayer(s, GameState, loc, victim);            
          victim.Traits.Add(new AlacrityTrait() { Amt = -0.5, SourceId = SourceId });
        }
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of slow monster"] = Item.IDInfo["wand of slow monster"] with { Known = true };
      wand.Used();
    }
    else if (_source is ICharged charged)
    {
      charged.Used();
    }

    return 1.0;
  }
}

class DigRayAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait Source = src;

  public override double Execute()
  {
    base.Execute();

    Loc casterLoc = Actor!.Loc;
    if (Util.Distance(casterLoc, Target) < 6)
    {
      (int, int) endPt = Util.ExtendLine(casterLoc.Row, casterLoc.Col, Target.Row, Target.Col, 6);
      Target = casterLoc with { Row = endPt.Item1, Col = endPt.Item2 };
    }
    List<Loc> pts = [.. Trajectory(Actor!.Loc, false).Take(7)];

    BeamAnimation anim = new(GameState!, pts, Colours.LIGHT_BROWN, Colours.WHITE);

    UserInterface ui = GameState!.UIRef();
    ui.PlayAnimation(anim, GameState);

    if (Actor is Player)
      ui.AlertPlayer("You hear a zap and the faint, ghostly humming of unseen dwarven miners!");

    bool stop = false;
    foreach (Loc loc in pts)
    {
      if (!GameState.CurrentMap.InBounds(loc.Row, loc.Col))
        break;
      Tile tile = GameState.TileAt(loc);
      switch (tile.Type)
      {
        case TileType.PermWall:
          stop = true;
          break;
        case TileType.DungeonWall:
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
          break;
        case TileType.StoneWall:
        case TileType.WoodWall:
        case TileType.HWindow:
        case TileType.VWindow:
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Dirt));
          break;
      }

      if (stop)
        break;

      var blockages = GameState.ObjDb.ItemsAt(loc).Where(i => i.Type == ItemType.Landscape);
      foreach (Item block in blockages)
        GameState.ObjDb.RemoveItemFromGame(loc, block);
    }

    if (Source is WandTrait wand)
    {
      Item.IDInfo["wand of digging"] = Item.IDInfo["wand of digging"] with { Known = true };
      wand.Used();
    }
    else if (Source is ICharged charged)
    {
      charged.Used();
    }

    return 1.0;
  }
}

class FrostRayAction(GameState gs, Actor actor, Trait src) : TargetedAction(gs, actor)
{
  readonly Trait _source = src;

  public override double Execute()
  {
    base.Execute();
    
    Item ray = new()
    {
      Name = "ray of frost",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.LIGHT_BLUE, Colours.BLUE, Colours.BLACK, false)
    };
    ray.Traits.Add(new DamageTrait() { DamageDie = 4, NumOfDie = 3, DamageType = DamageType.Cold });
    GameState!.ObjDb.Add(ray);

    // Ray of frost is a beam so unlike things like magic missle, it doesn't stop 
    // when it hits an occupant.
    List<Loc> pts = Trajectory(Actor!.Loc, true);

    BeamAnimation anim = new(GameState!, pts, Colours.LIGHT_BLUE, Colours.WHITE);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    foreach (var pt in pts)
    {
      Effects.ApplyDamageEffectToLoc(pt, DamageType.Cold, GameState);

      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        int attackMod = 6;
        Battle.MagicAttack(Actor!, occ, GameState, ray, attackMod, null);
      }
    }

    if (_source is WandTrait wand)
    {
      Item.IDInfo["wand of frost"] = Item.IDInfo["wand of frost"] with { Known = true };
      wand.Used();
    }
    else if (_source is ICharged charged)
    {
      charged.Used();
    }

    return 1.0;
  }
}

class MagicMissleAction(GameState gs, Actor actor, Trait? src) : TargetedAction(gs, actor)
{
  Trait? Source { get; set; } = src;
  
  public int DamageDie { get; set; } = 6;
  public int NumOfDie { get; set; } = 2;

  public override double Execute()
  {
    base.Execute();
    
    Item missile = new()
    {
      Name = "magic missile",
      Type = ItemType.Weapon,
      Glyph = new Glyph('-', Colours.LIGHT_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, false)
    };
    missile.Traits.Add(new DamageTrait() { DamageDie = DamageDie, NumOfDie = NumOfDie, DamageType = DamageType.Force });
    GameState!.ObjDb.Add(missile);

    List<Loc> pts = [];
    // I think I can probably clean this crap up
    foreach (var pt in Trajectory(Actor!.Loc, false))
    {
      var tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);

        // I didn't want magic missile to be auto-hit like in D&D, but I'll give it a nice
        // attack bonus
        int attackMod = 5;
        bool attackSuccessful = Battle.MagicAttack(Actor!, occ, GameState, missile, attackMod, new ArrowAnimation(GameState!, pts, Colours.LIGHT_BLUE));        
        if (attackSuccessful)
        {
          pts = [];
          break;
        }
      }
      else if (tile.Passable() || tile.PassableByFlight())
      {
        pts.Add(pt);
      }
      else
      {
        break;
      }
    }
   
    if (Source is WandTrait wand)
    {
      Item.IDInfo["wand of magic missiles"] = Item.IDInfo["wand of magic missiles"] with { Known = true };
      wand.Used();
    }
    else if (Source is ICharged charged) 
    {
      charged.Used();
    }

    ArrowAnimation anim = new(GameState!, pts, Colours.LIGHT_BLUE);
    GameState!.UIRef().PlayAnimation(anim, GameState);
    GameState!.UIRef().AlertPlayer("Pew pew pew!");

    return 1.0;
  }
}

abstract class TargetedAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public Loc Target { get; set; }

  // As in, clear of obstacles, not opacity
  bool ClearTileAt(Loc loc)
  {
    Tile tile = GameState!.TileAt(loc);
    if (!(tile.Passable() || tile.PassableByFlight()))
      return false;
    if (GameState.ObjDb.AreBlockersAtLoc(loc))
      return false;

    return true;
  }

  protected List<Loc> Trajectory(Loc origin, bool filterBlockers)
  {
    if (filterBlockers)
      return [.. Util.LerpLine(origin.Row, origin.Col, Target.Row, Target.Col)
               .Select(p => new Loc(origin.DungeonID, origin.Level, p.Item1, p.Item2))
               .Where(l => ClearTileAt(l))];
    else
      return [.. Util.LerpLine(origin.Row, origin.Col, Target.Row, Target.Col)
                 .Select(p => new Loc(origin.DungeonID, origin.Level, p.Item1, p.Item2))];
  }

  public override void ReceiveUIResult(UIResult result) => Target = ((LocUIResult)result).Loc;
}

class SwapWithMobAction(GameState gs, Actor actor, Trait src) : Action(gs, actor)
{
  // We'll display different messages if the swapping is from magic or just
  // asking an NPC to swap places.
  public bool Mundane { get; set; } = false;

  readonly Trait _source = src;
  Loc _target;

  public override double Execute()
  {
    base.Execute();
    
    if (GameState!.ObjDb.Occupant(_target) is Actor victim)
    {
      if (_source is WandTrait wand)
      {
        Item.IDInfo["wand of swap"] = Item.IDInfo["wand of swap"] with { Known = true };
        wand.Used();
      }

      if (Actor!.ID == victim.ID)
      {        
        var txt = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "feel")} a sense of vertigo followed by existential dread.";
        GameState.UIRef().AlertPlayer(txt);

        ConfusedTrait confused = new() { DC = 15 };
        confused.Apply(Actor, GameState);
      }
      else
      {
        GameState.SwapActors(Actor!, victim);
        string msg = "Bamf!";
        if (Mundane)
        {
          msg = $"{MsgFactory.CalcName(victim, GameState.Player).Capitalize()} {Grammar.Conjugate(victim, "step")} aside to let {MsgFactory.CalcName(Actor, GameState.Player)} pass.";
        }
        GameState.UIRef().AlertPlayer(msg);
      }      
    }
    else
    {
      if (_source is ICharged charged)
        charged.Used();
      GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class SwimAction(GameState gs, Actor actor, bool up) : Action(gs, actor)
{
  bool Up { get; set; } = up;

  public override double Execute()
  {
    base.Execute();

    int nextLevelNum = GameState!.CurrLevel;
    if (GameState!.CurrentDungeon.Descending && Up)
      nextLevelNum -= 1;
    else if (GameState.CurrentDungeon.Descending && !Up)
      nextLevelNum += 1;
    else if (!GameState.CurrentDungeon.Descending && Up)
      nextLevelNum += 1;
    else
      nextLevelNum -= 1;

    if (Up && (nextLevelNum < 0 || nextLevelNum == GameState.CurrentDungeon.LevelMaps.Count))
    {
      GameState!.UIRef().AlertPlayer("You cannot swim any further upward here.");

      return 0.0;
    }
    else if (!Up && (nextLevelNum < 0 || nextLevelNum == GameState.CurrentDungeon.LevelMaps.Count))
    {
      GameState!.UIRef().AlertPlayer("You cannot swim any deeper here.");

      return 0.0;
    }

    Map nextLevel = GameState.CurrentDungeon.LevelMaps[nextLevelNum];
    Loc nextLoc = GameState.Player.Loc with { Level = nextLevelNum };
    Tile nextTile = nextLevel.TileAt(nextLoc.Row, nextLoc.Col);
    if (Up && !(nextTile.Type == TileType.Underwater || nextTile.Type == TileType.Lake))
    {
      GameState.UIRef().AlertPlayer("You cannot swim further upward here.");

      return 0.0;
    }
    else if (!nextLevel.TileAt(nextLoc.Row, nextLoc.Col).Passable() && !(nextTile.Type == TileType.Underwater || nextTile.Type == TileType.Lake))
    {
      GameState.UIRef().AlertPlayer("You cannot swim deeper here.");

      return 0.0;
    }

    if (Up)
      GameState.UIRef().AlertPlayer("You swim upwards.");
    else
      GameState.UIRef().AlertPlayer("You swim into the depths.");

    GameState.ActorEntersLevel(GameState.Player, nextLoc.DungeonID, nextLoc.Level);
    GameState.ResolveActorMove(GameState.Player, GameState.Player.Loc, nextLoc);
    GameState.FlushPerformers();

    return 1.0;
  }
}

class CastHealMonster(GameState gs, Actor actor, Trait src) : Action(gs, actor)
{
  readonly Trait _source = src;
  Loc _target;

  public override double Execute()
  {
    base.Execute();
    double energyCost = 1.0;

    if (GameState!.ObjDb.Occupant(_target) is Actor target)
    {
      if (target is Player)
      {
        GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
      }
      else
      {
        Actor!.QueueAction(new HealAction(GameState, target, 6, 2));
        energyCost = 0.0;
      }

      if (_source is WandTrait wand)
      {
        Item.IDInfo["wand of heal monster"] = Item.IDInfo["wand of heal monster"] with { Known = true };
        wand.Used();
      }
    }
    else
    {
      GameState.UIRef().AlertPlayer("The magic is released but nothing happens. The spell fizzles.");
    }

    if (_source is ICharged charged)
      charged.Used();

    return energyCost;
  }

  public override void ReceiveUIResult(UIResult result) => _target = ((LocUIResult)result).Loc;
}

class InventoryChoiceAction(GameState gs, Actor actor, InventoryOptions opts, Action replacementAction) : Action(gs, actor)
{
  readonly InventoryOptions InvOptions = opts;
  Action ReplacementAction { get; set; } = replacementAction;

  public override double Execute()
  {
    base.Execute();

    if (Actor is Player player)
    {
      char[] slots = player.Inventory.UsedSlots();
      player.Inventory.ShowMenu(GameState!.UIRef(), InvOptions);
      Inventorier inputer = new(GameState!, [.. slots])
      {
        DeferredAction = ReplacementAction
      };
      GameState.UIRef().SetInputController(inputer);
    }

    return 1.0;
  }
}

class ScatterAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public override double Execute()
  {
    // Scatter can be used to escape from being swallowed, so if the casting 
    // actor is player and they are swallowed, treat this like a BlinkAction 
    // instead.
    if (Actor is Player && Actor.HasTrait<SwallowedTrait>())
    {
      Actor.QueueAction(new BlinkAction(GameState!, Actor));

      return 0.0;
    }

    List<Loc> affected = [];
    // I'm passing a new objdb because I am using CalcVisible() as a way to 
    // calculate a circular area of effect and I want to ignore objects like
    // fog and ink that reduce visibility.
    foreach (var kvp in FieldOfView.CalcVisible(4, Actor!.Loc, GameState!.CurrentMap, new GameObjectDB()))
    {
      if (kvp.Value != Illumination.Full)
        continue;
      if (kvp.Key == Actor.Loc || !GameState.ObjDb.Occupied(kvp.Key))
        continue;
      affected.Add(kvp.Key);
    }
    
    List<Loc> landingSpots = [];
    for (int r = 0; r < GameState.CurrentMap.Height; r++)
    {
      for (int c = 0; c < GameState.CurrentMap.Width; c++)
      {
        Loc loc = new(GameState.CurrDungeonID, GameState.CurrLevel, r, c);

        if (GameState.TileAt(loc).Passable() && !GameState.ObjDb.Occupied(loc))
          landingSpots.Add(loc);
      }
    }

    if (Actor is Player) 
    {
      GameState.UIRef().AlertPlayer("\"Aroint thee!\"");
    }
    else if (GameState.LastPlayerFoV.ContainsKey(Actor.Loc))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "read")} a scroll! Poof!";
      GameState.UIRef().AlertPlayer(s);
    }
      
    foreach (var loc in affected)
    {
      if (GameState.ObjDb.Occupant(loc) is not Actor victim)
        continue;

      int i = GameState.Rng.Next(landingSpots.Count);
      Loc landingSpot = landingSpots[i];
      landingSpots.RemoveAt(i);

      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, loc, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
      
      victim.ClearAnchors(GameState);
      GameState.ResolveActorMove(victim, loc, landingSpot);
    }

    return 1.0;
  }
}

class UseSpellItemAction(GameState gs, Actor actor, string spell, Item? item) : Action(gs, actor)
{
  string Spell { get; set; } = spell;
  Item? Item { get; set; } = item;

  public override double Execute()
  {
    Player player = GameState!.Player;
    
    switch (Spell)
    {
      case "gust of wind":
        ConeTargeter cone = new(GameState!, 5, player.Loc, [])
        {
          DeferredAction = new CastGustOfWindAction(GameState, player, Item) { FreeToCast = true }
        };
        GameState.UIRef().SetInputController(cone);
        break;
    }

    return 0.0;
  }
}

class UseWandAction(GameState gs, Actor actor, WandTrait wand, ulong wandId) : Action(gs, actor)
{
  readonly WandTrait _wand = wand;

  public override double Execute()
  {
    if (Actor is not Player player)
      throw new Exception("Boy did something sure go wrong!");

    GameState gs = GameState!;
    Inputer inputer;
    switch (_wand.Effect)
    {
      case "magicmissile":
        inputer = new Aimer(GameState!, player.Loc, 7)
        {
          DeferredAction = new MagicMissleAction(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "fireball":
        inputer = new Aimer(GameState!, player.Loc, 12)
        {
          DeferredAction = new FireballAction(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "swap":
        inputer = new Aimer(GameState!, player.Loc, 25)
        {
          DeferredAction = new SwapWithMobAction(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "healmonster":
        inputer = new Aimer(GameState!, player.Loc, 7)
        {
          DeferredAction = new CastHealMonster(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "frost":
        inputer = new Aimer(GameState!, player.Loc, 7)
        {
          DeferredAction = new FrostRayAction(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "slowmonster":
        inputer = new Aimer(GameState!, player.Loc, 9)
        {
          DeferredAction = new RayOfSlownessAction(GameState!, player, _wand, wandId)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "digging":
        inputer = new Aimer(GameState!, player.Loc, 6)
        {
          DeferredAction = new DigRayAction(GameState!, player, _wand)
        };
        gs.UIRef().SetInputController(inputer);
        break;
      case "summoning":
        SetupSummoning();
        break;
    }
    
    return 0.0;
  }

  void SetupSummoning()
  {
    // Kind of dorky to hardcore it, but otherwise I'd have to query ObjDb by the wand item ID to 
    // get it's name and currently it is always going to be wand of summoning...
    Item.IDInfo["wand of summoning"] = Item.IDInfo["wand of summoning"] with { Known = true };
    _wand.Used();
    
    // We don't need to replace the player's pending action here because 
    // there's no input needed from the player
    SummonAction summon = new(Actor!.Loc, GameState!.RandomMonster(Actor.Loc.DungeonID), 1)
    {
      GameState = GameState,
      Actor = Actor
    };
    Actor.QueueAction(summon);
  }
}

sealed class PassAction : Action
{
  public PassAction() { }
  public PassAction(GameState? gs, Actor? actor)
  {
    GameState = gs;
    Actor = actor;
  }

  public sealed override double Execute()
  {
    base.Execute();

    return 1.0;
  }      
}

class HighlightLocAction(GameState gs, Actor actor) : Action(gs, actor)
{
  Loc Loc { get; set; }

  readonly Dictionary<string, Util.CyclopediaEntry> _cyclopedia = Util.LoadCyclopedia();

  public override double Execute()
  {
    var (r, c) = GameState!.UIRef().LocToScrLoc(Loc.Row, Loc.Col, Actor!.Loc.Row, Actor.Loc.Col);

    LocDetails details = LocInfo(Loc);
    Popup popup = new(details.Desc, details.Title, r - 2, c);
    if (details.HpCurr != -1 && details.HpMax != -1)
    {
      popup.BarLabel = "HP";
      popup.Colour1 = Colours.SHAMROCK;
      popup.Colour2 = Colours.BRIGHT_RED;
      popup.Value1 = details.HpCurr;
      popup.Value2 = details.HpMax;
    }

    GameState.UIRef().ZLayer[r, c] = new Sqr(Colours.WHITE, Colours.EXAMINE, details.Ch);
    GameState.UIRef().SetPopup(popup);

    return 0.0;
  }

  LocDetails LocInfo(Loc loc)
  {
    string name;
    string desc = "I have no further info about this object. This is probably Dana's fault.";

    int hpCurr = -1;
    int hpMax = -1;
    if (GameState!.ObjDb.Occupant(loc) is Actor actor)
    {
      if (actor is Player)
      {
        name = actor.Name;
        desc = "You. A stalwart, rugged adventurer (probably). Keen for danger and glory. Currently alive.";
      }
      else if (actor.IsDisguised())
      {
        DisguiseTrait dt = actor.Traits.OfType<DisguiseTrait>().First();
        name = dt.DisguiseForm.IndefArticle().Capitalize();
        if (_cyclopedia.TryGetValue(dt.DisguiseForm, out var v))
          desc = v.Text;
      }
      else if (actor.HasTrait<VillagerTrait>())
      {
        name = actor.FullName.Capitalize();
        desc = "A villager.";
      }
      else
      {
        name = actor.Name.IndefArticle().Capitalize();
        if (_cyclopedia.TryGetValue(actor.Name, out var v))
          desc = v.Text;
        if (actor.Traits.OfType<DescriptionTrait>().FirstOrDefault() is DescriptionTrait dt)
          desc = dt.Text;

        Stat hp = actor.Stats[Attribute.HP];
        hpCurr = hp.Curr;
        hpMax = hp.Max;
      }

      string extraInfo = "";
      foreach (Trait t in actor.Traits)
      {
        if (t is PoisonedTrait)
          extraInfo += "[GREEN Poisoned]. ";
        else if (t is ConfusedTrait)
          extraInfo += "[YELLOW Confused]. ";
        else if (t is SleepingTrait)
          extraInfo += "[PINK Sleeping]. ";
        else if (t is TipsyTrait)
          extraInfo += "[PINK Tipsy]. ";
        else if (t is FrightenedTrait)
          extraInfo += "[YELLOW Frightened]. ";
      }

      if (actor.Stats.TryGetValue(Attribute.MobAttitude, out var attitude))
      {
        if (attitude.Curr == Mob.INDIFFERENT)
          extraInfo += "[WHITE Indifferent]. ";
        else if (attitude.Curr == Mob.INACTIVE)
          extraInfo += "[WHITE Inactive]. ";
      }

      extraInfo = extraInfo.Trim();
      if (extraInfo.Length > 0)
      {
        desc += "\n\n" + extraInfo;
      }

      return new LocDetails(name, desc, actor.Glyph.Ch, hpCurr, hpMax);
    }

    if (GameState.CurrentDungeon.RememberedLocs.TryGetValue(loc, out var mem) && GameState.ObjDb.GetObj(mem.ObjId) is Item item)
    { 
      string title = item.FullName;
      if (item.HasTrait<PluralTrait>())
        title = title.Capitalize();
      else
        title = title.IndefArticle().Capitalize();
        
      string details = "";
      if (item.HasTrait<DescriptionTrait>())
        details = item.Traits.OfType<DescriptionTrait>().First().Text;
      else if (_cyclopedia.TryGetValue(item.Name, out var v))
        details = v.Text;

      return new LocDetails(title, details, item.Glyph.Ch);
    }

    List<Item> env = [.. GameState!.ObjDb.EnvironmentsAt(loc).Where(e => e.Name != "photon")];
    if (env.Count > 0)
    {
      Item envItem = env[0];
      string title = envItem.Name.Capitalize();
      string details = "";
      if (_cyclopedia.TryGetValue(envItem.Name, out var v))
      {
        title = v.Title;
        details = v.Text;
      }

      return new LocDetails(title, details, envItem.Glyph.Ch);
    }

    Tile tile = GameState!.TileAt(loc);
    name = tile.Type.ToString().ToLower();
    if (_cyclopedia.TryGetValue(name, out var v2))
    {
      name = v2.Title;
      desc = v2.Text;
    }

    return new LocDetails(name.Capitalize(), desc, Util.TileToGlyph(tile).Ch);
  }

  public override void ReceiveUIResult(UIResult result)
  {
    if (result is LocUIResult locResult)
      Loc = locResult.Loc;
  }
}

class CloseMenuAction : Action
{
  readonly double _energyCost;

  public CloseMenuAction(GameState gs, double energyCost = 0.0)
  {
    GameState = gs;
    _energyCost = energyCost;
  }

  public override double Execute()
  {
    GameState!.ClearMenu();
    return _energyCost;
  }
}

// I guess I can later add extra info about whether or not the player died, quit,
// or quit and saved?
class QuitAction : Action
{
  public override double Execute() => throw new QuitGameException();
}

class SaveGameAction : Action
{
  public override double Execute() => throw new SaveGameException();
}

class NullAction : Action
{
  public override double Execute() => throw new Exception("Hmm this should never happen");
}