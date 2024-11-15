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
  protected GameState Gs { get; set; }
  protected Dictionary<char, ShopMenuItem> MenuItems { get; set; } = [];
  protected string Blurb { get; set; }
  protected bool SingleSelection { get; set; } = false;

  public ShopMenuInputer(Actor shopkeeper, string blurb, GameState gs)
  {
    Gs = gs;
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
    double markup = Shopkeeper.Stats[Attribute.Markup].Curr / 100.0;
    if (Gs.Player.HasTrait<LikeableTrait>())
      markup -= 0.33;
    
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
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if ((ch == '\n' || ch == '\r') && Gs.Player.Inventory.Zorkmids >= TotalInvoice())
    {
      Done = true;
      Success = true;
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
    Selections = MenuItems.Values.Where(i => i.SelectedCount > 0)
                                    .Select(i => (i.Slot, i.SelectedCount))
                                    .ToList()
  };

  protected int TotalInvoice() => MenuItems.Values.Select(mi => mi.SelectedCount * mi.Price).Sum();

  protected virtual string MenuScreen(string blurb)
  {
    var sb = new StringBuilder(Shopkeeper.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");
    sb.Append(blurb);
    sb.Append("\n\n");

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

    if (invoice > Gs.Player.Inventory.Zorkmids)
    {
      sb.Append("\n[brightred You don't have enough money for all that!]");
    }
    else
    {
      sb.Append("\n(Enter to accept)");
    }

    return sb.ToString();
  }

  protected virtual void WritePopup(string blurb)
  {
    string inventoryScreen = MenuScreen(blurb);  

    Gs.UIRef().SetPopup(new Popup(inventoryScreen, Shopkeeper.FullName, -1, -1));
  }
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
    foreach (Item item in Gs.Player.Inventory.Items())
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
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
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
      Gs.Player.ReplacePendingAction(new ShoppingCompletedAction(Gs, Shopkeeper), this);
      base.Input(ch);
    }
    else if (menuState == 2)
    {
      Gs.Player.ReplacePendingAction(new RepairItemAction(Gs, Shopkeeper), this);
      blurb = "What would you like repaired?";
      base.Input(ch);
    }
    else if (menuState == 3 && MenuItems.ContainsKey(ch))
    {
      Gs.Player.ReplacePendingAction(new UpgradeItemAction(Gs, Shopkeeper), this);
      _itemToEnchant = ch;
      Shopkeeper.Stats[Attribute.ShopMenu].SetMax(4);
      MenuItems = ReagentMenu();      
    }
    else if (menuState == 4)
    {
      SingleSelection = true;
      base.Input(ch);
    }

    WritePopup(blurb);
  }

  Dictionary<char, ShopMenuItem> ReagentMenu()
  {
    Dictionary<char, ShopMenuItem> menuItems = [];

    double markup = CalcMarkup();
    var reagents = Gs.Player.Inventory.Items()
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
    foreach (Item item in Gs.Player.Inventory.Items().OrderBy(i => i.Slot))
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
    var items = Shopkeeper.Inventory.UsedSlots()
                          .Select(Shopkeeper.Inventory.ItemAt);

    double markup = CalcMarkup();
    Dictionary<char, ShopMenuItem> menuItems = [];
    int slot = 0;
    foreach (Item item in Gs.Player.Inventory.Items().OrderBy(i => i.Slot))
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
      //Shopkeeper.Stats[Attribute.ShopMenu].SetMax(1);
      dialogueText = MenuScreen("What shall we try to upgrade?");
    }
    else if (menuState == 4)
    {
      var (item, _) = Gs.Player.Inventory.ItemAt(_itemToEnchant);
      string txt = $"Try to enchant your [ICEBLUE {item!.FullName}] with what?";
      dialogueText = MenuScreen(txt);
    }
    else
    {
      dialogueText = "Hmm this shouldn't happen?";
    }

    Gs.UIRef().SetPopup(new Popup(dialogueText, Shopkeeper.FullName, -1, -1));
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
    else
    {
      return new ShoppingUIResult()
      {
        Zorkminds = TotalInvoice(),
        Selections = MenuItems.Values.Where(i => i.SelectedCount > 0)
                                    .Select(i => (i.Slot, i.SelectedCount))
                                    .ToList()
      };
    }    
  }
}

class PriestInputer : Inputer
{
  readonly Actor Priest;
  readonly string Blurb;
  readonly GameState Gs;
  string Service { get; set; } = "";
  List<char> Options { get; set; } = [];

  public PriestInputer(Actor priest, string blurb, GameState gs)
  {
    Priest = priest;
    Blurb = blurb;
    Gs = gs;

    WritePopup(blurb);
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if (Options.Contains(ch) && ch == 'a')
    {
      Done = true;
      Success = true;
      Service = "Absolution";
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
    if (Gs.Player.Inventory.Zorkmids >= 50)
    {
      sb.Append("a) Absolution. [YELLOW $]50\n");
      Options.Add('a');
    }
    else 
    {
      sb.Append("Ah child, Huntokar would expect a donation of at least 50 zorkmids for this service.\n");
    }

    Gs.UIRef().SetPopup(new Popup(sb.ToString(), Priest.FullName, -1, -1));
  }

  public override UIResult GetResult()
  {
    return new PriestServiceUIResult()
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

class UpgradeItemUIResult : UIResult
{
  public char ItemSlot {  get; set; }
  public char ReagentSLot { get; set; }
  public int Zorkminds { get; set; } = 0;
}

class PriestServiceUIResult : UIResult
{
  public int Zorkminds { get; set; } = 0;
  public string Service { get; set; } = "";
}