
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

enum PlayerLineage
{
  Human,
  Elf,
  Orc,
  Dwarf
}

enum PlayerBackground
{
  Warrior,
  Scholar,
  Skullduggery
}

class Player : Actor, IPerformer, IGameEventListener
{
  public const int MAX_VISION_RADIUS = 25;
  public PlayerLineage Lineage { get; set; }
  public PlayerBackground Background { get; set; }

  Inputer? _inputController;
  Action? _deferred;
  public bool Running { get; set; } = false;
  
  char RepeatingCmd { get; set; }
  HashSet<Loc> LocsRan { get; set; } = [];

  public Player(string name)
  {
    Name = name;
    Recovery = 1.0; // Do I want a 'NaturalRecovery' or such to track cases when
                    // when a Player's recover is bolstered by, like, a Potion of Speed or such?
    Glyph = new Glyph('@', Colours.WHITE, Colours.WHITE, Colours.BLACK, Colours.BLACK);
  }

  public override int Z() => 12;

  public override string FullName => "you";

  public override int AC
  {
    get
    {
      int ac = 10 + Stats[Attribute.Dexterity].Curr;

      int armour = 0;
      foreach (var slot in Inventory.UsedSlots())
      {
        var (item, _) = Inventory.ItemAt(slot);
        if (item is not null && item.Equiped)
        {
          armour += item.Traits.OfType<ArmourTrait>()
                               .Select(t => t.ArmourMod + t.Bonus)
                               .Sum();
          armour += item.Traits.OfType<ACModTrait>()
                               .Select(t => t.ArmourMod)
                               .Sum();
        }
      }

      ac += Traits.OfType<ACModTrait>()
                  .Select(t => t.ArmourMod)
                  .Sum();

      return ac + armour;
    }
  }

  public bool Expired { get; set; } = false;
  public bool Listening => true;
  public ulong ObjId => ID;

  public void CalcHP()
  {
    int baseHP = 10;
    if (Lineage == PlayerLineage.Orc)
      baseHP += 5;
    if (Background == PlayerBackground.Warrior)
      baseHP += 5;

    if (Stats.TryGetValue(Attribute.Constitution, out var con))
    {
      baseHP += con.Max >= 0 ? con.Max * 5 : con.Max;
    }

    foreach (var t in Traits)
    {
      if (t is StatBuffTrait sbt && sbt.Attr == Attribute.HP)
        baseHP += sbt.Amt;
      if (t is StatDebuffTrait sdt && sdt.Attr == Attribute.HP)        
        baseHP += sdt.Amt;              
    }

    // We won't allow an HP buffs and debuffs to kill a character, just make
    // them very very weak
    if (baseHP < 1)
      baseHP = 1;

    Stats[Attribute.HP].SetMax(baseHP);
  }

  public override int TotalMissileAttackModifier(Item weapon)
  {
    int mod = Stats[Attribute.Dexterity].Curr;

    if (Stats.TryGetValue(Attribute.AttackBonus, out var attackBonus))
      mod += attackBonus.Curr;

    return mod;
  }

  public override int TotalSpellAttackModifier()
  {
    int mod = Stats[Attribute.Will].Curr;
    if (Stats.TryGetValue(Attribute.AttackBonus, out var attackBonus))
      mod += attackBonus.Curr;
    return mod;
  }

  public void ExerciseStat(Attribute attr)
  {
    if (Stats.TryGetValue(attr, out Stat? stat))
      stat.SetMax(stat.Curr + 1);
    else
      Stats.Add(attr, new Stat(1));
  }

