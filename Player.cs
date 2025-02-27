
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

enum StressLevel
{
  None,
  Skittish,
  Nervous,
  Anxious,
  Paranoid,
  Hystrical
}

class Player : Actor
{
  public const int MAX_VISION_RADIUS = 25;
  public PlayerLineage Lineage { get; set; }
  public PlayerBackground Background { get; set; }
  public List<string> SpellsKnown = [];
  public string LastSpellCast = "";
  
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
        if (item is not null && item.Equipped)
        {
          armour += item.Traits.OfType<ArmourTrait>()
                               .Select(t => t.ArmourMod + t.Bonus)
                               .Sum();
          armour += item.Traits.OfType<ACModTrait>()
                               .Select(t => t.ArmourMod)
                               .Sum();
        }
      }

      foreach (Trait t in Traits)
      {
        if (t is ACModTrait acMod)
          ac += acMod.ArmourMod;
        if (t is MageArmourTrait ma)
          ac += 3;
      }

      // Anxious is sort of a sweet spot where I picture the character going 
      // very defnesive to protect themselves, but with even more stress they
      // are beginning to lose it
      var (_, stress) = StressPenalty();
      ac += stress switch 
      {
        StressLevel.Anxious => 1,
        StressLevel.Paranoid => -1,
        StressLevel.Hystrical => -2,
        _ => 0
      };

      return ac + armour;
    }
  }

  public override Actor PickTarget(GameState gs)
  {
    throw new NotImplementedException();
  }
  public override Loc PickTargetLoc(GameState gamestate) => Loc;
  public override Loc PickRangedTargetLoc(GameState gamestate) => Loc;
 
  public override int SpellDC
  {
    get 
    {
      int dc = 12 + Stats[Attribute.Will].Curr;

      // Eventually items and other stuff will affect this value

      return dc;
    }
  }

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
      if (t is HeroismTrait)
        baseHP += 25;            
    }

    // We won't allow an HP buffs and debuffs to kill a character, just make
    // them very very weak
    if (baseHP < 1)
      baseHP = 1;

    Stats[Attribute.HP].SetMax(baseHP);
  }

  public void CalcStress()
  {
    int nerve = Stats[Attribute.Nerve].Curr;
    StressLevel stress;
    if (nerve > 750)
      stress = StressLevel.None;
    else if (nerve > 600)
      stress = StressLevel.Skittish;
    else if (nerve > 450)
      stress = StressLevel.Nervous;
    else if (nerve > 300)
      stress = StressLevel.Anxious;
    else if (nerve > 150)
      stress = StressLevel.Paranoid;
    else
      stress = StressLevel.Hystrical;
    
    StressTrait? current = Traits.OfType<StressTrait>().FirstOrDefault();
    if (stress == StressLevel.None)
    {
      if (current is not null)
        Traits.Remove(current);
    }
    else if (current is null)
    {
      current = new StressTrait() { Stress = stress, OwnerID = ID };
      Traits.Add(current);
    }
    else
    {
      current.Stress = stress;
    }
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
          DamageTrait dt = Inventory.ShieldEquipped() ? versatile.OneHanded : versatile.TwoHanded;
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

    foreach (Trait t in Traits)
    {
      if (t is BerzerkTrait)
      {
        dmgs.Add(new Damage(10, 1, DamageType.Force));
      }

      // A player might have general damage sources from items and such
      if (t is DamageTrait dt)
      {
        dmgs.Add(new Damage(dt.DamageDie, dt.NumOfDie, dt.DamageType));
      }
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
    bool blessed = false;
    int acmod = 0;
    int attackMod = 0;
    int meleeDmgMod = 0;
    bool quiet = false;
    bool fireDmg = false;
    bool fireRebuke = false;
    foreach (Trait trait in Traits)
    {
      if (trait is RageTrait)
        traitsToShow.Add("You have the ability to rage");
      else if (trait is LightStepTrait)
        traitsToShow.Add("You step lightly");
      else if (trait is DodgeTrait)
        traitsToShow.Add("You sometimes can dodge attacks");
      else if (trait is CutpurseTrait)
        traitsToShow.Add("You have sticky fingers");
      else if (trait is AlacrityTrait alacrityTrait)
        alacrity -= alacrityTrait.Amt;
      else if (trait is StressTrait st)
        traitsToShow.Add($"You are feeling {st.Stress.ToString().ToLower()}");
      else if (trait is FeatherFallTrait)
        traitsToShow.Add("You have feather fall");
      else if (trait is FrighteningTrait)
        traitsToShow.Add("You can frighten your foes");
      else if (trait is BlessingTrait)
        blessed = true;
      else if (trait is ACModTrait acm)
        acmod += acm.ArmourMod;
      else if (trait is AttackModTrait attm)
        attackMod += attm.Amt;
      else if (trait is MeleeDamageModTrait mdm)
        meleeDmgMod += mdm.Amt;
      else if (trait is QuietTrait)
        quiet = true;
      else if (trait is DamageTrait dt && dt.DamageType == DamageType.Fire)
        fireDmg = true;
      else if (trait is FireRebukeTrait)
        fireRebuke = true;
    }
    
    if (alacrity < 0)
      traitsToShow.Add("You are quicker than normal");
    else if (alacrity > 0)
      traitsToShow.Add("You are somewhat slowed");

    if (acmod < 0)
      traitsToShow.Add("You have a penalty to your AC");
    else if (acmod > 0)
      traitsToShow.Add("You have a bonus to your AC");

    if (attackMod < 0)
      traitsToShow.Add("You have an attack penalty");
    else if (attackMod > 0)
      traitsToShow.Add("You have an attack bonus");

    if (meleeDmgMod < 0)
      traitsToShow.Add("You deal reduced damage in melee");
    else if (meleeDmgMod > 0)
      traitsToShow.Add("You deal more damage in melee");

    if (blessed)
      traitsToShow.Add("You are blessed");

    if (quiet)
      traitsToShow.Add("You are quiet");

    if (fireDmg)
      traitsToShow.Add("You deal extra fire damage");

    if (fireRebuke)
      traitsToShow.Add("You may rebuke foes with flames");
      
    if (traitsToShow.Count > 0)
    {
      lines.Add(string.Join(". ", traitsToShow) + ".");
      lines.Add("");
    }
   
    // if (Stats.ContainsKey(Attribute.Nerve))
    // {
    //   lines.Add($"Stress: {Stats[Attribute.Nerve].Curr}");
    //   lines.Add("");
    // }

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

  public void ReplacePendingAction(Action? newAction, Inputer? inputer)
  {
    _deferred = newAction ?? _deferred;
    _inputController = inputer ?? _inputController;
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

  Action CalcMovementAction(GameState gs, char ch)
  {
    if (HasTrait<ConfusedTrait>())
    {
      gs.UIRef().AlertPlayer("You are confused!");
      char[] dirs = ['h', 'j', 'k', 'l', 'y', 'u', 'b', 'n'];
      ch = dirs[gs.Rng.Next(dirs.Length)];
    }

    if (HasTrait<TipsyTrait>() && gs.Rng.NextDouble() < 0.15)
    {
      gs.UIRef().AlertPlayer("You stagger!");
      ch = CalcStagger(gs, ch);
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

    return new BumpAction(gs, this, Loc.Move(dr, dc));
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

  void SetupSpellcastingMenu(GameState gs)
  {
    _inputController = new SpellcastMenu(gs);
    _deferred = new NullAction();
  }

  Action PickupCommand(GameState gs, UserInterface ui)
  {   
    List<Item> items = [];
    List<Item> itemsInPit = [];
    foreach (Item item in gs.ObjDb.VisibleItemsAt(Loc))
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

    if (items.Count == 0)
    {
      ui.AlertPlayer("There's nothing here you can pick up.");
      return new NullAction();
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

  public override void TakeTurn(GameState gs)
  {
    Action? action = DecideAction(gs);

    if (action is NullAction)
      return;

    // One of those calls to PrepareFieldOfView() *HAS* to be redundant
    ActionResult result;
    do
    {      
      result = action!.Execute();

      Energy -= CalcEnergyUsed(result.EnergyCost);
      if (result.AltAction is not null)
      {
        result = result.AltAction.Execute();
        Energy -= CalcEnergyUsed(result.EnergyCost);
        action = result.AltAction;
      }

      gs.PrepareFieldOfView();
    }
    while (result.AltAction is not null);
  }

  public Action DecideAction(GameState gameState)
  {
    UserInterface ui = gameState.UIRef();

    foreach (Trait t in Traits)
    {
      if (t is ParalyzedTrait paralyzed)
      {
        ++paralyzed.TurnsParalyzed;
        ui.AlertPlayer("You cannot move!");

        if (paralyzed.TurnsParalyzed % 5 == 0)
        {
          ui.SetPopup(new Popup("You are paralyzed", "", -1, -1));
          ui.BlockForInput(gameState);
          ui.ClosePopup();
        }
         
        return new PassAction(gameState, this);
      }

      if (t is RestingTrait)
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
          if (_inputController.Success && _deferred is not null)
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
        // If the player has an equipped bow, automatically select that, otherwise
        // have them pick a bow (and then equip it)
        if (Inventory.ReadiedBow() is Item bow)
        {
          FireReadedBow(bow, gameState);
        }
        else
        {          
          Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Fire what?" });
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
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Throw what?" });
        _inputController = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ThrowSelectionAction(gameState, this);
      }
      else if (ch == 'e')
      {
        _inputController = new Inventorier([.. Inventory.UsedSlots()]);
        _deferred = new ToggleEquippedAction(gameState, this);
        Inventory.ShowMenu(ui, new InventoryOptions() { Title = "Equip what?" });
      }
      else if (ch == 'c')
      {
        Loc singleDoor = SingleAdjTile(gameState, Loc, TileType.OpenDoor);
        if (singleDoor != Loc.Nowhere)
          return new CloseDoorAction(gameState, this) {  Loc = singleDoor };

        _inputController = new DirectionalInputer(gameState);
        _deferred = new CloseDoorAction(gameState, this);
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
          return new OpenDoorAction(gameState, this) { Loc = singleDoor };

        _inputController = new DirectionalInputer(gameState);
        _deferred = new OpenDoorAction(gameState, this);
      }
      else if (ch == 'Q')
      {
        _inputController = new YesOrNoInputer();
        _deferred = new QuitAction();
        ui.SetPopup(new Popup("Really quit?\n\nYour game won't be saved! (y/n)", "", -1, -1));
      }
      else if (ch == 'S' && !ui.InTutorial)
      {
        _inputController = new YesOrNoInputer();
        _deferred = new SaveGameAction();
        ui.SetPopup(new Popup("Quit & Save? (y/n)", "", -1, -1));
      }
      else if (ch == 'S' && ui.InTutorial)
      {
        ui.SetPopup(new Popup("Saving is disabled in the tutorial.", "", -1, -1));
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
        ui.CheatSheetMode = (CheatSheetMode)(x % 4);
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
      else if (ch == '=')
      {
        _inputController = new OptionsScreen(gameState);
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
      else if (ch == 'z')
      {
        
        if (SpellsKnown.Count == 0)
        {
          gameState.UIRef().SetPopup(new Popup("You don't know any spells!", "", -1, -1));
        }
        else if (!SpellcasingPrereqs())
        {
          gameState.UIRef().SetPopup(new Popup("You must have a casting focus prepared, like a wand or staff!", "", -1, -1));
        }
        else
        {
          SetupSpellcastingMenu(gameState);
        }
      }
      else if (ch == ' ' || ch == '.')
        return new PassAction();
    }

    return new NullAction();

    // Check if the player has a focus readied, or knows spells that don't need a focus
    bool SpellcasingPrereqs()
    {
      if (Inventory.FocusEquipped())
        return true;

      Item? rw = Inventory.ReadiedWeapon();
      if (rw is not null && rw.Name == "quarterstaff")
        return true;

      foreach (string spell in SpellsKnown)
      {
        if (Spells.NoFocus(spell))
          return true;
      }

      return false;
    }
  }

  public void EventAlert(GameEventType eventType, GameState gs, Loc loc)
  {
    if (eventType == GameEventType.MobSpotted)
      Running = false;
  }
}
