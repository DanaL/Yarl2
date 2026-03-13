
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

class PlayerCreator
{
  public static (Player?, SetupResult) NewPlayer(string playerName, GameState gs, int startRow, int startCol, UserInterface ui, Rng rng)
  {
    var (lineage, res) = PickLineage(ui);
    if (res == SetupResult.Quit || res == SetupResult.Cancel)
      return (null, res);
    var (background, bkgrRes) = PickBackground(ui);
    if (bkgrRes == SetupResult.Quit || bkgrRes == SetupResult.Cancel)
      return (null, res);
      
    Player player = new(playerName)
    {
      Loc = new Loc(0, 0, startRow, startCol),
      Lineage = lineage,
      Background = background,
      Energy = 1.0,
      ID = Constants.PLAYER_ID,
      Stats = new Dictionary<Attribute, Stat>()
      {
        { Attribute.Depth, new Stat(0) },
        { Attribute.Nerve, new Stat(1250) },
        { Attribute.BaseHP, new Stat(10) },
        { Attribute.HP, new Stat(1) }
      }
    };

    player.Stats[Attribute.MainQuestState] = new Stat(0);
    player.Traits.Add(new SwimmerTrait());
    player.Inventory = new Inventory(player.ID, gs.ObjDb);

    gs.ObjDb.Add(player);

    switch (background)
    {
      case PlayerBackground.Warrior:
        CreateWarrior(lineage, player, gs, rng);
        break;
      case PlayerBackground.Skullduggery:
        CreateRogue(player, gs, rng);
        break;
      case PlayerBackground.Scholar:
        CreateScholar(player, gs.ObjDb, rng);
        break;
    }

    switch (lineage)
    {
      case PlayerLineage.Orc:
        player.Stats[Attribute.Strength].ChangeMax(1);
        player.Stats[Attribute.Strength].Reset();
        player.Traits.Add(new RageTrait(player));
        break;
      case PlayerLineage.Elf:
        player.Stats[Attribute.Dexterity].ChangeMax(1);
        player.Stats[Attribute.Dexterity].Reset();
        player.Stats.Add(Attribute.ArcheryBonus, new Stat(2));
        if (!player.Stats.ContainsKey(Attribute.FinesseUse))
          player.Stats.Add(Attribute.FinesseUse, new Stat(100));
        break;
      case PlayerLineage.Dwarf:
        player.Stats[Attribute.Constitution].ChangeMax(1);
        player.Stats[Attribute.Constitution].Reset();
        break;
    }
     
    // Humans start with a little more money than the others
    if (lineage == PlayerLineage.Human)
    {
      Item money = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
      money.Value = 40;
      player.Inventory.Add(money, player.ID);
    }

    player.CalcHP();

    PlayerRegenTrait prt = new();
    prt.Apply(player, gs);

    player.Stats[Attribute.HP].Reset();
    
    return (player, SetupResult.Success);
  }

