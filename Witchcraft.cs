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
      case "cone of cold":
      case "gust of wind":
        return true;
      default:
        return false;
    }
  }
}

abstract class CastSpellAction(GameState gs, Actor actor) : TargetedAction(gs, actor)
{
  protected bool CheckCost(int mpCost, int stressCost)
  {
    Stat magicPoints = Actor!.Stats[Attribute.MagicPoints];
    if (magicPoints.Curr < mpCost)
    {
      GameState!.UIRef().AlertPlayer("You don't have enough mana!");
      return false;
    }

    magicPoints.Change(-mpCost);
    int stress = int.Max(0, stressCost - Actor!.Stats[Attribute.Will].Curr);
    Actor.Stats[Attribute.Nerve].Change(-stress);

    return true;
  }
}

class CastArcaneSpark(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    if (!CheckCost(1, 10))
      return 0.0;

    Item spark = new()
    {
      Name = "spark",
      Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.ICE_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, false)
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

        bool attackSuccessful = Battle.MagicAttack(Actor!, occ, GameState, spark, attackMod, new ArrowAnimation(GameState!, pts, Colours.ICE_BLUE));
        if (attackSuccessful)
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

    return 1.0;
  }
}

class CastSparkArc(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  HashSet<ulong> PreviousTargets { get; set; } = [];

  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(2, 10))
      return 0.0;

    PreviousTargets.Add(Actor!.ID);

    int attackMod = 2;
    if (Actor!.Stats.TryGetValue(Attribute.Will, out Stat? will))
      attackMod += will.Curr;
      
    Loc endPt = Arc(Actor.Loc, Target, attackMod, GameState!);
    
    Loc target2 = SelectNextTarget(endPt, GameState!);
    if (target2 == Loc.Nowhere)
      return 1.0;
    endPt = Arc(endPt, target2, attackMod, GameState!);

    Loc target3 = SelectNextTarget(endPt, GameState!);
    if (target3 == Loc.Nowhere)
      return 1.0;
    Arc(endPt, target3, attackMod, GameState!);

    return 1.0;
  }

  Loc Arc(Loc start, Loc target, int attackMod, GameState gs)
  {
    Loc endPt = Loc.Nowhere;
    List<Loc> trajectory = Util.Trajectory(start, target);
    List<Loc> pts = [];

    Item spark = new()
    {
      Name = "spark", Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.ICE_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, false)
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
        bool attackSuccessful = Battle.MagicAttack(Actor!, occ, gs, spark, attackMod, new ArrowAnimation(gs, pts, Colours.ICE_BLUE));        
        if (attackSuccessful)
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

class CastMageArmour(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(2, 25))
      return 0.0;

    MageArmourTrait t = new();
    foreach (string s in t.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class CastSlumberingSong(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(5, 15))
      return 0.0;

    GameState!.UIRef().AlertPlayer("Ala-ca-zzzzzzzzz!");
    HashSet<Loc> flooded = Util.FloodFill(GameState!, Actor!.Loc, 3, []);
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
   
    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) { }
}

class CastIllume(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(2, 20))
      return 0.0;

    LightSpellTrait ls = new();
    foreach (string s in ls.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class CastErsatzElevator(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  char Dir { get; set; } = '\0';

  public override double Execute()
  {
    base.Execute();
    
    GameState!.UIRef().SetInputController(new PlayerCommandController(GameState));

    if (!CheckCost(3, 25))
      return 0.0;

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

    return 1.0;
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
  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(0, 10))
      return 0.0;

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

    return 1.0;
  }
}

class CastPhaseDoor(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (CheckCost(1, 20))
    {
      GameState!.Player.QueueAction(new BlinkAction(GameState, Actor!));
    }

    return 0.0;
  }

  public override void ReceiveUIResult(UIResult result) { }
}

