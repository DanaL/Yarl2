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

abstract class Inputer(GameState gs)
{
  public string Msg { get; set; } = "";
  public Action? DeferredAction { get; set; } = null;

  public abstract void Input(char ch);

  public virtual void OnUpdate() { }

  protected GameState GS { get; set; } = gs;

  protected void QueueDeferredAction()
  {
    if (DeferredAction is not null)
    {
      DeferredAction.ReceiveUIResult(GetResult());
      GS.Player.QueueAction(DeferredAction);
    }
  }

  protected void Close()
  {
    GS.UIRef().ClosePopup();
    GS.UIRef().SetInputController(new PlayerCommandController(GS));
  }
  
  public virtual UIResult GetResult()
  {
    return new UIResult();
  }
}

record LocDetails(string Title, string Desc, char Ch);

class DummyInputer(GameState gs) : Inputer(gs)
{
  public override void Input(char ch) { }
}

// I might be able to merge some code between this and AimAccumulator
class Examiner : Inputer
{
  readonly List<Loc> _targets = [];
  int _currTarget;
  (int, int) _curr;
  readonly Dictionary<string, CyclopediaEntry> _cyclopedia;
  static Colour highlightColour = Colours.ICE_BLUE with { Alpha = 175 };

  public Examiner(GameState gs, Loc start) : base(gs)
  {
    FindTargets(start);
    _cyclopedia = LoadCyclopedia();

    if (gs.Options.ShowHints)
      gs.UIRef().SetPopup(new Hint(["Hit TAB to see info", "about dungeon features"], gs.UIRef().PlayerScreenRow - 4));
  }

  void FindTargets(Loc start)
  {
    var ui = GS.UIRef();
    int startRow = GS.Player.Loc.Row - ui.PlayerScreenRow;
    int startCol = GS.Player.Loc.Col - ui.PlayerScreenCol;
    var pq = new PriorityQueue<Loc, int>();

    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        Loc loc = new(start.DungeonID, start.Level, startRow + r, startCol + c);

        // This is a bad kludge because at the moment, a scroll of magic 
        // mapping can reveal the floor's map, which I implement by adding 
        // them to RememberedSqs dictionary for the Dungeon. So, the player
        // now 'remembers' squares they haven't seen yet so they haven't seen
        // items on the floor. But later on this function we're looking for 
        // items at a location that's visible on screen, which was revealing
        // items the player didn't know about. What I need to do is perhaps
        // track actually squares seen separately from map tiles known. But for
        // now this will prevent most of the unknown items from appearing.
        switch (ui.SqsOnScreen[r, c].Ch)
        {
          case '.':
          case '\\':
          case '=':
            continue;
        }
        if (ui.SqsOnScreen[r, c] == Constants.BLANK_SQ)
          continue;

        if (GS.ObjDb.Occupant(loc) is Actor actor && Util.AwareOfActor(actor, GS))
        {
          int distance = Distance(GS.Player.Loc, loc);
          if (loc == GS.Player.Loc)
          {
            _currTarget = _targets.Count - 1;
            ui.ZLayer[r, c] = new Sqr(Colours.WHITE, highlightColour, '@');
            _curr = (r, c);
            distance = int.MaxValue;
          }

          pq.Enqueue(loc, distance);
        }
        else if (GS.ObjDb.VisibleItemsAt(loc).Count > 0)
        {
          pq.Enqueue(loc, Distance(GS.Player.Loc, loc));
        }
        else if (GS.ObjDb.EnvironmentsAt(loc).Count > 0)
        {
          pq.Enqueue(loc, Distance(GS.Player.Loc, loc));
        }
        else
        {
          var tile = GS.TileAt(loc);
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
            case TileType.WaterTrap:
            case TileType.MagicMouth:
            case TileType.Pit:
            case TileType.DartTrap:
            case TileType.Well:
            case TileType.GateTrigger:
            case TileType.Lever:
            case TileType.RevealedSummonsTrap:
            case TileType.BridgeTrigger:
              pq.Enqueue(loc, Distance(GS.Player.Loc, loc));
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
    if (ch == Constants.ESC || ch == '\n' || ch == '\r')
    {
      ClearHighlight();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
    }
    else if (ch == Constants.TAB)
    {
      _currTarget = (_currTarget + 1) % _targets.Count;
      ClearHighlight();
      var loc = _targets[_currTarget];
      var (r, c) = GS.UIRef().LocToScrLoc(loc.Row, loc.Col, GS.Player.Loc.Row, GS.Player.Loc.Col);

      LocDetails details = LocInfo(loc);
      GS.UIRef().ZLayer[r, c] = new Sqr(Colours.WHITE, highlightColour, details.Ch);
      GS.UIRef().SetPopup(new Popup(details.Desc, details.Title, r - 2, c));
      _curr = (r, c);
      GS.LastPlayerFoV.Add(loc);
    }
  }

  LocDetails LocInfo(Loc loc)
  {
    string name;
    string desc = "I have no further info about this object. This is probably Dana's fault.";

    if (GS.ObjDb.Occupant(loc) is Actor actor)
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

      string extraInfo = "";
      foreach (Trait t in actor.Traits)
      {
        if (t is PoisonedTrait)
          extraInfo += "[GREEN Poisoned]. ";
        else if (t is ConfusedTrait)
          extraInfo += "[YELLOW Confused]. ";
        else if (t is SleepingTrait)
          extraInfo += "[PINK Sleeping]. ";
        else if (t is TipsyTrait)
          extraInfo += "[PINK Tipsy]. ";
        else if (t is FrightenedTrait)
          extraInfo += "[YELLOW Frightened]. ";
      }

      if (actor.Stats.TryGetValue(Attribute.MobAttitude, out var attitude))
      {
        if (attitude.Curr == Mob.INDIFFERENT)
          extraInfo += "[WHITE Indifferent]. ";
        else if (attitude.Curr == Mob.INACTIVE)
          extraInfo += "[WHITE Inactive]. ";
      }

      extraInfo = extraInfo.Trim();
      if (extraInfo.Length > 0)
      {
        desc += "\n\n" + extraInfo;
      }

      return new LocDetails(name, desc, actor.Glyph.Ch);
    }

    List<Item> items = GS.ObjDb.ItemsAt(loc);
    if (items.Count > 0)
    {
      Item item = items[0];
      string title = item.FullName.IndefArticle().Capitalize();
      string details = "";
      if (item.HasTrait<DescriptionTrait>())
        details = item.Traits.OfType<DescriptionTrait>().First().Text;
      else if (_cyclopedia.TryGetValue(item.Name, out var v))
        details = v.Text;

      return new LocDetails(title, details, item.Glyph.Ch);
    }

    List<Item> env = GS.ObjDb.EnvironmentsAt(loc);
    if (env.Count > 0)
    {
      Item item = env[0];
      string title = item.Name.Capitalize();
      string details = "";
      if (_cyclopedia.TryGetValue(item.Name, out var v))
      {
        title = v.Title;
        details = v.Text;
      }

      return new LocDetails(title, details, item.Glyph.Ch);
    }

    Tile tile = GS.TileAt(loc);
    name = tile.Type.ToString().ToLower();
    if (_cyclopedia.TryGetValue(name, out var v2))
    {
      name = v2.Title;
      desc = v2.Text;
    }

    return new LocDetails(name.Capitalize(), desc, TileToGlyph(tile).Ch);
  }

