// Delve - A roguelike computer RPG
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
  public static Dictionary<string, Dictionary<Component, int>> Components = new()
  {
    ["arcane spark"]      = new() { [Component.BlackPearl] = 1, [Component.SulphurousAsh] = 1 },
    ["spark arc"]         = new() { [Component.BlackPearl] = 1, [Component.SulphurousAsh] = 2 },
    ["illume"]            = new() { [Component.SulphurousAsh] = 1  },
    ["mage armour"]       = new() { [Component.SpiderSilk] = 2  },
    ["phase door" ]       = new() { [Component.SpiderSilk] = 1, [Component.BloodMoss] = 1, [Component.BlackPearl] = 1},
    ["ersatz elevator" ]  = new() { [Component.SpiderSilk] = 1, [Component.BloodMoss] = 1, [Component.SulphurousAsh] = 1 }
  };

//, , , , , , Nightshade, MandrakeRoot
  public static ItemNames ComponentName(Component component) => component switch
  {
    Component.BlackPearl => ItemNames.BLACK_PEARL,
    Component.BloodMoss => ItemNames.BLOOD_MOSS,
    Component.Ginseng => ItemNames.GINSENG,
    Component.Garlic => ItemNames.GARLIC,
    Component.SulphurousAsh => ItemNames.SULPHUROUS_ASH,
    Component.SpiderSilk => ItemNames.SPIDER_SILK,
    Component.Nightshade => ItemNames.NIGHTSHADE,
    Component.MandrakeRoot => ItemNames.MANDRAKE_ROOT,
    _ => throw new Exception("Unknown spell component")
  };

  public static Component StrToComponent(string n) => n switch
  {
    "black pearl" => Component.BlackPearl,
    "blood moss" => Component.BloodMoss,
    "ginseng" => Component.Ginseng,
    "garlic" => Component.Garlic,
    "sulphurous ash" => Component.SulphurousAsh,
    "spider silk" => Component.SpiderSilk,
    "nightshade" => Component.Nightshade,
    "mandrake root" => Component.MandrakeRoot,
    _ => throw new Exception($"Unknown component name: {n}")
  };
}

abstract class CastSpellAction(GameState gs, Actor actor) : TargetedAction(gs, actor)
{
  protected bool CheckCost(int mpCost, string spellName)
  {
    Stat magicPoints = Actor!.Stats[Attribute.MagicPoints];
    if (magicPoints.Curr < mpCost)
    {
      GameState.UIRef().AlertPlayer("You don't have enough mana!");
      return false;
    }

    if (Spells.Components.TryGetValue(spellName, out var components))
    {
      var ownedComponents = Actor!.Inventory.Components();
      foreach (var component in components.Keys)
      {
        int required = components[component];
        if (ownedComponents[component] < required)
        {
          GameState.UIRef().AlertPlayer("You are missing some spell components!");
          return false;
        }
      }

      foreach (var component in components.Keys)
      {
        Actor!.Inventory.UseComponent(component, components[component]);
      }
    }

    magicPoints.Change(-mpCost);

    return true;
  }
}

class CastArcaneSpark(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    if (!CheckCost(1, "arcane spark"))
      return 0.0;

    Item spark = new()
    {
      Name = "spark", Type = ItemType.Weapon,
      Glyph = new Glyph('*', Colours.ICE_BLUE, Colours.LIGHT_BLUE, Colours.BLACK, false)
    };
    spark.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 3, DamageType = DamageType.Electricity });
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
    
    if (!CheckCost(2, "spark arc"))
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
    spark.Traits.Add(new DamageTrait() { DamageDie = 6, NumOfDie = 3, DamageType = DamageType.Electricity });
    
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
    Dictionary<Loc, int> visible = FieldOfView.CalcVisible(7, currTarget, gs.CurrentMap, gs.ObjDb);    
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
    
    if (!CheckCost(2, "mage armour"))
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
    
    if (!CheckCost(5, "slumbering song"))
      return 0.0;

    GameState.UIRef().AlertPlayer("Ala-ca-zzzzzzzzz!");
    Map map = GameState.MapForActor(Actor!);
    HashSet<Loc> flooded = Util.FloodFill(GameState.ObjDb, map, Actor!.Loc, 3, []);
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
      if (GameState.Rng.Next(20) + 1 < casterSpellDC && GameState!.ObjDb.Occupant(loc) is Actor actor)
        Effects.ApplySleep(actor, GameState);
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
    
    if (!CheckCost(1, "illume"))
      return 0.0;

    LightSpellTrait ls = new();
    foreach (string s in ls.Apply(Actor!, GameState!))
      GameState!.UIRef().AlertPlayer(s);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) {}
}

