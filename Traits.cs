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

record UseResult(bool Successful, string Message, Action? ReplacementAction, InputAccumulator? Accumulator);

interface IReadable
{
  void Read(Actor actor, UserInterface ui, Item document);
}

interface IUSeable
{
  UseResult Use(Actor user, GameState gs, int row, int col);
}

interface IEffectApplier
{
  string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc);
}

abstract class Trait
{
  public virtual bool Active => true;
  public abstract string AsText();
}

abstract class BasicTrait : Trait
{
  public virtual string Desc() => "";  
  public virtual bool Aura => false;
  public virtual TerrainFlag Effect => TerrainFlag.None;
  public virtual int Radius { get; set; } = 0;
  public ulong ExpiresOn { get; set; } = ulong.MaxValue;
  public override string AsText() => $"{ExpiresOn}#{Radius}";
}

abstract class EffectTrait : BasicTrait
{
  public abstract string Apply(Actor victim, GameState gs);
  public abstract bool IsAffected(Actor victim, GameState gs);
}

// To let me classify traits that mobs can take on their turns
// Not sure if this is the best way to go...
abstract class ActionTrait : BasicTrait
{
  // I was thinking I could use MinRange to set abilities a monster might use
  // from further away. Ie., gobin archer has one attack from distance 2 to 7
  // and another they use when they are in melee range.
  public int MinRange { get; set; } = 0;
  public int MaxRange { get; set; } = 0;
  public ulong Cooldown { get; set; } = 0;
  public string Name { get; set; } = "";

  public abstract bool Available(Mob mob, GameState gs);
  protected bool InRange(Mob mob, GameState gs)
  {
    int dist = Util.Distance(mob.Loc, gs.Player.Loc);
    return MinRange <= dist && MaxRange >= dist;
  }

  protected static bool ClearShot(GameState gs, IEnumerable<Loc> trajectory)
  {
    foreach (var loc in trajectory)
    {
      var tile = gs.TileAt(loc);
      if (!(tile.Passable() || tile.PassableByFlight()))
        return false;
    }

    return true;
  }

  public static List<Loc> Trajectory(Mob mob, Loc target)
  {
    return Util.Bresenham(mob.Loc.Row, mob.Loc.Col, target.Row, target.Col)
               .Select(sq => mob.Loc with { Row = sq.Item1, Col = sq.Item2 })
               .ToList();
  }
}

class SummonTrait : ActionTrait
{
  public string Summons { get; set; } = "";
  public string Quip { get; set; } = "";

  public override bool Available(Mob mob, GameState gs)
  {
    // I don't want them spamming the level with summons so they'll only
    // perform a summons if they are near the player
    if (Util.Distance(mob.Loc, gs.Player.Loc) > 3)
      return false;

    foreach (var adj in Util.Adj8Locs(mob.Loc))
    {
      if (!gs.ObjDb.Occupied(adj))
        return true;
    }

    return false;
  }
}

class ConfusingScreamTrait : ActionTrait
{
  public int DC { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Radius;
  }

  public override string AsText() => $"ConfusingScream#{Radius}#{DC}#{Cooldown}#";
}

class SpellActionTrait : ActionTrait
{
  public override string AsText() => $"SpellAction#{Name}#{MinRange}#{MaxRange}#{Cooldown}#";
  public override bool Available(Mob mob, GameState gs) => true;
}

class RangedSpellActionTrait : ActionTrait
{
  public override string AsText() => $"RangedSpellAction#{Name}#{MinRange}#{MaxRange}#{Cooldown}#";
  public override bool Available(Mob mob, GameState gs)
  {
    if (!InRange(mob, gs))
      return false;

    var p = gs.Player;
    return ClearShot(gs, Trajectory(mob, p.Loc));
  }
}

class MobMeleeTrait : ActionTrait
{
  public override string AsText() => $"MobMelee#{MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}#";
  public int DamageDie { get; set; }
  public int DamageDice { get; set; }
  public DamageType DamageType { get; set; }

  public override bool Available(Mob mob, GameState gs) => InRange(mob, gs);
}

class MobMissileTrait : ActionTrait
{
  public override string AsText() => $"MobMissile#{MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}#";
  public int DamageDie { get; set; }
  public int DamageDice { get; set; }
  public DamageType DamageType { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    if (!InRange(mob, gs))
      return false;

    var p = gs.Player;
    return ClearShot(gs, Trajectory(mob, p.Loc));
  }
}

