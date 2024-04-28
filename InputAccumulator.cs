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

// Handers to keep state while we are waiting for user input. I'm sure someone
// smarter could come up with a cleaner solution but this is what I hit on
// when I decided to switch my Game Loop to be non-blocking

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

// I might be able to merge some code between this and AimAccumulator
class ExamineAccumulator : InputAccumulator
{
  readonly GameState _gs;
  readonly List<Loc> _targets = [];
  int _currTarget;
  (int, int) _curr;

  public ExamineAccumulator(GameState gs, Loc start)
  {
    _gs = gs;    
    FindTargets(start);
  }

  void FindTargets(Loc start)
  {
    var ui = _gs.UIRef();
    int startRow = _gs.Player.Loc.Row - ui.PlayerScreenRow;
    int startCol = _gs.Player.Loc.Col - ui.PlayerScreenCol;

    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        var loc = new Loc(start.DungeonID, start.Level, startRow + r, startCol + c);
        if (ui.SqsOnScreen[r, c] == Constants.BLANK_SQ)
          continue;
        
        if (_gs.ObjDb.Occupied(loc)) 
        {
          _targets.Add(loc);
          if (loc == _gs.Player.Loc) 
          {
            _currTarget = _targets.Count - 1;
            ui.ZLayer[r, c] = new Sqr(Colours.WHITE, Colours.HILITE, '@');
            _curr = (r, c);
          }
        }
        else if (_gs.ObjDb.ItemsAt(loc).Count > 0)
        {
          _targets.Add(loc);
        }
        else
        {
            var tile = _gs.TileAt(loc);
            switch (tile.Type)
            {
              case TileType.Upstairs:
              case TileType.Downstairs:
              case TileType.Portal:
              case TileType.Statue:
              case TileType.Landmark:
                _targets.Add(loc);
              break;
            }
        }
      }
    }
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;

      ClearHighlight();
      return;
    }
    else if (ch == Constants.TAB)
    {
      _currTarget = (_currTarget + 1) % _targets.Count;
      ClearHighlight();
      var loc = _targets[_currTarget];
      var (r, c) = _gs.UIRef().LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

      char locCh = LocInfo(loc);
      _gs.UIRef().ZLayer[r, c] = new Sqr(Colours.WHITE, Colours.HILITE, locCh);
      _curr = (r, c);
      _gs.LastPlayerFoV.Add(loc);
    }
  }

  char LocInfo(Loc loc)
  {
    if (_gs.ObjDb.Occupant(loc) is Actor actor)
      return actor.Glyph.Ch;
    
    var item = _gs.ObjDb.ItemGlyphAt(loc);
    if (item != GameObjectDB.EMPTY)
      return item.Ch;
      
    Tile tile = _gs.TileAt(loc);
    return Util.TileToGlyph(tile).Ch;
  }

  void ClearHighlight()
  {
    _gs.UIRef().ZLayer[_curr.Item1, _curr.Item2] = Constants.BLANK_SQ;
  }
}

class AimAccumulator : InputAccumulator
{
  readonly UserInterface _ui;
  readonly GameState _gs;
  Loc _start;
  Loc _target;
  readonly AimAnimation _anim;
  readonly int _maxRange;
  readonly List<Loc> _monsters = [];
  int _targeted = -1;

  public AimAccumulator(UserInterface ui, GameState gs, Loc start, int maxRange)
  {
    _ui = ui;
    _start = start;
    _target = start;
    _maxRange = maxRange;
    _gs = gs;
    FindTargets();

    _anim = new AimAnimation(_ui, gs, start, _target);
    _ui.RegisterAnimation(_anim);
  }

  void FindTargets()
  {
    foreach (var loc in _gs.LastPlayerFoV)
    {
      if (Util.Distance(loc, _start) <= _maxRange)
      {
        var occ = _gs.ObjDb.Occupant(loc);
        if (occ is null || occ.ID == _gs.Player.ID)
          continue;

        if (occ.HasActiveTrait<DisguiseTrait>() && occ.Stats.TryGetValue(Attribute.InDisguise, out var stat) && stat.Curr == 1)
          continue;

        _monsters.Add(loc);

        if (occ.ID == _gs.LastTarget)
        {
          _targeted = _monsters.Count - 1;
          _target = loc;
        }
      }
    }
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;

      ExpireAnimation();
      return;
    }
    else if (ch == Constants.TAB && _monsters.Count > 0)
    {
      int next = (++_targeted) % _monsters.Count;
      _target = _monsters[next];
      _anim.Target = _monsters[next];
      _targeted = next;
    }

