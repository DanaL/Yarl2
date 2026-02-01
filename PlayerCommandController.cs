
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

class PlayerCommandController(GameState gs) : Inputer(gs)
{
  static Loc SingleAdjTile(GameState gs, Loc centre, TileType seeking)
  {
    var adj = Util.Adj8Locs(centre)
                  .Where(loc => gs.TileAt(loc).Type == seeking);
    return adj.Count() == 1 ? adj.First() : Loc.Nowhere;
  }

  static KeyCmd CalcStagger(GameState gs, KeyCmd cmd)
  {
    double roll = gs.Rng.NextDouble();
    return cmd switch
    {
      KeyCmd.MoveN => roll < 0.5 ? KeyCmd.MoveNW : KeyCmd.MoveNE,
      KeyCmd.MoveNE => roll < 0.5 ? KeyCmd.MoveN : KeyCmd.MoveE,
      KeyCmd.MoveE => roll < 0.5 ? KeyCmd.MoveNE : KeyCmd.MoveSE,
      KeyCmd.MoveSE => roll < 0.5 ? KeyCmd.MoveE : KeyCmd.MoveS,
      KeyCmd.MoveS => roll < 0.5 ? KeyCmd.MoveSE : KeyCmd.MoveSW,
      KeyCmd.MoveSW => roll < 0.5 ? KeyCmd.MoveS : KeyCmd.MoveW,
      KeyCmd.MoveW => roll < 0.5 ? KeyCmd.MoveSW : KeyCmd.MoveNW,
      _ => roll < 0.5 ? KeyCmd.MoveW: KeyCmd.MoveS
    };
  }

  static List<(KeyCmd, Loc, bool)> Turns(KeyCmd cmd, Loc loc, Actor p, Map m)
  {
    List<(KeyCmd, Loc, bool)> turns = [];
    Loc a, b;
    bool valid;
    switch (cmd)
    {
      case KeyCmd.RunE:
      case KeyCmd.RunW:
      case KeyCmd.MoveE:
      case KeyCmd.MoveW:
        a = loc with { Row = loc.Row + 1};        
        valid = MoveAction.CanMoveTo(p, m, a, false);
        turns.Add((KeyCmd.MoveS, a, valid));
        b = loc with { Row = loc.Row - 1};
        valid = MoveAction.CanMoveTo(p, m, b, false);
        turns.Add((KeyCmd.MoveN, b, valid));
        break;
      case KeyCmd.RunN:
      case KeyCmd.RunS:
      case KeyCmd.MoveN:
      case KeyCmd.MoveS:
        a = loc with { Col = loc.Col + 1};        
        valid = MoveAction.CanMoveTo(p, m, a, false);
        turns.Add((KeyCmd.MoveE, a, valid));
        b = loc with { Col = loc.Col - 1};
        valid = MoveAction.CanMoveTo(p, m, b, false);
        turns.Add((KeyCmd.MoveW, b, valid));
        break;
    }

   return turns;
  }

  // Tiles that will cause the player to stop running
  static bool InterestingTiles( GameState gs, Loc loc)
  {
    foreach (Loc adj in Util.Adj4Locs(loc))
    {
      Tile tile = gs.TileAt(adj);
      switch (tile.Type)
      {
        case TileType.ClosedDoor:
        case TileType.OpenDoor:
        case TileType.LockedDoor:
        case TileType.BrokenDoor:
        case TileType.Landmark:
        case TileType.Upstairs:
        case TileType.Downstairs:
          return true;
      }

      if (tile.IsVisibleTrap())
        return true;

      if (gs.ObjDb.ItemsAt(adj).Count > 0)
      {
        return true;
      }

      foreach (Item obj in gs.ObjDb.EnvironmentsAt(adj))
      {
        foreach (Trait t in obj.Traits)
        {
          if (t is MoldSporesTrait)
            return true;
          if (t is OnFireTrait)
            return true;
        }
      }
    }

    return false;    
  }

