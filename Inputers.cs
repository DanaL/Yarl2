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
using static Yarl2.Util;

namespace Yarl2;

// Handers to keep state while we are waiting for user input. I'm sure someone
// smarter could come up with a cleaner solution but this is what I hit on
// when I decided to switch my Game Loop to be non-blocking

abstract class Inputer
{
  public virtual bool Success { get; set; }
  public virtual bool Done { get; set; }
  public string Msg { get; set; } = "";

  public abstract void Input(char ch);

  public virtual UIResult GetResult()
  {
    return new UIResult();
  }
}

record LocDetails(string Title, string Desc, char Ch);

// I might be able to merge some code between this and AimAccumulator
class Examiner : Inputer
{
  readonly GameState _gs;
  readonly List<Loc> _targets = [];
  int _currTarget;
  (int, int) _curr;
  readonly Dictionary<string, CyclopediaEntry> _cyclopedia;

  public Examiner(GameState gs, Loc start)
  {
    _gs = gs;
    FindTargets(start);
    _cyclopedia = LoadCyclopedia();
  }

  void FindTargets(Loc start)
  {
    var ui = _gs.UIRef();
    int startRow = _gs.Player.Loc.Row - ui.PlayerScreenRow;
    int startCol = _gs.Player.Loc.Col - ui.PlayerScreenCol;
    var pq = new PriorityQueue<Loc, int>();

    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        var loc = new Loc(start.DungeonID, start.Level, startRow + r, startCol + c);
        if (ui.SqsOnScreen[r, c] == Constants.BLANK_SQ)
          continue;

        if (_gs.ObjDb.Occupied(loc) && _gs.LastPlayerFoV.Contains(loc))
        {
          int distance = Distance(_gs.Player.Loc, loc);          
          if (loc == _gs.Player.Loc)
          {
            _currTarget = _targets.Count - 1;
            ui.ZLayer[r, c] = new Sqr(Colours.WHITE, Colours.HILITE, '@');
            _curr = (r, c);
            distance = int.MaxValue;
          }

          pq.Enqueue(loc, distance);
        }
        else if (_gs.ObjDb.ItemsAt(loc).Count > 0)
        {
          pq.Enqueue(loc, Distance(_gs.Player.Loc, loc));          
        }
        else
        {
          var tile = _gs.TileAt(loc);
          switch (tile.Type)
          {
            case TileType.Upstairs:
            case TileType.Downstairs:
            case TileType.Portal:
            case TileType.Landmark:
            case TileType.TrapDoor:
            case TileType.Portcullis:
            case TileType.OpenPortcullis:
            case TileType.TeleportTrap:
            case TileType.JetTrigger:
            case TileType.FireJetTrap:
            case TileType.Pit:
              pq.Enqueue(loc, Util.Distance(_gs.Player.Loc, loc));              
              break;
          }
        }
      }
    }

    while (pq.Count > 0)
    {
      _targets.Add(pq.Dequeue());
    }
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = true;
      ClearHighlight();
    }
    else if (ch == Constants.TAB)
    {
      _currTarget = (_currTarget + 1) % _targets.Count;
      ClearHighlight();
      var loc = _targets[_currTarget];
      var (r, c) = _gs.UIRef().LocToScrLoc(loc.Row, loc.Col, _gs.Player.Loc.Row, _gs.Player.Loc.Col);

      LocDetails details = LocInfo(loc);
      _gs.UIRef().ZLayer[r, c] = new Sqr(Colours.WHITE, Colours.HILITE, details.Ch);
      _gs.UIRef().SetPopup(new Popup(details.Desc, details.Title, r - 2, c));
      _curr = (r, c);
      _gs.LastPlayerFoV.Add(loc);
    }
  }

  LocDetails LocInfo(Loc loc)
  {
    string name;
    string desc = "I have no further info about this object. This is probably Dana's fault.";

    if (_gs.ObjDb.Occupant(loc) is Actor actor)
    {
      if (actor is Player)
      {
        name = actor.Name;
        desc = "You. A stalwart, rugged adventurer (probably). Keen for danger and glory. Currently alive.";
      }
      else if (actor.HasTrait<VillagerTrait>())
      {
        name = actor.FullName.Capitalize();
        desc = "A villager.";
      }      
      else
      {
        name = actor.Name.IndefArticle().Capitalize();
        if (_cyclopedia.TryGetValue(actor.Name, out var v))
          desc = v.Text;
      }

      return new LocDetails(name, desc, actor.Glyph.Ch);
    }

    var items = _gs.ObjDb.ItemsAt(loc);
    if (items.Count > 0)
    {
      Item item = items[0];
      string title = "";
      string details = "";
      if (item.Type == ItemType.Statue && item.HasTrait<DescriptionTrait>())
        details = item.Traits.OfType<DescriptionTrait>().First().Text;
      else if (_cyclopedia.TryGetValue(item.Name, out var v))
        details = v.Text;
      else
        title = item.Name.IndefArticle().Capitalize();
      
      return new LocDetails(title, details, item.Glyph.Ch);
    }

    Tile tile = _gs.TileAt(loc);
    name = tile.Type.ToString().ToLower();
    if (_cyclopedia.TryGetValue(name, out var v2))
    {
      name = v2.Title;
      desc = v2.Text;
    }

    return new LocDetails(name.Capitalize(), desc, Util.TileToGlyph(tile).Ch);
  }

  void ClearHighlight()
  {
    _gs.UIRef().ZLayer[_curr.Item1, _curr.Item2] = Constants.BLANK_SQ;
    _gs.UIRef().ClosePopup();
  }
}

