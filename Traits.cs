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

enum AuraEffect { Light }

interface IReadable
{
    void Read(Actor actor, UserInterface ui, Item document);
}

interface IUSeable
{
    UseResult Use(Actor user, GameState gs, int row, int col);
}

abstract class Trait 
{
    public virtual string Desc() => "";
    public abstract string AsText();
    public virtual bool Active => true;
    public virtual bool Aura => false;
    public virtual TerrainFlag Effect => TerrainFlag.None;
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    public virtual int Radius => 0;
    public ulong ExpiresOn { get; set; } = ulong.MaxValue;
}

class PlantTrait : Trait
{
    public override string AsText() => "Plant";
}

class PluralTrait : Trait
{
    public override string AsText() => "Plural";
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
    Actor _actor = actor;

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

    public override string AsText() => "Flying";
}

// Technically I suppose this is a Count Up not a Count Down...
class CountdownTrait : Trait, IGameEventListener
{
    public bool Expired { get; set; } = false;
    public override string AsText() => "EventListenerTrait";
    public ulong ContainerID { get; set; }

    public void Alert(UIEventType eventType, GameState gs)
    {
        if (gs.Turn < ExpiresOn)
            return;

        Expired = true;

        if (gs.ObjDB.GetObj(ContainerID) is Item item)
        {
            Loc loc = item.Loc;

            // Alert! Alert! This is cut-and-pasted from ExtinguishAction()
            if (item.ContainedBy > 0)
            {
                var owner = gs.ObjDB.GetObj(item.ContainedBy);
                if (owner is not null)
                {
                    // I don't think owner should ever be null, barring a bug
                    // but this placates the warning in VS/VS Code
                    loc = owner.Loc;
                    ((Actor)owner).Inventory.Remove(item.Slot, 1);
                }
            }

            gs.ObjDB.RemoveItem(loc, item);

            // This is rather tied to Fog Cloud atm -- I should perhaps provide an
            // expiry message that can be set for each trait
            var msg = MessageFactory.Phrase(item.ID, Verb.Dissipate, 0, 1, false, loc, gs);
            gs.UI.AlertPlayer([msg], "");                
        }              
    }
}

class OpaqueTrait : Trait
{
    public override string AsText() => "Opaque";
    public override TerrainFlag Effect => TerrainFlag.Obscures;
}

class CastBlinkTrait : Trait, IUSeable
{
    public override string AsText() => "Blink";

    public UseResult Use(Actor user, GameState gs, int row, int col)
    {
        return new UseResult(true, "", new BlinkAction(user, gs), null);
    }    
}

class CastMinorHealTrait : Trait, IUSeable
{
    public override string AsText() => "MinorHeal";
    
    public UseResult Use(Actor user, GameState gs, int row, int col)
    {        
        return new UseResult(true, "", new HealAction(user, gs, 4, 4), null);
    }
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

class ReadableTrait(string text) : Trait, IUSeable
{
    readonly string _text = text;
    public ulong ContainerID { get; set; }
    public override string AsText() => $"Document#{_text}";
    public override bool Aura => false;

