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

record UseResult(bool Successful, string Message, Action? ReplacementAction, Inputer? Accumulator);

interface INeedsID
{
  bool IDed { get; set; }
}

interface IReadable
{
  void Read(Actor actor, UserInterface ui, Item document);
}

interface IUSeable
{
  UseResult Use(Actor user, GameState gs, int row, int col, Item? item);
  void Used();
}

// Some traits need a reference to the object they are applied to
interface IOwner
{
  ulong OwnerID { get; set; }
}

interface IDesc
{
  string Desc();
}

abstract class Trait : IEquatable<Trait>
{
  public virtual bool Active => true;
  public abstract string AsText();
  
  public bool Equals(Trait? other)
  {
    if (other is null)
      return false;

    if (ReferenceEquals(this, other)) 
      return true;

    return AsText() == other.AsText();
  }

  public override bool Equals(object? obj) => Equals(obj as Trait);
  public override int GetHashCode() => AsText().GetHashCode();
}

// This lets me easily group traits that are upgrades for the player. Name
// cribbed from 5e's Feats
abstract class FeatTrait : Trait { }

abstract class BasicTrait : Trait
{
  public ulong ExpiresOn { get; set; } = ulong.MaxValue;
  public override string AsText() => $"{ExpiresOn}";
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

class AdjectiveTrait(string adj) : Trait
{
  public string Adj { get; set; } = adj;

  public override string AsText() => $"Adjective#{Adj}";
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

  public override string AsText() => $"Summon#{Cooldown}#{Summons}#{Quip}";  
}

class HealAlliesTrait : ActionTrait
{
  // Bsaically, if there is an ally the trait owner can see nearby who needs healing, indicate 
  // that the action is available
  public override bool Available(Mob mob, GameState gs)
  {
    if (mob.Traits.OfType<AlliesTrait>().FirstOrDefault() is AlliesTrait allies)
    {
      Loc loc = mob.Loc;
      var fov = FieldOfView.CalcVisible(6, loc, gs.CurrentMap, gs.ObjDb);
      foreach (ulong id in allies.IDs)
      {
        if (gs.ObjDb.GetObj(id) is Actor ally)
        {
          Stat hp = ally.Stats[Attribute.HP];
          if (fov.Contains(ally.Loc) && hp.Curr < hp.Max)
            return true;
        }        
      }
    }

    return false;
  }

  public override string AsText() => $"HealAllies#{Cooldown}";
}

class ConfusingScreamTrait : ActionTrait
{
  public int DC { get; set; }
  public int Radius { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Radius;
  }

  public override string AsText() => $"ConfusingScream#{Radius}#{DC}#{Cooldown}#";
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

class SpellActionTrait : ActionTrait
{
  public override string AsText() => $"SpellAction#{Name}#{MinRange}#{MaxRange}#{Cooldown}#";
  public override bool Available(Mob mob, GameState gs) => true;
}

class AcidSplashTrait : Trait
{
  public override string AsText() => "AcidSplash";
}

class AlliesTrait : Trait
{
  public List<ulong> IDs = [];

  public override string AsText() => $"Allies#{string.Join(',', IDs)}";
}

class AxeTrait : Trait
{
  public override string AsText() => "Axe";
}

class ConsumableTrait : Trait
{
  public override string AsText() => "Consumable";
}

class CorrosiveTrait : Trait
{
  public override string AsText() => "Corrosive";
}

class DialogueScriptTrait : Trait
{
  public string ScriptFile { get; set; } = "";

  public override string AsText() => $"DialogueScript#{ScriptFile}";
}

class DodgeTrait : FeatTrait
{
public int Rate { get; set; }

public override string AsText() => $"Dodge#{Rate}";
}

class FinesseTrait : Trait
{
  public override string AsText() => "Finesse";
}

class ImmunityTrait : BasicTrait
{
  public DamageType Type {  get; set; }

  public override string AsText() => $"Immunity#{Type}#{ExpiresOn}";
}

class InPitTrait : Trait
{
  public override string AsText() => "InPit";
}

class LightStepTrait : Trait
{
  public override string AsText() => "LightStep";
}

class MetalTrait : Trait
{
  public Metals Type {  get; set; }

  public override string AsText() => $"Metal#{(int)Type}";
}

class CoinsLootTrait : Trait
{
  public int Min { get; set; }
  public int Max { get; set; }

  public override string AsText() => $"CoinsLoot#{Min}#{Max}";
}

class PoorLootTrait : Trait
{
  public override string AsText() => "PoorLoot";
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

// I should replace the above 3 traits with this one
class ResistanceTrait : TemporaryTrait
{
  public DamageType Type { get; set; }
  protected override string ExpiryMsg() => $"You no longer feel resistant to {Type}.";
  public override string AsText() => $"Resistance#{Type}#{base.AsText()}";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [];
  }
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

class FallenAdventurerTrait : Trait
{
  public override string AsText() => "FallenAdventurer";
}

class FinalBossTrait : Trait
{
  public override string AsText() => "FinalBoss";
}

class FlammableTrait : Trait
{
  public override string AsText() => "Flammable";
}

class GrantsTrait : Trait
{
  public string[] TraitsGranted = [];

  public override string AsText() => "Grants#" + string.Join(';', TraitsGranted);

  public void Grant(GameObj obj)
  {
    foreach (string t in TraitsGranted)
    {
      Trait trait = TraitFactory.FromText(t, obj);
      obj.Traits.Add(trait);
    }
  }

  public void Remove(GameObj obj)
  {
    foreach (string t in TraitsGranted)
    {
      Trait granted = TraitFactory.FromText(t, obj);
      obj.Traits.Remove(granted);
    }
  }
}

class OwnsItemTrait : Trait
{
  public ulong ItemID { get; set; }

