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

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();

    if (!Tool.Equipped)
    {
      var (equipResult, _) = ((Player)Actor!).Inventory.ToggleEquipStatus(Tool.Slot);
      if (equipResult != EquipingResult.Equipped)
      {
        GameState!.UIRef().SetPopup(new Popup("You are unable to ready the pickaxe!", "", -1, -1));
        result.Complete = false;
        result.EnergyCost = 0.0;
        return result;
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
        var attackAction = new MeleeAttackAction(GameState, Actor, target.Loc);
        result.AltAction = attackAction;
        result.Complete = false;
        return result;
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
        var attackAction = new MeleeAttackAction(GameState, Actor, targetLoc);
        GameState!.UIRef().AlertPlayer($"When you have an axe, every {occ.Name} looks like a tree.");
        result.AltAction = attackAction;
        result.Complete = false;
        return result;
      }
      else
      {
        string msg = $"{occ.FullName.Capitalize()} is neither tree, nor rock.";
        GameState!.UIRef().AlertPlayer(msg);
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      }

      return result;
    }

    result.Complete = true;
    result.EnergyCost = 1.0;

    Tile tile = GameState!.TileAt(targetLoc);
    if (tile.IsTree())
    {
      ChopTree(targetLoc, tile, result);
    }
    else if (tile.Type == TileType.ClosedDoor || tile.Type == TileType.LockedDoor)
    {
      ChopDoor(targetLoc, result);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.DungeonFloor)
    {
      DigDungeonFloor(targetLoc, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.WoodBridge)
    {
      DigBridge(targetLoc, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.FrozenDeepWater)
    {
      DigFrozenWater(targetLoc, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Gravestone)
    {
      GraveRob(targetLoc, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Downstairs)
    {
      DigStairs(targetLoc, tile.Type, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Upstairs)
    {
      DigStairs(targetLoc, tile.Type, result, GameState, Actor);
    }
    else if (targetLoc == Actor.Loc && tile.Type == TileType.Pit)
    {
      DigInPit(targetLoc, result, GameState, Actor);
    }
    else if (GameState.ObjDb.ItemsAt(targetLoc).Any(i => i.HasTrait<BlockTrait>()))
    {
      DigBlock(targetLoc, result, GameState, Actor);
    }
    else
    {
      GameState!.UIRef().AlertPlayer("You swing your pickaxe through the air.");
      GameState.UIRef().SetPopup(new Popup("You swing your pickaxe through the air.", "", -1, -1));
    }

    return result;
  }

  static void DigBlock(Loc loc, ActionResult result, GameState gs, Actor digger)
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
      gs.ObjDb.RemoveItemFromGame(loc,blockage);
      string s = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, verb)} {blockage.Name.DefArticle()}.";
      gs.UIRef().AlertPlayer(s);
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
    else
    {
      string s = $"{digger.FullName.Capitalize()} chip away at the {blockage.Name.DefArticle()}.";
      gs.UIRef().AlertPlayer(s);
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
    }
  }

  static void DigInPit(Loc loc, ActionResult result, GameState gs, Actor digger)
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
      digger.Traits = digger.Traits.Where(t => t is not InPitTrait).ToList();

      result.AltAction = new MoveAction(gs, digger, loc);
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
  }

  static void GraveRob(Loc loc, ActionResult result, GameState gs, Actor digger)
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
        gs.AddPerformer(spook);
      }

      gs.UIRef().AlertPlayer("The grave's occuptant was still at home!");
      s += "\n\nYou feel unclean.";

      if (!digger.HasActiveTrait<ShunnedTrait>())
        digger.Traits.Add(new ShunnedTrait());
    }

    gs.UIRef().SetPopup(new Popup(s, "", -1, -1));
  }

  static void DigStairs(Loc loc, TileType tile, ActionResult result, GameState gs, Actor digger)
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

  static void DigFrozenWater(Loc loc, ActionResult result, GameState gs, Actor digger)
  {
    gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.DeepWater));
    string msg = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, "crack")} through the ice!";
    msg += gs.ResolveActorMove(digger, loc, loc);
    gs.UIRef().AlertPlayer(msg);
    if (digger == gs.Player) 
    {
      bool flying = digger.HasActiveTrait<FlyingTrait>() || digger.HasActiveTrait<FloatingTrait>();
      if (!flying)
        msg += "\n\nYou plunge into the icy water below!";
      gs.UIRef().SetPopup(new Popup(msg, "", -1, -1));
    }
  }

  static void DigBridge(Loc loc, ActionResult result, GameState gs, Actor digger)
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

  static void DigDungeonFloor(Loc loc, ActionResult result, GameState gs, Actor digger)
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
      
      string msg = gs.ResolveActorMove(digger, loc, loc);
      if (msg != "") 
      {
        gs.UIRef().AlertPlayer(msg);
        s += "\n\n" + msg;
      }
        
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

  void ChopDoor(Loc loc, ActionResult result)
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

  void ChopTree(Loc loc, Tile tile, ActionResult result)
  {
    GameState!.UIRef().SetPopup(new Popup("You chop down the tree...", "", -1, -1, 20));
    TileType t = GameState.Rng.NextDouble() < 0.5 ? TileType.Dirt : TileType.Grass;
    GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(t));

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
        GameState.AddPerformer(bees);
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

