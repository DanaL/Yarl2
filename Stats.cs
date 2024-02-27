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

// Class to trait various and sundry stats that game objects might have.
// My plan is to have almost anything numeric be a stat: armour mods
// the classic str, dex, etc, fuel a torch has

namespace Yarl2;

enum Attribute 
{
    HP,
    Strength,
    Dexterity,
    Constitution,
    Piety, // What I renamed D&D's Wisdom
    MeleeAttackBonus,
    Level,
    XP,
    Depth
}

class Stat(int maxValue)
{
    public int Max { get; private set; } = maxValue;
    public int Curr { get; private set; } = maxValue;

    public void SetMax(int newMax) 
    {
        Max = newMax;
        Curr = newMax;
    } 

    public int ChangeMax(int delta)
    {
        Max += delta;
        return Max;
    }

    public int Change(int delta)
    {
        Curr += delta;
        if (Curr > Max)
            Curr = Max;

        return Curr;
    }

    public void Reset() => Curr = Max;
}

