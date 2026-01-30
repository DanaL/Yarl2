// Delve - A roguelike computer RPG
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

enum CheatSheetMode
{
  Messages = 0,
  Commands = 1,
  Movement = 2,
  MvMixed = 3
}

enum UIState { InMainMenu, InGame }

record struct MsgHistory(string Message, int Count)
{
  public readonly string Fmt => Count > 1 ? $"{Message} x{Count}" : Message;
}

// I think that the way development is proceeding, it's soon not going
// to make sense for SDLUserInterface and BLUserInterface to be subclasses
// of UserInterface. It's more like they are being relegated to primive 
// display terminals and I'm pull more logic up into the base class, so
// I'll probably move towards Composition instead of Inheritance
abstract class UserInterface
{
  public const int ScreenWidth = 80;
  public const int ScreenHeight = 32;
  public const int SideBarWidth = 30;
  public const int ViewWidth = ScreenWidth - SideBarWidth;
  public const int ViewHeight = ScreenHeight - 6;

  public abstract void UpdateDisplay(GameState? gs);
  public abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour);
  public abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour, Colour bgColour);
  public abstract void WriteSq(int row, int col, Sqr sq);
  public abstract void ClearScreen();
  public abstract void SetFontSize(int newSize);

  protected abstract GameEvent PollForEvent(bool pause = true);
  protected abstract void Blit(); // Is blit the right term for this? 'Presenting the screen'

  protected int FontSize;
  public int PlayerScreenRow { get; set; }
  public int PlayerScreenCol { get; set; }
  protected List<string>? _longMessage;
  public UIState State = UIState.InMainMenu;

  readonly Queue<string> Messages = [];

  public Sqr[,] SqsOnScreen;
  public Sqr[,] ZLayer; // An extra layer of screen tiles that overrides what
                        // whatever else was calculated to be displayed

  public CheatSheetMode CheatSheetMode { get; set; } = CheatSheetMode.Messages;

  protected List<string> MenuRows { get; set; } = [];

  IPopup? _popup = null;
  IPopup? _confirm = null;

  public List<MsgHistory> MessageHistory = [];
  protected readonly int MaxHistory = 120;
  protected bool HistoryUpdated = false;

  List<Animation> _animations = [];

  public bool InTutorial { get; set; } = false;
  public bool PauseForResponse { get; set; } = false;

  Inputer? InputController { get; set; } = null;
  public void SetInputController(Inputer inputer) => InputController = inputer;

  public UserInterface()
  {
    PlayerScreenRow = ViewHeight / 2;
    PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
    ZLayer = new Sqr[ViewHeight, ViewWidth];
    ClearZLayer();
  }

  public void ClearLongMessage()
  {
    _longMessage = null;
  }

  public void ClearSqsOnScreen()
  {
    for (int r = 0; r < SqsOnScreen.GetLength(0); r++)
    {
      for (int c = 0; c < SqsOnScreen.GetLength(1); c++)
      {
        SqsOnScreen[r, c] = Constants.BLANK_SQ;
      }
    }
  }

  public void SetPopup(IPopup popup) => _popup = popup;
  public void ClosePopup()
  {
    _popup = null;
    _confirm = null;
  }
  public bool ActivePopup => _popup != null;

  public void PlayQueuedExplosions(GameState gs)
  {
    foreach (var anim in _animations.OfType<ExplosionAnimation>())
      PlayAnimation(anim, gs);
  }

  public void ClearFoggyAnimation() =>
    _animations = [.. _animations.Where(a => a is not FogAnimation)];

  public void ClearUnderwaterAnimation() =>
    _animations = [.. _animations.Where(a => a is not UnderwaterAnimation)];

  public void RegisterAnimation(Animation anim) => _animations.Add(anim);

  // This plays the full animation (as opposed to registering
  // it to be played as part of the game loop). This means the
  // UI will be blocked while it is playing
  public void PlayAnimation(Animation animation, GameState gs)
  {
    while (animation.Expiry > DateTime.UtcNow)
    {
      SetSqsOnScreen(gs);
      animation.Update();
      UpdateDisplay(gs);
      Delay(75);
    }
  }

  protected void WritePopUp() => _popup?.Draw(this);
  protected void WriteConfirmation() => _confirm?.Draw(this);

  protected void WriteLongMessage(List<string> msg)
  {
    for (int row = 0; row < msg.Count; row++)
    {
      LineScanner ls = new(msg[row]);
      List<(Colour, string)> words = ls.Scan();
      WriteText(words.ToArray(), row, 0);
    }
  }

  void WriteCommandCheatSheet()
  {
    (Colour, string)[] w;
    WriteLine("Commands:", ScreenHeight - 6, 0, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, " a"), (Colours.LIGHT_GREY, ": use item  "), (Colours.LIGHT_BLUE, "c"), (Colours.LIGHT_GREY, ": close door  "),
      (Colours.LIGHT_BLUE, "C"), (Colours.LIGHT_GREY, ": chat  "), (Colours.LIGHT_BLUE, "d"), (Colours.LIGHT_GREY, ": drop item  "),
      (Colours.LIGHT_BLUE, "e"), (Colours.LIGHT_GREY, ": equip/unequip item")];
    //s = "a - Use item  c - close door  C - chat  d - drop item  e - equip/unequip item"; 
    WriteText(w, ScreenHeight - 5, 0);

    w = [(Colours.LIGHT_BLUE, " f"), (Colours.LIGHT_GREY, ": fire bow  "), (Colours.LIGHT_BLUE, "F"), (Colours.LIGHT_GREY, ": bash door  "),
      (Colours.LIGHT_BLUE, "i"), (Colours.LIGHT_GREY, ": view inventory  "), (Colours.LIGHT_BLUE, "M"), (Colours.LIGHT_GREY, ": view map  "),
      (Colours.LIGHT_BLUE, "o"), (Colours.LIGHT_GREY, ": open door  ")
    ];
    WriteText(w, ScreenHeight - 4, 0);

    w = [(Colours.LIGHT_BLUE, " S"), (Colours.LIGHT_GREY, ": save game  "), (Colours.LIGHT_BLUE, "Q"), (Colours.LIGHT_GREY, ": quit "),
      (Colours.LIGHT_BLUE, "t"), (Colours.LIGHT_GREY, ": throw item "), (Colours.LIGHT_BLUE, "x"), (Colours.LIGHT_GREY, ": examine "),
      (Colours.LIGHT_BLUE, "z"), (Colours.LIGHT_GREY, ": cast spell "),
      (Colours.LIGHT_BLUE, ","), (Colours.LIGHT_GREY, ": pickup item")
    ];
    WriteText(w, ScreenHeight - 3, 0);

    w = [(Colours.LIGHT_BLUE, " @"), (Colours.LIGHT_GREY, ": character info  "), (Colours.LIGHT_BLUE, "<"),
      (Colours.LIGHT_GREY, " or "), (Colours.LIGHT_BLUE, ">"), (Colours.LIGHT_GREY, ": use stairs, or swim up or down  ")];
    WriteText(w, ScreenHeight - 2, 0);

    w = [(Colours.LIGHT_BLUE, " *"), (Colours.LIGHT_GREY, ": message history  "), (Colours.LIGHT_BLUE, "="), (Colours.LIGHT_GREY, ": options")];
    WriteText(w, ScreenHeight - 1, 0);
  }

  void WriteMessages()
  {
    Queue<(Colour, string)> buffer = [];
    int j = 0;
    Colour colour = Colours.WHITE;
    while (j < 5 && j < MessageHistory.Count)
    {
      string s = MessageHistory[j++].Fmt;
      List<(Colour, string)> pieces = [];
      while (s.Length >= ScreenWidth)
      {
        int c;
        // Find the point to split the line. I'm never going to send a
        // message that's a string with no spaces wider than the 
        // screen am I...
        for (c = ScreenWidth - 1; c >= 0; c--)
        {
          if (s[c] == ' ')
            break;
        }
        pieces.Add((colour, s[..c].Trim()));
        s = s[c..];
      }
      pieces.Add((colour, s.Trim()));

      pieces.Reverse();
      foreach (var p in pieces)
        buffer.Enqueue(p);

      if (colour == Colours.WHITE)
        colour = Colours.GREY;
      else if (colour == Colours.GREY)
        colour = Colours.DARK_GREY;
    }

    int row = ScreenHeight - 1;
    while (buffer.Count > 0 && row > ViewHeight)
    {
      var (c, s) = buffer.Dequeue();
      WriteLine(s, row--, 0, ScreenWidth, c);
    }
  }

  protected void WriteMovementCheatSheet()
  {
    (Colour, string)[] w;

    w = [(Colours.WHITE, "Movement keys:   "), (Colours.LIGHT_BLUE, "y  k  u")];
    WriteText(w, ScreenHeight - 5, 0);
    WriteLine(@"                  \ | /      SHIFT-mv key will move you in", ScreenHeight - 4, 0, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, "                h"), (Colours.WHITE, " - @ - "), (Colours.LIGHT_BLUE, "l"),
      (Colours.WHITE, "      that direction until interrupted")];
    WriteText(w, ScreenHeight - 3, 0);
    WriteLine(@"                  / | \", ScreenHeight - 2, 0, ScreenWidth, Colours.WHITE);
    WriteLine(@"                 b  j  n", ScreenHeight - 1, 0, ScreenWidth, Colours.LIGHT_BLUE);
  }

  protected void WriteMovementCheatSheetOverlay()
  {
    (Colour, string)[] w;

    w = [(Colours.WHITE, "Movement keys:   "), (Colours.LIGHT_BLUE, "y  k  u")];
    WriteText(w, ScreenHeight - 5, ScreenWidth - 26);
    WriteLine(@"                  \ | /      ", ScreenHeight - 4, ScreenWidth - 26, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, "                h"), (Colours.WHITE, " - @ - "), (Colours.LIGHT_BLUE, "l")];
    WriteText(w, ScreenHeight - 3, ScreenWidth - 26);
    WriteLine(@"                  / | \", ScreenHeight - 2, ScreenWidth - 26, ScreenWidth, Colours.WHITE);
    WriteLine(@"                 b  j  n", ScreenHeight - 1, ScreenWidth - 26, ScreenWidth, Colours.LIGHT_BLUE);
  }

  protected void WriteMessagesSection()
  {
    if (CheatSheetMode == CheatSheetMode.Commands)
    {
      WriteCommandCheatSheet();
      return;
    }
    else if (CheatSheetMode == CheatSheetMode.Movement)
    {
      WriteMovementCheatSheet();
      return;
    }

    if (MessageHistory.Count > 0)
    {
      WriteMessages();
    }

    if (CheatSheetMode == CheatSheetMode.MvMixed)
    {
      WriteMovementCheatSheetOverlay();
    }
  }

  public void WriteText(Span<(Colour C, string S)> pieces, int lineNum, int col)
  {
    foreach (var (C, S) in pieces)
    {
      if (S.Length == 0)
        continue;
      WriteLine(S, lineNum, col, S.Length, C);
      col += S.Length;
    }
  }

  static int FindSplit(string txt)
  {
    int start = int.Min(txt.Length - 1, SideBarWidth);
    for (int j = start; j > 0; j--)
    {
      if (txt[j] == ' ')
        return j;
    }

    return -1;
  }

  readonly (Colour, string) _sbSpacer = (Colours.WHITE, "│ ");
  int WriteSideBarLine(Colour colour, string text, int row)
  {
    WriteLine(_sbSpacer.Item2, row, ViewWidth, SideBarWidth, _sbSpacer.Item1);
    WriteLine(text, row, ViewWidth + _sbSpacer.Item2.Length, SideBarWidth, colour);

    return row + 1;
  }

  int WriteSideBarLine(Span<(Colour, string)> line, int row)
  {
    int lineWidth = 0;
    foreach (var item in line)
      lineWidth += item.Item2.Length;

    if (lineWidth < SideBarWidth)
    {
      WriteText(line, row++, ViewWidth);
    }
    else
    {
      // Split the line if it's too wide for the sidebar. Currently handling
      // only the simplest possible case. This won't work if the message sent
      // has to go across 3 lines, or there are no spaces in the text
      List<(Colour, string)> pieces = [];
      int width = 0;

      foreach (var piece in line)
      {
        if (piece.Item2.Length + width < SideBarWidth)
        {
          pieces.Add(piece);
          width += piece.Item2.Length;
        }
        else
        {
          int pos = FindSplit(piece.Item2);
          string part1 = piece.Item2[..pos];
          string part2 = "│  " + piece.Item2[pos..];
          pieces.Add((piece.Item1, part1));
          WriteText(pieces.ToArray(), row++, ViewWidth);
          WriteText([(piece.Item1, part2)], row++, ViewWidth);
          width = 0;
          pieces.Clear();
        }
      }
    }

    return row;
  }

  readonly HashSet<string> _statuses = [];
  readonly string _sbBlank = "│".PadRight(ViewWidth);
  protected void WriteSideBar(GameState gs)
  {
    int row = 0;
    WriteLine("│ ", row, ViewWidth, SideBarWidth, Colours.WHITE);
    WriteLine(gs.Player.Name, row++, ViewWidth + 2, SideBarWidth, Colours.WHITE);
    int currHP = gs.Player.Stats[Attribute.HP].Curr;
    int maxHP = gs.Player.Stats[Attribute.HP].Max;
    WriteLine($"│ HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);

    int bottomOffset = gs.Options.ShowTurns ? 2 : 1;

    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var magicPoints) && magicPoints.Max > 0)
    {
      WriteLine($"│ MP: {magicPoints.Curr} ({magicPoints.Max})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    WriteLine("│ AC:", row, ViewWidth, SideBarWidth, Colours.WHITE);
    WriteLine(gs.Player.AC.ToString(), row++, ViewWidth + 6, SideBarWidth, Colours.WHITE);

    (Colour, string)[] zorkmidLine = [_sbSpacer, (Colours.YELLOW, "$"), (Colours.WHITE, $": {gs.Player.Inventory.Zorkmids}")];
    row = WriteSideBarLine(zorkmidLine, row);

    WriteLine(_sbBlank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

    var weapon = gs.Player.Inventory.ReadiedWeapon();
    if (weapon is not null)
    {
      string weaponName = MsgFactory.CalcName(weapon, gs.Player, Article.InDef);
      (Colour, string)[] weaponLine;
      if (weapon.HasTrait<TwoHandedTrait>() || (weapon.HasTrait<VersatileTrait>() && !gs.Player.Inventory.ShieldEquipped()))
        weaponLine = [_sbSpacer, (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString()), (Colours.WHITE, $" {weaponName} (in hands)")];
      else
        weaponLine = [_sbSpacer, (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString()), (Colours.WHITE, $" {weaponName} (in hand)")];
      row = WriteSideBarLine(weaponLine, row);
    }
    var bow = gs.Player.Inventory.ReadiedBow();
    if (bow is not null)
    {
      (Colour, string)[] weaponLine = [_sbSpacer, (bow.Glyph.Lit, bow.Glyph.Ch.ToString()), (Colours.WHITE, $" {bow.FullName.IndefArticle()} (equipped)")];
      row = WriteSideBarLine(weaponLine, row);
    }

    for (; row < ViewHeight - 1; row++)
    {
      WriteLine(_sbBlank, row, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    // Write statuses
    int statusLineNum = ViewHeight - bottomOffset - 2;
    _statuses.Clear();
    foreach (var trait in gs.Player.Traits)
    {
      if (!_statuses.Contains("POISONED") && trait is PoisonedTrait)
      {
        row = WriteSideBarLine(Colours.LIME_GREEN, "POISONED", statusLineNum--);
        _statuses.Add("POISONED");
      }
      else if (!_statuses.Contains("NAUSEOUS") && gs.Player.HasTrait<NauseaTrait>())
      {
        row = WriteSideBarLine(Colours.GREEN, "NAUSEOUS", statusLineNum--);
        _statuses.Add("NAUSEOUS");
      }
      else if (!_statuses.Contains("RAGE") && trait is RageTrait rage && rage.Active)
      {
        row = WriteSideBarLine(Colours.BRIGHT_RED, "RAGE", statusLineNum--);
        _statuses.Add("RAGE");
      }
      else if (!_statuses.Contains("CURSED") && trait is CurseTrait)
      {
        row = WriteSideBarLine(Colours.BRIGHT_RED, "CURSED", statusLineNum--);
        _statuses.Add("CURSED");
      }
      else if (!_statuses.Contains("BERZERK") && trait is BerzerkTrait)
      {
        row = WriteSideBarLine(Colours.BRIGHT_RED, "BERZERK", statusLineNum--);
        _statuses.Add("BERZERK");
      }
      else if (!_statuses.Contains("PROTECTION") && trait is AuraOfProtectionTrait aura)
      {
        Colour colour;
        if (aura.HP >= 25)
          colour = Colours.ICE_BLUE;
        else if (aura.HP > 10)
          colour = Colours.LIGHT_BLUE;
        else
          colour = Colours.BLUE;
        row = WriteSideBarLine(colour, "PROTECTED", statusLineNum--);
        _statuses.Add("PROTECTION");
      }
      else if (trait is ResistanceTrait resist)
      {
        switch (resist.Type)
        {
          case DamageType.Fire:
            row = WriteSideBarLine(Colours.LIGHT_BLUE, "RESIST FIRE", statusLineNum--);
            _statuses.Add("RESIST FIRE");
            break;
          case DamageType.Cold:
            row = WriteSideBarLine(Colours.LIGHT_BLUE, "RESIST COLD", statusLineNum--);
            _statuses.Add("RESIST COLD");
            break;
        }
      }
      else if (trait is StressTrait st)
      {
        Colour colour = st.Stress switch
        {
          StressLevel.Hystrical => Colours.BRIGHT_RED,
          StressLevel.Paranoid => Colours.BRIGHT_RED,
          StressLevel.Nervous => Colours.YELLOW_ORANGE,
          _ => Colours.YELLOW,
        };
        row = WriteSideBarLine(colour, st.Stress.ToString().ToUpper(), statusLineNum--);
      }
    }
    if (!_statuses.Contains("GRAPPLED") && gs.Player.HasActiveTrait<GrappledTrait>())
    {
      WriteSideBarLine(Colours.BRIGHT_RED, "GRAPPLED", statusLineNum--);
      _statuses.Add("GRAPPLED");
    }
    if (!_statuses.Contains("PARALYZED") && gs.Player.HasActiveTrait<ParalyzedTrait>())
    {
      WriteSideBarLine(Colours.YELLOW, "PARALYZED", statusLineNum--);
      _statuses.Add("PARALYZED");
    }
    if (!_statuses.Contains("CONFUSED") && gs.Player.HasActiveTrait<ConfusedTrait>())
    {
      WriteSideBarLine(Colours.YELLOW, "CONFUSED", statusLineNum--);
      _statuses.Add("CONFUSED");
    }
    if (gs.Player.HasActiveTrait<ExhaustedTrait>())
    {
      WriteSideBarLine(Colours.PINK, "EXHAUSTED", statusLineNum--);
      _statuses.Add("EXHAUSTED");
    }
    if (gs.Player.HasActiveTrait<LameTrait>())
    {
      WriteSideBarLine(Colours.PINK, "LIMPING", statusLineNum--);
      _statuses.Add("LIMPING");
    }
    if (!_statuses.Contains("NUMBED") && gs.Player.HasTrait<NumbedTrait>())
    {
      WriteSideBarLine(Colours.GREY, "NUMBED", statusLineNum--);
      _statuses.Add("NUMBED");
    }
    if (!_statuses.Contains("TELEPATHIC") && gs.Player.HasActiveTrait<TelepathyTrait>())
    {
      WriteSideBarLine(Colours.PURPLE, "TELEPATHIC", statusLineNum--);
      _statuses.Add("TELEPATHIC");
    }
    if (!_statuses.Contains("LEVITATING") && gs.Player.HasActiveTrait<LevitationTrait>())
    {
      // Maybe change the colour if the effect is going to expire soon?
      WriteSideBarLine(Colours.LIGHT_BLUE, "LEVITATING", statusLineNum--);
      _statuses.Add("LEVITATING");
    }
    if (!_statuses.Contains("BLIND") && gs.Player.HasTrait<BlindTrait>())
    {
      WriteSideBarLine(Colours.GREY, "BLIND", statusLineNum--);
      _statuses.Add("BLIND");
    }
    if (!_statuses.Contains("TIPSY") && gs.Player.HasTrait<TipsyTrait>())
    {
      WriteSideBarLine(Colours.PINK, "TIPSY", statusLineNum--);
      _statuses.Add("TIPSY");
    }
    else if (!_statuses.Contains("DROWNING") && gs.Player.HasTrait<DrowningTrait>())
    {
      WriteSideBarLine(Colours.BRIGHT_RED, "DROWNING", statusLineNum--);
      _statuses.Add("DROWNING");
    }
    if (!_statuses.Contains("AFRAID") && gs.Player.HasTrait<FrightenedTrait>())
    {
      WriteSideBarLine(Colours.YELLOW, "AFRAID", statusLineNum--);
      _statuses.Add("AFRAID");
    }
    if (!_statuses.Contains("OBSCURED") && gs.Player.HasTrait<NondescriptTrait>())
    {
      WriteSideBarLine(Colours.GREY, "OBSCURED", statusLineNum--);
      _statuses.Add("OBSCURED");
    }
    if (!_statuses.Contains("FAST") && gs.Player.HasTrait<CelerityTrait>())
    {
      WriteSideBarLine(Colours.GREEN, "FAST", statusLineNum--);
      _statuses.Add("FAST");
    }
    if (!_statuses.Contains("BLESSED") && gs.Player.HasTrait<BlessingTrait>())
    {
      WriteSideBarLine(Colours.ICE_BLUE, "BLESSED", statusLineNum--);
      _statuses.Add("YELLOW");
    }
    if (!_statuses.Contains("DISEASED") && gs.Player.HasTrait<DiseasedTrait>())
    {
      WriteSideBarLine(Colours.LIME_GREEN, "DISEASED", statusLineNum--);
      _statuses.Add("DISEASED");
    }
    if (!_statuses.Contains("INVISIBLE") && gs.Player.HasTrait<InvisibleTrait>())
    {
      WriteSideBarLine(Colours.WHITE, "INVISIBLE", statusLineNum--);
      _statuses.Add("INVISIBLE");
    }
    foreach (StatDebuffTrait statBuff in gs.Player.Traits.OfType<StatDebuffTrait>())
    {
      if (!_statuses.Contains("WEAKENED") && statBuff.Attr == Attribute.Strength)
      {
        row = WriteSideBarLine(Colours.BRIGHT_RED, "WEAKENED", statusLineNum--);
        _statuses.Add("WEAKENED");
      }
    }
    foreach (VulnerableTrait vul in gs.Player.Traits.OfType<VulnerableTrait>())
    {
      string s = $"VULNERABLE: {vul.Type.ToString().ToUpper()}";
      if (!_statuses.Contains(s))
      {
        row = WriteSideBarLine(Colours.BRIGHT_RED, s, statusLineNum--);
        _statuses.Add(s);
      }
    }

    Tile tile = gs.TileAt(gs.Player.Loc);
    Glyph glyph = Util.TileToGlyph(tile);
    Sqr tileSq = new(glyph.Lit, Colours.BLACK, glyph.Ch);
    string tileText = " " + Tile.TileDesc(tile.Type).Capitalize();
    foreach (var item in gs.ObjDb.EnvironmentsAt(gs.Player.Loc))
    {
      if (item.Type == ItemType.Environment && item.Name != "light" && item.Name != "photon" && item.Name != "moon daughter tile" || item.Name == "darkness")
      {
        tileSq = new Sqr(item.Glyph.Lit, Colours.BLACK, item.Glyph.Ch);
        tileText = $" {item.Name.Capitalize()}";
        break;
      }
    }

    (Colour, string)[] tileLine = [_sbSpacer, (tileSq.Fg, tileSq.Ch.ToString()), (Colours.WHITE, tileText)];
    WriteSideBarLine(tileLine, ViewHeight - bottomOffset - 1);

    if (gs.CurrDungeonID == 0)
    {
      var time = gs.CurrTime();
      var mins = time.Item2.ToString().PadLeft(2, '0');
      WriteLine($"│ Outside {time.Item1}:{mins}", ViewHeight - bottomOffset, ViewWidth, SideBarWidth, Colours.WHITE);
    }
    else if (gs.CurrentDungeon.Descending)
    {
      WriteLine($"│ Depth: {gs.CurrLevel + 1}", ViewHeight - bottomOffset, ViewWidth, SideBarWidth, Colours.WHITE);
    }
    else
    {
      WriteLine($"│ Floor: {gs.CurrLevel + 1}", ViewHeight - bottomOffset, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    if (gs.Options.ShowTurns)
    {
      WriteLine($"│ Turn: {gs.Turn}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
    }
  }

  protected void WriteDropDown()
  {
    int width = 0;
    int row = 0;

    List<List<(Colour, string)>> lines = [];
    foreach (var line in MenuRows!)
    {
      LineScanner ls = new(line);
      List<(Colour, string)> words = [(Colours.BLACK, " ")];
      words.AddRange(ls.Scan());

      if (_popup is not null)
        words = [.. words.Select(w => (w.Item1 with { Alpha = w.Item1.Alpha / 2 }, w.Item2))];

      lines.Add(words);

      int chs = CountCh(words);
      if (chs > width)
        width = chs;
    }
    width += 1;

    int col = ViewWidth - width;

    foreach (var line in lines!)
    {
      int chs = CountCh(line);
      line.Add((Colours.BLACK, "".PadLeft(width - chs)));

      WriteText(line.ToArray(), row++, col);
    }
    WriteLine("", row, col, width, Colours.BLACK);

    static int CountCh(List<(Colour, string)> words) => words.Select(w => w.Item2.Length).Sum();
  }

  public void AlertPlayer(string alert)
  {
    if (alert.Trim().Length == 0)
      return;

    Messages.Enqueue(alert);
  }

  public void AlertPlayer(string alert, GameState gs, Loc loc, Actor? other = null)
  {
    if (!gs.LastPlayerFoV.ContainsKey(loc))
      return;

    if (other is not null && !other.VisibleTo(gs.Player))
      return;

    AlertPlayer(alert);
  }

  public void WriteAlerts()
  {
    List<string> msgs = [];
    while (Messages.Count > 0)
    {
      string s = Messages.Dequeue().Trim();
      if (s.Length > 0)
        msgs.Add(s);
    }

    string msgText = string.Join(' ', msgs).Trim();
    if (string.IsNullOrEmpty(msgText))
      return;

    HistoryUpdated = true;

    if (MessageHistory.Count > 0 && MessageHistory[0].Message == msgText)
      MessageHistory[0] = new MsgHistory(msgText, MessageHistory[0].Count + 1);
    else
      MessageHistory.Insert(0, new MsgHistory(msgText, 1));

    if (MessageHistory.Count > MaxHistory)
      MessageHistory.RemoveAt(MaxHistory);
  }

  public void SetLongMessage(List<string> message) => _longMessage = message;
  public void ShowDropDown(List<string> lines) => MenuRows = lines;
  public void CloseMenu() => MenuRows = [];

  protected static void Delay(int ms = 10) => Thread.Sleep(ms);

  // I am using this in input menus outside of the main game. Primarily
  // the start menu
  public char GetKeyInput()
  {
    var e = PollForEvent();
    if (e.Type == GameEventType.Quiting)
      throw new QuitGameException();

    if (e.Type == GameEventType.KeyInput)
      return e.Value;

    return '\0';
  }

  public void BlockForInput(GameState? gs)
  {
    GameEvent e;
    do
    {
      e = PollForEvent();

      UpdateDisplay(gs);
      Delay();
    }
    while (e.Type == GameEventType.NoEvent);
  }

  // Block until the character hits ESC, space, or enter. (To prevent a user
  // from dismissing popups and such while typing fast)
  public void BlockFoResponse(GameState gs)
  {
    while (true)
    {
      char ch = GetKeyInput();
      if (ch == '\n' || ch == ' ')
        break;
      gs.PrepareFieldOfView();
      SetSqsOnScreen(gs);
      UpdateDisplay(gs);
      Delay();
    }
  }

  public void BlockingPopup(GameState gs)
  {
    GameEvent e;
    do
    {
      UpdateDisplay(gs);
      e = PollForEvent();
      Delay();
    }
    while (e.Type == GameEventType.NoEvent);
  }

  public char FullScreenMenu(List<string> menu, HashSet<char> options, GameState? gs)
  {
    GameEvent e;

    do
    {
      SetLongMessage(menu);
      UpdateDisplay(gs);
      e = PollForEvent();

      if (e.Type == GameEventType.NoEvent)
      {
        Delay();
        continue;
      }
      else if (e.Value == Constants.ESC || e.Type == GameEventType.Quiting)
      {
        throw new QuitGameException();
      }
      else if (options.Contains(e.Value))
      {
        return e.Value;
      }
    }
    while (true);
  }

  public char BlockingPopupMenu(string menu, string title, HashSet<char> options, GameState gs, int popupWidth)
  {
    GameEvent e;

    int col = (ScreenWidth / 2) - (ScreenWidth - popupWidth) / 2 + 2;
    do
    {
      SetPopup(new Popup(menu, title, -1, col, popupWidth));
      UpdateDisplay(gs);

      e = PollForEvent();
      if (e.Type == GameEventType.NoEvent)
      {
        Delay();
        continue;
      }
      else if (e.Type == GameEventType.Quiting)
      {
        throw new QuitGameException();
      }
      else if (options.Contains(e.Value))
      {
        return e.Value;
      }
    }
    while (true);
  }

  public bool Confirmation(string txt, GameState gs)
  {
    GameEvent e;
    char ch = '\0';

    do
    {
      if (txt != "")
        _confirm = new Popup(txt, "", ViewHeight / 2 - 2, ScreenWidth / 2);
      _popup?.SetDefaultTextColour(Colours.DARK_GREY);

      UpdateDisplay(gs);

      e = PollForEvent();
      if (e.Type == GameEventType.NoEvent)
      {
        Delay();
        continue;
      }
      else if (e.Type == GameEventType.Quiting)
      {
        throw new QuitGameException();
      }
      else
      {
        ch = e.Value;
      }
    }
    while (!(ch == 'y' || ch == 'n'));

    _popup?.SetDefaultTextColour(Colours.WHITE);

    return ch == 'y';
  }

  public string BlockingGetResponse(string prompt, int maxLength, IInputChecker? validator = null)
  {
    string result = "";
    GameEvent e;

    do
    {
      int width = int.Max(prompt.Length + 4, result.Length + 2);
      SetPopup(new Popup($"{prompt}\n{result}", "", -1, -1, width));
      UpdateDisplay(null);
      e = PollForEvent();

      if (e.Type == GameEventType.NoEvent)
      {
        Delay();
        continue;
      }
      else if (e.Value == Constants.ESC)
      {
        throw new GameNotLoadedException();
      }
      else if (e.Type == GameEventType.Quiting)
      {
        throw new QuitGameException();
      }

      if (e.Value == '\n' || e.Value == 13)
        break;
      else if (e.Value == Constants.BACKSPACE)
        result = result.Length > 0 ? result[..^1] : "";
      else if (validator is not null && !validator.Valid(result + e.Value))
        continue;
      else if (result.Length == maxLength)
        continue;
      else
        result += e.Value;
    }
    while (true);

    ClosePopup();

    return result.Trim();
  }

  void SetSqsOnScreen(GameState gs)
  {
    int playerRow = gs.Player.Loc.Row;
    int playerCol = gs.Player.Loc.Col;

    int rowOffset = playerRow - PlayerScreenRow;
    int colOffset = playerCol - PlayerScreenCol;

    for (int r = 0; r < ViewHeight; r++)
    {
      for (int c = 0; c < ViewWidth; c++)
      {
        int mapRow = r + rowOffset;
        int mapCol = c + colOffset;

        Loc loc = new(gs.CurrDungeonID, gs.CurrLevel, mapRow, mapCol);
        Sqr sqr;
        if (gs.LastPlayerFoV.TryGetValue(loc, out Glyph glyph))
          sqr = ZLayer[r, c] != Constants.BLANK_SQ ? ZLayer[r, c] : new Sqr(glyph.Lit, glyph.BG, glyph.Ch);
        else if (gs.CurrentDungeon.RememberedLocs.TryGetValue(loc, out var memory))
          sqr = new Sqr(memory.Glyph.Unlit, memory.Glyph.BG, memory.Glyph.Ch);
        else
          sqr = Constants.BLANK_SQ;
        
        SqsOnScreen[r, c] = sqr;
      }
    }

    if (ZLayer[PlayerScreenRow, PlayerScreenCol] == Constants.BLANK_SQ)
    {
      Colour bg = gs.Options.HighlightPlayer ? Colours.HILITE : gs.Player.Glyph.BG;
      SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(gs.Player.Glyph.Lit, bg, gs.Player.Glyph.Ch);
    }
  }

  public (int, int) LocToScrLoc(int row, int col, int playerRow, int playerCol)
  {
    int rowOffset = playerRow - PlayerScreenRow;
    int colOffset = playerCol - PlayerScreenCol;

    return (row - rowOffset, col - colOffset);
  }

  public (int, int) ScrLocToGameLoc(int screenRow, int screenCol, int playerRow, int playerCol)
  {
    int rowOffset = playerRow - PlayerScreenRow;
    int colOffset = playerCol - PlayerScreenCol;

    return (screenRow + rowOffset, screenCol + colOffset);
  }

  void ClearZLayer()
  {
    for (int r = 0; r < ViewHeight; r++)
    {
      for (int c = 0; c < ViewWidth; c++)
      {
        ZLayer[r, c] = Constants.BLANK_SQ;
      }
    }
  }

  // Clear out anything that should shouldn't persist between games
  public void Reset()
  {
    MessageHistory = [];
    _animations = [];
    State = UIState.InMainMenu;
  }

  public bool CheckForInput(GameState gs)
  {
    var e = PollForEvent(!gs.PlayerAFK);
    if (e.Type == GameEventType.Quiting)
      return true;

    if (e.Type == GameEventType.KeyInput)
    {
      char ch;
      if (gs.Options.KeyRemaps.TryGetValue(e.Value, out var cmd))
        ch = KeyMapper.CmdToKey(cmd);
      else
        ch = e.Value;

      InputController!.Input(ch);
    }

    return false;
  }

  public void RefreshScreen(GameState gs)
  {
    SetSqsOnScreen(gs);

    foreach (var l in _animations)
      l.Update();
    _animations = [.. _animations.Where(a => a.Expiry > DateTime.UtcNow)];

    UpdateDisplay(gs);
  }
}

class KeyMapper
{
  public static char CmdToKey(string cmd)
  {
    return cmd.ToLower() switch
    {
      "pickup" => ',',
      "showmap" => 'M',
      "quit" => 'Q',
      "save" => 'S',
      "enterportal" => 'E',
      "upstairs" => '<',
      "downstars" => '>',
      "firebow" => 'f',
      "useitem" => 'a',
      "dropitem" => 'd',
      "throwitem" => 't',
      "characterscheet" => '@',
      "closedoor" => 'c',
      "opendoor" => 'o',
      "chat" => 'C',
      "equipitem" => 'e',
      "showmessages" => '*',
      "showhelp" => '?',
      "examine" => 'X',
      "passturn" => '.',
      "inventory" => 'i',
      "move_north" => 'k',
      "move_south" => 'j',
      "move_west" => 'h',
      "move_east" => 'l',
      "move_nw" => 'y',
      "move_ne" => 'u',
      "move_sw" => 'b',
      "move_se" => 'n',
      "run_north" => 'K',
      "run_south" => 'J',
      "run_west" => 'H',
      "run_east" => 'L',
      "run_nw" => 'Y',
      "run_ne" => 'U',
      "run_sw" => 'B',
      "run_se" => 'N',
      _ => throw new Exception("Unknown command!")
    };
  }
}

interface IInputChecker
{
  bool Valid(string iput);
}

class PlayerNameInputChecker : IInputChecker
{
  // This can be done with regex but as I write this I'm feeling too braindead
  // to write one up. $[azAZ][azAZ0-9 ] maybe?
  public bool Valid(string input)
  {
    if (input.Length < 1)
      return false;

    if (input.Length == 1 && char.IsAsciiLetter(input[0]))
      return true;

    char ch = input.Last();
    if (input.Length > 1 && (char.IsAsciiLetterOrDigit(ch) || ch == ' '))
      return true;

    return false;
  }
}