class CastConeOfCold(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  List<Loc> Affected { get; set; } = [];

  public override double Execute()
  {
    base.Execute();
    

    GameState!.UIRef().SetInputController(new PlayerCommandController(GameState));
    GameState.UIRef().ClosePopup();

    if (!CheckCost(1, 20))
      return 0.0;

    HashSet<Loc> animLocs = [..Affected.Where(l => GameState.LastPlayerFoV.Contains(l))];
    ExplosionAnimation blast = new(GameState!)
    {
      MainColour = Colours.ICE_BLUE,
      AltColour1 = Colours.LIGHT_BLUE,
      AltColour2 = Colours.BLUE,
      Highlight = Colours.WHITE,
      Centre = Actor!.Loc,
      Sqs = animLocs,
      Ch = '*'
    };
    blast.Sqs.Add(Actor.Loc);
    GameState!.UIRef().PlayAnimation(blast, GameState);

    GameState gs = GameState!;
    List<(int, DamageType)> dmg = [ (gs.Rng.Next(1, 7), DamageType.Cold), (gs.Rng.Next(1, 7), DamageType.Cold) ];
    foreach (Loc loc in Affected)
    {
      GameState.ApplyDamageEffectToLoc(loc, DamageType.Cold);
      if (GameState.ObjDb.Occupant(loc) is Actor victim)
      {
        string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} blasted by cold!";
        gs.UIRef().AlertPlayer(s, gs, loc);
        var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 3, GameState, null, 1.0);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "freezing", null);
        }        
      }
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) 
  {
    if (result is AffectedLocsUIResult affected)
    {
      Affected = [..affected.Affected.Where(l => l != Actor!.Loc)];
    }
  }
}

class CastGustOfWindAction(GameState gs, Actor actor, Item? item) : CastSpellAction(gs, actor)
{
  public bool FreeToCast { get; set; } = false;
  List<Loc> Affected { get; set; } = [];
  Item? Item { get; set; } = item;

  public override double Execute()
  {
    base.Execute();
    
    GameState!.UIRef().SetInputController(new PlayerCommandController(GameState));
    GameState.UIRef().ClosePopup();
    
    if (!FreeToCast && !CheckCost(1, 20))
      return 0.0;

    GameState!.UIRef().AlertPlayer("Whoooosh!!");

    HashSet<Loc> animLocs = [.. Affected.Where(l => GameState!.LastPlayerFoV.Contains(l))];
    ExplosionAnimation blast = new(GameState!)
    {
      MainColour = Colours.LIGHT_BLUE,
      AltColour1 = Colours.WHITE,
      AltColour2 = Colours.ICE_BLUE,
      Highlight = Colours.BLUE,
      Centre = Actor!.Loc,
      Sqs = animLocs,
      Ch = '≈'
    };
    blast.Sqs.Add(Actor.Loc);
    GameState!.UIRef().PlayAnimation(blast, GameState);

    List<GameObj> affectedObjs = [];
    foreach (Loc loc in Affected)
    {      
      if (GameState.TileAt(loc) is Door door && door.Type == TileType.ClosedDoor)
      {
        door.Open = true;
        GameState.UIRef().AlertPlayer("The door is blown open!", GameState, loc);
      }

      if (GameState.ObjDb.Occupant(loc) is Actor actor)
        affectedObjs.Add(actor);

      List<Item> items = GameState.ObjDb.ItemsAt(loc);
      items.AddRange(GameState.ObjDb.EnvironmentsAt(loc));
      foreach (Item item in items)
      {
        if (item.Name == "campfire")
        {
          GameState.UIRef().AlertPlayer("The campfire is extinguished!", GameState, loc);
          GameState.ObjDb.RemoveItemFromGame(loc, item);
          continue;
        }

        if (item.Type == ItemType.Fog)
        {
          GameState.UIRef().AlertPlayer("The fog is dispersed!", GameState, item.Loc);
          GameState.ObjDb.RemoveItemFromGame(item.Loc, item);
          continue;
        }

        bool moveable = true;
        foreach (Trait t in item.Traits)
        {
          if (t is AffixedTrait || t is BlockTrait || t is ImmobileTrait)
          {
            moveable = false;
            break;
          }
        }
        if (!moveable)
          continue;
        affectedObjs.Add(item);
      }
    }
   
    Loc origin = Actor!.Loc;
    affectedObjs.Sort((a, b) => 
      Util.Distance(a.Loc, origin).CompareTo(Util.Distance(b.Loc, origin)));

    // Pre-calculate the landing spots for everything affected by the gust
    List<(GameObj, Loc)> landingSpots = [];
    foreach (GameObj obj in affectedObjs) 
    {
      if (obj is Actor actor)
        BlowActorBack(actor, origin, GameState);
      else if (obj is Item item)
        BlowItemBack(item, origin, GameState);
    }

    if (Item is not null && Item.HasTrait<ConsumableTrait>())
    {
      Actor!.Inventory.ConsumeItem(Item, Actor, GameState);
    }

    return 1.0;
  }

