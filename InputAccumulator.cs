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

// Handers to keep state while we are waiting for user input.
// I'm sure someone smarter could come up with a cleaner 
// solution...

abstract class InputAccumulator 
{
    public virtual bool Success { get; set; }
    public virtual bool Done { get; set; }
    public string Msg { get; set; } = "";

    public abstract void Input(char ch);

    public virtual AccumulatorResult GetResult()
    {
        return new AccumulatorResult();
    }
}

class NumericAccumulator(UserInterface ui, string prompt) : InputAccumulator
{
    private UserInterface _ui = ui;
    private string _prompt = prompt;
    private string _value = "";

    public override void Input(char ch)
    {
        if (ch == Constants.ESC)
        {
            Done = true;
            Success = false;
        }
        else if (ch == '\n' || ch == '\r')
        {
            Done = true;
            Success = true;
        }
        else if (ch == Constants.BACKSPACE && _value.Length > 0)
        {
            _value = _value[..^1];
        }
        else if (char.IsDigit(ch))
        {
            _value += ch;
        }

        _ui.Popup($"{_prompt}\n{_value}");
    }

    public override AccumulatorResult GetResult()
    {
        if (int.TryParse(_value, out int result))
            return new NumericAccumulatorResult() { Amount = result };
        else
            return new NumericAccumulatorResult() { Amount = 0 };        
    }
}

class ShopMenuItem(char slot, Item item)
{
    public char Slot { get; set; } = slot;
    public Item Item { get; set; } = item;
    public int Count { get; set; } = 0;    
}

class ShopMenuAccumulator : InputAccumulator
{
    readonly Villager _shopkeeper;
    readonly UserInterface _ui;
    Dictionary<char, ShopMenuItem> _menuItems = [];
    
    public ShopMenuAccumulator(Villager shopkeeper, UserInterface ui)
    {
        _ui = ui;
        _shopkeeper = shopkeeper;
        var items = _shopkeeper.Inventory.UsedSlots()
                             .Select(_shopkeeper.Inventory.ItemAt);
        
        char ch = 'a';
        foreach (var item in items)
            _menuItems.Add(ch++, new ShopMenuItem(item.Slot, item));
            
        WritePopup();
    }
    
    public override void Input(char ch)
    {
        if (ch == Constants.ESC)
        {
            Done = true;
            Success = false;
        }
        else if (ch == '\n' || ch == '\r')
        {
            Done = true;
            Success = true;
        }
        else if (_menuItems.ContainsKey(ch))
        {
            var item = _menuItems[ch].Item;
            if (item.Count == 1 && _menuItems[ch].Count == 0)
                _menuItems[ch].Count = 1;
            else if (item.Count == 1)
                _menuItems[ch].Count = 0;
            else
            {
                ++_menuItems[ch].Count;
                if (_menuItems[ch].Count > _menuItems[ch].Item.Count)
                    _menuItems[ch].Count = 0;
            }
            WritePopup();
        }
    }

    public override AccumulatorResult GetResult()
    {
        var result = new ShoppingAccumulatorResult();

        //foreach (var (ch, num) in _selections)
        //{
        //    if (num > 0)
        //        result.Selections.Add((ch, num));
        //}

        return result;
    }

    int TotalInvoice()
    {
        return _menuItems.Values.Select(mi => mi.Count * (int) (mi.Item.Value * _shopkeeper.Markup)).Sum();
    }

