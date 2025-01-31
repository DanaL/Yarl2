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
    else if (name == "the Great Goblin")
      return GreatGoblin(rng);
      
    throw new Exception($"Uhoh -- unknown boss {name}!");
  }

  static Mob GreatGoblin(Random rng)
  {
    var glyph = new Glyph('@', Colours.GREY, Colours.LIGHT_PURPLE, Colours.PURPLE, Colours.BLACK);
    var g = new Mob()
    {
      Name = "the Great Goblin",
      Recovery = 1.0,
      Glyph = glyph
    };
    g.SetBehaviour(new MonsterBehaviour());
    g.Stats.Add(Attribute.HP, new Stat(50));
    g.Stats.Add(Attribute.AttackBonus, new Stat(5));
    g.Stats.Add(Attribute.AC, new Stat(16));
    g.Stats.Add(Attribute.Strength, new Stat(2));
    g.Stats.Add(Attribute.Dexterity, new Stat(3));
    g.Stats.Add(Attribute.MobAttitude, new Stat(Mob.INDIFFERENT));

    // {MinRange}#{MaxRange}#{DamageDie}#{DamageDice}#{DamageType}
    g.Traits.Add(new IntelligentTrait());    
    g.Traits.Add(new KnockBackTrait());
    g.Traits.Add(new FearsomeBellowTrait()
    {
      Radius = 2,
      DC = 15,
      Cooldown = 20
    });
    g.CalcMoveStrategy();

    return g;
  }

  static Mob PrintOfRats(Random rng)
  {
    var glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK);
    var prince = new Mob()
    {
      Name = "Prince of Rats",
      Recovery = 1.0,
      MoveStrategy = new DoorOpeningMoveStrategy(),
      Glyph = glyph
    };
    prince.SetBehaviour(new PrinceOfRatsBehaviour());
    
    prince.Stats.Add(Attribute.HP, new Stat(80));
    prince.Stats.Add(Attribute.MonsterForm, new Stat(0));

    prince.Stats.Add(Attribute.AttackBonus, new Stat(7));
    prince.Stats.Add(Attribute.AC, new Stat(15));
    prince.Stats.Add(Attribute.Strength, new Stat(1));
    prince.Stats.Add(Attribute.Dexterity, new Stat(3));
    
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
      prince.Traits.Add(new ImmunityTrait() { Type = DamageType.Slashing });
    else if (roll == 1)
      prince.Traits.Add(new ImmunityTrait() { Type = DamageType.Piercing });
    else
      prince.Traits.Add(new ImmunityTrait() { Type = DamageType.Blunt });
    prince.Traits.Add(new MiniBoss5Trait());
    
    return prince;
  }
}

class PrinceOfRatsBehaviour : IBehaviour
{
  const int HUMAN_FORM = 0;
  const int RAT_FORM = 1;

  Dictionary<string, ulong> _lastUse = [];
  DateTime _lastQuip = DateTime.Now;

  public string GetBark(Mob actor, GameState gs) => "";
  
  static Action CalcMoveAction(Mob mob, GameState gs)
  {
    if (mob.HasTrait<ConfusedTrait>())
      return new MoveAction(gs, mob, Util.RandomAdjLoc(mob.Loc, gs));
    else
      return mob.MoveStrategy.MoveAction(mob, gs);
  }

  static void CheckChangeForm(Mob prince, GameState gs, int currForm)
  {
    if (gs.Rng.NextDouble() < 0.1)
    {
      if (prince.Stats[Attribute.MonsterForm].Curr == HUMAN_FORM)
      {
        prince.Glyph = new Glyph('r', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK);
        prince.Stats[Attribute.MonsterForm].SetMax(RAT_FORM);
      }
      else
      {
        prince.Glyph = new Glyph('@', Colours.GREY, Colours.DARK_GREY, Colours.BLACK, Colours.BLACK);
        prince.Stats[Attribute.MonsterForm].SetMax(HUMAN_FORM);
      }

      gs.UIRef().AlertPlayer("The Prince of Rats shifts forms!");
    }
  }

  public Action CalcAction(Mob actor, GameState gameState)
  {
    bool CanSummonRats(Mob prince, GameState gs, int dist, int form)
    {
      if (form != RAT_FORM)
        return false;

      if (_lastUse.TryGetValue("Summon rats", out var last) && gameState.Turn - last < 7)
        return false;
      
      if (dist > 5)
        return false;

      var path = Util.Bresenham(prince.Loc.Row, prince.Loc.Col, gs.Player.Loc.Row, gs.Player.Loc.Col);
      foreach (var sq in path)
      {
        if (gs.CurrentMap.TileAt(sq).Opaque())
          return false;
      }

      return true;
    }

    Action action;
    int currForm = actor.Stats[Attribute.MonsterForm].Curr;
    int distFromPlayer = Util.Distance(actor.Loc, gameState.Player.Loc);

    CheckChangeForm(actor, gameState, currForm);

    if (CanSummonRats(actor, gameState, distFromPlayer, currForm))
    {
      int ratCount = gameState.Rng.Next(3, 6);
      action = new SummonAction(gameState.Player.Loc, "giant rat", ratCount)
      {
        GameState = gameState,
        Actor = actor,
        Quip = "Destroy them, my rats!"
      };
      _lastUse["Summon rats"] = gameState.Turn;
    }
    else if (distFromPlayer == 1)
    {
      actor.Dmg = new Damage(5, 2, DamageType.Slashing);
      action = new MeleeAttackAction(gameState, actor, actor.PickTargetLoc(gameState));     
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

  public (Action, Inputer?) Chat(Mob actor, GameState gameState) => (new NullAction(), null);
}
