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

enum ActionType
{
  Attack = 0b0001,
  Movement = 0b0010,
  Buff = 0b0100,
  Passive = 0b1000
}

record UseResult(Action? ReplacementAction, Inputer? Accumulator, bool Successful = true, string Message = "");

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
  public ulong SourceId { get; set;}

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

abstract class BasicTrait : Trait
{
  public ulong ExpiresOn { get; set; } = ulong.MaxValue;
  public override string AsText() => $"{ExpiresOn}";
}

abstract class LootTrait : Trait { }

// To let me classify traits that mobs can take on their turns
// Not sure if this is the best way to go...
abstract class ActionTrait : BasicTrait
{
  // I was thinking I could use MinRange to set abilities a monster might use
  // from further away. Ie., gobin archer has one attack from distance 2 to 7
  // and another they use when they are in melee range.
  public int MinRange { get; set; } = 0;
  public virtual int MaxRange { get; set; } = 0;
  public ulong Cooldown { get; set; } = 0;
  public string Name { get; set; } = "";

  public virtual ActionType ActionType { get; set; }

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

class AffixedTrait : Trait
{
  public override string AsText() => $"Affixed";
}

class AttackVerbTrait(Verb verb) : Trait
{
  public Verb Verb { get; set; } = verb;

  public override string AsText() => $"AttackVerb#{Verb}";
}

class AuraOfProtectionTrait : TemporaryTrait
{
  public int HP { get; set; }

  public override List<string> Apply(Actor target, GameState gs)
  {
    List<string> messages = [];
    if (target.Traits.OfType<AuraOfProtectionTrait>().FirstOrDefault() is AuraOfProtectionTrait aura)
    {
      aura.HP += HP;
      messages.Add($"The aura surrounding {target.FullName} brightens.");
    }
    else
    {
      target.Traits.Add(this);
      messages.Add($"A shimmering aura surrounds {target.FullName}.");
    }
    
    return messages;
  }

  public override string AsText() => $"AuraOfProtection#{HP}";
}

// For items that can be used by the Apply command but don't need to
// implement IUseable
class CanApplyTrait : Trait
{
  public override string AsText() => $"CanApply";
}

class CarriesTrait : LootTrait
{
  public string ItemName { get; set; } = "";
  public int Chance { get; set; }

  public override string AsText() => $"Carries#{ItemName}#{Chance}";
}

class SummonTrait : ActionTrait
{
  public string Summons { get; set; } = "";
  public string Quip { get; set; } = "";
  public override ActionType ActionType => ActionType.Attack;

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

class SummonUndeadTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Passive;

  // I'm not sure what a good limit is, but let's start with 100 and see 
  // if that's too many or causes performance issues
  public override bool Available(Mob mob, GameState gs) 
  {
    int levelPop = gs.ObjDb.LevelCensus(mob.Loc.DungeonID, mob.Loc.Level);

    return levelPop < 100;
  }

  public string Summons(GameState gs, Mob mob)
  {
    List<string> undead = [ "skeleton", "zombie" ];

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

    return undead[gs.Rng.Next(undead.Count)];
  }

  public override string AsText() => $"SummonUndead#{Cooldown}";
}

class HealAlliesTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Buff;

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
          if (fov.ContainsKey(ally.Loc) && hp.Curr < hp.Max)
            return true;
        }        
      }
    }

    return false;
  }

  public override string AsText() => $"HealAllies#{Cooldown}";
}

class HeroismTrait : TemporaryTrait 
{
  public override string AsText() => $"Heroism#{OwnerID}#{ExpiresOn}#{SourceId}";
  protected override string ExpiryMsg => "You feel less heroic.";

  public override List<string> Apply(Actor target, GameState gs)
  {
    if (target.Stats.TryGetValue(Attribute.Nerve, out var nerve))
    {
      nerve.Change(125);
    }

    // You can't stack heroism, so if another source is applied, just extend
    // duration of current source
    foreach (Trait t in target.Traits)
    {
      if (t is HeroismTrait ht)
      {
        ht.ExpiresOn += (ulong) gs.Rng.Next(50, 76);
        return [];
      }
    }
    
    OwnerID = target.ID;
    target.Traits.Add(this);

    // Note for if you ever implement monsters using items: this will fail
    // to grant monsters bonus HP
    if (target is Player p)
      p.CalcHP();

    gs.RegisterForEvent(GameEventType.EndOfRound, this);   

    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} heroic!" ];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn)
    {
      gs.StopListening(GameEventType.EndOfRound, this);
      Remove(gs);

      if (gs.ObjDb.GetObj(OwnerID) is Player player)
        player.CalcHP();      
    }
  }
}

class HiddenTrait : Trait
{
  public override string AsText() => $"Hidden";
}

// Trait for mobs who won't wander too far (normally) from
// a particular location
class HomebodyTrait : Trait
{
  public Loc Loc { get; set; }
  public int Range { get; set; }

  public override string AsText() => $"Homebody#{Loc}#{Range}";
}

class ConfusingScreamTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;

  public int DC { get; set; }
  public int Radius { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Radius;
  }

  public override string AsText() => $"ConfusingScream#{Radius}#{DC}#{Cooldown}";
}

class MobMeleeTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;
  public override string AsText() => $"MobMelee#{MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}";
  public int DamageDie { get; set; }
  public int DamageDice { get; set; }
  public DamageType DamageType { get; set; }

  public override bool Available(Mob mob, GameState gs) => InRange(mob, gs);
}

class MobMissileTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;
  public override string AsText() => $"MobMissile#{MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}";
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

class MosquitoTrait : Trait
{
  public override string AsText() => "Mosquito";
}

class RangedSpellActionTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;
  public override string AsText() => $"RangedSpellAction#{Name}#{Cooldown}#{MinRange}#{MaxRange}";
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
  public override ActionType ActionType { get; set; }
  public override string AsText() => $"SpellAction#{Name}#{Cooldown}#{MinRange}#{MaxRange}#{ActionType}";
  public override bool Available(Mob mob, GameState gs) => true;
}

class AcidSplashTrait : Trait
{
  public override string AsText() => "AcidSplash";
}

class AlacrityTrait : Trait
{
  public double Amt { get; set; }
  
  public override string AsText() => $"Alacrity#{Amt}#{SourceId}";
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

class BlockTrait : Trait
{
  public override string AsText() => "Block";
}

class ConstructTrait : Trait
{
  public override string AsText() => "Construct";
}

class ConsumableTrait : Trait
{
  public override string AsText() => "Consumable";
}

class CorrosiveTrait : Trait
{
  public override string AsText() => "Corrosive";
}

class DescriptionTrait(string text) : Trait
{
  public string Text { get; set; } = text;

  public override string AsText() => $"Description#{Text}";
}

class DialogueScriptTrait : Trait
{
  public string ScriptFile { get; set; } = "";

  public override string AsText() => $"DialogueScript#{ScriptFile}";
}

class DodgeTrait : Trait
{
public int Rate { get; set; }

public override string AsText() => $"Dodge#{Rate}#{SourceId}";
}

class FinesseTrait : Trait
{
  public override string AsText() => "Finesse";
}

class FireBreathTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;

  public int DC { get; set; }
  public int Range { get; set; }
  public int DmgDie { get; set; }
  public int DmgDice { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Range;
  }

  public override string AsText() => $"FireBreath#{DmgDie}#{DmgDice}#{Range}#{Cooldown}";
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

// For monsters who are smart enough to do things like jump into
// teleport traps when fleeing. (Maybe I can replace the Door Move
// strategy with this trait?)
class IntelligentTrait : Trait
{
  public override string AsText() => "Intelligent";
}

// Created this for the tutorial, but maybe there can be a potion
// of invincibility or such at some point?
class InvincibleTrait : Trait
{
  public override string AsText() => "Invincible";
}

class LightStepTrait : Trait
{
  public override string AsText() => "LightStep";
}

class LikeableTrait : Trait
{
  public override string AsText() => "Likeable";
}

class MetalTrait : Trait
{
  public Metals Type {  get; set; }

  public override string AsText() => $"Metal#{(int)Type}";
}

class BowTrait : Trait
{
  public override string AsText() => "Bow";
}

class BrainlessTrait : Trait
{
  public override string AsText() => "Brainless";
}

class CoinsLootTrait : LootTrait
{
  public int Min { get; set; }
  public int Max { get; set; }

