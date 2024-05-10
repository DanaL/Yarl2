
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

enum Boon
{
  StrInc,
  ConInc,
  DexInc,
  PietyInc,
  BonusHP,
  MeleeDmgBonus,
  Cleave,
  Impale,
  Rage
}

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
      " to progressive tax policy in this   bonus hit points. Due do participating",
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
      { Attribute.Level, new Stat(1) },
      { Attribute.XP, new Stat(0) },
      { Attribute.Depth, new Stat(0) }
    };

    int roll, hp = 0;

    switch (lineage)
    {
      case PlayerLineage.Orc:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Strength].Curr)
          stats[Attribute.Strength].SetMax(roll);
        hp = 5;
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
        hp += 12 + stats[Attribute.Constitution].Curr;
        stats.Add(Attribute.HitDie, new Stat(12));
        break;
      case PlayerBackground.Skullduggery:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Dexterity].Curr)
          stats[Attribute.Dexterity].SetMax(roll);
        hp += 10 + stats[Attribute.Constitution].Curr;
        stats.Add(Attribute.HitDie, new Stat(10));
        break;
      case PlayerBackground.Scholar:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Will].Curr)
          stats[Attribute.Will].SetMax(roll);
        hp += 8 + stats[Attribute.Constitution].Curr;
        stats.Add(Attribute.HitDie, new Stat(8));
        break;      
    }
    
    if (hp < 1)
      hp = 1;
    stats.Add(Attribute.HP, new Stat(hp));

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
  }

  public static void SetStartingGear(Player player, GameObjectDB objDb, Random rng)
  {
    var leather = ItemFactory.Get("leather armour", objDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Equiped = true;

    switch (player.Lineage)
    {
      case PlayerLineage.Orc:
        var spear = ItemFactory.Get("spear", objDb);
        spear.Traits.Add(new AdjectiveTrait("old"));
        spear.Equiped = true;
        player.Inventory.Add(spear, player.ID);        
        player.Inventory.Add(leather, player.ID);
        break;
      case PlayerLineage.Dwarf:
        var axe = ItemFactory.Get("hand axe", objDb);
        axe.Equiped = true;
        player.Inventory.Add(axe, player.ID);
        var studded = ItemFactory.Get("studded leather armour", objDb);
        studded.Equiped = true;
        player.Inventory.Add(studded, player.ID);
        var helmet = ItemFactory.Get("helmet", objDb);
        helmet.Equiped = true;
        player.Inventory.Add(helmet, player.ID);
        break;
      case PlayerLineage.Elf:
        var bow = ItemFactory.Get("longbow", objDb);
        bow.Equiped = true;
        player.Inventory.Add(bow, player.ID);
        var dagger = ItemFactory.Get("dagger", objDb);
        dagger.Equiped = true;
        player.Inventory.Add(dagger, player.ID);
        player.Inventory.Add(leather, player.ID);
        break;
      case PlayerLineage.Human:
        var sword = ItemFactory.Get("shortsword", objDb);
        sword.Equiped = true;
        player.Inventory.Add(sword, player.ID);
        player.Inventory.Add(leather, player.ID);
        break;
    }

    // Everyone gets 3 to 5 torches to start with
    player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
    for (int i = 0; i < rng.Next(3); i++)
    {
      player.Inventory.Add(ItemFactory.Get("torch", objDb), player.ID);
    }

    player.Inventory.Add(ItemFactory.Get("potion of healing", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("potion of healing", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("potion of healing", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);

    //player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);

    //player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);

    //player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("wand of frost", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("wand of fireballs", objDb), player.ID);

    var money = ItemFactory.Get("zorkmids", objDb);
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

    return player;
  }

  static int LevelForXP(int xp)
  {
    if (xp < 20)
      return 1;
    else if (xp < 40)
      return 2;
    else if (xp < 80)
      return 3;
    else if (xp < 160)
      return 4;
    else
      return 5;
  }

  static string ApplyBoon(Player player, Boon boon)
  {
    string msg = "";

    switch (boon)
    {
      case Boon.StrInc:
        player.Stats[Attribute.Strength].ChangeMax(1);
        player.Stats[Attribute.Strength].Change(1);
        msg = "\n  a Str increase";
        break;
      case Boon.ConInc:
        player.Stats[Attribute.Constitution].ChangeMax(1);
        player.Stats[Attribute.Constitution].Change(1);

        // Con went up, so we increase max HP for levels earned so far
        player.Stats[Attribute.HP].ChangeMax(player.Stats[Attribute.Level].Max);

        msg = "\n  a Con increase";
        break;
      case Boon.DexInc:
        player.Stats[Attribute.Dexterity].ChangeMax(1);
        player.Stats[Attribute.Dexterity].Change(1);
        msg = "\n  a Dex increase";
        break;
      case Boon.PietyInc:
        player.Stats[Attribute.Piety].ChangeMax(1);
        player.Stats[Attribute.Piety].Change(1);
        msg = "\n  a Piety increase";
        break;
      case Boon.BonusHP:
        player.Stats[Attribute.HP].ChangeMax(5);
        player.Stats[Attribute.HP].Change(5);
        msg = "\n  +5 extra HP";
        break;
      case Boon.MeleeDmgBonus:
        if (player.Stats.TryGetValue(Attribute.MeleeDmgBonus, out Stat? stat))
          stat.ChangeMax(2);
        else
          player.Stats[Attribute.MeleeDmgBonus] = new Stat(2);
        msg = "\n  a bonus to melee damage";
        break;      
      case Boon.Cleave:
        player.Traits.Add(new CleaveTrait());
        msg = "\n  the ability to Cleave";
        break;
      case Boon.Impale:
        player.Traits.Add(new ImpaleTrait());
        msg = "\n  the ability to Impale";
        break;
      case Boon.Rage:
        player.Traits.Add(new RageTrait(player));
        msg = "\n  you may now Rage";
        break;
    }

    return msg;
  }

  static string LevelUpReaver(Player player, int newLevel, Random rng)
  {
    string msg = "";

    if (newLevel % 2 == 0)
    {
      int ab = player.Stats[Attribute.AttackBonus].Max;
      player.Stats[Attribute.AttackBonus].SetMax(ab + 1);
      msg += $"\n  Attack Bonus increases to {ab + 1}";
    }
    else
    {
      // eventually add 'feats' like Cleave, etc
      List<Boon> boons = [Boon.BonusHP, Boon.MeleeDmgBonus];
      if (player.Stats[Attribute.Strength].Max < 4)
        boons.Add(Boon.StrInc);
      if (player.Stats[Attribute.Constitution].Max < 4)
        boons.Add(Boon.ConInc);
      if (!player.HasTrait<CleaveTrait>())
        boons.Add(Boon.Cleave);
      if (!player.HasTrait<ImpaleTrait>())
        boons.Add(Boon.Impale);
      if (!player.HasTrait<RageTrait>())
        boons.Add(Boon.Rage);

      Boon boon = boons[rng.Next(boons.Count)];
      msg += ApplyBoon(player, boon);
    }

    return msg;
  }

  static string LevelUpStalwart(Player player, int newLevel, Random rng)
  {
    string msg = "";

    if (newLevel % 2 == 0)
    {
      int ab = player.Stats[Attribute.AttackBonus].Max;
      player.Stats[Attribute.AttackBonus].SetMax(ab + 1);
      msg += $"\n  Attack Bonus increases to {ab + 1}";
    }
    else
    {
      // Need more boons for Stalwarts. Probably Prayers once I have
      // some implemented
      List<Boon> boons = [];
      if (player.Stats[Attribute.Strength].Max < 4)
        boons.Add(Boon.StrInc);
      if (player.Stats[Attribute.Constitution].Max < 4)
        boons.Add(Boon.ConInc);
      if (player.Stats[Attribute.Piety].Max < 4)
        boons.Add(Boon.PietyInc);      
      if (!player.HasTrait<ImpaleTrait>())
        boons.Add(Boon.Impale);

      Boon boon = boons[rng.Next(boons.Count)];
      msg += ApplyBoon(player, boon);
    }

    return msg;
  }

  // Am I going to have effects that reduce a player's XP/level? I dunno.
  // Classic D&D stuff but those effects have most been dropped from the 
  // modern rulesets
  public static void CheckLevelUp(Player player, UserInterface ui, Random rng)
  {
    int level = LevelForXP(player.Stats[Attribute.XP].Max);

    if (level > player.Stats[Attribute.Level].Curr)
    {
      player.Stats[Attribute.Level].SetMax(level);

      int hitDie = player.Stats[Attribute.HitDie].Max;
      int newHP = rng.Next(hitDie) + 1 + player.Stats[Attribute.Constitution].Max;
      if (newHP < 1)
        newHP = 1;
      player.Stats[Attribute.HP].ChangeMax(newHP);
      player.Stats[Attribute.HP].Change(newHP);

      string msg = $"\nWelcome to level {level}!";
      msg += $"\n  +{newHP} HP";

      switch (player.Lineage)
      {
        case PlayerLineage.Orc:
          msg += LevelUpReaver(player, level, rng);
          break;
        case PlayerLineage.Dwarf:
          msg += LevelUpStalwart(player, level, rng);
          break;
      }

      msg += "\n";

      ui.SetPopup(new Popup(msg, "Level up!", -1, -1));
    }
  }
}