  public override string AsText() => $"OwnsItem#{ItemID}";
}

class PlantTrait : Trait
{
  public override string AsText() => "Plant";
}

class PluralTrait : Trait
{
  public override string AsText() => "Plural";
}

class PolearmTrait : Trait
{
  public override string AsText() => "Polearm";
}

class RustedTrait : Trait
{
  public Rust Amount { get; set; }

  public override string AsText() => $"Rusted#{(int)Amount}";
}

class SwordTrait : Trait
{
  public override string AsText() => "Sword";
}

class TeflonTrait : Trait
{
  public override string AsText() => "Teflon";
}

abstract class TemporaryTrait : BasicTrait, IGameEventListener, IOwner
{
  public bool Expired { get; set; }
  public bool Listening => true;
  public ulong OwnerID {  get; set; }
  protected virtual string ExpiryMsg() => "";

  protected virtual void Remove(GameState gs)
  {
    var obj = gs.ObjDb.GetObj(OwnerID);
    obj?.Traits.Remove(this);
    gs.RemoveListener(this);

    if (obj is Player)
      gs.UIRef().AlertPlayer(new Message(ExpiryMsg(), obj.Loc), "", gs);
  }

  public virtual void EventAlert(GameEventType eventType, GameState gs)
  {
    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      Remove(gs);
    }
  }

  public abstract List<Message> Apply(Actor target, GameState gs);

  public override string AsText() => $"{ExpiresOn}#{OwnerID}";
}

class TelepathyTrait : TemporaryTrait
{  
  protected override string ExpiryMsg() => "You can no longer sense others' minds!";
  public override string AsText() => $"Telepathy#{base.AsText()}";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);    
    OwnerID = target.ID;

    return [ new Message($"{target.FullName.Capitalize()} can sense others' minds!", target.Loc) ];
  }
}

class LevitationTrait : TemporaryTrait
{
  protected override string ExpiryMsg() => "You alight on the ground.";

  public override string AsText() => $"Levitation#{OwnerID}#{ExpiresOn}";

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(OwnerID) is Actor actor)
    {
      if (ExpiresOn - gs.Turn == 15)
      {
        string s = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "wobble")} in the air.";
        gs.WriteMessages([new Message(s, actor.Loc)], "");
      }

      if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
      {
        Remove(gs);

        // I am assuming if someone has more than one FloatingTrait, we can just remove one of them
        // and it doesn't matter.
        int i = -1;
        for (int j = 0; j < actor.Traits.Count; j++)
        {
          if (actor.Traits[j] is FloatingTrait)
          {
            i = j;
            break;
          }
        }
        if (i != -1)
          actor.Traits.RemoveAt(i);

        gs.ResolveActorMove(actor, actor.Loc, actor.Loc);
      }
    }    
  }

  public override List<Message> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    target.Traits.Add(new FloatingTrait());
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    string s = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "begin")} to float in the air!";
    //
    return [ new Message(s, target.Loc) ];
  }
}

class TwoHandedTrait : Trait
{
  public override string AsText() => "TwoHanded";
}

class VaultKeyTrait(Loc loc) : Trait
{
  public Loc VaultLoc { get; set; } = loc;

  public override string AsText() => $"VaultKey#{VaultLoc}";
}

class CleaveTrait : FeatTrait
{
  public override string AsText() => "Cleave";
}

class ImpaleTrait : FeatTrait
{
  public override string AsText() => "Impale";
}

class KnockBackTrait : FeatTrait
{
  public override string AsText() => "KnockBack";
}

class ReachTrait : FeatTrait
{
  public override string AsText() => "Reach";
}

// For actors who go by a proper name
class NamedTrait : Trait
{
  public override string AsText() => "Named";
}

// I think I want to sort out/merge Bezerk and Rage
// I implemented Rage pretty early on before most of the
// rest of the traits
class BerzerkTrait : Trait
{
  public override string AsText() => "Berzerk";
}

class RageTrait(Actor actor) : FeatTrait
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

class SilverAllergyTrait : Trait
{
  public override string AsText() => "SilverAllergy";
}

// One could argue lots of weapons are stabby but I created this one to 
// differentiate between a Rapier (which can impale) and a Dagger which
// cannot
class StabbyTrait() : Trait
{
  public override string AsText() => "Stabby";
}

class StackableTrait() : Trait
{
  public override string AsText() => "Stackable";
}

class VillagerTrait : Trait
{
  public override string AsText() => "Villager";
}

class WrittenTrait : Trait
{
  public override string AsText() => "Written";
}

// A bit dumb to have floating and flying and maybe I'll merge them
// eventually but at the moment floating creatures won't make noise
// while they move
class FloatingTrait : Trait
{
  public override string AsText() => $"Floating";
}

class FlyingTrait : BasicTrait
{
  public FlyingTrait() { }
  public FlyingTrait(ulong expiry) => ExpiresOn = expiry;

  public override string AsText() => $"Flying#{ExpiresOn}";
}

class OpaqueTrait : Trait
{
  public override string AsText() => "Opaque";  
}

// Simple in that I don't need any extra info like a target to use the effect.
class UseSimpleTrait(string spell) : Trait, IUSeable
{
  public string Spell { get; set; } = spell;

  public override string AsText() => $"UseSimple#{Spell}";

