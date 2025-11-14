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

using System.Numerics;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Yarl2;

class RaylibUserInterface : UserInterface
{
  Font _font;
  readonly int _fontWidth;
  readonly int _fontHeight;
  Dictionary<Colour, Color> _colours = [];
  Queue<GameEvent> EventQ { get; set; } = [];

  public RaylibUserInterface(string windowTitle, Options opt) : base()
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

  protected override GameEvent PollForEvent(bool pause = true)
  {
    if (pause)
      Delay(2);

    if (WindowShouldClose())
    {
      return new GameEvent(GameEventType.Quiting, '\0');
    }

    int ch = GetCharPressed();
    while (ch > 0)
    {
      if (ch >= 32 && ch <= 126)
      {
        EventQ.Enqueue(new(GameEventType.KeyInput, (char)ch));
      }

      ch = GetCharPressed();
    }

    if (IsKeyPressed(KeyboardKey.Escape))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, (char)Constants.ESC));
      Delay(50);
    }
    else if (IsKeyPressed(KeyboardKey.Enter))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, (char)13));
      Delay(50);
    }
    else if (IsKeyPressed(KeyboardKey.Backspace))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, (char)Constants.BACKSPACE));
      Delay(50);
    }
    else if (IsKeyPressed(KeyboardKey.Tab))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, (char)Constants.TAB));
      Delay(50);
    }
    else if (IsKeyPressed(KeyboardKey.Left))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'h'));
      Delay(50);
    }
    else if (IsKeyDown(KeyboardKey.Left))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'h'));
      Delay(5);
    }
    else if (IsKeyPressed(KeyboardKey.Right))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'l'));
      Delay(50);
    }
    else if (IsKeyDown(KeyboardKey.Right))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'l'));
      Delay(5);
    }
    else if (IsKeyPressed(KeyboardKey.Down))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'j'));
      Delay(50);
    }
    else if (IsKeyDown(KeyboardKey.Down))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'j'));
      Delay(5);
    }
    else if (IsKeyPressed(KeyboardKey.Up))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'k'));
      Delay(50);
    }
    else if (IsKeyDown(KeyboardKey.Up))
    {
      EventQ.Enqueue(new(GameEventType.KeyInput, 'k'));
      Delay(5);
    }
    
    if (EventQ.Count > 0)
    {
      return EventQ.Dequeue();
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

  public override void WriteSq(int row, int col, Sqr sq)
  {
    Vector2 position = new(col * (FontSize / 2), row * FontSize);
    DrawRectangle((int) position.X, (int) position.Y, FontSize / 2, FontSize, ToRaylibColor(sq.Bg));
    
    DrawTextEx(_font, sq.Ch.ToString(), position, FontSize, 0, ToRaylibColor(sq.Fg));
  }

  public override void ClearScreen() => ClearBackground(Color.Black);
  protected override void Blit() => EndDrawing();

  public override void UpdateDisplay(GameState? gs)
  {
    BeginDrawing();
    ClearBackground(Color.Black);

    if (_longMessage is not null)
    {
      WriteLongMessage(_longMessage);
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