class Aimer : Inputer
{
  readonly UserInterface _ui;
  readonly GameState _gs;
  Loc _start;
  Loc _target;
  readonly AimAnimation _anim;
  readonly int _maxRange;
  readonly List<Loc> _monsters = [];
  int _targeted = -1;

  public Aimer(GameState gs, Loc start, int maxRange)
  {
    _ui = gs.UIRef();
    _start = start;
    _target = start;
    _maxRange = maxRange;
    _gs = gs;
    FindTargets();

    // If there's only one valid target, select it by default
    if (_monsters.Count == 1)
    {
      _targeted = 0;
      _target = _monsters[0];
    }

    _anim = new AimAnimation(_ui, gs, start, _target);
    _ui.RegisterAnimation(_anim);
  }

  void FindTargets()
  {
    var ui = _gs.UIRef();
    int startRow = _gs.Player.Loc.Row - ui.PlayerScreenRow;
    int startCol = _gs.Player.Loc.Col - ui.PlayerScreenCol;

    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        var loc = new Loc(_start.DungeonID, _start.Level, startRow + r, startCol + c);
        if (Util.Distance(_start, loc) > _maxRange)
          continue;
        if (!_gs.ObjDb.Occupied(loc) || loc == _gs.Player.Loc)
          continue;

        if (_gs.ObjDb.Occupant(loc) is Actor occ)
        {
          if (occ.HasActiveTrait<DisguiseTrait>() && occ.Stats.TryGetValue(Attribute.InDisguise, out var stat) && stat.Curr == 1)
            continue;

          // Bit of a hackey way to determine if something is visible, but this will cover monsters
          // seen via mind reading and such
          if (occ.Glyph.Ch == ui.SqsOnScreen[r, c].Ch)
          {
            _monsters.Add(loc);

            if (occ.ID == _gs.LastTarget)
            {
              _targeted = _monsters.Count - 1;
              _target = loc;
            }
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

  public override UIResult GetResult()
  {
    var result = new LocUIResult()
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

class NumericInputer(UserInterface ui, string prompt) : Inputer
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

    _ui.SetPopup(new Popup($"{_prompt}\n{_value}", "", -1, -1));
  }

  public override UIResult GetResult()
  {
    if (int.TryParse(_value, out int result))
      return new NumericUIResult() { Amount = result };
    else
      return new NumericUIResult() { Amount = 0 };
  }
}

record HelpEntry(string Title, List<string> Entry);

class HelpScreenInputer : Inputer
{
  static readonly int PageSize = UserInterface.ScreenHeight - 6;
  readonly UserInterface _ui;
  Dictionary<char, HelpEntry> _entries;
  char _selected;
  readonly int _textAreaWidth;
  int _page = 0;

  public HelpScreenInputer(UserInterface ui)
  {
    _ui = ui;
    _entries = [];

    var lines = File.ReadAllLines("data/help.txt")
                    .Select(line => line.TrimEnd()).ToArray();
    int l = 0;
    char ch = 'a';
    string title;
    do
    {
      title = lines[l++].Trim();
      List<string> entry = [];
      while (lines[l].Trim() != "#")
      {
        entry.Add(lines[l++]);
      }
      _entries.Add(ch, new HelpEntry(title, entry));
      ch = (char)(ch + 1);
      ++l;
    }
    while (l < lines.Length);

    // Figure out how wide the menu column is
    int menuWidth = _entries.Values.Select(v => v.Title.Length).Max() + 6;
    _textAreaWidth = UserInterface.ScreenWidth - menuWidth;

    // Make the lines of text fit the screen width.
    ResizeEntries();

    _page = 0;
    _selected = 'a';
    WriteHelpScreen();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = true;
      _ui.ClearLongMessage();
      return;
    }
    else if (_entries.ContainsKey(ch))
    {
      _selected = ch;
      _page = 0;
    }
    else if (ch == ' ')
    {
      _page = (_page + 1) % CurrPageCount;
    }

    WriteHelpScreen();
  }

  int CurrPageCount
  {
    get
    {
      int lineCount = _entries[_selected].Entry.Count;
      int pageCount = lineCount / PageSize;
      if (lineCount - pageCount * PageSize > 0)
        ++pageCount;

      return pageCount;
    }
  }

  void WriteHelpScreen()
  {
    List<string> help = [];
    help.Add("");
    help.Add(" Help for the Harried Adventurer!");
    help.Add("");

    int menuWidth = UserInterface.ScreenWidth - _textAreaWidth;
    foreach (char k in _entries.Keys)
    {
      var entry = _entries[k];
      string line = $" ({k}) {entry.Title}";
      line = line.PadRight(menuWidth);
      line += '|';
      help.Add(line);
    }

    for (int l = 2 + _entries.Count; l < UserInterface.ScreenHeight; l++)
      help.Add("|".PadLeft(menuWidth + 1));

    WriteHelpEntry(help);
    _ui.WriteLongMessage(help);
  }

  List<string> SplitLine(string line)
  {
    List<string> pieces = [];

    while (line.Length >= _textAreaWidth - 2)
    {
      int c = _textAreaWidth - 3;
      while (c >= 0 && line[c] != ' ')
        --c;
      string slice = line[..c].TrimEnd();
      if (slice[0] == ' ' && slice[1] != ' ')
        slice = slice.Trim();
      pieces.Add(slice);
      line = line[c..];
    }
    if (line.Trim().Length > 0)
    {
      if (line.Length > 1 && line[0] == ' ' && line[1] != ' ')
        pieces.Add(line.Trim());
      else
        pieces.Add(line.TrimEnd());
    }

    return pieces;
  }

  void ResizeEntries()
  {
    foreach (var k in _entries.Keys)
    {
      List<string> reformated = [];
      var entry = _entries[k];
      foreach (var line in entry.Entry)
      {
        if (line.Length < _textAreaWidth - 2)
          reformated.Add(line);
        else
          reformated.AddRange(SplitLine(line));
      }

      _entries[k] = entry with { Entry = reformated };
    }
  }

  void WriteHelpEntry(List<string> help)
  {
    var entry = _entries[_selected];
    int l = 3;

    help[l++] += $" ** {entry.Title} **";

    List<string> lines;
    if (entry.Entry.Count > PageSize)
    {
      lines = entry.Entry.Skip(_page * PageSize)
                         .Take(PageSize)
                         .ToList();
      lines.Add("");
      lines.Add($"- SPACE for next page ({_page + 1} of {CurrPageCount}) -".PadLeft(_textAreaWidth / 2 + 4));
    }
    else
    {
      lines = entry.Entry;
    }

    foreach (var line in lines)
    {
      help[l++] += " " + line;
    }
  }
}

class WizardCommander : Inputer
{
  readonly GameState _gs;
  string Buffer { get; set; } = "";
  string ErrorMessage { get; set; } = "";

  public WizardCommander(GameState gs) 
  {
    _gs = gs;
    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else if (ch == Constants.BACKSPACE)
    {
      if (Buffer.Length > 0)
        Buffer = Buffer[..^1];
      ErrorMessage = "";
    }
    else if (ch == '\n' || ch == '\r')
    {    
      DebugCommand cmd = new(_gs);
      ErrorMessage = cmd.DoCommand(Buffer.Trim());

      Console.WriteLine(ErrorMessage);
      if (ErrorMessage == "")
      {
        Done = true;
        Success = true;
      }
    }
    else
    {
      Buffer += ch;
      ErrorMessage = "";
    }

    WritePopup();
  }

  void WritePopup()
  {
    string message = Buffer;
    if (!string.IsNullOrEmpty(ErrorMessage))
      message += $"\n\n[BRIGHTRED {ErrorMessage}]";

    int width = int.Max(message.Length + 2, 25);
    _gs.UIRef().SetPopup(new Popup(message, "Debug Command", -1, -1, width));
  }
}

class Dialoguer : Inputer
{
  readonly Mob _interlocutor;
  readonly GameState _gs;
  HashSet<char> _currOptions = [];
  char _exitOpt = '\0';

  public Dialoguer(Mob interlocutor, GameState gs)
  {
    _interlocutor = interlocutor;
    _gs = gs;

    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == _exitOpt)
    {
      EndConversation("Farewell.");
    }
    else if (_currOptions.Contains(ch))
    {
      try
      {
        var dialgoue = (IDialoguer)_interlocutor.Behaviour;
        dialgoue.SelectOption(_interlocutor, ch, _gs);

        WritePopup();
      }
      catch (ConversationEnded ce)
      {
        EndConversation(ce.Message);
      }
    }
  }

  void EndConversation(string text)
  {
    _gs.UIRef().AlertPlayer([text]);

    Done = true;
    Success = true;
  }

  void WritePopup()
  {
    var dialgoue = _interlocutor.Behaviour as IDialoguer;

    var sb = new StringBuilder(_interlocutor.Appearance.IndefArticle().Capitalize());
    sb.Append(".\n\n");

    string blurb;
    List<(string, char)> opts;
    try
    {
      (blurb, opts) = dialgoue!.CurrentText(_interlocutor, _gs);
    }
    catch (ConversationEnded ce)
    {
      EndConversation(ce.Message);
      return;
    }

    if (blurb == "")
    {
      EndConversation($"{_interlocutor.FullName.Capitalize()} turns away from you.");
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

      _gs.UIRef().SetPopup(new Popup(sb.ToString(), _interlocutor.FullName, -1, -1));
    }
  }
}

class PickUpper(HashSet<(char, ulong)> options) : Inputer
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
      Msg = "That doesn't seem to exist.";
      Done = false;
      Success = false;
    }
  }

  public override UIResult GetResult() => new ObjIdUIResult()
  {
    ID = _choice
  };
}

