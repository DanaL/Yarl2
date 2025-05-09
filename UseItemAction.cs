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

class DigAction(GameState gs, Actor actor, Item tool) : Action(gs, actor)
{
  Item Tool { get; set; } = tool;
  int Row;
  int Col;

  public override double Execute()
  {
    if (!Tool.Equipped)
    {
      var (equipResult, _) = ((Player)Actor!).Inventory.ToggleEquipStatus(Tool.Slot);
      if (equipResult != EquipingResult.Equipped)
      {
        GameState!.UIRef().SetPopup(new Popup("You are unable to ready the pickaxe!", "", -1, -1));
        return 0.0;
      }
      else
      {
        GameState!.UIRef().AlertPlayer($"You ready {Tool.Name.DefArticle()}.");
      }
    }

    // if the actor is currently swallowed, trying to dig should attack
    // the monster instead
    if (Actor!.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swallowed)
    {
      if (GameState!.ObjDb.GetObj(swallowed.SwallowerID) is Actor target)
      {
        MeleeAttackAction attackAction = new(GameState, Actor, target.Loc);
        Actor.QueueAction(attackAction);
        return 0.0;
      }
    }

    // Assuming here if I implement NPC AI such that they might dig, the behaviour
    // code will ensure they don't try to dig an occupied tile, so I am assuming 
    // here the Actor is the player.
    Loc targetLoc = Actor.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };
    if (targetLoc != Actor.Loc && GameState!.ObjDb.Occupant(targetLoc) is Actor occ)
    {
      if (Battle.PlayerWillAttack(occ))
      {
        GameState!.UIRef().AlertPlayer($"When you have an axe, every {occ.Name} looks like a tree.");
        Actor.QueueAction(new MeleeAttackAction(GameState, Actor, targetLoc));
      }
      else
      {
        string msg = $"{occ.FullName.Capitalize()} is neither tree, nor rock.";
        GameState!.UIRef().AlertPlayer(msg);
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      }

      return 0.0;
    }