  public override string AsText() => $"CoinsLoot#{Min}#{Max}";
}

class CudgelTrait : Trait
{
  public override string AsText() => "Cudgel";
}

class EdibleTrait : Trait
{
  public override string AsText() => "Edible";
}

class PoorLootTrait : LootTrait
{
  public override string AsText() => "PoorLoot";
}

class AppleProducerTrait : Trait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Expired {  get; set;  }
  public bool Listening => true;
  public ulong ObjId => OwnerID;
  
  public override string AsText() => $"AppleProducer#{OwnerID}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(OwnerID) is not Item statue)
      return;

    if (gs.Rng.Next(250) != 0)
      return;

    // So we don't end up with giant piles of apples, if there is a nearby
    // golden apple, we won't spawn a new one
    HashSet<Loc> locs = Util.LocsInRadius(statue.Loc, 5, gs.CurrentMap.Height, gs.CurrentMap.Width);
    foreach (Loc sq in locs)
    {
      if (gs.ObjDb.ItemsAt(sq).Any(i => i.Name == "golden apple"))
      {
        return;
      }
    }

    List<Loc> trees = locs.Where(l => gs.TileAt(l).IsTree())
                          .ToList();
    if (trees.Count == 0)
      return;

    Loc spot = trees[gs.Rng.Next(trees.Count)];
    Item apple = ItemFactory.Get(ItemNames.GOLDEN_APPLE, gs.ObjDb);
    gs.ItemDropped(apple, spot);
  }
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

  protected override string ExpiryMsg => $"You no longer feel resistant to {Type}.";
  public override string AsText() => $"Resistance#{Type}#{base.AsText()}#{SourceId}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    if (target.Traits.OfType<ResistanceTrait>().FirstOrDefault(t => t.Type == Type) is ResistanceTrait existing)
    {
      existing.ExpiresOn = ulong.Max(existing.ExpiresOn, ExpiresOn);
    }
    else
    {
      target.Traits.Add(this);
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      OwnerID = target.ID;
    }

    return [];
  }
}

class RestingTrait : TemporaryTrait
{
  public override string AsText() => $"Resting#{OwnerID}#{ExpiresOn}";

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.UIRef().SetPopup(new Popup("You awake, rested and refreshed.", "", -1, -1));
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Player player)
    {
      Remove(gs);
      player.Traits = player.Traits.Where(t => t is not RestingTrait).ToList();
    }
  }

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [];
  }
}

class RobbedTrait : Trait
{
  public override string AsText() => "Robbed";
}

class RumBreathTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;

  public int DC { get; set; }
  public int Range { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Range;
  }

  public override string AsText() => $"RumBreath#{Range}#{Cooldown}";
}

class SleepingTrait : Trait
{
  public override string AsText() => "Sleeping";
}

class StickyTrait : BasicTrait
{
  public int DC => 13;

  public override string AsText() => "Sticky";
}

class StressTrait : Trait 
{
  public StressLevel Stress { get; set; }
  public ulong OwnerID { get; set; }

  public override string AsText() => $"Stress#{Stress}#{OwnerID}";
}

class DividerTrait : Trait
{
  public override string AsText() => "Divider";
}

class FallenAdventurerTrait : Trait
{
  public override string AsText() => "FallenAdventurer";
}

class FearsomeBellowTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Attack;

  public int DC { get; set; }
  public int Radius { get; set; }

  public override bool Available(Mob mob, GameState gs)
  {
    return Util.Distance(mob.Loc, gs.Player.Loc) <= Radius;
  }

  public override string AsText() => $"FearsomeBellow#{Radius}#{DC}#{Cooldown}";
}

class FinalBossTrait : Trait
{
  public override string AsText() => "FinalBoss";
}

class FlammableTrait : Trait
{
  public override string AsText() => "Flammable";
}

// I think I want to make it so that traits which can be granted much have a
// sourceId, but I'm not quite sure how to implement it. Maybe a grantable
// interface?
class GrantsTrait : Trait
{
  public string[] TraitsGranted = [];

  public override string AsText()
  {
    string grantedTraits = string.Join(';', TraitsGranted).Replace('#', '&');
    return "Grants#" + grantedTraits;
  }

  public List<string> Grant(GameObj obj, GameState gs, GameObj srcItem)
  {
    List<string> msgs = [];

    foreach (string t in TraitsGranted)
    {
      Trait trait = TraitFactory.FromText(t, obj);
      if (srcItem is not null)
        trait.SourceId = srcItem.ID;

      if (trait is TemporaryTrait tmp)
      {
        if (obj is Actor actor)
          msgs.AddRange(tmp.Apply(actor, gs));
      }
      else
      {
        obj.Traits.Add(trait);
      }
     
      if (trait is IGameEventListener listener)
      {       
        gs.RegisterForEvent(GameEventType.EndOfRound, listener);
      }
    }

    if (obj is Actor actor2)
      gs.ResolveActorMove(actor2, actor2.Loc, actor2.Loc);

    return msgs;
  }

  public void Remove(GameObj obj, GameState gs, GameObj src)
  {
    List<Trait> traits = obj.Traits.Where(t => t.SourceId == src.ID).ToList();
    foreach (Trait t in traits)
    {
      if (t is TemporaryTrait tt)
      {
        tt.Remove(gs);
      }

      obj.Traits.Remove(t);
    }
    
    gs.RemoveListenersBySourceId(src.ID);

    if (obj is Actor actor)
      gs.ResolveActorMove(actor, actor.Loc, actor.Loc);
  }
}

class MiniBoss5Trait : Trait
{
  public override string AsText() => "MiniBoss5";
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
  protected virtual string ExpiryMsg => "";
  public virtual ulong ObjId => OwnerID;
  
  public virtual void Remove(GameState gs)
  {
    var obj = gs.ObjDb.GetObj(OwnerID);
    obj?.Traits.Remove(this);
    gs.RemoveListener(this);

    if (obj is Player)
      gs.UIRef().AlertPlayer(ExpiryMsg);
  }

  public virtual void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      Remove(gs);
    }
  }

  public abstract List<string> Apply(Actor target, GameState gs);

  public override string AsText() => $"{ExpiresOn}#{OwnerID}";
}

class TelepathyTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "You can no longer sense others' minds!";
  public override string AsText() => $"Telepathy#{base.AsText()}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);    
    OwnerID = target.ID;

    return [ $"{target.FullName.Capitalize()} can sense others' minds!" ];
  }
}

class TipsyTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "A fog lifts.";
  public override string AsText() => $"Tipsy#{OwnerID}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    throw new NotImplementedException();
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(OwnerID) is Actor actor)
    {
      if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
      {
        Remove(gs);
      }
    }
  }
}

class LevitationTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "You alight on the ground.";

  public override string AsText() => $"Levitation#{OwnerID}#{ExpiresOn}";

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(OwnerID) is Actor actor)
    {
      if (ExpiresOn - gs.Turn == 15)
      {
        gs.UIRef().AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "wobble")} in the air.");
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

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    target.Traits.Add(new FloatingTrait());
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "begin")} to float in the air!" ];
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

class ViciousTrait : Trait
{
  public double Scale { get; set; }

  public override string AsText() => $"Vicious#{Scale}";
}

class CleaveTrait : Trait
{
  public override string AsText() => "Cleave";
}

class ImpaleTrait : Trait
{
  public override string AsText() => "Impale";
}

class KnockBackTrait : Trait
{
  public override string AsText() => "KnockBack";
}

class ReachTrait : Trait
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
  public override string AsText() => $"Berzerk#{SourceId}";
}

class RageTrait(Actor actor) : Trait
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

class ScrollTrait : Trait
{
  public override string AsText() => "Scroll";
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

// Later, when I implement the stress mechanics, becoming frightened
// should icnrease the player's stress
class FrightenedTrait : TemporaryTrait
{
  public int DC { get; set; }
  
  public override string AsText() => $"Frightened#{OwnerID}#{DC}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    foreach (Trait trait in target.Traits)
    {
      if (trait is TipsyTrait)
        return [];

      if (trait is BrainlessTrait)
        return [];

      if (trait is ImmunityTrait immunity && immunity.Type == DamageType.Fear)
        return [];

      if (trait is FrightenedTrait frightened)
      {
        ExpiresOn += (ulong)gs.Rng.Next(15, 26);
        return [];
      }
    }

    if (target.AbilityCheck(Attribute.Will, DC, gs.Rng))
      return [];

    OwnerID = target.ID;
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(15, 26);
    
    if (target.Stats.TryGetValue(Attribute.MobAttitude, out Stat? attitude))
    {
      attitude.SetMax(Mob.AFRAID);
    }

    return [$"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "become")} frightened!"];
  }

