﻿using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

// var dm = Map.TestMap();
// dm.Dump();

// var dj = new DjikstraMap(dm, 0, 20, 0, 20);
// Dictionary<TileType, int> passable = new() { { TileType.DungeonFloor, 1 }, { TileType.Door, 2 } };
// dj.Generate(passable, (18, 1));

// var path = dj.ShortestPath(16, 5, 0, 0);
// return;

// var dm = new DungeonMap(rng);
// var map = dm.DrawLevel(70, 30);
// map.Dump();
//return;


UserInterface display;
if (options.Display == "Bearlib")
    display = new BLUserInferface("Yarl2 0.0.1 + Bearlib", options);
else
    display = new SDLUserInterface("Yarl2 0.0.1 + SDL", options);

display.TitleScreen();

var pgh = new PreGameHandler(display);
var gameState = pgh.StartUp(options);
if (gameState is not null)
{
    display.GameLoop(gameState);
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
                    if (opts.TryGetValue("Display", out var displayValue))            
                        options.Display = displayValue;
                    if (opts.TryGetValue("FontSize", out var fsValue))
                        options.FontSize = int.Parse(fsValue);
                    if (opts.TryGetValue("BumpToOpen", out var btoValue))
                        options.BumpToOpen = bool.Parse(btoValue);
                }
            }
            
            return options;
        }
    }
}