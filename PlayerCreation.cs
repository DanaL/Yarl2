
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

using System.Text;

namespace Yarl2;

enum Boon
{
  StrInc,
  ConInc,
  DexInc,
  PietyInc,
  BonusHP,
  Cleave,
  Impale,
  Rage,
  Reach,
  Dodge
}

record BoonInfo(Boon Boon, string Name, string Desc);

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
      { Attribute.Depth, new Stat(0) },
      { Attribute.AttackBonus, new Stat(2) }
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

    switch (player.Background)
    {
      case PlayerBackground.Skullduggery:
        player.Traits.Add(new LightStepTrait());
        break;
    }
  }

  public static void SetStartingGear(Player player, GameObjectDB objDb, Random rng)
  {
    var leather = ItemFactory.Get("leather armour", objDb);
    leather.Traits.Add(new AdjectiveTrait("battered"));
    leather.Equiped = true;
    leather.Slot = 'b';

    Item startWeapon;

    switch (player.Lineage)
    {
      case PlayerLineage.Orc:
        startWeapon = ItemFactory.Get("shortsword", objDb);        
        player.Inventory.Add(leather, player.ID);
        break;
      case PlayerLineage.Dwarf:
        startWeapon = ItemFactory.Get("hand axe", objDb);
        var studded = ItemFactory.Get("ringmail", objDb);
        studded.Equiped = true;
        studded.Slot = 'b';
        player.Inventory.Add(studded, player.ID);
        var helmet = ItemFactory.Get("helmet", objDb);
        helmet.Equiped = true;
        helmet.Slot = 'c';
        player.Inventory.Add(helmet, player.ID);
        break;
      case PlayerLineage.Elf:
        startWeapon = ItemFactory.Get("dagger", objDb);

        var bow = ItemFactory.Get("longbow", objDb);
        bow.Equiped = true;
        bow.Slot = 'b';
        player.Inventory.Add(bow, player.ID);
        
        leather.Slot = 'c';
        player.Inventory.Add(leather, player.ID);        
        break;
      default:
        startWeapon = ItemFactory.Get("spear", objDb);
        startWeapon.Traits.Add(new AdjectiveTrait("old"));        
        player.Inventory.Add(leather, player.ID);
        break;
    }

    switch (player.Background)
    {
      case PlayerBackground.Skullduggery:
        startWeapon = ItemFactory.Get("dagger", objDb);
        break;
    }

    startWeapon.Equiped = true;
    player.Inventory.Add(startWeapon, player.ID);

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
    
    //player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("scroll of blink", objDb), player.ID);

    //player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("antidote", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);
    //player.Inventory.Add(ItemFactory.Get("potion of mind reading", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("potion of fire resistance", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("potion of fire resistance", objDb), player.ID);

    player.Inventory.Add(ItemFactory.Get("potion of cold resistance", objDb), player.ID);
    player.Inventory.Add(ItemFactory.Get("potion of cold resistance", objDb), player.ID);

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
    player.Stats.Add(Attribute.Attitude, new Stat((int)MobAttitude.Active));

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

  static void ApplyBoon(Player player, Boon boon)
  {    
    switch (boon)
    {
      case Boon.StrInc:
        player.Stats[Attribute.Strength].ChangeMax(1);
        player.Stats[Attribute.Strength].Change(1);
        break;
      case Boon.ConInc:
        player.Stats[Attribute.Constitution].ChangeMax(1);
        player.Stats[Attribute.Constitution].Change(1);

        // Con went up, so we increase max HP for levels earned so far
        int level = player.Stats[Attribute.Level].Max;
        player.Stats[Attribute.HP].ChangeMax(level);
        player.Stats[Attribute.HP].Change(level);
        break;
      case Boon.DexInc:
        player.Stats[Attribute.Dexterity].ChangeMax(1);
        player.Stats[Attribute.Dexterity].Change(1);
        break;
      case Boon.PietyInc:
        player.Stats[Attribute.Piety].ChangeMax(1);
        player.Stats[Attribute.Piety].Change(1);
        break;
      case Boon.BonusHP:
        player.Stats[Attribute.HP].ChangeMax(5);
        player.Stats[Attribute.HP].Change(5);
        break;
      case Boon.Cleave:
        player.Traits.Add(new CleaveTrait());
        break;
      case Boon.Impale:
        player.Traits.Add(new ImpaleTrait());
        break;
      case Boon.Rage:
        player.Traits.Add(new RageTrait(player));
        break;
      case Boon.Reach:
        player.Traits.Add(new ReachTrait());
        break;
      case Boon.Dodge:
        player.Traits.Add(new DodgeTrait() { Rate = 10 });
        break;
    }
  }

  // Determine what boons are available for the player to pick from.
  public static List<BoonInfo> AvailableBoons(Player player)
  {
    List<BoonInfo> boons = [];

    if (player.Stats[Attribute.Constitution].Max < 4)
      boons.Add(new (Boon.ConInc, "Con Increase", "Increase your Con. This will increase your max HP."));

    if (player.Stats[Attribute.Strength].Max < 4)
      boons.Add(new(Boon.StrInc, "Str Increase", "Increase your Str. You'll be more effective in melee combat."));

    if (player.Stats.TryGetValue(Attribute.PolearmsUse, out var p))
    {
      if (!player.HasTrait<ImpaleTrait>() && p.Curr > 25)
        boons.Add(new (Boon.Impale, "Impale", "Attacks with a polearm or rapier may also strike an opponent behind the target."));

      if (!player.HasTrait<ReachTrait>() && p.Curr > 25)
        boons.Add(new(Boon.Reach, "Reach", "With a long polearm, you can attack 2 squares away."));
    }

    if (player.Stats.TryGetValue(Attribute.SwordUse, out var sw) && sw.Curr > 25)
    {
      if (!player.HasTrait<CleaveTrait>())
        boons.Add(new(Boon.Cleave, "Cleave", "When you attack with sword or axe, you may also strike targets adjacent to your opponent."));

      if (player.Stats[Attribute.Strength].Max < 4)
        boons.Add(new(Boon.StrInc, "Str Increase", "Increase your Str. You'll be more effective in melee combat."));
    }

    if (player.Stats.TryGetValue(Attribute.AxeUse, out var axe))
    {
      if (!player.HasTrait<CleaveTrait>() && axe.Curr > 25)
        boons.Add(new(Boon.Cleave, "Cleave", "When you attack with sword or axe, you may also strike targets adjacent to your opponent."));

      if (player.Stats[Attribute.Strength].Max < 4 && axe.Curr > 10)
        boons.Add(new(Boon.StrInc, "Str Increase", "Increase your Str. You'll be more effective in melee combat."));
    }

    if (player.Stats.TryGetValue(Attribute.FinesseUse, out var fin))
    {
      if (player.Stats[Attribute.Dexterity].Max < 4 && fin.Curr > 10)
        boons.Add(new(Boon.DexInc, "Dex Increase", "Increase your Dex."));

      if (!player.HasTrait<ImpaleTrait>() && fin.Curr > 25)
        boons.Add(new (Boon.Impale, "Impale", "Attacks with a polearm or rapier may also strike an opponent behind the target."));

      if (!player.HasTrait<DodgeTrait>() && fin.Curr > 25)
        boons.Add(new (Boon.Dodge, "Dodge", "In melee, you have a chance to leap out of the way of an attack that might have hit you."));
    }

    return boons.Distinct().ToList();
  }

  // Am I going to have effects that reduce a player's XP/level? I dunno.
  // Classic D&D stuff but those effects have most been dropped from the 
  // modern rulesets
  public static void CheckLevelUp(GameState gs)
  {
    Player player = gs.Player;

    int level = LevelForXP(player.Stats[Attribute.XP].Max);

    if (level > player.Stats[Attribute.Level].Curr)
    {
      player.Stats[Attribute.Level].SetMax(level);
      
      int hitDie = player.Stats[Attribute.HitDie].Max;
      int newHP = gs.Rng.Next(hitDie) + 1 + player.Stats[Attribute.Constitution].Max;
      if (newHP < 1)
        newHP = 1;
      player.Stats[Attribute.HP].ChangeMax(newHP);
      player.Stats[Attribute.HP].Change(newHP);
      
      string msg = $"\nWelcome to level {level}!";
      msg += $"\n  +{newHP} HP";

      var ui = gs.UIRef();
      // On even levels, the player's attack bonus increases
      if (level % 2 == 0)
      {
        int ab = player.Stats[Attribute.AttackBonus].Max;
        player.Stats[Attribute.AttackBonus].SetMax(ab + 1);
        msg += $"\n  Attack Bonus increases to {ab + 1}";
        ui.SetPopup(new Popup(msg, "Level up!", -1, -1));
        ui.BlockingPopup(gs);
      }
      else
      {
        var boons = AvailableBoons(player);
        if (boons.Count > 0)
        {
          ChooseBoon(player, gs, msg, boons);
        }
        else
        {
          ui.SetPopup(new Popup(msg, "Level up!", -1, -1));
          ui.BlockingPopup(gs);
        }        
      }

      ui.ClosePopup();
    }    
  }

  static void ChooseBoon(Player player, GameState gs, string msg, List<BoonInfo> boons)
  {
    var sb = new StringBuilder();
    sb.Append(msg);
    sb.Append("\n\nPlease choose an upgrade for your character:\n\n");
    
    HashSet<char> opts = [];
    for (int j = 0; j < boons.Count; j += 2) 
    {
      BoonInfo a = boons[j];
      BoonInfo? b = j + 1 < boons.Count ? boons[j + 1] : null;

      sb.Append($" {j+1}) {a.Name}   ".PadRight(31));
      opts.Add((char)(j + 49));
      if (b is not null)
      {
        sb.Append($"{j + 2}) {b.Name}");
        opts.Add((char)(j + 50));
      }
      sb.Append('\n');

      List<string> descA = SplitToLines(a.Desc, 26);
      List<string> descB = [];
      if (b is not null)
      {
        descB = SplitToLines(b.Desc, 32);
      }
      int x = int.Max(descA.Count, descB.Count);
      for (int k = 0; k < x; k++)
      {
        if (k < descA.Count)
        {
          string txt = $"    {descA[k]}".PadRight(34);
          sb.Append(txt);
        }
        else
        {
          sb.Append(" ".PadRight(34));
        }

        if (k < descB.Count)
        {
          sb.Append(descB[k]);
        }

        sb.Append('\n');
      }

      sb.Append('\n');
    }

    var ui = gs.UIRef();
    int choice = 0;
    do
    {
      char ch = ui.BlockingPopupMenu(sb.ToString(), "Level up!", opts, gs, 72);

      if (opts.Contains(ch))
      {
        choice = ch - '0' - 1;
        bool confirmed = ui.Confirmation($"\nSelect {boons[choice].Name}? (y/n)\n", gs);
        if (confirmed)
          break;
        
        ui.CloseConfirmation();
      }
    }
    while (true);

    ApplyBoon(player, boons[choice].Boon);

    ui.ClosePopup();
  }

  static List<string> SplitToLines(string txt, int lineLen)
  {        
    if (txt.Length <= lineLen)
      return [ txt ];

    List<string> lines = [];
    while (txt.Length > lineLen)
    {
      int x = WhitespaceLoc(txt, lineLen);
      lines.Add(txt[..x]);
      txt = txt[x..].TrimStart();
    }
    lines.Add(txt);

    return lines;
  }

  static int WhitespaceLoc(string txt, int start)
  {
    for (int j = start; j > 0; j--)
    {
      if (txt[j] == ' ')
        return j;
    }

    return -1;
  }
}