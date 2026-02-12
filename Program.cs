// Delve - A roguelike computer RPG
// Written in 2024 by Dana Larose <ywg.dana@gmail.com>
//
// To the extent possible under law, the author(s) have dedicated all copyright
// and related and neighboring rights to this software to the public domain
// worldwide. This software is distributed without any warranty.
//
// You should have received a copy of the CC0 Public Domain Dedication along 
// with this software. If not, 
// see <http://creativecommons.org/publicdomain/zero/1.0/>.

using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

using Yarl2;

var options = Options.LoadOptions();

UserInterface display;
if (options.Display == "Bearlib")
  display = new BLUserInterface($"Dana's Delve {Constants.VERSION} + Bearlib", options);
else
  display = new RaylibUserInterface($"Dana's Delve {Constants.VERSION} + Raylib", options);

List<string> dialogueErrors = DialogueInterpreter.ValidateDialogueFiles();
if (dialogueErrors.Count > 0)
{
  List<string> errorMessage = ["Dialogue script file errors found:", ""];
  errorMessage.AddRange(dialogueErrors);
  errorMessage.Add("");
  errorMessage.Add("Unfortunately, with damaged script files, delve cannot run.");
  errorMessage.Add("");
  errorMessage.Add("Please fix the errors in the script files or perhaps download a fresh");
  errorMessage.Add("copy of delve!");

  display.SetLongMessage(errorMessage);
  display.BlockForInput(null);
  display.ClearLongMessage();

  Environment.Exit(1);
}

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
        state = RunningState.ExitGame;
        break;
      case SetupType.NewGame:
        try
        {
          gameState = new CampaignCreator(display).Create(options);
          
          display.InTutorial = false;
          if (gameState is null)
            state = RunningState.ExitGame;
        }
        catch (GameNotLoadedException)
        {
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
            state = RunningState.ExitGame;
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
    {
      display.State = UIState.InGame;
      GameLoop(display, gameState);
    }
    display.Reset();
    
    display.CheatSheetMode = CheatSheetMode.Messages;
    display.MessageHistory = [];
  }
  while (state != RunningState.ExitGame);
}
catch (QuitGameException)
{
}
//catch (Exception ex)
//{
//  List<string> lines = [];
//  lines.Add("");
//  lines.Add(" Uhoh, Delve seems to have crashed, likely due to Dana's incompetence :'( ");
//  lines.Add(" The execption thrown was: ");
//  lines.Add(" " + ex.Message);

//  if (ex.InnerException is not null)
//    lines.Add(" " + ex.InnerException.Message);

//  lines.Add("");
//  lines.Add(" Delve will now need to exit.");
  
//  var userDir = Util.UserDir;
//  if (!userDir.Exists)
//    userDir.Create();

//  string logPath = Path.Combine(userDir.FullName, "crash.txt");
//  File.WriteAllLines(logPath, lines);

//  if (ex.InnerException is not null)
//    File.AppendAllText(logPath, ex.InnerException.StackTrace);

//  display.ClosePopup();
//  display.SetLongMessage(lines);

//  display.BlockForInput(null);
//}

static void GameLoop(UserInterface ui, GameState gameState)
{
  Options opts = gameState.Options;
  if (opts.DefaultMoveHints)
    ui.CheatSheetMode = CheatSheetMode.MvMixed;

  if (gameState.KeyMapWarning.Length > 0)
    ui.AlertPlayer(gameState.KeyMapWarning);

  ui.SetInputController(new PlayerCommandController(gameState));
  ui.RegisterAnimation(new CloudAnimation(ui, gameState));
  ui.RegisterAnimation(new RoofAnimation(gameState));
  ui.RegisterAnimation(new LavaAnimation(ui, gameState));

  DateTime refresh = DateTime.UtcNow;

  while (true)
  {
    bool quitting = ui.CheckForInput(gameState);
    if (quitting)
      break;

    try
    {
      if (gameState.NextPerformer() is Actor actor)
      {
        gameState.ActorPreTurn(actor);
        actor.TakeTurn(gameState);
        if (actor.Energy >= 1.0)
          gameState.PushPerformer(actor);
      }

      ui.WriteAlerts();
    }
    catch (QuitGameException)
    {
      // If the players quits the game while playing, we bump them 
      // back to the main menu. (Whereas saving also exits the program)
      return;
    }
    catch (SaveGameException)
    {
      ui.WriteAlerts();

      bool success;
      try
      {
        Serialize.WriteSaveGame(gameState, ui);
        Serialize.WriteOptions(gameState.Options);

        success = true;

        ui.SetLongMessage([" Be seeing you..."]);
        ui.BlockForInput(gameState);
        success = true;
      }
      catch (Exception ex)
      {
        ui.SetPopup(new Popup(ex.Message, "", -1, -1));
        success = false;
      }
      
      if (success)
      {
        throw new QuitGameException();
      }
    }
    catch (PlayerKilledException pke)
    {
      string s = $"Oh noes you've been killed by {pke.Messages[0]} :(";
      if (pke.Messages[0] == "drowning")
        s = "Oh noes you have drowned :(";

      if (gameState.Player.HasTrait<ParalyzedTrait>())
        pke.Messages.Add("while paralyzed");
      ui.SetPopup(new Popup(s, "", -1, -1));
      ui.WriteAlerts();
      ui.BlockFoResponse(gameState);
      GravestoneScreen(ui, gameState, pke.Messages);

      return;
    }
    catch (VictoryException)
    {
      string s = "As you read the final incantation, the remaining chain becomes infused with the binding spell.\n\nArioch belows in rage as his arcane prison is renewed once more!";
      ui.SetPopup(new Popup(s, "", -1, -1));
      ui.WriteAlerts();
      ui.BlockFoResponse(gameState);

      Victory.VictoryScreen(gameState);

      return;
    }

    TimeSpan elapsed = DateTime.UtcNow - refresh;
    int totalMs = (int)elapsed.TotalMilliseconds;
    if (totalMs >= 25)
    {
      ui.RefreshScreen(gameState);

      refresh = DateTime.UtcNow;
    }
  }
}

