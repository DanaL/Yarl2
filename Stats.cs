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
  HitDie,
  Strength,
  Dexterity,
  Constitution,
  Piety, // What I renamed D&D's Wisdom
  MeleeAttackBonus,
  MissileAttackBonus,
  MonsterAttackBonus, // not going to bother having separate melee/missile atk bonuses for monsters
  Level,
  XP,
  Depth,
  AC, // used for monsters who have a simple AC
  DmgDie,
  DmgRolls,
  XPValue,
  MeleeDmgBonus,
  MissileDmgBonus,
  Radius,
  HomeID,
  MetPlayer,
  DialogueState
}

class Stat
{
  // Empty constructor and public setter methods was just simpler
  // for serialization
  public int Max { get; set; }
  public int Curr { get; set; }

  public Stat() { }

  public Stat(int maxValue)
  {
    Max = maxValue;
    Curr = maxValue;
  }

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