  public override List<Damage> MeleeDamage()
  {
    List<Damage> dmgs = [];

    Item? weapon = Inventory.ReadiedWeapon();
    if (weapon is not null)
    {
      foreach (var trait in weapon.Traits)
      {
        if (trait is DamageTrait dmg)
        {
          dmgs.Add(new Damage(dmg.DamageDie, dmg.NumOfDie, dmg.DamageType));
        }
        else if (trait is VersatileTrait versatile)
        {
          DamageTrait dt = Inventory.ShieldEquiped() ? versatile.OneHanded : versatile.TwoHanded;
          dmgs.Add(new Damage(dt.DamageDie, dt.NumOfDie, dt.DamageType));
        }
      }
    }
    else
    {
      // Perhaps eventually there will be a Monk Role, or one 
      // with claws or such
      dmgs.Add(new Damage(1, 1, DamageType.Blunt));
    }

    if (HasTrait<BerzerkTrait>())
    {
      dmgs.Add(new Damage(10, 1, DamageType.Force));
    }

    return dmgs;
  }

  static HashSet<(char, ulong)> ShowPickupMenu(UserInterface ui, List<Item> items)
  {
    var counts = new Dictionary<Item, int>();
    foreach (var item in items)
    {
      if (item.HasTrait<StackableTrait>() && counts.TryGetValue(item, out int value))
        counts[item] = value + 1;
      else
        counts.Add(item, 1);
    }

    HashSet<(char, ulong)> options = [];
    List<string> lines = ["What do you pick up?"];
    char slot = 'a';
    foreach (var (item, count) in counts)
    {
      options.Add((slot, item.ID));
      string desc;
      if (count > 1)
      {
        desc = $"{count} {item.FullName.Pluralize()}";
      }
      else if (item.Type == ItemType.Zorkmid)
      {
        desc = $"{item.Value} zorkmid";
        if (item.Value != 1)
          desc += "s";
      }
      else
      {
        desc = item.FullName;
      }
      lines.Add($"{slot++}) {desc}");
    }
    ui.ShowDropDown(lines);

    return options;
  }

  string PrintStat(Attribute attr)
  {
    static string Fmt(int v)
    {
      return v > 0 ? $"+{v}" : $"{v}";
    }
    int val = Stats[attr].Curr;
    int max = Stats[attr].Max;

    if (val != max)
      return $"{Fmt(val)} ({Fmt(max)})";
    else
      return Fmt(val);
  }

  string CharDesc()
  {
    string major = Background switch
    {
      PlayerBackground.Scholar => "Lore & History",
      PlayerBackground.Skullduggery => "Fine Sneaky Arts",
      _ => "Arms & Armour",
    };

    return $"{Lineage.ToString().ToLower().IndefArticle()} who majored in {major}.";
  }

  List<string> CharacterSheet()
  {
    List<string> lines = [];

    lines.Add($"{Name}, {CharDesc()}");
    lines.Add("");
    lines.Add($"Str: {PrintStat(Attribute.Strength)}  Con: {PrintStat(Attribute.Constitution)}  Dex: {PrintStat(Attribute.Dexterity)}  Piety: {PrintStat(Attribute.Piety)}  Will: {PrintStat(Attribute.Will)}");
    lines.Add("");

    if (Stats.TryGetValue(Attribute.SwordUse, out Stat? stat))
      lines.Add($"Swords bonus: +{stat.Curr / 100} ({stat.Curr})");
    if (Stats.TryGetValue(Attribute.PolearmsUse, out stat))
      lines.Add($"Polearms bonus: +{stat.Curr / 100} ({stat.Curr})");
    if (Stats.TryGetValue(Attribute.AxeUse, out stat))
      lines.Add($"Axes bonus: +{stat.Curr / 100} ({stat.Curr})");
    if (Stats.TryGetValue(Attribute.CudgelUse, out stat))
      lines.Add($"Cudgels bonus: +{stat.Curr / 100} ({stat.Curr})");
    if (Stats.TryGetValue(Attribute.FinesseUse, out stat))
      lines.Add($"Finesse bonus: +{stat.Curr / 100} ({stat.Curr})");
    if (Stats.TryGetValue(Attribute.BowUse, out stat))
      lines.Add($"Bow bonus: +{stat.Curr / 100} ({stat.Curr})");

    lines.Add("");

    List<string> traitsToShow = [];
    double alacrity = 0;
    foreach (Trait trait in Traits)
    {
      if (trait is RageTrait)
        traitsToShow.Add("You have the ability to rage");
      if (trait is LightStepTrait)
        traitsToShow.Add("You step lightly");
      if (trait is DodgeTrait)
        traitsToShow.Add("You sometimes can dodge attacks");
      if (trait is AlacrityTrait alacrityTrait)
        alacrity -= alacrityTrait.Amt;
    }
    
    if (alacrity < 0)
      traitsToShow.Add("You are quicker than normal");
    else if (alacrity > 0)
      traitsToShow.Add("You are somewhat slowed");

    if (traitsToShow.Count > 0)
    {
      lines.Add(string.Join(". ", traitsToShow) + ".");
      lines.Add("");
    }
       
    if (Stats[Attribute.Depth].Max == 0)
      lines.Add("You have yet to venture into the Dungeon.");
    else
      lines.Add($"You have ventured as deep as level {Stats[Attribute.Depth].Max}.");

    return lines;
  }

