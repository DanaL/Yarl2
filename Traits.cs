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

using System.Text;

namespace Yarl2;

record UseResult(Action? ReplacementAction, string Message = "");

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
  public virtual ulong SourceId { get; set;}

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

class AdjectiveTrait(string adj) : Trait
{
  public string Adj { get; set; } = adj;

  public override string AsText() => $"Adjective#{Adj}";
}

class ACModTrait : BasicTrait
{
  public int ArmourMod { get; set; }
  public override string AsText() => $"ACMod#{ArmourMod}#{SourceId}";
}

class AffixedTrait : Trait
{
  public override string AsText() => $"Affixed";
}

class ArtifactTrait : Trait
{
  public override string AsText() => $"Artifact";
}

class AttackModTrait : Trait
{
  public int Amt { get; set; }
  public override string AsText() => $"AttackMod#{Amt}#{SourceId}";
}

class AttackVerbTrait(string verb) : Trait
{
  public string Verb { get; set; } = verb;

  public override string AsText() => $"AttackVerb#{Verb}";
}

class AuraMessageTrait : Trait, IGameEventListener
{
  public bool Expired { get => false; set { } }
  public bool Listening => true;
  public ulong ObjId { get; set; }
  public int Radius { get; set; }
  public ulong SourceID => ObjId;
  public GameEventType EventType => GameEventType.EndOfRound;
  public string Message { get; set; } = "";
  bool PlayerSeen { get; set; }

  public override string AsText() => $"AuraMessage#{ObjId}#{Radius}#{Message}";
  
  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {    
    if (gs.ObjDb.GetObj(ObjId) is GameObj source)
    {
      foreach (Loc sq in FieldOfView.CalcVisible(Radius, source.Loc, gs.CurrentMap, gs.ObjDb).Keys)
      {
        if (gs.ObjDb.Occupant(sq) is Player)
        {
          if (!PlayerSeen)
          {
            gs.UIRef().AlertPlayer(Message);
            gs.UIRef().SetPopup(new Popup(Message, "", -1, -1));
          }

          PlayerSeen = true;
          return;
        }
      }

      PlayerSeen = false;
    }
  }
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

abstract class BlessingTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "Your blessing fades.";
  public abstract string Description(Actor owner);
}

// For items that can be used by the Apply command but don't need to
// implement IUseable
class CanApplyTrait : Trait
{
  public override string AsText() => "CanApply";
}

class CarriesTrait : LootTrait
{
  public string ItemName { get; set; } = "";
  public int Chance { get; set; }

  public override string AsText() => $"Carries#{ItemName}#{Chance}";
}

class CastTrait : Trait, IUSeable
{
  public string Spell { get; set; } = "";

  public override string AsText() => $"Cast#{Spell}";

  public UseResult Use(Actor caster, GameState gs, int row, int col, Item? item)
  {          
    return new UseResult(new UseSpellItemAction(gs, caster, "gust of wind", item));
  }

  public void Used(){ }
}

class DragonCultBlessingTrait : BlessingTrait
{
  const int MP_COST = 3;

  public override string Description(Actor owner) => "Dragon cult clessing";

  public override List<string> Apply(Actor _, GameState gs)
  {
    ACModTrait ac = new() { ArmourMod = 3, SourceId = Constants.DRAGON_GOD_ID };
    gs.Player.Traits.Add(ac);

    if (!gs.Player.SpellsKnown.Contains("breathe fire"))
      gs.Player.SpellsKnown.Add("breathe fire");

    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(MP_COST);
      mp.Change(MP_COST);
    }
    else
    {
      gs.Player.Stats[Attribute.MagicPoints] = new Stat(MP_COST);
    }

    GoldSnifferTrait sniffer = new() { SourceId = Constants.DRAGON_GOD_ID };
    sniffer.Apply(gs.Player, gs);
    gs.Player.Traits.Add(sniffer);

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.SpellsKnown.Remove("breather fire");
    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(-MP_COST);
    }

    List<Trait> playerTraits = [.. gs.Player.Traits];
    foreach (Trait t in playerTraits)
    {
      if (t is TemporaryTrait temp && temp.SourceId == SourceId)
      {
        temp.Remove(gs);
      }
    }

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"DragonCultBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";
}

class ChampionBlessingTrait : BlessingTrait
{
  public override List<string> Apply(Actor granter, GameState gs)
  {
    int piety = gs.Player.Stats[Attribute.Piety].Max;

    ACModTrait ac = new() { ArmourMod = 1 + piety, SourceId = granter.ID };
    gs.Player.Traits.Add(ac);
    AuraOfProtectionTrait prot = new() { HP = 10 + piety * 5, SourceId = granter.ID };
    prot.Apply(gs.Player, gs);
    AttackModTrait amt = new() { Amt = 1 + piety, SourceId = granter.ID };
    gs.Player.Traits.Add(amt);

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"ChampionBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";

  public override string Description(Actor owner)
  {
    string s = "You have the Champion Blessing. It grants";

    AuraOfProtectionTrait? prot = owner.Traits.OfType<AuraOfProtectionTrait>()
                              .Where(t => t.SourceId == SourceId)
                              .FirstOrDefault();

    ACModTrait? acMod = owner.Traits.OfType<ACModTrait>()
                              .Where(t => t.SourceId == SourceId)
                              .FirstOrDefault();
    if (acMod is not null)
    {
      s += $" a [lightblue +{acMod.ArmourMod}] AC bonus";
    }

    AttackModTrait? am = owner.Traits.OfType<AttackModTrait>()
                              .Where(t => t.SourceId == SourceId)
                              .FirstOrDefault();
    if (am is not null)
    {
      s += prot is null ? " and " : ", ";
      s += $"a [lightblue +{am.Amt}] attack bonus";
    }

    if (prot is not null)
    {
      s += ", and a [lightblue ward] against damage";
    }

    s += ".";

    return s;
  }
}

class SwallowedTrait : Trait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public ulong SwallowerID { get; set; }
  public bool Expired { get => false; set {} }
  public Loc Origin { get; set; }
  public bool Listening => true;
  public ulong ObjId => VictimID;
  public GameEventType EventType => GameEventType.Death;
  public override ulong SourceId => SwallowerID;

  public override string AsText() => $"Swallowed#{VictimID}#{SwallowerID}#{Origin}";
  public void EventAlert(GameEventType eventType, GameState gs, Loc loc) => Remove(gs);

  public void Remove(GameState gs)
  {
    if (gs.ObjDb.GetObj(VictimID) is Actor victim)
    {
      victim.Traits.Remove(this);
      gs.RemoveListener(this);
      if (gs.LastPlayerFoV.Contains(victim.Loc))
      {
        string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} expelled!";
        gs.UIRef().AlertPlayer(s);
      }

      if (gs.ObjDb.GetObj(SwallowerID) is Actor swallower)
      {
        List<Trait> toKeep = [];
        foreach (Trait t in swallower.Traits)
        {
          if (t is FullBellyTrait fbt)
          {
            gs.RemoveListener(fbt);
            continue;
          }

          toKeep.Add(t);
        }

        swallower.Traits = toKeep;
      }

      Loc start = victim.Loc;
      gs.ActorEntersLevel(victim, Origin.DungeonID, Origin.Level);
      gs.ResolveActorMove(victim, start, Origin);
      victim.Loc = Origin;      
      gs.FlushPerformers();
      gs.PrepareFieldOfView();
    }
  }
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

class HolyTrait : Trait
{
  public override string AsText() => $"Holy";
}

// Trait for mobs who won't wander too far (normally) from
// a particular location
class HomebodyTrait : Trait
{
  public Loc Loc { get; set; }
  public int Range { get; set; }

  public override string AsText() => $"Homebody#{Loc}#{Range}";
}

class MosquitoTrait : Trait
{
  public override string AsText() => "Mosquito";
}

class AbjurationBellTrait : Trait, IUSeable
{
  public override string AsText() => "AbjurationBell";

  public UseResult Use(Actor caster, GameState gs, int row, int col, Item? item)
  {    
    Action action = new CloseMenuAction(gs, 1.0);

    StringBuilder sb = new();
    sb.AppendLine("The Abjuration Bell rings out a clarion tone!");

    foreach (Loc adj in Util.Adj8Locs(caster.Loc))
    {
      List<Item> items = gs.ObjDb.ItemsAt(adj);
      if (items.Where(i => i.HasTrait<DemonVisageTrait>()).FirstOrDefault() is Item demonVisage)
      {
        sb.Append("\nThe demonic statue flares with red light, then explodes!");

        gs.ObjDb.RemoveItemFromGame(adj, demonVisage);

        Downstairs stairs = new("") { Destination =  adj };
        gs.CurrentMap.SetTile(adj.Row, adj.Col, stairs);

        Item light = ItemFactory.VirtualLight(Colours.BRIGHT_RED, Colours.DULL_RED, gs);
        gs.ObjDb.SetToLoc(adj, light);

        MessageAtLoc pal = new(adj, "[DULLRED An eerie red glow emanates from the depths beyond the stairs...]");
        gs.ObjDb.ConditionalEvents.Add(pal);

        break;
      }
    }

    gs.UIRef().SetPopup(new Popup(sb.ToString(), "", -1, -1, 35));

    return new UseResult(action);
  }

