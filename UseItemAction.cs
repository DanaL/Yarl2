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

    if (!Tool.Equiped)
    {
      var (equipResult, _) = ((Player)Actor!).Inventory.ToggleEquipStatus(Tool.Slot);
      if (equipResult != EquipingResult.Equiped)
      {
        GameState!.UIRef().SetPopup(new Popup("You are unable to ready the pickaxe!", "", -1, -1));
        result.Complete = false;
        result.EnergyCost = 0.0;
        return result;
      }
      else
      {
        result.Messages.Add($"You ready {Tool.Name.DefArticle()}.");
      }
    }

    // Assuming here if I implement NPC AI such that they might dig, the behaviour
    // code will ensure they don't try to dig an occupied tile, so I am assuming 
    // here the Actor is the player.
    Loc targetLoc = Actor!.Loc with { Row = Actor.Loc.Row + Row, Col = Actor.Loc.Col + Col };
    if (targetLoc != Actor.Loc && GameState!.ObjDb.Occupant(targetLoc) is Actor occ)
    {
      if (Battle.PlayerWillAttack(occ))
      {
        var attackAction = new MeleeAttackAction(GameState, Actor, targetLoc);
        result.Messages.Add($"When you have an axe, every {occ.Name} looks like a tree.");
        result.AltAction = attackAction;
        result.Complete = false;
        return result;
      }
      else
      {
        string msg = $"{occ.FullName.Capitalize()} is neither tree, nor rock.";
        result.Messages.Add(msg);
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      }

      return result;
    }

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
      GameState.CurrentMap.SetTile(targetLoc.Row, targetLoc.Col, TileFactory.Get(TileType.DeepWater));
      string msg = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "crack")} through the ice!";
      msg += GameState.ResolveActorMove(Actor, targetLoc, targetLoc);
      if (msg != "")
        result.Messages.Add(msg);
      if (Actor == GameState.Player) 
      {
        bool flying = Actor.HasActiveTrait<FlyingTrait>() || Actor.HasActiveTrait<FloatingTrait>();
        if (!flying)
          msg += "\n\nYou plunge into the icy water below!";
        GameState.UIRef().SetPopup(new Popup(msg, "", -1, -1));
      }
    }

    result.Complete = true;
    result.EnergyCost = 1.0;

    return result;
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

    result.Messages.Add(msg);
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
      result.Messages.Add(s);
      
      string msg = gs.ResolveActorMove(digger, loc, loc);
      if (msg != "") 
      {
        result.Messages.Add(msg);
        s += "\n\n" + msg;
      }
        
      if (digger == gs.Player)
        gs.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));

      return;
    }

    gs.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.Pit));
    s = $"{digger.FullName.Capitalize()} {Grammar.Conjugate(digger, "dig")} a pit.";
    result.Messages.Add(s);
    if (digger == gs.Player)
      gs.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));

    bool flying = digger.HasActiveTrait<FlyingTrait>() || digger.HasActiveTrait<FloatingTrait>();
    if (!flying)
      digger.Traits.Add(new InPitTrait());
  }

  void ChopDoor(Loc loc, ActionResult result)
  {
    int dc = 13 + GameState!.CurrLevel / 4;
    if (Actor is Player && GameState.Player.Lineage == PlayerLineage.Dwarf)
      dc -= 2;

    if (Actor!.AbilityCheck(Attribute.Strength, dc, GameState.Rng))
    {
      string s = $"{Actor.FullName.Capitalize()} {Grammar.Conjugate(Actor, "chop")} the door to pieces!";
      result.Messages.Add(s);
      if (Actor == GameState.Player)
        GameState.UIRef().SetPopup(new Popup(s, "", -1, -1, s.Length));
      GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.BrokenDoor));
    }
    else
    {
      result.Messages.Add("Splinters fly but the door remains intact.");
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
      result.Messages.Add("An apple tumbles to the ground.");
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
        result.Messages.Add("Uh-oh, you've angered a swarm of bees!");
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

    if (tile.Type == TileType.VaultDoor)
    {
      result.Messages.Add("That door requires a special key.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (tile.Type == TileType.OpenDoor)
    {
      result.Messages.Add("That door is not closed.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else if (!(tile.Type == TileType.LockedDoor || tile.Type == TileType.ClosedDoor))
    {
      result.Messages.Add("You find no lock there.");
      result.Complete = false;
      result.EnergyCost = 0.0;
    }
    else 
    {
      result.Complete = true;
      result.EnergyCost = 1.0;

      bool rogue = gs.Player.Background == PlayerBackground.Skullduggery;
      int dc = 12 + gs.CurrLevel + 1;
      if (rogue)
        dc -= 5;
      int roll = GameState.Rng.Next(1, 21);
      if (roll + Actor.Stats[Attribute.Dexterity].Curr > dc)
      {
        if (tile.Type == TileType.LockedDoor)
        {
          result.Messages.Add("The lock releases with a click.");
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.ClosedDoor));
        }
        else
        {
          result.Messages.Add("You lock the door.");
          GameState.CurrentMap.SetTile(loc.Row, loc.Col, TileFactory.Get(TileType.LockedDoor));
        }
      }
      else
      {
        result.Messages.Add("You fumble at the lock.");
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
        return new ActionResult() { Messages = [ "You see nowhere to use that key." ], Complete = true, EnergyCost = 0.0 };
      }

      string openMsg = "The metal doors swing open.";
      var door = (VaultDoor) GameState!.CurrentMap.TileAt(doorLoc.Row, doorLoc.Col);
      door.Open = true;

      // Just remove the key from the game since it's now useless
      Actor.Inventory.RemoveByID(key.ID);
      GameState.ObjDb.RemoveItemFromGame(Actor.Loc, key);

      return new ActionResult() { Messages = [ openMsg ], Complete = true, EnergyCost = 1.0 };
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

    bool consumable = item.HasTrait<ConsumableTrait>();
    bool torch = item.HasTrait<TorchTrait>();
    bool written = item.HasTrait<WrittenTrait>();
    bool vaultKey = item.HasTrait<VaultKeyTrait>();

    GameState!.ClearMenu();

    if (vaultKey)
      return UseVaultKey(item);

    var useableTraits = item.Traits.Where(t => t is IUSeable).ToList();
    if (useableTraits.Count != 0)
    {
      if (written)
      {
        // Eventually being blind will prevent you from reading things
        if (Actor.HasTrait<ConfusedTrait>())
        {
          string txt = $"{Actor.FullName} {Grammar.Conjugate(Actor, "is")} too confused to read that!";
          return new ActionResult() { Complete = true, Messages = [ txt ], EnergyCost = 1.0 };
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

        result.Messages.Add(s);
      }

      bool success = false;
      foreach (IUSeable trait in useableTraits)
      {
        var useResult = trait.Use(Actor, GameState, Actor.Loc.Row, Actor.Loc.Col, item);
        result.Complete = useResult.Successful;
        if (useResult.Message != "")
          result.Messages.Add(useResult.Message);
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
        result.Messages.AddRange(sideEffect.Apply(Actor, GameState));
      }

      return result;
    }
    else
    {
      return new ActionResult() 
      { 
        Complete = true, 
        Messages = [ "You don't know a way to use that!" ], 
        EnergyCost = 0.0 };
    }
  }

  public override void ReceiveUIResult(UIResult result) => Choice = ((MenuUIResult)result).Choice;
}
