﻿// Yarl2 - A roguelike computer RPG
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

class ShopMenuItem(char slot, Item item, int stockCount, int price)
{
  public char Slot { get; set; } = slot;
  public Item Item { get; set; } = item;
  public int StockCount { get; set; } = stockCount;
  public int SelectedCount { get; set; } = 0;
  public int Price { get; set; } = price;
}

class ShopMenuInputer : Inputer
{
  protected Mob Shopkeeper { get; set; }
  protected Dictionary<char, ShopMenuItem> MenuItems { get; set; } = [];
  protected string Blurb { get; set; }
  protected bool SingleSelection { get; set; } = false;
  protected bool Done { get; set; } = false;

  public ShopMenuInputer(Actor shopkeeper, string blurb, GameState gs) : base(gs)
  {
    Blurb = blurb;
    Shopkeeper = (Mob)shopkeeper;
    MenuItems = MenuFromInventory(Shopkeeper);

    if (Shopkeeper.Stats.TryGetValue(Attribute.ShopMenu, out Stat? menu))
      Shopkeeper.Stats[Attribute.ShopMenu] = new Stat(0);
    else
      Shopkeeper.Stats.Add(Attribute.ShopMenu, new Stat(0));

    WritePopup(blurb);
  }

  protected double CalcMarkup()
  {
    double markup;
    if (Shopkeeper.Stats.TryGetValue(Attribute.Markup, out var markUpStat))
      markup = markUpStat.Curr / 100.0;      
    else
      markup = 1.0;
    
    if (GS.Player.HasTrait<LikeableTrait>())
      markup -= 0.33;
    if (GS.Player.HasTrait<RepugnantTrait>())
      markup += 2.0;

    return markup;
  }

  protected virtual Dictionary<char, ShopMenuItem> MenuFromInventory(Mob shopkeeper)
  {
    var items = shopkeeper.Inventory.UsedSlots()
                          .Select(shopkeeper.Inventory.ItemAt);

    Dictionary<char, ShopMenuItem> menuItems = [];
    char ch = 'a';
    double markup = CalcMarkup();
    foreach (var (item, count) in items)
    {
      int price = (int) (item!.Value * markup);
      menuItems.Add(ch++, new ShopMenuItem(item!.Slot, item, count, price));
    }

    return menuItems;
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == ' ')
    {
      Close();
      Done = true;
      return;
    }
    else if ((ch == '\n' || ch == '\r') && TotalInvoice() > 0 && GS.Player.Inventory.Zorkmids >= TotalInvoice())
    {
      Close();
      QueueDeferredAction();
      Done = true;
      return;
    }
    else if (MenuItems.ContainsKey(ch))
    {
      if (SingleSelection)
      {
        foreach (char slot in MenuItems.Keys)
        {
          if (slot != ch)
            MenuItems[slot].SelectedCount = 0;
        }
      }

      if (MenuItems[ch].StockCount == 1 && MenuItems[ch].SelectedCount == 0)
        MenuItems[ch].SelectedCount = 1;
      else if (MenuItems[ch].StockCount == 1)
        MenuItems[ch].SelectedCount = 0;
      else
      {
        ++MenuItems[ch].SelectedCount;
        if (MenuItems[ch].SelectedCount > MenuItems[ch].StockCount)
          MenuItems[ch].SelectedCount = 0;
      }

      WritePopup(Blurb);
    }
  }

  public override UIResult GetResult() => new ShoppingUIResult()
  {
    Zorkminds = TotalInvoice(),
    Selections = [.. MenuItems.Values.Where(i => i.SelectedCount > 0)
                                    .Select(i => (i.Slot, i.SelectedCount))]
  };

  protected int TotalInvoice() => MenuItems.Values.Select(mi => mi.SelectedCount * mi.Price).Sum();

  protected virtual string MenuScreen(string blurb)
  {
    var sb = new StringBuilder(Shopkeeper.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");
    sb.Append(blurb);
    sb.Append("\n\n");

    if (MenuItems.Count == 0)
    {
      sb.Append("I'm afraid my shelves are a bit bare at the moment.");
    }
    else
    {
      List<char> keys = [.. MenuItems.Keys];
      keys.Sort();

      List<string> lines = [];
      foreach (var key in keys)
      {
        var line = new StringBuilder();
        line.Append(key);
        line.Append(") ");
        line.Append(MenuItems[key].Item.FullName);

        if (MenuItems[key].StockCount > 1)
        {
          line.Append(" (");
          line.Append(MenuItems[key].StockCount);
          line.Append(')');
        }

        if (MenuItems[key].Price > 0)
        {
          int price = MenuItems[key].Price;
          line.Append(" - [YELLOW $]");
          line.Append(price);
        }

        if (MenuItems[key].StockCount > 1)
          line.Append(" apiece");
        lines.Add(line.ToString());
      }

      int widest = lines.Select(l => l.Length).Max() + 2;
      int l = 0;
      foreach (var key in keys)
      {
        if (MenuItems[key].SelectedCount > 0)
        {
          sb.Append(lines[l].PadRight(widest + 2));
          if (MenuItems[key].StockCount == 1)
          {
            sb.Append("[GREEN *]");
          }
          else
          {
            sb.Append("[GREEN ");
            sb.Append(MenuItems[key].SelectedCount);
            sb.Append(']');
          }
        }
        else
        {
          sb.Append(lines[l]);
        }
        sb.Append('\n');
        ++l;
      }

      int invoice = TotalInvoice();
      if (invoice > 0)
      {
        sb.Append("\nTotal bill: ");
        sb.Append("[YELLOW $]");
        sb.Append(invoice);
        sb.Append('\n');
      }

      if (invoice > GS.Player.Inventory.Zorkmids)
      {
        sb.Append("\n[brightred You don't have enough money for all that!]");
      }
      else
      {
        sb.Append("\n(Enter to accept)");
      }
    }

    return sb.ToString();
  }

  protected virtual void WritePopup(string blurb)
  {
    string inventoryScreen = MenuScreen(blurb);  

    GS.UIRef().SetPopup(new Popup(inventoryScreen, Shopkeeper.FullName, -1, -1));
  }
}