  public UseResult Use(Actor user, GameState gs, int row, int col, Item? item) => Spell switch
  {
    "antidote" => new UseResult(true, "", new AntidoteAction(gs, user, item), null),
    "blink" => new UseResult(true, "", new BlinkAction(gs, user, item), null),
    "minorheal" => new UseResult(true, "", new HealAction(gs, user, 4, 4, item), null),
    "telepathy" => new UseResult(true, "", new ApplyTraitAction(gs, user, new TelepathyTrait() { ExpiresOn = gs.Turn + 200 }, item), null),
    "magicmap" => new UseResult(true, "", new MagicMapAction(gs, user, item), null),
    "resistfire" => new UseResult(true, "", new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Fire, ExpiresOn = gs.Turn + 200}, item), null),
    "resistcold" => new UseResult(true, "", new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Cold, ExpiresOn = gs.Turn + 200}, item), null),
    "recall" => new UseResult(true, "", new WordOfRecallAction(gs), null),
    "levitation" => 
      new UseResult(true, "", new ApplyTraitAction(gs, user, new LevitationTrait() 
                                  { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) }, item), null),
    "knock" => new UseResult(true, "", new KnockAction(gs, user, item), null),
    "identify" => new UseResult(true, "", 
        new InventoryChoiceAction(gs, user, 
          new InventoryOptions() { Title = "Identify which item?", Options = InvOption.UnidentifiedOnly }, 
          new IdentifyItemAction(gs, user, item)), null),
    "applypoison" => new UseResult(true, "",
        new InventoryChoiceAction(gs, user,
          new InventoryOptions() { Title = "Apply it to which item?" },
          new ApplyPoisonAction(gs, user, item)), null),
    "seeinvisible" => 
        new UseResult(true, "", new ApplyTraitAction(gs, user, new SeeInvisibleTrait()
            { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) }, item), null),
    _ => throw new NotImplementedException($"{Spell.Capitalize()} is not defined!")
  };

  public void Used() {}
}

class SideEffectTrait : Trait
{
  public string Effect { get; set; } = "";
  public int Odds { get; set; }

  public override string AsText() => $"SideEffect#{Odds}#{Effect}";

  public List<Message> Apply(Actor target, GameState gs)
  {
    if (gs.Rng.Next(1, 101) > Odds)
      return [];

    var trait = (TemporaryTrait) TraitFactory.FromText(Effect, target);
    return trait.Apply(target, gs);
  }
}

// Generate a generic arrow but replace its damage with the
// bow's since different types of bows will do different dmg
class AmmoTrait : Trait
{
  public int DamageDie { get; set; }
  public int NumOfDie { get; set; }
  public DamageType DamageType { get; set; }
  public int Range { get; set; }

  public override string AsText() => $"Ammo#{DamageDie}#{NumOfDie}#{DamageType}#{Range}";

  public Item Arrow(GameState gs)
  {    
    Item arrow = new()
    {
      Name = "arrow",
      Type = ItemType.Weapon,
      Value = 0,
      Glyph = new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, Colours.BLACK)
    };

    arrow.Traits.Add(new DamageTrait() { DamageDie = DamageDie, NumOfDie = NumOfDie, DamageType = DamageType });
    arrow.Traits.Add(new StackableTrait());

    gs.ObjDb.Add(arrow);

    return arrow;
  }
}

class DamageTrait : Trait
{
  public int DamageDie { get; set; }
  public int NumOfDie { get; set; }
  public DamageType DamageType { get; set; }

  public override string AsText() => $"Damage#{DamageDie}#{NumOfDie}#{DamageType}";  
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

  public override string AsText() => $"Armour#{Part}#{ArmourMod}#{Bonus}";  
}

class CursedTrait : Trait 
{
  public override string AsText() => $"Cursed";
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
      gs.ActorKilled(actor, "", null);
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

class ConfusedTrait : TemporaryTrait
{
  public int DC { get; set; }
  
  public override string AsText() => $"Confused#{OwnerID}#{DC}#{ExpiresOn}";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    foreach (Trait trait in target.Traits)
    {
      if (trait is ConfusedTrait)
        return [];

      if (trait is ImmunityTrait immunity && immunity.Type == DamageType.Confusion)
        return [];
    }

    if (target.AbilityCheck(Attribute.Will, DC, gs.Rng))
      return [];

    OwnerID = target.ID;
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(25, 51);
    string s = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} confused!";

    return [new Message(s, target.Loc)];
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Traits.Remove(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "regain")} {Grammar.Possessive(victim)} senses!";
      gs.UIRef().AlertPlayer(new Message(msg, victim.Loc), "", gs);
      gs.StopListening(GameEventType.EndOfRound, this);
    }    
  }
}

class LameTrait : TemporaryTrait
{  
  public override string AsText() => $"Lame#{OwnerID}#{ExpiresOn}";
  
  public override List<Message> Apply(Actor target, GameState gs)
  {    
    // if the actor already has the exhausted trait, just set the EndsOn
    // of the existing trait to the higher value
    foreach (var t in target.Traits)
    {
      if (t is LameTrait trait)
      {
        trait.ExpiresOn = ulong.Max(ExpiresOn, trait.ExpiresOn);
        return [ new Message("You hurt your leg even more.", target.Loc) ];
      }
    }

    target.Traits.Add(this);
    target.Recovery -= 0.25;
    target.Stats[Attribute.Dexterity].Change(-1);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Recovery += 0.25;
      victim.Stats[Attribute.Dexterity].Change(1);
      victim.Traits.Remove(this);
      Expired = true;
      gs.StopListening(GameEventType.EndOfRound, this);
      gs.UIRef().AlertPlayer(new Message("Your leg feels better.", victim.Loc), "", gs);      
    }      
  }
}

class ExhaustedTrait : TemporaryTrait
{  
  public override string AsText() => $"Exhausted#{OwnerID}#{ExpiresOn}";

