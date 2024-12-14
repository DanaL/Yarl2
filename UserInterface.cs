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

  protected abstract GameEvent PollForEvent();
  protected abstract void WriteSq(int row, int col, Sqr sq);
  protected abstract void ClearScreen();
  protected abstract void Blit(); // Is blit the right term for this? 'Presenting the screen'
  
  protected int FontSize;
  public int PlayerScreenRow { get; protected set; }
  public int PlayerScreenCol { get; protected set; }
  protected List<string>? _longMessage;
  protected Options _options;
  
  public Queue<char> InputBuffer = new Queue<char>();

  public Sqr[,] SqsOnScreen;
  public Sqr[,] ZLayer; // An extra layer of tiles to use for effects like clouds

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

  public UserInterface(Options opts)
  {
    _options = opts;
    PlayerScreenRow = ViewHeight / 2;
    PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
    ZLayer = new Sqr[ViewHeight, ViewWidth];
    ClearZLayer();

    if (opts.HighlightPlayer)
      PlayerGlyph = new Glyph('@', Colours.WHITE, Colours.WHITE, Colours.HILITE, Colours.HILITE);
    else
      PlayerGlyph = new Glyph('@', Colours.WHITE, Colours.WHITE, Colours.BLACK, Colours.BLACK);
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
    BlockForInput();
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

      if (bark is not null && bark.Expiry > DateTime.Now)
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

  public void ClosePopup()
  {
    _popup = null;
    _confirm = null;
  }

  public void CloseConfirmation() => _confirm = null;

  public void SetPopup(IPopup popup, bool fullWidth = false) 
  {
    popup.FullWidth = fullWidth;
    _popup = popup;
  }

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
    while (animation.Expiry > DateTime.Now)
    {
      SetSqsOnScreen(gs);
      animation.Update();
      UpdateDisplay(gs);
      Delay(75);
    }
  }

  protected void WritePopUp() =>  _popup?.Draw(this);
  protected void WriteConfirmation() => _confirm?.Draw(this);

  void WriteCommandCheatSheet()
  {
    List<(Colour, string)> w;
    WriteLine("Commands:", ScreenHeight - 5, 0, ScreenWidth, Colours.WHITE);
    w = [(Colours.LIGHT_BLUE, " a"), (Colours.LIGHT_GREY, ": use item  "), (Colours.LIGHT_BLUE, "c"), (Colours.LIGHT_GREY, ": close door  "),
      (Colours.LIGHT_BLUE, "C"), (Colours.LIGHT_GREY, ": chat  "), (Colours.LIGHT_BLUE, "d"), (Colours.LIGHT_GREY, ": drop item  "),
      (Colours.LIGHT_BLUE, "e"), (Colours.LIGHT_GREY, ": equip/unequip item")];
    //s = "a - Use item  c - close door  C - chat  d - drop item  e - equip/unequip item"; 
    WriteText(w, ScreenHeight - 4, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " f"), (Colours.LIGHT_GREY, ": fire bow  "), (Colours.LIGHT_BLUE, "F"), (Colours.LIGHT_GREY, ": bash door  "),
      (Colours.LIGHT_BLUE, "i"), (Colours.LIGHT_GREY, ": view inventory  "), (Colours.LIGHT_BLUE, "M"), (Colours.LIGHT_GREY, ": view map  "),
      (Colours.LIGHT_BLUE, "o"), (Colours.LIGHT_GREY, ": open door  ")
    ];
    WriteText(w, ScreenHeight - 3, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " S"), (Colours.LIGHT_GREY, ": save game  "), (Colours.LIGHT_BLUE, "Q"), (Colours.LIGHT_GREY, ": quit  "),
      (Colours.LIGHT_BLUE, "t"), (Colours.LIGHT_GREY, ": throw item  "), (Colours.LIGHT_BLUE, "x"), (Colours.LIGHT_GREY, ": examine item  "),
      (Colours.LIGHT_BLUE, ","), (Colours.LIGHT_GREY, ": pickup item")
    ];
    WriteText(w, ScreenHeight - 2, 0, ScreenWidth);

    w = [(Colours.LIGHT_BLUE, " @"), (Colours.LIGHT_GREY, ": character info  "), (Colours.LIGHT_BLUE, "<"),
      (Colours.LIGHT_GREY, " or "), (Colours.LIGHT_BLUE, ">"), (Colours.LIGHT_GREY, ": use stairs  "),
      (Colours.LIGHT_BLUE, "*"), (Colours.LIGHT_GREY, ": message history  "),
      (Colours.LIGHT_BLUE, "="), (Colours.LIGHT_GREY, ": options")];   
    WriteText(w, ScreenHeight - 1, 0, ScreenHeight);
  } 

  void WriteMessages()
  {
    var msgs = MessageHistory.Take(5)
                             .Select(msg => msg.Fmt);

    int count = 0;
    int row = ScreenHeight - 1;
    Colour colour = Colours.WHITE;
    foreach (var msg in msgs)
    {
      if (msg.Length >= ScreenWidth)
      {
        int c;
        // Find the point to split the line. I'm never going to send a
        // message that's a string with no spaces wider than the 
        // screen am I...
        for (c = ScreenWidth - 1; c >= 0; c--)
        {
          if (msg[c] == ' ')
            break;
        }
        string s1 = msg[(c + 1)..].TrimStart().PadRight(ScreenWidth);
        string s2 = msg[..c].TrimStart().PadRight(ScreenWidth);
        WriteLine(s1, row--, 0, ScreenWidth, colour);
        if (ScreenHeight - row < 5)
          WriteLine(s2, row--, 0, ScreenWidth, colour);
      }
      else
      {
        string s = msg.PadRight(ScreenWidth);
        WriteLine(s, row--, 0, ScreenWidth, colour);
      }

      if (++count == 5)
        break;

      if (colour == Colours.WHITE)
        colour = Colours.GREY;
      else if (colour == Colours.GREY)
        colour = Colours.DARK_GREY;
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

  int FindSplit(string txt)
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
          string part2 = "|  " + piece.Item2[pos..];
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
    WriteLine($"| {gs.Player.Name}", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    int currHP = gs.Player.Stats[Attribute.HP].Curr;
    int maxHP = gs.Player.Stats[Attribute.HP].Max;
    WriteLine($"| HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    WriteLine($"| AC: {gs.Player.AC}", row++, ViewWidth, SideBarWidth, Colours.WHITE);

    List<(Colour, string)> zorkmidLine = [(Colours.WHITE, "|  "), (Colours.YELLOW, "$"), (Colours.WHITE, $": {gs.Player.Inventory.Zorkmids}")];
    row = WriteSideBarLine(zorkmidLine, row);
    
    string blank = "|".PadRight(ViewWidth);
    WriteLine(blank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

    var weapon = gs.Player.Inventory.ReadiedWeapon();
    if (weapon is not null)
    {
      List<(Colour, string)> weaponLine = [(Colours.WHITE, "| "), (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString())];
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
    int statusLineNum = ViewHeight - 3;
    HashSet<string> statuses = [];
    foreach (var trait in gs.Player.Traits)
    {
      if (!statuses.Contains("POISONED") && trait is PoisonedTrait)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.GREEN, "POISONED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("POISONED");
      }
      else if (!statuses.Contains("NAUSEOUS") && gs.Player.HasTrait<NauseaTrait>())
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.GREEN, "NAUSEOUS")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("NAUSEOUS");
      }
      else if (!statuses.Contains("RAGE") && trait is RageTrait rage && rage.Active) 
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "RAGE")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("RAGE");
      }
      else if (!statuses.Contains("BERZERK") && trait is BerzerkTrait) 
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "BERZERK")];
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
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (colour, "PROTECTED")];
        row = WriteSideBarLine(statusLine, statusLineNum--);
        statuses.Add("PROTECTION");
      }
      else if (trait is ResistanceTrait resist) 
      {
        List<(Colour, string)> statusLine;
        switch (resist.Type)
        {
          case DamageType.Fire:
            statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "RESIST FIRE")];
            row = WriteSideBarLine(statusLine, statusLineNum--);
            statuses.Add("RESIST FIRE");
            break;
          case DamageType.Cold:
            statusLine = [(Colours.WHITE, "| "), (Colours.LIGHT_BLUE, "RESIST COLD")];
            row = WriteSideBarLine(statusLine, statusLineNum--);
            statuses.Add("RESIST COLD");
            break;
        }
      }
    }
    if (!statuses.Contains("GRAPPLED") && gs.Player.HasActiveTrait<GrappledTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "GRAPPLED")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("GRAPPLED");
    }
    if (!statuses.Contains("PARALYZED") && gs.Player.HasActiveTrait<ParalyzedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.YELLOW, "PARALYZED")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("PARALYZED");
    }
    if (!statuses.Contains("CONFUSED") && gs.Player.HasActiveTrait<ConfusedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.YELLOW, "CONFUSED")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("CONFUSED");
    }
    if (gs.Player.HasActiveTrait<ExhaustedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.PINK, "EXHAUSTED")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("EXHAUSTED");
    }
    if (!statuses.Contains("TELEPATHIC") && gs.Player.HasActiveTrait<TelepathyTrait>())
    {      
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.PURPLE, "TELEPATHIC")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("TELEPATHIC");
    }
    if (!statuses.Contains("LEVITATING") && gs.Player.HasActiveTrait<LevitationTrait>())
    {
      // Maybe change the colour if the effect is going to expire soon?
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.LIGHT_BLUE, "LEVITATING")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("LEVITATING");
    }
    if (!statuses.Contains("BLIND") && gs.Player.HasTrait<BlindTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.GREY, "BLIND")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("BLIND");
    }
    if (!statuses.Contains("TIPSY") && gs.Player.HasTrait<TipsyTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.PINK, "TIPSY")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("TIPSY");
    }
    if (!statuses.Contains("AFRAID") && gs.Player.HasTrait<FrightenedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.YELLOW, "AFRAID")];
      row = WriteSideBarLine(statusLine, statusLineNum--);
      statuses.Add("AFRAID");
    }
    foreach (StatDebuffTrait statBuff in gs.Player.Traits.OfType<StatDebuffTrait>())
    {
      if (!statuses.Contains("WEAKENED") && statBuff.Attr == Attribute.Strength)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "WEAKENED")];
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
      if (item.Type == ItemType.Environment)
      {
        var g = item.Glyph;
        tileSq = new Sqr(g.Lit, Colours.BLACK, g.Ch);
        tileText = $"Some {item.Name}";
        break;
      }
    }

    List<(Colour, string)> tileLine = [(Colours.WHITE, "| "), (tileSq.Fg, tileSq.Ch.ToString()), (Colours.WHITE, " " + tileText)];
    WriteSideBarLine(tileLine, ViewHeight - 2);

    if (gs.CurrDungeonID == 0)
    {
      var time = gs.CurrTime();
      var mins = time.Item2.ToString().PadLeft(2, '0');
      WriteLine($"| Outside {time.Item1}:{mins}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
    }
    else
    {
      WriteLine($"| Depth: {gs.CurrLevel + 1}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
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

  // TODO: DRY the two versions of AlertPlayer
  public void AlertPlayer(string alert)
  {            
    if (alert.Trim().Length == 0)
      return;

    HistoryUpdated = true;

    if (MessageHistory.Count > 0 && MessageHistory[0].Message == alert)
      MessageHistory[0] = new MsgHistory(alert, MessageHistory[0].Count + 1);
    else
      MessageHistory.Insert(0, new MsgHistory(alert, 1));

    if (MessageHistory.Count > MaxHistory)
      MessageHistory.RemoveAt(MaxHistory);
  }

  public void AlertPlayer(List<string> messages)
  {
    string msgText = string.Join(' ', messages).Trim();
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

  void TakeTurn(IPerformer performer, GameState gs)
  {
    static double CalcEnergyUsed(double baseCost, IPerformer performer)
    {
      if (performer is not Actor actor)
        return baseCost;

      // Maybe I should come up with a formal/better way to differentiate 
      // between real in-game actions and things like opening inventory or
      // looking athelp, etc?
      if (baseCost == 0)
        return 0;

      // Note also there are some actions like Chatting, etc that
      // shouldn't be made faster or slower by alacrity, but I'll
      // worry about that later

      foreach (var t in actor.Traits.OfType<AlacrityTrait>())
      {
        baseCost -= t.Amt;
      }

      // I think boosts to speed should get you only so far
      return Math.Max(0.35, baseCost);
    }

    var action = performer.TakeTurn(gs);
    
    if (action is NullAction)
    {
      // Player is idling
      return;
    }

    if (action is QuitAction)
    {
      // It feels maybe like overkill to use an exception here?
      if (InTutorial)
        // this returns us to the title screen instead of exiting the program
        throw new PlayerKilledException("");
      else
        throw new GameQuitException();
    }
    else if (action is SaveGameAction)
    {
      bool success;
      try
      {
        Serialize.WriteSaveGame(gs, this);
        success = true;
      }
      catch (Exception ex)
      {
        SetPopup(new Popup(ex.Message, "", -1, -1));
        success = false;
      }

      if (success)
        throw new GameQuitException();
    }    
    else
    {
      ActionResult result;
      do
      {
        if (performer is Player)
        {
          gs.PrepareFieldOfView();
        }
        result = action!.Execute();

        // I don't think I need to look over IPerformer anymore? The concept of 
        // items as performs is gone. I think?
        double energyUsed = CalcEnergyUsed(result.EnergyCost, performer);
        performer.Energy -= energyUsed;
        if (result.AltAction is not null)
        {
          if (result.Messages.Count > 0)
            AlertPlayer(result.Messages);
          result = result.AltAction.Execute();
          performer.Energy -= CalcEnergyUsed(result.EnergyCost, performer);
          action = result.AltAction;
        }

        if (result.Messages.Count > 0)
          AlertPlayer(result.Messages);

        if (performer is Player)
        {
          gs.PrepareFieldOfView();
        }
      }
      while (result.AltAction is not null);
    }
  }

  static void Delay(int ms = 10) => Thread.Sleep(ms);

  // I am using this in input menus outside of the main game. Primarily
  // the start menu
  public char GetKeyInput()
  {
    var e = PollForEvent();
    if (e.Type == GameEventType.Quiting)
      throw new GameQuitException();

    if (e.Type == GameEventType.KeyInput)
      return e.Value;

    return '\0';
  }

  public void BlockForInput()
  {
    GameEvent e;
    do
    {
      e = PollForEvent();

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
        throw new GameQuitException();
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
        throw new GameQuitException();
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
      _confirm = new Popup(txt, "", ViewHeight / 2 - 2,  ScreenWidth / 2);
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
        throw new GameQuitException();
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
        throw new GameQuitException();
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
        if (gs.ObjDb.Occupant(loc) is Actor actor && actor.VisibleTo(gs.Player))
          glyph = actor.Glyph;
        else 
          glyph = remembered[loc];
        sqr = new Sqr(glyph.Lit, glyph.BGLit, glyph.Ch);
      }
    }
    else if (remembered.TryGetValue(loc, out var glyph))
    {
      sqr = new Sqr(glyph.Unlit, glyph.BGUnlit, glyph.Ch);
    }
    else
    {
      sqr = Constants.BLANK_SQ;
    }

    return sqr;
  }

  void SetSqsOnScreen(GameState gs)
  {
    var dungeon = gs.CurrentDungeon;
    int playerRow = gs.Player.Loc.Row;
    int playerCol = gs.Player.Loc.Col;
    
    int rowOffset = playerRow - PlayerScreenRow;
    int colOffset = playerCol - PlayerScreenCol;

    for (int r = 0; r < ViewHeight; r++)
    {
      for (int c = 0; c < ViewWidth; c++)
      {
        // replace w/ LocToScrLoc?
        int mapRow = r + rowOffset;
        int mapCol = c + colOffset;
        
        var loc = new Loc(gs.CurrDungeonID, gs.CurrLevel, mapRow, mapCol);
        var sqr = SqrToDisplay(gs, dungeon.RememberedLocs, loc, ZLayer[r, c]);
         
        SqsOnScreen[r, c] = sqr;
      }
    }

    if (gs.Player.HasActiveTrait<TelepathyTrait>())
    {
      // If the player has telepathy, find any nearby monsters and display them
      // plus the squares adjacent to them
      int range = int.Max(ViewHeight / 2, ViewWidth / 2);
      foreach (var mob in gs.ObjDb.ActorsWithin(gs.Player.Loc, range))
      {
        if (mob != gs.Player)
        {
          var viewed = Util.Adj8Locs(mob.Loc).ToList();
          viewed.Add(mob.Loc);
          foreach (Loc loc in viewed)
          {
            if (gs.LastPlayerFoV.Contains(loc))
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
      SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(PlayerGlyph.Lit, PlayerGlyph.BGLit, PlayerGlyph.Ch);
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

  public void DisplayMapView(GameState gs)
  {
    var dungeon = gs.Campaign.Dungeons[gs.CurrDungeonID];
    var remembered = dungeon.RememberedLocs;

    var blank = new Sqr(Colours.BLACK, Colours.BLACK, ' ');
    Sqr[,] sqs = new Sqr[ScreenHeight, ScreenWidth];
    for (int r = 0; r < ScreenHeight; r++)
    {
      for (int c= 0; c < ScreenWidth; c++)
      {
        var loc = new Loc(gs.CurrDungeonID, gs.CurrLevel, r, c);
        var sq = remembered.TryGetValue(loc, out var g) ? new Sqr(g.Unlit, Colours.BLACK, g.Ch) : blank;

        // We'll make the stairs more prominent on the map so they stand out 
        // better to the player
        if (sq.Ch == '>' || sq.Ch == '<')
          sq = sq with { Fg = Colours.WHITE };

        sqs[r, c] = sq;                  
      }
    }

    int playerRow = gs.Player.Loc.Row;
    int playerCol = gs.Player.Loc.Col;
    sqs[playerRow, playerCol] = new Sqr(Colours.WHITE, Colours.BLACK, '@');

    DrawFullScreen(sqs);
    BlockForInput();
  }

  void DrawFullScreen(Sqr[,] sqs)
  {
    ClearScreen();

    var height = sqs.GetLength(0);
    var width = sqs.GetLength(1);
    for (int r = 0; r < height; r++)
    {
      for (int c = 0; c < width; c++)
      {
        WriteSq(r, c, sqs[r, c]);
      }
    }

    Blit();
  }

  void DrawGravestone(GameState gameState, string message)
  {
    string[] text =
      [
        "       ",
           "         __________________",
          @"        /                  \",
          @"       /        RIP         \   ___",
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
    text[7] = $@"    |{message.PadLeft((22 + message.Length) / 2).PadRight(26)}|          |    |";
    text[8] = $@"    |       on level: {depth}       |          |____|";
    ClosePopup();
    SqsOnScreen = new Sqr[ScreenHeight, ScreenWidth];
    ClearSqsOnScreen();
    for (int r = 0; r < text.Length; r++)
    {
      string row = text[r];
      for (int c = 0; c < row.Length; c++)
      {
        Sqr s = new(Colours.WHITE, Colours.BLACK, row[c]);
        SqsOnScreen[r + 1, c + 1] = s;
      }
    }
    UpdateDisplay(gameState);
    BlockForInput();
  }

  public RunningState GameLoop(GameState gameState)
  {
    Options opts = gameState.Options;
    gameState.BuildPerformersList();
    _animations.Add(new CloudAnimationListener(this, gameState));
    _animations.Add(new TorchLightAnimationListener(this, gameState));

    DateTime refresh = DateTime.Now;
    IPerformer currPerformer = gameState.Player;
    while (true)
    {
      var e = PollForEvent();
      if (e.Type == GameEventType.Quiting)
        break;

      if (e.Type == GameEventType.KeyInput)
      {
        if (opts.KeyRemaps.TryGetValue(e.Value, out var cmd))
          InputBuffer.Enqueue(KeyMapper.CmdToKey(cmd));
        else
          InputBuffer.Enqueue(e.Value);        
      }

      try
      {
        // Update step! This is where all the current performers gets a chance
        // to take their turn!
        if (currPerformer.Energy < 1.0) 
        {
          currPerformer = gameState.NextPerformer();
        }
        
        TakeTurn(currPerformer, gameState);        
      }
      catch (GameQuitException)
      {
        return RunningState.Quitting;
      }
      catch (PlayerKilledException pke)
      {
        MessageHistory = [];
        if (!InTutorial)
        {
          DrawGravestone(gameState, pke.Message);
        }
        return RunningState.GameOver;
      }
      catch (VictoryException) 
      {
        MessageHistory = [];
        return RunningState.GameOver;
      }

      TimeSpan elapsed = DateTime.Now - refresh;
      int totalMs = (int) elapsed.TotalMilliseconds;
      if (totalMs >= 16)
      {
        SetSqsOnScreen(gameState);

        foreach (var l in _animations)
          l.Update();
        _animations = _animations.Where(a => a.Expiry > DateTime.Now)
                                 .ToList();
        UpdateDisplay(gameState);
        refresh = DateTime.Now;
      }
      else
      {
        //Delay(5);
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