class InnkeeperInputer : Inputer
{
  Actor Shopkeeper { get; set; }
  bool InsufficentFunds { get; set; }
  string Selection { get; set; } = "";
  int Zorkmids { get; set; }

  public InnkeeperInputer(Actor shopkeeper, GameState gs) : base(gs)
  {
    Shopkeeper = shopkeeper;    
    WritePopup();
  }

  public override void Input(char ch)
  {
    InsufficentFunds = false;

    if (ch == Constants.ESC || ch == '\n' || ch == '\r' || ch == ' ' || ch == 'c')
    {
      Close();
      return;
    }
    else if (ch == 'a' && GS.Player.Inventory.Zorkmids < 2)
    {
      InsufficentFunds = true;
    }
    else if (ch == 'a')
    {
      Selection = "Booze";
      Zorkmids = 2;
      Close();
      QueueDeferredAction();
      return;
    }
    else if (ch == 'b' && GS.Player.Inventory.Zorkmids < 5)
    {
      InsufficentFunds = true;
    }
    else if (ch == 'b')
    {      
      Selection = "Rest";
      Zorkmids = 5;
      Close();
      QueueDeferredAction();
      return;
    }

    WritePopup();
  }

  string Blurb()
  {
    return GS.Rng.Next(4) switch
    {
      0 => "Come to sooth your dusty throat? We've got just your poison!",
      1 => "What'll it be?",
      2 => "They say good tips lead to good karma!",
      _ => "Ah! A brave adventurer come to rest their weary feet!"
    };
  }