  static void BlowItemBack(Item item, Loc origin, GameState gs)
  {
    int distance = item.Type switch
    {
      ItemType.Scroll => 5,
      ItemType.Document => 5,
      ItemType.Armour => 3,
      _ => 4
    };

    var (r, c) = Util.ExtendLine(origin.Row, origin.Col, item.Loc.Row, item.Loc.Col, distance);
    List<(int, int)> path = Util.Bresenham(item.Loc.Row, item.Loc.Col, r, c);

    (int, int) finalSq = path[0];
    Loc landingLoc = item.Loc;
    string msg = "";

    string itemName = item.FullName.DefArticle().Capitalize();    
    string verb = Grammar.Conjugate(item, "hit");
    if (item.Type == ItemType.Zorkmid)
    {
      itemName = itemName.Pluralize();
      verb = "hit";
    }
    
    bool broken = false;
    for (int j = 1; j < path.Count; j++)
    {
      Loc loc = origin with { Row = path[j].Item1, Col = path[j].Item2 };
      Tile tile = gs.TileAt(loc);
      if (!tile.PassableByFlight())
      {
        msg = $"{itemName} {verb} {Tile.TileDesc(tile.Type)}.";
        gs.UIRef().AlertPlayer(msg, gs, landingLoc);
        broken = CheckForItemDamage(item, landingLoc, gs);
        break;
      }
      else if (gs.ObjDb.BlockersAtLoc(loc))
      {
        Item blocker = gs.ObjDb.ItemsAt(loc).Where(i => i.HasTrait<BlockTrait>()).First();
        msg = $"{itemName} {verb} {blocker.Name.IndefArticle()}.";
        gs.UIRef().AlertPlayer(msg, gs, landingLoc);
        broken = CheckForItemDamage(item, landingLoc, gs);
        break;
      }
      else if (gs.ObjDb.Occupant(loc) is Actor occupant)
      {
        landingLoc = loc;
        string name = occupant.HasTrait<NamedTrait>() ? occupant.Name.Capitalize() : occupant.Name.IndefArticle();
        msg = $"{itemName} {verb} {occupant.FullName}.";
        gs.UIRef().AlertPlayer(msg, gs, landingLoc);
        InjuredByCollision(occupant, gs, landingLoc);
        broken = CheckForItemDamage(item, landingLoc, gs);
        break;
      }

      landingLoc = loc;
    }

    if (broken)
      return;

    if (item.Type == ItemType.Zorkmid && item.Value > 1)
    {
      ScatterCoins(item, landingLoc, gs);
    }
    else
    {
      gs.ObjDb.RemoveItemFromLoc(item.Loc, item);
      gs.ItemDropped(item, landingLoc);
    }
  }

