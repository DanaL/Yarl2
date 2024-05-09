using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions();

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

    public static Options LoadOptions()
    {
      // Okay, we'll check for the options file in the user's home dir, then the
      // folder the executable lives in and failing that, use some default options

      Options options = new()
      {
        Display = "Bearlib",
        FontSize = 12,
        BumpToOpen = true
      };

      var userDir = new DirectoryInfo(Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile));
      if (userDir.Exists)
      {
        string optionsPath = "ddoptions.json";

        foreach (FileInfo file in userDir.GetFiles())
        {
          if (file.Name == "ddoptions.json")
          {
            optionsPath = file.FullName;
            break;
          }
        }

        if (File.Exists(optionsPath))
        {
          var json = File.ReadAllText(optionsPath);
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
      }

      return options;
    }
  }
}