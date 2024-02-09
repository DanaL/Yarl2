using BearLibNET.DefaultImplementations;
using System.Numerics;

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
    protected const int ScreenWidth = 60;
    protected const int ScreenHeight = 30;
    protected const int SideBarWidth = 20;
    protected const int ViewWidth = ScreenWidth - SideBarWidth;
    
    protected int FontSize;
    protected int PlayerScreenRow;
    protected int PlayerScreenCol;

    protected readonly Color BLACK = new() { A = 255, R = 0, G = 0, B = 0 };
    protected readonly Color WHITE = new() { A = 255, R = 255, G = 255, B = 255 };
    protected readonly Color GREY = new() { A = 255, R = 136, G = 136, B = 136 };
    protected readonly Color LIGHT_GREY = new() { A = 255, R = 220, G = 220, B = 220 };
    protected readonly Color DARK_GREY = new() { A = 255, R = 72, G = 73, B = 75 };
    protected readonly Color YELLOW = new() { A = 255, R = 255, G = 255, B = 53 };
    protected readonly Color YELLOW_ORANGE = new() { A = 255, R = 255, G = 159, B = 0 };
    protected readonly Color LIGHT_BROWN = new() { A = 255, R = 101, G = 75, B = 0 };
    protected readonly Color BROWN = new() { A = 255, R = 101, G = 67, B = 33 };
    protected readonly Color GREEN = new() { A = 255, R = 144, G = 238, B = 144 };
    protected readonly Color DARK_GREEN = new() { A = 255, R = 0, G = 71, B = 49 };
    protected readonly Color BLUE = new() { A = 255, R = 0, G = 0, B = 200 };
    protected readonly Color LIGHT_BLUE = new() { A = 255, R = 55, G = 198, B = 255 };
    protected readonly Color DARK_BLUE = new() { A = 255, R = 12, G = 35, B = 64 };

    public abstract Action? GetCommand(GameState gameState);
    public abstract string QueryUser(string prompt);        
    public abstract void UpdateDisplay(GameState gameState);
    public abstract char WaitForInput();
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

    public UserInterface(Options opts)
    {
        _options = opts;
        PlayerScreenRow = (ScreenHeight - 1) / 2 + 1;
        PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;        
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

    private (int, int) AskForDirection()
    {
        do 
        {
            WriteMessage("Which way?");
            char ch = WaitForInput();            
            if (ch == 'y')
                return (-1, -1);
            else if (ch == 'u')
                return (-1, 1);
            else if (ch == 'h')
                return (0, -1);
            else if (ch == 'j')
                return (1, 0);
            else if (ch == 'k')
                return (-1, 0);
            else if (ch == 'l')
                return (0, 1);
            else if (ch == 'b')
                return (1, -1);
            else if (ch == 'n')
                return (1, 1);
        }
        while (true);
    }

    public bool QueryYesNo(string prompt)
    {        
        do 
        {
            WriteMessage(prompt);
            char ch = WaitForInput();
            if (ch == 'y')
                return true;
            else if (ch == 'n')
                return false;
        }
        while (true);
    }

    public void WriteMessage(string message)
    {
        _messageBuffer = message;
    }

    public void WriteLongMessage(List<string> message)
    {
        _longMessage = message;
    }

    protected Action KeyToAction(char ch, GameState gameState)
    {
        Player p = gameState.Player!;
        Map m = gameState.Map!;

        if (ch == 'c') 
        {
            var (dr, dc) = AskForDirection();            
            return new CloseDoorAction(p, p.Row + dr, p.Col + dc, m);
        }
        else if (ch == 'o') 
        {
            var (dr, dc) = AskForDirection();            
            return new OpenDoorAction(p, p.Row + dr, p.Col + dc, m);
        }
                
        
        else if (ch == 'S')
            return new SaveGameAction();

        return null;
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
            case TileType.DeepWater:
                return lit ? (BLUE, '~') : (DARK_BLUE, '~');
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
            //if (ui.QueryYesNo("Really quit and save? (y/n)"))
            //{
            //    Serialize.WriteSaveGame(player.Name, player, campaign, gameState);
            //    throw new GameQuitException();
            //}
            //else
            //{
            //    ui.WriteMessage("Nevermind.");
            //}
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
                // Maybe move this into gamestate?
                // or eventually the user input handler class?
                var c = GameState.Campaign;
                int currLevel = GameState.CurrLevel;
                var dungeon = c.Dungeons[GameState.CurrDungeon];
                var map = dungeon.LevelMaps[currLevel];
                GameState.Map = map;
                var vs = FieldOfView.CalcVisible(Player, map, currLevel);
                var toShow = vs.Select(v => (v.Item2, v.Item3)).ToHashSet();        
                dungeon.RememberedSqs = dungeon.RememberedSqs.Union(vs).ToHashSet();
                dungeon.RememberedSqs = dungeon.RememberedSqs.Union(vs).ToHashSet();
                GameState.Visible = vs;
                GameState.Remebered = dungeon.RememberedSqs;
            }
            UpdateDisplay(GameState);

            var dd = DateTime.Now - lastPollTime;
            if (dd.TotalSeconds > 5) 
            {
                Console.WriteLine("hello, world?");
                lastPollTime = DateTime.Now;
            }

            Thread.Sleep(50);
        }        
    }
}

// I dunno? Should this be in Player object?
//
// Aesthetics-wise, I think it would be nice to have the
// Player be another Actor so game loop is like "Hey Actors
// do your shit
internal class PlayerInputHandler
{
    public void HandleInput(UIEvent e)
    {

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
        var (c, startRow, startCol) = BeginCampaign(new Random());
        Player player = new Player(playerName, startRow, startCol);          
        _ui.Player = player;
        _ui.BeginGame(c);
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

internal interface IInputAccumulator 
{
    bool Done();
    void Input(char ch);
}

internal class YesNoAccumulator : IInputAccumulator
{
    private bool _done;

    public YesNoAccumulator() => _done = false;

    public bool Done() => _done;

    public void Input(char ch)
    {
        // Need to eventually handle ESC
        if (ch == 'y')
            _done = true;
    }
}