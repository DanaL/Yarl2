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

enum ArmourParts
{
    Helmet,
    Boots,
    Cloak,
    Shirt
}

class Armour : Item
{
    public ArmourParts Piece { get; set; }
}

interface IUSeable
{
    (bool, string) Use(GameState gs, int row, int col);
}

abstract class ItemTrait 
{
    public abstract string Desc();
    public abstract ItemTrait Duplicate(Item container);
    public abstract string AsText();
}

class MeleeAttackTrait : ItemTrait
{
    public int DamageDie { get; set; }
    public int NumOfDie { get; set; }
    public int Bonus { get; set; }

    public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";

    public override ItemTrait Duplicate(Item _) => 
        new MeleeAttackTrait() { Bonus = Bonus, DamageDie = DamageDie, NumOfDie = NumOfDie };

    public override string AsText() => $"MeleeAttackTrait,{DamageDie},{NumOfDie},{Bonus}";
}

class ArmourTrait : ItemTrait
{
    public ArmourParts Part { get; set; }
    public int ArmourMod {  get; set; }
    public int Bonus { set; get; }

    public override string Desc() => Bonus == 0 ? "" : $"[{Bonus}]";

    public override ItemTrait Duplicate(Item _) => 
        new ArmourTrait() { Bonus = Bonus, ArmourMod = ArmourMod, Part = Part };

    public override string AsText() => $"ArmourTrait,{Part},{ArmourMod},{Bonus}";
}

class LightSourceTrait() : ItemTrait, IPerformer, IUSeable
{
    public ulong ContainerID { get; set; }
    public bool Lit { get; set; }
    public int Radius { get; set; }
    public int Fuel { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery { get; set; }

    public override string Desc() => Lit ? "(lit)" : "";

    public override string AsText()
    {
        return $"LightSourceTrait,{ContainerID},{Lit},{Radius},{Fuel},{Energy},{Recovery}";
    }

    public override ItemTrait Duplicate(Item container)
    {
        return new LightSourceTrait()
        {
            ContainerID = container.ID,
            Fuel = Fuel,
            Radius = Radius,
            Lit = Lit,
            Energy = 0.0,
            Recovery = Recovery
        };
    }

    public (bool, string) Use(GameState gs, int row, int col)
    {
        Item item = (Item)gs.ObjDB.GetObj(ContainerID);
        var loc = new Loc(gs.CurrDungeon, gs.CurrLevel, row, col);
        if (Lit)
        {
            gs.CurrPerformers.Remove(this);

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
            gs.CurrPerformers.Add(this);
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
            return new PassAction(this);

        if (--Fuel > 0)
        {
            // I could also alert the player here that the torch is flickering, about to go out, etc            
            return new PassAction(this);
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
    public static ItemTrait FromText(string text)
    {
        var pieces = text.Split(',');
        var type = pieces[0];

        ItemTrait trait;

        switch (type)
        {
            case "MeleeAttackTrait":
                trait = new MeleeAttackTrait()
                {
                    DamageDie = int.Parse(pieces[1]),
                    NumOfDie = int.Parse(pieces[2]),
                    Bonus = int.Parse(pieces[3])
                };
                break;
            case "ArmourTrait":
                var part = pieces[1] switch
                {
                    "Helmet" => ArmourParts.Helmet,
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
            default:
                throw new Exception("I don't know how to make that kind of Trait");
        }

        return trait;
    }
}