class ConsumableTrait : Trait
{
  public override string AsText() => "Consumable";
}

class ImmuneConfusionTrait : Trait
{
  public override string AsText() => "ImmuneConfusion";
}

class Immunity : Trait
{
  public DamageType Type {  get; set; }

  public override string AsText() => $"Immunity#{Type}";
}

class ResistBluntTrait : Trait
{
  public override string AsText() => "ResistBlunt";
}

class ResistPiercingTrait : Trait
{
  public override string AsText() => "ResistPiercing";
}

class ResistSlashingTrait : Trait
{
  public override string AsText() => "ResistSlashing";
}

class PoorLootTrait : Trait
{
  public override string AsText() => "PoorLoot";
}

class StickyTrait : BasicTrait
{
  public int DC => 13;

  public override string AsText() => "Sticky";
}

class DividerTrait : Trait
{
  public override string AsText() => "Divider";
}

class FinalBossTrait : Trait
{
  public override string AsText() => "FinalBoss";
}

class FlammableTrait : Trait
{
  public override string AsText() => "Flammable";
}

class PlantTrait : Trait
{
  public override string AsText() => "Plant";
}

class PluralTrait : Trait
{
  public override string AsText() => "Plural";
}

class TeflonTrait : Trait
{
  public override string AsText() => "Teflon";
}

class TelepathyTrait : BasicTrait, IGameEventListener
{
  public ulong ActorID {  get; set; }
  public bool Expired { get; set; }
  public bool Listening => true;

  public override string AsText() => "Telepathy";

  void Remove(GameState gs)
  {
    var obj = gs.ObjDb.GetObj(ActorID);
    obj?.Traits.Remove(this);
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      Remove(gs);
    }    
  }
}

class VillagerTrait : Trait
{
  public override string AsText() => "Villager";
}

class WrittenTrait : Trait
{
  public override string AsText() => "Written";
}

class ImpaleTrait : Trait
{
  public override string AsText() => "Impale";
}

class CleaveTrait : Trait
{
  public override string AsText() => "Cleave";
}

class KnockBackTrait : Trait
{
  public override string AsText() => "KnockBack";
}

// For actors who go by a proper name
class NamedTrait : Trait
{
  public override string AsText() => "Named";
}

class RageTrait(Actor actor) : BasicTrait
{
  readonly Actor _actor = actor;

  public override bool Active
  {
    get
    {
      int currHP = _actor.Stats[Attribute.HP].Curr;
      int maxHP = _actor.Stats[Attribute.HP].Max;
      return currHP < maxHP / 2;
    }
  }

  public override string AsText() => "Rage";
}

// A bit dumb to have floating and flying and maybe I'll merge them
// eventually but at the moment floating creatures won't make noise
// while they move
class FloatingTrait : BasicTrait
{
  public FloatingTrait() { }
  public FloatingTrait(ulong expiry) => ExpiresOn = expiry;

  public override string AsText() => $"Floating#{ExpiresOn}";
}

class FlyingTrait : BasicTrait
{
  public FlyingTrait() { }
  public FlyingTrait(ulong expiry) => ExpiresOn = expiry;

  public override string AsText() => $"Flying#{ExpiresOn}";
}

class OpaqueTrait : BasicTrait
{
  public override string AsText() => "Opaque";
  public override TerrainFlag Effect => TerrainFlag.Obscures;
}

// Simple in that I don't need any extra info like a target to use the effect.
class UseSimpleTrait(string spell) : Trait, IUSeable
{
  public string Spell { get; set; } = spell;

  public override string AsText() => $"UseSimpleTrait#{Spell}";

  public UseResult Use(Actor user, GameState gs, int row, int col) => Spell switch
  {
    "antidote" => new UseResult(true, "", new AntidoteAction(gs, user), null),
    "blink" => new UseResult(true, "", new BlinkAction(gs, user), null),
    "minorheal" => new UseResult(true, "", new HealAction(gs, user, 4, 4), null),
    _ => throw new NotImplementedException($"{Spell.Capitalize()} is not defined!")
  };
}

class AttackTrait : BasicTrait
{
  public int Bonus { get; set; }

  public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";
  public override string AsText() => $"Attack#{Bonus}";
}

class DamageTrait : BasicTrait
{
  public int DamageDie { get; set; }
  public int NumOfDie { get; set; }
  public DamageType DamageType { get; set; }

  public override string AsText() => $"Damage#{DamageDie}#{NumOfDie}#{DamageType}";
  public override string Desc() => "";
  public override bool Aura => false;
}

