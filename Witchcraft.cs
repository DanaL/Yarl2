
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
      GameState!.UIRef().AlertPlayer("You don't have enough mana!");
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
    foreach (string s in t.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class CastSlumberingSong(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    if (!CheckCost(5, 15, result))
      return result;

    GameState!.UIRef().AlertPlayer("Ala-ca-zzzzzzzzz!");
    HashSet<Loc> flooded = Util.FloodFill(GameState!, Actor!.Loc, 3);
    HashSet<Loc> affected = [];
    foreach (Loc loc in flooded)
    {
      if (loc == Actor.Loc)
        continue;

      if (GameState!.ObjDb.Occupied(loc))
      {
        affected.Add(loc);
        SqAnimation anim = new(GameState, loc, Colours.WHITE, Colours.PURPLE, '*');
        GameState.UIRef().RegisterAnimation(anim);
      }      
    }

    int casterSpellDC = Actor.SpellDC;
    foreach (Loc loc in affected)
    {
      if (GameState!.ObjDb.Occupant(loc) is Actor actor)
      {
        if (actor.HasTrait<UndeadTrait>())
          continue;
        if (actor.HasTrait<BrainlessTrait>())
          continue;
        if (actor.HasTrait<PlantTrait>())
          continue;


        int roll = GameState.Rng.Next(20) + 1;
        if (roll < casterSpellDC)
        {
          GameState.UIRef().AlertPlayer($"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "fall")} asleep!");
          actor.Traits.Add(new SleepingTrait());
        } 
      }
    }
   
    return result;
  }

  public override void ReceiveUIResult(UIResult result) { }
}

class CastIllumeAction(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Complete = true;

    if (!CheckCost(2, 20, result))
      return result;

    LightSpellTrait ls = new();
    foreach (string s in ls.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class SpellcastMenu : Inputer
{
  readonly GameState GS;
  int row;
  bool targeting = false;
  
  public SpellcastMenu(GameState gs)
  {
    row = 0;
    int lastCast = gs.Player.SpellsKnown.IndexOf(gs.Player.LastSpellCast);
    if (lastCast > -1)
      row = lastCast;

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
      case "illume":
        inputer = new DummyInputer();
        GS.Player.ReplacePendingAction(new CastIllumeAction(GS, GS.Player), inputer);
        break;
      case "slumbering song":
        inputer = new DummyInputer();
        GS.Player.ReplacePendingAction(new CastSlumberingSong(GS, GS.Player), inputer);
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