  public override List<Message> Apply(Actor target, GameState gs)
  {    
    // if the actor already has the exhausted trait, just set the EndsOn
    // of the existing trait to the higher value
    foreach (var t in target.Traits)
    {
      if (t is ExhaustedTrait exhausted)
      {
        exhausted.ExpiresOn = ulong.Max(ExpiresOn, exhausted.ExpiresOn);
        string s = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "become")} more exhausted!";
        return [ new Message(s, target.Loc) ];
      }
    }

    target.Traits.Add(this);
    target.Recovery -= 0.5;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    string msg = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "become")} exhausted!";
    return [ new Message(msg, target.Loc) ];
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Recovery += 0.5;
      victim.Traits.Remove(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "feel")} less exhausted!";
      gs.UIRef().AlertPlayer(new Message(msg, victim.Loc), "", gs);
      gs.StopListening(GameEventType.EndOfRound, this);
    }      
  }
}

// Trait for items who have a specific owner, mainly so I can alert them when,
// say, the player picks them up, etc
class OwnedTrait : Trait
{
  // An item can have more than one or more owner. Maybe 'CaresAbout' is a 
  // better name for this trait?
  public List<ulong> OwnerIDs { get; set; } = [];

  public override string AsText() => $"Owned#{string.Join(',', OwnerIDs)}";
}

class ParalyzedTrait : TemporaryTrait
{
  public int DC { get; set; }
  
  public override string AsText() => $"Paralyzed#{OwnerID}#{DC}#{ExpiresOn}";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    if (target.HasTrait<ParalyzedTrait>())
      return [];

    if (target.AbilityCheck(Attribute.Will, DC, gs.Rng))
      return [];

    target.Traits.Add(this);
    OwnerID = target.ID;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(25, 51);
    string s = $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} paralyzed!";

    return [ new Message(s, target.Loc) ];
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(OwnerID) is Actor victim)
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

class PoisonCoatedTrait : Trait
{
  public override string AsText() => "PoisonCoated";
}

class PoisonerTrait : BasicTrait
{
  public int DC { get; set; }
  public int Strength { get; set; }

  public override string AsText() => $"Poisoner#{DC}#{Strength}";
}

class OnFireTrait : BasicTrait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Expired { get; set; } = false;
  public int Lifetime { get; set; } = 0;
  public bool Listening => true;

  public override string AsText() => $"OnFire#{Expired}#{OwnerID}#{Lifetime}";

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
    if (gs.ObjDb.GetObj(OwnerID) is Item fireSrc)
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
        var (hpLeft, dmgMsg) = victim.ReceiveDmg(fire, 0, gs, null);
        if (dmgMsg != "")
        {
          gs.UIRef().AlertPlayer(new Message(dmgMsg, victim.Loc), "", gs);
        }
          
        if (hpLeft < 1)
        {
          string msg = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Die)} from fire!";
          gs.WriteMessages([new Message(msg, victim.Loc)], "");
          gs.ActorKilled(victim, "fire", null);
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

class RelationshipTrait : Trait
{
  public ulong Person1ID { get; set; }
  public ulong Person2ID { get; set; }
  public string Label { get; set; } = "";

  public override string AsText() => $"Relationship#{Person1ID}#{Person2ID}#{Label}";
}

class RetributionTrait : Trait
{
  public DamageType Type {  get; set; }
  public int DmgDie { get; set; }
  public int NumOfDice {get; set; }
  public override string AsText() => $"Retribution#{Type}#{DmgDie}#{NumOfDice}";
}

class WeakenTrait : BasicTrait
{
  public int DC { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"Weaken#{DC}#{Amt}";
}

// Well, buff or debuff but that's fairly wordy...
class StatBuffTrait : TemporaryTrait
{
  public int DC { get; set; } = 10;
  public Attribute Attr { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"StatBuff#{OwnerID}#{ExpiresOn}#{Attr}#{Amt}";

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

  public override List<Message> Apply(Actor target, GameState gs)
  {
    // We won't let a staff debuff lower a stat below -5. Let's not get out
    // of hand
    if (Amt < 0 && target.Stats[Attr].Curr < -4)
      return [];

    if (target.AbilityCheck(Attribute.Constitution, DC, gs.Rng))
      return [];

    target.Stats[Attr].Change(Amt);
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    string s = CalcMessage(target, Amt);
    return [ new Message(s, target.Loc) ];
  }

  string Remove(Actor victim)
  {    
    victim.Stats[Attr].Change(-Amt);
    victim.Traits.Remove(this);
    
    return CalcMessage(victim, -Amt);    
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn > ExpiresOn)
    {
      gs.StopListening(GameEventType.EndOfRound, this);

      if (gs.ObjDb.GetObj(OwnerID) is Actor victim)
      {
        string txt = Remove(victim);
        gs.UIRef().AlertPlayer([new Message(txt, victim.Loc)], "", gs);
      }
    }
  }
}

class PoisonedTrait : TemporaryTrait
{
  public int DC { get; set; }
  public int Strength { get; set; }

  public override string AsText() => $"Poisoned#{DC}#{Strength}#{OwnerID}#{ExpiresOn}";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    foreach (Trait t in target.Traits)
    {
      // We won't apply multiple poison statuses to one victim. Although maybe I
      // should replace the weaker poison with the stronger one?
      if (t is PoisonedTrait)
        return [];

      if (t is ImmunityTrait imm && imm.Type == DamageType.Poison)
        return [];
    }

    int duration = gs.Rng.Next(50, 100) - target.Stats[Attribute.Constitution].Curr * 7;
    bool conCheck = target.AbilityCheck(Attribute.Constitution, DC, gs.Rng);
    if (duration > 0 && !conCheck)
    {      
      target.Traits.Add(this);
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      OwnerID = target.ID;
      ExpiresOn = gs.Turn + (ulong) duration;
      var msg = new Message($"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Etre)} poisoned!", target.Loc);
      return [msg];
    }

    return [];
  }

  public override void EventAlert(GameEventType eventType, GameState gs)
  {
    var victim = (Actor?)gs.ObjDb.GetObj(OwnerID);
    if (victim is null)
      return;

    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      victim.Traits.Remove(this);
      gs.RemoveListener(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Feel)} better.";
      gs.WriteMessages([new Message(msg, victim.Loc)], "");