  public void Used() { }
}

class AcidSplashTrait : Trait
{
  public override string AsText() => "AcidSplash";

  public void HandleSplash(Actor target, GameState gs)
  {
    foreach (var adj in Util.Adj8Locs(target.Loc))
    {
      if (gs.ObjDb.Occupant(adj) is Actor victim)
      {
        string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} splashed by acid!";
        gs.UIRef().AlertPlayer(txt, gs, adj);
        int roll = gs.Rng.Next(4) + 1;
        var (hpLeftAfterAcid, acidMsg, _) = victim.ReceiveDmg([(roll, DamageType.Acid)], 0, gs, null, 1.0);

        SqAnimation hitAnim = new(gs, victim.Loc, Colours.WHITE, Colours.DARK_GREEN, victim.Glyph.Ch);
        gs.UIRef().RegisterAnimation(hitAnim);
        
        if (hpLeftAfterAcid < 1)
          gs.ActorKilled(victim, "acid", null);
        gs.UIRef().AlertPlayer(acidMsg, gs, adj);
      }
    }
  }
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

// Temproary (god, I hope!) trait to designate which Mobs use behaviour trees to
// determine their behaviour instead of my OG hard-coded AI. Once I've converted
// everyone to use BTs I can ditch this
class BehaviourTreeTrait : Trait
{
  public string Plan { get; set; } = "";

  public override string AsText() => $"BehaviourTree#{Plan}";
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

class CorruptionTrait : Trait
{
  public int Amt { get; set; }

  public override string AsText() => $"Corruption#{Amt}";
}

class DescriptionTrait(string text) : Trait
{
  public string Text { get; set; } = text;

  public override string AsText() => $"Description#{Text}";
}

class DesecratedTrait : Trait
{
  public override string AsText() => "Desecrated";
}

class DialogueScriptTrait : Trait
{
  public string ScriptFile { get; set; } = "";

  public override string AsText() => $"DialogueScript#{ScriptFile}";
}

class DiggingToolTrait : Trait
{
  public override string AsText() => "DiggingTool";
}

class DirectionTrait : Trait
{
  public Dir Dir { get; set; } = Dir.None;

  public override string AsText() => $"Direction#{Dir}";
}

class DodgeTrait : Trait
{
  public int Rate { get; set; }

  public override string AsText() => $"Dodge#{Rate}#{SourceId}";
}

class DoorKeyTrait : Trait
{
  public int DCMod { get; set; }

  public override string AsText() => $"DoorKey#{DCMod}";
}

class FinesseTrait : Trait
{
  public override string AsText() => "Finesse";
}

// If I add more rebuke types, I'll either generalize this class or create a
// a superclass RebukeTrait
class FireRebukeTrait : Trait
{
  public override string AsText() => $"FireRebuke#{SourceId}";

  public void Rebuke(Actor target, Actor attacker, GameState gs)
  {
    int hpLeft;
    string msg;

    UserInterface ui = gs.UIRef();
    ui.AlertPlayer("Flames lash out at your foe!");

    int dmg = gs.Rng.Next(1, 7);
    (hpLeft, msg, _) = attacker.ReceiveDmg([(dmg, DamageType.Fire)], 0, gs, target, 0);
    ui.AlertPlayer(msg, gs, attacker.Loc);
    SqAnimation anim = new(gs, attacker.Loc, Colours.BRIGHT_RED, Colours.TORCH_ORANGE, '\u22CF');
    ui.RegisterAnimation(anim);

    if (hpLeft < 1)
    {
      gs.ActorKilled(attacker, "fire", null);
    }

    if (gs.Rng.Next(10) == 0 || true)
    {
      ui.AlertPlayer("The fire flares around you!");

      foreach (var adj in Util.Adj8Locs(target.Loc))
      {
        gs.ApplyDamageEffectToLoc(adj, DamageType.Fire);

        if (gs.ObjDb.Occupant(adj) is Actor victim)
        {
          string victimName = MsgFactory.CalcName(victim, gs.Player).Capitalize();
          string txt = $"{victimName} {Grammar.Conjugate(victim, "is")} engulfed in flames!";
          gs.UIRef().AlertPlayer(txt, gs, adj);
          int roll = gs.Rng.Next(1, 7);
          (hpLeft, msg, _) = victim.ReceiveDmg([(roll, DamageType.Acid)], 0, gs, null, 1.0);

          anim = new(gs, adj, Colours.BRIGHT_RED, Colours.TORCH_YELLOW, '\u22CF');
          ui.RegisterAnimation(anim);

          if (hpLeft < 1)
            gs.ActorKilled(victim, "fire", null);
          gs.UIRef().AlertPlayer(msg, gs, adj);
        }
      }
    }
  }
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

class MageArmourTrait : TemporaryTrait
{
  protected override string ExpiryMsg => $"You feel less protected.";
  public override string AsText() => $"MageArmour#{ExpiresOn}#{OwnerID}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    foreach (Trait t in target.Traits)
    {
      if (t is MageArmourTrait ma)
      {
        ma.ExpiresOn += 150;
        return [];
      }
    }

    ExpiresOn = gs.Turn + 150;    
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    target.Traits.Add(this);
    OwnerID = target.ID;

    return [ "Magical runes surround you, then disappear. You feel protected." ];
  }
}

class MeleeDamageModTrait : Trait
{
  public int Amt { get; set; }
  public override string AsText() => $"MeleeDamageMod#{Amt}#{SourceId}";
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

class EmberBlessingTrait : BlessingTrait
{
  public override List<string> Apply(Actor granter, GameState gs)
  {
    ResistanceTrait resist = new()
    {
      SourceId = granter.ID,
      OwnerID = gs.Player.ID,
      ExpiresOn = ExpiresOn,
      Type = DamageType.Fire
    };
    // I'm not calling the Apply() method here because I don't want a separate listener
    // registered for the ResistanceTrait. This trait will be removed when 
    // EmberBlessingTrait is removed.
    gs.Player.Traits.Add(resist);

    DamageTrait dt = new()
    {
      SourceId = granter.ID,
      DamageType = DamageType.Fire,
      DamageDie = 6,
      NumOfDie = 1
    };
    gs.Player.Traits.Add(dt);

    FireRebukeTrait rebuke = new() { SourceId = granter.ID };
    gs.Player.Traits.Add(rebuke);

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"EmberBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";
  
  public override string Description(Actor owner) => "Ember blessing";
}

class PoorLootTrait : LootTrait
{
  public override string AsText() => "PoorLoot";
}

class PrisonerTrait : TemporaryTrait
{
  public Loc Cell { get; set; }
  public override ulong ObjId => SourceId;
  
  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(SourceId) is Actor prisoner)
    {
      if (prisoner.Loc != Cell)
      {
        prisoner.Traits.Remove(this);
        gs.RemoveListener(this);

        prisoner.Stats[Attribute.DialogueState].SetMax(PrisonerBehaviour.DIALOGUE_FREE);
      }
    }
  }

  public override string AsText() => $"Prisoner#{SourceId}#{Cell}";
  public override List<string> Apply(Actor target, GameState gs) => [];
}

class QuietTrait : Trait
{
  public override string AsText() => $"Quiet#{SourceId}";  
}

class AppleProducerTrait : Trait, IGameEventListener, IOwner
{
  public ulong OwnerID { get; set; }
  public bool Expired {  get; set;  }
  public bool Listening => true;
  public ulong ObjId => OwnerID;
  public GameEventType EventType => GameEventType.EndOfRound;

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

    List<Loc> trees = [.. locs.Where(l => gs.TileAt(l).IsTree())];
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
      player.Traits = [.. player.Traits.Where(t => t is not RestingTrait)];
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

class SleepingTrait : Trait
{
  public override string AsText() => "Sleeping";
}

class StickyTrait : BasicTrait
{
  public int DC => 13;

  public override string AsText() => "Sticky";
}

class StoneTabletTrait(string text) : BasicTrait, IUSeable, IOwner
{
  public ulong OwnerID { get; set; }
  readonly string _text = text;
  public override string AsText() => $"StoneTablet#{_text.Replace("\n", "<br/>")}#{OwnerID}";
  
  public UseResult Use(Actor user, GameState gs, int row, int col, Item? item)
  {
    List<string> lines = [.._text.Split('\n')];
    gs.UIRef().SetPopup(new Hint(lines, 3));

    Action action = new CloseMenuAction(gs, 1.0);
    
    return new UseResult(action);
  }

  public void Used() {}
}

class StressTrait : Trait 
{
  public StressLevel Stress { get; set; }
  public ulong OwnerID { get; set; }

  public override string AsText() => $"Stress#{Stress}#{OwnerID}";
}

class StressReliefAuraTrait : Trait, IGameEventListener
{
  public bool Expired { get => false; set { } }
  public bool Listening => true;
  public ulong ObjId { get; set; }
  public int Radius { get; set; }
  public ulong SourceID => ObjId;

  public GameEventType EventType => GameEventType.EndOfRound;

  static void CheckSq(GameState gs, Loc loc)
  {
    if (gs.ObjDb.Occupant(loc) is Actor actor)
    {
      if (actor.Stats.TryGetValue(Attribute.Nerve, out Stat? nerve))
        nerve.Change(2);
    }
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(ObjId) is GameObj source)
    {
      foreach (Loc sq in FieldOfView.CalcVisible(Radius, source.Loc, gs.CurrentMap, gs.ObjDb).Keys)
        CheckSq(gs, sq);      
    }    
  }

