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

using System.Diagnostics;
using System.Text;

namespace Yarl2;

enum CheatSheetMode
{
  Messages = 0,
  Commands = 1,
  Movement = 2,
  MvMixed = 3
}

record struct MsgHistory(string Message, int Count)
{
  public readonly string Fmt => Count > 1 ? $"{Message} x{Count}" : Message;
}

// I think that the way development is proceeding, it's soon not going to
// make sense for SDLUserInterface and BLUserInterface to be subclasses
// of UserInterface. It's more like they are being relegated to primive 
// display terminals and I'm pull more logic up into the base class, so
// I'll probably move towards Composition instead of Inheritance
abstract class UserInterface
{
  public const int ScreenWidth = 80;
  public const int ScreenHeight = 32;
  public const int SideBarWidth = 30;
  public const int ViewWidth = ScreenWidth - SideBarWidth;
  public const int ViewHeight = ScreenHeight - 6;

  public abstract void UpdateDisplay(GameState? gs);
  public abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour);
  public abstract void WriteLine(string message, int lineNum, int col, int width, Colour textColour, Colour bgColour);
  public abstract void WriteSq(int row, int col, Sqr sq);
  public abstract void ClearScreen();

  protected abstract GameEvent PollForEvent(bool pause = true);  
  protected abstract void Blit(); // Is blit the right term for this? 'Presenting the screen'

  protected int FontSize;
  public int PlayerScreenRow { get; protected set; }
  public int PlayerScreenCol { get; protected set; }
  protected List<string>? _longMessage;
  
  readonly Queue<string> Messages = [];

  public Sqr[,] SqsOnScreen;
  public Sqr[,] ZLayer; // An extra layer of screen tiles that overrides what
                        // whatever else was calculated to be displayed

  public CheatSheetMode CheatSheetMode { get; set; } = CheatSheetMode.Messages;

  protected List<string> MenuRows { get; set; } = [];

  IPopup? _popup = null;
  IPopup? _confirm = null;

  public List<MsgHistory> MessageHistory = [];
  protected readonly int MaxHistory = 50;
  protected bool HistoryUpdated = false;

  List<Animation> _animations = [];

  public bool InTutorial { get; set; } = false;
  public bool PauseForResponse { get; set; } = false;

  Inputer? InputController { get; set; } = null;
  public void SetInputController(Inputer inputer) => InputController = inputer;

  // Reusable buffers to reduce allocations in WriteSideBar
  private readonly (Colour, string)[] _statusLineBuffer2 = new (Colour, string)[2];
  private readonly (Colour, string)[] _statusLineBuffer3 = new (Colour, string)[3];
  private readonly List<(Colour, string)> _piecesBuffer = new(16);
  private readonly HashSet<string> _statusesBuffer = new(32);
  private readonly StringBuilder _stringBuilder = new(64);

  public UserInterface()
  {
    PlayerScreenRow = ViewHeight / 2;
    PlayerScreenCol = (ScreenWidth - SideBarWidth - 1) / 2;
    SqsOnScreen = new Sqr[ViewHeight, ViewWidth];
    ZLayer = new Sqr[ViewHeight, ViewWidth];
    ClearZLayer();
  }
