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

using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Yarl2;

class RaylibUserInterface : UserInterface
{
  Font _font;
  int _fontWidth;
  int _fontHeight;
  Dictionary<Colour, Color> _colours = [];
  DateTime _lastKeyTime = DateTime.MinValue;

  public RaylibUserInterface(string windowTitle, Options opt) : base(opt)
  {
    int width = ScreenWidth * (opt.FontSize / 2) + 2;
    int height = ScreenHeight * opt.FontSize + 2;

    SetConfigFlags(ConfigFlags.VSyncHint);
    SetTraceLogLevel(TraceLogLevel.None); 
    InitWindow(width, height, windowTitle);
    
    SetExitKey(KeyboardKey.Null);
    SetTargetFPS(60);

    FontSize = opt.FontSize;
    string fontPath = ResourcePath.GetBaseFilePath("DejaVuSansMono.ttf");

    // This feels unnecessary to me, but if I don't load the font this way
    // the unicode characters don't render correctly.
    const int GLYPH_COUNT = 65535;
    int[] codepoints = new int[GLYPH_COUNT];
    for (int i = 0; i < GLYPH_COUNT; i++)
    {
      codepoints[i] = i;
    }
    _font = LoadFontEx(fontPath, FontSize, codepoints, GLYPH_COUNT);

    _fontWidth = FontSize / 2;
    _fontHeight = FontSize;

    SetWindowPosition(100, 0);
  }

  protected override GameEvent PollForEvent()
  {
    if (WindowShouldClose())
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.Quiting, '\0');
    }

    // Handle the initial key press
    int ch = GetCharPressed();
    if (ch > 0)
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)ch);
    }

    ch = GetKeyPressed();
    if (ch == (int)KeyboardKey.Escape && IsKeyPressed(KeyboardKey.Escape))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.ESC);
    }
    else if (ch == (int)KeyboardKey.Enter && IsKeyPressed(KeyboardKey.Enter))
    {        
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)13);
    }
    else if (ch == (int)KeyboardKey.Backspace && IsKeyPressed(KeyboardKey.Backspace))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.BACKSPACE);
    }
    else if (ch == (int)KeyboardKey.Tab && IsKeyPressed(KeyboardKey.Tab))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.TAB);
    }

    TimeSpan delta = DateTime.UtcNow - _lastKeyTime;
    double ms = delta.TotalMilliseconds;

    if (ms < 95)
      return new GameEvent(GameEventType.NoEvent, '\0');;

    if (IsKeyDown(KeyboardKey.Left) || IsKeyDown(KeyboardKey.H))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'h');
    }
    else if (IsKeyDown(KeyboardKey.Right) || IsKeyDown(KeyboardKey.L))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'l');
    }
    else if (IsKeyDown(KeyboardKey.Up) || IsKeyDown(KeyboardKey.K))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'k');
    }
    else if (IsKeyDown(KeyboardKey.Down) || IsKeyDown(KeyboardKey.J))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'j');
    }
    else if (IsKeyDown(KeyboardKey.Y))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'y');
    }
    else if (IsKeyDown(KeyboardKey.U))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'u');
    }
    else if (IsKeyDown(KeyboardKey.B))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'b');
    }
    else if (IsKeyDown(KeyboardKey.N))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, 'n');
    }
    else if (IsKeyDown(KeyboardKey.Backspace))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.BACKSPACE);
    }
    else if (IsKeyDown(KeyboardKey.Tab))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.TAB);
    }
    else if (IsKeyDown(KeyboardKey.Escape))
    {
      _lastKeyTime = DateTime.UtcNow;
      return new GameEvent(GameEventType.KeyInput, (char)Constants.ESC);
    }
    
    return new GameEvent(GameEventType.NoEvent, '\0');
  }

  Color ToRaylibColor(Colour colour)
  {
    if (!_colours.TryGetValue(colour, out Color value))
    {
      value = new Color(colour.R, colour.G, colour.B, colour.Alpha);
      _colours.Add(colour, value);
    }

    return value;
  }

  public override void WriteLine(string message, int lineNum, int col, int width, Colour textColour)
  {
    WriteLine(message, lineNum, col, width, textColour, Colours.BLACK);
  }

  public override void WriteLine(string message, int lineNum, int col, int width, Colour textColour, Colour bgColour)
  {
    message = message.PadRight(width);
    Vector2 position = new(col * _fontWidth, lineNum * _fontHeight);

    DrawRectangle((int)position.X, (int)position.Y, width * _fontWidth, _fontHeight, ToRaylibColor(bgColour));
    DrawTextEx(_font, message, position, _fontHeight, 0, ToRaylibColor(textColour));
  }

  protected override void WriteSq(int row, int col, Sqr sq)
  {
    Vector2 position = new(col * (FontSize / 2), row * FontSize);
    DrawRectangle((int) position.X, (int) position.Y, FontSize / 2, FontSize, ToRaylibColor(sq.Bg));
    
    DrawTextEx(_font, sq.Ch.ToString(), position, FontSize, 0, ToRaylibColor(sq.Fg));
  }

  protected override void ClearScreen() => ClearBackground(Color.Black);
  protected override void Blit() => EndDrawing();

  public override void UpdateDisplay(GameState? gs)
  {
    BeginDrawing();
    ClearBackground(Color.Black);

    if (_longMessage is not null)
    {
      for (int j = 0; j < _longMessage.Count; j++)
      {
        WriteLine(_longMessage[j], j, 0, ScreenWidth, Colours.WHITE);
      }
      WritePopUp();
    }
    else
    {
      if (gs is not null && gs.Player is not null)
      {
        WriteSideBar(gs);
      }

      // Update main display
      int displayHeight = SqsOnScreen.GetLength(0);
      int displayWidth = SqsOnScreen.GetLength(1);
      for (int row = 0; row < displayHeight; row++)
      {
        for (int col = 0; col < displayWidth; col++)
        {
          WriteSq(row, col, SqsOnScreen[row, col]);
        }
      }

      WriteMessagesSection();

      if (MenuRows.Count > 0)
      {
        WriteDropDown();
      }

      WritePopUp();
      WriteConfirmation();
    }

    EndDrawing();
  }
}