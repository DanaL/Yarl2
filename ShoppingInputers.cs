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

  public ShopMenuInputer(Actor shopkeeper, string blurb, GameState gs)
  {
    Gs = gs;
    Blurb = blurb;
    Shopkeeper = (Mob)shopkeeper;
    MenuItems = MenuFromInventory(Shopkeeper);

    WritePopup(blurb);
  }

  protected virtual Dictionary<char, ShopMenuItem> MenuFromInventory(Mob shopkeeper)
  {
    var items = shopkeeper.Inventory.UsedSlots()
                          .Select(shopkeeper.Inventory.ItemAt);

    Dictionary<char, ShopMenuItem> menuItems = [];
    char ch = 'a';
    double markup = Shopkeeper.Stats[Attribute.Markup].Curr / 100.0;
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

      int price = (int)(MenuItems[key].Price);
      line.Append(" - [YELLOW $]");
      line.Append(price);

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
      sb.Append("[brightred You don't have enough money for all that!]");
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
  public SmithyInputer(Actor shopkeeper, string blurb, GameState gs) : base(shopkeeper, blurb, gs)
  {
    if (!Shopkeeper.Stats.ContainsKey(Attribute.ShopMenu))
    {
      Shopkeeper.Stats.Add(Attribute.ShopMenu, new Stat(0));
    }

    bool hasRustyItem = false;
    foreach (Item item in Gs.Player.Inventory.Items())
    {
      if (item.HasTrait<RustedTrait>())
      {
        hasRustyItem = true;
        break;
      }
    }

    // Menu state:
    // 0 - Offer choice of shop/repair/upgrade
    // 1 - Shopping
    if (hasRustyItem) 
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

    WritePopup(blurb);
  }

  Dictionary<char, ShopMenuItem> RepairMenu()
  {
    var items = Shopkeeper.Inventory.UsedSlots()
                          .Select(Shopkeeper.Inventory.ItemAt);

    double markup = Shopkeeper.Stats[Attribute.Markup].Curr / 100.0;
    Dictionary<char, ShopMenuItem> menuItems = [];
    foreach (Item item in Gs.Player.Inventory.Items().OrderBy(i => i.Slot))
    {
      if (item.Traits.OfType<RustedTrait>().FirstOrDefault() is not RustedTrait rust)
        continue;
      int price = rust.Amount == Rust.Rusted ? 25 : 50;
      price = (int)(price * markup);
      menuItems.Add(item.Slot, new ShopMenuItem(item.Slot, item, 1, price));
    }

    return menuItems;
  }

  string RepairScreen()
  {
    StringBuilder sb = new();
    sb.Append("Would would you like repaired?\n\n");

    foreach (Item item in Gs.Player.Inventory.Items().OrderBy(i => i.Slot))
    {
      if (item.Traits.OfType<RustedTrait>().FirstOrDefault() is not RustedTrait rust)
        continue;

      sb.Append(item.Slot);
      sb.Append(") ");
      sb.Append(item.FullName);
      sb.Append(" - [YELLOW $]");
      if (rust.Amount == Rust.Rusted)
        sb.Append("25");
      else
        sb.Append("50");
      sb.Append('\n');
    }

    return sb.ToString();
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
      sb.Append("b) Repair gear.\n");

      dialogueText = sb.ToString();
    }    
    else if (menuState == 2)
    {
      dialogueText = MenuScreen("What would you like repaired?");
    }
    else
    {
      //Shopkeeper.Stats[Attribute.ShopMenu].SetMax(1);
      dialogueText = MenuScreen(Blurb);
    }

    Gs.UIRef().SetPopup(new Popup(dialogueText, Shopkeeper.FullName, -1, -1));
  }  
}