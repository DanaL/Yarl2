using System.Text.Json;

using Yarl2;

var wildernessGenerator = new Wilderness(new Random());
var m2 = wildernessGenerator.DrawLevel(257);

var options = Options.LoadOptions("options.json");

UserInterface display;
if (options.Display == "Bearlib")
    display = new BLUserInferface("Yarl2 0.0.1 + Bearlib", options);
else
    display = new SDLUserInterface("Yarl2 0.0.1 + SDL", options);

try
{
    var rng = new Random();
    display.GameLoop();

    // string playerName = display.QueryUser("Who are you?").Trim();
    // display.WriteMessage($"Hello, {playerName}.");
    // display.WaitForInput();
    
    // Player player;
    // Campaign campaign;
    // int currentLevel, currentDungeon;

    // string filename = $"{playerName}.dat";
    // if (File.Exists(filename))
    // {
    //     var bytes = File.ReadAllBytes(filename);
    //     var sgi = JsonSerializer.Deserialize<SaveGameInfo>(bytes);

    //     var (p, c, cl, cd) = Serialize.LoadSaveGame(playerName);
    //     player = p;
    //     campaign = c;
    //     currentDungeon = cd;
    //     currentLevel = cl;
    // }
    // else
    // {
    //     var (cm, row, col) = BeginCampaign(rng);
    //     campaign = cm;
    //     player = new Player(playerName, row, col);  
    //     currentDungeon = 0;
    //     currentLevel = 0;      
    // }
    
    //display.Player = player;
    //var engine = new GameEngine(29, 29, display, options);
    //engine.Play(player, campaign, currentLevel, currentDungeon);
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