class ACModTrait : BasicTrait
{
  public int ArmourMod { get; set; }
  public override string AsText() => $"ACMode#{ArmourMod}";
}

class ArmourTrait : ACModTrait
{
  public ArmourParts Part { get; set; }
  public int Bonus { set; get; }

  public override string Desc() => Bonus == 0 ? "" : $"[{Bonus}]";
  public override string AsText() => $"Armour#{Part}#{ArmourMod}#{Bonus}";
  public override bool Aura => false;
}

class ShieldOfTheFaithfulTrait : ACModTrait
{
  public override string AsText() => $"ShieldOfTheFaithful#{ArmourMod}";
}

class DeathMessageTrait : BasicTrait
{
  public string Message { get; set; } = "";
  public override string AsText() => $"DeathMessage#{Message}";
}

class DisguiseTrait : BasicTrait
{
  public Glyph Disguise {  get; set; }
  public Glyph TrueForm { get; set; }
  public string DisguiseForm { get; set; } = "";

  public override string AsText() => $"Disguise#{Disguise}#{TrueForm}#{DisguiseForm}";
}

class IllusionTrait : BasicTrait, IGameEventListener
{
  public ulong SourceID {  get; set; }
  public ulong ObjID { get; set; } // the GameObj the illusion trait is attached to
  public bool Expired { get => false; set { } }
  public bool Listening => true;

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    var obj = gs.ObjDb.GetObj(ObjID);
    if (obj is not null and Actor actor)
    {
      gs.ActorKilled(actor);
    }    
  }

  public override string AsText() => $"Illusion#{SourceID}#{ObjID}";
}

class GrappledTrait : BasicTrait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public ulong GrapplerID { get; set; }
  public int DC { get; set; }
  public bool Expired { get => false; set {} }
  public bool Listening => true;

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    var victim = gs.ObjDb.GetObj(VictimID);
    victim?.Traits.Remove(this);    
  }

  public override string AsText() => $"Grappled#{VictimID}#{GrapplerID}#{DC}";
}

class GrapplerTrait : BasicTrait 
{
  public int DC { get; set; }

  public override string AsText() => $"Grappler#{DC}";
}

class ParalyzingGazeTrait : BasicTrait
{
  public int DC { get; set; }

  public override string AsText() => $"ParalyzingGaze#{DC}";
}

// Ugh this feels like a dumb hack, but I wanted to keep AoEAction and
// such fairly generic
class EffectFactory(string effect, int dc)
{
  readonly string _effect = effect;
  readonly int _dc = dc;

  public EffectTrait Get(ulong victimID)
  {
    return _effect switch 
    {
      "confused" => new ConfusedTrait() { VictimID = victimID, DC = _dc },
      "paralyzed" => new ParalyzedTrait() { VictimID = victimID, DC = _dc },
      _ => throw new Exception($"I don't know about the effect '{_effect}'")
    };
  }
}

// EffectTrait subclasses I've implemented are *thiiiiiis* close to being
// duplicates of each other...
class ConfusedTrait : EffectTrait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public int DC { get; set; }
  public bool Expired { get; set; } = false;

  public override string AsText() => $"Confused#{VictimID}#{DC}";

  public bool Listening => throw new NotImplementedException();

  public override bool IsAffected(Actor victim, GameState gs)
  {
    foreach (Trait trait in victim.Traits)
    {
      if (trait is ConfusedTrait || trait is ImmuneConfusionTrait)
        return false;
    }
 
    return !victim.AbilityCheck(Attribute.Will, DC, gs.Rng);
  }

  public override string Apply(Actor victim, GameState gs)
  {
    victim.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} confused!";
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(VictimID) is Actor victim)
    {
      if (victim.AbilityCheck(Attribute.Will, DC, gs.Rng))
      {
        victim.Traits.Remove(this);
        Expired = true;
        string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "regain")} {Grammar.Possessive(victim)} senses!";
        gs.UIRef().AlertPlayer(new Message(msg, victim.Loc), "", gs);
        gs.StopListening(GameEventType.EndOfRound, this);
      }
    }
  }
}