  public void Remove(Actor victim, GameState gs)
  {
    victim.Traits.Remove(this);
    Expired = true;
    string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "shake")} off {Grammar.Possessive(victim)} fear!";
    if (gs.LastPlayerFoV.Contains(victim.Loc))
      gs.UIRef().AlertPlayer(msg);
    gs.StopListening(GameEventType.EndOfRound, this);

    if (victim.Stats.TryGetValue(Attribute.MobAttitude, out Stat? attitude))
    {
      attitude.SetMax(Mob.AGGRESSIVE);
    }
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      Remove(victim, gs);
    }    
  }
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

  TemporaryTrait BuildBlindTrait(Actor victim, GameState gs)
  {
    int duration = gs.Rng.Next(150, 251);
    if (victim.Stats.TryGetValue(Attribute.Constitution, out var con))
      duration -= 10 * con.Curr;

    return new BlindTrait()
    {
      OwnerID = victim.ID,
      ExpiresOn = gs.Turn + (ulong) duration
    };
  }

  public UseResult Use(Actor user, GameState gs, int row, int col, Item? item) => Spell switch
  {
    "antidote" => new UseResult(new AntidoteAction(gs, user), null, true, ""),
    "blink" => new UseResult(new BlinkAction(gs, user), null, true, ""),
    "booze" => new UseResult(new DrinkBoozeAction(gs, user), null, true, ""),
    "disarm" => new UseResult(new DisarmAction(gs, user, user.Loc), null, true, ""),
    "minorheal" => new UseResult(new HealAction(gs, user, 4, 4), null, true, ""),
    "maxheal" => new UseResult(new HealAction(gs, user, int.MaxValue, -1), null, true, ""),
    "trivialheal" => new UseResult(new HealAction(gs, user, 1, 1), null, true, ""),
    "telepathy" => new UseResult(new ApplyTraitAction(gs, user, new TelepathyTrait() { ExpiresOn = gs.Turn + 200 }), null, true, ""),
    "magicmap" => new UseResult(new MagicMapAction(gs, user), null, true, ""),
    "resistfire" => new UseResult(new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Fire, ExpiresOn = gs.Turn + 200}), null, true, ""),
    "resistcold" => new UseResult(new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Cold, ExpiresOn = gs.Turn + 200}), null, true, ""),
    "recall" => new UseResult(new WordOfRecallAction(gs), null, true, ""),
    "levitation" => new UseResult(new ApplyTraitAction(gs, user, new LevitationTrait() 
                                  { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) }), null, true, ""),
    "knock" => new UseResult(new KnockAction(gs, user), null, true, ""),
    "identify" => new UseResult(new InventoryChoiceAction(gs, user, 
          new InventoryOptions() { Title = "Identify which item?", Options = InvOption.UnidentifiedOnly }, 
          new IdentifyItemAction(gs, user)), null, true, ""),
    "applypoison" => new UseResult(new InventoryChoiceAction(gs, user,
          new InventoryOptions() { Title = "Apply it to which item?" },
          new ApplyPoisonAction(gs, user)), null, true, ""),
    "seeinvisible" => new UseResult(new ApplyTraitAction(gs, user, new SeeInvisibleTrait()
            { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) }), null, true, ""),
    "protection" => new UseResult(new ApplyTraitAction(gs, user, 
                        new AuraOfProtectionTrait() { HP = 25 }), null, true, ""),
    "blindness" => new UseResult(new ApplyTraitAction(gs, user, BuildBlindTrait(user, gs)), null, true, ""),
    "buffstrength" => new UseResult(new ApplyTraitAction(gs, user, 
                        new StatBuffTrait() { Attr = Attribute.Strength, Amt = 2, 
                          OwnerID = user.ID, ExpiresOn = gs.Turn + 50, SourceId = item!.ID }), null),
    "heroism" => new UseResult( 
                   new ApplyTraitAction(gs, user, 
                     new HeroismTrait() { 
                       OwnerID = user.ID, ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(50, 75), SourceId = item!.ID}), null),
    _ => throw new NotImplementedException($"{Spell.Capitalize()} is not defined!")
  };

  public void Used() {}
}

class SideEffectTrait : Trait
{
  public string Effect { get; set; } = "";
  public int Odds { get; set; }

  public override string AsText() => $"SideEffect#{Odds}#{Effect}";

  public List<string> Apply(Actor target, GameState gs)
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

  public override string AsText() => $"Damage#{DamageDie}#{NumOfDie}#{DamageType}#{SourceId}";  
}

class ACModTrait : BasicTrait
{
  public int ArmourMod { get; set; }
  public override string AsText() => $"ACMod#{ArmourMod}#{SourceId}";
}

class ArmourTrait : Trait
{
  public ArmourParts Part { get; set; }
  public int ArmourMod { get; set; }
  public int Bonus { set; get; }

  public override string AsText() => $"Armour#{Part}#{ArmourMod}#{Bonus}";  
}

class CursedTrait : Trait 
{
  public override string AsText() => $"Cursed";
}

class CutpurseTrait : Trait
{
  public override string AsText() => $"Cutpurse#{SourceId}";
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

class DisplacementTrait : Trait
{
  public override string AsText() => $"Displacement";
}

// For monsters who don't move (plants, etc)
class ImmobileTrait : Trait
{
  public override string AsText() => $"Immobile";
}

class IllusionTrait : BasicTrait, IGameEventListener
{
  public ulong ObjId { get; set; } // the GameObj the illusion trait is attached to
  public bool Expired { get => false; set { } }
  public bool Listening => true;
  
  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    var obj = gs.ObjDb.GetObj(ObjId);
    if (obj is not null and Actor actor)
    {
      gs.ActorKilled(actor, "", null, null);
    }    
  }

  public override string AsText() => $"Illusion#{SourceId}#{ObjId}";
}

class GrappledTrait : BasicTrait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public ulong GrapplerID { get; set; }
  public int DC { get; set; }
  public bool Expired { get => false; set {} }
  public bool Listening => true;
  public ulong ObjId => VictimID;
  
  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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

class BoostMaxStatTrait : TemporaryTrait
{
  public Attribute Stat {  get; set; }
  public int Amount { get; set; }

  public override List<string> Apply(Actor target, GameState gs)
  {
    if (!target.Stats.TryGetValue(Stat, out Stat? statValue))
    {
      statValue = new Stat(0);
      target.Stats.Add(Stat, statValue);
    }

    List<string> msgs = [];

    // For max HP, we'll only change max if curr == max
    // Note: not taking into account lowering a stat, if that's a thing that might happen
    if (Stat == Attribute.HP)
    {
      if (target.Stats[Stat].Curr == target.Stats[Stat].Max) 
      {
        statValue.ChangeMax(Amount);
        statValue.Change(Amount);
        msgs.Add($"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more robust!");
      }      
    }
    else if (Stat == Attribute.Constitution)
    {
      statValue.ChangeMax(Amount);
      statValue.Change(Amount);
      target.Stats[Attribute.HP].ChangeMax(Amount * 5);
      msgs.Add($"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} healthier!");
    }
    else
    {
      statValue.ChangeMax(Amount);
      statValue.Change(Amount);
      string s = Stat switch
      {
        Attribute.Strength => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} stronger!",
        Attribute.Dexterity => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more agile!",
        Attribute.FinesseUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept with light weapons!",
        Attribute.SwordUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept at swordplay!",
        Attribute.AxeUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept at axe-work!",
        Attribute.BowUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more skilled with bows!",
        _ => $"{Grammar.Possessive(target).Capitalize()} max {Stat} has changed!"
      };
      msgs.Add(s);
    }

    return msgs;
  }

  public override string AsText() => $"BoostMaxStat#{Stat}#{Amount}";
}

class ConfusedTrait : TemporaryTrait
{
  public int DC { get; set; }
  
  public override string AsText() => $"Confused#{OwnerID}#{DC}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    foreach (Trait trait in target.Traits)
    {
      if (trait is ConfusedTrait)
        return [];

      if (trait is ImmunityTrait immunity && immunity.Type == DamageType.Confusion)
        return [];

      if (trait is BrainlessTrait)
        return [];
    }

    if (target.AbilityCheck(Attribute.Will, DC, gs.Rng))
      return [];

    OwnerID = target.ID;
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(25, 51);
    
    return [$"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} confused!"];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Traits.Remove(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "regain")} {Grammar.Possessive(victim)} senses!";
      gs.UIRef().AlertPlayer(msg);
      gs.StopListening(GameEventType.EndOfRound, this);
    }    
  }
}

class DropTrait : Trait
{
  public string ItemName { get; set; } = "";
  public int Chance { get; set; }

