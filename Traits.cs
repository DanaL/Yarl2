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
    string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc);
}

abstract class Trait 
{
    public virtual string Desc() => "";
    public virtual bool Active => true;
    public virtual bool Aura => false;
    public virtual TerrainFlag Effect => TerrainFlag.None;
    public virtual int Radius { get; set; } = 0;
    public ulong ExpiresOn { get; set; } = ulong.MaxValue;
    public virtual string AsText() => $"{ExpiresOn}#{Radius}";
}

// To let me classify traits that mobs can take on their turns
// Not sure if this is the best way to go...
abstract class ActionTrait : Trait
{
    // I was thinking I could use MinRange to set abilities a monster might use
    // from further away. Ie., gobin archer has one attack from distance 2 to 7
    // and another they use when they are in melee range.
    public int MinRange { get; set; } = 0;
    public int MaxRange { get; set; } = 0;
    public ulong Cooldown { get; set; } = 0;
    public string Name { get; set; } = "";

    public abstract bool Available(Monster mob, GameState gs);
    protected bool InRange(Monster mob, GameState gs)
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

    public static List<Loc> Trajectory(Monster mob, Loc target)
    {
        return Util.Bresenham(mob.Loc.Row, mob.Loc.Col, target.Row, target.Col)
                   .Select(sq => mob.Loc with { Row = sq.Item1, Col = sq.Item2 })
                   .ToList();
    }
}

class SpellActionTrait : ActionTrait
{
    public override string AsText() => $"SpellAction#{Name}#{MinRange}#{MaxRange}#{Cooldown}#";
    public override bool Available(Monster mob, GameState gs) => true;
}

class FireboltActionTrait : SpellActionTrait
{    
    public override string AsText() => $"FireboltAction#{Cooldown}#{MinRange}#{MaxRange}#";
    public override bool Available(Monster mob, GameState gs)
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

    public override bool Available(Monster mob, GameState gs) => InRange(mob, gs);
}

class MobMissileTrait : ActionTrait
{
    public override string AsText() => $"MobMissile#{MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}#";
    public int DamageDie { get; set; }
    public int DamageDice { get; set; }
    public DamageType DamageType { get; set; }

    public override bool Available(Monster mob, GameState gs)
    {
        if (!InRange(mob, gs))
            return false;

        var p = gs.Player;                
        return ClearShot(gs, Trajectory(mob, p.Loc));
    }
}

class StickyTrait : Trait
{
    public int DC => 13;

    public override string AsText() => "Sticky";
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

class ImpaleTrait : Trait
{
    public override string AsText() => "Impale";
}

class CleaveTrait : Trait
{
    public override string AsText() => "Cleave";
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

class FlyingTrait : Trait
{
    public FlyingTrait() { }
    public FlyingTrait(ulong expiry) => ExpiresOn = expiry;

    public override string AsText() => $"Flying#{ExpiresOn}";
}

class OpaqueTrait : Trait
{
    public override string AsText() => "Opaque";
    public override TerrainFlag Effect => TerrainFlag.Obscures;
}

class CastAntidoteTrait : Trait, IUSeable
{   
    public override string AsText() => "CastAntidote";
    public string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc) => "";
 
    public UseResult Use(Actor user, GameState gs, int row, int col)
    {
        return new UseResult(true, "", new AntidoteAction(user, gs), null);
    }
}

// For items that can cast blink
class CastBlinkTrait : Trait, IUSeable
{
    public override string AsText() => "CastBlink";

    public UseResult Use(Actor user, GameState gs, int row, int col)
    {
        return new UseResult(true, "", new BlinkAction(user, gs), null);
    }

    public string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc) => "";
}

class CastMinorHealTrait : Trait, IUSeable
{
    public override string AsText() => "CastMinorHeal";
    
    public UseResult Use(Actor user, GameState gs, int row, int col)
    {        
        return new UseResult(true, "", new HealAction(user, gs, 4, 4), null);
    }

    public string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc) => "";
}

class AttackTrait : Trait
{
    public int Bonus { get; set; }
    
    public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";
    public override string AsText() => $"Attack#{Bonus}";        
}

class DamageTrait : Trait
{
    public int DamageDie { get; set; }
    public int NumOfDie { get; set; }
    public DamageType DamageType { get; set; }

    public override string AsText() => $"Damage#{DamageDie}#{NumOfDie}#{DamageType}";    
    public override string Desc() => "";    
    public override bool Aura => false;
}

class ACModTrait : Trait
{
    public int ArmourMod { get; set; }
    public override string AsText() => $"ACMode{ArmourMod}";
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

class PoisonerTrait: Trait
{
    public int DC { get; set; }
    public int Strength { get; set; }

    public override string AsText() => $"Poisoner#{DC}#{Strength}";
}

class OnFireTrait : Trait, IGameEventListener
{
    public ulong ContainerID { get; set; }    
    public bool Expired { get; set; } = false;
    public int Lifetime { get; set; } = 0;

    public override string AsText() => $"OnFire#{Expired}#{ContainerID}#{Lifetime}";

    public void Extinguish(Item fireSrc, GameState gs)
    {
        gs.WriteMessages([new Message("The fire burns out.", fireSrc.Loc)], "");
        gs.ObjDb.RemoveItemFromGame(fireSrc.Loc, fireSrc);
        gs.ItemDestroyed(fireSrc, fireSrc.Loc);

        Expired = true;
    }

    public void Alert(UIEventType eventType, GameState gs)
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