    double energyCost = 1.0;
    Tile tile = GameState!.TileAt(targetLoc);
    if (tile.IsTree())
    {
      ChopTree(targetLoc, tile);
    }
    else if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor)
    {
      ChopDoor(targetLoc);
    }
    else if (tile.Type == TileType.DungeonWall)
    {
      DigDungeonWall(targetLoc);
    }
    else if (tile.Type == TileType.PermWall)
    {
      GameState!.UIRef().AlertPlayer("Your pickaxe bounces off the wall without leaving the merest scratch.");
      GameState.UIRef().SetPopup(new Popup("Your pickaxe bounces off the wall without leaving the merest scratch.", "", -1, -1));
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.DungeonFloor)
    {
      DigDungeonFloor(targetLoc, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.WoodBridge)
    {
      DigBridge(targetLoc, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.FrozenDeepWater)
    {
      DigFrozenWater(targetLoc, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Gravestone)
    {
      GraveRob(targetLoc, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Downstairs)
    {
      DigStairs(targetLoc, tile.Type, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Upstairs)
    {
      DigStairs(targetLoc, tile.Type, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Pit)
    {
      DigInPit(targetLoc, GameState, Actor);
      energyCost = 0.0;
    }
    else if (GameState.ObjDb.ItemsAt(targetLoc).Any(i => i.HasTrait<BlockTrait>()))
    {
      DigBlock(targetLoc, GameState, Actor);
    }    
    else
    {
      GameState!.UIRef().AlertPlayer("You swing your pickaxe through the air.");
      GameState.UIRef().SetPopup(new Popup("You swing your pickaxe through the air.", "", -1, -1));
    }

    return energyCost;
  }

  static void DigBlock(Loc loc, GameState gs, Actor digger)
  {
    int dc = 13 + gs.CurrLevel / 4;
    if (digger is Player && gs.Player.Lineage == PlayerLineage.Dwarf)
      dc -= 2;

    // For now we clear the blockage. For something like a boulder it might
    // break up into rocks. But I'll only do that if rocks have some use in
    // the game.
    Item blockage = gs.ObjDb.ItemsAt(loc).Where(i => i.HasTrait<BlockTrait>())
                                         .First();
    string verb = blockage.Type == ItemType.Statue ? "destroy" : "clear";
    if (digger.AbilityCheck(Attribute.Strength, dc, gs.Rng))
    {
      gs.ItemDestroyed(blockage, loc);
      string s = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, verb)} {blockage.Name.DefArticle()}.";
      gs.UIRef().AlertPlayer(s);
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
    else
    {
      string s = $"{digger.FullName.Capitalize()} chop away at the {blockage.Name.DefArticle()}.";
      gs.UIRef().AlertPlayer(s);
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
  }

  static void DigInPit(Loc loc, GameState gs, Actor digger)
  {
    if (loc.Level == gs.CurrentDungeon.LevelMaps.Count - 1) {      
      gs.UIRef().AlertPlayer("The floor is too hard to dig here.");
      gs.UIRef().SetPopup(new Popup("The floor is too hard to dig here.", "", -1, -1));
    }
    else
    {
      gs.UIRef().AlertPlayer("You break through the floor!");
      gs.UIRef().SetPopup(new Popup("You break through the floor!", "", -1, -1));
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.TrapDoor));

      // Need to do this so that when we are resovling the move, the actor 
      // isn't still technically stuck in the pit and unable to leave the square
      digger.Traits = [..digger.Traits.Where(t => t is not InPitTrait)];

      digger.QueueAction(new MoveAction(gs, digger, loc));
    }
  }

  static void GraveRob(Loc loc, GameState gs, Actor digger)
  {
    gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DisturbedGrave));

    string s = "You desecrate the grave.";
    gs.UIRef().AlertPlayer(s);
    
    if (gs.Rng.NextDouble() < 0.20)
    {
      foreach (Item item in Treasure.GraveContents(gs, gs.CurrLevel, gs.Rng))
      {
        gs.ItemDropped(item, loc);
      }

      List<Loc> locOpts = Util.Adj8Locs(loc)
                              .Where(l => gs.TileAt(l).Passable() && !gs.ObjDb.Occupied(l))
                              .ToList();
      if (locOpts.Count > 0)
      {
        Loc spookLoc = locOpts[gs.Rng.Next(locOpts.Count)];
        Actor spook = gs.Rng.Next(3) switch 
        {
        0 => MonsterFactory.Get("skeleton", gs.ObjDb, gs.Rng),
          1 => MonsterFactory.Get("zombie", gs.ObjDb, gs.Rng),
          _ => MonsterFactory.Get("ghoul", gs.ObjDb, gs.Rng),
        };
        gs.ObjDb.AddNewActor(spook, spookLoc);
      }

      gs.UIRef().AlertPlayer("The grave's occuptant was still at home!");
      s += "\n\nYou feel unclean.";

      if (!digger.HasActiveTrait<ShunnedTrait>())
        digger.Traits.Add(new ShunnedTrait());
    }

    gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
  }

  static void DigStairs(Loc loc, TileType tile, GameState gs, Actor digger)
  {
    string s = "You destroy the stairs! This probably won't be a problem...";

    // Again, assuming digger is the player
    if (tile == TileType.Downstairs)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Chasm));
      // also destroy the upstairs on the level below
      var nextStairsLoc = loc with { Level = loc.Level + 1 };
      var nextLvl = loc.Level + 1;
      // add rubble to level below if/when I implement rubble
      gs.CurrentDungeon.LevelMaps[nextLvl].SetTile(nextStairsLoc.Row, nextStairsLoc.Col, TileFactory.Get(TileType.DungeonFloor));
      gs.ChasmCreated(loc);

      s += "\n\nYou plummet into the hole you create.";
    }
    else if (tile == TileType.Upstairs)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
      if (loc.Level > 0)
      {
        var stairsAboveLoc = loc with { Level = loc.Level - 1 };
        var lvlAbove = stairsAboveLoc.Level;
        gs.CurrentDungeon.LevelMaps[lvlAbove].SetTile(stairsAboveLoc.Row, stairsAboveLoc.Col, TileFactory.Get(TileType.Chasm));
        gs.ChasmCreated(stairsAboveLoc);
      }
      else
      {
        s += "\n\nYou're really committing to this adventure!";
      }
    }

    gs.UIRef().AlertPlayer("You destroy some stairs.");
    gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
  }

  static void DigFrozenWater(Loc loc, GameState gs, Actor digger)
  {
    gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
    gs.ResolveActorMove(digger, loc, loc);
    gs.UIRef().AlertPlayer($"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, "crack")} through the ice!");
    if (digger == gs.Player) 
    {
      bool flying = digger.HasActiveTrait<FlyingTrait>() || digger.HasActiveTrait<FloatingTrait>();
      if (!flying)
        gs.UIRef().SetPopup(new Popup("You plunge into the icy water below!", "", -1, -1));
    }
  }

  static void DigBridge(Loc loc, GameState gs, Actor digger)
  {
    // digging a bridge tile destroys it and any adjacent bridge tiles
    List<Loc> toDestroy = [ loc ];
    foreach (var adj in Util.Adj4Locs(loc))
    {
      if (gs.TileAt(adj).Type == TileType.WoodBridge)
        toDestroy.Add(adj);
    }

    string msg = "The bridge collapses!";
    if (toDestroy.Count > 1)
      msg += " Neighbouring bridge segments are washed away!";

    gs.UIRef().AlertPlayer(msg);
    if (digger == gs.Player)
      gs.UIRef().SetPopup(new Popup(msg, "", -1, -1));

    foreach (Loc bridge in toDestroy)
      gs.BridgeDestroyed(bridge);
  }

  static void DigDungeonFloor(Loc loc, GameState gs, Actor digger)
  {
    string s;

    // If any adjacent tiles are deep water, the tile being dug on is flooded
    bool flooded = false;
    foreach (var adj in Util.Adj4Locs(loc))
    {
      if (gs.TileAt(adj).Type == TileType.DeepWater) 
      {
        flooded = true;
        break;
      }
    }
    if (flooded)
    {
      gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
      s = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, "dig")} but water rushes in!";
      gs.UIRef().AlertPlayer(s);
      
      gs.ResolveActorMove(digger, loc, loc);
        
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));

      return;
    }

    gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pit));
    s = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, "dig")} a pit.";
    gs.UIRef().AlertPlayer(s);

    if (digger == gs.Player)
      gs.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));

    bool flying = digger.HasActiveTrait<FlyingTrait>() || digger.HasActiveTrait<FloatingTrait>();
    if (!flying)
      digger.Traits.Add(new InPitTrait());
  }

  void DigDungeonWall(Loc loc)
  {
    int dc = 16 + GameState!.CurrLevel / 4;
    if (Actor is Player && GameState.Player.Lineage == PlayerLineage.Dwarf)
      dc -= 2;

    if (Actor!.AbilityCheck(Attribute.Strength, dc, GameState.Rng))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "tunnel")} into the wall!";
      GameState.UIRef().AlertPlayer(s);
      if (Actor == GameState.Player)
        GameState.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));
      GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DungeonFloor));
    }
    else
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "chip")} away at the wall!";
      GameState.UIRef().AlertPlayer(s);
      GameState.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
  }

  void ChopDoor(Loc loc)
  {
    int dc = 11 + GameState!.CurrLevel / 4;
    if (Actor is Player && GameState.Player.Lineage == PlayerLineage.Dwarf)
      dc -= 2;

    if (Actor!.AbilityCheck(Attribute.Strength, dc, GameState.Rng))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "chop")} the door to pieces!";      
      GameState.UIRef().AlertPlayer(s);
      if (Actor == GameState.Player)
        GameState.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));
      GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.BrokenDoor));
    }
    else
    {
      GameState.UIRef().AlertPlayer("Splinters fly but the door remains intact.");
      GameState.UIRef().SetPopup(new Popup("Splinters fly but the door remains intact.", "", -1, -1));
    }
  }

  void ChopTree(Loc loc, Tile tile)
  {
    GameState!.UIRef().SetPopup(new Popup("You chop down the tree...", "", -1, -1, 20));
    TileType t = GameState.Rng.NextDouble() < 0.5 ? TileType.Dirt : TileType.Grass;
    GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(t));

    if (GameState.Rng.NextDouble() < 0.2)
    {
      Item staff = ItemFactory.Get(ItemNames.QUARTERSTAFF, GameState.ObjDb);
      GameState.ItemDropped(staff, loc);
    }
    
    if (tile.Type != TileType.Conifer && GameState.Rng.NextDouble() < 0.1)
    {
      Item apple = ItemFactory.Get(ItemNames.APPLE, GameState.ObjDb);
      GameState.ItemDropped(apple, loc);
      GameState.UIRef().AlertPlayer("An apple tumbles to the ground.");
    }

    if (GameState.Rng.NextDouble() < 0.5)
    {
      Loc swarmLoc = loc == Actor!.Loc ? PickAdjLoc(loc, GameState) : loc;

      if (swarmLoc != Loc.Nowhere)
      {
        Actor bees = MonsterFactory.Get("swarm of bees", GameState.ObjDb, GameState.Rng);
        GameState.ObjDb.AddNewActor(bees, swarmLoc);
        if (Actor == GameState.Player)
          GameState!.UIRef().SetPopup(new Popup("Uh-oh, you've angered a swarm of bees!", "", -1, -1, 20));
        GameState.UIRef().AlertPlayer("Uh-oh, you've angered a swarm of bees!");
      }
    }
  }

  static Loc PickAdjLoc(Loc loc, GameState gs)
  {
    var opts = Util.Adj8Locs(loc)
                   .Where(l => gs.TileAt(l).PassableByFlight() && !gs.ObjDb.Occupied(l));
    
    if (opts.Any())
      return opts.ElementAt(gs.Rng.Next(opts.Count()));

    return Loc.Nowhere;
  }

  public override void ReceiveUIResult(UIResult result) 
  {
    var dir = (DirectionUIResult)result;
    Row = dir.Row;
    Col = dir.Col;
  }
}