      return;
    }

    List<(int, DamageType)> p = [(Strength, DamageType.Poison)];
    var (hpLeft, dmgMsg) = victim.ReceiveDmg(p, 0, gs, null);
    if (dmgMsg != "")
    {
      gs.UIRef().AlertPlayer(new Message(dmgMsg, victim.Loc), "", gs);
    }

    if (hpLeft < 1)
    {
      string msg = $"{victim.FullName.Capitalize()} died from poison!";
      gs.WriteMessages([new Message(msg, victim.Loc)], "");
      gs.ActorKilled(victim, "poison", null);
    }
    else if (victim is Player)
    {
      gs.WriteMessages([new Message("You feel ill.", victim.Loc)], "");
    }
  }
}

class ReadableTrait(string text) : BasicTrait, IUSeable, IOwner
{
  public ulong OwnerID { get; set; }
  readonly string _text = text;
  public override string AsText() => $"Readable#{_text.Replace("\n", "<br/>")}#{OwnerID}";
  
  public UseResult Use(Actor user, GameState gs, int row, int col, Item? item)
  {
    Item? doc = gs.ObjDb.GetObj(OwnerID) as Item;
    string msg = $"{user.FullName.Capitalize()} read:\n{_text}";
    gs.UIRef().SetPopup(new Popup(msg, doc!.FullName.IndefArticle().Capitalize(), -1, -1));

    var action = new CloseMenuAction(gs, 1.0);
    var acc = new PauseForMoreInputer();

    return new UseResult(false, "", action, acc);
  }

  public void Used() {}
}

// I am making the assumption it will only be the Player who uses Recall.
class RecallTrait : BasicTrait, IGameEventListener
{
  public bool Expired { get; set; } = false;
  public bool Listening => true;

  public override string AsText() => $"Recall#{ExpiresOn}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn < ExpiresOn)
      return;

    Expired = true;

    var player = gs.Player;
    player.Traits.Remove(this);

    // We can get the entrance to the main dungeon via the History
    // object in Campaign. (I'd like to eventually have side quest
    // dungeons, though, where Recall will also need to be handled
    // but I'm not going to bother with that yet)
    if (gs.Campaign is null || gs.Campaign.History is null)
      throw new Exception("Checking for dungeon entrance fact: Campaign and History should never be null");

    if (player.Loc.DungeonID == 0)
    {
      var msg = new Message("You sudenly teleport exactly 1 cm to the left.", player.Loc, false);
      gs.WriteMessages([msg], "");
      return;
    }

    LocationFact? entrance = null;
    foreach (var fact in gs.Campaign.History.Facts) 
    {
      if (fact is LocationFact loc && loc.Desc == "Dungeon Entrance")
      {
        entrance = loc;
        break;
      }
    }

    if (entrance is not null)
    {
      gs.EnterLevel(player, 0, 0);
      var start = player.Loc;
      player.Loc = entrance.Loc;
      Message moveMsg = gs.ResolveActorMove(player, start, entrance.Loc);
      gs.RefreshPerformers();
      gs.UpdateFoV();
      var msg = new Message("A wave of vertigo...", player.Loc, false);
      gs.WriteMessages([msg, moveMsg], "");
    }
  }
}

class RegenerationTrait : BasicTrait, IGameEventListener
{
  public int Rate { get; set; }
  public ulong ActorID { get; set; }
  public bool Expired { get; set; } = false;
  public bool Listening => true;

  public override string AsText() => $"Regeneration#{Rate}#{ActorID}#{Expired}#{ExpiresOn}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(ActorID) is not Actor actor)
      return;

    if (gs.Turn > ExpiresOn)
    {
      actor.Traits.Remove(this);
      Expired = true;
    }
    else
    {
      actor.Stats[Attribute.HP].Change(Rate);
    }
  }
}

class SeeInvisibleTrait : TemporaryTrait
{
  protected override string ExpiryMsg() => "Your vision returns to normal.";

  public override List<Message> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [new Message($"{target.FullName.Capitalize()} can see into the beyond!", target.Loc)];
  }

  public override string AsText() => $"SeeInvisible#{OwnerID}#{ExpiresOn}";
}

class InvisibleTrait : BasicTrait, IGameEventListener
{
  public ulong ActorID { get; set; }
  public bool Expired { get; set; }
  public bool Listening => true;

  public override string AsText() => $"Invisible#{ActorID}#{Expired}#{ExpiresOn}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.ObjDb.GetObj(ActorID) is not Actor actor)
      return;

    if (gs.Turn > ExpiresOn)
    {
      actor.Traits.Remove(this);
      Expired = true;
    }    
  }
}

// Technically I suppose this is a Count Up not a Count Down...
class CountdownTrait : BasicTrait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Expired { get; set; } = false;
  public bool Listening => true;

  public override string AsText() => $"Countdown#{OwnerID}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (gs.Turn < ExpiresOn)
      return;

    Expired = true;

    if (gs.ObjDb.GetObj(OwnerID) is Item item)
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
class LightSourceTrait : BasicTrait, IOwner
{
  public ulong OwnerID { get; set; }
  public int Radius { get; set; }
  
  public override string AsText() => $"LightSource#{OwnerID}#{Radius}";
}

// Who knew torches would be so complicated...
class TorchTrait : BasicTrait, IGameEventListener, IUSeable, IOwner, IDesc
{
  public ulong OwnerID { get; set; }
  public bool Lit { get; set; }
  public int Fuel { get; set; }
  public string Desc() => Lit ? "(lit)" : "";

  public override bool Active => Lit;
  
  public bool Expired { get; set; } = false;
  public bool Listening => Lit;

  public override string AsText()
  {
    return $"Torch#{OwnerID}#{Lit}#{Fuel}#{Expired}";
  }

