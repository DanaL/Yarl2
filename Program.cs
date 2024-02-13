using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions("options.json");

UserInterface display;
if (options.Display == "Bearlib")
    display = new BLUserInferface("Yarl2 0.0.1 + Bearlib", options);
else
    display = new SDLUserInterface("Yarl2 0.0.1 + SDL", options);


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