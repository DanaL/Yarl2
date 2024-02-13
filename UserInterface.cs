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
//using System.Data;

using System.Collections.Generic;

namespace Yarl2;

enum UIEventType { Quiting, KeyInput, NoEvent }
internal record struct UIEvent(UIEventType Type, char Value);

// I think that the way development is proceeding, it's soon not going
// to make sense for SDLUserInterface and BLUserInterface to be subclasses
// of UserInterface. It's more like they are being relegated to primive 
// display terminals and I'm pull more logic up into the base class, so
// I'll probably move towards Composition instead of Inheritance
internal abstract class UserInterface
{    
    public const int ScreenWidth = 65;
    public const int ScreenHeight = 30;
    public const int SideBarWidth = 20;
    public const int ViewWidth = ScreenWidth - SideBarWidth;
    
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

    public UserInterface(Options opts)
    {
        _options = opts;
        PlayerScreenRow = (ScreenHeight - 1) / 2 + 1;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        SqsOnScreen = new (Colour, char)[ScreenHeight - 1, ViewWidth];
        ZLayer = new Tile[ScreenHeight - 1, ViewWidth];
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
        _longMessage = null;
    }

    public void WriteMessage(string message)
    {
        _messageBuffer = message;
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
    }

    // Handles waiting will we display a goodbye message, and eventually
    // High Scores screen, etc
    private void OnQuitListener(UIEvent e)
    {
        if (e.Type == UIEventType.KeyInput)
        {
            _playing = false;
        }
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
                    WriteMessage(result.Message);
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

        List<IPerformer> performers = [];
        // It's actually an error condition if at this point either Player or GameState is null
        if (Player is not null && GameState is not null)
        {
            performers.Add(Player);
            performers.AddRange(GameState.ObjDB.GetPerformers(GameState.CurrDungeon, GameState.CurrLevel));

            // I guess this should happen elsewhere, or Actors should be created with topped-up energy
            // for the case where we're entering GameLoop() from a saved game
            foreach (var performer in performers) 
            {
                performer.Energy = performer.Recovery;
            }
        }

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
                // Update step! This is where all the current performer gets a chance
                // to take their turn!                 
                if (performers[p].Energy < 1.0 || TakeTurn(performers[p]))
                {
                    p = (p + 1) % performers.Count;
                    performers[p].Energy += performers[p].Recovery;

                    if (performers[p] != Player)
                    {
                        UpdateDisplay();
                        Thread.Sleep(25);
                    }
                }

                // I imagine later on there'll be bookkeeping and such once we've run
                // through all the current performers?
            }
            catch (GameQuitException)
            {
                break;                
            }

            // TODO: I really need to cleanup the GameState object and
            // what uses what since it current has references to the Campaign,
            // the Dungeons, the current Map, etc and too much of its guts
            // are exposed and called directly
            if (GameState is not null)
            {                
                SetSqsOnScreen();
            }
            
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
            WriteMessage($"{prompt} {result}");
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
        
        return result.Trim();
    }

    (Colour, char) CalcGlyphAtLoc(HashSet<(int, int)> visible, HashSet<(int, int)> remembered, Map map,
                int mapRow, int mapCol, int scrRow, int scrCol)
    {
        var loc = new Loc(GameState.CurrDungeon, GameState.CurrLevel, mapRow, mapCol);
        var glyph = GameState.ObjDB.GlyphAt(loc);
        
        // This is getting a bit gross...
        if (visible.Contains((mapRow, mapCol))) 
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
            if (glyph != GameObjectDB.EMPTY)
                return (glyph.Unlit, glyph.Ch);
            else
                return TileToGlyph(map.TileAt(mapRow, mapCol), false);
        }
        
        return (Colours.BLACK, ' ');
    }

    void SetSqsOnScreen()
    {
        var cmpg = GameState.Campaign;
        var dungeon = cmpg.Dungeons[GameState.CurrDungeon];
        var map = dungeon.LevelMaps[GameState.CurrLevel];
        GameState.Map = map;
        var vs = FieldOfView.CalcVisible(Player, map, GameState.CurrLevel);
        var visible = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();

        // There is a glitch here that I don't want to fix right now in that
        // I am remembering only (row, col). So if a monster picks up an item
        // out of the player's FOV, the remembered square will then show the map
        // tile not the remembered item. I need to store a dictionary of loc + glyph
        // Or perhaps it just needs to be a collection of items + non-basic tiles not
        // every tile
        dungeon.RememberedSqs = dungeon.RememberedSqs.Union(vs).ToHashSet();
        var rememberd = dungeon.RememberedSqs.Select(rm => (rm.Item2, rm.Item3)).ToHashSet();
       
        int rowOffset = Player.Row - PlayerScreenRow;
        int colOffset = Player.Col - PlayerScreenCol;

        for (int r = 0; r < ScreenHeight - 1; r++) 
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
        for (int r = 0; r < ScreenHeight - 1; r++)
        {
            for (int c = 0; c < ViewWidth; c++)
            {
                ZLayer[r, c] = TileFactory.Get(TileType.Unknown);
            }
        }
    }
}