  public void Used() {}

  public string ReceiveEffect(EffectFlag flag, GameState gs, Item item, Loc loc)
  {
    if (Lit && flag == EffectFlag.Wet)
      return Extinguish(gs, item, loc);
    else
      return "";
  }

  public string Extinguish(GameState gs, Item item, Loc loc)
  {    
    gs.StopListening(GameEventType.EndOfRound, this);

    // Gotta set the lighting level before we extinguish the torch
    // so it's radius is still 5 when calculating which squares to 
    // affect            
    //gs.ToggleEffect(item, loc, TerrainFlag.Lit, false);
    Lit = false;

    for (int j = 0; j < item.Traits.Count; j++)
    {
      if (item.Traits[j] is DamageTrait dt && dt.DamageType == DamageType.Fire)
      {
        item.Traits.RemoveAt(j);
        break;
      }
    }

    item.Traits = item.Traits.Where(t => t is not LightSourceTrait).ToList();

    return $"{item!.FullName.DefArticle().Capitalize()} is extinguished.";
  }

  public UseResult Use(Actor _, GameState gs, int row, int col, Item? iitem)
  {
    Item? item = gs.ObjDb.GetObj(OwnerID) as Item;
    var loc = new Loc(gs.CurrDungeonID, gs.CurrLevel, row, col);
    if (Lit)
    {
      var msg = Extinguish(gs, item!, loc);
      return new UseResult(true, msg, null, null);
    }
    else if (Fuel > 0)
    {
      Lit = true;
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      
      item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
      item.Traits.Add(new LightSourceTrait() { Radius = 5 });

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

      if (gs.ObjDb.GetObj(OwnerID) is Item item)
      {
        Loc loc = item.Loc;
        if (item.ContainedBy > 0 && gs.ObjDb.GetObj(item.ContainedBy) is Actor owner)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          loc = owner.Loc;
          owner.Inventory.Remove(item.Slot, 1);
        }

        var msg = MsgFactory.Phrase(item.ID, Verb.BurnsOut, 0, 1, false, loc, gs);
        gs.WriteMessages([msg], "");
      }
    }
  }
}

class WandTrait : Trait, IUSeable, INeedsID, IDesc
{
  public int Charges { get; set; }
  public bool IDed { get; set; }
  public string Effect { get; set; } = "";
  public string Desc()
  {
    if (!IDed)
      return "";
    else if (Charges == 0)
      return "(empty)";
    else
      return $"({Charges})";
  }

  public override string AsText() => $"Wand#{Charges}#{IDed}#{Effect}";

  public UseResult Use(Actor user, GameState gs, int row, int col, Item? item)
  {
    if (Charges == 0) 
    {
      IDed = true;
      return new UseResult(true, "Nothing happens", new PassAction(), null);
    }

    return new UseResult(true, "", new UseWandAction(gs, user, this), null);  
  }

  public void Used() => --Charges;
}

// ArmourTrait also has a bonus field but I don't think I want to merge them
// into a single BonusTrait because perhaps there will be something like a
// Defender Sword which provides separate att/dmg and AC bonuses
class WeaponBonusTrait : Trait
{
  public int Bonus {  get; set; }
  public override string AsText() => $"WeaponBonus#{Bonus}";
}

class WorshiperTrait : Trait
{
  public Loc Altar { get; set; }
  public string Chant { get; set; } = "";

  public override string AsText() => $"Worshiper#{Altar}#{Chant}";
}