  void SetUpTravelPath(GameState gs, KeyCmd cmd)
  {
    Player player = GS.Player;

    foreach (Trait t in gs.Player.Traits)
    {
      if (t is ConfusedTrait)
      {
        gs.UIRef().AlertPlayer("You are too confused!");
        return;
      }

      // Mainly doing this because as I write this, I'm not sure how I want
      // to handle occasionally staggering while the player is drunk.
      if (t is TipsyTrait)
      {
        gs.UIRef().AlertPlayer("You need to sober up before you go running through the dungeon!");
        return;
      }
    }

    Map map = gs.CurrentMap;
    List<Action> moves = [];
    var (dr, dc) = RunDirection[cmd];
    Loc loc = player.Loc with { Row = player.Loc.Row + dr, Col = player.Loc.Col + dc };
    while (MoveAction.CanMoveTo(player, map, loc, false))
    {
      moves.Add(new MoveAction(gs, player, loc, false));  
      player.QueueAction(new MoveAction(gs, player, loc, false));
      Loc prev = loc;
      loc = player.Loc with { Row = loc.Row + dr, Col = loc.Col + dc };
      
      int adjFloors = 0;
      foreach (Loc adj in Util.Adj4Locs(prev))
      {
        if (gs.TileAt(adj).Type == TileType.DungeonFloor)
          ++adjFloors;
      }
      if (adjFloors > 2)
        break;

      if (InterestingTiles(gs, prev))
        break;

      if (!MoveAction.CanMoveTo(player, map, loc, false))
      {
        var turns = Turns(cmd, prev, player, map);
        var (ndir1, nloc1, valid1) = turns[0];
        var (ndir2, nloc2, valid2) = turns[1];

        if (valid1 && valid2)
        {
          // if both choices are valid, we're at an intersection 
          // or such so stop
          break;
        }
        else if (valid1)
        {
          loc = nloc1;
          cmd = ndir1;
          (dr, dc) = MovementDirections[cmd];
        }
        else if (valid2)
        {
          loc = nloc2;
          cmd = ndir2;
          (dr, dc) = MovementDirections[cmd];
        }
      }
    }

    player.Running = true;
  }

  static readonly Dictionary<KeyCmd, (int dr, int dc)> RunDirection = new()
  {
    [KeyCmd.RunW] = (0, -1),   // left
    [KeyCmd.RunS] = (1, 0),    // down
    [KeyCmd.RunN] = (-1, 0),   // up
    [KeyCmd.RunE] = (0, 1),    // right
  };

  static readonly Dictionary<KeyCmd, (int dr, int dc)> MovementDirections = new()
  {
    [KeyCmd.MoveW] = (0, -1),   // left
    [KeyCmd.MoveS] = (1, 0),    // down
    [KeyCmd.MoveN] = (-1, 0),   // up
    [KeyCmd.MoveE] = (0, 1),    // right
    [KeyCmd.MoveNW] = (-1, -1),  // up-left
    [KeyCmd.MoveNE] = (-1, 1),   // up-right
    [KeyCmd.MoveSW] = (1, -1),   // down-left
    [KeyCmd.MoveSE] = (1, 1)     // down-right
  };

  static bool IsMoveKey(KeyCmd cmd) => MovementDirections.ContainsKey(cmd);

  static bool AttackingWithReach(Player player)
  {
    if (player.Inventory.ReadiedWeapon() is Item item && item.HasTrait<ReachTrait>())
      return true;

    return false;
  }

