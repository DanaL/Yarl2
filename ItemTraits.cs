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

using System;

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
}

class MeleeAttackTrait : ItemTrait
{
    public int DamageDie { get; set; }
    public int NumOfDie { get; set; }
    public int Bonus { get; set; }

    public override string Desc() => Bonus == 0 ? "" : $"({Bonus})";

    public override ItemTrait Duplicate(Item _) => 
        new MeleeAttackTrait() { Bonus = Bonus, DamageDie = DamageDie, NumOfDie = NumOfDie };
}
class ArmourTrait : ItemTrait
{
    public ArmourParts Part { get; set; }
    public int ArmourMod {  get; set; }
    public int Bonus { set; get; }

    public override string Desc() => Bonus == 0 ? "" : $"[{Bonus}]";

    public override ItemTrait Duplicate(Item _) => 
        new ArmourTrait() { Bonus = Bonus, ArmourMod = ArmourMod, Part = Part };
}

class LightSourceTrait(Item item) : ItemTrait, IPerformer, IUSeable
{
    public Item Item { get; set; } = item;
    public bool Lit { get; set; }
    public int Radius { get; set; }
    public int Fuel { get; set; }
    public bool RemoveFromQueue { get; set; }
    public double Energy { get; set; }
    public double Recovery { get; set; }

    public override string Desc() => Lit ? "(Lit)" : "";

    public override ItemTrait Duplicate(Item container)
    {
        return new LightSourceTrait(container)
        {
            Fuel = Fuel,
            Radius = Radius,
            Lit = Lit,
            Energy = 0.0,
            Recovery = Recovery
        };
    }

    public (bool, string) Use(GameState gs, int row, int col)
    {
        var loc = new Loc(gs.CurrDungeon, gs.CurrLevel, row, col);
        if (Lit)
        {
            gs.CurrPerformers.Remove(this);

            // Gotta set the lighting level before we extinguish the torch
            // so it's radius is still 5 when calculating which squares to 
            // affect
            gs.ToggleEffect(Item, loc, TerrainFlags.Lit, false);
            Lit = false;

            return (true, $"You extinguish {Item.FullName.DefArticle()}.");
        }
        else if (Fuel > 0)
        {
            Lit = true;
            Item.Stackable = false;
            Energy = Recovery;
            gs.CurrPerformers.Add(this);
            gs.ToggleEffect(Item, loc, TerrainFlags.Lit, true);

            return (true, $"The {Item.Name} sparks to life!");
        }
        else
        {
            return (false, $"That {Item.Name} is burnt out!");
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