static void DrawGravestone(UserInterface ui, GameState gameState, List<string> messages, List<Colour> flowerColours)
{
  List<string> text =
    [
      "       ",
           "         __________________",
          @"        /                  \",
          @"       /       RIP          \   ___",
          @"      /                      \ /   \",
          @"     /                        \|    |       __",
          @"    |       killed by:         |___|       /  \",
          @"    |                          |         |     |",
          @"    |                          |         |_____|",
          @"    |                          |      ___",
          @"    |                          |     /   \",
          @"    |                          |    |     |",
          @"    |                          |    |     |",
          @"    *       *                  |*  _*__)__|_",
          @"____)/\_____(\__/_____*________\)/_|(/____",
          @"                     \(/                   "
      ];

  text[5] = $@"     /{gameState.Player.Name.PadLeft((21 + gameState.Player.Name.Length) / 2).PadRight(24)}\    |        __";
  text[7] = $@"    |{messages[0].PadLeft((22 + messages[0].Length) / 2),-26}|          |    |";

  int finalLevel = gameState.CurrLevel + 1;

  string dn;

  if (gameState.Player.Traits.OfType<SwallowedTrait>().FirstOrDefault() is SwallowedTrait swt)
  {
    dn = " in something's belly";

    if (gameState.ObjDb.GetObj(swt.SwallowerID) is Actor swallower)
      finalLevel = swallower.Loc.Level + 1;
  }
  else
  {
    dn = $"in {gameState.CurrentDungeon.Name}";
  }

  int x = (26 - dn.Length) / 2;
  dn = dn.PadLeft(26 - x, ' ');
  dn = dn.PadRight(26, ' ');
  text[8] = $@"    |{dn}|          |____|";

  string lvlTxt = $"on level {finalLevel}";
  lvlTxt = $@"    |{lvlTxt.PadLeft((22 + lvlTxt.Length) / 2),-26}|                ";
  text.Insert(9, lvlTxt);

  if (messages.Count > 1)
  {
    string s = $@"    |{messages[1].PadLeft((22 + messages[1].Length) / 2),-26}|          |    |";
    text.Insert(8, s);
  }

  text.Add("");
  text.Add(" a) See your inventory");
  text.Add(" b) View messages");
  text.Add(" q) Main menu");

  int flower = 0;
  ui.ClosePopup();
  ui.CheatSheetMode = CheatSheetMode.Messages;
  ui.SqsOnScreen = new Sqr[UserInterface.ScreenHeight, UserInterface.ScreenWidth];
  ui.ClearSqsOnScreen();
  for (int r = 0; r < text.Count; r++)
  {
    string row = text[r];
    for (int c = 0; c < row.Length; c++)
    {
      Colour colour = Colours.WHITE;
      char ch = row[c];
      if (r < text.Count - 3 && (ch == '(' || ch == ')'))
        colour = Colours.GREEN;
      else if (r == text.Count - 5 && (ch == '|' || ch == '\\' || ch == '/'))
        colour = Colours.GREEN;
      else if (r == text.Count - 6 && (ch == '|' || ch == '\\' || ch == '/'))
        colour = Colours.GREEN;
      else if (ch == '*')
        colour = flowerColours[flower++];
      Sqr s = new(colour, Colours.BLACK, ch);
      ui.SqsOnScreen[r + 1, c + 1] = s;
    }
  }
}