    else if (ch == '\n' || ch == '\r')
    {
      Done = true;
      Success = true;

      ExpireAnimation();
      return;
    }

    var dir = Util.KeyToDir(ch);
    if (dir != (0, 0))
    {
      Loc mv = _target with
      {
        Row = _target.Row + dir.Item1,
        Col = _target.Col + dir.Item2
      };
      if (Util.Distance(_start, mv) <= _maxRange && _gs.CurrentMap.InBounds(mv.Row, mv.Col))
      {
        _target = mv;
        _anim.Target = mv;
      }
    }
  }

  void ExpireAnimation()
  {
    _anim.Expiry = DateTime.MinValue;
  }

  public override AccumulatorResult GetResult()
  {
    var result = new LocAccumulatorResult()
    {
      Loc = _target
    };

    var occ = _gs.ObjDb.Occupant(_target);
    if (occ is not null)
    {
      _gs.LastTarget = occ.ID;
    }

    return result;
  }
}

class NumericAccumulator(UserInterface ui, string prompt) : InputAccumulator
{
  UserInterface _ui = ui;
  string _prompt = prompt;
  string _value = "";

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

class DialogueAccumulator : InputAccumulator
{
  readonly Mob _interlocutor;
  readonly GameState _gs;
  HashSet<char> _currOptions = [];
  char _exitOpt = '\0';

  public DialogueAccumulator(Mob interlocutor, GameState gs)
  {
    _interlocutor = interlocutor;
    _gs = gs;

    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == _exitOpt)
    {
      var msg = new Message("Farewell.", _interlocutor.Loc);
      _gs.UIRef().AlertPlayer([msg], "", _gs);

      Done = true;
      Success = true;
    }
    else if (_currOptions.Contains(ch))
    {
      var dialgoue = (IDialoguer) _interlocutor.Behaviour;
      dialgoue.SelectOption(_interlocutor, ch, _gs);

      WritePopup();
    }
  }

  void WritePopup()
  {
    var dialgoue = _interlocutor.Behaviour as IDialoguer;

    var sb = new StringBuilder(_interlocutor.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");

    var (blurb, opts) = dialgoue.CurrentText(_interlocutor, _gs);

    if (blurb == "")
    {
      Done = true;
      Success = true;
      var msg = new Message($"{_interlocutor.FullName.Capitalize()} turns away from you.", _interlocutor.Loc);
      _gs.UIRef().AlertPlayer([msg], "", _gs);
    }
    else
    {
      sb.Append(blurb);
      sb.Append("\n\n");
      char c = '`';
      _currOptions = [];
      foreach (var opt in opts)
      {
        c = opt.Item2;
        _currOptions.Add(c);
        sb.Append($"{c}) {opt.Item1}\n");
      }
      ++c;
      _exitOpt = c;
      sb.Append($"{c}) Farewell.\n");

      _gs.WritePopup(sb.ToString(), _interlocutor.FullName);
    }
  }
}

class ShopMenuItem(char slot, Item item, int stockCount)
{
  public char Slot { get; set; } = slot;
  public Item Item { get; set; } = item;
  public int StockCount { get; set; } = stockCount;
  public int SelectedCount { get; set; } = 0;
}

class ShopMenuAccumulator : InputAccumulator
{
  readonly Mob _shopkeeper;
  readonly GameState _gs;
  readonly Dictionary<char, ShopMenuItem> _menuItems = [];
  readonly string _blurb;

  public ShopMenuAccumulator(Actor shopkeeper, string blurb, GameState gs)
  {
    _gs = gs;
    _blurb = blurb;
    _shopkeeper = (Mob)shopkeeper;
    var items = _shopkeeper.Inventory.UsedSlots()
                         .Select(_shopkeeper.Inventory.ItemAt);

    char ch = 'a';
    foreach (var (item, count) in items)
      _menuItems.Add(ch++, new ShopMenuItem(item.Slot, item, count));

    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if ((ch == '\n' || ch == '\r') && _gs.Player.Inventory.Zorkmids >= TotalInvoice())
    {
      Done = true;
      Success = true;
    }
    else if (_menuItems.ContainsKey(ch))
    {
      if (_menuItems[ch].StockCount == 1 && _menuItems[ch].SelectedCount == 0)
        _menuItems[ch].SelectedCount = 1;
      else if (_menuItems[ch].StockCount == 1)
        _menuItems[ch].SelectedCount = 0;
      else
      {
        ++_menuItems[ch].SelectedCount;
        if (_menuItems[ch].SelectedCount > _menuItems[ch].StockCount)
          _menuItems[ch].SelectedCount = 0;
      }
      WritePopup();
    }
  }

