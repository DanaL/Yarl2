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
  public const int ViewHeight = ScreenHeight - 5;

  public abstract void UpdateDisplay(GameState? gs);
  protected abstract GameEvent PollForEvent();
  protected abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour);
  protected abstract void WriteSq(int row, int col, Sqr sq);
  protected abstract void ClearScreen();
  protected abstract void Blit(); // Is blit the right term for this? 'Presenting the screen'

  protected int FontSize;
  protected int PlayerScreenRow;
  protected int PlayerScreenCol;
  protected List<string>? _longMessage;
  protected Options _options;
  
  public Queue<char> InputBuffer = new Queue<char>();

  public Sqr[,] SqsOnScreen;
  public Tile[,] ZLayer; // An extra layer of tiles to use for effects like clouds

  protected List<string> MenuRows { get; set; } = [];

  protected bool ClosingPopUp { get; set; }
  protected bool OpeningPopUp { get; set; }
  protected string _popupBuffer = "";
  protected string _popupTitle = "";

  public List<MsgHistory> MessageHistory = [];
  protected readonly int MaxHistory = 50;
  protected bool HistoryUpdated = false;

  List<Animation> _animations = [];

  public UserInterface(Options opts)
  {
    _options = opts;
    PlayerScreenRow = ViewHeight / 2;
    PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
    ZLayer = new Tile[ViewHeight, ViewWidth];
    ClearZLayer();
  }

  public virtual void TitleScreen()
  {
    _longMessage =
    [
        "",
            "",
            "",
            "",
            "     Welcome to Dana's Delve",
            "       (yet another attempt to make a roguelike,",
            "           this time in C#...)"
    ];

    UpdateDisplay(null);
    BlockForInput();
    ClearLongMessage();
  }

  public void ClearLongMessage()
  {
    _longMessage = null;
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
          if (gs.TileAt(loc).Passable() && !gs.ObjDb.Occupied(loc))
            locs.Add(loc);
        }
      }

      if (locs.Count > 0)
      {
        var loc = locs[gs.Rng.Next(locs.Count)];
        gs.ObjDb.ActorMoved(villager, villager.Loc, loc);
        villager.Loc = loc;
      }
    }

    gs.CurrDungeonID = 0;
    gs.CurrLevel = 0;

    Popup($"\nYou have defeated {bossName}!\n\n  -- Press any key to continue --", "Victory");
    UpdateDisplay(gs);
    BlockForInput();
    ClearLongMessage();

    ClearScreen();

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
    gs.Player.Loc = playerLoc;

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
      for (int r = 0; r < ViewHeight; r++)
      {
        for (int c = 0; c < ScreenWidth / 2; c++)
        {
          SqsOnScreen[r, c] = Constants.BLANK_SQ;
        }
      }

      int screenR = 6;
      int screenC = 7;
      for (int r = minRow - 6; r < maxRow + 6; r++)
      {
        for (int c = minCol - 6; c < maxCol + 11; c++)
        {
          Glyph glyph;
          if (r == playerRow && c == playerCol)
          {
            glyph = new Glyph('@', Colours.WHITE, Colours.BLACK);
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

  public void KillScreen(string message, GameState gs)
  {
    Popup(message);
    SetSqsOnScreen(gs);
    UpdateDisplay(gs);
    BlockForInput();
    ClearLongMessage();
  }

  public void ClosePopup()
  {
    _popupBuffer = "";
    _popupTitle = "";
    ClosingPopUp = true;
  }

  public void Popup(string message, string title = "")
  {
    _popupBuffer = message;
    _popupTitle = title;
    OpeningPopUp = true;
    ClosingPopUp = false;
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

  protected void WriteMessagesSection()
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

  List<(Colour, string)> SplitPopupPiece((Colour, string) piece, int maxWidth)
  {
    List<(Colour, string)> split = [];

    var sb = new StringBuilder();
    foreach (var word in piece.Item2.Split(' '))
    {
      if (sb.Length + word.Length < maxWidth)
      {
        sb.Append(word);
        sb.Append(' ');
      }
      else
      {
        split.Add((piece.Item1, sb.ToString()));
        sb = new StringBuilder(word);
        sb.Append(' ');
      }
    }
    if (sb.Length > 0)
      split.Add((piece.Item1, sb.ToString()));

    return split;
  }

  // This is going to look ugly if a message contains a long line
  // followed by a line break then short line but I don't know
  // if I'm ever going to need to worry about that in my game.
  List<List<(Colour, string)>> ResizePopupLines(List<List<(Colour, string)>> lines, int maxWidth)
  {
    List<List<(Colour, string)>> resized = [];
    foreach (var line in lines)
    {
      if (PopupLineWidth(line) < maxWidth)
      {
        resized.Add(line);
      }
      else
      {
        Queue<(Colour, string)> q = [];
        foreach (var p in line)
        {
          if (p.Item2.Length < maxWidth)
          {
            q.Enqueue(p);
          }
          else
          {
            foreach (var split in SplitPopupPiece(p, maxWidth))
              q.Enqueue(split);
          }
        }

        List<(Colour, string)> resizedLine = [];
        while (q.Count > 0)
        {
          var curr = q.Dequeue();
          if (PopupLineWidth(resizedLine) + curr.Item2.Length < maxWidth)
          {
            resizedLine.Add(curr);
          }
          else
          {
            resized.Add(resizedLine);
            resizedLine = [curr];
          }
        }
        if (resizedLine.Count > 0)
          resized.Add(resizedLine);
      }
    }

    return resized;
  }

  // I'm sure there is a much cleaner version of this using a stack, but I
  // just want to add some colour to the shopkeeper pop-up menu right now T_T
  List<(Colour, string)> ParsePopupLine(string line)
  {
    string txt = "";
    List<(Colour, string)> pieces = [];
    int a = 0, s = 0;
    while (a < line.Length)
    {
      if (line[a] == '[')
      {
        txt = line.Substring(s, a - s);
        if (txt.Length > 0)
          pieces.Add((Colours.WHITE, txt));

        s = a;
        while (line[a] != ' ')
          ++a;
        string colourText = line.Substring(s + 1, a - s - 1).ToLower();
        Colour colour = Colours.TextToColour(colourText);
        s = ++a;
        while (line[a] != ']')
          a++;
        txt = line[s..a];
        pieces.Add((colour, txt));
        s = a + 1;
      }
      ++a;

    }

    txt = line.Substring(s, a - s);
    if (txt.Length > 0)
      pieces.Add((Colours.WHITE, txt));

    return pieces;
  }

  static int PopupLineWidth(List<(Colour, string)> line) => line.Select(p => p.Item2.Length).Sum();

  static int WidestPopupLine(List<List<(Colour, string)>> lines)
  {
    int bufferWidth = 0;
    foreach (var line in lines)
    {
      int length = PopupLineWidth(line);
      if (length > bufferWidth)
        bufferWidth = length;
    }

    return (bufferWidth > 20 ? bufferWidth : 20) + 4;
  }

  protected void WritePopUp()
  {
    int maxPopUpWidth = ViewWidth - 4;
    var lines = _popupBuffer.Split('\n').Select(ParsePopupLine).ToList();
    int width = WidestPopupLine(lines);

    if (width >= maxPopUpWidth)
    {
      lines = ResizePopupLines(lines, maxPopUpWidth - 4);
      width = WidestPopupLine(lines);
    }

    int col = (ViewWidth - width) / 2;
    int row = 5;

    string border = "+".PadRight(width - 1, '-') + "+";

    if (_popupTitle.Length > 0)
    {
      int left = (width - _popupTitle.Length) / 2 - 2;
      string title = "+".PadRight(left, '-') + ' ';
      title += _popupTitle + ' ';
      title = title.PadRight(width - 1, '-') + "+";
      WriteLine(title, row++, col, width, Colours.WHITE);
    }
    else
    {
      WriteLine(border, row++, col, width, Colours.WHITE);
    }

    foreach (var line in lines)
    {
      List<(Colour, string)> lt = [(Colours.WHITE, "| ")];
      lt.AddRange(line);
      var padding = (Colours.WHITE, "".PadRight(width - PopupLineWidth(line) - 4));
      lt.Add(padding);
      lt.Add((Colours.WHITE, " |"));
      WriteText(lt, row++, col, width - 4);
    }
    WriteLine(border, row, col, width, Colours.WHITE);
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

  protected void WriteSideBar(GameState gs)
  {
    int row = 0;
    WriteLine($"| {gs.Player.Name}", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    int currHP = gs.Player.Stats[Attribute.HP].Curr;
    int maxHP = gs.Player.Stats[Attribute.HP].Max;
    WriteLine($"| HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
    WriteLine($"| AC: {gs.Player.AC}", row++, ViewWidth, SideBarWidth, Colours.WHITE);

    List<(Colour, string)> zorkmidLine = [(Colours.WHITE, "|  "), (Colours.YELLOW, "$"), (Colours.WHITE, $": {gs.Player.Inventory.Zorkmids}")];
    WriteText(zorkmidLine, row++, ViewWidth, SideBarWidth);

    string blank = "|".PadRight(ViewWidth);
    WriteLine(blank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

    var weapon = gs.Player.Inventory.ReadiedWeapon();
    if (weapon != null)
    {
      List<(Colour, string)> weaponLine = [(Colours.WHITE, "| "), (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString())];
      weaponLine.Add((Colours.WHITE, $" {weapon.FullName.IndefArticle()} (in hand)"));
      WriteText(weaponLine, row++, ViewWidth, SideBarWidth);
    }

    for (; row < ViewHeight - 1; row++)
    {
      WriteLine(blank, row, ViewWidth, SideBarWidth, Colours.WHITE);
    }

    // Write statuses
    int statusLineNum = ViewHeight - 3;
    HashSet<string> statuses = [];
    if (!statuses.Contains("POISONED") && gs.Player.HasTrait<PoisonedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.GREEN, "POISONED")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("POISONED");
    }
    if (!statuses.Contains("RAGE") && gs.Player.HasActiveTrait<RageTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "RAGE")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("RAGE");
    }
    if (!statuses.Contains("GRAPPLED") && gs.Player.HasActiveTrait<GrappledTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "GRAPPLED")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("GRAPPLED");
    }
    if (!statuses.Contains("PARALYZED") && gs.Player.HasActiveTrait<ParalyzedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.YELLOW, "PARALYZED")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("PARALYZED");
    }
    if (!statuses.Contains("CONFUSED") && gs.Player.HasActiveTrait<ConfusedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.YELLOW, "CONFUSED")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("CONFUSED");
    }
    if (gs.Player.HasActiveTrait<ExhaustedTrait>())
    {
      List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.PINK, "EXHAUSTED")];
      WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
      statuses.Add("EXHAUSTED");
    }
    foreach (StatBuffTrait statBuff in gs.Player.Traits.OfType<StatBuffTrait>())
    {
      if (!statuses.Contains("WEAKENED") && statBuff.Attr == Attribute.Strength && statBuff.Amt < 0)
      {
        List<(Colour, string)> statusLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "WEAKENED")];
        WriteText(statusLine, statusLineNum--, ViewWidth, SideBarWidth);
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
    WriteText(tileLine, ViewHeight - 2, ViewWidth, SideBarWidth);

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

    foreach (var line in MenuRows!)
    {
      WriteLine(" " + line, row++, col, width, Colours.WHITE);
    }
    WriteLine("", row, col, width, Colours.WHITE);
  }

  // TODO: DRY the two versions of AlertPlayer
  public void AlertPlayer(Message alert, string ifNotSeen, GameState gs)
  {            
    var textToShow = gs.LastPlayerFoV.Contains(alert.Loc) ? alert.Text : ifNotSeen;

    if (textToShow.Trim().Length == 0)
      return;

    HistoryUpdated = true;

    if (MessageHistory.Count > 0 && MessageHistory[0].Message == textToShow)
      MessageHistory[0] = new MsgHistory(textToShow, MessageHistory[0].Count + 1);
    else
      MessageHistory.Insert(0, new MsgHistory(textToShow, 1));

    if (MessageHistory.Count > MaxHistory)
      MessageHistory.RemoveAt(MaxHistory);
  }

  public void AlertPlayer(List<Message> alerts, string ifNotSeen, GameState gs)
  {
    // TODO: only display messages that are within the player's
    // current FOV
    if (alerts.Count == 0 && string.IsNullOrEmpty(ifNotSeen))
      return;

    // Eventually I need to handle sight vs sound messages better...
    // In the meantime, we have a few cases.
    List<string> msgs = [];
    foreach (var alert in alerts)
    {
      if (!gs.LastPlayerFoV.Contains(alert.Loc) && alert.Sound)
        msgs.Add(alert.Text);
      else if (gs.LastPlayerFoV.Contains(alert.Loc) && !alert.Sound)
        msgs.Add(alert.Text);
      else if (!gs.LastPlayerFoV.Contains(alert.Loc))
        msgs.Add(ifNotSeen);
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

  void TakeTurn(IPerformer performer, GameState gs)
  {
    var action = performer.TakeTurn(this, gs);

    if (action is NullAction)
    {
      // Player is idling
      return;
    }

    if (action is QuitAction)
    {
      // It feels maybe like overkill to use an exception here?
      throw new GameQuitException();
    }
    else if (action is SaveGameAction)
    {
      //Serialize.WriteSaveGame(Player.Name, Player, GameState.Campaign, GameState, MessageHistory);
      Serialize.WriteSaveGame(gs);
      throw new GameQuitException();
    }    
    else
    {
      ActionResult result;
      do
      {
        result = action!.Execute();
        performer.Energy -= result.EnergyCost;
        if (result.AltAction is not null)
        {
          if (result.Messages.Count > 0)
            AlertPlayer(result.Messages, result.MessageIfUnseen, gs);
          result = result.AltAction.Execute();
          performer.Energy -= result.EnergyCost;
          action = result.AltAction;
        }

        if (result.Messages.Count > 0)
          AlertPlayer(result.Messages, result.MessageIfUnseen, gs);

        gs.UpdateFoV();
      }
      while (result.AltAction is not null);
    }
  }

  public void GameLoop(GameState gameState)
  {
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
        InputBuffer.Enqueue(e.Value);

      try
      {
        // Update step! This is where all the current performers gets a chance
        // to take their turn!
        if (currPerformer.Energy < 1.0)
          currPerformer = gameState.NextPerformer();
        TakeTurn(currPerformer, gameState);
      }
      catch (GameQuitException)
      {
        break;
      }
      catch (PlayerKilledException)
      {
        break;
      }
      catch (VictoryException) 
      {
        break;
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

    var msg = new List<string>()
        {
            "",
            " Be seeing you..."
        };
    WriteLongMessage(msg);
    UpdateDisplay(gameState);
    BlockForInput();
  }

  static void Delay(int ms = 10) => Thread.Sleep(ms);

  void BlockForInput()
  {
    GameEvent e;
    do
    {
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

  public string BlockingGetResponse(string prompt)
  {
    string result = "";
    GameEvent e;

    do
    {
      Popup($"{prompt}\n{result}");
      UpdateDisplay(null);
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

      if (e.Value == '\n' || e.Value == 13)
        break;
      else if (e.Value == Constants.BACKSPACE)
        result = result.Length > 0 ? result[..^1] : "";
      else
        result += e.Value;
    }
    while (true);

    ClosePopup();

    return result.Trim();
  }

  static Sqr SqrToDisplay(GameState gs, Dictionary<Loc, Glyph> remembered, Loc loc)
  {
    static Colour BGColour(GameState gs, Loc loc, char ch)
    {
      if (gs.CurrDungeonID == 0)
        return Colours.BLACK;

      if (!gs.CurrentMap.HasEffect(TerrainFlag.Lit, loc.Row, loc.Col))
        return Colours.BLACK;

      if (ch == '.' || ch == '#')
        return Colours.TORCH_ORANGE;

      return Colours.BLACK;
    }

    Sqr sqr;
    if (gs.LastPlayerFoV.Contains(loc))
    {
      Glyph glyph;
      if (gs.ObjDb.Occupant(loc) is Actor actor)
        glyph = actor.Glyph;
      else
        glyph = remembered[loc];
      
      char ch = glyph.Ch;
      Colour bg = BGColour(gs, loc, ch);
      sqr = new Sqr(glyph.Lit, bg, ch);
    }
    else if (remembered.TryGetValue(loc, out var glyph))
    {
      sqr = new Sqr(glyph.Unlit, Colours.BLACK, glyph.Ch);
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
        var sqr = SqrToDisplay(gs, dungeon.RememberedLocs, loc);

        // The ZLayer trumps. Although maybe now that I've added a Z-coord
        // to items and actors I can get rid of the ZLayer?
        if (ZLayer[r, c].Type != TileType.Unknown)
        {
          var glyph = Util.TileToGlyph(ZLayer[r, c]);
           sqr = new Sqr(glyph.Lit, Colours.BLACK, glyph.Ch);
        }
         
        SqsOnScreen[r, c] = sqr;
      }
    }

    if (ZLayer[PlayerScreenRow, PlayerScreenCol].Type == TileType.Unknown)
      SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(Colours.WHITE, Colours.BLACK, '@');
  }

  public (int, int) LocToScrLoc(int row, int col, int playerRow, int playerCol)
  {
    int rowOffset = playerRow - PlayerScreenRow;
    int colOffset = playerCol - PlayerScreenCol;

    return (row - rowOffset, col - colOffset);
  }

  void ClearZLayer()
  {
    for (int r = 0; r < ViewHeight; r++)
    {
      for (int c = 0; c < ViewWidth; c++)
      {
        ZLayer[r, c] = TileFactory.Get(TileType.Unknown);
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
        sqs[r, c] = remembered.TryGetValue(loc, out var g) ? new Sqr(g.Unlit, Colours.BLACK, g.Ch) : blank;                      
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
}
