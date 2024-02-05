
using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

Display display;
if (options.Display == "Bearlib")
    display = new BLDisplay("Yarl2 0.0.1", options.FontSize);
else
    display = new SDLDisplay("Yarl2 0.0.1", options.FontSize);

var map = Map.TestMap();
//var map = new Map(75, 75);
//map.SetRandomTestMap();

try
{
    display.TitleScreen();

    string playerName = display.QueryUser("Who are you?");
    display.WriteMessage($"Hello, {playerName}.");
    display.WaitForInput();

    var rnd = new Random();
    ushort startRow = (ushort) rnd.Next(1, map.Height);
    ushort startCol = (ushort)rnd.Next(1, map.Width);
    var player = new Player(playerName, startRow, startCol);
    display.Player = player;
   
    var engine = new GameEngine(29, 29, display);
    engine.Play(player, map);
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
        public string Display { get; set; }
        public int FontSize { get; set; }
    
        public static Options LoadOptions(string path)
        {
            Options options = new Options()
            {
                Display = "Bearlib",
                FontSize = 12,
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
                }
            }
            
            return options;
        }
    }
}