  public override string AsText() => $"Drop#{ItemName}#{Chance}";
}

class LameTrait : TemporaryTrait
{  
  public override string AsText() => $"Lame#{OwnerID}#{ExpiresOn}";
  
  public override List<string> Apply(Actor target, GameState gs)
  {    
    // if the actor already has the exhausted trait, just set the EndsOn
    // of the existing trait to the higher value
    foreach (var t in target.Traits)
    {
      if (t is LameTrait trait)
      {
        trait.ExpiresOn = ulong.Max(ExpiresOn, trait.ExpiresOn);
        return [ "You hurt your leg even more." ];
      }
    }

    target.Traits.Add(this);
    target.Recovery -= 0.25;
    target.Stats[Attribute.Dexterity].Change(-1);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Recovery += 0.25;
      victim.Stats[Attribute.Dexterity].Change(1);
      victim.Traits.Remove(this);
      Expired = true;
      gs.StopListening(GameEventType.EndOfRound, this);
      gs.UIRef().AlertPlayer("Your leg feels better.");      
    }      
  }
}

class GoodMagicLootTrait : LootTrait
{
  public override string AsText() => "GoodMagicLoot";
}

class ExhaustedTrait : TemporaryTrait
{  
  public override string AsText() => $"Exhausted#{OwnerID}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {    
    // if the actor already has the exhausted trait, just set the EndsOn
    // of the existing trait to the higher value
    foreach (var t in target.Traits)
    {
      if (t is ExhaustedTrait exhausted)
      {
        exhausted.ExpiresOn = ulong.Max(ExpiresOn, exhausted.ExpiresOn);
        return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "become")} more exhausted!" ];
      }
    }

    target.Traits.Add(this);
    target.Recovery -= 0.5;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "become")} exhausted!" ];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      victim.Recovery += 0.5;
      victim.Traits.Remove(this);
      Expired = true;
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "feel")} less exhausted!";
      gs.UIRef().AlertPlayer(msg);
      gs.StopListening(GameEventType.EndOfRound, this);
    }      
  }
}

class NauseaTrait : TemporaryTrait
{
  public override string AsText() => $"Nausea#{OwnerID}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    if (target.HasTrait<NauseaTrait>())
      return [];

    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    
    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} nauseous!" ];    
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is GameObj victim)
    {
      victim.Traits.Remove(this);
      Expired = true;
      gs.StopListening(GameEventType.EndOfRound, this);
      gs.UIRef().AlertPlayer($"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "feel")} better.");
    }
  }
}

class NauseousAuraTrait : Trait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Listening => true;
  public bool Expired { get => false; set {} }
  public int Strength { get; set; }
  public override string AsText() => $"NauseousAura#{OwnerID}#{Strength}";
  public ulong ObjId => OwnerID;
  
  public void EventAlert(GameEventType eventType, GameState gs, Loc _)
  {
    if (gs.ObjDb.GetObj(OwnerID) is not Actor owner)
      return;
     
    int duration = gs.Rng.Next(Strength - 20, Strength + 21);
    if (duration <= 0)
      return;

    foreach (Loc loc in Util.Adj8Locs(owner.Loc))
    {
      if (gs.ObjDb.Occupant(loc) is Actor victim)
      {
        if (victim.HasTrait<UndeadTrait>())
          continue;

        if (victim.Traits.OfType<NauseaTrait>().FirstOrDefault() is NauseaTrait nausea)
        {
          nausea.ExpiresOn += (ulong) duration;
        }
        else
        {
          NauseaTrait nt = new NauseaTrait()
          {
            OwnerID = victim.ID,
            ExpiresOn = gs.Turn + (ulong) duration
          };
          List<string> msgs = nt.Apply(victim, gs);
          gs.UIRef().AlertPlayer(msgs);
        }
      }      
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
  public int TurnsParalyzed { get; set; } = 0;
  
  public override string AsText() => $"Paralyzed#{OwnerID}#{DC}#{ExpiresOn}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    if (target.HasTrait<ParalyzedTrait>())
      return [];

    if (target.AbilityCheck(Attribute.Will, DC, gs.Rng))
      return [];

    target.Traits.Add(this);
    OwnerID = target.ID;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(25, 51);
    
    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} paralyzed!" ];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      if (victim.AbilityCheck(Attribute.Will, DC, gs.Rng))
      {
        victim.Traits.Remove(this);
        Expired = true;
        string msg = $"{victim.FullName.Capitalize()} can move again!";
        gs.UIRef().AlertPlayer(msg);
        gs.StopListening(GameEventType.EndOfRound, this);
      }
    }
  }
}

class PoisonCoatedTrait : Trait
{
  public override string AsText() => "PoisonCoated";
}

class PoisonedTrait : TemporaryTrait
{
  public int DC { get; set; }
  public int Strength { get; set; }
  public int Duration { get; set; }
  public override string AsText() => $"Poisoned#{DC}#{Strength}#{OwnerID}#{ExpiresOn}#{Duration}";

  public override List<string> Apply(Actor target, GameState gs)
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

    bool conCheck = target.AbilityCheck(Attribute.Constitution, DC, gs.Rng);
    if (!conCheck)
    {
      target.Traits.Add(this);
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      OwnerID = target.ID;
      ExpiresOn = gs.Turn + (ulong)Duration;
      return [$"{target.FullName.Capitalize()} {MsgFactory.CalcVerb(target, Verb.Etre)} poisoned!"];
    }

    return [];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
      gs.UIRef().AlertPlayer(msg);

      return;
    }

    List<(int, DamageType)> p = [(Strength, DamageType.Poison)];
    var (hpLeft, dmgMsg, _) = victim.ReceiveDmg(p, 0, gs, null, 1.0);
    if (dmgMsg != "")
      gs.UIRef().AlertPlayer(dmgMsg);

    if (hpLeft < 1)
    {
      string msg = $"{victim.FullName.Capitalize()} died from poison!";
      gs.UIRef().AlertPlayer(msg);
      gs.ActorKilled(victim, "poison", null, null);
    }
    else if (victim is Player)
    {
      gs.UIRef().AlertPlayer("You feel ill.");
    }
  }
}

class PoisonerTrait : BasicTrait
{
  public int DC { get; set; }
  public int Strength { get; set; }
  public int Duration { get; set; }
  public override string AsText() => $"Poisoner#{DC}#{Strength}#{Duration}";
}

class OnFireTrait : BasicTrait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Expired { get; set; } = false;
  public int Lifetime { get; set; } = 0;
  public bool Listening => true;
  public bool Spreads { get; set; }
  public ulong ObjId => OwnerID;
  
  public override string AsText() => $"OnFire#{Expired}#{OwnerID}#{Lifetime}#{Spreads}";

  public void Extinguish(Item fireSrc, GameState gs)
  {
    gs.UIRef().AlertPlayer("The fire burns out.");
    gs.ObjDb.RemoveItemFromGame(fireSrc.Loc, fireSrc);
    gs.ItemDestroyed(fireSrc, fireSrc.Loc);

    Expired = true;
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
        var (hpLeft, dmgMsg, _) = victim.ReceiveDmg(fire, 0, gs, null, 1.0);
        if (dmgMsg != "")
        {
          gs.UIRef().AlertPlayer(dmgMsg);
        }
          
        if (hpLeft < 1)
        {
          string msg = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Die)} from fire!";
          gs.UIRef().AlertPlayer(msg);
          gs.ActorKilled(victim, "fire", null, null);
        }
        else
        {
          string txt = $"{victim.FullName.Capitalize()} {MsgFactory.CalcVerb(victim, Verb.Etre)} burnt!";
          gs.UIRef().AlertPlayer(txt);
        }
      }

      // The fire might spread!
      if (Spreads && Lifetime > 1)
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

class RepugnantTrait : Trait
{
  public override string AsText() => $"Repugnant#{SourceId}";
}

class RetributionTrait : Trait
{
  public DamageType Type {  get; set; }
  public int DmgDie { get; set; }
  public int NumOfDice {get; set; }
  public override string AsText() => $"Retribution#{Type}#{DmgDie}#{NumOfDice}";
}

class ShriekTrait : ActionTrait
{
  public override ActionType ActionType => ActionType.Passive;
  public override int MaxRange => 1;

  public int ShriekRadius { get; set; }

  public override bool Available(Mob actor, GameState gs) 
  {
    foreach (Loc adj in Util.Adj8Locs(actor.Loc))
    {
      if (gs.ObjDb.Occupied(adj))
        return true;
    }

    return false;
  }

  public override string AsText() => $"Shriek#{Cooldown}#{ShriekRadius}";
}