static List<string> WrapLines(List<string> lines, int maxWidth)
{
  List<string> wrapped = [];

  foreach (string line in lines)
  {
    if (line.Length <= maxWidth)
    {
      wrapped.Add(line);
      continue;
    }

    string[] words = line.Split(' ');
    StringBuilder currentLine = new();

    foreach (var word in words)
    {
      if (currentLine.Length + word.Length + 1 > maxWidth)
      {
        wrapped.Add(currentLine.ToString().TrimEnd());
        currentLine.Clear();
      }

      if (currentLine.Length > 0)
        currentLine.Append(' ');
      currentLine.Append(word);
    }

    if (currentLine.Length > 0)
      wrapped.Add(currentLine.ToString());
  }

  return wrapped;
}

static void ShowMessageLog(UserInterface ui, List<string> lines)
{
  List<string> wrappedLines = WrapLines(lines, UserInterface.ScreenWidth);
  int row = 0;
  int pageSize = UserInterface.ScreenHeight - 1;

  bool more;
  while (row < wrappedLines.Count)
  {
    List<string> page = [.. wrappedLines.Skip(row).Take(pageSize)];

    if (row + pageSize < wrappedLines.Count) 
    {
      page.Add("-- Press space for more, ESC to return --");
      more = true;
    }
    else
    {
      page.Add("-- Press any key to return --");
      more = false;
    }

    char ch;
    do
    {
      ui.SetLongMessage(page);
      ui.UpdateDisplay(null);

      Thread.Sleep(30);
      ch = ui.GetKeyInput();

      if (ch == Constants.ESC || (!more && ch != '\0')) 
      {
        ui.ClearLongMessage();
        return;
      }
      else if (more && (ch == ' ' || ch == '\n' || ch == '\r'))
      {
        break;
      }
    } 
    while (true);

    row += pageSize;
  }

  ui.ClearLongMessage();
}

static void GravestoneScreen(UserInterface ui, GameState gameState, List<string> messages)
{
  static Colour RngColour(Rng rng) => rng.Next(4) switch
  {
    0 => Colours.LIGHT_PURPLE,
    1 => Colours.BLUE,
    2 => Colours.PINK,
    _ => Colours.YELLOW
  };

  List<Colour> flowerColours = [ RngColour(gameState.Rng), RngColour(gameState.Rng), 
    RngColour(gameState.Rng),  RngColour(gameState.Rng), RngColour(gameState.Rng) ];

  DrawGravestone(ui, gameState, messages, flowerColours);

  do
  {
    Thread.Sleep(30);
    char ch = ui.GetKeyInput();

    if (ch == 'a')
    {
      ui.ClosePopup();
      ui.ClearSqsOnScreen();
      gameState.Player.Inventory.ShowMenu(gameState.UIRef(), new InventoryOptions() { Title = "You were carrying", Options = InvOption.MentionMoney });
      DrawGravestone(ui, gameState, messages, flowerColours);
    }
    else if (ch == 'b')
    {
      List<string> lines = [.. ui.MessageHistory.Select(m => m.Fmt)];
      ShowMessageLog(ui, lines);
      DrawGravestone(ui, gameState, messages, flowerColours);
    }
    else if (ch == ' ')
    {
      ui.CloseMenu();
    }
    else if (ch == 'q' || ch == Constants.ESC)
    {
      ui.CloseMenu();
      break;
    }
    
    ui.UpdateDisplay(gameState);
  }
  while (true);  
}

namespace Yarl2
{
  enum RunningState
  {
    Playing, ExitGame, GameOver, Pregame
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
    public bool BumpToChat { get; set; }
    public bool BumpForLockedDoors { get; set; }
    public bool HighlightPlayer { get; set; }
    public bool ShowHints { get; set; }
    public bool ShowTurns { get; set; }
    public bool DefaultMoveHints { get; set; }
    public bool AutoPickupGold { get; set; }
    
    public Dictionary<char, string> KeyRemaps { get; set; } = [];

    public Options() { }

    public void SaveOptions()
    {
      var userDir = Util.UserDir;
      if (!userDir.Exists)
        userDir.Create();

      string optionsPath = Path.Combine(userDir.FullName, "ddoptions.json");
      string json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
      File.WriteAllText(optionsPath, json);
    }

    public static Options LoadOptions()
    {
      // Okay, we'll check for the options file in the user's home dir, then the
      // folder the executable lives in and failing that, use some default options

      Options options = new()
      {
        Display = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Bearlib" : "Raylib",
        FontSize = 18,
        BumpToOpen = true,
        BumpToChat = true,
        BumpForLockedDoors = true,
        HighlightPlayer = false,
        ShowHints = true,
        ShowTurns = false,
        DefaultMoveHints = true,
        AutoPickupGold = false,
      };

      DirectoryInfo userDir = Util.UserDir;
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
          string json = File.ReadAllText(optionsPath);
          Options? opts = JsonSerializer.Deserialize<Options>(json);
          if (opts is not null)
            options = opts;
        }
      }

      return options;
    }
  }
}