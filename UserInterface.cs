using System.Data;
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
    
    protected int FontSize;
    protected int PlayerScreenRow;
    protected int PlayerScreenCol;

    public readonly Color BLACK = new() { A = 255, R = 0, G = 0, B = 0 };
    public readonly Color WHITE = new() { A = 255, R = 255, G = 255, B = 255 };
    public readonly Color GREY = new() { A = 255, R = 136, G = 136, B = 136 };
    public readonly Color LIGHT_GREY = new() { A = 255, R = 220, G = 220, B = 220 };
    public readonly Color DARK_GREY = new() { A = 255, R = 72, G = 73, B = 75 };
    public readonly Color YELLOW = new() { A = 255, R = 255, G = 255, B = 53 };
    public readonly Color YELLOW_ORANGE = new() { A = 255, R = 255, G = 159, B = 0 };
    public readonly Color LIGHT_BROWN = new() { A = 255, R = 101, G = 75, B = 0 };
    public readonly Color BROWN = new() { A = 255, R = 101, G = 67, B = 33 };
    public readonly Color GREEN = new() { A = 255, R = 144, G = 238, B = 144 };
    public readonly Color DARK_GREEN = new() { A = 255, R = 0, G = 71, B = 49 };
    public readonly Color BLUE = new() { A = 255, R = 0, G = 0, B = 200 };
    public readonly Color LIGHT_BLUE = new() { A = 255, R = 55, G = 198, B = 255 };
    public readonly Color DARK_BLUE = new() { A = 255, R = 12, G = 35, B = 64 };

    public abstract void UpdateDisplay();
    protected abstract UIEvent PollForEvent();
    
    protected List<string>? _longMessage;
    protected string _messageBuffer = "";
    protected Options _options;
    private bool _playing;

    private delegate void InputListener(UIEvent e);
    private InputListener? CurrentListener;

    public Player? Player { get; set; } = null;
    public Queue<char> InputBuffer = new Queue<char>();

    protected GameState? GameState { get; set; } = null;

    public (Color, char)[,] SqsOnScreen;

    public UserInterface(Options opts)
    {
        _options = opts;
        PlayerScreenRow = (ScreenHeight - 1) / 2 + 1;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
        SqsOnScreen = new (Color, char)[ScreenHeight - 1, ViewWidth];
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

    protected (Color, char) TileToGlyph(Tile tile, bool lit)
    {
        switch (tile.Type)
        {
            case TileType.Wall:
            case TileType.PermWall:
                return lit ? (GREY, '#') : (DARK_GREY, '#');
            case TileType.Floor:
                return lit ? (YELLOW, '.') : (GREY, '.');
            case TileType.Door:
                char ch = ((Door)tile).Open ? '\\' : '+';
                return lit ? (LIGHT_BROWN, ch) : (BROWN, ch);
            case TileType.Water:
            case TileType.DeepWater:
                return lit ? (BLUE, '}') : (DARK_BLUE, '}');
            case TileType.Sand:
                return lit ? (YELLOW, '.') : (YELLOW_ORANGE, '.');
            case TileType.Grass:
                return lit ? (GREEN, '.') : (DARK_GREEN, '.');
            case TileType.Tree:
                return lit ? (GREEN, 'ϙ') : (DARK_GREEN, 'ϙ');
            case TileType.Mountain:
                return lit ? (GREY, '\u039B') : (DARK_GREY, '\u039B');
            case TileType.SnowPeak:
                return lit ? (WHITE, '\u039B') : (GREY, '\u039B');
            case TileType.Portal:
                return lit ? (WHITE, 'Ո') : (GREY, 'Ո');
            case TileType.Downstairs:
                return lit ? (GREY, '>') : (DARK_GREY, '>');
            case TileType.Upstairs:
                return lit ? (GREY, '<') : (DARK_GREY, '<');
            default:
                return (BLACK, ' ');
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
        animationListeners.Add(new WaterAnimationListener(this));
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

            Thread.Sleep(25);
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
                    SqsOnScreen[r, c] = TileToGlyph(map.TileAt(mapRow, mapCol), true);                
                else if (rememberd.Contains((mapRow, mapCol)))
                    SqsOnScreen[r, c] = TileToGlyph(map.TileAt(mapRow, mapCol), true);
                else
                    SqsOnScreen[r, c] = (BLACK, ' ');
            }
        }
        SqsOnScreen[PlayerScreenRow, PlayerScreenCol] = (WHITE, '@');
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

internal interface IAnimationListener
{
    void Update();
}

internal class WaterAnimationListener : IAnimationListener
{
    DateTime _lastFrame;
    UserInterface _ui;
    HashSet<(int, int)> _sparkles = [];

    public WaterAnimationListener(UserInterface ui)
    {
        _ui = ui;
        _lastFrame = DateTime.Now;
        
        SetSparkles();
    }

    void SetSparkles()
    {
        _sparkles = [];
        var rng = new Random();        
        for (int r = 0; r < UserInterface.ScreenHeight - 1; r++) 
        {
            for (int c = 0; c < UserInterface.ViewWidth; c++)
            {
                if (rng.NextDouble() < 0.05)
                    _sparkles.Add((r, c));
                if (_sparkles.Count >= 10)
                    return;
            }            
        }
    }
    
    public void Update() 
    {
        foreach (var sq in _sparkles)
        {
            var t = _ui.SqsOnScreen[sq.Item1, sq.Item2];
            if (t.Item2 == '}')
            {
                _ui.SqsOnScreen[sq.Item1, sq.Item2] = (_ui.LIGHT_BLUE, '~');
            }
        }

        var dd = DateTime.Now - _lastFrame;
        if (dd.TotalMilliseconds >= 150)
        {
            _lastFrame = DateTime.Now;
            SetSparkles();
        }    
    }
}