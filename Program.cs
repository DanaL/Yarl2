using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

// var dm = Map.TestMap();
// dm.Dump();

// var dj = new DjikstraMap(dm, 0, 20, 0, 20);
// Dictionary<TileType, int> passable = new() { { TileType.DungeonFloor, 1 }, { TileType.Door, 2 } };
// dj.Generate(passable, (18, 1));

// var path = dj.ShortestPath(16, 5, 0, 0);
// return;

//var i1 = ItemSaver.TextToItem("2|0,0,0,0|spear|False|a|True|1|1|old|);white;grey|MeleeAttackTrait,6,1,0|Weapon");
//var i2 = ItemSaver.TextToItem("3|0,0,0,0|leather armour|False|b|True|1|1|battered|[;brown;lightbrown|ArmourTrait,Shirt,1,0|Armour");
//var i3 = ItemSaver.TextToItem("7|0,0,0,0|torch|True||False|1|0||(;lightbrown;brown|LightSourceTrait,7,True,5,15,0,1|Tool");

UserInterface display;
if (options.Display == "Bearlib")
    display = new BLUserInferface("Yarl2 0.0.1 + Bearlib", options);
else
    display = new SDLUserInterface("Yarl2 0.0.1 + SDL", options);

// var a = TerrainFlags.None;
// a |= TerrainFlags.Lit;
// Console.WriteLine(a & TerrainFlags.Wet);

//var rng = new Random();
display.TitleScreen();

var pgh = new PreGameHandler(display);

if (pgh.StartUp())
{
    display.GameLoop();
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