  static readonly int[] _basicStatArray = [-2, -1, -1, -1, 0, 0, 0, 0, 0, 1, 1, 1, 2, 2];
  static void CreateWarrior(PlayerLineage lineage, Player player, GameState gs, Rng rng)
  {
    player.Stats[Attribute.Strength] = new Stat(int.Min(2, rng.Next(0, 4)));
    player.Stats[Attribute.Constitution] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length)]);
    player.Stats[Attribute.Dexterity] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length)]);
    player.Stats[Attribute.Will] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length - 2)]);

    char slot;
    Item leather = ItemFactory.Get(ItemNames.LEATHER_ARMOUR, gs.ObjDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Slot = 'b';

    Item startWeapon;

    switch (lineage)
    {
      case PlayerLineage.Orc:
        startWeapon = ItemFactory.Get(ItemNames.SHORTSHORD, gs.ObjDb);
        slot = player.Inventory.Add(leather, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        player.Stats.Add(Attribute.SwordUse, new Stat(100));
        break;
      case PlayerLineage.Dwarf:
        startWeapon = ItemFactory.Get(ItemNames.HAND_AXE, gs.ObjDb);
        Item studded = ItemFactory.Get(ItemNames.RINGMAIL, gs.ObjDb);
        studded.Slot = 'b';
        slot = player.Inventory.Add(studded, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        Item helmet = ItemFactory.Get(ItemNames.HELMET, gs.ObjDb);
        helmet.Slot = 'c';
        slot = player.Inventory.Add(helmet, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        player.Stats.Add(Attribute.AxeUse, new Stat(100));
        break;
      case PlayerLineage.Elf:
        startWeapon = ItemFactory.Get(ItemNames.DAGGER, gs.ObjDb);
        Item bow = ItemFactory.Get(ItemNames.LONGBOW, gs.ObjDb);
        bow.Slot = 'b';
        slot = player.Inventory.Add(bow, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        player.Stats.Add(Attribute.BowUse, new Stat(100));        
        leather.Slot = 'c';
        slot = player.Inventory.Add(leather, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        break;
      default:
        startWeapon = ItemFactory.Get(ItemNames.SPEAR, gs.ObjDb);
        startWeapon.Traits.Add(new AdjectiveTrait("old"));
        slot = player.Inventory.Add(leather, player.ID);
        player.Inventory.ToggleEquipStatus(slot);
        player.Stats.Add(Attribute.PolearmsUse, new Stat(100));
        break;
    }

    startWeapon.Slot = 'a';
    slot = player.Inventory.Add(startWeapon, player.ID);
    player.Inventory.ToggleEquipStatus(slot);

    // Everyone gets 3 to 5 torches to start with
    for (int i = 0; i < rng.Next(3, 6); i++)
    {
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), player.ID);
    }

    Item money = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
    money.Value = rng.Next(25, 51);
    player.Inventory.Add(money, player.ID);

    foreach (Item item in player.Inventory.Items())
    {
      if (item.Equipped && item.HasTrait<GrantsTrait>())
      {
        foreach (Trait t in item.Traits)
        {
          if (t is GrantsTrait grants)
            grants.Grant(player, gs, item);
        }
      }
    }
  }

  static void CreateScholar(Player player, GameObjectDB objDb, Rng rng)
  {
    player.Stats[Attribute.Will] = new Stat(int.Min(2, rng.Next(0, 4)));
    player.Stats[Attribute.Constitution] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length)]);
    player.Stats[Attribute.Dexterity] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length)]);
    player.Stats[Attribute.Strength] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length - 2)]);
    player.Stats.Add(Attribute.MagicPoints, new Stat(rng.Next(2, 5)));
    player.SpellsKnown.Add("arcane spark");

    char slot;

    Item dagger = ItemFactory.Get(ItemNames.DAGGER, objDb);
    slot = player.Inventory.Add(dagger, player.ID);
    player.Inventory.ToggleEquipStatus(slot);

    Item focus = ItemFactory.Get(ItemNames.GENERIC_WAND, objDb);
    slot = player.Inventory.Add(focus, player.ID);
    player.Inventory.ToggleEquipStatus(slot);

    for (int i = 0; i < rng.Next(3, 6); i++)
    {
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), player.ID);
    }

    // Scholars start off with less money because they are still paying off
    // their student loans
    Item money = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
    money.Value = rng.Next(10, 26);
    player.Inventory.Add(money, player.ID);

    Item scroll = rng.Next(3) switch
    {
      0 => ItemFactory.Get(ItemNames.SCROLL_SCATTERING, objDb),
      1 => ItemFactory.Get(ItemNames.SCROLL_PROTECTION, objDb),
      _ => ItemFactory.Get(ItemNames.SCROLL_BLINK, objDb)
    };
    player.Inventory.Add(scroll, player.ID);
  }

  static void CreateRogue(Player player, GameState gs, Rng rng)
  {
    player.Stats[Attribute.Dexterity] = new Stat(int.Min(2, rng.Next(0, 4)));
    player.Stats[Attribute.Constitution] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length)]);
    player.Stats[Attribute.Strength] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length - 2)]);
    player.Stats[Attribute.Will] = new Stat(_basicStatArray[rng.Next(_basicStatArray.Length - 2)]);
    player.Traits.Add(new LightStepTrait());
    
    if (!player.Stats.ContainsKey(Attribute.FinesseUse))
      player.Stats.Add(Attribute.FinesseUse, new Stat(100));
    
    Item startWeapon = ItemFactory.Get(ItemNames.DAGGER, gs.ObjDb);    
    startWeapon.Slot = 'a';
    player.Inventory.Add(startWeapon, player.ID);
    player.Inventory.ToggleEquipStatus('a');

    Item leather = ItemFactory.Get(ItemNames.LEATHER_ARMOUR, gs.ObjDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Slot = 'b';
    player.Inventory.Add(leather, player.ID);
    player.Inventory.ToggleEquipStatus('b');

    Item lockpick = ItemFactory.Get(ItemNames.LOCK_PICK, gs.ObjDb);
    player.Inventory.Add(lockpick, player.ID);

    // Everyone gets 3 to 5 torches to start with
    for (int i = 0; i < rng.Next(3, 6); i++)
    {
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, gs.ObjDb), player.ID);
    }

    Item money = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
    money.Value = rng.Next(25, 51);
    player.Inventory.Add(money, player.ID);

    foreach (Item item in player.Inventory.Items())
    {
      if (item.Equipped && item.HasTrait<GrantsTrait>())
      {
        foreach (Trait t in item.Traits)
        {
          if (t is GrantsTrait grants)
            grants.Grant(player, gs, item);
        }
      }
    }
  }

  static (PlayerLineage, SetupResult) PickLineage(UserInterface ui)
  {
    List<string> menu = [
      "To create a new character, first please select your lineage:",
      "\n",
      " (1) Human _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _(2) Orc",
      "\n",
      " Your standard homo sapien. Thanks _ _ _Naturally strong, orcs receive ",
      " to progressive tax policy in this _ _ _bonus hit points. Due to participating",
      " world of fantastical beings, _ _ _ _ _ _ _ _ in orc hockey as a child, you start",
      " humans begin with more gold. _ _ _ _ _ _ _ _ with the ability to Rage.",
      "\n",
      "\n",
      " (3) Elf _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ __ _ _ _ __ (4) Dwarf",
      "\n",
      " Magical, ethereal, fond of wine _ _ __ _Stout and bearded. Dwarves in Delve",
      " and grammar. Elves also benefit _ _ _ _ _absolutely do not have Scottish",
      " from mandatory archery classes _ _ _ _ _ _accents. They are less prone to illness",
      " in Elf Grade School. _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _and poison, and accrue stress more",
      " _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ _ ________ _ _ _ _ _ _ _ _ _slowly when exploring dungeons."
    ];
    HashSet<char> options = [ '1', '2', '3', '4'  ];
    char choice = ui.FullScreenMenu(menu, options, null);

    if (choice == Constants.QUIT_SIGNAL)
      return (PlayerLineage.Human, SetupResult.Quit);
    
    if (choice == Constants.ESC)
      return (PlayerLineage.Human, SetupResult.Cancel);

    return choice switch
    {
      '1' => (PlayerLineage.Human, SetupResult.Success),
      '2' => (PlayerLineage.Orc, SetupResult.Success),
      '3' => (PlayerLineage.Elf, SetupResult.Success),
      _ => (PlayerLineage.Dwarf, SetupResult.Success)
    };
  }

  static (PlayerBackground, SetupResult) PickBackground(UserInterface ui)
  {
List<string> menu = [
     "What was your major in Adventurer College?",
      "\n",
      " (1) Warrior __________________________ [darkgrey (2) Scholar]",
      "\n",
      " A broad curriculum in beating ________ You spent your time in college hitting",
      " people up has left you skilled in ____ the books. You passed Magic 101 and",
      " most weapons, as well as strong ______ thanks to your studies and student",
      " and tough. ___________________________ frugality, when you read scrolls there's",
      "______________________________________ a small chance they won't be consumed.",
      "\n",
      "______________________________________ (weaker than the others in this version)",
      "\n",
      " (3) Skullduggery",
      "\n",
      " Most of your time in school was",
      " spent sneaking into parties you",
      " weren't invited to. Skills which",
      " translate directly to adventuring."
    ];

    HashSet<char> options = ['1', '2', '3'];
    char choice = ui.FullScreenMenu(menu, options, null);

    if (choice == Constants.QUIT_SIGNAL)
      return (PlayerBackground.Warrior, SetupResult.Quit);
    
    if (choice == Constants.ESC)
      return (PlayerBackground.Warrior, SetupResult.Cancel);

    return choice switch
    {
      '1' => (PlayerBackground.Warrior, SetupResult.Success),
      '2' => (PlayerBackground.Scholar, SetupResult.Success),
      _ => (PlayerBackground.Skullduggery, SetupResult.Success)
    };
  }
}