  public void FireReadedBow(Item bow, GameState gs)
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
    if (Stats.TryGetValue(Attribute.ArcheryBonus, out var ab))
      archeryBonus = ab.Curr;
    var missleAction = new ArrowShotAction(gs, this, bow, arrow, archeryBonus);

    var acc = new Aimer(gs, Loc, range);
    ReplacePendingAction(missleAction, acc);
  }

  public void ReplacePendingAction(Action newAction, Inputer inputer)
  {
    _deferred = newAction;
    _inputController = inputer;
  }

  private static readonly Dictionary<char, (int dr, int dc)> MovementDirections = new()
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

  static (int, int) KeyToDir(char ch) => 
    MovementDirections.TryGetValue(ch, out var dir) ? dir : (0, 0);

  static char DirToKey((int dr, int dc) dir) => 
    MovementDirections.FirstOrDefault(x => x.Value == dir).Key;

  bool AttackingWithReach()
  {
    if (Inventory.ReadiedWeapon() is Item item && item.HasTrait<ReachTrait>())
      return true;

    return false;
  }

  Action CalcMovementAction(GameState gs, char ch)
  {
    if (HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer("You are confused!");
      char[] dirs = ['h', 'j', 'k', 'l', 'y', 'u', 'b', 'n'];
      ch = dirs[gs.Rng.Next(dirs.Length)];
    }

    (int dr, int dc) = KeyToDir(ch);

    // I'm not sure this is the best spot for this but it is a convenient place
    // to calculate attacking with Reach
    if (AttackingWithReach())
    {
      Loc adj = Loc.Move(dr, dc);
      Tile adjTile = gs.TileAt(adj);
      Loc adj2 = Loc.Move(dr * 2, dc * 2);

      if (adjTile.PassableByFlight() && !gs.ObjDb.Occupied(adj) && gs.ObjDb.Occupant(adj2) is Actor occ && Battle.PlayerWillAttack(occ))
      {
        var colour = Inventory.ReadiedWeapon()!.Glyph.Lit;
        var anim = new PolearmAnimation(gs, colour, Loc, adj2);
        gs.UIRef().RegisterAnimation(anim);

        return new MeleeAttackAction(gs, this, adj2);
      }
    }

    return new MoveAction(gs, this, Loc.Move(dr, dc));
  }

  // 'Running' just means repeated moving
  NullAction StartRunning(GameState gs, char ch)
  {
    if (HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer("You are too confused!");
      Running = false;
    }

    Running = true;
    RepeatingCmd = char.ToLower(ch);
    LocsRan = [];

    return new NullAction();
  }

  Loc[] RunningToward(char ch)
  {
    List<(int, int)> next = ch switch
    {
      'h' => [(-1, -1), (0, -1), (1, -1), (1, 0), (-1, 0)],
      'j' => [(1, -1), (1, 0), (1, 1), (0, 1), (0, -1)],
      'k' => [(-1, -1), (-1, 0), (-1, 1), (0, 1), (0, -1)],
      'l' => [(-1, 1), (0, 1), (1, 1), (1, 0), (-1, 0)],
      'y' => [(0, -1), (-1, -1), (-1, 0), (1, -1), (-1, 1)],
      'u' => [(-1, 0), (-1, 1), (0, 1), (-1, -1), (1, 1)],
      'b' => [(0, -1), (1, -1), (1, 0), (1, -1), (1, 1)],
      _ => [(1, 0), (1, 1), (0, 1), (1, -1), (-1, 1)]
    };

    return next.Select(n => Loc with { Row = Loc.Row + n.Item1, Col = Loc.Col + n.Item2 })
                .Where(l => !LocsRan.Contains(l))
                .ToArray();
  }

  char UpdateRunning(GameState gs)
  {
    Loc[] nextLocs = RunningToward(RepeatingCmd);

    // Running is interrupted by some tiles or sqs with items
    foreach (var loc in nextLocs)
    {
      var tile = gs.TileAt(loc);
      switch (tile.Type)
      {
        case TileType.ClosedDoor:
        case TileType.OpenDoor:
        case TileType.LockedDoor:
        case TileType.BrokenDoor:
        case TileType.Landmark:
        case TileType.Upstairs:
        case TileType.Downstairs:
          Running = false;
          return '\0';
      }

      if (tile.IsVisibleTrap())
      {
        Running = false;
        return '\0';
      }

      if (gs.ObjDb.ItemsAt(loc).Count > 0)
      {
        Running = false;
        return '\0';
      }
    }

    var (dr, dc) = KeyToDir(RepeatingCmd);
    var nextLoc = Loc with { Row = Loc.Row + dr, Col = Loc.Col + dc };
    if (MoveAction.CanMoveTo(this, gs.CurrentMap, nextLoc))
    {
      LocsRan.Add(nextLoc);
      return RepeatingCmd;
    }

    // If we can't travel any further in current direction and there's only one option 
    // of where to continue, change directions and keep going.
    var open = nextLocs.Where(l => MoveAction.CanMoveTo(this, gs.CurrentMap, l)).ToList();
    if (open.Count == 1)
    {
      var loc = (open[0].Row - Loc.Row, open[0].Col - Loc.Col);
      LocsRan.Add(open[0]);
      RepeatingCmd = DirToKey(loc);
      return RepeatingCmd;
    }

    Running = false;
    return '\0';
  }

  static Loc SingleAdjTile(GameState gs, Loc centre, TileType seeking)
  {
    var adj = Util.Adj8Locs(centre)
                  .Where(loc => gs.TileAt(loc).Type == seeking);
    return adj.Count() == 1 ? adj.First() : Loc.Nowhere;
  }

  Action PickupCommand(GameState gs, UserInterface ui)
  {
    var allItems = gs.ObjDb.VisibleItemsAt(Loc);
    if (allItems is null || allItems.Count == 0)
    {
      ui.AlertPlayer("There's nothing there...");
      return new NullAction();
    }

    List<Item> items = [];
    List<Item> itemsInPit = [];
    foreach (var item in gs.ObjDb.VisibleItemsAt(Loc))
    {
      if (item.HasTrait<BlockTrait>())
        continue;

      if (item.HasTrait<InPitTrait>())
        itemsInPit.Add(item);
      else
        items.Add(item);
    }
    
    // Note that in the current state of the game, an item that is on a pit square
    // will be in the pit. There's (currently) no concept of floating items so I
    // don't have to the worry about the situation where there are items in the pit
    // and items floating above the pit and what a player can and cannot reach in
    // that situation. This will change if/when I add floating items.
    bool playerInPit = HasTrait<InPitTrait>();
    if (itemsInPit.Count == 1 && !playerInPit)
    {
      ui.AlertPlayer($"You cannot reach {itemsInPit[0].FullName.DefArticle()}.");
      return new NullAction();
    }
    else if (itemsInPit.Count > 0 && !playerInPit)
    {
      ui.AlertPlayer("You cannot reach the items in the pit.");
      return new NullAction();
    }

    if (itemsInPit.Count > 0)
      items = itemsInPit;

    if (items.Count == 1)
    {
      var a = new PickupItemAction(gs, this);
      // A bit kludgy but this sets up the Action as though
      // the player had selected the first item in a list of one
      var r = new ObjIdUIResult() { ID = items[0].ID };
      a.ReceiveUIResult(r);

      return a;
    }
    else
    {
      var opts = ShowPickupMenu(ui, items);
      _inputController = new PickUpper(opts);
      _deferred = new PickupItemAction(gs, this);
    }

    return new NullAction();
  }

  public override Action TakeTurn(GameState gameState)
  {
    UserInterface ui = gameState.UIRef();

    if (HasActiveTrait<ParalyzedTrait>())
    {
      ui.AlertPlayer("You cannot move!");
      return new PassAction(gameState, this);
    }

    char ch = '\0';

    if (ui.InputBuffer.Count > 0 && ui.InputBuffer.Peek() == ' ')
      Running = false;

    // Check for repeated action here?
    if (Running)
    {
      ch = UpdateRunning(gameState);
    }
    else if (ui.InputBuffer.Count > 0)
    {
      ch = ui.InputBuffer.Dequeue();
    }

    if (ch != '\0')
    {
      if (_inputController is not null)
      {
        _inputController.Input(ch);
        if (!_inputController.Done)
        {
          if (_inputController.Msg != "")
            ui.SetPopup(new Popup(_inputController.Msg, "", -1, -1));
          return new NullAction();
        }
        else
        {
          if (_inputController.Success)
          {
            _deferred.ReceiveUIResult(_inputController.GetResult());
            _inputController = null;
            ui.ClosePopup();
            return _deferred;
          }
          else
          {
            _inputController = null;
            ui.CloseMenu();
            ui.ClosePopup();
            ui.AlertPlayer("Nevermind.");
            return new NullAction();
          }
        }
      }

      ui.ClosePopup();

      if (IsMoveKey(ch))
        return CalcMovementAction(gameState, ch);
      else if (IsMoveKey(char.ToLower(ch)))
        return StartRunning(gameState, ch);
      else if (ch == '>')
        return new DownstairsAction(gameState);
      else if (ch == '<')
        return new UpstairsAction(gameState);
      else if (ch == 'i')
      {
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "You are carrying:", Options = InvOption.MentionMoney });
        _inputController = new InventoryDetails(gameState);
        _deferred = new CloseMenuAction(gameState);
      }
      else if (ch == ',')
      {
        return PickupCommand(gameState, ui);        
      }
      else if (ch == 'a')
      {
        Inventory.ShowMenu(ui, new InventoryOptions("Use which item?"));
        _inputController = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new UseItemAction(gameState, this);
      }      
      else if (ch == 'd')
      {
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Drop what?", Options = InvOption.MentionMoney });
        HashSet<char> slots = [.. Inventory.UsedSlots()];
        slots.Add('$');
        _inputController = new Inventorier(slots);
        _deferred = new DropItemAction(gameState, this);
      }
      else if (ch == 'f')
      {
        // If the player has an equiped bow, automatically select that, otherwise
        // have them pick a bow (and then equip it)
        if (Inventory.ReadiedBow() is Item bow)
        {
          FireReadedBow(bow, gameState);
        }
        else
        {          
          string instructions = "* Use move keys to move to target\n  or TAB through targets;\n  Enter to select or ESC to abort *";
          Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Fire what?", Instructions = instructions });
          _inputController = new Inventorier([.. Inventory.UsedSlots()]);
          _deferred = new FireSelectedBowAction(gameState, this);
        }
      }
      else if (ch == 'F')
      {
        _inputController = new DirectionalInputer(gameState);
        _deferred = new BashAction(gameState, this);
      }
      else if (ch == 't')
      {
        // Eventually I'll want to remember the last item thrown
        // so the player doesn't need to always select an item if
        // they're throwing draggers several turns in a row
        string instructions = "* Use move keys to move to target\n  or TAB through targets;\n  Enter to select or ESC to abort *";
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Throw what?", Instructions = instructions });
        _inputController = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ThrowSelectionAction(gameState, this);
      }
      else if (ch == 'e')
      {
        _inputController = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ToggleEquipedAction(gameState, this);
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Equip what?" });
      }
      else if (ch == 'c')
      {
        Loc singleDoor = SingleAdjTile(gameState, Loc, TileType.OpenDoor);
        if (singleDoor != Loc.Nowhere)
          return new CloseDoorAction(gameState, this, gameState.CurrentMap) {  Loc = singleDoor };

        _inputController = new DirectionalInputer(gameState);
        _deferred = new CloseDoorAction(gameState, this, gameState.CurrMap);
      }
      else if (ch == 'C')
      {
        _inputController = new DirectionalInputer(gameState);
        _deferred = new ChatAction(gameState, this);
      }
      else if (ch == 'o')
      {
        Loc singleDoor = SingleAdjTile(gameState, Loc, TileType.ClosedDoor);
        if (singleDoor != Loc.Nowhere)
          return new OpenDoorAction(gameState, this, gameState.CurrentMap) { Loc = singleDoor };

        _inputController = new DirectionalInputer(gameState);
        _deferred = new OpenDoorAction(gameState, this, gameState.CurrMap);
      }
      else if (ch == 'Q')
      {
        _inputController = new YesOrNoInputer();
        _deferred = new QuitAction();
        ui.SetPopup(new Popup("Really quit?\n\nYour game won't be saved! (y/n)", "", -1, -1));
      }
      else if (ch == 'S')
      {
        _inputController = new YesOrNoInputer();
        _deferred = new SaveGameAction();
        ui.SetPopup(new Popup("Quit & Save? (y/n)", "", -1, -1));
      }
      else if (ch == 's')
      {
        return new SearchAction(gameState, this);
      }
      else if (ch == '*')
      {
        var lines = ui.MessageHistory.Select(m => m.Fmt);
        _inputController = new LongMessagerInputer(ui, lines);
        _deferred = new NullAction();
      }
      else if (ch == '@')
      {
        var lines = CharacterSheet();
        _inputController = new LongMessagerInputer(ui, lines);
        _deferred = new NullAction();
      }
      else if (ch == '/')
      {    
        int x = (int)ui.CheatSheetMode + 1;
        ui.CheatSheetMode = (CheatSheetMode)(x % 3);
        _deferred = new NullAction();
      }
      else if (ch == 'M')
      {
        if (gameState.CurrDungeonID == 0)
          ui.AlertPlayer("Not in the wilderness.");
        else
          gameState.UIRef().DisplayMapView(gameState);
      }
      else if (ch == '?')
      {
        _inputController = new HelpScreenInputer(gameState.UIRef());
        _deferred = new NullAction();
      }
      else if (ch == 'x')
      {
        _inputController = new Examiner(gameState, Loc);
        _deferred = new NullAction();
      }
      else if (ch == 'W')
      {
        _inputController = new WizardCommander(gameState);
        _deferred = new NullAction();
      }
      else if (ch == ' ' || ch == '.')
        return new PassAction();
    }

    return new NullAction();
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (eventType == GameEventType.MobSpotted)
      Running = false;
  }
}
