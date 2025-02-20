
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

class Spells
{
  public static bool NoFocus(string spell)
  {
    switch (spell.ToLower())
    {
      case "phase door":
        return true;
      default:
        return false;
    }
  }
}

abstract class CastSpellAction(GameState gs, Actor actor) : TargetedAction(gs, actor)
{
  protected bool CheckCost(int mpCost, int stressCost, ActionResult result)
  {
    Stat magicPoints = Actor!.Stats[Attribute.MagicPoints];
    if (magicPoints.Curr < mpCost)
    {
      result.EnergyCost = 0.0;
      result.Succcessful = false;
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
    result.Succcessful = true;

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
    foreach (var pt in Trajectory(Actor.Loc, false))
    {
      var tile = GameState!.TileAt(pt);
      if (GameState.ObjDb.Occupant(pt) is Actor occ && occ != Actor)
      {
        pts.Add(pt);

        var attackResult = Battle.MagicAttack(Actor!, occ, GameState, spark, attackMod, new ArrowAnimation(GameState!, pts, Colours.ICE_BLUE));
        if (attackResult.Succcessful)
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

    GameState!.ObjDb.RemoveItemFromGame(spark.Loc, spark);

    return result;
  }
}

class CastSparkArc(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  HashSet<ulong> PreviousTargets { get; set; } = [];

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    if (!CheckCost(2, 10, result))
      return result;

    PreviousTargets.Add(Actor!.ID);

    int attackMod = 2;
    if (Actor!.Stats.TryGetValue(Attribute.Will, out Stat? will))
      attackMod += will.Curr;
      
    Loc endPt = Arc(Actor.Loc, Target, attackMod, GameState!);
    
    Loc target2 = SelectNextTarget(endPt, GameState!);
    if (target2 == Loc.Nowhere)
      return result;
    endPt = Arc(endPt, target2, attackMod, GameState!);

    Loc target3 = SelectNextTarget(endPt, GameState!);
    if (target3 == Loc.Nowhere)
      return result;
    Arc(endPt, target3, attackMod, GameState!);

    return result;
  }

  Loc Arc(Loc start, Loc target, int attackMod, GameState gs)
  {
    Loc endPt = Loc.Nowhere;
    List<Loc> trajectory = Util.Trajectory(start, target);
    List<Loc> pts = [];

    Item spark = new()
    {
      Name = "spark", Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.ICE_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, Colours.BLACK)
    };
    spark.Traits.Add(new DamageTrait() { DamageDie = 8, NumOfDie = 1, DamageType = DamageType.Electricity });
    
    foreach (Loc pt in trajectory)
    {
      Tile tile = gs.TileAt(pt);
      if (!tile.PassableByFlight())
      {
        break;
      }

      pts.Add(pt);

      if (gs.ObjDb.Occupant(pt) is Actor occ && !PreviousTargets.Contains(occ.ID))
      {
        ActionResult attackResult = Battle.MagicAttack(Actor!, occ, gs, spark, attackMod, new ArrowAnimation(gs, pts, Colours.ICE_BLUE));        
        if (attackResult.Succcessful)
        {
          PreviousTargets.Add(occ.ID);
          return pt;
        }
      }
    }

    if (pts.Count > 0)
    {
      ArrowAnimation anim = new(gs, pts, Colours.ICE_BLUE);
      gs.UIRef().PlayAnimation(anim, gs);
    }

    return endPt;
  }

  Loc SelectNextTarget(Loc currTarget, GameState gs)
  {
    if (currTarget == Loc.Nowhere)
      return Loc.Nowhere;

    List<Loc> targets = [];
    Dictionary<Loc, Illumination> visible = FieldOfView.CalcVisible(7, currTarget, gs.CurrentMap, gs.ObjDb);    
    foreach (Loc loc in visible.Keys)
    {
      if (visible[loc] != Illumination.Full)
        continue;
      if (gs.ObjDb.Occupant(loc) is Actor actor && !PreviousTargets.Contains(actor.ID))
      {
        targets.Add(loc);
      }
    }

    if (targets.Count == 0)
      return Loc.Nowhere;

    return targets[gs.Rng.Next(targets.Count)];
  }
}

class CastMageArmourAction(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

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
    result.Succcessful = true;

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
    result.Succcessful = true;

    if (!CheckCost(2, 20, result))
      return result;

    LightSpellTrait ls = new();
    foreach (string s in ls.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class CastErsatzElevator(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  char Dir { get; set; } = '\0';

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;
    
    if (!CheckCost(3, 25, result))
      return result;

    GameState gs = GameState!;
    int maxDungeonLevel = gs.CurrentDungeon.LevelMaps.Count - 1;

    if (GameState!.InWilderness || (gs.Player.Loc.Level == maxDungeonLevel && Dir == '>'))
    {
      gs.UIRef().AlertPlayer("Your spell fizzles!");
      GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));
    }    
    else if (gs.Player.Loc.Level == 0 && Dir == '<')
    {
      // Exiting the dungeon will bring you to the portal entrance in the wilderness
      FactDb factDb = gs.Campaign.FactDb!;
      if (factDb.FactCheck("Dungeon Entrance") is LocationFact entrance)
      {
        DoElevate(entrance.Loc, "You rise up through the ceiling!", gs);
      }
    }
    else
    {
      Loc dest = PickDestLoc(gs);
      if (dest == Loc.Nowhere)
      {
        gs.UIRef().AlertPlayer("Your spell fizzles!");
        GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));
      }
      else
      {
        string s = Dir == '>' ? "You sink through the floor!" : "You rise up through the ceiling!";
        DoElevate(dest, s, gs);
      }
    }

    return result;
  }

  Loc PickDestLoc(GameState gs)
  {
    int delta = Dir == '>' ? 1 : -1;
    Loc dest = gs.Player.Loc with { Level = gs.Player.Loc.Level + delta };
    
    if (gs.LocOpen(dest) || gs.TileAt(dest).Type == TileType.Chasm)
      return dest;

    // if the loc directly above (or below) the player is blocked or impassable
    // we'll jsut pick a random, unoccupied spot
    List<Loc> opts = [];
    Map map = gs.Campaign.Dungeons[dest.DungeonID].LevelMaps[dest.Level];
    for (int r = 0; r < map.Height; r++)
    {
      for (int c = 0; c < map.Width; c++)
      {        
        Loc loc = new(dest.DungeonID, dest.Level, r, c);
        if (gs.LocOpen(loc))
          opts.Add(loc);
      }
    }

    if (opts.Count > 0)
      return opts[gs.Rng.Next(opts.Count)];
    
    return Loc.Nowhere;
  }

  void DoElevate(Loc dest, string msg, GameState gs)
  {
    gs.UIRef().AlertPlayer(msg);
    Loc start = gs.Player.Loc;
    gs.ActorEntersLevel(gs.Player, dest.DungeonID, dest.Level);
    gs.ResolveActorMove(gs.Player, start, dest);
    gs.RefreshPerformers();
    gs.PrepareFieldOfView();    
  }

  public override void ReceiveUIResult(UIResult result) => Dir = ((CharUIResult) result).Ch;
}