class CleansingAction(GameState gs, Actor actor, Item source) : Action(gs, actor)
{
  int Row;
  int Col;
  Item Source { get; set; } = source;

  public override double Execute()
  {
    Loc loc = Actor!.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };

    EffectApplier.CleanseLoc(GameState!, loc, Source);

    if (Source.HasTrait<ConsumableTrait>())
    {
      Actor!.Inventory.RemoveByID(Source.ID);
      GameState!.ObjDb.RemoveItemFromGame(loc, Source);
    }

    return 1.0;
  }

  public override void ReceiveUIResult(UIResult result) 
  {
    var dir = (DirectionUIResult)result;
    Row = dir.Row;
    Col = dir.Col;
  }
}

class PickLockAction(GameState gs, Actor actor, Item tool) : Action(gs, actor)
{
  int Row;
  int Col;
  Item Tool { get; set; } = tool;

  public override double Execute()
  {
    Loc loc = Actor!.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };
    Tile tile = GameState!.TileAt(loc);
    UserInterface ui = GameState.UIRef();

    double energyCost = 1.0;
    if (tile.Type == TileType.VaultDoor)
    {      
      ui.AlertPlayer("That door requires a special key.");
      energyCost = 0.0;
    }
    else if (tile.Type == TileType.OpenDoor)
    {
      ui.AlertPlayer("That door is not closed.");
      energyCost = 0.0;
    }
    else if (!(tile.Type == TileType.LockedDoor || tile.Type == TileType.ClosedDoor))
    {
      ui.AlertPlayer("You find no lock there.");
      energyCost = 0.0;
    }
    else 
    {
      bool rogue = GameState.Player.Background == PlayerBackground.Skullduggery;
      int dc = 12 + GameState.CurrLevel + 1;

      if (rogue)
        dc -= 5;

      foreach (Trait t in Tool.Traits)
      {
        if (t is DoorKeyTrait dkt)
          dc += dkt.DCMod;
      }

      if (Actor.AbilityCheck(Attribute.Dexterity, dc, GameState.Rng))
      {
        if (tile.Type == TileType.LockedDoor)
        {
          ui.AlertPlayer("The lock releases with a click.");
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.ClosedDoor));
        }
        else
        {
          ui.AlertPlayer("You lock the door.");
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.LockedDoor));
        }
      }
      else
      {
        ui.AlertPlayer("You fumble at the lock.");
      }

      if (Tool.HasTrait<FragileTrait>() && GameState.Rng.Next(5) == 0)
      {        
        Actor.Inventory.ConsumeItem(Tool, Actor, GameState);
        GameState.UIRef().AlertPlayer($"{Tool.Name.DefArticle().Capitalize()} breaks!");
      }
    }

    return energyCost;
  }

  public override void ReceiveUIResult(UIResult result) 
  {
    var dir = (DirectionUIResult)result;
    Row = dir.Row;
    Col = dir.Col;
  }
}