class PickLockAction(GameState gs, Actor actor) : Action(gs, actor)
{
  int Row;
  int Col;

  public override ActionResult Execute()
  {
    ActionResult result = base.Execute();
    
    Loc loc = Actor!.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };
    Tile tile = GameState!.TileAt(loc);
    UserInterface ui = GameState.UIRef();

    if (tile.Type == TileType.VaultDoor)
    {      
      ui.AlertPlayer("That door requires a special key.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (tile.Type == TileType.OpenDoor)
    {
      ui.AlertPlayer("That door is not closed.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (!(tile.Type == TileType.LockedDoor || tile.Type == TileType.ClosedDoor))
    {
      ui.AlertPlayer("You find no lock there.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else 
    {
      result.Complete = true;
      result.EnergyCost = 1.0;

      bool rogue = GameState.Player.Background == PlayerBackground.Skullduggery;
      int dc = 12 + GameState.CurrLevel + 1;
      if (rogue)
        dc -= 5;
      int roll = GameState.Rng.Next(1, 21);
      if (roll + Actor.Stats[Attribute.Dexterity].Curr > dc)
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
    }

    return result;
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
  
  ActionResult UseVaultKey(Item key)
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
        return new ActionResult() { Complete = true, EnergyCost = 0.0 };
      }

      GameState!.UIRef().AlertPlayer("The metal doors swing open.");
      var door = (VaultDoor) GameState!.CurrentMap.TileAt(doorLoc.Row, doorLoc.Col);
      door.Open = true;

      // Just remove the key from the game since it's now useless
      Actor.Inventory.RemoveByID(key.ID);
      GameState.ObjDb.RemoveItemFromGame(Actor.Loc, key);

      return new ActionResult() { Complete = true, EnergyCost = 1.0 };
    }

    throw new Exception("Attempted to use a vault key that isn't a vault key? This shouldn't happen!");
  }

  static bool IsUseableTool(Item item)
  {
    if (item.Type != ItemType.Tool)
      return false;
    if (item.Name == "lock pick" || item.Name == "pickaxe")
      return true;

    return false;
  }

  public override ActionResult Execute()
  {
    var (item, itemCount) = Actor!.Inventory.ItemAt(Choice);
    if (item is null)
      throw new Exception("Using item in inventory that doesn't exist :O This shouldn't happen :O");

    if (item.Type == ItemType.Bow)
    {
      GameState!.ClearMenu();
      ((Player)Actor).FireReadedBow(item, GameState);      
      return new ActionResult() { Complete = false, EnergyCost = 0.0 };
    }

    if (IsUseableTool(item))
    {
      GameState!.ClearMenu();
      
      if (item.Name == "pickaxe")
        ((Player)Actor).ReplacePendingAction(new DigAction(GameState, Actor, item), new DirectionalInputer(GameState, true));
      else
        ((Player)Actor).ReplacePendingAction(new PickLockAction(GameState, Actor), new DirectionalInputer(GameState));

      return new ActionResult() { Complete = false, EnergyCost = 0.0 };
    }

    bool torch = item.HasTrait<TorchTrait>();
    bool written = item.HasTrait<ScrollTrait>();
    bool vaultKey = item.HasTrait<VaultKeyTrait>();

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
          string txt = $"{Actor.FullName} {Grammar.Conjugate(Actor, "is")} too confused to read that!";
          GameState.UIRef().AlertPlayer(txt);
          return new ActionResult() { Complete = true, EnergyCost = 1.0 };
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

      var result = new ActionResult() { Complete = true, EnergyCost = 1.0 };
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
        var useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col, item);
        result.Complete = useResult.Successful;
        GameState.UIRef().AlertPlayer(useResult.Message);
        success = useResult.Successful;

        if (useResult.ReplacementAction is not null)
        {
          result.Complete = false;
          result.AltAction = useResult.ReplacementAction;
          result.EnergyCost = 0.0;
        }
      }

      foreach (SideEffectTrait sideEffect in item.Traits.OfType<SideEffectTrait>())
      {
        List<string> msgs = sideEffect.Apply(Actor, GameState);
        foreach (string s in msgs)
          GameState.UIRef().AlertPlayer(s);        
      }

      if (item.HasTrait<ConsumableTrait>())
        Actor.Inventory.ConsumeItem(item, Actor, GameState.Rng);

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

      return result;
    }
    else
    {
      GameState.UIRef().AlertPlayer("You don't know a way to use that!");
      return new ActionResult() 
      { 
        Complete = true, 
        EnergyCost = 0.0 };
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}