class ExhaustedTrait : EffectTrait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public ulong EndsOn { get; set; }
  public bool Expired { get; set; } = false;

  public override string AsText() => $"Exhausted#{VictimID}#{EndsOn}";

  public bool Listening => throw new NotImplementedException();

  public override string Apply(Actor victim, GameState gs)
  {    
    // if the actor already has the exhausted trait, just set the EndsOn
    // of the existing trait to the higher value
    foreach (var t in victim.Traits)
    {
      if (t is ExhaustedTrait exhausted)
      {
        exhausted.EndsOn = ulong.Max(EndsOn, exhausted.EndsOn);
        return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "become")} more exhausted!";
      }
    }

    victim.Traits.Add(this);
    victim.Recovery -= 0.5;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "become")} exhausted!";
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > EndsOn && gs.ObjDb.GetObj(VictimID) is Actor victim)
    {
      victim.Recovery += 0.5;
      victim.Traits.Remove(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "feel")} less exhausted!";
      gs.UIRef().AlertPlayer(new Message(msg, victim.Loc), "", gs);
      gs.StopListening(GameEventType.EndOfRound, this);
    }      
  }

  public override bool IsAffected(Actor victim, GameState gs) => true;
}

class ParalyzedTrait : EffectTrait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public int DC { get; set; }
  public bool Expired { get; set; } = false;

  public bool Listening => throw new NotImplementedException();

  public override string AsText() => $"Paralyzed#{VictimID}#{DC}";

  public override bool IsAffected(Actor victim, GameState gs)
  {
    // We'll allow only one paralyzed trait at a time. Although perhaps
    // I should keep which one has the higher DC?
    if (victim.HasTrait<ParalyzedTrait>())
      return false;

    return !victim.AbilityCheck(Attribute.Will, DC, gs.Rng);
  }

  public override string Apply(Actor victim, GameState gs)
  {    
    victim.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} paralyzed!";
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(VictimID) is Actor victim)
    {
      if (victim.AbilityCheck(Attribute.Will, DC, gs.Rng))
      {
        victim.Traits.Remove(this);
        Expired = true;
        string msg = $"{victim.FullName.Capitalize()} can move again!";
        gs.UIRef().AlertPlayer(new Message(msg, victim.Loc), "", gs);
        gs.StopListening(GameEventType.EndOfRound, this);
      }
    }
  }
}

class PoisonerTrait : BasicTrait
{
  public int DC { get; set; }
  public int Strength { get; set; }

  public override string AsText() => $"Poisoner#{DC}#{Strength}";
}

class OnFireTrait : BasicTrait, IGameEventListener
{
  public ulong ContainerID { get; set; }
  public bool Expired { get; set; } = false;
  public int Lifetime { get; set; } = 0;
  public bool Listening => true;

  public override string AsText() => $"OnFire#{Expired}#{ContainerID}#{Lifetime}";

  public void Extinguish(Item fireSrc, GameState gs)
  {
    gs.WriteMessages([new Message("The fire burns out.", fireSrc.Loc)], "");
    gs.ObjDb.RemoveItemFromGame(fireSrc.Loc, fireSrc);
    gs.ItemDestroyed(fireSrc, fireSrc.Loc);

    Expired = true;
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    ++Lifetime;
    if (gs.ObjDb.GetObj(ContainerID) is Item fireSrc)
    {
      if (Lifetime > 3 && gs.Rng.NextDouble() < 0.5)
      {
        Extinguish(fireSrc, gs);
        return;
      }

      var victim = gs.ObjDb.Occupant(fireSrc.Loc);
      gs.ApplyDamageEffectToLoc(fireSrc.Loc, DamageType.Fire);

      if (victim is not null)
      {
        int fireDmg = gs.Rng.Next(8) + 1;
        List<(int, DamageType)> fire = [(fireDmg, DamageType.Fire)];
        var (hpLeft, dmgMsg) = victim.ReceiveDmg(fire, 0, gs);
        if (dmgMsg != "")
        {
          gs.UIRef().AlertPlayer(new Message(dmgMsg, victim.Loc), "", gs);
        }
          
        if (hpLeft < 1)
        {
          string msg = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Die)} from fire!";
          gs.WriteMessages([new Message(msg, victim.Loc)], "");
          gs.ActorKilled(victim);
        }
        else
        {
          string txt = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Etre)} burnt!";
          gs.WriteMessages([new Message(txt, victim.Loc)], "");
        }
      }

      // The fire might spread!
      if (Lifetime > 1)
      {
        foreach (var sq in Util.Adj4Sqs(fireSrc.Loc.Row, fireSrc.Loc.Col))
        {
          var adj = fireSrc.Loc with { Row = sq.Item1, Col = sq.Item2 };
          gs.ApplyDamageEffectToLoc(adj, DamageType.Fire);
        }
      }
    }
  }
}