  void ClearHighlight()
  {
    GS.UIRef().ZLayer[_curr.Item1, _curr.Item2] = Constants.BLANK_SQ;
    GS.UIRef().ClosePopup();
  }
}

class Aimer : Inputer
{
  readonly UserInterface _ui;
  Loc _start;
  Loc _target;
  readonly AimAnimation _anim;
  readonly int _maxRange;
  readonly List<Loc> _monsters = [];
  int _targeted = -1;

  public Aimer(GameState gs, Loc start, int maxRange) : base(gs)
  {
    _ui = gs.UIRef();
    _ui.ClosePopup();

    _start = start;
    _target = start;
    _maxRange = maxRange;
    FindTargets();

    // If there's only one valid target, select it by default
    if (_monsters.Count == 1)
    {
      _targeted = 0;
      _target = _monsters[0];
    }

    _anim = new AimAnimation(_ui, gs, start, _target);
    _ui.RegisterAnimation(_anim);

    if (gs.Options.ShowHints)
      gs.UIRef().SetPopup(new Hint(["Use movement keys or TAB to select target", "", "ENTER to select"], 3));
  }

  void FindTargets()
  {
    var ui = GS.UIRef();
    int startRow = GS.Player.Loc.Row - ui.PlayerScreenRow;
    int startCol = GS.Player.Loc.Col - ui.PlayerScreenCol;

    for (int r = 0; r < UserInterface.ViewHeight; r++)
    {
      for (int c = 0; c < UserInterface.ViewWidth; c++)
      {
        var loc = new Loc(_start.DungeonID, _start.Level, startRow + r, startCol + c);
        if (Distance(_start, loc) > _maxRange)
          continue;
        if (!GS.ObjDb.Occupied(loc) || loc == GS.Player.Loc)
          continue;

        if (GS.ObjDb.Occupant(loc) is Actor occ)
        {
          if (occ.HasActiveTrait<DisguiseTrait>() && occ.Stats.TryGetValue(Attribute.InDisguise, out var stat) && stat.Curr == 1)
            continue;

          // Bit of a hackey way to determine if something is visible, but this will cover monsters
          // seen via mind reading and such
          if (occ.Glyph.Ch == ui.SqsOnScreen[r, c].Ch)
          {
            _monsters.Add(loc);

            if (occ.ID == GS.LastTarget)
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
      Close();
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
    else if (ch == '\n' || ch == '\r' || ch == 'f')
    {
      ExpireAnimation();      
      QueueDeferredAction();
      Close();

      return;
    }

    var dir = KeyToDir(ch);
    if (dir != (0, 0))
    {
      Loc mv = _target with
      {
        Row = _target.Row + dir.Item1,
        Col = _target.Col + dir.Item2
      };
      if (Distance(_start, mv) <= _maxRange && GS.CurrentMap.InBounds(mv.Row, mv.Col))
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

    var occ = GS.ObjDb.Occupant(_target);
    if (occ is not null)
    {
      GS.LastTarget = occ.ID;
    }

    return result;
  }
}

class TextInputer : Inputer
{
  string Buffer { get; set; } = "";  
  string Prompt { get; set; } = "";

  public TextInputer(GameState gs, string prompt) : base(gs)
  {
    Prompt = prompt;
    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
      return;
    }
    else if (ch == Constants.BACKSPACE)
    {
      if (Buffer.Length > 0)
        Buffer = Buffer[..^1];      
    }
    else if (ch == '\n' || ch == '\r')
    {
      Close();
      QueueDeferredAction();
      return;
    }
    else
    {
      Buffer += ch;      
    }

    WritePopup();
  }

  void WritePopup()
  {
    int width = int.Max(Buffer.Length + 2, 25);    
    GS.UIRef().SetPopup(new Popup($"{Prompt}\n{Buffer}", "", -1, -1, width));
  }

  public override UIResult GetResult() => new StringUIResult() { Text = Buffer };
}

class NumericInputer : Inputer
{
  readonly string _prompt;
  string _value = "";

  public NumericInputer(GameState gs, string prompt) : base(gs)
  {
    _prompt = prompt;
    GS.UIRef().SetPopup(new Popup($"{_prompt}\n{_value}", "", -1, -1));
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
      return;
    }
    else if (ch == '\n' || ch == '\r')
    {
      Close();
      QueueDeferredAction();
      return;
    }
    else if (ch == Constants.BACKSPACE && _value.Length > 0)
    {
      _value = _value[..^1];
    }
    else if (char.IsDigit(ch))
    {
      _value += ch;
    }

    GS.UIRef().SetPopup(new Popup($"{_prompt}\n{_value}", "", -1, -1));
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

class HelpScreen : Inputer
{
  static readonly int PageSize = UserInterface.ScreenHeight - 6;
  readonly UserInterface _ui;
  Dictionary<char, HelpEntry> _entries;
  char _selected;
  readonly int _textAreaWidth;
  int _page = 0;

  public HelpScreen(GameState gs, UserInterface ui) : base(gs)
  {
    _ui = ui;
    _entries = [];

    var lines = File.ReadAllLines(ResourcePath.GetDataFilePath("help.txt"))
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
      _ui.ClearLongMessage();
      _ui.SetInputController(new PlayerCommandController(GS));
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

class MapView : Inputer
{
  public MapView(GameState gs) : base(gs) => DrawMap();
  int OffSetRow { get; set; } = 0;
  int OffSetCol { get; set; } = 0;
  int HalfHeight { get; set; } = UserInterface.ScreenHeight / 2;
  int HalfWidth { get; set; } = UserInterface.ScreenWidth / 2;

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == ' ' || ch == 'q')
    {
      Close();
      return;
    }

    switch (ch)
    {
      case 'j':
        OffSetRow += 5;
        break;
      case 'k':
        OffSetRow -= 5;
        break;
      case 'h':
        OffSetCol -= 5;
        break;
      case 'l':
        OffSetCol += 5;
        break;
      case 'y':
        OffSetRow -= 5;
        OffSetCol -= 5;
        break;
      case 'u':
        OffSetRow -= 5;
        OffSetCol += 5;
        break;
      case 'b':
        OffSetRow += 5;
        OffSetCol -= 5;
        break;
      case 'n':
        OffSetRow += 5;
        OffSetCol += 5;
        break;
    }

    DrawMap();
  }

  Sqr[,] CalcMap(GameState gs)
  {
    Dungeon dungeon = gs.Campaign.Dungeons[gs.CurrDungeonID];
    Dictionary<Loc, Glyph> remembered = dungeon.RememberedLocs;

    if (gs.CurrentMap.Width < UserInterface.ScreenWidth)
      OffSetCol = 0;
    if (gs.CurrentMap.Height < UserInterface.ScreenHeight)
      OffSetRow = 0;

    int row = gs.Player.Loc.Row + OffSetRow;
    int col = gs.Player.Loc.Col + OffSetCol;
    
    int startRow = row - HalfHeight;
    if (startRow < 0)
    {
      OffSetRow -= startRow;
      startRow = 0;
    }
    if (row + HalfHeight > gs.CurrentMap.Height)
    {
      int d = row + HalfHeight - gs.CurrentMap.Height;
      OffSetRow -= d;
      startRow -= d;
    }

    int startCol = col - HalfWidth;
    if (startCol < 0)
    {
      OffSetCol -= startCol;
      startCol = 0;
    }
    if (col + HalfWidth > gs.CurrentMap.Width)
    {
      int d = col + HalfWidth - gs.CurrentMap.Width;
      OffSetCol -= d;
      startCol -= d;
    }

    Sqr[,] sqs = new Sqr[UserInterface.ScreenHeight, UserInterface.ScreenWidth];
    for (int r = 0; r < UserInterface.ScreenHeight; r++)
    {
      for (int c = 0; c < UserInterface.ScreenWidth; c++)
      {
        int mapRow = startRow + r, mapCol = startCol + c;
        Loc loc = new(gs.CurrDungeonID, gs.CurrLevel, mapRow, mapCol);

        Sqr sq;
        if (remembered.TryGetValue(loc, out var g))
        {
          // Probably overthinking things but for the wilderness map I prefer
          // the lit tiles and for dungeons the unlit
          Colour fg = gs.InWilderness ? g.Lit : g.Unlit;
          sq = new(fg, Colours.BLACK, g.Ch);
        }
        else
        {
          sq = Constants.BLANK_SQ;
        }
        
        if (mapRow == gs.Player.Loc.Row && mapCol == gs.Player.Loc.Col)
          sq = new(Colours.WHITE, Colours.BLACK, '@');
        else if (sq.Ch == '>' || sq.Ch == '<' || sq.Ch == 'Ո')
          sq = sq with { Fg = Colours.WHITE };

        sqs[r, c] = sq;
      }
    }

    return sqs;
  }

  void DrawMap()
  {
    Sqr[,] sqrs = CalcMap(GS);

    GS.UIRef().SetPopup(new FullScreenPopup(sqrs));
  }
}

class OptionsScreen : Inputer
{
  int row = 0;
  const int numOfOptions = 7;

  public OptionsScreen(GameState gs) : base(gs) => WritePopup();

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
      return;
    }
    else if (ch == 'j')
    {
      row = (row + 1) % numOfOptions;
    }
    else if (ch == 'k')
    {
      --row;
      if (row < 0)
        row = numOfOptions - 1;
    }
    else if (ch == '\n' || ch == '\r' || ch == ' ')
    {
      if (row == 0)
        GS.Options.BumpToOpen = !GS.Options.BumpToOpen;
      else if (row == 1)
        GS.Options.BumpForLockedDoors = !GS.Options.BumpForLockedDoors;
      else if (row == 2)
        GS.Options.BumpToChat = !GS.Options.BumpToChat;
      else if (row == 3)
        GS.Options.HighlightPlayer = !GS.Options.HighlightPlayer;
      else if (row == 4)
        GS.Options.TorchLightAnimation = !GS.Options.TorchLightAnimation;
      else if (row == 5)
        GS.Options.ShowHints = !GS.Options.ShowHints;
      else if (row == 6)
        GS.Options.ShowTurns = !GS.Options.ShowTurns;

      GS.UIRef().SetOptions(GS.Options, GS);
    }

    WritePopup();
  }

  void WritePopup()
  {
    string bumpToOpen = GS.Options.BumpToOpen ? "On" : "Off";
    string bumpDoorMenu = GS.Options.BumpForLockedDoors ? "On" : "Off";
    string bumpToChat = GS.Options.BumpToChat ? "On" : "Off";
    string hilitePlayer = GS.Options.HighlightPlayer ? "On" : "Off";
    string torchAnim = GS.Options.TorchLightAnimation ? "On" : "Off";
    string hints = GS.Options.ShowHints ? "On" : "Off";
    string turns = GS.Options.ShowTurns ? "On" : "Off";
    List<string> menuItems = [
      $"Bump to open doors: {bumpToOpen}",
      $"Bump for locked door menu: {bumpDoorMenu}",
      $"Bump to chat: {bumpToChat}",
      $"Highlight player: {hilitePlayer}",
      $"Torchlight animation: {torchAnim}",
      $"Show command hints: {hints}",
      $"Show turns: {turns}"
    ];

    GS.UIRef().SetPopup(new PopupMenu("Options", menuItems) { SelectedRow = row });
  }
}

class WizardCommander : Inputer
{
  string Buffer { get; set; } = "";
  string ErrorMessage { get; set; } = "";

  public WizardCommander(GameState gs) : base(gs) => WritePopup();

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
      return;
    }
    else if (ch == Constants.BACKSPACE)
    {
      if (Buffer.Length > 0)
        Buffer = Buffer[..^1];
      ErrorMessage = "";
    }
    else if (ch == '\n' || ch == '\r')
    {
      DebugCommand cmd = new(GS);
      ErrorMessage = cmd.DoCommand(Buffer.Trim());

      Console.WriteLine(ErrorMessage);
      if (ErrorMessage == "")
      {
        GS.UIRef().ClosePopup();
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        return;
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
    GS.UIRef().SetPopup(new Popup(message, "Debug Command", -1, -1, width));
  }
}

class Dialoguer : Inputer
{
  readonly Mob _interlocutor;
  readonly IDialoguer _dialogue;
  HashSet<char> _currOptions = [];
  char _exitOpt = '\0';
  readonly int _popupWidth = 60;

  public Dialoguer(Mob interlocutor, GameState gs) : base(gs)
  {
    _interlocutor = interlocutor;
    
    // Cast and validate the dialogue interface upfront
    if (interlocutor.Behaviour is not IDialoguer dialogue)
    {
      throw new ArgumentException("Interlocutor must have IDialoguer behaviour", nameof(interlocutor));
    }
    _dialogue = dialogue;
    _dialogue.InitDialogue(interlocutor, gs);

    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == _exitOpt || ch == ' ')
    {
      EndConversation("Farewell.");
      return;
    }

    if (_interlocutor.Behaviour is NPCBehaviour npc && (ch == '\n' || ch == '\r'))
    {
      if (!npc.ConfirmChoices(_interlocutor, GS))
        return;
      EndConversation("Farewell!");
    }

    if (!_currOptions.Contains(ch))
    {
      return; // Ignore invalid inputs
    }

    try
    {
      _dialogue.SelectOption(_interlocutor, ch, GS);
      WritePopup();
    }
    catch (ConversationEnded ce)
    {
      EndConversation(ce.Message);
    }
  }

  void EndConversation(string text)
  {
    Close();
    GS.UIRef().AlertPlayer(text);
    QueueDeferredAction();    
  }

  void WritePopup()
  {
    var sb = new StringBuilder(_interlocutor.Appearance.Capitalize());
    sb.Append("\n\n");

    try
    {
      var (blurb, footer, opts) = _dialogue.CurrentText(_interlocutor, GS);

      if (string.IsNullOrEmpty(blurb))
      {
        EndConversation($"{_interlocutor.FullName.Capitalize()} turns away from you.");
        return;
      }

      sb.Append('"');
      sb.Append(blurb)
        .Append("\"\n\n");

      _currOptions = [];
      foreach (var (text, key) in opts)
      {
        _currOptions.Add(key);

        string optionPrefix = $"{key}) ";
        int availableWidth = _popupWidth - optionPrefix.Length;
        string remainingText = text;
        bool isFirstLine = true;

        while (remainingText.Length > availableWidth)
        {
          int splitPoint = availableWidth;
          while (splitPoint > 0 && remainingText[splitPoint - 1] != ' ')
          {
            splitPoint--;
          }

          if (splitPoint == 0)
          {
            splitPoint = availableWidth;
          }

          if (isFirstLine)
          {
            sb.AppendLine(optionPrefix + remainingText[..splitPoint].TrimEnd());
            isFirstLine = false;
          }
          else
          {
            sb.AppendLine($"\t{remainingText[..splitPoint].TrimEnd()}");
          }

          remainingText = remainingText[splitPoint..].TrimStart();
        }

        if (remainingText.Length > 0)
        {
          if (isFirstLine)
            sb.AppendLine(optionPrefix + remainingText);
          else
            sb.AppendLine($"\t{remainingText}");
        }
      }

      _exitOpt = (char)(opts.Count > 0 ? opts[^1].Item2 + 1 : 'a');
      sb.AppendLine($"{_exitOpt}) Farewell.");

      sb.Append(footer);

      GS.UIRef().SetPopup(new Popup(sb.ToString(), _interlocutor.FullName, -2, -1, _popupWidth));
    }
    catch (ConversationEnded ce)
    {
      EndConversation(ce.Message);
    }
  }
}

class PickupMenu : Inputer
{
  List<Item> Items { get; set; }
  Dictionary<char, ulong> MenuOptions { get; set; } = [];
  List<char> Choices { get; set; } = [];

  public PickupMenu(List<Item> items, GameState gs) : base(gs)
  {
    Items = items;
    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == ' ')
    {
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
    }
    else if (MenuOptions.ContainsKey(ch) && !Choices.Contains(ch))
    {
      Choices.Add(ch);
    }
    else if (MenuOptions.ContainsKey(ch) && Choices.Contains(ch))
    {
      Choices.Remove(ch);
    }
    else if (ch == '\n' || ch == '\r')
    {
      QueueDeferredAction();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
    }

    WritePopup();
  }

  public override UIResult GetResult()
  {
    LongListResult result = new();
    foreach (char c in Choices)
    {
      result.Values.Add(MenuOptions[c]);
    }

    return result;
  }

  void WritePopup()
  {
    StringBuilder sb = new();
    foreach (var (slot, desc) in CalcPickupMenu(GS.UIRef()))
    {
      string s = desc;
      if (Choices.Contains(slot))
        s += " [GREEN *]";
      sb.AppendLine($"[ICEBLUE {slot})] {s}");
    }

    if (Choices.Count == 0)
      sb.AppendLine("\nSelect items to pick up");
    else
      sb.AppendLine("\n-enter- to pick up items");
      
    GS.UIRef().SetPopup(new Popup(sb.ToString(), "Pick up what?", -1, -1, 30));
  }

  HashSet<(char, string)> CalcPickupMenu(UserInterface ui)
  {
    Dictionary<Item, int> counts = [];
    foreach (var item in Items)
    {
      if (item.HasTrait<StackableTrait>() && counts.TryGetValue(item, out int value))
        counts[item] = value + 1;
      else
        counts.Add(item, 1);
    }

    HashSet<(char, string)> options = [];
    char slot = 'a';
    foreach (var (item, count) in counts)
    {      
      string desc;
      if (count > 1)
      {
        desc = $"{count} {item.FullName.Pluralize()}";
      }
      else if (item.Type == ItemType.Zorkmid)
      {
        desc = $"{item.Value} zorkmid";
        if (item.Value != 1)
          desc += "s";
      }
      else
      {
        desc = item.FullName;
      }

      MenuOptions[slot] = item.ID;
      options.Add((slot++, desc));
    }
    
    return options;
  }
}

class LockedDoorMenu : Inputer
{
  UserInterface UI { get; set; }
  Loc Loc { get; set; }
  List<char> Options { get; set; } = ['F'];
  List<string> MenuItems { get; set; } = [];
  int Row = 0;
  Item? Tool { get; set; } = null;