class CastPhaseDoor(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public Loc Loc { get; set; }

  public override double Execute()
  {
    base.Execute();
    
    GameState.UIRef().SetInputController(new PlayerCommandController(GameState));

    if (!CheckCost(1, "phase door"))
      return 0.0;
    
    if (GameState.MapForActor(Actor!).HasFeature(MapFeatures.NoTeleport))
    {
      string s = Actor is Player ? "You shudder for a moment as the magic fizzles." : "A spell fizzles.";
      GameState.UIRef().AlertPlayer(s, GameState, Actor!.Loc);
      return 1.0;  
    }

    (int, int) delta = (Loc.Row - Actor!.Loc.Row, Loc.Col - Actor.Loc.Col );    
    List<Loc> locs = [];
    for (int d = 1; d <= 5; d++)
    {
      Loc loc = Actor.Loc with { Row = Actor.Loc.Row + d * delta.Item1, Col = Actor.Loc.Col + d * delta.Item2 };
      if (!GameState.TileAt(loc).PassableByFlight() || GameState.ObjDb.AreBlockersAtLoc(loc))
        break;
      locs.Add(loc);
    }

    locs.Reverse();
    foreach (Loc landingSpot in locs)
    {
      if (!GameState.ObjDb.Occupied(landingSpot))
      {
        Actor.ClearAnchors(GameState!);
      
        GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, landingSpot, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));
        GameState.UIRef().RegisterAnimation(new SqAnimation(GameState, Actor.Loc, Colours.WHITE, Colours.LIGHT_PURPLE, '*'));

      base.Execute();
      Actor.QueueAction(new MoveAction(GameState, Actor, landingSpot, false));
      if (GameState.LastPlayerFoV.ContainsKey(Actor.Loc)) 
      {
        GameState.UIRef().AlertPlayer($"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "jump")}!");
      }

      return 0.0;
      }      
    }
    
    GameState.UIRef().AlertPlayer("A spell fizzles.", GameState, Actor!.Loc);

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result)
  {
    var dirResult = (DirectionUIResult)result;
    Loc = Actor!.Loc with { Row = Actor.Loc.Row + dirResult.Row, Col = Actor.Loc.Col + dirResult.Col };
  }
}

class CastErsatzElevator(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  char Dir { get; set; } = '\0';

  public override double Execute()
  {
    base.Execute();

    GameState.UIRef().SetInputController(new PlayerCommandController(GameState));

    if (!CheckCost(3, "ersatz elevator"))
      return 0.0;

    bool desc = GameState.CurrentDungeon.Descending;

    if (GameState.InWilderness || AtEndOfDungeon())
    {
      GameState.UIRef().AlertPlayer("Your spell fizzles!");
      GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));

      return 1.0;
    }

    // Exiting the dungeon will bring you to the portal entrance in the wilderness
    if (desc && Dir == '<' && GameState.Player.Loc.Level == 0)
    {
      FactDb factDb = GameState.Campaign.FactDb!;
      if (factDb.FactCheck("Dungeon Entrance") is LocationFact entrance)
      {
        DoElevate(entrance.Loc, "You rise up through the ceiling!", GameState);
      }

      return 1.0;
    }
    else if (!desc && Dir == '>' && GameState.Player.Loc.Level == 0)
    {
      FactDb factDb = GameState.Campaign.FactDb!;
      if (factDb.FactCheck("Dungeon Entrance") is LocationFact entrance)
      {
        DoElevate(entrance.Loc, "You sink through the floor!", GameState);
      }

      return 1.0;
    }
    
    Loc dest = PickDestLoc(GameState, desc);
    if (dest == Loc.Nowhere)
    {
      GameState.UIRef().AlertPlayer("Your spell fizzles!");
      GameState.UIRef().SetPopup(new Popup("Your spell fizzles!", "", -1, -1));
    }
    else
    {
      string s;
      if ((desc && Dir == '>') || (!desc && Dir == '<'))
        s = "You sink through the floor!";
      else
        s = "You rise up through the ceiling!";
      DoElevate(dest, s, GameState);
    }

    return 1.0;

    bool AtEndOfDungeon()
    {
      int maxDungeonLevel = GameState!.CurrentDungeon.LevelMaps.Count - 1;

      // Regular dungeon
      if (GameState.CurrentDungeon.Descending && Dir == '>' && GameState.Player.Loc.Level == maxDungeonLevel)
        return true;

      // Tower-style ascending dungeon
      if (!GameState.CurrentDungeon.Descending && Dir == '<' && GameState.Player.Loc.Level == maxDungeonLevel)
        return true;

      return false;
    }
  }

  Loc PickDestLoc(GameState gs, bool desc)
  {
    int delta;
    if (desc && Dir == '>' || (!desc && Dir == '<'))
      delta = 1;
    else
      delta = -1;
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

  static void DoElevate(Loc dest, string msg, GameState gs)
  {
    gs.UIRef().AlertPlayer(msg);
    Loc start = gs.Player.Loc;
    gs.ActorEntersLevel(gs.Player, dest.DungeonID, dest.Level);
    gs.ResolveActorMove(gs.Player, start, dest);
    gs.FlushPerformers();
    gs.PrepareFieldOfView();    
  }

  public override void ReceiveUIResult(UIResult result) => Dir = ((CharUIResult) result).Ch;
}

