
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

using System.Net.NetworkInformation;

namespace Yarl2;

class PlayerCreator
{
  static PlayerLineage PickLineage(UserInterface ui)
  {
    List<string> menu = [
      "To create a new character, first please select your lineage:",
      "",
      " (1) Human                           (2) Orc",
      "",
      " Your standard homo sapien. Thanks   Naturally strong, orcs receive ",
      " to progressive tax policy in this   bonus hit points. Due to participating",
      " world of fantastical beings,        in orc hockey as a child, you start",
      " humans begin with more gold.        with the ability to Rage.",
      "                              ",
      "",
      " (3) Elf                            (4) Dwarf",
      "",
      " Magical, ethereal, fond of wine    Stout and bearded. Dwarves in Delve",
      " and grammar. Elves also benefit    absoultely do not have Scottish",
      " from mandatory archery classes     acceents. They are less prone to illness",
      " in Elf Grade School.               and poison, and accrue stress more",
      "                                    slowly when exploring dungeons."
    ];
    var options = new HashSet<char>() { '1', '2', '3', '4' };
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
      " (1) Warrior                         (2) Scholar",
      "",
      " A broad curriculum in beating       You spent your time in college hitting",
      " people up has left you skilled in   the books. You passed Magic 101 and",
      " most weapons, as well as strong     thanks to your studies and student",
      " and tough.                          frugality, when you read scrolls there's",
      "                                     a small chance they won't be consumed.",
      "",
      "                                     (not recommended in this version)",
      "",
      " (3) Skullduggery                    ",
      "",
      " Most of your time in school was     ",
      " spent sneaking into parties you    ",
      " weren't invited to. Skills which",
      " translate directly to adventuring."
    ];

    var options = new HashSet<char>() { '1', '2', '3' };
    char choice = ui.FullScreenMenu(menu, options, null);

    return choice switch
    {
      '1' => PlayerBackground.Warrior,
      '2' => PlayerBackground.Scholar,
      _ => PlayerBackground.Skullduggery
    };
  }

  // If I use this enough, move it to Utils?
  static int Roll3d6(Random rng) => rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
  static int StatRoll(Random rng) => Util.StatRollToMod(Roll3d6(rng));

  static Dictionary<Attribute, Stat> RollStats(PlayerLineage lineage, PlayerBackground background, Random rng)
  {
    // First, set the basic stats
    var stats = new Dictionary<Attribute, Stat>()
    {
      { Attribute.Strength, new Stat(StatRoll(rng)) },
      { Attribute.Constitution, new Stat(StatRoll(rng)) },
      { Attribute.Dexterity, new Stat(StatRoll(rng)) },
      { Attribute.Piety, new Stat(StatRoll(rng)) },
      { Attribute.Will, new Stat(StatRoll(rng)) },
      { Attribute.Depth, new Stat(0) },
      { Attribute.Nerve, new Stat(1250) }
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
        stats.Add(Attribute.MagicPoints, new Stat(10));
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

  static void StartingGearForScholar(Player player, GameObjectDB objDb, Random rng)
  {
    Item dagger = ItemFactory.Get(ItemNames.DAGGER, objDb);
    dagger.Equipped = true;
    player.Inventory.Add(dagger, player.ID);

    Item focus = ItemFactory.Get(ItemNames.GENERIC_WAND, objDb);
    focus.Equipped = true;
    player.Inventory.Add(focus, player.ID);

    for (int i = 0; i < rng.Next(3, 6); i++)
    {
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), player.ID);
    }

    // Scholars start off with less money because they are still paying off
    // their student loans
    var money = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
    money.Value = rng.Next(10, 26);
    player.Inventory.Add(money, player.ID);
  }

  public static void SetStartingGear(Player player, GameObjectDB objDb, Random rng)
  {
    if (player.Background == PlayerBackground.Scholar)
    {
      StartingGearForScholar(player, objDb, rng);
      return;
    }

    var leather = ItemFactory.Get(ItemNames.LEATHER_ARMOUR, objDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Equipped = true;
    leather.Slot = 'b';

    Item startWeapon;

    switch (player.Lineage)
    {
      case PlayerLineage.Orc:
        startWeapon = ItemFactory.Get(ItemNames.SHORTSHORD, objDb);
        player.Inventory.Add(leather, player.ID);
        player.Stats.Add(Attribute.SwordUse, new Stat(100));
        break;
      case PlayerLineage.Dwarf:
        startWeapon = ItemFactory.Get(ItemNames.HAND_AXE, objDb);
        var studded = ItemFactory.Get(ItemNames.RINGMAIL, objDb);
        studded.Equipped = true;
        studded.Slot = 'b';
        player.Inventory.Add(studded, player.ID);
        var helmet = ItemFactory.Get(ItemNames.HELMET, objDb);
        helmet.Equipped = true;
        helmet.Slot = 'c';
        player.Inventory.Add(helmet, player.ID);
        player.Stats.Add(Attribute.AxeUse, new Stat(100));
        break;
      case PlayerLineage.Elf:
        startWeapon = ItemFactory.Get(ItemNames.DAGGER, objDb);
        var bow = ItemFactory.Get(ItemNames.LONGBOW, objDb);
        bow.Equipped = true;
        bow.Slot = 'b';
        player.Inventory.Add(bow, player.ID);
        player.Stats.Add(Attribute.BowUse, new Stat(100));
        player.Stats.Add(Attribute.FinesseUse, new Stat(100));
        leather.Slot = 'c';
        player.Inventory.Add(leather, player.ID);        
        break;
      default:
        startWeapon = ItemFactory.Get(ItemNames.SPEAR, objDb);
        startWeapon.Traits.Add(new AdjectiveTrait("old"));        
        player.Inventory.Add(leather, player.ID);
        player.Stats.Add(Attribute.PolearmsUse, new Stat(100));
        break;
    }

    if (player.Background == PlayerBackground.Skullduggery)
    {
      startWeapon = ItemFactory.Get(ItemNames.DAGGER, objDb);
      if (!player.Stats.ContainsKey(Attribute.FinesseUse)) 
        player.Stats.Add(Attribute.FinesseUse, new Stat(100));
    }

    startWeapon.Equipped = true;
    player.Inventory.Add(startWeapon, player.ID);

    // Everyone gets 3 to 5 torches to start with
    for (int i = 0; i < rng.Next(3, 6); i++)
    {
      player.Inventory.Add(ItemFactory.Get(ItemNames.TORCH, objDb), player.ID);
    }

    var money = ItemFactory.Get(ItemNames.ZORKMIDS, objDb);
    money.Value = rng.Next(25, 51);
    player.Inventory.Add(money, player.ID);
  }

  public static Player NewPlayer(string playerName, GameObjectDB objDb, int startRow, int startCol, UserInterface ui, Random rng)
  {
    var lineage = PickLineage(ui);
    var background = PickBackground(ui);

    Player player = new(playerName)
    {
      Loc = new Loc(0, 0, startRow, startCol),
      Lineage = lineage,
      Background = background,
      Energy = 1.0
    };
    player.Stats = RollStats(player.Lineage, player.Background, rng);

    player.Inventory = new Inventory(player.ID, objDb);

    objDb.Add(player);

    SetInitialAbilities(player);
    SetStartingGear(player, objDb, rng);

    player.CalcHP();
    
    return player;
  }
}