class UseItemAction(GameState gs, Actor actor) : Action(gs, actor)
{
  public char Choice { get; set; }
  
  double UseVaultKey(Item key)
  {
    VaultKeyTrait? keyTrait = key.Traits.OfType<VaultKeyTrait>()
                                        .FirstOrDefault();
    if (keyTrait is not null)
    {
      Loc doorLoc = keyTrait.VaultLoc;
      bool adj = false;
      foreach (var loc in Util.Adj4Locs(Actor!.Loc))
      {
        if (loc == doorLoc) 
        {
          adj = true;
          break;
        }
      }

      if (!adj)
      {
        GameState!.UIRef().AlertPlayer("You see nowhere to use that key.");
        return 0.0;
      }

      GameState!.UIRef().AlertPlayer("The metal doors swing open.");
      var door = (VaultDoor) GameState!.CurrentMap.TileAt(doorLoc.Row, doorLoc.Col);
      door.Open = true;

      // Just remove the key from the game since it's now useless
      Actor.Inventory.RemoveByID(key.ID);
      GameState.ObjDb.RemoveItemFromGame(Actor.Loc, key);

      return 1.0;
    }

    throw new Exception("Attempted to use a vault key that isn't a vault key? This shouldn't happen!");
  }

  public override double Execute()
  {
    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      throw new Exception("Using item in inventory that doesn't exist :O This shouldn't happen :O");

    if (item.Type == ItemType.Bow)
    {
      GameState!.ClearMenu();
      PlayerCommandController.FireReadedBow(item, GameState);
      
      return 0.0;
    }

    if (item.IsUseableTool())
    {
      GameState!.ClearMenu();
      
      DirectionalInputer dir = new(GameState, true);
      if (item.HasTrait<DiggingToolTrait>())
        dir.DeferredAction = new DigAction(GameState, Actor, item);
      else if (item.HasTrait<CleansingTrait>())
        dir.DeferredAction = new CleansingAction(GameState, Actor, item);
      else
        dir.DeferredAction = new PickLockAction(GameState, Actor, item);

      GameState.UIRef().SetInputController(dir);
      
      return 0.0;
    }

    bool torch = false, written = false, vaultKey = false;
    foreach (Trait t in item.Traits)
    {
      if (t is TorchTrait)
        torch = true;
      if (t is ScrollTrait)
        written = true;
      if (t is VaultKeyTrait)
        vaultKey = true;
    }

    GameState!.ClearMenu();

    if (vaultKey)
      return UseVaultKey(item);

    var useableTraits = item.Traits.Where(t => t is IUSeable).ToList();
    if (useableTraits.Count != 0 || item.HasTrait<CanApplyTrait>())
    {
      if (written)
      {
        // Eventually being blind will prevent you from reading things
        if (Actor.HasTrait<ConfusedTrait>())
        {
          string txt = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "is")} too confused to read that!";
          GameState.UIRef().AlertPlayer(txt);
          if (Actor is Player)
            GameState.UIRef().SetPopup(new Popup(txt, "", -1, -1));
          return 1.0;
        }
      }