class ShunnedTrait : Trait
{
  public override string AsText() => "Shunned";
}

class VersatileTrait(DamageTrait oneHanded, DamageTrait twoHanded) : Trait
{
  public DamageTrait OneHanded { get; set; } = oneHanded;
  public DamageTrait TwoHanded { get; set; } = twoHanded;

  public override string AsText() => $"Versatile#{OneHanded.AsText()}#{TwoHanded.AsText()}";
}

class WeakenTrait : BasicTrait
{
  public int DC { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"Weaken#{DC}#{Amt}";
}

class WeaponSpeedTrait : Trait
{
  public double Cost { get; set; }

  public override string AsText() => $"WeaponSpeed#{Cost}";
}

class StatBuffTrait : TemporaryTrait 
{
  public Attribute Attr { get; set; }
  public int Amt { get; set; }

  public override string AsText() => $"StatBuff#{OwnerID}#{ExpiresOn}#{Attr}#{Amt}#{SourceId}";

  string CalcMessage(Actor target)
  {
    bool player = target is Player;
    if (Attr == Attribute.Strength)
    {
      string adj = Amt >= 0 ? "stronger" : "weaker";
      if (player)
        return $"You feel {adj}!";
      else
        return $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "look")} {adj}!";
    }
    else if (Attr == Attribute.HP && player)
    {
      return Amt >= 0 ? "You feel more robust!" : "You feel frail!";
    }
    else if (Attr == Attribute.Dexterity && player)
    {
      return Amt >= 0 ? "You feel more agile!" : "You feel klutzy!";
    }

    return player ? "You feel different!" : "";
  }

  public override List<string> Apply(Actor target, GameState gs)
  {
    // If the buffs share the same source, just increase the expires on rather
    // than letting the player spam stat buffs
    StatBuffTrait? other = target.Traits.OfType<StatBuffTrait>().Where(t => t.SourceId == SourceId).FirstOrDefault();
    if (other is not null)
    {
      other.ExpiresOn = ulong.Max(other.ExpiresOn, ExpiresOn);
      return [];
    }
    
    OwnerID = target.ID;
    target.Stats[Attr].ChangeMax(Amt);
    target.Stats[Attr].Change(Amt);

    target.Traits.Add(this);
    if (target is Player player && (Attr == Attribute.HP || Attr == Attribute.Constitution))
      player.CalcHP();

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [ CalcMessage(target) ];
  }

  public override void Remove(GameState gs)
  {  
    if (gs.ObjDb.GetObj(OwnerID) is Actor actor)
    {
      var statBuffs = actor.Traits.OfType<StatBuffTrait>()
                                  .Where(t => t.SourceId == SourceId)
                                  .ToList();
      foreach (StatBuffTrait buff in statBuffs)
      {
        string s = RemoveBuff(actor, buff);
        if (s != "")
          gs.UIRef().AlertPlayer(s);
      }
    }

    base.Remove(gs);
  }

  string RemoveBuff(Actor target, Trait trait)
  {    
    target.Stats[Attr].ChangeMax(-Amt);
    target.Stats[Attr].Change(-Amt);
    
    target.Traits.Remove(trait);
    
    if (target is Player player && (Attr == Attribute.HP || Attr == Attribute.Constitution))
      player.CalcHP();

    if (target is Player)
    {
      if (Attr == Attribute.HP)
        return "You feel more frail.";
      else
        return $"Your {Attr} returns to normal.";
    }

    return "";    
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn)
    {
      gs.StopListening(GameEventType.EndOfRound, this);

      if (gs.ObjDb.GetObj(OwnerID) is Actor victim)
      {
        string txt = RemoveBuff(victim, this);
        gs.UIRef().AlertPlayer(txt);
      }
    }
  }
}

class StatDebuffTrait : TemporaryTrait
{
  public int DC { get; set; } = 10;
  public Attribute Attr { get; set; }
  public int Amt { get; set; }
  public override string AsText() => $"StatDebuff#{OwnerID}#{ExpiresOn}#{Attr}#{Amt}";

  string CalcMessage(Actor victim)
  {
    bool player = victim is Player;
    if (Attr == Attribute.Strength)
    {
      if (player)
        return "You feel weaker!";
      else
        return $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "look")} weaker!";
    }

    return player ? "You feel different!" : "";
  }

  public override List<string> Apply(Actor target, GameState gs)
  {
    // We won't let a debuff lower a stat below -5. Let's not get out
    // of hand
    if (target.Stats[Attr].Curr < -4)
      return [];

    if (target.AbilityCheck(Attribute.Constitution, DC, gs.Rng))
      return [];

    target.Stats[Attr].Change(Amt);
    target.Traits.Add(this);

    if (target is Player player && (Attr == Attribute.HP || Attr == Attribute.Constitution))
    {
      player.CalcHP();
    }

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [ CalcMessage(target) ];
  }

  string Remove(Actor victim)
  {    
    victim.Stats[Attr].Change(-Amt);
    victim.Traits.Remove(this);
    
    if (victim is Player)
    {
      return $"Your {Attr} returns to normal.";
    }

     if (victim is Player player && (Attr == Attribute.HP || Attr == Attribute.Constitution))
    {
      player.CalcHP();
    }

    return "";
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn)
    {
      gs.StopListening(GameEventType.EndOfRound, this);

      if (gs.ObjDb.GetObj(OwnerID) is Actor victim)
      {
        string txt = Remove(victim);
        gs.UIRef().AlertPlayer(txt);
      }
    }
  }
}

class BlindTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "You can see again!";

  public override List<string> Apply(Actor target, GameState gs)
  {
    // I imagine there will eventually be an immunity to blindess trait?
    List<string> msgs = [];

    OwnerID = target.ID;
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    
    if (!target.Traits.Contains(this)) 
      target.Traits.Add(this);

    if (target is Player) 
      msgs.Add("You cannot see a thing!");

    return msgs;
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    var victim = (Actor?)gs.ObjDb.GetObj(OwnerID);
    if (victim is null)
      return;

    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      Expired = true;
      Remove(gs);
    }
  }

  public override string AsText() => $"Blind#{OwnerID}#{ExpiresOn}#{SourceId}";
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

    return new UseResult(action, acc, false);
  }

  public void Used() {}
}

// I am making the assumption it will only be the Player who uses Recall.
class RecallTrait : BasicTrait, IGameEventListener
{
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public ulong ObjId => 0; // This trait will always/only be applied to the player (I think...)
  
  public override string AsText() => $"Recall#{ExpiresOn}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
    if (gs.Campaign is null || gs.Campaign.FactDb is null)
      throw new Exception("Checking for dungeon entrance fact: Campaign and History should never be null");

    if (player.Loc.DungeonID == 0)
    {
      gs.UIRef().AlertPlayer("You sudenly teleport exactly 1 cm to the left.");
      return;
    }

    LocationFact? entrance = (LocationFact?)gs.Campaign.FactDb.FactCheck("Dungeon Entrance");
    if (entrance is not null)
    {
      gs.PlayerEntersLevel(player, 0, 0);
      var start = player.Loc;
      player.Loc = entrance.Loc;
      string moveMsg = gs.ResolveActorMove(player, start, entrance.Loc);
      gs.RefreshPerformers();
      gs.PrepareFieldOfView();
      gs.UIRef().AlertPlayer("A wave of vertigo...");
      gs.UIRef().AlertPlayer(moveMsg);      
    }
  }
}

class RegenerationTrait : BasicTrait, IGameEventListener
{
  public int Rate { get; set; }
  public ulong ActorID { get; set; }
  public ulong ObjId => ActorID;
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  
  public override string AsText() => $"Regeneration#{Rate}#{ActorID}#{Expired}#{ExpiresOn}#{SourceId}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
  protected override string ExpiryMsg => "Your vision returns to normal.";

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [ $"{target.FullName.Capitalize()} can see into the beyond!" ];
  }

  public override string AsText() => $"SeeInvisible#{OwnerID}#{ExpiresOn}";
}

class InvisibleTrait : BasicTrait, IGameEventListener
{
  public ulong ActorID { get; set; }
  public ulong ObjId => ActorID;
  public bool Expired { get; set; }
  public bool Listening => true;
  
  public override string AsText() => $"Invisible#{ActorID}#{Expired}#{ExpiresOn}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
  public ulong ObjId => OwnerID;
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  
  public override string AsText() => $"Countdown#{OwnerID}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn < ExpiresOn)
      return;

    Expired = true;