    private void WritePopup()
    {
        var sb = new StringBuilder(_shopkeeper.Appearance.IndefArticle().Capitalize());
        sb.Append(".\n\n");
        sb.Append(_shopkeeper.ChatText());
        sb.Append("\n\n");

        var keys = _menuItems.Keys.ToList();
        keys.Sort();

        List<string> lines = [];
        foreach (var key in keys)
        {
            var line = new StringBuilder();
            line.Append(key);
            line.Append(") ");
            line.Append(_menuItems[key].Item.FullName);

            if (_menuItems[key].Item.Count > 1)
            {
                line.Append(" (");
                line.Append(_menuItems[key].Item.Count);
                line.Append(')');
            }

            int price = (int)(_menuItems[key].Item.Value * _shopkeeper.Markup);
            line.Append(" - [YELLOW $]");
            line.Append(price);
            
            if (_menuItems[key].Item.Count > 1)
                line.Append(" apiece");
            lines.Add(line.ToString());
        }

        int widest = lines.Select(l => l.Length).Max() + 2;
        int l = 0;
        foreach (var key in keys)
        {
            if (_menuItems[key].Count > 0)
            {
                sb.Append(lines[l].PadRight(widest + 2));
                if (_menuItems[key].Item.Count == 1) 
                {
                    sb.Append("[GREEN *]");
                }
                else
                {
                    sb.Append("[GREEN ");
                    sb.Append(_menuItems[key].Count);
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

        if (invoice > _ui.Player.Inventory.Zorkmids)
        {
            sb.Append("[brightred You don't have enough money for all that!]");
        }

        _ui.Popup(sb.ToString(), _shopkeeper.FullName);
    }
}

class InventoryAccumulator(HashSet<char> options) : InputAccumulator
{
    private char _choice;
    private HashSet<char> _options = options;

    public override void Input(char ch) 
    {
        if (ch == Constants.ESC)
        {
            Done = true;
            Success = false;
        }
        else if (_options.Contains(ch))
        {
            Msg = "";
            _choice = ch;
            Done = true;
            Success = true;
        }
        else
        {
            Msg = "You don't have that.";
            Done = false;
            Success = false;
        }
    }

    public override AccumulatorResult GetResult()
    {
        return new MenuAccumulatorResult()
        {
            Choice = _choice
        };
    }
}

class PauseForMoreAccumulator : InputAccumulator
{
    private bool _keyPressed;

    public override bool Success => _keyPressed;
    public override bool Done => _keyPressed;

    // We're done on any input 
    public override void Input(char ch) => _keyPressed = true;
}

class LongMessageAccumulator : InputAccumulator
{
    private UserInterface _ui;
    private int _row;
    private IEnumerable<string> _lines;
    private bool _done;
    private int _pageCount = 1;

    public override bool Done => _done;
    public override bool Success => true;

    public LongMessageAccumulator(UserInterface ui, IEnumerable<string> lines)
    {
        _ui = ui;       
        _lines = lines;

        _done = false;
        var page = _lines.Take(UserInterface.ScreenHeight).ToList();
        ui.WriteLongMessage(page);
        _row = page.Count;
    }

    public override void Input(char ch)
    {
        if (_row >= _lines.Count())
        {
            _done = true;
            _ui.ClearLongMessage();
        }
        else
        {
            var page = _lines.Skip(_row).Take(UserInterface.ScreenHeight - 1).ToList();
            var txt = $"~ page {++_pageCount} ~";
            txt = txt.PadLeft(UserInterface.ScreenWidth/2 - txt.Length + txt.Length/2, ' ');
            page.Insert(0, txt);

            _ui.WriteLongMessage(page);
            _row += page.Count;
        }
    }
}

class YesNoAccumulator : InputAccumulator
{    
    public YesNoAccumulator() => Done = false;

    public override void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            Done = true;
            Success = true;
        }
        else if (ch == 'n')
        {
            Done = true;
            Success = false;
        }
    }
}

class DirectionAccumulator : InputAccumulator
{
    private (int, int) _result;
    public DirectionAccumulator() => Done = false;

    public override void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
        {
            _result = (-1, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'u')
        {
            _result = (-1, 1);
            Done = true;
            Success = true;
        }
        else if (ch == 'h')
        {
            _result = (0, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'j')
        {
            _result = (1, 0);
            Done = true;
            Success = true;
        }
        else if (ch == 'k')
        {
            _result = (-1, 0);
            Done = true;
            Success = true;
        }
        else if (ch == 'l')
        {
            _result = (0, 1);
            Done = true;
            Success = true;
        }
        else if (ch == 'b')
        {
            _result = (1, -1);
            Done = true;
            Success = true;
        }
        else if (ch == 'n')
        {
            _result = (1, 1); 
            Done = true;
            Success = true;
        }
    }

    public override AccumulatorResult GetResult()
    {
        return new DirectionAccumulatorResult()
        {
            Row = _result.Item1,
            Col = _result.Item2
        };
    }
}

public class AccumulatorResult {}

public class DirectionAccumulatorResult : AccumulatorResult
{
    public int Row { get; set; }
    public int Col { get; set; }
}

public class MenuAccumulatorResult : AccumulatorResult
{
    public char Choice { get; set; }
}

public class NumericAccumulatorResult : AccumulatorResult
{
    public int Amount { get; set; }
}

public class ShoppingAccumulatorResult : AccumulatorResult
{
    public List<(char, int)> Selections { get; set; } = [];
    public int Zorkminds { get; set; } = 0;
}