  public override string AsText() => $"StressReliefAura#{ObjId}#{Radius}";
}

class DividerTrait : Trait
{
  public override string AsText() => "Divider";
}

class FallenAdventurerTrait : Trait
{
  public override string AsText() => "FallenAdventurer";
}

class FeatherFallTrait : TemporaryTrait
{
  protected override string ExpiryMsg => "You no longer feel feathery.";

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    OwnerID = target.ID;

    return [ $"{target.FullName.Capitalize()} feel light as a feather!" ];
  }

  public override string AsText() => $"FeatherFall#{OwnerID}#{ExpiresOn}";
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

    // I can't remember why I am calling this hear D: Perhaps because of
    // traits that grant things like flying or water walking??
    if (obj is Actor actor2)
      gs.ResolveActorMove(actor2, actor2.Loc, actor2.Loc);

    return msgs;
  }

  public void Remove(GameObj obj, GameState gs, GameObj src)
  {
    List<Trait> traits = [..obj.Traits.Where(t => t.SourceId == src.ID)];
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

class FirstBossTrait : Trait
{
  public override string AsText() => "FirstBoss";
}

class MolochAltarTrait : Trait
{
  public override string AsText() => "MolochAltar";
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

class PolymorphedTrait : Trait
{
  public ulong OriginalId { get; set; }

  public Actor Morph(Actor victim, GameState gs, string newForm)
  {
    Actor morphed = MonsterFactory.Get(newForm, gs.ObjDb, gs.Rng);
    
    Loc loc = victim.Loc;
    victim.Loc = Loc.Nowhere;

    gs.ObjDb.AddNewActor(morphed, loc);
    gs.FlushPerformers();

    OriginalId = victim.ID;
    morphed.Traits.Add(this);

    return morphed;
  }

  public override string AsText() => $"Polymorphed#{OriginalId}";
}

class QuestItem1 : Trait
{
  public override string AsText() => "QuestItem1";
}

class QuestItem2 : Trait
{
  public override string AsText() => "QuestItem2";
}

class RustedTrait : Trait
{
  public Rust Amount { get; set; }

  public override string AsText() => $"Rusted#{(int)Amount}";
}

class SwimmerTrait : Trait
{
  public override string AsText() => "Swimmer";  
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
  public GameEventType EventType => GameEventType.EndOfRound;

  public virtual void Remove(GameState gs)
  {
    GameObj? obj = gs.ObjDb.GetObj(OwnerID);
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

class ThiefTrait : Trait
{
  public override string AsText() => "Thief";
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

    List<string> msgs = [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "begin")} to float in the air!" ];

    if (target.HasTrait<InPitTrait>())
    {
      target.Traits = [.. target.Traits.Where(t => t is not InPitTrait)];
      msgs.Add($"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "rise")} up out of the pit!");
    }
    
    return msgs;
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

class CleansingTrait : Trait
{
  public override string AsText() => "Cleansing";
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

class KoboldAltarTrait : Trait
{
  public override string AsText() => "KoboldAltar";
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
  public override string AsText() => "Floating";
}

class FlyingTrait : BasicTrait
{
  public FlyingTrait() { }
  public FlyingTrait(ulong expiry) => ExpiresOn = expiry;

  public override string AsText() => $"Flying#{ExpiresOn}";
}

class FragileTrait : Trait
{
  public override string AsText() => "Fragile";
}

class FriendlyMonsterTrait : Trait
{
  public override string AsText() => "FriendlyMonster";
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

      if (trait is FrightenedTrait)
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

class FrighteningTrait : Trait 
{
  public int DC { get; set; }
  
  public override string AsText() => $"Frightening#{SourceId}#{DC}";
}

class FullBellyTrait : Trait, IGameEventListener
{
  public ulong VictimID { get; set; }
  public int AcidDie { get; set; }
  public int AcidDice { get; set; }
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public GameEventType EventType => GameEventType.EndOfRound;
  public ulong ObjId => VictimID;

  public override string AsText() => $"FullBelly#{VictimID}#{AcidDie}#{AcidDice}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(VictimID) is not Actor victim)
      return;

    if (victim is Player)
      gs.UIRef().AlertPlayer("You are being digested!");

    int total = 0;
    for (int j = 0; j < AcidDice; j++)
      total += gs.Rng.Next(AcidDie) + 1;
    List<(int, DamageType)> dmg = [(total, DamageType.Acid)];
    var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 0, gs, null, 1.0);
    if (hpLeft < 1)
    {
      gs.ActorKilled(victim, $"being digested", null);
    }
  }
}

class OpaqueTrait : Trait
{
  public int Visibility { get; set; }

  public override string AsText() => $"Opaque#{Visibility}";
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
    "antidote" => new UseResult(new AntidoteAction(gs, user)),
    "blink" => new UseResult(new BlinkAction(gs, user)),
    "booze" => new UseResult(new DrinkBoozeAction(gs, user)),
    "disarm" => new UseResult(new DisarmAction(gs, user, user.Loc)),
    "minorheal" => new UseResult(new HealAction(gs, user, 4, 4)),    
    "maxheal" => new UseResult(new HealAction(gs, user, int.MaxValue, -1)),
    "trivialheal" => new UseResult(new HealAction(gs, user, 1, 1)),
    "soothe" => new UseResult(new SootheAction(gs, user, 21)),
    "telepathy" => new UseResult(new ApplyTraitAction(gs, user, new TelepathyTrait() { ExpiresOn = gs.Turn + 200 })),
    "magicmap" => new UseResult(new MagicMapAction(gs, user)),
    "detecttreasure" => new UseResult(new DetectTreasureAction(gs, user)),
    "detecttraps" => new UseResult(new DetectTrapsAction(gs, user)),
    "scatter" => new UseResult(new ScatterAction(gs, user)),
    "resistfire" => new UseResult(new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Fire, ExpiresOn = gs.Turn + 200})),
    "resistcold" => new UseResult(new ApplyTraitAction(gs, user, 
                        new ResistanceTrait() { Type = DamageType.Cold, ExpiresOn = gs.Turn + 200})),
    "recall" => new UseResult(new EscapeDungeonAction(gs)),
    "levitation" => new UseResult(new ApplyTraitAction(gs, user, new LevitationTrait() 
                                  { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) })),
    "knock" => new UseResult(new KnockAction(gs, user)),
    "identify" => new UseResult(new InventoryChoiceAction(gs, user, 
          new InventoryOptions() { Title = "Identify which item?", Options = InvOption.UnidentifiedOnly }, 
          new IdentifyItemAction(gs, user, item))),
    "applypoison" => new UseResult(new InventoryChoiceAction(gs, user,
          new InventoryOptions() { Title = "Apply it to which item?" },
          new ApplyPoisonAction(gs, user, item))),
    "seeinvisible" => new UseResult(new ApplyTraitAction(gs, user, new SeeInvisibleTrait()
            { ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(30, 75) })),
    "protection" => new UseResult(new ApplyTraitAction(gs, user, 
                        new AuraOfProtectionTrait() { HP = 25 })),
    "blindness" => new UseResult(new ApplyTraitAction(gs, user, BuildBlindTrait(user, gs))),
    "buffstrength" => new UseResult(new ApplyTraitAction(gs, user, 
                        new StatBuffTrait() { Attr = Attribute.Strength, Amt = 2, 
                          OwnerID = user.ID, ExpiresOn = gs.Turn + 50, SourceId = item!.ID })),
    "heroism" => new UseResult( 
                   new ApplyTraitAction(gs, user, 
                     new HeroismTrait() { 
                       OwnerID = user.ID, ExpiresOn = gs.Turn + (ulong)gs.Rng.Next(50, 75), SourceId = item!.ID})),
    "nondescript" => new UseResult(new ApplyTraitAction(gs, user, new NondescriptTrait() { ExpiresOn = gs.Turn + 50 })),
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

    var trait = (TemporaryTrait)TraitFactory.FromText(Effect, target);
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
      Glyph = new Glyph('-', Colours.LIGHT_BROWN, Colours.BROWN, Colours.BLACK, false)
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

class ArmourTrait : Trait
{
  public ArmourParts Part { get; set; }
  public int ArmourMod { get; set; }
  public int Bonus { set; get; }

  public override string AsText() => $"Armour#{Part}#{ArmourMod}#{Bonus}";  
}

class CurseTrait : TemporaryTrait
{
  public override List<string> Apply(Actor target, GameState gs)
  {
    foreach (Trait t in target.Traits)
    {
      if (t is CurseTrait curse)
      {
        curse.ExpiresOn += (ulong) gs.Rng.Next(75, 126);
        return [];
      }
    }

    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);    
    OwnerID = target.ID;
    ExpiresOn = gs.Turn + (ulong) gs.Rng.Next(75, 126);

    return [ $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "fall")} under a spell of ill luck!" ];
  }

  public override string AsText() => $"Curse#{OwnerID}#{ExpiresOn}";
}