  public LockedDoorMenu(UserInterface ui, GameState gs, Loc loc) : base(gs)
  {
    UI = ui;
    Loc = loc;
    SetMenu();
    WritePopup();
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
      return;
    }
    else if (Options.Contains(ch))
    {
      SetUpCommand(ch);
      return;
    }
    else if (ch == '\n' || ch == '\r')
    {
      SetUpCommand(Options[Row]);
      return;
    }
    else if (ch == 'j')
    {
      Row = (Row + 1) % Options.Count;
    }
    else if (ch == 'k')
    {
      --Row;
      if (Row < 0)
        Row = Options.Count - 1;
    }

    WritePopup();
  }

  void SetUpCommand(char ch)
  {
    switch (ch)
    {
      case 'F':
        SetUpBash();
        break;
      case 'a':
        SetUpPickLock();
        break;
      case 'r':
        SetUpKnock();
        break;
      case 'c':
        SetUpPickAxe();
        break;
    }
  }

  void SetUpPickAxe()
  {
    foreach (Item item in GS.Player.Inventory.Items())
    {
      if (item.HasTrait<DiggingToolTrait>())
      {
        Loc playerLoc = GS.Player.Loc;        
        DigAction dig = new(GS, GS.Player, item);
        DirectionUIResult res = new() { Row = Loc.Row - playerLoc.Row, Col = Loc.Col - playerLoc.Col };
        dig.ReceiveUIResult(res);
        GS.Player.QueueAction(dig);
        Close();
        return;
      }
    }
  }