  void CalcMovementAction(GameState gs, KeyCmd cmd)
  {
    bool involuntary = false;
    if (GS.Player.HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer("You are confused!");
      KeyCmd[] dirs = [ KeyCmd.MoveN, KeyCmd.MoveS, KeyCmd.MoveE, KeyCmd.MoveW,
        KeyCmd.MoveNE, KeyCmd.MoveNW, KeyCmd.MoveSE, KeyCmd.MoveSW];
      KeyCmd nch = dirs[gs.Rng.Next(dirs.Length)];
      if (nch != cmd)
        involuntary = true;
      cmd = nch;
    }
    
    if (GS.Player.HasTrait<TipsyTrait>() && gs.Rng.NextDouble() < 0.15)
    {
      gs.UIRef().AlertPlayer("You stagger!");
      cmd = CalcStagger(gs, cmd);
      involuntary = true;
    }

    (int dr, int dc) = MovementDirections[cmd];

    // I'm not sure this is the best spot for this but it is a convenient place
    // to calculate attacking with Reach
    if (AttackingWithReach(gs.Player))
    {
      Loc adj = gs.Player.Loc.Move(dr, dc);
      Tile adjTile = gs.TileAt(adj);
      Loc adj2 = gs.Player.Loc.Move(dr * 2, dc * 2);

      if (adjTile.PassableByFlight() && !gs.ObjDb.Occupied(adj) && gs.ObjDb.Occupant(adj2) is Actor occ && Battle.PlayerWillAttack(occ))
      {
        Colour colour = gs.Player.Inventory.ReadiedWeapon()!.Glyph.Lit;
        PolearmAnimation anim = new(gs, colour, gs.Player.Loc, adj2);
        gs.UIRef().RegisterAnimation(anim);

        gs.Player.QueueAction(new MeleeAttackAction(gs, gs.Player, adj2));
        return;
      }
    }

    gs.Player.QueueAction(new BumpAction(gs, gs.Player, gs.Player.Loc.Move(dr, dc), involuntary));
  }

  // Check if the player has a focus readied, or knows spells that don't need a focus
  bool SpellcastingPrereqs()
  {
    if (GS.Player.Inventory.FocusEquipped())
      return true;

    Item? rw = GS.Player.Inventory.ReadiedWeapon();
    if (rw is not null && rw.Name == "quarterstaff")
      return true;

    foreach (string spell in GS.Player.SpellsKnown)
    {
      if (Spells.NoFocus(spell))
        return true;
    }

    return false;
  }

  static public void FireReadedBow(Item bow, GameState gs)
  {    
    int range;
    Item arrow;
    if (bow.Traits.OfType<AmmoTrait>().FirstOrDefault() is AmmoTrait ammoTrait)
    {
      arrow = ammoTrait.Arrow(gs);
      range = ammoTrait.Range;
    }
    else
    {
      arrow = ItemFactory.Get(ItemNames.ARROW, gs.ObjDb);
      range = 6;
    }

    int archeryBonus = 0;
    if (gs.Player.Stats.TryGetValue(Attribute.ArcheryBonus, out var ab))
      archeryBonus = ab.Curr;
    ArrowShotAction missleAction = new(gs, gs.Player, bow, arrow, archeryBonus);
    Aimer aimer = new(gs, gs.Player.Loc, range) { DeferredAction = missleAction };
    gs.UIRef().SetInputController(aimer);
  }