class InventoryDetails : Inputer
{
  GameState GameState { get; set; }
  HashSet<char> Options { get; set; }
  readonly Dictionary<string, CyclopediaEntry> Cyclopedia;

  public InventoryDetails(GameState gs) 
  {
    GameState = gs;
    Options = new(GameState.Player.Inventory.UsedSlots());
    Cyclopedia = LoadCyclopedia();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = true;
    }
    else if (ch == ' ')
    {
      GameState.UIRef().ClosePopup();
    }
    else if (Options.Contains(ch))
    {
      var (item, _) = GameState.Player.Inventory.ItemAt(ch);
      string title = item.FullName.Capitalize();
      string desc = "";
      if (item.Traits.OfType<DescriptionTrait>().SingleOrDefault() is { Text: var text })
        desc = text;
      else if (Item.IDInfo.TryGetValue(item.Name, out ItemIDInfo? info) && !info.Known)
        desc = "An item of unknown utility.";
      else if (Cyclopedia.TryGetValue(item.Name, out CyclopediaEntry? entry))
        desc = entry.Text;

      int width = desc.Length;
      
      string extraDesc = ExtraDetails(item);
      if (extraDesc != "")
      {
        desc += "\n\n" + extraDesc;
      }
      width = Math.Max(width, extraDesc.Length);
      if (width > UserInterface.ScreenWidth - 10)
        width = -1;
      GameState.UIRef().SetPopup(new Popup(desc, title, -1, -1, width));
    }
  }

  static string ExtraDetails(Item item)
  {
    List<string> items = [];
    foreach (var trait in item.Traits)
    {
      if (trait is WeaponSpeedTrait spd)
      {
        if (spd.Cost < 1.0)
          items.Add("it is a [GREEN quick] weapon");
        else if (spd.Cost > 1.0)
          items.Add("it is a [YELLOW slow] weapon");
      }
      if (trait is MetalTrait mt)
      {
        if (mt.Type == Metals.Silver)
          items.Add("it is made of silver");
        else if (mt.Type == Metals.Bronze)
          items.Add("it is made of bronze");
        else if (mt.Type == Metals.Mithril)
          items.Add("it is made of mithril");
      }
      if (trait is CleaveTrait)
        items.Add("it can [BRIGHTRED cleave] enemies");
      if (trait is TwoHandedTrait)
        items.Add("it must be wielded with two hands");
      if (trait is VersatileTrait)
        items.Add("it may be wielded in both hands or with a shield");
      if (trait is ViciousTrait)
        items.Add($"this vicious weapon deals [BRIGHTRED extra damage]");
      if (trait is RustedTrait)
        items.Add("it is [DULLRED rusty], but can be repaired");
    }

    if (items.Count == 0)
      return "";

    return string.Join("; ", items).Capitalize() + ".";
  }
}

