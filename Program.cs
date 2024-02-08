using System.Text.Json;

using Yarl2;

var wildernessGenerator = new Wilderness(new Random());
var m2 = wildernessGenerator.DrawLevel(257);


var options = Options.LoadOptions("options.json");

Display display;
if (options.Display == "Bearlib")
    display = new BLDisplay("Yarl2 0.0.1 + Bearlib", options.FontSize);
else
    display = new SDLDisplay("Yarl2 0.0.1 + SDL", options.FontSize);

static (Campaign, int, int) BeginCampaign(Random rng)
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

    return (campaign, entrance.Item1, entrance.Item2);
}

try
{
    var rng = new Random();
    display.TitleScreen();

    string playerName = display.QueryUser("Who are you?").Trim();
    display.WriteMessage($"Hello, {playerName}.");
    display.WaitForInput();
    
    Player player;
    Campaign campaign;
    int currentLevel, currentDungeon;

    string filename = $"{playerName}.dat";
    if (File.Exists(filename))
    {
        var bytes = File.ReadAllBytes(filename);
        var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

        var (p, c, cl, cd) = Serialize.LoadSaveGame(playerName);
        player = p;
        campaign = c;
        currentDungeon = cd;
        currentLevel = cl;
    }
    else
    {
        var (cm, row, col) = BeginCampaign(rng);
        campaign = cm;
        player = new Player(playerName, row, col);  
        currentDungeon = 0;
        currentLevel = 0;      
    }
    
    display.Player = player;
    var engine = new GameEngine(29, 29, display, options);
    engine.Play(player, campaign, currentLevel, currentDungeon);
}
catch (GameQuitException)
{
    var msg = new List<string>()
    {
        "",
        " Being seeing you..."
    };
    display.WriteLongMessage(msg);
}

static (int, int) RandomStartPos(Map map, Random rnd)
{
    while (true)
    {
        int r = rnd.Next(1, map.Height - 1);
        int c = rnd.Next(1, map.Width - 1);

        switch (map.TileAt(r, c).Type)
        {
            case TileType.Floor:
            case TileType.Grass:
            case TileType.Tree:
                return (r, c);
        }
    }
}

namespace Yarl2
{
    public class Options
    {
        public string? Display { get; set; }
        public int FontSize { get; set; }
        public bool BumpToOpen { get; set; }

        public static Options LoadOptions(string path)
        {
            Options options = new Options()
            {
                Display = "Bearlib",
                FontSize = 12,
                BumpToOpen = true
            };

            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var opts = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                if (opts is not null) 
                {
                    if (opts.TryGetValue("Display", out string displayValue))            
                        options.Display = displayValue;
                    if (opts.TryGetValue("FontSize", out string fsValue))
                        options.FontSize = int.Parse(fsValue);
                    if (opts.TryGetValue("BumpToOpen", out string btoValue))
                        options.BumpToOpen = bool.Parse(btoValue);
                }
            }
            
            return options;
        }
    }
}