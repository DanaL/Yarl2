
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

class PlayerCommandController(GameState gs) : Inputer(gs)
{
  static Loc SingleAdjTile(GameState gs, Loc centre, TileType seeking)
  {
    var adj = Util.Adj8Locs(centre)
                  .Where(loc => gs.TileAt(loc).Type == seeking);
    return adj.Count() == 1 ? adj.First() : Loc.Nowhere;
  }

  static char CalcStagger(GameState gs, char ch)
  {
    double roll = gs.Rng.NextDouble();
    return ch switch
    {
      'k' => roll < 0.5 ? 'y' : 'u',
      'u' => roll < 0.5 ? 'k' : 'l',
      'l' => roll < 0.5 ? 'u' : 'n',
      'n' => roll < 0.5 ? 'l' : 'j',
      'j' => roll < 0.5 ? 'n' : 'b',
      'b' => roll < 0.5 ? 'j' : 'h',
      'h' => roll < 0.5 ? 'b' : 'y',
      _ => roll < 0.5 ? 'h' : 'k'
    };
  }

  static (int, int) KeyToDir(char ch) =>
    MovementDirections.TryGetValue(ch, out var dir) ? dir : (0, 0);

  static char DirToKey((int dr, int dc) dir) =>
    MovementDirections.FirstOrDefault(x => x.Value == dir).Key;

