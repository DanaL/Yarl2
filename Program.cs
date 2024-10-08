using System.Text.Json;

using Yarl2;


var txt = File.ReadAllText(@"data\mayor.txt");
var scanner = new ScriptScanner(txt);
var tokens = scanner.ScanTokens();



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
  public class Configuration
  {
    public string? Display { get; set; }
    public int FontSize { get; set; }
    public bool BumpToOpen { get; set; }
    public Dictionary<string, string> KeyRemaps { get; set; } = [];
  }

  public class Options
  {
    public string? Display { get; set; }
    public int FontSize { get; set; }
    public bool BumpToOpen { get; set; }
    public Dictionary<char, string> KeyRemaps { get; set; } = [];

    public Options() { }

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

      var userDir = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
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
          var opts = JsonSerializer.Deserialize<Options>(json);
          if (opts is not null)
            options = opts;
        }
      }

      return options;
    }
  }
}