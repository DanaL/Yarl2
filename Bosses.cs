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

// Code for bosses who have specific Behaviour classes. They get their own
// file to prevent Behaviour.cs from getting too cluttered.

namespace Yarl2;

class BossFactory
{
  public static Mob Get(string name, Random rng)
  {    
    if (name == "Prince of Rats")
      return PrintOfRats(rng);

    throw new Exception($"Uhoh -- unknown boss {name}!");
  }

  static Mob PrintOfRats(Random rng)
  {
    var glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREY);
    var prince = new Mob()
    {
      Name = "Prince of Rats",
      Recovery = 1.0,
      MoveStrategy = new DumbMoveStrategy(),
      Glyph = glyph
    };
    prince.SetBehaviour(new PrinceOfRatsBehaviour());

    prince.Stats.Add(Attribute.HP, new Stat(80));
    prince.Stats.Add(Attribute.MonsterForm, new Stat(0));

    //prince.Stats.Add(Attribute.MonsterAttackBonus, new Stat(4));
    //prince.Stats.Add(Attribute.AC, new Stat(15));
    //prince.Stats.Add(Attribute.Strength, new Stat(1));
    //prince.Stats.Add(Attribute.Dexterity, new Stat(1));
    //prince.Stats.Add(Attribute.XPValue, new Stat(6));

    //prince.Actions.Add(new MobMeleeTrait()
    //{
    //  MinRange = 1,
    //  MaxRange = 1,
    //  DamageDie = 5,
    //  DamageDice = 2,
    //  DamageType = DamageType.Blunt
    //});

    //prince.Stats[Attribute.Attitude] = new Stat((int)MobAttitude.Active);
    //prince.Stats[Attribute.InDisguise] = new Stat(1);

    //var disguise = new DisguiseTrait()
    //{
    //  Disguise = glyph,
    //  TrueForm = new Glyph('G', Colours.LIGHT_GREY, Colours.GREY),
    //  DisguiseForm = "statue"
    //};
    //prince.Traits.Add(disguise);
    //prince.Traits.Add(new FlyingTrait());
    //prince.Traits.Add(new ResistPiercingTrait());
    //prince.Traits.Add(new ResistSlashingTrait());

    // The Prince is immune to one of slashing, blunt, or piercing damage
    int roll = rng.Next(3);
    if (roll == 0)
      prince.Traits.Add(new Immunity() { Type = DamageType.Slashing });
    else if (roll == 1)
      prince.Traits.Add(new Immunity() { Type = DamageType.Blunt });
    else
      prince.Traits.Add(new Immunity() { Type = DamageType.Piercing });

    return prince;
  }
}

class PrinceOfRatsBehaviour : IBehaviour
{
  const int HUMAN_FORM = 0;
  const int RAT_FORM = 1;

  DateTime _lastQuip = DateTime.Now;

  public Action CalcAction(Mob actor, GameState gameState, UserInterface ui)
  {
    int currForm = actor.Stats[Attribute.MonsterForm].Curr;

    if (gameState.Rng.NextDouble() < 0.1)
    {
      if (actor.Stats[Attribute.MonsterForm].Curr == HUMAN_FORM)
      {
        actor.Glyph = new Glyph('r', Colours.GREY, Colours.DARK_GREY);
        actor.Stats[Attribute.MonsterForm].SetMax(RAT_FORM);
      }
      else
      {
        actor.Glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREY);
        actor.Stats[Attribute.MonsterForm].SetMax(HUMAN_FORM);
      }

      gameState.UIRef().AlertPlayer(new Message("The Prince of Rats shifts forms!", actor.Loc), "", gameState);
    }

    if (currForm == HUMAN_FORM && (DateTime.Now - _lastQuip).TotalSeconds > 10)
    {
      _lastQuip = DateTime.Now;

      return new PassAction()
      {
        Actor = actor,
        GameState = gameState,
        Quip = "Cheese for the Rat God!"
      };
    }
    else
    {
      return new PassAction();
    }
  }

  public (Action, InputAccumulator?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}