class WeakenTrait : BasicTrait
{
  public int DC { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"Weaken#{DC}#{Amt}";
}

// Well, buff or debuff but that's fairly wordy...
class StatBuffTrait : EffectTrait, IGameEventListener
{
  public int DC { get; set; } = 10;
  public ulong VictimID { get; set; }
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public Attribute Attr { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"StatBuff#{VictimID}#{ExpiresOn}#{Attr}#{Amt}";

  string CalcMessage(Actor victim, int amt)
  {
    bool player = victim is Player;
    if (Attr == Attribute.Strength && amt > 0)
    {
      if (player)
        return "You feel stronger!";
      else
        return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "look")} stronger!";
    }
    else if (Attr == Attribute.Strength && amt < 0)
    {
      if (player)
        return "You feel weaker!";
      else
        return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "look")} weaker!";
    }

    return player ? "You feel different!" : "";
  }

  public override bool IsAffected(Actor victim, GameState gs)
  {
    // We won't let a staff debuff lower a stat below -5. Let's not get out
    // of hand
    if (Amt < 0 && victim.Stats[Attr].Curr < -4)
      return false;

    return !victim.AbilityCheck(Attribute.Constitution, DC, gs.Rng);
  }

  public override string Apply(Actor victim, GameState gs)
  {        
    victim.Stats[Attr].Change(Amt);
    victim.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return CalcMessage(victim, Amt);
  }

  // This perhaps doesn't need to be public?
  string Remove(Actor victim)
  {    
    victim.Stats[Attr].Change(-Amt);
    victim.Traits.Remove(this);
    
    return CalcMessage(victim, -Amt);    
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > ExpiresOn)
    {
      gs.StopListening(GameEventType.EndOfRound, this);

      if (gs.ObjDb.GetObj(VictimID) is Actor victim)
      {
        string txt = Remove(victim);
        gs.UIRef().AlertPlayer([new Message(txt, victim.Loc)], "", gs);
      }
    }
  }
}

class PoisonedTrait : BasicTrait, IGameEventListener
{
  public int DC { get; set; }
  public int Strength { get; set; }
  public ulong VictimID { get; set; }
  public bool Expired { get; set; } = false;
  public bool Listening => true;

  public override string AsText() => $"Poisoned#{DC}#{Strength}#{VictimID}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    var victim = (Actor?)gs.ObjDb.GetObj(VictimID);
    if (victim != null)
    {
      bool conCheck = victim.AbilityCheck(Attribute.Constitution, DC, gs.Rng);
      if (conCheck)
      {
        victim.Traits.Remove(this);
        Expired = true;
        string msg = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Feel)} better.";
        gs.WriteMessages([new Message(msg, victim.Loc)], "");
      }
      else
      {
        List<(int, DamageType)> p = [(Strength, DamageType.Poison)];
        var (hpLeft, dmgMsg) = victim.ReceiveDmg(p, 0, gs);
        if (dmgMsg != "")
        {
          gs.UIRef().AlertPlayer(new Message(dmgMsg, victim.Loc), "", gs);
        }

        if (hpLeft < 1)
        {
          string msg = $"{victim.FullName.Capitalize()} from poison!";
          gs.WriteMessages([new Message("You feel ill.", victim.Loc)], "");
          gs.ActorKilled(victim);
        }
        else if (victim is Player)
        {
          gs.WriteMessages([new Message("You feel ill.", victim.Loc)], "");
        }
      }
    }
  }
}

class ReadableTrait(string text) : BasicTrait, IUSeable
{
  readonly string _text = text;
  public ulong ContainerID { get; set; }
  public override string AsText() => $"Readable#{_text.Replace("\n", "<br/>")}#{ContainerID}";
  
  public override bool Aura => false;

  public UseResult Use(Actor user, GameState gs, int row, int col)
  {
    Item? doc = gs.ObjDb.GetObj(ContainerID) as Item;
    string msg = $"{user.FullName.Capitalize()} read:\n{_text}";
    gs.WritePopup(msg, doc!.FullName.IndefArticle().Capitalize());

    var action = new CloseMenuAction(gs, 1.0);
    var acc = new PauseForMoreAccumulator();

    return new UseResult(false, "", action, acc);
  }
}

// Technically I suppose this is a Count Up not a Count Down...
class CountdownTrait : BasicTrait, IGameEventListener
{
  public bool Expired { get; set; } = false;
  public ulong ContainerID { get; set; }
  public bool Listening => true;

