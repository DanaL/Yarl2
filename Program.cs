﻿using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

UserInterface display;
if (options.Display == "Bearlib")
    display = new BLUserInferface("Dana's Delve 0.0.1 + Bearlib", options);
else
    display = new SDLUserInterface("Dana's Delve 0.0.1 + SDL", options);

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