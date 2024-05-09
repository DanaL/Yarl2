
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

  Inputer? _accumulator;
  Action? _deferred;
  public bool Running { get; set; } = false;
  char RepeatingCmd { get; set; }
  HashSet<Loc> LocsRan { get; set; } = [];

  public Player(string name)
  {
    Name = name;
    Recovery = 1.0; // Do I want a 'NaturalRecovery' or such to track cases when
                    // when a Player's recover is bolstered by, like, a Potion of Speed or such?
    Glyph = new Glyph('@', Colours.WHITE, Colours.WHITE);
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
        if (item.Equiped)
        {
          armour += item.Traits.OfType<ArmourTrait>()
                               .Select(t => t.ArmourMod + t.Bonus)
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

  public override int TotalMissileAttackModifier(Item weapon)
  {
    int mod = Stats[Attribute.Dexterity].Curr;

    if (Stats.TryGetValue(Attribute.AttackBonus, out var attackBonus))
      mod += attackBonus.Curr;

    return mod;
  }

  public override int TotalMeleeAttackModifier()
  {
    int mod = Stats[Attribute.Strength].Curr;

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
      }
    }
    else
    {
      // Perhaps eventually there will be a Monk Role, or one 
      // with claws or such
      dmgs.Add(new Damage(1, 1, DamageType.Blunt));
    }

    return dmgs;
  }

  public override List<(ulong, int, TerrainFlag)> Auras(GameState gs)
  {
    int playerVisionRadius = 1;

    // What latitude does the game take place out? Will I eventually
    // have seasonal variation in the length of days? :O
    if (gs.InWilderness)
    {
      var (hour, _) = gs.CurrTime();
      if (hour >= 6 && hour <= 19)
        playerVisionRadius = MAX_VISION_RADIUS;
      else if (hour >= 20 && hour <= 21)
        playerVisionRadius = 7;
      else if (hour >= 21 && hour <= 23)
        playerVisionRadius = 3;
      else if (hour < 4)
        playerVisionRadius = 2;
      else if (hour == 4)
        playerVisionRadius = 3;
      else
        playerVisionRadius = 7;
    }

    List<(ulong, int, TerrainFlag)> auras = [(ID, playerVisionRadius, TerrainFlag.Lit)];

    foreach (var (item, _) in Inventory.UsedSlots().Select(Inventory.ItemAt))
    {
      auras.AddRange(item.Auras(gs));
    }

    return auras;
  }

  void ShowInventory(UserInterface ui, string title, string instructions, bool mentionMoney = false)
  {
    var slots = Inventory.UsedSlots().Order().ToArray();

    if (slots.Length == 0)
    {
      //ui.AlertPlayer("You are empty handed!");
      return;
    }

    List<string> lines = [title];
    foreach (var s in slots)
    {
      var (item, count) = Inventory.ItemAt(s);
      string desc = count == 1 ? item.FullName.IndefArticle()
                               : $"{count} {item.FullName.Pluralize()}";

      if (item.Equiped)
      {
        if (item.Type == ItemType.Weapon)
          desc += " (in hand)";
        else if (item.Type == ItemType.Armour)
          desc += " (worn)";
        else if (item.Type == ItemType.Bow)
          desc += " (equiped)";
      }
      lines.Add($"{s}) {desc}");
    }

    if (mentionMoney)
    {
      lines.Add("");
      if (Inventory.Zorkmids == 0)
        lines.Add("You seem to be broke.");
      else if (Inventory.Zorkmids == 1)
        lines.Add("You have a single zorkmid.");
      else
        lines.Add($"You wallet contains {Inventory.Zorkmids} zorkmids.");
    }

    if (!string.IsNullOrEmpty(instructions))
    {
      lines.Add("");
      lines.AddRange(instructions.Split('\n'));
    }

    ui.ShowDropDown(lines);
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
        desc = $"{count} {item.Name.Pluralize()}";
      }
      else if (item.Type == ItemType.Zorkmid)
      {
        desc = $"{item.Value} zorkmid";
        if (item.Value != 1)
          desc += "s";
      }
      else
      {
        desc = item.Name;
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

  List<string> CharacterSheet()
  {
    List<string> lines = [];

    lines.Add($"{Name}, a level {Stats[Attribute.Level].Curr} {Util.PlayerClassToStr(Lineage)}");
    lines.Add("");
    lines.Add($"Str: {PrintStat(Attribute.Strength)}  Con: {PrintStat(Attribute.Constitution)}  Dex: {PrintStat(Attribute.Dexterity)}  Piety: {PrintStat(Attribute.Piety)}  Will: {PrintStat(Attribute.Will)}");
    lines.Add("");
    lines.Add($"You have earned {Stats[Attribute.XP].Max} XP.");
    lines.Add("");

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
    if (bow.Traits.OfType<AmmoTrait>().Any())
    {
      var ammoTrait = bow.Traits.OfType<AmmoTrait>().First();
      arrow = ammoTrait.Arrow(gs);
      range = ammoTrait.Range;
    }
    else
    {
      arrow = ItemFactory.Get("arrow", gs.ObjDb);
      range = 6;
    }

    int archeryBonus = 0;
    if (Stats.TryGetValue(Attribute.ArcheryBonus, out var ab))
      archeryBonus = ab.Curr;
    var missleAction = new ArrowShotAction(gs, this, arrow, archeryBonus);

    var acc = new Aimer(gs, Loc, range);
    ReplacePendingAction(missleAction, acc);
  }

  public void ReplacePendingAction(Action newAction, Inputer newAccumulator)
  {
    _deferred = newAction;
    _accumulator = newAccumulator;
  }

  static bool IsMoveKey(char ch) => ch switch
  {
    'h' => true,
    'j' => true,
    'k' => true,
    'l' => true,
    'y' => true,
    'u' => true,
    'b' => true,
    'n' => true,
    _ => false
  };

  static (int, int) KeyToDir(char ch) => ch switch
  {
    'h' => (0, -1),
    'j' => (1, 0),
    'k' => (-1, 0),
    'l' => (0, 1),
    'y' => (-1, -1),
    'u' => (-1, 1),
    'b' => (1, -1),
    _ => (1, 1)
  };

  static char DirToKey((int, int) dir) => dir switch
  {
    (0, -1) => 'h',
    (1, 0) => 'j',
    (-1, 0) => 'k',
    (0, 1) => 'l',
    (-1, -1) => 'y',
    (-1, 1) => 'u',
    (1, -1) => 'b',
    _ => 'n'
  };

  MoveAction CalcMovementAction(GameState gs, char ch)
  {
    if (HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer(new Message("You are confused!", Loc), "", gs);
      char[] dirs = ['h', 'j', 'k', 'l', 'y', 'u', 'b', 'n'];
      ch = dirs[gs.Rng.Next(dirs.Length)];
    }

    (int dr, int dc) = KeyToDir(ch);

    return new MoveAction(gs, this, Loc.Move(dr, dc));
  }

  // 'Running' just means repeated moving
  NullAction StartRunning(GameState gs, char ch)
  {
    if (HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer(new Message("You are too confused!", Loc), "", gs);
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
        case TileType.Statue:
        case TileType.Upstairs:
        case TileType.Downstairs:
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

  public override Action TakeTurn(GameState gameState)
  {
    UserInterface ui = gameState.UIRef();

    if (HasActiveTrait<ParalyzedTrait>())
    {
      gameState.UIRef().AlertPlayer(new Message("You cannot move!", Loc), "", gameState);
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
      if (_accumulator is not null)
      {
        _accumulator.Input(ch);
        if (!_accumulator.Done)
        {
          if (_accumulator.Msg != "")
            ui.SetPopup(new Popup(_accumulator.Msg, "", -1, -1));
          return new NullAction();
        }
        else
        {
          if (_accumulator.Success)
          {
            _deferred.ReceiveUIResult(_accumulator.GetResult());
            _accumulator = null;
            ui.ClosePopup();
            return _deferred;
          }
          else
          {
            _accumulator = null;
            ui.CloseMenu();
            ui.ClosePopup();
            ui.AlertPlayer([new Message("Nevermind.", gameState.Player.Loc)], "", gameState);
            return new NullAction();
          }
        }
      }

      ui.ClosePopup();

      if (IsMoveKey(ch))
        return CalcMovementAction(gameState, ch);
      else if (IsMoveKey(char.ToLower(ch)))
        return StartRunning(gameState, ch);
      else if (ch == 'E')
        return new PortalAction(gameState);
      else if (ch == '>')
        return new DownstairsAction(gameState);
      else if (ch == '<')
        return new UpstairsAction(gameState);
      else if (ch == 'i')
      {
        ShowInventory(ui, "You are carrying:", "", true);
        _accumulator = new PauseForMoreInputer();
        _deferred = new CloseMenuAction(gameState);
      }
      else if (ch == ',')
      {
        var itemStack = gameState.ObjDb.ItemsAt(Loc);

        if (itemStack is null || itemStack.Count == 0)
        {
          ui.AlertPlayer([new Message("There's nothing there...", gameState.Player.Loc)], "", gameState);
          return new NullAction();
        }
        else if (itemStack.Count == 1)
        {
          var a = new PickupItemAction(gameState, this);
          // A bit kludgy but this sets up the Action as though
          // the player had selected the first item in a list of one
          var r = new ObjIdUIResult() { ID = itemStack[0].ID };
          a.ReceiveUIResult(r);
          return a;
        }
        else
        {
          var opts = ShowPickupMenu(ui, itemStack);
          _accumulator = new PickUpper(opts);
          _deferred = new PickupItemAction(gameState, this);
        }
      }
      else if (ch == 'a')
      {
        ShowInventory(ui, "Use which item?", "");
        _accumulator = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new UseItemAction(gameState, this);
      }
      else if (ch == 'd')
      {
        ShowInventory(ui, "Drop what?", "", true);
        HashSet<char> slots = [.. Inventory.UsedSlots()];
        slots.Add('$');
        _accumulator = new Inventorier(slots);
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
          ShowInventory(ui, "Fire what?", instructions);
          _accumulator = new Inventorier([.. Inventory.UsedSlots()]);
          _deferred = new FireSelectedBowAction(gameState, this);
        }
      }
      else if (ch == 't')
      {
        // Eventually I'll want to remember the last item thrown
        // so the player doesn't need to always select an item if
        // they're throwing draggers several turns in a row
        string instructions = "* Use move keys to move to target\n  or TAB through targets;\n  Enter to select or ESC to abort *";
        ShowInventory(ui, "Throw what?", instructions);
        _accumulator = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ThrowSelectionAction(gameState, this);
      }
      else if (ch == 'e')
      {
        _accumulator = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ToggleEquipedAction(gameState, this);
        ShowInventory(ui, "Equip what?", "");
      }
      else if (ch == 'c')
      {
        _accumulator = new DirectionalInputer();
        _deferred = new CloseDoorAction(gameState, this, gameState.CurrMap);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'C')
      {
        _accumulator = new DirectionalInputer();
        _deferred = new ChatAction(gameState, this);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'o')
      {
        _accumulator = new DirectionalInputer();
        _deferred = new OpenDoorAction(gameState, this, gameState.CurrMap);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'Q')
      {
        _accumulator = new YesOrNoInputer();
        _deferred = new QuitAction();
        ui.SetPopup(new Popup("Really quit?\n\nYour game won't be saved! (y/n)", "", -1, -1));
      }
      else if (ch == 'S')
      {
        _accumulator = new YesOrNoInputer();
        _deferred = new SaveGameAction();
        ui.SetPopup(new Popup("Quit & Save? (y/n)", "", -1, -1));
      }
      else if (ch == '*')
      {
        var lines = ui.MessageHistory.Select(m => m.Fmt);
        _accumulator = new LongMessagerInputer(ui, lines);
        _deferred = new NullAction();
      }
      else if (ch == '@')
      {
        var lines = CharacterSheet();
        _accumulator = new LongMessagerInputer(ui, lines);
        _deferred = new NullAction();
      }
      else if (ch == 'M')
      {
        if (gameState.CurrDungeonID == 0)
          ui.AlertPlayer([new Message("Not in the wilderness.", Loc)], "", gameState);
        else
          gameState.UIRef().DisplayMapView(gameState);
      }
      else if (ch == '?')
      {
        _accumulator = new HelpScreenInputer(gameState.UIRef());
        _deferred = new NullAction();
      }
      else if (ch == 'X')
      {
        _accumulator = new Examiner(gameState, Loc);
        _deferred = new NullAction();
      }
      else if (ch == ' ' || ch == '.')
        return new PassAction();
    }

    return new NullAction();
  }

  public void EventAlert(GameEventType eventType, GameState gs)
  {
    if (eventType == GameEventType.MobSpotted)
      Running = false;
  }
}
