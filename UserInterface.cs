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

enum UIEventType { Quiting, KeyInput, NoEvent }
record struct UIEvent(UIEventType Type, char Value);
record Sqr(Colour Fg, Colour Bg, char Ch);

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

    public abstract void UpdateDisplay();
    protected abstract UIEvent PollForEvent();
    protected abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour);
    
    protected int FontSize;
    protected int PlayerScreenRow;
    protected int PlayerScreenCol;
    protected List<string>? _longMessage;   
    protected Options _options;
    bool _playing;

    public Player? Player { get; set; } = null;
    public Queue<char> InputBuffer = new Queue<char>();

    public GameState? GameState { get; private set; }
    public Random Rng { get; set; }

    public Sqr[,] SqsOnScreen;
    public Tile[,] ZLayer; // An extra layer of tiles to use for effects like clouds

    // It's convenient for other classes to ask what dungeon and level we're on
    public int CurrentDungeon => GameState is not null ? GameState.CurrDungeon : -1;
    public int CurrentLevel => GameState is not null ? GameState.CurrLevel : -1;

    protected List<string> MenuRows { get; set; } = [];

    protected bool ClosingPopUp { get; set; }
    protected bool OpeningPopUp { get; set; }
    protected string _popupBuffer = "";
    protected string _popupTitle = "";
    
    public List<MsgHistory> MessageHistory = [];
    protected readonly int MaxHistory = 50;
    protected bool HistoryUpdated = false;
    
    List<Animation> _animations = [];

    public UserInterface(Options opts, Random rng)
    {
        _options = opts;
        PlayerScreenRow = ViewHeight / 2;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
        ZLayer = new Tile[ViewHeight, ViewWidth];
        Rng = rng;
        ClearZLayer();

        _animations.Add(new CloudAnimationListener(this, Rng));
        _animations.Add(new TorchLightAnimationListener(this, Rng));
    }

    public virtual void TitleScreen()
    {
        _longMessage =
        [
            "",
            "",
            "",
            "",
            "     Welcome to Yarl2",
            "       (yet another attempt to make a roguelike,",
            "           this time in C#...)"
        ];
        
        UpdateDisplay();
        BlockForInput();
        ClearLongMessage();
    }

    public void ClearLongMessage()
    {
        _longMessage = null;
    }

    public void KillScreen(string message)
    {
        Popup(message);
        SetSqsOnScreen();
        UpdateDisplay();
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
                Colour colour = ColourSave.TextToColour(colourText);
                s = ++a;
                while (line[a] != ']')
                    a++;
                txt = line.Substring(s, a - s);
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

    int PopupLineWidth(List<(Colour, string)> line) => line.Select(p => p.Item2.Length).Sum();
    int WidestPopupLine(List<List<(Colour, string)>> lines)
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

    protected void WriteSideBar()
    {
        int row = 0;
        WriteLine($"| {Player!.Name}", row++, ViewWidth, SideBarWidth, Colours.WHITE);
        int currHP = Player.Stats[Attribute.HP].Curr;
        int maxHP = Player.Stats[Attribute.HP].Max;
        WriteLine($"| HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
        WriteLine($"| AC: {Player.AC}", row++, ViewWidth, SideBarWidth, Colours.WHITE);

        List<(Colour, string)> zorkmidLine = [ (Colours.WHITE, "|  "), (Colours.YELLOW, "$"), (Colours.WHITE, $": {Player.Inventory.Zorkmids}")];
        WriteText(zorkmidLine, row++, ViewWidth, SideBarWidth);

        string blank = "|".PadRight(ViewWidth);
        WriteLine(blank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

        var weapon = Player.Inventory.ReadiedWeapon();
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
        if (Player.Features.Any(f => f.Attribute == Attribute.Rage) && currHP < maxHP / 2)
        {
            List<(Colour, string)> rageLine = [(Colours.WHITE, "| "), (Colours.BRIGHT_RED, "RAGE")];
            WriteText(rageLine, ViewHeight - 3, ViewWidth, SideBarWidth);
        }

        var tile = GameState!.TileAt(Player.Loc);
        var tileSq = TileToSqr(tile, true);
        var tileText = Tile.TileDesc(tile.Type).Capitalize();
        foreach (var item in GameState.ObjDB.EnvironmentsAt(Player.Loc))
        {
            if (item.Type == ItemType.Environment)
            {
                var g = item.Glyph;
                tileSq = new Sqr(g.Lit, Colours.BLACK, g.Ch);
                tileText = $"Some {item.Name}";
                break;
            }
        }
        
        List<(Colour, string)> tileLine = [(Colours.WHITE, "| "), (tileSq.Fg, $"{tileSq.Ch} "), (Colours.WHITE, tileText)];
        WriteText(tileLine, ViewHeight - 2, ViewWidth, SideBarWidth);

        if (GameState.CurrDungeon == 0) 
        {
            var time = GameState.CurrTime();
            var mins = time.Item2.ToString().PadLeft(2, '0');
            WriteLine($"| Outside {time.Item1}:{mins}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
        }
        else 
        {
            WriteLine($"| Depth: {GameState.CurrLevel + 1}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
        }

        //WriteLine($"| Turn: {GameState.Turn}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
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

    public void AlertPlayer(List<Message> alerts, string ifNotSeen) 
    {
        // TODO: only display messages that are within the player's
        // current FOV
        if (alerts.Count == 0 && string.IsNullOrEmpty(ifNotSeen))
            return;

        // Eventually I need to handle sight vs sound messages better...
        // In the meantime, we have a few cases.
        List<string> msgs = [];
        foreach (var alert in alerts) {
            if (!GameState.RecentlySeen.Contains(alert.Loc) && alert.Sound)
                msgs.Add(alert.Text);
            else if (GameState.RecentlySeen.Contains(alert.Loc) && !alert.Sound)
                msgs.Add(alert.Text);
            else if (!GameState.RecentlySeen.Contains(alert.Loc))
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

    // I dunno about having this here. In previous games, I had each Tile object
    // know what its colours were, but maybe the UI class *is* the correct spot
    // to decide how to draw the glyph
    protected static Sqr TileToSqr(Tile tile, bool lit)
    {
        switch (tile.Type)
        {
            case TileType.DungeonWall:
            case TileType.PermWall:
                return lit ? new Sqr(Colours.GREY, Colours.TORCH_ORANGE, '#') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '#');
            case TileType.StoneWall:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '#') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '#');
            case TileType.DungeonFloor:
                return lit ? new Sqr(Colours.YELLOW, Colours.TORCH_ORANGE, '.') : new Sqr(Colours.GREY, Colours.BLACK, '.');
            case TileType.StoneFloor:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '.') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '.');
            case TileType.StoneRoad:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '\'') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '\'');
            case TileType.ClosedDoor:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '+') : new Sqr(Colours.BROWN, Colours.BLACK, '+');
            case TileType.OpenDoor:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '\\') : new Sqr(Colours.BROWN, Colours.BLACK, '\\');
            case TileType.Water:
            case TileType.DeepWater:
                return lit ? new Sqr(Colours.BLUE, Colours.BLACK, '}') : new Sqr(Colours.DARK_BLUE, Colours.BLACK, '}');
            case TileType.Sand:
                return lit ? new Sqr(Colours.YELLOW, Colours.BLACK, '.') : new Sqr(Colours.YELLOW_ORANGE, Colours.BLACK, '.');
            case TileType.Grass:
                return lit ? new Sqr(Colours.GREEN, Colours.BLACK, '.') : new Sqr(Colours.DARK_GREEN, Colours.BLACK, '.');
            case TileType.Tree:
                return lit ? new Sqr(Colours.GREEN, Colours.BLACK, 'ϙ') : new Sqr(Colours.DARK_GREEN, Colours.BLACK, 'ϙ');
            case TileType.Mountain:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '\u039B') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '\u039B');
            case TileType.SnowPeak:
                return lit ? new Sqr(Colours.WHITE, Colours.BLACK, '\u039B') : new Sqr(Colours.GREY, Colours.BLACK, '\u039B');
            case TileType.Portal:
                return lit ? new Sqr(Colours.WHITE, Colours.BLACK, 'Ո') : new Sqr(Colours.GREY, Colours.BLACK, 'Ո');
            case TileType.Downstairs:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '>') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '>');
            case TileType.Upstairs:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '<') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '<');
            case TileType.Cloud:
                return lit ? new Sqr(Colours.WHITE, Colours.BLACK, '#') : new Sqr(Colours.WHITE, Colours.BLACK, '#');
            case TileType.WoodFloor:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '.') : new Sqr(Colours.BROWN, Colours.BLACK, '.');
            case TileType.WoodWall:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '#') : new Sqr(Colours.BROWN, Colours.BLACK, '#');
            case TileType.HWindow:
                return lit ? new Sqr(Colours.LIGHT_GREY, Colours.BLACK, '-') : new Sqr(Colours.GREY, Colours.BLACK, '-');
            case TileType.VWindow:
                return lit ? new Sqr(Colours.LIGHT_GREY, Colours.BLACK, '|') : new Sqr(Colours.GREY, Colours.BLACK, '|');
            case TileType.Forge:
                return lit ? new Sqr(Colours.BRIGHT_RED, Colours.TORCH_ORANGE, '^') : new Sqr(Colours.DULL_RED, Colours.BLACK, '^');
            case TileType.Dirt:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '.') : new Sqr(Colours.BROWN, Colours.BLACK, '.');
            case TileType.Well:
                return lit ? new Sqr(Colours.LIGHT_GREY, Colours.BLACK, 'o') : new Sqr(Colours.GREY, Colours.BLACK, 'o');
            case TileType.Bridge:
                return lit ? new Sqr(Colours.GREY, Colours.BLACK, '=') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '=');
            case TileType.WoodBridge:
                return lit ? new Sqr(Colours.LIGHT_BROWN, Colours.BLACK, '=') : new Sqr(Colours.BROWN, Colours.BLACK, '=');
            case TileType.Statue:
                return lit ? new Sqr(Colours.LIGHT_GREY, Colours.BLACK, '&') : new Sqr(Colours.GREY, Colours.BLACK, '&');
            case TileType.Landmark:
                return lit ? new Sqr(Colours.YELLOW, Colours.TORCH_ORANGE, '_') : new Sqr(Colours.GREY, Colours.BLACK, '_');
            case TileType.Chasm:
                return lit ? new Sqr(Colours.DARK_GREY, Colours.BLACK, '\u2237') : new Sqr(Colours.DARK_GREY, Colours.BLACK, '\u2237');
            default:
                return new Sqr(Colours.BLACK, Colours.BLACK, ' ');
        }        
    }

    public void SetupGameState(Campaign campaign, GameObjectDB itemDB, ulong currentTurn)
    {
        GameState = new GameState(Player, campaign, _options, this)
        {
            Map = campaign!.Dungeons[campaign.CurrentDungeon].LevelMaps[campaign.CurrentLevel],
            CurrLevel = campaign.CurrentLevel,
            CurrDungeon = campaign.CurrentDungeon,
            ObjDB = itemDB,
            Turn = currentTurn
        };

        itemDB.SetToLoc(Player.Loc, Player);
        GameState.ToggleEffect(Player, Player.Loc, TerrainFlag.Lit, true);
    }

    bool TakeTurn(IPerformer performer)
    {
        var action = performer.TakeTurn(this, GameState);

        if (action is NullAction)
        {
            // Player is idling
            return false;
        }

        if (action is QuitAction)
        {
            // It feels maybe like overkill to use an exception here?
            throw new GameQuitException();
        }
        else if (action is SaveGameAction)
        {
            Serialize.WriteSaveGame(Player.Name, Player, GameState.Campaign, GameState, MessageHistory);
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
                        AlertPlayer(result.Messages, result.MessageIfUnseen);
                    result = result.AltAction.Execute();
                    performer.Energy -= result.EnergyCost;
                }

                if (result.Messages.Count > 0)
                    AlertPlayer(result.Messages, result.MessageIfUnseen);

                if (result.PlayerKilled)
                {
                     KillScreen("You died :(");
                     throw new GameQuitException();
                }
                    
            }
            while (result.AltAction is not null);
        }

        return true;
    }

    public void GameLoop()
    {                
        GameState.BuildPerformersList();

        _playing = true;
        int p = 0;
        DateTime refresh = DateTime.Now;
        while (_playing) 
        {
            var e = PollForEvent();
            if (e.Type == UIEventType.Quiting)
                break;

            if (e.Type == UIEventType.KeyInput)
                InputBuffer.Enqueue(e.Value);

            try
            {
                // Update step! This is where all the current performers gets a chance
                // to take their turn!
                IPerformer performer = GameState.NextPerformer();
                TakeTurn(performer);
            }
            catch (GameQuitException)
            {
                break;                
            }
            
            var elapsed = DateTime.Now - refresh;
            if (elapsed.TotalMilliseconds > 60)
            {
                SetSqsOnScreen();

                foreach (var l in _animations)
                   l.Update();
                _animations = _animations.Where(a => a.Expiry > DateTime.Now)
                                         .ToList();
                UpdateDisplay();
                refresh = DateTime.Now;
            }

            Delay();
        }

        var msg = new List<string>()
        {
            "",
            " Be seeing you..."
        };
        WriteLongMessage(msg);
        UpdateDisplay();
        BlockForInput();
    }

    static void Delay() => Thread.Sleep(10);

    void BlockForInput()
    {
        UIEvent e;
        do
        {
            e = PollForEvent();
            Delay();
        }
        while (e.Type == UIEventType.NoEvent);
    }

    public char FullScreenMenu(List<string> menu, HashSet<char> options)
    {
        UIEvent e;

        do
        {
            WriteLongMessage(menu);
            UpdateDisplay();
            e = PollForEvent();

            if (e.Type == UIEventType.NoEvent)
            {
                Delay();
                continue;
            }
            else if (e.Value == Constants.ESC || e.Type == UIEventType.Quiting)
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
        UIEvent e;

        do
        {
            Popup($"{prompt}\n{result}");
            UpdateDisplay();
            e = PollForEvent();

            if (e.Type == UIEventType.NoEvent)
            {
                Delay();
                continue;
            }
            else if (e.Value == Constants.ESC || e.Type == UIEventType.Quiting)
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

    Sqr CalcSqrAtLoc(HashSet<(int, int)> visible, Dictionary<(int, int, int), Sqr> remembered, Map map,
                int mapRow, int mapCol, int scrRow, int scrCol)
    {
        var loc = new Loc(GameState.CurrDungeon, GameState.CurrLevel, mapRow, mapCol);
       
        // Okay, squares have to be lit and within visible radius to be seen and a visible, lit Z-Layer tile trumps
        // For a square within visible that isn't lit, return remembered or Unknown
        bool isVisible = visible.Contains((mapRow, mapCol));

        if (!isVisible || !map.HasEffect(TerrainFlag.Lit, mapRow, mapCol))
        {
            if (remembered.TryGetValue((GameState.CurrLevel, mapRow, mapCol), out var remSq))
                return remSq;
            else
                return new Sqr(Colours.BLACK, Colours.BLACK, ' ');;
        }

        Tile tile = map.TileAt(mapRow, mapCol);
        // First, check if we have a chasm square, in which case we'll look up info
        // from the level below
        Sqr? sqBelow = null;
        if (tile.Type == TileType.Chasm)
        {
            Loc below = loc with { Level = GameState.CurrLevel + 1 };
            Glyph glyphBelow = GameState.ObjDB.GlyphAt(below);
            char ch;
            if (glyphBelow != GameObjectDB.EMPTY)
            {
                ch = glyphBelow.Ch;
            }
            else
            {
                var belowTile = TileToSqr(GameState.CurrentDungeon.LevelMaps[GameState.CurrLevel + 1].TileAt(mapRow, mapCol), false);
                ch = belowTile.Ch;
            }
            sqBelow = new Sqr(Colours.FAR_BELOW, Colours.BLACK, ch);
        }

        //Sqr memory = TileToSqr(tile, false);
        GameState.RecentlySeen.Add(loc);

        // The ZLayer trumps. Although maybe now that I've added a Z-coord
        // to items and actors I can get rid of the ZLayer?
        if (ZLayer[scrRow, scrCol].Type != TileType.Unknown)        
            return TileToSqr(ZLayer[scrRow, scrCol], true);
        
         var (glyph, z, item) = GameState.ObjDB.TopGlyph(loc);
        // For a chasm sq, return the tile from the level below,
        // unless there's an Actor on this level (such as a flying
        // creature)
        if (sqBelow != null && glyph == GameObjectDB.EMPTY)
            return sqBelow;
        
        Sqr memory = TileToSqr(tile, false);
        Sqr sqr; 
        if (z > tile.Z())
        {            
            sqr = new Sqr(glyph.Lit, Colours.BLACK, glyph.Ch);
            if (item)
                memory = sqr with { Fg = glyph.Unlit};
        }
        else
        {
            sqr = TileToSqr(tile, true);
        }

        remembered[(GameState.CurrLevel, mapRow, mapCol)] = memory;

        return sqr;
    }

    public (int, int) LocToScrLoc(int row, int col)
    {
        int rowOffset = Player.Loc.Row - PlayerScreenRow;
        int colOffset = Player.Loc.Col - PlayerScreenCol;

        return (row - rowOffset, col - colOffset);
    }
    
    void SetSqsOnScreen()
    {
        var cmpg = GameState!.Campaign;
        var dungeon = cmpg!.Dungeons[GameState.CurrDungeon];
        var map = dungeon.LevelMaps[GameState.CurrLevel];
        GameState.Map = map;
        var vs = FieldOfView.CalcVisible(Player!.MaxVisionRadius, Player!.Loc.Row, Player!.Loc.Col, map, GameState.CurrDungeon, GameState.CurrLevel, GameState.ObjDB);        
        var visible = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();

        GameState.RecentlySeen = [];

        // There is a glitch here that I don't want to fix right now in that
        // I am remembering only (row, col). So if a monster picks up an item
        // out of the player's FOV, the remembered square will then show the map
        // tile not the remembered item. I need to store a dictionary of loc + glyph
        // Or perhaps it just needs to be a collection of items + non-basic tiles not
        // every tile
        var rememberd = dungeon.RememberedSqs;
       
        int rowOffset = Player.Loc.Row - PlayerScreenRow;
        int colOffset = Player.Loc.Col - PlayerScreenCol;
                
        for (int r = 0; r < ViewHeight; r++) 
        {
            for (int c = 0; c < ViewWidth; c++)
            {
                // replace w/ LocToScrLoc?
                int mapRow = r + rowOffset;
                int mapCol = c + colOffset;
                SqsOnScreen[r, c] = CalcSqrAtLoc(visible, rememberd, map, mapRow, mapCol, r, c);                
            }
        }

        if (ZLayer[PlayerScreenRow, PlayerScreenCol].Type == TileType.Unknown)
            SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(Colours.WHITE, Colours.BLACK, '@');
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
}