class CastFrogify(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (!CheckCost(0, "frogify"))
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
        
        if (GameState.LastPlayerFoV.ContainsKey(frog.Loc))
        {
          string s = $"{victim.FullName.Capitalize()} turns into {frog.Name.IndefArticle()}.";
          GameState.UIRef().AlertPlayer(s);
          GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
        }
      }
      else if (GameState.LastPlayerFoV.ContainsKey(victim.Loc))
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

class CastSummonDecoy(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();

    if (CheckCost(3, "summon decoy"))
    {
      GameState.Player.QueueAction(new PassAction(GameState, Actor!));
    }

    return 0.0;
  }
}

class CastMirrorImage(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  public override double Execute()
  {
    base.Execute();
    
    if (CheckCost(2, "mirror image"))
      GameState.Player.QueueAction(new SummonDecoyAction(GameState, Actor!));
    
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

    if (!CheckCost(1, "cone of cold"))
      return 0.0;

    HashSet<Loc> animLocs = [..Affected.Where(l => GameState.LastPlayerFoV.ContainsKey(l))];
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
      Effects.ApplyDamageEffectToLoc(loc, DamageType.Cold, gs);
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
    
    if (!FreeToCast && !CheckCost(1, "gust of wind"))
      return 0.0;

    GameState!.UIRef().AlertPlayer("Whoooosh!!");

    HashSet<Loc> animLocs = [.. Affected.Where(l => GameState!.LastPlayerFoV.ContainsKey(l))];
    ExplosionAnimation blast = new(GameState!)
    {
      MainColour = Colours.LIGHT_BLUE,
      AltColour1 = Colours.WHITE,
      AltColour2 = Colours.ICE_BLUE,
      Highlight = Colours.BLUE,
      Centre = Actor!.Loc,
      Sqs = animLocs,
      Ch = Constants.WIND_CHAR
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
      ItemType.SpellBook => 5,
      ItemType.Document => 5,
      ItemType.Armour => 3,
      _ => 4
    };

    var (r, c) = Util.ExtendLine(origin.Row, origin.Col, item.Loc.Row, item.Loc.Col, distance);
    List<(int, int)> path = Util.LerpLine(item.Loc.Row, item.Loc.Col, r, c);

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
      else if (gs.ObjDb.AreBlockersAtLoc(loc))
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
    List<(int, int)> path = Util.LerpLine(actor.Loc.Row, actor.Loc.Col, r, c);

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
      else if (gs.ObjDb.AreBlockersAtLoc(loc))
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

class CastFireBreath(GameState gs, Actor actor) : CastSpellAction(gs, actor)
{
  List<Loc> Affected { get; set; } = [];

  public override double Execute()
  {
    base.Execute();

    GameState!.UIRef().SetInputController(new PlayerCommandController(GameState));
    GameState.UIRef().ClosePopup();

    if (!CheckCost(3, "fire breath"))
      return 0.0;

    HashSet<Loc> animLocs = [.. Affected.Where(l => GameState.LastPlayerFoV.ContainsKey(l))];
    ExplosionAnimation blast = new(GameState!)
    {
      MainColour = Colours.BRIGHT_RED,
      AltColour1 = Colours.YELLOW,
      AltColour2 = Colours.YELLOW_ORANGE,
      Highlight = Colours.WHITE,
      Centre = Actor!.Loc,
      Sqs = animLocs,
      Ch = '*'
    };
    blast.Sqs.Add(Actor.Loc);
    GameState!.UIRef().PlayAnimation(blast, GameState);

    GameState gs = GameState!;
    List<(int, DamageType)> dmg = [(gs.Rng.Next(1, 9), DamageType.Fire), (gs.Rng.Next(1, 9), DamageType.Fire)];
    foreach (Loc loc in Affected)
    {
      Effects.ApplyDamageEffectToLoc(loc, DamageType.Fire, gs);
      if (GameState.ObjDb.Occupant(loc) is Actor victim)
      {
        string s = $"{victim.FullName.Capitalize()} {Grammar.Conjugate(victim, "is")} caught in the flames!";
        gs.UIRef().AlertPlayer(s, gs, loc);
        var (hpLeft, _, _) = victim.ReceiveDmg(dmg, 3, GameState, null, 1.0);
        if (hpLeft < 1)
        {
          GameState.ActorKilled(victim, "fire", null);
        }
      }
    }

    return 1.0;
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

  void SetSpellMenu() => SpellList = [..GS.Player.SpellsKnown.Select(s => s.CapitalizeWords())];

  public override void Input(char ch)
  {
    KeyCmd cmd = GS.KeyMap.ToCmd(ch);

    if (ch == Constants.ESC)
    {
      GS.UIRef().ClosePopup();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
      return;
    }
    else if (cmd == KeyCmd.MoveS)
    {
      row = (row + 1 ) % SpellList.Count;
    }
    else if (cmd == KeyCmd.MoveN)
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
        DirectionalInputer dir = new(GS, false) { DeferredAction = new CastPhaseDoor(GS, GS.Player) };
        GS.UIRef().SetInputController(dir);
        SpellSelection = false;
        break;
      case "mirror image":
        GS.Player.QueueAction(new CastMirrorImage(GS, GS.Player));
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.UIRef().ClosePopup();
        break;
      case "cone of cold":
        inputer = new ConeTargeter(GS, 5, GS.Player.Loc, [])
        {
          DeferredAction = new CastConeOfCold(GS, GS.Player)
        };
        SpellSelection = false;
        GS.UIRef().SetInputController(inputer);
        PopupRow = -3;
        PopupText = "Which direction?";
        break;
      case "gust of wind":
        inputer = new ConeTargeter(GS, 5, GS.Player.Loc, [])
        {
          DeferredAction = new CastGustOfWindAction(GS, GS.Player, null)
        };
        GS.UIRef().SetInputController(inputer);
        SpellSelection = false;
        PopupRow = -3;
        PopupText = "Which direction?";
        break;
      case "breathe fire":
        inputer = new ConeTargeter(GS, 5, GS.Player.Loc, [ DamageType.Fire ])
        {
          DeferredAction = new CastFireBreath(GS, GS.Player),
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
      int width = 0;

      List<string> components = [];
      var neededComponents = Spells.Components[SpellList[row].ToLower()];
      foreach (var kvp in GS.Player.Inventory.Components())
      {
        if (neededComponents.ContainsKey(kvp.Key))
          components.Add( $"[green {kvp.Key} {kvp.Value}]");
        else
          components.Add( $"{kvp.Key} {kvp.Value}");
      }
      
      GS.UIRef().SetPopup(
        new PopupMenu("Cast which spell?", SpellList, components,  "") 
        { 
          SelectedRow = row, 
          Width = width }
      );
    }
    else
    {
      GS.UIRef().SetPopup(new Popup(PopupText, "", PopupRow, -1));
    }
  }
}