    if (gs.ObjDb.GetObj(OwnerID) is Item item)
    {      
      // Alert! Alert! This is cut-and-pasted from ExtinguishAction()
      if (item.ContainedBy > 0)
      {
        var owner = gs.ObjDb.GetObj(item.ContainedBy);
        if (owner is not null)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          ((Actor)owner).Inventory.Remove(item.Slot, 1);
        }
      }

      gs.ObjDb.RemoveItemFromGame(loc, item);

      // This is rather tied to Fog Cloud atm -- I should perhaps provide an
      // expiry message that can be set for each trait
      string msg = MsgFactory.Phrase(item.ID, Verb.Dissipate, 0, 1, false, gs);
      gs.UIRef().AlertPlayer(msg);
    }
  }
}

class CrusherTrait : ActionTrait
{
  public int DmgDie { get; set; }
  public int DmgDice { get; set; }

  public ulong Victim(Mob mob, GameState gs)
  {
    foreach (Loc adj in Util.Adj8Locs(mob.Loc))
    {
      if (gs.ObjDb.Occupant(adj) is not Actor actor)
        continue;

      foreach (Trait t in actor.Traits)
      {
        if (t is GrappledTrait grappled && grappled.GrapplerID == mob.ID)
          return actor.ID;
      }
    }

    return 0;
  }

  public override bool Available(Mob mob, GameState gs)
  {
    return Victim(mob, gs) != 0;
  }

  public override string AsText() => $"Crusher#{DmgDie}#{DmgDice}";
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
  public ulong ObjId => OwnerID;
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

  public string ReceiveEffect(DamageType damageType, GameState gs, Item item, Loc loc)
  {
    if (Lit && damageType == DamageType.Wet)
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
      return new UseResult(null, null, true, msg);
    }
    else if (Fuel > 0)
    {
      Lit = true;
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      
      item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
      item.Traits.Add(new LightSourceTrait() { Radius = 5 });

      return new UseResult(null, null, true, $"The {item!.Name} sparks to life!");
    }
    else
    {
      return new UseResult(null, null, false, $"That {item!.Name} is burnt out!");
    }
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
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
        if (item.ContainedBy > 0 && gs.ObjDb.GetObj(item.ContainedBy) is Actor owner)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          owner.Inventory.Remove(item.Slot, 1);
        }

        string msg = MsgFactory.Phrase(item.ID, Verb.BurnsOut, 0, 1, false, gs);
        gs.UIRef().AlertPlayer(msg);
      }
    }
  }
}

class UndeadTrait : Trait
{
  public override string AsText() => $"Undead";
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
      return new UseResult(new PassAction(), null, true, "Nothing happens");
    }

    ulong itemId = item is not null ? item.ID : 0;
    return new UseResult(new UseWandAction(gs, user, this, itemId), null, true, "");  
  }

  public void Used() => --Charges;
}

