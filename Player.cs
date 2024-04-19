
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

enum PlayerClass
{
  OrcReaver,
  DwarfStalwart
}

class Player : Actor, IPerformer, IGameEventListener
{
  public const int MAX_VISION_RADIUS = 25;
  public PlayerClass CharClass { get; set; }

  InputAccumulator? _accumulator;
  Action? _deferred;
  public bool Running { get; set; } = false;
  char RepeatingCmd { get; set; }
  HashSet<Loc> LocsRan { get; set; } = [];

  public Player(string name)
  {
    Name = name;
    Recovery = 1.0; // Do I want a 'NaturalRecovery' or such to track cases when
                    // when a Player's recover is bolstered by, like, a Potion of Speed or such?        
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

    if (Stats.TryGetValue(Attribute.MissileAttackBonus, out var missibleAttackBonus))
      mod += missibleAttackBonus.Curr;

    AttackTrait? attackTrait = (AttackTrait?)weapon.Traits
                                                .Where(t => t is AttackTrait)
                                                .FirstOrDefault()
                                        ?? new AttackTrait() { Bonus = 0 };
    mod += attackTrait.Bonus;

    return mod;
  }

  public override int TotalMeleeAttackModifier()
  {
    int mod = Stats[Attribute.Strength].Curr;

    var weapon = Inventory.ReadiedWeapon();
    if (weapon is not null)
    {
      AttackTrait? attackTrait = (AttackTrait?)weapon.Traits
                                                      .Where(t => t is AttackTrait)
                                                      .FirstOrDefault()
                                      ?? new AttackTrait() { Bonus = 0 };
      mod += attackTrait.Bonus;
    }

    if (Stats.TryGetValue(Attribute.MeleeAttackBonus, out var meleeAttackBonus))
      mod += meleeAttackBonus.Curr;

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

  static HashSet<char> ShowPickupMenu(UserInterface ui, List<Item> items)
  {
    HashSet<char> options = [];
    List<string> lines = ["What do you pick up?"];
    char slot = 'a';
    foreach (var item in items)
    {
      options.Add(slot);
      string desc = item.Name; // ItemMenuDesc(item);
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

    lines.Add($"{Name}, a level {Stats[Attribute.Level].Curr} {Util.PlayerClassToStr(CharClass)}");
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

  public void ReplacePendingAction(Action newAction, InputAccumulator newAccumulator)
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
    (0, -1)  => 'h',
    (1, 0)   => 'j',
    (-1, 0)  => 'k',
    (0, 1)   => 'l',
    (-1, -1) => 'y',
    (-1, 1)  => 'u',
    (1, -1)  => 'b',
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
      'h' => [ (-1, -1), (0, -1), (1, -1), (1, 0), (-1, 0)],
      'j' => [ (1, -1), (1, 0), (1, 1), (0, 1), (0, -1)],
      'k' => [(-1, -1), (-1, 0), (-1, 1), (0, 1), (0, -1)],
      'l' => [ (-1, 1), (0, 1), (1, 1), (1, 0), (-1, 0)],
      'y' => [ (0, -1), (-1, -1), (-1, 0), (1, -1), (-1, 1) ],
      'u' => [ (-1, 0), (-1, 1), (0, 1), (-1, -1), (1, 1) ],
      'b' => [ (0, -1), (1, -1), (1, 0), (1, -1), (1, 1) ],
      _ => [ (1, 0), (1, 1), (0, 1), (1, -1), (-1, 1) ]
    };

    return next.Select(n => Loc with {  Row = Loc.Row + n.Item1, Col = Loc.Col + n.Item2 })
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

  public override Action TakeTurn(UserInterface ui, GameState gameState)
  {
    if (HasActiveTrait<ParalyzedTrait>())
    {
      gameState.UIRef().AlertPlayer(new Message("You cannot move!", Loc), "", gameState);
      return new PassAction(gameState, this);
    }

    char ch = '\0';

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
            ui.Popup(_accumulator.Msg);
          return new NullAction();
        }
        else
        {
          if (_accumulator.Success)
          {
            _deferred.ReceiveAccResult(_accumulator.GetResult());
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
        _accumulator = new PauseForMoreAccumulator();
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
          var mr = new MenuAccumulatorResult() { Choice = 'a' };
          a.ReceiveAccResult(mr);
          return a;
        }
        else
        {
          var opts = ShowPickupMenu(ui, itemStack);
          _accumulator = new InventoryAccumulator(opts);
          _deferred = new PickupItemAction(gameState, this);
        }
      }
      else if (ch == 'a')
      {
        ShowInventory(ui, "Use which item?", "");
        _accumulator = new InventoryAccumulator([.. Inventory.UsedSlots()]);
        _deferred = new UseItemAction(gameState, this);
      }
      else if (ch == 'd')
      {
        ShowInventory(ui, "Drop what?", "", true);
        HashSet<char> slots = [.. Inventory.UsedSlots()];
        slots.Add('$');
        _accumulator = new InventoryAccumulator(slots);
        _deferred = new DropItemAction(gameState, this);
      }
      else if (ch == 't')
      {
        // Eventually I'll want to remember the last item thrown
        // so the player doesn't need to always select an item if
        // they're throwing draggers several turns in a row
        string instructions = "* Use move keys to move to target\n  or TAB through targets;\n  Enter to select or ESC to abort *";
        ShowInventory(ui, "Throw what?", instructions);
        _accumulator = new InventoryAccumulator([.. Inventory.UsedSlots()]);
        _deferred = new ThrowSelectionAction(gameState, this);
      }
      else if (ch == 'e')
      {
        _accumulator = new InventoryAccumulator([.. Inventory.UsedSlots()]);
        _deferred = new ToggleEquipedAction(gameState, this);
        ShowInventory(ui, "Equip what?", "");
      }
      else if (ch == 'c')
      {
        _accumulator = new DirectionAccumulator();
        _deferred = new CloseDoorAction(gameState, this, gameState.Map);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'C')
      {
        _accumulator = new DirectionAccumulator();
        _deferred = new ChatAction(gameState, this);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'o')
      {
        _accumulator = new DirectionAccumulator();
        _deferred = new OpenDoorAction(gameState, this, gameState.Map);
        ui.AlertPlayer([new Message("Which way?", gameState.Player.Loc)], "", gameState);
      }
      else if (ch == 'Q')
      {
        _accumulator = new YesNoAccumulator();
        _deferred = new QuitAction();
        ui.Popup("Really quit?\n\nYour game won't be saved! (y/n)");
      }
      else if (ch == 'S')
      {
        _accumulator = new YesNoAccumulator();
        _deferred = new SaveGameAction();
        ui.Popup("Quit & Save? (y/n)");
      }
      else if (ch == '*')
      {
        var lines = ui.MessageHistory.Select(m => m.Fmt);
        _accumulator = new LongMessageAccumulator(ui, lines);
        _deferred = new NullAction();
      }
      else if (ch == '@')
      {
        var lines = CharacterSheet();
        _accumulator = new LongMessageAccumulator(ui, lines);
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
        var time = gameState.CurrTime();
        var hour = time.Item1.ToString().PadLeft(2, '0');
        var minute = time.Item2.ToString().PadLeft(2, '0');
        var msg = new Message($"The current time is {hour}:{minute}", Loc);
        gameState.Turn += 1000;
        ui.AlertPlayer([msg], "", gameState);
      }
      else if (ch == 'W')
      {
        foreach (var sq in Util.Adj8Sqs(Loc.Row, Loc.Col))
        {
          if (gameState.Rng.NextDouble() < 0.666)
          {
            var w = ItemFactory.Web();
            gameState.ObjDb.Add(w);
            gameState.ItemDropped(w, Loc with { Row = sq.Item1, Col = sq.Item2 });
          }
        }
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
