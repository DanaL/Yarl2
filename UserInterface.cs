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

namespace Yarl2;

enum UIEventType { Quiting, KeyInput, NoEvent }
record struct UIEvent(UIEventType Type, char Value);

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
    public const int ScreenWidth = 70;
    public const int ScreenHeight = 32;
    public const int SideBarWidth = 20;
    public const int ViewWidth = ScreenWidth - SideBarWidth;
    public const int ViewHeight = ScreenHeight - 5;

    public abstract void UpdateDisplay();
    protected abstract UIEvent PollForEvent();
    
    protected int FontSize;
    protected int PlayerScreenRow;
    protected int PlayerScreenCol;
    protected List<string>? _longMessage;
    protected string _messageBuffer = "";    
    protected Options _options;
    private bool _playing;

    public Player? Player { get; set; } = null;
    public Queue<char> InputBuffer = new Queue<char>();

    protected GameState? GameState { get; set; } = null;

    public (Colour, char)[,] SqsOnScreen;
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
    protected int _popupWidth;

    public List<MsgHistory> MessageHistory = [];
    protected readonly int MaxHistory = 50;
    protected bool HistoryUpdated = false;
    
    public UserInterface(Options opts)
    {
        _options = opts;
        PlayerScreenRow = ViewHeight / 2;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        SqsOnScreen = new (Colour, char)[ViewHeight, ViewWidth];
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

    public void ClosePopup()
    {
        _popupBuffer = null;
        ClosingPopUp = true;
    }

    public void Popup(string message, int width = 0)
    {
        _popupBuffer = message;
        _popupWidth = width;
        OpeningPopUp = true;
    }

    public void AlertPlayer(string message) 
    {
        message = message.Trim();
        if (string.IsNullOrEmpty(message))
            return;

        HistoryUpdated = true;
        
        if (MessageHistory.Count > 0 && MessageHistory[0].Message == message)
            MessageHistory[0] = new MsgHistory(message, MessageHistory[0].Count + 1);
        else
            MessageHistory.Insert(0, new MsgHistory(message, 1));
        
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
    protected static (Colour, char) TileToGlyph(Tile tile, bool lit)
    {
        switch (tile.Type)
        {
            case TileType.Wall:
            case TileType.PermWall:
                return lit ? (Colours.GREY, '#') : (Colours.DARK_GREY, '#');
            case TileType.Floor:
                return lit ? (Colours.YELLOW, '.') : (Colours.GREY, '.');
            case TileType.Door:
                char ch = ((Door)tile).Open ? '\\' : '+';
                return lit ? (Colours.LIGHT_BROWN, ch) : (Colours.BROWN, ch);
            case TileType.Water:
            case TileType.DeepWater:
                return lit ? (Colours.BLUE, '}') : (Colours.DARK_BLUE, '}');
            case TileType.Sand:
                return lit ? (Colours.YELLOW, '.') : (Colours.YELLOW_ORANGE, '.');
            case TileType.Grass:
                return lit ? (Colours.GREEN, '.') : (Colours.DARK_GREEN, '.');
            case TileType.Tree:
                return lit ? (Colours.GREEN, 'ϙ') : (Colours.DARK_GREEN, 'ϙ');
            case TileType.Mountain:
                return lit ? (Colours.GREY, '\u039B') : (Colours.DARK_GREY, '\u039B');
            case TileType.SnowPeak:
                return lit ? (Colours.WHITE, '\u039B') : (Colours.GREY, '\u039B');
            case TileType.Portal:
                return lit ? (Colours.WHITE, 'Ո') : (Colours.GREY, 'Ո');
            case TileType.Downstairs:
                return lit ? (Colours.GREY, '>') : (Colours.DARK_GREY, '>');
            case TileType.Upstairs:
                return lit ? (Colours.GREY, '<') : (Colours.DARK_GREY, '<');
            case TileType.Cloud:
                return lit ? (Colours.WHITE, '#') : (Colours.WHITE, '#');
            default:
                return (Colours.BLACK, ' ');
        }        
    }

    public void SetupGameState(Campaign campaign, GameObjectDB itemDB)
    {
        GameState = new GameState()
        {
            Map = campaign!.Dungeons[0].LevelMaps[campaign.CurrentLevel],
            Options = _options,
            Player = Player,
            Campaign = campaign,
            CurrLevel = campaign.CurrentLevel,
            CurrDungeon = campaign.CurrentDungeon,
            ObjDB = itemDB
        };

        var playerLoc = new Loc(campaign.CurrentDungeon, campaign.CurrentDungeon, Player.Row, Player.Col);
        GameState.ToggleEffect(Player, playerLoc, TerrainFlags.Lit, true);
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
            Serialize.WriteSaveGame(Player.Name, Player, GameState.Campaign, GameState);
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
                    result = result.AltAction.Execute();
                if (result.Message is not null)
                    AlertPlayer(result.Message);
            }
            while (result.AltAction is not null);
        }

        return true;
    }

    public void GameLoop()
    {        
        List<IAnimationListener> animationListeners = [];
        animationListeners.Add(new CloudAnimationListener(this));
     
        DateTime lastPollTime = DateTime.Now;

        GameState.RefreshPerformers();

        _playing = true;
        int p = 0;
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
                //while (performers[p].Energy >= 0.9999 && TakeTurn(performers[p])
                if (GameState.CurrPerformers[p].Energy < 1.0)
                {
                    GameState.CurrPerformers[p].Energy += GameState.CurrPerformers[p].Recovery;
                    p = (p + 1) % GameState.CurrPerformers.Count;
                }
                else if (TakeTurn(GameState.CurrPerformers[p]))
                {                    
                    if (GameState.CurrPerformers[p] != Player)
                    {
                        // I dunno if this is necessary
                        //SetSqsOnScreen();
                        //UpdateDisplay();
                        //Thread.Sleep(25);
                    }

                    if (GameState.CurrPerformers[p].Energy < 1.0)
                        p = (p + 1) % GameState.CurrPerformers.Count;
                }                    
                // I imagine later on there'll be bookkeeping and such once we've run
                // through all the current performers?
            }
            catch (GameQuitException)
            {
                break;                
            }
                                   
            SetSqsOnScreen();

            foreach (var l in animationListeners)
                l.Update();

            UpdateDisplay();

            var dd = DateTime.Now - lastPollTime;
            if (dd.TotalSeconds > 5) 
            {
                Console.WriteLine("hello, world?");
                lastPollTime = DateTime.Now;                
            }

            Delay();
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

    static void Delay() => Thread.Sleep(30);

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

    public string BlockingGetResponse(string prompt)
    {
        string result = "";
        UIEvent e;

        do
        {
            Popup($"{prompt}\n{result}", 20);
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

    (Colour, char) CalcGlyphAtLoc(HashSet<(int, int)> visible, HashSet<(int, int)> remembered, Map map,
                int mapRow, int mapCol, int scrRow, int scrCol)
    {
        var loc = new Loc(GameState.CurrDungeon, GameState.CurrLevel, mapRow, mapCol);
        var glyph = GameState.ObjDB.GlyphAt(loc);

        // Working toward having a difference between tiles that are lit and tiles within the player's vision radius
        // So player could be at one location with VR 3 and see the aura of a torch 8 squares away but not the sqs
        // in between because they are not lit
        // So it's going to become a combo of (1) squares within vision radius and currently lit squares

        // Okay, squares have to be lit and within visible radius to be seen and a visible, lit Z-Layer tile trumps
        // For a square within visible that isn't lit, return remembered or Unknown
        bool isVisible = visible.Contains((mapRow, mapCol));
        if (isVisible && map.HasEffect(TerrainFlags.Lit, mapRow, mapCol))
        {
            if (ZLayer[scrRow, scrCol].Type != TileType.Unknown)
                return TileToGlyph(ZLayer[scrRow, scrCol], true);
            else if (glyph != GameObjectDB.EMPTY)
                return (glyph.Lit, glyph.Ch);
            else
                return TileToGlyph(map.TileAt(mapRow, mapCol), true);
        }
        else if (remembered.Contains((mapRow, mapCol)))
        {
            return TileToGlyph(map.TileAt(mapRow, mapCol), false);
        }
                        
        return (Colours.BLACK, ' ');
    }

    void SetSqsOnScreen()
    {
        var cmpg = GameState!.Campaign;
        var dungeon = cmpg!.Dungeons[GameState.CurrDungeon];
        var map = dungeon.LevelMaps[GameState.CurrLevel];
        GameState.Map = map;
        var vs = FieldOfView.CalcVisible(Player!.MaxVisionRadius, Player!.Row, Player!.Col, map, GameState.CurrLevel);
        var visible = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();

        // There is a glitch here that I don't want to fix right now in that
        // I am remembering only (row, col). So if a monster picks up an item
        // out of the player's FOV, the remembered square will then show the map
        // tile not the remembered item. I need to store a dictionary of loc + glyph
        // Or perhaps it just needs to be a collection of items + non-basic tiles not
        // every tile
        var rememberd = dungeon.RememberedSqs.Select(rm => (rm.Item2, rm.Item3)).ToHashSet();
       
        int rowOffset = Player.Row - PlayerScreenRow;
        int colOffset = Player.Col - PlayerScreenCol;
                
        for (int r = 0; r < ViewHeight; r++) 
        {
            for (int c = 0; c < ViewWidth; c++)
            {
                int mapRow = r + rowOffset;
                int mapCol = c + colOffset;
                SqsOnScreen[r, c] = CalcGlyphAtLoc(visible, rememberd, map, mapRow, mapCol, r, c);                
            }
        }

        if (ZLayer[PlayerScreenRow, PlayerScreenCol].Type == TileType.Unknown)
            SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = (Colours.WHITE, '@');
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