            if (victim is not null) {
                int fireDmg = gs.Rng.Next(8) + 1;
                List<(int, DamageType)> fire = [(fireDmg, DamageType.Fire)];
                int hpLeft = victim.ReceiveDmg(fire, 0);

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

class PoisonedTrait : Trait, IGameEventListener
{
    public int DC { get; set; }
    public int Strength { get; set; }
    public ulong VictimID { get; set; }
    public bool Expired { get; set; } = false;

    public override string AsText() => $"Poisoned#{DC}#{Strength}#{VictimID}#{Expired}";

    public void Alert(UIEventType eventType, GameState gs)
    {
        var victim = (Actor?) gs.ObjDb.GetObj(VictimID);
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
                int hpLeft = victim.ReceiveDmg(p, 0);

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

class ReadableTrait(string text) : Trait, IUSeable
{
    readonly string _text = text;
    public ulong ContainerID { get; set; }
    public override string AsText() => $"Document#{_text}#{ContainerID}";
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

    public string ApplyEffect(TerrainFlag flag, GameState gs, Item item, Loc loc) => "";
}

// Technically I suppose this is a Count Up not a Count Down...
class CountdownTrait : Trait, IGameEventListener
{
    public bool Expired { get; set; } = false;
    public ulong ContainerID { get; set; }

    public override string AsText() => $"Countdown#{ContainerID}#{Expired}";

    public void Alert(UIEventType eventType, GameState gs)
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
class LightSourceTrait : Trait
{
    public ulong ContainerID { get; set; }
    public override int Radius { get; set; }
    public sealed override bool Aura => true;
    public sealed override TerrainFlag Effect => TerrainFlag.Lit;

    public override string AsText() => $"LightSource#{ContainerID}#{Radius}";    
}

class TorchTrait : Trait, IGameEventListener, IUSeable
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
        gs.StopListening(UIEventType.EndOfRound, this);

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
            gs.RegisterForEvent(UIEventType.EndOfRound, this);
            gs.ToggleEffect(item, loc, TerrainFlag.Lit, true);

            item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
            return new UseResult(true, $"The {item.Name} sparks to life!", null, null);
        }
        else
        {
            return new UseResult(false, $"That {item!.Name} is burnt out!", null, null);
        }
    }

    public void Alert(UIEventType eventType, GameState gs)
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
                var part = pieces[1] switch
                {
                    "Helmet" => ArmourParts.Hat,
                    "Boots" => ArmourParts.Boots,
                    "Cloak" => ArmourParts.Cloak,
                    "Shirt" => ArmourParts.Shirt,
                    _ => throw new Exception("I don't know about that Armour Part")
                };
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
            case "CastAntidote":
                return new CastAntidoteTrait();
            case "CastBlink":
                return new CastBlinkTrait();
            case "CastMinorHeal":
                return new CastMinorHealTrait();
            case "Cleave":
                return new CleaveTrait();
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
            case "Entangle":
            case "Web":            
                digits = Util.DigitsRegex().Split(text);
                return new SpellActionTrait()
                {
                    Name = name,
                    Cooldown = ulong.Parse(digits[1]),
                    MinRange = int.Parse(digits[2]),
                    MaxRange = int.Parse(digits[3])
                };
            case "Firebolt":
                digits = Util.DigitsRegex().Split(text);
                return new FireboltActionTrait()
                {
                    Name = name,
                    Cooldown = ulong.Parse(digits[1]),
                    MinRange = int.Parse(digits[2]),
                    MaxRange = int.Parse(digits[3])
                };
            case "Flammable":
                return new FlammableTrait();
            case "Flying":
                return new FlyingTrait();
            case "Impale":
                return new ImpaleTrait();
            // "LightSource#{ContainerID}#{Radius}"
            case "LightSource":
                return new LightSourceTrait()
                {   
                    ContainerID = ulong.Parse(pieces[1]),
                    Radius = int.Parse(pieces[2])
                };
            case "Melee":
                Enum.TryParse(text[(text.LastIndexOf('#') + 1)..], out DamageType mdt);
                digits = Util.DigitsRegex().Split(text);
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
                digits = Util.DigitsRegex().Split(text);
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
            case "OnFire":
                return new OnFireTrait()
                {
                    Expired = bool.Parse(pieces[1]),
                    ContainerID = ulong.Parse(pieces[2]),
                    Lifetime = int.Parse(pieces[3])
                };
            case "Opaque":
                return new OpaqueTrait();
            case "Plant":
                return new PlantTrait();
            case "Poisoned":
                return new PoisonedTrait()
                {
                    DC = int.Parse(pieces[1]),
                    Strength = int.Parse(pieces[2]),
                    VictimID = ulong.Parse(pieces[3]),
                    Expired = bool.Parse(pieces[4])
                };
            case "Poisoner":
                digits = Util.DigitsRegex().Split(text);
                return new PoisonerTrait()
                {
                    DC = int.Parse(digits[1]),
                    Strength = int.Parse(digits[2])
                };
            case "Plural":
                return new PluralTrait();
            case "Rage":
                return new RageTrait((Actor)container);
            case "Readable":
                return new ReadableTrait(pieces[1].Replace("<br/>", "\n"));
            case "ShieldOfTheFaithful":
                return new ShieldOfTheFaithfulTrait()
                {
                    ArmourMod = int.Parse(pieces[1])
                };
            case "Sticky":
                return new StickyTrait();
            case "Teflon":
                return new TeflonTrait();
            case "Torch":
                return new TorchTrait()
                {
                    ContainerID = ulong.Parse(pieces[1]),
                    Lit = bool.Parse(pieces[2]),                    
                    Fuel = int.Parse(pieces[4])                    
                };
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