
using Yarl2;

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
        var colour = gs.Player.Inventory.ReadiedWeapon()!.Glyph.Lit;
        var anim = new PolearmAnimation(gs, colour, gs.Player.Loc, adj2);
        gs.UIRef().RegisterAnimation(anim);

        gs.Player.QueueAction(new MeleeAttackAction(gs, gs.Player, adj2));
        return;
      }
    }

    gs.Player.QueueAction(new BumpAction(gs, gs.Player, gs.Player.Loc.Move(dr, dc)));
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

  public override void Input(char ch)
  {    
    UserInterface ui = GS.UIRef();
    ui.ClosePopup();

    if (IsMoveKey(ch))
    {
      CalcMovementAction(GS, ch);
    }
    else if (ch == 'a')
    {
      GS.Player.Inventory.ShowMenu(ui, new InventoryOptions("Use which item?"));
      ui.SetInputController(new Inventorier(GS, [.. GS.Player.Inventory.UsedSlots()]) { DeferredAction = new UseItemAction(GS, GS.Player) });
    }
    else if (ch == 'c')
    {
      Loc singleDoor = SingleAdjTile(GS, GS.Player.Loc, TileType.OpenDoor);

      if (singleDoor != Loc.Nowhere)
        GS.Player.QueueAction(new CloseDoorAction(GS, GS.Player) { Loc = singleDoor });
      else
      {
        DirectionalInputer dir = new(GS) { DeferredAction = new CloseDoorAction(GS, GS.Player)};
        ui.SetInputController(dir);
      }
    }
    else if (ch == 'C')
    {
      DirectionalInputer dir = new(GS) { DeferredAction = new ChatAction(GS, GS.Player) };
      ui.SetInputController(dir);
    }
    else if (ch == 'd')
    {
      gs.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Drop what?", Options = InvOption.MentionMoney });
      HashSet<char> slots = [.. gs.Player.Inventory.UsedSlots()];
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
    else if (ch == 'F')
    {
      ui.SetInputController(new DirectionalInputer(GS) { DeferredAction = new BashAction(GS, GS.Player) });      
    }
    else if (ch == 'o')
    {
      Loc singleDoor = SingleAdjTile(GS, GS.Player.Loc, TileType.ClosedDoor);
      if (singleDoor != Loc.Nowhere)
        GS.Player.QueueAction(new OpenDoorAction(GS, GS.Player) { Loc = singleDoor });
      else
      {
        DirectionalInputer dir = new(GS) { DeferredAction = new OpenDoorAction(GS, GS.Player) };
        ui.SetInputController(dir);
      }
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
      gs.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Throw what?" });
      Inventorier inv = new(GS, [.. GS.Player.Inventory.UsedSlots()])
      {
        DeferredAction = new ThrowSelectionAction(GS, GS.Player)
      };
      ui.SetInputController(inv);
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
      GS.Player.QueueAction(new DownstairsAction(GS));
    }
    else if (ch == '<')
    {
      GS.Player.QueueAction(new UpstairsAction(GS));
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
    else if (ch == 'M')
    {
      if (GS.InWilderness)
        ui.AlertPlayer("Not in the wilderness.");
      else
        ui.DisplayMapView(GS);
    }
    else if (ch == '?')
    {
      ui.SetInputController(new HelpScreenInputer(GS, ui));
    }
    else if (ch == ' ' || ch == '.')
    {
      GS.Player.QueueAction(new PassAction());
    }
  }
}