      // When a torch has been lit, we want to remove it from the stack of 
      // other torches
      if (torch)
      {
        Actor.Inventory.RemoveByID(item.ID);        
        item.Traits = item.Traits.Where(t => t is not StackableTrait).ToList();
        Actor.Inventory.Add(item, Actor.ID);
      }

      double energyCost = 1.0;
      if (item.HasTrait<EdibleTrait>())
      {
        string s;
        if (item.Name == "apple" && Actor is Player)
          s = "Delicious!";
        else
          s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "eat")} {item.FullName.DefArticle()}.";

        if (Actor == GameState.Player && item.Name == "ogre liver")
        {
          GameState.UIRef().SetPopup(new Popup("Gross!!\n\nYou feel [BRIGHTRED strong]!", "", -1, -1, 20));
        }

        GameState.UIRef().AlertPlayer(s);
      }

      bool success = false;
      foreach (IUSeable trait in useableTraits)
      {
        UseResult useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col, item);
        GameState.UIRef().AlertPlayer(useResult.Message);
        success = useResult.Successful;

        if (useResult.ReplacementAction is not null)
        {
          energyCost = 0.0;
          Actor.QueueAction(useResult.ReplacementAction);
        }

        // This prevents a consumable item from getting used up in the event
        // a targeting menu or such is bailed out of.
        if (useResult.ReplacementAction is UseSpellItemAction)
        {
          return 0.0;
        }
      }

      foreach (SideEffectTrait sideEffect in item.Traits.OfType<SideEffectTrait>())
      {
        List<string> msgs = sideEffect.Apply(Actor, GameState);
        foreach (string s in msgs)
          GameState.UIRef().AlertPlayer(s);        
      }

      if (item.HasTrait<ConsumableTrait>()) 
        Actor.Inventory.ConsumeItem(item, Actor, GameState);

      if (Actor is Player)
      {
        Animation anim;
        Loc animLoc = Actor.Loc with { Row = Actor.Loc.Row - 1 };
        switch (item.Type)
        {
          case ItemType.Potion:
            anim = new SqAnimation(GameState, animLoc, Colours.WHITE, Colours.FAINT_PINK, '!');
            GameState.UIRef().RegisterAnimation(anim);
            break;
          case ItemType.Scroll:
            anim = new SqAnimation(GameState, animLoc, Colours.WHITE, Colours.FAINT_PINK, '?');
            GameState.UIRef().RegisterAnimation(anim);
            break;
        }
      }

      return energyCost;
    }
    else
    {
      GameState.UIRef().AlertPlayer("You don't know a way to use that!");
      return 0.0;      
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}