    public UseResult Use(Actor user, GameState gs, int row, int col)
    {
        Item? doc = gs.ObjDB.GetObj(ContainerID) as Item;
        string msg = $"{user.FullName.Capitalize()} read:\n{_text}";        
        gs.UI.Popup(msg, doc!.FullName.IndefArticle().Capitalize());

        var action = new CloseMenuAction(gs.UI, 1.0);
        var acc = new PauseForMoreAccumulator();
        
        return new UseResult(false, "", action, acc);
    }
}

class FlameLightSourceTrait : Trait, IPerformer, IUSeable
{
    public ulong ContainerID { get; set; }
    public bool Lit { get; set; }
    public int Fuel { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery { get; set; }
    public override bool Aura => true;
    public override TerrainFlag Effect => TerrainFlag.Lit;
    public override string Desc() => Lit ? "(lit)" : "";    

    public override bool Active => Lit;
    public override int Radius => Lit ? Stats[Attribute.Radius].Max : 0;
    
    public override string AsText()
    {
        return $"FlameLightSourceTrait#{ContainerID}#{Lit}#{Fuel}#{Energy}#{Recovery}";
    }

    public UseResult Use(Actor _, GameState gs, int row, int col)
    {
        Item? item = gs.ObjDB.GetObj(ContainerID) as Item;
        var loc = new Loc(gs.CurrDungeon, gs.CurrLevel, row, col);
        if (Lit)
        {
            gs.Performers.Remove(this);

            // Gotta set the lighting level before we extinguish the torch
            // so it's radius is still 5 when calculating which squares to 
            // affect            
            gs.ToggleEffect(item!, loc, TerrainFlag.Lit, false);
            Lit = false;

            for (int j = 0; j < item!.Traits.Count; j++)
            {
                if (item!.Traits[j] is DamageTrait dt && dt.DamageType == DamageType.Fire)
                {
                    item!.Traits.RemoveAt(j);
                    break;
                }
            }

            return new UseResult(true, $"You extinguish {item!.FullName.DefArticle()}.", null, null);
        }
        else if (Fuel > 0)
        {
            Lit = true;
            item!.Stackable = false;
            Energy = Recovery;
            gs.Performers.Add(this);
            gs.ToggleEffect(item, loc, TerrainFlag.Lit, true);

            item!.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 1, DamageType = DamageType.Fire });
            return new UseResult(true, $"The {item.Name} sparks to life!", null, null);
        }
        else
        {
            return new UseResult(false, $"That {item!.Name} is burnt out!", null, null);
        }
    }

    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        if (!Lit)
            return new PassAction();

        if (--Fuel > 0)
        {
            // I could also alert the player here that the torch is flickering, about to go out, etc            
            return new PassAction();
        }
        else
        {
            Lit = false;
            return new ExtinguishAction(this, gameState);
        }
    }
}

class TraitFactory
{
    public static Trait FromText(string text)
    {
        var pieces = text.Split('#');
        var type = pieces[0];

        Trait trait;

        switch (type)
        {
            case "Attack":
                trait = new AttackTrait()
                {
                    Bonus = int.Parse(pieces[3])
                };
                break;
            case "Damage":
                Enum.TryParse(pieces[3], out DamageType damageType);
                trait = new DamageTrait()
                {
                    DamageDie = int.Parse(pieces[1]),
                    NumOfDie = int.Parse(pieces[2]),
                    DamageType = damageType
                };
                break;
            case "Armour":
                var part = pieces[1] switch
                {
                    "Helmet" => ArmourParts.Hat,
                    "Boots" => ArmourParts.Boots,
                    "Cloak" => ArmourParts.Cloak,
                    "Shirt" => ArmourParts.Shirt,
                    _ => throw new Exception("I don't know about that Armour Part")
                };
                trait = new ArmourTrait()
                {
                    Part = part,
                    ArmourMod = int.Parse(pieces[2]),
                    Bonus = int.Parse(pieces[3])
                };
                break;
            case "LightSource":
                trait = new FlameLightSourceTrait()
                {
                    ContainerID = ulong.Parse(pieces[1]),
                    Lit = bool.Parse(pieces[2]),                    
                    Fuel = int.Parse(pieces[4]),
                    Energy = int.Parse(pieces[5]),
                    Recovery = int.Parse(pieces[6])
                };
                trait.Stats[Attribute.Radius].SetMax(5);
                break;
            case "Readable":
                trait = new ReadableTrait(pieces[1].Replace("<br/>", "\n"));
                break;
            case "Flying":
                trait = new FlyingTrait();
                break;
            case "Plant":
                trait = new PlantTrait();
                break;
            case "Plural":
                trait = new PluralTrait();
                break;
            default:
                throw new Exception("I don't know how to make that kind of Trait");
        }

        return trait;
    }
}