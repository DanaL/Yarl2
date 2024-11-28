// Yarl2 - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions();

UserInterface display;
if (options.Display == "Bearlib")
  display = new BLUserInferface("Dana's Delve 0.2.0 + Bearlib", options);
else
  display = new SDLUserInterface("Dana's Delve 0.2.0 + SDL", options);

RunningState state;
do
{
  TitleScreen ts = new(display);
  SetupType gameSetup = ts.Display();

  GameState? gameState;
  switch (gameSetup)
  {
    case SetupType.Quit:
      gameState = null;
      state = RunningState.Quitting;
      break;
    case SetupType.NewGame:
      gameState = new CampaignCreator(display).Create(options);
      if (gameState is null)
        state = RunningState.Quitting;
      break;
    default:
      gameState = new GameLoader(display).Load(options);
      if (gameState is null)
        state = RunningState.Quitting;
      break;
  }

  if (gameSetup == SetupType.Quit || gameState is null)
    break;

  state = display.GameLoop(gameState);
}
while (state == RunningState.Playing);

namespace Yarl2
{
  enum RunningState
  {
    Playing, Quitting, GameOver
  }

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
    public bool HighlightPlayer { get; set; }
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
        BumpToOpen = true,
        HighlightPlayer = false
      };

      var userDir = Util.UserDir;
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