  List<(char, Loc, bool)> Turns(char ch, Loc loc, Actor p, Map m)
  {
    List<(char, Loc, bool)> turns = [];
    Loc a, b;
    bool valid;
    switch (ch)
    {
      case 'l':
      case 'h':
        a = loc with { Row = loc.Row + 1};        
        valid = MoveAction.CanMoveTo(p, m, a);
        turns.Add(('j', a, valid));
        b = loc with { Row = loc.Row - 1};
        valid = MoveAction.CanMoveTo(p, m, b);
        turns.Add(('k', b, valid));
        break;
      case 'j':
      case 'k':
        a = loc with { Col = loc.Col + 1};        
        valid = MoveAction.CanMoveTo(p, m, a);
        turns.Add(('l', a, valid));
        b = loc with { Col = loc.Col - 1};
        valid = MoveAction.CanMoveTo(p, m, b);
        turns.Add(('h', b, valid));
        break;
    }

   return turns;
  }

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
    }

    return false;    
  }

  void SetUpTravelPath(GameState gs, char ch)
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
    char dir = char.ToLower(ch);
    var (dr, dc) = MovementDirections[dir];
    Loc loc = player.Loc with { Row = player.Loc.Row + dr, Col = player.Loc.Col + dc };
    while (MoveAction.CanMoveTo(player, map, loc))
    {
      moves.Add(new MoveAction(gs, player, loc));  
      player.QueueAction(new MoveAction(gs, player, loc));
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

      if (!MoveAction.CanMoveTo(player, map, loc))
      {
        List<(char, Loc, bool)> turns = Turns(dir, prev, player, map);
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
          dir = ndir1;
          (dr, dc) = MovementDirections[dir];
        }
        else if (valid2)
        {
          loc = nloc2;
          dir = ndir2;
          (dr, dc) = MovementDirections[dir];
        }
      }
    }

    player.Running = true;
  }

  static readonly Dictionary<char, (int dr, int dc)> MovementDirections = new()
  {
    ['h'] = (0, -1),   // left
    ['j'] = (1, 0),    // down
    ['k'] = (-1, 0),   // up
    ['l'] = (0, 1),    // right
    ['y'] = (-1, -1),  // up-left
    ['u'] = (-1, 1),   // up-right
    ['b'] = (1, -1),   // down-left
    ['n'] = (1, 1)     // down-right
  };

  static bool IsMoveKey(char ch) => MovementDirections.ContainsKey(ch);

  static bool AttackingWithReach(Player player)
  {
    if (player.Inventory.ReadiedWeapon() is Item item && item.HasTrait<ReachTrait>())
      return true;

    return false;
  }

  void CalcMovementAction(GameState gs, char ch)
  {
    if (GS.Player.HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer("You are confused!");
      char[] dirs = ['h', 'j', 'k', 'l', 'y', 'u', 'b', 'n'];
      ch = dirs[gs.Rng.Next(dirs.Length)];
    }

    if (GS.Player.HasTrait<TipsyTrait>() && gs.Rng.NextDouble() < 0.15)
    {
      gs.UIRef().AlertPlayer("You stagger!");
      ch = CalcStagger(gs, ch);
    }

    (int dr, int dc) = KeyToDir(ch);

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

    gs.Player.QueueAction(new BumpAction(gs, gs.Player, gs.Player.Loc.Move(dr, dc)));
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

    if (IsMoveKey(ch))
    {
      CalcMovementAction(GS, ch);
    }
    else if (ch == 'H' || ch == 'J' || ch == 'K' || ch == 'L')
    {
      SetUpTravelPath(GS, ch);
    }
    else if (ch == 'a')
    {
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions("Use which item?"));
      ui.SetInputController(new Inventorier(GS, [.. GS.Player.Inventory.UsedSlots()]) { DeferredAction = new UseItemAction(GS, GS.Player) });
    }
    else if (ch == 'c')
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
    else if (ch == 'C')
    {
      DirectionalInputer dir = new(GS, true) { DeferredAction = new ChatAction(GS, GS.Player) };
      ui.SetInputController(dir);
    }
    else if (ch == 'd')
    {
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Drop what?", Options = InvOption.MentionMoney });
      HashSet<char> slots = [.. GS.Player.Inventory.UsedSlots()];
      slots.Add('$');
      ui.SetInputController(new Inventorier(GS, slots) { DeferredAction = new DropItemAction(GS, GS.Player) });      
    }
    else if (ch == 'e')
    {
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Equip what?" });
      Inventorier inven = new(GS, [.. GS.Player.Inventory.UsedSlots()])
      {
        DeferredAction = new ToggleEquippedAction(GS, GS.Player)
      };
      ui.SetInputController(inven);      
    }
    else if (ch == 'f')
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
    else if (ch == 'F')
    {
      ui.SetInputController(new DirectionalInputer(GS) { DeferredAction = new BashAction(GS, GS.Player) });      
    }
    else if (ch == 'i')
    {
      InventoryDetails details = new(GS, "You are carrying", InvOption.MentionMoney)
      {
        DeferredAction = new CloseMenuAction(GS)
      };
      ui.SetInputController(details);
    }
    else if (ch == 'M')
    {
      ui.DisplayMapView(GS);
    }
    else if (ch == 'o')
    {
      ProcessInteractCmd(ui);
    }
    else if (ch == 'Q')
    {
      ui.SetInputController(new YesOrNoInputer(GS) { DeferredAction = new QuitAction() });
      ui.SetPopup(new Popup("Really quit?\n\nYour game won't be saved! (y/n)", "", -1, -1));
    }
    else if (ch == 'S' && !ui.InTutorial)
    {
      ui.SetInputController(new YesOrNoInputer(GS) { DeferredAction = new SaveGameAction() });
      ui.SetPopup(new Popup("Quit & Save? (y/n)", "", -1, -1));
    }
    else if (ch == 'S' && ui.InTutorial)
    {
      ui.SetPopup(new Popup("Saving is disabled in the tutorial.", "", -1, -1));
    }
    else if (ch == 's')
    {
      GS.Player.QueueAction(new SearchAction(GS, GS.Player));
    }
    else if (ch == 't')
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
    else if (ch == 'W')
    {
      ui.SetInputController(new WizardCommander(GS));
    }
    else if (ch == 'x')
    {
      ui.SetInputController(new Examiner(GS, GS.Player.Loc));
    }   
    else if (ch == ',')
    {
      PickupCommand(GS, ui);
    }
    else if (ch == '>')
    {
      Action action = GS.CurrentMap.Submerged || GS.TileAt(GS.Player.Loc).Type == TileType.Lake
        ? new SwimAction(GS, GS.Player, false)
        : new DownstairsAction(GS);
      GS.Player.QueueAction(action);
    }
    else if (ch == '<')
    {
      Action action = GS.CurrentMap.Submerged ? new SwimAction(GS, GS.Player, true) : new UpstairsAction(GS);
      GS.Player.QueueAction(action);      
    }
    else if (ch == '*')
    {
      var lines = ui.MessageHistory.Select(m => m.Fmt);
      ui.SetInputController(new LongMessagerInputer(GS, ui, lines));
    }
    else if (ch == '@')
    {
      List<string> lines = GS.Player.CharacterSheet();
      ui.SetInputController(new LongMessagerInputer(GS, ui, lines));
    }
    else if (ch == '/')
    {
      int x = (int)ui.CheatSheetMode + 1;
      ui.CheatSheetMode = (CheatSheetMode)(x % 4);
    }    
    else if (ch == '?')
    {
      ui.SetInputController(new HelpScreen(GS, ui));
    }
    else if (ch == '=')
    {
      ui.SetInputController(new OptionsScreen(GS));
    }
    else if (ch == ' ' || ch == '.')
    {
      GS.Player.QueueAction(new PassAction());
    }
  }
}