class CursedItemTrait : Trait 
{
  public override string AsText() => $"CursedItem";
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

class DemonVisageTrait : Trait
{
  public override string AsText() => "DemonVisage";
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
  public GameEventType EventType => GameEventType.Death;
  
  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    var obj = gs.ObjDb.GetObj(ObjId);
    if (obj is not null and Actor actor)
    {
      gs.ActorKilled(actor, "", null);
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
  public GameEventType EventType => GameEventType.Death;
  public override ulong SourceId => GrapplerID;

  public void Remove(GameState gs)
  {
    var victim = gs.ObjDb.GetObj(VictimID);
    victim?.Traits.Remove(this);

    if (gs.ObjDb.GetObj(GrapplerID) is Actor grappler)
    {
      grappler.Traits = [..grappler.Traits.Where(t => t is not GrapplingTrait)];
    }

    gs.ObjDb.DeathWatchListeners = [..gs.ObjDb.DeathWatchListeners.Where(w => w.Item1 != GrapplerID)];    
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    Remove(gs);
  }

  public override string AsText() => $"Grappled#{VictimID}#{GrapplerID}#{DC}";
}

class GrapplerTrait : BasicTrait 
{
  public int DC { get; set; }

  public override string AsText() => $"Grappler#{DC}";
}

class GrapplingTrait : Trait
{
  public ulong VictimId { get; set; }

  public override string AsText() => $"Grappling#{VictimId}";
}

class HeavyTrait : Trait
{
  public override string AsText() => "Heavy";
}

class ParalyzingGazeTrait : BasicTrait
{
  public int DC { get; set; }

  public override string AsText() => $"ParalyzingGaze#{DC}";
}

class PassiveTrait : Trait
{
  public override string AsText() => $"Passive";
}

class BoolTrait : Trait
{
  public string Name { get; set; } = "";
  public bool Value { get; set; }

  public override string AsText() => $"Bool#{Name}#{Value}";
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

    statValue.ChangeMax(Amount);
    statValue.Change(Amount);
    string s = Stat switch
    {
      Attribute.BaseHP => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more robust!",
      Attribute.HP => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more healthier!",
      Attribute.Constitution => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more robust!",
      Attribute.Strength => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} stronger!",
      Attribute.Dexterity => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more agile!",
      Attribute.FinesseUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept with light weapons!",
      Attribute.SwordUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept at swordplay!",
      Attribute.AxeUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more adept at axe-work!",
      Attribute.BowUse => $"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "feel")} more skilled with bows!",
      _ => $"{Grammar.Possessive(target).Capitalize()} max {Stat} has changed!"
    };
    msgs.Add(s);

    if (Stat == Attribute.Constitution || Stat == Attribute.BaseHP)
      target.CalcHP();

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

  public void Remove(GameState gs, Actor victim)
  {
    victim.Recovery += 0.25;
    victim.Stats[Attribute.Dexterity].Change(1);
    victim.Traits.Remove(this);
    Expired = true;
    gs.StopListening(GameEventType.EndOfRound, this);

    if (victim is Player)
      gs.UIRef().AlertPlayer("Your leg feels better.");
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn > ExpiresOn && gs.ObjDb.GetObj(OwnerID) is Actor victim)
    {
      Remove(gs, victim);
    }      
  }
}

class LeaveDungeonTrait : Trait, IGameEventListener
{
  public bool Expired { get; set; }
  public bool Listening => true;
  public ulong ObjId => SourceId;

  public GameEventType EventType => GameEventType.EndOfRound;

  public override string AsText() => $"LeaveDungeon#{SourceId}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(SourceId) is Actor actor)
    {
      // I'm implementing this for now for the Gnome Merchant in the dungeon
      // but if I ever have other NPCs who need different conditions I'll
      // implement a configurable parameter of some sort.
      if (actor.Inventory.Items().Count == 0)
      {
        gs.RemovePerformerFromGame(actor);

        if (gs.LastPlayerFoV.Contains(actor.Loc))
        {
          string s = $"{actor.FullName.Capitalize()} disappears in a puff of smoke!";
          gs.UIRef().AlertPlayer(s);
          gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
        }
      }
    }
  }
}

class GoldSnifferTrait : TemporaryTrait, IGameEventListener
{
  const int RANGE = 10;

  public override List<string> Apply(Actor target, GameState gs)
  {
    OwnerID = target.ID;

    return [];
  }

  public override void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (eventType == GameEventType.EndOfRound && gs.Turn > ExpiresOn)
    {
      Remove(gs);
    }
    else
    {
      if (gs.ObjDb.GetObj(OwnerID) is not Actor actor)
      {
        Remove(gs);
        return;
      }
      
      bool zorkmidsFound = false;
      Map map = gs.MapForActor(actor);
      for (int r = actor.Loc.Row - RANGE; r <= actor.Loc.Row + RANGE; r++)
      {
        for (int c = actor.Loc.Col - RANGE; c <= actor.Loc.Col + RANGE; c++)
        {
          if (!map.InBounds(r, c))
            continue;

          Loc sq = actor.Loc with { Row = r, Col = c };
          bool zorkmidsAt = false;
          foreach (Item item in gs.ObjDb.ItemsAt(sq))
          {
            if (item.Type == ItemType.Zorkmid)
            {
              zorkmidsAt = true;
              break;
            }
          }

          if (zorkmidsAt && (!gs.CurrentDungeon.RememberedLocs.TryGetValue(sq, out var glyph) || glyph.Ch != '$'))
          {
            zorkmidsFound = true;
            gs.CurrentDungeon.RememberedLocs[sq] = new('$', Colours.YELLOW, Colours.YELLOW_ORANGE, Colours.BLACK, false);
          }
        }
      }

      if (zorkmidsFound)
      {
        gs.UIRef().AlertPlayer("Your nose twitches and you smell gold!");
      }      
    }
  }

  public override string AsText() => $"GoldSniffer#{OwnerID}#{SourceId}";
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
  public GameEventType EventType => GameEventType.EndOfRound;

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
          
          foreach (string s in nt.Apply(victim, gs))
            gs.UIRef().AlertPlayer(s);
        }
      }      
    }    
  }
}

class NondescriptTrait : TemporaryTrait, IGameEventListener
{
  protected override string ExpiryMsg => "You feel the attention of others turn toward you.";
  public override string AsText() => $"Nondescript#{base.AsText()}";

  public override List<string> Apply(Actor target, GameState gs)
  {
    target.Traits.Add(this);
    gs.RegisterForEvent(GameEventType.EndOfRound, this);    
    OwnerID = target.ID;

    return [ $"{target.FullName.Capitalize()} escape the notice of others!" ];
  }
}

class NumberListTrait : Trait
{
  public string Name { get; set; } = "";
  public List<int> Items { get; set; } = [];

  public override string AsText() => $"NumberList#{Name}#{string.Join(',', Items)}";
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
      ++TurnsParalyzed;

      // If you have a low enough Will score you might never pass the saving 
      // throw, so put a ceiling on how long someone can be paralyzed.
      if (victim.AbilityCheck(Attribute.Will, DC, gs.Rng) ||  TurnsParalyzed > 75)
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
      return [$"{target.FullName.Capitalize()} {Grammar.Conjugate(target, "is")} poisoned!"];
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
      string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "feel")} better.";
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
      gs.ActorKilled(victim, "poison", null);
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
  public GameEventType EventType => GameEventType.EndOfRound;

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
        UserInterface ui = gs.UIRef();
        int fireDmg = gs.Rng.Next(8) + 1;
        List<(int, DamageType)> fire = [(fireDmg, DamageType.Fire)];
        var (hpLeft, dmgMsg, _) = victim.ReceiveDmg(fire, 0, gs, null, 1.0);
          
        if (hpLeft < 1)
        {
          string msg = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "die")} by fire!";
          ui.AlertPlayer(msg);
          gs.ActorKilled(victim, "fire", null);
        }
        else
        {
          string txt = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} burnt!";
          ui.AlertPlayer(txt);

          if (dmgMsg != "")
            ui.AlertPlayer(dmgMsg);          
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

class OnPickupTrait : Trait
{
  public string Event { get; set; } = "";
  public bool Clear { get; set; }

  public void Apply(Actor target, GameState gs)
  {
    if (TraitFactory.FromText(Event, target) is IGameEventListener el)
    {
      string s = AsText();
      el.EventAlert(GameEventType.NoEvent, gs, target.Loc);
    }
  }

  public override string AsText() => $"OnPickup#{Clear}#{Event}";
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
  public int Radius { get; set; }
  public override string AsText() => $"Retribution#{Type}#{DmgDie}#{NumOfDice}#{Radius}";
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
    else if (Attr == Attribute.Constitution && player)
      return Amt >= 0 ? "You feel healthier!" : "You feel frail!";