class TraitFactory
{
  private static readonly Dictionary<string, Func<string[], GameObj?, Trait>> traitFactories = new()
  {
    { "AcidSplash", (pieces, gameObj) => new AcidSplashTrait() },
    { "ACMod", (pieces, gameObj) => new ACModTrait() { ArmourMod = int.Parse(pieces[1]) }},
    { "Adjective", (pieces, gameObj) => new AdjectiveTrait(pieces[1]) },
    { "Allies", (pieces, gameObj) => { var ids = pieces[1].Split(',').Select(ulong.Parse).ToList(); return new AlliesTrait() { IDs = ids }; } },
    { "Ammo", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[3], out DamageType ammoDt);
        return new AmmoTrait() { DamageDie = int.Parse(pieces[1]), NumOfDie = int.Parse(pieces[2]), DamageType = ammoDt, Range = int.Parse(pieces[4]) }; 
      }
    },
    { "Armour", (pieces, gameObj) => { Enum.TryParse(pieces[1], out ArmourParts part);
      return new ArmourTrait() { Part = part, ArmourMod = int.Parse(pieces[2]), Bonus = int.Parse(pieces[3]) }; }
    },
    { "Axe", (pieces, gameObj) => new AxeTrait() },
    { "Berzerk", (pieces, gameObj) => new BerzerkTrait() },
    { "Cleave", (pieces, gameObj) => new CleaveTrait() },
    { "CoinsLoot", (pieces, gameObj) => new CoinsLootTrait() { Min = int.Parse(pieces[1]), Max = int.Parse(pieces[2])} },
    { "Confused", (pieces, gameObj) => new ConfusedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "ConfusingScream", (pieces, gameObj) => new ConfusingScreamTrait() { Radius = int.Parse(pieces[1]), DC = int.Parse(pieces[2]), Cooldown = ulong.Parse(pieces[3]) }},
    { "Consumable", (pieces, gameObj) => new ConsumableTrait() },
    { "Corrosive", (pieces, gameObj) => new CorrosiveTrait() },
    { "Countdown", (pieces, gameObj) => new CountdownTrait() { OwnerID = ulong.Parse(pieces[1]), Expired = bool.Parse(pieces[2]) }},
    { "Cursed", (pieces, gameObj) => new CursedTrait() },
    { "Damage", (pieces, gameObj) => {
      Enum.TryParse(pieces[3], out DamageType dt);
      return new DamageTrait() { DamageDie = int.Parse(pieces[1]), NumOfDie = int.Parse(pieces[2]), DamageType = dt }; }},
    { "DeathMessage", (pieces, gameObj) => new DeathMessageTrait() { Message = pieces[1] } },
    { "DialogueScript", (pieces, gameObj) => new DialogueScriptTrait() { ScriptFile = pieces[1] } },
    { "Disguise", (pieces, gameObj) =>  new DisguiseTrait() { Disguise = Glyph.TextToGlyph(pieces[1]), TrueForm = Glyph.TextToGlyph(pieces[2]), DisguiseForm = pieces[3] }},
    { "Divider", (pieces, gameObj) => new DividerTrait() },
    { "Dodge", (pieces, gameObj) => new DodgeTrait() { Rate = int.Parse(pieces[1]) }},
    { "Exhausted", (pieces, gameObj) =>  new ExhaustedTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "FallenAdventurer", (pieces, gameObj) => new FallenAdventurerTrait() },
    { "FinalBoss", (pieces, gameObj) => new FinalBossTrait() },
    { "Finesse", (pieces, gameObj) => new FinesseTrait() },
    { "Flammable", (pieces, gameObj) => new FlammableTrait() },
    { "Floating", (pieces, gameObj) => new FloatingTrait() },
    { "Flying", (pieces, gameObj) => new FlyingTrait() },
    { "Lame", (pieces, gameObj) =>  new LameTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "Grants", (pieces, gameObj) => new GrantsTrait() { TraitsGranted = pieces[1].Split(';') }},
    { "Grappled", (pieces, gameObj) => new GrappledTrait() { VictimID = ulong.Parse(pieces[1]), GrapplerID = ulong.Parse(pieces[2]), DC = int.Parse(pieces[3]) } },
    { "Grappler", (pieces, gameObj) => new GrapplerTrait { DC = int.Parse(pieces[1]) }},
    { "HealAllies", (pieces, gameObj) => new HealAlliesTrait() { Cooldown = ulong.Parse(pieces[1]) }},
    { "Illusion", (pieces, gameObj) => new IllusionTrait() { SourceID = ulong.Parse(pieces[1]), ObjID = ulong.Parse(pieces[2]) } },
    { "Immunity", (pieces, gameObj) => {
      Enum.TryParse(pieces[1], out DamageType dt);
      ulong expiresOn = pieces.Length > 2 ? ulong.Parse(pieces[2]) : ulong.MaxValue;
      return new ImmunityTrait() { Type = dt, ExpiresOn = expiresOn }; }},
    { "Impale", (pieces, gameObj) => new ImpaleTrait() },
    { "InPit", (pieces, gameObj) => new InPitTrait() },
    { "Invisible", (pieces, gameObj) =>
      {      
        return new InvisibleTrait()
        {
          ActorID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
          Expired = bool.Parse(pieces[2]),
          ExpiresOn = pieces[3] == "max" ? ulong.MaxValue : ulong.Parse(pieces[3])
      }; }
    },
    { "LightSource", (pieces, gameObj) => new LightSourceTrait() { OwnerID = pieces[1] == "owner" ? gameObj!.ID :  ulong.Parse(pieces[1]), Radius = int.Parse(pieces[2]) } },
    { "LightStep", (pieces, gameObj) => new LightStepTrait() },
    { "Melee", (pieces, gameObj) => {
      Enum.TryParse(pieces[3], out DamageType dt);      
      return new MobMeleeTrait() {
          Name = "Melee", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = 1, MaxRange = 1, DamageType = dt }; }},
    { "Metal", (pieces, gameObj) => new MetalTrait() { Type = (Metals)int.Parse(pieces[1]) } },
    { "Missile", (pieces, gameObj) => {
      Enum.TryParse(pieces[5], out DamageType dt);
      return new MobMissileTrait() {
          Name = "Missile", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]), DamageType = dt }; }},
    { "MobMelee", (pieces, gameObj) => {
      Enum.TryParse(pieces[3], out DamageType dt);
      return new MobMeleeTrait() {
          Name = "Melee", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = 1, MaxRange = 1, DamageType = dt }; }},
    { "MobMissile", (pieces, gameObj) => {
      Enum.TryParse(pieces[5], out DamageType dt);
      return new MobMissileTrait() {
          Name = "Missile", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]), DamageType = dt }; }},
    { "KnockBack", (pieces, gameObj) => new KnockBackTrait() },
    { "Levitation", (pieces, gameObj) => new LevitationTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "Named", (pieces, gameObj) => new NamedTrait() },
    { "OnFire", (pieces, gameObj) => new OnFireTrait() { Expired = bool.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]), Lifetime = int.Parse(pieces[3]) } },
    { "Owned", (pieces, gameObj) => new OwnedTrait() { OwnerIDs = pieces[1].Split(',').Select(ulong.Parse).ToList() } },
    { "Opaque", (pieces, gameObj) => new OpaqueTrait() },
    { "OwnsItem", (pieces, gameObj) => new OwnsItemTrait() { ItemID = ulong.Parse(pieces[1]) } },
    { "Paralyzed", (pieces, gameObj) => new ParalyzedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "ParalyzingGaze", (pieces, gameObj) => new ParalyzingGazeTrait() { DC = int.Parse(pieces[1]) } },
    { "Plant", (pieces, gameObj) => new PlantTrait() },
    { "Plural", (pieces, gameObj) => new PluralTrait() },
    { "PoisonCoated", (pieces, gameObj) => new PoisonCoatedTrait() },
    { "Poisoned", (pieces, gameObj) => new PoisonedTrait() { DC = int.Parse(pieces[1]), Strength = int.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]), ExpiresOn = ulong.Parse(pieces[4]) } },
    { "Poisoner", (pieces, gameObj) => new PoisonerTrait() { DC = int.Parse(pieces[1]), Strength = int.Parse(pieces[2]) } },
    { "Polearm", (pieces, gameObj) => new PolearmTrait() },
    { "PoorLoot", (pieces, gameObj) => new PoorLootTrait() },
    { "Rage", (pieces, gameObj) => new RageTrait((Actor)gameObj) },
    { "RangedSpellAction", (pieces, gameObj) => new RangedSpellActionTrait() { Name = pieces[1], Cooldown = ulong.Parse(pieces[2]),
        MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]) }},
    { "Reach", (pieces, gameObj) => new ReachTrait() },
    { "Readable", (pieces, gameObj) => new ReadableTrait(pieces[1].Replace("<br/>", "\n")) { OwnerID = ulong.Parse(pieces[2]) } },
    { "Recall", (pieces, gameObj) => new RecallTrait() { ExpiresOn = ulong.Parse(pieces[1]), Expired = bool.Parse(pieces[2]) } },
    { "Regeneration", (pieces, gameObj) => {
      return new RegenerationTrait()
        {
          Rate = int.Parse(pieces[1]),
          ActorID = pieces[2] == "owner" ? gameObj!.ID : ulong.Parse(pieces[2]),
          Expired = bool.Parse(pieces[3]),
          ExpiresOn = pieces[4] == "max" ? ulong.MaxValue : ulong.Parse(pieces[4])
        };
    } },
    { "Relationship", (pieces, gameObj) => new RelationshipTrait() { Person1ID = ulong.Parse(pieces[1]), Person2ID = ulong.Parse(pieces[2]), Label = pieces[3] } },
    { "Resistance", (pieces, gameObj) => {
        Enum.TryParse(pieces[1], out DamageType rdt);
        ulong expiresOn = pieces.Length > 2 ? ulong.Parse(pieces[1]) : ulong.MaxValue;
        ulong ownerID = pieces.Length > 3 ? ulong.Parse(pieces[2]): 0;
        return new ResistanceTrait() { Type = rdt, ExpiresOn = expiresOn, OwnerID = ownerID
        }; }},
    { "Retribution", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[1], out DamageType dt);
        return new RetributionTrait() { Type = dt, DmgDie = int.Parse(pieces[2]), NumOfDice = int.Parse(pieces[3]) };
      } 
    },
    { "ResistBlunt", (pieces, gameObj) => new ResistBluntTrait() },
    { "ResistPiercing", (pieces, gameObj) => new ResistPiercingTrait() },
    { "ResistSlashing", (pieces, gameObj) => new ResistSlashingTrait() },
    { "Rusted", (pieces, gameObj) => new RustedTrait() { Amount = (Rust)int.Parse(pieces[1]) } },
    { "SeeInvisible", (pieces, gameObj) => new SeeInvisibleTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "SideEffect", (pieces, gameObj) => new SideEffectTrait() { Odds = int.Parse(pieces[1]), Effect = string.Join('#', pieces[2..] ) } },
    { "SilverAllergy", (pieces, gameObj) => new SilverAllergyTrait() },
    { "SpellAction", (pieces, gameObj) => new SpellActionTrait() { Name = pieces[1], Cooldown = ulong.Parse(pieces[2]) }},
    { "Stabby", (pieces, gameObj) => new StabbyTrait() },
    { "Stackable", (pieces, gameObj) => new StackableTrait() },
    { "StatBuff", (pieces, gameObj) =>
    {
      Enum.TryParse(pieces[3], out Attribute attr);
      return new StatBuffTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), Attr = attr, Amt = int.Parse(pieces[4]) };
    } },
    { "Sticky", (pieces, gameObj) => new StickyTrait() },
    { "Summon", (pieces, gameObj) => new SummonTrait() { Name = pieces[0], Cooldown = ulong.Parse(pieces[1]), Summons = pieces[2], Quip = pieces[3] } },
    { "Sword", (pieces, gameObj) => new SwordTrait() },
    { "Teflon", (pieces, gameObj) => new TeflonTrait() },
    { "Telepathy", (pieces, gameObj) => new TelepathyTrait() { ExpiresOn = ulong.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]) } },
    { "Torch", (pieces, gameObj) => new TorchTrait() { OwnerID = ulong.Parse(pieces[1]), Lit = bool.Parse(pieces[2]), Fuel = int.Parse(pieces[3]) } },
    { "TwoHanded", (pieces, gameObj) => new TwoHandedTrait() },
    { "UseSimple", (pieces, gameObj) => new UseSimpleTrait(pieces[1]) },
    { "VaultKey", (pieces, GameObj) => new VaultKeyTrait(Loc.FromStr(pieces[1])) },
    { "Villager", (pieces, gameObj) => new VillagerTrait() },
    { "Wand", (pieces, gameObj) => new WandTrait() { Charges = int.Parse(pieces[1]), IDed = bool.Parse(pieces[2]), Effect = pieces[3] } },    
    { "Weaken", (pieces, gameObj) =>  new WeakenTrait() { DC = int.Parse(pieces[1]), Amt = int.Parse(pieces[2]) } },
    { "WeaponBonus", (pieces, gameObj) => new WeaponBonusTrait() { Bonus = int.Parse(pieces[1]) } },
    { "Worshiper", (pieces, gameObj) => new WorshiperTrait() { Altar = Loc.FromStr(pieces[1]), Chant = pieces[2] } },
    { "Written", (pieces, gameObj) => new WrittenTrait() }        
  };

  public static Trait FromText(string text, GameObj? container)
  {
    string[] pieces = text.Split('#');
    if (traitFactories.TryGetValue(pieces[0], out var factory))
    {
      return factory(pieces, container);
    }

    throw new Exception($"Unparseable trait string: {text}");
  }
}