class CastFrogify(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    if (!CheckCost(0, 10, result))
      return result;

    // I don't yet want to deal with the player being polymorphed...
    if (Target == Actor!.Loc)
    {
      GameState!.UIRef().AlertPlayer("Your spell fizzles!");
      GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));
    }
   
    if (GameState!.ObjDb.Occupant(Target) is Actor victim)
    {
      if (victim.Name != "giant toad")
      {
        SqAnimation anim = new(GameState, Target, Colours.WHITE, Colours.DARK_GREEN, 't');
        GameState.UIRef().PlayAnimation(anim, GameState);

        PolymorphedTrait pt = new();
        string form = "frog";
        if (victim.HasTrait<UndeadTrait>())
          form = "zombie frog";
        Actor frog = pt.Morph(victim, GameState, form);
        
        if (GameState.LastPlayerFoV.Contains(frog.Loc))
        {
          string s = $"{victim.FullName.Capitalize()} turns into {frog.Name.IndefArticle()}.";
          GameState.UIRef().AlertPlayer(s);
          GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
        }
      }
      else if (GameState.LastPlayerFoV.Contains(victim.Loc))
      {
        GameState.UIRef().AlertPlayer("The spell fizzles as a giant toad is more or less already a frog.");
        GameState.UIRef().SetPopup(new Popup("The spell fizzles as a giant toad is more or less already a frog.", "", -1, -1));
      }
    }
    else
    {
      GameState.UIRef().AlertPlayer("Your spell fizzles!");
      GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));
    }

    return result;
  }
}

