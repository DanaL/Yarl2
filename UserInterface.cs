using System.Data;
using System.Runtime.Intrinsics.X86;
using BearLibNET.DefaultImplementations;

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
    protected const int BACKSPACE = 8;
    public const int ScreenWidth = 60;
    public const int ScreenHeight = 30;
    public const int SideBarWidth = 20;
    public const int ViewWidth = ScreenWidth - SideBarWidth;
    
    public abstract void CloseMenu();
    public abstract void ShowDropDown(List<string> lines);
    public abstract void UpdateDisplay();
    protected abstract UIEvent PollForEvent();
    
    protected int FontSize;
    protected int PlayerScreenRow;
    protected int PlayerScreenCol;

    protected List<string>? _longMessage;
    protected string _messageBuffer = "";
    protected Options _options;
    private bool _playing;

    private delegate void InputListener(UIEvent e);
    private InputListener? CurrentListener;

    public Player? Player { get; set; } = null;
    public Queue<char> InputBuffer = new Queue<char>();

    protected GameState? GameState { get; set; } = null;

    public (Colour, char)[,] SqsOnScreen;
    public Tile[,] ZLayer; // An extra layer of tiles to use for effects like clouds

    // It's convenient for other classes to ask what dungeon and level we're on
    public int CurrentDungeon => GameState is not null ? GameState.CurrDungeon : -1;
    public int CurrentLevel => GameState is not null ? GameState.CurrLevel : -1;
    
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
        _longMessage = new List<string>()
        {
            "",
            "",
            "",
            "",
            "     Welcome to Yarl2",
            "       (yet another attempt to make a roguelike,",
            "           this time in C#...)"
        };        
    }

    public void WriteMessage(string message)
    {
        _messageBuffer = message;
    }

    public void WriteLongMessage(List<string> message)
    {
        _longMessage = message;
    }

    // I dunno about having this here. In previous games, I had each Tile object
    // know what its colours were, but maybe the UI class *is* the correct spot
    // to decide how to draw the glyph
    protected (Colour, char) TileToGlyph(Tile tile, bool lit)
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

    public void BeginGame(Campaign campaign)
    {
        GameState = new GameState()
        {
            Map = campaign!.Dungeons[0].LevelMaps[campaign.CurrentLevel],
            Options = _options,
            Player = Player,
            Campaign = campaign,
            CurrLevel = campaign.CurrentLevel,
            CurrDungeon = campaign.CurrentDungeon,
        };

        CurrentListener = new InputListener(MainListener);
    }

    private void StartupListener(UIEvent e)
    {
        if (e.Type == UIEventType.KeyInput)
        {
            _longMessage = null;
            var pregameHandler = new PreGameHandler(this);
            CurrentListener = new InputListener(pregameHandler.HandleInput);
        }
    }

    private void MainListener(UIEvent e)
    {
        if (e.Type == UIEventType.KeyInput)
        {
            InputBuffer.Enqueue(e.Value);
        }
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

    private void TakeTurn(Actor actor)
    {
        var action = actor.TakeTurn(this, GameState);

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
        else if (action is NullAction)
        {
            // Just idling...                
            return;
        }
        else
        {
            ActionResult result;
            do
            {
                result = action!.Execute();
                if (result.AltAction is not null)
                    result = result.AltAction.Execute();
                if (result.Message is not null)
                    WriteMessage(result.Message);
            }
            while (result.AltAction is not null);
        }
    }

    private void DoActorTurns()
    {
        TakeTurn(Player);
    }

    public void GameLoop()
    {
        CurrentListener = StartupListener;
        List<IAnimationListener> animationListeners = [];
        animationListeners.Add(new CloudAnimationListener(this));
        TitleScreen();  

        DateTime lastPollTime = DateTime.Now;

        _playing = true;
        while (_playing) 
        {
            var e = PollForEvent();
            if (e.Type == UIEventType.Quiting)
                break;

            if (e.Type == UIEventType.KeyInput)
                CurrentListener(e);

            try
            {
                // Update step! This is where all the Actors get a chance
                // to take their turn! (At the moment the only actor is 
                // the player)

                // Return a value indicating if all the actors were idle?
                // then I perhaps don't have to recalculate the view, etc
                if (Player is not null)
                    DoActorTurns();
            }
            catch (GameQuitException)
            {
                var msg = new List<string>()
                {
                    "",
                    " Being seeing you..."
                };
                WriteLongMessage(msg);
                CurrentListener = new InputListener(OnQuitListener);
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

            Thread.Sleep(30);
        }        
    }

    void SetSqsOnScreen()
    {
        var cmpg = GameState.Campaign;
        int currLevel = GameState.CurrLevel;
        var dungeon = cmpg.Dungeons[GameState.CurrDungeon];
        var map = dungeon.LevelMaps[currLevel];
        GameState.Map = map;
        var vs = FieldOfView.CalcVisible(Player, map, currLevel);
        var visible = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();
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
                if (visible.Contains((mapRow, mapCol))) 
                {
                    if (ZLayer[r, c].Type != TileType.Unknown)                    
                        SqsOnScreen[r, c] = TileToGlyph(ZLayer[r, c], true);
                    else
                        SqsOnScreen[r, c] = TileToGlyph(map.TileAt(mapRow, mapCol), true);
                }
                else if (rememberd.Contains((mapRow, mapCol)))
                {
                    SqsOnScreen[r, c] = TileToGlyph(map.TileAt(mapRow, mapCol), false);
                }
                else
                {
                    SqsOnScreen[r, c] = (Colours.BLACK, ' ');
                }
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

internal class PreGameHandler
{
    protected const int BACKSPACE = 8;
    private const string _prompt = "Who are you?"; 
    private UserInterface _ui { get; set; }
    private string _playerName { get; set; } = "";
    
    public PreGameHandler(UserInterface ui)
    {
        _ui = ui;
        ui.WriteMessage(_prompt);
    }

    (Campaign, int, int) BeginCampaign(Random rng)
    {
        var dm = new DungeonMaker(rng);
        var campaign = new Campaign();
        var wilderness = new Dungeon(0, "You draw a deep breath of fresh air.");
        var wildernessGenerator = new Wilderness(rng);
        var map = wildernessGenerator.DrawLevel(257);
        wilderness.AddMap(map);
        campaign.AddDungeon(wilderness);

        var mainDungeon = new Dungeon(1, "Musty smells. A distant clang. Danger.");
        var firstLevel = dm.DrawLevel(100, 40);
        mainDungeon.AddMap(firstLevel);
        campaign.AddDungeon(mainDungeon);

        // Find an open floor in the first level of the dungeon
        // and create a Portal to it in the wilderness
        var stairs = firstLevel.RandomTile(TileType.Floor, rng);
        var entrance = map.RandomTile(TileType.Tree, rng);
        var portal = new Portal("You stand before a looming portal.")
        {
            Destination = (1, 0, stairs.Item1, stairs.Item2)
        };
        map.SetTile(entrance, portal);

        var exitStairs = new Upstairs("")
        {
            Destination = (0, 0, entrance.Item1, entrance.Item2)
        };
        firstLevel.SetTile(stairs, exitStairs);

        campaign.CurrentDungeon = 0;
        campaign.CurrentLevel = 0;
        return (campaign, entrance.Item1, entrance.Item2);
    }

    private void SetupGame(string playerName)
    {
        if (Serialize.SaveFileExists(playerName))
        {
            var (player, c) = Serialize.LoadSaveGame(playerName);
            _ui.Player = player;
            _ui.BeginGame(c);
        }
        else
        {
            var (c, startRow, startCol) = BeginCampaign(new Random());
            Player player = new Player(playerName, startRow, startCol);
            var spear = ItemFactory.Get("spear");
            spear.Adjectives.Add("old");
            player.Inventory.Add(spear);
            var armour = ItemFactory.Get("leather armour");
            armour.Adjectives.Add("battered");
            player.Inventory.Add(armour);
            
            _ui.Player = player;
            _ui.BeginGame(c);
        }
    }

    // Eventually this is going to need state because
    // we'll be handling picking a character class, etc
    // for a new game

    // This code is going to end up duplciated so I wonder
    // if it'll be too complicated to make a generic 
    // "typing in text handler"
    public void HandleInput(UIEvent e)
    {
        if (e.Value == '\n' || e.Value == 13) 
        {
            // done, 
            SetupGame(_playerName);
            _ui.WriteMessage($"Welcome, {_playerName}");
            
            return;
        }
        else if (e.Value == BACKSPACE)
        {
            _playerName = _playerName.Length > 0 
                            ? _playerName[..^1] 
                            : "";
        }
        else 
        {
            _playerName += e.Value;            
        }

        _ui.WriteMessage($"{_prompt} {_playerName}");
    }
}
