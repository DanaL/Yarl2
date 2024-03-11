﻿// Yarl2 - A roguelike computer RPG
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

interface IReadable
{
    void Read(Actor actor, UserInterface ui, Item document);
}

interface IUSeable
{
    (bool, string) Use(Actor user, GameState gs, int row, int col);
}

abstract class ObjTrait 
{
    public abstract string Desc();
    //public abstract ObjTrait Duplicate(Item container);
    public abstract string AsText();
    public abstract bool Acitve { get; }
}

class MinorHealTrait : ObjTrait, IUSeable
{
    public override bool Acitve => throw new NotImplementedException();

    public override string AsText() => "MinorHealTrait";
    public override string Desc() => "";
    //public override ObjTrait Duplicate(Item container)  => new MinorHealTrait();

    public (bool, string) Use(Actor user, GameState gs, int row, int col)
    {        
        var hp = 0;
        for (int j = 0; j < 4; j++)
            hp += gs.UI.Rng.Next(4) + 1;
        user.Stats[Attribute.HP].Change(hp);
        var msg = MessageFactory.Phrase(user.ID, Verb.Etre, Verb.Heal, false, user.Loc, gs);
        var txt = msg.Text[..^1] + $" for {hp} HP.";

        return (true, txt);
    }
}

class AttackTrait : ObjTrait
{
    public int Bonus { get; set; }
    
    public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";

    //public override ObjTrait Duplicate(Item _) => 
    //    new AttackTrait() { Bonus = Bonus };

    public override string AsText() => $"AttackTrait#{Bonus}";

    public override bool Acitve => true;
}

class DamageTrait : ObjTrait
{
    public int DamageDie { get; set; }
    public int NumOfDie { get; set; }
    public DamageType DamageType { get; set; }

    public override string AsText() => $"DamageTrait#{DamageDie}#{NumOfDie}#{DamageType}";
    
    //public override ObjTrait Duplicate(Item _) =>
    //    new DamageTrait() { DamageDie = DamageDie, NumOfDie = NumOfDie, DamageType = DamageType };

    public override string Desc() => "";
    public override bool Acitve => true;    
}

class ArmourTrait : ObjTrait
{
    public ArmourParts Part { get; set; }
    public int ArmourMod {  get; set; }
    public int Bonus { set; get; }

    public override string Desc() => Bonus == 0 ? "" : $"[{Bonus}]";

    //public override ObjTrait Duplicate(Item _) => 
    //    new ArmourTrait() { Bonus = Bonus, ArmourMod = ArmourMod, Part = Part };

    public override string AsText() => $"ArmourTrait#{Part}#{ArmourMod}#{Bonus}";

    public override bool Acitve => true;
}

class DocumentTrait : ObjTrait, IReadable
{
    string _text;

    public DocumentTrait(string text)
    {
        _text = text;
    }

    public override bool Acitve => true;
    public override string Desc() => "";
    public override string AsText() => $"DocumentTrait#{_text}";

    //public override ObjTrait Duplicate(Item container)
    //{
    //    throw new NotImplementedException();
    //}

    public void Read(Actor actor, UserInterface ui, Item document)
    {
        string msg = $"{actor.FullName} read:\n{_text}";
        ui.Popup(msg, document.FullName.IndefArticle());   
    }
}

class LightSourceTrait : ObjTrait, IPerformer, IUSeable
{
    public ulong ContainerID { get; set; }
    public bool Lit { get; set; }
    public int Radius { get; set; }
    public int Fuel { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery { get; set; }

    public override string Desc() => Lit ? "(lit)" : "";

    public override bool Acitve => Lit;

    public override string AsText()
    {
        return $"LightSourceTrait#{ContainerID}#{Lit}#{Radius}#{Fuel}#{Energy}#{Recovery}";
    }

    //public override ObjTrait Duplicate(Item container)
    //{
    //    return new LightSourceTrait()
    //    {
    //        ContainerID = container.ID,
    //        Fuel = Fuel,
    //        Radius = Radius,
    //        Lit = Lit,
    //        Energy = 0.0,
    //        Recovery = Recovery
    //    };
    //}

    public (bool, string) Use(Actor _, GameState gs, int row, int col)
    {
        Item item = gs.ObjDB.GetObj(ContainerID) as Item;
        var loc = new Loc(gs.CurrDungeon, gs.CurrLevel, row, col);
        if (Lit)
        {
            gs.Performers.Remove(this);

            // Gotta set the lighting level before we extinguish the torch
            // so it's radius is still 5 when calculating which squares to 
            // affect            
            gs.ToggleEffect(item, loc, TerrainFlags.Lit, false);
            Lit = false;

            return (true, $"You extinguish {item.FullName.DefArticle()}.");
        }
        else if (Fuel > 0)
        {
            Lit = true;
            item.Stackable = false;
            Energy = Recovery;
            gs.Performers.Add(this);
            gs.ToggleEffect(item, loc, TerrainFlags.Lit, true);

            return (true, $"The {item.Name} sparks to life!");
        }
        else
        {
            return (false, $"That {item.Name} is burnt out!");
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
                trait = new LightSourceTrait()
                {
                    ContainerID = ulong.Parse(pieces[1]),
                    Lit = bool.Parse(pieces[2]),
                    Radius = int.Parse(pieces[3]),
                    Fuel = int.Parse(pieces[4]),
                    Energy = int.Parse(pieces[5]),
                    Recovery = int.Parse(pieces[6])
                };
                break;
            case "DocumentTrait":
                trait = new DocumentTrait(pieces[1].Replace("<br/>", "\n"));
                break;
            default:
                throw new Exception("I don't know how to make that kind of Trait");
        }

        return trait;
    }
}