  void SetUpPickLock()
  {
    Loc playerLoc = GS.Player.Loc;    
    PickLockAction pickLock = new(GS, GS.Player, Tool!);
    DirectionUIResult res = new() { Row = Loc.Row - playerLoc.Row, Col = Loc.Col - playerLoc.Col };
    pickLock.ReceiveUIResult(res);
    GS.Player.QueueAction(pickLock);
    Close();
  }

  void SetUpKnock()
  {
    char slot = '\0';
    foreach (Item item in GS.Player.Inventory.Items())
    {
      if (item.Name == "scroll of knock")
      {
        slot = item.Slot;
        break;
      }
    }

    GS.Player.QueueAction(new UseItemAction(GS, GS.Player) { Choice = slot });
    Close();
  }

  void SetUpBash()
  {
    GS.Player.QueueAction(new BashAction(GS, GS.Player) { Target = Loc });
    Close();
  }

  void SetMenu()
  {
    MenuItems = ["F) kick it down"];

    bool lockpick = false;
    bool knock = false;
    bool pickaxe = false;
    foreach (Item item in GS.Player.Inventory.Items())
    {
      if (item.HasTrait<DoorKeyTrait>())
      {
        Tool = item;
        lockpick = true;
      }
      else if (item.Name == "scroll of knock")
        knock = true;
      else if (item.HasTrait<DiggingToolTrait>())
        pickaxe = true;
    }

    if (lockpick)
    {
      Options.Add('a');
      MenuItems.Add("a) pick lock");
    }

    if (knock)
    {
      Options.Add('r');
      MenuItems.Add("r) read scroll of knock");
    }

    if (pickaxe)
    {
      Options.Add('c');
      MenuItems.Add("c) chop door with pickaxe");
    }
  }

