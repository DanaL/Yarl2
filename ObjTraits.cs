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

abstract class ObjTrait 
{
    public virtual string Desc() => "";
    public abstract string AsText();
    public abstract bool Acitve { get; }
    public virtual bool Aura => false;
    public virtual TerrainFlag Effect => TerrainFlag.None;
    public Dictionary<Attribute, Stat> Stats { get; set; } = [];
    public virtual int Radius => 0;
}

class ExpiresTrait : ObjTrait, IPerformer
{
    public ulong ExpiresOn { get; set; }
    public ulong ContainerID { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery { get; set; }

    public override bool Acitve => true;
    public override string AsText() => $"ExpiresTrait#{ExpiresOn}";

    public Action TakeTurn(UserInterface ui, GameState gameState)
    {
        if (gameState.Turn >= ExpiresOn)
            return new ObjTraitExpiredAction(this, gameState);
        else
            return new PassAction();
    }
}

class OpaqueTrait : ObjTrait
{
    public override bool Acitve => false;
    public override string AsText() => "OpaqueTrait";
    public override TerrainFlag Effect => TerrainFlag.Obscures;
}

class BlinkTrait : ObjTrait, IUSeable
{
    public override bool Acitve => throw new NotImplementedException();
    public override string AsText() => "BlinkTrait";

    public UseResult Use(Actor user, GameState gs, int row, int col)
    {
        List<Loc> sqs = [];
        var start = user.Loc;

        for (var r = start.Row - 12; r < start.Row + 12; r++)
        {
            for (var c = start.Col - 12; c < start.Col + 12; c++)
            {
                var loc = start with { Row = r, Col = c };
                int d = Util.Distance(start, loc);
                if (d >= 8 && d <= 12 && gs.TileAt(loc).Passable() && !gs.ObjDB.Occupied(loc))
                {
                    sqs.Add(loc);
                }
            }
        }

        if (sqs.Count == 0)
        {
            return new UseResult(false, "Bloop?", null, null);
        }
        else
        {
            var landingSpot = sqs[gs.UI.Rng.Next(sqs.Count)];            
            var mv = new MoveAction(user, landingSpot, gs, gs.UI.Rng);
            return new UseResult(true, "Bamf!", mv, null);
        }        
    }    
}

class MinorHealTrait : ObjTrait, IUSeable
{
    public override bool Acitve => throw new NotImplementedException();
    public override string AsText() => "MinorHealTrait";
    
    public UseResult Use(Actor user, GameState gs, int row, int col)
    {        
        var hp = 0;
        for (int j = 0; j < 4; j++)
            hp += gs.UI.Rng.Next(4) + 1;
        user.Stats[Attribute.HP].Change(hp);
        var msg = MessageFactory.Phrase(user.ID, Verb.Etre, Verb.Heal, false, user.Loc, gs);
        var txt = msg.Text[..^1] + $" for {hp} HP.";

        return new UseResult(true, txt, null, null);
    }
}

class AttackTrait : ObjTrait
{
    public int Bonus { get; set; }
    
    public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";
    public override string AsText() => $"AttackTrait#{Bonus}";    
    public override bool Acitve => true;
}

class DamageTrait : ObjTrait
{
    public int DamageDie { get; set; }
    public int NumOfDie { get; set; }
    public DamageType DamageType { get; set; }

    public override string AsText() => $"DamageTrait#{DamageDie}#{NumOfDie}#{DamageType}";    
    public override string Desc() => "";
    public override bool Acitve => true;
    public override bool Aura => false;
}

class ArmourTrait : ObjTrait
{
    public ArmourParts Part { get; set; }
    public int ArmourMod {  get; set; }
    public int Bonus { set; get; }

    public override string Desc() => Bonus == 0 ? "" : $"[{Bonus}]";
    public override string AsText() => $"ArmourTrait#{Part}#{ArmourMod}#{Bonus}";
    public override bool Aura => false;
    public override bool Acitve => true;
}

class ReadableTrait(string text) : ObjTrait, IUSeable
{
    readonly string _text = text;
    public ulong ContainerID { get; set; }
    public override bool Acitve => true;
    public override string AsText() => $"DocumentTrait#{_text}";
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

class FlameLightSourceTrait : ObjTrait, IPerformer, IUSeable
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

    public override bool Acitve => Lit;
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
    public static ObjTrait FromText(string text)
    {
        var pieces = text.Split('#');
        var type = pieces[0];

        ObjTrait trait;

        switch (type)
        {
            case "AttackTrait":
                trait = new AttackTrait()
                {
                    Bonus = int.Parse(pieces[3])
                };
                break;
            case "DamageTrait":
                Enum.TryParse(pieces[3], out DamageType damageType);
                trait = new DamageTrait()
                {
                    DamageDie = int.Parse(pieces[1]),
                    NumOfDie = int.Parse(pieces[2]),
                    DamageType = damageType
                };
                break;
            case "ArmourTrait":
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
            case "LightSourceTrait":
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
            case "ReadableTrait":
                trait = new ReadableTrait(pieces[1].Replace("<br/>", "\n"));
                break;
            default:
                throw new Exception("I don't know how to make that kind of Trait");
        }

        return trait;
    }
}