
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

class PlayerCreator
{
  static PlayerLineage PickLineage(UserInterface ui)
  {
    List<string> menu = [
      "To create a new character, first please select your lineage:",
      "",
      " (1) Human\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t(2) Orc",
      "",
      " Your standard homo sapien. Thanks\t\t\t\t\t\tNaturally strong, orcs receive ",
      " to progressive tax policy in this\t\t\t\t\t\tbonus hit points. Due to participating",
      " world of fantastical beings,\t\t\t\t\t\t\t\t\t\t\tin orc hockey as a child, you start",
      " humans begin with more gold.\t\t\t\t\t\t\t\t\t\t\twith the ability to Rage.",
      "                              ",
      "",
      " (3) Elf\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t(4) Dwarf",
      "",
      " Magical, ethereal, fond of wine\t\t\t\t\t\t\t\tStout and bearded. Dwarves in Delve",
      " and grammar. Elves also benefit\t\t\t\t\t\t\t\tabsoultely do not have Scottish",
      " from mandatory archery classes\t\t\t\t\t\t\t\t\taccents. They are less prone to illness",
      " in Elf Grade School.\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tand poison, and accrue stress more",
      "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tslowly when exploring dungeons."
    ];
    HashSet<char> options = [ '1', '2', '3', '4'  ];
    char choice = ui.FullScreenMenu(menu, options, null);

    return choice switch
    {
      '1' => PlayerLineage.Human,
      '2' => PlayerLineage.Orc,
      '3' => PlayerLineage.Elf,
      _ => PlayerLineage.Dwarf
    };

  }

  static PlayerBackground PickBackground(UserInterface ui)
  {
    List<string> menu = [
     "What was your major in Adventurer College?",
      "",
      " (1) Warrior\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t(2) Scholar",
      "",
      " A broad curriculum in beating\t\t\t\t\t\t\t\t\tYou spent your time in college hitting",
      " people up has left you skilled in\t\t\t\t\tthe books. You passed Magic 101 and",
      " most weapons, as well as strong\t\t\t\t\t\t\tthanks to your studies and student",
      " and tough.\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\tfrugality, when you read scrolls there's",
      "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\ta small chance they won't be consumed.",
      "",
      "\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t\t(weaker than the others in this version)",
      "",
      " (3) Skullduggery                    ",
      "",
      " Most of your time in school was     ",
      " spent sneaking into parties you    ",
      " weren't invited to. Skills which",
      " translate directly to adventuring."
    ];

    HashSet<char> options = ['1', '2', '3'];
    char choice = ui.FullScreenMenu(menu, options, null);

    return choice switch
    {
      '1' => PlayerBackground.Warrior,
      '2' => PlayerBackground.Scholar,
      _ => PlayerBackground.Skullduggery
    };
  }

  // If I use this enough, move it to Utils?
  static int Roll3d6(Rng rng) => rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
  static int StatRoll(Rng rng) => Util.StatRollToMod(Roll3d6(rng));

  static Dictionary<Attribute, Stat> RollStats(PlayerLineage lineage, PlayerBackground background, Rng rng)
  {
    // First, set the basic stats
    var stats = new Dictionary<Attribute, Stat>()
    {
      { Attribute.Strength, new Stat(StatRoll(rng)) },
      { Attribute.Constitution, new Stat(StatRoll(rng)) },
      { Attribute.Dexterity, new Stat(StatRoll(rng)) },      
      { Attribute.Will, new Stat(StatRoll(rng)) },
      { Attribute.Depth, new Stat(0) },
      { Attribute.Nerve, new Stat(1250) },
      { Attribute.LastBlessing, new Stat(0) },
      { Attribute.BaseHP, new Stat(10) },
      { Attribute.Piety, new Stat(0) }
    };

    int roll;

    switch (lineage)
    {
      case PlayerLineage.Orc:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Strength].Curr)
          stats[Attribute.Strength].SetMax(roll);
        break;
      case PlayerLineage.Elf:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Dexterity].Curr)
          stats[Attribute.Dexterity].SetMax(roll);
        stats.Add(Attribute.ArcheryBonus, new Stat(2));
        break;
      case PlayerLineage.Dwarf:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Constitution].Curr)
          stats[Attribute.Constitution].SetMax(roll);
        break;
    }

    switch (background)
    {
      case PlayerBackground.Warrior:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Strength].Curr)
          stats[Attribute.Strength].SetMax(roll);
        break;
      case PlayerBackground.Skullduggery:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Dexterity].Curr)
          stats[Attribute.Dexterity].SetMax(roll);
        break;
      case PlayerBackground.Scholar:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Will].Curr)
          stats[Attribute.Will].SetMax(roll);
        stats.Add(Attribute.MagicPoints, new Stat(rng.Next(2, 5)));
        break;      
    }
    
    stats.Add(Attribute.HP, new Stat(1));

    return stats;
  }

  public static void SetInitialAbilities(Player player)
  {
    switch (player.Lineage)
    {
      case PlayerLineage.Orc:
        player.Traits.Add(new RageTrait(player));
        break;
    }

    switch (player.Background)
    {
      case PlayerBackground.Skullduggery:
        player.Traits.Add(new LightStepTrait());
        break;
      case PlayerBackground.Scholar:
        player.SpellsKnown.Add("arcane spark");
        break;
    }
  }

  static void StartingGearForScholar(Player player, GameObjectDB objDb, Rng rng)
  {
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
  }

  public static void SetStartingGear(Player player, GameState gs, Rng rng)
  {
    if (player.Background == PlayerBackground.Scholar)
    {
      StartingGearForScholar(player, gs.ObjDb, rng);
      return;
    }

    char slot;
    Item leather = ItemFactory.Get(ItemNames.LEATHER_ARMOUR, gs.ObjDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Slot = 'b';

    Item startWeapon;

    switch (player.Lineage)
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
        player.Stats.Add(Attribute.FinesseUse, new Stat(100));
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

    if (player.Background == PlayerBackground.Skullduggery)
    {
      startWeapon = ItemFactory.Get(ItemNames.DAGGER, gs.ObjDb);
      if (!player.Stats.ContainsKey(Attribute.FinesseUse))
        player.Stats.Add(Attribute.FinesseUse, new Stat(100));
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

  public static Player NewPlayer(string playerName, GameState gs, int startRow, int startCol, UserInterface ui, Rng rng)
  {
    PlayerLineage lineage = PickLineage(ui);
    PlayerBackground background = PickBackground(ui);

    Player player = new(playerName)
    {
      Loc = new Loc(0, 0, startRow, startCol),
      Lineage = lineage,
      Background = background,
      Energy = 1.0,
      ID = 1
    };
    player.Stats = RollStats(player.Lineage, player.Background, rng);
    player.Stats[Attribute.MainQuestState] = new Stat(0);
    player.Traits.Add(new SwimmerTrait());
    player.Inventory = new Inventory(player.ID, gs.ObjDb);

    gs.ObjDb.Add(player);

    SetInitialAbilities(player);
    SetStartingGear(player, gs, rng);

    // Humans start with a little more money than the others
    if (lineage == PlayerLineage.Human)
    {
      Item money = ItemFactory.Get(ItemNames.ZORKMIDS, gs.ObjDb);
      money.Value = 20;
      player.Inventory.Add(money, player.ID);
    }

    player.CalcHP();

    return player;
  }
}