  public override AccumulatorResult GetResult() => new ShoppingAccumulatorResult()
  {
    Zorkminds = TotalInvoice(),
    Selections = _menuItems.Values.Where(i => i.SelectedCount > 0)
                                    .Select(i => (i.Slot, i.SelectedCount))
                                    .ToList()
  };

  int TotalInvoice()
  {
    double markup = _shopkeeper.Stats[Attribute.Markup].Curr / 100.0;
    return _menuItems.Values.Select(mi => mi.SelectedCount * (int)(mi.Item.Value * markup)).Sum();
  }

  void WritePopup()
  {
    var sb = new StringBuilder(_shopkeeper.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");
    sb.Append(_blurb);
    sb.Append("\n\n");

    var keys = _menuItems.Keys.ToList();
    keys.Sort();

    double markup = _shopkeeper.Stats[Attribute.Markup].Curr / 100.0;
    List<string> lines = [];
    foreach (var key in keys)
    {
      var line = new StringBuilder();
      line.Append(key);
      line.Append(") ");
      line.Append(_menuItems[key].Item.FullName);

      if (_menuItems[key].StockCount > 1)
      {
        line.Append(" (");
        line.Append(_menuItems[key].StockCount);
        line.Append(')');
      }

      int price = (int)(_menuItems[key].Item.Value * markup);
      line.Append(" - [YELLOW $]");
      line.Append(price);

      if (_menuItems[key].StockCount > 1)
        line.Append(" apiece");
      lines.Add(line.ToString());
    }

    int widest = lines.Select(l => l.Length).Max() + 2;
    int l = 0;
    foreach (var key in keys)
    {
      if (_menuItems[key].SelectedCount > 0)
      {
        sb.Append(lines[l].PadRight(widest + 2));
        if (_menuItems[key].StockCount == 1)
        {
          sb.Append("[GREEN *]");
        }
        else
        {
          sb.Append("[GREEN ");
          sb.Append(_menuItems[key].SelectedCount);
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

    if (invoice > _gs.Player.Inventory.Zorkmids)
    {
      sb.Append("[brightred You don't have enough money for all that!]");
    }

    _gs.WritePopup(sb.ToString(), _shopkeeper.FullName);
  }
}

class PickupAccumulator(HashSet<(char, ulong)> options) : InputAccumulator
{
  ulong _choice;
  readonly HashSet<(char, ulong)> _options = options;

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if (_options.Any(o => o.Item1 == ch))
    {
      Msg = "";
      ulong itemID = _options.Where(o => o.Item1 == ch).First().Item2;
      _choice = itemID;
      Done = true;
      Success = true;
    }
    else
    {
      Msg = "That doesn't seem ot exist.";
      Done = false;
      Success = false;
    }
  }

  public override AccumulatorResult GetResult() => new ObjIDAccumulatorResult()
  {
    ID = _choice
  };
}

class InventoryAccumulator(HashSet<char> options) : InputAccumulator
{
  char _choice;
  readonly HashSet<char> _options = options;

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
  bool _keyPressed;

  public override bool Success => _keyPressed;
  public override bool Done => _keyPressed;

  // We're done on any input 
  public override void Input(char ch) => _keyPressed = true;
}

class LongMessageAccumulator : InputAccumulator
{
  UserInterface _ui;
  int _row;
  IEnumerable<string> _lines;
  bool _done;
  int _pageCount = 1;

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
      txt = txt.PadLeft(UserInterface.ScreenWidth / 2 - txt.Length + txt.Length / 2, ' ');
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
  (int, int) _result;
  public DirectionAccumulator() => Done = false;

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else
    {
      var dir = Util.KeyToDir(ch);
      if (dir != (0, 0))
      {
        _result = dir;
        Done = true;
        Success = true;
      }
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

class AccumulatorResult { }

class LocAccumulatorResult : AccumulatorResult
{
  public Loc Loc { get; set; }
}

class DirectionAccumulatorResult : AccumulatorResult
{
  public int Row { get; set; }
  public int Col { get; set; }
}

class ObjIDAccumulatorResult: AccumulatorResult
{
  public ulong ID { get; set; }
}

class MenuAccumulatorResult : AccumulatorResult
{
  public char Choice { get; set; }
}

class NumericAccumulatorResult : AccumulatorResult
{
  public int Amount { get; set; }
}

class ShoppingAccumulatorResult : AccumulatorResult
{
  public List<(char, int)> Selections { get; set; } = [];
  public int Zorkminds { get; set; } = 0;
}