class CastPhaseDoor(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    result.EnergyCost = 1.0;
    result.Succcessful = true;

    if (!CheckCost(1, 20, result))
      return result;

    result.AltAction = new BlinkAction(gs, actor);

    return result;
  }

  public override void ReceiveUIResult(UIResult result) { }
}

class SpellcastMenu : Inputer
{  
  readonly GameState GS;
  int row;
  bool SpellSelection { get; set; } = true;
  string PopupText { get; set; } = "";
  int PopupRow { get; set; } = -1;
  List<string> SpellList { get; set; } = [];

  public SpellcastMenu(GameState gs)
  {   
    GS = gs;

    SetSpellMenu();

    row = 0;
    int lastCast = SpellList.IndexOf(gs.Player.LastSpellCast);
    if (lastCast > -1)
      row = lastCast;

    WritePopup();
  }

  void SetSpellMenu()
  {
    bool focusEquiped;
    if (GS.Player.Inventory.FocusEquipped())
      focusEquiped = true;
    else if (GS.Player.Inventory.ReadiedWeapon() is Item rw && rw.Name == "quarterstaff")
      focusEquiped = true;
    else
      focusEquiped = false;

    if (focusEquiped)
    {
      SpellList = GS.Player.SpellsKnown
                        .Select(s => s.CapitalizeWords())
                        .ToList();
    }
    else
    {
      SpellList = GS.Player.SpellsKnown
                        .Where(s => Spells.NoFocus(s))
                        .Select(s => s.CapitalizeWords())
                        .ToList();
    }
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
      row = (row + 1 ) % SpellList.Count;
    }
    else if (ch == 'k')
    {
      --row;
      if (row < 0)
        row = SpellList.Count - 1;
    }
    else if (ch == '\n' || ch == '\r')
    {
      string spell = SpellList[row].ToLower();
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
        SpellSelection = false;
        PopupText = "Select target";
        PopupRow = -3;
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
      case "spark arc":
        inputer = new Aimer(GS, GS.Player.Loc, 7);
        GS.Player.ReplacePendingAction(new CastSparkArc(GS, GS.Player), inputer);
        SpellSelection = false;
        PopupText = "Select target";
        PopupRow = -3;
        break;
      case "ersatz elevator":
        inputer = new CharSetInputer(['<', '>']);
        GS.Player.ReplacePendingAction(new CastErsatzElevator(GS, GS.Player), inputer);
        SpellSelection = false;
        PopupText = "Which direction? [LIGHTBLUE <] for up, [LIGHTBLUE >] for down";        
        break;
      case "frogify":
        inputer = new Aimer(GS, GS.Player.Loc, 5);
        GS.Player.ReplacePendingAction(new CastFrogify(GS, GS.Player), inputer);
        SpellSelection = false;
        PopupText = "Select target:";
        PopupRow = -3;
        break;
      case "phase door":
        inputer = new DummyInputer();
        GS.Player.ReplacePendingAction(new CastPhaseDoor(GS, GS.Player), inputer);
        break;
    }
  }

  void WritePopup()
  {    
    if (SpellSelection)
    {
      GS.UIRef().SetPopup(new PopupMenu("Cast which spell?", SpellList) { SelectedRow = row });
    }
    else
    {
      GS.UIRef().SetPopup(new Popup(PopupText, "", PopupRow, -1));
    }
  }
}