  static void PickupCommand(GameState gs, UserInterface ui)
  {
    Actor player = gs.Player;
    List<Item> items = [];
    List<Item> itemsInPit = [];
    foreach (Item item in gs.ObjDb.VisibleItemsAt(player.Loc))
    {
      if (item.HasTrait<BlockTrait>())
        continue;
      if (item.HasTrait<AffixedTrait>())
        continue;

      if (item.HasTrait<InPitTrait>())
        itemsInPit.Add(item);
      else
        items.Add(item);
    }

    bool playerInPit = player.HasTrait<InPitTrait>();
    if (itemsInPit.Count == 1 && !playerInPit)
    {
      ui.AlertPlayer($"You cannot reach {itemsInPit[0].FullName.DefArticle()}.");
      return;
    }
    else if (itemsInPit.Count > 0 && !playerInPit)
    {
      ui.AlertPlayer("You cannot reach the items in the pit.");
      return;
    }

    // Note that in the current state of the game, an item that is on a pit square
    // will be in the pit. There's (currently) no concept of floating items so I
    // don't have to the worry about the situation where there are items in the pit
    // and items floating above the pit and what a player can and cannot reach in
    // that situation. This will change if/when I add floating items.
    if (itemsInPit.Count > 0)
    {
      items = itemsInPit;
    }

    if (items.Count == 0)
    {
      ui.AlertPlayer("There's nothing here you can pick up.");
      return;
    }

    int numStacks = items.DistinctBy(i => i.Name).Count();
    if (numStacks == 1)
    {
      PickupItemAction action = new(gs, player);
      // A bit kludgy but this sets up the Action as though
      // the player had selected the first item in a list of one
      LongListResult res = new() { Values = [items[0].ID] };
      action.ReceiveUIResult(res);
      player.QueueAction(action);
    }
    else
    {
      ui.SetInputController(new PickupMenu(items, gs) { DeferredAction = new PickupItemAction(gs, player) });
    }    
  }

  void ProcessInteractCmd(UserInterface ui)
  {
    Loc singleClosedDoor = SingleAdjTile(GS, GS.Player.Loc, TileType.ClosedDoor);
    Loc singleOpenDoor = SingleAdjTile(GS, GS.Player.Loc, TileType.OpenDoor);
    Loc singleLockedDoor = SingleAdjTile(GS, GS.Player.Loc, TileType.LockedDoor);

    Loc singleNPC = Loc.Nowhere;
    int occupiedCount = 0;
    foreach (Loc adj in Util.Adj8Locs(GS.Player.Loc))
    {
      if (GS.ObjDb.Occupied(adj))
      {
        ++occupiedCount;
        singleNPC = adj;
      }
    }

    Loc singleDevice = Loc.Nowhere;
    int deviceCount = 0;
    if (GS.ObjDb.ItemsAt(GS.Player.Loc).Where(i => i.Type == ItemType.Device).Any())
    {
      ++deviceCount;
      singleDevice = GS.Player.Loc;
    }
    foreach (Loc adj in Util.Adj8Locs(GS.Player.Loc))
    {
      if (GS.ObjDb.ItemsAt(adj).Where(i => i.Type == ItemType.Device).Any())
      {
        ++deviceCount;
        singleDevice = adj;
      }
    }

    if (singleClosedDoor != Loc.Nowhere && singleOpenDoor == Loc.Nowhere && occupiedCount == 0 && deviceCount == 0)
    {
      GS.Player.QueueAction(new OpenDoorAction(GS, GS.Player) { Loc = singleClosedDoor });
      return;
    }
    else if (singleLockedDoor != Loc.Nowhere && singleOpenDoor == Loc.Nowhere && singleClosedDoor == Loc.Nowhere && occupiedCount == 0 && deviceCount == 0)
    {
      LockedDoorMenu menu = new(ui, GS, singleLockedDoor);
      ui.SetInputController(menu);
      return;
    }
    else if (singleClosedDoor == Loc.Nowhere && singleOpenDoor != Loc.Nowhere && singleLockedDoor == Loc.Nowhere && occupiedCount == 0 && deviceCount == 0)
    {
      GS.Player.QueueAction(new CloseDoorAction(GS, GS.Player) { Loc = singleOpenDoor });
      return;
    }
    else if (singleClosedDoor == Loc.Nowhere && singleOpenDoor == Loc.Nowhere && singleLockedDoor == Loc.Nowhere && occupiedCount == 1 && deviceCount == 0)
    {
      GS.Player.QueueAction(new ChatAction(GS, GS.Player) { Loc = singleNPC });
      return;
    }
    else if (singleClosedDoor == Loc.Nowhere && singleOpenDoor == Loc.Nowhere && singleLockedDoor == Loc.Nowhere && occupiedCount == 0 && deviceCount == 1)
    {
      GS.Player.QueueAction(new DeviceInteractionAction(GS, GS.Player) { Loc = singleDevice });
      return;
    }

    DirectionalInputer dir = new(GS, true) { DeferredAction = new SelectActionAction(GS, GS.Player) };
    ui.SetInputController(dir);
  }