  static void ScatterCoins(Item zorkmids, Loc landingLoc, GameState gs)
  {
    gs.ObjDb.RemoveItemFromGame(zorkmids.Loc, zorkmids);
    gs.UIRef().AlertPlayer("The coins scatter!");

    List<Loc> locs = [..Util.Adj8Locs(landingLoc).Where(l => gs.TileAt(l).PassableByFlight())];
    locs.Add(landingLoc);

    int total = zorkmids.Value;
    while (total > 0)
    {
      Item z = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
      int amt = int.Min(total, gs.Rng.Next(1, 5));
      z.Value = amt;

      Loc loc = locs[gs.Rng.Next(locs.Count)];
      gs.ItemDropped(z, loc);

      total -= amt;
    }
  }

  static void BlowActorBack(Actor actor, Loc origin, GameState gs)
  {
    int distance;
    if (actor.HasTrait<FlyingTrait>())
      distance = 5;
    else if (actor.HasTrait<HeavyTrait>())
      distance = 1;
    else
      distance = 3;

    var (r, c) = Util.ExtendLine(origin.Row, origin.Col, actor.Loc.Row, actor.Loc.Col, distance);
    List<(int, int)> path = Util.Bresenham(actor.Loc.Row, actor.Loc.Col, r, c);

    (int, int) finalSq = path[0];
    Loc landingLoc = actor.Loc;
    string msg = "";
    for (int j = 1; j < path.Count; j++)
    {
      Loc loc = origin with { Row = path[j].Item1, Col = path[j].Item2 };
      Tile tile = gs.TileAt(loc);
      if (!tile.PassableByFlight())
      {
        msg = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "collide")} with {Tile.TileDesc(tile.Type)}.";
        if (InjuredByCollision(actor, gs, landingLoc))
          return;

        break;
      }
      else if (gs.ObjDb.BlockersAtLoc(loc))
      {
        Item blocker = gs.ObjDb.ItemsAt(loc).Where(i => i.HasTrait<BlockTrait>()).First();
        msg = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "collide")} with {blocker.Name.IndefArticle()}.";
        if (InjuredByCollision(actor, gs, landingLoc))
          return;

        break;
      }
      else if (gs.ObjDb.Occupant(loc) is Actor occupant)
      {
        landingLoc = loc;
        string name = occupant.HasTrait<NamedTrait>() ? occupant.Name.Capitalize() : occupant.Name.IndefArticle();
        msg = $"{actor.FullName.Capitalize()} {Grammar.Conjugate(actor, "collide")} with {name}.";
        break;
      }

      landingLoc = loc;
    }

    gs.UIRef().AlertPlayer(msg, gs, landingLoc);
    gs.ResolveActorMove(actor,actor.Loc, landingLoc);
  }

  static bool CheckForItemDamage(Item item, Loc loc, GameState gs)
  {
    if (item.Type == ItemType.Potion)
    {
      gs.ObjDb.RemoveItemFromGame(item.Loc, item);
      gs.UIRef().AlertPlayer($"{item.FullName.DefArticle().Capitalize()} shatters!", gs, loc);

      return true;
    }
    else if (Item.IDInfo.TryGetValue(item.Name, out var idInfo) && idInfo.Desc.Contains("glass"))
    {
      gs.ObjDb.RemoveItemFromGame(item.Loc, item);
      gs.UIRef().AlertPlayer($"{item.FullName.DefArticle().Capitalize()} shatters!", gs, loc);

      return true;
    }

    return false;
  }

  static bool InjuredByCollision(Actor actor, GameState gs, Loc landingLoc)
  {
    int d = gs.Rng.Next(1, 7);
    var (hpLeft, _, _) = actor.ReceiveDmg([(d, DamageType.Blunt)], 0, gs, null, 1.0);
    if (hpLeft < 1)
    {
      gs.ObjDb.ActorMoved(actor, actor.Loc, landingLoc);
      gs.ActorKilled(actor, "a collision", null);
      return true;
    }

    return false;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    if (result is AffectedLocsUIResult affected)
    {
      Affected = [.. affected.Affected.Where(l => l != Actor!.Loc)];
    }
  }
}