class Inventorier(HashSet<char> options) : Inputer
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

  public override UIResult GetResult()
  {
    return new MenuUIResult()
    {
      Choice = _choice
    };
  }
}

class PauseForMoreInputer : Inputer
{
  bool _keyPressed;

  public override bool Success => _keyPressed;
  public override bool Done => _keyPressed;

  // We're done on any input 
  public override void Input(char ch) => _keyPressed = true;
}

class LongMessagerInputer : Inputer
{
  UserInterface _ui;
  int _row;
  IEnumerable<string> _lines;
  bool _done;
  int _pageCount = 1;

  public override bool Done => _done;
  public override bool Success => true;

  public LongMessagerInputer(UserInterface ui, IEnumerable<string> lines)
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

class YesOrNoInputer : Inputer
{
  public YesOrNoInputer() => Done = false;

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

class DirectionalInputer : Inputer
{
  (int, int) _result;
  bool TargetSelf { get; set; }

  public DirectionalInputer(GameState gs, bool targetSelf = false)
  {
    TargetSelf = targetSelf;
    Done = false;

    string prompt = "Which direction?";
    int width = prompt.Length + 2;
    if (targetSelf) 
    {
      prompt += "\n(hit '.' to target your location.)";
      width = 35;
    }
    gs.UIRef().SetPopup(new Popup(prompt, "", gs.UIRef().PlayerScreenRow - 8, -1, width));
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Done = true;
      Success = false;
    }
    else
    {
      var dir = KeyToDir(ch);
      if (dir != (0, 0))
      {
        _result = dir;
        Done = true;
        Success = true;
      }
      else if (TargetSelf && ch == '.')
      {
        _result = (0, 0);
        Done = true;
        Success = true;
      }
    }
  }

  public override UIResult GetResult()
  {
    return new DirectionUIResult()
    {
      Row = _result.Item1,
      Col = _result.Item2
    };
  }
}

class UIResult { }

class LocUIResult : UIResult
{
  public Loc Loc { get; set; }
}

class DirectionUIResult : UIResult
{
  public int Row { get; set; }
  public int Col { get; set; }
}

class ObjIdUIResult : UIResult
{
  public ulong ID { get; set; }
}

class MenuUIResult : UIResult
{
  public char Choice { get; set; }
}

class NumericUIResult : UIResult
{
  public int Amount { get; set; }
}