    return player ? "You feel different!" : "";
  }

  static void SetPlayerHP(Player player)
  {
    int hpDelta = player.Stats[Attribute.HP].Max - player.Stats[Attribute.HP].Curr;
    player.CalcHP();

    int m = player.Stats[Attribute.HP].Max;
    int maxHp = int.Min(m, m - hpDelta);
    if (maxHp < 1)
      maxHp = 1;

    player.Stats[Attribute.HP].SetCurr(maxHp);
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
    if (Attr != Attribute.HP)
    {
      target.Stats[Attr].Change(Amt);
    }
    
    target.Traits.Add(this);

    // If a buff affects HP or Con, recalculate HP. But we don't want to allow
    // at-will healing by equipping or unequipping an item like the lesser 
    // health charm.
    if (target is Player player && (Attr == Attribute.HP || Attr == Attribute.Constitution))
    {
      SetPlayerHP(player);
    } 

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [ CalcMessage(target) ];
  }

  public override void Remove(GameState gs)
  {  
    if (gs.ObjDb.GetObj(OwnerID) is Actor actor)
    {
      List<StatBuffTrait> statBuffs = [.. actor.Traits.OfType<StatBuffTrait>().Where(t => t.SourceId == SourceId)];
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
    int currAmt = target.Stats[Attr].Curr;

    target.Stats[Attr].ChangeMax(-Amt);
    if (Attr != Attribute.HP)
    {
      target.Stats[Attr].Change(-Amt);
    }
    
    target.Traits.Remove(trait);
    
    if (target is Player player)
    {
      if (Attr == Attribute.HP || Attr == Attribute.Constitution)
        SetPlayerHP(player);

      if (Attr == Attribute.HP && Amt < 0)
        return "Some of your vitality returns.";
      else if (Attr == Attribute.HP && Amt >= 0)
        return "You feel less sturdy.";
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

    Action action = new CloseMenuAction(gs, 1.0);
    
    return new UseResult(action);
  }

  public void Used() {}
}

class ReaverBlessingTrait : BlessingTrait
{
  public override List<string> Apply(Actor granter, GameState gs)
  {
    int piety = gs.Player.Stats[Attribute.Piety].Max;

    MeleeDamageModTrait dmg = new() { Amt = 2 + piety, SourceId = granter.ID };
    gs.Player.Traits.Add(dmg);

    FrighteningTrait fright = new() { DC = 10 + piety, SourceId = granter.ID };
    gs.Player.Traits.Add(fright);

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"ReaverBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";

  public override string Description(Actor owner)
  {
    string s = "You have the Reaver Blessing. It grants";

    MeleeDamageModTrait? dmg = owner.Traits.OfType<MeleeDamageModTrait>()
                                           .Where(t => t.SourceId == SourceId)
                                           .FirstOrDefault();
    if (dmg is not null)
    {
      s += $" a [lightblue +{dmg.Amt}] bonus to melee damage";
    }

    if (owner.Traits.OfType<FrighteningTrait>().Where(t => t.SourceId == SourceId).Any())
    {
      s += " and your attacks may [brightred frighten] your foes";
    }

    s += ".";

    return s;
  }
}

// I am making the assumption it will only be the Player who uses Recall.
class RecallTrait : BasicTrait, IGameEventListener
{
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public ulong ObjId { get; set; }
  public GameEventType EventType => GameEventType.EndOfRound;

  public override string AsText() => $"Recall#{ExpiresOn}#{Expired}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.Turn < ExpiresOn)
      return;

    Expired = true;

    Player player = gs.Player;
    player.Traits.Remove(this);

    // We can get the entrance to the main dungeon via the History
    // object in Campaign. (I'd like to eventually have side quest
    // dungeons, though, where Recall will also need to be handled
    // but I'm not going to bother with that yet)
    if (gs.Campaign is null || gs.Campaign.FactDb is null)
      throw new Exception("Checking for dungeon entrance fact: Campaign and History should never be null");

    if (gs.InWilderness)
    {
      gs.UIRef().AlertPlayer("You sudenly teleport exactly 1 cm to the left.");
      return;
    }

    int zorkmids = player.Inventory.Zorkmids;
    player.Inventory.Zorkmids = 0;
    Item coins = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
    coins.Value = zorkmids;
    gs.ItemDropped(coins, player.Loc);

    List<Item> itemsDropped = [];
    foreach (Item item in player.Inventory.Items())
    {
      if (item.Traits.OfType<MetalTrait>().FirstOrDefault() is MetalTrait mt && mt.Type == Metals.Gold)
      {
        player.Inventory.RemoveByID(item.ID);
        itemsDropped.Add(item);

        if (item.Equipped)
        {
          foreach (GrantsTrait granted in item.Traits.OfType<GrantsTrait>())
          {
            granted.Remove(player, gs, item);
          }
        }

        item.Equipped = false;

        gs.ItemDropped(item, player.Loc);
      }
    }

    Loc exitPoint = gs.CurrentDungeon.ExitLoc;
    gs.ActorEntersLevel(player, 0, 0);
    Loc start = player.Loc;
    player.Loc = exitPoint;
    gs.ResolveActorMove(player, start, exitPoint);
    gs.FlushPerformers();
    gs.PrepareFieldOfView();

    string msg = "A wave of vertigo...";
    if (zorkmids > 0)
    {
      msg += " Your purse feels lighter!";
    }
    foreach (Item item in itemsDropped)
    {
      msg += $" Your {item.FullName} falls to the floor!";
    }

    gs.UIRef().AlertPlayer(msg);
  }
}

class RegenerationTrait : BasicTrait, IGameEventListener
{
  public int Rate { get; set; }
  public ulong ActorID { get; set; }
  public ulong ObjId => ActorID;
  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public GameEventType EventType => GameEventType.EndOfRound;

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

class SetAttributeTriggerTrait : Trait, IGameEventListener
{
  public Attribute Attribute { get; set; }
  public int Value { get; set; }
  public bool Expired { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

  public bool Listening => true;
  public ulong ObjId => SourceId;
  public GameEventType EventType => GameEventType.Death;

  public override string AsText() => $"SetAttributeTrigger#{Attribute}#{Value}#{SourceId}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc) =>
    gs.Player.Stats[Attribute] = new Stat(Value);
}

class InvisibleTrait : BasicTrait, IGameEventListener
{
  public ulong ActorID { get; set; }
  public ulong ObjId => ActorID;
  public bool Expired { get; set; }
  public bool Listening => true;
  public GameEventType EventType => GameEventType.EndOfRound;

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
  public GameEventType EventType => GameEventType.EndOfRound;

  public override string AsText() => $"Countdown#{OwnerID}#{ExpiresOn}";

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
      string msg = $"{item.Name.DefArticle().Capitalize()} dissipates!";
      gs.UIRef().AlertPlayer(msg, gs, item.Loc);
    }
  }
}

class CroesusTouchTrait : Trait
{
  public override string AsText() => $"CroesusTouch#{SourceId}";
}

class LightBeamTrait : Trait, IGameEventListener
{
  public bool Expired { get; set; }
  public bool Listening => true;
  public ulong ObjId => SourceId;
  public GameEventType EventType => GameEventType.EndOfRound;

  public List<ulong> Photons { get; set; } = [];

  public override string AsText() => $"LightBeam#{SourceId}#{string.Join(',', Photons)}";

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (gs.ObjDb.GetObj(SourceId) is not Item lamp)
    {
      gs.StopListening(EventType, this);
      return;
    }

    Dir dir = Dir.None;
    if (lamp.Traits.OfType<DirectionTrait>().FirstOrDefault() is DirectionTrait dt)
    {
      dir = dt.Dir;
    }

    List<Loc> locs = FollowPath(gs, lamp.Loc, dir);

    HashSet<ulong> foundPhotons = [];
    foreach (Loc sq in locs)
    {
      bool photonFound = false;
      foreach (Item item in gs.ObjDb.ItemsAt(sq))
      {
        if (item.Traits.OfType<LightSourceTrait>().FirstOrDefault() is LightSourceTrait lst && 
            lst.OwnerID == lamp.ID)
        {
          foundPhotons.Add(item.ID);
          photonFound = true;
        }
      }

      if (!photonFound)
      {
        Item photon = ItemFactory.Photon(gs, lamp.ID);
        gs.ObjDb.SetToLoc(sq, photon);
        foundPhotons.Add(photon.ID);
      }
    }

    foreach (ulong photonId in Photons)
    {
      if (!foundPhotons.Contains(photonId) && gs.ObjDb.GetObj(photonId) is Item photon)
      {
        gs.ObjDb.RemoveItemFromGame(photon.Loc, photon);
      }
    }

    Photons = [.. foundPhotons];
  }

  static void DestroyBlock(GameState gs, Item block)
  {
    string msg;
    if (gs.LastPlayerFoV.Contains(block.Loc))
      msg = "Exposed to the light, the stone block crumbles to dust!";
    else
      msg = "You hear a loud crack and the clattering of stones.";
    
    gs.UIRef().AlertPlayer(msg);
    gs.UIRef().SetPopup(new Popup(msg, "", -1, -1));
    
    gs.ObjDb.RemoveItemFromGame(block.Loc, block);

    Loc dest = block.Loc with { Level = block.Loc.Level + 1 };
    Downstairs stairs = new("")
    {
      Destination = dest
    };
    gs.CurrentMap.SetTile(block.Loc.Row, block.Loc.Col, stairs);
  }

  List<Loc> FollowPath(GameState gs, Loc start, Dir dir)
  {
    HashSet<Loc> visited = [];
    List<Loc> locs = [];
    Loc curr = start;
    while (true)
    {      
      curr = Move(curr, dir);
      // Prevent infinite loops from players aligning mirrors in a loop. 
      // Someone's gonna try it...
      if (visited.Contains(curr))
        break;

      foreach (Item item in gs.ObjDb.ItemsAt(curr))
      {
        if (item.Name == "stone block")
        {
          DestroyBlock(gs, item);
        }
      }

      if (!gs.TileAt(curr).PassableByFlight())
        break;
      if (gs.ObjDb.AreBlockersAtLoc(curr))
        break;
      locs.Add(curr);

      foreach (Item item in gs.ObjDb.ItemsAt(curr).Where(i => i.Type == ItemType.Device))
      {
        if (item.Traits.OfType<BoolTrait>().FirstOrDefault() is BoolTrait tilt)
        {
          if (tilt.Value && dir == Dir.South)
            dir = Dir.East;
          else if (tilt.Value && dir == Dir.North)
            dir = Dir.West;
          else if (tilt.Value && dir == Dir.West)
            dir = Dir.North;
          else if (tilt.Value && dir == Dir.East)
            dir = Dir.South;
          else if (!tilt.Value && dir == Dir.South)
            dir = Dir.West;
          else if (!tilt.Value && dir == Dir.North)
            dir = Dir.East;
          else if (!tilt.Value && dir == Dir.West)
            dir = Dir.South;
          else if (!tilt.Value && dir == Dir.East)
            dir = Dir.North;
        }

        // What's the correct behaviour if there's more than one mirror at a 
        // location?  ¯\(ツ)/¯
        break;
      }

      visited.Add(curr);
    }

    return locs;
  }

  Loc Move(Loc curr, Dir dir) => dir switch
  {    
    Dir.North => curr with { Row = curr.Row - 1},
    Dir.South => curr with { Row = curr.Row + 1 },
    Dir.East => curr with { Col = curr.Col + 1 },
    Dir.West => curr with { Col = curr.Col - 1 },
    _ => curr
  };
}