class SpellcastMenu : Inputer
{  
  int row;
  bool SpellSelection { get; set; } = true;
  string PopupText { get; set; } = "";
  int PopupRow { get; set; } = -1;
  List<string> SpellList { get; set; } = [];

  public SpellcastMenu(GameState gs) : base(gs)
  {       
    SetSpellMenu();

    row = 0;
    int lastCast = SpellList.FindIndex(s => s.Equals(gs.Player.LastSpellCast, StringComparison.OrdinalIgnoreCase));
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
      SpellList = [..GS.Player.SpellsKnown
                        .Select(s => s.CapitalizeWords())];
    }
    else
    {
      SpellList = [..GS.Player.SpellsKnown
                        .Where(s => Spells.NoFocus(s))
                        .Select(s => s.CapitalizeWords())];
    }
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      GS.UIRef().ClosePopup();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
      return;
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
      return;
    }

    WritePopup();
  }

  void HandleSelectedSpell(string spell)
  {
    Inputer inputer;
    switch (spell)
    {
      case "arcane spark":        
        inputer = new Aimer(GS, GS.Player.Loc, 7)
        {
          DeferredAction = new CastArcaneSpark(GS, GS.Player)
        };
        SpellSelection = false;
        PopupText = "Select target";
        PopupRow = -3;
        GS.UIRef().SetInputController(inputer);
        break;
      case "mage armour":
        GS.Player.QueueAction(new CastMageArmour(GS, GS.Player));
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.UIRef().ClosePopup();
        break;
      case "illume":
        GS.Player.QueueAction(new CastIllume(GS, GS.Player));
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.UIRef().ClosePopup();
        break;
      case "slumbering song":
        GS.Player.QueueAction(new CastSlumberingSong(GS, GS.Player));
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.UIRef().ClosePopup();
        break;
      case "spark arc":
        inputer = new Aimer(GS, GS.Player.Loc, 7)
        {
          DeferredAction = new CastArcaneSpark(GS, GS.Player)
        };        
        GS.UIRef().SetInputController(inputer);
        SpellSelection = false;
        PopupText = "Select target";
        PopupRow = -3;
        break;
      case "ersatz elevator":
        inputer = new CharSetInputer(GS, ['<', '>'])
        {
          DeferredAction = new CastErsatzElevator(GS, GS.Player)
        };
        GS.UIRef().SetInputController(inputer);
        SpellSelection = false;
        PopupText = "Which direction? [LIGHTBLUE <] for up, [LIGHTBLUE >] for down";
        GS.UIRef().SetPopup(new Popup(PopupText, "", -1, -1));
        break;
      case "frogify":
        inputer = new Aimer(GS, GS.Player.Loc, 5)
        {
          DeferredAction = new CastFrogify(GS, GS.Player)
        };
        GS.UIRef().SetInputController(inputer);
        SpellSelection = false;
        PopupText = "Select target:";
        PopupRow = -3;
        break;
      case "phase door":
        GS.Player.QueueAction(new CastPhaseDoor(GS, GS.Player));
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.UIRef().ClosePopup();
        break;
      case "cone of cold":
        inputer = new ConeTargeter(GS, 5, GS.Player.Loc)
        {
          DeferredAction = new CastConeOfCold(GS, GS.Player)
        };
        SpellSelection = false;
        GS.UIRef().SetInputController(inputer);
        PopupRow = -3;
        PopupText = "Which direction?";
        break;
      case "gust of wind":
        inputer = new ConeTargeter(GS, 5, GS.Player.Loc)
        {
          DeferredAction = new CastGustOfWindAction(GS, GS.Player, null)
        };
        GS.UIRef().SetInputController(inputer);
        SpellSelection = false;
        PopupRow = -3;
        PopupText = "Which direction?";
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