  protected void WritePopup()
  {
    SimpleFact tavernName = GS.FactDb.FactCheck("TavernName") as SimpleFact 
                                      ?? throw new Exception("Should never not be a tavern name");
     var sb = new StringBuilder(Shopkeeper.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\nWelcome to [LIGHTBLUE ");
    sb.Append(tavernName.Value);
    sb.Append("]!\n\n");
    sb.Append(Blurb());
    sb.Append("\n\n");
    sb.Append("a) Purchase a flagon of booze. [YELLOW $]2\n");
    sb.Append("b) Rent a bed and rest. [YELLOW $]5\n");
    sb.Append("c) Farewell.");
    
    if (InsufficentFunds)
      sb.Append("\n[BRIGHTRED You don't have enough money!]");

    GS.UIRef().SetPopup(new Popup(sb.ToString(), Shopkeeper.FullName, -1, -1));
  }

  public override UIResult GetResult() => new ServiceResult()
  {
    Service = Selection,
    Zorkminds = Zorkmids
  };
}

class SmithyInputer : ShopMenuInputer
{
  bool _offerRepair;
  bool _offerUpgrade;
  char _itemToEnchant;
  
  public SmithyInputer(Actor shopkeeper, string blurb, GameState gs) : base(shopkeeper, blurb, gs)
  {    
    _offerRepair = false;
    _offerUpgrade = false;
    foreach (Item item in GS.Player.Inventory.Items())
    {
      if (item.HasTrait<RustedTrait>())
        _offerRepair = true;

      if (item.Type == ItemType.Reagent)
        _offerUpgrade = true;
    }

    // Menu state:
    // 0 - Offer choice of shop/repair/upgrade
    // 1 - Shopping
    if (_offerRepair || _offerUpgrade) 
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(0);
    else
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(1);

    WritePopup(blurb);
  }

  public override void Input(char ch)
  {
    int menuState;
    if (Shopkeeper.Stats.TryGetValue(Attribute.ShopMenu, out Stat? menu))
      menuState = menu.Curr;
    else
      menuState = 0;

    string blurb = Blurb;
    if (ch == Constants.ESC || ch == ' ')
    {
      Close();
      return;
    }
    else if (menuState == 0 && ch == 'a')
    {
      MenuItems = MenuFromInventory(Shopkeeper);
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(1);
    }
    else if (menuState == 0 && ch == 'b')
    {
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(2);
      MenuItems = RepairMenu();      
    }
    else if (menuState == 0 && ch == 'c')
    {
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(3);
      MenuItems = UpgradeMenu();
    }
    else if (menuState == 1)
    {
      base.Input(ch);
    }
    else if (menuState == 2)
    {
      GS.Player.QueueAction(new RepairItemAction(GS, Shopkeeper));
      blurb = "What would you like repaired?";
      base.Input(ch);
    }
    else if (menuState == 3 && MenuItems.ContainsKey(ch))
    {
      GS.Player.QueueAction(new UpgradeItemAction(GS, Shopkeeper));
      _itemToEnchant = ch;
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(4);
      MenuItems = ReagentMenu();      
    }
    else if (menuState == 4)
    {
      SingleSelection = true;
      base.Input(ch);
    }

    if (Done)
      return;

    WritePopup(blurb);
  }

  Dictionary<char, ShopMenuItem> ReagentMenu()
  {
    Dictionary<char, ShopMenuItem> menuItems = [];

    double markup = CalcMarkup();
    var reagents = GS.Player.Inventory.Items()
                            .Where(i => i.Type == ItemType.Reagent)
                            .OrderBy(i => i.Slot);
    foreach (Item item in reagents)
    {
      int price = (int)(50 * markup);
      if (!menuItems.ContainsKey(item.Slot))
        menuItems.Add(item.Slot, new ShopMenuItem(item.Slot, item, 1, price));
    }

    return menuItems;
  }

  Dictionary<char, ShopMenuItem> UpgradeMenu()
  {
    Dictionary<char, ShopMenuItem> menuItems = [];
    foreach (Item item in GS.Player.Inventory.Items().OrderBy(i => i.Slot))
    {
      if (menuItems.ContainsKey(item.Slot))
        continue;
      if (item.Type == ItemType.Reagent)
        continue;
      if (item.HasTrait<RustedTrait>())
        continue;
      
      menuItems.Add(item.Slot, new ShopMenuItem(item.Slot, item, 1, 0));
    }

    return menuItems;
  }

  Dictionary<char, ShopMenuItem> RepairMenu()
  {    
    double markup = CalcMarkup();
    Dictionary<char, ShopMenuItem> menuItems = [];
    int slot = 0;
    
    foreach (Item item in GS.Player.Inventory.Items().OrderBy(i => i.Slot))
    {
      if (item.Traits.OfType<RustedTrait>().FirstOrDefault() is not RustedTrait rust)
        continue;
      int labour = rust.Amount == Rust.Rusted ? 5 : 10;
      int price = (int)((item.Value / 4 + labour) * markup);
      menuItems.Add((char)('a' + slot++), new ShopMenuItem(item.Slot, item, 1, price));
    }

    return menuItems;
  }

  protected override void WritePopup(string blurb)
  {
    if (!GS.Town.Smithy.Contains(Shopkeeper.Loc))
    {
      string s = Shopkeeper.Appearance.IndefArticle().Capitalize();
      s += ".\n\nI'm off the clock. Come see me at the forge tomorrow.";
      GS.UIRef().SetPopup(new Popup(s, Shopkeeper.FullName, -1, -1));
      return;
    }

    // If the player has any rusty items, offer a menu where they can choose between
    // shopping or repair. (And eventually also upgrade items)
    int menuState;
    if (Shopkeeper.Stats.TryGetValue(Attribute.ShopMenu, out var value))
      menuState = value.Curr;
    else
    {
      Shopkeeper.Stats.Add(Attribute.ShopMenu, new Stat(0));
      menuState = 0;
    }

    string dialogueText;
    if (menuState == 0)
    {
      var sb = new StringBuilder(Shopkeeper.Appearance.IndefArticle().Capitalize());
      sb.Append(".\n\n");
      sb.Append(Blurb);
      sb.Append("\n\n");

      sb.Append("a) Do some shopping.\n");
      if (_offerRepair)
        sb.Append("b) Repair gear.\n");
      if (_offerUpgrade)
        sb.Append("c) Try to enchant an item.\n");

      dialogueText = sb.ToString();
    }
    else if (menuState == 1)
    {
      dialogueText = MenuScreen(Blurb);
    }
    else if (menuState == 2)
    {
      dialogueText = MenuScreen("What would you like repaired?");
    }
    else if (menuState == 3)
    {
      dialogueText = MenuScreen("What shall we try to upgrade?");
    }
    else if (menuState == 4)
    {
      var (item, _) = GS.Player.Inventory.ItemAt(_itemToEnchant);
      string txt = $"Try to enchant your [ICEBLUE {item!.FullName}] with what?";
      dialogueText = MenuScreen(txt);
    }
    else
    {
      dialogueText = "Hmm this shouldn't happen?";
    }

    GS.UIRef().SetPopup(new Popup(dialogueText, Shopkeeper.FullName, -1, -1));
  }

  public override UIResult GetResult()
  {
    if (Shopkeeper.Stats.TryGetValue(Attribute.ShopMenu, out var menuState) && menuState.Curr == 4)
    {
      char reagent = MenuItems.Values.Where(i => i.SelectedCount > 0)
                              .Select(i => i.Slot)
                              .First();

      return new UpgradeItemUIResult()
      {
        Zorkminds = TotalInvoice(),
        ItemSlot = _itemToEnchant,
        ReagentSLot = reagent
      };
    }
    else if (Shopkeeper.Stats.TryGetValue(Attribute.ShopMenu, out menuState) && menuState.Curr == 2)
    {
      List<ulong> itemIds = [.. MenuItems.Values.Where(i => i.SelectedCount > 0)
                                  .Select(i => i.Item.ID)];
      return new RepairItemUIResult()
      {
        Zorkminds = TotalInvoice(),
        ItemIds = itemIds
      };
    }
    else
    {
      return new ShoppingUIResult()
      {
        Zorkminds = TotalInvoice(),
        Selections = [.. MenuItems.Values.Where(i => i.SelectedCount > 0)
                                    .Select(i => (i.Slot, i.SelectedCount))]
      };
    }    
  }
}

record SpellInfo(int Price, int ManaCost, string Prereq);
class WitchDialogue : Inputer
{
  Actor Witch { get; set; } 
  string Service { get; set; } = "";
  Dictionary<char, string> Options { get; set; } = [];
  string Blurb { get; set; } = "";
  int Invoice { get; set; } = 0;

  int DialogueState => Witch.Stats[Attribute.DialogueState].Curr;
  int PlayerMana => GS.Player.Stats.TryGetValue(Attribute.MagicPoints, out Stat? mana) ? mana.Max : 0;

  const int START_STATE = 0;
  const int BUY_SPELLS = 1;
  const int GIVE_QUEST = 3;
  const int ON_QUEST = 4;
  const int QUEST_ITEM_FOUND = 5;
  const int QUEST_DONE = 6;
  const int TABLET1 = 7;
  const int TRANSLATE_TABLET_1 = 8;

  bool PlayerHasCrystal 
  {  
    get
    {
      foreach (Item item in GS.Player.Inventory.Items())
      {
        if (item.Name == "meditation crystal")
          return true;        
      }

      return false;
    } 
  }

  readonly Dictionary<string, SpellInfo> Spells = new()
  {
    { "arcane spark", new SpellInfo(20, 1, "") },
    { "mage armour", new SpellInfo(20, 2, "") },
    { "illume", new SpellInfo(20, 1, "") },
    { "slumbering song", new SpellInfo(25, 5, "") },
    { "spark arc", new SpellInfo(25, 2, "arcane spark") },
    { "ersatz elevator", new SpellInfo(25, 3, "") }
  };

  public WitchDialogue(Actor witch, GameState gs) : base(gs)
  {
    Witch = witch;
    Witch.Stats[Attribute.DialogueState].SetMax(START_STATE);

    if (HasQuestItem1() && GS.FactDb.FactCheck("Tablet1Translated") is null)
    {
      Witch.Stats[Attribute.DialogueState] = new Stat(TABLET1);
    }
    else if (GS.FactDb.FactCheck("KylieQuest") is SimpleFact fact)
    {
      if (fact.Value == "begun" && !PlayerHasCrystal)
      {
        Witch.Stats[Attribute.DialogueState] = new Stat(ON_QUEST);
      }
      else if (fact.Value == "begun" && PlayerHasCrystal)
      {
        Witch.Stats[Attribute.DialogueState] = new Stat(QUEST_ITEM_FOUND);
      }
    }
    
    SetDialogueText();
    WritePopup();
  }

  bool HasQuestItem1()
  {
    foreach (Item item in GS.Player.Inventory.Items())
    {
      if (item.HasTrait<QuestItem1>())
        return true;
    }

    return false;
  }

  public override void Input(char ch)
  {
    int dialogueState = Witch.Stats[Attribute.DialogueState].Curr;

    if (ch == Constants.ESC || ch == '\n' || ch == '\r' || ch == ' ')
    {
      Close();
      return;
    }

    if (dialogueState == BUY_SPELLS && Options.ContainsKey(ch))
    {
      string spell = Options[ch];
      int price = Spells[spell].Price;

      if (GS.Player.Inventory.Zorkmids >= price)
      {
        Invoice = price;
        Service = spell;
        Close();
        QueueDeferredAction();
      }
      else
      {
        SetDialogueText();
        Blurb += "\n[BRIGHTRED You can't afford that!]\n";
        WritePopup();
      }
      
      return;
    }    
    else if (dialogueState == GIVE_QUEST && ch == 'a')
    {
      Close();
      return;
    }
    else if (dialogueState == START_STATE && PlayerMana > 0 && ch == 'a')
    {
      Witch.Stats[Attribute.DialogueState].SetMax(BUY_SPELLS);
    }
    else if (dialogueState == START_STATE && PlayerMana > 0 && ch == 'b')
    {
      Witch.Stats[Attribute.DialogueState].SetMax(BUY_SPELLS);
    }
    else if (dialogueState == START_STATE && PlayerMana == 0 && ch == 'a')
    {
      Witch.Stats[Attribute.DialogueState].SetMax(GIVE_QUEST);
    }
    else if (dialogueState == QUEST_ITEM_FOUND && ch == 'a')
    {
      Invoice = 0;
      Service = "magic101";
      Close();
      QueueDeferredAction();
    }
    else if (dialogueState == QUEST_ITEM_FOUND && ch == 'b')
    {
      Close();
      return;
    }
    else if (dialogueState == START_STATE && ch == 'b')
    {
      Close();
      return;
    }
    else if (dialogueState == ON_QUEST && ch == 'a')
    {
      Close();
      return;
    }
    else if (dialogueState == TABLET1 && ch == 'a')
    {
      GS.FactDb.Add(new SimpleFact() { Name = "Tablet1Translated", Value = "True" });
      Witch.Stats[Attribute.DialogueState] = new Stat(TRANSLATE_TABLET_1);
    }    
    else if (dialogueState == TABLET1 && ch == 'b')
    {
      Close();
      return;
    }
    else if (dialogueState == TRANSLATE_TABLET_1 && ch == 'a')
    {
      Close();
      return;
    }
    
    SetDialogueText();
    WritePopup();
  }

  void SetSpellMenu()
  {
    Options = [];
    int opt = 'a';

    int available = 0;
    int notYetAvailable = 0;
    foreach (string spell in Spells.Keys)
    {
      if (GS.Player.SpellsKnown.Contains(spell))
        continue;

      SpellInfo si = Spells[spell];
      if (si.ManaCost <= PlayerMana && (si.Prereq == "" || GS.Player.SpellsKnown.Contains(si.Prereq)))
      {
        char ch = (char)opt++;
        Blurb += $"{ch}) {spell.CapitalizeWords()} - [YELLOW $]{si.Price}\n";
        Options.Add(ch, spell);
        ++available;
      }
      else
      {
        notYetAvailable++;
      }
    }

    if (available == 0)
      Blurb += "\nThere's nothing I can teach you right now.\n";
    if (notYetAvailable > 0)
      Blurb += "\nThere are more spells I can impart when you are more powerful!\n";
  }

  void SetupQuest()
  {
    if (GS.Player.Stats[Attribute.Depth].Curr == 0)
    {
      Blurb = "I feel like you should see a little more of the world before you delve into the arcane arts. Magic benefits from the wisdom of experience.";
      Blurb += "\n\na) Farewell";
    }
    else
    {
      GS.FactDb.Add(new SimpleFact() { Name = "KylieQuest", Value = "begun" });

      Loc entrance = WitchQuest.QuestEntrance(GS);
      Console.WriteLine(entrance);
      var (dungeon, dungeonExit) = WitchQuest.GenerateDungeon(GS, entrance);
      GS.FactDb.Add(new LocationFact() { Desc = "KylieQuestEntrance", Loc = entrance });

      Downstairs stairs = new("")
      {
        Destination = dungeonExit
      };
      GS.Campaign.Dungeons[0].LevelMaps[0].SetTile(entrance.Row, entrance.Col, stairs);
      GS.Campaign.AddDungeon(dungeon, dungeon.ID);

      string entranceDir = Util.RelativeDir(Witch.Loc, entrance);
      Blurb = "Hmm, you're going to need a meditation crystal to get into the correct mindset for learning magic. ";
      Blurb += $"I'm fresh out, but you should be able to find one in a cave to the [ICEBLUE {entranceDir}]. Retrieve it, and we can get started on the curriculum!";
      Blurb += "\n\na) Farewell";

      Witch.Stats[Attribute.DialogueState].SetMax(ON_QUEST);
    }    
  }

  void SetDialogueText()
  {
    switch (DialogueState)
    {
      case BUY_SPELLS:        
        Blurb = "Hmm, here is what I can teach you.\n\n";
        SetSpellMenu();
        break;
      case TABLET1:
        Blurb = "That tablet you carry -- may I take a look at it?";
        Blurb += $"\n\na) Show {Witch.Name.Capitalize()} the tablet";
        Blurb += "\nb) Farewell";
        break;
      case TRANSLATE_TABLET_1:
        Blurb = "These runes are an ancient language but I think I can read them. It appears to be part ";
        Blurb += "of a warding or binding spell of immense power. If you find the other half of the tablet I can ";
        Blurb += "probably recreate the spell!";
        Blurb += "\n\na) Farewell";
        break;
      case GIVE_QUEST:
        SetupQuest();
        break;
      case QUEST_ITEM_FOUND:
        Blurb = "Great! You found one! Are you ready to learn some magic?";
        Blurb += "\n\na) Learn to cast spells";
        Blurb += "\nb) Farewell";
        break;
      case ON_QUEST:
        Loc questLoc = Loc.Nowhere;
        if (GS.FactDb.FactCheck("KylieQuestEntrance") is LocationFact fact)
          questLoc = fact.Loc;

        string entranceDir = Util.RelativeDir(Witch.Loc, questLoc);
        Blurb = "We won't be able to make much progress on your lessons without that crystal. ";
        Blurb += $"You should be able to find it in that cave off to the [ICEBLUE {entranceDir}]!";
        Blurb += "\n\na) Farewell";
        break;
       default:
        Options = [];
        if (PlayerMana > 0)
        {
          Blurb = GS.Rng.Next(5) switch
          {
            0 => "Looking for spells? You're better off studying with us than one of those shady underground magic dealers!",
            1 => "I studied library magic at Yendor University.",
            2 => "Casting spells is stressful. But Sophie's mushroom stew is great for fraught nerves!",
            3 => "Have you considered joining the Adventurers' Union?",
            _ => "You can't put undead to sleep with magic!"
          };
          Blurb += "\n\na) Learn some spells";
          Blurb += "\nb) Farewell";
        }
        else
        {
          if (GS.Rng.NextDouble() < 0.5)
            Blurb = "Need to learn the basics, huh? I used to TA Magic 101.";
          else
            Blurb = "Oh, anyone can learn magic. Don't listen to Big Thaumaturgy.";
          Blurb += "\n\na) Learn how to cast spells.";
          Blurb += "\nb) Farewell";
        }
        Options.Add('a', "");
        Options.Add('b', "");
        break;
    }
  }

  protected void WritePopup()
  {
    var sb = new StringBuilder(Witch.Appearance.Capitalize());
    sb.Append("\n\n");
    sb.Append(Blurb);
    
    GS.UIRef().SetPopup(new Popup(sb.ToString(), Witch.FullName, -1, -1));
  }

  public override UIResult GetResult()
  {
    return new ServiceResult()
    {
      Zorkminds = Invoice,
      Service = Service
    };
  }
}

class PriestInputer : Inputer
{
  readonly Actor Priest;
  readonly string Blurb;
  string Service { get; set; } = "";
  List<char> Options { get; set; } = [];

  public PriestInputer(Actor priest, string blurb, GameState gs) : base(gs)
  {
    Priest = priest;
    Blurb = blurb;
    
    WritePopup(blurb);
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == '\n' || ch == '\r' || ch == ' ')
    {
      return;
    }
    else if (Options.Contains(ch) && ch == 'a')
    {
      Service = "Absolution";
      return;
    }
    
    WritePopup(Blurb);
  }

  protected void WritePopup(string blurb)
  {
    var sb = new StringBuilder(Priest.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");
    sb.Append(Blurb);
    sb.Append("\n\n");

    Options = [];
    if (GS.Player.Inventory.Zorkmids >= 50)
    {
      sb.Append("a) Absolution. [YELLOW $]50\n");
      Options.Add('a');
    }
    else 
    {
      sb.Append("Ah child, Huntokar would expect a donation of at least 50 zorkmids for this service.\n");
    }

    GS.UIRef().SetPopup(new Popup(sb.ToString(), Priest.FullName, -1, -1));
  }

  public override UIResult GetResult()
  {
    return new ServiceResult()
    {
      Zorkminds = 50,
      Service = Service
    };
  }
}

class ShoppingUIResult : UIResult
{
  public List<(char, int)> Selections { get; set; } = [];
  public int Zorkminds { get; set; } = 0;
}

class RepairItemUIResult : UIResult
{
  public List<ulong> ItemIds { get; set; } = [];
  public int Zorkminds { get; set; } = 0;
}

class UpgradeItemUIResult : UIResult
{
  public char ItemSlot {  get; set; }
  public char ReagentSLot { get; set; }
  public int Zorkminds { get; set; } = 0;
}

class ServiceResult : UIResult
{
  public int Zorkminds { get; set; } = 0;
  public string Service { get; set; } = "";
}