  public override void Input(char ch)
  {
    GS.Player.HaltTravel();
    UserInterface ui = GS.UIRef();
    ui.ClosePopup();

    KeyCmd cmd = GS.KeyMap.ToCmd(ch);
    if (IsMoveKey(cmd))
    {
      CalcMovementAction(GS, cmd);
    }
    else if (cmd == KeyCmd.RunW || cmd == KeyCmd.RunS || cmd == KeyCmd.RunN || cmd == KeyCmd.RunE)
    {
      SetUpTravelPath(GS, cmd);
    }
    else if (cmd == KeyCmd.UseItem)
    {
      InvOption invOptions = InvOption.OnlyUseable;
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions("Use which item?") { Options = invOptions });
      HashSet<char> options = [];
      foreach (char c in GS.Player.Inventory.UsedSlots())
      {
        var (item, _) = GS.Player.Inventory.ItemAt(c);
        if (item is not null && item.IsUseableItem())
          options.Add(c);
      }
      ui.SetInputController(new Inventorier(GS, options) { DeferredAction = new UseItemAction(GS, GS.Player) });
    }
    else if (cmd == KeyCmd.CastSpell)
    {
      if (GS.Player.SpellsKnown.Count == 0)
      {
        ui.SetPopup(new Popup("You don't know any spells!", "", -1, -1));
      }
      else if (!SpellcastingPrereqs())
      {
        ui.SetPopup(new Popup("You must have a casting focus prepared, like a wand or staff!", "", -1, -1));
      }
      else
      {
        ui.SetInputController(new SpellcastMenu(GS));
      }
    }
    else if (cmd == KeyCmd.Chat)
    {
      DirectionalInputer dir = new(GS, true) { DeferredAction = new ChatAction(GS, GS.Player) };
      ui.SetInputController(dir);
    }
    else if (cmd == KeyCmd.Drop)
    {
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Drop what?", Options = InvOption.MentionMoney });
      HashSet<char> slots = [.. GS.Player.Inventory.UsedSlots()];
      slots.Add('$');
      ui.SetInputController(new Inventorier(GS, slots) { DeferredAction = new DropItemAction(GS, GS.Player) });      
    }
    else if (cmd == KeyCmd.Equip)
    {
      InvOption invOptions = InvOption.OnlyEquipable;
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Equip what?", Options = invOptions });

      HashSet<char> options = [];
      foreach (char c in GS.Player.Inventory.UsedSlots())
      {
        var (item, _) = GS.Player.Inventory.ItemAt(c);
        if (item is not null && item.Equipable())
          options.Add(c);
      }

      Inventorier inven = new(GS, options)
      {
        DeferredAction = new ToggleEquippedAction(GS, GS.Player)
      };
      ui.SetInputController(inven);      
    }
    else if (cmd == KeyCmd.Fire)
    {
      // If the player has an equipped bow, automatically select that, otherwise
      // have them pick a bow (and then equip it)
      if (GS.Player.Inventory.ReadiedBow() is Item bow)
      {
        FireReadedBow(bow, GS);
      }
      else
      {
        GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Fire what?" });
        Inventorier inv = new(GS, [.. GS.Player.Inventory.UsedSlots()]) 
        { 
          DeferredAction = new FireSelectedBowAction(GS, GS.Player) 
        };

        GS.UIRef().SetInputController(inv);
      }
    }
    else if (cmd == KeyCmd.Force)
    {
      ui.SetInputController(new DirectionalInputer(GS, false) { DeferredAction = new BashAction(GS, GS.Player) });      
    }
    else if (cmd == KeyCmd.Inv)
    {
      InventoryDetails details = new(GS, "You are carrying", InvOption.MentionMoney)
      {
        DeferredAction = new CloseMenuAction(GS)
      };
      ui.SetInputController(details);
    }
    else if (cmd == KeyCmd.Map)
    {
      ui.SetInputController(new MapView(GS));
    }    
    else if (cmd == KeyCmd.Interact)
    {
      ProcessInteractCmd(ui);
    }
    else if (cmd == KeyCmd.Quit)
    {
      ui.SetInputController(new YesOrNoInputer(GS) { DeferredAction = new QuitAction() });
      ui.SetPopup(new Popup("Really quit?\n\nYour game won't be saved! (y/n)", "", -1, -1));
    }
    else if (cmd == KeyCmd.Save && !ui.InTutorial)
    {
      ui.SetInputController(new YesOrNoInputer(GS) { DeferredAction = new SaveGameAction() });
      ui.SetPopup(new Popup("Quit & Save? (y/n)", "", -1, -1));
    }
    else if (cmd == KeyCmd.Save && ui.InTutorial)
    {
      ui.SetPopup(new Popup("Saving is disabled in the tutorial.", "", -1, -1));
    }
    else if (cmd == KeyCmd.Search)
    {
      GS.Player.QueueAction(new SearchAction(GS, GS.Player));
    }
    else if (cmd == KeyCmd.Throw)
    {
      // Eventually I'll want to remember the last item thrown
      // so the player doesn't need to always select an item if
      // they're throwing draggers several turns in a row
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Throw what?" });
      Inventorier inv = new(GS, [.. GS.Player.Inventory.UsedSlots()])
      {
        DeferredAction = new ThrowSelectionAction(GS, GS.Player)
      };
      ui.SetInputController(inv);
    }
    else if (cmd == KeyCmd.Debug)
    {
      ui.SetInputController(new WizardCommander(GS));
    }
    else if (cmd == KeyCmd.Examine)
    {
      ui.SetInputController(new Examiner(GS, GS.Player.Loc));
    }
    else if (cmd == KeyCmd.Pickup)
    {
      PickupCommand(GS, ui);
    }
    else if (cmd == KeyCmd.Descend)
    {
      Action action = GS.CurrentMap.HasFeature(MapFeatures.Submerged) || GS.TileAt(GS.Player.Loc).Type == TileType.Lake
        ? new SwimAction(GS, GS.Player, false)
        : new DownstairsAction(GS);
      GS.Player.QueueAction(action);
    }
    else if (cmd == KeyCmd.Climb)
    {
      Action action;
      if (!GS.CurrentMap.HasFeature(MapFeatures.Submerged) || GS.TileAt(GS.Player.Loc).Type == TileType.Upstairs)
        action = new UpstairsAction(GS);
      else
        action = new SwimAction(GS, GS.Player, true);
      GS.Player.QueueAction(action);
    }
    else if (cmd == KeyCmd.Messages)
    {
      var lines = ui.MessageHistory.Select(m => m.Fmt);
      ui.SetInputController(new LongMessagerInputer(GS, ui, lines));
    }
    else if (cmd == KeyCmd.CharacterSheet)
    {
      List<string> lines = GS.Player.CharacterSheet();
      ui.SetInputController(new LongMessagerInputer(GS, ui, lines));
    }
    else if (cmd == KeyCmd.CheatSheetMode)
    {
      int x = (int)ui.CheatSheetMode + 1;
      ui.CheatSheetMode = (CheatSheetMode)(x % 4);
    }
    else if (cmd == KeyCmd.Help)
    {
      ui.SetInputController(new HelpScreen(GS, ui));
    }
    else if (cmd == KeyCmd.Options)
    {
      ui.SetInputController(new OptionsScreen(GS));
    }
    else if (cmd == KeyCmd.Pass)
    {
      GS.Player.QueueAction(new PassAction());
    }
  }
}