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

using System.Reflection.Metadata;
using System.Text.Json;

using Yarl2;

var options = Options.LoadOptions();

UserInterface display;
if (options.Display == "Bearlib")
  display = new BLUserInferface("Dana's Delve 0.2.0 + Bearlib", options);
else
  display = new SDLUserInterface("Dana's Delve 0.2.0 + SDL", options);

try
{
  RunningState state = RunningState.Pregame;

  do
  {
    display.ClosePopup();
    TitleScreen ts = new(display);
    SetupType gameSetup = ts.Display();

    GameState? gameState = null;
    switch (gameSetup)
    {
      case SetupType.Quit:
        state = RunningState.Quitting;
        break;
      case SetupType.NewGame:
        try
        {
          gameState = new CampaignCreator(display).Create(options);
          display.InTutorial = false;
          if (gameState is null)
            state = RunningState.Quitting;
        }
        catch (GameNotLoadedException)
        {
          Console.WriteLine("flag?");
          state = RunningState.Pregame;
        }
        break;
      case SetupType.Tutorial:
        display.InTutorial = true;
        gameState = new Tutorial(display).Setup(options);
        break;
      default:
        try
        {
          display.InTutorial = false;
          gameState = new GameLoader(display).Load(options);
          if (gameState is null)
            state = RunningState.Quitting;
        }
        catch (GameNotLoadedException)
        {
          state = RunningState.Pregame;
        }
        break;
    }

    if (gameSetup == SetupType.Quit)
      break;

    if (gameState is not null)
      state = display.GameLoop(gameState);

    display.CheatSheetMode = CheatSheetMode.Messages;
    display.MessageHistory = [];
  }
  while (state != RunningState.Quitting);
}
catch (GameQuitException)
{
}
catch (Exception ex)
{
  List<string> lines = [];
  lines.Add("");
  lines.Add(" Uhoh, Delve seems to have crashed, likely due to Dana's incompetence :'( ");
  lines.Add(" The execption throw was: ");
  lines.Add(" " + ex.Message);
  lines.Add("");
  lines.Add(" Delve will now need to exit.");
  display.WriteLongMessage(lines);
  display.BlockForInput();
}

namespace Yarl2
{
  enum RunningState
  {
    Playing, Quitting, GameOver, Pregame
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
        FontSize = 14,
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