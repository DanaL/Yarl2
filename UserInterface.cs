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

enum CheatSheetMode
{
  Messages = 0,
  Commands = 1,
  Movement = 2,
  MvMixed = 3
}

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

  protected abstract GameEvent PollForEvent();  
  protected abstract void Blit(); // Is blit the right term for this? 'Presenting the screen'

  protected int FontSize;
  public int PlayerScreenRow { get; protected set; }
  public int PlayerScreenCol { get; protected set; }
  protected List<string>? _longMessage;
  protected Options _options;

  readonly Queue<string> Messages = [];

  public Sqr[,] SqsOnScreen;
  public Sqr[,] ZLayer; // An extra layer of screen tiles that overrides what
                        // whatever else was calculated to be displayed

  public CheatSheetMode CheatSheetMode { get; set; } = CheatSheetMode.Messages;

  protected List<string> MenuRows { get; set; } = [];

  IPopup? _popup = null;
  IPopup? _confirm = null;

  public List<MsgHistory> MessageHistory = [];
  protected readonly int MaxHistory = 50;
  protected bool HistoryUpdated = false;

  List<Animation> _animations = [];

  Glyph PlayerGlyph { get; set; }

  public bool InTutorial { get; set; } = false;
  public bool PauseForResponse { get; set; } = false;

  Inputer? InputController { get; set; } = null;
  public void SetInputController(Inputer inputer) => InputController = inputer;

  public UserInterface(Options opts)
  {
    _options = opts;
    SetOptions(opts, null);
    PlayerScreenRow = ViewHeight / 2;
    PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
    ZLayer = new Sqr[ViewHeight, ViewWidth];
    ClearZLayer();
  }

  public void SetOptions(Options opts, GameState? gs)
  {
    _options = opts;
    if (opts.HighlightPlayer)
      PlayerGlyph = new Glyph('@', Colours.WHITE, Colours.WHITE, Colours.HILITE, false);
    else
      PlayerGlyph = new Glyph('@', Colours.WHITE, Colours.WHITE, Colours.BLACK, false);
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

  public void VictoryScreen(string bossName, GameState gs)
  {
    int WriteParagraph(string txt, int startLine)
    {
      var words = txt.Split(' ');
      string line = "";
      foreach (var word in words)
      {
        if (41 + line.Length + word.Length >= 80)
        {
          WriteLine(line, startLine++, 40, 40, Colours.WHITE);
          line = "";
        }
        line += word + " ";
      }
      WriteLine(line, startLine++, 40, 40, Colours.WHITE);

      return startLine;
    }

    void PlaceVillager(GameState gs, Actor villager, int centerRow, int centerCol)
    {
      List<Loc> locs = [];

      for (int r = centerRow - 5; r < centerRow + 5; r++)
      {
        for (int c = centerCol - 5; c < centerCol + 5; c++)
        {
          var loc = new Loc(0, 0, r, c);

          if (gs.ObjDb.Occupied(loc))
            continue;

          switch (gs.TileAt(loc).Type)
          {
            case TileType.Bridge:
            case TileType.Dirt:
            case TileType.Grass:
            case TileType.GreenTree:
            case TileType.RedTree:
            case TileType.YellowTree:
            case TileType.OrangeTree:
              locs.Add(loc);
              break;
          }
        }
      }

      if (locs.Count > 0)
      {
        var loc = locs[gs.Rng.Next(locs.Count)];
        gs.ObjDb.ActorMoved(villager, villager.Loc, loc);
      }
    }

    gs.CurrDungeonID = 0;
    gs.CurrLevel = 0;

    var popup = new Popup($"\nYou have defeated {bossName}!\n\n  -- Press any key to continue --", "Victory", -1, -1);
    SetPopup(popup);
    UpdateDisplay(gs);
    BlockForInput(gs);
    ClearLongMessage();

    var town = gs.Campaign.Town!;

    int minRow = int.MaxValue, minCol = int.MaxValue, maxRow = 0, maxCol = 0;
    foreach (var loc in town.TownSquare)
    {
      if (loc.Row < minRow)
        minRow = loc.Row;
      if (loc.Col < minCol)
        minCol = loc.Col;
      if (loc.Row > maxRow)
        maxRow = loc.Row;
      if (loc.Col > maxCol)
        maxCol = loc.Col;
    }

    int playerRow = (minRow + maxRow) / 2;
    int playerCol = (minCol + maxCol) / 2;
    var playerLoc = new Loc(0, 0, playerRow, playerCol);
    gs.ObjDb.ActorMoved(gs.Player, gs.Player.Loc, playerLoc);

    List<Actor> villagers = [];
    foreach (var obj in gs.ObjDb.Objs)
    {
      if (obj.Value is Actor actor && actor.HasTrait<VillagerTrait>())
      {
        villagers.Add(actor);
        PlaceVillager(gs, actor, playerRow, playerCol);
      }
    }

    Animation? bark = null;
    GameEvent e;
    do
    {
      ClearScreen();
      ClearSqsOnScreen();

      int screenR = 6;
      int screenC = 7;
      for (int r = minRow - 6; r < maxRow + 6; r++)
      {
        for (int c = minCol - 6; c < maxCol + 11; c++)
        {
          Glyph glyph;
          if (r == playerRow && c == playerCol)
          {
            glyph = PlayerGlyph;
            PlayerScreenRow = screenR;
            PlayerScreenCol = screenC;
          }
          else if (gs.ObjDb.Occupant(new Loc(0, 0, r, c)) is Actor actor)
          {
            glyph = actor.Glyph;
          }
          else
          {
            var tile = gs.TileAt(new Loc(0, 0, r, c));
            glyph = Util.TileToGlyph(tile);
          }

          var sqr = new Sqr(glyph.Lit, Colours.BLACK, glyph.Ch);
          SqsOnScreen[screenR, screenC++] = sqr;
        }
        ++screenR;
        screenC = 7;
      }

      if (bark is not null && bark.Expiry > DateTime.UtcNow)
      {
        bark.Update();
      }
      else if (villagers.Count > 0)
      {
        var v = villagers[gs.Rng.Next(villagers.Count)];

        var cheer = "";
        if (v.Glyph.Ch == 'd')
        {
          cheer = "Arf! Arf!";
        }
        else
        {
          int roll = gs.Rng.Next(4);
          if (roll == 0)
            cheer = "Huzzah!";
          else if (roll == 1)
            cheer = "Praise them with great praise!";
          else if (roll == 2)
            cheer = "Our hero!";
          else
            cheer = "We'll be safe now!";
        }

        bark = new BarkAnimation(gs, 2000, v, cheer);
      }

      for (int r = 0; r < ViewHeight; r++)
      {
        for (int c = 0; c < ScreenWidth / 2; c++)
        {
          WriteSq(r, c, SqsOnScreen[r, c]);
        }
      }

      int lineNum = WriteParagraph("Congratulations, Hero!!", 1);

      var sb = new StringBuilder();
      sb.Append("After your defeat of ");
      sb.Append(bossName);
      sb.Append(" you return to ");
      sb.Append(town.Name);
      sb.Append(" and receive the accoldates of the townsfolk.");

      lineNum = WriteParagraph(sb.ToString(), lineNum + 1);
      var para2 = "The darkness has been lifted from the region and the village will soon begin again to prosper. Yet after resting for a time and enjoying the villagers' gratitude and hospitality, the yearning for adventure begins to overtake you.";
      lineNum = WriteParagraph(para2, lineNum + 1);
      var para3 = "You've heard, for instance, tales of a fabled dungeon in whose depths lies the legendary Amulet of Yender...";
      lineNum = WriteParagraph(para3, lineNum + 1);
      WriteParagraph("Press any key to exit.", lineNum + 1);

      Blit();

      e = PollForEvent();
      Delay();
    }
    while (e.Type == GameEventType.NoEvent);
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

  public void RegisterAnimation(Animation animation)
  {
    _animations.Add(animation);
  }

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

  void WriteCommandCheatSheet()
  {
    List<(Colour, string)> w;
    WriteLine("Commands:", ScreenHeight - 6, 0, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, " a"), (Colours.LIGHT_GREY, ": use item  "), (Colours.LIGHT_BLUE, "c"), (Colours.LIGHT_GREY, ": close door  "),
      (Colours.LIGHT_BLUE, "C"), (Colours.LIGHT_GREY, ": chat  "), (Colours.LIGHT_BLUE, "d"), (Colours.LIGHT_GREY, ": drop item  "),
      (Colours.LIGHT_BLUE, "e"), (Colours.LIGHT_GREY, ": equip/unequip item")];
    //s = "a - Use item  c - close door  C - chat  d - drop item  e - equip/unequip item"; 
    WriteText(w, ScreenHeight - 5, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " f"), (Colours.LIGHT_GREY, ": fire bow  "), (Colours.LIGHT_BLUE, "F"), (Colours.LIGHT_GREY, ": bash door  "),
      (Colours.LIGHT_BLUE, "i"), (Colours.LIGHT_GREY, ": view inventory  "), (Colours.LIGHT_BLUE, "M"), (Colours.LIGHT_GREY, ": view map  "),
      (Colours.LIGHT_BLUE, "o"), (Colours.LIGHT_GREY, ": open door  ")
    ];
    WriteText(w, ScreenHeight - 4, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " S"), (Colours.LIGHT_GREY, ": save game  "), (Colours.LIGHT_BLUE, "Q"), (Colours.LIGHT_GREY, ": quit "),
      (Colours.LIGHT_BLUE, "t"), (Colours.LIGHT_GREY, ": throw item "), (Colours.LIGHT_BLUE, "x"), (Colours.LIGHT_GREY, ": examine "),
      (Colours.LIGHT_BLUE, "z"), (Colours.LIGHT_GREY, ": cast spell "),
      (Colours.LIGHT_BLUE, ","), (Colours.LIGHT_GREY, ": pickup item")
    ];
    WriteText(w, ScreenHeight - 3, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " @"), (Colours.LIGHT_GREY, ": character info  "), (Colours.LIGHT_BLUE, "<"),
      (Colours.LIGHT_GREY, " or "), (Colours.LIGHT_BLUE, ">"), (Colours.LIGHT_GREY, ": use stairs, or swim up or down  ")];
    WriteText(w, ScreenHeight - 2, 0, ScreenHeight);

    w = [(Colours.LIGHT_BLUE, " *"), (Colours.LIGHT_GREY, ": message history  "), (Colours.LIGHT_BLUE, "="), (Colours.LIGHT_GREY, ": options")];
    WriteText(w, ScreenHeight - 1, 0, ScreenHeight);
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
    List<(Colour, string)> w;

    w = [(Colours.WHITE, "Movement keys:   "), (Colours.LIGHT_BLUE, "y  k  u")];
    WriteText(w, ScreenHeight - 5, 0, ScreenWidth);
    WriteLine(@"                  \ | /      SHIFT-mv key will move you in", ScreenHeight - 4, 0, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, "                h"), (Colours.WHITE, " - @ - "), (Colours.LIGHT_BLUE, "l"),
      (Colours.WHITE, "      that direction until interrupted")];
    WriteText(w, ScreenHeight - 3, 0, ScreenWidth);
    WriteLine(@"                  / | \", ScreenHeight - 2, 0, ScreenWidth, Colours.WHITE);
    WriteLine(@"                 b  j  n", ScreenHeight - 1, 0, ScreenWidth, Colours.LIGHT_BLUE);
  }

  protected void WriteMovementCheatSheetOverlay()
  {
    List<(Colour, string)> w;

    w = [(Colours.WHITE, "Movement keys:   "), (Colours.LIGHT_BLUE, "y  k  u")];
    WriteText(w, ScreenHeight - 5, ScreenWidth - 26, ScreenWidth);
    WriteLine(@"                  \ | /      ", ScreenHeight - 4, ScreenWidth - 26, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, "                h"), (Colours.WHITE, " - @ - "), (Colours.LIGHT_BLUE, "l")];
    WriteText(w, ScreenHeight - 3, ScreenWidth - 26, ScreenWidth);
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

  public void WriteText(List<(Colour, string)> pieces, int lineNum, int col, int width)
  {
    foreach (var piece in pieces)
    {
      if (piece.Item2.Length == 0)
        continue;
      WriteLine(piece.Item2, lineNum, col, piece.Item2.Length, piece.Item1);
      col += piece.Item2.Length;
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

  int WriteSideBarLine(List<(Colour, string)> line, int row)
  {
    int lineWidth = line.Select(item => item.Item2.Length).Sum();

    if (lineWidth < SideBarWidth)
    {
      WriteText(line, row++, ViewWidth, SideBarWidth);
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
          WriteText(pieces, row++, ViewWidth, SideBarWidth);
          WriteText([(piece.Item1, part2)], row++, ViewWidth, SideBarWidth);
          width = 0;
          pieces = [];
        }
      }
    }

    return row;
  }

  protected void WriteSideBar(GameState gs)
  {
    int row = 0;
    WriteLine($"│ {gs.Player.Name}", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    int currHP = gs.Player.Stats[Attribute.HP].Curr;
    int maxHP = gs.Player.Stats[Attribute.HP].Max;
    WriteLine($"│ HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);

    int bottomOffset = _options.ShowTurns ? 2 : 1;

    if (gs.Player.Stats.TryGetValue(Attribute.MagicPoints, out var magicPoints) && magicPoints.Max > 0)
    {
      WriteLine($"│ MP: {magicPoints.Curr} ({magicPoints.Max})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    WriteLine($"│ AC: {gs.Player.AC}", row++, ViewWidth, SideBarWidth, Colours.WHITE);

    List<(Colour, string)> zorkmidLine = [(Colours.WHITE, "│  "), (Colours.YELLOW, "$"), (Colours.WHITE, $": {gs.Player.Inventory.Zorkmids}")];
    row = WriteSideBarLine(zorkmidLine, row);

    string blank = "│".PadRight(ViewWidth);
    WriteLine(blank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

    var weapon = gs.Player.Inventory.ReadiedWeapon();
    if (weapon is not null)
    {
      List<(Colour, string)> weaponLine = [(Colours.WHITE, "│ "), (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString())];
      if (weapon.HasTrait<TwoHandedTrait>() || (weapon.HasTrait<VersatileTrait>() && !gs.Player.Inventory.ShieldEquipped()))
        weaponLine.Add((Colours.WHITE, $" {weapon.FullName.IndefArticle()} (in hands)"));
      else
        weaponLine.Add((Colours.WHITE, $" {weapon.FullName.IndefArticle()} (in hand)"));
      row = WriteSideBarLine(weaponLine, row);
    }
    var bow = gs.Player.Inventory.ReadiedBow();
    if (bow is not null)
    {
      List<(Colour, string)> weaponLine = [(Colours.WHITE, "| "), (bow.Glyph.Lit, bow.Glyph.Ch.ToString())];
      weaponLine.Add((Colours.WHITE, $" {bow.FullName.IndefArticle()} (equipped)"));
      row = WriteSideBarLine(weaponLine, row);
    }

    for (; row < ViewHeight - 1; row++)
    {
      WriteLine(blank, row, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    // Write statuses
    int statusLineNum = ViewHeight - bottomOffset - 2;
    HashSet<string> statuses = [];
    foreach (var trait in gs.Player.Traits)
    {
      if (!statuses.Contains("POISONED") && trait is PoisonedTrait)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.GREEN, "POISONED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("POISONED");
      }
      else if (!statuses.Contains("NAUSEOUS") && gs.Player.HasTrait<NauseaTrait>())
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.GREEN, "NAUSEOUS")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("NAUSEOUS");
      }
      else if (!statuses.Contains("RAGE") && trait is RageTrait rage && rage.Active)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "RAGE")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("RAGE");
      }
      else if (!statuses.Contains("CURSED") && trait is CurseTrait)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "CURSED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("CURSED");
      }
      else if (!statuses.Contains("BERZERK") && trait is BerzerkTrait)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "BERZERK")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("BERZERK");
      }
      else if (!statuses.Contains("PROTECTION") && trait is AuraOfProtectionTrait aura)
      {
        Colour colour;
        if (aura.HP >= 25)
          colour = Colours.ICE_BLUE;
        else if (aura.HP > 10)
          colour = Colours.LIGHT_BLUE;
        else
          colour = Colours.BLUE;
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (colour, "PROTECTED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("PROTECTION");
      }
      else if (trait is ResistanceTrait resist)
      {
        List<(Colour, string)> statusLine;
        switch (resist.Type)
        {
          case DamageType.Fire:
            statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "RESIST FIRE")];
            row = WriteSideBarLine(statusLine, statusLineNum--);
            statuses.Add("RESIST FIRE");
            break;
          case DamageType.Cold:
            statusLine = [(Colours.WHITE, "│ "), (Colours.LIGHT_BLUE, "RESIST COLD")];
            row = WriteSideBarLine(statusLine, statusLineNum--);
            statuses.Add("RESIST COLD");
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
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (colour, st.Stress.ToString().ToUpper())];
        row = WriteSideBarLine(statusLine, statusLineNum--);
      }
    }
    if (!statuses.Contains("GRAPPLED") && gs.Player.HasActiveTrait<GrappledTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "GRAPPLED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("GRAPPLED");
    }
    if (!statuses.Contains("PARALYZED") && gs.Player.HasActiveTrait<ParalyzedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.YELLOW, "PARALYZED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("PARALYZED");
    }
    if (!statuses.Contains("CONFUSED") && gs.Player.HasActiveTrait<ConfusedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.YELLOW, "CONFUSED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("CONFUSED");
    }
    if (gs.Player.HasActiveTrait<ExhaustedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.PINK, "EXHAUSTED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("EXHAUSTED");
    }
    if (gs.Player.HasActiveTrait<LameTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.PINK, "LIMPING")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("LIMPING");
    }
    if (!statuses.Contains("TELEPATHIC") && gs.Player.HasActiveTrait<TelepathyTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.PURPLE, "TELEPATHIC")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("TELEPATHIC");
    }
    if (!statuses.Contains("LEVITATING") && gs.Player.HasActiveTrait<LevitationTrait>())
    {
      // Maybe change the colour if the effect is going to expire soon?
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.LIGHT_BLUE, "LEVITATING")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("LEVITATING");
    }
    if (!statuses.Contains("BLIND") && gs.Player.HasTrait<BlindTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.GREY, "BLIND")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("BLIND");
    }
    if (!statuses.Contains("TIPSY") && gs.Player.HasTrait<TipsyTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.PINK, "TIPSY")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("TIPSY");
    }
    if (!statuses.Contains("AFRAID") && gs.Player.HasTrait<FrightenedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.YELLOW, "AFRAID")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("AFRAID");
    }
    if (!statuses.Contains("OBSCURED") && gs.Player.HasTrait<NondescriptTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.GREY, "OBSCURED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("OBSCURED");
    }
    if (!statuses.Contains("BLESSED") && gs.Player.HasTrait<BlessingTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.YELLOW, "BLESSED")];
      WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("YELLOW");
    }
    foreach (StatDebuffTrait statBuff in gs.Player.Traits.OfType<StatDebuffTrait>())
    {
      if (!statuses.Contains("WEAKENED") && statBuff.Attr == Attribute.Strength)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "│ "), (Colours.BRIGHT_RED, "WEAKENED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("WEAKENED");
      }
    }

    var tile = gs.TileAt(gs.Player.Loc);
    var glyph = Util.TileToGlyph(tile);
    var tileSq = new Sqr(glyph.Lit, Colours.BLACK, glyph.Ch);
    var tileText = Tile.TileDesc(tile.Type).Capitalize();
    foreach (var item in gs.ObjDb.EnvironmentsAt(gs.Player.Loc))
    {
      if (item.Type == ItemType.Environment && item.Name != "light" && item.Name != "photon")
      {
        var g = item.Glyph;
        tileSq = new Sqr(g.Lit, Colours.BLACK, g.Ch);
        tileText = $"Some {item.Name}";
        break;
      }
    }

    List<(Colour, string)> tileLine = [(Colours.WHITE, "│ "), (tileSq.Fg, tileSq.Ch.ToString()), (Colours.WHITE, " " + tileText)];
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
    int width = MenuRows!.Select(r => r.Length).Max() + 2;
    int col = ViewWidth - width;
    int row = 0;

    Colour colour = _popup is null ? Colours.WHITE : Colours.GREY;
    foreach (var line in MenuRows!)
    {
      WriteLine(" " + line, row++, col, width, colour);
    }
    WriteLine("", row, col, width, colour);
  }

  public void AlertPlayer(string alert)
  {
    if (alert.Trim().Length == 0)
      return;

    Messages.Enqueue(alert);
  }

  public void AlertPlayer(string alert, GameState gs, Loc loc)
  {
    if (!gs.LastPlayerFoV.Contains(loc))
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

  public void WriteLongMessage(List<string> message) => _longMessage = message;
  public void ShowDropDown(List<string> lines) => MenuRows = lines;
  public void CloseMenu() => MenuRows = [];

  static void Delay(int ms = 10) => Thread.Sleep(ms);

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
      WriteLongMessage(menu);
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

  static Sqr SqrToDisplay(GameState gs, Dictionary<Loc, Glyph> remembered, Loc loc, Sqr zsqr)
  {
    Sqr sqr;
    if (gs.LastPlayerFoV.Contains(loc))
    {
      if (zsqr != Constants.BLANK_SQ)
      {
        sqr = zsqr;
      }
      else
      {
        Glyph glyph;
        bool isMob = false;
        if (gs.ObjDb.Occupant(loc) is Actor actor && actor.VisibleTo(gs.Player))
        {
          glyph = actor.Glyph;
          isMob = true;
        }
        else if (gs.ObjDb.FogAtLoc(loc, gs.Player.Loc) is Glyph fog)
          glyph = fog;
        else if (remembered.TryGetValue(loc, out Glyph rememberedGlyph))
          glyph = rememberedGlyph;
        else
          glyph = GameObjectDB.EMPTY;

        Colour fgColour, bgColour;
        if (glyph.Lit != Colours.FAR_BELOW && gs.LitSqs.TryGetValue(loc, out (Colour FgColour, Colour BgColour, double Scale) lightInfo))
        {
          double scale = isMob ? double.Min(1.0, lightInfo.Scale + 0.15) : lightInfo.Scale;
          fgColour = glyph.Illuminate ? lightInfo.FgColour : glyph.Lit;
          int alpha = int.Max(15, (int)(fgColour.Alpha * scale));
          fgColour = fgColour with { Alpha = alpha };

          bgColour = glyph.BG;
          if (bgColour == Colours.BLACK)
            bgColour = lightInfo.BgColour;

          alpha = int.Max(15, (int)(bgColour.Alpha * scale));
          bgColour = bgColour with { Alpha = alpha };

          if (isMob)
            bgColour = glyph.BG;
        }
        else
        {
          fgColour = glyph.Lit;
          bgColour = glyph.BG;
        }

        sqr = new Sqr(fgColour, bgColour, glyph.Ch);
      }
    }
    else if (remembered.TryGetValue(loc, out var glyph))
    {
      if (gs.InWilderness && remembered.ContainsKey(loc) && gs.Town.Roofs.Contains(loc))
      {
        sqr = Constants.ROOF;
      }
      else if (gs.CurrentMap.Submerged)
      {
        Colour bg = Colours.UNDERWATER with { Alpha = Colours.UNDERWATER.Alpha - 10 };
        Colour fg = glyph.Unlit with { Alpha = glyph.Unlit.Alpha / 2 };
        sqr = new Sqr(fg, bg, glyph.Ch);
      }
      else
      {
        sqr = new Sqr(glyph.Unlit, glyph.BG, glyph.Ch);
      }
    }
    else
    {
      sqr = Constants.BLANK_SQ;
    }

    return sqr;
  }

  void SetSqsOnScreen(GameState gs)
  {
    Dungeon dungeon = gs.CurrentDungeon;
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
        Sqr sqr = SqrToDisplay(gs, dungeon.RememberedLocs, loc, ZLayer[r, c]);

        SqsOnScreen[r, c] = sqr;
      }
    }

    if (gs.Player.HasActiveTrait<TelepathyTrait>())
    {
      // If the player has telepathy, find any nearby monsters and display them
      // plus the squares adjacent to them
      int range = int.Max(ViewHeight / 2, ViewWidth / 2);
      foreach (Actor mob in gs.ObjDb.ActorsWithin(gs.Player.Loc, range))
      {
        if (mob.HasTrait<BrainlessTrait>())
          continue;

        if (mob != gs.Player)
        {
          List<Loc> viewed = [mob.Loc];
          if (!gs.LastPlayerFoV.Contains(mob.Loc))
            viewed.AddRange(Util.Adj8Locs(mob.Loc));

          foreach (Loc loc in viewed)
          {
            // Eventually I want to reveal gargoyles, mimics, etc as well via 
            // telepathy
            if (gs.LastPlayerFoV.Contains(loc) && !mob.HasTrait<InvisibleTrait>())
              continue;

            int screenRow = loc.Row - rowOffset;
            int screenCol = loc.Col - colOffset;
            if (screenRow >= 0 && screenRow < ViewHeight && screenCol >= 0 && screenCol < ViewWidth)
            {
              Glyph g = gs.ObjDb.GlyphAt(loc);
              if (g == GameObjectDB.EMPTY)
                g = Util.TileToGlyph(gs.TileAt(loc));

              var sqr = new Sqr(Colours.LIGHT_GREY, Colours.FADED_PURPLE, g.Ch);
              SqsOnScreen[screenRow, screenCol] = sqr;
            }
          }
        }
      }
    }

    if (ZLayer[PlayerScreenRow, PlayerScreenCol] == Constants.BLANK_SQ)
    {
      SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(PlayerGlyph.Lit, PlayerGlyph.BG, PlayerGlyph.Ch);
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

  void DrawGravestone(GameState gameState, List<string> messages)
  {
    List<string> text =
      [
        "       ",
           "         __________________",
          @"        /                  \",
          @"       /       RIP          \   ___",
          @"      /                      \ /   \",
          @"     /                        \|    |       __",
          @"    |       killed by:         |___|       /  \",
          @"    |                          |         |     |",
          @"    |                          |         |_____|",
          @"    |                          |      ___",
          @"    |                          |     /   \",
          @"    |                          |    |     |",
          @"    |                          |    |     |",
          @"    *       *         *        |*  _*__)__|_",
          @"____)/\_____(\__/____\(/_______\)/_|(/____"
        ];

    string depth = gameState.Player.Stats[Attribute.Depth].Curr.ToString()!.PadRight(2);
    text[5] = $@"     /{gameState.Player.Name.PadLeft((21 + gameState.Player.Name.Length) / 2).PadRight(24)}\    |        __";
    text[7] = $@"    |{messages[0].PadLeft((22 + messages[0].Length) / 2),-26}|          |    |";
    text[8] = $@"    |       on level {depth}        |          |____|";

    if (messages.Count > 1)
    {
      string s = $@"    |{messages[1].PadLeft((22 + messages[1].Length) / 2),-26}|          |    |";
      text.Insert(8, s);
    }

    ClosePopup();
    CheatSheetMode = CheatSheetMode.Messages;
    SqsOnScreen = new Sqr[ScreenHeight, ScreenWidth];
    ClearSqsOnScreen();
    for (int r = 0; r < text.Count; r++)
    {
      string row = text[r];
      for (int c = 0; c < row.Length; c++)
      {
        Sqr s = new(Colours.WHITE, Colours.BLACK, row[c]);
        SqsOnScreen[r + 1, c + 1] = s;
      }
    }

    BlockForInput(gameState);
  }

  // Clear out anything that should shouldn't persist between games
  void Reset()
  {
    MessageHistory = [];
    _animations = [];
  }

  public RunningState GameLoop(GameState gameState)
  {
    Options opts = gameState.Options;

    _animations.Add(new CloudAnimation(this, gameState));
    //_animations.Add(new RainAnimation(this, gameState));

    InputController = new PlayerCommandController(gameState);
    DateTime refresh = DateTime.UtcNow;

    while (true)
    {
      var e = PollForEvent();
      if (e.Type == GameEventType.Quiting)
        break;

      if (e.Type == GameEventType.KeyInput)
      {
        char ch;
        if (opts.KeyRemaps.TryGetValue(e.Value, out var cmd))
          ch = KeyMapper.CmdToKey(cmd);
        else
          ch = e.Value;

        InputController.Input(ch);
      }

      try
      {
        if (gameState.NextPerformer() is Actor actor)
        {
          actor.TakeTurn(gameState);
          if (actor.Energy >= 1.0)
            gameState.PushPerformer(actor);
        }

        WriteAlerts();
      }
      catch (SaveGameException)
      {
        WriteAlerts();

        bool success;
        try
        {
          Serialize.WriteSaveGame(gameState, this);
          success = true;

          WriteLongMessage([" Be seeing you..."]);
          BlockForInput(gameState);
        }
        catch (Exception ex)
        {
          SetPopup(new Popup(ex.Message, "", -1, -1));
          success = false;
        }

        if (success)
          return RunningState.Quitting;
      }
      catch (QuitGameException)
      {
        Reset();
        return RunningState.GameOver;
      }
      catch (PlayerKilledException pke)
      {
        string s = $"Oh noes you've been killed by {pke.Messages[0]} :(";
        if (gameState.Player.HasTrait<ParalyzedTrait>())
          pke.Messages.Add("while paralyzed");
        SetPopup(new Popup(s, "", -1, -1));
        WriteAlerts();
        BlockFoResponse(gameState);

        DrawGravestone(gameState, pke.Messages);
        Reset();
        return RunningState.GameOver;
      }
      catch (VictoryException)
      {
        Reset();
        return RunningState.GameOver;
      }

      TimeSpan elapsed = DateTime.UtcNow - refresh;
      int totalMs = (int)elapsed.TotalMilliseconds;
      if (totalMs >= 25)
      {
        SetSqsOnScreen(gameState);

        foreach (var l in _animations)
          l.Update();
        _animations = [.. _animations.Where(a => a.Expiry > DateTime.UtcNow)];

        UpdateDisplay(gameState);

        refresh = DateTime.UtcNow;
      }
    }

    return RunningState.Quitting;
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