// A light source that doesn't have fuel/burn out on its own.
class LightSourceTrait : BasicTrait, IOwner
{
  public ulong OwnerID { get; set; }
  public int Radius { get; set; }
  public Colour FgColour { get; set; }
  public Colour BgColour { get; set; }

  public override string AsText() => $"LightSource#{OwnerID}#{Radius}#{Colours.ColourToText(FgColour)}#{Colours.ColourToText(BgColour)}";
}

class LightSpellTrait : TemporaryTrait
{
  static readonly int Radius = 5;
  protected override string ExpiryMsg => $"Your light spell fades.";
  public override string AsText() => $"LightSpell#{ExpiresOn}#{OwnerID}";

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    // Remove a (or the) light source from the owner. If there is
    // more than one light source of the same radius, it doesn't 
    // really matter which one we remove
    if (gs.ObjDb.GetObj(OwnerID) is not GameObj obj)
      return;

    int i = -1;
    for (int j = 0; j < obj.Traits.Count; j++)
    {
      if (obj.Traits[j] is LightSourceTrait ls && ls.Radius == Radius)
      {
        i = j;
        break;
      }
    }
    if (i > -1)
      obj.Traits.RemoveAt(i);
  }

  public override List<string> Apply(Actor target, GameState gs)
  {
    LightSourceTrait lst = new()
    {
      Radius = Radius,
      OwnerID = target.ID,
      FgColour = Colours.YELLOW,
      BgColour = Colours.TORCH_ORANGE
    };

    ExpiresOn = gs.Turn + 250;    
    gs.RegisterForEvent(GameEventType.EndOfRound, this);
    target.Traits.Add(this);
    target.Traits.Add(lst);
    OwnerID = target.ID;

    return [ "Let there be light!" ];
  }
}

// Who knew torches would be so complicated...
class TorchTrait : BasicTrait, IGameEventListener, IUSeable, IOwner, IDesc
{
  public ulong OwnerID { get; set; }
  public ulong ObjId => OwnerID;
  public bool Lit { get; set; }
  public int Fuel { get; set; }
  public string Desc() => Lit ? "(lit)" : "";
  public GameEventType EventType => GameEventType.EndOfRound;
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

    item.Traits = [..item.Traits.Where(t => t is not LightSourceTrait)];

    return $"{item!.FullName.DefArticle().Capitalize()} is extinguished.";
  }

  public UseResult Use(Actor _, GameState gs, int row, int col, Item? iitem)
  {
    Item? item = gs.ObjDb.GetObj(OwnerID) as Item;
    var loc = new Loc(gs.CurrDungeonID, gs.CurrLevel, row, col);
    if (Lit)
    {
      var msg = Extinguish(gs, item!, loc);
      return new UseResult(null, msg);
    }
    else if (Fuel > 0)
    {
      Lit = true;
      gs.RegisterForEvent(GameEventType.EndOfRound, this);
      
      item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
      item.Traits.Add(new LightSourceTrait() { Radius = 5, FgColour = Colours.YELLOW, BgColour = Colours.TORCH_ORANGE });

      return new UseResult(null, $"The {item!.Name} sparks to life!");
    }
    else
    {
      return new UseResult(null, $"That {item!.Name} is burnt out!");
    }
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    // Although if it's not Lit, it shouldn't be listening for events
    if (!Lit)
      return;

    if (--Fuel < 0)
    {
      Lit = false;
      Expired = true;      
      if (gs.ObjDb.GetObj(OwnerID) is Item item)
      {
        Loc torchLoc = item.Loc;

        string msg = $"{item.Name.IndefArticle().Capitalize()} burns out.";

        if (item.ContainedBy > 0 && gs.ObjDb.GetObj(item.ContainedBy) is Actor owner)
        {
          // I don't think owner should ever be null, barring a bug
          // but this placates the warning in VS/VS Code
          owner.Inventory.Remove(item.Slot, 1);
          torchLoc = owner.Loc;
          msg = $"{item.Name.Possessive(owner).Capitalize()} burns out.";
        }
        
        gs.UIRef().AlertPlayer(msg, gs, torchLoc);
        gs.ObjDb.RemoveItemFromGame(item.Loc, item);
      }
    }
  }
}

class TricksterBlessingTrait : BlessingTrait
{
  public override List<string> Apply(Actor granter, GameState gs)
  {
    QuietTrait quiet = new() { SourceId = granter.ID };
    gs.Player.Traits.Add(quiet);

    if (!gs.Player.SpellsKnown.Contains("phase door"))
      gs.Player.SpellsKnown.Add("phase door");

    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(2);
      mp.Change(2);
    }
    else
    {
      gs.Player.Stats[Attribute.MagicPoints] = new Stat(2);
    }

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.SpellsKnown.Remove("phase door");
    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(-2);
    }

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"TricksterBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";

  public override string Description(Actor owner) => "Trickster blessing";
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
      return new UseResult(new PassAction(), "Nothing happens");
    }

    ulong itemId = item is not null ? item.ID : 0;
    return new UseResult(new UseWandAction(gs, user, this, itemId), "");
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

class WinterBlessingTrait : BlessingTrait
{
  public override List<string> Apply(Actor granter, GameState gs)
  {
    ResistanceTrait resist = new()
    {
      SourceId = granter.ID,
      OwnerID = gs.Player.ID,
      ExpiresOn = ExpiresOn,
      Type = DamageType.Cold
    };
    gs.Player.Traits.Add(resist);

    if (!gs.Player.SpellsKnown.Contains("cone of cold"))
      gs.Player.SpellsKnown.Add("cone of cold");
    if (!gs.Player.SpellsKnown.Contains("gust of wind"))
      gs.Player.SpellsKnown.Add("gust of wind");

    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(2);
      mp.Change(2);
    }
    else
    {
      gs.Player.Stats[Attribute.MagicPoints] = new Stat(2);
    }

    gs.Player.Traits.Add(this);

    gs.RegisterForEvent(GameEventType.EndOfRound, this);

    return [];
  }

  public override void Remove(GameState gs)
  {
    base.Remove(gs);

    gs.Player.SpellsKnown.Remove("cone of cold");
    gs.Player.SpellsKnown.Remove("gust of wind");
    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var mp))
    {
      mp.ChangeMax(-2);
    }

    gs.Player.Traits = [.. gs.Player.Traits.Where(t => t.SourceId != SourceId)];
  }

  public override string AsText() => $"WinterBlessing#{SourceId}#{ExpiresOn}#{OwnerID}";

  public override string Description(Actor owner) => "Winter blessing";
}

class WorshiperTrait : Trait
{
  public Loc AltarLoc { get; set; }
  public ulong AltarId { get; set; }
  public string Chant { get; set; } = "";

  public override string AsText() => $"Worshiper#{AltarLoc}#{AltarId}#{Chant}";
}

