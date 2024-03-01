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
    private bool _playing;

    public Player? Player { get; set; } = null;
    public Queue<char> InputBuffer = new Queue<char>();

    protected GameState? GameState { get; set; } = null;
    public Random Rng { get; set; }

    public Sqr[,] SqsOnScreen;
    public Tile[,] ZLayer; // An extra layer of tiles to use for effects like clouds

    // It's convenient for other classes to ask what dungeon and level we're on
    public int CurrentDungeon => GameState is not null ? GameState.CurrDungeon : -1;
    public int CurrentLevel => GameState is not null ? GameState.CurrLevel : -1;

    protected bool ClosingMenu { get; set; }
    protected bool OpeningMenu { get; set; }
    protected List<string>? MenuRows { get; set; }

    protected bool ClosingPopUp { get; set; }
    protected bool OpeningPopUp { get; set; }
    protected string? _popupBuffer = "";
    protected string _popupTitle = "";
    
    public List<MsgHistory> MessageHistory = [];
    protected readonly int MaxHistory = 50;
    protected bool HistoryUpdated = false;
    
    public UserInterface(Options opts, Random rng)
    {
        _options = opts;
        PlayerScreenRow = ViewHeight / 2;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
        ZLayer = new Tile[ViewHeight, ViewWidth];
        Rng = rng;
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
        _popupBuffer = null;
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

    // This is going to look ugly if a message contains a long line
    // followed by a line break then short line but I don't know
    // if I'm ever going to need to worry about that in my game.
    private string[] ResizePopupLines(string[] lines, int maxWidth)
    {
        List<string> resized = [];

        foreach (var line in lines)
        {
            if (line.Length < maxWidth)
                resized.Add(line);
            else
            {
                var sb = new StringBuilder();
                foreach (var word in line.Split(' '))
                {
                    if (sb.Length + word.Length < maxWidth) 
                    {
                        sb.Append(word);
                        sb.Append(' ');
                    }
                    else
                    {
                        resized.Add(sb.ToString().TrimEnd());
                        sb = new StringBuilder(word);
                        sb.Append(' ');
                    }
                }
                if (sb.Length > 0)
                    resized.Add(sb.ToString().TrimEnd());
            }
        }

        return resized.ToArray();
    }

    protected void WritePopUp()
    {
        int maxPopUpWidth = ViewWidth - 4;
        var lines = _popupBuffer.Split('\n');
        int bufferWidth = lines.Select(l => l.Length).Max();
        int width = bufferWidth > 20 ? bufferWidth : 20;
        width += 4;

        if (width >= maxPopUpWidth)
        {
            lines = ResizePopupLines(lines, maxPopUpWidth - 4);
            _popupBuffer = string.Join('\n', lines);
            width = lines.Select(l => l.Length).Max() + 4;
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
            WriteLine(("| " + line).PadRight(width - 2) + " |", row++, col, width, Colours.WHITE);
        }
        WriteLine(border, row, col, width, Colours.WHITE);
    }

    protected void WriteLine(List<(Colour, string)> pieces, int lineNum, int col, int width)
    {
        foreach (var piece in pieces)
        {
            WriteLine(piece.Item2, lineNum, col, width, piece.Item1);
            col += piece.Item2.Length;
        }
    }

    protected void WriteSideBar()
    {
        int row = 0;
        WriteLine($"| {Player.Name}", row++, ViewWidth, SideBarWidth, Colours.WHITE);
        int currHP = Player.Stats[Attribute.HP].Curr;
        int maxHP = Player.Stats[Attribute.HP].Max;
        WriteLine($"| HP: {currHP} ({maxHP})", row++, ViewWidth, SideBarWidth, Colours.WHITE);
        WriteLine($"| AC: {Player.AC}", row++, ViewWidth, SideBarWidth, Colours.WHITE);

        List<(Colour, string)> zorkmidLine = [ (Colours.WHITE, "|  "), (Colours.YELLOW, "$"), (Colours.WHITE, $": {Player.Inventory.Zorkmids}")];
        WriteLine(zorkmidLine, row++, ViewWidth, SideBarWidth);

        string blank = "|".PadRight(ViewWidth);
        WriteLine(blank, row++, ViewWidth, SideBarWidth, Colours.WHITE);

        var weapon = Player.Inventory.ReadiedWeapon();
        if (weapon != null) 
        {
            List<(Colour, string)> weaponLine = [(Colours.WHITE, "| "), (weapon.Glyph.Lit, weapon.Glyph.Ch.ToString())];
            weaponLine.Add((Colours.WHITE, $" {weapon.FullName.IndefArticle()} (in hand)"));                
            WriteLine(weaponLine, row++, ViewWidth, SideBarWidth);
        }

        for (; row < ViewHeight - 2; row++)
        {
            WriteLine(blank, row, ViewWidth, SideBarWidth, Colours.WHITE);
        }

        if (GameState.CurrDungeon == 0)
            WriteLine("| Outside", ViewHeight - 2, ViewWidth, SideBarWidth, Colours.WHITE);
        else
            WriteLine($"| Depth: {GameState.CurrLevel + 1}", ViewHeight - 2, ViewWidth, SideBarWidth, Colours.WHITE);
        WriteLine($"| Turn: {GameState.Turn}", ViewHeight - 1, ViewWidth, SideBarWidth, Colours.WHITE);
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

    public void AlertPlayer(Message alert, string ifNotSeen) 
    {
        // TODO: only display messages that are within the player's
        // current FOV
        if (string.IsNullOrEmpty(alert.Text))
            return;

        HistoryUpdated = true;

        string msgText;
        if (GameState.RecentlySeen.Contains(alert.Loc))
            msgText = alert.Text;
        else
            msgText = ifNotSeen;

        if (MessageHistory.Count > 0 && MessageHistory[0].Message == msgText)
            MessageHistory[0] = new MsgHistory(msgText, MessageHistory[0].Count + 1);
        else
            MessageHistory.Insert(0, new MsgHistory(msgText, 1));
        
        if (MessageHistory.Count > MaxHistory)
            MessageHistory.RemoveAt(MaxHistory);
    }

    public void WriteLongMessage(List<string> message)
    {
        _longMessage = message;
    }

    public void ShowDropDown(List<string> lines)
    {
        OpeningMenu = true;
        MenuRows = lines;
    }

    public virtual void CloseMenu() => MenuRows = null;

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
            case TileType.Statue:
                return lit ? new Sqr(Colours.LIGHT_GREY, Colours.BLACK, '&') : new Sqr(Colours.GREY, Colours.BLACK, '&');
            case TileType.Landmark:
                return lit ? new Sqr(Colours.YELLOW, Colours.TORCH_ORANGE, '_') : new Sqr(Colours.GREY, Colours.BLACK, '_');
            default:
                return new Sqr(Colours.BLACK, Colours.BLACK, ' ');
        }        
    }

    public void SetupGameState(Campaign campaign, GameObjectDB itemDB, int currentTurn)
    {
        GameState = new GameState(Player, campaign, _options)
        {
            Map = campaign!.Dungeons[campaign.CurrentDungeon].LevelMaps[campaign.CurrentLevel],
            CurrLevel = campaign.CurrentLevel,
            CurrDungeon = campaign.CurrentDungeon,
            ObjDB = itemDB,
            Turn = currentTurn
        };

        itemDB.SetToLoc(Player.Loc, Player);
        GameState.ToggleEffect(Player, Player.Loc, TerrainFlags.Lit, true);
    }

    private bool TakeTurn(IPerformer performer)
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
                    result = result.AltAction.Execute();
                    performer.Energy -= result.EnergyCost;
                }

                if (result.Message is not null)
                    AlertPlayer(result.Message, result.MessageIfUnseen);

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
        List<IAnimationListener> animationListeners = [];
        animationListeners.Add(new CloudAnimationListener(this, Rng));
        animationListeners.Add(new TorchLightAnimationListener(this, Rng));

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

                foreach (var l in animationListeners)
                   l.Update();

                UpdateDisplay();
                refresh = DateTime.Now;
            }

            
            Delay();
            
            //Console.WriteLine($"{elapsed.TotalMilliseconds} ms");            
        }

        var msg = new List<string>()
        {
            "",
            " Being seeing you..."
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
        var glyph = GameState.ObjDB.GlyphAt(loc);
                
        // Okay, squares have to be lit and within visible radius to be seen and a visible, lit Z-Layer tile trumps
        // For a square within visible that isn't lit, return remembered or Unknown
        bool isVisible = visible.Contains((mapRow, mapCol));
        if (isVisible && map.HasEffect(TerrainFlags.Lit, mapRow, mapCol))
        {
            Tile tile = map.TileAt(mapRow, mapCol);
            Sqr memory = TileToSqr(tile, false);

            GameState.RecentlySeen.Add(loc);

            // Remember the unlit version of the sqr since it's displayed in the
            // unlit portion of the view.
            if (ZLayer[scrRow, scrCol].Type != TileType.Unknown) 
            {
                return TileToSqr(ZLayer[scrRow, scrCol], true);
            }
            else if (glyph != GameObjectDB.EMPTY) 
            {
                var sqr = new Sqr(glyph.Lit, Colours.BLACK, glyph.Ch);

                var item = GameState.ObjDB.ItemGlyphAt(loc);
                if (item != GameObjectDB.EMPTY)
                    memory = sqr with { Fg = item.Unlit };
                remembered[(GameState.CurrLevel, mapRow, mapCol)] = memory;

                return sqr;
            }
            else 
            {
                remembered[(GameState.CurrLevel, mapRow, mapCol)] = TileToSqr(tile, false);
                return TileToSqr(tile, true);
            }
        }
        else if (remembered.TryGetValue((GameState.CurrLevel, mapRow, mapCol), out var remSq))
        {
            return remSq;
        }
                        
        return new Sqr(Colours.BLACK, Colours.BLACK, ' ');
    }

    void SetSqsOnScreen()
    {
        var cmpg = GameState!.Campaign;
        var dungeon = cmpg!.Dungeons[GameState.CurrDungeon];
        var map = dungeon.LevelMaps[GameState.CurrLevel];
        GameState.Map = map;
        var vs = FieldOfView.CalcVisible(Player!.MaxVisionRadius, Player!.Loc.Row, Player!.Loc.Col, map, GameState.CurrLevel);        
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
                int mapRow = r + rowOffset;
                int mapCol = c + colOffset;
                SqsOnScreen[r, c] = CalcSqrAtLoc(visible, rememberd, map, mapRow, mapCol, r, c);                
            }
        }

        if (ZLayer[PlayerScreenRow, PlayerScreenCol].Type == TileType.Unknown)
            SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = new Sqr(Colours.WHITE, Colours.BLACK, '@');
    }

    private void ClearZLayer()
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
