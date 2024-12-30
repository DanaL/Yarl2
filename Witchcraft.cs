
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

abstract class CastSpellAction(GameState gs, Actor actor) : TargetedAction(gs, actor)
{
  protected bool CheckCost(int mpCost, int stressCost, ActionResult result)
  {
    Stat magicPoints = Actor!.Stats[Attribute.MagicPoints];
    if (magicPoints.Curr < mpCost)
    {
      result.EnergyCost = 0.0;
      result.Complete = false;
      result.Messages.Add("You don't have enough mana!");
      return false;
    }

    magicPoints.Change(-mpCost);
    int stress = int.Max(0, stressCost - Actor!.Stats[Attribute.Will].Curr);
    Actor.Stats[Attribute.Nerve].Change(-stress);

    return true;
  }
}

class CastArcaneSparkAction(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    if (!CheckCost(1, 10, result))
      return result;

    Stat magicPoints = Actor!.Stats[Attribute.MagicPoints];
    if (magicPoints.Curr == 0)
    {
      result.EnergyCost = 0.0;
      result.Complete = false;
      result.Messages.Add("You don't have enough mana!");
      return result;
    }
    magicPoints.Change(-1);
    int stress = int.Max(0, 10 - Actor!.Stats[Attribute.Will].Curr);
    Actor.Stats[Attribute.Nerve].Change(-stress);

    Item spark = new()
    {
      Name = "spark",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.ICE_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, Colours.BLACK)
    };
    spark.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Electricity });
    GameState!.ObjDb.Add(spark);

    int attackMod = 2;
    if (Actor!.Stats.TryGetValue(Attribute.Will, out Stat? will))
      attackMod += will.Curr;
    
    // Hmmm variants of this code are in a bunch of acitons like Magic Missile, shooting bows,
    // etc etc. I wonder if I can push more of this up into TargetedAction?
    List<Loc> pts = [];
    
    // I think I can probably clean this crap up
    foreach (var pt in Trajectory(false))
    {
      var tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);

        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, spark, attackMod, new ArrowAnimation(GameState!, pts, Colours.ICE_BLUE));
        result.Messages.AddRange(attackResult.Messages);
        if (attackResult.Complete)
        {
          pts = [];
          break;
        }
      }
      else if (tile.Passable() || tile.PassableByFlight())
      {
        pts.Add(pt);
      }
      else
      {
        break;
      }
    }

    var anim = new ArrowAnimation(GameState!, pts, Colours.ICE_BLUE);
    GameState!.UIRef().PlayAnimation(anim, GameState);

    return result;
  }
}

class CastMageArmourAction(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    if (!CheckCost(2, 25, result))
      return result;

    MageArmourTrait t = new();
    result.Messages.AddRange(t.Apply(Actor!, GameState!));

    return result;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class SpellcastMenu : Inputer
{
  readonly GameState GS;
  int row = 0;
  bool targeting = false;

  public SpellcastMenu(GameState gs)
  {
    GS = gs;
    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if (ch == 'j')
    {
      row = (row + 1 ) % GS.Player.SpellsKnown.Count;
    }
    else if (ch == 'k')
    {
      --row;
      if (row < 0)
        row = GS.Player.SpellsKnown.Count - 1;
    }
    else if (ch == '\n' || ch == '\r')
    {
      string spell = GS.Player.SpellsKnown[row];
      GS.Player.LastSpellCast = spell;
      HandleSelectedSpell(spell);
      Done = true;
      Success = false;
    }

    WritePopup();
  }

  void HandleSelectedSpell(string spell)
  {
    Inputer inputer;

    switch (spell)
    {
      case "arcane spark":        
        inputer = new Aimer(GS, GS.Player.Loc, 7);
        GS.Player.ReplacePendingAction(new CastArcaneSparkAction(GS, GS.Player), inputer);
        targeting = true;
        break;
      case "mage armour":
        inputer = new DummyInputer();
        GS.Player.ReplacePendingAction(new CastMageArmourAction(GS, GS.Player), inputer);
        break;
    }
  }

  void WritePopup()
  {
    if (!targeting)
    {
      List<string> spells = GS.Player.SpellsKnown
                            .Select(s => s.CapitalizeWords()).ToList();
      GS.UIRef().SetPopup(new PopupMenu("Cast which spell?", spells) { SelectedRow = row });
    }
    else
    {
      GS.UIRef().SetPopup(new Popup("Select target", "", -3, -1));
    }
  }
}