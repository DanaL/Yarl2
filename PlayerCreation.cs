
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
  ShieldOfFaith,
  Cleave,
  Impale,
  Rage
}

class PlayerCreator
{
  static PlayerClass PickClass(UserInterface ui)
  {
    List<string> menu = [
"Which role will you assume this time?",
      "",
      " (1) Orcish Reaver                 (2) Dwarven Stalwart",
      "",
      " An intimidating orc warrior,      A dwarf paladin oath-sworn",
      " trained to defend their clan in   to root out evil. Strong knights",
      " battle. Fierce and tough.         who can call upon holy magic.",
      "                                "
    ];
    var options = new HashSet<char>() { '1', '2' };
    char choice = ui.FullScreenMenu(menu, options, null);

    return choice switch
    {
      '1' => PlayerClass.OrcReaver,
      _ => PlayerClass.DwarfStalwart
    };

  }

  // If I use this enough, move it to Utils?
  static int Roll3d6(Random rng) => rng.Next(1, 7) + rng.Next(1, 7) + rng.Next(1, 7);
  static int StatRoll(Random rng) => Util.StatRollToMod(Roll3d6(rng));

  static Dictionary<Attribute, Stat> RollStats(PlayerClass charClass, Random rng)
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

    // Now the class-specific stuff
    int roll, hp = 1;
    switch (charClass)
    {
      case PlayerClass.OrcReaver:
        roll = Util.StatRollToMod(10 + rng.Next(1, 5) + rng.Next(1, 5));
        if (roll > stats[Attribute.Strength].Curr)
          stats[Attribute.Strength].SetMax(roll);
        hp = 150 + stats[Attribute.Constitution].Curr;
        stats.Add(Attribute.MeleeAttackBonus, new Stat(3));
        stats.Add(Attribute.HitDie, new Stat(12));
        break;
      case PlayerClass.DwarfStalwart:
        // Should Stalwarts also be strength based?
        roll = Util.StatRollToMod(8 + rng.Next(1, 6) + rng.Next(1, 6));
        if (roll > stats[Attribute.Strength].Curr)
          stats[Attribute.Strength].SetMax(roll);

        if (stats[Attribute.Piety].Curr < 0)
          stats[Attribute.Piety].SetMax(0);

        hp = 10 + stats[Attribute.Constitution].Curr;
        stats.Add(Attribute.MeleeAttackBonus, new Stat(2));
        stats.Add(Attribute.HitDie, new Stat(10));
        break;
    }

    if (hp < 1)
      hp = 1;
    stats.Add(Attribute.HP, new Stat(hp));

    return stats;
  }

  public static void SetStartingGear(Player player, GameObjectDB objDb, Random rng)
  {
    switch (player.CharClass)
    {
      case PlayerClass.OrcReaver:
        var spear = ItemFactory.Get("spear", objDb);
        spear.Adjectives.Add("old");
        spear.Equiped = true;
        player.Inventory.Add(spear, player.ID);
        var slarmour = ItemFactory.Get("studded leather armour", objDb);
        slarmour.Adjectives.Add("battered");
        slarmour.Equiped = true;
        player.Inventory.Add(slarmour, player.ID);
        break;
      case PlayerClass.DwarfStalwart:
        var axe = ItemFactory.Get("hand axe", objDb);
        axe.Equiped = true;
        player.Inventory.Add(axe, player.ID);
        var chain = ItemFactory.Get("ringmail", objDb);
        chain.Equiped = true;
        player.Inventory.Add(chain, player.ID);
        var helmet = ItemFactory.Get("helmet", objDb);
        helmet.Equiped = true;
        player.Inventory.Add(helmet, player.ID);
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
    player.Inventory.Add(ItemFactory.Get("potion of healing", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("potion of healing", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);

    for (int j = 0; j < 5; j++)
      player.Inventory.Add(ItemFactory.Get("dagger", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("scroll of magic mapping", objDb), player.ID);

    var money = ItemFactory.Get("zorkmids", objDb);
    money.Value = rng.Next(25, 51);
    player.Inventory.Add(money, player.ID);
  }

  public static Player NewPlayer(string playerName, GameObjectDB objDb, int startRow, int startCol, UserInterface ui, Random rng)
  {
    Player player = new(playerName)
    {
      Loc = new Loc(0, 0, startRow, startCol),
      CharClass = PickClass(ui)
    };
    player.Stats = RollStats(player.CharClass, rng);
    player.Inventory = new Inventory(player.ID, objDb);

    objDb.Add(player);

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
      case Boon.ShieldOfFaith:
        player.Traits.Add(new ShieldOfTheFaithfulTrait() { ArmourMod = 2 });
        msg = "\n  Shield of the Faithful";
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
      int ab = player.Stats[Attribute.MeleeAttackBonus].Max;
      player.Stats[Attribute.MeleeAttackBonus].SetMax(ab + 1);
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
      int ab = player.Stats[Attribute.MeleeAttackBonus].Max;
      player.Stats[Attribute.MeleeAttackBonus].SetMax(ab + 1);
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
      if (!player.HasTrait<ShieldOfTheFaithfulTrait>())
        boons.Add(Boon.ShieldOfFaith);
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

      switch (player.CharClass)
      {
        case PlayerClass.OrcReaver:
          msg += LevelUpReaver(player, level, rng);
          break;
        case PlayerClass.DwarfStalwart:
          msg += LevelUpStalwart(player, level, rng);
          break;
      }

      msg += "\n";

      ui.Popup(msg, "Level up!");
    }
  }
}