class WaterWalkingTrait : Trait
{
  public override string AsText() => $"WaterWalking#{SourceId}";
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
    { "ACMod", (pieces, gameObj) => 
      { 
        ulong sourceId = pieces.Length > 2 ? ulong.Parse(pieces[2]) : 0;
        return new ACModTrait() { ArmourMod = int.Parse(pieces[1]), SourceId = sourceId };
      }
    },
    { "Adjective", (pieces, gameObj) => new AdjectiveTrait(pieces[1]) },
    { "Affixed", (pieces, gameObj) => new AffixedTrait() },
    { "Alacrity", (pieces, gameObj) =>  
      new AlacrityTrait() 
      { 
        Amt = double.Parse(pieces[1]),
        SourceId = pieces.Length > 2 ? ulong.Parse(pieces[2]) : 0
      }
    },
    { "Allies", (pieces, gameObj) => { var ids = pieces[1].Split(',').Select(ulong.Parse).ToList(); return new AlliesTrait() { IDs = ids }; } },
    { "Ammo", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[3], out DamageType ammoDt);
        return new AmmoTrait() { DamageDie = int.Parse(pieces[1]), NumOfDie = int.Parse(pieces[2]), DamageType = ammoDt, Range = int.Parse(pieces[4]) };
      }
    },
    { "AppleProducer", (pieces, gameObj) => new AppleProducerTrait() { OwnerID = ulong.Parse(pieces[1]) } },
    { "Armour", (pieces, gameObj) => { Enum.TryParse(pieces[1], out ArmourParts part);
      return new ArmourTrait() { Part = part, ArmourMod = int.Parse(pieces[2]), Bonus = int.Parse(pieces[3]) }; }
    },
    { "AttackVerb", (pieces, gameObj) => {
      Enum.TryParse(pieces[1], out Verb verb);
      return new AttackVerbTrait(verb);
    }},
    { "AuraOfProtection", (pieces, gameObj) => new AuraOfProtectionTrait() { HP = int.Parse(pieces[1])}},
    { "Axe", (pieces, gameObj) => new AxeTrait() },
    { 
      "Berzerk", (pieces, gameObj) => pieces.Length == 1 ? new BerzerkTrait() : new BerzerkTrait() { SourceId = ulong.Parse(pieces[1])} 
    },
    { "Blind", (pieces, gameObj) => 
      new BlindTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        ExpiresOn = pieces[2] == "max" ? ulong.MaxValue : ulong.Parse(pieces[2]),
        SourceId = pieces.Length > 3 ? ulong.Parse(pieces[3]) : 0
      }
    },
    { "Block", (pieces, gameObj) => new BlockTrait() },
    { "BoostMaxStat", (pieces, gameObj) => {
      Enum.TryParse(pieces[1], out Attribute attr);
      return new BoostMaxStatTrait() { Stat = attr, Amount = int.Parse(pieces[2])}; }},
    { "Bow", (pieces, gameObj) => new BowTrait() },
    { "Brainless", (pieces, gameObj) => new BrainlessTrait() },
    { "CanApply", (pieces, gameObj) => new CanApplyTrait() },
    { "Carries", (pieces, gameObj) => new CarriesTrait() { ItemName = pieces[1], Chance = int.Parse(pieces[2]) }},
    { "Cleave", (pieces, gameObj) => new CleaveTrait() },
    { "CoinsLoot", (pieces, gameObj) => new CoinsLootTrait() { Min = int.Parse(pieces[1]), Max = int.Parse(pieces[2])} },
    { "Confused", (pieces, gameObj) => new ConfusedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "ConfusingScream", (pieces, gameObj) => new ConfusingScreamTrait() { Radius = int.Parse(pieces[1]), DC = int.Parse(pieces[2]), Cooldown = ulong.Parse(pieces[3]) }},
    { "Construct", (pieces, gameObj) => new ConstructTrait() },
    { "Consumable", (pieces, gameObj) => new ConsumableTrait() },
    { "Corrosive", (pieces, gameObj) => new CorrosiveTrait() },
    { "Countdown", (pieces, gameObj) => new CountdownTrait() { OwnerID = ulong.Parse(pieces[1]), Expired = bool.Parse(pieces[2]) }},
    { "Crusher", (pieces, gameObj) => new CrusherTrait() { DmgDie = int.Parse(pieces[1]), DmgDice = int.Parse(pieces[2]) } },
    { "Cudgel", (pieces, gameObj) => new CudgelTrait() },
    { "Cursed", (pieces, gameObj) => new CursedTrait() },
    { "Cutpurse", (pieces, gameObj) =>
      {
        ulong sourceId = pieces.Length > 1 ? ulong.Parse(pieces[1]) : 0;
        return new CutpurseTrait() { SourceId = sourceId };
      } 
    },
    { "Damage", (pieces, gameObj) => 
      {
        Enum.TryParse(pieces[3], out DamageType dt);
        ulong sourceId = pieces.Length >= 5 ? ulong.Parse(pieces[4]) : 0;
        return new DamageTrait() 
        { 
          DamageDie = int.Parse(pieces[1]), 
          NumOfDie = int.Parse(pieces[2]), 
          DamageType = dt,
          SourceId = sourceId
        }; 
      }
    },
    { "Description", (pieces, gameObj) => new DescriptionTrait(pieces[1]) },
    { "DeathMessage", (pieces, gameObj) => new DeathMessageTrait() { Message = pieces[1] } },
    { "DialogueScript", (pieces, gameObj) => new DialogueScriptTrait() { ScriptFile = pieces[1] } },
    { "Disguise", (pieces, gameObj) =>  new DisguiseTrait() { Disguise = Glyph.TextToGlyph(pieces[1]), TrueForm = Glyph.TextToGlyph(pieces[2]), DisguiseForm = pieces[3] }},
    { "Displacement", (pieces, gameObj) => new DisplacementTrait() },
    { "Divider", (pieces, gameObj) => new DividerTrait() },
    { "Dodge", (pieces, gameObj) => 
      { 
        int rate = int.Parse(pieces[1]);
        ulong sourceId = pieces.Length > 2 ? ulong.Parse(pieces[2]) : 0;
          return new DodgeTrait() { Rate = int.Parse(pieces[1]), SourceId = sourceId };
      }
    },
    { "Drop", (pieces, gameObj) => new DropTrait() { ItemName = pieces[1], Chance = int.Parse(pieces[2]) }},
    { "Edible", (pieces, gameObj) => new EdibleTrait() },
    { "Exhausted", (pieces, gameObj) =>  new ExhaustedTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "FallenAdventurer", (pieces, gameObj) => new FallenAdventurerTrait() },
    { "FearsomeBellow", (pieces, gameObj) => new FearsomeBellowTrait()
      {
        Radius = int.Parse(pieces[1]), DC = int.Parse(pieces[2]), 
        Cooldown = ulong.Parse(pieces[3]), Name=pieces[0]
      }
    },
    { "FinalBoss", (pieces, gameObj) => new FinalBossTrait() },
    { "Finesse", (pieces, gameObj) => new FinesseTrait() },
    { "FireBreath", (pieces, gameObj) => new FireBreathTrait()
      {
        DmgDie = int.Parse(pieces[1]),
        DmgDice = int.Parse(pieces[2]),
        Range = int.Parse(pieces[3]),
        Cooldown = ulong.Parse(pieces[4]),
        Name=pieces[0]
      }
    },
    { "Flammable", (pieces, gameObj) => new FlammableTrait() },
    { "Floating", (pieces, gameObj) => new FloatingTrait() },
    { "Flying", (pieces, gameObj) => new FlyingTrait() },
    { "Frightened", (pieces, gameObj) => new FrightenedTrait()
      { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) }
    },
    { "Lame", (pieces, gameObj) =>  new LameTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "GoodMagicLoot", (pieces, gameObj) => new GoodMagicLootTrait() },
    { "Grants", (pieces, gameObj) => {
      string[] grantedTraits = pieces[1].Split(';').Select(s => s.Replace('&', '#')).ToArray();
      return new GrantsTrait() { TraitsGranted = grantedTraits };
     }},
    { "Grappled", (pieces, gameObj) => new GrappledTrait() { VictimID = ulong.Parse(pieces[1]), GrapplerID = ulong.Parse(pieces[2]), DC = int.Parse(pieces[3]) } },
    { "Grappler", (pieces, gameObj) => new GrapplerTrait { DC = int.Parse(pieces[1]) }},
    { "HealAllies", (pieces, gameObj) => new HealAlliesTrait() { Cooldown = ulong.Parse(pieces[1]) }},
    { "Heroism", (pieces, gameObj) => new HeroismTrait() 
      { 
        OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), SourceId = ulong.Parse(pieces[3])
      } 
    },
    { "Hidden", (pieces, gameObj) => new HiddenTrait() },
    { "Homebody", (pieces, gameObj) => new HomebodyTrait() { Loc = Loc.FromStr(pieces[1]), Range = int.Parse(pieces[2]) }},
    { "Illusion", (pieces, gameObj) => new IllusionTrait() { SourceId = ulong.Parse(pieces[1]), ObjId = ulong.Parse(pieces[2]) } },
    { "Immobile", (pieces, gameObj) => new ImmobileTrait() },
    { "Immunity", (pieces, gameObj) => {
      Enum.TryParse(pieces[1], out DamageType dt);
      ulong expiresOn = pieces.Length > 2 ? ulong.Parse(pieces[2]) : ulong.MaxValue;
      return new ImmunityTrait() { Type = dt, ExpiresOn = expiresOn }; }},
    { "Impale", (pieces, gameObj) => new ImpaleTrait() },
    { "InPit", (pieces, gameObj) => new InPitTrait() },
    { "Intelligent", (pieces, gameObj) => new IntelligentTrait() },
    { "Invincible", (pieces, gameObj) => new InvincibleTrait() },
    { "Invisible", (pieces, gameObj) =>
      new InvisibleTrait()
      {
        ActorID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Expired = bool.Parse(pieces[2]),
        ExpiresOn = pieces[3] == "max" ? ulong.MaxValue : ulong.Parse(pieces[3])
      }
    },
    { "KnockBack", (pieces, gameObj) => new KnockBackTrait() },
    { "Levitation", (pieces, gameObj) => new LevitationTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "LightSource", (pieces, gameObj) => new LightSourceTrait() { OwnerID = pieces[1] == "owner" ? gameObj!.ID :  ulong.Parse(pieces[1]), Radius = int.Parse(pieces[2]) } },
    { "LightStep", (pieces, gameObj) => new LightStepTrait() },
    { "Likeable", (pieces, gameObj) => new LikeableTrait() },
    { "Melee", (pieces, gameObj) => {
      Enum.TryParse(pieces[3], out DamageType dt);
      return new MobMeleeTrait() {
          Name = "Melee", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = 1, MaxRange = 1, DamageType = dt }; }},
    { "Metal", (pieces, gameObj) => new MetalTrait() { Type = (Metals)int.Parse(pieces[1]) } },
    { "MiniBoss5", (pieces, gameObj) => new MiniBoss5Trait() },
    { "Missile", (pieces, gameObj) => {
      Enum.TryParse(pieces[5], out DamageType dt);
      return new MobMissileTrait() {
          Name = "Missile", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]), DamageType = dt }; }},
    { "MobMelee", (pieces, gameObj) => {
      Enum.TryParse(pieces[5], out DamageType dt);
      return new MobMeleeTrait() {
          Name = "Melee", DamageDie = int.Parse(pieces[3]), DamageDice = int.Parse(pieces[4]),
          MinRange = 1, MaxRange = 1, DamageType = dt }; }},
    { "MobMissile", (pieces, gameObj) => {
      Enum.TryParse(pieces[5], out DamageType dt);
      return new MobMissileTrait() {
          Name = "Missile", DamageDie = int.Parse(pieces[1]), DamageDice = int.Parse(pieces[2]),
          MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]), DamageType = dt }; }},
    { "Mosquito", (pieces, gameObj) => new MosquitoTrait() },
    { "Named", (pieces, gameObj) => new NamedTrait() },
    { "Nausea", (pieces, gameObj) => new NauseaTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "NauseousAura", (pieces, gameObj) => new NauseousAuraTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Strength = int.Parse(pieces[2])
      }
    },
    { "OnFire", (pieces, gameObj) => new OnFireTrait()
    {
      Expired = bool.Parse(pieces[1]), OwnerID = pieces[2] == "owner" ? gameObj!.ID : ulong.Parse(pieces[2]),
      Lifetime = pieces[3] == "max" ? int.MaxValue :  int.Parse(pieces[3]) , Spreads = bool.Parse(pieces[4]) }
    },
    { "Owned", (pieces, gameObj) => new OwnedTrait() { OwnerIDs = pieces[1].Split(',').Select(ulong.Parse).ToList() } },
    { "Opaque", (pieces, gameObj) => new OpaqueTrait() },
    { "OwnsItem", (pieces, gameObj) => new OwnsItemTrait() { ItemID = ulong.Parse(pieces[1]) } },
    { "Paralyzed", (pieces, gameObj) => new ParalyzedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "ParalyzingGaze", (pieces, gameObj) => new ParalyzingGazeTrait() { DC = int.Parse(pieces[1]) } },
    { "Plant", (pieces, gameObj) => new PlantTrait() },
    { "Plural", (pieces, gameObj) => new PluralTrait() },
    { "PoisonCoated", (pieces, gameObj) => new PoisonCoatedTrait() },
    { "Poisoned", (pieces, gameObj) => new PoisonedTrait()
      { DC = int.Parse(pieces[1]), Strength = int.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]),
        ExpiresOn = ulong.Parse(pieces[4]), Duration = int.Parse(pieces[5])
      }
    },
    { "Poisoner", (pieces, gameObj) => new PoisonerTrait() { DC = int.Parse(pieces[1]), Strength = int.Parse(pieces[2]), Duration = int.Parse(pieces[3]) } },
    { "Polearm", (pieces, gameObj) => new PolearmTrait() },
    { "PoorLoot", (pieces, gameObj) => new PoorLootTrait() },
    { "Rage", (pieces, gameObj) => new RageTrait(gameObj as Actor
        ?? throw new ArgumentException("gameObj must be an Actor for RageTrait")) },
    { "RangedSpellAction", (pieces, gameObj) =>
    new RangedSpellActionTrait() { Name = pieces[1], Cooldown = ulong.Parse(pieces[2]),
        MinRange = int.Parse(pieces[3]), MaxRange = int.Parse(pieces[4]) }},
    { "Reach", (pieces, gameObj) => new ReachTrait() },
    { "Readable", (pieces, gameObj) => new ReadableTrait(pieces[1].Replace("<br/>", "\n")) { OwnerID = ulong.Parse(pieces[2]) } },
    { "Recall", (pieces, gameObj) => new RecallTrait() { ExpiresOn = ulong.Parse(pieces[1]), Expired = bool.Parse(pieces[2]) } },
    { "Regeneration", (pieces, gameObj) => {
      ulong sourceId = pieces.Length > 5 ? ulong.Parse(pieces[5]) : 0;
      return new RegenerationTrait()
        {
          Rate = int.Parse(pieces[1]),
          ActorID = pieces[2] == "owner" ? gameObj!.ID : ulong.Parse(pieces[2]),
          Expired = bool.Parse(pieces[3]),
          ExpiresOn = pieces[4] == "max" ? ulong.MaxValue : ulong.Parse(pieces[4]),
          SourceId = sourceId
        };
    } },
    { "Relationship", (pieces, gameObj) => new RelationshipTrait() { Person1ID = ulong.Parse(pieces[1]), Person2ID = ulong.Parse(pieces[2]), Label = pieces[3] } },
    { "Repugnant", (pieces, gameObj) =>
      {
        ulong sourceId = pieces.Length > 1 ? ulong.Parse(pieces[1]) : 0;
        return new RepugnantTrait() { SourceId = sourceId };
      }
    },
    { "Resistance", (pieces, gameObj) => 
      {
        Enum.TryParse(pieces[1], out DamageType rdt);
        ulong expiresOn = pieces.Length >= 3 ? ulong.Parse(pieces[2]) : ulong.MaxValue;
        ulong ownerID = pieces.Length >= 4 ? ulong.Parse(pieces[3]) : 0;
        ulong sourceId = pieces.Length >= 5 ? ulong.Parse(pieces[4] ): 0;
        return new ResistanceTrait() { Type = rdt, ExpiresOn = expiresOn, OwnerID = ownerID, SourceId = sourceId
        }; 
      }
    },
    { "Resting", (pieces, gameObj) => new RestingTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "Retribution", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[1], out DamageType dt);
        return new RetributionTrait() { Type = dt, DmgDie = int.Parse(pieces[2]), NumOfDice = int.Parse(pieces[3]) };
      }
    },
    { "ResistBlunt", (pieces, gameObj) => new ResistBluntTrait() },
    { "ResistPiercing", (pieces, gameObj) => new ResistPiercingTrait() },
    { "ResistSlashing", (pieces, gameObj) => new ResistSlashingTrait() },
    { "Robbed", (pieces, gameObj) => new RobbedTrait() },
    { "RumBreath", (pieces, gameObj) => new RumBreathTrait() { Range=int.Parse(pieces[1]), Cooldown=ulong.Parse(pieces[2]), Name="RumBreath" } },
    { "Rusted", (pieces, gameObj) => new RustedTrait() { Amount = (Rust)int.Parse(pieces[1]) } },
    { "SeeInvisible", (pieces, gameObj) => new SeeInvisibleTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "SideEffect", (pieces, gameObj) => new SideEffectTrait() { Odds = int.Parse(pieces[1]), Effect = string.Join('#', pieces[2..] ) } },
    { "Shriek", (pieces, gameObj) => new ShriekTrait() { Cooldown = ulong.Parse(pieces[1]), ShriekRadius = int.Parse(pieces[2]) }
    },
    { "Shunned", (pieces, gameObj) => new ShunnedTrait() },
    { "SilverAllergy", (pieces, gameObj) => new SilverAllergyTrait() },
    { "Sleeping", (pieces, gameObj) => new SleepingTrait() },
    { "SpellAction", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[3], out ActionType at);
        return new SpellActionTrait() { Name = pieces[1], Cooldown = ulong.Parse(pieces[2]), ActionType = at };
      }
    },
    { "Stabby", (pieces, gameObj) => new StabbyTrait() },
    { "Stackable", (pieces, gameObj) => new StackableTrait() },
    { "StatBuff", (pieces, gameObj) =>
    {
      Enum.TryParse(pieces[3], out Attribute attr);
      ulong expires = pieces[2] == "max" ? ulong.MaxValue : ulong.Parse(pieces[2]);
      ulong s;
      if (pieces[5] == "item" && gameObj is not null)
        s = gameObj.ID;
      else
        s = ulong.Parse(pieces[5]);
      return new StatBuffTrait()
      {
        OwnerID = ulong.Parse(pieces[1]), ExpiresOn = expires, Attr = attr,
        Amt = int.Parse(pieces[4]), SourceId = s
      };
    } },
    { "StatDebuff", (pieces, gameObj) =>
    {
      Enum.TryParse(pieces[3], out Attribute attr);
      ulong expires = pieces[2] == "max" ? ulong.MaxValue : ulong.Parse(pieces[2]);
      return new StatDebuffTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = expires, Attr = attr, Amt = int.Parse(pieces[4]) };
    }},
    { "Sticky", (pieces, gameObj) => new StickyTrait() },
    { "Stress", (pieces, gameObj) => 
      {
        Enum.TryParse(pieces[1], out StressLevel stress);
        return new StressTrait() { Stress = stress, OwnerID = ulong.Parse(pieces[2]) };
      }
    },
    { "Summon", (pieces, gameObj) => new SummonTrait() { Name = pieces[0], Cooldown = ulong.Parse(pieces[1]), Summons = pieces[2], Quip = pieces[3] } },
    { "SummonUndead", (pieces, gameObj) => new SummonUndeadTrait() { Cooldown = ulong.Parse(pieces[1]), Name=pieces[0] }},
    { "Sword", (pieces, gameObj) => new SwordTrait() },
    { "Teflon", (pieces, gameObj) => new TeflonTrait() },
    { "Telepathy", (pieces, gameObj) => new TelepathyTrait() { ExpiresOn = ulong.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]) } },
    { "Torch", (pieces, gameObj) => new TorchTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Lit = bool.Parse(pieces[2]),
        Fuel = int.Parse(pieces[3])
      }
    },
    { "TwoHanded", (pieces, gameObj) => new TwoHandedTrait() },
    { "Undead", (pieces, gameObj) => new UndeadTrait() },
    { "UseSimple", (pieces, gameObj) => new UseSimpleTrait(pieces[1]) },
    { "VaultKey", (pieces, GameObj) => new VaultKeyTrait(Loc.FromStr(pieces[1])) },
    { "Versatile", (pieces, GameObject) =>
    {
      Enum.TryParse(pieces[4], out DamageType dt);
      DamageTrait oneHanded = new DamageTrait() { DamageDie = int.Parse(pieces[2]), NumOfDie = int.Parse(pieces[3]), DamageType = dt };
      Enum.TryParse(pieces[9], out dt);
      DamageTrait twoHanded = new DamageTrait() { DamageDie = int.Parse(pieces[7]), NumOfDie = int.Parse(pieces[8]), DamageType = dt };
      return new VersatileTrait(oneHanded, twoHanded);
    } },
    { "Vicious", (pieces, gameObj) => new ViciousTrait() { Scale = double.Parse(pieces[1]) }},
    { "Villager", (pieces, gameObj) => new VillagerTrait() },
    { "Wand", (pieces, gameObj) => new WandTrait() { Charges = int.Parse(pieces[1]), IDed = bool.Parse(pieces[2]), Effect = pieces[3] } },
    { "WaterWalking", (pieces, gameObj) => 
      pieces.Length > 1 ? new WaterWalkingTrait() { SourceId = ulong.Parse(pieces[1])} : new WaterWalkingTrait()
    },
    { "Weaken", (pieces, gameObj) =>  new WeakenTrait() { DC = int.Parse(pieces[1]), Amt = int.Parse(pieces[2]) } },
    { "WeaponBonus", (pieces, gameObj) => new WeaponBonusTrait() { Bonus = int.Parse(pieces[1]) } },
    { "WeaponSpeed", (pieces, gameObj) => new WeaponSpeedTrait() { Cost = double.Parse(pieces[1])} },
    { "Worshiper", (pieces, gameObj) => new WorshiperTrait() { Altar = Loc.FromStr(pieces[1]), Chant = pieces[2] } },
    { "Scroll", (pieces, gameObj) => new ScrollTrait() }
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