  void WritePopup()
  {
    PopupMenu menu = new("locked door  ", MenuItems) { SelectedRow = Row };
    UI.SetPopup(menu);
  }
}

class InventoryDetails : Inputer
{
  public string MenuTitle { get; set; } = "";
  public InvOption MenuOptions { get; set; } = InvOption.None;

  HashSet<char> Options { get; set; } = [];
  readonly Dictionary<string, CyclopediaEntry> Cyclopedia = [];
  HashSet<string> InteractionMenu { get; set; } = [];
  Item? SelectedItem { get; set; } = null;

  public InventoryDetails(GameState gs, string menuTile, InvOption menuOptions) : base(gs)
  {
    MenuTitle = menuTile;
    MenuOptions = menuOptions;
    Options = [.. GS.Player.Inventory.UsedSlots()];
    Cyclopedia = LoadCyclopedia();
    GS.Player.Inventory.ShowMenu(GS.UIRef(), new InventoryOptions() { Title = MenuTitle, Options = MenuOptions });
  }

  public override void Input(char ch)
  {
    UserInterface ui = GS.UIRef();
    GS.Player.Inventory.ShowMenu(ui, new InventoryOptions() { Title = MenuTitle, Options = MenuOptions });

    bool itemPopup = ui.ActivePopup;
    if (ch == ' ' || ch == '\n' || ch == '\r' || ch == Constants.ESC)
    {
      if (itemPopup)
      {
        Options = [.. GS.Player.Inventory.UsedSlots()];        
        ui.ClosePopup();
        SelectedItem = null;
      }
      else
      {
        ui.CloseMenu();
        ui.ClosePopup();
        ui.SetInputController(new PlayerCommandController(GS));
        return;
      }
    }    
    else if (itemPopup && Options.Contains(ch) && SelectedItem is not null)
    {
      SetUpItemCommand(SelectedItem, ch);
      return;
    }
    else if (Options.Contains(ch))
    {
      var (item, _) = GS.Player.Inventory.ItemAt(ch);
      if (item is null)
        return;

      SelectedItem = item;

      string title = item.FullName.Capitalize();
      string desc = "";
      if (item.Traits.OfType<DescriptionTrait>().SingleOrDefault() is { Text: var text })
        desc = text;
      else if (Item.IDInfo.TryGetValue(item.Name, out ItemIDInfo? info) && !info.Known)
        desc = "An item of unknown utility. You have not identified it yet.";
      else if (Cyclopedia.TryGetValue(item.Name, out CyclopediaEntry? entry))
        desc = entry.Text;

      int width = Math.Max(desc.Length, title.Length + 1);

      string extraDesc = ExtraDetails(item);
      if (extraDesc != "")
      {
        desc += "\n\n" + extraDesc;
      }
      width = Math.Max(width, extraDesc.Length);
      if (width > UserInterface.ScreenWidth - 10)
        width = -1;

      SetInteractionMenu(item);
      if (InteractionMenu.Count > 0)
      {
        Options.Clear();
        desc += "\n\n";
        if (InteractionMenu.Contains("use"))
        {
          desc += "[ICEBLUE a)] use item\n";
          Options.Add('a');
        }
        if (InteractionMenu.Contains("drop"))
        {
          desc += "[ICEBLUE d)] drop item\n";
          Options.Add('d');
        }
        if (InteractionMenu.Contains("equip"))
        {
          desc += "[ICEBLUE e)] equip item\n";
          Options.Add('e');
        }
        if (InteractionMenu.Contains("unequip"))
        {
          desc += "[ICEBLUE e)] unequip item\n";
          Options.Add('e');
        }
        if (InteractionMenu.Contains("throw"))
        {
          desc += "[ICEBLUE t)] throw item\n";
          Options.Add('t');
        }
      }

      ui.SetPopup(new Popup(desc, title, -1, -1, width));
    }
  }

