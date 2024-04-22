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
      MoveStrategy = new DoorOpeningMoveStrategy(),
      Glyph = glyph
    };
    prince.SetBehaviour(new PrinceOfRatsBehaviour());
    prince.Stats[Attribute.Attitude] = new Stat((int)MobAttitude.Idle);
    
    prince.Stats.Add(Attribute.HP, new Stat(80));
    prince.Stats.Add(Attribute.MonsterForm, new Stat(0));

    prince.Stats.Add(Attribute.MonsterAttackBonus, new Stat(7));
    prince.Stats.Add(Attribute.AC, new Stat(15));
    prince.Stats.Add(Attribute.Strength, new Stat(1));
    prince.Stats.Add(Attribute.Dexterity, new Stat(3));
    prince.Stats.Add(Attribute.XPValue, new Stat(20));

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
      prince.Traits.Add(new Immunity() { Type = DamageType.Piercing });
    else
      prince.Traits.Add(new Immunity() { Type = DamageType.Blunt });

    return prince;
  }
}

class PrinceOfRatsBehaviour : IBehaviour
{
  const int HUMAN_FORM = 0;
  const int RAT_FORM = 1;

  DateTime _lastQuip = DateTime.Now;

  static Action CalcMoveAction(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ConfusedTrait>())
      return new MoveAction(gs, mob, Util.RandomAdjLoc(mob.Loc, gs));
    else
      return mob.MoveStrategy.MoveAction(mob, gs);
  }

  void CheckChangeForm(Mob prince, GameState gs, int currForm)
  {
    if (gs.Rng.NextDouble() < 0.1)
    {
      if (prince.Stats[Attribute.MonsterForm].Curr == HUMAN_FORM)
      {
        prince.Glyph = new Glyph('r', Colours.GREY, Colours.DARK_GREY);
        prince.Stats[Attribute.MonsterForm].SetMax(RAT_FORM);
      }
      else
      {
        prince.Glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREY);
        prince.Stats[Attribute.MonsterForm].SetMax(HUMAN_FORM);
      }

      gs.UIRef().AlertPlayer(new Message("The Prince of Rats shifts forms!", prince.Loc), "", gs);
    }
  }

  public Action CalcAction(Mob actor, GameState gameState, UserInterface ui)
  {
    if (actor.Status == MobAttitude.Idle)
    {
      return new PassAction();
    }

    Action action;
    int currForm = actor.Stats[Attribute.MonsterForm].Curr;

    CheckChangeForm(actor, gameState, currForm);

    if (Util.Distance(actor.Loc, gameState.Player.Loc) == 1)
    {
      actor.Dmg = new Damage(5, 2, DamageType.Slashing);
      action = new MeleeAttackAction(gameState, actor, gameState.Player.Loc);     
    }
    else
    {
      action = CalcMoveAction(actor, gameState);
    }

    if (currForm == HUMAN_FORM && (DateTime.Now - _lastQuip).TotalSeconds > 10)
    {
      _lastQuip = DateTime.Now;
      action.Quip = "Cheese for the Rat God!";
    }

    return action;
  }

  public (Action, InputAccumulator?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}