  public override string AsText() => $"Countdown#{ContainerID}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn < ExpiresOn)
      return;

    Expired = true;

    if (gs.ObjDb.GetObj(ContainerID) is Item item)
    {
      Loc loc = item.Loc;

      // Alert! Alert! This is cut-and-pasted from ExtinguishAction()
      if (item.ContainedBy > 0)
      {
        var owner = gs.ObjDb.GetObj(item.ContainedBy);
        if (owner is not null)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          loc = owner.Loc;
          ((Actor)owner).Inventory.Remove(item.Slot, 1);
        }
      }

      gs.ObjDb.RemoveItemFromGame(loc, item);

      // This is rather tied to Fog Cloud atm -- I should perhaps provide an
      // expiry message that can be set for each trait
      var msg = MsgFactory.Phrase(item.ID, Verb.Dissipate, 0, 1, false, loc, gs);
      gs.WriteMessages([msg], "");
    }
  }
}

// A light source that doesn't have fuel/burn out on its own.
class LightSourceTrait : BasicTrait
{
  public ulong ContainerID { get; set; }
  public override int Radius { get; set; }
  public sealed override bool Aura => true;
  public sealed override TerrainFlag Effect => TerrainFlag.Lit;

  public override string AsText() => $"LightSource#{ContainerID}#{Radius}";
}

// Who knew torches would be so complicated...
class TorchTrait : BasicTrait, IGameEventListener, IUSeable, IEffectApplier
{
  public ulong ContainerID { get; set; }
  public bool Lit { get; set; }
  public int Fuel { get; set; }
  public sealed override bool Aura => true;
  public sealed override TerrainFlag Effect => TerrainFlag.Lit;
  public override string Desc() => Lit ? "(lit)" : "";

  public override bool Active => Lit;
  public override int Radius
  {
    get => Lit ? 5 : 0;
  }

  public bool Expired { get; set; } = false;
  public bool Listening => Lit;

  public override string AsText()
  {
    return $"Torch#{ContainerID}#{Lit}#{Fuel}#{Expired}";
  }

  public string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc)
  {
    if (Lit && flag == TerrainFlag.Wet)
      return Extinguish(gs, item, loc);
    else
      return "";
  }

  string Extinguish(GameState gs, Item item, Loc loc)
  {
    gs.StopListening(GameEventType.EndOfRound, this);

    // Gotta set the lighting level before we extinguish the torch
    // so it's radius is still 5 when calculating which squares to 
    // affect            
    gs.ToggleEffect(item, loc, TerrainFlag.Lit, false);
    Lit = false;

    for (int j = 0; j < item.Traits.Count; j++)
    {
      if (item.Traits[j] is DamageTrait dt && dt.DamageType == DamageType.Fire)
      {
        item.Traits.RemoveAt(j);
        break;
      }
    }

    return $"{item!.FullName.DefArticle().Capitalize()} is extinguished.";
  }

  public UseResult Use(Actor _, GameState gs, int row, int col)
  {
    Item? item = gs.ObjDb.GetObj(ContainerID) as Item;
    var loc = new Loc(gs.CurrDungeonID, gs.CurrLevel, row, col);
    if (Lit)
    {
      var msg = Extinguish(gs, item!, loc);
      return new UseResult(true, msg, null, null);
    }
    else if (Fuel > 0)
    {
      Lit = true;
      item!.Stackable = false;
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      gs.ToggleEffect(item, loc, TerrainFlag.Lit, true);

      item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
      return new UseResult(true, $"The {item.Name} sparks to life!", null, null);
    }
    else
    {
      return new UseResult(false, $"That {item!.Name} is burnt out!", null, null);
    }
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    // Although if it's not Lit, it shouldn't be listening for events
    if (!Lit)
      return;

    if (--Fuel < 1)
    {
      Lit = false;
      Expired = true;

      if (gs.ObjDb.GetObj(ContainerID) is Item item)
      {
        Loc loc = item.Loc;
        if (item.ContainedBy > 0 && gs.ObjDb.GetObj(item.ContainedBy) is Actor owner)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          loc = owner.Loc;
          owner.Inventory.Remove(item.Slot, 1);
        }

        gs.CurrentMap.RemoveEffectFromMap(TerrainFlag.Lit, (item).ID);

        var msg = MsgFactory.Phrase(item.ID, Verb.BurnsOut, 0, 1, false, loc, gs);
        gs.WriteMessages([msg], "");
      }
    }
  }
}