  void SetUpItemCommand(Item item, char cmd)
  {
    switch (cmd)
    {
      case 'd':
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.Player.QueueAction(new DropItemAction(GS, GS.Player) { Choice = item.Slot });
        break;
      case 'a':
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.Player.QueueAction(new UseItemAction(GS, GS.Player) { Choice = item.Slot });
        break;
      case 't':
        GS.UIRef().SetInputController(new PlayerCommandController(GS));
        GS.Player.QueueAction(new ThrowSelectionAction(GS, GS.Player) { Choice = item.Slot });
        break;
      default:        
        ToggleEquippedAction toggle = new(GS, GS.Player) { Choice = item.Slot };
        GS.Player.QueueAction(toggle);
        break;
    }

    GS.UIRef().ClosePopup();
    GS.UIRef().CloseMenu();
  }

  void SetInteractionMenu(Item item)
  {
    InteractionMenu = [];

    bool equipedArmour = item.Type == ItemType.Armour && item.Equipped;
    if (!equipedArmour)
    {
      InteractionMenu.Add("drop"); InteractionMenu.Add("throw");
    }

    if (item.Equipable())
    {
      if (item.Equipped)
        InteractionMenu.Add("unequip");
      else
        InteractionMenu.Add("equip");
    }

    foreach (Trait t in item.Traits)
    {
      if (t is IUSeable)
        InteractionMenu.Add("use");
      else if (t is VaultKeyTrait)
        InteractionMenu.Add("use");
      else if (t is CanApplyTrait)
        InteractionMenu.Add("use");
    }

    if (item.IsUseableTool())
      InteractionMenu.Add("use");
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

class Inventorier(GameState gs, HashSet<char> options) : Inputer(gs)
{
  char _choice;
  readonly HashSet<char> _options = options;

  public override void Input(char ch)
  {
    if (ch == Constants.ESC || ch == ' ' || ch == '\n' || ch == '\r')
    {
      GS.UIRef().CloseMenu();
      Close();
    }
    else if (_options.Contains(ch))
    {
      Msg = "";
      _choice = ch;

      GS.UIRef().CloseMenu();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
      QueueDeferredAction();
    }
    else
    {
      Msg = "You don't have that.";

      GS.UIRef().CloseMenu();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
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

class PauseForMoreInputer(GameState gs) : Inputer(gs)
{
  // We're done on any input 
  public override void Input(char ch) {}
}

class LongMessagerInputer : Inputer
{
  UserInterface _ui;
  int _row;
  List<string> _wrappedLines;
  int _pageCount;
  
  public LongMessagerInputer(GameState gs, UserInterface ui, IEnumerable<string> lines) : base(gs)
  {
    _ui = ui;
    _wrappedLines = WrapLines(lines);

    var page = _wrappedLines.Take(UserInterface.ScreenHeight).ToList();
    ui.WriteLongMessage(page);
    _row = page.Count;
  }

  static List<string> WrapLines(IEnumerable<string> lines)
  {
    List<string> wrapped = [];

    foreach (string line in lines)
    {
      if (line.Length <= UserInterface.ScreenWidth)
      {
        wrapped.Add(line);
        continue;
      }

      string[] words = line.Split(' ');
      StringBuilder currentLine = new();

      foreach (var word in words)
      {
        if (currentLine.Length + word.Length + 1 > UserInterface.ScreenWidth)
        {
          wrapped.Add(currentLine.ToString().TrimEnd());
          currentLine.Clear();
        }

        if (currentLine.Length > 0)
          currentLine.Append(' ');
        currentLine.Append(word);
      }

      if (currentLine.Length > 0)
        wrapped.Add(currentLine.ToString());
    }

    return wrapped;
  }

  public override void Input(char ch)
  {
    if (_row >= _wrappedLines.Count)
    {
      _ui.ClearLongMessage();
      _ui.SetInputController(new PlayerCommandController(GS));
    }
    else
    {
      var page = _wrappedLines.Skip(_row).Take(UserInterface.ScreenHeight - 1).ToList();
      var txt = $"~ page {++_pageCount} ~";
      txt = txt.PadLeft(UserInterface.ScreenWidth / 2 - txt.Length + txt.Length / 2, ' ');
      page.Insert(0, txt);

      _ui.WriteLongMessage(page);
      _row += page.Count;
    }
  }
}

class YesOrNoInputer(GameState gs) : Inputer(gs)
{
  public override void Input(char ch)
  {
    // Need to eventually handle ESC
    if (ch == 'y')
    {
      Close();
      QueueDeferredAction();
    }
    else if (ch == 'n')
    { 
      Close();
    }
  }
}

class CharSetInputer(GameState gs, HashSet<char> allowed) : Inputer(gs)
{
  HashSet<char> Allowed { get; set; } = allowed;
  char Result { get; set; } = '\0';

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Close();
    }
    else if (Allowed.Contains(ch))
    {
      Result = ch;
      QueueDeferredAction();
      Close();
    }
  }

  public override UIResult GetResult() => new CharUIResult() { Ch = Result };
}

class DirectionalInputer : Inputer
{
  (int, int) _result;
  bool TargetSelf { get; set; }

  public DirectionalInputer(GameState gs, bool targetSelf = false) : base(gs)
  {
    TargetSelf = targetSelf;
    
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
      Close();
    }
    else
    {
      var dir = KeyToDir(ch);
      if (dir != (0, 0))
      {
        _result = dir;
        
        Close();
        QueueDeferredAction();
      }
      else if (TargetSelf && ch == '.')
      {
        _result = (0, 0);
        
        Close();
        QueueDeferredAction();
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

class ConeTargeter : Inputer
{
  int Range { get; set; }
  Loc Origin { get; set; }
  Loc Target { get; set; }

  public HashSet<DamageType> IncludeDamagedBy { get; set; } = [];

  ConeAnimation Anim { get; set; }

  public ConeTargeter(GameState gs, int range, Loc origin, HashSet<DamageType> damageTypes) : base(gs)
  {
    GS = gs;
    Range = range;
    Origin = origin;
    Target = origin with { Row = origin.Row - 1 };
    IncludeDamagedBy = damageTypes;
    
    UserInterface ui = gs.UIRef();

    Anim = new ConeAnimation(GS.UIRef(), GS, Origin, Target, Range, damageTypes);

    ui.RegisterAnimation(Anim);

    if (gs.Options.ShowHints)
      gs.UIRef().SetPopup(new Hint(["Select direction with movement keys", "", "ENTER to select"], 3));
  }

  public override void Input(char ch)
  {
    if (ch == Constants.ESC)
    {
      Anim.Expiry = DateTime.MinValue;
      GS.UIRef().ClosePopup();
      GS.UIRef().SetInputController(new PlayerCommandController(GS));
      return;
    }
    else if (ch == '\n' || ch == '\r')
    {
      Anim.Expiry = DateTime.MinValue;
      QueueDeferredAction();
      return;
    }

    var dir = KeyToDir(ch);
    if (dir != (0, 0))
    {
      Target = Origin with { Row = Origin.Row + Range * dir.Item1, Col = Origin.Col + Range * dir.Item2 };
      Anim.Target = Target;
    }
  }

  public override UIResult GetResult()
  {
    Map map = GS.Campaign.Dungeons[Origin.DungeonID].LevelMaps[Origin.Level];

    return new AffectedLocsUIResult()
    {
      Affected = ConeCalculator.Affected(Range, Origin, Target, map, GS.ObjDb, IncludeDamagedBy)
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

class LongListResult : UIResult
{
  public List<ulong> Values { get; set; } = [];
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

class StringUIResult : UIResult
{
  public string Text { get; set; } = "";
}

class CharUIResult : UIResult
{
  public char Ch { get; set; }
}

class AffectedLocsUIResult : UIResult
{
  public List<Loc> Affected { get; set; } = [];
}