class TraitFactory
{
  static readonly Dictionary<string, Func<string[], GameObj?, Trait>> traitFactories = new()
  {
    { "AbjurationBell", (pieces, gameObj) => new AbjurationBellTrait() },
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
        Amt = Util.ToDouble(pieces[1]),
        SourceId = pieces.Length > 2 ? ulong.Parse(pieces[2]) : 0
      }
    },
    { "Allies", (pieces, gameObj) =>
      {
        List<ulong> ids = [];
        if (pieces[1] != "")
          ids = [..pieces[1].Split(',').Select(ulong.Parse)];
        return new AlliesTrait() { IDs = ids }; }
      },
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
    { "Artifact", (pieces, gameObj) => new ArtifactTrait() },
    { "AttackMod", (pieces, gameObj) => new AttackModTrait() { Amt = int.Parse(pieces[1]), SourceId = ulong.Parse(pieces[2]) } },
    { "AttackVerb", (pieces, gameObj) => new AttackVerbTrait(pieces[1])},
    { "AuraMessage", (pieces, gameObj) => new AuraMessageTrait() { ObjId = ulong.Parse(pieces[1]), Radius = int.Parse(pieces[2]), Message = pieces[3] } },
    { "AuraOfProtection", (pieces, gameObj) => new AuraOfProtectionTrait() { HP = int.Parse(pieces[1]) }},
    { "Axe", (pieces, gameObj) => new AxeTrait() },
    { "BehaviourTree", (pieces, gameObj) => new BehaviourTreeTrait() { Plan = pieces[1] } },
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
    { "Bool", (pieces, gameObj) => new BoolTrait() { Name = pieces[1], Value = bool.Parse(pieces[2]) }},
    { "BoostMaxStat", (pieces, gameObj) => {
      Enum.TryParse(pieces[1], out Attribute attr);
      return new BoostMaxStatTrait() { Stat = attr, Amount = int.Parse(pieces[2])}; }},
    { "Bow", (pieces, gameObj) => new BowTrait() },
    { "Brainless", (pieces, gameObj) => new BrainlessTrait() },
    { "CanApply", (pieces, gameObj) => new CanApplyTrait() },
    { "Carries", (pieces, gameObj) => new CarriesTrait() { ItemName = pieces[1], Chance = int.Parse(pieces[2]) }},
    { "Cast", (pieces, gameObj) => new CastTrait() { Spell = pieces[1] }},
    { "ChampionBlessing", (pieces, gameObj) => new ChampionBlessingTrait() { SourceId = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]) } },
    { "Cleansing", (pieces, gamObj) => new CleansingTrait() },
    { "Cleave", (pieces, gameObj) => new CleaveTrait() },
    { "CoinsLoot", (pieces, gameObj) => new CoinsLootTrait() { Min = int.Parse(pieces[1]), Max = int.Parse(pieces[2])} },
    { "Confused", (pieces, gameObj) => new ConfusedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "Construct", (pieces, gameObj) => new ConstructTrait() },
    { "Consumable", (pieces, gameObj) => new ConsumableTrait() },
    { "Corrosive", (pieces, gameObj) => new CorrosiveTrait() },
    { "Corruption", (pieces, gameObj) => new CorruptionTrait() { Amt = int.Parse(pieces[1]) } },
    { "Countdown", (pieces, gameObj) => new CountdownTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "CroesusTouch", (pieces, gameObj) => new CroesusTouchTrait { SourceId = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]) }},
    { "Cudgel", (pieces, gameObj) => new CudgelTrait() },
    { "Curse", (pieces, gameObj) =>
      new CurseTrait()
      {
        OwnerID = ulong.Parse(pieces[1]),
        ExpiresOn = ulong.Parse(pieces[2])
      }
    },
    { "CursedItem", (pieces, gameObj) => new CursedItemTrait() },
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
    { "DeathMessage", (pieces, gameObj) => new DeathMessageTrait() { Message = pieces[1] } },
    { "DemonVisage", (pieces, gameObj) => new DemonVisageTrait() },
    { "Description", (pieces, gameObj) => new DescriptionTrait(pieces[1]) },
    { "Desecrated", (pieces, gameObj) => new DesecratedTrait() },
    { "DialogueScript", (pieces, gameObj) => new DialogueScriptTrait() { ScriptFile = pieces[1] } },
    { "DiggingTool", (pieces, gameObj) => new DiggingToolTrait() },
    { "Disguise", (pieces, gameObj) =>  new DisguiseTrait() { Disguise = Glyph.TextToGlyph(pieces[1]), TrueForm = Glyph.TextToGlyph(pieces[2]), DisguiseForm = pieces[3] }},
    { "Direction", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[1], out Dir dir);
        return new DirectionTrait() { Dir = dir };
      }
    },
    { "Displacement", (pieces, gameObj) => new DisplacementTrait() },
    { "Divider", (pieces, gameObj) => new DividerTrait() },
    { "Dodge", (pieces, gameObj) =>
      {
        int rate = int.Parse(pieces[1]);
        ulong sourceId = pieces.Length > 2 ? ulong.Parse(pieces[2]) : 0;
          return new DodgeTrait() { Rate = int.Parse(pieces[1]), SourceId = sourceId };
      }
    },
    { "DoorKey", (pieces, gameObj) => new DoorKeyTrait() { DCMod = int.Parse(pieces[1])} },
    { "DragonCultBlessing", (pieces, gameObj) => new DragonCultBlessingTrait()
      {
        SourceId = ulong.Parse(pieces[1]),
        ExpiresOn = ulong.Parse(pieces[2]),
        OwnerID = ulong.Parse(pieces[3])
      }
    },
    { "Drop", (pieces, gameObj) => new DropTrait() { ItemName = pieces[1], Chance = int.Parse(pieces[2]) }},
    { "Edible", (pieces, gameObj) => new EdibleTrait() },
    { "EmberBlessing", (pieces, gameObj) => new EmberBlessingTrait() { SourceId = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]) } },
    { "Exhausted", (pieces, gameObj) =>  new ExhaustedTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "FallenAdventurer", (pieces, gameObj) => new FallenAdventurerTrait() },
    { "FeatherFall", (pieces, gameObj) => {
      ulong id = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]);
      ulong expiresOn = pieces[2] == "max" ? ulong.MaxValue : ulong.Parse(pieces[2]);
      return new FeatherFallTrait() { OwnerID = id, ExpiresOn = expiresOn };
    }},
    { "FinalBoss", (pieces, gameObj) => new FinalBossTrait() },
    { "Finesse", (pieces, gameObj) => new FinesseTrait() },
    { "FireRebuke", (pieces, gameObj) => new FireRebukeTrait() { SourceId = ulong.Parse(pieces[1])} },
    { "Flammable", (pieces, gameObj) => new FlammableTrait() },
    { "Floating", (pieces, gameObj) => new FloatingTrait() },
    { "Flying", (pieces, gameObj) => new FlyingTrait() },
    { "Fragile", (pieces, gameObj) => new FragileTrait() },
    { "FriendlyMonster", (pieces, gameObj) => new FriendlyMonsterTrait() },
    { "Frightened", (pieces, gameObj) => new FrightenedTrait()
      { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) }
    },
    { "Frightening", (pieces, gameObj) => new FrighteningTrait() { SourceId = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2])} },
    { "FullBelly", (pieces, gameObj) => new FullBellyTrait()
      {
        VictimID = ulong.Parse(pieces[1]),
        AcidDie = int.Parse(pieces[2]),
        AcidDice = int.Parse(pieces[3])
      }
    },
    { "GoldSniffer", (pieces, gameObj) => new GoldSnifferTrait()
      {
        OwnerID = ulong.Parse(pieces[1]),
        SourceId = ulong.Parse(pieces[2])
      }
    },
    { "GoodMagicLoot", (pieces, gameObj) => new GoodMagicLootTrait() },
    { "Grants", (pieces, gameObj) => {
      string[] grantedTraits = [.. pieces[1].Split(';').Select(s => s.Replace('&', '#'))];
      return new GrantsTrait() { TraitsGranted = grantedTraits };
     }},
    { "Grappled", (pieces, gameObj) => new GrappledTrait() { VictimID = ulong.Parse(pieces[1]), GrapplerID = ulong.Parse(pieces[2]), DC = int.Parse(pieces[3]) } },
    { "Grappler", (pieces, gameObj) => new GrapplerTrait { DC = int.Parse(pieces[1]) }},
    { "Grappling", (pieces, gameObj) => new GrapplingTrait { VictimId = ulong.Parse(pieces[1]) }},
    { "Heavy", (pieces, gameObj) => new HeavyTrait() },
    { "Heroism", (pieces, gameObj) => new HeroismTrait()
      {
        OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), SourceId = ulong.Parse(pieces[3])
      }
    },
    { "Hidden", (pieces, gameObj) => new HiddenTrait() },
    { "Holy", (pieces, gameObj) => new HolyTrait() },
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
    { "KoboldAltar", (pieces, gameObj) => new KoboldAltarTrait() },
    { "Lame", (pieces, gameObj) =>  new LameTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) }},
    { "LeaveDungeon", (pieces, gameObj) => new LeaveDungeonTrait() { SourceId = ulong.Parse(pieces[1]) }},
    { "Levitation", (pieces, gameObj) => new LevitationTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "LightBeam", (pieces, gameObj) => new LightBeamTrait()
      {
        SourceId = ulong.Parse(pieces[1]),
        Photons = pieces[2] == "" ? [] : [..pieces[2].Split(',').Select(ulong.Parse)]
      }
    },
    { "LightSource", (pieces, gameObj) => new LightSourceTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Radius = int.Parse(pieces[2]),
        FgColour = Colours.TextToColour(pieces[3]),
        BgColour = Colours.TextToColour(pieces[4])
      }
    },
    { "LightStep", (pieces, gameObj) => new LightStepTrait() },
    { "Likeable", (pieces, gameObj) => new LikeableTrait() },
    { "MageArmour", (pieces, gameObj) =>
      new MageArmourTrait() { ExpiresOn = ulong.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]) }
    },
    { "MeleeDamageMod", (pieces, gameObj) => new MeleeDamageModTrait() { Amt = int.Parse(pieces[1]), SourceId = ulong.Parse(pieces[2]) }},
    { "Metal", (pieces, gameObj) => new MetalTrait() { Type = (Metals)int.Parse(pieces[1]) } },
    { "FirstBoss", (pieces, gameObj) => new FirstBossTrait() },
    { "MolochAltar", (pieces, gameObj) => new MolochAltarTrait() },    
    { "Mosquito", (pieces, gameObj) => new MosquitoTrait() },
    { "Named", (pieces, gameObj) => new NamedTrait() },
    { "Nausea", (pieces, gameObj) => new NauseaTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "NauseousAura", (pieces, gameObj) => new NauseousAuraTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Strength = int.Parse(pieces[2])
      }
    },
    { "Nondescript", (pieces, gameObj) => new NondescriptTrait() { ExpiresOn = ulong.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]) } },
    { "NumberList", (pieces, gameObj) =>
      new NumberListTrait()
      {
        Name = pieces[1],
        Items = pieces[2] == "" ? [] : [..pieces[2].Split(',').Select(int.Parse)]
      }
    },
    { "OnFire", (pieces, gameObj) => new OnFireTrait()
    {
      Expired = bool.Parse(pieces[1]), OwnerID = pieces[2] == "owner" ? gameObj!.ID : ulong.Parse(pieces[2]),
      Lifetime = pieces[3] == "max" ? int.MaxValue :  int.Parse(pieces[3]) , Spreads = bool.Parse(pieces[4]) }
    },
    { "OnPickup", (pieces, gameObj) => new OnPickupTrait()
      {
        Clear = bool.Parse(pieces[1]),
        Event = string.Join('#', pieces[2..] )
      }
    },
    { "Owned", (pieces, gameObj) => new OwnedTrait() { OwnerIDs = [..pieces[1].Split(',').Select(ulong.Parse)] } },
    { "Opaque", (pieces, gameObj) => new OpaqueTrait() { Visibility = int.Parse(pieces[1]) } },
    { "OwnsItem", (pieces, gameObj) => new OwnsItemTrait() { ItemID = ulong.Parse(pieces[1]) } },
    { "Paralyzed", (pieces, gameObj) => new ParalyzedTrait() { OwnerID = ulong.Parse(pieces[1]), DC = int.Parse(pieces[2]), ExpiresOn = ulong.Parse(pieces[3]) } },
    { "ParalyzingGaze", (pieces, gameObj) => new ParalyzingGazeTrait() { DC = int.Parse(pieces[1]) } },
    { "Passive", (pieces, gameObj) => new PassiveTrait() },
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
    { "Polymorphed", (pieces, gameObj) => new PolymorphedTrait() { OriginalId = ulong.Parse(pieces[1]) } },
    { "PoorLoot", (pieces, gameObj) => new PoorLootTrait() },
    { "Prisoner", (pieces, gameObj) => new PrisonerTrait() { SourceId = ulong.Parse(pieces[1]), Cell = Loc.FromStr(pieces[2]) } },
    { "Quiet", (pieces, gameObj) => new QuietTrait() { SourceId = ulong.Parse(pieces[1])} },
    { "QuestItem1", (pieces, gameObj) => new QuestItem1() },
    { "QuestItem2", (pieces, gameObj) => new QuestItem2() },
    { "Rage", (pieces, gameObj) => new RageTrait(gameObj as Actor
        ?? throw new ArgumentException("gameObj must be an Actor for RageTrait")) },
    { "Reach", (pieces, gameObj) => new ReachTrait() },
    { "Readable", (pieces, gameObj) => new ReadableTrait(pieces[1].Replace("<br/>", "\n")) { OwnerID = ulong.Parse(pieces[2]) } },
    { "ReaverBlessing", (pieces, gameObj) => new ReaverBlessingTrait() { SourceId = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]) } },
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
        return new RetributionTrait() { Type = dt, DmgDie = int.Parse(pieces[2]), NumOfDice = int.Parse(pieces[3]), Radius = int.Parse(pieces[4]) };
      }
    },
    { "ResistBlunt", (pieces, gameObj) => new ResistBluntTrait() },
    { "ResistPiercing", (pieces, gameObj) => new ResistPiercingTrait() },
    { "ResistSlashing", (pieces, gameObj) => new ResistSlashingTrait() },
    { "Robbed", (pieces, gameObj) => new RobbedTrait() },
    { "Rusted", (pieces, gameObj) => new RustedTrait() { Amount = (Rust)int.Parse(pieces[1]) } },
    { "Scroll", (pieces, gameObj) => new ScrollTrait() },
    { "SeeInvisible", (pieces, gameObj) => new SeeInvisibleTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "SetAttributeTrigger", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[1], out Attribute attr);
        return new SetAttributeTriggerTrait()
        {
          Attribute = attr,
          Value = int.Parse(pieces[2]),
          SourceId = ulong.Parse(pieces[3])
        };
      }
    },
    { "SideEffect", (pieces, gameObj) => new SideEffectTrait() { Odds = int.Parse(pieces[1]), Effect = string.Join('#', pieces[2..] ) } },
    { "Shunned", (pieces, gameObj) => new ShunnedTrait() },
    { "SilverAllergy", (pieces, gameObj) => new SilverAllergyTrait() },
    { "Sleeping", (pieces, gameObj) => new SleepingTrait() },
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
      } 
    },
    { "StatDebuff", (pieces, gameObj) =>
    {
      Enum.TryParse(pieces[3], out Attribute attr);
      ulong expires = pieces[2] == "max" ? ulong.MaxValue : ulong.Parse(pieces[2]);
      return new StatDebuffTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = expires, Attr = attr, Amt = int.Parse(pieces[4]) };
    }},
    { "Sticky", (pieces, gameObj) => new StickyTrait() },
    { "StoneTablet", (pieces, gameObj) => new StoneTabletTrait(pieces[1].Replace("<br/>", "\n")) { OwnerID = ulong.Parse(pieces[2]) } },
    { "Stress", (pieces, gameObj) =>
      {
        Enum.TryParse(pieces[1], out StressLevel stress);
        return new StressTrait() { Stress = stress, OwnerID = ulong.Parse(pieces[2]) };
      }
    },
    { "StressReliefAura", (pieces, gameObj) => new StressReliefAuraTrait() { ObjId = ulong.Parse(pieces[1]), Radius = int.Parse(pieces[2]) } },
    { "Swallowed", (pieces, gameObj) => new SwallowedTrait()
      {
        VictimID = ulong.Parse(pieces[1]), SwallowerID = ulong.Parse(pieces[2]),
        Origin = Loc.FromStr(pieces[3])
      }
    },
    { "Swimmer", (pieces, gameObj) => new SwimmerTrait() },
    { "Sword", (pieces, gameObj) => new SwordTrait() },
    { "Teflon", (pieces, gameObj) => new TeflonTrait() },
    { "Telepathy", (pieces, gameObj) => new TelepathyTrait() { ExpiresOn = ulong.Parse(pieces[1]), OwnerID = ulong.Parse(pieces[2]) } },
    { "Thief", (pieces, gameObj) => new ThiefTrait() },
    { "Tipsy", (pieces, gameObj) => new TipsyTrait() { OwnerID = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]) } },
    { "Torch", (pieces, gameObj) => new TorchTrait()
      {
        OwnerID = pieces[1] == "owner" ? gameObj!.ID : ulong.Parse(pieces[1]),
        Lit = bool.Parse(pieces[2]),
        Fuel = int.Parse(pieces[3])
      }
    },
    { "TricksterBlessing", (pieces, gameObj) => new TricksterBlessingTrait() { SourceId = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]) } },
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
    { "Vicious", (pieces, gameObj) => new ViciousTrait() { Scale = Util.ToDouble(pieces[1]) }},
    { "Villager", (pieces, gameObj) => new VillagerTrait() },
    { "Wand", (pieces, gameObj) => new WandTrait() { Charges = int.Parse(pieces[1]), IDed = bool.Parse(pieces[2]), Effect = pieces[3] } },
    { "WaterWalking", (pieces, gameObj) =>
      pieces.Length > 1 ? new WaterWalkingTrait() { SourceId = ulong.Parse(pieces[1])} : new WaterWalkingTrait()
    },
    { "Weaken", (pieces, gameObj) =>  new WeakenTrait() { DC = int.Parse(pieces[1]), Amt = int.Parse(pieces[2]) } },
    { "WeaponBonus", (pieces, gameObj) => new WeaponBonusTrait() { Bonus = int.Parse(pieces[1]) } },
    { "WeaponSpeed", (pieces, gameObj) => new WeaponSpeedTrait() { Cost = Util.ToDouble(pieces[1]) } },
    { "WinterBlessing", (pieces, gameObj) => new WinterBlessingTrait() { SourceId = ulong.Parse(pieces[1]), ExpiresOn = ulong.Parse(pieces[2]), OwnerID = ulong.Parse(pieces[3]) } },
    { "Worshiper", (pieces, gameObj) => new WorshiperTrait() { AltarLoc = Loc.FromStr(pieces[1]), AltarId = ulong.Parse(pieces[2]), Chant = pieces[3] } }
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