class TraitFactory
{
  public static Trait FromText(string text, GameObj? container)
  {
    var pieces = text.Split('#');
    var name = pieces[0];
    string[] digits;

    switch (name)
    {
      case "ACMod":
        return new ACModTrait()
        {
          ArmourMod = int.Parse(pieces[1])
        };
      case "Armour":
        Enum.TryParse(pieces[1], out ArmourParts part);
        return new ArmourTrait()
        {
          Part = part,
          ArmourMod = int.Parse(pieces[2]),
          Bonus = int.Parse(pieces[3])
        };
      case "Attack":
        return new AttackTrait()
        {
          Bonus = int.Parse(pieces[1])
        };
      case "Cleave":
        return new CleaveTrait();
      case "ConfusingScream":
        return new ConfusingScreamTrait()
        {
          Radius = int.Parse(pieces[1]),
          DC = int.Parse(pieces[2]),
          Cooldown = ulong.Parse(pieces[3])
        };
      case "Consumable":
        return new ConsumableTrait();
      case "Countdown":
        return new CountdownTrait()
        {
          ContainerID = ulong.Parse(pieces[1]),
          Expired = bool.Parse(pieces[2])
        };
      case "Damage":
        Enum.TryParse(pieces[3], out DamageType dt);
        return new DamageTrait()
        {
          DamageDie = int.Parse(pieces[1]),
          NumOfDie = int.Parse(pieces[2]),
          DamageType = dt
        };
      case "DeathMessage":
        return new DeathMessageTrait()
        {
          Message = pieces[1]
        };
      case "Disguise":
        return new DisguiseTrait()
        {
          Disguise = Glyph.TextToGlyph(pieces[1]),
          TrueForm = Glyph.TextToGlyph(pieces[2]),
          DisguiseForm = pieces[3]
        };
      case "Exhausted":
        return new ExhaustedTrait()
        {
          VictimID = ulong.Parse(pieces[1]),
          EndsOn = ulong.Parse(pieces[2])
        };
      case "SpellAction":
        return new SpellActionTrait()
        {
          Name = pieces[1],
          Cooldown = ulong.Parse(pieces[2])
        };
      case "RangedSpellAction":      
        return new RangedSpellActionTrait()
        {
          Name = pieces[1],
          Cooldown = ulong.Parse(pieces[2]),
          MinRange = int.Parse(pieces[3]),
          MaxRange = int.Parse(pieces[4])
        };
      case "Flammable":
        return new FlammableTrait();
      case "Floating":
        return new FloatingTrait();
      case "Flying":
        return new FlyingTrait();
      case "Illusion":
        return new IllusionTrait()
        {
          SourceID = ulong.Parse(pieces[1]),
          ObjID = ulong.Parse(pieces[2])
        };
      case "ImmuneConfusion":
        return new ImmuneConfusionTrait();
      case "Immunity":
        Enum.TryParse(text[(text.LastIndexOf('#') + 1)..], out DamageType idt);
        return new Immunity()
        {
          Type = idt
        };
      case "Impale":
        return new ImpaleTrait();      
      case "LightSource":
        return new LightSourceTrait()
        {
          ContainerID = ulong.Parse(pieces[1]),
          Radius = int.Parse(pieces[2])
        };
      case "Melee":
        Enum.TryParse(text[(text.LastIndexOf('#') + 1)..], out DamageType mdt);
        digits = text.Split('#');
        return new MobMeleeTrait()
        {
          Name = "Melee",
          DamageDie = int.Parse(digits[1]),
          DamageDice = int.Parse(digits[2]),
          MinRange = 1,
          MaxRange = 1,
          DamageType = mdt

        };      
      case "Missile":
        Enum.TryParse(text[(text.LastIndexOf('#') + 1)..], out DamageType mmdt);
        digits = text.Split('#');
        return new MobMissileTrait()
        {
          Name = "Missile",
          DamageDie = int.Parse(digits[1]),
          DamageDice = int.Parse(digits[2]),
          MinRange = int.Parse(digits[3]),
          MaxRange = int.Parse(digits[4]),
          DamageType = mmdt

        };
      case "MobMelee":
        Enum.TryParse(pieces[5], out DamageType mmelDt);
        return new MobMeleeTrait()
        {
          MinRange = int.Parse(pieces[1]),
          MaxRange = int.Parse(pieces[2]),
          DamageDie = int.Parse(pieces[3]),
          DamageDice = int.Parse(pieces[4]),
          DamageType = mmelDt
        };
      case "MobMissile":
        Enum.TryParse(pieces[5], out DamageType mmisDt);
        return new MobMeleeTrait()
        {
          MinRange = int.Parse(pieces[1]),
          MaxRange = int.Parse(pieces[2]),
          DamageDie = int.Parse(pieces[3]),
          DamageDice = int.Parse(pieces[4]),
          DamageType = mmisDt
        };
      case "Named":
        return new NamedTrait();
      case "OnFire":
        return new OnFireTrait()
        {
          Expired = bool.Parse(pieces[1]),
          ContainerID = ulong.Parse(pieces[2]),
          Lifetime = int.Parse(pieces[3])
        };
      case "Confused":
        return new ConfusedTrait()
        {
          VictimID = ulong.Parse(pieces[1]),
          DC = int.Parse(pieces[2])
        };
      case "Divider":
        return new DividerTrait();
      case "FinalBoss":
        return new FinalBossTrait();
      case "Grappled":
        return new GrappledTrait()
        {
          VictimID = ulong.Parse(pieces[1]),
          GrapplerID = ulong.Parse(pieces[2]),
          DC = int.Parse(pieces[3])
        };
      case "Grappler":
        return new GrapplerTrait() 
        {
          DC = int.Parse(pieces[1])
        };
      case "KnockBack":
        return new KnockBackTrait();
      case "Opaque":
        return new OpaqueTrait();
      case "Paralyzed":
        return new ParalyzedTrait()
        {
          VictimID = ulong.Parse(pieces[1]),
          DC = int.Parse(pieces[2])
        };
      case "ParalyzingGaze":
        return new ParalyzingGazeTrait()
        {
          DC = int.Parse(pieces[1])
        };
      case "Plant":
        return new PlantTrait();
      case "Plural":
        return new PluralTrait();
      case "Poisoned":
        return new PoisonedTrait()
        {
          DC = int.Parse(pieces[1]),
          Strength = int.Parse(pieces[2]),
          VictimID = ulong.Parse(pieces[3]),
          Expired = bool.Parse(pieces[4])
        };
      case "Poisoner":
        digits = text.Split('#');
        return new PoisonerTrait()
        {
          DC = int.Parse(digits[1]),
          Strength = int.Parse(digits[2])
        };
      case "PoorLoot":
        return new PoorLootTrait();
      case "Rage":
        return new RageTrait((Actor)container);
      case "Readable":
        return new ReadableTrait(pieces[1].Replace("<br/>", "\n"))
        {
          ContainerID = ulong.Parse(pieces[2])
        };        
      case "ResistBlunt":
        return new ResistBluntTrait();
      case "ResistPiercing":
        return new ResistPiercingTrait();
      case "ResistSlashing":
        return new ResistSlashingTrait();
      case "ShieldOfTheFaithful":
        return new ShieldOfTheFaithfulTrait()
        {
          ArmourMod = int.Parse(pieces[1])
        };
      case "StatBuff":
        Enum.TryParse(pieces[3], out Attribute attr);
        return new StatBuffTrait()
        {
          VictimID = ulong.Parse(pieces[1]),
          ExpiresOn = ulong.Parse(pieces[2]),
          Attr = attr,
          Amt = int.Parse(pieces[4])
        };
      case "Sticky":
        return new StickyTrait();
      case "Summon":
        return new SummonTrait()
        {
          Name = name,
          Cooldown = ulong.Parse(pieces[1]),
          Summons = pieces[2],
          Quip = pieces[3]
        };
      case "Teflon":
        return new TeflonTrait();
      case "Telepathy":
        return new TelepathyTrait();
      case "Torch":
        return new TorchTrait()
        {
          ContainerID = ulong.Parse(pieces[1]),
          Lit = bool.Parse(pieces[2]),
          Fuel = int.Parse(pieces[3])
        };
      case "UseSimple":
        return new UseSimpleTrait(pieces[1]);
      case "Weaken":
        return new WeakenTrait()
        {
          DC = int.Parse(pieces[1]),
          Amt = int.Parse(pieces[2])
        };
      case "Written":
        return new WrittenTrait();
      case "Villager":
        return new VillagerTrait();
      default:
        ulong cooldown = ulong.Parse(text[(text.IndexOf('#') + 1)..]);
        return new SpellActionTrait()
        {
          Name = name,
          Cooldown = cooldown
        };
    }
  }
}