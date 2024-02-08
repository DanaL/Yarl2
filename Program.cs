using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

Display display;
if (options.Display == "Bearlib")
    display = new BLDisplay("Yarl2 0.0.1 + Bearlib", options.FontSize);
else
    display = new SDLDisplay("Yarl2 0.0.1 + SDL", options.FontSize);


var rng = new Random();

var dm = new DungeonMaker(rng);
//var map = dungeon.DrawLevel(100, 40);
//map.Dump();

var campaign = new Campaign();
var wilderness = new Dungeon(0);
var wildernessGenerator = new Wilderness(rng);
var map = wildernessGenerator.DrawLevel(257);
wilderness.AddMap(map);
campaign.AddDungeon(wilderness);

var mainDungeon = new Dungeon(1);
var firstLevel = dm.DrawLevel(100, 40);
mainDungeon.AddMap(firstLevel);
campaign.AddDungeon(mainDungeon);

// Find an open floor in the first level of the dungeon
// and create a Portal to it in the wilderness
var stairs = firstLevel.RandomTile(TileType.Floor, rng);
var entrance = map.RandomTile(TileType.Tree, rng);
var portal = new Portal(TileType.Portal);
map.SetTile(entrance, portal);

try
{
    display.TitleScreen();

    string playerName = display.QueryUser("Who are you?");
    display.WriteMessage($"Hello, {playerName}.");
    display.WaitForInput();

    var rnd = new Random();    
    var player = new Player(playerName, entrance.Item1, entrance.Item2);
    display.Player = player;
   
    var engine = new GameEngine(29, 29, display, options);
    engine.Play(player, campaign);
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